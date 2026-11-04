"""Export exact indexed GameObjects for the F026 native hub widgets."""
import hashlib
import json
from pathlib import Path

from .export_runtime import RuntimeExport


PROJECT = Path(__file__).resolve().parents[1]
MANIFEST = PROJECT / 'manifests' / 'hub-native-widgets.json'


def validate_source(exporter, source):
    row = exporter.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (source['asset_id'],)).fetchone()
    expected = (source['bundle'], source['cab'], source['pathid'], 'GameObject', source['name'])
    actual = None if row is None else (row['bundle'], row['cab'], row['pathid'], row['type'], row['name'])
    if actual != expected:
        raise RuntimeError(f"source identity mismatch for {source['id']}: {actual!r} != {expected!r}")

    source_guid = source.get('source_guid')
    if source_guid:
        guids = json.loads(row['guids'] or '[]')
        if source_guid not in guids:
            raise RuntimeError(f"source GUID is not indexed for {source['id']}: {source_guid}")
        obj, bundle, _ = exporter.imp.reader.resolve(source_guid)
        if (bundle, obj.assets_file.name, str(obj.path_id)) != (source['bundle'], source['cab'], source['pathid']):
            raise RuntimeError(f"source GUID resolved to a different object for {source['id']}")
        return source_guid

    if not source.get('allow_duplicate_name'):
        matches = exporter.imp.store.db.execute(
            'SELECT id,cab,pathid FROM assets WHERE name=? AND type=? AND bundle=?',
            (source['name'], 'GameObject', source['bundle'])).fetchall()
        if len(matches) != 1 or matches[0]['id'] != source['asset_id'] or \
                matches[0]['cab'] != source['cab'] or matches[0]['pathid'] != source['pathid']:
            raise RuntimeError(f"name and bundle do not uniquely identify {source['id']}")
    return None


def validate_exported_renderers(exporter, source):
    required = source.get('required_renderers', [])
    if not required:
        return

    scene_path = exporter.folder / (source['id'] + '.scene.json')
    nodes = json.loads(scene_path.read_text(encoding='utf-8')).get('nodes', [])
    expected_count = source.get('expected_node_count')
    if expected_count is not None and len(nodes) != expected_count:
        raise RuntimeError(f"scene node count mismatch for {source['id']}: {len(nodes)} != {expected_count}")
    paths = []
    for index, node in enumerate(nodes):
        parent = node.get('parent', -1)
        if parent < 0:
            paths.append([node['name']])
        elif parent >= index or parent >= len(paths):
            raise RuntimeError(f"invalid scene parent for {source['id']} node {index}")
        else:
            paths.append(paths[parent] + [node['name']])

    include_paths = source.get('include_node_paths')
    if include_paths is not None:
        include_set = {tuple(path) for path in include_paths}
        required_nodes = {tuple(path[:depth]) for path in include_paths
                          for depth in range(1, len(path) + 1)}
        actual_nodes = {tuple(path) for path in paths}
        if actual_nodes != required_nodes:
            raise RuntimeError(f"filtered scene node paths mismatch for {source['id']}")
        actual_renderers = {tuple(paths[index]) for index, node in enumerate(nodes)
                            if node.get('mesh')}
        if actual_renderers != include_set:
            raise RuntimeError(f"filtered renderer paths mismatch for {source['id']}: {actual_renderers!r}")

    mesh_names = {entry.get('file'): entry.get('name') for entry in exporter.provenance
                  if entry.get('type') == 'Mesh' and entry.get('file')}
    texture_names = {entry.get('file'): entry.get('name') for entry in exporter.provenance
                     if entry.get('type') == 'Texture2D' and entry.get('file')}
    for expected in required:
        expected_path = expected['path']
        matches = [(node, paths[index]) for index, node in enumerate(nodes)
                   if paths[index] == expected_path]
        if len(matches) != 1:
            raise RuntimeError(f"expected one renderer node {source['id']}:{'/'.join(expected_path)}, got {len(matches)}")
        node = matches[0][0]
        mesh_name = mesh_names.get(node.get('mesh'))
        if mesh_name != expected.get('mesh'):
            raise RuntimeError(f"mesh mismatch for {source['id']}:{'/'.join(expected_path)}: {mesh_name!r}")
        material_names = {material.get('name') for material in node.get('materials', [])}
        if not set(expected.get('materials', [])).issubset(material_names):
            raise RuntimeError(f"material mismatch for {source['id']}:{'/'.join(expected_path)}: {material_names!r}")
        node_texture_names = {texture_names.get(material.get('texture'))
                              for material in node.get('materials', []) if material.get('texture')}
        if not set(expected.get('textures', [])).issubset(node_texture_names):
            raise RuntimeError(f"texture mismatch for {source['id']}:{'/'.join(expected_path)}: {node_texture_names!r}")


