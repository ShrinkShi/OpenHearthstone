"""按需导出客户端所需的小集合，包括原始 mesh/transform/material 参数。"""
import hashlib
import json
import time
from pathlib import Path

from UnityPy.helpers.MeshHelper import MeshHandler

from game.database import HEROES, build_database, preset, scope
from .importer import Importer
from .asset_lock import asset_lock, atomic_json
from .audio_cache import publish_audio


def mesh_data(handler, tree):
    def packed_rows(rows, width, fill=0):
        if not rows:
            return None
        output = []
        for row in rows:
            values = list(row)
            if len(values) > width:
                raise ValueError('Mesh vertex row exceeds supported width')
            output.extend(values + [fill] * (width - len(values)))
        return output

    bindposes = []
    matrix_fields = [f'e{row}{column}' for row in range(4) for column in range(4)]
    for matrix in tree.get('m_BindPose', []):
        if not all(field in matrix for field in matrix_fields):
            raise ValueError('Mesh bindpose has an unknown serialized matrix shape')
        bindposes.extend(matrix[field] for field in matrix_fields)
    return {
        'vertices': [v for row in (handler.m_Vertices or []) for v in row[:3]],
        'uv': [v for row in (handler.m_UV0 or []) for v in row[:2]],
        'normals': [v for row in (handler.m_Normals or []) for v in row[:3]],
        'colors': [v for row in (handler.m_Colors or []) for v in row[:4]],
        'bone_indices': packed_rows(handler.m_BoneIndices, 4),
        'bone_weights': packed_rows(handler.m_BoneWeights, 4),
        'bindposes': bindposes or None,
        'submeshes': [{'triangles': [v for tri in t for v in tri]} for t in handler.get_triangles()],
    }


