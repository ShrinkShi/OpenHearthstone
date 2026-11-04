"""Normalize explicitly selected Unity ShapeModule.radius cache fields."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .asset_lock import asset_lock, atomic_json
from .importer import PROJECT
from .native_effects import shape_radius_value


def normalize_program(program):
    nodes = program.get('nodes')
    if not isinstance(nodes, list):
        raise ValueError('native-effect cache has no nodes array')
    converted = 0
    scalars = 0
    for node in nodes:
        if not isinstance(node, dict):
            raise ValueError('native-effect cache contains a non-object node')
        particle = node.get('particle')
        if particle is None:
            continue
        if not isinstance(particle, dict):
            raise ValueError('native-effect cache contains a non-object particle')
        shape = particle.get('shape')
        if shape is None:
            continue
        if not isinstance(shape, dict):
            raise ValueError('native-effect cache contains a non-object shape')
        if 'radius' not in shape:
            continue
        radius = shape['radius']
        value = shape_radius_value(radius)
        if isinstance(radius, dict):
            shape['radius'] = value
            converted += 1
        else:
            scalars += 1
    return {'converted': converted, 'already_scalar': scalars}


def _inside_runtime(runtime, file_name):
    path = Path(file_name)
    if not path.is_absolute():
        path = runtime / path
    path = path.resolve()
    try:
        path.relative_to(runtime.resolve())
    except ValueError as error:
        raise ValueError(f'cache file must stay under {runtime}: {file_name}') from error
    if path.suffix.lower() != '.json':
        raise ValueError(f'cache file must be JSON: {file_name}')
    return path


def resolve_files(runtime_dir, files=(), card_ids=()):
    runtime = Path(runtime_dir)
    selected = []
    if files:
        selected.extend(files)
    if card_ids:
        index_path = runtime / 'native-effect-index.json'
        index = json.loads(index_path.read_text(encoding='utf-8'))
        entries = index.get('entries')
        if not isinstance(entries, list):
            raise ValueError(f'{index_path} has no entries array')
        by_card = {row.get('card_id'): row for row in entries if isinstance(row, dict)}
        for card_id in card_ids:
            row = by_card.get(card_id)
            if row is None or not row.get('file'):
                raise ValueError(f'no native-effect cache is indexed for {card_id}')
            selected.append(row['file'])
    if not selected:
        raise ValueError('select at least one cache with --files or --card-ids')
    paths = [_inside_runtime(runtime, name) for name in selected]
    unique = list(dict.fromkeys(paths))
    for path in unique:
        if not path.is_file():
            raise FileNotFoundError(path)
    return unique


def migrate_files(runtime_dir, files=(), card_ids=(), dry_run=False):
    runtime = Path(runtime_dir)
    paths = resolve_files(runtime, files, card_ids)

    def plan_and_write():
        planned = []
        for path in paths:
            data = json.loads(path.read_text(encoding='utf-8'))
            counts = normalize_program(data)
            planned.append((path, data, counts))
        if not dry_run:
            for path, data, counts in planned:
                if counts['converted']:
                    atomic_json(path, data)
        return [
            {'file': str(path.relative_to(runtime.resolve())), **counts}
            for path, _, counts in planned
        ]

    if dry_run:
        return plan_and_write()
    with asset_lock(runtime.parent):
        return plan_and_write()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-dir', type=Path, default=PROJECT / 'local' / 'runtime')
    parser.add_argument('--files', nargs='+', action='append', default=[], metavar='FILE',
                        help='exact cache file relative to --runtime-dir; repeatable')
    parser.add_argument('--card-ids', nargs='+', action='append', default=[], metavar='CARD_ID',
                        help='resolve exact files through native-effect-index.json; repeatable')
    parser.add_argument('--dry-run', action='store_true', help='validate and report without writing')
    args = parser.parse_args(argv)
    files = [name for group in args.files for name in group]
    card_ids = [card_id for group in args.card_ids for card_id in group]
    try:
        report = migrate_files(args.runtime_dir, files, card_ids, args.dry_run)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        parser.error(str(error))
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
