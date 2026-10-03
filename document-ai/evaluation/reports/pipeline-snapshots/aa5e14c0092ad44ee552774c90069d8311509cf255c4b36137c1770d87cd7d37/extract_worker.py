"""Bounded parser subprocess. Stdout is a private pipe consumed by the API, never a log."""
import json
import sys
import resource
from .extraction import extract

if __name__ == '__main__':
    resource.setrlimit(resource.RLIMIT_CPU, (110, 110))
    if sys.platform == 'linux':
        resource.setrlimit(resource.RLIMIT_AS, (1024**3,1024**3))
    data = sys.stdin.buffer.read(10*1024*1024+1)
    if len(data)>10*1024*1024: sys.exit(1)
    try:
        pages=extract(data,sys.argv[1])
        sys.stdout.write(json.dumps([page.model_dump() for page in pages],ensure_ascii=False))
    except Exception:
        sys.exit(1)
