"""Run two built Unity Players through the hero-emote UI replay on a private local server."""
from __future__ import annotations

import argparse
import json
import math
import os
import socket
import struct
import subprocess
import sys
import time
from pathlib import Path

from tools.background_player import startup
from game.hero_emotes import EMOTE_IDS


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_EMOTES = ('THANKS', 'WELL_PLAYED', 'GREETINGS', 'WOW', 'OOPS', 'THREATEN', 'SORRY')


def reserve_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind(('127.0.0.1', 0))
        return listener.getsockname()[1]


def wait_for_server(port, process, timeout=15):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f'hero speech test server exited with {process.returncode}')
        try:
            with socket.create_connection(('127.0.0.1', port), timeout=.5) as connection:
                connection.settimeout(.5)
                hello = json.loads(connection.makefile('rb').readline(8192))
                capabilities = hello.get('capabilities', [])
                if (hello.get('type') != 'hello' or 'hero_emotes_v1' not in capabilities or
                        'accounts_v1' in capabilities):
                    raise RuntimeError('server is not the expected loopback guest hero-emote fixture')
                return
        except (OSError, json.JSONDecodeError):
            time.sleep(.1)
    raise TimeoutError('hero speech test server did not become ready')


def concede_text(runtime, hero_id):
    index_path = runtime / 'hero-speech-index.json'
    if not index_path.is_file():
        return ''
    index = json.loads(index_path.read_text(encoding='utf-8'))
    entry = next((row for row in index.get('entries', [])
                  if row.get('card_id') == hero_id and row.get('emote_id') == 'CONCEDE'), None)
    return entry.get('text', '') if entry else ''


def emote_text(runtime, hero_id, emote_id):
    index_path = runtime / 'hero-speech-index.json'
    if not index_path.is_file():
        return ''
    index = json.loads(index_path.read_text(encoding='utf-8'))
    entry = next((row for row in index.get('entries', [])
                  if row.get('card_id') == hero_id and row.get('emote_id') == emote_id), None)
    return entry.get('text', '') if entry else ''


def validate_listener_audio(record, expected_path=None):
    errors = []
    if not isinstance(record, dict):
        return ['Unity AudioListener PCM capture was not reported']
    if record.get('error'):
        errors.append(f"Unity AudioListener capture failed: {record['error']}")
    try:
        audio_path = Path(record.get('path', '')).resolve()
    except (OSError, RuntimeError):
        return errors + ['Unity AudioListener WAV path is invalid']
    if expected_path is not None and os.path.normcase(str(audio_path)) != os.path.normcase(str(Path(expected_path).resolve())):
        errors.append('Unity AudioListener WAV path does not match this Player report')
        return errors
    if not audio_path.is_file():
        return errors + ['Unity AudioListener WAV file is missing']

    try:
        with audio_path.open('rb') as stream:
            header = stream.read(44)
            if len(header) != 44 or header[:4] != b'RIFF' or header[8:12] != b'WAVE' or header[12:16] != b'fmt ' or header[36:40] != b'data':
                raise ValueError('unsupported WAV header')
            format_tag, channels, rate, byte_rate, block_align, bits = struct.unpack_from('<HHIIHH', header, 20)
            data_size = struct.unpack_from('<I', header, 40)[0]
            if format_tag != 3 or channels != 2 or bits != 32 or block_align != channels * 4 or rate <= 0:
                raise ValueError('expected stereo IEEE float32 PCM')
            if data_size <= 0 or data_size % block_align or audio_path.stat().st_size != 44 + data_size:
                raise ValueError('invalid WAV sample data size')
            peak = 0.0
            energy = 0.0
            clipped = 0
            sample_count = 0
            remaining = data_size
            while remaining:
                chunk = stream.read(min(65536, remaining))
                if not chunk or len(chunk) % 4:
                    raise ValueError('truncated float sample data')
                remaining -= len(chunk)
                for (sample,) in struct.iter_unpack('<f', chunk):
                    if not math.isfinite(sample):
                        raise ValueError('non-finite float sample')
                    magnitude = abs(sample)
                    peak = max(peak, magnitude)
                    energy += sample * sample
                    clipped += magnitude > 1.0
                    sample_count += 1
    except (OSError, ValueError, struct.error) as error:
        return errors + [f'Unity AudioListener WAV is invalid: {error}']

    frames = sample_count // channels
    rms = math.sqrt(energy / sample_count) if sample_count else 0.0
    try:
        report_frames = int(record.get('sample_frames', 0))
        report_channels = int(record.get('channels', 0))
        report_rate = int(record.get('rate', 0))
        report_callbacks = int(record.get('callbacks', 0))
        report_overflows = int(record.get('overflows', -1))
        report_clipped = int(record.get('clipped_samples', -1))
        report_peak = float(record.get('peak', 0))
        report_rms = float(record.get('rms', 0))
    except (TypeError, ValueError, OverflowError):
        return errors + ['Unity AudioListener metrics are malformed']
    if report_rate != rate or report_channels != channels or report_frames != frames:
        errors.append('Unity AudioListener report sample rate/channels/frame count differs from its WAV')
    if report_callbacks <= 0 or report_overflows != 0 or report_clipped != clipped:
        errors.append('Unity AudioListener callback/overflow/clipping metrics are incomplete or inconsistent')
    if peak <= 0 or rms <= 0 or not math.isclose(report_peak, peak, rel_tol=1e-4, abs_tol=1e-6) or not math.isclose(report_rms, rms, rel_tol=1e-4, abs_tol=1e-6):
        errors.append('Unity AudioListener WAV is silent or its peak/RMS metrics do not match')
    return errors


