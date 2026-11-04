import json
from pathlib import Path

from tools.battle_camera_source import recover
from tools.importer import Importer

ROOT = Path(__file__).parents[1]


def test_camera_catalog_replays_exact_scene_data_and_transform_hierarchy():
    catalog = json.loads((ROOT / 'manifests/original-battle-cameras.json').read_text(encoding='utf-8'))
    assert recover(Importer()) == catalog
    assert json.loads((ROOT / 'local/runtime/original-battle-cameras.json').read_text(encoding='utf-8')) == catalog
    assert not catalog['full_scene_camera_restored']
    assert {c['name'] for c in catalog['cameras']} == {
        'Main Camera', 'MainFullscreenFXCamera', 'IgnoreFullScreenEffectsCamera', 'TooltipCamera'}
    main = next(c for c in catalog['cameras'] if c['name'] == 'Main Camera')
    assert main['projection_mode'] == 1 and not main['orthographic']
    assert main['fov'] == main['raw']['field of view'] == 34.870445251464844
    assert main['near'] == 1 and main['far'] == 200
    assert main['chain'][0]['position'] == {'x': 1092.5, 'y': 32.0, 'z': 6.099999904632568}
    assert len(main['chain']) == 1
    for camera in catalog['cameras']:
        assert camera['chain'][-1]['raw']['m_Father']['m_PathID'] == 0
        assert camera['chain'][0]['position'] == camera['chain'][0]['raw']['m_LocalPosition']
