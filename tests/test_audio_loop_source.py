import copy
import json
from pathlib import Path
from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_original_loop_controls_source_hierarchy_and_audio_bindings():
    root = Path(__file__).parents[1]
    importer = Importer()
    report = json.loads((root / 'manifests/native-audio-loop-refresh.json').read_text(encoding='utf-8'))
    bindings = json.loads((root / 'manifests/native-audio-loop-bindings.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == len(bindings['programs']) == 12 and not report['failures']
    names = {'AudioLoopingStartAction', 'AudioLoopingEndAction'}
    counts = {name: 0 for name in names}

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    for record in report['programs']:
        original = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(original['reference'])
        nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in original['nodes']]
        replay = copy.deepcopy(original)
        assert refresh_program_assets(replay, source, importer.reader, lambda obj: {}, names, node_ids) == record['parameters']
        assert replay == original and not names.intersection(original['unknown_actions'])
        audio = json.loads((root / 'local/runtime' / original['audio_file']).read_text(encoding='utf-8'))
        assert not audio['errors'] and audio['source'] == original['source']
        emitters = {n['node']: n for n in audio['nodes']}
        for machine in original['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name'] not in names:
                        continue
                    counts[action['name']] += 1
                    params = {p['name']: p for p in action['params']}
                    assert not any(p.get('unresolved') for p in params.values())
                    emitter = emitters[params['m_GameObject']['node']]
                    assert emitter['node_known'] and len(emitter['audio_sources']) == 1
                    if action['name'] == 'AudioLoopingStartAction':
                        assert len(emitter['sound_defs']) == 1
                        assert params['m_randomizeStartPoint']['number'] == params['m_dontDestroySource']['number'] == 0
                        assert all(p['none_known'] for n, p in params.items() if n != 'm_GameObject')
                    else:
                        assert params['m_FadeOutTime']['none_known'] and params['m_stop']['none_known']
    assert counts == {'AudioLoopingStartAction': 12, 'AudioLoopingEndAction': 24}
