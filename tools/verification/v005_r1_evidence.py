"""Independently inspect run files, PNG headers, journal and actual RIFF samples."""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct


def inspect(folder, natural=False):
    run = json.loads((folder / 'run.json').read_text(encoding='utf-8'))
    result = {k: run[k] for k in ('dll_sha256', 'processes', 'result')}
    assert all(p['exit'] == 0 for p in run['processes'] if p['role'] != 'server')
    result['players'] = []
    for role in ('driver', 'observer'):
        report = json.loads((folder / role / 'report.json').read_text(encoding='utf-8'))
        player = {k: report[k] for k in ('role', 'checks', 'width', 'height', 'publicHash')}
        player['images'] = []
        for path in sorted((folder / role).glob('*.png')):
            raw = path.read_bytes()
            assert raw[:8] == b'\x89PNG\r\n\x1a\n'
            size = struct.unpack_from('>II', raw, 16)
            assert size == (report['width'], report['height'])
            player['images'].append({'file': str(path.relative_to(folder)), 'sha256': hashlib.sha256(raw).hexdigest()})
        wav = folder / role / 'scene-mix.wav'
        if wav.exists():
            raw = wav.read_bytes()
            assert raw[:4] == b'RIFF' and raw[8:12] == b'WAVE'
            assert struct.unpack_from('<I', raw, 4)[0] + 8 == len(raw)
            offset, fmt, payload = 12, None, None
            while offset + 8 <= len(raw):
                tag, length = struct.unpack_from('<4sI', raw, offset)
                data = raw[offset + 8:offset + 8 + length]
                if tag == b'fmt ':
                    fmt = struct.unpack_from('<HHIIHH', data)
                if tag == b'data':
                    payload = data
                offset += 8 + length + (length & 1)
            assert fmt[0] == 3 and fmt[1] == 2 and fmt[2] == 48000 and fmt[-1] == 32
            count, energy, peak, outside = 0, 0., 0., 0
            for (value,) in struct.iter_unpack('<f', payload):
                assert math.isfinite(value)
                count += 1
                energy += value * value
                peak = max(peak, abs(value))
                outside += abs(value) > 1
            rms = math.sqrt(energy / count)
            assert count // 2 == report['sampleFrames'] and outside == report['clippedSamples']
            assert abs(peak - report['peak']) < 1e-6 and abs(rms - report['rms']) < 1e-6
            assert report['callbacks'] * report['dspBuffer'] == report['sampleFrames'] and report['overflows'] == 0
            player['independent_dsp'] = dict(sha256=hashlib.sha256(raw).hexdigest(), frames=count // 2, peak=peak, rms=rms, outside_unit=outside)
        player['play_cues'] = [r for r in report['records'] if r['message'] == 'source play']
        player['stop_count'] = sum(r['message'] == 'source stop' for r in report['records'])
        result['players'].append(player)
    journals = list((folder / 'matches').glob('*.jsonl'))
    assert len(journals) == 1
    rows = [json.loads(s) for s in journals[0].read_text(encoding='utf-8').splitlines()]
    result['journal'] = {'kinds': dict(Counter(r['command']['kind'] for r in rows)), 'errors': [r['error'] for r in rows if r['error']]}
    state_file = next((folder / 'matches').glob('*-state.json'))
    state = json.loads(state_file.read_text(encoding='utf-8'))
    result['final_authority'] = {k: state['views'][0][k] for k in ('phase', 'version', 'turn', 'public_hash')}
    result['fixture'] = not natural
    if not result['fixture']:
        assert not result['journal']['errors'] and 'concede' not in result['journal']['kinds']
        assert all(v['phase'] == 'finished' for v in state['views'])
        assert all(p['publicHash'] == state['views'][0]['public_hash'] for p in result['players'])
    (folder / 'v005-independent-index.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(folder.name, json.dumps({k: result[k] for k in ('processes', 'journal', 'final_authority')}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--natural', action='store_true', help='Require an unmodified Match natural terminal run')
    parser.add_argument('folders', nargs='+', type=Path)
    args = parser.parse_args()
    for folder in args.folders:
        inspect(folder, args.natural)
