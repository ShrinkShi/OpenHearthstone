import json
from pathlib import Path

import pytest

from tools.import_hero_power_super_spell import (
    referenced_prefabs,
    priest_source_profile,
    resolve_card_effect_reference,
    source_visual_duration,
    warlock_source_duration,
    rogue_source_profile,
    ROGUE_ROOT,
    ROGUE_PROFILE,
    demon_hunter_source_profile,
    DEMON_HUNTER_ROOT,
    DEMON_HUNTER_START,
    DEMON_HUNTER_RUNTIME_PROFILE,
    DEMON_HUNTER_PARTICLE_NODES,
    native_effect_enabled,
    update_effect_index,
    DEMON_HUNTER_FSMS,
    hunter_impact_camera_shake_profile,
    HUNTER_IMPACT,
    HUNTER_IMPACT_PATHID,
    warrior_impact_camera_shake_profile,
    WARRIOR_IMPACT,
    WARRIOR_IMPACT_PATHID,
    WARRIOR_IMPACT_FSM_PATHID,
    WARRIOR_IMPACT_CAMERA_SHAKER_PATHID,
)


def test_card_effect_reference_preserves_direct_spell_path():
    direct = 'Direct.prefab:' + '1' * 32
    definition = {
        'm_PlayEffectDef': {'m_SpellPath': direct},
        'm_SubSpellEffectDefs': [{'m_SpellPath': 'Nested.prefab:' + '2' * 32}],
    }

    assert resolve_card_effect_reference('HERO_TEST', definition) == direct


def test_card_effect_reference_uses_the_unique_nested_spell_path():
    nested = 'Nested.prefab:' + '2' * 32
    definition = {
        'm_PlayEffectDef': {'m_SpellPath': ''},
        'm_SubSpellEffectDefs': [
            {'m_SpellPath': ''},
            {'m_SpellPath': nested},
            None,
        ],
    }

    assert resolve_card_effect_reference('HERO_01bp', definition) == nested


def test_card_effect_reference_reports_missing_paths():
    definition = {
        'm_PlayEffectDef': {},
        'm_SubSpellEffectDefs': [{'m_SpellPath': ''}, None],
    }

    with pytest.raises(
        ValueError,
        match=r'HERO_TEST has no CardDef SuperSpell reference.*m_SubSpellEffectDefs/\*/m_SpellPath',
    ):
        resolve_card_effect_reference('HERO_TEST', definition)


def test_card_effect_reference_reports_all_ambiguous_nested_paths():
    first = 'First.prefab:' + '1' * 32
    second = 'Second.prefab:' + '2' * 32
    definition = {
        'm_SubSpellEffectDefs': [
            {'m_SpellPath': first},
            {'m_SpellPath': ''},
            {'m_SpellPath': second},
        ],
    }

    with pytest.raises(ValueError) as error:
        resolve_card_effect_reference('HERO_TEST', definition)

    assert 'm_SubSpellEffectDefs/0/m_SpellPath=' + repr(first) in str(error.value)
    assert 'm_SubSpellEffectDefs/2/m_SpellPath=' + repr(second) in str(error.value)


def test_disabled_missile_does_not_require_a_projectile_prefab():
    config = {
        'card_id': 'HERO_06bp',
        'missile': {'m_Enabled': 0},
        'missile_reference': '',
        'impact': {
            'enabled': True,
            'prefab': {'prefab_null': False, 'prefab_reference': 'Impact.prefab:' + '1' * 32},
        },
        'area': {
            'friendly': {'enabled': True, 'prefab_null': True, 'prefab_reference': ''},
            'opponent': {'enabled': True, 'prefab_null': True, 'prefab_reference': ''},
        },
    }

    assert referenced_prefabs(config) == {'Impact.prefab:' + '1' * 32}


def test_enabled_missile_with_missing_prefab_is_not_silently_omitted():
    config = {
        'card_id': 'HERO_TEST',
        'missile': {'m_Enabled': 1},
        'missile_reference': '',
        'impact': {'enabled': False},
        'area': {},
    }

    with pytest.raises(ValueError, match='enabled missile without a source prefab'):
        referenced_prefabs(config)


def test_enabled_area_with_unresolved_prefab_is_not_silently_omitted():
    config = {
        'card_id': 'HERO_TEST',
        'missile': {'m_Enabled': 0},
        'impact': {'enabled': False},
        'area': {
            'friendly': {'enabled': True, 'prefab_null': False, 'prefab_reference': ''},
        },
    }

    with pytest.raises(ValueError, match='unresolved friendly area prefab'):
        referenced_prefabs(config)


