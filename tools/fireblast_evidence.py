"""Bounded Fireblast fixtures; actual actions continue through the normal server."""
import argparse
import asyncio
import json
import hashlib
import subprocess
import sys
import time
import array
import csv
import math
import struct
import re
from pathlib import Path

from game.database import preset
from game.match import Match
from .importer import PROJECT


def apply(match, seat, move, identity):
    return match.apply(seat, dict(move, version=match.version, match_id=match.id, action_id=identity))


def legal_move(match, seat, kind, *, entity=None, target=None):
    return next(item for item in match.legal_moves(seat)
                if item['kind'] == kind and
                (entity is None or item.get('entity') == entity) and
                (target is None or item.get('target') == target))


ROGUE_WEAPONS_SHAMAN_SEED = 634333382356097596
ROGUE_WEAPONS_SHAMAN_TOTEMS = ('CS2_050', 'NEW1_009', 'CS2_051')


def prepare_rogue_weapons_shaman(match, finish=False):
    """Replay the saved Rogue/Shaman action order from its deterministic seed."""
    rogue, shaman = match.game.players
    if [player.hero.power.id for player in match.game.players] != ['HERO_03bp', 'HERO_02bp']:
        raise ValueError('The Rogue/Shaman reproduction requires Rogue seat 0 and Shaman seat 1')
    if rogue.hero.entity_id != 4 or rogue.hero.power.entity_id != 5 or shaman.hero.power.entity_id != 38:
        raise ValueError('The Rogue/Shaman reproduction seed no longer maps to the saved entity IDs')

    for seat in (1, 0):
        assert apply(match, seat, {'kind': 'mulligan', 'cards': []}, f'748-mulligan-{seat}')
    assert apply(match, 0, legal_move(match, 0, 'end_turn'), '748-v3-end-turn')
    assert apply(match, 1, legal_move(match, 1, 'play', entity=70), '748-v4-play-70')
    assert apply(match, 1, legal_move(match, 1, 'power', entity=38, target=0), '748-v5-shaman-power')
    assert shaman.field[-1].entity_id == 71 and shaman.field[-1].id == ROGUE_WEAPONS_SHAMAN_TOTEMS[0]
    assert apply(match, 1, legal_move(match, 1, 'end_turn'), '748-v6-end-turn')
    assert match.version == 6 and match.game.current_player is rogue
    state = match.snapshot(0)
    assert rogue.weapon is None and not state['players'][0]['power']['used']
    assert state['players'][1]['power']['used'] and shaman.field[-1].entity_id == 71
    assert any(item['kind'] == 'power' and item['entity'] == 5 and item['target'] == 0
               for item in match.legal_moves(0))
    if not finish:
        return match

    # The trace refreshes Rogue's power through legal turn transitions; it does
    # not use a second Rogue power in the same turn as the first.
    assert apply(match, 0, legal_move(match, 0, 'power', entity=5, target=0), '748-v7-rogue-power')
    first_weapon = rogue.weapon
    assert first_weapon.id == 'CS2_082' and first_weapon.durability == 2
    assert apply(match, 0, legal_move(match, 0, 'attack', entity=4, target=71), '748-v8-attack-totem-71')
    assert rogue.weapon is first_weapon and rogue.weapon.durability == 1
    assert not any(item['kind'] == 'power' for item in match.legal_moves(0))
    assert apply(match, 0, legal_move(match, 0, 'end_turn'), '748-v9-end-turn')

    assert apply(match, 1, legal_move(match, 1, 'power', entity=38, target=0), '748-v10-shaman-power')
    assert shaman.field[-1].entity_id == 73 and shaman.field[-1].id == ROGUE_WEAPONS_SHAMAN_TOTEMS[1]
    assert apply(match, 1, legal_move(match, 1, 'end_turn'), '748-v11-end-turn')

    assert apply(match, 0, legal_move(match, 0, 'attack', entity=4, target=73), '748-v12-attack-totem-73')
    assert rogue.weapon is None
    assert any(item['kind'] == 'power' and item['entity'] == 5 for item in match.legal_moves(0))
    assert apply(match, 0, legal_move(match, 0, 'power', entity=5, target=0), '748-v13-rogue-power')
    first_replacement = rogue.weapon
    assert first_replacement.id == 'CS2_082' and first_replacement.entity_id != first_weapon.entity_id
    assert match.version == 13
    assert [event['type'] for event in match.snapshot(0)['events']] == ['SpendMana', 'PlayHeroPower', 'Summon']
    assert apply(match, 0, legal_move(match, 0, 'end_turn'), '748-v14-end-turn')

    assert apply(match, 1, legal_move(match, 1, 'power', entity=38, target=0), '748-v15-shaman-power')
    assert shaman.field[-1].entity_id == 75 and shaman.field[-1].id == ROGUE_WEAPONS_SHAMAN_TOTEMS[2]
    assert apply(match, 1, legal_move(match, 1, 'end_turn'), '748-v16-end-turn')

    assert apply(match, 0, legal_move(match, 0, 'attack', entity=4, target=75), '748-v17-attack-totem-75')
    assert rogue.weapon is first_replacement and rogue.weapon.durability == 1
    assert any(item['kind'] == 'power' and item['entity'] == 5 for item in match.legal_moves(0))
    assert apply(match, 0, legal_move(match, 0, 'power', entity=5, target=0), '748-v18-rogue-power')
    second_replacement = rogue.weapon
    assert second_replacement.id == 'CS2_082' and second_replacement.entity_id != first_replacement.entity_id
    assert match.version == 18
    events = match.snapshot(0)['events']
    assert [event['type'] for event in events] == ['SpendMana', 'PlayHeroPower', 'Destroy', 'Death', 'Summon']
    v18_moves = match.legal_moves(0)
    assert len(v18_moves) == 1 and v18_moves[0]['kind'] == 'end_turn'
    assert apply(match, 0, {'kind': 'concede'}, '748-v19-concede')

    own, peer = match.snapshot(0), match.snapshot(1)
    assert own['phase'] == peer['phase'] == 'finished'
    assert own['version'] == peer['version'] == 19
    assert own['public_hash'] == peer['public_hash']
    assert [player['result'] for player in own['players']] == ['LOST', 'WON']
    return match


def prepare(match, review=False, hunter=False, priest=False, rogue=False, warrior=False, druid=False, warlock=False, paladin=False, shaman=False, priest_full_health=False):
    priest = priest or priest_full_health
    if sum((review, hunter, priest, rogue, warrior, druid, warlock, paladin, shaman)) > 1:
        raise ValueError('Hero-power evidence fixtures are mutually exclusive')
    if shaman and [player.hero.power.id for player in match.game.players] != ['HERO_02bp', 'HERO_08bp']:
        raise ValueError('The Shaman evidence fixture requires Shaman seat 0 and Mage seat 1')
    if paladin and [player.hero.power.id for player in match.game.players] != ['HERO_04bp', 'HERO_08bp']:
        raise ValueError('The Paladin evidence fixture requires Paladin seat 0 and Mage seat 1')
    if warlock and [player.hero.power.id for player in match.game.players] != ['HERO_07bp', 'HERO_08bp']:
        raise ValueError('The Warlock evidence fixture requires Warlock seat 0 and Mage seat 1')
    if druid and [player.hero.power.id for player in match.game.players] != ['HERO_06bp', 'HERO_08bp']:
        raise ValueError('The Druid evidence fixture requires Druid seat 0 and Mage seat 1')
    if rogue and [player.hero.power.id for player in match.game.players] != ['HERO_03bp', 'HERO_08bp']:
        raise ValueError('The Rogue evidence fixture requires Rogue seat 0 and Mage seat 1')
    if warrior and [player.hero.power.id for player in match.game.players] != ['HERO_01bp', 'HERO_08bp']:
        raise ValueError('The Warrior evidence fixture requires Warrior seat 0 and Mage seat 1')
    for seat in range(2):
        apply(match, seat, dict(kind='mulligan', cards=[]), f'fixture-m{seat}')
    if match.game.current_player is not match.game.players[0]:
        apply(match, 1, dict(kind='end_turn'), 'fixture-turn0')
    for seat, player in enumerate(match.game.players):
        player.max_mana = 10
        player.used_mana = 0
        if priest and seat == 1:
            player.summon('GIL_646')
        elif not priest and not rogue and not warlock and not paladin and not shaman:
            player.summon('CS2_119' if review else 'CS2_182')
    if review:
        match.game.players[0].give('CS2_029')
        match.game.players[0].give('CS2_189')
    apply(match, 0, dict(kind='end_turn'), 'fixture-turn1')
    player = match.game.players[1]
    player.max_mana = 10
    player.used_mana = 0
    priest_full_health_target = player.field[0].entity_id if priest_full_health else None
    def expected_target(move):
        if hunter:
            return move['target'] == 0
        if review:
            return True
        target = priest_full_health_target if priest_full_health else player.opponent.hero.entity_id
        return move['target'] == target
    move = next(m for m in match.legal_moves(1) if m['kind'] == 'power' and expected_target(m))
    apply(match, 1, move, 'fixture-enemy-power')
    if warlock:
        apply(match, 1, dict(kind='end_turn'), 'fixture-warlock-turn')
    if priest:
        priest_hero = match.game.players[0].hero
        assert player.hero.power.id == 'HERO_08bp'
        assert player.get_heropower_damage(1) == 2
        assert priest_hero.health == (30 if priest_full_health else 28)
    if not warlock:
        apply(match, 1, dict(kind='end_turn'), 'fixture-turn2')
    match.observer.events.clear()
    assert match.snapshot(0)['players'][0]['power']['used'] is False
    assert match.snapshot(0)['players'][1]['power']['used'] is True
    if priest:
        assert match.snapshot(0)['players'][0]['power']['card_id'] == 'HERO_09bp'
        assert match.snapshot(0)['players'][0]['hero']['health'] == (30 if priest_full_health else 28)
    if warlock:
        state = match.snapshot(0)
        assert state['seat'] == 0
        assert state['players'][0]['power']['card_id'] == 'HERO_07bp'
        assert state['players'][0]['hero']['health'] == 29
        assert state['players'][0]['power']['used'] is False
        assert state['players'][1]['power']['used'] is True
        assert state['players'][0]['deck_count'] > 0 and len(state['players'][0]['hand']) < 10
    if rogue:
        state = match.snapshot(0)
        assert state['players'][0]['power']['card_id'] == 'HERO_03bp'
        assert state['players'][0]['weapon'] is None
    if shaman:
        state = match.snapshot(0)
        assert state['seat'] == 0
        assert state['players'][0]['power']['card_id'] == 'HERO_02bp'
        assert state['players'][0]['power']['used'] is False
        assert state['players'][0]['field'] == []
    return match


def generate(folder):
    folder.mkdir(parents=True, exist_ok=True)
    match = prepare(Match(['MageA', 'MageB'], [4, 4], [preset(4), preset(4)], 52))
    for seat in range(2):
        (folder / f'ready-seat{seat}.json').write_text(json.dumps(match.snapshot(seat), ensure_ascii=False), encoding='utf-8')
    player = match.game.players[0]
    move = next(m for m in match.legal_moves(0) if m['kind'] == 'power' and m['target'] == player.opponent.hero.entity_id)
    apply(match, 0, move, 'evidence-power')
    for seat in range(2):
        (folder / f'used-seat{seat}.json').write_text(json.dumps(match.snapshot(seat), ensure_ascii=False), encoding='utf-8')
    print('FIREBLAST FIXTURES', folder)


def wav_metadata(path):
    raw = path.read_bytes()
    if raw[:4] != b'RIFF' or raw[8:12] != b'WAVE':
        raise ValueError('Expected actual RIFF/WAVE capture')
    position, fmt, pcm = 12, None, None
    while position + 8 <= len(raw):
        tag, size = struct.unpack_from('<4sI', raw, position)
        data = raw[position + 8:position + 8 + size]
        if tag == b'fmt ':
            fmt = struct.unpack_from('<HHIIHH', data)
        elif tag == b'data':
            pcm = data
        position += 8 + size + (size & 1)
    if fmt is None or pcm is None:
        raise ValueError('WAV lacks actual format/data')
    encoding, channels, rate, _, align, bits = fmt
    if (encoding, bits) not in ((1, 16), (3, 32)):
        raise ValueError('Unreviewed evidence PCM encoding')
    values = array.array('f' if encoding == 3 else 'h')
    values.frombytes(pcm)
    if sys.byteorder != 'little':
        values.byteswap()
    scale = 1 if encoding == 3 else 32768
    peak = max(abs(v / scale) for v in values)
    energy = sum((v / scale) ** 2 for v in values)
    result = dict(file=path.name, sha256=hashlib.sha256(raw).hexdigest(), encoding=encoding, bits=bits,
                  channels=channels, rate=rate, frames=len(pcm) // align, duration=len(pcm) / align / rate,
                  peak=peak, rms=math.sqrt(energy / len(values)), clipped_samples=sum(abs(v / scale) > 1 for v in values))
    return result, values


