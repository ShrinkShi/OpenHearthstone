import hashlib
import json
from pathlib import Path

from tools.additive_shader_audit import audit, verify_sources

ROOT = Path(__file__).parents[1]
MANIFEST = json.loads((ROOT / 'manifests/native-additive-variants.json').read_text(encoding='utf-8'))
FOLDER = ROOT / 'local/compiled-shader-audit/Hero_Additive_Additive--666151408197827071'


def test_additive_audit_repeats_exactly():
    assert audit() == MANIFEST
    assert len(MANIFEST['references']) == 147
    assert sum(len(r['materials']) for r in MANIFEST['references']) == 404
    assert not MANIFEST['full_card_effects_verified']


def test_original_additive_stage_bytes_match_audit():
    assert [(b['pass_index'], b['stage'], b['blob']) for b in MANIFEST['bindings']] == [
        (0, 'progVertex', 1), (0, 'progFragment', 3), (1, 'progVertex', 4), (1, 'progFragment', 6)]
    for binding in MANIFEST['bindings']:
        assert hashlib.sha256((FOLDER / f"4-{binding['blob']}.bin").read_bytes()).hexdigest() == binding['program_sha256']
        assert hashlib.sha256((FOLDER / f"4-{binding['parameter_blob']}.parameters.bin").read_bytes()).hexdigest() == binding['parameter_sha256']


def test_original_base_tint_and_glow_color_are_different_parameters():
    layout = MANIFEST['pass_constant_byte_offsets']
    assert layout[0]['progVertex']['UnityPerMaterial']['_TintColor'] == 16
    assert layout[1]['progFragment']['UnityPerMaterial']['_Color'] == 32
    assert layout[0]['progVertex']['$Globals']['_MainLightColor'] == 96
    fragment = (FOLDER / '4-3.asm.txt').read_text(encoding='utf-8')
    assert 'mul o0.xyzw, r0.xyzw, v2.xyzw' in fragment
    assert '_sat' not in fragment
    states = MANIFEST['pass_render_states']
    assert [(s['blend']['srcBlend'], s['blend']['destBlend'], s['blend']['srcBlendAlpha'], s['blend']['destBlendAlpha']) for s in states] == [(5, 1, 5, 1), (1, 1, 1, 1)]
    assert all(s['culling'] == 0 and s['zWrite'] == 0 and s['zTest'] == 4 for s in states)


def test_all_unique_materials_resolve_to_identical_source_shader_programs():
    source = json.loads((ROOT / 'manifests/native-additive-source-bindings.json').read_text(encoding='utf-8'))
    assert verify_sources(MANIFEST) == source
    assert len(source['bindings']) == 100
    assert all(shader['stages'] == MANIFEST['bindings'] for shader in source['shaders'])
