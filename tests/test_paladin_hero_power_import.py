import pytest

from tools.import_hero_power_super_spell import (
    PALADIN_AREA,
    PALADIN_AREA_PATHID,
    PALADIN_ROOT,
    merge_paladin_area_catalogs,
    paladin_area_registration,
    paladin_source_profile,
    referenced_prefabs,
)
from tools.super_spell_source import chain_source_profile


def paladin_config():
    return {
        'card_id': 'HERO_04bp', 'file': 'paladin-root.json', 'reference': PALADIN_ROOT,
        'location': 3, 'target_behavior': 6, 'start_enabled': False,
        'start_config_null': True, 'start': None,
        'missile': {'m_Enabled': 0, 'm_Prefab': {'m_FileID': 0, 'm_PathID': 0},
                    'm_ReversePrefab': {'m_FileID': 0, 'm_PathID': 0}},
        'missile_reference': '',
        'impact': {'enabled': False, 'prefab': {'prefab_null': True, 'prefab_reference': ''},
                   'ranges': [], 'game_delay_min': .5, 'game_delay_max': .5},
        'area': {
            'node': 0, 'kind': 'SuperSpell', 'source': {'pathid': '3071782026345097463'},
            'friendly': {'enabled': True, 'prefab_null': False, 'prefab_reference': PALADIN_AREA,
                         'prefab_source': {'pathid': PALADIN_AREA_PATHID}, 'use_super_location': True,
                         'location': 0, 'parent_to_location': False, 'facing': 0,
                         'delay_min': 0, 'delay_max': 0},
            'opponent': {'enabled': True, 'prefab_null': True, 'prefab_reference': ''},
        },
        'chain_enabled': True, 'chain_prefab_null': True, 'chain_reference': '',
        'chain_source': None, 'chain_available': False, 'reverse_available': False,
    }


def paladin_row():
    config = paladin_config()
    config['friendly_area_profile'] = 'friendly-area-only-reconstruction; enabled-null-chain-unresolved'
    prefab = {'reference': PALADIN_AREA, 'file': 'paladin-area.json', 'audio_file': 'paladin-area-audio.json',
              'audio_errors': []}
    return {'config': config, 'prefabs': [prefab]}


def test_chain_import_preserves_enabled_and_null_as_distinct_source_facts():
    source = chain_source_profile(
        {'m_Enabled': 1},
        {'prefab_null': True, 'prefab_reference': '', 'prefab_source': None},
    )

    assert source == {
        'chain_enabled': True, 'chain_prefab_null': True, 'chain_reference': '',
        'chain_source': None, 'chain_available': False,
    }


def test_paladin_source_profile_accepts_only_the_authored_enabled_null_chain():
    assert paladin_source_profile({'reference': PALADIN_ROOT, 'nodes': [{}] * 13}, paladin_config()) == \
        'friendly-area-only-reconstruction; enabled-null-chain-unresolved'

    for field, value in [('chain_enabled', False), ('chain_prefab_null', False),
                         ('chain_reference', 'Unexpected.prefab:' + '1' * 32),
                         ('chain_available', True)]:
        changed = paladin_config()
        changed[field] = value
        with pytest.raises(ValueError, match='enabled-null Chain source state changed'):
            paladin_source_profile({'reference': PALADIN_ROOT, 'nodes': [{}] * 13}, changed)


def test_paladin_import_keeps_only_the_verified_friendly_area_prefab_reference():
    assert referenced_prefabs(paladin_config()) == {PALADIN_AREA}

    changed = paladin_config()
    changed['area']['friendly']['prefab_reference'] = ''
    with pytest.raises(ValueError, match='unresolved friendly area prefab'):
        referenced_prefabs(changed)


def test_paladin_area_registration_merges_config_and_prefab_without_losing_existing_records():
    existing_config = {'programs': [{'file': 'existing.json', 'reference': 'existing'}],
                       'full_super_spell_restored': False}
    existing_prefabs = {'prefabs': [{'reference': 'existing-prefab', 'file': 'existing-area.json'}]}
    config_catalog, prefab_catalog = merge_paladin_area_catalogs(
        existing_config, existing_prefabs, [paladin_row()])

    record = next(row for row in config_catalog['programs'] if row['reference'] == PALADIN_ROOT)
    registered = next(row for row in prefab_catalog['prefabs'] if row['reference'] == PALADIN_AREA)
    assert record['file'] == 'paladin-root.json'
    assert record['config']['node'] == 0
    assert record['config']['friendly']['prefab_reference'] == PALADIN_AREA
    assert registered['file'] == 'paladin-area.json'
    assert registered['audio_file'] == 'paladin-area-audio.json'
    assert any(row['file'] == 'existing.json' for row in config_catalog['programs'])
    assert any(row['reference'] == 'existing-prefab' for row in prefab_catalog['prefabs'])


def test_paladin_area_registration_fails_closed_if_the_child_prefab_was_not_imported():
    row = paladin_row()
    row['prefabs'] = []
    with pytest.raises(ValueError, match='FriendlyArea prefab was not imported'):
        paladin_area_registration(row)
