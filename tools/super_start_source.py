"""Read original start configuration and independently export source start graphs."""
import json
import argparse
from .importer import PROJECT
from .export_runtime import RuntimeExport
from .super_area_source import prefab_identity
from .native_effects import export_program
from .native_audio import recover as recover_audio, bind_program
from .asset_lock import atomic_json, asset_lock


def recover(importer, program):
    root = next((t for t in program.get('spell_transforms', []) if t['node'] == 0
                 and 'm_StartInfo' in t['raw']), None)
    if root is None:
        return None
    source = root['source']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    if raw != root['raw']:
        raise ValueError('Original root Spell changed')
    info = raw['m_StartInfo']
    config = None if info is None else dict(enabled=bool(info['m_Enabled']),
        **prefab_identity(importer, obj, info['m_Prefab'], program),
        use_super_location=bool(info['m_UseSuperSpellLocation']),
        death_after_missiles=bool(info['m_DeathAfterAllMissilesFire']),
        adjust_rotation=bool(info['m_AdjustRotation']), rotation=info['m_StartRotationAdjustment'], raw=info)
    return dict(node=0, kind=root['kind'], source=source, start=config, action=raw['m_ActionInfo'])


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--prefabs-only',action='store_true');args=parser.parse_args()
    ex = RuntimeExport()
    records = []
    if args.prefabs_only:
        records=json.loads((ex.folder/'native-super-start-config.json').read_text(encoding='utf-8'))['programs']
    else:
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program = json.loads(path.read_text(encoding='utf-8'))
            config = recover(ex.imp, program)
            if config is not None:
                records.append(dict(file=path.name, reference=program['reference'], config=config))
    result = dict(programs=records, full_super_spell_restored=False)
    if not args.prefabs_only:
        with asset_lock(ex.imp.local):
            atomic_json(ex.folder / 'native-super-start-config.json', result)
            atomic_json(PROJECT / 'manifests/original-super-start-config.json', result)
    refs = sorted({r['config']['start']['prefab_reference'] for r in records
                   if r['reference'].startswith(('Holy_Targeted_BlessingOfWisdom_FX.prefab:',
                                                  'Holy_Targeted_BlessingOfKings_FX.prefab:'))})
    prefabs = []
    with asset_lock(ex.imp.local):
        for ref in refs:
            if not ref:
                raise ValueError('Original start prefab reference is unrestored')
            filename, source = export_program(ex, ref, suffix='.start-spell.json')
            root, _, _ = ex.imp.reader.resolve(ref)
            audio = recover_audio(ex, ref)
            audio_file = ex.key(root) + '.start-audio.json'
            atomic_json(ex.folder / audio_file, audio)
            bind_program(ex, root, audio_file, suffix='.start-spell.json')
            prefabs.append(dict(reference=ref, file=filename, source=source, audio_file=audio_file,
                                audio_errors=audio['errors']))
        report = dict(prefabs=prefabs, full_super_spell_restored=False)
        atomic_json(ex.folder / 'native-super-start-prefabs.json', report)
        atomic_json(PROJECT / 'manifests/original-super-start-prefabs.json', report)
    ex.save()
    print('SOURCE START CONFIG:', len(records), 'programs;', len(prefabs), 'independent start prefabs', flush=True)


if __name__ == '__main__':
    main()
