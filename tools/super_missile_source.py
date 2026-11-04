"""Recover original SuperSpell missile settings and independently cloned prefabs."""
import argparse
import json

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import recover as recover_audio, bind_program
from .native_effects import export_program
from .super_area_source import prefab_identity


def recover(importer, program):
    root = next((t for t in program.get('spell_transforms', []) if t['node'] == 0 and 'm_MissileInfo' in t['raw']), None)
    if root is None:
        return None
    source = root['source']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    if raw != root['raw']:
        raise ValueError('Original root Spell changed')
    info = raw['m_MissileInfo']
    if info is None:
        return dict(source=source, missile=None)
    return dict(source=source, missile=dict(raw=info,
        prefab=prefab_identity(importer, obj, info['m_Prefab'], program),
        reverse_prefab=prefab_identity(importer, obj, info['m_ReversePrefab'], program)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--prefabs-only', action='store_true')
    args = parser.parse_args()
    exporter = RuntimeExport()
    if args.prefabs_only:
        records = json.loads((exporter.folder / 'native-super-missile-config.json').read_text(encoding='utf-8'))['programs']
    else:
        records = []
        for path in sorted(exporter.folder.glob('*.native-effect.json')):
            program = json.loads(path.read_text(encoding='utf-8'))
            config = recover(exporter.imp, program)
            if config is not None:
                records.append(dict(file=path.name, reference=program['reference'], config=config))
        report = dict(programs=records, full_super_spell_restored=False)
        with asset_lock(exporter.imp.local):
            atomic_json(exporter.folder / 'native-super-missile-config.json', report)
            atomic_json(PROJECT / 'manifests/original-super-missile-config.json', report)
    index = json.loads((exporter.folder / 'native-effect-index.json').read_text(encoding='utf-8'))
    chosen = {e['file'] for e in index['entries'] if e['card_id'] in ('EX1_129', 'EX1_134')}
    refs = sorted({record['config']['missile'][kind]['prefab_reference'] for record in records
        if record['file'] in chosen and record['config']['missile'] is not None
        for kind in ('prefab', 'reverse_prefab') if not record['config']['missile'][kind]['prefab_null']})
    prefabs = []
    with asset_lock(exporter.imp.local):
        for reference in refs:
            if not reference:
                raise ValueError('Original missile prefab reference is unresolved')
            filename, source = export_program(exporter, reference, suffix='.missile-spell.json')
            obj, _, _ = exporter.imp.reader.resolve(reference)
            audio = recover_audio(exporter, reference, include_program_hierarchy=True)
            audio_file = exporter.key(obj) + '.missile-audio.json'
            atomic_json(exporter.folder / audio_file, audio)
            bind_program(exporter, obj, audio_file, suffix='.missile-spell.json')
            prefabs.append(dict(reference=reference, file=filename, source=source, audio_file=audio_file, audio_errors=audio['errors']))
        report = dict(prefabs=prefabs, full_super_spell_restored=False)
        atomic_json(exporter.folder / 'native-super-missile-prefabs.json', report)
        atomic_json(PROJECT / 'manifests/original-super-missile-prefabs.json', report)
    exporter.save()
    print('ORIGINAL SUPER MISSILE', len(records), 'source roots;', len(prefabs), 'independent missile prefabs', flush=True)


if __name__ == '__main__':
    main()
