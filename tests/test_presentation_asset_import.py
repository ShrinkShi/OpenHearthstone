import json

from PIL import Image
import pytest

from tools import card_presentation_assets as pipeline
from tools import presentation_assets


def test_armor_hit_sound_uses_confirmed_blood_splat_clip():
    assert presentation_assets.AUDIO['SFX_ARMOR_HIT'] == 'Armor_Splat_Damage'


def test_render_metadata_preserves_source_and_rejects_invisible_initialization(tmp_path):
    folder = tmp_path / 'runtime'
    folder.mkdir()
    image = Image.new('RGBA', (8, 8), (160, 160, 160, 255))
    image.putpixel((4, 4), (20, 200, 100, 255))
    image.save(folder / 'original.png')
    source = (folder / 'original.png').read_bytes()
    entries = [dict(card_id='visible', visuals=[dict(texture='VFX_ORIGINAL', tint=dict(r=1, g=1, b=1, a=1))]),
               dict(card_id='runtime_only', visuals=[dict(texture='VFX_ORIGINAL', tint=dict(r=0, g=0, b=0, a=1))]),
               dict(card_id='authored', projectile='VFX_AUTHORED'), dict(card_id='audio_only', cast='SFX_CAST')]
    (folder / 'effect-index.json').write_text(json.dumps(dict(entries=entries)))
    omitted = pipeline.refresh_render_metadata(folder, {'VFX_ORIGINAL': 'original.png'})
    refreshed = json.loads((folder / 'effect-index.json').read_text())['entries']
    assert {p['card_id'] for p in refreshed} == {'visible', 'authored', 'audio_only'}
    assert len(omitted) == 1 and omitted[0]['card_id'] == 'runtime_only'
    sprite = refreshed[0]['visuals'][0]
    assert sprite['key_background'] and sprite['soft_mask']
    assert (folder / 'original.png').read_bytes() == source


def test_audit_cannot_claim_missing_player_and_preserves_failure_history(tmp_path, monkeypatch):
    monkeypatch.setattr(pipeline, 'PROJECT', tmp_path)
    for name in ('local/runtime', 'manifests', 'docs'):
        (tmp_path / name).mkdir(parents=True)
    (tmp_path / 'local/runtime/effect-index.json').write_text(json.dumps(dict(entries=[dict(card_id='visible')])))
    audit = dict(cards=[dict(card_id='runtime_only', name='Invisible', status='associated_sequence'),
                        dict(card_id='visible', name='Visible', status='associated_sequence')],
                 assets=[], omitted_visual_layers=[dict(card_id='runtime_only', texture='VFX_ORIGINAL')])
    pipeline.save_audit(audit)
    assert audit['cards'][0]['status'] == 'incomplete'
    assert audit['cards'][1]['status'] == 'associated_sequence'
    audit['omitted_visual_layers'] = []
    pipeline.save_audit(audit)
    saved = json.loads((tmp_path / 'manifests/card-presentation-audit.json').read_text(encoding='utf-8'))
    assert len(saved['omitted_visual_layers']) == 1
    assert '| [ ] | runtime_only |' in (tmp_path / 'docs/CARD_PRESENTATION_STATUS.md').read_text(encoding='utf-8')


def test_vs_projection_rasterizes_only_front_facing_mesh_geometry():
    atlas = Image.new('RGBA', (32, 32), (0, 220, 40, 255))
    for y in range(20, 32):
        for x in range(20, 32):
            atlas.putpixel((x, y), (230, 30, 20, 255))
    identity = {'m_LocalPosition': {'x': 0, 'y': 0, 'z': 0},
                'm_LocalRotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1},
                'm_LocalScale': {'x': 1, 'y': 1, 'z': 1}}
    mesh = {'vertices': [(0, 0, 0), (1, 0, 0), (0, 0, -1),
                        (3, 0, 0), (3, 0, -1), (4, 0, 0)],
            'uv': [(0.05, 0.95), (0.4, 0.95), (0.05, 0.6),
                   (0.7, 0.3), (0.95, 0.3), (0.95, 0.05)],
            'triangles': [[(0, 1, 2), (3, 4, 5)]],
            'transform_chain': [identity]}
    texture_settings = {'m_FilterMode': 1, 'm_Aniso': 1, 'm_MipBias': 0,
                        'm_WrapU': 0, 'm_WrapV': 0, 'm_WrapW': 0}

    sprite, _, triangle_count = presentation_assets.rasterize_vs_mesh(
        atlas, [mesh], texture_settings=texture_settings, resolution=128, supersample=2)

    assert triangle_count == 1
    assert sprite.getchannel('A').getbbox() is not None
    pixels = sprite.load()
    assert all(pixels[x, y][0] < 100 for y in range(sprite.height) for x in range(sprite.width)
               if pixels[x, y][3] > 0)


def test_vs_texture_sampling_repeats_uv_overflow_and_rejects_unknown_wrap():
    atlas = Image.new('RGBA', (32, 32))
    for y in range(32):
        for x in range(32):
            atlas.putpixel((x, y), (x * 7, y * 7, (x + y) * 3, 255))
    tiled = presentation_assets._repeat_atlas(atlas)
    coords = [presentation_assets._repeat_uv_to_pixel((u, .5), (1, 1), (0, 0), atlas.size)
              for u in (.0038, 1.0038)]
    affine_mode = getattr(Image, 'Transform', Image).AFFINE
    resampling = getattr(Image, 'Resampling', Image)
    samples = [tiled.transform((1, 1), affine_mode, (0, 0, x, 0, 0, y),
                               resample=resampling.BILINEAR).getpixel((0, 0)) for x, y in coords]

    assert abs((coords[1][0] - coords[0][0]) - atlas.width) < 1e-6
    assert samples[0] == samples[1]

    unsupported = {'m_FilterMode': 1, 'm_Aniso': 1, 'm_MipBias': 0,
                   'm_WrapU': 2, 'm_WrapV': 0, 'm_WrapW': 0}
    with pytest.raises(ValueError, match='Repeat'):
        presentation_assets.rasterize_vs_mesh(
            atlas, [{'vertices': [], 'uv': [], 'triangles': [], 'transform_chain': []}],
            texture_settings=unsupported)
