"""Export independently cloned source area prefabs without altering approved graphs."""
import json
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import export_program
from .native_audio import recover as recover_audio, bind_program


def main():
    ex = RuntimeExport()
    index = json.loads((ex.folder / 'native-effect-index.json').read_text(encoding='utf-8'))
    configs = json.loads((ex.folder / 'native-super-area-config.json').read_text(encoding='utf-8'))
    files = {entry['file'] for entry in index['entries'] if entry['card_id'] in ('CS2_025', 'CS2_062')}
    refs = sorted({info['prefab_reference'] for record in configs['programs'] if record['file'] in files
                   for info in (record['config']['friendly'], record['config']['opponent'])
                   if info is not None and info['enabled'] and not info['prefab_null']})
    records = []
    with asset_lock(ex.imp.local):
        for ref in refs:
            if not ref:
                raise ValueError('Original area prefab reference is unrestored')
            filename, source = export_program(ex, ref, suffix='.area-spell.json')
            root, _, _ = ex.imp.reader.resolve(ref)
            audio = recover_audio(ex, ref)
            audio_file = ex.key(root) + '.area-audio.json'
            atomic_json(ex.folder / audio_file, audio)
            bind_program(ex, root, audio_file, suffix='.area-spell.json')
            records.append(dict(reference=ref, file=filename, source=source, audio_file=audio_file,
                                audio_errors=audio['errors']))
        report = dict(prefabs=records, full_super_spell_restored=False)
        atomic_json(ex.folder / 'native-super-area-prefabs.json', report)
        atomic_json(PROJECT / 'manifests/original-super-area-prefabs.json', report)
    ex.save()
    print('SOURCE CLONED AREA PREFABS:', len(records))


if __name__ == '__main__':
    main()
