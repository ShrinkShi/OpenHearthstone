import json
from pathlib import Path

from tools.additive_shader_audit import audit, verify_sources
from tools.commonfx_none_audit import FOLDER, KEYWORDS
from tools.transparent_shader_audit import refresh

ROOT = Path(__file__).parents[1]


def test_exact_commonfx_none_programs_and_material_keywords():
    manifest = json.loads((ROOT / 'manifests/native-commonfx-none-variants.json').read_text(encoding='utf-8'))
    result = audit(FOLDER, KEYWORDS)
    assert result['bindings'] == manifest['bindings']
    assert [(b['blob'], b['parameter_blob']) for b in manifest['bindings']] == [
        (114, 0), (1202, 1151), (2240, 2239), (2242, 2241)]
    assert manifest['keywords'] == sorted(KEYWORDS)
    assert len(manifest['references']) == 22
    assert sum(len(r['materials']) for r in manifest['references']) == 55
    assert manifest['full_card_effects_verified'] is False


def test_commonfx_none_actual_shader_dependencies():
    manifest = json.loads((ROOT / 'manifests/native-commonfx-none-variants.json').read_text(encoding='utf-8'))
    sources = json.loads((ROOT / 'manifests/native-commonfx-none-source-bindings.json').read_text(encoding='utf-8'))
    assert verify_sources(manifest) == sources
    assert len(sources['bindings']) == 12


def test_commonfx_none_live_blends_repeat_without_changes():
    report = refresh('FX/CommonFX', KEYWORDS)
    assert report['updated'] == 0 and report['unique_materials'] == 12
    assert len(report['materials']) == 55
    for material in report['materials']:
        blend = material['blend']
        assert blend['src_property'] == blend['src_alpha_property'] == '_SrcBlendOutline'
        assert blend['dst_property'] == blend['dst_alpha_property'] == '_DstBlendOutline'
