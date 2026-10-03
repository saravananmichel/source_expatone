"""Measured OCR CER/WER over generated synthetic scans; no model inference needed."""
import io
import json
import time
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter
from app.extraction import extract

TEXT='EMPLOYMENT CONTRACT\nEmployee: Alex Example\nEmployer: Synthetic Systems\nPosition: Software Engineer\nSalary: RM12,000 per month.\nStart date: 2027-01-01\nEither party must give sixty days written notice.\nThe employee must protect confidential business information.'

def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        new=[i]
        for j,y in enumerate(b,1):new.append(min(new[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=new
    return row[-1]

def main():
    image=Image.new('RGB',(1800,1300),'white')
    fonts=['/System/Library/Fonts/Supplemental/Arial.ttf','/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf']
    font=next((ImageFont.truetype(path,38) for path in fonts if Path(path).exists()),ImageFont.load_default(size=38))
    ImageDraw.Draw(image).multiline_text((60,60),TEXT,fill='black',font=font,spacing=20)
    cases=[('clean',image),('low_quality',image.resize((900,650)).filter(ImageFilter.GaussianBlur(.3))),('rotated',image.rotate(90,expand=True,fillcolor='white'))]
    results=[]
    expected=' '.join(TEXT.split())
    for name,scan in cases:
        data=io.BytesIO();scan.save(data,format='PNG');start=time.monotonic()
        pages=extract(data.getvalue(),'image/png');actual=' '.join(pages[0].text.split())
        results.append({'id':name,'characterErrorRate':distance(expected,actual)/len(expected),
            'wordErrorRate':distance(expected.split(),actual.split())/len(expected.split()),
            'rotationDegrees':pages[0].rotationDegrees,'seconds':round(time.monotonic()-start,2)})
    report={'synthetic':True,'language':'eng','results':results,'limitations':'Only three generated English scans; excludes handwriting, tables and multilingual quality.'}
    Path('evaluation/results.ocr.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':main()
