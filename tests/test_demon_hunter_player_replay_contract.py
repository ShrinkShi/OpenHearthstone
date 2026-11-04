from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

import pytest

from tools.demon_hunter_player_evidence import (
    EXPECTED_DRIVER_PHASE_IMAGES,
    EXPECTED_IMAGES,
    analyze_timing,
    _timing_summary,
    _validate_h10_followup_timing,
)


ROOT = Path(__file__).resolve().parents[1]


def test_fireblast_evidence_exposes_bounded_demon_hunter_player_mode():
    result = subprocess.run(
        [sys.executable, '-X', 'utf8', '-m', 'tools.fireblast_evidence', '--help'],
        cwd=ROOT, capture_output=True, text=True, check=True,
    )
    assert '--demon-hunter' in result.stdout
    assert 'two GUI Players' in result.stdout
    assert '--timing-only' in result.stdout


def test_h10_player_contract_is_separate_from_mage_fallback_and_checks_real_source():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    runner = (ROOT / 'tools/demon_hunter_player_evidence.py').read_text(encoding='utf-8')

    assert '--h10-player-replay' in player
    assert 'NativeDemonHunterHeroPowerPlayback.TryGetPair' in player
    assert 'public Rect drawRect,targetDrawRect;' in player
    assert 'source.targetDrawRect=spell.TargetDrawRect' in player
    for field in ('unboundAudioActions', 'unboundAnimationActions', 'unboundMachineOwnerActions',
                  'unboundTimelineActions', 'unboundTimelineSounds', 'effectUnhandledActions',
                  'spellUnhandledActions', 'spellUnboundAudioActions', 'spellUnboundControllerActions',
                  'spellUnboundComponentActions', 'UnboundControllerDetails'):
        assert field in player
    assert 'AudioPlayClipAction' in player and 'AudioPlaythroughAction' in player
    assert 'new NativeSuperSpellPlayback' not in player
    assert '--h11-player-replay' not in player
    assert 'if(unlock.sampled&&move!=null)' in player
    assert 'effects.SourceBusy(SourceKey(castState))' in player
    assert 'endTurnSent=true;Invoke("SendMove",move)' in player
    assert 'unlock.sendMoveSubmitted=Field<bool>("pending")' in player
    assert 'unlock.authorityAccepted=true;unlock.acceptedVersion=state.version' in player
    assert 'float frameAllowance=updateFrameWallSeconds' in player
    assert 'float budget=.2f+frameAllowance' in player
    assert 'unlock.submissionFrame==unlock.frame' in player
    assert 'unlock.sourceActive&&!unlock.sourceReleased' in player
    assert 'not unlock.get(\'sourceReleased\')' in runner
    assert 'unlock.get(\'sendMoveSubmitted\') and unlock.get(\'sourceActiveAtSend\') and unlock.get(\'authorityAccepted\')' in runner
    assert "shutil.copytree(self.source_data / 'runtime'" in runner
    assert 'verify_production_unchanged' in runner
    assert 'productionIndexSha256Before' in runner and 'productionIndexSha256After' in runner
    assert "'self-hero-buff.png'" in runner and "'power-button.png'" in runner


def test_h10_fixture_waits_for_natural_opening_speech_expiry():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    assert 'if(!initialized){if(!OpeningSpeechSettled())return;Initialize(state);return;}' in player
    assert 'GetField("heroSpeech",Private)' in player
    assert 'GetProperty("OpeningComplete",Private)' in player and 'GetMethod("HasBubble",Private)' in player
    assert 'ClearBubbles' not in player and 'Stop()' not in player


def test_h10_one_frame_allowance_uses_monotonic_update_wall_boundaries():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    runner = (ROOT / 'tools/demon_hunter_player_evidence.py').read_text(encoding='utf-8')

    assert 'System.Diagnostics.Stopwatch.StartNew()' in player
    assert 'updateWallBoundary=(float)wallClock.Elapsed.TotalSeconds' in player
    assert 'unlock.powerAcceptedWallTime=(float)wallClock.Elapsed.TotalSeconds' in player
    assert 'unlock.submissionFrame=Time.frameCount' in player
    assert 'unlock.previousUpdateWallBoundary=previousUpdateWallBoundary' in player
    assert 'unlock.submissionUpdateWallBoundary=updateWallBoundary' in player
    assert 'float frameAllowance=updateFrameWallSeconds' in player
    assert 'float budget=.2f+frameAllowance' in player
    assert 'Time.unscaledDeltaTime' not in player
    assert '1f/60f' not in player
    assert 'expected_frame_allowance = submission_update_wall - previous_update_wall' in runner
    assert 'unlock.get(\'submissionFrame\') == unlock.get(\'frame\')' in runner
    assert '0 <= unlock.get(\'sendLatency\', -1) <= .2 + frame_allowance + .001' in runner


