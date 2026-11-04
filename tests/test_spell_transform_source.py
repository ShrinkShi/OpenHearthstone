import json
from pathlib import Path
import pytest

from tools.importer import Importer
from tools.spell_transform_source import recover, recover_effects

ROOT = Path(__file__).parents[1]


def test_original_spell_transform_catalog_matches_all_actual_sources():
    catalog = json.loads((ROOT / 'manifests/original-spell-transform-source.json').read_text(encoding='utf-8'))
    assert not catalog['full_spell_controller_restored']
    assert len(catalog['programs']) == 205
    importer = Importer()
    for record in catalog['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program) == record['transforms'] == program['spell_transforms']
        assert len(record['transforms']) == 1
        data = record['transforms'][0]
        assert data['node'] == 0 and data['facing'] == 0 and data['source_null']
        assert data['raw']['m_Location'] == data['location']


def test_attack_placement_is_authored_source_auto_without_forced_parent_or_orientation():
    catalog = json.loads((ROOT / 'manifests/original-spell-transform-source.json').read_text(encoding='utf-8'))
    attacks = [p['transforms'][0] for p in catalog['programs'] if p['transforms'][0]['kind'] == 'ActorAttackSpell']
    assert len(attacks) == 4
    assert all(t['location'] == 2 and not t['parent_to_location'] and not t['location_transform'] for t in attacks)


def test_original_effect_placements_match_detached_and_nested_source_spell_components():
    catalog = json.loads((ROOT / 'manifests/original-effect-transform-source.json').read_text(encoding='utf-8'))
    assert not catalog['full_spell_controller_restored']
    paths = sorted((ROOT / 'local/runtime').glob('*.native-effect.json'))
    assert {p.name for p in paths} == {record['file'] for record in catalog['programs']}
    importer = Importer()
    nested = 0
    for record in catalog['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover_effects(importer, program) == record['transforms'] == program['spell_transforms']
        for data in record['transforms']:
            assert 0 <= data['node'] < len(program['nodes'])
            assert data['location'] == data['raw']['m_Location']
            assert data['facing'] == data['raw']['m_Facing']
            nested += data['node'] != 0
    assert nested > 0


def test_effect_hierarchy_mismatch_cannot_assign_placement_to_a_guessed_node():
    program = json.loads((ROOT / 'local/runtime/1438c953f1f357de128e06d3.native-effect.json').read_text(encoding='utf-8'))
    program['nodes'][0]['name'] = 'changed'
    with pytest.raises(ValueError, match='Source effect hierarchy changed'):
        recover_effects(Importer(), program)
