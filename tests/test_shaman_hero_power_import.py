import copy
from pathlib import Path

import pytest
from game.database import preset
from game.match import Match
from tools.fireblast_evidence import analyze_shaman, apply, prepare

from tools.shaman_hero_power import (
    CUSTOM_SPELL_PATHID, EXPECTED_AUDIO, FSM_EVENTS, HERO_POWER_ID, NODE_LAYOUT,
    ROOT_REFERENCE, ROOT_SOURCE, STATE_ACTIONS, STATE_TRANSITIONS, UNKNOWN_ACTIONS,
    PROFILE, shaman_source_profile,
)


def _action(name, params=None):
    return {'name': name, 'params': params or []}


def _state(owner, name):
    return {
        'name': name,
        'actions': [_action(action) for action in STATE_ACTIONS[owner][name]],
        'transitions': [{'event': event, 'state': target}
                        for event, target in STATE_TRANSITIONS[owner][name]],
    }


def h02_fixture():
    machines = []
    for owner in (0, 3):
        states = [_state(owner, name) for name in STATE_ACTIONS[owner]]
        machine = {
            'owner': owner, 'owner_known': True, 'start': 'State 1',
            'events': [{'event': event, 'state': state} for event, state in FSM_EVENTS[owner]],
            'states': states,
        }
        machines.append(machine)
    by_state = {machine['owner']: {state['name']: state for state in machine['states']}
                for machine in machines}

    def set_params(owner, state_name, action_name, params):
        row = next(row for row in by_state[owner][state_name]['actions']
                   if row['name'] == action_name)
        row['params'] = [{'name': name, **value} for name, value in params.items()]

    set_params(0, 'State0', 'AnimationPlaythroughAction', {
        'm_GameObject': {'node': 2}, 'm_AnimName': {'text': 'FX_TotemFadeIn'},
    })
    set_params(0, 'State0', 'AudioPlayClipAction', {
        'm_ParentObject': {'node': 0},
        'm_Sound': {'asset_path': EXPECTED_AUDIO[0]['asset_path']},
        'm_Volume': {'number': 0.5}, 'm_SpatialBlend': {'number': 1.0},
    })
    set_params(3, 'State 4', 'AnimationPlaythroughAction', {
        'm_GameObject': {'node': 3}, 'm_AnimName': {'text': 'TotemSpawn'},
    })
    set_params(3, 'State 4', 'AudioPlayClipAction', {
        'm_ParentObject': {'node': 3},
        'm_Sound': {'asset_path': EXPECTED_AUDIO[1]['asset_path']},
        'm_Volume': {'number': 0.7}, 'm_SpatialBlend': {'number': 0.0},
    })
    set_params(3, 'State 3', 'Wait', {'time': {'number': 0.4}})
    set_params(3, 'State 3', 'SpellInitActorVariables', {
        'm_actorObject': {'variable': 'Actor'}, 'm_rootObject': {'is_none': True},
        'm_rootObjectMesh': {'is_none': True},
    })
    set_params(3, 'State 6', 'MainCameraShakerAction', {
        'm_Amount': {'vector': {'x': 0.02500000037252903, 'y': 0.02500000037252903,
                                'z': 0.02500000037252903}},
        'm_Time': {'number': 0.5},
    })
    set_params(3, 'State 5', 'ActorSetVisibilityRecursiveAction', {
        'm_GameObject': {'variable': 'Actor'}, 'm_Visible': {'number': 1},
        'm_IncludeChildren': {'number': 1},
    })
    set_params(3, 'State 3', 'ActorSetVisibilityRecursiveAction', {
        'm_GameObject': {'variable': 'Actor'}, 'm_Visible': {'number': 0},
        'm_IncludeChildren': {'number': 1},
    })
    set_params(3, 'State 5', 'DestroyObject', {
        'gameObject': {'node': 4}, 'delay': {'number': 0},
        'detachChildren': {'number': 0},
    })
    set_params(3, 'State 6', 'SetParent', {
        'gameObject': {'node': 4}, 'parent': {'is_none': True},
        'worldPositionStays': {'number': 1},
    })

    audio_actions = []
    for item in EXPECTED_AUDIO:
        clip_name = item['clip']
        audio_actions.append({
            'action': 'AudioPlayClipAction',
            'machine_owner_node': item['owner'], 'state': item['state'],
            'action_index': item['action_index'],
            'params': [{'name': 'm_Sound', 'asset_path': item['asset_path']}],
            'sound_def': {
                'class_name': 'SoundDef',
                'source_file': {'sha256': item['sound_bundle_sha256']},
                'values': {'m_AudioClip': clip_name},
                'clips': [{
                    'reference': clip_name,
                    'source_file': {'sha256': item['clip_bundle_sha256']},
                    'decoded_export': {
                        'sample_rate': item['sample_rate'], 'channels': item['channels'],
                        'duration_seconds': item['duration_seconds'],
                    },
                }],
            },
        })

    null = {'m_FileID': 0, 'm_PathID': 0}
    override_fields = {'m_TargetInfo': {
        'm_Behavior': 10, 'm_RandomTargetCountMin': 8, 'm_RandomTargetCountMax': 10,
    }}
    for name in ('m_StartInfo', 'm_MissileInfo', 'm_ImpactInfo',
                 'm_FriendlyAreaEffectInfo', 'm_OpponentAreaEffectInfo', 'm_ChainInfo'):
        override_fields[name] = {'m_Enabled': 1, 'm_Prefab': copy.deepcopy(null)}
    override_fields['m_MissileInfo']['m_ReversePrefab'] = copy.deepcopy(null)
    spell_component = {
        'class_name': 'Spell', 'enabled': 1,
        'source': {'pathid': CUSTOM_SPELL_PATHID,
                   'bundle': 'initial_base_global-41caf0cf-prefab-6.unity3d'},
    }
    effect = {
        'reference': ROOT_REFERENCE, 'root': ROOT_SOURCE,
        'spell_components': [
            {'class_name': 'OverrideCustomSpawnSpell', 'enabled': 1, 'fields': override_fields},
            spell_component,
        ],
        'program': {'audio_actions': audio_actions},
    }
    nodes = [{'name': name, 'parent': parent, 'particle': {'restored': True} if index in (1, 4, 5, 7, 8) else None,
              'animation_refs': {2: ['fade'], 3: ['spawn']}.get(index, [])}
             for index, (name, parent) in enumerate(NODE_LAYOUT)]
    program = {
        'reference': ROOT_REFERENCE, 'source': ROOT_SOURCE,
        'nodes': nodes, 'machines': machines,
        'animations': [
            {'id': 'fade', 'name': 'FX_TotemFadeIn', 'duration': 1.3, 'loop': False},
            {'id': 'spawn', 'name': 'TotemSpawn', 'duration': 1.5, 'loop': False},
        ],
        'unknown_actions': sorted(UNKNOWN_ACTIONS),
        'spell_transforms': [{
            'kind': 'OverrideCustomSpawnSpell',
            'raw': {'m_CustomSpawnSpell': {'m_FileID': 5, 'm_PathID': int(CUSTOM_SPELL_PATHID)}},
        }],
    }
    return program, {'card_id': HERO_POWER_ID, 'effect_references': [effect]}


