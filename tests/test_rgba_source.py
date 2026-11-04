import copy
import json
from pathlib import Path
from tools.export_runtime import RuntimeExport
from tools.native_effects import refresh_program_assets, fsm_asset

ROOT = Path(__file__).parents[1]


def test_rgba_parameter_refresh_matches_original_fields_and_none_flags():
    audit = json.loads((ROOT / 'manifests/native-rgba-refresh.json').read_text(encoding='utf-8'))
    assert audit['programs'] and not audit['failures']
    ex = RuntimeExport()
    def source_object(source):
        return ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    for record in audit['programs']:
        program = json.loads((ex.folder / record['file']).read_text(encoding='utf-8'))
        original = copy.deepcopy(program)
        refresh_program_assets(program, source_object, ex.imp.reader, lambda obj: fsm_asset(ex, obj), {'SetColorRGBA'})
        assert program == original
        actions = [a for m in program['machines'] for s in m['states'] for a in s['actions'] if a['name'] == 'SetColorRGBA']
        assert len(actions) == record['actions']
        assert 'SetColorRGBA' not in program['unknown_actions']
        for action in actions:
            for param in action['params']:
                if param['name'] in {'red', 'green', 'blue', 'alpha'}:
                    assert param['none_known'] and not param.get('unresolved', False)