def test_h10_first_load_hitch_is_counted_as_one_measured_update_frame():
    timing = dict(castAcceptedFrame=100, frame=101, submissionFrame=101,
                  powerAcceptedWallTime=10.0, submissionWallTime=10.361,
                  previousUpdateWallBoundary=9.982, submissionUpdateWallBoundary=10.36,
                  inputFrameAllowance=.378, sendLatency=.36, latencyBudget=.578)

    _validate_h10_followup_timing(timing)
    timing['submissionFrame'] = 102
    with pytest.raises(AssertionError, match='timing evidence'):
        _validate_h10_followup_timing(timing)


def test_h10_peer_progress_waits_for_final_release_without_dropping_authority_hash_checks():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    poll = player.split('void PollPeer()', 1)[1].split('\n    void Finish(', 1)[0]

    assert 'if(String.IsNullOrEmpty(peer.castHash))return;' in poll
    assert 'Check(peer.castHash==report.cast.publicHash,"peer accepted cast authority hash differs")' in poll
    assert 'if(!peer.sourceReleased||!peer.unlocked||String.IsNullOrEmpty(peer.endHash))return;' in poll
    assert 'Check(peer.endHash==report.afterEndTurn.publicHash,"post-end-turn authority hashes differ")' in poll


def test_h10_submission_phase_screenshot_is_driver_specific():
    name = 'end-turn-submitted-during-source.png'
    assert name not in EXPECTED_IMAGES
    assert name in EXPECTED_DRIVER_PHASE_IMAGES


def test_h10_timing_only_suppresses_pngs_but_keeps_capture_requests_and_strict_timing():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    effect_player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    spell_playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    runner = (ROOT / 'tools/demon_hunter_player_evidence.py').read_text(encoding='utf-8')
    wrapper = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    assert 'replay.timingOnly=Argument("--h10-timing-only")=="1"' in player
    assert 'if(timingOnly){suppressedCaptureNames.Add(name);return;}' in player
    assert 'ScreenCapture.CaptureScreenshot(Path.Combine(folder,file))' in player
    assert "command.extend(['--h10-timing-only', '1'])" in runner
    assert "not list(folder.glob('*.png'))" in runner
    assert "--timing-only requires --demon-hunter" in wrapper
    assert "prepare-enabled-check" in runner and "prepare-cold-create-and-seek" in runner
    assert 'demonHunterProfileAlreadyValidated:entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId' in effect_player
    assert 'demonHunterProfileAlreadyValidated?NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(root):' in spell_playback

    source = {
        'frameTimings': [{'frame': frame, 'time': frame / 60, 'wallMilliseconds': float(frame)}
                         for frame in range(10)],
        'renderTimings': [{'stage': stage, 'milliseconds': value} for stage, value in (
            ('prepare-enabled-check', 0.01), ('prepare-cold-create-and-seek', 12.0),
            ('prepare-hot-seek', 1.0), ('draw-total', 0.5))],
    }
    summary = _timing_summary(source)
    assert summary['frameCount'] == 10 and summary['firstFrameMilliseconds'] == 0
    assert summary['stageSummary']['prepare-enabled-check']['medianMilliseconds'] == .01
    source['frameTimings'][4]['frame'] = source['frameTimings'][3]['frame']
    with pytest.raises(AssertionError, match='strictly ordered'):
        _timing_summary(source)


def test_h10_quiet_analyzer_revalidates_current_raw_players_when_provided(tmp_path):
    raw = os.environ.get('H10_QUIET_REPORT_DIR')
    if not raw:
        pytest.skip('set H10_QUIET_REPORT_DIR to revalidate an actual quiet dual-Player run')
    source = Path(raw).resolve()
    for name in ('run.json', 'runtime-overlay.json'):
        shutil.copy2(source / name, tmp_path / name)
    for role in ('driver', 'observer'):
        player = tmp_path / role
        player.mkdir()
        shutil.copy2(source / role / 'report.json', player / 'report.json')

    result = analyze_timing(tmp_path, '1920x1080')
    assert result['result'] == 'PASS H10 dual Player timing-only 1920x1080'
    assert all(row['timing']['frameCount'] >= 10 for row in result['players'])
    assert all(row['screenshots'] == [] for row in result['players'])