def test_h02_profile_opens_only_the_reviewed_runtime_route_and_preserves_sound_sources():
    program, source = h02_fixture()
    profile = shaman_source_profile(program, source)
    assert profile['profile'] == PROFILE
    assert profile['runtime_route_enabled'] is True
    assert profile['fsm_owners'] == [0, 3]
    assert [row['clip'].split('.')[0] for row in profile['audio_sources']] == [
        'Shaman_TotemicCall_Cast_1', 'Totems_CustomSpawn_1',
    ]
    assert profile['custom_spawn_completion_age_seconds'] == 1.9
    assert any('official same-camera' in gap.lower() for gap in profile['known_gaps'])


def test_h02_player_dispatches_playfx_then_finishes_the_actor_after_source_animation():
    source = (Path(__file__).resolve().parents[1] /
              'UnityClient' / 'Assets' / 'Scripts' / 'NativeShamanHeroPowerPlayback.cs').read_text(encoding='utf-8')
    sample = source.split('void Sample(Entry entry,float age)', 1)[1].split('static string Describe', 1)[0]
    schedule = sample.split('if(!entry.playFx&&age>=PlayFxDelay)', 1)[1].split('if(!Valid', 1)[0]
    event_time = schedule.index('float eventAge=Mathf.Max(entry.age,PlayFxDelay)')
    seek_source_time = schedule.index('entry.effect.Seek(eventAge)')
    dispatch_play_fx = schedule.index('DispatchMachineEvent(0,"PlayFX")')
    finish_guard = schedule.index('if(!entry.finishing&&age>=entry.spawnFinishAge)')
    finish_seek = schedule.index('entry.effect.Seek(finishAge)')
    dispatch_finish = schedule.index('DispatchMachineEvent(0,"Die")')
    continue_to_sample_time = schedule.index('entry.effect.Seek(age);entry.age=age;')
    assert event_time < seek_source_time < dispatch_play_fx < finish_guard < finish_seek < dispatch_finish < continue_to_sample_time
    assert 'TryGetSpawnFinishAge' in source and 'clip.duration' in source
    assert 'entry.effect.Seek(PlayFxDelay)' not in sample
    regression_guard = sample.split('if(age<entry.age)', 1)[1].split('if(!entry.playFx&&age>=PlayFxDelay)', 1)[0]
    assert 'age=entry.age' in regression_guard
    assert 'entry.effect.Seek(age)' not in regression_guard
    assert 'entry.finishing&&entry.effect.CurrentStates.Length>0&&entry.effect.CurrentStates[0]=="NoneState"' in source


