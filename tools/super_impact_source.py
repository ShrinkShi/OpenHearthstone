"""Read the two sample SuperSpell impact configurations and export their clones."""
import json

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import bind_program, recover as recover_audio
from .native_effects import export_program
from .super_area_source import prefab_identity


def recover(importer, program):
    root = next(t for t in program['spell_transforms'] if t['node'] == 0)
    source = root['source']
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    if raw != root['raw']:
        raise ValueError('Original root Spell changed')
    info = raw['m_ImpactInfo']
    if info['m_damageAmountImpactSpells']:
        raise NotImplementedError('This source exporter only covers the two samples with empty damage ranges')
    return dict(source=source, raw=info, impact=dict(
        enabled=bool(info['m_Enabled']), prefab=prefab_identity(importer, obj, info['m_Prefab'], program),
        ranges=[], use_super_location=bool(info['m_UseSuperSpellLocation']), location=info['m_Location'],
        parent_to_location=bool(info['m_SetParentToLocation']), delay_min=info['m_SpawnDelaySecMin'],
        delay_max=info['m_SpawnDelaySecMax'], spawn_offset=info['m_SpawnOffset'],
        game_delay_min=info['m_GameDelaySecMin'], game_delay_max=info['m_GameDelaySecMax'],
        adjust_rotation=bool(info['m_AdjustRotation']), rotation=info['m_ImpactRotationAdjustment']))


def main():
    exporter = RuntimeExport()
    index = json.loads((exporter.folder / 'native-effect-index.json').read_text(encoding='utf-8'))
    records = []
    for card in ('EX1_129', 'EX1_134'):
        entry = next(e for e in index['entries'] if e['card_id'] == card)
        program = json.loads((exporter.folder / entry['file']).read_text(encoding='utf-8'))
        records.append(dict(card_id=card, file=entry['file'], reference=program['reference'],
                            config=recover(exporter.imp, program)))
    prefabs = []
    references = {r['config']['impact']['prefab']['prefab_reference'] for r in records}
    with asset_lock(exporter.imp.local):
        for reference in sorted(references):
            if not reference:
                raise ValueError('Original impact prefab is unresolved')
            filename, source = export_program(exporter, reference, suffix='.impact-spell.json')
            obj, _, _ = exporter.imp.reader.resolve(reference)
            audio = recover_audio(exporter, reference, include_program_hierarchy=True)
            audio_file = exporter.key(obj) + '.impact-audio.json'
            atomic_json(exporter.folder / audio_file, audio)
            bind_program(exporter, obj, audio_file, suffix='.impact-spell.json')
            prefabs.append(dict(reference=reference, file=filename, source=source,
                                audio_file=audio_file, audio_errors=audio['errors']))
        report = dict(programs=records, prefabs=prefabs, full_super_spell_restored=False)
        atomic_json(exporter.folder / 'native-super-impact.json', report)
        atomic_json(PROJECT / 'manifests/original-super-impact.json', report)
    exporter.save()
    print('ORIGINAL SUPER IMPACT:', len(records), 'sample roots;', len(prefabs), 'independent prefabs', flush=True)


if __name__ == '__main__':
    main()
