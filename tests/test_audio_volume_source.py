import copy
import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_original_audio_volume_nodes_none_and_frame_flags():
    root = Path(__file__).parents[1]
    importer = Importer()
    total = 0

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    for suffix, expected in [('', 46), ('-extra', 11)]:
        report = json.loads((root / f'manifests/native-audio-volume{suffix}-refresh.json').read_text(encoding='utf-8'))
        assert not report['failures'] and len(report['programs']) == expected
        for record in report['programs']:
            original = json.loads((root / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
            obj, _, _ = importer.reader.resolve(original['reference'])
            nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
            assert nodes == [(n['name'], n['parent'], n['path']) for n in original['nodes']]
            replay = copy.deepcopy(original)
            assert refresh_program_assets(replay, source, importer.reader, lambda obj: {},
                                          {'AudioSetVolumeAction'}, node_ids) == record['parameters']
            assert replay == original
            for machine in original['machines']:
                for state in machine['states']:
                    for action in state['actions']:
                        if action['name'] == 'AudioSetVolumeAction':
                            total += 1
                            volume = next(p for p in action['params'] if p['name'] == 'm_Volume')
                            assert volume['none_known'] and not volume.get('unresolved')
            assert 'AudioSetVolumeAction' not in original['unknown_actions']
    assert total == 126