def test_h10_readiness_is_role_specific_and_precast_disconnect_is_decisive():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    runner = (ROOT / 'tools/demon_hunter_player_evidence.py').read_text(encoding='utf-8')
    initialize = player.split('void Initialize(StateView state)', 1)[1].split('\n    void TrySendPower()', 1)[0]
    update = player.split('void Update()', 1)[1].split('\n    bool EnsureResolution()', 1)[0]
    trace = player.split('void TraceInput(', 1)[1].split('\n    bool TryBindCast()', 1)[0]

    assert 'if(role=="driver")Check(legalPower' in initialize
    assert 'else Check(!legalPower&&(initial.moves??new Move[0]).Length==0' in initialize
    assert 'Capture("ready");TraceInput("ready-snapshot",initial)' in initialize
    assert 'TraceInput("state-left-play",state);Capture("state-left-play")' in update
    assert 'match left play before the H10 action' in update
    assert 'connection-lost-before-cast' in update
    assert 'waiting-input-unlock' in player
    assert 'H10 input never became available' in player
    for field in ('moveCount=moves.Length', 'moves=moves.Select(MoveOf)', 'powerRect=rect', 'pending=app!=null&&Field<bool>("pending")',
                  'actionCounter=app==null?0:Field<int>("actionCounter")', 'connected=connection!=null&&connection.Connected',
                  'connectionLost=app!=null&&Field<bool>("connectionLost")', 'status=app==null?"":Field<string>("status")',
                  'mulliganActive=mulliganActive', 'inputReady=!pending&&!inputLocked&&!mulliganActive&&!battleMenu'):
        assert field in trace
    for stage in ('"preflight"', '"mouse-down"', '"mouse-up"', '"waiting-power-location"'):
        assert stage in player
    assert 'SetField("EvidencePressed",true);Invoke("HandleBattleInput",EventType.MouseDown,0,inputPoint)' in player
    assert 'SetField("EvidencePressed",false);Invoke("HandleBattleInput",EventType.MouseUp,0,inputPoint)' in player
    assert 'EXPECTED_DRIVER_INPUT_IMAGES' in runner
    assert 'driver GUI input telemetry does not prove a connected legal click inside the H10 hit rect' in runner
    assert 'input screenshot missing:' in runner


def test_h10_releases_the_real_mouse_down_before_rechecking_authority_moves():
    player = (ROOT / 'UnityClient/Assets/Scripts/H10PlayerReplay.cs').read_text(encoding='utf-8')
    send = player.split('void TrySendPower()', 1)[1].split('\n    static MoveRow MoveOf', 1)[0]

    assert send.index('if(mouseDownSent)') < send.index('if(!moves.Any(')
    assert 'mouseDownSent=false;actionSent=true;TraceInput("mouse-up"' in send


def test_h10_card_type_child_states_are_not_reported_as_unbound_lifecycle_events():
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectInstance.cs').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/NativeDemonHunterHeroPowerChecks.cs').read_text(encoding='utf-8')
    idle = json.loads((ROOT / 'local/runtime/d7f540fad79d4d4b8410ee69.hero-power-prefab.json').read_text(encoding='utf-8'))
    machine = next(row for row in idle['machines'] if row['owner'] == 0)
    init_fx = next(row for row in machine['states'] if row['name'] == 'Init FX')
    get_target = next(row for row in machine['states'] if row['name'] == 'Get Target')

    assert any(action['name'] == 'SpellStateAction' for action in init_fx['actions'])
    assert any(row['event'] == 'Hero' and row['state'] == 'Init FX' for row in get_target['transitions'])
    assert 'bool internalState=transition==null&&clock.machine.states.Any' in playback
    assert '&&internalState){}' in playback
    assert 'spell.UnboundControllerActions==0&&spell.UnboundControllerDetails.Count==0' in checks


def test_h10_source_position_uses_the_live_power_control():
    board = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    dh = (ROOT / 'UnityClient/Assets/Scripts/NativeDemonHunterHeroPowerPlayback.cs').read_text(encoding='utf-8')
    player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/PresentationChecks.cs').read_text(encoding='utf-8')

    assert 'cardId!="HERO_10bp"' in board
    assert 'return new Vector2(960,480)' in board
    assert 'heroPowers.All(card=>NativeSuperSpellBoard.ResolvePresentationPosition(card,2,2,locate)==sourcePosition)' in checks
    assert 'TargetScreenPoint=target==null?null:position(target.entity)' in board
    assert 'TargetDrawRect' in board and 'TargetScreenRect' in board
    assert 'RenderDemonHunterSourceLayer(sampleAge)' in player
    assert 'RenderDemonHunterTargetLayer(sampleAge)' in player
    assert 'spell.DemonHunterTargetBodyRect' in player
    assert 'spell.DemonHunterTargetTransmission' in player
    assert 'age=NativeDemonHunterHeroPowerPlayback.PlaybackAge(age)' in player
    assert 'NativeDemonHunterHeroPowerPlayback.PlaybackAge(age)+spell.CastOffset' in player
    assert 'public static float PlaybackAge(float age)' in dh