def analyze_rogue_report(report, frames):
    before, after = report['rogueBefore'], report['rogueAfter']
    assert report['heroPower'] == 'HERO_03bp'
    assert before['powerCardId'] == after['powerCardId'] == 'HERO_03bp'
    assert before['powerUsed'] is False and after['powerUsed'] is True
    assert before['weaponEntity'] == 0 and before['weaponCardId'] == ''
    assert after['weaponCardId'] == 'CS2_082' and after['weaponAttack'] == 1 and after['weaponDurability'] == 2
    assert after['heroAttack'] == 1 and before['mana'] - after['mana'] == 2
    assert before['legalPowerMove'] is (report['role'] == 'driver')
    assert after['legalPowerMove'] is False
    assert after['legalEndTurn'] is (report['role'] == 'driver')
    if not report.get('rogueWeaponsMode'):
        assert report['roguePlayHeroPowerCount'] == 1
        assert report['rogueSummonCount'] == report['rogueDaggerSummonCount'] == 1
        assert report['rogueSummonTarget'] == after['weaponEntity']
        assert report['rogueUnexpectedHeroCueCount'] == 0
    assert report['rogueSourceCueCount'] == 1
    assert report['rogueSourceCueSemanticsValid'] is True
    assert report['rogueSourceOnly'] is True and report['rogueParentFlowConstructed'] is False
    assert report['rogueSourceRootNodeCount'] == 3 and report['rogueSourceRootReleased'] is True
    assert report['rogueSourceStateActions'] >= 2
    expected_source_audio_plays = 3 if report.get('rogueWeaponsShamanMode') else 2 if report.get('rogueWeaponsMode') else 1
    if report.get('rogueWeaponsShamanMode'):
        assert report['rogueSourceAudioPlayCount'] >= expected_source_audio_plays
    else:
        assert report['rogueSourceAudioPlayCount'] == expected_source_audio_plays
    if report['role'] == 'driver':
        assert report['listenerCount'] == 1
    assert before['publicHash'] != '' and before['publicHash'] != after['publicHash']
    if report.get('rogueWeaponsMode'):
        assert report['rogueDisplayHash'] == after['publicHash']
        assert report['publicHash'] == report['displayHash'] == report['rogueWeaponDisplayHash']
    else:
        assert report['publicHash'] == report['displayHash'] == after['publicHash']
    if report['role'] == 'driver':
        assert report['rogueInputUnlockedAfterSource'] is True
        assert report['rogueInputUnlockSampleFrame'] > 0
        sampled = [frame for frame in frames if frame['frame'] == report['rogueInputUnlockSampleFrame']]
        assert len(sampled) == 1
        sample = sampled[0]
        assert sample['sourceCompleted'] and sample['sourceReleased']
        assert not sample['inputLocked'] and sample['hashMatched'] and sample['endTurnAvailable']
    return dict(rule_before=before, rule_after=after, source_only=report['rogueSourceOnly'],
                source_cue_count=report['rogueSourceCueCount'], source_cue_semantics_valid=report['rogueSourceCueSemanticsValid'],
                source_root_released=report['rogueSourceRootReleased'],
                input_unlocked_after_source=report['rogueInputUnlockedAfterSource'],
                input_unlock_sample_frame=report['rogueInputUnlockSampleFrame'],
                authority_public_hash=report['publicHash'], display_hash=report['displayHash'])


def analyze_rogue_weapon_report(report, frames, lifecycle=None):
    assert report['rogueWeaponsMode'] is True and report['role'] == 'driver'
    assert report['rogueWeaponLifecycleComplete'] is True
    assert report['rogueWeaponInputUnlockedAfterAttack'] is True
    assert report['rogueWeaponInputUnlockSampleFrame'] > 0
    assert report['rogueWeaponDisplayHash'] == report['publicHash'] == report['displayHash']
    attacks = report['rogueWeaponAttacks']
    if report.get('rogueWeaponsShamanMode'):
        assert len(attacks) == 3
        first, second, third = attacks
        assert [attack['attackIndex'] for attack in attacks] == [1, 2, 3]
        assert [attack['targetEntity'] for attack in attacks] == [71, 73, 75]
        assert [attack.get('targetCardId') for attack in attacks] == list(ROGUE_WEAPONS_SHAMAN_TOTEMS)
        assert first['weaponDurability'] == 1 and first['weaponDeathEvents'] == 0
        assert second['weaponDurability'] == 0 and second['weaponDeathEvents'] == 1
        assert third['weaponDurability'] == 1 and third['weaponDeathEvents'] == 0
        for attack in attacks:
            assert attack['attackEvents'] == attack['targetHitEvents'] == attack['weaponHitEvents'] == 1
            assert attack['targetHealth'] == attack['targetHealthBefore'] - 1
            assert attack['hashMatched'] and attack['inputUnlocked']
            assert attack['publicHash'] == attack['displayHash']
        assert [attack['slot7Events'] for attack in attacks] == [1, 1, 1]
        assert [attack['slot5Events'] for attack in attacks] == [0, 1, 0]
        assert report['rogueWeaponSlot7Events'] == report['rogueWeaponSlot7AudioPlayCount'] == 3
        assert report['rogueWeaponSlot5Events'] == report['rogueWeaponSlot5AudioPlayCount'] == 1
        assert report['rogueWeaponReplacementCount'] == 2
        assert report['rogueWeaponEmptySlotEquipVersion'] == 13
        assert report['rogueWeaponOccupiedReplacementVersion'] == 18
        assert report['rogueWeaponPowerUseCount'] == 3
        assert report['rogueWeaponEmptySlotEquipVersion'] < report['rogueWeaponOccupiedReplacementVersion']
        assert report['rogueWeaponEndTurnGreenAfterOccupiedReplacement'] is True
        assert report['rogueWeaponOnlyEndTurnAfterOccupiedReplacement'] is True
        assert report['rogueWeaponResultReadyOnBothPeers'] is True
        assert report['rogueWeaponDisplayHash'] == report['publicHash'] == report['displayHash']
        if lifecycle is not None:
            lifecycle_summary = analyze_rogue_weapon_shaman_lifecycle(report, lifecycle)
        else:
            lifecycle_summary = None
        return dict(attacks=attacks, slot7_events=report['rogueWeaponSlot7Events'],
                    slot5_events=report['rogueWeaponSlot5Events'],
                    empty_slot_equip_version=report['rogueWeaponEmptySlotEquipVersion'],
                    occupied_replacement_version=report['rogueWeaponOccupiedReplacementVersion'],
                    end_turn_green_after_replacement=True, result_ready_on_both_peers=True,
                    final_public_hash=report['publicHash'], replacement_lifecycle=lifecycle_summary)
    assert len(attacks) == 2
    first, second = attacks
    assert [attack['attackIndex'] for attack in attacks] == [1, 2]
    assert report['rogueWeaponInputUnlockSampleFrame'] > 0
    assert first['weaponEntity'] > 0 and first['weaponAttack'] == 1 and first['weaponDurability'] == 1
    assert second['weaponEntity'] == second['weaponAttack'] == second['weaponDurability'] == 0
    for attack in attacks:
        assert attack['attackEvents'] == attack['targetHitEvents'] == attack['weaponHitEvents'] == 1
        assert attack['targetHealth'] == attack['targetHealthBefore'] - 1
        assert attack['hashMatched'] and attack['inputUnlocked']
        assert attack['publicHash'] == attack['displayHash']
    assert first['slot7Events'] == 1 and first['slot5Events'] == 0
    assert second['slot7Events'] == second['slot5Events'] == second['weaponDeathEvents'] == 1
    assert not first['legalNextAttack'] and first['legalEndTurn']
    assert not second['legalNextAttack'] and second['legalEndTurn']
    assert report['rogueWeaponSlot7Events'] == report['rogueWeaponSlot7AudioPlayCount'] == 2
    assert report['rogueWeaponSlot5Events'] == report['rogueWeaponSlot5AudioPlayCount'] == 1
    audio_records = report['rogueWeaponAudioRecords']
    durability = [record for record in audio_records if 'slot=7 ' in record['message']]
    destruction = [record for record in audio_records if 'slot=5 ' in record['message']]
    assert len(durability) == 2 and len(destruction) == 1
    assert all(record['audioSource'] == 'native-weapon' and 'actualStarted=True' in record['message'] for record in durability + destruction)
    assert all(record['file'] == '508d69647c76de93961e372c.wav' for record in durability)
    assert destruction[0]['file'] == '08d714cf9696d373af93bc9b.wav'
    unlock = [frame for frame in frames if frame['frame'] == report['rogueWeaponInputUnlockSampleFrame']
              and frame.get('version') == second['version'] and frame.get('target') == second['targetEntity']]
    assert len(unlock) == 1
    sample = unlock[0]
    assert not sample['inputLocked'] and not sample['busy'] and sample['hashMatched'] and sample['endTurnAvailable']
    assert sample['authorityHealth'] == sample['displayHealth'] == second['targetHealth']
    lifecycle_summary = None
    if lifecycle is not None:
        lifecycle_summary = analyze_rogue_weapon_lifecycle(report, lifecycle)
    return dict(attacks=attacks, slot7_events=report['rogueWeaponSlot7Events'],
                slot5_events=report['rogueWeaponSlot5Events'], slot7_audio_play_count=report['rogueWeaponSlot7AudioPlayCount'],
                slot5_audio_play_count=report['rogueWeaponSlot5AudioPlayCount'],
                source_audio_files=[record['file'] for record in durability + destruction],
                final_public_hash=report['publicHash'], input_unlocked_after_attack=True,
                input_unlock_sample_frame=report['rogueWeaponInputUnlockSampleFrame'],
                replacement_lifecycle=lifecycle_summary)


def analyze_rogue_weapon_lifecycle(report, lifecycle):
    assert lifecycle['role'] == report['role']
    points = lifecycle.get('points') or []
    assert points, 'Rogue lifecycle needs timestamped authority and presentation samples'
    if report['role'] == 'driver':
        assert lifecycle['replacementSubmitted'] and lifecycle['replacementAccepted']
        assert lifecycle['replacementSourceSeen'] and lifecycle['replacementSourceReleased']
        assert lifecycle['replacementInputUnlocked'] and lifecycle['endTurnSubmitted']
        assert lifecycle['endTurnAccepted'] and lifecycle['concedeSubmitted']
        assert lifecycle['concessionSynchronized']
        assert lifecycle['replacementAcceptedVersion'] > lifecycle['replacementSubmitVersion']
        assert lifecycle['endTurnAcceptedVersion'] > lifecycle['replacementAcceptedVersion']
        assert lifecycle['concedeAcceptedVersion'] > lifecycle['endTurnAcceptedVersion']
        assert lifecycle['finalHash'] == report['publicHash'] == report['displayHash']
        accepted = next(point for point in points if point['stage'] == 'replacement-power-accepted')
        unlocked = next(point for point in points if point['stage'] == 'replacement-input-unlocked')
        turn_accepted = next(point for point in points if point['stage'] == 'replacement-end-turn-accepted')
        concede_submitted = next(point for point in points if point['stage'] == 'concede-submitted')
        final = next(point for point in points if point['stage'] == 'concede-finished-synchronized')
        assert accepted['version'] == lifecycle['replacementAcceptedVersion']
        assert accepted['phase'] == 'play' and accepted['activePlayer'] == 0
        assert accepted['weaponEntity'] > 0 and accepted['weaponDurability'] == 2
        assert accepted['legalEndTurn'] and not accepted['legalPowerMove']
        assert unlocked['version'] == accepted['version'] and not unlocked['pending']
        assert not unlocked['inputLocked'] and not unlocked['nativeBusy'] and unlocked['hashMatched']
        assert unlocked['legalEndTurn'] and unlocked['endTurnReady']
        assert turn_accepted['version'] == lifecycle['endTurnAcceptedVersion']
        assert turn_accepted['phase'] == 'play' and turn_accepted['activePlayer'] == 1
        assert not turn_accepted['pending']
        assert concede_submitted['phase'] == 'play' and concede_submitted['activePlayer'] == 1
        assert concede_submitted['pending']
        assert final['version'] == lifecycle['concedeAcceptedVersion']
        assert final['phase'] == 'finished' and final['resultReady']
        assert not final['pending'] and not final['busy'] and not final['inputLocked'] and final['hashMatched']
        return dict(replacement_accepted_version=lifecycle['replacementAcceptedVersion'],
                    replacement_input_unlocked=dict(time=unlocked['time'], batch_time=unlocked['batchTime'],
                        busy=unlocked['busy'], native_busy=unlocked['nativeBusy'],
                        end_turn_ready=unlocked['endTurnReady'], end_turn_green=unlocked['endTurnGreen']),
                    end_turn_accepted_version=lifecycle['endTurnAcceptedVersion'],
                    concede_accepted_version=lifecycle['concedeAcceptedVersion'],
                    concession_synchronized=True, final_hash=lifecycle['finalHash'])
    assert lifecycle['replacementAccepted'] and lifecycle['endTurnAccepted']
    assert lifecycle['concessionSynchronized'] and lifecycle['finalHash'] == report['publicHash']
    final = next(point for point in points if point['stage'] == 'concede-finished-synchronized')
    assert final['phase'] == 'finished' and final['resultReady'] and final['hashMatched']
    assert not final['pending'] and not final['busy'] and not final['inputLocked']
    return dict(replacement_accepted_version=lifecycle['replacementAcceptedVersion'],
                end_turn_accepted_version=lifecycle['endTurnAcceptedVersion'],
                concession_synchronized=True, final_hash=lifecycle['finalHash'])


