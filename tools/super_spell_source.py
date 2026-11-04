"""Export the two reviewed root branches; never execute the original assemblies."""
import json

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import bind_program, recover as recover_audio
from .native_effects import export_program
from .super_area_source import recover as recover_area, prefab_identity
from .super_impact_source import recover as recover_impact
from .super_missile_source import recover as recover_missile
from .smoke_source_textures import recover_texture


def recover(importer, program, card_id, filename):
    root = next(t for t in program['spell_transforms'] if t['node'] == 0)
    source = root['source']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    if raw != root['raw']:
        raise ValueError('Original root changed')
    if raw['m_ActionInfo'] != {'m_ShowSpellVisuals': 0, 'm_StartDelayMin': 0.0, 'm_StartDelayMax': 0.0}:
        raise NotImplementedError('Unreviewed action gate')
    missile = recover_missile(importer, program)['missile']
    chain = prefab_identity(importer, obj, raw['m_ChainInfo']['m_Prefab'], program)
    chain_info = chain_source_profile(raw['m_ChainInfo'], chain)
    return dict(card_id=card_id, file=filename, reference=program['reference'], source=source,
                location=raw['m_Location'], target_behavior=raw['m_TargetInfo']['m_Behavior'],
                start_enabled=bool(raw['m_StartInfo']['m_Enabled']),
                **chain_info,
                reverse_available=not missile['reverse_prefab']['prefab_null'],
                missile=missile['raw'], missile_reference=missile['prefab']['prefab_reference'],
                impact=recover_impact(importer, program)['impact'], area=recover_area(importer, program))


def chain_source_profile(raw, identity):
    """Preserve enabled and prefab-null as separate authored Chain facts."""
    enabled = bool(raw['m_Enabled'])
    return dict(chain_enabled=enabled, chain_prefab_null=identity['prefab_null'],
                chain_reference=identity['prefab_reference'], chain_source=identity['prefab_source'],
                chain_available=enabled and not identity['prefab_null'])


def main():
    ex = RuntimeExport()
    index = json.loads((ex.folder / 'native-effect-index.json').read_text(encoding='utf-8'))
    records = []
    for card in ('EX1_129', 'EX1_134'):
        entry = next(e for e in index['entries'] if e['card_id'] == card)
        program = json.loads((ex.folder / entry['file']).read_text(encoding='utf-8'))
        records.append(recover(ex.imp, program, card, entry['file']))
    prefabs = []
    refs = {area['prefab_reference'] for record in records for area in
            (record['area']['friendly'], record['area']['opponent']) if area and area['enabled'] and not area['prefab_null']}
    with asset_lock(ex.imp.local):
        for reference in sorted(refs):
            filename, source = export_program(ex, reference, suffix='.root-area-spell.json')
            obj, _, _ = ex.imp.reader.resolve(reference)
            audio_file = ex.key(obj) + '.root-area-audio.json'
            atomic_json(ex.folder / audio_file, recover_audio(ex, reference, include_program_hierarchy=True))
            bind_program(ex, obj, audio_file, suffix='.root-area-spell.json')
            prefabs.append(dict(reference=reference, file=filename, source=source))
        missiles = json.loads((ex.folder / 'native-super-missile-prefabs.json').read_text(encoding='utf-8'))['prefabs']
        for record in missiles:
            reference = record['reference']
            filename, source = export_program(ex, reference, suffix='.root-missile-spell.json', include_trails=True)
            obj, _, _ = ex.imp.reader.resolve(reference)
            audio_file = ex.key(obj) + '.root-missile-audio.json'
            atomic_json(ex.folder / audio_file, recover_audio(ex, reference, include_program_hierarchy=True))
            bind_program(ex, obj, audio_file, suffix='.root-missile-spell.json')
            prefabs.append(dict(reference=reference, file=filename, source=source))
        prefabs.extend(json.loads((ex.folder / 'native-super-impact.json').read_text(encoding='utf-8'))['prefabs'])
        textures = {}
        for prefab in prefabs:
            program = json.loads((ex.folder / prefab['file']).read_text(encoding='utf-8'))
            for node in program['nodes']:
                if not node.get('trail'):
                    continue
                for material in node['materials']:
                    source = material['source']
                    obj = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                    for name, saved in obj.read_typetree()['m_SavedProperties']['m_TexEnvs']:
                        if not saved['m_Texture']['m_PathID']:
                            continue
                        texture = ex.imp.reader.pointer(obj, saved['m_Texture'])
                        definition = recover_texture(ex, texture)
                        textures[definition['png']] = definition
        atomic_json(ex.folder / 'native-super-spell-textures.json', dict(textures=list(textures.values())))
        report = dict(programs=records, prefabs=prefabs, trail_textures=list(textures.values()), official_geometry_verified=False, whole_effect_verified=False)
        atomic_json(ex.folder / 'native-super-spell.json', report)
        atomic_json(PROJECT / 'manifests/original-super-spell.json', report)
    ex.save()
    print('SUPER SPELL ROOT:', len(records), 'roots;', len(prefabs), 'independent prefab identities')


if __name__ == '__main__':
    main()
