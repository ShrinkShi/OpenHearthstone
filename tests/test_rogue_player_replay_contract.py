import json
from pathlib import Path

from PIL import Image
import pytest

from game.database import preset
from game.match import Match
from tools import fireblast_evidence
from tools.fireblast_evidence import analyze_rogue_report, prepare


ROOT = Path(__file__).resolve().parents[1]


def apply(match, seat, move, identity):
    match.apply(seat, dict(move, action_id=identity, version=match.version, match_id=match.id))


def test_rogue_formal_fixture_starts_with_legal_untargeted_power_and_one_authoritative_dagger():
    match = prepare(Match(['RogueA', 'MageB'], [7, 4], [preset(7), preset(4)], 52), rogue=True)
    rogue = match.game.players[0]
    before = [match.snapshot(seat) for seat in range(2)]

    assert [view['players'][0]['power']['card_id'] for view in before] == ['HERO_03bp', 'HERO_03bp']
    assert before[0]['players'][0]['weapon'] is None
    assert any(move['kind'] == 'power' and move['target'] == 0 for move in match.legal_moves(0))
    mana = rogue.mana
    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0)
    apply(match, 0, move, 'rogue-formal-dagger')

    after = [match.snapshot(seat) for seat in range(2)]
    assert rogue.weapon.id == 'CS2_082'
    assert rogue.weapon.atk == 1 and rogue.weapon.durability == 2
    assert rogue.mana == mana - 2 and after[0]['players'][0]['power']['used']
    assert not any(move['kind'] == 'power' for move in match.legal_moves(0))
    assert after[0]['public_hash'] == after[1]['public_hash']
    for view in after:
        powers = [event for event in view['events'] if event['type'] == 'PlayHeroPower']
        summons = [event for event in view['events'] if event['type'] == 'Summon']
        daggers = [event for event in summons if event.get('card_type') == 7 and
                   event.get('visual', {}).get('card_id') == 'CS2_082']
        assert len(powers) == 1
        assert len(summons) == len(daggers) == 1
        assert daggers[0]['target'] == rogue.weapon.entity_id
        assert view['players'][0]['weapon']['card_id'] == 'CS2_082'


def test_rogue_weapon_fixture_executes_two_legal_authority_attacks_then_destroys_dagger():
    match = prepare(Match(['RogueA', 'MageB'], [7, 4], [preset(7), preset(4)], 52), rogue=True)
    player = match.game.players[0]
    opponent = match.game.players[1]
    power = next(move for move in match.legal_moves(0) if move['kind'] == 'power' and move['target'] == 0)
    apply(match, 0, power, 'rogue-weapons-power')
    assert player.weapon.id == 'CS2_082' and player.weapon.durability == 2

    batches = []
    for index in (1, 2):
        legal = [move for move in match.legal_moves(0) if move['kind'] == 'attack' and
                 move['entity'] == player.hero.entity_id and move['target'] == opponent.hero.entity_id]
        assert len(legal) == 1
        before_hash = match.snapshot(0)['public_hash']
        apply(match, 0, legal[0], f'rogue-weapons-attack-{index}')
        own, peer = match.snapshot(0), match.snapshot(1)
        assert own['public_hash'] == peer['public_hash'] != before_hash
        batches.append(own)
        if index == 1:
            assert player.weapon.id == 'CS2_082' and player.weapon.durability == 1
            assert not any(move['kind'] == 'attack' and move['entity'] == player.hero.entity_id
                           for move in match.legal_moves(0))
            assert any(move['kind'] == 'end_turn' for move in match.legal_moves(0))
            apply(match, 0, next(move for move in match.legal_moves(0) if move['kind'] == 'end_turn'), 'rogue-weapons-end-turn')
            assert any(move['kind'] == 'end_turn' for move in match.legal_moves(1))
            apply(match, 1, next(move for move in match.legal_moves(1) if move['kind'] == 'end_turn'), 'rogue-weapons-mage-end-turn')

    assert player.weapon is None and player.hero.atk == 0
    assert not any(move['kind'] == 'attack' and move['entity'] == player.hero.entity_id
                   for move in match.legal_moves(0))
    assert any(move['kind'] == 'end_turn' for move in match.legal_moves(0))
    first_events, second_events = batches[0]['events'], batches[1]['events']
    weapon = batches[0]['players'][0]['weapon']
    first_hit = [event for event in first_events if event['type'] == 'Hit' and event['source'] == event['target'] == weapon['entity']]
    second_hit = [event for event in second_events if event['type'] == 'Hit' and event['source'] == event['target'] == weapon['entity']]
    second_death = [event for event in second_events if event['type'] == 'Death' and event['target'] == weapon['entity']]
    assert len(first_hit) == len(second_hit) == len(second_death) == 1
    assert first_hit[0]['visual']['durability'] == 1
    assert second_hit[0].get('visual', {}).get('durability', 0) == 0


