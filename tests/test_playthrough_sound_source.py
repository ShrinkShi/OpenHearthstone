import copy
import json
from pathlib import Path
from types import SimpleNamespace

from tools.importer import Importer
from tools.native_effects import identity, refresh_program_assets, source_program_nodes


def test_playthrough_controls_and_explicit_sound_dependencies_replay_source():
    root = Path(__file__).parents[1]
    report = json.loads((root / 'manifests/native-playthrough-audio-refresh.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 173 and not report['failures']
    importer = Importer()
    total = explicit = 0

    def source(value):
        return importer.reader.load(value['bundle']).get_cab(value['cab'].lower()).objects[int(value['pathid'])]

    for record in report['programs']:
        program = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(program['reference'])
        nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in program['nodes']]
        assets = {}
        for machine in program['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name'] != 'AudioPlaythroughAction':
                        continue
                    total += 1
                    params = {p['name']: p for p in action['params']}
                    assert params['m_VolumeScale']['none_known']
                    sound = params['m_OneShotSound']
                    if sound['object_null']:
                        continue
                    explicit += 1
                    assert not sound['unresolved']
                    exported = {key: sound[key] for key in ('asset_source', 'asset_path', 'unresolved')}
                    assets[json.dumps(sound['asset_source'], sort_keys=True)] = exported
                    data = json.loads((root / 'local/runtime' / sound['asset_path']).read_text(encoding='utf-8'))
                    assert data['source'] == sound['asset_source'] and not data['errors']
                    source_tree = source(sound['asset_source']).read_typetree()
                    assert data['values'] == source_tree
                    assert all((root / 'local/runtime' / clip['file']).is_file() for clip in data['clips'])
        replay = copy.deepcopy(program)
        assert refresh_program_assets(replay, source, importer.reader,
            lambda obj: assets[json.dumps(identity(SimpleNamespace(imp=importer), obj), sort_keys=True)],
            {'AudioPlaythroughAction'}, node_ids) == record['parameters']
        assert replay == program
    assert total == 280 and explicit == 89
