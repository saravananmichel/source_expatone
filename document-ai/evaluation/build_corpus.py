"""Safe, deliberately fictitious fixtures. No user document inputs."""
import json
from pathlib import Path
ROOT=Path(__file__).parent
base=json.loads((ROOT/'dataset.json').read_text())
for c in base:
    c['pages']=[c.pop('text')];c['language']='en';c['layout']='native';c['findings']=[];c['actions']=[]
extra=[
 ('pass','Employment Pass','EMPLOYMENT PASS - SYNTHETIC\nHolder: Avery Example\nPass number: TEST-EP-001\nEmployer: Example Research Sdn. Bhd.\nValid until: 2027-06-30\nCondition: Employment is restricted to the employer named on this pass.',['Avery Example','TEST-EP-001','2027-06-30'],['employer']),
 ('government-letter','Government Letter','GOVERNMENT LETTER - SYNTHETIC\nSynthetic Licensing Office\nRecipient: Avery Example\nApplication reference: TEST-APP-01\nDecision: Additional information required.\nPlease submit proof of address by 2027-02-15.',['TEST-APP-01','2027-02-15'],['address']),
 ('government-form','Government Form','GOVERNMENT FORM - SYNTHETIC\nApplicant: Avery Example\nReference: TEST-FORM-01\nAddress: [blank]\nAttachment checklist: [X] Passport copy [ ] Proof of address\nDeclaration: The applicant confirms the supplied information is accurate.',['Avery Example','TEST-FORM-01'],['declaration']),
 ('tax','Tax Document','TAX STATEMENT - SYNTHETIC\nTaxpayer: Avery Example\nAssessment year: 2026\nEmployment income: RM84,000\nTax deducted: RM3,000\nThis statement records deductions and is not a tax assessment.',['2026','RM84,000','RM3,000'],['assessment']),
 ('medical','Medical Document','MEDICAL REPORT - SYNTHETIC\nPatient: Avery Example\nProvider: Example Clinic\nVisit date: 2026-09-15\nObservation: Blood pressure reading 120/80.\nInstruction: Return for review in four weeks.',['Avery Example','2026-09-15','120/80'],['review']),
 ('table','Bank Statement','BANK STATEMENT - SYNTHETIC\nHolder: Avery Example\nDate | Description | Debit | Credit\n2026-09-01 | Opening | - | RM1,000\n2026-09-03 | Rent | RM200 | -\n2026-09-04 | Transfer | - | RM500\nClosing balance: RM1,300',['RM1,000','RM200','RM500','RM1,300'],[]),
 ('malay','Offer Letter','SURAT TAWARAN - SINTETIK\nNama pekerja: Ali Contoh\nJawatan: Jurutera\nGaji: RM5,000 sebulan\nTarikh mula: 2027-01-01\nTempoh percubaan: enam bulan.',['Ali Contoh','RM5,000','2027-01-01'],['probation']),
 ('tamil','Offer Letter','வேலை வாய்ப்புக் கடிதம்\nபெயர்: அருண் மாதிரி\nபதவி: பொறியாளர்\nமாத சம்பளம்: RM5,000\nதொடக்க தேதி: 2027-01-01',['அருண் மாதிரி','RM5,000','2027-01-01'],[]),
 ('chinese','Offer Letter','录用通知书（合成测试）\n员工姓名：林示例\n职位：工程师\n月薪：RM5,000\n入职日期：2027-01-01\n试用期：六个月。',['林示例','RM5,000','2027-01-01'],['probation']),
]
for ident,category,text,values,clauses in extra:
    base.append(dict(id=ident,category=category,pages=[text],values=values,clauses=clauses,forbidden=['legal requirement','automatic renewal'],language={'malay':'ms','tamil':'ta','chinese':'zh'}.get(ident,'en'),layout='table' if ident=='table' else 'native',findings=[],actions=[]))
base.append(dict(id='cross-page',category='Employment Contract',pages=[
 'EMPLOYMENT CONTRACT - SYNTHETIC\nEmployee: Avery Example\nEmployer: Example Research\nStart date: 2027-01-01\nMonthly salary: RM6,000',
 'PROBATION\nThe employee serves a six-month probation period from the start date.\nDuring probation, either party may terminate with fourteen days written notice.',
 'CONFIRMATION\nAfter written confirmation, either party may terminate with sixty days written notice.\nConfidentiality: The employee must protect confidential information.'],values=['2027-01-01','RM6,000','fourteen days','sixty days'],clauses=['probation','confirmation'],forbidden=['2027-07-01','after tax'],language='en',layout='multi-page',findings=['probation','notice'],actions=[]))
base.append(dict(id='columns',category='Insurance Document',pages=['INSURANCE POLICY - SYNTHETIC\nPolicyholder: Avery Example\nCoverage column: Hospital treatment up to RM20,000.\nExclusions column: Cosmetic treatment is excluded.\nClaims column: Submit receipts within thirty days.\nPremium column: RM100 per month.'],values=['Avery Example','RM20,000','RM100'],clauses=['excluded','receipts'],forbidden=['guaranteed payment'],language='en',layout='columns',findings=[],actions=[]))
# Real, bounded 24-page agreement; unique page-local facts exercise context coverage.
base.append(dict(id='long-agreement',category='Employment Contract',pages=base[-2]['pages']+[
 f'EMPLOYMENT CONTRACT - SYNTHETIC / SCHEDULE {i}\nTraining allowance for schedule {i}: RM100\nThe employee must retain receipts for reimbursement.\nEnd of schedule {i}.' for i in range(4,25)],values=['2027-01-01','RM6,000','fourteen days','sixty days'],clauses=['probation','receipts'],forbidden=['guaranteed'],language='en',layout='headers-footers',findings=[],actions=[]))
for c in base:
    (ROOT/'dataset'/f'{c["id"]}.json').write_text(json.dumps(c,ensure_ascii=False,indent=2))
    gold={'id':c['id'],'category':c['category'],'expectedValues':c['values'],
      'entities':[],'fields':[{'value':v,'evidencePages':[i+1 for i,p in enumerate(c['pages']) if v in p]} for v in c['values']],
      'dates':[v for v in c['values'] if v[:4].isdigit() and '-' in v], 'money':[v for v in c['values'] if v.startswith('RM')],
      'sections':[],'clauses':c['clauses'],'obligations':[],'conditions':[], 'relationships':c['findings'],
      'findings':c['findings'],'actions':c['actions'],'forbiddenClaims':c['forbidden'],
      'summaryCoverageTargets':c['values']+c['clauses'],'descriptiveExplanationTargets':c['clauses'],
      'goldCompleteness':'partial synthetic targets; independent human annotation pending',
      'rubric':{'0':'incorrect','1':'partially correct','2':'correct','3':'useful and contextual'}}
    (ROOT/'gold'/f'{c["id"]}.json').write_text(json.dumps(gold,ensure_ascii=False,indent=2))
print('Synthetic cases:',len(base))
