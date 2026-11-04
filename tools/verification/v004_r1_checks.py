"""Independent, bounded V004-r1 replay and failure controls.

Originally authored by V004-r1; V004-r2 reviewed and reused it, adding --work
isolation and an absolute-time negative control. The default is work/v004-r1.
Production tools and the recorded F004 evidence are read only.
"""
from copy import deepcopy
import argparse
import json
from pathlib import Path
import shutil

from PIL import Image

from tools import task4_visual_evidence as evidence


ROOT = evidence.PROJECT
WORK = ROOT / 'work/v004-r1'
ORIGINAL_WRITE = evidence.write
DESTINATION = WORK / 'manifests'


def isolated_write(path, value):
    path = Path(path)
    if path.parent == evidence.MANIFESTS and path.name.startswith('task-0004-'):
        path = DESTINATION / path.name
    ORIGINAL_WRITE(path, value)


def configure():
    evidence.WORK = WORK
    evidence.write = isolated_write


def prepare():
    evidence.inventory()
    evidence.scenes()
    evidence.shader_source()
    for path in (WORK / 'scenes').glob('*.json'):
        baseline = ROOT / 'work/task4/scenes' / path.name
        assert evidence.read(path) == evidence.read(baseline), path.name
    inventory = evidence.read(DESTINATION / 'task-0004-reference-inventory.json')
    baseline = evidence.read(ROOT / 'manifests/task-0004-reference-inventory.json')
    assert inventory['candidates'] == baseline['candidates']
    assert inventory['official_target_sequences'] == []
    assert inventory['official_product_version'] == 'Unknown'
    audit = evidence.read(DESTINATION / 'task-0004-shader-audit.json')
    assert audit == evidence.read(ROOT / 'manifests/task-0004-shader-audit.json')
    print('V004 PREPARE PASS: 35 source candidates and 8 rule snapshots match; fresh shader source audit matches')


def unknown_result(value):
    assert value['task_status'] == 'BLOCKED'
    assert not value['compatibility_upgrade'] and not value['wholeEffectVerified']
    assert len(value['official_comparison']) == 4
    assert all(row['overall'] == 'Unknown' for row in value['official_comparison'])
    assert all(item == {'grade': 'Unknown', 'difference': 'Unknown',
                       'reason': 'Missing qualified official evidence; project telemetry is one-sided.'}
               for row in value['official_comparison'] for item in row['dimensions'].values())


def assemble():
    evidence.assemble(WORK / 'capture-a', WORK / 'capture-b', ROOT / 'manifests/task-0004-official-input.json')
    value = evidence.read(DESTINATION / 'task-0004-visual-evidence.json')
    unknown_result(value)
    assert value['internal_repeatability_passed'] and len(value['internal_repeatability']) == 28
    assert len(value['shaderSamples']) == 7 and max(s['maxError'] for s in value['shaderSamples']) < .003
    for sequence in value['sequences']:
        assert sequence['completed'] and sequence['released']
        assert len(sequence['frames']) == 7
        frames = {frame['phase']: frame for frame in sequence['frames']}
        assert frames['Arrival']['step'] == frames['Death']['step']
        assert frames['Arrival']['step'] <= frames['Hit']['step']
        assert frames['Cleanup']['completed'] and frames['Cleanup']['clones'] == []
        for frame in sequence['frames']:
            assert abs(frame['time'] - frame['step'] / 240) < 1e-6
        events = sequence['events']
        arrival = next(i for i, event in enumerate(events) if ' Arrival ' in event)
        death = next(i for i, event in enumerate(events) if ' MissileDeath ' in event)
        hit = next(i for i, event in enumerate(events) if ' ImpactState5 ' in event)
        assert arrival < death < hit
    print('V004 ASSEMBLE PASS: 28 repeat-identical frames; phase order, absolute clock, cleanup, 7 GPU samples and Unknown gates')


