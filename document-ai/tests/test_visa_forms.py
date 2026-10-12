import io
import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient
from pypdf import PdfReader
from app.visa_forms import fill_pdf, mapped_values, router, PACK


def check_widgets(reader):
    for page in reader.pages:
        for ref in page.get('/Annots', []):
            widget = ref.get_object()
            if widget.get('/Subtype') != '/Widget': continue
            field = widget.get('/Parent', widget).get_object()
            if field.get('/FT') == '/Sig': continue
            assert '/AP' in widget and '/N' in widget['/AP']
            if field.get('/FT') == '/Tx':
                assert widget['/AP']['/N'].get_data()
                assert field.get('/V', '') == reader.get_fields()[field['/T']].get('/V', '')
            if field.get('/FT') == '/Btn':
                value = str(field.get('/V', '/Off'))
                expected = value if value in widget['/AP']['/N'] else '/Off'
                assert widget.get('/AS', '/Off') == expected
                assert widget['/AP']['/N'][expected].get_data()


def test_14a_boxes_radio_groups_and_declarations():
    values = {'fullName':'SAMPLE APPLICANT','birthDate':'1985-06-15','sex':'Female','passportNumber':'A1234567',
              'otherResidence':'Yes','antecedent1':'Yes','antecedent2':'No','antecedent3':'No','antecedent4':'No'}
    reader = PdfReader(io.BytesIO(fill_pdf('14a', values)))
    fields = reader.get_fields()
    assert fields['undefined']['/V'] == 'SAMPLE APPLICANT'
    assert fields['Y']['/V'] == '1985'
    assert fields['undefined_6']['/V'] == '/On'
    assert fields['undefined_5']['/V'] == '/Off'
    assert fields['Check Box3']['/V'] == '/Yes'
    assert fields['Check Box7']['/V'] == '/Off'
    assert fields['undefined_21']['/V'] == ''
    assert fields['Date']['/V'] == ''
    check_widgets(reader)
    # Regression: original template appearances clipped the first part of the name/date.
    assert b'53414d504c45' not in reader.pages[0]['/Annots'][0].get_object()['/AP']['/N'].get_data()  # comb characters drawn individually


def test_v39a_maps_physical_lines_not_misleading_field_names():
    values={'fullName':'SAMPLE APPLICANT','nationality':'India','passportNumber':'A1234567','loiContactType':'Individual',
            'loiRelationship':'Daughter','localRelationship':'Father','localName':'SAMPLE CONTACT','loiNric':'S1234567A'}
    reader=PdfReader(io.BytesIO(fill_pdf('v39a',values)));f=reader.get_fields()
    assert f['countryplace holder of passporttravel document no']['/V']=='SAMPLE APPLICANT'
    assert f['The applicant for the visa']['/V']=='India'
    assert f['daughter spouse business partner for others please specify']['/V']=='Daughter'
    assert not f['Signature of Local Contact'].get('/V')
    assert not f['Signature of person acting on behalf of the'].get('/V')
    check_widgets(reader)


def test_missing_answers_remain_unanswered():
    f=PdfReader(io.BytesIO(fill_pdf('14a',{}))).get_fields()
    assert f['Check Box3']['/V']=='/Off' and f['Check Box7']['/V']=='/Off'

@pytest.mark.parametrize('values',[{'signature':'fake'},{'fullName':'x'*51},{'sex':'Unknown'},{'fullName':'测试'}])
def test_unsafe_or_unrenderable_inputs_are_rejected(values):
    with pytest.raises(ValueError):fill_pdf('14a',values)


def test_private_endpoint_requires_key(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY','test-key')
    app=FastAPI();app.include_router(router);client=TestClient(app)
    body={'templateId':PACK['id'],'values':{}}
    assert client.post('/visa/forms/14a',json=body).status_code==401
    assert client.post('/visa/forms/14a',json=body,headers={'X-Service-Key':'test-key'}).status_code==200
    assert client.post('/visa/forms/other',json=body,headers={'X-Service-Key':'test-key'}).status_code==400
