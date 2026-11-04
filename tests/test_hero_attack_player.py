import json
import struct
from pathlib import Path
from types import SimpleNamespace

import pytest

from game.database import HEROES
from game.database import preset
from game.match import Match
from tools.hero_attack_player import (
    NATIVE_WEAPON_CASES,
    seed_weapon_equipment,
    validate_report,
    weapon_audio_sources,
    weapon_fixture_card_id,
)


def _peer(name, class_id):
    return SimpleNamespace(name=name, class_id=class_id)


def _write_float_wav(path):
    samples = [0.1, -0.1] * 256
    data = struct.pack('<' + 'f' * len(samples), *samples)
    fmt = struct.pack('<HHIIHH', 3, 2, 48000, 48000 * 8, 8, 32)
    path.write_bytes(b'RIFF' + struct.pack('<I', 36 + len(data)) + b'WAVEfmt ' + struct.pack('<I', 16) +
                     fmt + b'data' + struct.pack('<I', len(data)) + data)
    return samples


def _weapon_source_fixture(root):
    runtime = root / 'local' / 'runtime'
    runtime.mkdir(parents=True)
    rows = []
    refs = {
        5: 'Weapon_Destruction_01.wav:780a0d0d979a7684484209017ab3ddd9',
        7: 'Weapon_Durability_Hit_01.wav:8cc86918ea7f78f4487be0f430b11119',
        41: 'hero_weapon_draw.wav:e6f1e9d2da090c94fb2b0048acb84599',
        42: 'hero_weapon_draw.wav:e6f1e9d2da090c94fb2b0048acb84599',
    }
    for slot, reference in refs.items():
        file = f'weapon-slot-{slot}.wav'
        (runtime / file).write_bytes(f'clip-{slot}'.encode())
        clip = {'reference': reference, 'file': file,
                'source': {'type': 'AudioClip', 'bundle': f'bundle-{slot}', 'cab': f'CAB-{slot}',
                           'pathid': str(slot)}}
        row = {'slot': slot, 'definition': {'clips': [clip]} if slot == 7 else {},
               'sound': {'clips': [clip]} if slot != 7 else {}}
        rows.append(row)
    (runtime / 'weapon-audio.json').write_text(json.dumps({'entries': rows}), encoding='utf-8')
    return runtime, refs


def _weapon_report(tmp_path, hero, weapon_id, sources):
    wav_path = tmp_path / 'report.listener.wav'
    samples = _write_float_wav(wav_path)
    cues = []
    for slot, count in ((7, 2), (5, 2), (41, 1), (42, 1)):
        cues.extend({'source_name': f'Weapon slot {slot} match:1:{slot}:{index}',
                     'file': sources[slot]['file'], 'playback_started': True, 'started_at': index + 1.0}
                    for index in range(count))
    attacks = [{'event_key': f'{seat}:target', 'card_id': hero, 'weapon_card_id': weapon_id,
                'seat': seat, 'accepted_count': 1, 'voice_start_count': 1, 'voice_id': 'voice-attack',
                'accepted_at': 1.0, 'voice_started_at': 1.1}
               for seat in (0, 1)]
    report = {'attack_replay_enabled': True, 'attack_replay_complete': True, 'hero_card_id': hero,
              'seat': 0, 'attack_mulligan_count': 1, 'attack_move_count': 1, 'duplicate_attack_callbacks': 0,
              'hero_attacks': attacks, 'weapon_audio': cues,
              'listener_audio': {'path': str(wav_path.resolve()), 'rate': 48000, 'channels': 2,
                                 'sample_frames': len(samples) // 2, 'callbacks': 1, 'overflows': 0,
                                 'clipped_samples': 0, 'peak': 0.10000000149, 'rms': 0.10000000149}}
    report_path = tmp_path / 'report.json'
    report_path.write_text(json.dumps(report), encoding='utf-8')
    return report_path, report


def test_native_weapon_cases_are_class_legal_and_keep_rogue_legacy_name_compatible():
    assert NATIVE_WEAPON_CASES == (('fiery-war-axe', 10, 'CS2_106'), ('doomhammer', 8, 'EX1_567'))
    assert weapon_fixture_card_id([_peer('HeroAttackWeapon-CS2_106-0', 10),
                                   _peer('HeroAttackWeapon-CS2_106-1', 10)]) == 'CS2_106'
    assert weapon_fixture_card_id([_peer('HeroAttackWeapon-EX1_567-0', 8),
                                   _peer('HeroAttackWeapon-EX1_567-1', 8)]) == 'EX1_567'
    assert weapon_fixture_card_id([_peer('HeroAttackWeapon0', 7), _peer('HeroAttackWeapon1', 7)]) == 'CS2_082'
    with pytest.raises(ValueError, match='requires class id 10'):
        weapon_fixture_card_id([_peer('HeroAttackWeapon-CS2_106-0', 7),
                                _peer('HeroAttackWeapon-CS2_106-1', 7)])


@pytest.mark.parametrize(('class_id', 'card_id', 'maximum'), [(10, 'CS2_106', 2), (8, 'EX1_567', 8)])
def test_native_weapon_seed_uses_real_class_card_with_one_prior_durability_loss(class_id, card_id, maximum):
    deck = preset(class_id)
    match = Match(['WeaponA', 'WeaponB'], [class_id, class_id], [deck, deck], 52)
    for player in match.game.players:
        player.choice.choose()

    seeds = seed_weapon_equipment(match.game.players, card_id, terminal_wear=True)
    assert [player.weapon.id for player in match.game.players] == [card_id, card_id]
    assert [player.weapon.durability for player in match.game.players] == [1, 1]
    assert [player.weapon.max_durability for player in match.game.players] == [maximum, maximum]
    assert [seed['durability'] for seed in seeds] == [1, 1]
    assert [seed['max_durability'] for seed in seeds] == [maximum, maximum]


def test_weapon_audio_source_map_includes_original_draw_wear_and_destruction_clips(tmp_path):
    _, refs = _weapon_source_fixture(tmp_path)
    sources = weapon_audio_sources(tmp_path)

    assert set(sources) == {5, 7, 41, 42}
    assert {slot: source['reference'] for slot, source in sources.items()} == refs
    assert all(source['source']['type'] == 'AudioClip' for source in sources.values())


def test_native_weapon_report_requires_all_four_original_slots_and_listener_pcm(tmp_path):
    _, _ = _weapon_source_fixture(tmp_path)
    sources = weapon_audio_sources(tmp_path)
    report_path, report = _weapon_report(tmp_path, HEROES[10], 'CS2_106', sources)
    expected_counts = {7: 2, 5: 2, 41: 1, 42: 1}
    parsed, errors = validate_report(report_path, HEROES[10], 'voice-attack', sources,
                                     expected_counts, 'CS2_106')

    assert parsed['weapon_audio'] == report['weapon_audio']
    assert errors == []

    report['weapon_audio'] = [cue for cue in report['weapon_audio'] if not cue['source_name'].startswith('Weapon slot 5 ')]
    report_path.write_text(json.dumps(report), encoding='utf-8')
    _, errors = validate_report(report_path, HEROES[10], 'voice-attack', sources,
                                expected_counts, 'CS2_106')
    assert any('weapon slot 5' in error for error in errors)
    assert any('unknown or missing native weapon cue' in error for error in errors)
