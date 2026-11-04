import json
from pathlib import Path

import pytest
from PIL import Image

from game.database import preset
from game.match import Match
from tools import fireblast_evidence
from tools.fireblast_evidence import analyze_warlock_report, apply, prepare, run_players


ROOT = Path(__file__).resolve().parents[1]
SOURCE_CLIP = '54ef1e4a536ef9d7d3565210.wav'


def test_warlock_fixture_records_life_tap_self_damage_and_draw_order():
    match = prepare(Match(['WarlockA', 'MageB'], [9, 4], [preset(9), preset(4)], 52), warlock=True)
    before = match.snapshot(0)
    warlock = match.game.players[0]

    assert before['seat'] == 0
    assert [player['power']['card_id'] for player in before['players']] == ['HERO_07bp', 'HERO_08bp']
    assert before['players'][0]['hero']['health'] == 29
    assert before['players'][0]['mana'] == 10
    assert before['players'][0]['power']['used'] is False and before['players'][1]['power']['used'] is True
    assert before['players'][0]['deck_count'] > 0 and len(before['players'][0]['hand']) < 10

    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0)
    apply(match, 0, move, 'warlock-formal-life-tap')
    after = match.snapshot(0)
    events = after['events']

    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'Hit', 'Draw']
    spend, power, hit, draw = events
    assert spend['source'] == warlock.hero.power.entity_id and spend['amount'] == 2 and spend['target'] != 0
    assert power['source'] == spend['target'] and power['target'] == warlock.hero.power.entity_id
    assert 'card_id' not in power
    assert hit['source'] == warlock.hero.power.entity_id and hit['target'] == warlock.hero.entity_id
    assert hit['owner'] == 0 and hit['amount'] == 2
    assert draw['source'] == warlock.hero.power.entity_id and draw['owner'] == 0 and draw['target'] != 0
    assert warlock.mana == 8 and after['players'][0]['hero']['health'] == 27
    assert len(after['players'][0]['hand']) == len(before['players'][0]['hand']) + 1
    assert after['players'][0]['deck_count'] == before['players'][0]['deck_count'] - 1
    assert after['public_hash'] == match.snapshot(1)['public_hash']


def _report(role='driver'):
    visible_hand_before = 7 if role == 'driver' else 0
    visible_hand_after = visible_hand_before + 1 if role == 'driver' else 0
    before = dict(powerCardId='HERO_07bp', powerUsed=False, heroHealth=29, mana=10,
                  handCount=visible_hand_before, deckCount=24,
                  legalPowerMove=role == 'driver', legalEndTurn=False, publicHash='before')
    after = dict(powerCardId='HERO_07bp', powerUsed=True, heroHealth=27, mana=8,
                 handCount=visible_hand_after, deckCount=23,
                 legalPowerMove=False, legalEndTurn=role == 'driver', publicHash='after')
    source_record = dict(phase='warlock-direct-cast', message='source play', audioSource='native-effect', file=SOURCE_CLIP)
    return dict(result='PASS bounded formal Warlock Life Tap GUI/rules/source/DSP', role=role,
                publicHash='after', displayHash='after', heroPower='HERO_07bp', listenerCount=1 if role == 'driver' else 0,
                rate=48000, channels=2, sampleFrames=9600, callbacks=10, overflows=0, peak=.1, rms=.05, clippedSamples=0,
                warlockBefore=before, warlockAfter=after, warlockEventOrderValid=True,
                warlockSpendManaCount=1, warlockPlayHeroPowerCount=1, warlockHitCount=1, warlockDrawCount=1,
                warlockSourceCueCount=1, warlockSourceCueSemanticsValid=True,
                warlockSourceWaitActionCount=1, warlockSourceSpellStateActionCount=2,
                warlockSourceRootCompleted=True, warlockSourceRootReleased=True,
                warlockSourceAudioActionCount=1, warlockSourceAudioPlayCount=1,
                warlockUnhandledActions=0, warlockUnboundAudioActions=0,
                warlockMissileEventCount=0, warlockArrivalEventCount=0,
                warlockInputUnlockedAfterSource=role == 'driver',
                warlockInputUnlockSampleFrame=42 if role == 'driver' else -1,
                warlockSourceAudioRecords=[source_record])


def _frame(role='driver'):
    return dict(frame=42, target=0, authorityHealth=27, displayHealth=27,
                sourceCompleted=True, sourceReleased=True, busy=False, inputLocked=False,
                hashMatched=True, endTurnAvailable=role == 'driver', arrival=False, nativeEvents='')


def test_warlock_analyzer_requires_rules_source_audio_and_unlock_evidence():
    report = _report()
    summary = analyze_warlock_report(report, [_frame()])
    assert summary['event_order_valid'] is True
    assert summary['source_cue_semantics_valid'] is True
    assert summary['source_wait_action_count'] == 1
    assert summary['source_spell_state_action_count'] == 2
    assert summary['source_audio_action_count'] == summary['source_audio_play_count'] == 1
    assert summary['input_unlocked_after_source'] is True
    assert summary['authority_public_hash'] == summary['display_hash'] == 'after'

    observer = _report('observer')
    observer_summary = analyze_warlock_report(observer, [_frame('observer')])
    assert observer_summary['rule_after']['handCount'] == 0
    observer['warlockAfter']['handCount'] = 1
    with pytest.raises(AssertionError):
        analyze_warlock_report(observer, [_frame('observer')])

    for key, invalid in (('warlockEventOrderValid', False), ('warlockSourceCueSemanticsValid', False),
                         ('warlockSourceRootReleased', False), ('warlockSourceAudioPlayCount', 0),
                         ('warlockSourceAudioActionCount', 0), ('warlockHitCount', 0),
                         ('warlockDrawCount', 0), ('warlockUnhandledActions', 1),
                         ('warlockSourceWaitActionCount', 0), ('warlockSourceSpellStateActionCount', 1)):
        changed = _report()
        changed[key] = invalid
        with pytest.raises(AssertionError):
            analyze_warlock_report(changed, [_frame()])

    changed = _report()
    changed['warlockAfter']['handCount'] = 7
    with pytest.raises(AssertionError):
        analyze_warlock_report(changed, [_frame()])


