import json
import hashlib
import array
from pathlib import Path

from PIL import Image
import pytest

from game.database import preset
from game.match import Match
from tools import fireblast_evidence
from tools.fireblast_evidence import analyze_paladin_report, apply, prepare, run_players


ROOT = Path(__file__).resolve().parents[1]
AREA_REFERENCE = 'ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10'


def test_paladin_fixture_records_one_authoritative_silver_hand_recruit():
    match = prepare(Match(['PaladinA', 'MageB'], [5, 4], [preset(5), preset(4)], 52), paladin=True)
    paladin = match.game.players[0]
    before = [match.snapshot(seat) for seat in range(2)]

    assert [view['players'][0]['power']['card_id'] for view in before] == ['HERO_04bp', 'HERO_04bp']
    assert before[0]['players'][0]['field'] == []
    assert before[0]['players'][0]['power']['used'] is False
    assert any(move['kind'] == 'power' and move['entity'] == paladin.hero.power.entity_id and move['target'] == 0
               for move in match.legal_moves(0))
    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0)
    apply(match, 0, move, 'paladin-formal-recruit')

    after = [match.snapshot(seat) for seat in range(2)]
    events = after[0]['events']
    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'Summon']
    spend, power, summon = events
    assert spend['source'] == paladin.hero.power.entity_id and spend['amount'] == 2 and spend['target'] != 0
    assert power['source'] == spend['target'] and power['target'] == paladin.hero.power.entity_id
    assert 'card_id' not in power
    assert summon['source'] == paladin.hero.power.entity_id and summon['owner'] == 0
    assert summon['visual']['card_id'] == 'CS2_101t' and summon['target'] != 0
    assert paladin.mana == 8 and after[0]['players'][0]['power']['used']
    recruit = after[0]['players'][0]['field']
    assert len(recruit) == 1 and recruit[0]['card_id'] == 'CS2_101t'
    assert recruit[0]['attack'] == recruit[0]['health'] == 1
    assert after[0]['public_hash'] == after[1]['public_hash']


def _report(role='driver'):
    before = dict(powerCardId='HERO_04bp', powerUsed=False, mana=10, recruitCount=0, recruitEntity=0,
                  recruitCardId='', recruitAttack=0, recruitHealth=0, legalPowerMove=role == 'driver',
                  legalEndTurn=False, publicHash='before')
    after = dict(powerCardId='HERO_04bp', powerUsed=True, mana=8, recruitCount=1, recruitEntity=21,
                 recruitCardId='CS2_101t', recruitAttack=1, recruitHealth=1, legalPowerMove=False,
                 legalEndTurn=role == 'driver', publicHash='after')
    source_record = dict(phase='paladin-direct-cast', message='source play', audioSource='native-effect',
                         file='paladin-area-clip.wav')
    return dict(result='PASS bounded formal Paladin Recruit GUI/FriendlyArea/rules/DSP capture', role=role,
                publicHash='after', displayHash='after', heroPower='HERO_04bp', listenerCount=1 if role == 'driver' else 0,
                rate=48000, channels=2, sampleFrames=9600, callbacks=10, overflows=0, peak=.1, rms=.05, clippedSamples=0,
                paladinBefore=before, paladinAfter=after, paladinEventOrderValid=role == 'driver',
                paladinSpendManaCount=1 if role == 'driver' else 0,
                paladinPlayHeroPowerCount=1 if role == 'driver' else 0,
                paladinSummonCount=1 if role == 'driver' else 0,
                paladinRecruitSummonCount=1 if role == 'driver' else 0,
                paladinSourceCueCount=1, paladinSourceCueSemanticsValid=True,
                paladinFriendlyAreaOnlyReconstruction=True, paladinParentFlowConstructed=False,
                paladinAreaReference=AREA_REFERENCE, paladinAreaCloneCreations=1,
                paladinAreaFinishedNotifications=1, paladinAreaPending=0,
                paladinAudioActionCount=1, paladinAudioPlayCount=1, paladinUnhandledActions=0,
                paladinUnboundAudioActions=0, paladinAreaCompleted=True, paladinAreaReleased=True,
                paladinInputUnlockedAfterArea=role == 'driver', paladinInputUnlockSampleFrame=42 if role == 'driver' else -1,
                paladinTailEndTurnSubmitted=role == 'driver', paladinTailSubmitFrame=42 if role == 'driver' else -1,
                paladinTurnSubmitVersion=7 if role == 'driver' else -1,
                paladinNextTurnAppliedDuringTail=role == 'driver', paladinNextTurnVersion=8 if role == 'driver' else -1,
                paladinNextTurnAppliedFrame=51 if role == 'driver' else -1,
                paladinInputOpenDuringNextTurnTail=role == 'driver', paladinNextTurnFrame=55 if role == 'driver' else -1,
                paladinTailReleaseFrame=72 if role == 'driver' else -1,
                paladinNextTurnPublicHash='next-turn' if role == 'driver' else '',
                paladinNextTurnDisplayHash='next-turn' if role == 'driver' else '',
                paladinTailReleasedAfterNextTurn=role == 'driver',
                paladinSourceAudioRecords=[source_record])


