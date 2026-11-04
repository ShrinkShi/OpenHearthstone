"""Bounded Task4 evidence: inventory, legal replay inputs, indexed GPU output.

Internal repeatability is reported separately from official fidelity. Missing
official data never becomes a zero difference, pass, or compatibility upgrade.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import subprocess

from PIL import Image, ImageChops, ImageDraw, ImageStat

from .importer import PROJECT, DEFAULT_GAME

CARDS = ('EX1_129', 'EX1_134')
PHASES = ('Birth', 'Early', 'Mid', 'Arrival', 'Hit', 'Death')
DIMENSIONS = ('position', 'rotation', 'timing', 'color', 'alpha', 'particles', 'trail', 'audio')
MEDIA = {'.png', '.jpg', '.jpeg', '.gif', '.mp4', '.webm', '.mov', '.avi', '.mkv'}
WORK = PROJECT / 'work/task4'
MANIFESTS = PROJECT / 'manifests'
SKIP = {'.git', '.venv', '__pycache__', '.pytest_cache', 'Library', 'Temp', 'Obj', 'node_modules'}


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    sha = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(block)
    return sha.hexdigest()


def identity(path):
    path = Path(path).resolve()
    record = {'path': str(path), 'sha256': digest(path), 'bytes': path.stat().st_size}
    if path.suffix.lower() in {'.png', '.jpg', '.jpeg', '.gif'}:
        with Image.open(path) as image:
            record['size'] = list(image.size)
    return record


def inventory():
    """Only the challenge-associated E:\\HS tree; no user-directory search."""
    root = PROJECT.parent
    counts = Counter()
    candidates = []
    excluded = []
    for current, dirs, files in os.walk(root):
        current = Path(current)
        for name in list(dirs):
            path = current / name
            # These are imported source assets or build dependencies, not captures.
            if name in SKIP or path in (PROJECT / 'local/runtime', PROJECT / 'local/cache',
                                       PROJECT / 'UnityClient/Assets/StreamingAssets', PROJECT / 'build'):
                dirs.remove(name)
                excluded.append(str(path))
        for name in sorted(files):
            path = current / name
            if path.suffix.lower() not in MEDIA:
                continue
            relative = path.relative_to(root).as_posix()
            if relative.startswith('HearthstoneClone/tests/visual/reference/oracle/task4/'):
                kind = 'official_target_candidate_requires_provenance_review'
            elif relative.startswith('HearthstoneClone/tests/visual/reference/oracle/'):
                kind = 'official_ui_still_not_target_sequence'
            elif relative.startswith('references/'):
                kind = 'reference_repository_ui_or_logo_not_gameplay'
            elif relative.startswith(('HearthstoneClone/tests/visual/', 'HearthstoneClone/local/visual/',
                                      'HearthstoneClone/work/')):
                kind = 'project_generated_not_official'
            else:
                kind = 'unclassified_requires_provenance'
            counts[kind] += 1
            if kind != 'project_generated_not_official':
                candidates.append({**identity(path), 'kind': kind, 'card_id': 'Unknown',
                                   'phase': 'Unknown', 'official_effect_comparable': False})
    value = {'schema_version': 1, 'search_root': str(root), 'method': 'bounded media walk after rg file discovery',
             'extensions': sorted(MEDIA), 'excluded_directories': sorted(excluded),
             'counts': dict(counts), 'candidates': sorted(candidates, key=lambda row: row['path']),
             'official_target_sequences': [], 'status': 'BLOCKED',
             'source_installation': str(DEFAULT_GAME), 'source_installation_usage': 'read-only source bundles, no client interaction',
             'official_product_version': 'Unknown',
             'reason': 'No attributable EX1_129/EX1_134 Birth/Early/Mid/Arrival/Hit/Death sequence in searched capture locations.',
             'scope_limit': 'Not an assertion about unrelated directories or unprovided user captures; imported textures are not frames.'}
    old_frame = PROJECT / 'tests/visual/current/native-effects/EX1_134-0.30.png'
    old_report = PROJECT / 'tests/visual/current/native-effects/render-report.json'
    value['historical_white_center'] = {'card': 'EX1_134', 'name': 'SI:7 Agent',
        'image': identity(old_frame) if old_frame.exists() else 'Unknown',
        'report': identity(old_report) if old_report.exists() else 'Unknown',
        'reported_program_sha256': next((row['program_sha256'] for row in read(old_report).get('cards', []) if row['card'] == 'EX1_134'), 'Unknown') if old_report.exists() else 'Unknown',
        'scope': 'project offscreen flattened diagnostic, not official; do not migrate to EX1_124'}
    official = [row for row in candidates if row['kind'] == 'official_ui_still_not_target_sequence']
    contact = Image.new('RGB', (960, ((len(official) + 2) // 3) * 220), '#20242a')
    draw = ImageDraw.Draw(contact)
    for index, row in enumerate(official):
        x, y = (index % 3) * 320, (index // 3) * 220
        with Image.open(row['path']) as image:
            image = image.convert('RGB'); image.thumbnail((316, 190)); contact.paste(image, (x, y))
        draw.text((x + 4, y + 194), Path(row['path']).parent.name + '/' + Path(row['path']).name, fill='white')
    contact_path = WORK / 'oracle-inventory.png'; contact_path.parent.mkdir(parents=True, exist_ok=True); contact.save(contact_path)
    value['oracle_inventory_contact'] = identity(contact_path)
    write(MANIFESTS / 'task-0004-reference-inventory.json', value)
    print('INVENTORY', dict(counts), 'target sequences', 0)


def scenes():
    from .visual_scenarios import fixture, transition
    folder = WORK / 'scenes'
    for card in CARDS:
        match = fixture(0, 0, classes=(7, 10))
        if card == 'EX1_129':
            match.game.players[1].summon('EX1_402')
        else:
            match.game.players[0].give('GAME_005').play()
        match.game.players[0].give(card)
        match.observer.events.clear()
        before = [match.snapshot(seat) for seat in (0, 1)]
        transition(match, 'play', lambda move: match.entity(move.get('entity', 0)).id == card
                   and (not move.get('target') or match.entity(move['target']) == match.game.players[1].hero))
        after = [match.snapshot(seat) for seat in (0, 1)]
        for seat in (0, 1):
            for phase, states in (('before', before), ('after', after)):
                write(folder / f'{card}-{seat}-{phase}.json', states[seat])
        print('SCENE', card, 'Match.apply', after[0]['version'], [e['type'] for e in after[0]['events']])


def official_gaps(official):
    """An explicit input contract, not a claim of source authenticity."""
    result = []
    for card in CARDS:
        for seat in (0, 1):
            row = next((r for r in official.get('sequences', []) if r.get('card') == card and r.get('seat') == seat), {})
            gaps = []
            for key in ('source_media', 'source_sha256', 'product_version', 'capture_origin', 'camera',
                        'resolution', 'state', 'clock', 'seed', 'color_pipeline', 'audio_environment'):
                if row.get(key) in (None, '', 'Unknown', []):
                    gaps.append(key)
            if row.get('source_kind') != 'official_client_capture':
                gaps.append('source_kind')
            frames = row.get('frames', [])
            for phase in PHASES:
                frame = next((f for f in frames if f.get('phase') == phase), {})
                if not all(frame.get(k) not in (None, '', 'Unknown') for k in ('file', 'sha256', 'absolute_time', 'event_alignment')):
                    gaps.append('frame:' + phase)
            result.append({'card': card, 'seat': seat, 'missing': gaps,
                           'status': 'MISSING' if gaps else 'REQUIRES_PROVENANCE_AND_COMPARABILITY_REVIEW'})
    return result


def pixel_metrics(path):
    with Image.open(path) as image:
        rgba = image.convert('RGBA')
        rgb = rgba.convert('RGB')
        background = rgb.getpixel((0, 0))
        delta = ImageChops.difference(rgb, Image.new('RGB', rgb.size, background))
        red, green, blue = delta.split()
        maximum = ImageChops.lighter(ImageChops.lighter(red, green), blue)
        mask = maximum.point(lambda v: 255 if v > 3 else 0)
        visible = mask.getbbox()
        stat = ImageStat.Stat(rgba)
        return {'rgba_mean_8bit': stat.mean, 'background_rgb_8bit': list(background),
                'effect_bbox': list(visible) if visible else None, 'pixels_delta_above_3': mask.histogram()[255],
                'alpha_extrema_8bit': list(rgba.getchannel('A').getextrema()),
                'interpretation': 'production C+D*K on fixed diagnostic background; no official delta'}


def sheet(sequence, folder, destination):
    canvas = Image.new('RGB', (3 * 384, 3 * 246), '#20242a')
    draw = ImageDraw.Draw(canvas)
    draw.text((10, 8), f"PROJECT ONLY {sequence['card']} seat {sequence['seat']} - no official reference", fill='white')
    for index, frame in enumerate(sequence['frames']):
        x, y = (index % 3) * 384, (index // 3) * 234 + 30
        with Image.open(folder / frame['file']) as image:
            image = image.convert('RGB'); image.thumbnail((380, 210));canvas.paste(image, (x, y))
        draw.text((x + 5, y + 212), f"{frame['phase']} t={frame['time']:.6f}s step={frame['step']}", fill='white')
    canvas.save(destination)


def assemble(capture, repeat, official):
    report = read(capture / 'capture.json')
    second = read(repeat / 'capture.json') if repeat else None
    index = []
    repetitions = []
    for sequence in report['sequences']:
        frames = []
        for frame in sequence['frames']:
            path = capture / frame['file']
            frames.append({**frame, **identity(path), 'metrics': pixel_metrics(path),
                           'emission': identity(capture / frame['emissionFile']),
                           'transmission': identity(capture / frame['transmissionFile'])})
            if second:
                other_sequence = next(s for s in second['sequences'] if s['card'] == sequence['card'] and s['seat'] == sequence['seat'])
                other = next(f for f in other_sequence['frames'] if f['phase'] == frame['phase'])
                with Image.open(path) as first_image, Image.open(repeat / other['file']) as next_image:
                    delta = ImageChops.difference(first_image.convert('RGBA'), next_image.convert('RGBA'))
                    maximum = max(max(pair) for pair in delta.getextrema())
                # Exclude path identities, but retain positions/rotations/particles/clock.
                repetitions.append({'card': sequence['card'], 'seat': sequence['seat'], 'phase': frame['phase'],
                                    'pixels_identical': digest(path) == digest(repeat / other['file']),
                                    'max_channel_delta_8bit': maximum, 'telemetry_identical': frame == other})
        contacts = capture / f"{sequence['card']}-seat{sequence['seat']}-contact.png"
        sheet(sequence, capture, contacts)
        index.append({**sequence, 'before': identity(sequence['before']), 'after': identity(sequence['after']),
                      'frames': frames, 'contact_sheet': identity(contacts)})
    gaps = official_gaps(read(official))
    comparison = []
    for entry in gaps:
        comparison.append({**entry, 'official_frame_index': 'Unknown', 'overall': 'Unknown',
                           'dimensions': {dimension: {'grade': 'Unknown', 'difference': 'Unknown',
                               'reason': 'Missing qualified official evidence; project telemetry is one-sided.'} for dimension in DIMENSIONS}})
    database = read(PROJECT / 'docs/hearth_visual_db/effects.json')
    dependencies = {PROJECT / source for effect in database['effects'] for source in effect['source_graphs'].values()}
    dependencies.update(PROJECT / 'local/runtime' / name for name in (
        'native-super-spell.json', 'native-effect-index.json', 'native-smoke-source-textures.json'))
    dependencies.update(PROJECT / source for source in (
        'manifests/original-super-spell.json', 'manifests/original-super-missile-config.json',
        'manifests/original-super-impact.json', 'manifests/native-smoke-source-textures.json'))
    audio_files = {PROJECT / 'local/runtime' / cue['file'] for sequence in report['sequences'] for cue in sequence['audio']}
    value = {**{k: v for k, v in report.items() if k != 'sequences'}, 'schema_version': 1,
             'git_base': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=PROJECT, text=True).strip(),
             'implementation_files': [identity(PROJECT / path) for path in (
                 'UnityClient/Assets/Editor/Task4VisualEvidence.cs', 'UnityClient/Assets/Resources/NativeEffect.shader',
                 'UnityClient/Assets/Resources/NativeEffectAffineComposite.shader',
                 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs', 'UnityClient/Assets/Scripts/NativeSuperSpell.cs',
                 'tools/task4_visual_evidence.py', 'tools/Invoke-Task4VisualEvidence.ps1')],
             'calibration': identity(PROJECT / 'local/ui-calibration.json'), 'capture': identity(capture / 'capture.json'),
             'source_data': [identity(path) for path in sorted(dependencies)],
             'audio_files': [identity(path) for path in sorted(audio_files)],
             'process_records': [identity(path) for path in sorted(WORK.glob('process-*.json'))],
             'repeat_capture': identity(repeat / 'capture.json') if repeat else 'Unknown',
             'sequences': index, 'internal_repeatability': repetitions,
             'internal_repeatability_passed': bool(repetitions) and all(row['pixels_identical'] and row['telemetry_identical'] for row in repetitions),
             'official_comparison': comparison, 'task_status': 'BLOCKED',
             'compatibility_upgrade': False, 'official_input_contract': str(official.resolve()),
             'limitations': ['Diagnostic production C+D*K on fixed gray; no final GUI/glow/postprocessing.',
                            'Audio cue metadata captured, no waveform/physical playback comparison.',
                            'Death can share Arrival timestamp; event ordering retained.',
                            'Positions/rotations/particles/trail data have no corresponding official telemetry.',
                            'Official product version, source sequence, camera/state/time/seed remain Unknown.']}
    write(MANIFESTS / 'task-0004-visual-evidence.json', value)
    print('EVIDENCE', len(index), 'sequences', sum(len(s['frames']) for s in index), 'frames',
          sum(r['pixels_identical'] and r['telemetry_identical'] for r in repetitions), '/', len(repetitions), 'repeat-identical; BLOCKED official')
    if repeat and not value['internal_repeatability_passed']:
        raise RuntimeError('Internal replay differs; failure evidence saved, not an official comparison.')


def shader_source():
    from .additive_shader_audit import verify_sources
    from .particle_alpha_shader_audit import sneaky_smoke_audit, SMOKE_FOLDER
    from .inspect_compiled_shaders import disassemble
    manifest = read(MANIFESTS / 'native-sneaky-smoke-shader.json')
    fresh = sneaky_smoke_audit()
    assert fresh == manifest
    bindings = verify_sources(fresh)
    assert bindings == read(MANIFESTS / 'native-sneaky-smoke-source-bindings.json')
    cache = PROJECT / 'local/compiled-shader-audit' / SMOKE_FOLDER
    stages = []
    for binding in manifest['bindings']:
        program = cache / f"4-{binding['blob']}.bin"
        parameters = cache / f"4-{binding['parameter_blob']}.parameters.bin"
        assert digest(program) == binding['program_sha256']
        assert digest(parameters) == binding['parameter_sha256']
        assembly = disassemble(program.read_bytes())
        assert assembly == (cache / f"4-{binding['blob']}.asm.txt").read_text(encoding='utf-8')
        stages.append({**binding, 'program': identity(program), 'parameters': identity(parameters),
                       'assembly': identity(cache / f"4-{binding['blob']}.asm.txt")})
    bundles = [DEFAULT_GAME / 'Data/Win' / name for name in sorted({manifest['shader']['bundle']} |
               {row['material']['bundle'] for row in bindings['bindings']})]
    parsed = read(cache / 'parsed-form.json')
    source_state = parsed['m_SubShaders'][0]['m_Passes'][0]['m_State']
    source_tags = dict(parsed['m_SubShaders'][0]['m_Tags']['tags'])
    material_values = []
    from .importer import Importer
    importer = Importer()
    for binding in bindings['bindings']:
        source = binding['material']
        obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
        tree = obj.read_typetree()
        material_values.append({'source': source, 'name': tree['m_Name'],
                                'custom_render_queue': tree['m_CustomRenderQueue'],
                                'saved_properties': tree['m_SavedProperties']})
    value = {'schema_version': 1, 'kind': 'behavior_reconstruction_from_read_only_compiled_source',
             'official_product_version': 'Unknown', 'source': manifest['shader'],
             'bundles': [identity(path) for path in bundles], 'stages': stages,
             'source_recovery': 'direct recovery of compiled programs/parameters/state/material identity, not author source',
             'behavior_recovery': 'MainTex red * vertex alpha * Color alpha; unclamped color lerp; intensity; RGBA SrcAlpha/OneMinusSrcAlpha',
             'render_state': manifest['pass_render_states'], 'source_state': source_state,
             'source_tags': source_tags, 'material_values': material_values, 'bindings': bindings,
             'samplers_and_constant_layout': parsed['m_SubShaders'][0]['m_Passes'][0]['progFragment']['m_CommonParameters'],
             'unresolved': {'original_scene_postprocessing': 'Unknown', 'original_runtime_pipeline_globals': 'Unknown'},
             'formula': {'mask': 'MainTex(uv).r * vertexAlpha * Color.a',
                         'rgb': '(Color.rgb + mask * (SecondColor.rgb - Color.rgb)) * Intensity',
                         'alpha': 'mask * Intensity', 'blend_rgba': 'source * source.a + destination * (1-source.a)'},
             'gpu_evidence': 'manifests/task-0004-visual-evidence.json:shaderSamples',
             'visual_accuracy': 'controlled GPU arithmetic only; official whole-card Unknown',
             'whole_effect_verified': False}
    write(MANIFESTS / 'task-0004-shader-audit.json', value)
    print('SHADER SOURCE', len(stages), 'stages,', len(bindings['bindings']), 'materials, fresh source hashes and D3DDisassemble match')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('inventory', 'scenes', 'assemble', 'shader-source'))
    parser.add_argument('--capture', type=Path, default=WORK / 'capture-a')
    parser.add_argument('--repeat', type=Path)
    parser.add_argument('--official', type=Path, default=MANIFESTS / 'task-0004-official-input.json')
    args = parser.parse_args()
    if args.command == 'inventory': inventory()
    elif args.command == 'scenes': scenes()
    elif args.command == 'shader-source': shader_source()
    else: assemble(args.capture.resolve(), args.repeat.resolve() if args.repeat else None, args.official)


if __name__ == '__main__':
    main()
