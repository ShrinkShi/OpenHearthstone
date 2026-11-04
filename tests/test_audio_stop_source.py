import copy
import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_original_audio_stop_owner_replays_source():
    root = Path(__file__).parents[1]
    importer = Importer()
    report = json.loads((root / 'manifests/native-audio-stop-refresh.json').read_text(encoding='utf-8'))
    assert not report['failures']
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
                                      {'AudioStopAction'}, node_ids) == record['parameters']
        assert replay == original
        assert 'AudioStopAction' not in original['unknown_actions']
        total += record['actions']
    assert total == 16
