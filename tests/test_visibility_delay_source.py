import copy
import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import refresh_program_assets, source_program_nodes


def test_delayed_visibility_replays_original_node_pointer_and_none_flags():
    root = Path(__file__).parents[1]
    original = json.loads((root / 'local/runtime/4fc0fe4fe4824ecbe3db289c.native-effect.json').read_text(encoding='utf-8'))
    importer = Importer()
    obj, _, _ = importer.reader.resolve(original['reference'])
    nodes, node_ids, _ = source_program_nodes(importer.reader, obj)
    assert nodes == [(node['name'], node['parent'], node['path']) for node in original['nodes']]

    def source(identity):
        return importer.reader.load(identity['bundle']).get_cab(identity['cab'].lower()).objects[int(identity['pathid'])]

    replay = copy.deepcopy(original)
    assert refresh_program_assets(replay, source, importer.reader, lambda obj: {},
                                  {'SetVisibilityRecursiveDelayAction'}, node_ids) == 4
    assert replay == original
    actions = [action for machine in replay['machines'] for state in machine['states']
               for action in state['actions'] if action['name'] == 'SetVisibilityRecursiveDelayAction']
    assert len(actions) == 1
    params = {p['name']: p for p in actions[0]['params']}
    assert params['gameObject']['node'] == 2
    assert params['m_Delay']['number'] == .25
    assert params['m_Delay']['none_known'] and not params['m_Delay']['is_none']
    assert params['includeChildren']['number'] == 1
    assert 'SetVisibilityRecursiveDelayAction' not in original['unknown_actions']
    audit = json.loads((root / 'manifests/native-effect-audit.json').read_text(encoding='utf-8'))
    record = next(r for r in audit['cards'] if r['reference'] == original['reference'])
    assert 'SetVisibilityRecursiveDelayAction' not in record['unknown_actions']