def validate_opening_evidence(report, capture_dir=None, profile=''):
    if not report.get('opening_evidence_enabled'):
        return []
    errors = []
    voices = report.get('opening_voices', [])
    announcer_order = [item.get('event_name') for item in voices if item.get('source') == 'announcer']
    expected_announcer = ['ANNOUNCER_BEFORE_VERSUS', 'VERSUS', 'ANNOUNCER_AFTER_VERSUS']
    if announcer_order != expected_announcer:
        errors.append('opening announcer voice starts do not follow before/versus/after source order')
    if sum(item.get('source') == 'hero_speech' and item.get('event_name') == 'START' for item in voices) != 2:
        errors.append('both original Hero START voice clips were not observed')
    phases = [item.get('phase') for item in report.get('opening_phases', [])]
    required_phases = ['versus_complete', 'hero_start', 'fade_out', 'complete']
    phase_positions = [phases.index(phase) if phase in phases else -1 for phase in required_phases]
    if any(position < 0 for position in phase_positions) or phase_positions != sorted(phase_positions):
        errors.append('BattleOpening phase trace is missing or out of order')
    if not report.get('versus_drawn') or not report.get('mulligan_visible'):
        errors.append('actual Player did not report both the VS and mulligan frames')
    if (report.get('replay_harness_version', 0) >= 1 and
            (report.get('versus_drawn_age', -1) < .3 or not report.get('versus_announcer_playing'))):
        errors.append('VS frame was not captured after fade-in while an announcer clip was playing')
    if not report.get('mulligan_interactable'):
        errors.append('mulligan input did not become interactable after BattleOpening')
    if not report.get('mulligan_interactable_before_start_complete'):
        errors.append('mulligan input remained gated until Hero START playback completed')
    if report.get('replay_harness_version', 0) >= 2:
        if report.get('mulligan_input_repaint_count', 0) < 1:
            errors.append('mulligan interactivity was not recorded during a Repaint before replay input')
        if (not report.get('replay_mulligan_keep_submitted') or
                not report.get('replay_mulligan_keep_accepted') or
                report.get('replay_mulligan_keep_accepted_phase') != 'play' or
                report.get('replay_mulligan_keep_accepted_version', -1) < 0 or
                report.get('replay_mulligan_keep_submitted_at', -1) < report.get('mulligan_interactable_at', -1) or
                report.get('replay_mulligan_keep_accepted_at', -1) < report.get('replay_mulligan_keep_submitted_at', -1)):
            errors.append('replay Keep was not submitted after the input Repaint and accepted into play')
    if not report.get('start_bubble_visible_during_mulligan'):
        errors.append('original START bubble was not rendered during mulligan')
    frame_tags = set(report.get('opening_frame_tags', []))
    required_frames = {'battle-opening-vs', 'battle-opening-mulligan', 'battle-opening-mulligan-input'}
    if not required_frames.issubset(frame_tags):
        errors.append('opening evidence frame tags are incomplete')
    if capture_dir is not None:
        missing = [tag for tag in required_frames
                   if not (capture_dir / f'{profile}-{tag}.png').is_file()]
        if missing:
            errors.append('opening screenshot files are missing: ' + ', '.join(sorted(missing)))
    times = {item.get('phase'): item.get('time') for item in report.get('opening_phases', [])}
    if (times.get('versus_complete', -1) < 0 or times.get('hero_start', -1) < times.get('versus_complete', -1) or
            times.get('complete', -1) < times.get('hero_start', -1) or
            report.get('mulligan_interactable_at', -1) < times.get('complete', -1)):
        errors.append('opening and mulligan timestamps do not prove the expected release order')
    return errors


