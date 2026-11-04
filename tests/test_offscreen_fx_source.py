import json
from pathlib import Path
from tools.importer import Importer

ROOT = Path(__file__).parents[1]


def test_authored_offscreen_component_fields_and_pointer_identity():
    program = json.loads((ROOT / 'local/runtime/5a360810c62fabb080bc018b.start-spell.json').read_text(encoding='utf-8'))
    assert len(program['offscreen_fx']) == 1
    data = program['offscreen_fx'][0]
    importer = Importer()
    source = data['source_identity']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    assert data['raw'] == raw
    assert (data['width'], data['height'], data['above'], data['below']) == (raw['RenderResolutionX'], raw['RenderResolutionY'], raw['AboveClip'], raw['BelowClip'])
    assert data['rect'] == raw['CameraRect'] and data['use_bounds'] == bool(raw['UseBounds'])
    assert program['nodes'][data['owner']]['name'] == 'RenderPlane'
    script = importer.reader.pointer(obj, raw['m_Script']).read_typetree()
    assert script['m_ClassName'] == 'OffScreenRenderFX'
