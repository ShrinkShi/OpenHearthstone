"""Isolated, bounded dual-Player evidence for the real H11 hero-power route."""
from __future__ import annotations

import argparse
import atexit
import asyncio
import hashlib
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path

from game.database import preset
from game.match import Match
from .importer import PROJECT

HERO_POWER = 'HERO_11bp'
GHOUL = 'HERO_11bpt'
H11_AUTHORED_DURATION = 5.0
H11_TIMELINE_START_AGE = 1 / 6
H11_TOKEN_AUDIO_FRAMES = 192634
H11_TOKEN_AUDIO_SAMPLE_RATE = 48000
H11_TOKEN_AUDIO_DURATION = H11_TOKEN_AUDIO_FRAMES / H11_TOKEN_AUDIO_SAMPLE_RATE
H11_AUDIO_COMPLETE_AGE = H11_TIMELINE_START_AGE + H11_TOKEN_AUDIO_DURATION
SIZE_CHOICES = ('1280x720', '1920x1080')
PRODUCTION_DATA = PROJECT / 'local'
PRODUCTION_INDEX = PRODUCTION_DATA / 'runtime/native-effect-index.json'


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest().upper()


def h11_entry(index_path: Path) -> tuple[dict, dict]:
    value = json.loads(index_path.read_text(encoding='utf-8'))
    entries = value.get('entries')
    matches = [entry for entry in entries or [] if entry.get('card_id') == HERO_POWER]
    if len(matches) != 1:
        raise ValueError(f'{index_path} must contain exactly one {HERO_POWER} entry')
    return value, matches[0]


def player_profile(token: str, role: str) -> str:
    if role not in ('driver', 'observer') or len(token) != 16 or any(char not in '0123456789abcdef' for char in token):
        raise ValueError('H11 Player profile requires a 16-character lowercase hex token and a known role')
    profile = f'f017-h11-{token}-{role}'
    worst_action_id_length = len(profile) + 1 + 10 + 1 + 32
    if worst_action_id_length > 80:
        raise ValueError(f'Player action_id may exceed the authority 80-character limit: {worst_action_id_length}')
    return profile


