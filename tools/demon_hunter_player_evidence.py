"""Isolated dual-Player evidence for the authoritative HERO_10bp self-hero Buff route."""
from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import math
import os
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path

from game.database import preset
from game.match import Match
from .importer import PROJECT
from .import_hero_power_super_spell import DEMON_HUNTER_RUNTIME_PROFILE

POWER = 'HERO_10bp'
MAGE_POWER = 'HERO_08bp'
ROOT_REFERENCE = 'ReuseFX_BuffImpact_DemonHunter_Small_Super.prefab:7d6b21e193de5cc44a280f817f088120'
SIZE_CHOICES = ('1280x720', '1920x1080')
PRODUCTION_DATA = PROJECT / 'local'
PRODUCTION_INDEX = PRODUCTION_DATA / 'runtime/native-effect-index.json'
EXPECTED_IMAGES = {'ready.png', 'power-button.png', 'cast.png', 'self-hero-buff.png',
                   'source-active.png', 'input-unlocked-during-source.png',
                   'source-released.png', 'after-end-turn.png'}
EXPECTED_DRIVER_PHASE_IMAGES = {'end-turn-submitted-during-source.png'}
EXPECTED_DRIVER_INPUT_IMAGES = {'power-hover.png', 'power-pressed.png', 'power-clicked.png'}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest().upper()


def h10_entry(path: Path) -> tuple[dict, dict]:
    index = json.loads(path.read_text(encoding='utf-8'))
    matches = [row for row in index.get('entries', []) if row.get('card_id') == POWER]
    if len(matches) != 1:
        raise ValueError(f'{path} must contain exactly one {POWER} row')
    return index, matches[0]


