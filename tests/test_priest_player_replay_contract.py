import json
from pathlib import Path

import pytest

from tools.fireblast_evidence import _ensure_player_report, run_players
from game.database import preset
from game.match import Match


ROOT = Path(__file__).resolve().parents[1]


def test_priest_player_fixture_rejects_non_own_hero_target_before_creating_output(tmp_path):
    output = tmp_path / 'invalid-priest-run'
    with pytest.raises(ValueError, match='requires --target own-hero'):
        run_players(37245, output, 'enemy-hero', '1280x720', False, False, False, priest=True)
    assert not output.exists()


def test_priest_player_replay_contract_tracks_authority_source_and_listener_evidence():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    assert 'bool Priest{get{return Argument("--hero-power-replay-id")=="HERO_09bp";}}' in replay
    assert 'Priest?state.players[0].hero.entity' in replay
    assert 'initialTargetHealth==(PriestFullHealth?30:28)&&health==30' in replay
    assert 'var spendEvents=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0' in replay
    assert 'powerEvents=allPowerEvents.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0)' in replay
    assert 'initial.players[0].power.card_id=="HERO_09bp"&&spendEvents.Length==1&&playerEntity!=0&&' in replay
    assert 'allTargetEvents.Length==1&&targetEvents.Length==1' in replay
    assert 'allHeals.Length==(PriestFullHealth?0:1)&&heals.Length==(PriestFullHealth?0:1)' in replay
    assert 'e.source==initial.players[0].hero.entity' not in replay
    assert 'Priest authority tuples spend=' in replay
    assert 'cast.Flow.FirstArrivalTime==null&&cast.Flow.Flights.Count()==0' in replay
    assert 'sourceAudioPlayCount=records.Count(record=>record.audioSource=="native-effect"&&record.message=="source play")' in replay
    assert 'listenerCount==1' in replay and 'tap.Overflows==0' in replay
    assert "if priest and target != 'own-hero'" in harness
    assert "['--hero-power-replay-id', 'HERO_09bp']" in harness
    assert "--target', 'own-hero" in harness


def test_missing_player_report_is_preserved_as_an_explicit_failure(tmp_path):
    (tmp_path / 'driver').mkdir()

    report = _ensure_player_report(tmp_path, 'driver', 0)

    assert report['role'] == 'driver'
    assert report['failureKind'] == 'missing-player-report'
    assert report['playerExitCode'] == 0
    assert Path(report['playerLog']).parent.name == 'driver'
    assert Path(report['playerLog']).name == 'player.log'
    assert report['result'].startswith('FAIL: Player exited with code 0')
    assert json.loads((tmp_path / 'driver/report.json').read_text(encoding='utf-8')) == report


def test_priest_observer_health_check_and_peer_failure_are_role_aware():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')

    assert 'initialTargetHealth==(PriestFullHealth?30:28)' in replay
    assert 'bool IsPairedPriestRole{get{return Priest&&(role=="driver"||role=="observer");}}' in replay
    assert 'IsPairedPriestRole(' not in replay
    assert 'CheckPriestPeerFailure();Tick();' in replay
    assert 'PollPriestPeerCompletion();return;' in replay
    assert 'Peer "+PeerRole+" formal replay failed:' in replay
    assert 'Peer "+PeerRole+" did not publish a replay report before the 15s completion bound' in replay
    assert 'report.role!=PeerRole||!String.Equals(report.publicHash,finishedReport.publicHash,StringComparison.Ordinal)' in replay
    assert 'PublishReportResult(finishResult);' in replay
    assert 'void OnApplicationQuit()' in replay
    assert 'Environment.ExitCode=1;Finish("FAIL: Player application quit before its replay report completed",1)' in replay