def test_h10_runtime_dto_omits_only_recursive_field_and_keeps_source_gate_strict():
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeDemonHunterHeroPowerPlayback.cs').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/NativeDemonHunterHeroPowerChecks.cs').read_text(encoding='utf-8')
    dto = playback.split('sealed class RuntimeParam', 1)[1].split('\n    }', 1)[0]
    reader = playback.split('public static NativeProgram ReadProgram', 1)[1].split('\n    static bool FindEntities', 1)[0]
    particle_reader = reader.split('for (int i = 0; i < result.nodes.Length; i++)', 1)[1].split('return result;', 1)[0]

    assert 'property_data' not in dto
    for field in ('name', 'text', 'variable', 'asset_type', 'asset_path', 'array_type', 'asset_source',
                  'object_null', 'none_known', 'is_none', 'wrap_known', 'integer_known', 'array_known',
                  'unresolved', 'integer', 'kind', 'node', 'pre_wrap', 'post_wrap', 'array_count',
                  'number', 'vector', 'color', 'curve'):
        assert f'{field} = parameter.{field}' in reader
    assert 'HasNoRecursivePropertyData(rootPath)' in playback
    assert 'RuntimeParticleProgram' in reader and 'view.nodes.Length != particleView.nodes.Length' in reader
    for field in ('collision', 'empty_sub_emitters', 'restored', 'loop', 'awake', 'size3d', 'rotation3d',
                  'space', 'scaling', 'max_particles', 'duration', 'speed', 'delay', 'life', 'velocity',
                  'size', 'size_y', 'size_z', 'rotation', 'rotation_x', 'rotation_y', 'gravity', 'start_color',
                  'color', 'color_speed', 'emission', 'shape', 'modules', 'uv', 'render', 'unsupported'):
        assert f'{field} = source.{field}' in particle_reader
    assert 'ReadParticlePresence(json)' in reader and 'if (!particlePresence[i])' in reader
    assert 'node particle indices differ' in playback and 'node particles not restored=' in playback
    assert 'ExpectedParticleNodes' in playback and 'HERO_10bp node omits explicit particle presence' in playback
    assert 'ParticleProfileSummary' in playback and 'row.node.name' in playback and 'row.node.particle.restored' in playback
    assert 'H10 particle DTO readback:' in checks
    assert 'rejects a missing authored particle node' in checks
    assert 'rejects a particle object at an unreviewed node' in checks
    assert 'rejects an authored particle that is not restored' in checks
    assert 'TextureParameterShape(target)' in playback and 'ScaleParameterShape(source' in playback
    assert 'RootProgramFailure(parsedRoot)' in checks and 'changedTexture' in checks and 'changedScale' in checks
    assert 'owner12.Hero SetMaterialTexture[' in playback and 'SetScale[' in playback
    assert 'SourceGraphActions().ContainsKey("SetMaterialTexture")' in checks
    assert 'UnboundTextureActions==0' in checks and 'UnboundScaleActions==0' in checks
    assert 'ObserveDepthWarning' in checks and 'depthWarnings==0' in checks

    source = json.loads((ROOT / 'local/runtime/77a947e5d6ef3ec34b9f2f93.hero-power-source.json').read_text(encoding='utf-8'))
    assert not any('property_data' in row for machine in source['machines'] for state in machine['states']
                   for action in state['actions'] for row in action['params'])
    source_particles = [index for index, node in enumerate(source['nodes']) if isinstance(node.get('particle'), dict)]
    assert source_particles == [7, 8, 10, 18, 19, 21, 27, 28, 29, 30, 32, 38, 39]
    assert all('particle' in node for node in source['nodes'])
    assert all(source['nodes'][index]['particle']['restored'] for index in source_particles)
    assert all(source['nodes'][index]['particle'] is None for index in set(range(42)) - set(source_particles))


def test_demon_hunter_mode_rejects_target_and_unaffordable_fallbacks():
    target = subprocess.run(
        [sys.executable, '-X', 'utf8', '-m', 'tools.fireblast_evidence', '--run-players',
         '--demon-hunter', '--target', 'own-hero', '--output', str(ROOT / 'work/h10-invalid')],
        cwd=ROOT, capture_output=True, text=True,
    )
    assert target.returncode != 0
    assert 'accepts no --target override' in target.stderr

    unavailable = subprocess.run(
        [sys.executable, '-X', 'utf8', '-m', 'tools.fireblast_evidence', '--run-players',
         '--demon-hunter', '--unaffordable', '--output', str(ROOT / 'work/h10-invalid')],
        cwd=ROOT, capture_output=True, text=True,
    )
    assert unavailable.returncode != 0
    assert 'exact authoritative legal H10 input route' in unavailable.stderr