class TestRuntime:
    """Full isolated runtime copy with only the exact H10 index row enabled."""
    def __init__(self, source_data: Path):
        self.source_data = source_data.resolve()
        self.source_index = self.source_data / 'runtime/native-effect-index.json'
        self.source_sha_before = sha256(self.source_index)
        _, original = h10_entry(self.source_index)
        self.source_enabled = original.get('enabled')
        if type(self.source_enabled) is not bool:
            raise RuntimeError('Production H10 enabled state must be an explicit boolean')
        catalog_path = self.source_data / 'runtime/native-super-spell.json'
        catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
        configs = [row for row in catalog.get('programs', []) if row.get('card_id') == POWER]
        if len(configs) != 1 or configs[0].get('source_root_profile') != DEMON_HUNTER_RUNTIME_PROFILE:
            raise RuntimeError('H10 native source profile is not the exact reviewed runtime profile')
        self._temporary = tempfile.TemporaryDirectory(prefix='f020-h10-runtime-', dir=PROJECT)
        self.path = Path(self._temporary.name).resolve()
        self._cleaned = False
        try:
            shutil.copytree(self.source_data / 'runtime', self.path / 'runtime')
            for name in ('card-database.json', 'ui-calibration.json'):
                source = self.source_data / name
                if source.is_file():
                    shutil.copy2(source, self.path / name)
            isolated_index = self.path / 'runtime/native-effect-index.json'
            index, entry = h10_entry(isolated_index)
            if entry.get('enabled') != self.source_enabled:
                raise RuntimeError('Copied production H10 enabled state differs')
            entry.update(enabled=True, duration=.1, replace=False)
            isolated_index.write_text(json.dumps(index, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
            self.index_sha256 = sha256(isolated_index)
            self.verify_production_unchanged()
        except BaseException:
            self.cleanup()
            raise

    def verify_production_unchanged(self) -> str:
        digest = sha256(self.source_index)
        _, entry = h10_entry(self.source_index)
        if digest != self.source_sha_before or entry.get('enabled') != self.source_enabled:
            raise RuntimeError('Production native-effect index changed during the isolated H10 run')
        return digest

    def cleanup(self) -> None:
        if self._cleaned:
            return
        if self.path.parent != PROJECT.resolve() or not self.path.name.startswith('f020-h10-runtime-'):
            raise RuntimeError('Refusing to remove an H10 runtime outside its unique owned directory')
        self._temporary.cleanup()
        self._cleaned = True


def apply(match: Match, seat: int, move: dict, identity: str) -> None:
    match.apply(seat, dict(move, version=match.version, match_id=match.id, action_id=identity))


def prepare_demon_hunter(match: Match) -> Match:
    if [player.hero.power.id for player in match.game.players] != [POWER, MAGE_POWER]:
        raise ValueError('H10 fixture requires Demon Hunter seat 0 and Mage seat 1')
    for seat in range(2):
        apply(match, seat, {'kind': 'mulligan', 'cards': []}, f'f020-h10-mulligan-{seat}')
    dh = match.game.players[0]
    steps = 0
    while match.game.current_player is not dh:
        seat = match.game.players.index(match.game.current_player)
        apply(match, seat, {'kind': 'end_turn'}, f'f020-h10-handoff-{steps}')
        steps += 1
        if steps > 8:
            raise RuntimeError('H10 fixture failed to put Demon Hunter on turn')
    dh.max_mana = 10
    dh.used_mana = 0
    match.observer.events.clear()
    ready = match.snapshot(0)
    if ready['phase'] != 'play' or ready['seat'] != 0 or ready['players'][0]['hero']['attack'] != 0 or \
       ready['players'][0]['mana'] != 10 or not any(
            row.get('kind') == 'power' and row.get('entity') == ready['players'][0]['power']['entity'] and row.get('target') == 0
            for row in ready['moves']):
        raise AssertionError('H10 fixture did not expose the legal untargeted power on the 0-attack baseline')
    return match


async def serve(port: int, folder: Path) -> None:
    from game import server as authority

    class EvidenceMatch(Match):
        def __init__(self, names, classes, decks):
            if classes != [14, 4]:
                raise ValueError('H10 evidence requires Demon Hunter seat 0 and Mage seat 1')
            super().__init__(names, classes, decks, 20261023)
            prepare_demon_hunter(self)

    original_send = authority.Peer.send

    async def observed_send(peer, payload):
        await original_send(peer, payload)
        if payload.get('type') == 'queued':
            path = folder / 'queued.json'
            existing = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {'names': []}
            if peer.name not in existing['names']:
                existing['names'].append(peer.name)
            path.write_text(json.dumps(existing), encoding='utf-8')

    authority.Match = EvidenceMatch
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


def _report(folder: Path, role: str, code: int | None) -> dict:
    path = folder / role / 'report.json'
    return json.loads(path.read_text(encoding='utf-8')) if path.is_file() else {
        'result': f'FAIL: Player exited without report (exit={code})', 'role': role}


def run_players(port: int, folder: Path, size: str, timing_only: bool = False) -> None:
    from .background_player import startup

    if size not in SIZE_CHOICES:
        raise ValueError(f'Unsupported size {size}; choose {", ".join(SIZE_CHOICES)}')
    folder = folder.resolve()
    folder.mkdir(parents=True, exist_ok=False)
    overlay = None
    processes: list[tuple[str, subprocess.Popen]] = []
    streams = []
    result = {'size': size, 'timingOnly': timing_only, 'processes': [], 'result': 'FAIL: run did not complete'}
    production_before = sha256(PRODUCTION_INDEX)
    try:
        overlay = TestRuntime(PRODUCTION_DATA)
        overlay_record = {'dataRoot': str(overlay.path), 'indexSha256': overlay.index_sha256,
                          'productionIndexSha256Before': overlay.source_sha_before,
                          'productionH10EnabledBefore': overlay.source_enabled, 'testH10Enabled': True,
                          'isFullCopy': True, 'timingOnly': timing_only, 'cleanupOwnedPath': str(overlay.path)}
        (folder / 'runtime-overlay.json').write_text(json.dumps(overlay_record, indent=2), encoding='utf-8')
        executable = PROJECT / 'build/client/HearthstoneClone.exe'
        if not executable.is_file():
            raise FileNotFoundError(f'Windows Player executable not found: {executable}')
        server_log = (folder / 'server.log').open('w', encoding='utf-8')
        streams.append(server_log)
        server_cmd = [sys.executable, '-X', 'utf8', '-m', 'tools.demon_hunter_player_evidence',
                      '--serve', '--port', str(port), '--output', str(folder)]
        server = subprocess.Popen(server_cmd, cwd=PROJECT, stdout=server_log, stderr=subprocess.STDOUT,
                                  creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        processes.append(('server', server));result['processes'].append({'role': 'server', 'pid': server.pid})
        _wait_for(folder / 'authority-ready.json', server, 30, 'H10 evidence authority')
        token = uuid.uuid4().hex[:16]
        dll = executable.parent / 'HearthstoneClone_Data/Managed/Assembly-CSharp.dll'
        result['playerDllSha256'] = sha256(dll)
        width, height = size.split('x')
        for role, name, class_id in (('driver', 'DemonHunterA', '14'), ('observer', 'MageB', '4')):
            evidence = folder / role
            evidence.mkdir()
            command = [str(executable), '--profile', f'f020-h10-{token}-{role}', '--name', name,
                       '--class', class_id, '--auto-match', '--host', '127.0.0.1', '--port', str(port),
                       '--data', str(overlay.path), '--h10-player-replay', role, '--h10-evidence-dir', str(evidence),
                       '-screen-width', width, '-screen-height', height, '-screen-fullscreen', '0',
                       '-logFile', str(evidence / 'player.log')]
            if timing_only:
                command.extend(['--h10-timing-only', '1'])
            player = subprocess.Popen(command, cwd=PROJECT, **startup(True))
            processes.append((role, player));result['processes'].append({'role': role, 'pid': player.pid,
                'dataRoot': str(overlay.path), 'dataIndexSha256': overlay.index_sha256})
            if role == 'driver':
                _wait_for(folder / 'queued.json', player, 45, 'H10 driver')
        deadline = time.monotonic() + 150
        while any(proc.poll() is None for role, proc in processes if role != 'server'):
            failed = [(role, proc.returncode) for role, proc in processes
                      if role != 'server' and proc.poll() is not None and proc.returncode != 0]
            if failed:
                raise RuntimeError(f'H10 Player failed while its peer was running: {failed}')
            if time.monotonic() >= deadline:
                raise TimeoutError('Bounded H10 dual Player replay exceeded 150 seconds')
            time.sleep(.25)
        reports = [_report(folder, role, proc.returncode) for role, proc in processes if role in ('driver', 'observer')]
        result['reports'] = reports
        if any(proc.returncode != 0 for role, proc in processes if role != 'server') or \
           any(not row.get('result', '').startswith('PASS') for row in reports):
            raise RuntimeError('An H10 Player failed; preserve report and logs')
        result['result'] = f'PASS bounded H10 dual Player {size}' + (' timing-only' if timing_only else '')
    except BaseException as error:
        result['result'] = f'FAIL: {error}'
        raise
    finally:
        cleanup_error = None
        for role, process in reversed(processes):
            try:
                if process.poll() is None:
                    process.terminate()
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill();process.wait(timeout=15)
            record = next((row for row in result['processes'] if row['role'] == role), None)
            if record is not None:
                record['exit'] = process.returncode
        for stream in streams:
            stream.close()
        (folder / 'reports.json').write_text(json.dumps([_report(folder, role,
            next((p.returncode for r, p in processes if r == role), None)) for role in ('driver', 'observer')],
            ensure_ascii=False, indent=2), encoding='utf-8')
        overlay_record = json.loads((folder / 'runtime-overlay.json').read_text(encoding='utf-8')) if overlay else {}
        if overlay:
            try:
                after = overlay.verify_production_unchanged()
                isolated_after = sha256(overlay.path / 'runtime/native-effect-index.json')
                if isolated_after != overlay.index_sha256:
                    raise RuntimeError('H10 Player changed the isolated index')
                overlay_record.update(productionIndexSha256After=after,
                    productionH10EnabledAfter=overlay.source_enabled, isolatedIndexSha256After=isolated_after)
            except BaseException as error:
                cleanup_error = error
            finally:
                overlay.cleanup()
                overlay_record['cleanupConfirmed'] = not overlay.path.exists()
                (folder / 'runtime-overlay.json').write_text(json.dumps(overlay_record, indent=2), encoding='utf-8')
        result['productionIndexSha256Before'] = production_before
        result['productionIndexSha256After'] = sha256(PRODUCTION_INDEX)
        if result['productionIndexSha256Before'] != result['productionIndexSha256After']:
            cleanup_error = RuntimeError('Production native-effect index hash changed')
        result['productionH10EnabledAfter'] = h10_entry(PRODUCTION_INDEX)[1].get('enabled')
        (folder / 'run.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        if cleanup_error:
            raise cleanup_error


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def _validate_h10_followup_timing(unlock: dict) -> None:
    accepted_wall = unlock.get('powerAcceptedWallTime', -1)
    submitted_wall = unlock.get('submissionWallTime', -1)
    previous_update_wall = unlock.get('previousUpdateWallBoundary', -1)
    submission_update_wall = unlock.get('submissionUpdateWallBoundary', -1)
    frame_allowance = unlock.get('inputFrameAllowance', -1)
    expected_latency = submitted_wall - accepted_wall
    expected_frame_allowance = submission_update_wall - previous_update_wall
    update_frame_interval = submission_update_wall - previous_update_wall
    _require(unlock.get('castAcceptedFrame', 0) > 0 and unlock.get('submissionFrame', 0) > 0 and
             unlock.get('submissionFrame') == unlock.get('frame') and
             unlock.get('submissionFrame') >= unlock.get('castAcceptedFrame') and
             math.isfinite(accepted_wall) and math.isfinite(submitted_wall) and submitted_wall >= accepted_wall and
             math.isfinite(previous_update_wall) and math.isfinite(submission_update_wall) and
             previous_update_wall < submission_update_wall <= submitted_wall and update_frame_interval > 0 and
             abs(frame_allowance - expected_frame_allowance) <= .002 and
             abs(unlock.get('sendLatency', -1) - expected_latency) <= .002 and
             abs(unlock.get('latencyBudget', -1) - (.2 + frame_allowance)) <= .002,
             'driver monotonic Update/frame timing evidence is incomplete or inconsistent')
    _require(0 <= unlock.get('sendLatency', -1) <= .2 + frame_allowance + .001,
             'driver End Turn exceeded the 0.2s input gate plus its measured monotonic Update frame')


def _timing_summary(source: dict) -> dict:
    frames = source.get('frameTimings') or []
    _require(len(frames) >= 10, 'quiet H10 report has too few sampled Update frames')
    frame_ids = [row.get('frame', -1) for row in frames]
    _require(all(type(value) is int for value in frame_ids) and
             all(right > left for left, right in zip(frame_ids, frame_ids[1:])),
             'quiet H10 frame samples are not strictly ordered')
    wall = [row.get('wallMilliseconds', math.nan) for row in frames]
    times = [row.get('time', math.nan) for row in frames]
    _require(all(type(value) in (int, float) and math.isfinite(value) and value >= 0 for value in wall) and
             all(type(value) in (int, float) and math.isfinite(value) for value in times) and
             all(right >= left for left, right in zip(times, times[1:])),
             'quiet H10 frame timing values are incomplete or non-monotonic')
    stages: dict[str, list[float]] = {}
    for row in source.get('renderTimings') or []:
        stage, value = row.get('stage'), row.get('milliseconds', math.nan)
        _require(bool(stage) and type(value) in (int, float) and math.isfinite(value) and value >= 0,
                 'quiet H10 render timing row is invalid')
        stages.setdefault(stage, []).append(float(value))
    required = {'prepare-enabled-check', 'prepare-cold-create-and-seek', 'prepare-hot-seek', 'draw-total'}
    _require(required <= stages.keys(), f'quiet H10 report lacks timing stages: {sorted(required - stages.keys())}')
    steady = wall[1:]
    ordered = sorted(steady)
    p95 = ordered[max(0, math.ceil(.95 * len(ordered)) - 1)]
    return {
        'frameCount': len(frames), 'firstFrameMilliseconds': wall[0],
        'steadyMedianMilliseconds': statistics.median(steady), 'steadyP95Milliseconds': p95,
        'steadyMaxMilliseconds': max(steady),
        'stageSummary': {name: {'count': len(values), 'medianMilliseconds': statistics.median(values),
                                'maxMilliseconds': max(values)} for name, values in sorted(stages.items())
                         if name in required},
    }


def _player(folder: Path, size: str, root: str, index_hash: str, timing_only: bool = False) -> dict:
    report = json.loads((folder / 'report.json').read_text(encoding='utf-8'))
    _require(report.get('result', '').startswith('PASS'), f'{folder.name} Player replay failed')
    _require(report.get('timingOnly', False) is timing_only,
             f'{folder.name} timing-only mode marker differs from requested analyzer')
    _require(report.get('screenWidth') == int(size.split('x')[0]) and report.get('screenHeight') == int(size.split('x')[1]),
             f'{folder.name} Player resolution differs')
    _require(report.get('dataRoot') == root and report.get('indexSha256') == index_hash,
             f'{folder.name} did not use the isolated H10 index')
    before, cast, ended = report['before'], report['cast'], report['afterEndTurn']
    _require(cast.get('powerEntity') == before.get('powerEntity') and cast.get('heroEntity') == before.get('heroEntity') and
             cast.get('mana') == 9 and cast.get('heroAttack') == 1 and cast.get('powerUsed') is True,
             f'{folder.name} cast state does not prove the authoritative H10 result')
    _require(cast.get('publicHash') == cast.get('peerPublicHash') and cast.get('displayHash') == cast.get('publicHash'),
             f'{folder.name} cast authority/peer/display hashes differ')
    events = report.get('sourceEvents') or []
    _require([row.get('type') for row in events] == ['SpendMana', 'PlayHeroPower', 'Buff'] and
             events[1].get('target') == events[0].get('source') and events[2].get('source') == events[0].get('source') and
             events[2].get('target') == cast.get('heroEntity'), f'{folder.name} lacks the exact authoritative event pairing')
    ready_moves = report.get('readyMoves') or []
    legal_h10_moves = [row for row in ready_moves if row.get('kind') == 'power' and
                       row.get('entity') == before.get('powerEntity') and row.get('target') == 0]
    if report.get('role') == 'driver':
        _require(len(legal_h10_moves) == 1, 'driver ready moves do not contain exactly one legal untargeted H10 action')
        traces = report.get('inputTrace') or []
        down = [row for row in traces if row.get('stage') == 'mouse-down']
        up = [row for row in traces if row.get('stage') == 'mouse-up']
        _require(len(down) == len(up) == 1, 'driver report lacks one real H10 mouse-down/mouse-up pair')
        input_row = down[0];rect = input_row.get('powerRect') or {};point = input_row.get('point') or {}
        _require(input_row.get('h10PowerMoveLegal') and input_row.get('hitRegion') and input_row.get('connected') and
                 not input_row.get('connectionLost') and input_row.get('pending') and
                 input_row.get('actionCounterDelta') == 1 and rect.get('width', 0) > 0 and rect.get('height', 0) > 0 and
                 rect.get('x', 0) <= point.get('x', -1) <= rect.get('x', 0) + rect.get('width', 0) and
                 rect.get('y', 0) <= point.get('y', -1) <= rect.get('y', 0) + rect.get('height', 0),
                 'driver GUI input telemetry does not prove a connected legal click inside the H10 hit rect')
        if not timing_only:
            _require(set(report.get('images') or []) >= EXPECTED_DRIVER_INPUT_IMAGES,
                     'driver report lacks hover/pressed/released input screenshots')
    else:
        _require(not legal_h10_moves and len(ready_moves) == 0,
                 'observer ready snapshot unexpectedly exposes the active driver move')
    source = report.get('source') or {}
    _require(source.get('seen') and source.get('released') and source.get('rootReference') == ROOT_REFERENCE and
             source.get('hasOriginalHistory') is False and
             source.get('firstArrival') is False and source.get('buttonActions', 0) > 0 and
             source.get('playthroughActions', 0) > 0 and source.get('unboundActions') == 0,
             f'{folder.name} source actions/audio/History checks failed')
    unlock = report.get('inputUnlockedAfterSource') or {}
    _require(unlock.get('sampled') and unlock.get('sourceActive') and not unlock.get('sourceReleased') and
             not unlock.get('inputLocked') and not unlock.get('pending'),
             f'{folder.name} input did not unlock while the H10 source was active')
    if report.get('role') == 'driver':
        _validate_h10_followup_timing(unlock)
        _require(unlock.get('sendMoveSubmitted') and unlock.get('sourceActiveAtSend') and unlock.get('authorityAccepted') and
                 unlock.get('actionCounterDelta') == 2 and unlock.get('acceptedVersion', 0) > unlock.get('version', 0) and
                 unlock.get('activeAfter') == 1 and unlock.get('authorityPublicHash') and
                 unlock.get('sendLatency', -1) >= 0,
                 'driver did not submit and receive authority acceptance for a legal H10 End Turn during the source tail')
    else:
        _require(not unlock.get('sendMoveSubmitted', False), 'observer unexpectedly submitted the driver H10 follow-up')
    dsp = report.get('dsp') or {}
    _require(dsp.get('listenerCount') == 1 and dsp.get('channels') == 2 and dsp.get('sampleFrames', 0) > 0 and
             dsp.get('callbacks', 0) > 0 and dsp.get('overflows') == 0 and dsp.get('peak', 0) > 1e-5,
             f'{folder.name} Unity Listener PCM capture is invalid')
    _require(any(row.get('kind') == 'native-effect' for row in report.get('audio') or []),
             f'{folder.name} recorded no native source audio callback')
    _require(ended.get('turn', 0) > cast.get('turn', 0) and ended.get('active') == 1 and
             ended.get('publicHash') == ended.get('peerPublicHash') == ended.get('displayHash'),
             f'{folder.name} natural end-turn state hashes differ')
    if timing_only:
        required_captures = {'ready', 'power-button', 'self-hero-buff', 'cast', 'source-active',
                             'reference-target-peak', 'reference-source-tail', 'input-unlocked-during-source',
                             'source-released', 'after-end-turn'}
        if report.get('role') == 'driver':
            required_captures |= {'power-hover', 'power-pressed', 'power-clicked',
                                  'end-turn-submitted-during-source'}
        _require(not (report.get('images') or []) and not list(folder.glob('*.png')),
                 f'{folder.name} timing-only run still generated screenshot PNGs')
        _require(required_captures <= set(report.get('suppressedCaptures') or []),
                 f'{folder.name} timing-only capture requests are incomplete')
        timing = _timing_summary(source)
    else:
        role_images = EXPECTED_IMAGES | (EXPECTED_DRIVER_PHASE_IMAGES if report.get('role') == 'driver' else set())
        _require(set(report.get('images') or []) >= role_images, f'{folder.name} lacks phase screenshots')
        for name in role_images:
            path = folder / name
            _require(path.is_file(), f'{folder.name} screenshot missing: {name}')
            header = path.read_bytes()[:24]
            _require(len(header) == 24 and header[:8] == b'\x89PNG\r\n\x1a\n' and
                     (int.from_bytes(header[16:20], 'big'), int.from_bytes(header[20:24], 'big')) ==
                     tuple(map(int, size.split('x'))), f'{folder.name}/{name} screenshot dimensions differ')
        if report.get('role') == 'driver':
            for name in EXPECTED_DRIVER_INPUT_IMAGES:
                path = folder / name
                _require(path.is_file(), f'{folder.name} input screenshot missing: {name}')
                header = path.read_bytes()[:24]
                _require(len(header) == 24 and header[:8] == b'\x89PNG\r\n\x1a\n' and
                         (int.from_bytes(header[16:20], 'big'), int.from_bytes(header[20:24], 'big')) ==
                         tuple(map(int, size.split('x'))), f'{folder.name}/{name} screenshot dimensions differ')
        timing = None
    return {'role': folder.name, 'castHash': cast['publicHash'], 'endHash': ended['publicHash'],
            'heroAttack': cast['heroAttack'], 'mana': cast['mana'], 'source': source, 'dsp': dsp, 'followup': unlock,
            'screenshots': [] if timing_only else sorted(role_images), 'timing': timing}


def analyze(folder: Path, size: str) -> dict:
    folder = folder.resolve();run = json.loads((folder / 'run.json').read_text(encoding='utf-8'))
    overlay = json.loads((folder / 'runtime-overlay.json').read_text(encoding='utf-8'))
    _require(run.get('result', '').startswith('PASS'), 'dual H10 Player run did not pass')
    _require(run.get('timingOnly', False) is False, 'timing-only run requires the strict timing analyzer')
    _require(run.get('size') == size, 'resolution does not match the requested size')
    source_enabled = overlay.get('productionH10EnabledBefore')
    _require(type(source_enabled) is bool and run.get('productionH10EnabledAfter') == source_enabled and
             overlay.get('productionH10EnabledAfter') == source_enabled, 'production H10 enable state changed')
    _require(overlay.get('isFullCopy') is True and overlay.get('testH10Enabled') is True and
             overlay.get('productionIndexSha256Before') == overlay.get('productionIndexSha256After') ==
             run.get('productionIndexSha256Before') == run.get('productionIndexSha256After'),
             'formal H10 run changed production index or failed to use an isolated full copy')
    _require(overlay.get('cleanupConfirmed') is True, 'isolated runtime was not cleaned')
    data_root,index_hash=overlay['dataRoot'],overlay['indexSha256']
    processes={row.get('role'):row for row in run.get('processes') or []}
    for role in ('driver','observer'):
        _require(processes.get(role,{}).get('dataRoot')==data_root and processes[role].get('dataIndexSha256')==index_hash,
                 f'{role} did not use the shared isolated --data root')
    players=[_player(folder/role,size,data_root,index_hash) for role in ('driver','observer')]
    _require(players[0]['castHash']==players[1]['castHash'] and players[0]['endHash']==players[1]['endHash'],
             'dual Player cast/end authority hashes differ')
    result={'result':f'PASS H10 dual Player {size}','players':players,
            'productionIndexSha256':run['productionIndexSha256After'],'isolatedIndexSha256':index_hash,'dataRoot':data_root}
    (folder/'analysis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    return result


def analyze_timing(folder: Path, size: str) -> dict:
    folder = folder.resolve();run = json.loads((folder / 'run.json').read_text(encoding='utf-8'))
    _require(run.get('result', '').startswith('PASS'), 'dual H10 timing-only Player run did not pass')
    _require(run.get('timingOnly') is True, 'run was not launched in timing-only mode')
    _require(run.get('size') == size, 'resolution does not match the requested timing run')
    overlay = json.loads((folder / 'runtime-overlay.json').read_text(encoding='utf-8'))
    source_enabled = overlay.get('productionH10EnabledBefore')
    _require(overlay.get('timingOnly') is True and type(source_enabled) is bool and
             run.get('productionH10EnabledAfter') == source_enabled and
             overlay.get('productionH10EnabledAfter') == source_enabled,
             'timing-only run changed the production H10 enable state')
    _require(overlay.get('isFullCopy') is True and overlay.get('testH10Enabled') is True and
             overlay.get('productionIndexSha256Before') == overlay.get('productionIndexSha256After') ==
             run.get('productionIndexSha256Before') == run.get('productionIndexSha256After'),
             'timing-only run changed production data or failed to use an isolated full copy')
    _require(overlay.get('cleanupConfirmed') is True, 'timing-only isolated runtime was not cleaned')
    data_root, index_hash = overlay['dataRoot'], overlay['indexSha256']
    processes = {row.get('role'): row for row in run.get('processes') or []}
    for role in ('driver', 'observer'):
        _require(processes.get(role, {}).get('dataRoot') == data_root and
                 processes[role].get('dataIndexSha256') == index_hash,
                 f'{role} did not use the shared isolated --data root')
    players = [_player(folder / role, size, data_root, index_hash, timing_only=True)
               for role in ('driver', 'observer')]
    _require(players[0]['castHash'] == players[1]['castHash'] and players[0]['endHash'] == players[1]['endHash'],
             'quiet dual Player cast/end authority hashes differ')
    result = {'result': f'PASS H10 dual Player timing-only {size}', 'players': players,
              'productionIndexSha256': run['productionIndexSha256After'], 'isolatedIndexSha256': index_hash,
              'dataRoot': data_root}
    (folder / 'analysis.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


def main() -> None:
    parser=argparse.ArgumentParser(description=__doc__)
    modes=parser.add_mutually_exclusive_group(required=True)
    modes.add_argument('--serve',action='store_true');modes.add_argument('--run-players',action='store_true');modes.add_argument('--analyze',action='store_true')
    modes.add_argument('--analyze-timing',action='store_true')
    parser.add_argument('--timing-only',action='store_true',help='Suppress H10 screenshots for timing isolation')
    parser.add_argument('--size',choices=SIZE_CHOICES,default='1920x1080');parser.add_argument('--port',type=int,default=37262)
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    if args.timing_only and not args.run_players:parser.error('--timing-only is valid only with --run-players')
    if args.serve:asyncio.run(serve(args.port,args.output.resolve()))
    elif args.run_players:run_players(args.port,args.output,args.size,args.timing_only)
    elif args.analyze_timing:print(json.dumps(analyze_timing(args.output,args.size),ensure_ascii=False,indent=2))
    else:print(json.dumps(analyze(args.output,args.size),ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
