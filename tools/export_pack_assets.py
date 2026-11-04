"""Export native pack previews and wallet icons through RuntimeExport."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from game.accounts.catalog import get_catalog

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport


PROJECT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = PROJECT / 'manifests' / 'pack-native-assets.json'
RUNTIME_VISUALS_NAME = 'pack-visuals.json'

SOURCE_PRESENTATIONS = {
    'native_3d_pack': {'official_pack_model', 'themed_pack_model_derived',
                       'class_pack_model_derived'},
    'adventure_book_model': {'adventure_book_model_derived'},
    'related_theme_book_model': {'related_theme_book_derived'},
}


class PackAssetExportError(RuntimeError):
    """Raised when a manifest source cannot be resolved exactly."""


def load_manifest(path: Path = MANIFEST_PATH) -> dict:
    return json.loads(path.read_text(encoding='utf-8'))


def _asset_ref_error(reference: dict, label: str, require_guid: bool = False) -> str:
    required = ('asset_id', 'name', 'bundle', 'cab', 'pathid')
    missing = [key for key in required if not reference.get(key)]
    if require_guid and not reference.get('guid'):
        missing.append('guid')
    if missing:
        return f'{label} missing {", ".join(missing)}'
    return ''


def validate_manifest(manifest: dict, expected_sets) -> None:
    packs = manifest.get('packs')
    if not isinstance(packs, list):
        raise ValueError('pack manifest must contain a packs list')
    ids = [int(entry['set_id']) for entry in packs]
    if len(ids) != len(set(ids)):
        raise ValueError('pack manifest contains duplicate set_id values')
    expected = set(map(int, expected_sets))
    missing = sorted(expected - set(ids))
    if missing:
        raise ValueError(f'pack manifest coverage mismatch; missing={missing}')

    sources = manifest.get('source_assets', {})
    for entry in packs:
        set_id = int(entry['set_id'])
        if set_id not in expected:
            continue
        source_set_id = int(entry.get('source_asset_set_id', set_id))
        if source_set_id != set_id:
            raise ValueError(f'set {set_id} must use its exact source set; implicit fallback is forbidden')
        source = sources.get(str(source_set_id))
        if not source:
            raise ValueError(f'set {set_id} references missing source asset set {source_set_id}')

        source_type = entry.get('source_type')
        if source_type != source.get('source_kind') or source_type not in SOURCE_PRESENTATIONS:
            raise ValueError(f'set {set_id} source_type does not match its indexed source kind')
        presentation_kind = entry.get('presentation_kind')
        if presentation_kind not in SOURCE_PRESENTATIONS[source_type]:
            raise ValueError(f'set {set_id} has invalid presentation_kind for {source_type}')
        if presentation_kind != 'official_pack_model' and not entry.get('presentation_note'):
            raise ValueError(f'set {set_id} derived presentation must explain its source boundary')

        logical = entry.get('logical_ids', {})
        if logical.get('scene') != f'MODEL_PACK_{set_id}' or logical.get('ui') != f'UI_PACK_{set_id}':
            raise ValueError(f'set {set_id} has invalid logical IDs')
        render = entry.get('render', {})
        primary_path = render.get('primary_node_path')
        if (not render.get('primary_node_name') or not isinstance(primary_path, list)
                or not primary_path or primary_path[-1] != render['primary_node_name']):
            raise ValueError(f'set {set_id} has no exact primary node path')
        if primary_path[0] != source['game_object']['name']:
            raise ValueError(f'set {set_id} primary node path does not start at its source GameObject')
        if render.get('include_primary_mesh_only') is not True:
            raise ValueError(f'set {set_id} must explicitly select the primary mesh only')
        camera_axes = manifest.get('render_contract', {}).get('verified_pack_axes_by_set', {}).get(str(set_id))
        if camera_axes and camera_axes != {'front_axis': '-Y', 'up_axis': '-Z'}:
            raise ValueError(f'set {set_id} has an invalid verified front/up axis mapping')
        if presentation_kind == 'official_pack_model':
            if not camera_axes:
                raise ValueError(f'set {set_id} requires reviewed front/up axes from its primary mesh UVs')
        camera = render.get('camera_suggestion', '').lower()
        if 'orthographic' not in camera or 'ignore source world position' not in camera or 'uv' not in camera:
            raise ValueError(f'set {set_id} camera suggestion must describe bounds-fit and UV-based framing')

        for part in ('game_object', 'mesh', 'material', 'texture'):
            ref = source.get(part, {})
            error = _asset_ref_error(
                ref, f'set {source_set_id} {part}',
                require_guid=(part == 'game_object' and source_type == 'native_3d_pack'),
            )
            if error:
                raise ValueError(error)
        root_name = source['game_object']['name'].lower()
        if any(token in root_name for token in ('silhouette', 'golden', 'special', 'cardback')):
            raise ValueError(f'set {source_set_id} maps to a prohibited pack variant: {root_name}')

    currencies = {item.get('logical_id'): item for item in manifest.get('currency_assets', [])}
    for logical_id in ('UI_GOLD', 'UI_DUST'):
        item = currencies.get(logical_id)
        if not item:
            raise ValueError(f'missing currency asset {logical_id}')
        error = _asset_ref_error(item.get('source', {}), logical_id, require_guid=True)
        if error:
            raise ValueError(error)
    gold, dust = currencies['UI_GOLD'], currencies['UI_DUST']
    if (gold['source']['type'], gold['source']['name'], gold['export_kind']) != (
            'Sprite', 'large_coin_0', 'sprite_crop_png'):
        raise ValueError('UI_GOLD must use the in-game gold coin Sprite, not premium currency art')
    if (dust['source']['type'], dust['source']['name'], dust['export_kind']) != (
            'Texture2D', 'DustJar_Icon', 'texture_png'):
        raise ValueError('UI_DUST must use the original Arcane Dust jar icon')


def _verify_indexed_ref(exporter, reference: dict, label: str, expected_type: str | None = None) -> dict:
    row = exporter.imp.store.db.execute(
        'SELECT * FROM assets WHERE id=?', (reference['asset_id'],)
    ).fetchone()
    if row is None:
        raise KeyError(f'{label}: source asset id not present in the existing index: {reference["asset_id"]}')
    for key in ('bundle', 'cab', 'pathid', 'name'):
        if str(row[key]) != str(reference[key]):
            raise ValueError(
                f'{label}: indexed {key} mismatch for {reference["asset_id"]}: '
                f'{row[key]!r} != {reference[key]!r}'
            )
    expected_type = expected_type or reference.get('type')
    if expected_type and row['type'] != expected_type:
        raise ValueError(f'{label}: indexed type mismatch for {reference["asset_id"]}')
    guid = reference.get('guid')
    if guid and guid not in json.loads(row['guids'] or '[]'):
        raise ValueError(f'{label}: indexed GUID mismatch for {reference["asset_id"]}')
    return row


def _verify_scene_selector(exporter, source: dict, label: str) -> None:
    game_object = source['game_object']
    if game_object.get('guid'):
        return
    rows = exporter.imp.store.db.execute(
        'SELECT * FROM assets WHERE name=? AND type="GameObject"', (game_object['name'],)
    ).fetchall()
    selected = next((row for row in rows if row['bundle'] == game_object['bundle']), None)
    if selected is None or selected['id'] != game_object['asset_id']:
        raise ValueError(f'{label}: RuntimeExport.scene name/bundle selector is not unique to the exact source')


def _verify_exported_primary_node(exporter, pack: dict) -> None:
    scene_id = pack['logical_ids']['scene']
    scene_file = exporter.registry.get(scene_id)
    if not scene_file:
        raise ValueError('scene Registry entry is missing')
    scene_path = exporter.folder / scene_file
    if not scene_path.is_file():
        raise ValueError(f'SceneData file is missing: {scene_path.name}')
    nodes = json.loads(scene_path.read_text(encoding='utf-8')).get('nodes', [])
    expected_path = pack['render']['primary_node_path']
    for index, node in enumerate(nodes):
        path = []
        current = index
        seen = set()
        while current >= 0 and current not in seen and current < len(nodes):
            seen.add(current)
            path.append(nodes[current].get('name', ''))
            current = int(nodes[current].get('parent', -1))
        if path[::-1] == expected_path and node.get('mesh'):
            return
    raise ValueError(f'primary mesh path not found in SceneData: {"/".join(expected_path)}')


def _export_exact_asset(exporter, logical_id: str, reference: dict) -> bool:
    row = _verify_indexed_ref(exporter, reference, logical_id)
    source_path = exporter.imp.export(reference['asset_id'])
    target = exporter.folder / source_path.name
    target.write_bytes(source_path.read_bytes())
    exporter.registry[logical_id] = target.name
    exporter.provenance.append({
        'file': target.name,
        'asset_id': row['id'],
        'bundle': row['bundle'],
        'cab': row['cab'],
        'pathid': row['pathid'],
        'type': row['type'],
    })
    return True


def build_runtime_visuals(manifest: dict, registry: dict, exported_scenes: set[str], packs=None) -> dict:
    sources = manifest['source_assets']
    camera_contract = manifest['render_contract']['camera']
    camera_axes_by_set = manifest['render_contract'].get('verified_pack_axes_by_set', {})
    entries = []
    if packs is None:
        current_set_ids = set(map(int, get_catalog().pack_sets))
        selected_packs = [pack for pack in manifest['packs']
                          if int(pack['set_id']) in current_set_ids]
    else:
        selected_packs = packs
    for pack in selected_packs:
        set_id = int(pack['set_id'])
        source = sources[str(pack.get('source_asset_set_id', set_id))]
        source_ref = source['game_object']
        scene_id = pack['logical_ids']['scene']
        ui_id = pack['logical_ids']['ui']
        camera_axes = camera_axes_by_set.get(str(set_id), {})
        entries.append({
            'set_id': set_id,
            'model_id': scene_id,
            'ui_id': ui_id,
            'source_type': pack['source_type'],
            'presentation_kind': pack['presentation_kind'],
            'product_variant': pack.get('product_variant', ''),
            'presentation_note': pack.get('presentation_note', ''),
            'source_set_id': int(pack.get('source_asset_set_id', set_id)),
            'source_asset_id': source_ref['asset_id'],
            'source_asset_name': source_ref['name'],
            'primary_node_name': pack['render']['primary_node_name'],
            'primary_node_path': pack['render']['primary_node_path'],
            'include_primary_mesh_only': pack['render']['include_primary_mesh_only'],
            'excluded_node_names': pack['render'].get('excluded_node_names', []),
            'camera_suggestion': pack['render']['camera_suggestion'],
            'front_axis': camera_axes.get('front_axis', 'unknown'),
            'up_axis': camera_axes.get('up_axis', ''),
            'front_axis_note': camera_contract['front_axis_note'],
            'scene_file': registry.get(scene_id, ''),
            'exported': scene_id in exported_scenes and ui_id in registry,
        })
    return {'schema_version': 1, 'entries': entries}


def update_runtime_pack_metadata(manifest=None, local: Path | None = None) -> dict:
    """Update reviewed pack camera axes without exporting SceneData or textures."""
    manifest = manifest or load_manifest()
    local = local or PROJECT / 'local'
    runtime_folder = local / 'runtime'
    visuals_path = runtime_folder / RUNTIME_VISUALS_NAME
    with asset_lock(local):
        if not visuals_path.is_file():
            raise PackAssetExportError(f'pack visual metadata is missing: {visuals_path}')

        visuals = json.loads(visuals_path.read_text(encoding='utf-8'))
        entries = visuals.get('entries')
        if not isinstance(entries, list):
            raise PackAssetExportError('pack visual metadata has no entries list')

        current_sets = set(map(int, get_catalog().pack_sets))
        packs_by_id = {int(pack['set_id']): pack for pack in manifest['packs']}
        axes_by_set = manifest['render_contract'].get('verified_pack_axes_by_set', {})
        updated = set()
        for entry in entries:
            set_id = int(entry.get('set_id', -1))
            if set_id not in current_sets:
                continue
            pack = packs_by_id.get(set_id)
            axes = axes_by_set.get(str(set_id))
            if (pack is None or pack.get('presentation_kind') != 'official_pack_model' or
                    entry.get('presentation_kind') != 'official_pack_model' or
                    axes != {'front_axis': '-Y', 'up_axis': '-Z'}):
                raise PackAssetExportError(f'set {set_id} is not an explicitly reviewed standard pack')
            entry['front_axis'] = axes['front_axis']
            entry['up_axis'] = axes['up_axis']
            entry['front_axis_note'] = manifest['render_contract']['camera']['front_axis_note']
            updated.add(set_id)

        missing = sorted(current_sets - updated)
        if missing:
            raise PackAssetExportError(f'pack metadata is missing current standard sets: {missing}')
        atomic_json(visuals_path, visuals)
    return {'updated_set_ids': sorted(updated), 'runtime_visuals': str(visuals_path)}


def export_pack_assets(exporter=None, expected_sets=None, manifest=None) -> dict:
    manifest = manifest or load_manifest()
    expected_sets = get_catalog().pack_sets if expected_sets is None else expected_sets
    expected_sets = tuple(sorted(map(int, expected_sets)))
    validate_manifest(manifest, expected_sets)
    expected_set_ids = set(expected_sets)
    packs = [pack for pack in manifest['packs'] if int(pack['set_id']) in expected_set_ids]
    non_standard = [int(pack['set_id']) for pack in packs
                    if pack['presentation_kind'] != 'official_pack_model']
    if non_standard:
        raise ValueError(f'current shop set contains non-standard pack presentation: {non_standard}')
    exporter = exporter or RuntimeExport()

    sources = manifest['source_assets']
    exported_scenes = set()
    failures = []
    for pack in packs:
        set_id = int(pack['set_id'])
        source_set_id = int(pack.get('source_asset_set_id', set_id))
        source = sources[str(source_set_id)]
        source_root = source['game_object']
        scene_id = pack['logical_ids']['scene']
        try:
            asset_types = {
                'game_object': 'GameObject',
                'mesh': 'Mesh',
                'material': 'Material',
                'texture': 'Texture2D',
            }
            for part, expected_type in asset_types.items():
                _verify_indexed_ref(
                    exporter, source[part], f'set {set_id} {part}', expected_type
                )
            _verify_scene_selector(exporter, source, f'set {set_id}')
            before = len(exporter.provenance)
            exporter.scene(scene_id, source_root['name'], bundle=source_root['bundle'],
                           source_ref=source_root.get('guid'))
            rows = [row for row in exporter.provenance[before:]
                    if row.get('logical_id') == scene_id]
            exact_root = any(
                row.get('asset_id') == source_root['asset_id']
                and row.get('bundle') == source_root['bundle']
                and row.get('cab') == source_root['cab']
                and row.get('pathid') == source_root['pathid']
                for row in rows
            )
            if exact_root and scene_id in exporter.registry:
                _verify_exported_primary_node(exporter, pack)
                exported_scenes.add(scene_id)
                exporter.registry[pack['logical_ids']['ui']] = exporter.registry[scene_id]
            else:
                raise ValueError('scene export provenance did not match the exact GameObject source')
        except Exception as exc:
            failures.append(scene_id)
            exporter.errors.append({'id': scene_id, 'error': f'{type(exc).__name__}: {exc}'})

    exported_currencies = set()
    for item in manifest['currency_assets']:
        logical_id = item['logical_id']
        try:
            _export_exact_asset(exporter, logical_id, item['source'])
            exported_currencies.add(logical_id)
        except Exception as exc:
            failures.append(logical_id)
            exporter.errors.append({'id': logical_id, 'error': f'{type(exc).__name__}: {exc}'})

    visuals = build_runtime_visuals(manifest, exporter.registry, exported_scenes, packs)
    with asset_lock(exporter.imp.local):
        atomic_json(exporter.folder / RUNTIME_VISUALS_NAME, visuals)
    exporter.save()

    expected_currency_ids = {'UI_GOLD', 'UI_DUST'}
    if failures or exported_currencies != expected_currency_ids:
        raise PackAssetExportError(
            f'pack asset export incomplete: unresolved={sorted(set(failures))}, '
            f'currencies={sorted(exported_currencies)}'
        )
    return {'pack_count': len(exported_scenes), 'currency_ids': sorted(exported_currencies),
            'runtime_visuals': str(exporter.folder / RUNTIME_VISUALS_NAME)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        '--metadata-only', action='store_true',
        help='update reviewed camera axes in local/runtime/pack-visuals.json without exporting assets',
    )
    args = parser.parse_args()
    result = update_runtime_pack_metadata() if args.metadata_only else export_pack_assets()
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
