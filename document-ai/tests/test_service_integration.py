import io
from fastapi.testclient import TestClient
from app.main import app
from app.schemas import SemanticDocument
from pdf_helpers import native_pdf

def test_pdf_ingestion_to_grounded_analysis(monkeypatch):
    text='EMPLOYMENT CONTRACT\nSalary: RM12,000 per month.\nThe employee must protect confidential information.'
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY','synthetic-test-key')
    monkeypatch.setenv('OLLAMA_MODEL','test-model')
    async def model(pages):
        assert '12,000' in pages[0].text
        return SemanticDocument.model_validate({
            'documentCategory':'Employment Contract','classificationConfidence':.8,'classificationEvidenceIds':['e1'],
            'evidence':[{'id':'e1','page':1,'sourceText':'Salary: RM12,000 per month.'}],
            'statements':[{'id':'s1','kind':'fact','label':'Salary','text':'The agreement states RM12,000 per month.',
                'originalValue':'RM12,000','confidence':.8,'evidenceIds':['e1']}]
        }),'test-model'
    monkeypatch.setattr('app.main.reason',model)
    with TestClient(app) as client:
        response=client.post('/analyze',headers={'X-Service-Key':'synthetic-test-key'},
            files={'file':('synthetic.pdf',native_pdf([text]),'application/pdf')})
        assert response.status_code==200
        output=response.json()
        assert output['documentCategory']=='Employment Contract'
        assert output['semanticDocument']['pages'][0]['extraction']=='native'
        assert output['evidence'][0]['page']==1
        assert output['statements'][0]['kind']=='fact'
        assert '12,000' in output['summary']
