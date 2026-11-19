"""Bounded parser subprocess. Stdout is a private pipe consumed by the API, never a log."""
import json
import logging
import os
import sys
from .extraction import extract

MAX_OUTPUT_BYTES = 8 * 1024 * 1024

if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, stream=sys.stderr)
    # resource is Unix-only. Keep Linux Cloud Run CPU/memory limits intact;
    # Windows local validation must not fail before document parsing begins.
    if sys.platform == 'linux':
        import resource
        resource.setrlimit(resource.RLIMIT_CPU, (110, 110))
        # ONNX needs additional virtual address space; Cloud Run still
        # enforces the container's physical memory limit.
        limit = (4 if os.getenv('DOCUMENT_OCR_ENGINE', 'tesseract').lower() == 'rapidocr' else 1) * 1024**3
        resource.setrlimit(resource.RLIMIT_AS, (limit, limit))
    data = sys.stdin.buffer.read(10*1024*1024+1)
    if len(data)>10*1024*1024: sys.exit(1)
    try:
        pages = extract(data, sys.argv[1])
        output = json.dumps(
            [page.model_dump() for page in pages],
            ensure_ascii=False,
        ).encode('utf-8')

        if len(output) > MAX_OUTPUT_BYTES:
            sys.exit(1)

        sys.stdout.buffer.write(output)
    except Exception as exc:
        logging.error('extraction_failed exception_type=%s', type(exc).__name__)
        sys.exit(1)