def analyze_rogue_weapon_shaman_lifecycle(report, lifecycle):
    assert lifecycle['role'] == report['role']
    points = lifecycle.get('points') or []
    assert points
    assert lifecycle['emptySlotEquipVersion'] == 13
    assert lifecycle['occupiedReplacementVersion'] == 18
    assert report['rogueWeaponPowerUseCount'] == 3
    if report['role'] == 'driver':
        empty_slot = next(point for point in points if point['stage'] == 'empty-slot-equip-input-unlocked')
        occupied = next(point for point in points if point['stage'] == 'occupied-replacement-input-unlocked')
        concede = next(point for point in points if point['stage'] == 'concede-submitted')
        final = next(point for point in points if point['stage'] == 'concede-finished-synchronized')
        assert report['rogueWeaponReplacementCount'] == 2
        assert report['rogueWeaponEmptySlotEquipVersion'] == empty_slot['version'] == 13
        assert report['rogueWeaponOccupiedReplacementVersion'] == occupied['version'] == 18
        assert occupied['weaponDurability'] == 2 and occupied['legalEndTurn']
        assert occupied['endTurnGreen'] and occupied['onlyEndTurn']
        assert not occupied['pending'] and not occupied['inputLocked'] and not occupied['nativeBusy']
        assert occupied['hashMatched'] and occupied['endTurnReady']
        assert concede['phase'] == 'play' and concede['activePlayer'] == 0 and concede['pending']
    else:
        empty_slot = next(point for point in points if point['stage'] == 'observer-empty-slot-equip-accepted')
        occupied = next(point for point in points if point['stage'] == 'observer-occupied-replacement-accepted')
        shaman_turn = next(point for point in points if point['stage'] == 'observer-shaman-power-2-accepted')
        shaman_turn_complete = next(point for point in points if point['stage'] == 'observer-shaman-turn-complete')
        final = next(point for point in points if point['stage'] == 'observer-both-peers-result-ready')
        concede = next(point for point in points if point['stage'] == 'concede-finished-synchronized')
        assert report['rogueObserverShamanPowerCount'] == 2
        assert report['rogueObserverShamanTotemSequence'] == '73:NEW1_009;75:CS2_051'
        assert empty_slot['version'] == lifecycle['emptySlotEquipVersion']
        assert occupied['version'] == lifecycle['occupiedReplacementVersion']
        assert shaman_turn['activePlayer'] == 1 and shaman_turn['phase'] == 'play'
        assert shaman_turn_complete['version'] == shaman_turn['version'] + 1
        assert shaman_turn_complete['activePlayer'] == 0 and shaman_turn_complete['phase'] == 'play'
        assert shaman_turn_complete['queueCount'] == 0
        assert not shaman_turn_complete['pending'] and not shaman_turn_complete['busy']
        assert not shaman_turn_complete['inputLocked'] and not shaman_turn_complete['nativeBusy']
        assert not shaman_turn_complete['resultReady'] and shaman_turn_complete['hashMatched']
    assert final['phase'] == 'finished' and final['resultReady']
    assert not final['pending'] and not final['busy'] and not final['inputLocked'] and final['hashMatched']
    assert lifecycle['concedeAcceptedVersion'] == concede['version'] == final['version']
    assert lifecycle['concessionSynchronized'] and lifecycle['resultReadyOnBothPeers']
    assert lifecycle['finalHash'] == report['publicHash'] == report['displayHash']
    return dict(empty_slot_equip_version=lifecycle['emptySlotEquipVersion'],
                occupied_replacement_version=lifecycle['occupiedReplacementVersion'],
                end_turn_green=report['rogueWeaponEndTurnGreenAfterOccupiedReplacement'] if report['role'] == 'driver' else None,
                only_end_turn_move=report['rogueWeaponOnlyEndTurnAfterOccupiedReplacement'] if report['role'] == 'driver' else None,
                concede_version=final['version'], result_ready=final['resultReady'],
                result_ready_on_both_peers=lifecycle['resultReadyOnBothPeers'],
                final_hash=lifecycle['finalHash'])


def analyze_rogue_weapon_peer_sync(driver_report, observer_report, driver_lifecycle, observer_lifecycle):
    assert driver_report['role'] == 'driver' and observer_report['role'] == 'observer'
    assert driver_report['result'].startswith('PASS') and observer_report['result'].startswith('PASS')
    assert driver_report['publicHash'] == observer_report['publicHash']
    final_hash = driver_report['publicHash']
    assert driver_lifecycle['concessionSynchronized'] and observer_lifecycle['concessionSynchronized']
    assert driver_lifecycle['finalHash'] == observer_lifecycle['finalHash'] == final_hash
    for field in ('replacementAcceptedVersion', 'endTurnAcceptedVersion', 'concedeAcceptedVersion'):
        assert driver_lifecycle[field] == observer_lifecycle[field], f'Rogue peers disagree on {field}'
    if driver_lifecycle.get('resultReadyOnBothPeers') or observer_lifecycle.get('resultReadyOnBothPeers'):
        assert driver_lifecycle['resultReadyOnBothPeers'] and observer_lifecycle['resultReadyOnBothPeers']
        for field in ('emptySlotEquipVersion', 'occupiedReplacementVersion'):
            assert driver_lifecycle[field] == observer_lifecycle[field], f'Rogue peers disagree on {field}'
        assert driver_report['rogueWeaponEndTurnGreenAfterOccupiedReplacement']
        assert driver_report['rogueWeaponOnlyEndTurnAfterOccupiedReplacement']
    return dict(final_hash=final_hash,
                replacement_accepted_version=driver_lifecycle['replacementAcceptedVersion'],
                empty_slot_equip_version=driver_lifecycle.get('emptySlotEquipVersion'),
                occupied_replacement_version=driver_lifecycle.get('occupiedReplacementVersion'),
                end_turn_accepted_version=driver_lifecycle['endTurnAcceptedVersion'],
                concede_accepted_version=driver_lifecycle['concedeAcceptedVersion'],
                both_players_finished=True,
                end_turn_green_after_occupied_replacement=driver_report.get('rogueWeaponEndTurnGreenAfterOccupiedReplacement'))


def warrior_audio_action_design(runtime):
    catalog = json.loads((runtime / 'native-super-spell.json').read_text(encoding='utf-8'))
    configs = [row for row in catalog.get('programs', []) if row.get('card_id') == 'HERO_01bp']
    if len(configs) != 1:
        raise ValueError('HERO_01bp source audio design requires one imported program')
    config = configs[0]
    impact_reference = ((config.get('impact') or {}).get('prefab') or {}).get('prefab_reference')
    impacts = [row for row in catalog.get('prefabs', []) if row.get('reference') == impact_reference]
    if not impact_reference or len(impacts) != 1:
        raise ValueError('HERO_01bp source audio design requires one imported Impact prefab')
    rows = ((config['file'], config['reference'], 1, 'parent'),
            (impacts[0]['file'], impact_reference, 0, 'impact_clone'))
    sounds = {}
    for filename, reference, owner, label in rows:
        program = json.loads((runtime / filename).read_text(encoding='utf-8'))
        if program.get('reference') != reference:
            raise ValueError('HERO_01bp ' + label + ' source audio program identity changed')
        machines = [machine for machine in program.get('machines', [])
                    if machine.get('owner_known') and machine.get('owner') == owner]
        actions = [action for machine in machines for state in machine.get('states', [])
                   for action in state.get('actions', []) if action.get('name') == 'AudioPlayClipAction']
        if len(machines) != 1 or len(actions) != 1:
            raise ValueError('HERO_01bp ' + label + ' must author exactly one AudioPlayClipAction')
        sound_params = [param for param in actions[0].get('params', []) if param.get('name') == 'm_Sound']
        if len(sound_params) != 1 or sound_params[0].get('unresolved') or not sound_params[0].get('asset_path'):
            raise ValueError('HERO_01bp ' + label + ' AudioPlayClipAction source is unresolved')
        sounds[label] = sound_params[0]['asset_path']
    if sounds['parent'] != sounds['impact_clone']:
        raise ValueError('HERO_01bp parent and Impact audio actions do not share one source clip')
    return dict(parent_action_count=1, impact_clone_action_count=1, total_action_count=2,
                shared_sound_asset=sounds['parent'])


def analyze_warrior_report(report, frames):
    before, after = report['warriorBefore'], report['warriorAfter']
    followup = report.get('warriorFollowup') or {}
    cast_version = followup.get('castAuthorityVersion')
    cast_events = followup.get('castAuthorityEvents') or []
    assert report['role'] in ('driver', 'observer')
    assert report['heroPower'] == 'HERO_01bp'
    assert cast_version == after['version'] and cast_version > before['version']
    ordered_events = [event for event in cast_events if event.get('type') in ('SpendMana', 'PlayHeroPower', 'GainArmor')]
    assert [event.get('type') for event in ordered_events] == ['SpendMana', 'PlayHeroPower', 'GainArmor']
    spend_event, power_event, armor_event = ordered_events
    assert spend_event.get('source') == before['powerEntity'] and spend_event.get('target') != 0
    assert spend_event.get('owner') == 0 and abs(spend_event.get('amount', 0) - 2) < 1e-3
    assert power_event.get('source') == spend_event.get('target') and power_event.get('target') == before['powerEntity']
    assert power_event.get('owner') == 0 and power_event.get('card_id') in (None, '')
    assert armor_event.get('source') == before['powerEntity'] and armor_event.get('target') == before['heroEntity']
    assert armor_event.get('owner') == 0 and abs(armor_event.get('amount', 0) - 2) < 1e-3
    assert before['powerCardId'] == after['powerCardId'] == 'HERO_01bp'
    assert before['powerUsed'] is False and after['powerUsed'] is True
    assert before['heroArmor'] == 0 and after['heroArmor'] == 2
    assert before['heroHealth'] == after['heroHealth']
    assert before['mana'] - after['mana'] == 2
    assert before['legalPowerMove'] is (report['role'] == 'driver')
    assert after['legalPowerMove'] is False
    assert after['legalEndTurn'] is (report['role'] == 'driver')
    assert report['warriorEventOrderValid'] is True
    assert report['warriorPlayHeroPowerCount'] == report['warriorGainArmorCount'] == 1
    assert report['warriorSourceCueCount'] == 1 and report['warriorSourceCueSemanticsValid'] is True
    assert report['warriorUnexpectedHitCueCount'] == 0
    assert report['warriorMissileEventCount'] == report['warriorArrivalEventCount'] == 0
    assert report['warriorSourceRootCompleted'] is True and report['warriorSourceRootReleased'] is True
    assert report['warriorParentCameraShakerStarts'] == 1
    assert report['warriorImpactCloneCameraShakerStarts'] == 1
    assert report['warriorParentCameraShakerStarts'] + report['warriorImpactCloneCameraShakerStarts'] == report['warriorCameraShakerStarts'] == 2
    assert report['warriorUnhandledActions'] == report['warriorParentUnhandledActions'] + report['warriorImpactCloneUnhandledActions']
    assert report['warriorUnhandledActions'] == report['warriorUnboundAudioActions'] == 0
    if report['role'] == 'driver':
        source_plays = [record for record in report['records'] if record.get('phase') == 'warrior-direct-cast' and
                        record.get('audioSource') == 'native-effect' and record.get('message') == 'source play']
        assert report['listenerCount'] == 1
        assert report['warriorSourceAudioPlayCount'] == len(source_plays) == 2
        assert len({record.get('file') for record in source_plays if record.get('file')}) == 1
        assert all(record.get('actionCount') == 1 for record in source_plays)
    if report['role'] == 'driver':
        assert report['warriorInputUnlockedAfterSource'] is True
        assert followup['inputUnlockedDuringSource'] is True
        assert followup['sendMoveSubmitted'] and followup['authorityAccepted']
        assert followup['sourceActiveAtSend'] is True
        assert followup['actionCounterDelta'] == 2
        assert followup['submitVersion'] == after['version']
        assert followup['acceptedVersion'] > followup['submitVersion'] and followup['activeAfter'] == 1
        assert 0 <= followup['sendLatency'] <= followup['latencyBudget'] <= .25
        assert report['warriorInputUnlockSampleFrame'] == followup['sampleFrame'] > 0
        sampled = [frame for frame in frames if frame['frame'] == followup['sampleFrame']]
        assert len(sampled) == 1
        sample = sampled[0]
        assert sample['version'] == followup['submitVersion']
        assert sample['sourceActive'] and sample['busy'] and not sample['inputLocked'] and sample['endTurnAvailable']
        assert sample['followupSent'] and sample['actionCounterDelta'] == followup['actionCounterDelta']
        assert abs(sample['followupSendLatency'] - followup['sendLatency']) < 1e-4
        assert followup['sourceTailCleanedAfterFollowup'] is True
        accepted = [frame for frame in frames if frame.get('followupAccepted')]
        assert accepted and accepted[0]['publicHash'] == followup['publicHash']
        assert followup['displayHash'] == report['publicHash'] == report['displayHash']
        cleaned = [frame for frame in frames if frame.get('followupTailCleaned') and frame.get('hashMatched')]
        assert cleaned and cleaned[-1]['sourceReleased'] and not cleaned[-1]['busy']
    else:
        followup = report.get('warriorFollowup') or {}
        assert not followup.get('sendMoveSubmitted', False)
    if frames:
        assert frames[-1]['authorityArmor'] == frames[-1]['displayArmor'] == 2
        assert frames[-1]['authorityHealth'] == frames[-1]['displayHealth'] == after['heroHealth']
    if report['role'] == 'observer':
        assert report['publicHash'] == report['displayHash']
        assert after['publicHash']
    return dict(rule_before=before, rule_after=after, cast_authority_version=cast_version,
                cast_authority_event_types=[event.get('type') for event in ordered_events],
                event_order_valid=report['warriorEventOrderValid'],
                source_cue_count=report['warriorSourceCueCount'],
                source_cue_semantics_valid=report['warriorSourceCueSemanticsValid'],
                camera_shaker_starts=report['warriorCameraShakerStarts'],
                parent_camera_shaker_starts=report['warriorParentCameraShakerStarts'],
                impact_clone_camera_shaker_starts=report['warriorImpactCloneCameraShakerStarts'],
                parent_unhandled_actions=report['warriorParentUnhandledActions'],
                impact_clone_unhandled_actions=report['warriorImpactCloneUnhandledActions'],
                parent_unhandled_action_details=report['warriorParentUnhandledActionDetails'],
                impact_clone_unhandled_action_details=report['warriorImpactCloneUnhandledActionDetails'],
                source_audio_play_count=report['warriorSourceAudioPlayCount'] if report['role'] == 'driver' else None,
                source_audio_capture_scope='driver-only; observer audio playback and PCM are not captured',
                source_root_released=report['warriorSourceRootReleased'],
                input_unlocked_after_source=report['warriorInputUnlockedAfterSource'],
                input_unlock_sample_frame=report['warriorInputUnlockSampleFrame'],
                input_unlocked_during_source=report.get('warriorFollowup', {}).get('inputUnlockedDuringSource', False),
                followup_send_latency=report.get('warriorFollowup', {}).get('sendLatency'),
                followup_accepted=report.get('warriorFollowup', {}).get('authorityAccepted', False),
                source_tail_cleaned_after_followup=report.get('warriorFollowup', {}).get('sourceTailCleanedAfterFollowup', False),
                authority_public_hash=report['publicHash'], display_hash=report['displayHash'])


