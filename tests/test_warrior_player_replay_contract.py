import json
from pathlib import Path

import pytest

from game.database import preset
from game.match import Match
from tools import fireblast_evidence
from tools.fireblast_evidence import analyze_warrior_report, apply, prepare, run_players


ROOT = Path(__file__).resolve().parents[1]


def test_warrior_fixture_preserves_authoritative_armor_event_order():
    match = prepare(Match(['WarriorA', 'MageB'], [10, 4], [preset(10), preset(4)], 52), warrior=True)
    warrior = match.game.players[0]
    before = match.snapshot(0)

    assert before['players'][0]['power']['card_id'] == 'HERO_01bp'
    assert before['players'][1]['power']['card_id'] == 'HERO_08bp'
    assert before['players'][0]['power']['cost'] == 2
    assert before['players'][0]['hero']['health'] == 29
    assert before['players'][0]['hero']['armor'] == 0
    assert any(move['kind'] == 'power' and move['entity'] == warrior.hero.power.entity_id and move['target'] == 0
               for move in match.legal_moves(0))
    mana = warrior.mana
    apply(match, 0, next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0),
          'warrior-formal-armor')

    after = match.snapshot(0)
    events = after['events']
    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'GainArmor']
    spend, power, armor = events
    assert spend['source'] == warrior.hero.power.entity_id and spend['amount'] == 2
    assert spend['target'] != 0
    assert power['source'] == spend['target'] and power['target'] == warrior.hero.power.entity_id
    assert 'card_id' not in power
    assert armor['source'] == warrior.hero.power.entity_id
    assert armor['target'] == warrior.hero.entity_id and armor['amount'] == 2
    assert warrior.mana == mana - 2 and after['players'][0]['power']['used']
    assert after['players'][0]['hero']['health'] == before['players'][0]['hero']['health']
    assert after['players'][0]['hero']['armor'] == before['players'][0]['hero']['armor'] + 2
    assert after['public_hash'] == match.snapshot(1)['public_hash']


