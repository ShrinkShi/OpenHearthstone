"""Read-only source recovery for the standard HERO_08bp; original DLLs never execute."""
import argparse
import json

from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import export_program
from .native_audio import recover as recover_audio, bind_program
from .super_spell_source import recover as recover_root
from .super_area_source import prefab_identity
from .super_start_source import recover as recover_start
from .smoke_source_textures import recover_texture


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--export', action='store_true')
    args = parser.parse_args()
    ex = RuntimeExport()
    definition = ex.imp.reader.definition('HERO_08bp')
    reference = definition['m_PlayEffectDef']['m_SpellPath']
    root, _, _ = ex.imp.reader.resolve(reference)
    components = []
    for obj, tree in ex.imp.reader.walk(root, include_visual=False):
        if obj.type.name != 'MonoBehaviour':
            continue
        tree = tree or obj.read_typetree()
        script = ex.imp.reader.pointer(obj, tree['m_Script'])
        kind = script.read_typetree()['m_ClassName'] if script else ''
        if kind in ('SuperSpell', 'Spell'):
            components.append(dict(kind=kind, raw=tree))
    arrows = [dict(row) for row in ex.imp.store.db.execute(
        "SELECT name,type,bundle,cab,pathid,guids FROM assets WHERE name LIKE '%TargetArrow%' OR name LIKE '%TargetRetic%' OR name LIKE '%TargetingArrow%'")]
    report = dict(card_id='HERO_08bp', definition=definition, reference=reference,
                  components=components, arrow_candidates=arrows,
                  official_comparison_verified=False, whole_effect_verified=False)
    with asset_lock(ex.imp.local):
        if args.export:
            filename, source = export_program(ex, reference, suffix='.fireblast-root.json', include_trails=True)
            program = json.loads((ex.folder / filename).read_text(encoding='utf-8'))
            report.update(file=filename, source=source, config=recover_root(ex.imp, program, 'HERO_08bp', filename))
            report['config']['start'] = recover_start(ex.imp, program)['start']
            prefabs = []
            for kind, ref in (
                    ('start', report['config']['start']['prefab_reference']),
                    ('missile', report['config']['missile_reference']),
                    ('impact', report['config']['impact']['prefab']['prefab_reference'])):
                suffix = '.fireblast-' + kind + '.json'
                exported, audit = export_program(ex, ref, suffix=suffix, include_trails=True, include_component_effects=True)
                obj, _, _ = ex.imp.reader.resolve(ref)
                audio = recover_audio(ex, ref, include_program_hierarchy=True)
                audio_file = ex.key(obj) + '.fireblast-' + kind + '-audio.json'
                atomic_json(ex.folder / audio_file, audio)
                bind_program(ex, obj, audio_file, suffix=suffix)
                prefabs.append(dict(reference=ref, file=exported, source=audit,
                                    audio_file=audio_file, audio_errors=audio['errors']))
            report['prefabs'] = prefabs
            textures = {}
            for prefab in prefabs:
                graph = json.loads((ex.folder / prefab['file']).read_text(encoding='utf-8'))
                for node in graph['nodes']:
                    for material in node['materials']:
                        source = material['source']
                        owner = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                        for name, saved in owner.read_typetree()['m_SavedProperties']['m_TexEnvs']:
                            if not saved['m_Texture']['m_PathID']:
                                continue
                            texture = ex.imp.reader.pointer(owner, saved['m_Texture'])
                            if texture is not None and texture.type.name == 'Texture2D':
                                definition = recover_texture(ex, texture)
                                textures[definition['png']] = definition
            report['textures'] = list(textures.values())
            atomic_json(ex.folder / 'native-fireblast-textures.json', dict(textures=report['textures']))
            atomic_json(ex.folder / 'native-fireblast.json', dict(programs=[report['config']], prefabs=prefabs))
            path = ex.folder / 'native-effect-index.json'
            index = json.loads(path.read_text(encoding='utf-8'))
            index['entries'] = [e for e in index['entries'] if e['card_id'] != 'HERO_08bp']
            index['entries'].append(dict(card_id='HERO_08bp', file=filename, enabled=False))
            atomic_json(path, index)
        atomic_json(ex.folder / 'fireblast-source.json', report)
    ex.save()
    print(json.dumps(dict(reference=reference, file=report.get('file'),
                          prefabs=report.get('prefabs')), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
