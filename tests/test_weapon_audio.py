import json
from pathlib import Path

from tools.additive_shader_audit import audit as additive_audit
from tools.weapon_audio_evidence import batches
from tools.weapon_audio import parsed_shader_name

ROOT = Path(__file__).parents[1]


def material_value(rows, name):
    values = [row['value'] for row in rows if row['name'] == name]
    assert len(values) == 1
    return values[0]


def test_shader_name_comes_from_parsed_form_when_serialized_name_is_empty():
    assert parsed_shader_name({'m_Name': '', 'm_ParsedForm': {'m_Name': 'Hero/Additive/Additive'}}) == \
        'Hero/Additive/Additive'


def test_authority_emits_unambiguous_weapon_audio_anchors_for_both_seats():
    rows = batches()
    assert len(rows) == 8
    for row in rows:
        before, after = row['before'], row['after']
        events = after['events']
        weapon = before['players'][row['owner']]['weapon']
        if row['kind'] in ('equip', 'replace'):
            summons = [e for e in events if e['type'] == 'Summon' and e.get('card_type') == 7]
            assert len(summons) == 1 and summons[0]['visual']['card_id'] == 'CS2_082'
            assert 'card_id' not in summons[0]  # Production uses the public entity, not a missing field.
            if row['kind'] == 'replace':
                assert sum(e['type'] == 'Death' and e['target'] == weapon['entity'] for e in events) == 1
        else:
            hits = [e for e in events if e['type'] == 'Hit' and e['source'] == e['target'] == weapon['entity']]
            assert len(hits) == 1 and hits[0]['amount'] == 1
            assert hits[0]['visual']['durability'] == weapon['durability'] - 1
            assert any(e['type'] == 'Attack' and e['source'] == before['players'][row['owner']]['hero']['entity'] for e in events)
            assert sum(e['type'] == 'Death' and e['target'] == weapon['entity'] for e in events) == (row['kind'] == 'break')
    for index in range(0, len(rows), 2):
        assert rows[index]['after']['public_hash'] == rows[index + 1]['after']['public_hash']


def test_summon_glow_exports_exact_additive_shader_and_material_evidence():
    runtime = json.loads((ROOT / 'local/runtime/weapon-audio.json').read_text(encoding='utf-8'))
    manifest = json.loads((ROOT / 'manifests/original-weapon-audio.json').read_text(encoding='utf-8'))
    baseline = additive_audit()
    assert runtime == manifest

    entries = [row for row in runtime['entries'] if row['slot'] in (41, 42)]
    assert [row['slot'] for row in entries] == [41, 42]
    for row, texture_name in zip(entries, ('WeaponSummonBlue', 'WeaponSummonRed')):
        glow = row['summon_glow']
        assert glow['profile'] == 'source-additive-tint-v2'
        assert glow['shader_name'] == baseline['shader']['name'] == 'Hero/Additive/Additive'
        assert glow['shader_known'] and glow['compiled_source_verified']
        assert glow['shader_source']['pathid'] == baseline['shader']['pathid']
        assert glow['shader_source']['cab'] != baseline['shader']['cab']
        verification = glow['shader_verification']
        assert verification['status'] == 'exact-audit-match'
        assert verification['baseline_shader'] == baseline['shader']
        assert verification['family'] == 16 and verification['platform'] == 4
        assert verification['bindings'] == baseline['bindings']
        assert verification['pass_render_states'] == baseline['pass_render_states']
        assert verification['pass_constant_byte_offsets'] == baseline['pass_constant_byte_offsets']
        assert verification['full_card_effects_verified'] is False

        source_material = glow['material_definition']
        assert source_material['name'] == row['summon_glow']['material']
        assert source_material['source'] == glow['material_source']
        assert source_material['shader'] == glow['shader_name']
        assert source_material['keywords'] == []
        assert (source_material['src'], source_material['dst'], source_material['src_alpha'],
                source_material['dst_alpha']) == (5, 1, 5, 1)
        assert source_material['render_queue'] == 3000
        assert source_material['passes'] == [
            {'index': 0, 'light_mode': 'SRPDefaultUnlit'},
            {'index': 1, 'light_mode': 'GlowTransparent'},
        ]
        main_tex = [texture for texture in source_material['textures'] if texture['name'] == '_MainTex']
        assert len(main_tex) == 1 and main_tex[0]['path'] == glow['texture_file']
        assert main_tex[0]['scale'] == {'x': 1.0, 'y': 1.0}
        assert main_tex[0]['offset'] == {'x': 0.0, 'y': 0.0}
        assert glow['texture_name'] == texture_name
        assert glow['texture_alpha_opaque'] is True
        assert glow['texture_alpha'] == {'min': 255, 'max': 255, 'pixels': 512 * 512}
        tint = material_value(source_material['colors'], '_TintColor')
        assert abs(tint['a'] - glow['tint_alpha']) < 1e-6
        assert abs(tint['a'] - 128 / 255) < 1e-6
        assert {name: material_value(source_material['floats'], name)
                for name in ('_LightingBlend', '_GlowIntensity', '_GlowAddMul')} == {
                    '_LightingBlend': 0.0, '_GlowIntensity': 0.0, '_GlowAddMul': 0.0}
