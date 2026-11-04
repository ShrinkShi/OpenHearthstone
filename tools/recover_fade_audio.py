"""Bind original emitter components and clips for every cached fade program."""
import json
import argparse

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import recover


if __name__ == '__main__':
    exporter = RuntimeExport()
    parser = argparse.ArgumentParser()
    parser.add_argument('--loop', action='store_true', help='Bind the original looping action program set')
    args = parser.parse_args()
    label = 'loop' if args.loop else 'fade'
    report = json.loads((PROJECT / f'manifests/native-audio-{label}-refresh.json').read_text(encoding='utf-8'))
    records = []
    with asset_lock(exporter.imp.local):
        for entry in report['programs']:
            path = exporter.folder / entry['file']
            program = json.loads(path.read_text(encoding='utf-8'))
            audio = recover(exporter, program['reference'], include_program_hierarchy=True)
            if audio['source'] != program['source']:
                raise ValueError('Original audio/program identity mismatch: ' + entry['file'])
            filename = path.stem + f'.{label}-audio.json'
            atomic_json(exporter.folder / filename, audio)
            program['audio_file'] = filename
            atomic_json(path, program)
            records.append(dict(file=entry['file'], reference=program['reference'], source=audio['source'],
                                audio_file=filename, nodes=len(audio['nodes']), clips=len(audio['clips']), errors=audio['errors']))
            print('ORIGINAL '+label.upper()+' AUDIO', entry['file'], len(audio['nodes']), 'nodes;', len(audio['clips']), 'clips;', len(audio['errors']), 'errors', flush=True)
    atomic_json(PROJECT / f'manifests/native-audio-{label}-bindings.json', dict(programs=records,
        notes=['Original owned AudioSource/SoundDef/clip dependencies, not full SoundManager or all-card fidelity.']))