def priest_config():
    return {
        'card_id': 'HERO_09bp', 'location': 2, 'target_behavior': 0,
        'start_enabled': True, 'start_config_null': False,
        'start': {'enabled': True, 'prefab_null': False,
                  'prefab_source': {'pathid': '2826725205073506016'},
                  'prefab_reference': 'ReuseFX_Holy_Idle_HeroPower.prefab:' + '8' * 32,
                  'use_super_location': True, 'death_after_missiles': True,
                  'adjust_rotation': False, 'rotation': {'x': 0.0, 'y': 0.0, 'z': 0.0}},
        'missile': {'m_Enabled': 0, 'm_Prefab': {'m_FileID': 0, 'm_PathID': 0},
                    'm_ReversePrefab': {'m_FileID': 0, 'm_PathID': 0}},
        'missile_reference': '', 'chain_available': False, 'reverse_available': False,
        'impact': {'enabled': True, 'prefab': {'prefab_null': False,
                    'prefab_source': {'pathid': '-8365228841793198537'},
                    'prefab_reference': 'ReuseFX_Holy_Impact_01.prefab:' + '7' * 32},
                   'ranges': [], 'use_super_location': False, 'location': 6,
                   'parent_to_location': False, 'delay_min': 0, 'delay_max': 0,
                   'spawn_offset': 0, 'game_delay_min': .5, 'game_delay_max': .5,
                   'adjust_rotation': False, 'rotation': {'x': 0.0, 'y': 0.0, 'z': 0.0}},
        'area': {'friendly': {'enabled': True, 'prefab_null': True},
                 'opponent': {'enabled': True, 'prefab_null': True}},
    }


def test_priest_source_profile_accepts_only_targeted_start_and_impact_route():
    priest_source_profile({
        'reference': 'Holy_Targeted_PriestHeroPower_FX.prefab:' + '9a1243a1e217073439fc02475eba1605',
        'nodes': [{} for _ in range(22)],
    }, priest_config())


@pytest.mark.parametrize('mutation, message', [
    (lambda c: c.update(start=None), 'Start source profile changed'),
    (lambda c: c['missile'].update(m_Enabled=1), 'disabled Missile must remain authored-null'),
    (lambda c: c['impact'].update(game_delay_min=.4), 'Impact source profile changed'),
    (lambda c: c['area']['friendly'].update(prefab_null=False), 'enabled-null friendly area changed'),
])
def test_priest_source_profile_fails_closed_on_branch_changes(mutation, message):
    config = priest_config()
    mutation(config)
    with pytest.raises(ValueError, match=message):
        priest_source_profile({
            'reference': 'Holy_Targeted_PriestHeroPower_FX.prefab:' + '9a1243a1e217073439fc02475eba1605',
            'nodes': [{} for _ in range(22)],
        }, config)


def test_enabled_start_with_null_prefab_is_not_silently_omitted():
    config = priest_config()
    config['start']['prefab_null'] = True

    with pytest.raises(ValueError, match='enabled Start without a source prefab'):
        referenced_prefabs(config)


def test_priest_start_and_impact_are_imported_as_independent_prefabs():
    config = priest_config()

    assert referenced_prefabs(config) == {
        config['start']['prefab_reference'], config['impact']['prefab']['prefab_reference']}


