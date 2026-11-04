import json

import pytest
import tools.export_pack_assets as pack_export_module

from tools.export_pack_assets import (
    PackAssetExportError,
    export_pack_assets,
    load_manifest,
    validate_manifest,
)


EXPECTED_PACK_SETS = (
    3, 12, 13, 14, 15, 20, 21, 23, 25, 27, 1001, 1004,
    1125, 1127, 1129, 1130, 1158, 1347, 1403, 1414, 1443, 1463, 1869,
)
CURRENT_SHOP_PACK_SETS = (
    3, 13, 15, 21, 25, 27, 1001, 1004, 1125, 1127, 1129, 1130,
    1158, 1347, 1414, 1443,
)


class FakeDB:
    def __init__(self, rows):
        self.rows = rows

    def execute(self, query, parameters):
        if 'WHERE id=?' in query:
            row = self.rows.get(parameters[0])
            return FakeRows([row] if row else [])
        name = parameters[0]
        kind = 'GameObject' if 'type="GameObject"' in query else parameters[1]
        return FakeRows([row for row in self.rows.values()
                         if row['name'] == name and row['type'] == kind])


class FakeRows(list):
    def fetchone(self):
        return self[0] if self else None

    def fetchall(self):
        return list(self)


class FakeImporter:
    def __init__(self, root, rows):
        self.local = root
        self.store = type('Store', (), {'db': FakeDB(rows)})()
        self.exported_ids = []

    def export(self, asset_id):
        self.exported_ids.append(asset_id)
        path = self.local / 'cache' / f'{asset_id}.png'
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b'png')
        return path


class FakeExporter:
    def __init__(self, root, manifest, rows):
        self.imp = FakeImporter(root, rows)
        self.folder = root / 'runtime'
        self.folder.mkdir(parents=True, exist_ok=True)
        self.registry = {}
        self.provenance = []
        self.errors = []
        self.save_calls = 0
        self.scene_calls = []
        self.sources = manifest['source_assets']
        self.pack_by_scene = {
            pack['logical_ids']['scene']: pack for pack in manifest['packs']
        }

    def scene(self, logical_id, name, bundle=None, source_ref=None):
        pack = self.pack_by_scene[logical_id]
        self.scene_calls.append(logical_id)
        root = self.sources[str(pack['source_asset_set_id'])]['game_object']
        self.provenance.append({
            'logical_id': logical_id,
            'asset_id': root['asset_id'],
            'bundle': root['bundle'],
            'cab': root['cab'],
            'pathid': root['pathid'],
        })
        scene_file = f'{logical_id}.scene.json'
        self.registry[logical_id] = scene_file
        nodes = []
        for index, node_name in enumerate(pack['render']['primary_node_path']):
            nodes.append({'name': node_name, 'parent': index - 1,
                          'mesh': 'primary.mesh.json' if index == len(pack['render']['primary_node_path']) - 1 else ''})
        (self.folder / scene_file).write_text(json.dumps({'nodes': nodes}), encoding='utf-8')

    def save(self):
        self.save_calls += 1


def _manifest_rows(manifest):
    rows = {}
    kinds = {'game_object': 'GameObject', 'mesh': 'Mesh',
             'material': 'Material', 'texture': 'Texture2D'}
    for source in manifest['source_assets'].values():
        for part, kind in kinds.items():
            reference = source[part]
            rows[reference['asset_id']] = {
                'id': reference['asset_id'],
                'bundle': reference['bundle'],
                'cab': reference['cab'],
                'pathid': reference['pathid'],
                'type': kind,
                'name': reference['name'],
                'guids': json.dumps([reference['guid']] if reference.get('guid') else []),
            }
    for item in manifest['currency_assets']:
        reference = item['source']
        rows[reference['asset_id']] = {
            'id': reference['asset_id'],
            'bundle': reference['bundle'],
            'cab': reference['cab'],
            'pathid': reference['pathid'],
            'type': reference['type'],
            'name': reference['name'],
            'guids': json.dumps([reference['guid']]),
        }
    return rows