def test_warrior_analyzer_requires_armor_source_completion_and_unlock_evidence():
    before = dict(powerCardId='HERO_01bp', powerUsed=False, heroArmor=0, heroHealth=29,
                  legalPowerMove=True, legalEndTurn=False, mana=10, publicHash='before',
                  version=9, powerEntity=5, heroEntity=4)
    after = dict(version=10, powerCardId='HERO_01bp', powerUsed=True, heroArmor=2, heroHealth=29,
                 legalPowerMove=False, legalEndTurn=True, mana=8, publicHash='after')
    report = dict(role='driver', heroPower='HERO_01bp', warriorBefore=before, warriorAfter=after,
                  warriorEventOrderValid=True, warriorPlayHeroPowerCount=1, warriorGainArmorCount=1,
                  warriorSourceCueCount=1, warriorSourceCueSemanticsValid=True, warriorUnexpectedHitCueCount=0,
                  warriorMissileEventCount=0, warriorArrivalEventCount=0, warriorSourceRootCompleted=True,
                  warriorSourceRootReleased=True, warriorCameraShakerStarts=2, warriorParentCameraShakerStarts=1,
                  warriorImpactCloneCameraShakerStarts=1, warriorUnhandledActions=0, warriorParentUnhandledActions=0,
                  warriorImpactCloneUnhandledActions=0, warriorParentUnhandledActionDetails=[],
                  warriorImpactCloneUnhandledActionDetails=[], warriorUnboundAudioActions=0,
                  warriorSourceAudioPlayCount=2, records=[dict(phase='warrior-direct-cast', message='source play',
                  audioSource='native-effect', file='warrior.wav', actionCount=1) for _ in range(2)], listenerCount=1,
                  warriorInputUnlockedAfterSource=True, warriorInputUnlockSampleFrame=42,
                  warriorFollowup=dict(inputUnlockedDuringSource=True, sendMoveSubmitted=True, authorityAccepted=True,
                      sourceActiveAtSend=True, sourceTailCleanedAfterFollowup=True, sampleFrame=42,
                      submitVersion=10, acceptedVersion=11, actionCounterDelta=2, activeAfter=1,
                      castAuthorityVersion=10, castAuthorityEvents=[
                          dict(type='SpendMana', source=5, target=9, owner=0, amount=2),
                          dict(type='PlayHeroPower', source=9, target=5, owner=0, card_id=''),
                          dict(type='GainArmor', source=5, target=4, owner=0, amount=2)],
                      sendLatency=.18, latencyBudget=.217, publicHash='final', displayHash='final'),
                  publicHash='final', displayHash='final')
    frame = dict(frame=42, version=10, authorityHealth=29, displayHealth=29, sourceCompleted=False, sourceActive=True,
                 sourceReleased=False, busy=True, inputLocked=False, followupSent=True, actionCounterDelta=2,
                 followupSendLatency=.18, hashMatched=False, endTurnAvailable=True, authorityArmor=2, displayArmor=2)
    cleaned_frame = dict(frame=52, version=11, authorityHealth=29, displayHealth=29, sourceCompleted=True,
                 sourceActive=False, sourceReleased=True, busy=False, inputLocked=False, followupAccepted=True,
                 followupTailCleaned=True, publicHash='final', displayHash='final',
                 hashMatched=True, endTurnAvailable=False, authorityArmor=2, displayArmor=2)

    frames = [frame, cleaned_frame]
    summary = analyze_warrior_report(report, frames)

    assert summary['event_order_valid'] is True
    assert summary['cast_authority_version'] == 10
    assert summary['cast_authority_event_types'] == ['SpendMana', 'PlayHeroPower', 'GainArmor']
    assert summary['rule_after']['heroArmor'] == 2
    assert summary['camera_shaker_starts'] == 2
    assert summary['parent_camera_shaker_starts'] == 1 and summary['impact_clone_camera_shaker_starts'] == 1
    assert summary['source_audio_play_count'] == 2
    for field, value in (('listenerCount', 0), ('warriorSourceAudioPlayCount', 1)):
        bad_driver = json.loads(json.dumps(report))
        bad_driver[field] = value
        with pytest.raises(AssertionError):
            analyze_warrior_report(bad_driver, frames)
    bad_driver = json.loads(json.dumps(report))
    bad_driver['records'][1]['file'] = 'different.wav'
    with pytest.raises(AssertionError):
        analyze_warrior_report(bad_driver, frames)
    bad_driver = json.loads(json.dumps(report))
    bad_driver['records'][0]['actionCount'] = 2
    with pytest.raises(AssertionError):
        analyze_warrior_report(bad_driver, frames)
    report['warriorImpactCloneUnhandledActions'] = 1
    report['warriorUnhandledActions'] = 1
    report['warriorImpactCloneUnhandledActionDetails'] = ['impact reference | action=InvokeMethod unsupported target']
    with pytest.raises(AssertionError):
        analyze_warrior_report(report, frames)
    report['warriorImpactCloneUnhandledActions'] = 0
    report['warriorUnhandledActions'] = 0
    report['warriorImpactCloneUnhandledActionDetails'] = []
    report['warriorParentCameraShakerStarts'] = 0
    report['warriorCameraShakerStarts'] = 1
    with pytest.raises(AssertionError):
        analyze_warrior_report(report, frames)
    report['warriorParentCameraShakerStarts'] = 1
    report['warriorImpactCloneCameraShakerStarts'] = 0
    report['warriorCameraShakerStarts'] = 1
    with pytest.raises(AssertionError):
        analyze_warrior_report(report, frames)
    report['warriorImpactCloneCameraShakerStarts'] = 1
    report['warriorCameraShakerStarts'] = 1
    with pytest.raises(AssertionError):
        analyze_warrior_report(report, frames)
    observer = dict(report, role='observer', warriorCameraShakerStarts=2,
                    warriorParentCameraShakerStarts=1, warriorImpactCloneCameraShakerStarts=1,
                    warriorBefore=dict(before, legalPowerMove=False),
                    warriorAfter=dict(after, legalEndTurn=False), warriorSourceAudioPlayCount=0,
                    warriorInputUnlockedAfterSource=False, warriorInputUnlockSampleFrame=-1,
                    listenerCount=0)
    observer.pop('records')
    observer['warriorFollowup'] = dict(report['warriorFollowup'], sendMoveSubmitted=False)
    observer_summary = analyze_warrior_report(observer, [])
    assert observer_summary['source_audio_play_count'] is None
    assert observer_summary['camera_shaker_starts'] == 2
    assert observer_summary['source_audio_capture_scope'].startswith('driver-only;')
    stale_snapshot = json.loads(json.dumps(observer))
    stale_snapshot['warriorFollowup']['castAuthorityVersion'] = 9
    with pytest.raises(AssertionError):
        analyze_warrior_report(stale_snapshot, [])
    bad_event = json.loads(json.dumps(observer))
    bad_event['warriorFollowup']['castAuthorityEvents'][0]['amount'] = 3
    with pytest.raises(AssertionError):
        analyze_warrior_report(bad_event, [])


