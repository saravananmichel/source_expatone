"""Internal real inference verification using only synthetic input."""
import os
import time
import httpx

"""Generate synthetic native PDFs without embedding real user information."""
def native_pdf(texts):
    objects = [b'',b'']
    kids=[]
    for text in texts:
        page_id=len(objects)+1; content_id=page_id+1; kids.append(f'{page_id} 0 R')
        lines=text.splitlines()
        def escape(value): return value.replace('\\','\\\\').replace('(','\\(').replace(')','\\)')
        content=('BT /F1 14 Tf 40 750 Td '+' '.join(f'({escape(line)}) Tj 0 -24 Td' for line in lines)+' ET').encode()
        objects.extend([
            f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 800] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {content_id} 0 R >>'.encode(),
            f'<< /Length {len(content)} >>\nstream\n'.encode()+content+b'\nendstream'])
    objects[0]=b'<< /Type /Catalog /Pages 2 0 R >>'
    objects[1]=f'<< /Type /Pages /Kids [{" ".join(kids)}] /Count {len(texts)} >>'.encode()
    output=b'%PDF-1.4\n';offsets=[0]
    for i,obj in enumerate(objects,1):
        offsets.append(len(output));output+=f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n'
    xref=len(output)
    output+=f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode()
    output+=b''.join(f'{offset:010} 00000 n \n'.encode() for offset in offsets[1:])
    output+=f'trailer << /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF'.encode()
    return output


def main():
    text = 'PASSPORT\nName: Morgan Example\nPassport number: TEST000001\nNationality: Exampleland\nDate of expiry: 2034-01-01'
    started = time.monotonic()
    with httpx.Client(timeout=httpx.Timeout(950, connect=5)) as client:
        response = client.post('http://127.0.0.1:8090/analyze',
            headers={'X-Service-Key': os.environ['DOCUMENT_AI_SERVICE_KEY']},
            files={'file': ('synthetic.pdf', native_pdf([text]), 'application/pdf')})
        if response.status_code != 200:
            raise SystemExit(f'Analysis smoke failed: HTTP {response.status_code}')
        result = response.json()
        assert result['provider'] == 'Local'
        assert result['modelVersion'].startswith('qwen3:4b@')
        assert result['evidence'] and result['statements'] and result['semanticDocument']['pages']
        print({'status': 'passed', 'evidenceCount': len(result['evidence']),
               'statementCount': len(result['statements']), 'seconds': round(time.monotonic()-started, 2)})


if __name__ == '__main__': main()
