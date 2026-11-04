import copy
import json
from pathlib import Path
from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_original_main_camera_shake_controls_replay_source():
    root = Path(__file__).parents[1]
    importer = Importer()
    report = json.loads((root / 'manifests/native-main-shake-refresh.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 66 and not report['failures']
    total = 0

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    for record in report['programs']:
        original = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(original['reference'])
        nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in original['nodes']]
        replay = copy.deepcopy(original)
        assert refresh_program_assets(replay, source, importer.reader, lambda obj: {}, {'MainCameraShakerAction'}, node_ids) == record['parameters']
        assert replay == original and 'MainCameraShakerAction' not in original['unknown_actions']
        for machine in original['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name'] == 'MainCameraShakerAction':
                        total += 1
                        params = {p['name']: p for p in action['params']}
                        assert set(params) == {'m_Amount', 'm_Time', 'm_Delay', 'm_FinishedEvent'}
                        assert not any(p.get('unresolved') for p in params.values())
                        assert all(params[n]['none_known'] for n in ('m_Amount', 'm_Time', 'm_Delay'))
    assert total == 87
