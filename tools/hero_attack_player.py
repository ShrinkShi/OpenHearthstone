"""Record accepted base-hero Attack voice playback from paired Unity Players."""
from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import os
import socket
import subprocess
import sys
import time
from pathlib import Path

from game.database import HEROES
from game.server import Server
from tools.background_player import startup
from tools.hero_speech_player import validate_listener_audio


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CLASSES = tuple(sorted(HEROES))
WEAPON_FIXTURE_CLASSES = {'CS2_082': 7, 'CS2_106': 10, 'EX1_567': 8}
NATIVE_WEAPON_CASES = (('fiery-war-axe', 10, 'CS2_106'), ('doomhammer', 8, 'EX1_567'))


def weapon_fixture_card_id(peers):
    named_weapon_peers = [peer for peer in peers if peer.name.startswith('HeroAttackWeapon')]
    if not named_weapon_peers:
        return None
    if len(peers) != 2 or len(named_weapon_peers) != 2:
        raise ValueError('weapon fixtures require exactly two matching weapon replay peers')
    explicit = []
    for peer in peers:
        parts = peer.name.split('-')
        explicit.append(parts[1] if len(parts) >= 3 and parts[0] == 'HeroAttackWeapon' else '')
    if all(explicit):
        if len(set(explicit)) != 1 or explicit[0] not in WEAPON_FIXTURE_CLASSES:
            raise ValueError('paired weapon fixture names must specify one supported weapon card id')
        card_id = explicit[0]
    elif all(not value for value in explicit) and all(peer.class_id == 7 for peer in peers):
        card_id = 'CS2_082'
    else:
        raise ValueError('weapon fixture names do not identify a class-legal weapon')
    expected_class = WEAPON_FIXTURE_CLASSES[card_id]
    if any(peer.class_id != expected_class for peer in peers):
        raise ValueError(f'{card_id} weapon fixture requires class id {expected_class} on both peers')
    return card_id


def seed_weapon_equipment(players, card_id, terminal_wear=False):
    if card_id not in WEAPON_FIXTURE_CLASSES:
        raise ValueError(f'unsupported native weapon fixture {card_id}')
    seeded = []
    for seat, player in enumerate(players):
        if player.weapon is not None:
            raise RuntimeError('weapon fixture unexpectedly starts equipped')
        weapon = player.summon(card_id)
        if terminal_wear:
            weapon.damage = weapon.max_durability - 1
        seeded.append({'seat': seat, 'card_id': weapon.id, 'attack': weapon.atk,
                       'durability': weapon.durability, 'max_durability': weapon.max_durability})
    return seeded


class HeroAttackFixtureServer(Server):
    """Seed a temporary hero attack state after mulligan; keep attacks authoritative."""

    def __init__(self, logdir, fixture_path):
        super().__init__(logdir)
        self.fixture_path = Path(fixture_path)
        self.seeded_matches = set()

    def record(self, match, command, seat, error=''):
        if match.id not in self.seeded_matches and not match.mulligan:
            players = match.game.players
            peers = self.rooms.get(match.id, [])
            weapon_card_id = weapon_fixture_card_id(peers)
            if weapon_card_id:
                weapon_seeds = seed_weapon_equipment(players, weapon_card_id,
                                                     terminal_wear=weapon_card_id != 'CS2_082')
                fixture_kind = ('class_legal_rogue_weapon_CS2_082' if weapon_card_id == 'CS2_082' else
                                'class_legal_native_weapon_terminal_wear')
            else:
                weapon_seeds = []
                for player in players:
                    player.hero.atk = max(1, player.hero.atk)
                fixture_kind = 'temporary_hero_attack_grant_without_weapon'
            fixture = {
                'match_id': match.id,
                'fixture_kind': fixture_kind,
                'weapon_card_id': weapon_card_id or '',
                'heroes': [{'seat': seat, 'card_id': player.hero.id,
                            'attack': player.hero.atk,
                            'weapon_card_id': player.weapon.id if player.weapon else ''}
                           for seat, player in enumerate(players)],
                'weapons': weapon_seeds,
                'legal_hero_attacks': [
                    {'seat': seat, 'entity': move['entity'], 'target': move['target']}
                    for seat in range(len(players))
                    for move in match.legal_moves(seat)
                    if move['kind'] == 'attack' and move['entity'] == players[seat].hero.entity_id
                ],
            }
            self.fixture_path.parent.mkdir(parents=True, exist_ok=True)
            with self.fixture_path.open('a', encoding='utf-8') as stream:
                stream.write(json.dumps(fixture, ensure_ascii=False) + '\n')
            self.seeded_matches.add(match.id)
        super().record(match, command, seat, error)