def test_rogue_analyzer_requires_source_only_release_unlock_and_hash_evidence():
    before = dict(powerCardId='HERO_03bp', powerUsed=False, weaponEntity=0, weaponCardId='',
                  legalPowerMove=True, legalEndTurn=False, mana=10, publicHash='before')
    after = dict(powerCardId='HERO_03bp', powerUsed=True, weaponEntity=21, weaponCardId='CS2_082',
                 weaponAttack=1, weaponDurability=2, heroAttack=1, legalPowerMove=False,
                 legalEndTurn=True, mana=8, publicHash='after')
    report = dict(role='driver', heroPower='HERO_03bp', rogueBefore=before, rogueAfter=after,
                  roguePlayHeroPowerCount=1, rogueSummonCount=1, rogueDaggerSummonCount=1, rogueSummonTarget=21,
                  rogueUnexpectedHeroCueCount=0, rogueSourceCueCount=1, rogueSourceCueSemanticsValid=True, rogueSourceOnly=True,
                  rogueParentFlowConstructed=False, rogueSourceRootNodeCount=3, rogueSourceRootReleased=True,
                  rogueSourceStateActions=2, rogueSourceAudioPlayCount=1, listenerCount=1,
                  rogueInputUnlockedAfterSource=True, rogueInputUnlockSampleFrame=42,
                  publicHash='after', displayHash='after')
    frame = dict(frame=42, sourceCompleted=True, sourceReleased=True, inputLocked=False,
                 hashMatched=True, endTurnAvailable=True)

    summary = analyze_rogue_report(report, [frame])

    assert summary['source_only'] is True
    assert summary['source_root_released'] is True
    assert summary['source_cue_semantics_valid'] is True
    assert summary['authority_public_hash'] == summary['display_hash'] == 'after'
    report['rogueSourceCueSemanticsValid'] = False
    with pytest.raises(AssertionError):
        analyze_rogue_report(report, [frame])


def test_rogue_weapon_mode_does_not_apply_single_cast_event_counts_to_later_attacks():
    before = dict(powerCardId='HERO_03bp', powerUsed=False, weaponEntity=0, weaponCardId='',
                  legalPowerMove=True, legalEndTurn=False, mana=10, publicHash='before')
    after = dict(powerCardId='HERO_03bp', powerUsed=True, weaponEntity=71, weaponCardId='CS2_082',
                 weaponAttack=1, weaponDurability=2, heroAttack=1, legalPowerMove=False,
                 legalEndTurn=True, mana=8, publicHash='after')
    report = dict(role='driver', heroPower='HERO_03bp', rogueWeaponsMode=True,
                  rogueBefore=before, rogueAfter=after, rogueUnexpectedHeroCueCount=2,
                  listenerCount=1,
                  rogueSourceCueCount=1, rogueSourceCueSemanticsValid=True, rogueSourceOnly=True,
                  rogueParentFlowConstructed=False, rogueSourceRootNodeCount=3, rogueSourceRootReleased=True,
                  rogueSourceStateActions=2, rogueSourceAudioPlayCount=2,
                  rogueInputUnlockedAfterSource=True, rogueInputUnlockSampleFrame=42,
                  publicHash='after', displayHash='after', rogueDisplayHash='after',
                  rogueWeaponDisplayHash='after')
    frame = dict(frame=42, sourceCompleted=True, sourceReleased=True, inputLocked=False,
                 hashMatched=True, endTurnAvailable=True)

    summary = analyze_rogue_report(report, [frame])

    assert summary['source_only'] is True
    assert summary['input_unlocked_after_source'] is True