def test_h02_player_fixture_treats_totemic_call_as_an_untargeted_power():
    source = (Path(__file__).resolve().parents[1] /
              'UnityClient' / 'Assets' / 'Scripts' / 'FireblastReplay.cs').read_text(encoding='utf-8')
    selection = source.split('bool automaticPower=Paladin||Shaman;', 1)[1].split('if(!effects.HasPowerSelection', 1)[0]
    assert '(Rogue||Shaman||!legalTarget)&&(Rogue||Shaman||!arrowVisible)&&!selectedTarget' in selection


def test_h02_import_manifest_preserves_the_profile_runtime_route_decision():
    source = (Path(__file__).resolve().parents[1] /
              'tools' / 'import_hero_power_super_spell.py').read_text(encoding='utf-8')
    assert "runtime_route_enabled=row['source_profile'].get('runtime_route_enabled') is True" in source
    assert "custom_spawn_completion_age_seconds=row['source_profile'].get('custom_spawn_completion_age_seconds')" in source


@pytest.mark.parametrize('mutation, message', [
    (lambda p, s: p['spell_transforms'][0]['raw']['m_CustomSpawnSpell'].update(m_PathID=9),
     'm_CustomSpawnSpell source binding'),
    (lambda p, s: s['effect_references'][0]['spell_components'][0]['fields']['m_StartInfo'].update(m_Enabled=0),
     'enabled-null m_StartInfo'),
    (lambda p, s: s['effect_references'][0]['spell_components'][0]['fields']['m_TargetInfo'].update(m_RandomTargetCountMax=9),
     'authored target fields'),
    (lambda p, s: p['machines'][1]['states'][4]['transitions'].clear(),
     'FSM transition changed'),
    (lambda p, s: p['animations'][1].update(duration=float('inf')),
     'TotemSpawn completion timing'),
    (lambda p, s: p['nodes'][3].update(animation_refs=['fade']),
     'TotemSpawn completion timing'),
    (lambda p, s: p['nodes'][3].update(parent=0), 'prefab hierarchy'),
    (lambda p, s: s['effect_references'][0]['program']['audio_actions'][0]['sound_def']['clips'][0].update(reference='Other.wav'),
     'SoundDef or AudioClip source identity'),
])
def test_h02_profile_rejects_source_drift(mutation, message):
    program, source = h02_fixture()
    mutation(program, source)
    with pytest.raises(ValueError, match=message):
        shaman_source_profile(program, source)


