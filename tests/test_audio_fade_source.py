import copy
import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_original_audio_fade_controls_and_explicit_source_replay():
    root = Path(__file__).parents[1]
    importer = Importer()
    report = json.loads((root / 'manifests/native-audio-fade-refresh.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 61 and not report['failures']
    total = explicit = 0

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    for record in report['programs']:
        original = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(original['reference'])
        nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in original['nodes']]
        replay = copy.deepcopy(original)
        assert refresh_program_assets(replay, source, importer.reader, lambda obj: {},
                                      {'AudioVolumeFadeAction'}, node_ids) == record['parameters']
        for old_machine, new_machine in zip(original['machines'], replay['machines']):
            for old_state, new_state in zip(old_machine['states'], new_machine['states']):
                for old_action, new_action in zip(old_state['actions'], new_state['actions']):
                    if old_action['name'] != 'AudioVolumeFadeAction':
                        continue
                    for old_param, new_param in zip(old_action['params'], new_action['params']):
                        assert old_param == new_param, (record['file'], old_state['name'], old_param['name'], old_param, new_param)
        assert replay == original
        assert 'AudioVolumeFadeAction' not in original['unknown_actions']
        for action in original['machines']:
            for state in action['states']:
                for fade in state['actions']:
                    if fade['name'] != 'AudioVolumeFadeAction':
                        continue
                    total += 1
                    params = {p['name']: p for p in fade['params']}
                    for name in ('m_FadeTime', 'm_TargetVolume', 'm_StopWhenDone', 'm_RealTime'):
                        assert params[name]['none_known'] and not params[name].get('unresolved')
                    assert params['m_RealTime']['number'] == 0
                    audio = params.get('m_AudioSource')
                    if audio and not audio['object_null'] and not audio.get('unresolved'):
                        explicit += 1
                        assert audio['asset_type'] == 'AudioSource' and audio['node'] >= 0
                        assert audio['asset_source']['type'] == 'AudioSource'
    assert total == 168 and explicit == 12


def test_fade_audio_definitions_cover_detached_original_emitters():
    root = Path(__file__).parents[1]
    report = json.loads((root / 'manifests/native-audio-fade-bindings.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 61
    explicit = 0
    for record in report['programs']:
        assert not record['errors']
        program = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        audio = json.loads((root / 'local/runtime' / record['audio_file']).read_text(encoding='utf-8'))
        assert program['audio_file'] == record['audio_file']
        assert audio['source'] == program['source'] == record['source']
        nodes = {n['node']: n for n in audio['nodes']}
        assert len(nodes) == len(audio['nodes'])
        for node in audio['nodes']:
            assert node['node_known'] and node['path'] == program['nodes'][node['node']]['path']
        for machine in program['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name'] != 'AudioVolumeFadeAction':
                        continue
                    params = {p['name']: p for p in action['params']}
                    source = params.get('m_AudioSource')
                    if source and not source['object_null']:
                        sources = nodes[source['node']]['audio_sources']
                        assert len(sources) == 1
                        assert sources[0]['source']['cab'] == source['asset_source']['cab']
                        assert sources[0]['source']['pathid'] == source['asset_source']['pathid']
                        explicit += 1
    assert explicit == 12