def test_warrior_player_fixture_rejects_target_override_and_wires_source_route(tmp_path):
    output = tmp_path / 'invalid-warrior-run'
    with pytest.raises(ValueError, match='uses its untargeted power'):
        run_players(37245, output, 'own-minion', '1280x720', False, False, False, warrior=True)
    assert not output.exists()

    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/NativeWarriorHeroPowerChecks.cs').read_text(encoding='utf-8')
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')

    assert 'bool Warrior{get{return Argument("--hero-power-replay-id")=="HERO_01bp";}}' in replay
    assert 'if(Hunter||Rogue||Warrior||Druid)' in replay
    assert 'public int warriorPlayHeroPowerCount,warriorGainArmorCount,warriorSourceCueCount' in replay
    assert 'int warriorPlayHeroPowerCount,warriorGainArmorCount,warriorSourceCueCount' in replay
    assert 'warriorSourceCueCount=warriorSourceCueCount' in replay
    assert 'warriorSourceCueCount=Math.Max(warriorSourceCueCount,sourceCues.Length)' in replay
    assert 'warriorSourceCueSemanticsValid=sourceCues[0].data.target==0' in replay
    assert 'warriorEventOrderValid=powerSequence.SequenceEqual(new[]{"SpendMana","PlayHeroPower","GainArmor"})' in replay
    assert 'warriorParentCameraShakerStarts==1&&' in replay
    assert 'warriorImpactCloneCameraShakerStarts==1&&warriorCameraShakerStarts==2' in replay
    assert '0.1-second delayed CameraShaker.StartShake' in replay
    assert 'warriorParentCameraShakerStarts=cast.ParentCameraShakerStarts' in replay
    assert 'warriorImpactCloneCameraShakerStarts=cast.ImpactCloneCameraShakerStarts' in replay
    assert 'warriorParentUnhandledActionDetails=cast.ParentUnhandledActionDetails.ToArray()' in replay
    assert 'warriorImpactCloneUnhandledActionDetails=cast.ImpactCloneUnhandledActionDetails.ToArray()' in replay
    assert 'warriorSourceAudioPlayCount==2' in replay
    assert 'record.phase=="warrior-direct-cast"' in replay
    assert 'TrySubmitWarriorFollowup(state)' in replay
    assert 'current.version!=warriorCastVersion' in replay
    assert 'Check(presentation.Busy&&sourceActive' in replay
    assert 'warriorFollowup.sendMoveSubmitted=Field<bool>("pending")' in replay
    assert 'warriorFollowup.authorityAccepted=state.active==1' in replay
    assert 'sourceTailCleanedAfterFollowup' in replay
    assert 'warriorCastAuthorityEvents=currentEvents.Select(item=>JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(item))).ToArray()' in replay
    assert 'warriorFollowup.castAuthorityVersion==warriorCastVersion' in replay
    assert 'var currentEvents=warriorCastAuthorityEvents??new SemanticEvent[0]' in replay
    assert "cast_events = followup.get('castAuthorityEvents')" in harness
    assert "cast_version == after['version'] and cast_version > before['version']" in harness
    assert '0.2s input gate plus one frame' in replay
    assert "sample['sourceActive'] and sample['busy'] and not sample['inputLocked']" in harness
    assert 'ParentCameraShakerStarts+ImpactCloneCameraShakerStarts' in playback
    assert 'ImpactCloneUnhandledActionDetails.AddRange' in playback
    assert 'parser.add_argument(\'--warrior\'' in harness
    assert "['--hero-power-replay-id', 'HERO_01bp']" in harness
    assert 'NativeSuperSpellPlayback.SupportsWarriorHeroPowerRoot(program)' in checks
    assert 'if(warrior)ValidateWarriorHeroPowerRoot(parent);' in playback


