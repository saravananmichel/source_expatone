import io
import math
import os
import re
from PIL import Image, ImageOps, ImageFilter
from pypdf import PdfReader
import pypdfium2 as pdfium
import pytesseract
from .schemas import Page, Block

MAX_PAGES = 40
MAX_TEXT = 100_000
Image.MAX_IMAGE_PIXELS = 20_000_000

def block_kind(text):
    if re.match(r'^\s*[-•\d]+[.)\s]', text): return 'list'
    if ':' in text and len(text) < 250: return 'key_value'
    if len(text) < 100 and (text.isupper() or text.istitle()): return 'heading'
    return 'paragraph'

def _rapid_horizontal_fraction(result):
    horizontal = total = 0
    for polygon, text, score in result:
        if not math.isfinite(float(score)) or float(score) < 0.35:
            continue
        xs, ys = zip(*polygon)
        width, height = max(xs)-min(xs), max(ys)-min(ys)
        if min(width, height) <= 0 or len(str(text)) < 4:
            continue
        weight = len(str(text))
        total += weight
        if width >= height:
            horizontal += weight
    return horizontal / max(1, total)


def _rapid_orientation_rank(result):
    usable = [(str(t), float(s)) for _, t, s in result
              if math.isfinite(float(s)) and float(s) >= 0.35]
    text = ' '.join(t.upper() for t, _ in usable)
    labels = sum(bool(re.search(pattern, text)) for pattern in (
        r'PASSPORT', r'SURNAME', r'GIVEN\s*NAME', r'DATE\s*OF\s*BIRTH',
        r'EXPIRY', r'NATIONALITY'))
    quality = sum(len(t)*s for t, s in usable) / max(1, sum(len(t) for t, _ in usable))
    return (_rapid_horizontal_fraction(result), labels, quality)


def _rapid_reading_order(blocks):
    rows = []
    for block in sorted(blocks, key=lambda b: (b.boundingBox[1], b.boundingBox[0])):
        x0, y0, x1, y1 = block.boundingBox
        center = (y0+y1)/2
        if rows and abs(center-rows[-1][0]) <= min(y1-y0, rows[-1][1])*0.5:
            rows[-1][2].append(block)
        else:
            rows.append((center, y1-y0, [block]))
    return [b for _, _, row in rows for b in sorted(row, key=lambda b: b.boundingBox[0])]


def _rapid_mrz_blocks(image, blocks, page_number):
    """Bounded local MRZ recovery; retain only checksum-valid TD3 pairs."""
    from .passport_mrz import mrz_fields
    if page_number != 1 or image.width <= image.height:
        return []
    text = '\n'.join(b.text for b in blocks)
    if not re.search(r'given\s*name', text, re.I) or not re.search(r'date\s*of\s*expiry', text, re.I):
        return []
    if mrz_fields([Page(page=page_number, text=text, width=image.width,
                        height=image.height, extraction='ocr', blocks=blocks)]):
        return []
    y = int(image.height * 0.70)
    crop = ImageOps.autocontrast(image.crop((0, y, image.width, image.height)).convert('L'))
    try:
        raw = pytesseract.image_to_string(crop, lang='eng',
            config='--psm 6 -c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<',
            timeout=5)
    except (pytesseract.TesseractError, RuntimeError):
        return []
    lines = [line.strip() for line in raw.splitlines() if line.strip()]
    for first, second in zip(lines, lines[1:]):
        pair = first + '\n' + second
        if mrz_fields([Page(page=page_number, text=pair, width=image.width,
                            height=image.height, extraction='ocr', blocks=[])]):
            # Region-level evidence, not fabricated character coordinates.
            return [Block(text=pair, boundingBox=[0, y, image.width, image.height],
                          kind='paragraph')]
    return []


