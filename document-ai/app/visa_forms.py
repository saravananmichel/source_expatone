"""Deterministic, private PDF filling. No LLM calls, remote downloads or signatures."""
import base64
import hashlib
import hmac
import io
import json
import os
import textwrap
from datetime import date
from pathlib import Path
from fastapi import APIRouter, Header, HTTPException
from pydantic import BaseModel, Field
from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject, DictionaryObject, DecodedStreamObject, ArrayObject, FloatObject, TextStringObject

router = APIRouter(prefix='/visa')
ROOT = Path(__file__).parent / 'visa_templates'
PACK = json.loads((ROOT / 'sg-visit.json').read_text())

class FillRequest(BaseModel):
    templateId: str = Field(max_length=80)
    values: dict[str, str] = Field(default_factory=dict, max_length=100)


def mapped_values(form: str, values: dict[str, str]) -> dict[str, str]:
    result = {}
    if form == '14a':
        for field in PACK['fields']:
            value = values.get(field['key'], '')
            target = field.get('pdf')
            if not value or not target:
                continue
            if isinstance(target, dict):
                if value not in target:
                    raise ValueError('Unknown choice')
                name = target[value]
                # These are radio parents; their selected state is held on the parent.
                if name.endswith('.Yes') or name.endswith('.No'):
                    name, state = name.rsplit('.', 1)
                    result[name] = '/' + state
                else:
                    result[name] = '/On'  # translated to the actual widget on-state below
            elif isinstance(target, list):
                if field['type'] == 'date':
                    d = date.fromisoformat(value)
                    for name, part in zip(target, (f'{d.day:02}', f'{d.month:02}', str(d.year))):
                        result[name] = part
                elif field['key'] == 'spouseNric':
                    result[target[0 if values.get('spouseStatus') == 'Singapore Citizen' else 1]] = value
                else:
                    width = 25 if field['key'] in ('fullName', 'alias', 'companionName', 'localName') else 100
                    for i, name in enumerate(target):
                        result[name] = value[i*width:(i+1)*width]
            else:
                result[target] = value
        result['Social'] = '/On'
    elif form == 'v39a':
        # Original widget names are misleading: coordinates identify the applicant-name line.
        mapping = {'fullName': 'countryplace holder of passporttravel document no',
                   'nationality': 'The applicant for the visa', 'passportNumber': 'is',
                   'embarkationCountry': 'countryplace',
                   'visitDetails': 'visiting friends  relatives employment education for others please specify The',
                   'loiRelationship': 'daughter spouse business partner for others please specify'}
        if values.get('loiContactType') == 'Individual':
            mapping.update(localName='Name of Local Contact', localPhone='Contact Number',
                           localEmail='Email Address', loiNric='NRIC Pink  Blue No', loiAddress='Address of Local Contact')
        elif values.get('loiContactType') == 'Company':
            mapping.update(loiUen='Unique Entity Number UEN of', loiRepresentative='Name NRIC No and DesignationCapacity',
                           localPhone='Contact Number_2', localEmail='Email Address_2', loiCompanyAddress='Company Name and Address')
        for key, target in mapping.items():
            if values.get(key): result[target] = values[key]
    else:
        raise ValueError('Unknown form')
    return result


