import json
from pathlib import Path

import pytest
from PIL import Image

from game.database import preset
from game.match import Match
from tools import fireblast_evidence
from tools.fireblast_evidence import analyze_druid_report, apply, prepare, run_players


ROOT = Path(__file__).resolve().parents[1]
SOURCE_CLIP = '74537c37a8e26deff87c5a4a.wav'


def test_druid_fixture_records_the_observed_authoritative_event_order():
    match = prepare(Match(['DruidA', 'MageB'], [2, 4], [preset(2), preset(4)], 52), druid=True)
    druid = match.game.players[0]
    before = [match.snapshot(seat) for seat in range(2)]

    assert before[0]['players'][0]['power']['card_id'] == 'HERO_06bp'
    assert before[0]['players'][1]['power']['card_id'] == 'HERO_08bp'
    assert before[0]['players'][0]['power']['cost'] == 2
    assert before[0]['players'][0]['hero']['attack'] == 0
    assert before[0]['players'][0]['hero']['armor'] == 0
    assert any(move['kind'] == 'power' and move['entity'] == druid.hero.power.entity_id and move['target'] == 0
               for move in match.legal_moves(0))
    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0)
    apply(match, 0, move, 'druid-formal-shapeshift')

    after = [match.snapshot(seat) for seat in range(2)]
    events = after[0]['events']
    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'Buff', 'GainArmor']
    spend, power, buff, armor = events
    assert spend['source'] == druid.hero.power.entity_id and spend['amount'] == 2
    assert spend['target'] != 0
    assert power['source'] == spend['target'] and power['target'] == druid.hero.power.entity_id
    assert 'card_id' not in power
    assert buff['source'] == armor['source'] == druid.hero.power.entity_id
    assert buff['target'] == armor['target'] == druid.hero.entity_id
    assert buff['visual']['attack'] == 1 and buff['visual']['attack_buffed'] is True
    assert armor['amount'] == 1
    assert druid.mana == 8 and after[0]['players'][0]['power']['used']
    assert after[0]['players'][0]['hero']['attack'] == 1
    assert after[0]['players'][0]['hero']['armor'] == 1
    assert after[0]['players'][0]['hero']['health'] == before[0]['players'][0]['hero']['health']
    assert after[0]['public_hash'] == after[1]['public_hash']


def _report(role='driver'):
    before = dict(powerCardId='HERO_06bp', powerUsed=False, heroAttack=0, heroArmor=0, heroHealth=29,
                  legalPowerMove=role == 'driver', legalEndTurn=False, mana=10, publicHash='before')
    after = dict(powerCardId='HERO_06bp', powerUsed=True, heroAttack=1, heroArmor=1, heroHealth=29,
                 legalPowerMove=False, legalEndTurn=role == 'driver', mana=8, publicHash='after')
    source_record = dict(phase='druid-direct-cast', message='source play', audioSource='native-effect', file=SOURCE_CLIP)
    return dict(result='PASS bounded formal Druid Shapeshift GUI/rules/source/DSP', role=role,
                publicHash='after', displayHash='after', heroPower='HERO_06bp', listenerCount=1 if role == 'driver' else 0,
                rate=48000, channels=1, sampleFrames=4800, callbacks=5, overflows=0, peak=.1, rms=.05, clippedSamples=0,
                druidBefore=before, druidAfter=after, druidEventOrderValid=True,
                druidSpendManaCount=1, druidPlayHeroPowerCount=1, druidBuffCount=1, druidGainArmorCount=1,
                druidHitEventCount=0, druidUnexpectedHeroCueCount=0, druidMissileEventCount=0, druidArrivalEventCount=0,
                druidCastRequested=True, druidCastCompleted=True, druidFirstArrivalTimeMissing=True,
                druidSourceStartActionCount=1, druidImpactCloneCount=1, druidImpactFinishedNotificationCount=1,
                druidSourceAnimationActionCount=1, druidSourceParticleActionCount=1,
                druidSourceCueCount=1, druidSourceCueSemanticsValid=True,
                druidSourceRootCompleted=True, druidSourceRootReleased=True,
                druidSourceAudioActionCount=1, druidSourceAudioPlayCount=1,
                druidUnhandledActions=0, druidUnboundAudioActions=0, druidMissingAudioPlaybackServices=0,
                druidInputUnlockedAfterSource=role == 'driver', druidInputUnlockSampleFrame=42 if role == 'driver' else -1,
                druidSourceAudioRecords=[source_record], records=[source_record])


def _frame(role='driver'):
    return dict(frame=42, target=0, authorityHealth=29, displayHealth=29,
                authorityAttack=1, displayAttack=1, authorityArmor=1, displayArmor=1,
                sourceCompleted=True, sourceReleased=True, busy=False, inputLocked=False,
                hashMatched=True, endTurnAvailable=role == 'driver', arrival=False, nativeEvents='Birth;Action')