def test_priest_power_selection_is_source_gated_and_adopts_authoritative_target():
    player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    board = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    flow = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpell.cs').read_text(encoding='utf-8')
    impact = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperImpact.cs').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/NativePriestHeroPowerChecks.cs').read_text(encoding='utf-8')

    assert 'power.card_id!="HERO_08bp"&&!priest' in player
    assert 'priest&&!Enabled(entry)' in player
    assert 'type=priest?"HeroPowerSelection":"SpellEffect"' in player
    assert 'data.type=="HeroPowerSelection"&&before!=null&&after!=null' in board
    assert 'public static int ResolvePriestTarget(SemanticEvent data,StateView before,StateView after)' in board
    assert 'e.type=="HeroPowerTarget"' in board
    assert 'var heals=events.Where(e=>e!=null&&e.type=="Heal"&&e.source==data.source).ToArray();' in board
    assert 'var legacy=events.Where(e=>e!=null&&e.type=="Heal"&&e.source==data.source).ToArray();' in board
    assert 'CardId=="HERO_09bp"?' in board and 'e.type=="Heal"&&e.source==data.source' in board
    assert 'HeroPowerTarget conflicts with its same-batch Heal' in board
    assert 'targetCardId:scene.Placement.TargetRecord(clone?.Targets.GetTarget())?.card_id' in board
    assert 'selection)' in board
    assert 'targetedSelection?services.Targets.Count!=0:!validTarget' in flow
    assert 'HERO_09bp cast requires one distinct actual target' in flow
    assert 'missing H09 source profile fails closed before opening target selection' in checks
    assert 'accepted source-only cue adopts the selected H09 graph' in checks
    assert 'does not replay the already-started H09 source clip' in checks
    assert 'selection-time Start clone is created before a Heal target exists' in checks
    assert 'pre-acceptance Start clone targetless instead of backfilling it' in checks
    assert 'a full-health cast retains its authority-selected target without inventing a Heal event' in checks
    assert 'binds each post-acceptance Impact clone to the authority-selected target' in checks
    assert 'accepted source route binds its delayed Impact clone to the unique Heal entity' in checks
    assert 'impact.AddTarget(target);' in impact
    assert 'keeps every active source clone on that same Heal entity' not in checks


def test_priest_unlock_evidence_is_sampled_after_full_source_release():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    assert 'bool PriestSourceUnlockReady(StateView current)' in replay
    assert 'cast.Flow.Completed&&!presentation.Busy&&!presentation.InputLocked' in replay
    assert 'current.moves.Any(move=>move.kind=="end_turn")' in replay
    assert 'current.public_hash==presentation.Display.public_hash' in replay
    assert 'if(Priest&&(!PriestSourceUnlockReady(state)||!priestInputUnlockedAfterSource))' in replay
    assert 'public bool inputUnlockedAfterSource' in replay
    assert 'inputUnlockSampleFrame' in replay
    assert 'inputUnlockSampleTime,inputUnlockBatchTime' in replay
    assert 'sourceCompleted=sourceCompleted,sourceReleased=sourceReleased' in replay
    assert 'hashMatched=hashMatched,endTurnAvailable=endTurnAvailable' in replay
    assert "report['inputUnlockedAfterSource'] is True" in harness
    assert "report['inputUnlockSampleFrame'] > 0" in harness
    assert "sample['sourceCompleted'] and sample['sourceReleased'] and not sample['busy']" in harness
    assert "not sample['inputLocked'] and sample['hashMatched'] and sample['endTurnAvailable']" in harness
    assert 'inputLockedAfterSource' not in replay and 'inputLockedAfterSource' not in harness


def test_priest_stale_rejection_waits_for_the_source_tail_from_the_response_clock():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')

    assert 'const float PriestStaleResponseTimeout=10f,PriestStaleCancellationTimeout=8f' in replay
    assert 'if(step==5&&Age>2)' not in replay
    assert 'bool staleRejected=staleServerRejectionSeen;' in replay
    assert 'if(step==5&&message=="stale state version")' in replay
    assert 'if(Priest&&step==5&&message=="stale state version")' not in replay
    assert 'app.ServerActionRejected+=' in replay and 'stale server response observed on transport' in replay
    assert 'if(staleRejectionReceivedAt<0)' in replay
    assert 'Time.realtimeSinceStartup-staleRejectionReceivedAt>=PriestStaleCancellationTimeout' in replay
    assert 'rejectedFlow.TargetingCancelled&&!rejectedSelection.Busy&&rejectedFlow.Completed' in replay
    assert 'rejectedFlow.ActiveClones==0&&rejectedFlow.Pending==0' in replay
    assert 'sourceClock=observedSelection==null?0:observedSelection.Time' in replay
    assert 'serverStatus=app==null?null:Field<string>("status")' in replay
    assert 'pending=app!=null&&Field<bool>("pending")' in replay
    assert 'waiting for source cancellation tail' in replay


def test_priest_full_health_fixture_preserves_legal_power_target_and_no_heal():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    match = Match(['PriestA', 'MageB'], [6, 4], [preset(6), preset(4)], seed=2209)
    from tools.fireblast_evidence import prepare
    prepare(match, priest=True, priest_full_health=True)
    state = match.snapshot(0)

    assert state['players'][0]['hero']['health'] == 30
    assert state['players'][0]['power']['used'] is False
    assert state['players'][1]['power']['used'] is True
    assert not any(event['type'] == 'Heal' for event in match.observer.events)
    assert 'bool PriestFullHealth' in replay
    assert 'target and no Heal to the own-hero Impact' in replay
    assert 'priestMode=Priest?(PriestFullHealth?"full-health-no-heal":"damaged-target-heal")' in replay
    assert "--priest-full-health" in harness
    assert "report['priestTargetEventCount'] == 1" in harness
    assert "report['healEventCount'] == report['healCallbacks'] == 0" in harness