class RuntimeExport:
    def __init__(self, importer=None):
        self.imp = importer or Importer()
        self.folder = self.imp.local / 'runtime'
        self.folder.mkdir(exist_ok=True)
        self.registry = {}
        path = self.folder / 'registry.json'
        if path.exists():
            self.registry = {r['id']: r['path'] for r in json.loads(path.read_text(encoding='utf-8'))['entries']}
        self.original_registry = self.registry.copy()
        self.provenance = []
        self.errors = []
        self.source_records = set()

    def key(self, obj):
        bundle = self.imp.reader.cabs.get(obj.assets_file.name, '')
        path = self.imp.reader.source_paths().get(bundle)
        stamp = f'{path.stat().st_size}:{path.stat().st_mtime_ns}' if path else ''
        return hashlib.sha256(f'{obj.assets_file.name}:{obj.path_id}:{stamp}'.encode()).hexdigest()[:24]

    def record_source(self, obj, **fields):
        identity = (obj.assets_file.name, obj.path_id, obj.type.name)
        if identity in self.source_records:
            return
        self.source_records.add(identity)
        source = {'bundle': self.imp.reader.cabs.get(obj.assets_file.name, ''),
                  'cab': obj.assets_file.name, 'pathid': str(obj.path_id), 'type': obj.type.name}
        name = obj.peek_name()
        if name:
            source['name'] = name
        source.update(fields)
        self.provenance.append(source)

    def texture(self, obj):
        path = self.folder / (self.key(obj) + '.png')
        if not path.exists():
            obj.read().image.save(path)
        self.record_source(obj, file=path.name)
        return path.name

    def named(self, logical_id, name, kind='Texture2D'):
        rows = self.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle', (name, kind)).fetchall()
        if not rows:
            self.errors.append({'id': logical_id, 'name': name, 'error': 'not indexed'})
            return
        try:
            path = self.imp.export(rows[0]['id'])
            target = self.folder / path.name
            if kind == 'AudioClip':
                publish_audio(target, path.read_bytes())
            elif not target.exists():
                target.write_bytes(path.read_bytes())
            self.registry[logical_id] = target.name
            self.provenance.append({'file': target.name, 'asset_id': rows[0]['id'], 'bundle': rows[0]['bundle'],
                                    'cab': rows[0]['cab'], 'pathid': rows[0]['pathid'],
                                    'type': rows[0]['type'], 'name': rows[0]['name']})
        except Exception as exc:
            self.errors.append({'id': logical_id, 'error': str(exc)})

    def portrait(self, card_id):
        try:
            data = self.imp.reader.definition(card_id)
            reference = data.get('m_PortraitTexturePath')
            obj, _, _ = self.imp.reader.resolve(reference)
            self.registry['CARD_' + card_id] = self.texture(obj)
        except Exception as exc:
            self.errors.append({'id': card_id, 'error': str(exc)})

    def mesh(self, obj):
        path = self.folder / (self.key(obj) + '.mesh.json')
        if not path.exists():
            h = MeshHandler(obj.read())
            h.process()
            value = mesh_data(h, obj.read_typetree())
            path.write_text(json.dumps(value), encoding='utf-8')
        self.record_source(obj, file=path.name)
        return path.name

    def sprite_mesh(self, obj):
        path = self.folder / (self.key(obj) + '.mesh.json')
        if not path.exists():
            sprite = obj.read()
            handler = MeshHandler(sprite.m_RD, version=obj.version)
            handler.process()
            path.write_text(json.dumps(mesh_data(handler, obj.read_typetree())), encoding='utf-8')
        self.record_source(obj, file=path.name)
        return path.name

    def material(self, obj):
        tree = obj.read_typetree()
        shader_obj = self.imp.reader.pointer(obj, tree['m_Shader'])
        shader_tree = shader_obj.read_typetree() if shader_obj else {}
        shader_name = shader_tree.get('m_ParsedForm', {}).get('m_Name') or shader_tree.get('m_Name', '')
        self.record_source(obj, shader=shader_name)
        props = tree.get('m_SavedProperties', {})
        texture = ''
        slots = dict(props.get('m_TexEnvs', []))
        effect = '_FxTex' in slots
        chosen = None
        for slot in (['_FxTex'] if effect else []) + ['_MainTex', '_Texture', '_BaseMap', '_Diffuse', '_BotTex', '_TopTex'] + list(slots):
            value = slots.get(slot)
            if value and value['m_Texture']['m_PathID']:
                ptr = self.imp.reader.pointer(obj, value['m_Texture'])
                if ptr:
                    texture = self.texture(ptr)
                    chosen = value
                    break
        floats = dict(props.get('m_Floats', []))
        colors = dict(props.get('m_Colors', []))
        return {'name': tree['m_Name'], 'texture': texture, 'shader': shader_name,
                'transparent': effect or any(x in tree['m_Name'].lower() for x in ('shadow', 'glow', 'ray', 'effect', 'particle', 'light')),
                'effect': effect, 'intensity': floats.get('_Intensity', 1),
                'tint': colors.get('_Color', {'r': 1, 'g': 1, 'b': 1, 'a': 1}),
                'texture_scale': chosen.get('m_Scale') if chosen else {'x': 1, 'y': 1},
                'texture_offset': chosen.get('m_Offset') if chosen else {'x': 0, 'y': 0},
                'properties': json.dumps(props, default=str)}

    def scene(self, logical_id, name, bundle=None, source_ref=None, source_asset_id=None,
              include_node_paths=None):
        rows = self.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type="GameObject"', (name,)).fetchall()
        if source_asset_id:
            row = self.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (source_asset_id,)).fetchone()
            if row is not None and (row['name'] != name or row['type'] != 'GameObject' or
                                    (bundle and row['bundle'] != bundle)):
                raise RuntimeError(f"exact scene source mismatch for {logical_id}: {source_asset_id}")
            if source_ref and row is not None:
                source, source_bundle, _ = self.imp.reader.resolve(source_ref)
                identity = (source_bundle, source.assets_file.name, str(source.path_id))
                if identity != (row['bundle'], row['cab'], row['pathid']):
                    raise RuntimeError(f"scene source reference mismatch for {logical_id}: {identity!r}")
        elif source_ref:
            source,source_bundle,_=self.imp.reader.resolve(source_ref)
            row=next((r for r in rows if r['bundle']==source_bundle and r['cab']==source.assets_file.name and r['pathid']==str(source.path_id)),None)
        else:
            row = next((r for r in rows if not bundle or r['bundle'] == bundle), None)
        if row is None:
            self.errors.append({'id': logical_id, 'error': 'scene root not indexed: ' + name})
            return 0
        self.provenance.append({'logical_id': logical_id, 'asset_id': row['id'], 'bundle': row['bundle'],
                                'cab': row['cab'], 'pathid': row['pathid'], 'type': 'GameObject'})
        env = self.imp.reader.load(row['bundle'])
        root = next(o for o in env.objects if str(o.path_id) == row['pathid'] and o.assets_file.name == row['cab'])
        nodes = []
        visited = set()
        included_paths = None if include_node_paths is None else tuple(
            tuple(path) for path in include_node_paths)
        if included_paths is not None and (not included_paths or any(not path for path in included_paths)):
            raise ValueError(f"invalid include node paths for {logical_id}")

        def walk(go, parent, parent_path=()):
            identity = self.key(go)
            if identity in visited or len(nodes) > 1500:
                return
            visited.add(identity)
            tree = go.read_typetree()
            node_path = parent_path + (tree['m_Name'],)
            if included_paths is not None and not any(
                    path[:len(node_path)] == node_path for path in included_paths):
                return
            components = {}
            for c in tree.get('m_Component', []):
                component = self.imp.reader.pointer(go, c['component'])
                if component:
                    components[component.type.name] = component
            transform = components.get('Transform')
            if not transform:
                return
            t = transform.read_typetree()
            node = {'name': tree['m_Name'], 'parent': parent, 'active': bool(tree.get('m_IsActive', 1)),
                    'position': t['m_LocalPosition'], 'rotation': t['m_LocalRotation'], 'scale': t['m_LocalScale'],
                    'mesh': '', 'materials': []}
            index = len(nodes)
            nodes.append(node)
            if 'Camera' in components:
                camera = components['Camera'].read_typetree()
                node['camera'] = {'field_of_view': camera['field of view'],
                                  'near': camera['near clip plane'], 'far': camera['far clip plane']}
            try:
                if ('MeshFilter' in components and 'MeshRenderer' in components) or 'SkinnedMeshRenderer' in components:
                    mf = components.get('MeshFilter') or components['SkinnedMeshRenderer']
                    mr = components.get('MeshRenderer') or components['SkinnedMeshRenderer']
                    mesh = self.imp.reader.pointer(mf, mf.read_typetree()['m_Mesh'])
                    if mesh:
                        node['mesh'] = self.mesh(mesh)
                    for ptr in mr.read_typetree().get('m_Materials', []):
                        material = self.imp.reader.pointer(mr, ptr)
                        node['materials'].append(self.material(material) if material else {})
                elif 'SpriteRenderer' in components:
                    renderer = components['SpriteRenderer']
                    values = renderer.read_typetree()
                    sprite = self.imp.reader.pointer(renderer, values.get('m_Sprite', {}))
                    if sprite:
                        sprite_values = sprite.read_typetree()
                        sprite_data = sprite_values.get('m_RD', {})
                        texture_ref = sprite_data.get('texture', {})
                        texture = self.imp.reader.pointer(sprite, texture_ref) if texture_ref.get('m_PathID') else None
                        sprite_material = {
                            'name': sprite_values.get('m_Name') or node['name'],
                            'texture': self.texture(texture) if texture else '',
                            'shader': 'Sprites/Default',
                            'transparent': True,
                            'effect': False,
                            'intensity': 1,
                            'tint': values.get('m_Color', {'r': 1, 'g': 1, 'b': 1, 'a': 1}),
                            'texture_scale': {'x': 1, 'y': 1},
                            'texture_offset': {'x': 0, 'y': 0},
                            'properties': json.dumps({
                                'source': 'SpriteRenderer',
                                'sprite_cab': sprite.assets_file.name,
                                'sprite_pathid': str(sprite.path_id),
                                'sorting_layer_id': values.get('m_SortingLayerID', 0),
                                'sorting_order': values.get('m_SortingOrder', 0),
                                'texture_rect': sprite_data.get('textureRect'),
                                'uv_transform': sprite_data.get('uvTransform'),
                            }),
                        }
                        node['mesh'] = self.sprite_mesh(sprite)
                        node['materials'] = [sprite_material]
                        node['sorting_order'] = int(values.get('m_SortingOrder', 0))
                elif 'MonoBehaviour' in components:
                    definition = components['MonoBehaviour']
                    values = definition.read_typetree()
                    if values.get('m_CardBackMesh', {}).get('m_PathID'):
                        mesh = self.imp.reader.pointer(definition, values['m_CardBackMesh'])
                        material = self.imp.reader.pointer(definition, values['m_CardBackMaterial'])
                        node['mesh'] = self.mesh(mesh)
                        node['materials'] = [self.material(material)]
            except Exception as exc:
                self.errors.append({'scene': logical_id, 'node': node['name'], 'error': str(exc)})
            if 'ParticleSystem' in components:
                path = self.folder / (identity + '.particle.json')
                path.write_text(json.dumps(components['ParticleSystem'].read_typetree(), default=str), encoding='utf-8')
                node['particle'] = path.name
            for ptr in t.get('m_Children', []):
                tr = self.imp.reader.pointer(transform, ptr)
                if tr:
                    child = self.imp.reader.pointer(tr, tr.read_typetree()['m_GameObject'])
                    if child:
                        walk(child, index, node_path)
        walk(root, -1)
        path = self.folder / (logical_id + '.scene.json')
        path.write_text(json.dumps({'nodes': nodes}, ensure_ascii=False), encoding='utf-8')
        self.registry[logical_id] = path.name
        print(logical_id, len(nodes), 'nodes', sum(bool(n['mesh']) for n in nodes), 'meshes',
              sum('sorting_order' in n for n in nodes), 'sprites', flush=True)
        return len(nodes)

    def run(self):
        build_database()
        selected = set(scope()) | set(HEROES.values()) | {'GAME_005', 'CS2_tk1', 'CS2_mirror', 'CS2_101t', 'CS2_050'}
        from fireplace import cards
        from hearthstone.enums import GameTag
        selected.update(cards.db.dbf[cards.db[hero].tags[GameTag.HERO_POWER]] for hero in HEROES.values())
        selected.update({'RLK_018t', 'RLK_008t', 'RLK_039t', 'RLK_070t', 'RLK_057t', 'RLK_061t',
                         'RLK_063t', 'RLK_077t', 'RLK_085t', 'RLK_118t3', 'RLK_226t', 'RLK_506t',
                         'RLK_705t', 'RLK_907t', 'HERO_11bpt', 'HERO_11bp2', 'HERO_11bp2t'})
        for i, card in enumerate(sorted(selected)):
            self.portrait(card)
            if i % 50 == 0:
                print('portraits', i, '/', len(selected), flush=True)
        for logical, name in {
            'UI_HERO_TRAY': 'Board_HeroTray', 'UI_END_TURN': 'Board_EndTurnButton',
            'UI_MENU_FRAME': 'TheBox_FrameV2', 'UI_MENU_RING': 'TheBox_Ring',
            'UI_CARD_BACK': 'CardBackGeneric_echo', 'UI_MANA': 'Coin_Mana',
            'BGM_MAIN_TITLE': 'Main_Title', 'BGM_DUEL': 'Duel',
            'SFX_CARD_DRAW': 'draw_card_1', 'SFX_ATTACK': 'FX_Minion_AttackImpact',
            'SFX_FIREBALL': 'FX_FireballEvent04_SpellImpact_01',
        }.items():
            self.named(logical, name, 'AudioClip' if logical.startswith(('BGM', 'SFX')) else 'Texture2D')
        self.scene('SCENE_MAIN_MENU', 'TheBox', 'essential_base_box_global-prefab-0.unity3d')
        self.save()

    def save(self):
        with asset_lock(self.imp.local):
            path = self.folder / 'registry.json'
            latest = {r['id']: r['path'] for r in json.loads(path.read_text(encoding='utf-8'))['entries']} if path.exists() else {}
            latest.update({k: v for k, v in self.registry.items() if self.original_registry.get(k) != v})
            atomic_json(path, {'entries': [{'id': k, 'path': v} for k, v in sorted(latest.items())]})
            self.registry = latest
            self.original_registry = latest.copy()
            report = {'time': time.time(), 'provenance': self.provenance, 'errors': self.errors}
            atomic_json(self.folder / 'export-report.json', report)
            with (self.folder / 'export-history.jsonl').open('a', encoding='utf-8') as journal:
                journal.write(json.dumps(report, ensure_ascii=False)+'\n')
        print('registry', len(self.registry), 'errors', len(self.errors))


if __name__ == '__main__':
    RuntimeExport().run()