def validate_widget_node_paths(exporter, source):
    widget_paths = source.get('native_node_paths', {})
    if not widget_paths:
        return

    scene_path = exporter.folder / (source['id'] + '.scene.json')
    nodes = json.loads(scene_path.read_text(encoding='utf-8')).get('nodes', [])
    paths = []
    for index, node in enumerate(nodes):
        parent = node.get('parent', -1)
        if parent < 0:
            paths.append([node['name']])
        elif parent >= index or parent >= len(paths):
            raise RuntimeError(f"invalid scene parent for {source['id']} node {index}")
        else:
            paths.append(paths[parent] + [node['name']])

    widget_ids = set(source.get('widget_ids', []))
    for widget_id, expected_path in widget_paths.items():
        if widget_id not in widget_ids:
            raise RuntimeError(f"native node path has no matching widget ID for {source['id']}: {widget_id}")
        matches = [path for path in paths if path == expected_path]
        if len(matches) != 1:
            raise RuntimeError(f"expected one native widget path {source['id']}:{'/'.join(expected_path)}, got {len(matches)}")


def apply_scene_material_overrides(exporter, source):
    overrides = source.get('material_overrides', [])
    if not overrides:
        return

    scene_path = exporter.folder / (source['id'] + '.scene.json')
    data = json.loads(scene_path.read_text(encoding='utf-8'))
    nodes = data.get('nodes', [])
    paths = []
    for index, node in enumerate(nodes):
        parent = node.get('parent', -1)
        if parent < 0:
            paths.append([node['name']])
        elif parent >= index or parent >= len(paths):
            raise RuntimeError(f"invalid scene parent for {source['id']} node {index}")
        else:
            paths.append(paths[parent] + [node['name']])

    for override in overrides:
        expected_path = override['path']
        matches = [index for index, path in enumerate(paths) if path == expected_path]
        if len(matches) != 1:
            raise RuntimeError(f"expected one material override node {source['id']}:{'/'.join(expected_path)}, got {len(matches)}")
        node = nodes[matches[0]]
        materials = [material for material in node.get('materials', [])
                     if material.get('name') == override['material']]
        if len(materials) != 1:
            raise RuntimeError(f"expected one material {override['material']} at {source['id']}:{'/'.join(expected_path)}")
        for key, value in override.items():
            if key not in ('path', 'material', 'source_note'):
                if key != 'transparent':
                    raise RuntimeError(f"unsupported material override for {source['id']}: {key}")
                materials[0][key] = value

    scene_path.write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')


def validate_texture_source_settings(source, obj):
    expected = source.get('source_settings', {})
    if not expected:
        return {}

    tree = obj.read_typetree()
    settings = tree.get('m_TextureSettings', {})
    if not isinstance(settings, dict):
        raise RuntimeError(f"Texture2D m_TextureSettings is not a mapping for {source['id']}")
    wrap = [settings.get('m_Wrap' + axis) for axis in ('U', 'V', 'W')]
    wrap_names = {0: 'Repeat', 1: 'Clamp', 2: 'Mirror', 3: 'MirrorOnce'}
    actual = {
        'width': tree.get('m_Width'),
        'height': tree.get('m_Height'),
        'format': tree.get('m_TextureFormat'),
        'filter': settings.get('m_FilterMode'),
        'aniso': settings.get('m_Aniso'),
        'mip_bias': settings.get('m_MipBias'),
        'wrap_u': wrap[0],
        'wrap_v': wrap[1],
        'wrap_w': wrap[2],
        'wrap_mode': wrap_names.get(wrap[0]) if wrap[0] == wrap[1] == wrap[2] else 'Mixed',
    }
    if 'alpha_extrema' in expected:
        image = obj.read().image
        actual['alpha_extrema'] = list(image.getchannel('A').getextrema()) \
            if 'A' in image.getbands() else [255, 255]
    aliases = {
        'm_Width': 'width', 'm_Height': 'height', 'm_TextureFormat': 'format',
        'm_FilterMode': 'filter', 'm_Aniso': 'aniso', 'm_MipBias': 'mip_bias',
        'm_WrapU': 'wrap_u', 'm_WrapV': 'wrap_v', 'm_WrapW': 'wrap_w',
    }
    for key, value in expected.items():
        actual_key = aliases.get(key, key)
        if actual_key not in actual:
            raise RuntimeError(f"unsupported texture source setting for {source['id']}:{key}")
        if actual[actual_key] != value:
            raise RuntimeError(f"texture settings mismatch for {source['id']}:{key}: {actual[actual_key]!r} != {value!r}")
    return actual


