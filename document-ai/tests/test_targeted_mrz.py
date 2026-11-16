from PIL import Image
from app.schemas import Block
from app.extraction import _rapid_mrz_blocks
from app.passport_mrz import check_digit


def labels():
    return [Block(text=t, boundingBox=[10, 10, 200, 30], kind="heading")
            for t in ("GivenName(s)", "DateofExpiry")]


def pair():
    first = "P<XXXEXAMPLE<<SAMPLE".ljust(44, "<")
    number = "A1234567<"
    birth, expiry, optional = "800201", "300201", "<" * 14
    second = (number + check_digit(number) + "XXX" + birth + check_digit(birth)
              + "M" + expiry + check_digit(expiry) + optional + check_digit(optional))
    return first + "\n" + second + check_digit(second[:10] + second[13:20] + second[21:43])


def test_valid_targeted_mrz(monkeypatch):
    def read(crop, **kwargs):
        assert crop.size == (900, 180)
        assert kwargs["timeout"] == 5
        return pair()
    monkeypatch.setattr("app.extraction.pytesseract.image_to_string", read)
    blocks = _rapid_mrz_blocks(Image.new("RGB", (900, 600)), labels(), 1)
    assert len(blocks) == 1
    assert blocks[0].text == pair()
    assert blocks[0].boundingBox == [0, 420, 900, 600]


def test_invalid_checksum_rejected(monkeypatch):
    raw = pair()
    invalid = raw[:-1] + str((int(raw[-1]) + 1) % 10)
    monkeypatch.setattr("app.extraction.pytesseract.image_to_string", lambda *a, **k: invalid)
    assert not _rapid_mrz_blocks(Image.new("RGB", (900, 600)), labels(), 1)


def test_second_page_never_retried(monkeypatch):
    def unexpected(*args, **kwargs):
        raise AssertionError("Unexpected OCR call")
    monkeypatch.setattr("app.extraction.pytesseract.image_to_string", unexpected)
    assert not _rapid_mrz_blocks(Image.new("RGB", (900, 600)), labels(), 2)


def test_timeout_returns_no_candidate(monkeypatch):
    def timeout(*args, **kwargs):
        raise RuntimeError("timeout")
    monkeypatch.setattr("app.extraction.pytesseract.image_to_string", timeout)
    assert not _rapid_mrz_blocks(Image.new("RGB", (900, 600)), labels(), 1)