def demon_hunter_source_fixture():
    machines = []
    for owner, profile in DEMON_HUNTER_FSMS.items():
        states = [
            {'name': name, 'actions': [{'name': action} for action in actions],
             'transitions': [{'event': event, 'state': target} for event, target in transitions]}
            for name, (actions, transitions) in profile['states'].items()
        ]
        machines.append({'owner_known': True, 'owner': owner, 'start': 'StartState',
                         'events': [{'event': event, 'state': state} for event, state in profile['events']],
                         'states': states})
    texture_profiles = iter([
        (24, 'cd7ba7885a2d12ab34d1dabf.png', '-8039451771726812956'),
        (25, 'cd7ba7885a2d12ab34d1dabf.png', '-8039451771726812956'),
        (26, 'db598f55f4e4c96b7e32d722.png', '3506897862555594978'),
    ])
    scale_profiles = {
        (1, 'Minion'): [(6, (.7, .85, .85)), (10, (.65, .75, .75))],
        (1, 'Weapon/HP'): [(6, (.7, .7, .7)), (10, (.65, .65, .65))],
        (12, 'Weapon/HP'): [(14, (1.05, 1.05, .75))],
    }
    scale_indexes = {}
    for machine in machines:
        owner = machine['owner']
        for state in machine['states']:
            for action in state['actions']:
                if action['name'] == 'SetMaterialTexture':
                    node, path, pathid = next(texture_profiles)
                    action['params'] = [
                        {'name': 'gameObject', 'kind': 20, 'node': node, 'none_known': True, 'is_none': False, 'variable': ''},
                        {'name': 'materialIndex', 'kind': 16, 'number': 0, 'none_known': True, 'is_none': False, 'variable': ''},
                        {'name': 'material', 'kind': 32, 'asset_type': 'Material', 'object_null': True, 'variable': ''},
                        {'name': 'namedTexture', 'kind': 18, 'text': '_MainTex', 'variable': ''},
                        {'name': 'texture', 'kind': 33, 'asset_type': 'Texture', 'object_null': False, 'variable': '',
                         'asset_path': path, 'asset_source': {'cab': 'CAB-572cbaceb2612afe464bd92ebbf0736a',
                             'pathid': pathid, 'type': 'Texture2D', 'bundle': 'essential_base_global-texture-2.unity3d'}},
                    ]
                elif action['name'] == 'SetScale':
                    key = (owner, state['name'])
                    index = scale_indexes.get(key, 0)
                    node, scale = scale_profiles[key][index]
                    scale_indexes[key] = index + 1
                    action['params'] = [
                        {'name': 'gameObject', 'kind': 20, 'node': node, 'none_known': True, 'is_none': False, 'variable': ''},
                        {'name': 'vector', 'kind': 28, 'none_known': True, 'is_none': True, 'variable': ''},
                        *[{'name': name, 'kind': 15, 'number': value, 'none_known': True, 'is_none': False, 'variable': ''}
                          for name, value in zip(('x', 'y', 'z'), scale)],
                        {'name': 'everyFrame', 'kind': 1, 'number': 0, 'variable': ''},
                        {'name': 'lateUpdate', 'kind': 1, 'number': 0, 'variable': ''},
                    ]
    node_names = {0: 'ReuseFX_Fel_DemonHunter_BuffImpact_Small_Super_WithIdle',
                  1: 'ReuseFX_Idle_DemonHunter_Small', 12: 'ReuseFX_BuffImpact_DemonHunter_Small'}
    program = {
        'reference': DEMON_HUNTER_ROOT,
        'nodes': [{'name': node_names.get(index, 'node-' + str(index)), 'parent': -1,
                   'particle': {'restored': True} if index in DEMON_HUNTER_PARTICLE_NODES else None}
                  for index in range(42)],
        'machines': machines,
        'animations': [{} for _ in range(3)], 'captures': [{} for _ in range(3)],
        'unknown_actions': ['AnimatorPlayAction', 'AudioPlayClipAction', 'AudioPlaythroughAction',
                            'SetMaterialTexture', 'SetScale'],
    }
    config = {
        'card_id': 'HERO_10bp', 'file': '77a947e5d6ef3ec34b9f2f93.hero-power-source.json',
        'reference': DEMON_HUNTER_ROOT, 'target_behavior': 0,
        'start_enabled': True, 'start_config_null': False,
        'start': {'enabled': True, 'prefab_null': False, 'prefab_reference': DEMON_HUNTER_START,
                  'prefab_source': {'pathid': '-7316641151859032915'}, 'use_super_location': True,
                  'death_after_missiles': True, 'adjust_rotation': False,
                  'rotation': {'x': 0.0, 'y': 0.0, 'z': 0.0}},
        'missile': {'m_Enabled': 0, 'm_Prefab': {'m_FileID': 0, 'm_PathID': 0},
                    'm_ReversePrefab': {'m_FileID': 0, 'm_PathID': 0}},
        'missile_reference': '', 'reverse_available': False,
        'impact': {'enabled': True, 'prefab': {'prefab_null': False, 'prefab_reference': 'Impact.prefab:' + '1' * 32,
                    'prefab_source': {'pathid': '-618183129397526064'}}, 'ranges': [],
                   'use_super_location': False, 'location': 6, 'parent_to_location': False,
                   'delay_min': 0, 'delay_max': 0, 'spawn_offset': 0,
                   'game_delay_min': .5, 'game_delay_max': .5, 'adjust_rotation': False,
                   'rotation': {'x': 0.0, 'y': 0.0, 'z': 0.0}},
        'area': {slot: {'enabled': True, 'prefab_null': True, 'prefab_reference': ''}
                 for slot in ('friendly', 'opponent')},
        'chain_enabled': True, 'chain_prefab_null': True, 'chain_reference': '', 'chain_available': False,
    }
    return program, config


def test_demon_hunter_source_profile_opens_only_the_bounded_runtime_route():
    program, config = demon_hunter_source_fixture()

    assert demon_hunter_source_profile(program, config) == DEMON_HUNTER_RUNTIME_PROFILE
    config['source_root_profile'] = DEMON_HUNTER_RUNTIME_PROFILE
    assert native_effect_enabled(config)
    assert config['missile']['m_Enabled'] == 0
    assert config['impact']['game_delay_min'] == config['impact']['game_delay_max'] == .5
    assert config['chain_enabled'] and config['chain_prefab_null'] and not config['chain_available']