def _frame(role='driver'):
    if role == 'observer':
        return dict(frame=42, target=0, sourceCompleted=True, sourceReleased=True, sourceTailActive=False,
                    activePlayer=0, turnStart=False, busy=False, inputLocked=False, hashMatched=True,
                    endTurnAvailable=False, nativeEvents='FriendlyArea:Finished')
    return dict(frame=42, version=7, target=0, activePlayer=0, sourceCompleted=True, sourceReleased=False,
                sourceTailActive=True, busy=True, inputLocked=False, turnStart=False, hashMatched=True,
                endTurnAvailable=True, nativeEvents='SpendMana,PlayHeroPower,Summon')


def _frames(role='driver'):
    if role == 'observer':
        return [_frame(role)]
    applied = dict(frame=51, version=8, target=0, activePlayer=1, sourceCompleted=True, sourceReleased=False,
                   sourceTailActive=True, busy=True, inputLocked=False, turnStart=True, hashMatched=False,
                   endTurnAvailable=False, nativeEvents='TURN_START')
    synchronized = dict(applied, frame=55, hashMatched=True)
    released = dict(synchronized, frame=72, sourceReleased=True, sourceTailActive=False, busy=False)
    return [_frame(role), applied, synchronized, released]


def _observer_tail_contract():
    # Minimal evidence from f024-paladin-recruit-audio-r2: observer power v6->7,
    # accepted end-turn v7->8, and its first TURN_START frame while the Area tail is locked.
    initial_hash = '5285a68a30d9d481fddc222ca0fb910d358ff9ad4b335c95b6ee5786adf450af'
    power_hash = '15f756929a0c62dbb1f00085776e0e16ffa8ab883ee8dbfdebb2d9f5a121fa06'
    turn_hash = '560f687460ae301eb7273d52b45c3a74725515f14bf3fa410a9c8546edd8b5f0'
    report = _report('observer')
    report['paladinBefore']['publicHash'] = initial_hash
    report['paladinAfter']['publicHash'] = turn_hash
    report['publicHash'] = turn_hash
    report['displayHash'] = power_hash
    report['paladinAreaReleased'] = False
    authority = [
        dict(error='', seat=-1, command=dict(kind='GAME_START'), version=6, hash=initial_hash),
        dict(error='', seat=0, command=dict(kind='power', version=6), version=7, hash=power_hash),
        dict(error='', seat=0, command=dict(kind='end_turn', version=7), version=8, hash=turn_hash),
    ]
    frames = [dict(frame=2157, version=8, activePlayer=1, turnStart=True,
                   sourceTailActive=True, busy=True, inputLocked=True)]
    return report, frames, authority