def test_h02_formal_fixture_uses_authoritative_untargeted_totem_result():
    match = prepare(Match(['ShamanA', 'MageB'], [8, 4], [preset(8), preset(4)], 52), shaman=True)
    before = match.snapshot(0)
    shaman = match.game.players[0]
    move = next(row for row in match.legal_moves(0)
                if row['kind'] == 'power' and row['entity'] == shaman.hero.power.entity_id and row['target'] == 0)
    apply(match, 0, move, 'formal-totemic-call')
    after = match.snapshot(0)
    events = after['events']

    assert before['players'][0]['power']['card_id'] == 'HERO_02bp'
    assert before['players'][1]['power']['card_id'] == 'HERO_08bp'
    assert before['players'][0]['field'] == []
    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'Summon']
    spend, power, summon = events
    assert spend['source'] == shaman.hero.power.entity_id and spend['amount'] == 2
    assert power['target'] == summon['source'] == shaman.hero.power.entity_id
    assert summon['target'] == summon['visual']['entity']
    assert summon['visual']['card_id'] == after['players'][0]['field'][0]['card_id']
    assert after['players'][0]['power']['used'] is True
    assert after['public_hash'] == match.snapshot(1)['public_hash']


def test_h02_player_analyzer_requires_actor_audio_and_live_input_unlock(tmp_path):
    import json

    record_a = {'file': 'source-cast.wav'}
    record_b = {'file': 'source-totem.wav'}
    driver = {'role': 'driver', 'heroPower': HERO_POWER_ID,
              'result': 'PASS H02', 'publicHash': 'same'}
    observer = {'role': 'observer', 'heroPower': HERO_POWER_ID,
                'result': 'PASS H02', 'publicHash': 'same'}
    evidence = {
        'role': 'driver', 'heroPower': HERO_POWER_ID, 'publicHash': 'same', 'displayHash': 'same',
        'eventOrderValid': True, 'eventLinkValid': True, 'spendManaCount': 1,
        'playHeroPowerCount': 1, 'summonCount': 1, 'totemEntity': 71, 'totemCardId': 'CS2_052',
        'nativeActiveCount': 1, 'runtimeFailures': 0, 'nativeActorVisible': True,
        'actorVisualHiddenDuringSource': True, 'actorVisualRestoredAfterSource': True,
        'sourceAudioPlayCount': 2, 'sourceAudioRecords': [record_a, record_b],
        'inputUnlockedDuringSource': True, 'inputUnlockBatchTime': 1.16, 'inputUnlockSampleFrame': 42,
        'displayHashMatched': True, 'sampleFrames': 4800, 'overflows': 0,
    }
    folder = tmp_path / 'run'
    (folder / 'driver').mkdir(parents=True)
    (folder / 'observer').mkdir()
    (folder / 'driver' / 'report.json').write_text(json.dumps(driver), encoding='utf-8')
    (folder / 'observer' / 'report.json').write_text(json.dumps(observer), encoding='utf-8')
    (folder / 'driver' / 'shaman-evidence.json').write_text(json.dumps(evidence), encoding='utf-8')
    summary = analyze_shaman(folder)
    assert summary['event_link_valid'] is True
    assert summary['native_actor_visible'] is True
    assert summary['actor_visual_hidden_during_source'] is True
    assert summary['actor_visual_restored_after_source'] is True
    assert summary['input_unlocked_during_source'] is True

    evidence['inputUnlockedDuringSource'] = False
    (folder / 'driver' / 'shaman-evidence.json').write_text(json.dumps(evidence), encoding='utf-8')
    with pytest.raises(AssertionError):
        analyze_shaman(folder)
    evidence['inputUnlockedDuringSource'] = True
    evidence['inputUnlockBatchTime'] = 1.149
    (folder / 'driver' / 'shaman-evidence.json').write_text(json.dumps(evidence), encoding='utf-8')
    with pytest.raises(AssertionError):
        analyze_shaman(folder)
