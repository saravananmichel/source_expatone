"""Target matching metrics are proxies; independent semantic human ratings stay null."""
import asyncio,argparse,json,os,time,sys
from pathlib import Path
import httpx
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from app.schemas import Page
from app.pipeline import reason,render
from app.extraction import extract
from evaluation.run import gemini
ROOT=Path(__file__).parent

def fraction(values,source):
    return sum(v.lower() in source.lower() for v in values)/len(values) if values else None

async def run(args):
    selected=sorted((ROOT/'dataset').glob('*.json'))
    if args.cases:selected=[p for p in selected if p.stem in args.cases.split(',')]
    results=[];model_label=os.getenv('OLLAMA_MODEL','local') if args.provider=='local' else os.getenv('GEMINI_EVALUATION_MODEL','gemini')
    suffix=model_label.replace(':','-').replace('/','-')+('-cpu' if os.getenv('OLLAMA_NUM_GPU')=='0' else '')
    import hashlib
    source_fingerprint=hashlib.sha256(b''.join(p.read_bytes() for p in sorted((ROOT.parent/'app').glob('*.py')))).hexdigest()
    for path in selected:
        case=json.loads(path.read_text());gold=json.loads((ROOT/'gold'/path.name).read_text());start=time.monotonic()
        result={'id':case['id'],'language':case['language'],'layout':case['layout'],'pageCount':len(case['pages'])}
        try:
            started=time.monotonic()
            pages=extract((ROOT/'dataset'/case['inputFile']).read_bytes(),'application/pdf') if case.get('inputFile') else [Page(page=i+1,text=p,width=612,height=792,extraction='synthetic_text',blocks=[]) for i,p in enumerate(case['pages'])]
            result['extractionMs']=round((time.monotonic()-started)*1000)
            semantic,model=await (reason(pages) if args.provider=='local' else gemini(pages))
            if args.provider=='gemini':
                # Same support review policy for external synthetic reference outputs.
                from app.postprocessing import enrich
                from app.reasoning import document_reasoning
                from app.support import review_support
                semantic=enrich(semantic);await document_reasoning(semantic);semantic._quality=await review_support(semantic)
            output=render(semantic,pages,model)
            if args.provider=='gemini':output['provider']='Gemini extraction with local reasoning/support review'
            (ROOT/'predictions'/f'{case["id"]}.{suffix}.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))
            values=' '.join(s.originalValue for s in semantic.statements)
            prose=' '.join(s.text for s in semantic.statements if s.supportStatus=='SUPPORTED')
            result.update(valid=True,model=model,classificationCorrect=semantic.documentCategory==gold['category'],
                exactFieldTargetRecall=fraction(gold['expectedValues'],values),dateTargetRecall=fraction(gold['dates'],values),moneyTargetRecall=fraction(gold['money'],values),
                clauseKeywordRecall=fraction(gold['clauses'],prose),findingKeywordRecall=fraction(gold['findings'],' '.join(s.text for s in semantic.statements if s.kind in ('finding','warning','relationship') and s.supportStatus=='SUPPORTED')),
                actionKeywordRecall=fraction(gold['actions'],' '.join(output['requiredActions'])),
                summaryTargetCoverage=fraction(gold['summaryCoverageTargets'],output['summary']),
                forbiddenPhraseCount=sum(v.lower() in prose.lower() for v in gold['forbiddenClaims']),
                quoteMatchRate=1.0,modelJudgeUnsupportedFraction=sum(s.supportStatus=='UNSUPPORTED' for s in semantic.statements)/len(semantic.statements),
                modelJudgeSupportedFraction=sum(s.supportStatus=='SUPPORTED' for s in semantic.statements)/len(semantic.statements),
                independentEvidenceSupportRate=None,semanticHallucinationRate=None,humanUsefulness=None,
                diagnostics=output['qualityDiagnostics'])
            async with httpx.AsyncClient(timeout=3) as client:
                ps=await client.get(os.getenv('OLLAMA_URL','http://127.0.0.1:11434')+'/api/ps');ps.raise_for_status()
                result['residentModels']=[{k:m.get(k) for k in ('name','size','size_vram','context_length')} for m in ps.json()['models']]
        except Exception as exc:
            result.update(valid=False,errorType=type(exc).__name__,failureCategory=exc.args[0] if exc.args and str(exc.args[0]).startswith('invalid_model_output:') else 'processing_failed')
        result['seconds']=round(time.monotonic()-start,2);results.append(result)
        report={'provider':args.provider,'model':model_label,'pipelineSourceHash':source_fingerprint,'executionConfig':{'gpu':os.getenv('OLLAMA_NUM_GPU','automatic'),'explanationLanguage':os.getenv('EXPLANATION_LANGUAGE','English'),'ocrLanguages':os.getenv('OCR_LANGUAGES','eng'),'supportModel':os.getenv('SUPPORT_MODEL',os.getenv('OLLAMA_MODEL'))},'cases':len(results),'valid':sum(r['valid'] for r in results),
            'classificationAccuracy':sum(r.get('classificationCorrect',False) for r in results)/len(results),
            'meanExactFieldTargetRecall':sum(r.get('exactFieldTargetRecall',0) or 0 for r in results)/len(results),
            'independentSemanticAccuracy':None,'confidenceCalibrated':False,
            'limitations':'Partial synthetic gold. Keyword/target scores and same-family model-judge verdicts are proxies, not scientific entailment or human accuracy. Missing measurements are null.', 'results':results}
        (ROOT/'reports'/f'quality.{suffix}.json').write_text(json.dumps(report,indent=2))
        print(json.dumps({k:result.get(k) for k in ('id','valid','classificationCorrect','exactFieldTargetRecall','modelJudgeSupportedFraction','seconds','errorType')}),flush=True)
    return report

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--provider',choices=['local','gemini'],default='local');p.add_argument('--cases')
    asyncio.run(run(p.parse_args()))