def analyze_druid_report(report, frames):
    before, after = report['druidBefore'], report['druidAfter']
    assert report['heroPower'] == 'HERO_06bp'
    assert before['powerCardId'] == after['powerCardId'] == 'HERO_06bp'
    assert before['powerUsed'] is False and after['powerUsed'] is True
    assert before['heroAttack'] == 0 and after['heroAttack'] == 1
    assert before['heroArmor'] == 0 and after['heroArmor'] == 1
    assert before['heroHealth'] == after['heroHealth']
    assert before['mana'] - after['mana'] == 2
    assert before['legalPowerMove'] is (report['role'] == 'driver')
    assert after['legalPowerMove'] is False
    assert after['legalEndTurn'] is (report['role'] == 'driver')
    assert report['druidEventOrderValid'] is True
    assert report['druidSpendManaCount'] == report['druidPlayHeroPowerCount'] == 1
    assert report['druidBuffCount'] == report['druidGainArmorCount'] == 1
    assert report['druidCastRequested'] is True and report['druidCastCompleted'] is True
    assert report['druidFirstArrivalTimeMissing'] is True
    assert report['druidHitEventCount'] == 0
    assert report['druidUnexpectedHeroCueCount'] == 0
    assert report['druidMissileEventCount'] == report['druidArrivalEventCount'] == 0
    assert report['druidSourceStartActionCount'] == 1
    assert report['druidImpactCloneCount'] == report['druidImpactFinishedNotificationCount'] == 1
    assert report['druidSourceAnimationActionCount'] == report['druidSourceParticleActionCount'] == 1
    assert report['druidSourceCueCount'] == 1 and report['druidSourceCueSemanticsValid'] is True
    assert report['druidSourceRootCompleted'] is True and report['druidSourceRootReleased'] is True
    assert report['druidSourceAudioActionCount'] == report['druidSourceAudioPlayCount'] == 1
    assert report['druidUnhandledActions'] == report['druidUnboundAudioActions'] == 0
    assert report['druidMissingAudioPlaybackServices'] == 0
    source_plays = report['druidSourceAudioRecords']
    assert len(source_plays) == 1 and source_plays[0].get('file') == '74537c37a8e26deff87c5a4a.wav'
    if report['role'] == 'driver':
        assert report['listenerCount'] == 1
        assert report['druidInputUnlockedAfterSource'] is True
        assert report['druidInputUnlockSampleFrame'] > 0
        sampled = [frame for frame in frames if frame['frame'] == report['druidInputUnlockSampleFrame']]
        assert len(sampled) == 1
        sample = sampled[0]
        assert sample['sourceCompleted'] and sample['sourceReleased']
        assert not sample['busy'] and not sample['inputLocked']
        assert sample['hashMatched'] and sample['endTurnAvailable']
        assert sample['authorityAttack'] == sample['displayAttack'] == 1
        assert sample['authorityArmor'] == sample['displayArmor'] == 1
        assert sample['authorityHealth'] == sample['displayHealth'] == after['heroHealth']
    if frames:
        assert all(frame['target'] == 0 and not frame['arrival'] and
                   'MissileBirth' not in (frame.get('nativeEvents') or '') and
                   ' Arrival ' not in (frame.get('nativeEvents') or '') for frame in frames)
        assert frames[-1]['authorityAttack'] == frames[-1]['displayAttack'] == 1
        assert frames[-1]['authorityArmor'] == frames[-1]['displayArmor'] == 1
        assert frames[-1]['authorityHealth'] == frames[-1]['displayHealth'] == after['heroHealth']
    assert before['publicHash'] != '' and before['publicHash'] != after['publicHash']
    assert report['publicHash'] == report['displayHash'] == after['publicHash']
    return dict(rule_before=before, rule_after=after, event_order_valid=report['druidEventOrderValid'],
                cast_requested=report['druidCastRequested'], cast_completed=report['druidCastCompleted'],
                first_arrival_time_missing=report['druidFirstArrivalTimeMissing'],
                missile_event_count=report['druidMissileEventCount'], arrival_event_count=report['druidArrivalEventCount'],
                source_start_action_count=report['druidSourceStartActionCount'],
                impact_clone_count=report['druidImpactCloneCount'],
                impact_finished_notification_count=report['druidImpactFinishedNotificationCount'],
                source_animation_action_count=report['druidSourceAnimationActionCount'],
                source_particle_action_count=report['druidSourceParticleActionCount'],
                source_cue_count=report['druidSourceCueCount'],
                source_cue_semantics_valid=report['druidSourceCueSemanticsValid'],
                source_root_released=report['druidSourceRootReleased'],
                source_audio_action_count=report['druidSourceAudioActionCount'],
                source_audio_play_count=report['druidSourceAudioPlayCount'],
                input_unlocked_after_source=report['druidInputUnlockedAfterSource'],
                input_unlock_sample_frame=report['druidInputUnlockSampleFrame'],
                authority_public_hash=report['publicHash'], display_hash=report['displayHash'])


def analyze_warlock_report(report, frames):
    before, after = report['warlockBefore'], report['warlockAfter']
    assert report['role'] in ('driver', 'observer')
    assert report['heroPower'] == 'HERO_07bp'
    assert before['powerCardId'] == after['powerCardId'] == 'HERO_07bp'
    assert before['powerUsed'] is False and after['powerUsed'] is True
    assert before['heroHealth'] - after['heroHealth'] == 2
    assert before['mana'] - after['mana'] == 2
    if report['role'] == 'driver':
        assert after['handCount'] == before['handCount'] + 1
    else:
        assert before['handCount'] == after['handCount'] == 0
    assert after['deckCount'] == before['deckCount'] - 1
    assert before['legalPowerMove'] is (report['role'] == 'driver')
    assert after['legalPowerMove'] is False
    assert after['legalEndTurn'] is (report['role'] == 'driver')
    assert report['warlockEventOrderValid'] is True
    assert report['warlockSpendManaCount'] == report['warlockPlayHeroPowerCount'] == 1
    assert report['warlockHitCount'] == report['warlockDrawCount'] == 1
    assert report['warlockSourceCueCount'] == 1 and report['warlockSourceCueSemanticsValid'] is True
    assert report['warlockSourceWaitActionCount'] == 1
    assert report['warlockSourceSpellStateActionCount'] == 2
    assert report['warlockSourceRootCompleted'] is True and report['warlockSourceRootReleased'] is True
    assert report['warlockMissileEventCount'] == report['warlockArrivalEventCount'] == 0
    assert report['warlockUnhandledActions'] == report['warlockUnboundAudioActions'] == 0
    assert report['publicHash'] == report['displayHash'] == after['publicHash']
    if report['role'] == 'driver':
        assert report['listenerCount'] == 1
        assert report['warlockSourceAudioActionCount'] == report['warlockSourceAudioPlayCount'] == 1
        assert report['warlockInputUnlockedAfterSource'] is True
        assert report['warlockInputUnlockSampleFrame'] > 0
        sampled = [frame for frame in frames if frame['frame'] == report['warlockInputUnlockSampleFrame']]
        assert len(sampled) == 1
        sample = sampled[0]
        assert sample['sourceCompleted'] and sample['sourceReleased']
        assert not sample['busy'] and not sample['inputLocked']
        assert sample['hashMatched'] and sample['endTurnAvailable']
    return dict(rule_before=before, rule_after=after, event_order_valid=report['warlockEventOrderValid'],
                source_cue_count=report['warlockSourceCueCount'],
                source_cue_semantics_valid=report['warlockSourceCueSemanticsValid'],
                source_wait_action_count=report['warlockSourceWaitActionCount'],
                source_spell_state_action_count=report['warlockSourceSpellStateActionCount'],
                source_root_released=report['warlockSourceRootReleased'],
                source_audio_action_count=report['warlockSourceAudioActionCount'],
                source_audio_play_count=report['warlockSourceAudioPlayCount'],
                input_unlocked_after_source=report['warlockInputUnlockedAfterSource'],
                input_unlock_sample_frame=report['warlockInputUnlockSampleFrame'],
                authority_public_hash=report['publicHash'], display_hash=report['displayHash'])


def _paladin_authority_events(folder):
    rows = []
    match_dir = Path(folder) / 'matches'
    for path in sorted(match_dir.glob('*.jsonl')):
        for line in path.read_text(encoding='utf-8').splitlines():
            if line.strip():
                rows.append(json.loads(line))
    return rows


def _validate_paladin_observer_tail_advance(report, frames, authority_events):
    before, after = report['paladinBefore'], report['paladinAfter']
    assert report['role'] == 'observer'
    assert report['publicHash'] == after['publicHash'] and report['displayHash'] != report['publicHash']
    assert report['paladinAreaCompleted'] is True and report['paladinAreaPending'] == 0
    assert report['paladinAreaReleased'] is False
    assert authority_events, 'Observer hash lag requires the bounded match authority journal'

    power_rows = [row for row in authority_events
                  if row.get('error') == '' and row.get('command', {}).get('kind') == 'power' and
                  row.get('hash') == report['displayHash']]
    assert len(power_rows) == 1, 'Observer display hash must identify the single accepted HERO_04bp action result'
    power = power_rows[0]
    start_rows = [row for row in authority_events
                  if row.get('command', {}).get('kind') == 'GAME_START' and
                  row.get('hash') == before['publicHash'] and row.get('version') == power['command'].get('version')]
    assert len(start_rows) == 1, 'Paladin action must continue from the recorded initial authority snapshot'

    turn_rows = [row for row in authority_events
                 if row.get('error') == '' and row.get('command', {}).get('kind') == 'end_turn' and
                 row.get('command', {}).get('version') == power.get('version') and
                 row.get('seat') == power.get('seat') and row.get('hash') == report['publicHash'] and
                 row.get('version', 0) > power.get('version', 0)]
    assert len(turn_rows) == 1, 'Observer public hash must identify the accepted end-turn after the Paladin action'
    turn = turn_rows[0]
    overlapping_tail = [frame for frame in frames
                        if frame.get('version') == turn['version'] and frame.get('turnStart') is True and
                        frame.get('activePlayer') != turn.get('seat') and frame.get('sourceTailActive') is True and
                        frame.get('busy') is True and frame.get('inputLocked') is True]
    assert overlapping_tail, 'Timeline must show the next authority turn arriving while the original Area tail remains active'


