import json
from pathlib import Path

from tools.additive_shader_audit import audit, verify_sources
from tools.color_alpha_shader_audit import FOLDER

ROOT = Path(__file__).parents[1]


def test_original_luminance_mask_program_and_constants():
    manifest = json.loads((ROOT / 'manifests/native-color-alpha-variants.json').read_text(encoding='utf-8'))
    result = audit(FOLDER)
    assert result['bindings'] == manifest['bindings']
    assert manifest['family'] == 18
    base = manifest['pass_constant_byte_offsets'][0]
    assert base['progVertex']['UnityPerMaterial'] == {
        '_MainTex_ST': 0, '_TintColor': 16, '_Intensity': 64, '_LightingBlend': 72}
    assert base['progFragment']['UnityPerMaterial'] == {'_AlphaIntensity': 68}
    assert manifest['full_card_effects_verified'] is False


def test_actual_original_material_shader_dependencies():
    manifest = json.loads((ROOT / 'manifests/native-color-alpha-variants.json').read_text(encoding='utf-8'))
    sources = json.loads((ROOT / 'manifests/native-color-alpha-source-bindings.json').read_text(encoding='utf-8'))
    assert verify_sources(manifest) == sources
    assert len(manifest['references']) == 6
    assert sum(len(r['materials']) for r in manifest['references']) == 9
    assert len(sources['bindings']) > 0
