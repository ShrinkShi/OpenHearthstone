"""Match the source explosion waveform against the actual Unity Listener capture."""
import argparse
import json
import math
from pathlib import Path

from .fireblast_evidence import PROJECT, wav_metadata


def mono(path, rate=4000):
    metadata, values = wav_metadata(path)
    channels, source_rate = metadata['channels'], metadata['rate']
    scale = 1 if metadata['encoding'] == 3 else 32768
    samples = []
    for index in range(int(metadata['duration'] * rate)):
        position = index * source_rate / rate
        first = int(position)
        second = min(first + 1, metadata['frames'] - 1)
        fraction = position - first
        samples.append(sum((values[first * channels + channel] * (1 - fraction) +
                            values[second * channels + channel] * fraction) / scale
                           for channel in range(channels)) / channels)
    return metadata, samples


def correlate(mix, source, expected, rate=4000):
    length = min(len(source), int(.128 * rate))
    limit = min(len(source) - length, int(.4 * rate))
    offset = max(range(0, max(1, limit + 1), 8),
                 key=lambda start: sum(value * value for value in source[start:start + length]))
    template = source[offset:offset + length]
    mean = sum(template) / length
    template = [value - mean for value in template]
    energy = sum(value * value for value in template)
    if energy <= 0:
        raise ValueError('Explosion template is silent')
    low = max(0, round((expected - .2) * rate) + offset)
    high = min(len(mix) - length, round((expected + .2) * rate) + offset)
    if low > high:
        raise ValueError('Expected explosion lies outside the Listener recording')
    best = None
    for start in range(low, high + 1, 2):
        window = mix[start:start + length]
        total = sum(window)
        window_energy = sum(value * value for value in window) - total * total / length
        dot = sum(a * b for a, b in zip(template, window))
        correlation = dot / math.sqrt(energy * window_energy) if window_energy > 0 else 0
        if best is None or correlation > best['correlation']:
            best = dict(correlation=correlation, projection_gain=dot / energy,
                        capture_start=(start - offset) / rate, template_offset=offset / rate,
                        source_rms=math.sqrt(energy / length), mix_rms=math.sqrt(max(0, window_energy) / length))
    return best


def analyze(folder):
    folder = Path(folder)
    report = json.loads((folder / 'report.json').read_text(encoding='utf-8'))
    records = report['records']
    capture_start = next(row['time'] for row in records if row['phase'] == 'hover' and row['message'] == 'enter')
    hit = next(row['time'] for row in records if row['message'].startswith('actual Hit callback'))
    source_files = {}
    for name in ('38dc39eff4ccb3616e7b89ea', 'd4e7853ed281083a4e54bf76', '866ce32403a1e84d9b7cced8'):
        sound = json.loads((PROJECT / 'local/runtime' / (name + '.native-sound.json')).read_text(encoding='utf-8'))
        for clip in sound['clips']:
            source_files[clip['file']] = clip['reference']
    explosions = [row for row in records if row['message'] == 'source play' and row.get('file') in source_files]
    if len(explosions) != 1:
        raise ValueError(f'Expected one original explosion callback, found {len(explosions)}')
    cue = explosions[0]
    mix_metadata, mix = mono(folder / 'scene-mix.wav')
    _, source = mono(PROJECT / 'local/runtime' / cue['file'])
    match = correlate(mix, source, cue['time'] - capture_start)
    result = dict(source_reference=source_files[cue['file']], file=cue['file'],
                  callback_after_hit_ms=1000 * (cue['time'] - hit),
                  listener_signal_detected=match['correlation'] >= .4, **match,
                  pcm_sha256=mix_metadata['sha256'], sample_rate=mix_metadata['rate'],
                  scope='Unity Listener PCM; recording anchor uses the adjacent hover-enter timestamp; no device loopback')
    (folder / 'hunter-audio-correlation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    print(json.dumps(analyze(parser.parse_args().folder), indent=2))