def test_druid_analyzer_requires_rule_source_audio_and_unlock_evidence():
    report = _report()
    summary = analyze_druid_report(report, [_frame()])

    assert summary['event_order_valid'] is True
    assert summary['source_cue_semantics_valid'] is True
    assert summary['source_audio_action_count'] == summary['source_audio_play_count'] == 1
    assert summary['cast_requested'] and summary['cast_completed'] and summary['first_arrival_time_missing']
    assert summary['missile_event_count'] == summary['arrival_event_count'] == 0
    assert summary['source_start_action_count'] == summary['impact_clone_count'] == 1
    assert summary['impact_finished_notification_count'] == 1
    assert summary['source_animation_action_count'] == summary['source_particle_action_count'] == 1
    assert summary['authority_public_hash'] == summary['display_hash'] == 'after'

    for key, invalid in (('druidEventOrderValid', False), ('druidSourceCueSemanticsValid', False),
                         ('druidSourceRootReleased', False), ('druidSourceAudioPlayCount', 0),
                         ('druidCastRequested', False), ('druidCastCompleted', False),
                         ('druidFirstArrivalTimeMissing', False), ('druidArrivalEventCount', 1),
                         ('druidSourceStartActionCount', 0), ('druidImpactCloneCount', 0),
                         ('druidImpactFinishedNotificationCount', 0), ('druidSourceAnimationActionCount', 0),
                         ('druidSourceParticleActionCount', 0)):
        changed = _report()
        changed[key] = invalid
        with pytest.raises(AssertionError):
            analyze_druid_report(changed, [_frame()])

    changed = _report()
    changed['druidAfter']['heroHealth'] = 28
    with pytest.raises(AssertionError):
        analyze_druid_report(changed, [_frame()])


def test_druid_fixture_rejects_target_overrides_and_wires_the_official_source_path(tmp_path):
    output = tmp_path / 'invalid-druid-run'
    with pytest.raises(ValueError, match='uses its untargeted power'):
        run_players(37245, output, 'own-minion', '1280x720', False, False, False, druid=True)
    assert not output.exists()

    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    presentation = (ROOT / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    effect_player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    editor_route = (ROOT / 'UnityClient/Assets/Editor/NativeDruidHeroPowerRouteChecks.cs').read_text(encoding='utf-8')

    assert 'bool Druid{get{return Argument("--hero-power-replay-id")=="HERO_06bp";}}' in replay
    assert 'druidEventOrderValid=currentEvents.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Buff","GainArmor"})' in replay
    assert 'druidSourceCueSemanticsValid=sourceCues[0].data.target==0' in replay
    assert 'druidFirstArrivalTimeMissing=!cast.Flow.FirstArrivalTime.HasValue' in replay
    assert 'actual accepted HERO_06bp cast completes with missing FirstArrivalTime and zero Arrival events' in replay
    assert 'druidSourceStartActionCount==1&&druidImpactCloneCount==1&&druidImpactFinishedNotificationCount==1' in replay
    assert 'druidSourceAnimationActionCount==1&&druidSourceParticleActionCount==1' in replay
    cast_checks = replay.split('if((Review||Hunter||Priest||Rogue||Warrior||Druid||Warlock)&&!castChecksDone)', 1)[1].split(
        'Check(cast.UnhandledActions==0', 1)[0]
    assert 'else if(Druid)' in cast_checks
    assert 'actual accepted graph completes cast and arrival' in cast_checks
    assert 'druidSourceAudioActionCount==1&&druidSourceAudioPlayCount==1' in replay
    assert 'DruidSourceUnlockReady(state)&&druidInputUnlockedAfterSource' in replay
    assert 'parser.add_argument(\'--druid\'' in harness
    assert "['--hero-power-replay-id', 'HERO_06bp']" in harness
    druid_cue = presentation.split('else if(card=="HERO_06bp"', 1)[1].split('else if(card=="HERO_01bp"', 1)[0]
    assert 'duration=NativeDuration(card),apply=start' in druid_cue
    assert 'game_delay_min' not in replay and 'game_delay_max' not in replay
    assert 'SupportsNoMissileUntargeted(config)' in playback
    assert 'if(scene.SourceCamera!=null)Effect.ApplySourceCamera(scene.SourceCamera,scene.SourceCameraOrigin);' in playback
    assert 'DruidHeroPowerButtonClone' in effect_player and 'SupportsDruidFangButtonProgram' in effect_player
    assert 'sourceAge=claw.PlaybackTime' in effect_player and 'effect.CaptureSubtreeFrame(sourceAge,1)' in effect_player
    assert 'player.DrawHeroPowerButton(key,cue.data,before,after,powerBounds,.51f' in editor_route
    assert 'CountRgbPixels(buttonFrame,powerBounds)>0' in editor_route


def test_druid_folder_analysis_keeps_both_observers_and_native_audio_records(tmp_path, monkeypatch):
    metadata = dict(rate=48000, channels=1, frames=4800, duration=.1, peak=.1, rms=.05, clipped_samples=0)
    for role in ('driver', 'observer'):
        player = tmp_path / role
        player.mkdir()
        Image.new('RGB', (4, 4), 'white').save(player / '00-druid-ready.png')
        (player / 'report.json').write_text(json.dumps(_report(role)), encoding='utf-8')
        (player / 'timeline.json').write_text(json.dumps({'frames': [_frame(role)]}), encoding='utf-8')

    monkeypatch.setattr(fireblast_evidence, 'wav_metadata', lambda path: (dict(metadata, file=Path(path).name), []))
    fireblast_evidence.analyze_druid(tmp_path)

    result = json.loads((tmp_path / 'evidence-index.json').read_text(encoding='utf-8'))
    assert result['druid_rule_hash'] == 'after'
    assert [player['actual_druid_shapeshift']['source_root_released'] for player in result['players']] == [True, True]
    assert len(result['native_action_audio']) == 2