def test_warrior_parent_and_impact_each_complete_their_source_delayed_camera_shake():
    for filename, owner, target_node in (
        ('ba75c6fc744cbe4325207e64.hero-power-source.json', 1, 1),
        ('d662fbec7d66e969372fb2fd.hero-power-prefab.json', 0, 0),
    ):
        program = json.loads((ROOT / 'local/runtime' / filename).read_text(encoding='utf-8'))
        machines = [machine for machine in program['machines']
                    if machine.get('owner_known') and machine.get('owner') == owner]
        assert len(machines) == 1
        invokes = [action for state in machines[0]['states'] for action in state['actions']
                   if action['name'] == 'InvokeMethod']
        assert len(invokes) == 1
        params = {param['name']: param for param in invokes[0]['params']}
        assert params['gameObject']['node'] == target_node
        assert params['behaviour']['text'] == 'CameraShaker'
        assert params['methodName']['text'] == 'StartShake'
        assert params['delay']['number'] == pytest.approx(.1, abs=1e-6)
        assert params['repeating']['number'] == 0
        assert params['repeatDelay']['number'] == 1
        assert params['cancelOnExit']['number'] == 0
        shakers = [(index, node['camera_shaker']) for index, node in enumerate(program['nodes'])
                   if node.get('camera_shaker')]
        assert len(shakers) == 1 and shakers[0][0] == target_node
        shaker = shakers[0][1]
        assert [shaker['amount'][axis] for axis in ('x', 'y', 'z')] == pytest.approx([.1, .1, .1])
        assert [key['time'] for key in shaker['curve']] == pytest.approx([0, .6])
        assert [key['value'] for key in shaker['curve']] == pytest.approx([1, 0])


def test_warrior_parent_and_impact_each_author_one_play_of_the_same_source_audio():
    sound_refs = []
    for filename in ('ba75c6fc744cbe4325207e64.hero-power-source.json',
                     'd662fbec7d66e969372fb2fd.hero-power-prefab.json'):
        program = json.loads((ROOT / 'local/runtime' / filename).read_text(encoding='utf-8'))
        refs = [param.get('asset_path') for machine in program['machines'] for state in machine['states']
                for action in state['actions'] if action['name'] == 'AudioPlayClipAction'
                for param in action['params'] if param['name'] == 'm_Sound']
        assert len(refs) == 1
        sound_refs.extend(refs)
    assert sound_refs == ['ebc29ba6584ae8990b9ee4ec.native-sound.json'] * 2


