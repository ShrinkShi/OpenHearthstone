import json
import struct

from tools.hero_speech_player import DEFAULT_EMOTES, validate_player_report, validate_saved_reports


def passing_report(tmp_path):
    path = tmp_path / 'player.listener.wav'
    samples = [0.25, -0.25] * 100
    data = struct.pack('<' + 'f' * len(samples), *samples)
    fmt = struct.pack('<HHIIHH', 3, 2, 48000, 48000 * 8, 8, 32)
    path.write_bytes(b'RIFF' + struct.pack('<I', 36 + len(data)) + b'WAVEfmt ' + struct.pack('<I', 16) +
                     fmt + b'data' + struct.pack('<I', len(data)) + data)
    return {
        'server_supported': True,
        'opening_complete': True,
        'opening_play_count': 2,
        'menu_opened': True,
        'menu_selection_count': len(DEFAULT_EMOTES),
        'sent_options': list(DEFAULT_EMOTES),
        'received_emotes': [f'{seat}:HERO_{seat}:{emote}:event-{seat}-{index}'
                            for seat in (0, 1) for index, emote in enumerate(DEFAULT_EMOTES)],
        'voice_play_count': len(DEFAULT_EMOTES) * 2 + 2,
        'concede_owner': 0,
        'concede_received': True,
        'concede_bubble_rendered': True,
        'result_seen': True,
        'rendered_bubbles': ['0:Concede speech', '0:抱歉'],
        'seat': 0,
        'hero_card_id': 'HERO_0',
        'last_own_emote_id': 'SORRY',
        'emote_bubble_id': 'SORRY',
        'emote_bubble_text': '抱歉',
        'emote_bubble_captured': True,
        'listener_audio': {
            'path': str(path.resolve()), 'format': 'WAVE_FORMAT_IEEE_FLOAT', 'rate': 48000,
            'channels': 2, 'sample_frames': len(samples) // 2, 'callbacks': 2,
            'overflows': 0, 'peak': 0.25, 'rms': 0.25, 'clipped_samples': 0,
        },
    }, path


def test_player_report_requires_opening_both_peers_and_retained_concede_bubble(tmp_path):
    report, wav_path = passing_report(tmp_path)
    assert validate_player_report(report, DEFAULT_EMOTES, 'Concede speech', wav_path) == []

    report['concede_bubble_rendered'] = False
    errors = validate_player_report(report, DEFAULT_EMOTES, 'Concede speech', wav_path)
    assert 'accepted concede bubble was not drawn by the Player UI' in errors


def test_player_report_rejects_missing_opening_and_broadcasts(tmp_path):
    report, wav_path = passing_report(tmp_path)
    report['opening_play_count'] = 1
    report['received_emotes'] = report['received_emotes'][:len(DEFAULT_EMOTES)]
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path)
    assert 'the two opening voices did not each play exactly once' in errors
    assert 'player did not receive both peers\' emote broadcasts' in errors


def test_player_report_checks_each_peer_sent_every_requested_emote(tmp_path):
    report, wav_path = passing_report(tmp_path)
    report['received_emotes'].remove('0:HERO_0:WOW:event-0-3')
    report['received_emotes'].append('0:HERO_0:THANKS:event-0-duplicate')
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path)
    assert 'one or more emote IDs were not broadcast from both seats' in errors


def test_player_report_accepts_captured_first_emote_after_last_emote_advances(tmp_path):
    report, wav_path = passing_report(tmp_path)
    report['emote_bubble_id'] = 'THANKS'
    report['emote_bubble_text'] = '谢谢'
    report['rendered_bubbles'].append('0:谢谢')
    report['listener_audio']['rms'] = 0
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path,
                                    expected_emote_text='谢谢')
    assert not any('captured emote bubble' in error for error in errors)
    assert 'Unity AudioListener WAV is silent or its peak/RMS metrics do not match' in errors


def test_player_report_matches_captured_bubble_to_any_received_own_emote_and_original_text(tmp_path):
    report, wav_path = passing_report(tmp_path)
    report['emote_bubble_id'] = 'THANKS'
    report['emote_bubble_text'] = 'not the original subtitle'
    report['rendered_bubbles'].append('0:not the original subtitle')
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path,
                                    expected_emote_text='谢谢')
    assert 'captured emote bubble text does not match its original hero subtitle' in errors

    report['received_emotes'] = [item for item in report['received_emotes']
                                 if item != '0:HERO_0:THANKS:event-0-0']
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path)
    assert 'captured emote bubble does not match a received emote from this Player\'s hero' in errors

    report['emote_bubble_id'] = 'START'
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path)
    assert 'captured emote bubble does not match a received emote from this Player\'s hero' in errors


def test_saved_reports_can_be_revalidated_without_starting_player(tmp_path):
    report, _ = passing_report(tmp_path)
    report['emote_bubble_id'] = 'THANKS'
    report['emote_bubble_text'] = '谢谢'
    report['rendered_bubbles'].append('0:谢谢')
    report_path = tmp_path / 'player.json'
    report_path.write_text(json.dumps(report, ensure_ascii=False), encoding='utf-8')
    runtime = tmp_path / 'runtime'
    runtime.mkdir()
    (runtime / 'hero-speech-index.json').write_text(json.dumps({'entries': [
        {'card_id': 'HERO_0', 'emote_id': 'THANKS', 'text': '谢谢'},
    ]}, ensure_ascii=False), encoding='utf-8')

    results = validate_saved_reports([report_path], runtime)
    assert results == [{'report': str(report_path.resolve()), 'passed': True, 'errors': []}]


def test_opening_evidence_requires_stable_menu_capture_after_start(tmp_path):
    report, wav_path = passing_report(tmp_path)
    report['opening_evidence_enabled'] = True
    report['replay_harness_version'] = 1
    report['menu_capture_phase'] = 'mulligan'
    errors = validate_player_report(report, DEFAULT_EMOTES, expected_listener_path=wav_path)
    assert 'hero menu screenshot was not captured in play after START speech and bubble expiry' in errors