def validate_player_report(report, emotes, expected_concede_text='', expected_listener_path=None,
                           expected_emote_text='', expected_capture_dir=None, profile=''):
    errors = []
    expected = list(emotes)
    if not report.get('server_supported'):
        errors.append('server capability was not negotiated')
    if not report.get('opening_complete'):
        errors.append('opening sequence did not finish before replay')
    if report.get('opening_play_count') != 2:
        errors.append('the two opening voices did not each play exactly once')
    if not report.get('menu_opened'):
        errors.append('right-click replay did not open the hero menu')
    if report.get('opening_evidence_enabled') and report.get('replay_harness_version', 0) >= 1 and (
            report.get('menu_capture_phase') != 'play' or not report.get('menu_capture_after_start') or
            not report.get('menu_capture_after_start_bubble_expired')):
        errors.append('hero menu screenshot was not captured in play after START speech and bubble expiry')
    if report.get('menu_selection_count') != len(expected):
        errors.append('not all requested menu options were selected')
    if report.get('sent_options') != expected:
        errors.append('sent emote sequence differs from the requested sequence')
    received = report.get('received_emotes', [])
    if len(received) < len(expected) * 2:
        errors.append('player did not receive both peers\' emote broadcasts')
    received_pairs = set()
    for item in received:
        fields = item.split(':', 3)
        if len(fields) == 4 and fields[0] in ('0', '1'):
            received_pairs.add((int(fields[0]), fields[2]))
    if not {(seat, emote) for seat in (0, 1) for emote in expected}.issubset(received_pairs):
        errors.append('one or more emote IDs were not broadcast from both seats')
    if report.get('voice_play_count', 0) < len(expected) * 2 + 2:
        errors.append('original opening/emote audio clips did not all start')
    if report.get('concede_owner', -1) not in (0, 1) or not report.get('concede_received'):
        errors.append('accepted concede speech event was not received')
    if not report.get('result_seen'):
        errors.append('finished result was not rendered')
    bubbles = report.get('rendered_bubbles', [])
    if not report.get('concede_bubble_rendered'):
        errors.append('accepted concede bubble was not drawn by the Player UI')
    if expected_concede_text and f"{report.get('concede_owner')}:{expected_concede_text}" not in bubbles:
        errors.append('concede bubble was not retained on the result screen')
    if not bubbles:
        errors.append('no hero speech bubbles were observed')
    if not report.get('emote_bubble_captured'):
        errors.append('no rendered bubble was captured after receiving this Player\'s own emote')
    own_seat = str(report.get('seat', -1))
    own_hero = report.get('hero_card_id', '')
    own_emotes = set()
    for item in received:
        fields = item.split(':', 3)
        if len(fields) == 4 and fields[0] == own_seat and fields[1] == own_hero:
            own_emotes.add(fields[2])
    captured_id = report.get('emote_bubble_id', '')
    captured_text = report.get('emote_bubble_text', '')
    if captured_id not in expected or captured_id not in own_emotes:
        errors.append('captured emote bubble does not match a received emote from this Player\'s hero')
    if not captured_text or f'{own_seat}:{captured_text}' not in bubbles:
        errors.append('captured emote bubble text is missing from this Player\'s rendered bubbles')
    if expected_emote_text and captured_text != expected_emote_text:
        errors.append('captured emote bubble text does not match its original hero subtitle')
    errors.extend(validate_opening_evidence(report, expected_capture_dir, profile))
    errors.extend(validate_listener_audio(report.get('listener_audio'), expected_listener_path))
    return errors


