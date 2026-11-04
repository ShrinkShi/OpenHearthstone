import json
import pytest

from game.database import preset
from game.match import Match
from tools.death_knight_player_evidence import (
    GHOUL,
    H11_AUDIO_COMPLETE_AGE,
    H11_AUTHORED_DURATION,
    H11_TOKEN_AUDIO_DURATION,
    H11_TIMELINE_START_AGE,
    HERO_POWER,
    PRODUCTION_INDEX,
    TestRuntime as RuntimeOverlay,
    analyze_playback_clock,
    h11_playback_age,
    h11_entry,
    prepare_death_knight,
    player_profile,
    sha256,
    _validate_h11_followup_timing,
)
from tools.importer import PROJECT


def test_h11_fixture_keeps_authority_pair_and_observer_without_end_turn():
    match = prepare_death_knight(
        Match(['DeathKnightA', 'MageB'], [1, 4], [preset(1), preset(4)], 20261021)
    )
    ready = match.snapshot(0)
    power = ready['players'][0]['power']
    assert power['card_id'] == HERO_POWER
    move = next(move for move in ready['moves'] if move.get('kind') == 'power')

    match.apply(0, dict(move, version=match.version, match_id=match.id, action_id='test-h11-cast'))
    driver = match.snapshot(0)
    observer = match.snapshot(1)
    play = [event for event in driver['events'] if event['type'] == 'PlayHeroPower']
    summons = [event for event in driver['events'] if event['type'] == 'Summon']
    assert len(play) == len(summons) == 1
    assert play[0]['target'] == summons[0]['source'] == power['entity']
    assert summons[0]['visual']['card_id'] == GHOUL
    ghoul = next(card for card in driver['players'][0]['field'] if card['entity'] == summons[0]['target'])
    assert (ghoul['attack'], ghoul['health']) == (1, 1)
    assert 'charge' in ghoul['keywords']
    assert driver['public_hash'] == observer['public_hash']
    assert not any(move.get('kind') == 'end_turn' for move in observer['moves'])
    assert any(move.get('kind') == 'end_turn' for move in driver['moves'])

    end_turn = next(move for move in driver['moves'] if move.get('kind') == 'end_turn')
    match.apply(0, dict(end_turn, version=match.version, match_id=match.id, action_id='test-h11-end'))
    ended = match.snapshot(0)
    assert any(event['type'] == 'Death' and event['target'] == ghoul['entity'] for event in ended['events'])
    assert all(card['entity'] != ghoul['entity'] for card in ended['players'][0]['field'])


@pytest.mark.parametrize('source_enabled', [False, True])
def test_h11_test_runtime_is_a_direct_child_copy_and_never_changes_production(tmp_path, source_enabled):
    production_before = sha256(PRODUCTION_INDEX)
    _, production_entry = h11_entry(PRODUCTION_INDEX)
    production_enabled = production_entry['enabled']

    source = tmp_path / 'source-data'
    runtime = source / 'runtime'
    runtime.mkdir(parents=True)
    index = {'entries': [{'card_id': 'HERO_11bp', 'enabled': source_enabled, 'duration': 5.0 if source_enabled else 0.0,
                          'replace': False, 'impact_delay': -1.0}]}
    source_index = runtime / 'native-effect-index.json'
    source_index.write_text(json.dumps(index), encoding='utf-8')
    source_before = sha256(source_index)
    (runtime / 'source.json').write_text('immutable fixture', encoding='utf-8')
    (source / 'card-database.json').write_text('{}', encoding='utf-8')

    overlay = RuntimeOverlay(source, PROJECT)
    path = overlay.path
    try:
        assert path.parent == PROJECT.resolve()
        assert (path / 'runtime/source.json').read_text(encoding='utf-8') == 'immutable fixture'
        copied, enabled = h11_entry(path / 'runtime/native-effect-index.json')
        assert enabled['enabled'] is True and enabled['duration'] == 5.0
        assert h11_entry(source_index)[1]['enabled'] is source_enabled
        assert overlay.verify_production_unchanged() == source_before
    finally:
        overlay.cleanup()

    assert not path.exists()
    assert sha256(PRODUCTION_INDEX) == production_before
    assert h11_entry(PRODUCTION_INDEX)[1]['enabled'] is production_enabled