async def serve(port: int, output: Path):
    matches = output / 'matches'
    matches.mkdir(parents=True, exist_ok=True)
    server = HeroAttackFixtureServer(matches, output / 'fixtures.jsonl')
    listener = await asyncio.start_server(server.handle, '127.0.0.1', port, limit=32768)
    print(f'Hero Attack fixture server listening on 127.0.0.1:{port}', flush=True)
    async with listener:
        await listener.serve_forever()


def reserve_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind(('127.0.0.1', 0))
        return listener.getsockname()[1]


def fingerprint_player_binaries(client):
    client = Path(client).resolve()
    binaries = {
        'executable': client,
        'assembly_csharp': (client.parent / f'{client.stem}_Data' / 'Managed' / 'Assembly-CSharp.dll').resolve(),
    }
    missing = [str(path) for path in binaries.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError('Player provenance file is missing: ' + ', '.join(missing))
    result = {}
    for name, path in binaries.items():
        digest = hashlib.sha256()
        with path.open('rb') as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                digest.update(chunk)
        result[name] = {'path': str(path), 'sha256': digest.hexdigest()}
    return result


def record_player_binary_provenance(summary, client, before=None, before_error=''):
    provenance = summary.setdefault('player_binary_provenance', {'before': None, 'after': None,
                                                                  'unchanged': None})
    errors = []
    if before is None:
        provenance['before_error'] = before_error or 'Player binaries were not fingerprinted before launch'
        provenance['unchanged'] = False
        errors.append('Player binary provenance could not be captured before launch: ' + provenance['before_error'])
    else:
        provenance['before'] = before
        try:
            after = fingerprint_player_binaries(client)
        except OSError as error:
            provenance['after_error'] = str(error)
            provenance['unchanged'] = False
            errors.append('Player binary provenance could not be captured after all Players exited: ' + str(error))
        else:
            provenance['after'] = after
            for name, initial in before.items():
                final = after[name]
                if initial['path'] != final['path']:
                    errors.append(f'{name} absolute path changed during the Player run')
                if initial['sha256'] != final['sha256']:
                    errors.append(f'{name} SHA-256 changed during the Player run')
            provenance['unchanged'] = not errors
    if errors:
        summary['passed'] = False
        analysis = summary.setdefault('analysis', {'passed': False, 'results': []})
        analysis['passed'] = False
        analysis.setdefault('results', []).append({'case': 'player-binary-provenance', 'errors': errors})
    return errors


def wait_for_server(port, process, timeout=20):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f'Hero Attack fixture server exited with {process.returncode}')
        try:
            with socket.create_connection(('127.0.0.1', port), timeout=.5) as connection:
                connection.settimeout(.5)
                hello = json.loads(connection.makefile('rb').readline(8192))
                if hello.get('type') == 'hello' and hello.get('protocol') == 1:
                    return
                raise RuntimeError('server handshake does not match game protocol')
        except (OSError, json.JSONDecodeError):
            time.sleep(.1)
    raise TimeoutError('Hero Attack fixture server did not become ready')


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def attack_source_map(project_root=ROOT):
    manifest_path = project_root / 'manifests' / 'original-hero-attack-voices.json'
    runtime = project_root / 'local' / 'runtime'
    manifest = read_json(manifest_path)
    index = read_json(runtime / 'voice-index.json')
    registry = read_json(runtime / 'registry.json')
    sources = manifest.get('voices') or []
    heroes = {row['card_id'] for row in manifest.get('heroes') or []}
    if len(heroes) != 11 or len(sources) != 15 or manifest.get('errors'):
        raise ValueError('expected 11 original heroes, 15 attack clips, and no source errors')
    if len({row['id'] for row in sources}) != len(sources):
        raise ValueError('source manifest repeats a logical attack clip id')
    registry_by_id = {row['id']: row for row in registry.get('entries') or []}
    index_rows = [row for row in index.get('entries') or []
                  if row.get('event') == 'ATTACK' and row.get('card_id') in heroes]
    expected = {}
    for hero in sorted(heroes):
        voice_rows = [row for row in index_rows if row.get('card_id') == hero]
        source_rows = [row for row in sources if row.get('card_id') == hero and row.get('event') == 'ATTACK']
        if not voice_rows or len(voice_rows) != len(source_rows):
            raise ValueError(f'{hero} does not have a matching original ATTACK source/index group')
        expected[hero] = voice_rows[0]['id']
    for row in sources:
        if row.get('event') != 'ATTACK' or row.get('card_id') not in heroes:
            raise ValueError('attack manifest contains an unexpected card/event row')
        required = ('name', 'asset_id', 'bundle', 'cab', 'pathid', 'file', 'sha256')
        if any(not row.get(field) for field in required):
            raise ValueError(f"{row.get('id')} is missing original AudioClip identity fields")
        refs = row.get('reference')
        if not isinstance(refs, list) or not refs or any(':' not in ref for ref in refs):
            raise ValueError(f"{row.get('id')} is missing CardDef SoundSpell source references")
        index_row = next((item for item in index_rows if item.get('id') == row['id']), None)
        asset = registry_by_id.get(row['id'])
        if index_row is None or asset is None or asset.get('path') != row['file']:
            raise ValueError(f"{row['id']} is not linked through voice-index and registry")
        audio_path = runtime / row['file']
        if not audio_path.is_file() or hashlib.sha256(audio_path.read_bytes()).hexdigest() != row['sha256']:
            raise ValueError(f"{row['id']} cache file does not match the original source manifest")
    return expected


