"""Read authored gameplay cameras and their parent transforms without running scripts."""
from .asset_lock import atomic_json, asset_lock
from .importer import Importer, PROJECT


def recover(importer):
    reader = importer.reader
    records = []
    rows = importer.store.db.execute(
        'SELECT bundle,cab,pathid FROM assets WHERE type=? AND bundle=? ORDER BY CAST(pathid AS INTEGER)',
        ('Camera', '@player/level30')).fetchall()
    for row in rows:
        obj = reader.load(row['bundle']).get_cab(row['cab'].lower()).objects[int(row['pathid'])]
        tree = obj.read_typetree()
        go = reader.pointer(obj, tree['m_GameObject'])
        transform = next(value for item in go.read_typetree()['m_Component']
                         if (value := reader.pointer(go, item['component']))
                         and value.type.name == 'Transform')
        chain, seen = [], set()
        while transform is not None:
            identity = (transform.assets_file.name, transform.path_id)
            if identity in seen:
                raise ValueError('Cyclic source camera Transform hierarchy')
            seen.add(identity)
            raw = transform.read_typetree()
            owner = reader.pointer(transform, raw['m_GameObject'])
            chain.append(dict(name=owner.read_typetree()['m_Name'],
                position=raw['m_LocalPosition'], rotation=raw['m_LocalRotation'], scale=raw['m_LocalScale'],
                source=dict(cab=identity[0], pathid=str(identity[1]), bundle=row['bundle']), raw=raw))
            transform = reader.pointer(transform, raw['m_Father'])
        records.append(dict(name=go.read_typetree()['m_Name'], source=dict(row), raw=tree,
            projection_mode=tree['m_projectionMatrixMode'], orthographic=tree['orthographic'],
            size=tree['orthographic size'], fov=tree['field of view'],
            near=tree['near clip plane'], far=tree['far clip plane'], chain=chain))
    if len(records) != 4 or sum(c['name'] == 'Main Camera' for c in records) != 1:
        raise ValueError('Expected original gameplay camera scene changed')
    return dict(cameras=records, full_scene_camera_restored=False)


def main():
    result = recover(Importer())
    with asset_lock(PROJECT / 'local'):
        atomic_json(PROJECT / 'local/runtime/original-battle-cameras.json', result)
        atomic_json(PROJECT / 'manifests/original-battle-cameras.json', result)
    print('SOURCE GAMEPLAY CAMERAS:', len(result['cameras']), '; runtime viewport/scripts not restored')


if __name__ == '__main__':
    main()