def validate_saved_reports(report_paths, runtime):
    results = []
    for raw_path in report_paths:
        report_path = Path(raw_path).resolve()
        result = {'report': str(report_path), 'passed': False, 'errors': []}
        if not report_path.is_file():
            result['errors'] = ['Player report is missing']
            results.append(result)
            continue
        try:
            report = json.loads(report_path.read_text(encoding='utf-8'))
            if not isinstance(report, dict):
                raise ValueError('report root must be a JSON object')
            emotes = report.get('requested_options') or list(DEFAULT_EMOTES)
            if (not isinstance(emotes, list) or not emotes or
                    any(not isinstance(emote, str) or emote not in EMOTE_IDS for emote in emotes) or
                    len(set(emotes)) != len(emotes)):
                result['errors'].append('report contains invalid requested emote IDs')
            else:
                expected_emote = emote_text(runtime, report.get('hero_card_id', ''), report.get('emote_bubble_id', ''))
                if not expected_emote:
                    result['errors'].append('captured hero emote subtitle is missing from the local source index')
                result['errors'].extend(validate_player_report(report, emotes,
                    concede_text(runtime, report.get('concede_card_id', '')),
                    report_path.with_suffix('.listener.wav'), expected_emote,
                    report_path.parent.parent / 'captures', report_path.stem))
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            result['errors'].append(f'Player report could not be validated: {error}')
        result['passed'] = not result['errors']
        results.append(result)
    return results