def weapon_audio_sources(project_root=ROOT):
    runtime = project_root / 'local' / 'runtime'
    manifest = read_json(runtime / 'weapon-audio.json')
    result = {}
    for slot in (5, 7, 41, 42):
        matches = [row for row in manifest.get('entries') or [] if row.get('slot') == slot]
        if len(matches) != 1:
            raise ValueError(f'expected one original native weapon audio source at slot {slot}')
        row = matches[0]
        clips = ((row.get('definition') or {}).get('clips') if slot == 7 else
                 (row.get('sound') or {}).get('clips')) or []
        if len(clips) != 1:
            raise ValueError(f'native weapon slot {slot} must map to exactly one original AudioClip')
        clip = clips[0]
        source = clip.get('source') or {}
        if (source.get('type') != 'AudioClip' or not source.get('bundle') or
                not source.get('cab') or not source.get('pathid') or
                Path(clip.get('file', '')).name != clip.get('file') or
                not (runtime / clip.get('file', '')).is_file()):
            raise ValueError(f'native weapon slot {slot} is not linked to an original AudioClip source')
        required_reference = {
            5: 'Weapon_Destruction_01.wav:780a0d0d979a7684484209017ab3ddd9',
            7: 'Weapon_Durability_Hit_01.wav:8cc86918ea7f78f4487be0f430b11119',
        }.get(slot)
        if required_reference and clip.get('reference') != required_reference:
            raise ValueError(f'weapon slot {slot} is not linked to its original transition clip')
        result[slot] = {'file': clip['file'], 'reference': clip['reference'], 'source': source}
    return result