def test_paladin_analyzer_requires_authoritative_summon_area_only_route_and_input_unlock():
    report = _report()
    summary = analyze_paladin_report(report, _frames())

    assert summary['event_order_valid'] is True
    assert summary['recruit_summon_count'] == summary['area_clone_creations'] == 1
    assert summary['friendly_area_only_reconstruction'] is True
    assert summary['parent_flow_constructed'] is False
    assert summary['input_unlocked_after_area'] is True
    assert summary['end_turn_submitted_during_tail'] is True
    assert summary['next_turn_applied_during_tail'] is True
    assert summary['input_open_during_next_turn_tail'] is True
    assert summary['tail_released_after_next_turn'] is True
    assert summary['authority_public_hash'] == summary['display_hash'] == 'after'

    observer = _report('observer')
    observer_summary = analyze_paladin_report(observer, [])
    assert observer_summary['rule_after']['recruitCardId'] == 'CS2_101t'

    for key, invalid in (('paladinEventOrderValid', False), ('paladinRecruitSummonCount', 2),
                         ('paladinSourceCueSemanticsValid', False), ('paladinFriendlyAreaOnlyReconstruction', False),
                         ('paladinAreaReference', 'Unverified.prefab:11111111111111111111111111111111'),
                         ('paladinAreaCloneCreations', 2), ('paladinAreaFinishedNotifications', 0),
                         ('paladinParentFlowConstructed', True), ('paladinUnhandledActions', 1)):
        changed = _report()
        changed[key] = invalid
        with pytest.raises(AssertionError):
            analyze_paladin_report(changed, _frames())

    for key in ('paladinTailEndTurnSubmitted', 'paladinNextTurnAppliedDuringTail',
                'paladinInputOpenDuringNextTurnTail', 'paladinTailReleasedAfterNextTurn'):
        changed = _report()
        changed[key] = False
        with pytest.raises(AssertionError):
            analyze_paladin_report(changed, _frames())

    for key, invalid in (('busy', False), ('sourceTailActive', False), ('inputLocked', True), ('hashMatched', False)):
        changed_frames = _frames()
        changed_frames[0][key] = invalid
        with pytest.raises(AssertionError):
            analyze_paladin_report(_report(), changed_frames)

    changed = _report()
    changed['paladinAfter']['recruitCardId'] = 'CS2_102t'
    with pytest.raises(AssertionError):
        analyze_paladin_report(changed, _frames())

    quiet = _report()
    quiet['paladinAudioActionCount'] = quiet['paladinAudioPlayCount'] = 0
    quiet['paladinSourceAudioRecords'] = []
    assert analyze_paladin_report(quiet, _frames())['source_audio_play_count'] == 0


def test_paladin_observer_hash_lag_requires_real_authority_transition_and_old_tail():
    report, frames, authority = _observer_tail_contract()
    summary = analyze_paladin_report(report, frames, authority)
    assert summary['observer_authority_advanced_during_area_tail'] is True
    assert summary['authority_public_hash'] == report['publicHash']
    assert summary['display_hash'] == report['displayHash']

    with pytest.raises(AssertionError, match='authority journal'):
        analyze_paladin_report(report, frames, [])

    bad_power_version = [dict(row) for row in authority]
    bad_power_version[1] = dict(bad_power_version[1], version=8)
    with pytest.raises(AssertionError):
        analyze_paladin_report(report, frames, bad_power_version)

    bad_turn_hash = [dict(row) for row in authority]
    bad_turn_hash[2] = dict(bad_turn_hash[2], hash='not-the-observed-turn-hash')
    with pytest.raises(AssertionError):
        analyze_paladin_report(report, frames, bad_turn_hash)

    no_turn_start = [dict(frame, turnStart=False) for frame in frames]
    with pytest.raises(AssertionError, match='next authority turn'):
        analyze_paladin_report(report, no_turn_start, authority)