def rapid_ocr(image, page_number):
    # Lazy initialization keeps the existing Tesseract path independent.
    import numpy as np
    import logging
    import time
    started = time.monotonic()
    logging.getLogger(__name__).info('rapidocr stage=initialize')
    engine = _rapid_engine()
    logging.getLogger(__name__).info('rapidocr stage=inference init_ms=%d', int((time.monotonic()-started)*1000))
    image = ImageOps.exif_transpose(image).convert('RGB')
    image.thumbnail((2400, 3400))
    rotation = 0
    # OSD detects full-page orientation, unlike RapidOCR's crop classifier.
    try:
        orientation = pytesseract.image_to_osd(
            image, output_type=pytesseract.Output.DICT, timeout=5)
        if orientation.get('orientation_conf', 0) >= 5:
            angle = int(orientation.get('rotate', 0))
            if angle in (90, 180, 270):
                image = image.rotate(-angle, expand=True, fillcolor='white')
                rotation = angle
    except (pytesseract.TesseractError, RuntimeError):
        pass
    def infer(candidate):
        result, _ = engine(np.asarray(candidate)[:, :, ::-1].copy())
        return result or []
    result = infer(image)
    # When OSD cannot orient a sparse scan, tall text polygons identify
    # sideways pages. Compare both quarter turns without trusting text length.
    if _rapid_horizontal_fraction(result) < 0.4 and len(result) >= 3:
        original = image
        base_rotation = rotation
        best = _rapid_orientation_rank(result)
        for angle in (90, 270):
            candidate = original.rotate(-angle, expand=True, fillcolor='white')
            candidate_result = infer(candidate)
            rank = _rapid_orientation_rank(candidate_result)
            if rank[0] >= 0.65 and rank > best:
                image, result, best = candidate, candidate_result, rank
                rotation = (base_rotation + angle) % 360
                # Skip the opposite quarter-turn only with strong readable label evidence.
                if rank[0] >= 0.95 and rank[1] >= 4 and rank[2] >= 0.90:
                    break
    logging.getLogger(__name__).info('rapidocr orientation_degrees=%d', rotation)
    blocks = []
    weighted = total = 0
    for polygon, value, score in result or []:
        text = suppress_repetitive_ocr_noise(str(value)).strip()
        if not text:
            continue
        confidence = float(score)
        if not math.isfinite(confidence) or confidence < 0.35:
            continue
        xs, ys = zip(*polygon)
        box = [max(0, float(min(xs))), max(0, float(min(ys))),
               min(image.width, float(max(xs))), min(image.height, float(max(ys)))]
        if box[2] <= box[0] or box[3] <= box[1]:
            continue
        blocks.append(Block(text=text, boundingBox=box, kind=block_kind(text)))
        total += len(text)
        weighted += len(text) * confidence
    if not blocks:
        raise ValueError('no_readable_text')
    # Group nearby baselines, then order each row left to right.
    blocks = _rapid_reading_order(blocks)
    blocks.extend(_rapid_mrz_blocks(image, blocks, page_number))
    return Page(page=page_number, text='\n'.join(b.text for b in blocks),
        width=image.width, height=image.height, extraction='ocr',
        rotationDegrees=rotation, coordinateSpace='preprocessed_image_pixels',
        extractionConfidence=max(0, min(1, weighted / max(1, total))), blocks=blocks)


from functools import lru_cache

@lru_cache(maxsize=1)
def _rapid_engine():
    from rapidocr_onnxruntime import RapidOCR
    return RapidOCR(intra_op_num_threads=2, inter_op_num_threads=1)


def ocr(image, page_number):
    import logging
    import time
    start = time.monotonic()
    engine = os.getenv('DOCUMENT_OCR_ENGINE', 'tesseract').lower()
    try:
        if engine == 'rapidocr':
            try:
                page = rapid_ocr(image, page_number)
                if page.extractionConfidence >= 0.65 or os.getenv('RAPIDOCR_FALLBACK', '0') != '1':
                    return page
            except Exception as exc:
                logging.getLogger(__name__).warning('rapidocr_failed type=%s', type(exc).__name__)
                if os.getenv('RAPIDOCR_FALLBACK', '0') != '1':
                    raise
            engine = 'tesseract_fallback'
        return _tesseract_ocr(image, page_number)
    finally:
        logging.getLogger(__name__).info('ocr_engine=%s elapsed_ms=%d', engine,
            int((time.monotonic() - start) * 1000))