def validate_report(report_path, expected_hero, expected_voice_id, expected_weapon_sources=None,
                    expected_weapon_counts=None, expected_weapon_card_id=None):
    report_path = Path(report_path)
    errors = []
    report = read_json(report_path)
    if not report.get('attack_replay_enabled') or not report.get('attack_replay_complete'):
        errors.append('Player attack replay did not complete')
    if report.get('hero_card_id') != expected_hero:
        errors.append('Player local hero differs from the requested base hero')
    if type(report.get('seat')) is not int or report.get('seat') not in (0, 1):
        errors.append('Player report does not contain a valid actual match seat')
    if report.get('attack_mulligan_count') != 1 or report.get('attack_move_count') != 1:
        errors.append('Player did not submit exactly one mulligan keep and one hero attack move')
    if report.get('duplicate_attack_callbacks') != 0:
        errors.append('Client observed duplicate accepted Attack callbacks')
    attacks = report.get('hero_attacks') or []
    if len(attacks) != 2 or {row.get('seat') for row in attacks} != {0, 1}:
        errors.append('Player did not observe one accepted hero Attack from each seat')
    elif report.get('seat') in (0, 1) and sum(row.get('seat') == report.get('seat') for row in attacks) != 1:
        errors.append('Player did not observe the accepted hero Attack owned by its actual seat')
    for row in attacks:
        if (row.get('card_id') != expected_hero or row.get('accepted_count') != 1 or
                row.get('voice_start_count') != 1 or row.get('voice_id') != expected_voice_id or
                row.get('accepted_at', -1) < 0 or row.get('voice_started_at', -1) < row.get('accepted_at', -1)):
            errors.append('accepted hero Attack did not start its mapped original voice exactly once')
    if len({row.get('event_key') for row in attacks}) != len(attacks):
        errors.append('two accepted hero attacks share an event key')
    weapon_cues = report.get('weapon_audio') or []
    if expected_weapon_sources:
        expected_counts = expected_weapon_counts or {7: 2, 41: 1, 42: 1}
        for slot, count in expected_counts.items():
            expected = expected_weapon_sources[slot]
            slot_cues = [cue for cue in weapon_cues
                         if str(cue.get('source_name', '')).startswith(f'Weapon slot {slot} ')]
            if len(slot_cues) != count or any(not cue.get('playback_started') or
                                              Path(cue.get('file', '')).name != expected['file']
                                              for cue in slot_cues):
                errors.append(f'weapon slot {slot} cues do not match the original source AudioClip')
        if len(weapon_cues) != sum(expected_counts.values()):
            errors.append('weapon replay recorded an unknown or missing native weapon cue')
    elif weapon_cues:
        errors.append('weaponless hero fixture unexpectedly played a weapon collision cue')
    if expected_weapon_card_id and any(row.get('weapon_card_id') != expected_weapon_card_id for row in attacks):
        errors.append(f'hero attacks did not retain the expected class-legal {expected_weapon_card_id} weapon')
    errors.extend(validate_listener_audio(report.get('listener_audio'), report_path.with_suffix('.listener.wav')))
    return report, errors


def match_attack_commands(output, match_id):
    path = Path(output) / 'matches' / f'{match_id}.jsonl'
    rows = [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines() if line.strip()]
    attacks = [row for row in rows if row.get('command', {}).get('kind') == 'attack' and not row.get('error')]
    return attacks


