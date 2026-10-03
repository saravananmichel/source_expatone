"""Hand-authored entailment challenge; controlled synthetic claims, not corpus accuracy."""
import asyncio,json,sys,os
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from app.schemas import SemanticDocument,Statement,Evidence
from app.support import review_support
ROOT=Path(__file__).parent
cases=[
 ('Salary: RM5,000 per month.','Salary is RM5,000 per month.',True),
 ('Salary: RM5,000 per month.','Salary after tax is RM5,000 per month.',False),
 ('Employment is not permitted under this visa.','The holder may work under this visa.',False),
 ('Employment is not permitted under this visa.','This visa does not permit employment.',True),
 ('During probation, either party may terminate with fourteen days written notice.','Either party must always give fourteen days written notice.',False),
 ('During probation, either party may terminate with fourteen days written notice.','During probation, either party may terminate with fourteen days written notice.',True),
 ('Pre-existing conditions are excluded.','Pre-existing conditions are covered.',False),
 ('Start date: 2027-01-01. Probation period: six months.','Probation is guaranteed to end on 2027-07-01.',False),
 ('Closing balance: RM1,300.','The account has a closing balance of RM1,300.',True),
 ('Closing balance: RM1,300.','The account holder is creditworthy.',False),
]
async def main():
    s=SemanticDocument(documentCategory='Other',classificationConfidence=.9,classificationEvidenceIds=['e0'],
      evidence=[Evidence(id=f'e{i}',page=1,sourceText=quote) for i,(quote,claim,truth) in enumerate(cases)],
      statements=[Statement(id=f's{i}',kind='interpretation',label='Claim',text=claim,originalValue=quote,confidence=.9,evidenceIds=[f'e{i}']) for i,(quote,claim,truth) in enumerate(cases)])
    diagnostics=await review_support(s)
    rows=[{'id':item.id,'claim':item.text,'evidence':cases[i][0],'goldSupported':cases[i][2],
       'predictedStatus':item.supportStatus,'reason':item.supportReason,'confidence':item.confidence} for i,item in enumerate(s.statements)]
    supported=[r for r in rows if r['predictedStatus']=='SUPPORTED']
    report={'model':os.environ['OLLAMA_MODEL'],'handAuthoredSyntheticClaims':len(rows),'diagnostics':diagnostics,
        'binaryAccuracy':sum((r['predictedStatus']=='SUPPORTED')==r['goldSupported'] for r in rows)/len(rows),
        'supportedPrecision':sum(r['goldSupported'] for r in supported)/len(supported) if supported else None,
        'supportedRecall':sum(r['goldSupported'] and r['predictedStatus']=='SUPPORTED' for r in rows)/sum(r['goldSupported'] for r in rows),
        'unknownCount':sum(r['predictedStatus']=='UNCERTAIN' for r in rows),'results':rows,
        'limitations':'10 narrow author-defined examples, no independent human corpus review or calibration.'}
    (ROOT/'reports'/('support-challenge.'+os.environ['OLLAMA_MODEL'].replace(':','-')+'.json')).write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='results'}))
if __name__=='__main__':asyncio.run(main())
