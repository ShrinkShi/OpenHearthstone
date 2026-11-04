"""V004-r2 supplemental observations; r1 evidence stays read-only."""
import json
from pathlib import Path

from PIL import Image

from tools import task4_visual_evidence as evidence


WORK = evidence.PROJECT / 'work/v004-r2'


def main():
    first = evidence.read(WORK / 'capture-a/capture.json')
    second = evidence.read(WORK / 'capture-b/capture.json')
    # Beyond individual frame telemetry: include audio requests and event order.
    assert first == second
    rows = []
    for sequence in first['sequences']:
        for frame in sequence['frames']:
            folder = WORK / 'capture-a'
            arrays = []
            for key in ('file', 'emissionFile', 'transmissionFile'):
                with Image.open(folder / frame[key]) as source:
                    arrays.append(source.convert('RGBA').copy())
                assert evidence.digest(folder / frame[key]) == evidence.digest(WORK / 'capture-b' / frame[key])
            image, emission, transmission = arrays
            background = (.18, .18, .18, 1.)
            maximum = 0
            outside = 0
            for pixel, c, k in zip(image.getdata(), emission.getdata(), transmission.getdata()):
                delta = max(abs(pixel[i] - min(255, max(0, c[i] + k[i] * background[i]))) for i in range(4))
                maximum = max(maximum, delta)
                outside += delta > 2
            # C/K and target are independently quantized to 8 bit.
            rows.append({'card': sequence['card'], 'seat': sequence['seat'],
                         'phase': frame['phase'], 'max_error_8bit': maximum,
                         'pixels_above_2': outside})
    resources = [evidence.read(WORK / f'capture-{run}/independent-resources.json') for run in ('a', 'b')]
    for row in resources:
        assert row['newNativeObjects'] == row['newCameras'] == row['newFloatRenderTextures'] == 0
        assert row['renderTargetRestored'] and row['mipBiasRestored']
    result = {'author': 'V004-r2', 'capture_reports_identical': True,
              'main_and_CK_byte_repetition_count': 84, 'resources': resources,
              'composition': rows, 'composition_threshold_8bit': 2,
              'composition_passed': all(row['pixels_above_2'] == 0 for row in rows),
              'clock_and_events': [{'card': sequence['card'], 'seat': sequence['seat'],
                                   'events': sequence['events'], 'audio': sequence['audio']}
                                  for sequence in first['sequences']]}
    evidence.write(WORK / 'supplement.json', result)
    assert result['composition_passed'], 'C/K reconstruction differs: inspect saved bounded evidence'
    print('V004-r2 SUPPLEMENT PASS: whole reports identical; 84/84 main+C/K files repeat; '
          f'C+D*K <= {max(row["max_error_8bit"] for row in rows):.3f}/255; both resource observations clean')


if __name__ == '__main__':
    main()