def analyze_paladin_report(report, frames, authority_events=None):
    before, after = report['paladinBefore'], report['paladinAfter']
    assert report['role'] in ('driver', 'observer')
    assert report['heroPower'] == 'HERO_04bp'
    assert before['powerCardId'] == after['powerCardId'] == 'HERO_04bp'
    assert before['powerUsed'] is False and after['powerUsed'] is True
    assert before['mana'] - after['mana'] == 2
    assert before['recruitCount'] == 0 and after['recruitCount'] == 1
    assert after['recruitCardId'] == 'CS2_101t' and after['recruitAttack'] == after['recruitHealth'] == 1
    assert before['legalPowerMove'] is (report['role'] == 'driver')
    assert after['legalPowerMove'] is False
    assert after['legalEndTurn'] is (report['role'] == 'driver')
    if report['role'] == 'driver':
        assert report['paladinEventOrderValid'] is True
        assert report['paladinSpendManaCount'] == report['paladinPlayHeroPowerCount'] == report['paladinSummonCount'] == 1
        assert report['paladinRecruitSummonCount'] == 1
    else:
        assert report['paladinEventOrderValid'] is False
        assert report['paladinSpendManaCount'] == report['paladinPlayHeroPowerCount'] == report['paladinSummonCount'] == 0
        assert report['paladinRecruitSummonCount'] == 0
    assert report['paladinSourceCueCount'] == 1 and report['paladinSourceCueSemanticsValid'] is True
    assert report['paladinFriendlyAreaOnlyReconstruction'] is True
    assert report['paladinAreaReference'] == 'ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10'
    assert report['paladinAreaCloneCreations'] == report['paladinAreaFinishedNotifications'] == 1
    assert report['paladinAreaPending'] == 0
    assert report['paladinParentFlowConstructed'] is False
    assert report['paladinUnhandledActions'] == report['paladinUnboundAudioActions'] == 0
    source_audio = report['paladinSourceAudioRecords']
    assert report['paladinAudioPlayCount'] == len(source_audio)
    assert all(record.get('audioSource') == 'native-effect' and record.get('message') == 'source play'
               for record in source_audio)
    observer_tail_advance = report['publicHash'] != report['displayHash']
    if observer_tail_advance:
        _validate_paladin_observer_tail_advance(report, frames, authority_events or [])
    else:
        assert report['publicHash'] == report['displayHash'] == after['publicHash']
    if report['role'] == 'driver':
        assert report['listenerCount'] == 1
        assert report['rate'] > 0 and report['channels'] > 0 and report['sampleFrames'] > 0
        assert report['callbacks'] > 0 and report['overflows'] == 0
        assert report['paladinInputUnlockedAfterArea'] is True
        assert report['paladinInputUnlockSampleFrame'] > 0
        sampled = [frame for frame in frames if frame['frame'] == report['paladinInputUnlockSampleFrame']]
        assert len(sampled) == 1
        sample = sampled[0]
        assert sample['sourceCompleted'] and sample['busy'] and not sample['inputLocked']
        assert sample['sourceTailActive'] and not sample['sourceReleased']
        assert sample['hashMatched'] and sample['endTurnAvailable']
        assert report['paladinTailEndTurnSubmitted'] is True
        assert report['paladinTailSubmitFrame'] == report['paladinInputUnlockSampleFrame']
        assert report['paladinTurnSubmitVersion'] > 0
        assert report['paladinNextTurnAppliedDuringTail'] is True
        applied = [frame for frame in frames if frame['frame'] == report['paladinNextTurnAppliedFrame']]
        assert len(applied) == 1
        applied = applied[0]
        assert report['paladinNextTurnVersion'] > report['paladinTurnSubmitVersion']
        assert applied['version'] == report['paladinNextTurnVersion']
        assert applied['activePlayer'] == 1 and applied['turnStart'] and applied['sourceTailActive']
        assert report['paladinInputOpenDuringNextTurnTail'] is True
        synchronized = [frame for frame in frames if frame['frame'] == report['paladinNextTurnFrame']]
        assert len(synchronized) == 1
        synchronized = synchronized[0]
        assert synchronized['activePlayer'] == 1 and synchronized['turnStart']
        assert applied['frame'] <= synchronized['frame']
        assert synchronized['sourceTailActive'] and not synchronized['inputLocked'] and synchronized['hashMatched']
        assert report['paladinNextTurnPublicHash'] == report['paladinNextTurnDisplayHash']
        assert report['paladinTailReleasedAfterNextTurn'] is True
        released = [frame for frame in frames if frame['frame'] == report['paladinTailReleaseFrame']]
        assert len(released) == 1
        released = released[0]
        assert released['frame'] > synchronized['frame'] and released['sourceReleased']
        assert not released['sourceTailActive'] and not released['inputLocked'] and released['hashMatched']
    return dict(rule_before=before, rule_after=after, event_order_valid=report['paladinEventOrderValid'],
                recruit_summon_count=report['paladinRecruitSummonCount'],
                friendly_area_only_reconstruction=report['paladinFriendlyAreaOnlyReconstruction'],
                area_clone_creations=report['paladinAreaCloneCreations'],
                area_finished_notifications=report['paladinAreaFinishedNotifications'],
                parent_flow_constructed=report['paladinParentFlowConstructed'],
                unhandled_actions=report['paladinUnhandledActions'],
                source_audio_action_count=report['paladinAudioActionCount'],
                source_audio_play_count=report['paladinAudioPlayCount'],
                input_unlocked_after_area=report['paladinInputUnlockedAfterArea'],
                input_unlock_sample_frame=report['paladinInputUnlockSampleFrame'],
                end_turn_submitted_during_tail=report.get('paladinTailEndTurnSubmitted', False),
                next_turn_applied_during_tail=report.get('paladinNextTurnAppliedDuringTail', False),
                input_open_during_next_turn_tail=report.get('paladinInputOpenDuringNextTurnTail', False),
                tail_released_after_next_turn=report.get('paladinTailReleasedAfterNextTurn', False),
                observer_authority_advanced_during_area_tail=observer_tail_advance,
                authority_public_hash=report['publicHash'], display_hash=report['displayHash'])


def analyze(folder, hunter=False, priest=False, rogue=False, rogue_weapons=False, priest_full_health=False,
            rogue_weapons_shaman=False):
    return _analyze_evidence(folder, hunter=hunter, priest=priest, rogue=rogue or rogue_weapons or rogue_weapons_shaman,
                            rogue_weapons=rogue_weapons, priest_full_health=priest_full_health,
                            rogue_weapons_shaman=rogue_weapons_shaman)


def analyze_warrior(folder):
    return _analyze_evidence(folder, warrior=True)


def analyze_druid(folder):
    return _analyze_evidence(folder, druid=True)


def analyze_warlock(folder):
    return _analyze_evidence(folder, warlock=True)


def analyze_paladin_voice_capture(folder, project_root=PROJECT):
    folder = Path(folder)
    runtime = Path(project_root) / 'local' / 'runtime'
    voice_id = 'VOICE_CS2_101t_PLAY_0'
    index = json.loads((runtime / 'voice-index.json').read_text(encoding='utf-8'))
    registry = json.loads((runtime / 'registry.json').read_text(encoding='utf-8'))
    rows = [row for row in index.get('entries') or [] if row.get('id') == voice_id]
    if len(rows) != 1:
        raise AssertionError('Silver Hand Recruit original PLAY voice is missing or duplicated in voice-index')
    source = rows[0]
    if (source.get('card_id') != 'CS2_101t' or source.get('event') != 'PLAY' or
            source.get('suppress_play_sound_on_summon') is not False or
            not source.get('reference') or not source.get('bundle') or
            not source.get('cab') or not source.get('pathid') or not source.get('file') or
            not source.get('sha256')):
        raise AssertionError('Silver Hand Recruit PLAY voice source mapping is incomplete')
    assets = [row for row in registry.get('entries') or [] if row.get('id') == voice_id]
    if len(assets) != 1 or assets[0].get('path') != source['file']:
        raise AssertionError('Silver Hand Recruit PLAY voice is not linked through the runtime registry')
    source_path = runtime / source['file']
    if not source_path.is_file() or hashlib.sha256(source_path.read_bytes()).hexdigest() != source['sha256']:
        raise AssertionError('Silver Hand Recruit source WAV does not match voice-index SHA-256')

    log_pattern = re.compile(r'\bVOICE\s+' + re.escape(voice_id) +
                             r'\s+(?:samples=(?P<samples>\d+)\s+)?t=(?P<time>\d+(?:\.\d+)?)')
    player_logs = {}
    for role in ('driver', 'observer'):
        log_path = folder / role / 'player.log'
        if not log_path.is_file():
            raise AssertionError(f'{role} Player log is missing from Paladin voice evidence')
        matches = []
        for line_number, line in enumerate(log_path.read_text(encoding='utf-8', errors='replace').splitlines(), 1):
            match = log_pattern.search(line)
            if match:
                matches.append({'line': line_number, 'text': line.strip(),
                                'samples': int(match.group('samples')) if match.group('samples') else None,
                                'time': float(match.group('time'))})
        if len(matches) != 1 or (matches[0]['samples'] is not None and matches[0]['samples'] <= 0):
            raise AssertionError(f'{role} Player did not log exactly one actual {voice_id} playback')
        player_logs[role] = matches

    listener_path = folder / 'driver' / 'scene-mix.wav'
    if not listener_path.is_file():
        raise AssertionError('Paladin driver AudioListener scene-mix WAV is missing')
    listener_meta, listener_values = wav_metadata(listener_path)
    player_report = json.loads((folder / 'driver' / 'paladin-evidence.json').read_text(encoding='utf-8'))
    if (listener_meta['rate'] != player_report['rate'] or listener_meta['channels'] != player_report['channels'] or
            listener_meta['frames'] != player_report['sampleFrames'] or listener_meta['peak'] <= 0 or
            player_report['listenerCount'] != 1 or player_report['overflows'] != 0):
        raise AssertionError('Paladin Listener WAV does not match the captured DSP report')
    source_pcm, source_values = wav_metadata(source_path)
    correlation = pcm_envelope_correlation(source_pcm, source_values, listener_meta, listener_values)
    return {
        'card_id': 'CS2_101t', 'event': 'PLAY', 'voice_id': voice_id,
        'source': {key: source.get(key) for key in
                   ('name', 'asset_id', 'bundle', 'cab', 'pathid', 'file', 'sha256', 'reference', 'locale')},
        'player_log_playbacks': player_logs,
        'original_source_pcm': source_pcm,
        'listener_capture': dict(listener_meta, scope='full Unity AudioListener scene mix; not source-isolated'),
        'source_listener_correlation': correlation,
    }


def pcm_envelope_correlation(source_meta, source_values, listener_meta, listener_values, window_ms=10):
    def envelope(metadata, values):
        channels = metadata['channels']
        rate = metadata['rate']
        frames = len(values) // channels
        frames_per_window = max(1, round(rate * window_ms / 1000))
        scale = 32768 if metadata['encoding'] == 1 else 1
        output = []
        for start in range(0, frames, frames_per_window):
            end = min(frames, start + frames_per_window)
            energy = 0.0
            for frame in range(start, end):
                base = frame * channels
                for channel in range(channels):
                    sample = values[base + channel] / scale
                    energy += sample * sample
            output.append(math.sqrt(energy / ((end - start) * channels)))
        return output

    source = envelope(source_meta, source_values)
    listener = envelope(listener_meta, listener_values)
    if not source or len(listener) < len(source):
        raise AssertionError('Recruit source/listener PCM is too short for envelope comparison')
    source_mean = sum(source) / len(source)
    centered_source = [value - source_mean for value in source]
    source_norm = math.sqrt(sum(value * value for value in centered_source))
    if source_norm <= 1e-12:
        raise AssertionError('Recruit source clip has no varying RMS envelope')
    best_score = -1.0
    best_offset = -1
    for offset in range(len(listener) - len(source) + 1):
        sample = listener[offset:offset + len(source)]
        mean = sum(sample) / len(sample)
        centered_sample = [value - mean for value in sample]
        sample_norm = math.sqrt(sum(value * value for value in centered_sample))
        if sample_norm <= 1e-12:
            continue
        score = sum(left * right for left, right in zip(centered_source, centered_sample)) / (source_norm * sample_norm)
        if score > best_score:
            best_score, best_offset = score, offset
    return {'method': 'Pearson correlation of 10 ms RMS envelopes; mono source vs Listener mix',
            'window_ms': window_ms, 'best_offset_seconds': best_offset * window_ms / 1000,
            'best_correlation': best_score, 'source_windows': len(source),
            'listener_windows': len(listener),
            'interpretation': 'mixed-PCM similarity indicator; source playback is separately identified by Player VOICE logs'}


