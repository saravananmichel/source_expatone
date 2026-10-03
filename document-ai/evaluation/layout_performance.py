"""Measured extraction at 1/5/10/20/40 pages. Inference is separately benchmarked."""
import json,time,sys,resource
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tests'))
from app.extraction import extract
from pdf_helpers import native_pdf
ROOT=Path(__file__).parent
text=json.loads((ROOT/'dataset'/'contract.json').read_text())['pages'][0]
results=[]
for n in (1,5,10,20,40):
    data=native_pdf([text]*n);started=time.monotonic();pages=extract(data,'application/pdf')
    results.append({'pages':n,'bytes':len(data),'extractionMs':round((time.monotonic()-started)*1000),
        'processedPages':len(pages),'maxRssBytes':resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,
        'uploadMs':None,'preprocessingMs':None,'inferenceMs':None,'reasoningMs':None,'validationMs':None,'databaseMs':None,'totalE2eMs':None})
report={'results':results,'limitations':'Repeated synthetic native pages measure extraction only. No GPU inference, real scans or full workflow latency in this report.'}
(ROOT/'reports'/'extraction-performance.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