def export_texture_source(exporter, source):
    row = exporter.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (source['asset_id'],)).fetchone()
    expected = (source['bundle'], source['cab'], source['pathid'], 'Texture2D', source['name'])
    actual = None if row is None else (row['bundle'], row['cab'], row['pathid'], row['type'], row['name'])
    if actual != expected:
        raise RuntimeError(f"texture source identity mismatch for {source['id']}: {actual!r} != {expected!r}")

    env = exporter.imp.reader.load(source['bundle'])
    obj = next((item for item in env.objects
                if item.assets_file.name == source['cab'] and str(item.path_id) == source['pathid']), None)
    if obj is None or obj.type.name != 'Texture2D':
        raise RuntimeError(f"texture source could not be resolved for {source['id']}")
    source_settings = validate_texture_source_settings(source, obj)

    filename = exporter.texture(obj)
    exporter.registry[source['id']] = filename
    record = next((item for item in reversed(exporter.provenance)
                   if item.get('cab') == source['cab'] and item.get('pathid') == source['pathid']
                   and item.get('type') == 'Texture2D'), None)
    if record is None:
        raise RuntimeError(f"texture provenance was not recorded for {source['id']}")
    record.update(logical_id=source['id'], asset_id=source['asset_id'], bundle=source['bundle'],
                  cab=source['cab'], pathid=source['pathid'], name=source['name'], file=filename)
    if source_settings:
        record['source_settings'] = source_settings
    record['sha256'] = hashlib.sha256((exporter.folder / filename).read_bytes()).hexdigest().upper()
    return filename


def main():
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    exporter = RuntimeExport()
    expected = {}
    for source in manifest['scenes']:
        source_guid = validate_source(exporter, source)
        count = exporter.scene(source['id'], source['name'], source['bundle'], source_guid,
                               source_asset_id=source['asset_id'],
                               include_node_paths=source.get('include_node_paths'))
        if count <= 0:
            raise RuntimeError(f"{source['id']} exported no nodes")
        apply_scene_material_overrides(exporter, source)
        validate_exported_renderers(exporter, source)
        validate_widget_node_paths(exporter, source)
        expected[source['id']] = (source['asset_id'], source['bundle'], source['cab'], source['pathid'])

    expected_textures = {}
    for source in manifest.get('textures', []):
        filename = export_texture_source(exporter, source)
        expected_textures[source['id']] = (source['asset_id'], source['bundle'], source['cab'],
                                           source['pathid'], filename)

    errors = [error for error in exporter.errors
              if error.get('id') in expected or error.get('scene') in expected
              or error.get('id') in expected_textures]
    if errors:
        raise RuntimeError(json.dumps(errors, ensure_ascii=False))
    for entry in exporter.provenance:
        logical_id = entry.get('logical_id')
        if logical_id in expected:
            identity = (entry['asset_id'], entry['bundle'], entry['cab'], entry['pathid'])
            if identity != expected[logical_id]:
                raise RuntimeError(f"export provenance mismatch for {logical_id}: {identity!r}")
    if set(expected) != {entry.get('logical_id') for entry in exporter.provenance if entry.get('logical_id') in expected}:
        raise RuntimeError('one or more widget scenes are absent from export provenance')
    for logical_id, identity in expected_textures.items():
        source = next(entry for entry in exporter.provenance if entry.get('logical_id') == logical_id)
        actual = (source.get('asset_id'), source.get('bundle'), source.get('cab'),
                  source.get('pathid'), source.get('file'))
        if actual != identity:
            raise RuntimeError(f"texture export provenance mismatch for {logical_id}: {actual!r} != {identity!r}")
        if not source.get('sha256') or len(source['sha256']) != 64:
            raise RuntimeError(f"texture export SHA-256 is absent for {logical_id}")

    exporter.save()
    print('F026 hub widget scenes', len(expected), 'textures', len(expected_textures), flush=True)


if __name__ == '__main__':
    main()