def test_demon_hunter_import_writes_enabled_index_only_after_source_profile_passes(tmp_path):
    program, config = demon_hunter_source_fixture()
    index_path = tmp_path / 'native-effect-index.json'
    config['source_root_profile'] = demon_hunter_source_profile(program, config)

    update_effect_index(index_path, {'HERO_10bp'},
                        [{'config': config, 'source_duration': .1}], [])
    entry = json.loads(index_path.read_text(encoding='utf-8'))['entries'][0]
    assert entry['card_id'] == 'HERO_10bp'
    assert entry['enabled'] is True
    assert entry['duration'] == .1

    config['source_root_profile'] = 'source-audit-only'
    update_effect_index(index_path, {'HERO_10bp'},
                        [{'config': config, 'source_duration': .1}], [])
    entry = json.loads(index_path.read_text(encoding='utf-8'))['entries'][0]
    assert entry['enabled'] is False


@pytest.mark.parametrize('profile', [None, '', 'source-audit-only', 'source-backed HERO_10bp self-hero Buff route; no-History Impact is immediate per source IL; extra'])
def test_demon_hunter_index_stays_closed_without_the_exact_source_profile(profile):
    assert not native_effect_enabled({'card_id': 'HERO_10bp', 'source_root_profile': profile})


def test_demon_hunter_runtime_gate_does_not_change_other_index_rules():
    assert native_effect_enabled({'card_id': 'HERO_07bp', 'missile': {'m_Enabled': 0}})
    assert not native_effect_enabled({'card_id': 'HERO_06bp', 'missile': {'m_Enabled': 0}})


def test_source_only_import_can_preserve_the_production_index_byte_for_byte(tmp_path):
    path = tmp_path / 'native-effect-index.json'
    original = b'{\n  "entries": [{"card_id": "HERO_10bp", "enabled": false}]\n}\n'
    path.write_bytes(original)

    assert update_effect_index(path, {'HERO_10bp'}, [], [], preserve_existing=True) is False
    assert path.read_bytes() == original


@pytest.mark.parametrize('mutation, message', [
    (lambda p, c: p.update(reference='Changed.prefab'), 'root identity or node graph'),
    (lambda p, c: p['nodes'][12].update(parent=0), 'source/target layer roots'),
    (lambda p, c: p['nodes'][1].update(parent=0), 'source/target layer roots'),
    (lambda p, c: p['machines'].pop(), 'root runtime profile'),
    (lambda p, c: p['nodes'][7].update(particle={'restored': False}), 'root runtime profile'),
    (lambda p, c: p['nodes'][0].update(particle={'restored': True}), 'root runtime profile'),
    (lambda p, c: p['nodes'][7].update(particle=None), 'root runtime profile'),
    (lambda p, c: p['nodes'][0].pop('particle'), 'root runtime profile'),
    (lambda p, c: p['machines'][0]['states'][3]['actions'].pop(), 'root FSM profile changed'),
    (lambda p, c: c['start'].update(prefab_reference='ChangedStart.prefab'), 'Start source profile'),
    (lambda p, c: c['missile'].update(m_Enabled=1), 'Missile must remain authored-disabled/null'),
    (lambda p, c: c['impact'].update(game_delay_min=.4), 'Impact source profile'),
    (lambda p, c: c['area']['friendly'].update(prefab_null=False), 'enabled-null friendly area'),
    (lambda p, c: c.update(chain_available=True), 'enabled-null Chain source state'),
])
def test_demon_hunter_source_profile_fails_closed_on_source_changes(mutation, message):
    program, config = demon_hunter_source_fixture()
    mutation(program, config)

    with pytest.raises(ValueError, match=message):
        demon_hunter_source_profile(program, config)


def test_demon_hunter_source_profile_checks_direct_texture_and_scale_inputs():
    program, config = demon_hunter_source_fixture()
    texture = next(action for machine in program['machines'] if machine['owner'] == 12
                   for state in machine['states'] if state['name'] == 'Hero'
                   for action in state['actions'] if action['name'] == 'SetMaterialTexture')
    texture['params'][-1]['asset_path'] = 'tampered.png'
    with pytest.raises(ValueError, match='SetMaterialTexture direct parameter profile'):
        demon_hunter_source_profile(program, config)

    program, config = demon_hunter_source_fixture()
    scale = next(action for machine in program['machines'] if machine['owner'] == 1
                 for state in machine['states'] if state['name'] == 'Minion'
                 for action in state['actions'] if action['name'] == 'SetScale')
    scale['params'][2]['number'] = .71
    with pytest.raises(ValueError, match='SetScale direct parameter profile'):
        demon_hunter_source_profile(program, config)

    program, config = demon_hunter_source_fixture()
    texture = next(action for machine in program['machines'] if machine['owner'] == 12
                   for state in machine['states'] if state['name'] == 'Hero'
                   for action in state['actions'] if action['name'] == 'SetMaterialTexture')
    texture['params'][0]['property_data'] = {'values': []}
    with pytest.raises(ValueError, match='recursive property_data'):
        demon_hunter_source_profile(program, config)