def test_rogue_weapon_analyzer_requires_durability_events_source_clips_hashes_and_unlock():
    report = dict(role='driver', heroPower='HERO_03bp', rogueWeaponsMode=True,
                  publicHash='weapon-final', displayHash='weapon-final', rogueDisplayHash='dagger-equipped',
                  rogueWeaponDisplayHash='weapon-final', rogueWeaponLifecycleComplete=True,
                  rogueWeaponInputUnlockedAfterAttack=True, rogueWeaponInputUnlockSampleFrame=99,
                  rogueWeaponSlot7Events=2, rogueWeaponSlot5Events=1,
                  rogueWeaponSlot7AudioPlayCount=2, rogueWeaponSlot5AudioPlayCount=1,
                  rogueWeaponAttacks=[
                      dict(attackIndex=1, frame=80, version=8, heroEntity=4, targetEntity=9, targetHealthBefore=30, targetHealth=29,
                           weaponEntity=21, weaponAttack=1, weaponDurability=1, attackEvents=1, targetHitEvents=1,
                           weaponHitEvents=1, weaponDeathEvents=0, slot7Events=1, slot5Events=0,
                           publicHash='attack-one', displayHash='attack-one', inputUnlocked=True,
                           hashMatched=True, legalNextAttack=False, legalEndTurn=True),
                      dict(attackIndex=2, frame=99, version=9, heroEntity=4, targetEntity=9, targetHealthBefore=29, targetHealth=28,
                           weaponEntity=0, weaponAttack=0, weaponDurability=0, attackEvents=1, targetHitEvents=1,
                           weaponHitEvents=1, weaponDeathEvents=1, slot7Events=1, slot5Events=1,
                           publicHash='weapon-final', displayHash='weapon-final', inputUnlocked=True,
                           hashMatched=True, legalNextAttack=False, legalEndTurn=True)],
                  rogueWeaponAudioRecords=[
                      dict(audioSource='native-weapon', message='weapon source play slot=7 actualStarted=True',
                           file='508d69647c76de93961e372c.wav'),
                      dict(audioSource='native-weapon', message='weapon source play slot=7 actualStarted=True',
                           file='508d69647c76de93961e372c.wav'),
                      dict(audioSource='native-weapon', message='weapon source play slot=5 actualStarted=True',
                           file='08d714cf9696d373af93bc9b.wav')])
    frames = [dict(frame=99, version=9, target=71, inputLocked=False, busy=False, hashMatched=False,
                   endTurnAvailable=True, authorityHealth=29, displayHealth=29),
              dict(frame=99, version=8, target=9, inputLocked=False, busy=False, hashMatched=True,
                   endTurnAvailable=True, authorityHealth=28, displayHealth=28),
              dict(frame=99, version=9, target=9, inputLocked=False, busy=False, hashMatched=True,
                   endTurnAvailable=True, authorityHealth=28, displayHealth=28)]
    lifecycle = dict(role='driver', replacementSubmitVersion=9, replacementAcceptedVersion=10,
                     endTurnAcceptedVersion=11, concedeAcceptedVersion=12,
                     replacementSubmitted=True, replacementAccepted=True, replacementSourceSeen=True,
                     replacementSourceReleased=True, replacementInputUnlocked=True, endTurnSubmitted=True,
                     endTurnAccepted=True, concedeSubmitted=True, concessionSynchronized=True, finalHash='weapon-final',
                     points=[
                         dict(stage='replacement-power-accepted', version=10, phase='play', activePlayer=0,
                              weaponEntity=22, weaponDurability=2, legalPowerMove=False, legalEndTurn=True),
                         dict(stage='replacement-input-unlocked', version=10, pending=False, inputLocked=False,
                              nativeBusy=False, hashMatched=True, legalEndTurn=True, endTurnReady=True,
                              endTurnGreen=False, busy=True, time=1.0, batchTime=1.0),
                         dict(stage='replacement-end-turn-accepted', version=11, phase='play', activePlayer=1,
                              pending=False),
                         dict(stage='concede-submitted', version=11, phase='play', activePlayer=1, pending=True),
                         dict(stage='concede-finished-synchronized', version=12, phase='finished', resultReady=True,
                              pending=False, busy=False, inputLocked=False, hashMatched=True)])

    summary = fireblast_evidence.analyze_rogue_weapon_report(report, frames, lifecycle)

    assert summary['slot7_audio_play_count'] == 2 and summary['slot5_audio_play_count'] == 1
    assert summary['input_unlocked_after_attack'] is True
    assert summary['replacement_lifecycle']['concession_synchronized'] is True
    assert summary['replacement_lifecycle']['replacement_input_unlocked']['busy'] is True
    changed = json.loads(json.dumps(report))
    changed['rogueWeaponAudioRecords'][2]['file'] = 'wrong.wav'
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(changed, frames)
    changed = json.loads(json.dumps(report))
    changed['rogueWeaponAttacks'][1]['weaponDurability'] = 1
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(changed, frames)
    changed = json.loads(json.dumps(report))
    changed['rogueWeaponAttacks'][1]['targetEntity'] = 71
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(changed, frames)
    changed_frames = json.loads(json.dumps(frames))
    changed_frames[2]['authorityHealth'] = 29
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(report, changed_frames)
    for field, invalid in (('inputLocked', True), ('busy', True), ('hashMatched', False),
                           ('displayHealth', 29), ('endTurnAvailable', False)):
        changed_frames = json.loads(json.dumps(frames))
        changed_frames[2][field] = invalid
        with pytest.raises(AssertionError):
            fireblast_evidence.analyze_rogue_weapon_report(report, changed_frames)
    changed_lifecycle = json.loads(json.dumps(lifecycle))
    changed_lifecycle['concessionSynchronized'] = False
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(report, frames, changed_lifecycle)
    changed_lifecycle = json.loads(json.dumps(lifecycle))
    changed_lifecycle['points'][-1]['inputLocked'] = True
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_report(report, frames, changed_lifecycle)


