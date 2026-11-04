import json
from pathlib import Path
from tools.importer import Importer

ROOT = Path(__file__).parents[1]


def test_property_descriptors_match_original_action_payloads_and_target_identity():
    audit = json.loads((ROOT / 'manifests/native-property-refresh.json').read_text(encoding='utf-8'))
    assert len(audit['programs']) == 99 and not audit['failures']
    importer = Importer()
    checked = 0
    for record in audit['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        for machine in program['machines']:
            if not any(a['name'] in {'SetProperty', 'GetProperty'} for s in machine['states'] for a in s['actions']):
                continue
            source = machine['source']
            owner = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
            fsm = owner.read_typetree()['fsm']
            variables = {v['name']: v for group in fsm['variables'].values() if isinstance(group, list)
                         for v in group if isinstance(v, dict) and 'name' in v}
            for state, original in zip(machine['states'], fsm['states']):
                assert state['name'] == original['name']
                data = original['actionData']
                source_indices = [i for i in range(len(data['actionNames'])) if data.get('actionEnabled', [1]*len(data['actionNames']))[i]]
                assert len(source_indices) == len(state['actions'])
                for action, index in zip(state['actions'], source_indices):
                    if action['name'] not in {'SetProperty', 'GetProperty'}:
                        continue
                    begin = data['actionStartIndex'][index]
                    end = data['actionStartIndex'][index+1] if index+1 < len(data['actionStartIndex']) else len(data['paramName'])
                    position = next(n for n in range(begin, end) if data['paramDataType'][n] == 36)
                    raw = data['fsmPropertyParams'][data['paramDataPos'][position]]
                    descriptor = next(p['property_data'] for p in action['params'] if p['kind'] == 36)
                    assert descriptor['raw'] == raw
                    assert descriptor['target_type'] == raw['TargetTypeName'] and descriptor['property_name'] == raw['PropertyName']
                    target = raw['TargetObject'];var = target['name'] if target['useVariable'] else ''
                    value = variables.get(var, target)['value']
                    obj = importer.reader.pointer(owner, value)
                    if obj is not None:
                        assert descriptor['target_source']['cab'] == obj.assets_file.name
                        assert int(descriptor['target_source']['pathid']) == obj.path_id
                    for param in descriptor['values']:
                        field = raw[param['name']]
                        assert param['none_known'] and param['is_none'] == bool(field['useVariable'] and not field['name'])
                        if param['name'] == 'IntParameter' and not param['variable']:
                            assert param['integer_known'] and param['integer'] == field['value']
                    checked += 1
    assert checked == sum(r['actions'] for r in audit['programs']) == 554