def test_warrior_analyzer_folder_keeps_native_source_audio_and_rule_hash(tmp_path, monkeypatch):
    before = dict(powerCardId='HERO_01bp', powerUsed=False, heroArmor=0, heroHealth=29,
                  legalPowerMove=True, legalEndTurn=False, mana=10, publicHash='before',
                  version=9, powerEntity=5, heroEntity=4)
    after = dict(version=10, powerCardId='HERO_01bp', powerUsed=True, heroArmor=2, heroHealth=29,
                 legalPowerMove=False, legalEndTurn=True, mana=8, publicHash='after')
    report = dict(result='PASS bounded formal Warrior armor GUI/input/source/DSP', publicHash='final',
                  displayHash='final', heroPower='HERO_01bp', warriorBefore=before, warriorAfter=after,
                  warriorEventOrderValid=True, warriorPlayHeroPowerCount=1, warriorGainArmorCount=1,
                  warriorSourceCueCount=1, warriorSourceCueSemanticsValid=True, warriorUnexpectedHitCueCount=0,
                  warriorMissileEventCount=0, warriorArrivalEventCount=0, warriorSourceRootCompleted=True,
                  warriorSourceRootReleased=True, warriorCameraShakerStarts=2, warriorParentCameraShakerStarts=1,
                  warriorImpactCloneCameraShakerStarts=1, warriorUnhandledActions=0, warriorParentUnhandledActions=0,
                  warriorImpactCloneUnhandledActions=0, warriorParentUnhandledActionDetails=[],
                  warriorImpactCloneUnhandledActionDetails=[], warriorUnboundAudioActions=0,
                  warriorSourceAudioPlayCount=2, listenerCount=1,
                  warriorInputUnlockedAfterSource=True, warriorInputUnlockSampleFrame=42,
                  warriorFollowup=dict(inputUnlockedDuringSource=True, sendMoveSubmitted=True, authorityAccepted=True,
                      sourceActiveAtSend=True, sourceTailCleanedAfterFollowup=True, sampleFrame=42,
                      submitVersion=10, acceptedVersion=11, actionCounterDelta=2, activeAfter=1,
                      castAuthorityVersion=10, castAuthorityEvents=[
                          dict(type='SpendMana', source=5, target=9, owner=0, amount=2),
                          dict(type='PlayHeroPower', source=9, target=5, owner=0, card_id=''),
                          dict(type='GainArmor', source=5, target=4, owner=0, amount=2)],
                      sendLatency=.18, latencyBudget=.217, publicHash='final', displayHash='final'),
                  records=[dict(phase='warrior-direct-cast', message='source play', audioSource='native-effect',
                                file='warrior.wav', actionCount=1) for _ in range(2)],
                  sampleFrames=4410, peak=.1, rate=44100, channels=2, callbacks=5, overflows=0,
                  clippedSamples=0, rms=.05)
    frame = dict(frame=42, version=10, authorityHealth=29, displayHealth=29, sourceCompleted=False, sourceActive=True,
                 sourceReleased=False, busy=True, inputLocked=False, followupSent=True, actionCounterDelta=2,
                 followupSendLatency=.18, hashMatched=False, endTurnAvailable=True, authorityArmor=2, displayArmor=2,
                 arrival=False, nativeEvents='')
    cleaned_frame = dict(frame=52, version=11, authorityHealth=29, displayHealth=29, sourceCompleted=True,
                 sourceActive=False, sourceReleased=True, busy=False, inputLocked=False, followupAccepted=True,
                 followupTailCleaned=True, publicHash='final', displayHash='final',
                 hashMatched=True, endTurnAvailable=False, authorityArmor=2, displayArmor=2,
                 arrival=False, nativeEvents='')
    for role in ('driver', 'observer'):
        player = tmp_path / role
        player.mkdir()
        role_before = dict(before, legalPowerMove=role == 'driver')
        role_after = dict(after, legalEndTurn=role == 'driver')
        payload = dict(report, role=role,
            warriorBefore=role_before, warriorAfter=role_after,
            listenerCount=1 if role == 'driver' else 0,
            warriorSourceAudioPlayCount=2 if role == 'driver' else 0,
            records=report['records'] if role == 'driver' else [],
            warriorInputUnlockedAfterSource=role == 'driver',
            warriorInputUnlockSampleFrame=42 if role == 'driver' else -1,
            warriorFollowup=report['warriorFollowup'] if role == 'driver' else
                dict(report['warriorFollowup'], sendMoveSubmitted=False))
        if role == 'observer':
            payload.pop('records')
        (player / 'report.json').write_text(json.dumps(payload), encoding='utf-8')
        (player / 'timeline.json').write_text(json.dumps({'frames': [frame, cleaned_frame] if role == 'driver' else [cleaned_frame]}), encoding='utf-8')
        if role == 'driver':
            (player / 'scene-mix.wav').write_bytes(b'driver PCM fixture')
        from PIL import Image
        Image.new('RGB', (4, 4), 'white').save(player / '00-ready.png')

    monkeypatch.setattr(fireblast_evidence, 'wav_metadata', lambda path: (dict(rate=44100, channels=2, frames=4410,
                          duration=.2, peak=.1), []))
    fireblast_evidence.analyze_warrior(tmp_path)

    result = json.loads((tmp_path / 'evidence-index.json').read_text(encoding='utf-8'))
    assert result['warrior_rule_hash'] == 'after'
    assert result['warrior_public_hash'] == 'final'
    assert result['native_action_audio_scope'].startswith('driver-only')
    assert result['warrior_audio_action_design'] == dict(
        parent_action_count=1, impact_clone_action_count=1, total_action_count=2,
        shared_sound_asset='ebc29ba6584ae8990b9ee4ec.native-sound.json')
    assert all(player['actual_warrior_armor']['source_root_released'] for player in result['players'])
    by_role = {player['role']: player['actual_warrior_armor'] for player in result['players']}
    assert 'actual_scene_mix' in next(player for player in result['players'] if player['role'] == 'driver')
    assert 'actual_scene_mix' not in next(player for player in result['players'] if player['role'] == 'observer')
    assert by_role['driver']['source_audio_play_count'] == 2
    assert by_role['observer']['source_audio_play_count'] is None
    assert len(result['native_action_audio']) == 2

    observer_path = tmp_path / 'observer' / 'report.json'
    observer = json.loads(observer_path.read_text(encoding='utf-8'))
    observer['publicHash'] = observer['displayHash'] = 'different'
    observer['warriorAfter']['publicHash'] = 'different'
    observer_path.write_text(json.dumps(observer), encoding='utf-8')
    with pytest.raises(AssertionError, match='public hashes differ'):
        fireblast_evidence.analyze_warrior(tmp_path)
