"""Real PDF/image fixtures with fictitious source content; evaluation-only reportlab."""
import json,io,textwrap
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from PIL import Image,ImageDraw,ImageFont,ImageFilter
ROOT=Path(__file__).parent
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',34)

def scan(text,poor=False):
    im=Image.new('RGB',(1600,1800),'white');ImageDraw.Draw(im).multiline_text((70,100),text,font=font,fill='black',spacing=20)
    if poor:
        im=im.resize((640,720)).filter(ImageFilter.GaussianBlur(.6)).rotate(3,expand=True,fillcolor='white')
        shade=Image.new('RGBA',im.size,(0,0,0,0));d=ImageDraw.Draw(shade)
        for x in range(im.width):d.line((x,0,x,im.height),fill=(0,0,0,int(70*x/im.width)))
        im=Image.alpha_composite(im.convert('RGBA'),shade).convert('RGB')
    return im
for ident in ['table','columns','long-agreement']:
    case=json.loads((ROOT/'dataset'/f'{ident}.json').read_text());out=ROOT/'dataset'/f'{ident}.pdf';c=canvas.Canvas(str(out),pagesize=(612,792))
    for n,text in enumerate(case['pages'],1):
        c.setFont('Helvetica',9);c.drawString(42,766,'SYNTHETIC EVALUATION - NO REAL PERSONAL DATA')
        lines=text.splitlines()
        if ident=='long-agreement':lines=[wrapped for line in lines for wrapped in textwrap.wrap(line,width=80)]
        c.setFont('Helvetica',11)
        if ident=='table':
            for i,line in enumerate(lines[:2]):c.drawString(42,730-i*25,line)
            cols=[42,150,360,455,570]
            rows=[660,630,600,570,540]
            for x in cols:c.line(x,rows[-1],x,rows[0])
            for y in rows:c.line(cols[0],y,cols[-1],y)
            table=[['Date','Description','Debit','Credit'],['2026-09-01','Opening','-','RM1,000'],['2026-09-03','Rent','RM200','-'],['2026-09-04','Transfer','-','RM500']]
            for i,row in enumerate(table):
                for j,value in enumerate(row):c.drawString(cols[j]+6,rows[i]-20,value)
            c.drawString(42,510,'Closing balance: RM1,300')
        for i,line in enumerate([] if ident=='table' else lines):
            if ident=='columns' and i>1:
                col=(i-2)%2;row=(i-2)//2
                # Wrap long column lines within 245 points.
                words=line.split();wrapped=[];current=''
                for w in words:
                    if len(current+w)>34:wrapped.append(current);current=''
                    current+=w+' '
                wrapped.append(current)
                for j,part in enumerate(wrapped): c.drawString(42+col*275,670-row*95-j*16,part)
            else:c.drawString(42,730-i*(20 if ident=='long-agreement' else 25),line)
        c.setFont('Helvetica',9);c.drawString(42,30,f'Synthetic fixture / Page {n}');c.showPage()
    c.save();case['inputFile']=out.name;(ROOT/'dataset'/f'{ident}.json').write_text(json.dumps(case,indent=2))
case=json.loads((ROOT/'dataset'/'contract.json').read_text())
for ident,poor in [('scan',False),('poor-scan',True)]:
    image=scan(case['pages'][0],poor);image.save(ROOT/'dataset'/f'{ident}.png')
    c=canvas.Canvas(str(ROOT/'dataset'/f'{ident}.pdf'),pagesize=(612,792));c.drawImage(ImageReader(image),0,0,width=612,height=792);c.save()
    fixture={**case,'id':ident,'layout':ident,'inputFile':f'{ident}.pdf'}
    (ROOT/'dataset'/f'{ident}.json').write_text(json.dumps(fixture,indent=2))
    gold=json.loads((ROOT/'gold'/'contract.json').read_text());gold['id']=ident;(ROOT/'gold'/f'{ident}.json').write_text(json.dumps(gold,indent=2))
print('Created 5 PDF fixtures and 2 PNG source fixtures.')
