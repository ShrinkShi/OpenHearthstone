import hashlib
import json
from pathlib import Path

from tools.additive_shader_audit import audit, verify_sources
from tools.transparent_shader_audit import refresh

ROOT = Path(__file__).parents[1]
MANIFEST = json.loads((ROOT / 'manifests/native-transparent-variants.json').read_text(encoding='utf-8'))


def test_exact_source_program_stages_and_rgb_alpha_parameters():
    result = audit('Hero_Particle_Particles_Transparent-1610204260223125505')
    assert result['bindings'] == MANIFEST['bindings']
    layouts = MANIFEST['pass_constant_byte_offsets']
    assert layouts[0]['progVertex']['UnityPerMaterial']['_TintColor'] == 32
    assert layouts[0]['progFragment']['UnityPerMaterial'] == {'_Intensity': 64, '_Alpha': 68}
    assert layouts[1]['progFragment']['UnityPerMaterial']['_Color'] == 16
    sources = json.loads((ROOT / 'manifests/native-transparent-source-bindings.json').read_text(encoding='utf-8'))
    assert verify_sources(MANIFEST) == sources
    assert len(sources['bindings']) == 65


def test_blend_cache_refresh_is_idempotent_and_property_backed():
    report = refresh()
    assert report['updated'] == 0 and report['unique_materials'] == 65
    assert len(report['materials']) == 314
    for item in report['materials']:
        blend = item['blend']
        assert blend['src_property'] == '_RgbSrcBlend'
        assert blend['dst_property'] == '_RgbDstBlend'
        assert blend['src_alpha_property'] == '_AlphaSrcBlend'
        assert blend['dst_alpha_property'] == '_AlphaDstBlend'
        data = json.loads((ROOT / 'local/runtime' / item['file']).read_text(encoding='utf-8'))
        for node in data['nodes']:
            for material in node['materials']:
                if material['source'] != item['material']:
                    continue
                floats = {p['name']: p['value'] for p in material['floats']}
                for key, default in [('src', 5), ('dst', 10), ('src_alpha', 5), ('dst_alpha', 10)]:
                    assert int(floats.get(material[key + '_property'], default)) == blend[key]


def test_existing_enabled_effect_fingerprints_remain_verified():
    index = json.loads((ROOT / 'local/runtime/native-effect-index.json').read_text(encoding='utf-8'))
    active = [e for e in index['entries'] if e.get('enabled')]
    assert {e['card_id'] for e in active} == {'CS2_025', 'CS2_062'}
    for entry in active:
        assert hashlib.sha256((ROOT / 'local/runtime' / entry['file']).read_bytes()).hexdigest() == entry['program_sha256']
