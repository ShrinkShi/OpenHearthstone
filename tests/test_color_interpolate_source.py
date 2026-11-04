import copy
import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_all_color_interpolate_arrays_replay_original_source():
    root = Path(__file__).parents[1]
    report = json.loads((root / 'manifests/native-color-interpolate-refresh.json').read_text(encoding='utf-8'))
    importer = Importer()
    audit = json.loads((root / 'manifests/native-effect-audit.json').read_text(encoding='utf-8'))
    assert not report['failures'] and len(report['programs']) == 17
    total = 0

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    for record in report['programs']:
        original = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(original['reference'])
        nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in original['nodes']]
        replay = copy.deepcopy(original)
        assert refresh_program_assets(replay, source, importer.reader, lambda obj: {},
                                      {'ColorInterpolate'}, node_ids) == record['parameters']
        assert replay == original
        for machine in original['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name'] != 'ColorInterpolate':
                        continue
                    total += 1
                    params = action['params']
                    index = next(i for i, p in enumerate(params) if p['name'] == 'colors')
                    header = params[index]
                    assert header['array_known'] and not header.get('unresolved')
                    assert header['array_type'] == 'HutongGames.PlayMaker.FsmColor'
                    assert all(p['kind'] == 25 and not p.get('unresolved')
                               for p in params[index + 1:index + 1 + header['array_count']])
                    assert next(p for p in params if p['name'] == 'realTime')['number'] == 0
        assert 'ColorInterpolate' not in original['unknown_actions']
        for entry in audit['cards']:
            if entry['reference'] == original['reference']:
                assert entry['unresolved_parameters'] == sum(bool(p.get('unresolved'))
                    for m in original['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
                assert 'ColorInterpolate' not in entry['unknown_actions']
    assert total == 44