def test_warlock_fixture_rejects_target_override_and_wires_source_route(tmp_path):
    output = tmp_path / 'invalid-warlock-run'
    with pytest.raises(ValueError, match='untargeted power'):
        run_players(37245, output, 'own-minion', '1280x720', False, False, False, warlock=True)
    assert not output.exists()

    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    app = (ROOT / 'UnityClient/Assets/Scripts/ClientApp.cs').read_text(encoding='utf-8')
    presentation = (ROOT / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    selection = (ROOT / 'UnityClient/Assets/Scripts/NativeSpellObjectSelection.cs').read_text(encoding='utf-8')
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    effects = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    source_checks = (ROOT / 'UnityClient/Assets/Editor/NativeWarlockHeroPowerChecks.cs').read_text(encoding='utf-8')

    assert 'bool Warlock{get{return Argument("--hero-power-replay-id")=="HERO_07bp";}}' in replay
    assert 'warlockEventOrderValid=currentEvents.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Hit","Draw"})' in replay
    assert 'warlockSourceCueSemanticsValid=sourceCues[0].data.target==0' in replay
    assert 'warlockSourceWaitActionCount==1&&warlockSourceSpellStateActionCount==2' in replay
    assert 'warlockSourceRootCompleted=warlockSourceWaitActionCount==1&&warlockSourceSpellStateActionCount==2' in replay
    assert 'Check(cast.CardId=="HERO_07bp"&&cast.Flow==null' in replay
    assert 'spell.Busy||spell.SourceRootPending' in effects
    assert 'return (waits!=1||states!=2)&&Time<4.5f;' in playback
    assert 'spell.data.card_id=="HERO_07bp")continue;' in presentation
    assert 'four-second decorative tail does not hold input after Hit and Draw' in source_checks
    assert 'warlockSourceAudioActionCount==1&&warlockSourceAudioPlayCount==1' in replay
    assert 'WarlockSourceUnlockReady(state)&&warlockInputUnlockedAfterSource' in replay
    assert "parser.add_argument('--warlock'" in harness
    assert "['--hero-power-replay-id', 'HERO_07bp']" in harness
    warlock_cue = presentation.split('else if(card=="HERO_07bp"', 1)[1].split('else if(card=="HERO_09bp"', 1)[0]
    assert 'apply=float.MaxValue' in warlock_cue and 'duration=NativeDuration(card)' in warlock_cue
    assert 'ValidateWarlockHeroPowerRoot(parent,config)' in playback
    assert 'case 2:return SourceAuto();' in selection
    assert 'NativeSuperSpellBoard.ResolvePresentationPosition(data.card_id,data.source,id' in app
    assert 'entity=>presentation.NativePosition(entity,locations)' in app
    assert 'visualRoot.kind!="Spell"||visualRoot.location!=2' in playback
    assert 'new NativeSpellTransform(sourceTransform,Effect.Nodes[1].transform,scene.Placement.Location,scene.Placement.Facing).UpdatePosition();' in playback
    assert playback.index('Effect.Seek(0);') < playback.index('new NativeSpellTransform(')
    assert 'Effect.UpdateOriginalPosition();' in playback


def test_warlock_folder_analysis_uses_source_sidecar_and_listener_capture(tmp_path, monkeypatch):
    metadata = dict(rate=48000, channels=2, frames=9600, duration=.1, peak=.1, rms=.05, clipped_samples=0)
    for role in ('driver', 'observer'):
        player = tmp_path / role
        player.mkdir()
        Image.new('RGB', (4, 4), 'white').save(player / '00-warlock-ready.png')
        generic = dict(result='PASS bounded formal Warlock Life Tap GUI/rules/source/DSP', role=role,
                       publicHash='after', displayHash='after', heroPower='HERO_07bp',
                       checks=20, width=1920, height=1080, rate=48000, channels=2, sampleFrames=9600,
                       callbacks=10, overflows=0, peak=.1, rms=.05, clippedSamples=0, listenerCount=1 if role == 'driver' else 0)
        (player / 'report.json').write_text(json.dumps(generic), encoding='utf-8')
        (player / 'warlock-evidence.json').write_text(json.dumps(_report(role)), encoding='utf-8')
        (player / 'timeline.json').write_text(json.dumps({'frames': [_frame(role)]}), encoding='utf-8')

    monkeypatch.setattr(fireblast_evidence, 'wav_metadata', lambda path: (dict(metadata, file=Path(path).name), []))
    fireblast_evidence.analyze_warlock(tmp_path)

    result = json.loads((tmp_path / 'evidence-index.json').read_text(encoding='utf-8'))
    assert result['warlock_rule_hash'] == 'after'
    assert result['source_pcm'][0]['file'] == SOURCE_CLIP
    assert [player['actual_warlock_life_tap']['source_root_released'] for player in result['players']] == [True, True]