def test_demon_hunter_raw_source_and_config_still_pass_strict_import_profile():
    root = Path(__file__).resolve().parents[1]
    runtime = root / 'local/runtime'
    program = json.loads((runtime / '77a947e5d6ef3ec34b9f2f93.hero-power-source.json').read_text(encoding='utf-8'))
    catalog = json.loads((runtime / 'native-super-spell.json').read_text(encoding='utf-8'))
    config = next(row for row in catalog['programs'] if row.get('card_id') == 'HERO_10bp')
    assert demon_hunter_source_profile(program, config) == DEMON_HUNTER_RUNTIME_PROFILE


def hunter_impact_source_program():
    invoke = {'name': 'InvokeMethod', 'params': [
        {'name': 'gameObject', 'kind': 20, 'node': 0},
        {'name': 'behaviour', 'text': 'CameraShaker'},
        {'name': 'methodName', 'text': 'StartShake'},
        {'name': 'delay', 'number': .15},
        {'name': 'repeating', 'number': 0},
        {'name': 'repeatDelay', 'number': 1},
        {'name': 'cancelOnExit', 'number': 0},
    ]}
    actions = [
        {'name': 'SpellFinishAction'}, {'name': 'Wait'},
        *[{'name': 'ParticlePlayAction'} for _ in range(5)], invoke,
        {'name': 'AnimateFloat'}, {'name': 'SetMaterialFloat'}, {'name': 'AudioPlayRandomClipAction'},
    ]
    nodes = [{'name': 'ReuseFX_Arrow_Impact_01'}] + [{} for _ in range(20)]
    nodes[0]['camera_shaker'] = {
        'restored': True, 'source': {'pathid': '-5851296759909699064'},
        'amount': {'x': .2, 'y': .2, 'z': .2},
        'curve': [{'time': 0, 'value': 1}, {'time': .25, 'value': 0}],
        'hold': False, 'hold_at_sec': 0, 'is_override': False,
    }
    return {
        'reference': HUNTER_IMPACT, 'source': {'pathid': HUNTER_IMPACT_PATHID},
        'unknown_actions': ['AudioPlayRandomClipAction', 'InvokeMethod'], 'nodes': nodes,
        'machines': [{'owner_known': True, 'owner': 0, 'start': 'State 1', 'states': [
            {'name': 'DeathState 2', 'actions': actions,
             'transitions': [{'event': 'FINISHED', 'state': 'DeathState 3'}]},
        ]}],
    }


def test_hunter_impact_camera_shake_profile_is_exact_source_and_owner_bound():
    assert hunter_impact_camera_shake_profile(hunter_impact_source_program()) == \
        'source-impact-root-use-owner; delayed-one-shot-camera-shake'


@pytest.mark.parametrize('mutation, message', [
    (lambda p: p['machines'][0]['states'][0]['actions'][7]['params'][0].update(node=1), 'InvokeMethod source profile'),
    (lambda p: p['machines'][0]['states'][0]['actions'][7]['params'][3].update(number=.2), 'InvokeMethod source profile'),
    (lambda p: p['nodes'][0]['camera_shaker']['curve'][1].update(time=.3), 'CameraShaker source values'),
    (lambda p: p['nodes'][0].pop('camera_shaker'), 'CameraShaker source values'),
])
def test_hunter_impact_camera_shake_profile_rejects_changed_source(mutation, message):
    program = hunter_impact_source_program()
    mutation(program)

    with pytest.raises(ValueError, match=message):
        hunter_impact_camera_shake_profile(program)


def warrior_impact_source_program():
    invoke = {'name': 'InvokeMethod', 'params': [
        {'name': 'gameObject', 'kind': 20, 'node': 0},
        {'name': 'behaviour', 'text': 'CameraShaker'},
        {'name': 'methodName', 'text': 'StartShake'},
        {'name': 'delay', 'number': .1},
        {'name': 'repeating', 'number': 0},
        {'name': 'repeatDelay', 'number': 1},
        {'name': 'cancelOnExit', 'number': 0},
    ]}
    action_names = {
        'State 1': [],
        'DeathState': ['SpellStateAction', 'ParticlePlayAction', 'ParticlePlayAction',
                       'Wait', 'AudioPlayClipAction'],
        'NoneState': ['SpellStateAction'],
        'DeathState3': ['SpellFinishAction', 'Wait'],
        'DeathState2': ['ParticlePlayAction', 'ParticlePlayAction', 'ParticlePlayAction', 'InvokeMethod'],
    }
    next_state = {'DeathState': 'DeathState2', 'DeathState2': 'DeathState3',
                  'DeathState3': 'NoneState'}
    states = []
    for name, actions in action_names.items():
        rows = [dict(name=action) for action in actions]
        if name == 'DeathState2':
            rows[-1] = invoke
        states.append(dict(name=name, actions=rows,
                           transitions=([dict(event='FINISHED', state=next_state[name])]
                                        if name in next_state else [])))
    nodes = [{} for _ in range(7)]
    nodes[0]['camera_shaker'] = {
        'restored': True, 'source': {'pathid': WARRIOR_IMPACT_CAMERA_SHAKER_PATHID},
        'amount': {'x': .1, 'y': .1, 'z': .1},
        'curve': [{'time': 0, 'value': 1}, {'time': .6, 'value': 0}],
        'hold': False, 'hold_at_sec': 0, 'is_override': False,
    }
    return {
        'reference': WARRIOR_IMPACT,
        'source': {'pathid': WARRIOR_IMPACT_PATHID},
        'unknown_actions': ['AudioPlayClipAction', 'InvokeMethod'],
        'nodes': nodes,
        'machines': [{
            'owner_known': True, 'owner': 0, 'start': 'State 1',
            'source': {'pathid': WARRIOR_IMPACT_FSM_PATHID},
            'events': [{'event': 'Death', 'state': 'DeathState'},
                       {'event': 'None', 'state': 'NoneState'}],
            'states': states,
        }],
    }