def test_rogue_player_replay_contract_keeps_parent_chain_unknown_and_checks_native_root():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    profile = (ROOT / 'UnityClient/Assets/Scripts/NativeRogueHeroPowerProfile.cs').read_text(encoding='utf-8')

    assert 'bool Rogue{get{return Argument("--hero-power-replay-id")=="HERO_03bp";}}' in replay
    assert 'cast.CardId=="HERO_03bp"&&cast.Flow==null&&rogueSourceRootNodeCount==3' in replay
    assert 'rogueSourceRootReleased&&rogueSourceStateActions>=2' in replay
    assert 'rogueSourceCueCount==1&&rogueSourceCueSemanticsValid&&rogueUnexpectedHeroCueCount==0' in replay
    assert 'rogueSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].apply==float.MaxValue;' in replay
    assert 'sourceCues.Length==1&&sourceCues[0].data.target' not in replay
    assert 'if(!RogueWeapons||!rogueDaggerSnapshotCaptured)' in replay
    assert "assert report['rogueSourceCueSemanticsValid'] is True" in harness
    assert 'parser.add_argument(\'--rogue\'' in harness
    assert 'def analyze(folder, hunter=False, priest=False, rogue=False, rogue_weapons=False, priest_full_health=False,' in harness
    assert 'config.chain_enabled&&config.chain_prefab_null&&!config.chain_available' in profile
    assert 'Chain must remain enabled-null; its original runtime semantics are unresolved' in profile


def test_rogue_weapons_mode_uses_the_real_player_attack_input_and_reports_source_audio():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    assert 'bool RogueWeapons{get{return Rogue&&(Environment.GetCommandLineArgs().Contains("--rogue-weapons")||RogueWeaponsShaman);}}' in replay
    assert 'Click(locations[ownHero.entity].center)' in replay and 'Click(locations[target.entity].center)' in replay
    assert 'StartRogueWeaponReplacementPower(current,2)' in replay
    assert 'actual replacement power button click submits one legal HERO_03bp request' in replay
    assert 'replacement-end-turn-accepted' in replay and 'concede-finished-synchronized' in replay
    assert 'queueCount=presentation.QueueCount' in replay and 'nativeBusy=nativeBusy' in replay
    assert 'endTurnGreen=ready&&BattleInteraction.NoActions(current)' in replay
    assert 'authority accepted the replacement power but Presentation did not expose its HERO_03bp source cue' in replay
    assert 'RogueObserverSawReplacement(current,1)' in replay and 'observer-saw-replacement' in replay
    assert 'rogue-weapon-lifecycle.json' in replay
    assert 'NativeBattleWeapons.Slot(e,rogueWeaponBeforeAttack,current)' in replay
    assert 'rogueDaggerSnapshotCaptured=rogueAfter.powerUsed&&rogueAfter.weaponCardId=="CS2_082"' in replay
    assert 'audio.StartedVoices==prior+1' in replay
    assert "parser.add_argument('--rogue-weapons'" in harness
    assert "server_command.append('--rogue-weapons-shaman' if rogue_weapons_shaman else '--rogue-weapons' if rogue_weapons else '--rogue')" in harness
    assert "command.append('--rogue-weapons')" in harness
    assert 'def analyze_rogue_weapon_report(report, frames, lifecycle=None):' in harness
    assert "analyze_rogue_weapon_lifecycle(report, lifecycle)" in harness