def repair_appearances(writer, values):
    # The supplied forms contain broken Courier comb metrics and empty checkbox glyphs.
    # Build explicit appearances while keeping the canonical fields and widgets interactive.
    font = DictionaryObject({NameObject('/Type'):NameObject('/Font'), NameObject('/Subtype'):NameObject('/Type1'),
                             NameObject('/BaseFont'):NameObject('/Courier'), NameObject('/Encoding'):NameObject('/WinAnsiEncoding')})
    font_ref = writer._add_object(font)
    acro = writer.root_object['/AcroForm']
    dr = acro.setdefault(NameObject('/DR'), DictionaryObject())
    dr.setdefault(NameObject('/Font'), DictionaryObject())[NameObject('/FVisa')] = font_ref
    def stream(commands, w, h):
        obj = DecodedStreamObject(); obj.set_data(commands.encode('ascii'))
        obj.update({NameObject('/Type'):NameObject('/XObject'), NameObject('/Subtype'):NameObject('/Form'),
                    NameObject('/BBox'):ArrayObject([FloatObject(0),FloatObject(0),FloatObject(w),FloatObject(h)]),
                    NameObject('/Resources'):DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/FVisa'):font_ref})})})
        return writer._add_object(obj)
    for page in writer.pages:
        for ref in page.get('/Annots', []):
            widget = ref.get_object()
            if widget.get('/Subtype') != '/Widget': continue
            field = widget.get('/Parent', widget).get_object()
            name = field.get('/T')
            if name not in values: continue
            rect = widget['/Rect']; w, h = float(rect[2]-rect[0]), float(rect[3]-rect[1])
            if field.get('/FT') == '/Btn':
                normal = widget['/AP']['/N']
                for state in list(normal):
                    commands = 'q Q' if state == '/Off' else f'q 0 G 1.2 w 2 2 m {w-2:.2f} {h-2:.2f} l S 2 {h-2:.2f} m {w-2:.2f} 2 l S Q'
                    normal[NameObject(state)] = stream(commands,w,h)
                continue
            if field.get('/FT') != '/Tx': continue
            value = values[name]
            field[NameObject('/DA')] = TextStringObject('/FVisa 0 Tf 0 g')
            commands = ['q', f'0 0 {w:.3f} {h:.3f} re W n', 'BT', '0 g']
            comb = bool(int(field.get('/Ff',0)) & (1 << 24))
            def put(text, x, y, size):
                commands.append(f'/FVisa {size:.3f} Tf 1 0 0 1 {x:.3f} {y:.3f} Tm <{text.encode("cp1252").hex()}> Tj')
            if comb:
                count = int(field.get('/MaxLen',len(value) or 1)); cell = w/count
                size = min(12,h*0.70,cell/0.75)
                if len(value)>count: raise ValueError('Text exceeds form boxes')
                for i,ch in enumerate(value): put(ch,cell*i+(cell-0.6*size)/2,(h-size)/2+size*0.18,size)
            else:
                size = min(10,h*0.7)
                multiline = bool(int(field.get('/Ff',0)) & (1 << 12)) or '\n' in value or h>45
                while True:
                    capacity=max(1,int((w-4)/(0.6*size)))
                    lines=[piece for line in value.split('\n') for piece in (textwrap.wrap(line,capacity) or [''])] if multiline else [value]
                    if (multiline and len(lines)*size*1.2<=h-2 or not multiline and len(value)*size*0.6<=w-4): break
                    size-=0.25
                    if size<5: raise ValueError('Text is too long to fit legibly in the official form')
                for i,line in enumerate(lines): put(line,2,h-size*1.05-i*size*1.2 if multiline else (h-size)/2+size*0.18,size)
            commands += ['ET','Q']
            widget[NameObject('/AP')] = DictionaryObject({NameObject('/N'):stream('\n'.join(commands),w,h)})


def fill_pdf(form: str, values: dict[str, str]) -> bytes:
    definitions = {f['key']: f for f in PACK['fields']}
    for key, value in values.items():
        f = definitions.get(key)
        if f is None or len(value) > f['maxLength']:
            raise ValueError('Unknown or overlong field')
        if f.get('options') and value and value not in f['options']:
            raise ValueError('Unknown choice')
        # The official forms use Latin fonts. Never silently corrupt non-Latin input.
        try: value.encode('cp1252')
        except UnicodeEncodeError: raise ValueError('Use the Latin-script spelling in the passport for PDF export') from None
        if any(ord(c) < 32 and c not in '\n\r\t' for c in value):
            raise ValueError('Invalid text')
    reader = PdfReader(ROOT / ('form14a.pdf' if form == '14a' else 'formv39a.pdf'))
    writer = PdfWriter(); writer.clone_document_from_reader(reader)
    fields = writer.get_fields() or {}
    # All source widgets are already represented, including radio /Kids. Do not reattach.
    values_to_write = {}
    for name, field in fields.items():
        if field.get('/FT') == '/Btn': values_to_write[name] = '/Off'
        elif field.get('/FT') == '/Tx': values_to_write[name] = ''
    values_to_write.update(mapped_values(form, values))
    if not set(values_to_write).issubset(fields):
        raise ValueError('Template field mapping changed')
    for page in writer.pages:
        for ref in page.get('/Annots', []):
            widget = ref.get_object()
            field = widget.get('/Parent', widget).get_object()
            name = field.get('/T')
            if field.get('/FT') != '/Btn': continue
            appearance = widget.get('/AP', {}).get('/N')
            if not isinstance(appearance, DictionaryObject): continue
            if '/Off' not in appearance:
                blank = DecodedStreamObject(); blank.set_data(b'q Q')
                rect = widget['/Rect']
                blank.update({NameObject('/Type'):NameObject('/XObject'), NameObject('/Subtype'):NameObject('/Form'),
                              NameObject('/BBox'):ArrayObject([FloatObject(0),FloatObject(0),FloatObject(rect[2]-rect[0]),FloatObject(rect[3]-rect[1])]),
                              NameObject('/Resources'):DictionaryObject()})
                appearance[NameObject('/Off')] = writer._add_object(blank)
            if values_to_write.get(name) == '/On' and '/On' not in appearance:
                states = [s for s in appearance if s != '/Off']
                if len(states) != 1: raise ValueError('Ambiguous checkbox')
                values_to_write[name] = states[0]
    writer.update_page_form_field_values(None, values_to_write, auto_regenerate=False)
    repair_appearances(writer, values_to_write)
    out = io.BytesIO(); writer.write(out)
    check = PdfReader(io.BytesIO(out.getvalue()))
    actual = check.get_fields() or {}
    for name, value in values_to_write.items():
        if str(actual[name].get('/V', '')) != value:
            raise ValueError('PDF field verification failed')
    # Never populate the applicant signature/date or either local-contact digital signature.
    return out.getvalue()

@router.post('/forms/{form}')
def export(form: str, payload: FillRequest, x_service_key: str = Header(default='')):
    expected = os.getenv('DOCUMENT_AI_SERVICE_KEY', '')
    if not expected or not hmac.compare_digest(expected, x_service_key):
        raise HTTPException(401, 'Unauthorized')
    if payload.templateId != PACK['id'] or form not in ('14a', 'v39a'):
        raise HTTPException(400, 'Unsupported form template')
    try:
        data = fill_pdf(form, payload.values)
    except (ValueError, KeyError) as error:
        raise HTTPException(422, str(error)) from None
    return {'fileName': f'SG-Form-{form}-for-review.pdf', 'contentType': 'application/pdf',
            'base64': base64.b64encode(data).decode('ascii'),
            'sha256': hashlib.sha256(data).hexdigest(),
            'notice': 'Review every field and obtain the required signatures. This is not a visa approval or submission.'}