def test_warrior_impact_camera_shake_profile_is_exact_source_and_node_bound():
    assert warrior_impact_camera_shake_profile(warrior_impact_source_program()) == \
        'source-impact-root-use-owner; delayed-one-shot-camera-shake'


@pytest.mark.parametrize('mutation, message', [
    (lambda p: p.update(reference='Changed.prefab'), 'Impact identity or source action inventory'),
    (lambda p: p['machines'][0].update(owner=1), 'reviewed owner-0 FSM'),
    (lambda p: p['machines'][0]['states'][4]['actions'][3]['params'][0].update(node=1),
     'InvokeMethod source profile'),
    (lambda p: p['machines'][0]['states'][4]['actions'][3]['params'][3].update(number=.2),
     'InvokeMethod source profile'),
    (lambda p: p['nodes'][0]['camera_shaker']['amount'].update(x=.2), 'CameraShaker source values'),
    (lambda p: p['nodes'][0]['camera_shaker']['curve'][1].update(time=.5), 'CameraShaker source values'),
    (lambda p: p['nodes'][0]['camera_shaker']['source'].update(pathid='1'), 'CameraShaker source values'),
    (lambda p: p['nodes'][0].pop('camera_shaker'), 'CameraShaker on node 0 only'),
])
def test_warrior_impact_camera_shake_profile_rejects_changed_source(mutation, message):
    program = warrior_impact_source_program()
    mutation(program)

    with pytest.raises(ValueError, match=message):
        warrior_impact_camera_shake_profile(program)


def warrior_source_program():
    def action(name, **values):
        return {'name': name, 'params': [{'name': key, **value} for key, value in values.items()]}

    def number(value):
        return {'number': value}

    def particle(duration, lifetime, delay=0):
        return {'restored': True, 'duration': duration,
                'delay': {'mode': 0, 'scalar': delay},
                'life': {'mode': 3, 'scalar': lifetime, 'minimum': lifetime - .1}}

    def particle_action(node):
        return action('ParticlePlayAction', m_GameObject={'node': node}, m_IncludeChildren=number(0))

    shaker = {'restored': True, 'amount': {'x': .1, 'y': .1, 'z': .1},
              'curve': [{'time': 0, 'value': 1}, {'time': .6, 'value': 0}],
              'hold': False, 'hold_at_sec': 0, 'is_override': False}
    invoke = action('InvokeMethod', gameObject={'node': 1}, behaviour={'text': 'CameraShaker'},
                    methodName={'text': 'StartShake'}, delay=number(.1), repeating=number(0),
                    repeatDelay=number(1), cancelOnExit=number(0))
    state = lambda name, actions, transitions=None: {
        'name': name, 'actions': actions, 'transitions': transitions or []}
    machine = {
        'owner_known': True, 'owner': 1, 'start': 'State 1',
        'events': [{'event': 'Death', 'state': 'DeathState'}, {'event': 'None', 'state': 'NoneState'}],
        'states': [
            state('State 1', []),
            state('DeathState', [action('SpellStateAction'), particle_action(4), particle_action(5),
                                 action('Wait', time=number(.55), realTime=number(0)),
                                 action('AudioPlayClipAction')], [{'event': 'FINISHED', 'state': 'DeathState2'}]),
            state('DeathState2', [particle_action(3), particle_action(6), particle_action(7), invoke],
                  [{'event': 'FINISHED', 'state': 'DeathState3'}]),
            state('DeathState3', [action('SpellFinishAction'), action('Wait', time=number(2), realTime=number(0))],
                  [{'event': 'FINISHED', 'state': 'NoneState'}]),
            state('NoneState', [action('SpellStateAction')]),
        ],
    }
    nodes = [{'particle': None, 'camera_shaker': None} for _ in range(8)]
    nodes[1]['camera_shaker'] = shaker
    nodes[3]['particle'] = particle(.3, .7)
    nodes[4]['particle'] = particle(.3, .5)
    nodes[5]['particle'] = particle(.3, .7)
    nodes[6]['particle'] = particle(.2, .7, .05)
    nodes[7]['particle'] = particle(.2, .65, .05)
    return {'unknown_actions': ['AudioPlayClipAction', 'InvokeMethod'],
            'machines': [machine], 'nodes': nodes}


