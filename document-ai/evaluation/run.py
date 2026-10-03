"""Evaluate ONLY the checked-in synthetic corpus; never reads a user's documents."""
import argparse
import asyncio
import json
import os
import time
from pathlib import Path
import httpx
from app.schemas import Page, SemanticDocument
from app.pipeline import reason, validate_grounding, render, PROMPT

async def gemini(pages):
    # Optional external comparison explicitly requested in the feature requirements.
    model=os.environ['GEMINI_EVALUATION_MODEL']
    async with httpx.AsyncClient(timeout=180) as client:
        response=await client.post(f'https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent',
            headers={'x-goog-api-key':os.environ['GEMINI_API_KEY']}, json={
                'contents':[{'parts':[{'text':PROMPT+'\nSchema: '+json.dumps(SemanticDocument.model_json_schema())+
                    '\nSynthetic pages: '+json.dumps([p.model_dump() for p in pages])}]}],
                'generationConfig':{'responseMimeType':'application/json','temperature':0}})
        response.raise_for_status()
        parsed=SemanticDocument.model_validate_json(response.json()['candidates'][0]['content']['parts'][0]['text'])
        return validate_grounding(parsed,pages),model

async def main(args):
    dataset=json.loads(Path(__file__).with_name('dataset.json').read_text())
    results=[]
    selected=[case for case in dataset if not args.case or case['id']==args.case]
    for case in selected[:args.limit]:
        start=time.monotonic()
        pages=[Page(page=1,text=case['text'],width=600,height=800,extraction='synthetic_text',blocks=[])]
        try:
            semantic,model=await (reason(pages) if args.provider=='local' else gemini(pages))
            output=render(semantic,pages,model)
            values=' '.join(s.originalValue or '' for s in semantic.statements)
            text=' '.join(s.text for s in semantic.statements).lower()
            results.append({'id':case['id'],'model':model,'valid':True,
                'classificationCorrect':semantic.documentCategory==case['category'],
                'fieldRecall':sum(v in values for v in case['values'])/len(case['values']),
                'clauseRecall':sum(v in text for v in case['clauses'])/len(case['clauses']) if case['clauses'] else None,
                'forbiddenClaims':sum(v.lower() in text for v in case['forbidden']),
                'evidenceQuoteMatch':1.0,'seconds':round(time.monotonic()-start,2)})
        except Exception:
            results.append({'id':case['id'],'valid':False,'seconds':round(time.monotonic()-start,2)})
        print(json.dumps(results[-1]),flush=True) # Aggregate metrics only; never source/model contents.
    valid=[r for r in results if r['valid']]
    report={'provider':args.provider,'syntheticCases':len(results),'validResults':len(valid),
        'classificationAccuracy':sum(r.get('classificationCorrect',False) for r in results)/len(results),
        'meanFieldRecall':sum(r.get('fieldRecall',0) for r in results)/len(results),
        'semanticHallucinationRate':None,'humanUsefulness':None,
        'analyzerVersion':'document-engine-v1','promptVersion':'grounding-v2','limitations':'Exact quote matching does not establish semantic entailment; human ratings and real scan benchmarks required.',
        'results':results}
    Path(args.output).write_text(json.dumps(report,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--provider',choices=['local','gemini'],default='local')
    parser.add_argument('--limit',type=int,default=8);parser.add_argument('--case');parser.add_argument('--output',default='evaluation/results.local.json')
    asyncio.run(main(parser.parse_args()))