def test_h11_player_harness_requires_a_real_followup_during_the_source_tail():
    root = PROJECT
    runner = (root / 'tools/death_knight_player_evidence.py').read_text(encoding='utf-8')
    player = (root / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    assert "'--data', str(overlay.path)" in runner
    assert "work_dir = PROJECT" in runner
    assert "'1280x720', '1920x1080'" in runner
    assert 'finally:' in runner and 'overlay.cleanup()' in runner
    assert "process_rows[role].get('dataRoot') == data_root" in runner
    assert "process_rows[role].get('dataIndexSha256') == index_hash" in runner
    assert "run.get('productionH11EnabledAfter') == source_enabled" in runner
    assert 'NativeDeathKnightHeroPowerPlayback.TryGetPair(powerEvents[0],before,state,out pair)' in player
    assert 'unlock.legalEndTurn=move!=null' in player
    assert 'state.seat==0&&state.active==0&&move!=null' in player
    assert 'if(pending||presentation.InputLocked)return;' in player
    assert 'Check(matched,"driver display hash has not caught the cast authority state")' not in player
    assert 'Invoke("SendMove",move)' in player
    assert 'unlock.sourceActiveAtSubmit=active' in player
    assert 'unlock.castToSubmitSeconds<=.2f+unlock.inputFrameAllowance+.005f' in player
    assert 'unlock.authorityResponseWhileSourceActive' in player
    assert 'unlock.sourceTailClearedAfterResponse' in player
    assert 'unlock.sourceActiveAtSample&&unlock.unlockedDuringSource' in player
    assert 'source.tokenAudioActiveBeyondIdentity=true' in player
    assert 'source.tokenPlaybackEndTime>=source.tokenCueCallbackTime' in player
    assert 'source.tokenCuePitch=cue.pitch' in player
    assert 'tokenAudioActiveAtMap=tokenAudioActive' in player
    assert 'boardCameraMask&(1<<30)' in player
    assert 'inputUnlockedDuringSource' in player
    assert player.count('void SampleUnlock(StateView state)') == 1


def test_h11_authority_response_submission_check_is_driver_only():
    player = (PROJECT / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    release = player.split('if(!source.released)return;', 1)[1].split('if(finalizing)PollPeer();', 1)[0]
    assert 'if(role=="driver")' in release
    assert 'Check(unlock.authorityResponseMatched&&unlock.sourceActiveAtSubmit' in release


def test_h11_real_followup_does_not_wait_for_display_hash_settle():
    root = PROJECT
    player = (root / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    analyzer = (root / 'tools/death_knight_player_evidence.py').read_text(encoding='utf-8')
    assert 'if(pending||presentation.InputLocked)return;' in player
    assert 'unlock.displayHashAtSample=displayHash' in player
    assert 'unlock.displayHashAtSubmit=displayHash;unlock.displayHashMatchedAtSubmit=matched' in player
    assert 'report.castDisplaySettleHash=presentation.Display.public_hash' in player
    assert 'displayMatchedAtAcceptance=displayHash==after.public_hash' in player
    assert 'report.cast.displayHash=presentation.Display.public_hash' not in player
    assert 'Check(matched,"driver display hash has not caught the cast authority state")' not in player
    assert 'unlocked.get(\'displayHashMatched\') is True' not in analyzer
    assert 'unlocked.get(\'displayHashMatchedAtSubmit\') is (submit_hash == cast.get(\'publicHash\'))' in analyzer
    assert "cast.get('publicHash') == cast.get('peerPublicHash')" in analyzer
    assert "cast.get('displayMatchedAtAcceptance') is (accepted_display_hash == cast.get('publicHash'))" in analyzer
    assert "ended.get('publicHash') == ended.get('peerPublicHash') == ended.get('displayHash')" in analyzer


def test_h11_player_uses_monotonic_wall_frames_and_keeps_acceptance_display_distinct():
    root = PROJECT
    player = (root / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    analyzer = (root / 'tools/death_knight_player_evidence.py').read_text(encoding='utf-8')
    submit = player.split('void SubmitEndTurn(StateView state)', 1)[1].split('\n    void ObserveEndTurnResponse', 1)[0]
    bind = player.split('bool TryBindCast(StateView state)', 1)[1].split('\n    void ObserveSource', 1)[0]
    assert 'System.Diagnostics.Stopwatch.StartNew()' in player
    assert 'updateWallBoundary=(float)wallClock.Elapsed.TotalSeconds' in player
    assert 'unlock.inputFrameAllowance=updateFrameWallSeconds' in submit
    assert 'unlock.submitFrame==unlock.sampleFrame' in submit
    assert 'Time.unscaledDeltaTime' not in submit
    assert '1f/60f' not in submit
    assert bind.index('castAcceptedWallTime=') < bind.index('report.cast=CastSnapshot(before,state,pair)')
    assert 'accepted_display_hash = cast.get(\'displayHash\')' in analyzer
    assert '_validate_h11_followup_timing(report, unlocked)' in analyzer


def test_h11_prepare_and_both_draws_share_the_actual_token_audio_age():
    effects = (PROJECT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    route = (PROJECT / 'UnityClient/Assets/Scripts/NativeDeathKnightHeroPowerPlayback.cs').read_text(encoding='utf-8')
    assert 'GetDeathKnightPlaybackAge(key,age,out tokenAudioPlaying)' in effects
    assert 'deathKnightSampleFrames' in effects and 'sampledFrame==Time.frameCount' in effects
    assert 'RenderAffineExcludingSubtree(mappedAge,17,2.5f)' in effects
    assert 'RenderAffineSubtree(mappedAge,17,1.25f)' in effects
    assert 'deathKnightPairs.ContainsKey(pair.Key)&&NativeDeathKnightHeroPowerPlayback.TokenAudioPlaying(pair.Value)' in effects
    assert 'return cues.Length==1?cues[0]:null' in route
    assert 'cue.playbackActive!=null&&cue.playbackActive()' in route


def test_h11_phase_screenshots_are_serialized_and_verified_before_pass():
    player = (PROJECT / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    assert 'captureQueue.Enqueue(file)' in player and 'StartCoroutine(DrainCaptures())' in player
    assert 'ScreenCapture.CaptureScreenshot(path)' in player and 'ValidScreenshot(path)' in player
    assert 'requiredImages.All(file=>images.Contains(file)&&ValidScreenshot(Path.Combine(folder,file)))' in player


def test_h11_fixture_waits_for_natural_opening_speech_expiry():
    player = (PROJECT / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    assert 'if(!initialized){if(!OpeningSpeechSettled())return;Initialize(state);return;}' in player
    assert 'GetField("heroSpeech",Private)' in player
    assert 'GetProperty("OpeningComplete",Private)' in player and 'GetMethod("HasBubble",Private)' in player
    assert 'ClearBubbles' not in player and 'Stop()' not in player


def test_h11_first_source_update_allowance_is_recomputed_from_stopwatch_boundaries():
    report = dict(castAcceptedFrame=2303, castAcceptedWallTime=10.0)
    unlocked = dict(submitFrame=2304, sampleFrame=2304, submitWallTime=10.3218,
                    previousUpdateWallBoundary=9.99, submissionUpdateWallBoundary=10.14,
                    inputFrameAllowance=.15, castToSubmitSeconds=.3218)

    _validate_h11_followup_timing(report, unlocked)
    unlocked['sampleFrame'] = 2303
    with pytest.raises(AssertionError, match='timing evidence'):
        _validate_h11_followup_timing(report, unlocked)


def test_h11_clock_preserves_source_audio_then_skips_only_the_empty_fsm_tail():
    assert H11_TIMELINE_START_AGE == pytest.approx(1 / 6)
    assert H11_TOKEN_AUDIO_DURATION == pytest.approx(192634 / 48000)
    assert H11_AUDIO_COMPLETE_AGE == pytest.approx(4.179875)
    grace = 512 * 4 / 51200
    identity_through = H11_AUDIO_COMPLETE_AGE + grace
    assert h11_playback_age(.499) == pytest.approx(.499)
    assert h11_playback_age(.5) == pytest.approx(.5)
    assert h11_playback_age(1.0) == pytest.approx(1.0)
    assert h11_playback_age(H11_AUDIO_COMPLETE_AGE - .001) == pytest.approx(H11_AUDIO_COMPLETE_AGE - .001)
    assert h11_playback_age(H11_AUDIO_COMPLETE_AGE, grace) == H11_AUDIO_COMPLETE_AGE
    assert h11_playback_age(identity_through, grace) == H11_AUTHORED_DURATION
    assert h11_playback_age(identity_through, grace, True) == pytest.approx(identity_through)
    assert h11_playback_age(identity_through + .2, grace, True) == pytest.approx(identity_through + .2)
    assert h11_playback_age(20, grace, True) == pytest.approx(H11_AUTHORED_DURATION - .001)

    source = {
        'playbackClock': {'timelineStartAge': H11_TIMELINE_START_AGE,
                          'tokenAudioDuration': H11_TOKEN_AUDIO_DURATION,
                          'audioGraceSeconds': grace,
                          'identityThroughAge': identity_through,
                          'authoredDuration': H11_AUTHORED_DURATION},
        'playbackClockSamples': [
            {'wallAge': 0, 'sourceAge': .1, 'playbackAge': .1, 'tokenAudioActiveAtMap': False},
            {'wallAge': .4, 'sourceAge': .5, 'playbackAge': .5, 'tokenAudioActiveAtMap': False},
            {'wallAge': identity_through - .101,
             'sourceAge': identity_through - .001,
             'playbackAge': identity_through - .001, 'tokenAudioActiveAtMap': True},
            {'wallAge': identity_through - .1,
             'sourceAge': identity_through, 'playbackAge': identity_through,
             'tokenAudioActiveAtMap': True},
            {'wallAge': identity_through + .1,
             'sourceAge': identity_through + .2, 'playbackAge': identity_through + .2,
             'tokenAudioActiveAtMap': True},
            {'wallAge': identity_through + .2,
             'sourceAge': identity_through + .3, 'playbackAge': H11_AUTHORED_DURATION,
             'tokenAudioActiveAtMap': False},
        ],
        'releaseFrameAllowance': .04,
        'duration': 4.08,
        'tokenCueSeen': True,
        'tokenPlaybackObserved': True,
        'tokenPlaybackEnded': True,
        'tokenAudioActiveBeyondIdentity': True,
        'tokenCueCallbackTime': .02,
        'tokenCuePitch': 1.0,
        'tokenPlaybackFirstActiveTime': .021,
        'tokenPlaybackEndTime': 4.04,
        'tokenPlaybackEndFrameAllowance': .03,
        'tokenCallbackToEndSeconds': 4.02,
        'releaseTime': 4.08,
    }
    dsp = {'rate': 51200, 'dspBuffer': 512, 'dspBuffers': 4}
    summary = analyze_playback_clock(source, dsp)
    assert summary['samples'] == 6
    assert summary['lastSourceAge'] == pytest.approx(identity_through + .3)
    assert summary['audioGraceSeconds'] == pytest.approx(grace)
    assert summary['tokenCallbackToEndSeconds'] == pytest.approx(4.02)

    source['playbackClockSamples'][-1]['playbackAge'] = identity_through + .3
    with pytest.raises(AssertionError, match='actual-token-aware'):
        analyze_playback_clock(source, dsp)

    source['playbackClockSamples'][-1]['playbackAge'] = H11_AUTHORED_DURATION
    source['tokenCallbackToEndSeconds'] = 3.8
    with pytest.raises(AssertionError, match='real token WAV playback'):
        analyze_playback_clock(source, dsp)

    source['tokenCallbackToEndSeconds'] = 3.8
    source['tokenCuePitch'] = 1.1
    source['tokenPlaybackEndTime'] = source['tokenCueCallbackTime'] + 3.8
    source['tokenPlaybackEndFrameAllowance'] = .03
    source['releaseTime'] = source['tokenPlaybackEndTime']
    assert analyze_playback_clock(source, dsp)['tokenCuePitch'] == pytest.approx(1.1)

    source['releaseTime'] += .5
    with pytest.raises(AssertionError, match='remained alive'):
        analyze_playback_clock(source, dsp)


def test_player_profiles_keep_worst_case_action_id_within_authority_limit():
    for role in ('driver', 'observer'):
        profile = player_profile('0123456789abcdef', role)
        longest_action_id = f'{profile}-{2**31 - 1}-{"f" * 32}'
        assert len(longest_action_id) <= 80