class TestRuntime:
    """A private full copy; never hardlinks mutable production runtime files."""

    def __init__(self, source_data: Path, work_dir: Path):
        self.source_data = source_data.resolve()
        self.work_dir = work_dir.resolve()
        if self.work_dir != PROJECT.resolve():
            raise ValueError('The isolated data root must be a direct child of the project root')
        self.source_index = self.source_data / 'runtime/native-effect-index.json'
        self.source_sha_before = sha256(self.source_index)
        _, source_entry = h11_entry(self.source_index)
        self.source_enabled = source_entry.get('enabled')
        if type(self.source_enabled) is not bool:
            raise RuntimeError('Production HERO_11bp enabled state must be explicit')

        self._temporary = tempfile.TemporaryDirectory(prefix='f017-h11-runtime-', dir=self.work_dir)
        self.path = Path(self._temporary.name).resolve()
        if self.path.parent != self.work_dir or not self.path.name.startswith('f017-h11-runtime-'):
            self._temporary.cleanup()
            raise RuntimeError('Temporary runtime escaped its owned work directory')
        self._cleaned = False
        atexit.register(self.cleanup)
        try:
            shutil.copytree(self.source_data / 'runtime', self.path / 'runtime')
            for name in ('card-database.json', 'ui-calibration.json'):
                source = self.source_data / name
                if source.is_file():
                    shutil.copy2(source, self.path / name)
            isolated_index = self.path / 'runtime/native-effect-index.json'
            index, entry = h11_entry(isolated_index)
            if entry.get('enabled') != self.source_enabled:
                raise RuntimeError('Copied HERO_11bp index state changed')
            entry['enabled'] = True
            entry['duration'] = 5.0
            entry['replace'] = False
            entry['impact_delay'] = -1.0
            isolated_index.write_text(json.dumps(index, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
            self.index_sha256 = sha256(isolated_index)
            self.verify_production_unchanged()
        except BaseException:
            self.cleanup()
            raise

    def verify_production_unchanged(self) -> str:
        digest = sha256(self.source_index)
        _, entry = h11_entry(self.source_index)
        if digest != self.source_sha_before or entry.get('enabled') != self.source_enabled:
            raise RuntimeError('Production H11 index hash/state changed during isolated run')
        return digest

    def cleanup(self) -> None:
        if self._cleaned:
            return
        resolved = self.path.resolve()
        if resolved.parent != self.work_dir or not resolved.name.startswith('f017-h11-runtime-'):
            raise RuntimeError('Refusing to remove a runtime outside its owned unique directory')
        self._temporary.cleanup()
        self._cleaned = True


def apply(match: Match, seat: int, move: dict, identity: str) -> None:
    match.apply(seat, dict(move, version=match.version, match_id=match.id, action_id=identity))


def prepare_death_knight(match: Match) -> Match:
    if [player.hero.power.id for player in match.game.players] != [HERO_POWER, 'HERO_08bp']:
        raise ValueError('H11 fixture requires Death Knight seat 0 and Mage seat 1')
    for seat in range(2):
        apply(match, seat, {'kind': 'mulligan', 'cards': []}, f'f017-h11-mulligan-{seat}')
    step = 0
    while match.game.current_player is not match.game.players[0]:
        active = match.game.players.index(match.game.current_player)
        apply(match, active, {'kind': 'end_turn'}, f'f017-h11-handoff-{step}')
        step += 1
        if step > 8:
            raise RuntimeError('H11 fixture failed to place Death Knight on its turn')
    knight = match.game.players[0]
    knight.max_mana = 10
    knight.used_mana = 0
    match.observer.events.clear()
    ready = match.snapshot(0)
    if ready['phase'] != 'play' or ready['seat'] != 0 or not any(
            move.get('kind') == 'power' and move.get('entity') == ready['players'][0]['power']['entity']
            for move in ready['moves']):
        raise AssertionError('H11 fixture did not expose the real legal hero-power move')
    if any(card['card_id'] == GHOUL for card in ready['players'][0]['field']):
        raise AssertionError('H11 fixture unexpectedly started with a Ghoul')
    return match


async def serve(port: int, folder: Path) -> None:
    from game import server as authority

    class EvidenceMatch(Match):
        def __init__(self, names, classes, decks):
            if classes != [1, 4]:
                raise ValueError('H11 evidence requires Death Knight seat 0 and Mage seat 1')
            super().__init__(names, classes, decks, 20261021)
            prepare_death_knight(self)

    authority.Match = EvidenceMatch
    original_send = authority.Peer.send

    async def observed_send(peer, payload):
        await original_send(peer, payload)
        if payload.get('type') == 'queued':
            queued_path = folder / 'queued.json'
            existing = json.loads(queued_path.read_text(encoding='utf-8')) if queued_path.exists() else {'names': []}
            if peer.name not in existing['names']:
                existing['names'].append(peer.name)
            queued_path.write_text(json.dumps(existing), encoding='utf-8')

    authority.Peer.send = observed_send
    folder.mkdir(parents=True, exist_ok=True)
    server = authority.Server(folder / 'matches')
    listener = await asyncio.start_server(server.handle, '127.0.0.1', port)
    (folder / 'authority-ready.json').write_text(json.dumps({'port': port}), encoding='utf-8')
    async with listener:
        await listener.serve_forever()


def _wait_for(path: Path, process: subprocess.Popen, timeout: float, label: str) -> None:
    deadline = time.monotonic() + timeout
    while not path.exists():
        if process.poll() is not None:
            raise RuntimeError(f'{label} exited before creating {path.name}: {process.returncode}')
        if time.monotonic() >= deadline:
            raise TimeoutError(f'{label} did not create {path.name} within {timeout:g}s')
        time.sleep(.1)


def _fallback_report(folder: Path, role: str, exit_code: int | None) -> dict:
    path = folder / role / 'report.json'
    if path.is_file():
        return json.loads(path.read_text(encoding='utf-8'))
    return {'result': f'FAIL: Player exited before writing report (exit={exit_code})', 'role': role}


def run_players(port: int, folder: Path, size: str) -> None:
    from .background_player import startup

    if size not in SIZE_CHOICES:
        raise ValueError(f'Unsupported size {size}; choose {", ".join(SIZE_CHOICES)}')
    folder = folder.resolve()
    folder.mkdir(parents=True, exist_ok=False)
    work_dir = PROJECT
    overlay = None
    processes: list[tuple[str, subprocess.Popen]] = []
    streams = []
    result = {'size': size, 'processes': [], 'result': 'FAIL: run did not complete'}
    production_before = sha256(PRODUCTION_INDEX)
    try:
        _, production_entry = h11_entry(PRODUCTION_INDEX)
        overlay = TestRuntime(PRODUCTION_DATA, work_dir)
        overlay_record = {'dataRoot': str(overlay.path), 'indexSha256': overlay.index_sha256,
                          'productionIndexSha256Before': overlay.source_sha_before,
                          'productionH11EnabledBefore': overlay.source_enabled, 'testH11Enabled': True,
                          'isFullCopy': True, 'cleanupOwnedPath': str(overlay.path)}
        (folder / 'runtime-overlay.json').write_text(json.dumps(overlay_record, indent=2), encoding='utf-8')

        executable = PROJECT / 'build/client/HearthstoneClone.exe'
        if not executable.is_file():
            raise FileNotFoundError(f'Windows Player executable not found: {executable}')
        server_log = (folder / 'server.log').open('w', encoding='utf-8')
        streams.append(server_log)
        server_cmd = [sys.executable, '-X', 'utf8', '-m', 'tools.death_knight_player_evidence',
                      '--serve', '--port', str(port), '--output', str(folder)]
        server = subprocess.Popen(server_cmd, cwd=PROJECT, stdout=server_log, stderr=subprocess.STDOUT,
                                  creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        processes.append(('server', server))
        result['processes'].append({'role': 'server', 'pid': server.pid})
        _wait_for(folder / 'authority-ready.json', server, 30, 'Evidence authority')

        token = uuid.uuid4().hex[:16]
        dll = executable.parent / 'HearthstoneClone_Data/Managed/Assembly-CSharp.dll'
        result['playerDllSha256'] = sha256(dll)
        width, height = size.split('x')
        for role, name, class_id in (('driver', 'DeathKnightA', '1'), ('observer', 'MageB', '4')):
            evidence = folder / role
            evidence.mkdir()
            command = [str(executable), '--profile', player_profile(token, role), '--name', name,
                       '--class', class_id, '--auto-match', '--host', '127.0.0.1', '--port', str(port),
                       '--data', str(overlay.path), '--h11-player-replay', role,
                       '--h11-evidence-dir', str(evidence), '-screen-width', width,
                       '-screen-height', height, '-screen-fullscreen', '0',
                       '-logFile', str(evidence / 'player.log')]
            player = subprocess.Popen(command, cwd=PROJECT, **startup(True))
            processes.append((role, player))
            result['processes'].append({'role': role, 'pid': player.pid,
                                        'dataRoot': str(overlay.path), 'dataIndexSha256': overlay.index_sha256})
            if role == 'driver':
                _wait_for(folder / 'queued.json', player, 45, 'Death Knight driver')

        deadline = time.monotonic() + 150
        while any(process.poll() is None for role, process in processes if role != 'server'):
            if time.monotonic() >= deadline:
                raise TimeoutError('Bounded dual Player H11 replay exceeded 150s')
            time.sleep(.25)
        reports = [_fallback_report(folder, role, process.returncode)
                   for role, process in processes if role in ('driver', 'observer')]
        result['reports'] = reports
        if any(process.returncode != 0 for role, process in processes if role != 'server'):
            raise RuntimeError('A Player exited nonzero; preserve reports and logs')
        if any(not report.get('result', '').startswith('PASS') for report in reports):
            raise RuntimeError('At least one Player report failed')
        result['result'] = f'PASS bounded H11 dual Player {size}'
    except BaseException as error:
        result['result'] = f'FAIL: {error}'
        raise
    finally:
        shutdown_errors = []
        for role, process in reversed(processes):
            try:
                if process.poll() is None:
                    process.terminate()
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                try:
                    process.kill()
                    process.wait(timeout=15)
                except BaseException as error:
                    shutdown_errors.append(f'{role} process did not stop: {error}')
            except BaseException as error:
                shutdown_errors.append(f'{role} process cleanup failed: {error}')
                try:
                    if process.poll() is None:
                        process.kill()
                    process.wait(timeout=15)
                except BaseException as kill_error:
                    shutdown_errors.append(f'{role} forced stop failed: {kill_error}')
            record = next((item for item in result['processes'] if item['role'] == role), None)
            if record is not None:
                record['exit'] = process.returncode
        for stream in streams:
            try:
                stream.close()
            except BaseException as error:
                shutdown_errors.append(f'log stream close failed: {error}')
        try:
            reports = [_fallback_report(folder, role, next((p.returncode for r, p in processes if r == role), None))
                       for role in ('driver', 'observer')]
            (folder / 'reports.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding='utf-8')
        except BaseException as error:
            shutdown_errors.append(f'report collection failed: {error}')
        overlay_error = None
        if shutdown_errors:
            overlay_error = RuntimeError('; '.join(shutdown_errors))
        if overlay is not None:
            try:
                production_after = overlay.verify_production_unchanged()
                isolated_after = sha256(overlay.path / 'runtime/native-effect-index.json')
                if isolated_after != overlay.index_sha256:
                    raise RuntimeError('Player changed its isolated H11 runtime index')
                overlay_record.update({'productionIndexSha256After': production_after,
                                       'productionH11EnabledAfter': overlay.source_enabled,
                                       'isolatedIndexSha256After': isolated_after})
            except BaseException as error:
                if overlay_error is None:
                    overlay_error = error
            finally:
                try:
                    overlay.cleanup()
                except BaseException as error:
                    if overlay_error is None:
                        overlay_error = error
                overlay_record['cleanupConfirmed'] = not overlay.path.exists()
                (folder / 'runtime-overlay.json').write_text(json.dumps(overlay_record, indent=2), encoding='utf-8')
        result['productionIndexSha256Before'] = production_before
        try:
            result['productionIndexSha256After'] = sha256(PRODUCTION_INDEX)
            result['productionH11EnabledAfter'] = h11_entry(PRODUCTION_INDEX)[1].get('enabled') is True
            if result['productionIndexSha256Before'] != result['productionIndexSha256After']:
                raise RuntimeError('Production H11 index changed')
        except BaseException as error:
            if overlay_error is None:
                overlay_error = error
        if overlay_error is not None:
            result['result'] = f'FAIL: isolated runtime verification/cleanup failed: {overlay_error}'
        (folder / 'run.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        if overlay_error is not None:
            raise RuntimeError(result['result']) from overlay_error


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def _validate_h11_followup_timing(report: dict, unlocked: dict) -> None:
    accepted_wall = report.get('castAcceptedWallTime', -1)
    submitted_wall = unlocked.get('submitWallTime', -1)
    previous_update_wall = unlocked.get('previousUpdateWallBoundary', -1)
    submission_update_wall = unlocked.get('submissionUpdateWallBoundary', -1)
    frame_allowance = unlocked.get('inputFrameAllowance', -1)
    expected_latency = submitted_wall - accepted_wall
    update_frame_interval = submission_update_wall - previous_update_wall
    _require(report.get('castAcceptedFrame', 0) > 0 and unlocked.get('submitFrame', 0) > 0 and
             unlocked.get('submitFrame') == unlocked.get('sampleFrame') and
             unlocked.get('submitFrame') >= report.get('castAcceptedFrame') and
             math.isfinite(accepted_wall) and math.isfinite(submitted_wall) and submitted_wall >= accepted_wall and
             math.isfinite(previous_update_wall) and math.isfinite(submission_update_wall) and
             previous_update_wall < submission_update_wall <= submitted_wall and update_frame_interval > 0 and
             abs(frame_allowance - update_frame_interval) <= .002 and
             abs(unlocked.get('castToSubmitSeconds', -1) - expected_latency) <= .002,
             'driver monotonic Update/frame timing evidence is incomplete or inconsistent')
    _require(0 <= unlocked.get('castToSubmitSeconds', -1) <= .2 + frame_allowance + .005,
             'driver submission exceeded the 0.2s input gate plus its measured monotonic Update frame')


def h11_playback_age(source_age: float, audio_grace_seconds: float = 0.0,
                     token_audio_active: bool = False) -> float:
    if not math.isfinite(source_age) or source_age < 0:
        raise ValueError('H11 source age must be finite and nonnegative')
    if not math.isfinite(audio_grace_seconds) or audio_grace_seconds < 0:
        raise ValueError('H11 audio grace must be finite and nonnegative')
    if token_audio_active and source_age >= H11_AUDIO_COMPLETE_AGE + audio_grace_seconds:
        return min(source_age, H11_AUTHORED_DURATION - .001)
    return source_age if source_age < H11_AUDIO_COMPLETE_AGE + audio_grace_seconds else H11_AUTHORED_DURATION


def analyze_playback_clock(source: dict, dsp: dict) -> dict:
    clock = source.get('playbackClock') or {}
    rate = dsp.get('rate', 0)
    buffer_size = dsp.get('dspBuffer', 0)
    buffers = dsp.get('dspBuffers', 0)
    _require(type(rate) is int and rate > 0 and type(buffer_size) is int and buffer_size > 0 and
             type(buffers) is int and buffers > 0,
             'H11 Player report lacks its actual Unity DSP queue configuration')
    measured_grace = buffer_size * buffers / rate
    audio_grace = clock.get('audioGraceSeconds', -1)
    identity_through = H11_AUDIO_COMPLETE_AGE + measured_grace
    _require(abs(clock.get('timelineStartAge', -1) - H11_TIMELINE_START_AGE) < 1e-5 and
             abs(clock.get('tokenAudioDuration', -1) - H11_TOKEN_AUDIO_DURATION) < 1e-5 and
             abs(audio_grace - measured_grace) < 1e-5 and
             abs(clock.get('identityThroughAge', -1) - identity_through) < 1e-5 and
             abs(clock.get('authoredDuration', -1) - H11_AUTHORED_DURATION) < 1e-5,
             'H11 Player clock configuration differs from the source WAV or measured Unity DSP queue timing')
    samples = source.get('playbackClockSamples') or []
    _require(len(samples) > 1, 'H11 Player report lacks actual source clock samples')
    previous_wall_age = previous_source_age = previous_playback_age = -1.0
    for sample in samples:
        wall_age = sample.get('wallAge', -1)
        source_age = sample.get('sourceAge', -1)
        playback_age = sample.get('playbackAge', -1)
        token_audio_active = sample.get('tokenAudioActiveAtMap')
        _require(math.isfinite(wall_age) and math.isfinite(source_age) and math.isfinite(playback_age) and
                 type(token_audio_active) is bool and
                 abs(playback_age - h11_playback_age(source_age, measured_grace, token_audio_active)) < 1e-4,
                 'H11 Prepare/Draw used a different actual-token-aware playback age')
        _require(wall_age >= previous_wall_age and source_age >= previous_source_age and playback_age >= previous_playback_age,
                 'H11 Player wall/source playback clock regressed')
        previous_wall_age, previous_source_age, previous_playback_age = wall_age, source_age, playback_age
    _require(any(sample['sourceAge'] < identity_through and
                 abs(sample['playbackAge'] - sample['sourceAge']) < 1e-4 for sample in samples) and
             any(sample['sourceAge'] >= identity_through and sample['tokenAudioActiveAtMap'] is True and
                 sample['playbackAge'] < H11_AUTHORED_DURATION for sample in samples),
             'H11 report did not hold the authored tail while the actual token source remained active')
    allowance = source.get('releaseFrameAllowance', 0)
    duration = source.get('duration', 0)
    callback_time = source.get('tokenCueCallbackTime', -1)
    cue_pitch = source.get('tokenCuePitch', -1)
    first_active_time = source.get('tokenPlaybackFirstActiveTime', -1)
    end_time = source.get('tokenPlaybackEndTime', -1)
    end_allowance = source.get('tokenPlaybackEndFrameAllowance', -1)
    callback_to_end = source.get('tokenCallbackToEndSeconds', -1)
    release_time = source.get('releaseTime', -1)
    _require(source.get('tokenCueSeen') is True and source.get('tokenPlaybackObserved') is True and
             source.get('tokenPlaybackEnded') is True and source.get('tokenAudioActiveBeyondIdentity') is True,
             'H11 report lacks evidence that the actual token cue stayed active beyond the nominal completion age')
    _require(math.isfinite(callback_time) and math.isfinite(cue_pitch) and cue_pitch > 0 and
             math.isfinite(first_active_time) and math.isfinite(end_time) and
             math.isfinite(end_allowance) and math.isfinite(callback_to_end) and math.isfinite(release_time) and
             callback_time <= first_active_time <= end_time and
             abs(callback_to_end - (end_time - callback_time)) < .002 and
             end_allowance > 0 and callback_to_end + .001 >= H11_TOKEN_AUDIO_DURATION / cue_pitch and
             release_time + end_allowance >= end_time and math.isfinite(allowance) and allowance > 0 and
             math.isfinite(duration) and duration >= 0,
             'H11 source release preceded the measured end of the real token WAV playback at the cue pitch')
    _require(release_time - end_time <= end_allowance + allowance,
             'H11 source remained alive beyond its measured audio-end and release observation frames')
    first_source_age = samples[0]['sourceAge']
    return {'samples': len(samples), 'firstSourceAge': first_source_age,
            'lastSourceAge': previous_source_age, 'releaseFrameAllowance': allowance,
            'audioGraceSeconds': measured_grace, 'identityThroughAge': identity_through,
            'tokenCallbackToEndSeconds': callback_to_end, 'tokenPlaybackEndFrameAllowance': end_allowance,
            'tokenCuePitch': cue_pitch, 'audioEndToReleaseSeconds': release_time - end_time,
            'observedDuration': duration}


def analyze_player(folder: Path, expected_size: str, expected_data_root: str, expected_index_hash: str) -> dict:
    report = json.loads((folder / 'report.json').read_text(encoding='utf-8'))
    _require(report.get('result', '').startswith('PASS'), f"{folder.name} report is not PASS")
    _require(report.get('role') == folder.name, f"{folder.name} role mismatch")
    _require(f"{report.get('screenWidth')}x{report.get('screenHeight')}" == expected_size,
             f'{folder.name} ran at an unexpected resolution')
    _require(Path(report.get('dataRoot', '')).resolve() == Path(expected_data_root).resolve(),
             f'{folder.name} did not use the exact isolated --data root')
    _require(report.get('dataIndexSha256') == expected_index_hash,
             f'{folder.name} used a different runtime index')

    cast = report.get('cast') or {}
    ended = report.get('afterEndTurn') or {}
    before = report.get('before') or {}
    _require(before.get('heroPower') == HERO_POWER, f'{folder.name} did not start with HERO_11bp')
    _require(cast.get('powerCardId') == HERO_POWER, f'{folder.name} cast was not HERO_11bp')
    _require(cast.get('powerUsed') is True and cast.get('powerManaSpent') == 2,
             f'{folder.name} cast snapshot lacks the real used/2-mana update')
    _require(cast.get('playHeroPowerCount') == 1 and cast.get('summonCount') == 1,
             f'{folder.name} cast must contain exactly one PlayHeroPower and Summon')
    _require(cast.get('playHeroPowerTarget') == cast.get('powerEntity') == cast.get('summonSource'),
             f'{folder.name} PlayHeroPower.target != Summon.source')
    _require(cast.get('ghoulEntity') == cast.get('summonTarget') and cast.get('ghoulCardId') == GHOUL,
             f'{folder.name} summon did not resolve to the authoritative HERO_11bpt')
    _require(cast.get('ghoulAttack') == 1 and cast.get('ghoulHealth') == 1 and cast.get('ghoulCharge'),
             f'{folder.name} authoritative Ghoul stats/Charge are incorrect')
    _require(cast.get('actorEntity') == cast.get('ghoulEntity') and cast.get('actorCardId') == GHOUL and
             cast.get('actorPresent') is True and cast.get('actorBound') is True,
             f'{folder.name} did not bind a real Ghoul Actor to the native source')
    accepted_display_hash = cast.get('displayHash')
    _require(cast.get('publicHash') == cast.get('peerPublicHash') and isinstance(accepted_display_hash, str) and
             cast.get('displayMatchedAtAcceptance') is (accepted_display_hash == cast.get('publicHash')),
             f'{folder.name} accepted cast authority/peer or actual-display snapshot differs')
    settled_cast_hash = report.get('castDisplaySettleHash') or ''
    if settled_cast_hash:
        _require(settled_cast_hash == cast.get('publicHash') and
                 report.get('castDisplaySettleFrame', 0) >= report.get('castAcceptedFrame', 0),
                 f'{folder.name} observed an incorrect cast display-settle snapshot')

    source = report.get('source') or {}
    _require(source.get('sourceSeen') is True and source.get('released') is True,
             f'{folder.name} did not observe source activation and release')
    _require(source.get('customSpawnInitializations', 0) == 1 and source.get('sideEventDispatches', 0) == 1,
             f'{folder.name} source did not initialize/dispatch exactly once')
    _require(source.get('timelineStarts', 0) == 1 and source.get('timelineSoundStarts', 0) == 1,
             f'{folder.name} source Timeline/token sound counts differ')
    _require(source.get('genericSummonActiveCount') == 0 and source.get('unboundActions', 0) == 0,
             f'{folder.name} H11 route fell back to generic SUMMON_IN or had unbound actions')
    dsp = report.get('dsp') or {}
    playback_clock = analyze_playback_clock(source, dsp)
    unlocked = report.get('inputUnlockedDuringSource') or {}
    sample_hash = unlocked.get('displayHashAtSample')
    _require(isinstance(sample_hash, str) and
             unlocked.get('displayHashMatched') is (sample_hash == cast.get('publicHash')),
             f'{folder.name} sample-time display hash observation is inconsistent with the cast authority hash')
    _require(unlocked.get('sampled') is True and unlocked.get('sourceReleased') is False and
             unlocked.get('inputLocked') is False and unlocked.get('pending') is False and
             unlocked.get('sourceActiveAtSample') is True and unlocked.get('unlockedDuringSource') is True and
             unlocked.get('busy') is True,
             f'{folder.name} input was not observably unlocked while the H11 source was active')
    _require((folder.name == 'driver' and unlocked.get('legalEndTurn') is True) or
             (folder.name == 'observer' and unlocked.get('legalEndTurn') is False),
             f'{folder.name} legal end-turn availability does not match its role')
    if folder.name == 'driver':
        _validate_h11_followup_timing(report, unlocked)
        submit_hash = unlocked.get('displayHashAtSubmit')
        _require(isinstance(submit_hash, str) and
                 unlocked.get('displayHashMatchedAtSubmit') is (submit_hash == cast.get('publicHash')),
                 'driver submit-time display hash observation is inconsistent with the cast authority hash')
        _require(unlocked.get('seat') == 0 and unlocked.get('active') == 0 and
                 unlocked.get('sourceActiveAtSubmit') is True and
                 unlocked.get('presentationBusyAtSubmit') is True and unlocked.get('inputLockedAtSubmit') is False,
                 'driver did not submit End Turn while the H11 source remained active and the input gate was open')
        _require(unlocked.get('actionCounterAtSubmit') == report.get('initialActionCounter', -2) + 1 and
                 unlocked.get('actionCounterAfterSend') == report.get('initialActionCounter', -2) + 2 and
                 unlocked.get('submitFrame') == unlocked.get('sampleFrame'),
                 'driver action counter or same-frame input-gate submission evidence is incomplete')
        _require(unlocked.get('submitFrame', -1) >= unlocked.get('castAcceptedFrame', 0),
                 'driver follow-up action predates the accepted hero-power snapshot')
        _require(unlocked.get('castToSubmitSeconds', 1e9) <= .2 + unlocked.get('inputFrameAllowance', 0) + .005,
                 'driver submission exceeded the 0.2s input gate plus the measured next-frame allowance')
        _require(unlocked.get('submitVersion', 0) < unlocked.get('responseVersion', 0) and
                 unlocked.get('authorityResponseMatched') is True and unlocked.get('responseActive') == 1 and
                 unlocked.get('authorityResponseWhileSourceActive') is True and
                 unlocked.get('responseHash') == ended.get('publicHash'),
                 'driver End Turn did not receive the expected authority response while the H11 source was active')
        _require(unlocked.get('actionCounterAfterSend') == unlocked.get('actionCounterAtSubmit', -2) + 1 and
                 unlocked.get('sourceTailClearedAfterResponse') is True and
                 unlocked.get('sourceTailCountAfterResponse', -1) == 0,
                 'driver action submission or old visual-tail cleanup evidence is incomplete')
    else:
        _require(unlocked.get('seat') == 1 and unlocked.get('active') == 0,
                 'observer did not remain inactive when input unlocked during source playback')
    _require(unlocked.get('sampleTime', -1) < source.get('releaseTime', -1),
             f'{folder.name} input unlock was sampled after source release')
    projections = source.get('projections') or []
    _require(len(projections) == 5, f'{folder.name} lacks the five scheduled H11 source/Actor projection samples')
    for sample in projections:
        _require(sample.get('sourceLayer') == 30 and not sample.get('boardCameraMask', 0) & (1 << 30) and
                 sample.get('nativeCameraMask', 0) & (1 << 30) and
                 sample.get('viewerSeat') == (0 if folder.name == 'driver' else 1) and sample.get('ownerSeat') == 0 and
                 sample.get('cameraName') == 'PresentationCamera',
                 f'{folder.name} H11 source layer is not isolated from the board camera and retained by the native camera')
        _require(abs(sample.get('playbackAge', -1) - h11_playback_age(
            sample.get('sourceAge', -1), playback_clock['audioGraceSeconds'])) < 1e-4,
                 f'{folder.name} H11 projected source age differs from the shared playback clock')
        _require(len(sample.get('nodeWorld') or []) == 3 and len(sample.get('nodeScreen') or []) == 3 and
                 len(sample.get('cameraPosition') or []) == 3,
                 f'{folder.name} H11 source world/screen projection is incomplete')
    first_projection = projections[0]
    _require(first_projection.get('actorPresent') is True and first_projection.get('actorBound') is True and
             first_projection.get('hasPowerRect') is True and first_projection.get('hasGhoulRect') is True and
             len(first_projection.get('actorWorld') or []) == 3 and len(first_projection.get('actorScreen') or []) == 3,
             f'{folder.name} early H11 sample lacks the actual Actor and GUI button/summon target projections')

    audio = report.get('audio') or {}
    audio_files = {(row.get('kind'), row.get('file')) for row in audio.get('records') or []}
    _require(any(kind == 'native-effect-button' and file == audio.get('buttonFile') for kind, file in audio_files),
             f'{folder.name} native source Button sound was not observed')
    _require(any(kind == 'native-effect-token' and file == audio.get('tokenFile') for kind, file in audio_files),
             f'{folder.name} Timeline token sound was not observed')
    _require(audio.get('buttonFile') != audio.get('tokenFile'), f'{folder.name} Button and Token sounds collapsed')
    _require(dsp.get('listenerCount') == 1 and dsp.get('channels') == 2 and dsp.get('rate', 0) > 0 and
             dsp.get('sampleFrames', 0) > 0 and dsp.get('callbacks', 0) > 0 and dsp.get('overflows') == 0 and
             dsp.get('peak', 0) > 1e-5, f'{folder.name} has no valid actual Unity Listener PCM capture')

    death_events = ended.get('deathEvents') or []
    _require(any(event.get('type') == 'Death' and event.get('target') == cast.get('ghoulEntity')
                 for event in death_events), f'{folder.name} end-turn snapshot lacks the real Ghoul Death event')
    _require(ended.get('ghoulPresent') is False, f'{folder.name} Ghoul remained after its owning turn ended')
    _require(ended.get('publicHash') == ended.get('peerPublicHash') == ended.get('displayHash'),
             f'{folder.name} post-death authority/peer/display hashes differ')

    images = report.get('images') or []
    expected_images = {'ready.png', 'cast.png', 'source-active.png', 'input-unlocked-during-source.png',
                       'source-released.png', 'after-end-turn.png'}
    _require(set(images) >= expected_images, f'{folder.name} report lacks required phase screenshots')
    for name in expected_images:
        image_path = folder / name
        _require(image_path.is_file(), f'{folder.name} screenshot is missing: {name}')
        with image_path.open('rb') as stream:
            header = stream.read(24)
        _require(len(header) == 24 and header[:8] == b'\x89PNG\r\n\x1a\n' and
                 (int.from_bytes(header[16:20], 'big'), int.from_bytes(header[20:24], 'big')) ==
                 tuple(map(int, expected_size.split('x'))), f'{folder.name}/{name} PNG dimensions mismatch')
    return {'role': folder.name, 'castHash': cast['publicHash'], 'afterEndTurnHash': ended['publicHash'],
            'ghoulEntity': cast['ghoulEntity'], 'sourceDuration': source.get('duration'),
            'sourcePlaybackClock': playback_clock, 'sourceActionCounts': source,
            'audio': audio, 'dsp': dsp, 'screenshots': sorted(expected_images)}


def analyze(folder: Path, expected_size: str) -> dict:
    folder = folder.resolve()
    run = json.loads((folder / 'run.json').read_text(encoding='utf-8'))
    overlay = json.loads((folder / 'runtime-overlay.json').read_text(encoding='utf-8'))
    _require(run.get('result', '').startswith('PASS'), 'dual Player run did not complete successfully')
    _require(run.get('size') == expected_size, 'run resolution does not match --size')
    source_enabled = overlay.get('productionH11EnabledBefore')
    _require(type(source_enabled) is bool and run.get('productionH11EnabledAfter') == source_enabled,
             'run production native-effect index enabled state changed')
    _require(overlay.get('isFullCopy') is True and overlay.get('testH11Enabled') is True and
             overlay.get('productionH11EnabledAfter') == source_enabled,
             'run did not use an isolated H11-only runtime index')
    _require(overlay.get('productionIndexSha256Before') == overlay.get('productionIndexSha256After') ==
             run.get('productionIndexSha256Before') == run.get('productionIndexSha256After'),
             'production native-effect index hash changed during test')
    _require(overlay.get('cleanupConfirmed') is True, 'private runtime was not confirmed cleaned')
    data_root, index_hash = overlay['dataRoot'], overlay['indexSha256']
    process_rows = {row.get('role'): row for row in run.get('processes') or []}
    for role in ('driver', 'observer'):
        _require(role in process_rows and process_rows[role].get('dataRoot') == data_root and
                 process_rows[role].get('dataIndexSha256') == index_hash,
                 f'{role} process did not record the shared isolated --data root/index hash')
    players = [analyze_player(folder / role, expected_size, data_root, index_hash)
               for role in ('driver', 'observer')]
    _require(players[0]['castHash'] == players[1]['castHash'], 'dual Player cast authority hashes differ')
    _require(players[0]['afterEndTurnHash'] == players[1]['afterEndTurnHash'],
             'dual Player post-death authority hashes differ')
    summary = {'result': f'PASS H11 dual Player {expected_size}', 'players': players,
               'productionIndexSha256': run['productionIndexSha256After'],
               'isolatedIndexSha256': index_hash, 'sharedDataRoot': data_root}
    (folder / 'analysis.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    return summary


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument('--serve', action='store_true')
    modes.add_argument('--run-players', action='store_true')
    modes.add_argument('--analyze', action='store_true')
    parser.add_argument('--size', choices=SIZE_CHOICES, default='1920x1080')
    parser.add_argument('--port', type=int, default=37261)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.serve:
        asyncio.run(serve(args.port, args.output.resolve()))
    elif args.run_players:
        run_players(args.port, args.output, args.size)
    else:
        print(json.dumps(analyze(args.output, args.size), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
