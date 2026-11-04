"""Recover exact area prefab references and configuration for existing SuperSpells."""
import json
from .asset_lock import atomic_json, asset_lock
from .importer import Importer, PROJECT
from .native_effects import pointer_key


def prefab_identity(importer, obj, pointer, program):
    prefab = importer.reader.pointer(obj, pointer)
    reference, identity, node = '', None, -1
    if prefab is not None:
        cab, pathid = pointer_key(prefab)
        row = importer.store.db.execute('SELECT bundle FROM assets WHERE cab=? LIMIT 1', (cab,)).fetchone()
        identity = dict(cab=cab, pathid=str(pathid), bundle=row['bundle'] if row else '', type=prefab.type.name)
        node = next((t['node'] for t in program['spell_transforms']
                     if (t['source']['cab'], int(t['source']['pathid'])) == (cab, pathid)), -1)
        go = importer.reader.pointer(prefab, prefab.read_typetree()['m_GameObject'])
        row = importer.store.db.execute('SELECT name,guids FROM assets WHERE cab=? AND pathid=?',
                                        (go.assets_file.name, str(go.path_id))).fetchone()
        if row:
            for guid in json.loads(row['guids']):
                candidate = row['name'] + '.prefab:' + guid
                resolved, _, _ = importer.reader.resolve(candidate)
                if pointer_key(resolved) == pointer_key(go):
                    reference = candidate
                    break
    return dict(prefab_null=prefab is None, prefab_source=identity, prefab_reference=reference, prefab_node=node)


def recover(importer, program):
    root = next((t for t in program.get('spell_transforms', []) if t['node'] == 0
                 and 'm_FriendlyAreaEffectInfo' in t['raw']), None)
    if root is None:
        return None
    source = root['source']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    if raw != root['raw']:
        raise ValueError('Original root component changed')
    def area(slot):
        info = raw[slot]
        if info is None:
            return None
        return dict(enabled=bool(info['m_Enabled']), **prefab_identity(importer, obj, info['m_Prefab'], program),
            use_super_location=bool(info['m_UseSuperSpellLocation']), location=info['m_Location'],
            parent_to_location=bool(info['m_SetParentToLocation']), facing=info['m_Facing'],
            rotate_x=bool(info['m_FacingOptions']['m_RotateX']), rotate_y=bool(info['m_FacingOptions']['m_RotateY']),
            delay_min=info['m_SpawnDelaySecMin'], delay_max=info['m_SpawnDelaySecMax'], raw=info)
    return dict(node=0, kind=root['kind'], source=source,
                friendly=area('m_FriendlyAreaEffectInfo'), opponent=area('m_OpponentAreaEffectInfo'))


def main():
    importer = Importer()
    records = []
    for path in sorted((PROJECT / 'local/runtime').glob('*.native-effect.json')):
        program = json.loads(path.read_text(encoding='utf-8'))
        config = recover(importer, program)
        if config is not None:
            records.append(dict(file=path.name, reference=program['reference'], config=config))
    result = dict(programs=records, full_super_spell_restored=False)
    with asset_lock(PROJECT / 'local'):
        atomic_json(PROJECT / 'local/runtime/native-super-area-config.json', result)
        atomic_json(PROJECT / 'manifests/original-super-area-config.json', result)
    print('SOURCE SUPER AREA CONFIG:', len(records), 'programs')


if __name__ == '__main__':
    main()