def test_rogue_shaman_third_attack_does_not_wait_for_weapon_destruction_audio():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    attack = replay.split('void CompleteRogueWeaponAttack(StateView current,int attackIndex)', 1)[1].split(
        'void StartRogueWeaponReplacementPower', 1)[0]

    assert 'int expectedSlot5Audio=attackIndex==2?1:0;' in attack
    assert 'rogueWeaponSlot5AudioPlayCount-rogueWeaponPendingSlot5Audio==expectedSlot5Audio' in attack
    assert "third['weaponDurability'] == 1 and third['weaponDeathEvents'] == 0" in harness
    assert "[attack['slot5Events'] for attack in attacks] == [0, 1, 0]" in harness


def test_748_rogue_shaman_harness_covers_occupied_replacement_and_peer_result_ready():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    harness = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')

    assert 'ROGUE_WEAPONS_SHAMAN_SEED = 634333382356097596' in harness
    assert '[7, 8] if rogue_weapons_shaman else [7, 4] if rogue_mode' in harness
    assert 'Check(state.version==6' in replay
    assert 'v18 authority destroys and removes the one-durability CS2_082' in replay
    assert 'v18 event order is SpendMana -> PlayHeroPower -> Destroy -> Death -> Summon' in replay
    assert 'only the legal end-turn move remains and ClientApp\'s green-tint condition is true' in replay
    assert 'driver-result-ready' in replay and 'peer-result-ready' in replay
    assert 'resultReadyOnBothPeers' in replay
    assert "parser.add_argument('--rogue-weapons-shaman'" in harness
    assert "player_class = '3' if hunter else '6' if priest and role == 'driver' else '7' if rogue and role == 'driver' else '8' if rogue_weapons_shaman and role == 'observer'" in harness


def test_rogue_shaman_replay_baseline_expects_the_selected_opponent_class():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')

    assert 'state.players[1].power.card_id==(RogueWeaponsShaman?"HERO_02bp":Review?' in replay
    assert 'RogueWeaponsShaman?"real Rogue versus Shaman":Review?' in replay
    assert 'Rogue?"real Rogue versus Mage"' in replay


def test_rogue_shaman_turn_handoff_uses_peer_attestation_and_records_settled_state():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    analyzer = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    driver = replay.split('void CompleteRogueWeaponShamanTurn(StateView current)', 1)[1].split(
        'void SubmitRogueWeaponConcede(StateView current)', 1)[0]
    observer = replay.split('void TickRogueWeaponShamanObserver(StateView current)', 1)[1].split(
        'bool RogueConcessionReady(StateView current)', 1)[0]

    assert 'rogueObserverShamanPowerCount==2' not in driver
    assert 'RogueWeaponMarker(current.match_id,"observer-shaman-turn-complete")' in driver
    assert 'current.version+"|"+current.public_hash+"|2|73:NEW1_009;75:CS2_051"' in driver
    assert 'rogueObserverShamanPowerCount==2' in observer
    assert 'RecordRogueWeaponLifecyclePoint(current,"observer-shaman-turn-complete")' in observer
    assert "shaman_turn_complete['queueCount'] == 0" in analyzer
    assert 'not shaman_turn_complete[\'nativeBusy\']' in analyzer