def _tesseract_ocr(image, page_number):
    image = ImageOps.exif_transpose(image).convert('RGB')
    image.thumbnail((2400, 3400))
    rotation=0
    orientation_confidence=0
    try:
        orientation=pytesseract.image_to_osd(image,output_type=pytesseract.Output.DICT,timeout=10)
        orientation_confidence=orientation.get('orientation_conf',0)
        if orientation_confidence>=5:
            rotation=int(orientation.get('rotate',0))
            if rotation: image=image.rotate(-rotation,expand=True,fillcolor='white')
    except (pytesseract.TesseractError, RuntimeError):
        pass # Short pages may not have enough text for reliable orientation detection.
    data = pytesseract.image_to_data(image, lang=os.getenv('OCR_LANGUAGES', 'eng'),
        config='--psm 3', output_type=pytesseract.Output.DICT, timeout=40)
    def quality(result):
        words=[(text,float(result['conf'][i])) for i,text in enumerate(result['text']) if text.strip()]
        total=sum(len(text) for text,_ in words)
        return sum(len(text)*max(0,confidence) for text,confidence in words)/max(1,total)
    best_score = quality(data)
    # Retry low-confidence scans with conservative cleanup. Preserve the original
    # when preprocessing does not improve OCR confidence; never mutate native PDF text.
    # Median filtering removes isolated pixel speckles; autocontrast improves faint
    # text on uneven backgrounds. Keep dimensions unchanged for OCR bounding boxes.
    if best_score < 85:
        try:
            cleaned = ImageOps.autocontrast(
                image.convert('L').filter(ImageFilter.MedianFilter(size=3)),
                cutoff=1,
            )
            cleaned_data = pytesseract.image_to_data(
                cleaned, lang=os.getenv('OCR_LANGUAGES', 'eng'),
                config='--psm 3', output_type=pytesseract.Output.DICT, timeout=40,
            )
            cleaned_score = quality(cleaned_data)
            if cleaned_score > best_score + 2:
                image = cleaned
                data = cleaned_data
                best_score = cleaned_score
        except (pytesseract.TesseractError, RuntimeError, ValueError):
            pass
    # Compare each orientation against the same original image. Never rotate
    # later candidates relative to an already selected candidate.
    if best_score < 85:
        original = image
        original_rotation = rotation
        def passport_evidence(result):
            joined = ' '.join(str(t).upper() for t in result['text'] if str(t).strip())
            return sum(bool(re.search(pattern, joined)) for pattern in (
                r'\bPASSPORT\b', r'\bSURNAME\b', r'\bGIVEN\b',
                r'\bBIRTH\b', r'\bEXPIRY\b', r'\bNATIONALITY\b'))
        best_rank = (passport_evidence(data), best_score)
        for angle in (90, 180, 270):
            candidate = original.rotate(-angle, expand=True, fillcolor='white')
            try:
                candidate_data = pytesseract.image_to_data(
                    candidate, lang=os.getenv('OCR_LANGUAGES', 'eng'),
                    config='--psm 3', output_type=pytesseract.Output.DICT, timeout=30)
                candidate_score = quality(candidate_data)
                candidate_rank = (passport_evidence(candidate_data), candidate_score)
                if (candidate_rank[0] >= best_rank[0] + 2
                        or (candidate_rank[0] == best_rank[0]
                            and candidate_score > best_rank[1] + 5)):
                    image, data, best_score = candidate, candidate_data, candidate_score
                    rotation = (original_rotation + angle) % 360
                    best_rank = candidate_rank
            except (pytesseract.TesseractError, RuntimeError):
                continue
    lines = {}
    for i, value in enumerate(data['text']):
        if not value.strip(): continue
        key = (data['block_num'][i], data['par_num'][i], data['line_num'][i])
        lines.setdefault(key, []).append(i)
    blocks = []
    for indices in lines.values():
        text = ' '.join(data['text'][i] for i in indices)
        text = suppress_repetitive_ocr_noise(text).strip()
        if not text:
            continue
        x = min(data['left'][i] for i in indices)
        y = min(data['top'][i] for i in indices)
        right = max(data['left'][i] + data['width'][i] for i in indices)
        bottom = max(data['top'][i] + data['height'][i] for i in indices)
        blocks.append(Block(text=text, boundingBox=[x,y,right,bottom], kind=block_kind(text)))
    return Page(page=page_number, text='\n'.join(b.text for b in blocks), width=image.width,
        height=image.height, extraction='ocr', rotationDegrees=rotation, coordinateSpace='preprocessed_image_pixels', extractionConfidence=max(0,min(1,best_score/100)), blocks=blocks)

def repetitive_ocr_noise(text: str) -> bool:
    """Identify implausibly repeated OCR tokens without rejecting valid codes.

    Repetition alone is not enough: require a long contiguous repeated
    alphabetic sequence, such as a corrupted watermark or logo extraction.
    """
    normalized = ''.join(text.split())
    if len(normalized) < 36:
        return False
    return bool(re.search(r'([A-Za-z]{3,12})\1{5,}', normalized, flags=re.IGNORECASE))


def suppress_repetitive_ocr_noise(text: str) -> str:
    """Remove only long repeated alphabetic runs, preserving nearby fields."""
    return re.sub(
        r'([A-Za-z]{3,12})\1{5,}',
        '',
        text,
        flags=re.IGNORECASE,
    )