def boundaries():
    global DESTINATION
    official = {'sequences': []}
    for card in evidence.CARDS:
        for seat in (0, 1):
            # Deliberately fabricated metadata and internal PNG: a complete shape
            # cannot be promoted into an official accuracy conclusion.
            row = {key: 'fabricated-for-negative-control' for key in (
                'source_media', 'source_sha256', 'product_version', 'capture_origin',
                'camera', 'resolution', 'state', 'clock', 'seed', 'color_pipeline', 'audio_environment')}
            row.update(card=card, seat=seat, source_kind='official_client_capture',
                       frames=[dict(phase=phase, file=str(WORK / 'capture-a' / f'{card}-seat{seat}-{phase}.png'),
                                    sha256='not-a-valid-sha', absolute_time=0, event_alignment='fabricated')
                               for phase in evidence.PHASES])
            official['sequences'].append(row)
    official_path = WORK / 'controls/fabricated-official.json'
    ORIGINAL_WRITE(official_path, official)
    DESTINATION = WORK / 'controls/complete-shape'
    evidence.assemble(WORK / 'capture-a', WORK / 'capture-b', official_path)
    shaped = evidence.read(DESTINATION / 'task-0004-visual-evidence.json')
    unknown_result(shaped)
    assert all(row['status'] == 'REQUIRES_PROVENANCE_AND_COMPARABILITY_REVIEW' for row in shaped['official_comparison'])

    wrong = deepcopy(official)
    wrong['sequences'][2]['card'] = 'EX1_124'
    assert evidence.official_gaps(wrong)[2]['status'] == 'MISSING'
    internal = deepcopy(official)
    internal['sequences'][0]['source_kind'] = 'project_generated'
    assert 'source_kind' in evidence.official_gaps(internal)[0]['missing']

    outcomes = []
    for mode in ('pixels', 'telemetry', 'absolute-time'):
        repeat = WORK / 'controls' / ('repeat-' + mode)
        repeat.mkdir(parents=True, exist_ok=True)
        report = deepcopy(evidence.read(WORK / 'capture-b/capture.json'))
        for sequence in report['sequences']:
            for frame in sequence['frames']:
                shutil.copyfile(WORK / 'capture-b' / frame['file'], repeat / frame['file'])
        frame = report['sequences'][0]['frames'][0]
        if mode == 'pixels':
            path = repeat / frame['file']
            with Image.open(path) as source:
                image = source.convert('RGBA')
            image.putpixel((0, 0), (255, 0, 0, 255))
            image.save(path)
        elif mode == 'telemetry':
            frame['step'] += 1
        else:
            frame['time'] += 1 / 240
        ORIGINAL_WRITE(repeat / 'capture.json', report)
        DESTINATION = WORK / 'controls' / ('result-' + mode)
        try:
            evidence.assemble(WORK / 'capture-a', repeat, ROOT / 'manifests/task-0004-official-input.json')
        except RuntimeError as error:
            assert str(error).startswith('Internal replay differs;')
        else:
            raise AssertionError('Tampered repeat passed: ' + mode)
        result = evidence.read(DESTINATION / 'task-0004-visual-evidence.json')
        unknown_result(result)
        assert not result['internal_repeatability_passed']
        mismatches = [row for row in result['internal_repeatability']
                      if not row['pixels_identical'] or not row['telemetry_identical']]
        assert len(mismatches) == 1
        outcomes.append({'mode': mode, 'mismatch': mismatches[0], 'expected_error_observed': True})
    ORIGINAL_WRITE(WORK / 'controls/result.json', {'complete_shape_still_unknown': True,
                   'wrong_card_missing': True, 'internal_source_missing': True, 'tamper_controls': outcomes})
    print('V004 BOUNDARIES PASS: complete fabricated shape Unknown; wrong card/internal source missing; pixel, step and absolute-time tamper each rejected (27/28)')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('prepare', 'assemble', 'boundaries'))
    # V004-r2 reuses the reviewed r1 probe in a separate evidence directory.
    parser.add_argument('--work', type=Path, default=WORK)
    args = parser.parse_args()
    command = args.command
    WORK = args.work.resolve()
    DESTINATION = WORK / 'manifests'
    configure()
    globals()[command]()