def analyze_paladin(folder, project_root=PROJECT):
    result = _analyze_evidence(folder, paladin=True)
    result['paladin_summon_voice'] = analyze_paladin_voice_capture(folder, project_root)
    (Path(folder) / 'evidence-index.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


def analyze_shaman(folder):
    folder = Path(folder)
    driver = json.loads((folder / 'driver' / 'report.json').read_text(encoding='utf-8'))
    observer = json.loads((folder / 'observer' / 'report.json').read_text(encoding='utf-8'))
    evidence = json.loads((folder / 'driver' / 'shaman-evidence.json').read_text(encoding='utf-8'))
    assert driver['role'] == 'driver' and observer['role'] == 'observer'
    assert driver['heroPower'] == observer['heroPower'] == evidence['heroPower'] == 'HERO_02bp'
    assert driver['result'].startswith('PASS') and observer['result'].startswith('PASS')
    assert driver['publicHash'] == observer['publicHash'] == evidence['publicHash'] == evidence['displayHash']
    assert evidence['eventOrderValid'] and evidence['eventLinkValid']
    assert evidence['spendManaCount'] == evidence['playHeroPowerCount'] == evidence['summonCount'] == 1
    assert evidence['totemEntity'] > 0 and evidence['totemCardId']
    assert evidence['nativeActiveCount'] == 1 and evidence['runtimeFailures'] == 0
    assert evidence['nativeActorVisible'] and evidence['actorVisualHiddenDuringSource']
    assert evidence['actorVisualRestoredAfterSource']
    assert evidence['sourceAudioPlayCount'] == len(evidence['sourceAudioRecords']) == 2
    assert all(record['file'] for record in evidence['sourceAudioRecords'])
    assert evidence['inputUnlockedDuringSource'] and 1.15 <= evidence['inputUnlockBatchTime'] < 2.5
    assert evidence['inputUnlockSampleFrame'] > 0 and evidence['displayHashMatched']
    assert evidence['sampleFrames'] > 0 and evidence['overflows'] == 0
    return dict(official_fidelity_verified=False, event_order_valid=evidence['eventOrderValid'],
                event_link_valid=evidence['eventLinkValid'], totem_entity=evidence['totemEntity'],
                totem_card_id=evidence['totemCardId'], native_actor_visible=evidence['nativeActorVisible'],
                actor_visual_hidden_during_source=evidence['actorVisualHiddenDuringSource'],
                actor_visual_restored_after_source=evidence['actorVisualRestoredAfterSource'],
                source_audio_play_count=evidence['sourceAudioPlayCount'],
                input_unlocked_during_source=evidence['inputUnlockedDuringSource'],
                input_unlock_batch_time=evidence['inputUnlockBatchTime'], authority_hash=evidence['publicHash'],
                display_hash=evidence['displayHash'])


def _analyze_evidence(folder, hunter=False, priest=False, rogue=False, warrior=False, druid=False, warlock=False, paladin=False, rogue_weapons=False, priest_full_health=False, rogue_weapons_shaman=False):
    priest = priest or priest_full_health
    rogue_weapon_mode = rogue_weapons or rogue_weapons_shaman
    paladin_authority_events = _paladin_authority_events(folder) if paladin else None
    from PIL import Image, ImageDraw
    result = dict(official_fidelity_verified=False, players=[], source_pcm=[])
    warrior_action_design = warrior_audio_action_design(PROJECT / 'local/runtime') if warrior else None
    clips = set()
    thumbnails = []
    for role in ('driver', 'observer'):
        path = folder / role
        report = json.loads((path / 'report.json').read_text(encoding='utf-8'))
        source_report = json.loads((path / ('warlock-evidence.json' if warlock else 'paladin-evidence.json')).read_text(encoding='utf-8')) if warlock or paladin else report
        images = []
        cast_thumbnails = []
        for png in sorted(path.glob('*.png')):
            with Image.open(png) as image:
                rgb = image.convert('RGB')
                assert max(channel[1] for channel in rgb.getextrema()) > 20
                images.append(dict(file=str(png.relative_to(folder)), width=image.width, height=image.height, sha256=hashlib.sha256(png.read_bytes()).hexdigest()))
                rgb.thumbnail((480, 270))
                item = (role + '/' + png.stem, rgb.copy())
                (cast_thumbnails if png.stem.startswith('review-cast-frame-') else thumbnails).append(item)
        player = dict(role=role, result=report['result'], public_hash=report['publicHash'], images=images,
                      hero_power='HERO_05bp' if hunter else 'HERO_09bp' if priest else
                      'HERO_03bp' if rogue else 'HERO_01bp' if warrior else 'HERO_06bp' if druid else
                      'HERO_07bp' if warlock else 'HERO_04bp' if paladin else 'HERO_08bp')
        timeline_path = path / 'timeline.json'
        frames = []
        if timeline_path.exists():
            frames = json.loads(timeline_path.read_text(encoding='utf-8'))['frames']
            waiting = [] if rogue or warrior or druid or warlock or paladin else [f for f in frames if not f['ready'] and (not hunter or f['hitCallbacks'] == 0)]
            ready = [] if rogue or warrior or druid or warlock or paladin else [f for f in frames if f['ready']]
            if hunter and frames:
                assert waiting and ready, 'Hunter timeline must cover pre-impact and resolved Hit states'
                assert all(not f['applied'] and f['displayHealth'] == frames[0]['displayHealth'] for f in waiting)
                assert all(f['inputLocked'] for f in waiting), 'Hunter interaction unlocked before native Hit resolved'
                assert any(f['hitCallbacks'] == 1 for f in ready), 'Hunter Hit callback missing after source readiness'
                player['actual_hit_timeline'] = dict(frames=len(frames), waiting_frames=len(waiting), ready_frames=len(ready),
                    hit_callbacks=max(f['hitCallbacks'] for f in frames), input_locked_until_hit=True,
                    final_authority_health=frames[-1]['authorityHealth'], final_display_health=frames[-1]['displayHealth'])
            if priest:
                assert report['heroPower'] == 'HERO_09bp'
                assert report['priestMode'] == ('full-health-no-heal' if priest_full_health else 'damaged-target-heal')
                if priest_full_health:
                    assert report['initialTargetHealth'] == report['finalTargetHealth'] == 30
                    assert report['healEventCount'] == report['healCallbacks'] == 0
                    assert report['priestDamageDecorations'] == 0
                else:
                    assert report['initialTargetHealth'] == 28 and report['finalTargetHealth'] == 30
                    assert report['healEventCount'] == report['healCallbacks'] == 1
                assert report['priestTargetEventCount'] == 1
                assert report['priestImpactVisualCaptured'] is True
                assert report['priestImpactTargetEntity'] == report['targetEntity']
                assert report['priestTargetGuiPositionValid'] is True
                assert any(image['file'].endswith('05-priest-impact-active.png') for image in images)
                assert 'StartBirth' in report['priestNativeEvents'] and 'ImpactScheduled' in report['priestNativeEvents']
                assert report['missileEventCount'] == report['arrivalEventCount'] == 0
                if role == 'driver':
                    assert report['inputUnlockedAfterSource'] is True
                    assert report['inputUnlockSampleFrame'] > 0
                assert report['publicHash'] == report['displayHash']
                player['actual_priest_power'] = dict(mode=report['priestMode'], target=report['targetEntity'],
                    initial_health=report['initialTargetHealth'], final_health=report['finalTargetHealth'],
                    authority_target_events=report['priestTargetEventCount'], authority_heal_events=report['healEventCount'],
                    presentation_heal_callbacks=report['healCallbacks'], impact_target_entity=report['priestImpactTargetEntity'],
                    target_gui_position=[report['priestTargetGuiX'], report['priestTargetGuiY']],
                    target_gui_position_error=report['priestTargetGuiPositionError'], missile_events=report['missileEventCount'],
                    arrival_events=report['arrivalEventCount'], input_unlocked_after_source=report.get('inputUnlockedAfterSource'),
                    input_unlock_sample_frame=report.get('inputUnlockSampleFrame'),
                    input_unlock_sample_time=report.get('inputUnlockSampleTime'),
                    input_unlock_batch_time=report.get('inputUnlockBatchTime'),
                    public_hash=report['publicHash'], display_hash=report['displayHash'])
                if frames:
                    assert max(frame['healEvents'] for frame in frames) == (0 if priest_full_health else 1)
                    assert max(frame['healCallbacks'] for frame in frames) == (0 if priest_full_health else 1)
                    assert frames[-1]['authorityHealth'] == frames[-1]['displayHealth'] == 30
                    assert not any(frame['arrival'] or 'MissileBirth' in (frame.get('nativeEvents') or '') or
                                   ' Arrival ' in (frame.get('nativeEvents') or '') for frame in frames)
                    if role == 'driver':
                        unlock_frame = report['inputUnlockSampleFrame']
                        sampled = [frame for frame in frames if frame['frame'] == unlock_frame]
                        assert len(sampled) == 1, 'Priest unlock report must point at one captured timeline frame'
                        sample = sampled[0]
                        assert sample['sourceCompleted'] and sample['sourceReleased'] and not sample['busy']
                        assert not sample['inputLocked'] and sample['hashMatched'] and sample['endTurnAvailable']
                        assert abs(sample['time'] - report['inputUnlockSampleTime']) < 1e-4
                        assert abs(sample['batchTime'] - report['inputUnlockBatchTime']) < 1e-4
                    player['actual_priest_timeline'] = dict(frames=len(frames), mode=report['priestMode'],
                        authority_health=frames[-1]['authorityHealth'], display_health=frames[-1]['displayHealth'],
                        max_authority_heal_events=max(frame['healEvents'] for frame in frames),
                        max_presentation_heal_callbacks=max(frame['healCallbacks'] for frame in frames),
                        input_unlocked_after_source=report.get('inputUnlockedAfterSource') if role == 'driver' else None,
                        input_unlock_sample_frame=report.get('inputUnlockSampleFrame') if role == 'driver' else None)
        if rogue:
            timeline_frames = frames if timeline_path.exists() else []
            player['actual_rogue_dagger'] = analyze_rogue_report(report, timeline_frames)
            if rogue_weapon_mode:
                lifecycle_path = path / 'rogue-weapon-lifecycle.json'
                lifecycle = json.loads(lifecycle_path.read_text(encoding='utf-8'))
                if role == 'driver':
                    player['actual_rogue_weapon_lifecycle'] = analyze_rogue_weapon_report(report, timeline_frames, lifecycle)
                    weapon_audio = report['rogueWeaponAudioRecords']
                    clips.update(record['file'] for record in weapon_audio if record.get('file'))
                    player['native_weapon_audio'] = weapon_audio
                else:
                    if rogue_weapons_shaman:
                        player['actual_rogue_concession_lifecycle'] = analyze_rogue_weapon_shaman_lifecycle(report, lifecycle)
                    else:
                        player['actual_rogue_concession_lifecycle'] = analyze_rogue_weapon_lifecycle(report, lifecycle)
        if warrior:
            timeline_frames = frames if timeline_path.exists() else []
            player['actual_warrior_armor'] = analyze_warrior_report(report, timeline_frames)
            if frames:
                assert all(not frame['arrival'] and 'MissileBirth' not in (frame.get('nativeEvents') or '') and
                           ' Arrival ' not in (frame.get('nativeEvents') or '') for frame in frames)
        if druid:
            timeline_frames = frames if timeline_path.exists() else []
            player['actual_druid_shapeshift'] = analyze_druid_report(report, timeline_frames)
            source_records = report['druidSourceAudioRecords']
            player['native_source_audio'] = dict(action_count=report['druidSourceAudioActionCount'],
                play_count=report['druidSourceAudioPlayCount'], play_records=source_records,
                listener_count=report['listenerCount'], rate=report['rate'], channels=report['channels'],
                sample_frames=report['sampleFrames'], callbacks=report['callbacks'], overflows=report['overflows'],
                peak=report['peak'], rms=report['rms'], clipped_samples=report['clippedSamples'])
        if warlock:
            timeline_frames = frames if timeline_path.exists() else []
            player['actual_warlock_life_tap'] = analyze_warlock_report(source_report, timeline_frames)
            source_records = source_report['warlockSourceAudioRecords']
            player['native_source_audio'] = dict(action_count=source_report['warlockSourceAudioActionCount'],
                play_count=source_report['warlockSourceAudioPlayCount'], play_records=source_records,
                listener_count=source_report['listenerCount'], rate=source_report['rate'], channels=source_report['channels'],
                sample_frames=source_report['sampleFrames'], callbacks=source_report['callbacks'], overflows=source_report['overflows'],
                peak=source_report['peak'], rms=source_report['rms'], clipped_samples=source_report['clippedSamples'])
        if paladin:
            timeline_frames = frames if timeline_path.exists() else []
            player['actual_paladin_recruit'] = analyze_paladin_report(
                source_report, timeline_frames, paladin_authority_events)
            player['native_source_audio'] = dict(action_count=source_report['paladinAudioActionCount'],
                play_count=source_report['paladinAudioPlayCount'], play_records=source_report['paladinSourceAudioRecords'],
                listener_count=source_report['listenerCount'], rate=source_report['rate'], channels=source_report['channels'],
                sample_frames=source_report['sampleFrames'], callbacks=source_report['callbacks'], overflows=source_report['overflows'],
                peak=source_report['peak'], rms=source_report['rms'], clipped_samples=source_report['clippedSamples'])
        if frames and not hunter and not priest and not rogue and not warrior and not druid and not warlock and not paladin:
                assert waiting and ready and all(f['hitCallbacks'] == 0 and not f['applied'] and f['tint'] == 0 for f in waiting)
                assert all(f['hitCallbacks'] == 1 for f in ready)
                start = ready[0]
                assert abs(start['batchTime'] - start['hitStart']) < 1e-5
                tint_errors, motion_errors, shake_errors = [], [], []
                for frame in ready:
                    age = frame['batchTime'] - frame['hitStart']
                    tint = math.sin(age / .28 * math.pi) * .65 if 0 <= age < .28 else 0
                    motion = abs(math.sin(age * 110) * 9 * math.exp(-age * 10)) if 0 <= age <= frame['hitDuration'] else 0
                    shake = math.hypot(math.sin(age * 130), math.cos(age * 110)) * 3 * (1 - age / .16) if 0 <= age < .16 else 0
                    tint_errors.append(abs(frame['tint'] - tint))
                    motion_errors.append(abs(frame.get('motion', motion) - motion))
                    shake_errors.append(abs(frame.get('shake', shake) - shake))
                drift = max(abs((f['time'] - start['time']) - (f['batchTime'] - start['batchTime'])) for f in ready)
                assert drift < .03, ('Real GUI reaction clock drift', drift)
                assert max(tint_errors) < .0001 and max(motion_errors) < .001 and max(shake_errors) < .001
                end = next(f for f in ready if f['batchTime'] - f['hitStart'] >= f['hitDuration'])
                assert all(f['displayHealth'] == waiting[0]['displayHealth'] for f in waiting)
                assert all(f['displayHealth'] == waiting[0]['displayHealth'] - 1 for f in ready)
                player['actual_hit_timeline'] = dict(frames=len(frames), waiting_frames=len(waiting), ready_frames=len(ready),
                    hit_callbacks=ready[-1]['hitCallbacks'], duration=start['hitDuration'], first_ready_age=start['batchTime'] - start['hitStart'],
                    complete_window_wall_seconds=end['time'] - start['time'], maximum_wall_batch_drift=drift,
                    maximum_tint_error=max(tint_errors), maximum_motion_error=max(motion_errors), maximum_shake_error=max(shake_errors))
        if cast_thumbnails:
            strip = Image.new('RGB', (1440, math.ceil(len(cast_thumbnails) / 3) * 302), '#202020')
            pen = ImageDraw.Draw(strip)
            for index, (label, bitmap) in enumerate(cast_thumbnails):
                x, y = index % 3 * 480, index // 3 * 302
                strip.paste(bitmap, (x, y + 25))
                pen.text((x + 5, y + 5), label, fill='white')
            strip.save(folder / f'{role}-cast-contact-sheet.png')
        if (path / 'scene-mix.wav').exists() and not (warrior and role == 'observer'):
            metadata, values = wav_metadata(path / 'scene-mix.wav')
            assert metadata['frames'] == report['sampleFrames'] and abs(metadata['peak'] - report['peak']) < 1e-6
            if hunter or priest or rogue or warrior or druid or warlock:
                assert metadata['duration'] > 0.1 and metadata['peak'] > 0.00001, 'Hero-power scene mix lacks captured audio'
            player['actual_scene_mix'] = metadata
            if priest:
                source_records = [record for record in report['records'] if record.get('audioSource') == 'native-effect']
                player['native_source_audio'] = dict(play_count=sum(record['message'] == 'source play' for record in source_records),
                    stop_count=sum(record['message'] == 'source stop' for record in source_records),
                    files=sorted({record['file'] for record in source_records if record.get('file')}),
                    play_records=[record for record in source_records if record['message'] == 'source play'],
                    listener_count=report['listenerCount'], rate=report['rate'], channels=report['channels'],
                    sample_frames=report['sampleFrames'], callbacks=report['callbacks'], overflows=report['overflows'],
                    peak=report['peak'], rms=report['rms'], clipped_samples=report['clippedSamples'])
            block = metadata['rate'] // 100 * metadata['channels']
            with (path / 'scene-mix-energy.csv').open('w', newline='', encoding='utf-8') as stream:
                writer = csv.writer(stream)
                writer.writerow(['seconds', 'peak', 'rms'])
                for offset in range(0, len(values), block):
                    part = values[offset:offset + block]
                    writer.writerow([offset / metadata['rate'] / metadata['channels'], max(abs(v) for v in part), math.sqrt(sum(v * v for v in part) / len(part))])
        audio_records = [] if warrior and role == 'observer' else \
            source_report['warlockSourceAudioRecords'] if warlock else source_report['paladinSourceAudioRecords'] if paladin else report['druidSourceAudioRecords'] if druid else report['records']
        clips.update(r['file'] for r in audio_records if r['message'] == 'source play' and r.get('file') and
                     (not (priest or rogue or warrior or druid or warlock or paladin) or r.get('audioSource') == 'native-effect'))
        result['players'].append(player)
    if warrior:
        hashes = {player['public_hash'] for player in result['players']}
        assert len(hashes) == 1, 'Warrior driver and observer public hashes differ'
        result['warrior_public_hash'] = next(iter(hashes))
        result['warrior_audio_action_design'] = warrior_action_design
    if hunter or priest or rogue or warrior or druid or warlock or paladin:
        audio_players = [player for player in result['players'] if not warrior or player['role'] == 'driver']
        source_records = [record for player in audio_players for record in
                          json.loads((folder / player['role'] / ('warlock-evidence.json' if warlock else 'paladin-evidence.json' if paladin else 'report.json')).read_text(encoding='utf-8'))[
                              'warlockSourceAudioRecords' if warlock else 'paladinSourceAudioRecords' if paladin else 'druidSourceAudioRecords' if druid else 'records']
                          if record['message'] == 'source play' and record.get('file') and
                          (not (priest or rogue or warrior or druid or warlock or paladin) or record.get('audioSource') == 'native-effect')]
        if not paladin:
            assert source_records, 'Native hero-power source did not route any audio cue to playback'
        result['native_action_audio'] = source_records
        if warrior:
            result['native_action_audio_scope'] = 'driver-only native-effect action/play records; driver scene-mix PCM is included when present; static source action design is counted separately; observer playback/PCM is not captured'
        if priest:
            driver_audio = result['players'][0].get('native_source_audio', {})
            assert driver_audio.get('play_count', 0) > 0, 'Priest driver did not record a native source audio cue'
        if rogue:
            driver_report = json.loads((folder / 'driver' / 'report.json').read_text(encoding='utf-8'))
            expected_rogue_source_plays = 3 if rogue_weapons_shaman else 2 if rogue_weapons else 1
            if rogue_weapons_shaman:
                assert driver_report['rogueSourceAudioPlayCount'] >= expected_rogue_source_plays
            else:
                assert driver_report['rogueSourceAudioPlayCount'] == expected_rogue_source_plays
            result['rogue_rule_hash'] = driver_report['rogueAfter']['publicHash']
        if rogue_weapon_mode:
            driver_report = json.loads((folder / 'driver' / 'report.json').read_text(encoding='utf-8'))
            observer_report = json.loads((folder / 'observer' / 'report.json').read_text(encoding='utf-8'))
            driver_lifecycle = json.loads((folder / 'driver' / 'rogue-weapon-lifecycle.json').read_text(encoding='utf-8'))
            observer_lifecycle = json.loads((folder / 'observer' / 'rogue-weapon-lifecycle.json').read_text(encoding='utf-8'))
            result['rogue_client_convergence'] = analyze_rogue_weapon_peer_sync(
                driver_report, observer_report, driver_lifecycle, observer_lifecycle)
        if warrior:
            driver_report = json.loads((folder / 'driver' / 'report.json').read_text(encoding='utf-8'))
            assert driver_report['warriorSourceAudioPlayCount'] == 2
            result['warrior_rule_hash'] = driver_report['warriorAfter']['publicHash']
        if druid:
            driver_report = json.loads((folder / 'driver' / 'report.json').read_text(encoding='utf-8'))
            assert driver_report['druidSourceAudioPlayCount'] == 1
            result['druid_rule_hash'] = driver_report['druidAfter']['publicHash']
        if warlock:
            driver_report = json.loads((folder / 'driver' / 'warlock-evidence.json').read_text(encoding='utf-8'))
            assert driver_report['warlockSourceAudioPlayCount'] == 1
            result['warlock_rule_hash'] = driver_report['warlockAfter']['publicHash']
        if paladin:
            driver_report = json.loads((folder / 'driver' / 'paladin-evidence.json').read_text(encoding='utf-8'))
            result['paladin_rule_hash'] = driver_report['paladinAfter']['publicHash']
    for clip in sorted(clips):
        metadata, _ = wav_metadata(PROJECT / 'local/runtime' / clip)
        result['source_pcm'].append(metadata)
    sheet = Image.new('RGB', (1440, math.ceil(len(thumbnails) / 3) * 302), '#202020')
    draw = ImageDraw.Draw(sheet)
    for index, (label, image) in enumerate(thumbnails):
        x, y = index % 3 * 480, index // 3 * 302
        sheet.paste(image, (x, y + 25))
        draw.text((x + 5, y + 5), label, fill='white')
    sheet.save(folder / 'contact-sheet.png')
    (folder / 'evidence-index.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('FIREBLAST ACTUAL EVIDENCE INDEX', folder, flush=True)
    return result


async def serve(port, folder, disconnect=False, unaffordable=False, natural=False, review=False, hunter=False, priest=False, rogue=False, warrior=False, druid=False, warlock=False, paladin=False, shaman=False, rogue_weapons=False, priest_full_health=False, rogue_weapons_shaman=False):
    from game import server as authority
    priest = priest or priest_full_health
    rogue_mode = rogue or rogue_weapons or rogue_weapons_shaman

    class EvidenceMatch(Match):
        def __init__(self, names, classes, decks):
            expected = [4, 9] if review else [3, 3] if hunter else [6, 4] if priest else [7, 8] if rogue_weapons_shaman else [7, 4] if rogue_mode else [10, 4] if warrior else [2, 4] if druid else [9, 4] if warlock else [5, 4] if paladin else [8, 4] if shaman else [4, 4]
            if classes != expected:
                raise ValueError('Hero-power evidence classes differ from explicit fixture')
            seed = ROGUE_WEAPONS_SHAMAN_SEED if rogue_weapons_shaman else 52
            super().__init__(names, classes, decks, seed)
            if rogue_weapons_shaman:
                prepare_rogue_weapons_shaman(self)
            else:
                prepare(self, review, hunter, priest, rogue_mode, warrior, druid, warlock, paladin, shaman, priest_full_health)
            if unaffordable:
                self.game.players[0].used_mana = self.game.players[0].max_mana - 1
    # Only fixture construction changes. Peer handling, commands, Match.apply,
    # version/replay validation and public-state broadcasts remain production.
    if not natural:
        authority.Match = EvidenceMatch
    original_send = authority.Peer.send

    async def observed_send(peer, payload):
        await original_send(peer, payload)
        if payload.get('type') == 'queued':
            (folder.parent / 'queued.json').write_text(json.dumps({'name': peer.name}), encoding='utf-8')

    authority.Peer.send = observed_send
    server = authority.Server(folder)
    listener = await asyncio.start_server(server.handle, '127.0.0.1', port)
    (folder.parent / 'authority-ready.json').write_text(json.dumps({'port': port}), encoding='utf-8')
    print('FIREBLAST AUTHORITY', port, folder, flush=True)
    async def close_requested_transport():
        while not (folder.parent / 'driver-disconnect-request.txt').exists():
            await asyncio.sleep(.05)
        count = 0
        for peers in list(server.rooms.values()):
            for peer in peers:
                peer.writer.close()
                count += 1
        (folder.parent / 'authority-disconnected.json').write_text(json.dumps({'closed_transports': count}), encoding='utf-8')
    closer = asyncio.create_task(close_requested_transport()) if disconnect else None
    async with listener:
        try:
            await listener.serve_forever()
        finally:
            if closer:
                closer.cancel()


def _ensure_player_report(folder, role, exit_code):
    report_path = folder / role / 'report.json'
    if report_path.exists():
        return json.loads(report_path.read_text(encoding='utf-8'))
    report = {
        'result': f'FAIL: Player exited with code {exit_code} before writing its formal replay report',
        'role': role,
        'failureKind': 'missing-player-report',
        'playerExitCode': exit_code,
        'playerLog': str(report_path.parent / 'player.log'),
        'checks': 0,
        'records': [],
    }
    temporary = report_path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(report_path)
    return report


def run_players(port, folder, target, size, disconnect, unaffordable, natural, review=False, hunter=False, priest=False, rogue=False, warrior=False, druid=False, warlock=False, paladin=False, shaman=False, rogue_weapons=False, priest_full_health=False, rogue_weapons_shaman=False):
    from .background_player import startup
    priest = priest or priest_full_health
    rogue = rogue or rogue_weapons or rogue_weapons_shaman
    if priest and target != 'own-hero':
        raise ValueError('The Priest evidence fixture requires --target own-hero')
    if rogue and target != 'enemy-hero':
        raise ValueError('The Rogue evidence fixture uses untargeted actions and accepts no --target override')
    if warrior and target != 'enemy-hero':
        raise ValueError('The Warrior evidence fixture uses its untargeted power and accepts no --target override')
    if druid and target != 'enemy-hero':
        raise ValueError('The Druid evidence fixture uses its untargeted power and accepts no --target override')
    if warlock and target != 'enemy-hero':
        raise ValueError('The Warlock evidence fixture uses its untargeted power and accepts no --target override')
    if paladin and target != 'enemy-hero':
        raise ValueError('The Paladin evidence fixture uses its untargeted power and accepts no --target override')
    if shaman and target != 'enemy-hero':
        raise ValueError('The Shaman evidence fixture uses its untargeted power and accepts no --target override')
    if not priest and target == 'own-hero':
        raise ValueError('--target own-hero is supported only with --priest')
    folder = folder.resolve()
    folder.mkdir(parents=True, exist_ok=False)
    executable = PROJECT / 'build/client/HearthstoneClone.exe'
    dll = executable.parent / 'HearthstoneClone_Data/Managed/Assembly-CSharp.dll'
    result = {'dll_sha256': hashlib.sha256(dll.read_bytes()).hexdigest(), 'processes': []}
    processes = []
    streams = []
    try:
        stream = (folder / 'server.log').open('w', encoding='utf-8')
        streams.append(stream)
        server_command = [sys.executable, '-X', 'utf8', '-m', 'tools.fireblast_evidence', '--serve', '--port', str(port), '--output', str(folder)]
        if disconnect:
            server_command.append('--disconnect')
        if unaffordable:
            server_command.append('--unaffordable')
        if natural:
            server_command.append('--natural')
        if review:
            server_command.append('--review')
        if hunter:
            server_command.append('--hunter')
        if priest:
            server_command.append('--priest')
            server_command.extend(['--target', 'own-hero'])
        if priest_full_health:
            server_command.append('--priest-full-health')
        if rogue:
            server_command.append('--rogue-weapons-shaman' if rogue_weapons_shaman else '--rogue-weapons' if rogue_weapons else '--rogue')
        if warrior:
            server_command.append('--warrior')
        if druid:
            server_command.append('--druid')
        if warlock:
            server_command.append('--warlock')
        if paladin:
            server_command.append('--paladin')
        if shaman:
            server_command.append('--shaman')
        server = subprocess.Popen(server_command,
                                  cwd=PROJECT, stdout=stream, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        processes.append(server)
        result['processes'].append({'role': 'server', 'pid': server.pid})
        deadline = time.monotonic() + 45
        while not (folder / 'authority-ready.json').exists():
            if server.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('Evidence authority did not bind before players')
            time.sleep(.1)
        names = ([('driver', 'HunterA'), ('observer', 'HunterB')] if hunter else
                 [('driver', 'PriestA'), ('observer', 'MageB')] if priest else
                 [('driver', 'RogueA'), ('observer', 'ShamanB' if rogue_weapons_shaman else 'MageB')] if rogue else
                 [('driver', 'WarriorA'), ('observer', 'MageB')] if warrior else
                 [('driver', 'DruidA'), ('observer', 'MageB')] if druid else
                 [('driver', 'WarlockA'), ('observer', 'MageB')] if warlock else
                 [('driver', 'PaladinA'), ('observer', 'MageB')] if paladin else
                 [('driver', 'ShamanA'), ('observer', 'MageB')] if shaman else
                 [('driver', 'MageA'), ('observer', 'MageB')])
        for role, name in names:
            evidence = folder / role
            evidence.mkdir()
            player_class = '3' if hunter else '6' if priest and role == 'driver' else '7' if rogue and role == 'driver' else '8' if rogue_weapons_shaman and role == 'observer' else '10' if warrior and role == 'driver' else '2' if druid and role == 'driver' else '9' if warlock and role == 'driver' else '5' if paladin and role == 'driver' else '8' if shaman and role == 'driver' else '9' if review and role == 'observer' else '4'
            replay_flag = '--hero-power-replay' if hunter or priest or rogue or warrior or druid or warlock or paladin or shaman else '--fireblast-replay'
            command = [str(executable), '--profile', 'f006b-' + folder.name + '-' + role, '--name', name, '--class', player_class, '--auto-match',
                       '--host', '127.0.0.1', '--port', str(port), replay_flag, 'duel' if natural else role, '--fireblast-evidence-dir', str(evidence),
                       '--fireblast-replay-target', target, '-screen-width', size.split('x')[0], '-screen-height', size.split('x')[1], '-screen-fullscreen', '0', '-logFile', str(evidence / 'player.log')]
            if hunter:
                command.extend(['--hero-power-replay-id', 'HERO_05bp'])
            if priest:
                command.extend(['--hero-power-replay-id', 'HERO_09bp'])
            if priest_full_health:
                command.append('--priest-full-health')
            if rogue:
                command.extend(['--hero-power-replay-id', 'HERO_03bp'])
            if rogue_weapons:
                command.append('--rogue-weapons')
            if rogue_weapons_shaman:
                command.append('--rogue-weapons-shaman')
            if warrior:
                command.extend(['--hero-power-replay-id', 'HERO_01bp'])
            if druid:
                command.extend(['--hero-power-replay-id', 'HERO_06bp'])
            if warlock:
                command.extend(['--hero-power-replay-id', 'HERO_07bp'])
            if paladin:
                command.extend(['--hero-power-replay-id', 'HERO_04bp'])
            if shaman:
                command.extend(['--hero-power-replay-id', 'HERO_02bp'])
            if disconnect:
                command.append('--fireblast-disconnect')
            if unaffordable:
                command.append('--fireblast-unaffordable')
            if natural:
                command.append('--autoplay')
            if review:
                command.append('--fireblast-review')
            player = subprocess.Popen(command, cwd=PROJECT, **startup(True))
            processes.append(player)
            result['processes'].append({'role': role, 'pid': player.pid})
            if role == 'driver':
                deadline = time.monotonic() + 45
                while not (folder / 'queued.json').exists():
                    if player.poll() is not None or time.monotonic() > deadline:
                        raise RuntimeError('Driver did not reach actual queue')
                    time.sleep(.25)
                assert json.loads((folder / 'queued.json').read_text())['name'] == name
        deadline = time.monotonic() + (620 if natural else 110)
        while any(p.poll() is None for p in processes[1:]):
            if time.monotonic() > deadline:
                raise TimeoutError('Bounded GUI players did not exit')
            time.sleep(.5)
        reports = [_ensure_player_report(folder, role, process.returncode)
                   for role, process in zip(('driver', 'observer'), processes[1:])]
        result['reports'] = reports
        if any(p.returncode != 0 for p in processes[1:]) or any(not r['result'].startswith('PASS') for r in reports):
            raise RuntimeError('Actual GUI replay failed; preserve reports and Player logs')
        if not disconnect and reports[0]['publicHash'] != reports[1]['publicHash']:
            raise AssertionError('Actual Player public hashes differ')
        if shaman:
            result['shaman'] = analyze_shaman(folder)
        result['result'] = ('PASS bounded formal Hunter GUI/input/native effect/DSP' if hunter else
                            ('PASS bounded formal Priest GUI/input/native effect/DSP ' + ('full-health-no-heal' if priest_full_health else 'damaged-target-heal')) if priest else
                            'PASS bounded formal Rogue/Shaman totem weapon lifecycle dual-Player GUI/authority/result sync' if rogue_weapons_shaman else
                            'PASS bounded formal Rogue weapon lifecycle dual-Player GUI/authority/source audio' if rogue_weapons else
                            'PASS bounded formal Rogue dagger GUI/input/source/DSP' if rogue else
                            'PASS bounded formal Warrior armor GUI/input/source/DSP' if warrior else
                            'PASS bounded formal Druid Shapeshift GUI/rules/source/DSP' if druid else
                            'PASS bounded formal Warlock Life Tap GUI/rules/source/DSP' if warlock else
                            'PASS bounded formal Paladin Recruit GUI/rules/source/DSP' if paladin else
                            'PASS bounded formal Shaman Totemic Call GUI/rules/source/DSP' if shaman else
                            'PASS bounded formal GUI/input/DSP')
    except Exception as exc:
        result['result'] = 'FAIL: ' + str(exc)
        raise
    finally:
        for process, record in zip(processes, result['processes']):
            if process.poll() is None:
                process.terminate()
                record['termination'] = 'owned authority cleanup' if record['role'] == 'server' else 'owned timed-out Player cleanup'
            process.wait(timeout=15)
            record['exit'] = process.returncode
        for role, process in zip(('driver', 'observer'), processes[1:]):
            _ensure_player_report(folder, role, process.returncode)
        for stream in streams:
            stream.close()
        (folder / 'run.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        print(result['result'], folder, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--serve', action='store_true')
    parser.add_argument('--analyze', action='store_true')
    parser.add_argument('--run-players', action='store_true')
    parser.add_argument('--review', action='store_true', help='Human-review Mage/Warlock and CS2_119 fixture')
    parser.add_argument('--hunter', action='store_true', help='Run the HERO_05bp target-power slice against two Hunter clients')
    parser.add_argument('--priest', action='store_true', help='Run the HERO_09bp own-hero healing slice against Priest and Mage clients')
    parser.add_argument('--priest-full-health', action='store_true', help='Run HERO_09bp at 30 health; assert one authority target event and no Heal or +0 decoration')
    parser.add_argument('--rogue', action='store_true', help='Run the HERO_03bp untargeted dagger slice against Rogue and Mage clients')
    parser.add_argument('--rogue-weapons', action='store_true', help='Replay Rogue dagger wear, next-turn replacement, end turn, and concession sync')
    parser.add_argument('--rogue-weapons-shaman', action='store_true', help='Replay the saved Rogue/Shaman entity and totem sequence through occupied-slot replacement and concession sync')
    parser.add_argument('--warrior', action='store_true', help='Run the HERO_01bp untargeted armor slice against Warrior and Mage clients')
    parser.add_argument('--druid', action='store_true', help='Run the HERO_06bp untargeted Shapeshift slice against Druid and Mage clients')
    parser.add_argument('--warlock', action='store_true', help='Run the HERO_07bp Life Tap slice against Warlock and Mage clients')
    parser.add_argument('--paladin', action='store_true', help='Run the HERO_04bp FriendlyArea-only Recruit slice against Paladin and Mage clients')
    parser.add_argument('--shaman', action='store_true', help='Run the HERO_02bp Totemic Call actor/source slice against Shaman and Mage clients')
    parser.add_argument('--demon-hunter', action='store_true', help='Run the isolated HERO_10bp self-hero Buff route with two GUI Players')
    parser.add_argument('--timing-only', action='store_true', help='With --demon-hunter, suppress PNG captures and validate timing evidence')
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--disconnect', action='store_true')
    modes.add_argument('--unaffordable', action='store_true')
    modes.add_argument('--natural', action='store_true')
    parser.add_argument('--target', choices=['enemy-hero', 'own-minion', 'own-hero'], default='enemy-hero')
    parser.add_argument('--size', choices=['1920x1080', '1280x720', '1600x1200'], default='1920x1080')
    parser.add_argument('--port', type=int, default=37245)
    parser.add_argument('--output', type=Path, default=PROJECT / 'work/f005-formal')
    args = parser.parse_args()
    if sum((args.review, args.hunter, args.priest or args.priest_full_health, args.rogue, args.rogue_weapons, args.rogue_weapons_shaman, args.warrior, args.druid, args.warlock, args.paladin, args.shaman, args.demon_hunter)) > 1:
        parser.error('--review and each hero-power flag select mutually exclusive fixtures')
    if args.demon_hunter and args.target != 'enemy-hero':
        parser.error('--demon-hunter uses the untargeted HERO_10bp route and accepts no --target override')
    if args.demon_hunter and args.size == '1600x1200':
        parser.error('--demon-hunter supports --size 1920x1080 or 1280x720')
    if args.demon_hunter and (args.disconnect or args.unaffordable or args.natural):
        parser.error('--demon-hunter only supports the exact authoritative legal H10 input route')
    if args.demon_hunter and not (args.serve or args.run_players or args.analyze):
        parser.error('--demon-hunter requires --serve, --run-players, or --analyze')
    if args.timing_only and not (args.demon_hunter and (args.run_players or args.analyze)):
        parser.error('--timing-only requires --demon-hunter with --run-players or --analyze')
    if args.priest_full_health and args.target == 'enemy-hero':
        args.target = 'own-hero'
    if (args.priest or args.priest_full_health) and args.target != 'own-hero':
        parser.error('--priest requires --target own-hero')
    if args.rogue and args.target != 'enemy-hero':
        parser.error('--rogue uses its untargeted power and accepts no --target override')
    if args.rogue_weapons and args.target != 'enemy-hero':
        parser.error('--rogue-weapons uses untargeted power/attack actions and accepts no --target override')
    if args.rogue_weapons_shaman and (args.target != 'enemy-hero' or args.natural):
        parser.error('--rogue-weapons-shaman uses the saved deterministic Rogue/Shaman action sequence')
    if args.warrior and args.target != 'enemy-hero':
        parser.error('--warrior uses its untargeted power and accepts no --target override')
    if args.druid and args.target != 'enemy-hero':
        parser.error('--druid uses its untargeted power and accepts no --target override')
    if args.warlock and args.target != 'enemy-hero':
        parser.error('--warlock uses its untargeted power and accepts no --target override')
    if args.paladin and args.target != 'enemy-hero':
        parser.error('--paladin uses its untargeted power and accepts no --target override')
    if args.shaman and args.target != 'enemy-hero':
        parser.error('--shaman uses its untargeted power and accepts no --target override')
    if not (args.priest or args.priest_full_health) and args.target == 'own-hero':
        parser.error('--target own-hero requires --priest')
    if args.analyze:
        if args.demon_hunter:
            if args.timing_only:
                from .demon_hunter_player_evidence import analyze_timing as analyze_demon_hunter_timing
                analyze_demon_hunter_timing(args.output.resolve(), args.size)
            else:
                from .demon_hunter_player_evidence import analyze as analyze_demon_hunter
                analyze_demon_hunter(args.output.resolve(), args.size)
        elif args.warrior:
            analyze_warrior(args.output.resolve())
        else:
            if args.druid:
                analyze_druid(args.output.resolve())
            elif args.warlock:
                analyze_warlock(args.output.resolve())
            elif args.paladin:
                analyze_paladin(args.output.resolve())
            elif args.shaman:
                analyze_shaman(args.output.resolve())
            else:
                analyze(args.output.resolve(), args.hunter, args.priest or args.priest_full_health, args.rogue, args.rogue_weapons, args.priest_full_health, args.rogue_weapons_shaman)
    elif args.run_players:
        if args.demon_hunter:
            from .demon_hunter_player_evidence import run_players as run_demon_hunter
            run_demon_hunter(args.port, args.output, args.size, timing_only=args.timing_only)
        else:
            run_players(args.port, args.output, args.target, args.size, args.disconnect, args.unaffordable, args.natural, args.review, args.hunter, args.priest or args.priest_full_health, args.rogue, args.warrior, args.druid, args.warlock, args.paladin, args.shaman, args.rogue_weapons, args.priest_full_health, args.rogue_weapons_shaman)
    elif args.serve:
        if args.demon_hunter:
            from .demon_hunter_player_evidence import serve as serve_demon_hunter
            asyncio.run(serve_demon_hunter(args.port, args.output))
        else:
            asyncio.run(serve(args.port, args.output / 'matches', args.disconnect, args.unaffordable, args.natural, args.review, args.hunter, args.priest or args.priest_full_health, args.rogue, args.warrior, args.druid, args.warlock, args.paladin, args.shaman, args.rogue_weapons, args.priest_full_health, args.rogue_weapons_shaman))
    else:
        generate(args.output / 'states')


if __name__ == '__main__':
    main()