def paired_attack_errors(output, reports):
    errors = []
    seats = [report.get('seat') for report in reports]
    if len(reports) != 2 or set(seats) != {0, 1}:
        errors.append('paired Player reports do not contain two distinct actual match seats')
    match_ids = [report.get('match_id') for report in reports]
    if len(reports) != 2 or not match_ids[0] or match_ids[0] != match_ids[1]:
        errors.append('paired Player reports do not share a match id')
        return errors

    signatures = [sorted((row.get('event_key'), row.get('seat'), row.get('voice_id'))
                         for row in report.get('hero_attacks') or []) for report in reports]
    if signatures[0] != signatures[1]:
        errors.append('paired Players disagree on accepted attack/voice evidence')
    commands = match_attack_commands(output, match_ids[0])
    if len(commands) != 2 or {row.get('seat') for row in commands} != {0, 1}:
        errors.append('authority log does not contain exactly one accepted attack action per seat')
        return errors

    event_by_seat = {}
    for row in reports[0].get('hero_attacks') or []:
        seat = row.get('seat')
        if seat in event_by_seat:
            errors.append(f'paired Player evidence repeats the accepted attack for actual seat {seat}')
        event_by_seat[seat] = (row.get('source'), row.get('target'))
    command_by_seat = {row.get('seat'): row.get('command') or {} for row in commands}
    for seat in (0, 1):
        command = command_by_seat.get(seat, {})
        if event_by_seat.get(seat) != (command.get('entity'), command.get('target')):
            errors.append(f'authority accepted attack does not match the Player event owned by actual seat {seat}')
        if command.get('match_id') != match_ids[0]:
            errors.append(f'authority accepted attack for actual seat {seat} has a different match id')
    return errors


def analyze(output, class_ids=None, include_rogue_weapon=True, project_root=ROOT, include_native_weapons=False):
    output = Path(output).resolve()
    expected_voices = attack_source_map(project_root)
    classes = tuple(class_ids or DEFAULT_CLASSES)
    expected_weapon_sources = weapon_audio_sources(project_root) if include_rogue_weapon or include_native_weapons else None
    results = []
    for class_id in classes:
        hero = HEROES[class_id]
        reports = []
        for seat in (0, 1):
            report_path = output / 'reports' / f'class-{class_id}-seat-{seat}.json'
            expected_voice = expected_voices.get(hero, '')
            report, errors = validate_report(report_path, hero, expected_voice)
            reports.append(report)
            results.append({'case': f'class-{class_id}', 'launch_role': seat,
                            'actual_seat': report.get('seat'),
                            'report': str(report_path), 'errors': errors})
        pair_errors = paired_attack_errors(output, reports)
        if pair_errors:
            results.append({'case': f'class-{class_id}', 'errors': pair_errors})
    if include_rogue_weapon:
        reports = []
        for seat in (0, 1):
            report_path = output / 'reports' / f'rogue-weapon-seat-{seat}.json'
            report, errors = validate_report(report_path, HEROES[7], expected_voices[HEROES[7]],
                                             expected_weapon_sources)
            if any(row.get('weapon_card_id') != 'CS2_082' for row in report.get('hero_attacks') or []):
                errors.append('Rogue weapon fixture did not retain its class-legal CS2_082 equipment')
            reports.append(report)
            results.append({'case':'rogue-weapon','launch_role':seat,'actual_seat':report.get('seat'),
                            'report':str(report_path),'errors':errors})
        pair_errors = paired_attack_errors(output, reports)
        if pair_errors:
            results.append({'case':'rogue-weapon','errors':pair_errors})
    if include_native_weapons:
        native_counts = {7: 2, 5: 2, 41: 1, 42: 1}
        for case, class_id, weapon_card_id in NATIVE_WEAPON_CASES:
            reports = []
            for seat in (0, 1):
                report_path = output / 'reports' / f'native-weapon-{weapon_card_id}-seat-{seat}.json'
                report, errors = validate_report(report_path, HEROES[class_id], expected_voices[HEROES[class_id]],
                    expected_weapon_sources, native_counts, weapon_card_id)
                reports.append(report)
                results.append({'case': f'native-weapon-{weapon_card_id}', 'launch_role': seat,
                                'actual_seat': report.get('seat'), 'report': str(report_path),
                                'weapon_audio_sources': {str(slot): source for slot, source in expected_weapon_sources.items()},
                                'weapon_audio_cues': report.get('weapon_audio') or [],
                                'listener_audio': report.get('listener_audio'), 'errors': errors})
            pair_errors = paired_attack_errors(output, reports)
            if pair_errors:
                results.append({'case': f'native-weapon-{weapon_card_id}', 'errors': pair_errors})
    return {'passed':all(not row.get('errors') for row in results),'results':results}


