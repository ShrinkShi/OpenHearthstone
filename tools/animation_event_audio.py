"""Recover existing AudioSources used by original animation event receivers."""
import json
from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import recover


def export(ex, path):
    program = json.loads(path.read_text(encoding='utf-8'))
    audio = recover(ex, program['reference'])
    filename = path.name.replace('.actor-spell.json', '.event-audio.json')
    atomic_json(ex.folder / filename, audio)
    program['event_audio_file'] = filename
    atomic_json(path, program)
    return dict(file=path.name, audio_file=filename, source=audio['source'],
                nodes=audio['nodes'], clips=audio['clips'], errors=audio['errors'])


def main():
    ex = RuntimeExport()
    states = json.loads((PROJECT / 'manifests/original-spell-state-playback.json').read_text(encoding='utf-8'))
    records = [export(ex, ex.folder / record['file']) for record in states['programs']]
    atomic_json(PROJECT / 'manifests/original-animation-event-audio.json',
                dict(programs=records, full_sound_manager_restored=False,
                     notes=['Exact existing AudioSource/SoundDef recovery; global category, ducking and repeated source extension semantics remain incomplete.']))
    print('ANIMATION EVENT AUDIO:', len(records), 'programs;',
          sum(len(r['nodes']) for r in records), 'audio nodes;',
          sum(len(r['clips']) for r in records), 'clip references;',
          sum(len(r['errors']) for r in records), 'errors')


if __name__ == '__main__':
    main()