def test_manifest_covers_catalog_sets_with_explicit_presentations():
    manifest = load_manifest()
    validate_manifest(manifest, EXPECTED_PACK_SETS)

    by_id = {pack['set_id']: pack for pack in manifest['packs']}
    assert tuple(sorted(by_id)) == EXPECTED_PACK_SETS
    assert [by_id[set_id]['presentation_kind'] for set_id in (12, 14, 20, 23)] == [
        'adventure_book_model_derived',
    ] * 4
    assert by_id[1403]['presentation_kind'] == 'themed_pack_model_derived'
    assert by_id[1463]['presentation_kind'] == 'class_pack_model_derived'
    assert by_id[1869]['presentation_kind'] == 'related_theme_book_derived'
    assert all(pack['source_asset_set_id'] == pack['set_id'] for pack in manifest['packs'])
    assert manifest['currency_assets'][0]['logical_id'] == 'UI_GOLD'
    assert manifest['currency_assets'][1]['logical_id'] == 'UI_DUST'


def test_export_registers_exact_scenes_aliases_and_coin_sources_once(tmp_path):
    manifest = load_manifest()
    exporter = FakeExporter(tmp_path, manifest, _manifest_rows(manifest))

    result = export_pack_assets(exporter, CURRENT_SHOP_PACK_SETS, manifest)

    assert result['pack_count'] == 16
    assert exporter.save_calls == 1
    assert set(exporter.scene_calls) == {f'MODEL_PACK_{set_id}' for set_id in CURRENT_SHOP_PACK_SETS}
    assert set(exporter.imp.exported_ids) == {
        item['source']['asset_id'] for item in manifest['currency_assets']
    }
    for pack in (item for item in manifest['packs'] if item['set_id'] in CURRENT_SHOP_PACK_SETS):
        scene_id = pack['logical_ids']['scene']
        assert exporter.registry[pack['logical_ids']['ui']] == exporter.registry[scene_id]
    visuals = json.loads((exporter.folder / 'pack-visuals.json').read_text(encoding='utf-8'))
    assert len(visuals['entries']) == 16
    assert {entry['set_id'] for entry in visuals['entries']} == set(CURRENT_SHOP_PACK_SETS)
    assert all(entry['presentation_kind'] == 'official_pack_model' for entry in visuals['entries'])
    assert visuals['entries'][4]['presentation_kind'] == 'official_pack_model'
    assert all(entry['front_axis'] == '-Y' and entry['up_axis'] == '-Z' for entry in visuals['entries'])
    assert all(entry['exported'] for entry in visuals['entries'])


def test_runtime_visuals_default_to_current_catalog_subset(monkeypatch):
    manifest = load_manifest()
    catalog = type('Catalog', (), {'pack_sets': CURRENT_SHOP_PACK_SETS})()
    monkeypatch.setattr(pack_export_module, 'get_catalog', lambda: catalog)

    visuals = pack_export_module.build_runtime_visuals(manifest, {}, set())

    assert {entry['set_id'] for entry in visuals['entries']} == set(CURRENT_SHOP_PACK_SETS)


def test_missing_exact_scene_source_is_reported_and_saved(tmp_path):
    manifest = load_manifest()
    rows = _manifest_rows(manifest)
    missing_id = manifest['source_assets']['3']['game_object']['asset_id']
    rows.pop(missing_id)
    exporter = FakeExporter(tmp_path, manifest, rows)

    with pytest.raises(PackAssetExportError, match='MODEL_PACK_3'):
        export_pack_assets(exporter, CURRENT_SHOP_PACK_SETS, manifest)

    assert exporter.save_calls == 1
    assert any(error['id'] == 'MODEL_PACK_3' for error in exporter.errors)
    visuals = json.loads((exporter.folder / 'pack-visuals.json').read_text(encoding='utf-8'))
    assert not next(entry for entry in visuals['entries'] if entry['set_id'] == 3)['exported']


def test_current_shop_subset_refuses_derived_pack_presentations(tmp_path):
    manifest = load_manifest()
    pack = next(item for item in manifest['packs'] if item['set_id'] == 3)
    pack['presentation_kind'] = 'themed_pack_model_derived'
    pack['presentation_note'] = 'Test-only incorrect derived shop entry.'
    exporter = FakeExporter(tmp_path, manifest, _manifest_rows(manifest))

    with pytest.raises(ValueError, match='non-standard pack presentation'):
        export_pack_assets(exporter, CURRENT_SHOP_PACK_SETS, manifest)

    assert exporter.scene_calls == []
    assert exporter.save_calls == 0


def test_manifest_rejects_cross_set_or_generic_pack_fallback():
    manifest = load_manifest()
    pack = next(item for item in manifest['packs'] if item['set_id'] == 12)
    pack['source_asset_set_id'] = 3
    pack['source_type'] = 'explicit_generic_3d_fallback'
    with pytest.raises(ValueError, match='exact source set'):
        validate_manifest(manifest, EXPECTED_PACK_SETS)