def stop_child(process, timeout=4):
    if process is None or process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=timeout)


def run_pair(client, port, output, case, class_id, timeout, foreground, weapon_case=False, weapon_card_id=''):
    processes=[]
    profiles=[]
    logs=output/'logs';reports=output/'reports';captures=output/'captures'
    for seat in (0,1):
        profile=f'heroAttack{os.getpid()}{case.replace("-","")}{seat}'
        name=(f'HeroAttackWeapon-{weapon_card_id}-{seat}' if weapon_card_id else
              'HeroAttackWeapon'+str(seat) if weapon_case else 'HeroAttack'+str(seat))
        report=reports/f'{case}-seat-{seat}.json'
        command=[str(client),'--profile',profile,'--name',name,'--host','127.0.0.1','--port',str(port),
                 '--class',str(class_id),'--auto-match','--hero-attack-replay','--hero-attack-report',str(report),
                 '--screenshot-dir',str(captures),'-logFile',str(logs/f'{profile}.log')]
        process=subprocess.Popen(command,cwd=ROOT,**startup(not foreground))
        processes.append(process);profiles.append({'seat':seat,'profile':profile,'report':str(report),'pid':process.pid})
    deadline=time.monotonic()+timeout
    try:
        while time.monotonic()<deadline:
            if all(process.poll() is not None for process in processes):
                break
            if any(process.poll() not in (None,0) for process in processes):
                raise RuntimeError(f'{case}: a Player exited with an error; preserve logs in {logs}')
            time.sleep(.25)
        else:
            raise TimeoutError(f'{case}: Player attack replay exceeded {timeout}s; artifacts retained at {output}')
        for row,process in zip(profiles,processes):
            row['exit_code']=process.returncode
            if process.returncode!=0:
                raise RuntimeError(f"{case}: Player exit code {process.returncode}")
        return profiles
    finally:
        for process in processes:
            stop_child(process)


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client',default=str(ROOT/'build/client/HearthstoneClone.exe'))
    parser.add_argument('--class-ids',default=','.join(map(str,DEFAULT_CLASSES)))
    parser.add_argument('--no-rogue-weapon-case',action='store_true')
    parser.add_argument('--native-weapons',action='store_true',
                        help='add class-legal Fiery War Axe and Doomhammer Player cases, including wear/destruction cues')
    parser.add_argument('--timeout-per-match',type=int,default=180)
    parser.add_argument('--output-dir',default='')
    parser.add_argument('--analyze-dir',default='',help='validate saved Player/authority evidence without launching anything')
    parser.add_argument('--source-root',default=str(ROOT),help='project root containing source manifests and runtime assets')
    parser.add_argument('--serve',action='store_true',help=argparse.SUPPRESS)
    parser.add_argument('--port',type=int,default=0,help=argparse.SUPPRESS)
    parser.add_argument('--foreground',action='store_true')
    args=parser.parse_args(argv)
    if args.serve:
        if args.port<1 or not args.output_dir:parser.error('--serve requires --port and --output-dir')
        asyncio.run(serve(args.port,Path(args.output_dir).resolve()));return 0
    if args.analyze_dir:
        try:
            class_ids=tuple(int(value) for value in args.class_ids.split(',') if value)
        except ValueError:parser.error('--class-ids must be comma-separated integers')
        if not class_ids or len(set(class_ids))!=len(class_ids) or any(value not in HEROES for value in class_ids):
            parser.error('--class-ids must be unique known base hero class ids')
        summary=analyze(Path(args.analyze_dir),class_ids,not args.no_rogue_weapon_case,
                        project_root=Path(args.source_root).resolve(),include_native_weapons=args.native_weapons)
        print(json.dumps(summary,ensure_ascii=False,indent=2));return 0 if summary['passed'] else 1
    if os.name!='nt':parser.error('Unity Player evidence harness requires Windows')
    client=Path(args.client).resolve()
    if not client.is_file():parser.error(f'Unity Player not found: {client}')
    runtime=ROOT/'local'/'runtime'
    if not (runtime/'registry.json').is_file() or not (runtime/'voice-index.json').is_file():
        parser.error('root-controlled runtime assets and original voice index are required')
    try:
        class_ids=tuple(int(value) for value in args.class_ids.split(',') if value)
    except ValueError:parser.error('--class-ids must be comma-separated integers')
    if not class_ids or len(set(class_ids))!=len(class_ids) or any(value not in HEROES for value in class_ids):
        parser.error('--class-ids must be unique known base hero class ids')
    if args.output_dir:output=Path(args.output_dir).resolve()
    else:output=ROOT/'work'/f'hero-attack-player-{int(time.time())}-{os.getpid()}'
    try:output.relative_to(ROOT.resolve())
    except ValueError:parser.error('output directory must remain inside the repository work tree')
    for folder in (output,output/'logs',output/'reports',output/'captures',output/'matches'):
        folder.mkdir(parents=True,exist_ok=False if folder==output else True)
    port=reserve_port();server_log=(output/'logs'/'server.log').open('ab');server=None
    summary={'port':port,'classes':list(class_ids),'fixture':'temporary +1 hero attack after mulligan','cases':[]}
    summary['player_binary_provenance']={'before':None,'after':None,'unchanged':None}
    if args.native_weapons:
        summary['native_weapon_sources'] = {str(slot): source for slot, source in weapon_audio_sources(ROOT).items()}
    binary_before=None
    try:
        binary_before=fingerprint_player_binaries(client)
        summary['player_binary_provenance']['before']=binary_before
        server=subprocess.Popen([sys.executable,'-X','utf8','-m','tools.hero_attack_player','--serve',
                                 '--port',str(port),'--output-dir',str(output)],cwd=ROOT,stdout=server_log,
                                stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        wait_for_server(port,server)
        for class_id in class_ids:
            case=f'class-{class_id}'
            players=run_pair(client,port,output,case,class_id,args.timeout_per_match,args.foreground)
            summary['cases'].append({'case':case,'class_id':class_id,'hero':HEROES[class_id],'players':players})
        if not args.no_rogue_weapon_case:
            players=run_pair(client,port,output,'rogue-weapon',7,args.timeout_per_match,args.foreground,True)
            summary['cases'].append({'case':'rogue-weapon','class_id':7,'hero':HEROES[7],
                                     'fixture':'class-legal CS2_082','players':players})
        if args.native_weapons:
            for case, class_id, weapon_card_id in NATIVE_WEAPON_CASES:
                players=run_pair(client,port,output,f'native-weapon-{weapon_card_id}',class_id,
                                 args.timeout_per_match,args.foreground,True,weapon_card_id)
                summary['cases'].append({'case':f'native-weapon-{weapon_card_id}','class_id':class_id,
                    'hero':HEROES[class_id],'fixture':f'class-legal {weapon_card_id} at terminal durability',
                    'players':players})
        results=analyze(output,class_ids,not args.no_rogue_weapon_case,
                        include_native_weapons=args.native_weapons)
        summary['analysis']=results;summary['passed']=results['passed']
        record_player_binary_provenance(summary,client,binary_before)
        (output/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(summary,ensure_ascii=False,indent=2))
        return 0 if summary['passed'] else 1
    except Exception as error:
        summary['passed']=False;summary['error']=str(error)
        if binary_before is None:
            record_player_binary_provenance(summary,client,before_error=str(error))
        elif summary['player_binary_provenance']['after'] is None:
            record_player_binary_provenance(summary,client,binary_before)
        (output/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
        raise
    finally:
        stop_child(server)
        server_log.close()


if __name__=='__main__':
    raise SystemExit(main())