def test_rogue_shaman_finished_concede_routes_before_play_gate_and_waits_for_both_markers():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    tick = replay.split('void Tick()', 1)[1]
    phase_gate = tick.index('if(state==null||state.phase!="play")')
    terminal_driver_route = tick.index(
        'if(state!=null&&state.phase=="finished"&&RogueWeapons&&role=="driver"&&step==36)')

    assert terminal_driver_route < phase_gate
    assert tick.index('CompleteRogueWeaponConcede(state);return;', terminal_driver_route) < phase_gate
    assert tick.index('if(RogueWeapons&&role=="driver"&&(step==31||step==32))') > phase_gate
    assert tick.index('if(step==83)') < phase_gate
    assert tick.index('if(step==71)') < phase_gate
    assert tick.index('if(step==8&&Age>.5f)') < phase_gate

    observer = replay.split('void TickRogueWeaponShamanObserver(StateView current)', 1)[1].split(
        'bool RogueConcessionReady(StateView current)', 1)[0]
    finished = observer.index('if(current.phase=="finished")')
    play_gate = observer.index('if(current.phase!="play"||current.seat!=1||current.active!=1)')
    assert finished < play_gate
    assert observer.index('RogueConcessionReady(current)', finished) < observer.index(
        'File.WriteAllText(ready,expected)', finished)
    assert observer.index('if(File.ReadAllText(ready)!=expected)return;', finished) < observer.index(
        'File.ReadAllText(driverReady)!=expected', finished)
    assert observer.index('rogueWeaponResultReadyOnBothPeers=true;', finished) < play_gate

    step83 = tick[tick.index('if(step==83)'):tick.index('if(role=="observer")', tick.index('if(step==83)'))]
    assert step83.index('TickRogueWeaponShamanObserver(observed);') < step83.index('if(File.Exists(marker))')
    assert step83.index('if(RogueWeaponsShaman&&!rogueWeaponResultReadyOnBothPeers)return;') < step83.index(
        'Advance(8,"scene-exit")')
    assert 'if(RogueWeaponsShaman){rogueWeaponResultReadyOnBothPeers=true;' not in step83


def test_rogue_shaman_lifecycle_rejects_unsettled_second_turn_handoff():
    report = dict(role='observer', publicHash='finished-hash', displayHash='finished-hash',
                  rogueWeaponPowerUseCount=3, rogueObserverShamanPowerCount=2,
                  rogueObserverShamanTotemSequence='73:NEW1_009;75:CS2_051')
    points = [
        dict(stage='observer-empty-slot-equip-accepted', version=13),
        dict(stage='observer-occupied-replacement-accepted', version=18),
        dict(stage='observer-shaman-power-2-accepted', version=15, phase='play', activePlayer=1),
        dict(stage='observer-shaman-turn-complete', version=16, phase='play', activePlayer=0,
             queueCount=0, pending=False, busy=False, inputLocked=False, nativeBusy=False,
             resultReady=False, hashMatched=True),
        dict(stage='observer-both-peers-result-ready', version=19, phase='finished', resultReady=True,
             pending=False, busy=False, inputLocked=False, hashMatched=True),
        dict(stage='concede-finished-synchronized', version=19, phase='finished', resultReady=True,
             pending=False, busy=False, inputLocked=False, hashMatched=True),
    ]
    lifecycle = dict(role='observer', emptySlotEquipVersion=13, occupiedReplacementVersion=18,
                     concedeAcceptedVersion=19, concessionSynchronized=True,
                     resultReadyOnBothPeers=True, finalHash='finished-hash', points=points)

    summary = fireblast_evidence.analyze_rogue_weapon_shaman_lifecycle(report, lifecycle)
    assert summary['result_ready_on_both_peers'] is True

    for field, invalid in (('queueCount', 1), ('busy', True), ('inputLocked', True),
                           ('nativeBusy', True), ('hashMatched', False)):
        changed = json.loads(json.dumps(lifecycle))
        next(point for point in changed['points'] if point['stage'] == 'observer-shaman-turn-complete')[field] = invalid
        with pytest.raises(AssertionError):
            fireblast_evidence.analyze_rogue_weapon_shaman_lifecycle(report, changed)


def test_rogue_observer_lifecycle_requires_finished_result_and_matching_hash():
    report = dict(role='observer', publicHash='finished-hash')
    lifecycle = dict(role='observer', replacementAccepted=True, endTurnAccepted=True,
                     concessionSynchronized=True, finalHash='finished-hash',
                     replacementAcceptedVersion=13, endTurnAcceptedVersion=14, concedeAcceptedVersion=15,
                     points=[dict(stage='concede-finished-synchronized', version=15, phase='finished', resultReady=True,
                                  hashMatched=True, pending=False, busy=False, inputLocked=False)])

    summary = fireblast_evidence.analyze_rogue_weapon_lifecycle(report, lifecycle)
    assert summary['concession_synchronized'] is True
    lifecycle['points'][0]['resultReady'] = False
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_lifecycle(report, lifecycle)


