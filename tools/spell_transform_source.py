"""Export original Spell placement for actor roots and complete effect hierarchies."""
import argparse
import json
from .asset_lock import atomic_json, asset_lock
from .importer import Importer, PROJECT
from .native_effects import pointer_key, source_program_nodes, source_spell_placements


def recover(importer, program):
    root, bundle, _ = importer.reader.resolve(program['reference'])
    return component_placements(importer, root, 0, bundle)


def component_placements(importer, root, node, bundle):
    return source_spell_placements(importer.reader, root, node, bundle)


def recover_effects(importer, program):
    root, _, _ = importer.reader.resolve(program['reference'])
    hierarchy, _, objects = source_program_nodes(importer.reader, root)
    if hierarchy != [(n['name'], n['parent'], n['path']) for n in program['nodes']]:
        raise ValueError('Source effect hierarchy changed: ' + program['reference'])
    result = []
    for node, obj in enumerate(objects):
        row = importer.store.db.execute('SELECT bundle FROM assets WHERE cab=? LIMIT 1',
                                        (pointer_key(obj)[0],)).fetchone()
        if row is None:
            raise ValueError('Source CAB bundle missing: ' + pointer_key(obj)[0])
        result.extend(component_placements(importer, obj, node, row['bundle']))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--kind', choices=('actors', 'effects'), default='actors')
    args = parser.parse_args()
    importer = Importer()
    records = []
    pattern = '*.actor-spell.json' if args.kind == 'actors' else '*.native-effect.json'
    manifest = 'original-spell-transform-source.json' if args.kind == 'actors' else 'original-effect-transform-source.json'
    with asset_lock(PROJECT / 'local'):
        for path in sorted((PROJECT / 'local/runtime').glob(pattern)):
            program = json.loads(path.read_text(encoding='utf-8'))
            transforms = (recover if args.kind == 'actors' else recover_effects)(importer, program)
            program['spell_transforms'] = transforms
            atomic_json(path, program)
            records.append(dict(file=path.name, reference=program['reference'], transforms=transforms))
        atomic_json(PROJECT / 'manifests' / manifest,
                    dict(programs=records, full_spell_controller_restored=False))
    print('SOURCE SPELL TRANSFORMS:', len(records), 'programs;',
          sum(len(r['transforms']) for r in records), 'Spell components')


if __name__ == '__main__':
    main()