def corrupted_text_layer(text: str) -> bool:
    """Detect strongly garbled text layers before trusting their length.

    This deliberately requires multiple independent signals to avoid rejecting
    valid short codes, machine-readable zones and non-Latin documents.
    """
    normalized = ' '.join(text.split())
    if len(normalized) < 40:
        return False
    tokens = normalized.split()
    cjk = sum(
        '\u4e00' <= ch <= '\u9fff'
        or '\u3040' <= ch <= '\u30ff'
        or '\uac00' <= ch <= '\ud7af'
        for ch in normalized
    )
    if cjk >= 12:
        return False
    alphabetic = sum(ch.isalpha() for ch in normalized)
    punctuation = sum(not ch.isalnum() and not ch.isspace() for ch in normalized)
    short_tokens = sum(len(token) <= 2 for token in tokens)
    suspicious = sum(bool(re.search(r'[<>|;#~]', token)) for token in tokens)
    # Corrupted embedded fonts can retain long English boilerplate while
    # surrounding labels become punctuation-heavy nonsense.
    if repetitive_ocr_noise(normalized):
        return True
    if len(tokens) >= 8 and suspicious / len(tokens) >= 0.22:
        return True
    return (
        len(tokens) >= 12
        and punctuation / len(normalized) > 0.20
        and short_tokens / len(tokens) > 0.55
        and alphabetic / len(normalized) < 0.42
    )


def prefer_native_pdf_text(text: str, has_images: bool) -> bool:
    """Keep a readable PDF text layer even when logos, photos or QR codes exist.

    A previous image-presence heuristic sent short but perfectly searchable
    one-page passes through OCR. A scanned page with only a small searchable
    header should still use OCR.
    """
    normalized = ' '.join(text.split())
    if len(normalized) < 40 or corrupted_text_layer(text):
        return False
    if not has_images:
        return True
    # Substantial text layers are generally more faithful than raster OCR.
    if len(normalized) >= 350:
        return True
    # Country-neutral signal: multiple readable lines/words from a text layer.
    # Do not infer that a portrait, seal, stamp or QR code makes the PDF a scan.
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    words = re.findall(r'\w+', normalized, flags=re.UNICODE)
    # CJK scripts may not separate words with whitespace.
    cjk_chars = sum(
        '\u4e00' <= char <= '\u9fff'
        or '\u3040' <= char <= '\u30ff'
        or '\uac00' <= char <= '\ud7af'
        for char in normalized
    )
    readable = len(words) >= 16 or cjk_chars >= 30
    return len(normalized) >= 120 and readable and (
        len(lines) >= 3 or len(normalized) >= 180 or cjk_chars >= 30
    )

def extract(data: bytes, content_type: str):
    if content_type == 'application/pdf':
        if not data.startswith(b'%PDF-'): raise ValueError('invalid_file')
        reader = PdfReader(io.BytesIO(data), strict=True)
        if reader.is_encrypted: raise ValueError('encrypted_pdf')
        if not 0 < len(reader.pages) <= MAX_PAGES: raise ValueError('page_limit')
        pages = []
        with pdfium.PdfDocument(data) as rendered:
            for i, native in enumerate(reader.pages):
                page = rendered[i]
                try:
                    # PDFium supplies native coordinates and reading-order text rectangles.
                    textpage = page.get_textpage()
                    try:
                        text = textpage.get_text_range()
                        # Embedded images (photos, logos, QR codes) do not
                        # imply the text layer is a scan. Prefer searchable
                        # text for structured documents from any country.
                        if prefer_native_pdf_text(text, bool(native.images)):
                            blocks = []
                            for j in range(textpage.count_rects()):
                                x0,y0,x1,y1 = textpage.get_rect(j)
                                value = textpage.get_text_bounded(x0,y0,x1,y1).strip()
                                if value:
                                    box = [x0,page.get_height()-y1,x1,page.get_height()-y0]
                                    blocks.append(Block(text=value, boundingBox=box, kind=block_kind(value)))
                            pages.append(Page(page=i+1,text=text,width=page.get_width(),height=page.get_height(),
                                extraction='native',blocks=blocks))
                        else:
                            render_width = math.ceil(page.get_width() * 2)
                            render_height = math.ceil(page.get_height() * 2)
                            if render_width * render_height > 20_000_000:
                                raise ValueError('page_dimensions')
                            bitmap = page.render(scale=2)
                            try: pages.append(ocr(bitmap.to_pil(),i+1))
                            finally: bitmap.close()
                    finally: textpage.close()
                finally: page.close()
    else:
        signatures = {'image/png': b'\x89PNG\r\n\x1a\n', 'image/jpeg': b'\xff\xd8\xff'}
        if content_type not in signatures or not data.startswith(signatures[content_type]):
            raise ValueError('invalid_file')
        with Image.open(io.BytesIO(data)) as image:
            if image.width*image.height>20_000_000: raise ValueError('image_dimensions')
            pages = [ocr(image,1)]
    if sum(len(page.text) for page in pages) > MAX_TEXT:
        raise ValueError('text_limit')

    if not any(page.text.strip() for page in pages):
        raise ValueError('no_readable_text')

    return pages