def test_rogue_peer_analyzer_requires_matching_end_state_and_authority_versions():
    driver_report = dict(role='driver', result='PASS driver', publicHash='finished-hash')
    observer_report = dict(role='observer', result='PASS observer', publicHash='finished-hash')
    driver_lifecycle = dict(concessionSynchronized=True, finalHash='finished-hash', replacementAcceptedVersion=13,
                            endTurnAcceptedVersion=14, concedeAcceptedVersion=15)
    observer_lifecycle = dict(concessionSynchronized=True, finalHash='finished-hash', replacementAcceptedVersion=13,
                              endTurnAcceptedVersion=14, concedeAcceptedVersion=15)

    summary = fireblast_evidence.analyze_rogue_weapon_peer_sync(
        driver_report, observer_report, driver_lifecycle, observer_lifecycle)

    assert summary['both_players_finished'] is True
    assert summary['concede_accepted_version'] == 15
    observer_lifecycle['endTurnAcceptedVersion'] = 12
    with pytest.raises(AssertionError):
        fireblast_evidence.analyze_rogue_weapon_peer_sync(
            driver_report, observer_report, driver_lifecycle, observer_lifecycle)


def test_rogue_source_contract_accepts_empty_weapon_sentinel():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')

    assert 'bool RogueWeaponIsEmpty(EntityView weapon)' in replay
    assert 'return weapon==null||(weapon.entity==0&&String.IsNullOrEmpty(weapon.card_id));' in replay
    assert 'RogueWeaponIsEmpty(state.players[0].weapon)&&!state.players[0].power.used' in replay
    assert 'Rogue authority snapshot starts with no weapon and an unused HERO_03bp power' in replay
    assert '!RogueWeaponIsEmpty(rogueWeapon)&&rogueWeapon.card_id=="CS2_082"' in replay


def test_rogue_folder_analysis_skips_mage_hit_reaction_assertions(tmp_path, monkeypatch):
    before = dict(powerCardId='HERO_03bp', powerUsed=False, weaponEntity=0, weaponCardId='',
                  weaponAttack=0, weaponDurability=0, heroAttack=0, legalPowerMove=True,
                  legalEndTurn=False, mana=10, publicHash='before')
    after = dict(powerCardId='HERO_03bp', powerUsed=True, weaponEntity=21, weaponCardId='CS2_082',
                 weaponAttack=1, weaponDurability=2, heroAttack=1, legalPowerMove=False,
                 legalEndTurn=True, mana=8, publicHash='after')
    source_record = dict(message='source play', file='test-native-source.wav', audioSource='native-effect')
    rogue_frame = dict(frame=42, sourceCompleted=True, sourceReleased=True, inputLocked=False,
                       hashMatched=True, endTurnAvailable=True)
    for role in ('driver', 'observer'):
        path = tmp_path / role
        path.mkdir()
        Image.new('RGB', (4, 4), 'white').save(path / '00-ready.png')
        report = dict(result='PASS fixture', role=role, publicHash='after', displayHash='after',
                      heroPower='HERO_03bp', listenerCount=1 if role == 'driver' else 0,
                      rogueBefore=dict(before, legalPowerMove=role == 'driver'),
                      rogueAfter=dict(after, legalEndTurn=role == 'driver'),
                      roguePlayHeroPowerCount=1, rogueSummonCount=1, rogueDaggerSummonCount=1,
                      rogueSummonTarget=21, rogueUnexpectedHeroCueCount=0, rogueSourceCueCount=1,
                      rogueSourceCueSemanticsValid=True,
                      rogueSourceOnly=True, rogueParentFlowConstructed=False, rogueSourceRootNodeCount=3,
                      rogueSourceRootReleased=True, rogueSourceStateActions=2, rogueSourceAudioPlayCount=1,
                      rogueInputUnlockedAfterSource=role == 'driver',
                      rogueInputUnlockSampleFrame=42 if role == 'driver' else -1,
                      records=[source_record] if role == 'driver' else [])
        (path / 'report.json').write_text(json.dumps(report), encoding='utf-8')
        (path / 'timeline.json').write_text(json.dumps({'frames': [rogue_frame] if role == 'driver' else []}), encoding='utf-8')

    monkeypatch.setattr(fireblast_evidence, 'wav_metadata', lambda path: (dict(rate=44100, channels=2, frames=4410,
                          duration=.1, peak=.1), []))
    fireblast_evidence.analyze(tmp_path, rogue=True)

    result = json.loads((tmp_path / 'evidence-index.json').read_text(encoding='utf-8'))
    assert [player['actual_rogue_dagger']['source_root_released'] for player in result['players']] == [True, True]
    assert all('actual_hit_timeline' not in player for player in result['players'])