def test_paladin_formal_replay_wires_source_area_only_evidence_and_untargeted_input(tmp_path):
    output = tmp_path / 'invalid-paladin-run'
    with pytest.raises(ValueError, match='uses its untargeted power'):
        run_players(37245, output, 'own-minion', '1280x720', False, False, False, paladin=True)
    assert not output.exists()

    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    effect_player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    area_playback = (ROOT / 'UnityClient/Assets/Scripts/NativeAreaPlayback.cs').read_text(encoding='utf-8')
    presentation = (ROOT / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    client_app = (ROOT / 'UnityClient/Assets/Scripts/ClientApp.cs').read_text(encoding='utf-8')
    editor_checks = (ROOT / 'UnityClient/Assets/Editor/NativePaladinHeroPowerChecks.cs').read_text(encoding='utf-8')

    assert 'bool Paladin{get{return Argument("--hero-power-replay-id")=="HERO_04bp";}}' in replay
    assert 'paladinSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start' in replay
    assert 'effects.SourceArea(paladinSourceKey)' in replay
    assert 'paladinAreaCloneCreations==1&&paladinAreaFinishedNotifications==1' in replay
    assert 'paladinParentFlowConstructed=paladinParentFlowConstructed||effects.SourceSpell(paladinSourceKey)!=null' in replay
    assert 'recruits.Length==1&&paladinEventOrderValid' in replay
    assert 'new PaladinEvidence' in replay and 'paladin-evidence.json' in replay
    assert 'if(Paladin&&role=="driver"&&step==72){CompletePaladinTailTurn(state);return;}' in replay
    assert 'SendMove",endTurn' in replay
    assert 'effects.SourceVisualTailActive(paladinSourceKey)' in replay
    assert "parser.add_argument('--paladin'" in harness
    assert "['--hero-power-replay-id', 'HERO_04bp']" in harness
    assert 'else [5, 4] if paladin else' in harness
    assert 'entry.card_id=="HERO_04bp"' in effect_player
    assert 'if(sourceAreas.TryGetValue(key,out area)&&area.Pending>0)return true;' in effect_player
    assert 'public bool SourceVisualTailActive(string key)' in effect_player
    assert 'FriendlyArea-only reconstruction failed its source gate; enabled-null Chain remains unsupported' in area_playback
    assert 'raw.m_ChainInfo.m_Enabled!=1' in area_playback
    assert 'Check(config.chain_enabled&&config.chain_prefab_null' in editor_checks
    assert 'presentation.NativeTailActive=nativeEffects.SourceVisualTailActive' in client_app
    assert 'NativeVisualTailCount' in presentation and 'RetainNativeVisualTails(current)' in presentation
    assert 'tail=NativeTailActive!=null&&NativeTailActive(key)' in presentation
    assert 'route.NativeKeys().Contains(key)' in editor_checks
    assert 'later authority batch completes and unlocks input before the prior source tail finishes' in editor_checks
    assert 'state.choices.Length>0&&!presentation.InputLocked)Choices();' in client_app


def test_paladin_folder_analysis_preserves_both_gui_reports_and_source_audio_evidence(tmp_path, monkeypatch):
    metadata = dict(rate=48000, channels=2, frames=9600, duration=.2, peak=.1, rms=.05,
                    clipped_samples=0, encoding=3)
    project = tmp_path / 'source-project'
    runtime = project / 'local' / 'runtime'
    runtime.mkdir(parents=True)
    source_file = runtime / 'recruit.wav'
    source_file.write_bytes(b'original Recruit voice fixture')
    source = dict(id='VOICE_CS2_101t_PLAY_0', card_id='CS2_101t', event='PLAY',
                  reference=['VO_HERO_04bk.prefab:fa26f855eaab4a108f0afa230eccda34',
                             'Play.prefab:2686b8e11804a8b4abac3a851d6d5cc0'],
                  name='VO_CS2_101t_Play_01', asset_id='asset-recruit', bundle='playsound_base_zhcn.unity3d',
                  cab='CAB-recruit', pathid='12345', file='recruit.wav',
                  sha256=hashlib.sha256(source_file.read_bytes()).hexdigest(), locale='zhcn',
                  suppress_play_sound_on_summon=False)
    (runtime / 'voice-index.json').write_text(json.dumps({'entries': [source]}, ensure_ascii=False), encoding='utf-8')
    (runtime / 'registry.json').write_text(json.dumps({'entries': [{'id': source['id'], 'path': source['file']}]}),
                                          encoding='utf-8')
    for role in ('driver', 'observer'):
        player = tmp_path / role
        player.mkdir()
        Image.new('RGB', (4, 4), 'white').save(player / '00-paladin-ready.png')
        generic = dict(result='PASS bounded formal Paladin Recruit GUI/FriendlyArea/rules/DSP capture', role=role,
                       publicHash='after', displayHash='after', heroPower='HERO_04bp', checks=18,
                       width=1920, height=1080, sampleFrames=9600, peak=.1)
        (player / 'report.json').write_text(json.dumps(generic), encoding='utf-8')
        (player / 'paladin-evidence.json').write_text(json.dumps(_report(role)), encoding='utf-8')
        (player / 'timeline.json').write_text(json.dumps({'frames': _frames(role)}), encoding='utf-8')
        (player / 'player.log').write_text(
            'VOICE VOICE_CS2_101t_PLAY_0 samples=23040 t=12.345\n', encoding='utf-8')
        if role == 'driver':
            (player / 'scene-mix.wav').write_bytes(b'listener mix fixture')

    envelope = [0.01, 0.04, 0.02, 0.08, 0.03, 0.06, 0.015, 0.07, 0.025, 0.05,
                0.012, 0.09, 0.035, 0.055, 0.018, 0.075, 0.028, 0.045, 0.014, 0.065]
    source_values = array.array('f')
    for level in envelope:
        source_values.extend([level, -level] * 240)
    listener_values = array.array('f', (sample for value in source_values for sample in (value, value)))

    def fake_wav_metadata(path):
        name = Path(path).name
        if name == 'recruit.wav':
            return dict(metadata, channels=1, file=name), source_values
        return dict(metadata, file=name), listener_values

    monkeypatch.setattr(fireblast_evidence, 'wav_metadata', fake_wav_metadata)
    fireblast_evidence.analyze_paladin(tmp_path, project_root=project)

    result = json.loads((tmp_path / 'evidence-index.json').read_text(encoding='utf-8'))
    assert result['official_fidelity_verified'] is False
    assert result['paladin_rule_hash'] == 'after'
    assert [player['actual_paladin_recruit']['area_clone_creations'] for player in result['players']] == [1, 1]
    assert len(result['native_action_audio']) == 2
    assert result['source_pcm'][0]['file'] == 'paladin-area-clip.wav'
    voice = result['paladin_summon_voice']
    assert voice['voice_id'] == 'VOICE_CS2_101t_PLAY_0'
    assert voice['source']['reference'] == source['reference']
    assert all(len(voice['player_log_playbacks'][role]) == 1 for role in ('driver', 'observer'))
    assert voice['original_source_pcm']['file'] == 'recruit.wav'
    assert voice['listener_capture']['scope'] == 'full Unity AudioListener scene mix; not source-isolated'
    assert voice['source_listener_correlation']['best_offset_seconds'] == 0
    assert voice['source_listener_correlation']['best_correlation'] > .99
    (tmp_path / 'observer' / 'player.log').write_text(
        'VOICE VOICE_CS2_101t_PLAY_0 t=12.3\nVOICE VOICE_CS2_101t_PLAY_0 t=12.4\n', encoding='utf-8')
    with pytest.raises(AssertionError, match='exactly one actual'):
        fireblast_evidence.analyze_paladin_voice_capture(tmp_path, project)