def test_warrior_source_duration_tracks_particle_tail_and_camera_curve():
    assert source_visual_duration(warrior_source_program()) == pytest.approx(1.55)


def test_warrior_source_duration_rejects_unsupported_invoke_semantics():
    program = warrior_source_program()
    invoke = next(action for state in program['machines'][0]['states']
                  for action in state['actions'] if action['name'] == 'InvokeMethod')
    next(param for param in invoke['params'] if param['name'] == 'repeating')['number'] = 1

    with pytest.raises(ValueError, match='unsupported root method invocation'):
        source_visual_duration(program)


def warlock_source_program():
    def action(name, **values):
        return {'name': name, 'params': [{'name': key, **value} for key, value in values.items()]}

    def number(value):
        return {'number': value}

    def node(value):
        return {'node': value}

    state = lambda name, actions, transitions=None: {
        'name': name, 'actions': actions, 'transitions': transitions or []}
    play = [
        action('SpellStateAction', m_SpellObject=node(1)),
        action('SpellFinishAction', m_SpellObject=node(1), m_Delay=number(0)),
        action('ActivateGameObject', gameObject=node(3), activate=number(1), recursive=number(0),
               resetOnExit=number(0), everyFrame=number(0)),
        action('ActivateGameObject', gameObject=node(8), activate=number(1), recursive=number(0),
               resetOnExit=number(0), everyFrame=number(0)),
        action('ParticlePlayAction', m_GameObject=node(4), m_IncludeChildren=number(1)),
        action('AudioPlayClipAction', m_ParentObject=node(1),
               m_Sound={'asset_type': 'MonoBehaviour',
                        'asset_path': 'ce406cfd04976a99ebc44463.native-sound.json'},
               m_Volume=number(.8), m_Pitch=number(1), m_SpatialBlend=number(0),
               m_Category=number(1), m_Delay=number(0), m_TemplateSource={'object_null': True},
               m_IsMinionSummonVO=number(0), m_InstanceLimited=number(1),
               m_InstanceLimitedDuration=number(.5), m_InstanceLimitMaximum=number(1),
               m_LimitMaxOutOption=number(0)),
        action('ParticlePlayAction', m_GameObject=node(9), m_IncludeChildren=number(1)),
        action('Wait', time=number(4), finishEvent={'text': 'FINISHED'}, realTime=number(0)),
    ]
    machine = {
        'owner_known': True, 'owner': 1, 'start': 'StartState',
        'events': [{'event': 'Death', 'state': 'Play FX'}, {'event': 'None', 'state': 'NoneState'}],
        'states': [state('Play FX', play, [{'event': 'FINISHED', 'state': 'NoneState'}]),
                   state('StartState', []), state('NoneState', [action('SpellStateAction')])],
    }
    nodes = [{} for _ in range(21)]
    nodes[4]['particle'] = {'restored': True}
    nodes[9]['particle'] = {'restored': True}
    return {
        'reference': 'ReuseFX_BuffImpact_Void_SwirlIntake_Small_HeroPower_Super.prefab:'
                     '5c3f93a27c9cb434ea6e0c37005a2c0d',
        'nodes': nodes, 'machines': [machine], 'unknown_actions': ['AudioPlayClipAction'],
    }


def test_warlock_source_duration_accepts_only_reviewed_life_tap_root():
    assert warlock_source_duration(warlock_source_program()) == 4


def test_warlock_source_duration_rejects_added_unknown_action():
    program = warlock_source_program()
    program['unknown_actions'].append('InvokeMethod')

    with pytest.raises(ValueError, match='unsupported source action'):
        warlock_source_duration(program)


def test_warlock_source_duration_rejects_reordered_authoritative_root_actions():
    program = warlock_source_program()
    actions = program['machines'][0]['states'][0]['actions']
    actions[5], actions[6] = actions[6], actions[5]

    with pytest.raises(ValueError, match='action order changed'):
        warlock_source_duration(program)