def stop_child(process, timeout=4):
    if process is None or process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=timeout)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', default=str(ROOT / 'build/client/HearthstoneClone.exe'))
    parser.add_argument('--class-a', type=int, default=4)
    parser.add_argument('--class-b', type=int, default=10)
    parser.add_argument('--emotes', default=','.join(DEFAULT_EMOTES))
    parser.add_argument('--timeout', type=int, default=150)
    parser.add_argument('--output-dir', default='')
    parser.add_argument('--validate-reports', nargs='+', metavar='REPORT',
                        help='validate existing Player JSON reports and Listener WAVs without starting Unity or a server')
    parser.add_argument('--foreground', action='store_true', help='allow test windows to take focus')
    args = parser.parse_args(argv)

    emotes = tuple(value.strip() for value in args.emotes.split(',') if value.strip())
    if not emotes or any(value not in EMOTE_IDS for value in emotes) or len(set(emotes)) != len(emotes):
        parser.error('--emotes must be a comma-separated list of unique approved emote IDs')
    if args.validate_reports:
        runtime = ROOT / 'local' / 'runtime'
        if not (runtime / 'hero-speech-index.json').is_file():
            parser.error('local hero-speech-index.json is required to validate saved reports')
        results = validate_saved_reports(args.validate_reports, runtime)
        summary = {'passed': all(result['passed'] for result in results), 'reports': results}
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0 if summary['passed'] else 1
    if os.name != 'nt':
        parser.error('the Unity Player harness currently requires Windows')
    client_path = Path(args.client).resolve()
    if not client_path.is_file():
        parser.error(f'Unity Player not found: {client_path}')
    local = ROOT / 'local' / 'runtime'
    if not (local / 'registry.json').is_file() or not (local / 'hero-speech-index.json').is_file():
        parser.error('run the root-controlled asset export before starting Player replay')

    if args.output_dir:
        output = Path(args.output_dir).resolve()
    else:
        output = ROOT / 'work' / f'hero-speech-player-{int(time.time())}-{os.getpid()}'
    try:
        output.relative_to(ROOT.resolve())
    except ValueError:
        parser.error('output directory must remain inside the repository work tree')
    captures = output / 'captures'
    reports = output / 'reports'
    logs = output / 'logs'
    for folder in (captures, reports, logs, output / 'matches'):
        folder.mkdir(parents=True, exist_ok=True)

    port = reserve_port()
    server_log_path = logs / 'server.log'
    client_processes = []
    server = None
    summary = {'port': port, 'emotes': list(emotes), 'clients': [], 'server_log': str(server_log_path)}
    try:
        server_log = server_log_path.open('ab')
        server = subprocess.Popen([sys.executable, '-X', 'utf8', '-m', 'tools.guest_fixture_server',
            '--host', '127.0.0.1', '--port', str(port), '--logdir', str(output / 'matches')],
            cwd=ROOT, stdout=server_log, stderr=subprocess.STDOUT,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        wait_for_server(port, server)

        profiles = [f'heroSpeech{os.getpid()}A', f'heroSpeech{os.getpid()}B']
        classes = [args.class_a, args.class_b]
        for index in range(2):
            profile = profiles[index]
            report_path = reports / f'{profile}.json'
            command = [str(client_path), '--profile', profile, '--name', f'HeroSpeech{index+1}',
                '--host', '127.0.0.1', '--port', str(port), '--class', str(classes[index]),
                '--auto-match', '--hero-speech-replay', ','.join(emotes),
                '--hero-speech-report', str(report_path), '--hero-speech-replay-exit',
                '--screenshot-dir', str(captures), '-logFile', str(logs / f'{profile}.log')]
            if index == 0:
                command.append('--hero-speech-replay-concede')
            player = subprocess.Popen(command, cwd=ROOT, **startup(not args.foreground))
            client_processes.append(player)
            summary['clients'].append({'profile': profile, 'pid': player.pid, 'report': str(report_path)})

        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            if all(player.poll() is not None for player in client_processes):
                break
            if server.poll() is not None:
                raise RuntimeError(f'hero speech test server exited with {server.returncode}')
            time.sleep(.5)
        else:
            raise TimeoutError(f'Player replay timed out after {args.timeout}s; artifacts retained in {output}')

        for client in summary['clients']:
            report_path = Path(client['report'])
            if not report_path.is_file():
                client['errors'] = ['Player did not create replay report']
                continue
            report = json.loads(report_path.read_text(encoding='utf-8'))
            text = concede_text(local, report.get('concede_card_id', ''))
            emote = emote_text(local, report.get('hero_card_id', ''), report.get('emote_bubble_id', ''))
            client['errors'] = validate_player_report(report, emotes, text,
                report_path.with_suffix('.listener.wav'), emote, captures, client['profile'])
            if not emote:
                client['errors'].append('captured hero emote subtitle is missing from the local source index')
            client['exit_code'] = next(p.returncode for p in client_processes if p.pid == client['pid'])
            if client['exit_code'] != 0:
                client['errors'].append(f"Player exited with {client['exit_code']}")
        summary['passed'] = all(not client.get('errors') for client in summary['clients'])
        summary_path = output / 'summary.json'
        summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0 if summary['passed'] else 1
    finally:
        for process in client_processes:
            stop_child(process)
        stop_child(server)
        if server is not None:
            try:
                server_log.close()
            except (UnboundLocalError, OSError):
                pass


if __name__ == '__main__':
    raise SystemExit(main())
