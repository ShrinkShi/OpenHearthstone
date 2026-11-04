"""Export the indexed PC hub page prefabs and direct UI texture sources."""
from copy import deepcopy
import json
from pathlib import Path

from .export_runtime import RuntimeExport


PROJECT = Path(__file__).resolve().parents[1]
MANIFEST = PROJECT / 'manifests' / 'hub-native-scenes.json'


def export_source_material(exporter, source):
    row = exporter.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (source['asset_id'],)).fetchone()
    if row is None or (row['bundle'], row['cab'], row['pathid'], row['type'], row['name']) != (
            source['bundle'], source['cab'], source['pathid'], 'Material', source['name']):
        raise RuntimeError(f"material source identity mismatch: {source['scene']}:{source['name']}")
    env = exporter.imp.reader.load(source['bundle'])
    obj = next((item for item in env.objects
                if item.assets_file.name == source['cab'] and str(item.path_id) == source['pathid']), None)
    if obj is None:
        raise RuntimeError(f"material source object missing: {source['scene']}:{source['name']}")
    return exporter.material(obj)


def export_scene_variant(exporter, variant, material_sources, material_cache):
    source_id = variant['source_scene']
    source_file = exporter.registry.get(source_id)
    if not source_file:
        raise RuntimeError(f"{variant['id']} source scene was not exported: {source_id}")
    data = json.loads((exporter.folder / source_file).read_text(encoding='utf-8'))
    nodes = deepcopy(data.get('nodes', []))
    paths = []
    path_to_indices = {}
    for index, node in enumerate(nodes):
        parent = int(node.get('parent', -1))
        if parent < 0:
            path = '/' + node['name']
        elif parent >= index:
            raise RuntimeError(f"{variant['id']} has invalid parent index at node {index}")
        else:
            path = paths[parent] + '/' + node['name']
        paths.append(path)
        path_to_indices.setdefault(path, []).append(index)

    for field, active in (('enable_nodes', True), ('disable_nodes', False)):
        for path in variant.get(field, []):
            matches = path_to_indices.get(path, [])
            if len(matches) != 1:
                raise RuntimeError(f"{variant['id']} missing {field} path: {path}")
            nodes[matches[0]]['active'] = active

    materials = {}
    for node in nodes:
        for material in node.get('materials', []):
            materials.setdefault(material.get('name'), material)
    for override in variant.get('material_overrides', []):
        path = override['node']
        name = override['material']
        matches = path_to_indices.get(path, [])
        if len(matches) != 1:
            raise RuntimeError(f"{variant['id']} missing material node: {path}")
        node = nodes[matches[0]]
        material_index = int(override.get('index', 0))
        if material_index < 0 or material_index >= len(node.get('materials', [])):
            raise RuntimeError(f"{variant['id']} invalid material slot {material_index}: {path}")
        source = material_sources.get((source_id, name))
        if source:
            material = material_cache.get(source['asset_id'])
            if material is None:
                material = export_source_material(exporter, source)
                material_cache[source['asset_id']] = material
        else:
            material = materials.get(name)
        if material is None:
            raise RuntimeError(f"{variant['id']} missing source material: {name}")
        node['materials'][material_index] = deepcopy(material)

    expected_nodes = variant.get('expected_nodes')
    if expected_nodes is not None and len(nodes) != expected_nodes:
        raise RuntimeError(f"{variant['id']} expected {expected_nodes} nodes, got {len(nodes)}")
    filename = variant['id'] + '.scene.json'
    (exporter.folder / filename).write_text(json.dumps({'nodes': nodes}, ensure_ascii=False), encoding='utf-8')
    exporter.registry[variant['id']] = filename
    print(variant['id'], len(nodes), 'nodes (source-state variant)', flush=True)


def main():
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    exporter = RuntimeExport()
    for scene in manifest['scenes']:
        count = exporter.scene(scene['id'], scene['name'], scene['bundle'], scene.get('source_guid'))
        if count != scene['expected_nodes']:
            raise RuntimeError(f"{scene['id']} expected {scene['expected_nodes']} nodes, got {count}")
    for texture in manifest['textures']:
        exporter.named(texture['id'], texture['name'], 'Texture2D')
    expected_ids = {scene['id'] for scene in manifest['scenes']} | {texture['id'] for texture in manifest['textures']}
    errors = [error for error in exporter.errors
              if error.get('id') in expected_ids or error.get('scene') in expected_ids]
    if errors:
        raise RuntimeError(json.dumps(errors, ensure_ascii=False))
    material_sources = {(row['scene'], row['name']): row for row in manifest.get('material_sources', [])}
    material_cache = {}
    for variant in manifest.get('scene_variants', []):
        export_scene_variant(exporter, variant, material_sources, material_cache)
    exporter.save()
    print('hub native scenes', len(manifest['scenes']), 'textures', len(manifest['textures']), flush=True)


if __name__ == '__main__':
    main()
