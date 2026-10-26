"""Validate versioned template/schema and migration payload consistency (stdlib only)."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
infra = root / 'src/backend/ExpatOne.Infrastructure'
packs = [json.loads(p.read_text()) for p in (infra / 'VisaTemplates').glob('*.json')]
assert len(packs) == 13
assert len({p['id'] for p in packs}) == len(packs)
new = [p for p in packs if p.get('validationProfile') == 'preparation-v1']
assert {(p['country'], p['purpose']) for p in new} == {
    (c, p) for c in ['MY', 'VU', 'IN', 'CN'] for p in ['Tourism', 'Employment', 'Dependent']}
for pack in packs:
    fields, checks = pack['fields'], pack['checks']
    assert len(fields) <= 100 and len(checks) <= 30
    keys = {f['key'] for f in fields}
    assert len(keys) == len(fields)
    assert len({c['key'] for c in checks}) == len(checks)
    for f in fields:
        assert f['type'] in ['text', 'date', 'choice', 'email', 'number', 'multiline']
        if f['type'] == 'choice': assert f['options'] and len(set(f['options'])) == len(f['options'])
        assert f['maxLength'] > 0
    for check in checks:
        if check['needsDocument']: assert check['role'] in pack['documentRoles']
    for item in fields + checks:
        condition = item.get('when')
        if condition and condition.startswith('eq:'):
            _, key, value = condition.split(':')
            assert key in keys
            assert value in next(f['options'] for f in fields if f['key'] == key)
    assert all(s['url'].startswith('https://') for s in pack['sources'])
    if pack in new:
        assert pack['exportForms'] == []
        assert all('pdf' not in f for f in fields)
        assert 'routeConfirmed' in keys
        assert 'Singapore' not in json.dumps(pack)
        assert all(item.get('when') in [None, 'translation'] or item['when'].startswith('eq:') for item in fields + checks)

snapshot = infra / 'Persistence/Migrations/ExpatOneDbContextModelSnapshot.cs'
designer = infra / 'Persistence/Migrations/20261026100000_AddPriorityCountryVisaTemplates.Designer.cs'
for path in [snapshot, designer]:
    embedded = [json.loads(json.loads(x)) for x in re.findall(r'RulesJson = (".*")\s*\n', path.read_text())]
    assert {x['id']: x for x in embedded} == {x['id']: x for x in packs}, path
migration = (infra / 'Persistence/Migrations/20261026100000_AddPriorityCountryVisaTemplates.cs').read_text()
sql = [json.loads(x) for x in re.findall(r'migrationBuilder.Sql\((".*")\);', migration)]
assert len(sql) == 16
for p in (infra / 'VisaTemplates').glob('*.json'):
    if p.name != 'sg-visit.json': assert any(p.read_text().replace("'", "''") in q for q in sql), p
print('PASS: 13 templates, 12 country/purpose combinations, field/check schemas, conditional references and frozen migration/snapshot payloads.')