def rogue_source_fixture():
    def action(name, **values):
        return {'name': name, 'params': [{'name': key, **value} for key, value in values.items()]}
    node = lambda value: {'node': value}
    number = lambda value: {'number': value}
    audio = action('AudioPlayRandomClipAction', m_ParentObject=node(1),
                   m_Sounds={'array_count': 1}, m_Weights={'array_count': 1},
                   m_TemplateSource={'object_null': True}, **{
                       name: number(value) for name, value in {
                           'm_MinVolume': 1, 'm_MaxVolume': 1, 'm_MinPitch': 1, 'm_MaxPitch': 1,
                           'm_SpatialBlend': 1, 'm_Category': 1, 'm_Delay': 0, 'm_InstanceLimited': 0,
                           'm_InstanceLimitedDuration': 0, 'm_InstanceLimitMaximum': 1, 'm_LimitMaxOutOption': 0,
                       }.items()})
    audio['params'].insert(2, {'name': '', 'asset_path': 'd7861ddcda8550b8d2114840.native-sound.json'})
    audio['params'].insert(4, {'name': '', 'number': 1})
    actions = [action('SpellStateAction', m_SpellObject=node(1)),
               action('ParticlePlayAction', m_GameObject=node(2), m_IncludeChildren=number(0)), audio,
               action('SpellFinishAction', m_SpellObject=node(1), m_Delay=number(0)),
               action('Wait', time=number(1), realTime=number(0), finishEvent={'text': 'FINISHED'})]
    state = lambda name, actions, transitions=[]: {'name': name, 'actions': actions, 'transitions': transitions}
    program = {'reference': ROGUE_ROOT, 'nodes': [{'parent': -1}, {'parent': -1},
                                                  {'parent': 1, 'particle': {'restored': True}}],
               'unknown_actions': ['AudioPlayRandomClipAction'], 'machines': [{
                   'owner_known': True, 'owner': 1, 'start': 'State 1',
                   'events': [{'event': 'Death', 'state': 'Birth State'}, {'event': 'None', 'state': 'NoneState'}],
                   'states': [state('State 1', []), state('Birth State', actions, [{'event': 'FINISHED', 'state': 'NoneState'}]),
                              state('NoneState', [action('SpellStateAction', m_SpellObject=node(1))])]}]}
    config = {'card_id': 'HERO_03bp', 'reference': ROGUE_ROOT, 'location': 2, 'target_behavior': 6,
              'start_enabled': False, 'start_config_null': True, 'start': None,
              'missile': {'m_Enabled': 0}, 'missile_reference': '', 'reverse_available': False,
              'impact': {'enabled': True, 'prefab': {'prefab_null': False,
                         'prefab_source': {'pathid': '-9149849810296163763'}}, 'use_super_location': True,
                         'game_delay_min': 0, 'game_delay_max': 0, 'ranges': []},
              'area': {slot: {'enabled': True, 'prefab_null': True, 'prefab_reference': ''}
                       for slot in ('friendly', 'opponent')},
              'chain_enabled': True, 'chain_prefab_null': True, 'chain_reference': '', 'chain_available': False}
    return program, config


def test_rogue_source_profile_preserves_enabled_null_branch_facts():
    program, config = rogue_source_fixture()
    assert rogue_source_profile(program, config) == ROGUE_PROFILE
    assert config['chain_enabled'] and config['chain_prefab_null'] and not config['chain_available']


@pytest.mark.parametrize('field,value', [('chain_enabled', False), ('chain_prefab_null', False),
                                      ('chain_available', True), ('chain_reference', 'unexpected.prefab')])
def test_rogue_source_profile_rejects_changed_chain_facts(field, value):
    program, config = rogue_source_fixture()
    config[field] = value
    with pytest.raises(ValueError, match='Chain'):
        rogue_source_profile(program, config)


@pytest.mark.parametrize('slot', ['friendly', 'opponent'])
def test_rogue_source_profile_rejects_disabled_or_invented_area(slot):
    program, config = rogue_source_fixture()
    config['area'][slot]['enabled'] = False
    with pytest.raises(ValueError, match='area'):
        rogue_source_profile(program, config)


def test_rogue_source_profile_rejects_added_audio_choice_and_changed_lifecycle():
    program, config = rogue_source_fixture()
    actions = program['machines'][0]['states'][1]['actions']
    next(p for p in actions[2]['params'] if p['name'] == 'm_Sounds')['array_count'] = 2
    with pytest.raises(ValueError, match='single-candidate'):
        rogue_source_profile(program, config)
    program, config = rogue_source_fixture()
    program['machines'][0]['events'][0]['event'] = 'Birth'
    with pytest.raises(ValueError, match='lifecycle'):
        rogue_source_profile(program, config)


def test_rogue_source_profile_rejects_unrestored_particle():
    program, config = rogue_source_fixture()
    program['nodes'][2]['particle']['restored'] = False
    with pytest.raises(ValueError, match='particle'):
        rogue_source_profile(program, config)


def test_rogue_source_profile_rejects_changed_embedded_root_hierarchy():
    program, config = rogue_source_fixture()
    program['nodes'][1]['parent'] = 0
    with pytest.raises(ValueError, match='identity or target profile'):
        rogue_source_profile(program, config)
