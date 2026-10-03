"""Measured original-script OCR on safe generated samples, including degraded English."""
import sys,os,json,io,time
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from app.extraction import extract
from evaluation.ocr import distance
ROOT=Path(__file__).parent
results=[]
for ident,lang,font_path in [('contract','eng','Arial.ttf'),('malay','msa','Arial.ttf'),('tamil','tam','Tamil Sangam MN.ttc'),('chinese','chi_sim','Arial Unicode.ttf')]:
    case=json.loads((ROOT/'dataset'/f'{ident}.json').read_text());text=case['pages'][0]
    font=ImageFont.truetype('/System/Library/Fonts/Supplemental/'+font_path,40)
    image=Image.new('RGB',(2000,1400),'white');ImageDraw.Draw(image).multiline_text((65,70),text,fill='black',font=font,spacing=22)
    image.save(ROOT/'dataset'/f'{ident}.ocr.png')
    os.environ['OCR_LANGUAGES']=lang
    for quality,scan in [('clean',image),('low-dpi',image.resize((1000,700)))]:
        b=io.BytesIO();scan.save(b,format='PNG');start=time.monotonic();pages=extract(b.getvalue(),'image/png')
        expected=' '.join(text.split());actual=' '.join(pages[0].text.split())
        results.append({'id':ident,'language':lang,'quality':quality,'CER':distance(''.join(expected.split()),''.join(actual.split()))/len(''.join(expected.split())),
            'WER':distance(expected.split(),actual.split())/len(expected.split()) if lang!='chi_sim' else None,'ocrConfidence':pages[0].extractionConfidence,
            'seconds':round(time.monotonic()-start,2),'fieldTargetRecall':sum(v in actual for v in case['values'])/len(case['values'])})
(ROOT/'reports'/'multilingual-ocr.json').write_text(json.dumps({'synthetic':True,'results':results,'limitations':'CER excludes whitespace; WER is whitespace-tokenized and is not defined for Chinese without a segmentation standard. Printed synthetic samples only; no independent human ratings.'},indent=2))
print(json.dumps(results))
