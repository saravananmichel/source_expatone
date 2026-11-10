import io
import math
import os
import re
from PIL import Image, ImageOps
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

def ocr(image, page_number):
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
    # Short pages often cannot support OSD. Compare alternate rotations only when the
    # recognized text is uncertain, avoiding repeated OCR for clear upright pages.
    best_score=quality(data)
    if best_score<85:
        original=image
        original_rotation=rotation
        for angle in (90,180,270):
            candidate=original.rotate(-angle,expand=True,fillcolor='white')
            try:
                candidate_data=pytesseract.image_to_data(candidate,lang=os.getenv('OCR_LANGUAGES','eng'),
                    config='--psm 3',output_type=pytesseract.Output.DICT,timeout=30)
                candidate_score=quality(candidate_data)
                if candidate_score>best_score+5:
                    image=candidate;data=candidate_data;best_score=candidate_score
                    rotation=(original_rotation+angle)%360
            except (pytesseract.TesseractError,RuntimeError):
                continue
    lines = {}
    for i, value in enumerate(data['text']):
        if not value.strip(): continue
        key = (data['block_num'][i], data['par_num'][i], data['line_num'][i])
        lines.setdefault(key, []).append(i)
    blocks = []
    for indices in lines.values():
        text = ' '.join(data['text'][i] for i in indices)
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
    return bool(re.search(r'([A-Za-z]{3,12})\\1{5,}', normalized, flags=re.IGNORECASE))


def suppress_repetitive_ocr_noise(text: str) -> str:
    """Remove only long repeated alphabetic runs, preserving nearby fields."""
    return re.sub(
        r'([A-Za-z]{3,12})\\1{5,}',
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
