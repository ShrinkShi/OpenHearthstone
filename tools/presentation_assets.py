"""只读提取本轮表现所需素材，保留逻辑 ID、原始对象身份与用途。"""
import hashlib
import json
import math
from array import array

from PIL import Image, ImageChops, ImageDraw
from UnityPy.helpers.MeshHelper import MeshHandler

from .export_runtime import RuntimeExport


TEXTURES = {
    'UI_MULLIGAN_BANNER': 'Mulligan_Banner',
    'UI_MULLIGAN_MARK': 'Mulligan_X',
    'UI_MULLIGAN_REPLACE': 'Mulligan_ReplaceKeep',
    'UI_STANDARD_BUTTON': 'StandardButton',
    'UI_MINION_READY': 'GenFX_AllyInPlayGlow',
    'UI_CARD_GLOW': 'GenFX_CardGlow_Generic1',
    'UI_VICTORY': 'Victory',
    'UI_TARGET_ARROW': 'ArrowTail_D',
    'UI_TARGET_HEAD': 'ArrowTargetA_D',
    'VFX_IMPACT_SOURCE': 'GenFX_ArrowImpactGlow',
    'UI_END_TURN': 'Board_EndTurnButton',
    'VFX_FIREBALL_BOLT': 'GenFX_Fireball_1Hot',
    'UI_GENFX_HEROFRAME': 'GenFX_HeroFrame',
    'UI_GENFX_CARD_INPLAY_ALLYFRAME': 'GenFX_Card_InPlay_AllyFrame',
    'UI_GENFX_CARD_INHAND_BANNERATLAS': 'GenFX_Card_InHand_BannerAtlas',
}
AUDIO = {
    'SFX_TURN_START': 'ALERT_YourTurn_0v2',
    'SFX_END_TURN': 'FX_EndTurn_Down',
    'SFX_ATTACK_IMPACT': 'FX_Minion_AttackImpact',
    'SFX_ARMOR_HIT': 'Armor_Splat_Damage',
    'SFX_MULLIGAN_CONFIRM': 'FX_MulliganCoin09_DeckShuffle',
    'SFX_SPELL_IMPACT': 'FX_FireballEvent04_SpellImpact_01',
    'SFX_TRANSFORM': 'Shared_PolymorphHex_Impact',
    'SFX_VICTORY': 'victory_jingle',
    'SFX_DEFEAT': 'defeat_jingle',
    'SFX_MINION_DEATH': 'Minion_Death_01',
    'SFX_CARD_DRAW': 'draw_card_1',
    'SFX_UI_CLICK': 'box_hub_button_press_2',
    'SFX_UI_HOVER': 'box_large_button_mouse_over',
    'SFX_CARD_PLAY': 'FX_MinionSummon01_DrawFromHand_01',
    'SFX_SUMMON': 'FX_MinionSummon_Drop',
    'SFX_ATTACK_LAUNCH': 'FX_Minion_AttackLaunch',
    'BGM_MAIN_TITLE': 'Main_Title',
    'BGM_DUEL': 'Duel',
}
FONTS = {'FONT_NUMBERS_SOURCE': 'BelweNumbers', 'FONT_LABEL_SOURCE': 'UGCSH00M',
         'FONT_BODY_SOURCE': 'gkai00m'}
SCENES = {
    'SCENE_BOARD': ('Board_STR', 'essential_base_qualityhigh_global-content-0.unity3d'),
    'SCENE_MAIN_MENU': ('TheBox', 'essential_base_box_global-prefab-0.unity3d'),
    'MODEL_BOARD_TABLE': ('TableTop', 'essential_base_qualityhigh_global-content-0.unity3d'),
    'MODEL_HERO': ('GenFX_Card_Hero_v3', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_HERO_POWER': ('Card_InPlay_HeroPowerV2', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_CARD_MINION': ('GenFX_Card_InHand_Ally_v3', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_CARD_SPELL': ('Card_InHand_Ability', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_CARD_WEAPON': ('Card_InHand_Weapon_v3', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_MINION': ('GenFX_Card_InPlay_Ally_v2', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_CARD_BACK': ('Card_Back_Default', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_GEM_MANA': ('Gem_Mana', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_GEM_ATTACK': ('Gem_Attack', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_GEM_HEALTH': ('Gem_Health', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_MULLIGAN_BANNER': ('BannerMulligan', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_MULLIGAN_BUTTON': ('MulliganButton', 'essential_base_global-prefab-0.unity3d'),
}
PREFAB_SOURCES = {
    'SOURCE_GAMESTART_VS_LETTERS': ('GameStart_VS_Letters', 'essential_base_global-prefab-0.unity3d',
                                    '3cb2cbed6d44a694eb23fb8791684003'),
}
INSPECTED_ONLY = {'UI_VS_LETTERS_ATLAS', 'UI_MULLIGAN_BANNER', 'UI_MULLIGAN_REPLACE', 'UI_STANDARD_BUTTON',
                  'UI_MINION_READY', 'UI_CARD_GLOW', 'UI_VICTORY', 'UI_TARGET_ARROW',
                  'UI_TARGET_HEAD', 'VFX_IMPACT_SOURCE'}


def _rotate(quaternion, vector):
    x, y, z, w = (quaternion[key] for key in ('x', 'y', 'z', 'w'))
    q = (x, y, z)
    cross = (q[1] * vector[2] - q[2] * vector[1],
             q[2] * vector[0] - q[0] * vector[2],
             q[0] * vector[1] - q[1] * vector[0])
    doubled = tuple(2 * value for value in cross)
    second = (q[1] * doubled[2] - q[2] * doubled[1],
              q[2] * doubled[0] - q[0] * doubled[2],
              q[0] * doubled[1] - q[1] * doubled[0])
    return tuple(vector[i] + w * doubled[i] + second[i] for i in range(3))


def _transform_point(vertex, chain):
    point = tuple(vertex)
    for transform in chain:
        scale = transform['m_LocalScale']
        point = _rotate(transform['m_LocalRotation'], tuple(point[i] * scale['xyz'[i]] for i in range(3)))
        position = transform['m_LocalPosition']
        point = tuple(point[i] + position['xyz'[i]] for i in range(3))
    return point


def _xyz(value):
    if isinstance(value, dict):
        return tuple(float(value[key]) for key in ('x', 'y', 'z'))
    return tuple(float(value[index]) for index in range(3))


def _xy(value):
    if isinstance(value, dict):
        return float(value['x']), float(value['y'])
    return float(value[0]), float(value[1])


def _uv(value):
    return _xy(value)


def _texture_sampling_settings(texture):
    texture_tree = texture.read_typetree()
    settings = texture_tree.get('m_TextureSettings')
    required = ('m_FilterMode', 'm_Aniso', 'm_MipBias', 'm_WrapU', 'm_WrapV', 'm_WrapW')
    if not isinstance(settings, dict) or any(key not in settings for key in required):
        raise ValueError('VS source Texture2D has incomplete m_TextureSettings')
    return {'m_FilterMode': int(settings['m_FilterMode']),
            'm_Aniso': int(settings['m_Aniso']),
            'm_MipBias': float(settings['m_MipBias']),
            'm_WrapU': int(settings['m_WrapU']),
            'm_WrapV': int(settings['m_WrapV']),
            'm_WrapW': int(settings['m_WrapW'])}


def _repeat_uv_to_pixel(uv, texture_scale, texture_offset, texture_size):
    width, height = texture_size
    u = uv[0] * texture_scale[0] + texture_offset[0]
    v = uv[1] * texture_scale[1] + texture_offset[1]
    x = (u + 1) * width - .5
    y = (2 - v) * height - .5
    if x < 1 or x > width * 3 - 2 or y < 1 or y > height * 3 - 2:
        raise ValueError('VS source UV exceeds the supported 3x3 Repeat sampling atlas')
    return x, y


def _repeat_atlas(atlas):
    width, height = atlas.size
    tiled = Image.new('RGBA', (width * 3, height * 3))
    for y in range(3):
        for x in range(3):
            tiled.paste(atlas, (x * width, y * height))
    return tiled


def _affine_from_triangles(source, target):
    (x0, y0), (x1, y1), (x2, y2) = target
    (u0, v0), (u1, v1), (u2, v2) = source
    determinant = x0 * (y1 - y2) + x1 * (y2 - y0) + x2 * (y0 - y1)
    if abs(determinant) < 1e-10:
        return None
    a = (u0 * (y1 - y2) + u1 * (y2 - y0) + u2 * (y0 - y1)) / determinant
    b = (u0 * (x2 - x1) + u1 * (x0 - x2) + u2 * (x1 - x0)) / determinant
    c = (u0 * (x1 * y2 - x2 * y1) + u1 * (x2 * y0 - x0 * y2) + u2 * (x0 * y1 - x1 * y0)) / determinant
    d = (v0 * (y1 - y2) + v1 * (y2 - y0) + v2 * (y0 - y1)) / determinant
    e = (v0 * (x2 - x1) + v1 * (x0 - x2) + v2 * (x1 - x0)) / determinant
    f = (v0 * (x1 * y2 - x2 * y1) + v1 * (x2 * y0 - x0 * y2) + v2 * (x0 * y1 - x1 * y0)) / determinant
    return a, b, c, d, e, f


def rasterize_vs_mesh(atlas, meshes, texture_scale=(1, 1), texture_offset=(0, 0),
                      texture_settings=None, tint=(1, 1, 1, 1), resolution=512, supersample=2):
    """Depth-render source triangles visible from +Y into an orthographic X/Z view."""
    if resolution < 64 or supersample < 1 or not meshes:
        raise ValueError('VS mesh raster settings or source meshes are invalid')
    if texture_settings is None or any(key not in texture_settings for key in
                                       ('m_FilterMode', 'm_Aniso', 'm_MipBias', 'm_WrapU', 'm_WrapV', 'm_WrapW')):
        raise ValueError('VS source texture sampling settings are missing')
    if (texture_settings['m_FilterMode'] != 1 or texture_settings['m_Aniso'] != 1 or
            texture_settings['m_MipBias'] != 0 or texture_settings['m_WrapU'] != 0 or
            texture_settings['m_WrapV'] != 0 or texture_settings['m_WrapW'] != 0):
        raise ValueError('VS source texture sampling only supports bilinear, aniso=1, mipBias=0 and Repeat wrapping')
    sampled_atlas = _repeat_atlas(atlas)
    faces = []
    triangle_count = 0
    for mesh in meshes:
        vertices = [_xyz(vertex) for vertex in mesh['vertices']]
        uvs = [_uv(uv) for uv in mesh['uv']]
        chain = mesh['transform_chain']
        positions = [_transform_point(vertex, chain) for vertex in vertices]
        for submesh in mesh['triangles']:
            for triangle in submesh:
                if len(triangle) != 3 or any(index < 0 or index >= len(vertices) for index in triangle):
                    raise ValueError('VS source mesh contains invalid triangle indices')
                face_positions = [positions[index] for index in triangle]
                a, b, c = face_positions
                edge_a = tuple(b[i] - a[i] for i in range(3))
                edge_b = tuple(c[i] - a[i] for i in range(3))
                normal = (edge_a[1] * edge_b[2] - edge_a[2] * edge_b[1],
                          edge_a[2] * edge_b[0] - edge_a[0] * edge_b[2],
                          edge_a[0] * edge_b[1] - edge_a[1] * edge_b[0])
                length = math.sqrt(sum(value * value for value in normal))
                if length < 1e-10:
                    continue
                if normal[1] / length <= .005:
                    continue
                if any(index >= len(uvs) for index in triangle):
                    raise ValueError('VS source mesh triangle is missing UV0 coordinates')
                face_uvs = [uvs[index] for index in triangle]
                faces.append((face_positions, face_uvs))
                triangle_count += 1
    if not faces:
        raise ValueError('VS prefab has no triangles facing the verified +Y view')

    points = [point for triangle, _ in faces for point in triangle]
    min_x, max_x = min(point[0] for point in points), max(point[0] for point in points)
    min_z, max_z = min(point[2] for point in points), max(point[2] for point in points)
    width, height = max_x - min_x, max_z - min_z
    if width <= 1e-8 or height <= 1e-8:
        raise ValueError('VS front-face geometry has empty projected bounds')
    side = resolution * supersample
    margin = 32
    scale = (side - 2 * margin) / max(width, height)
    projected = []
    for triangle, uvs in faces:
        target = [((point[0] - min_x) * scale + margin,
                   (max_z - point[2]) * scale + margin) for point in triangle]
        source = [_repeat_uv_to_pixel(uv, texture_scale, texture_offset, atlas.size) for uv in uvs]
        projected.append((target, [point[1] for point in triangle], source))

    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas_pixels = canvas.load()
    depth_buffer = array('f', [-math.inf]) * (side * side)
    affine_mode = getattr(Image, 'Transform', Image).AFFINE
    resampling = getattr(Image, 'Resampling', Image)
    tint_pixel = tuple(max(0, min(255, round(component * 255))) for component in tint)
    tint_image = Image.new('RGBA', (side, side), tint_pixel)
    for target, depths, source in projected:
        xs, ys = [point[0] for point in target], [point[1] for point in target]
        left = max(0, math.floor(min(xs)))
        top = max(0, math.floor(min(ys)))
        right = min(side, math.ceil(max(xs)) + 1)
        bottom = min(side, math.ceil(max(ys)) + 1)
        if right <= left or bottom <= top:
            continue
        affine = _affine_from_triangles(source, target)
        if affine is None:
            continue
        a, b, c, d, e, f = affine
        patch = sampled_atlas.transform((right - left, bottom - top), affine_mode,
                                        (a, b, c + a * left + b * top,
                                         d, e, f + d * left + e * top),
                                resample=resampling.BILINEAR)
        patch = ImageChops.multiply(patch, tint_image.crop((left, top, right, bottom)))
        mask = Image.new('L', patch.size, 0)
        ImageDraw.Draw(mask).polygon([(x - left, y - top) for x, y in target], fill=255)
        mask_pixels = mask.load()
        patch_pixels = patch.load()
        (x0, y0), (x1, y1), (x2, y2) = target
        denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(denominator) < 1e-10:
            continue
        # Keep the frontmost source triangle at each projected pixel.
        for y in range(top, bottom):
            py = y + .5
            row = y * side
            for x in range(left, right):
                if not mask_pixels[x - left, y - top]:
                    continue
                px = x + .5
                weight0 = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / denominator
                weight1 = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / denominator
                weight2 = 1 - weight0 - weight1
                depth = weight0 * depths[0] + weight1 * depths[1] + weight2 * depths[2]
                index = row + x
                if depth <= depth_buffer[index]:
                    continue
                depth_buffer[index] = depth
                canvas_pixels[x, y] = patch_pixels[x - left, y - top]
    bounds = canvas.getchannel('A').getbbox()
    if bounds is None:
        raise ValueError('source VS front-face raster is transparent')
    sprite = canvas.crop(bounds)
    output_size = (max(1, round(sprite.width / supersample)),
                   max(1, round(sprite.height / supersample)))
    return sprite.resize(output_size, resampling.LANCZOS), bounds, triangle_count


def _asset_identity(ex, obj):
    cab = obj.assets_file.name
    bundle = (ex.imp.reader.cabs.get(cab.lower()) or ex.imp.reader.cabs.get(cab) or
              ex.imp.reader.file_bundle.get(cab.lower()))
    row = ex.imp.store.db.execute(
        'SELECT * FROM assets WHERE bundle=? AND lower(cab)=lower(?) AND pathid=? AND type=?',
        (bundle, cab, str(obj.path_id), obj.type.name)).fetchone() if bundle else None
    if row is None:
        raise ValueError(f'VS prefab dependency is not indexed: {obj.type.name}/{cab}/{obj.path_id}')
    return row, {'name': row['name'] or obj.peek_name() or '', 'asset_id': row['id'],
                 'bundle': row['bundle'], 'cab': row['cab'], 'pathid': row['pathid'],
                 'type': row['type'], 'guids': json.loads(row['guids'])}


def _components(ex, game_object):
    return [ex.imp.reader.pointer(game_object, entry['component'])
            for entry in game_object.read_typetree().get('m_Component', [])
            if entry.get('component', {}).get('m_PathID')]


def _letter_objects(ex, root):
    reader = ex.imp.reader
    pending = [root]
    visited = set()
    found = {}
    while pending:
        game_object = pending.pop()
        key = (game_object.assets_file.name, game_object.path_id)
        if key in visited:
            continue
        visited.add(key)
        tree = game_object.read_typetree()
        name = tree.get('m_Name', '')
        if name in ('Letter_V', 'Letter_S'):
            if name in found:
                raise ValueError(f'VS source prefab contains duplicate {name} objects')
            found[name] = (game_object, tree, _components(ex, game_object))
        transforms = [component for component in _components(ex, game_object)
                      if component is not None and component.type.name == 'Transform']
        if len(transforms) != 1:
            continue
        transform = transforms[0]
        for child_ref in transform.read_typetree().get('m_Children', []):
            child = reader.pointer(transform, child_ref)
            if child is None:
                continue
            child_object = reader.pointer(child, child.read_typetree()['m_GameObject'])
            if child_object is not None:
                pending.append(child_object)
    if set(found) != {'Letter_V', 'Letter_S'}:
        raise ValueError('GameStart_VS_Letters does not contain both source Letter_V and Letter_S meshes')
    return found


def _transform_chain(ex, game_object, stop_at):
    reader = ex.imp.reader
    current = game_object
    chain = []
    seen = set()
    while current is not None:
        key = (current.assets_file.name, current.path_id)
        if key in seen:
            raise ValueError('VS source prefab transform parent chain contains a cycle')
        seen.add(key)
        name = current.read_typetree().get('m_Name', '')
        transform = next((component for component in _components(ex, current)
                          if component is not None and component.type.name == 'Transform'), None)
        if transform is None:
            raise ValueError(f'VS source transform is missing on {name}')
        tree = transform.read_typetree()
        _, identity = _asset_identity(ex, transform)
        source_id = {
            'GameStart_VS_Letters': 'SOURCE_GAMESTART_VS_LETTERS_TRANSFORM',
            'GameStart_VS_Letters_Root': 'SOURCE_VS_LETTERS_GROUP_TRANSFORM',
            'Letter_V': 'SOURCE_VS_LETTER_V_TRANSFORM',
            'Letter_S': 'SOURCE_VS_LETTER_S_TRANSFORM',
        }.get(name)
        if source_id is None:
            raise ValueError(f'VS source transform hierarchy contains an unknown node: {name}')
        chain.append({'source_id': source_id, 'name': name, 'tree': tree, 'identity': identity})
        if (current.assets_file.name, current.path_id) == (stop_at.assets_file.name, stop_at.path_id):
            break
        parent = reader.pointer(transform, tree.get('m_Father', {}))
        if parent is None:
            raise ValueError(f'{name} is not parented under the source GameStart_VS_Letters root')
        parent_tree = parent.read_typetree()
        current = reader.pointer(parent, parent_tree['m_GameObject'])
    expected = ['Letter_V' if game_object.read_typetree()['m_Name'] == 'Letter_V' else 'Letter_S',
                'GameStart_VS_Letters_Root', 'GameStart_VS_Letters']
    if [row['name'] for row in chain] != expected:
        raise ValueError('VS source transform parent chain differs from the original letter/group/prefab hierarchy')
    source_rows = []
    for index, row in enumerate(chain):
        tree = row['tree']
        source_rows.append({'id': row['source_id'], **row['identity'], 'usage': 'source_only', 'cache': None,
                            'parent_transform_id': chain[index + 1]['source_id'] if index + 1 < len(chain) else '',
                            'local_position': [tree['m_LocalPosition'][axis] for axis in ('x', 'y', 'z')],
                            'local_rotation': [tree['m_LocalRotation'][axis] for axis in ('x', 'y', 'z', 'w')],
                            'local_scale': [tree['m_LocalScale'][axis] for axis in ('x', 'y', 'z')]})
    return [row['tree'] for row in chain], [row['source_id'] for row in chain], source_rows


def export_vs_letters(ex):
    name, bundle, guid = PREFAB_SOURCES['SOURCE_GAMESTART_VS_LETTERS']
    rows = ex.imp.store.db.execute(
        'SELECT * FROM assets WHERE name=? AND type=? AND bundle=? AND guids LIKE ?',
        (name, 'GameObject', bundle, '%' + guid + '%')).fetchall()
    if len(rows) != 1:
        raise ValueError('GameStart_VS_Letters source GUID does not resolve to one prefab')
    prefab_row = rows[0]
    env = ex.imp.reader.load(prefab_row['bundle'])
    root = next((obj for obj in env.objects if obj.type.name == 'GameObject' and
                 obj.assets_file.name == prefab_row['cab'] and str(obj.path_id) == prefab_row['pathid']), None)
    if root is None:
        raise ValueError('GameStart_VS_Letters prefab object is missing from its source CAB')

    letters = _letter_objects(ex, root)
    source_meshes = []
    source_components = []
    source_transforms = {}
    materials = []
    texture = None
    texture_identity = None
    material_settings = None
    mesh_geometry = []
    for label in ('Letter_V', 'Letter_S'):
        game_object, _, components = letters[label]
        filters = [component for component in components if component.type.name == 'MeshFilter']
        renderers = [component for component in components if component.type.name == 'MeshRenderer']
        if len(filters) != 1 or len(renderers) != 1:
            raise ValueError(f'{label} must have one source MeshFilter and MeshRenderer')
        mesh = ex.imp.reader.pointer(filters[0], filters[0].read_typetree()['m_Mesh'])
        _, mesh_identity = _asset_identity(ex, mesh)
        expected_mesh = 'Letter_V_New' if label == 'Letter_V' else 'Letter_S_New'
        if mesh_identity['name'] != expected_mesh:
            raise ValueError(f'{label} resolves to an unexpected source mesh: {mesh_identity["name"]}')
        _, filter_identity = _asset_identity(ex, filters[0])
        _, renderer_identity = _asset_identity(ex, renderers[0])
        source_components.extend([
            {'id': 'SOURCE_VS_' + label.upper() + '_MESH_FILTER', **filter_identity,
             'usage': 'source_only', 'cache': None,
             'mesh_id': 'SOURCE_VS_' + label.upper() + '_MESH'},
            {'id': 'SOURCE_VS_' + label.upper() + '_MESH_RENDERER', **renderer_identity,
             'usage': 'source_only', 'cache': None,
             'material_ids': ['SOURCE_VS_LETTERS_MATERIAL']},
        ])
        handler = MeshHandler(mesh.read())
        handler.process()
        vertices = [_xyz(vertex) for vertex in (handler.m_Vertices or [])]
        uv = handler.m_UV0 or []
        triangles = handler.get_triangles()
        if not vertices or len(uv) != len(vertices) or not triangles:
            raise ValueError(f'{label} source mesh has incomplete vertex/UV0 triangle data')
        transform_chain, transform_ids, transform_rows = _transform_chain(ex, game_object, root)
        for transform_row in transform_rows:
            existing = source_transforms.get(transform_row['id'])
            if existing is not None and existing != transform_row:
                raise ValueError(f"VS transform source identity changed: {transform_row['id']}")
            source_transforms[transform_row['id']] = transform_row
        mesh_geometry.append({'name': label, 'vertices': vertices, 'uv': uv,
                              'triangles': triangles, 'transform_chain': transform_chain})
        source_meshes.append({'id': 'SOURCE_VS_' + label.upper() + '_MESH', **mesh_identity,
                              'usage': 'source_only', 'cache': None,
                              'vertex_count': len(vertices),
                              'uv0_vertex_count': len(uv),
                              'triangle_count': sum(len(group) for group in triangles),
                              'transform_ids': transform_ids,
                              'uv0_bounds': [min(point[0] for point in uv), min(point[1] for point in uv),
                                             max(point[0] for point in uv), max(point[1] for point in uv)]})

        material_refs = renderers[0].read_typetree().get('m_Materials', [])
        if len(material_refs) != 1:
            raise ValueError(f'{label} source MeshRenderer does not have one material')
        material = ex.imp.reader.pointer(renderers[0], material_refs[0])
        _, material_identity = _asset_identity(ex, material)
        if materials and (material_identity['cab'], material_identity['pathid']) != (
                materials[0]['cab'], materials[0]['pathid']):
            raise ValueError('source VS letters do not share their verified material')
        material_tree = material.read_typetree()
        slots = dict(material_tree.get('m_SavedProperties', {}).get('m_TexEnvs', []))
        main_texture = slots.get('_MainTex')
        if main_texture is None:
            raise ValueError('source VS letter material has no _MainTex')
        colors = dict(material_tree.get('m_SavedProperties', {}).get('m_Colors', []))
        color = colors.get('_Color')
        if not all(key in main_texture for key in ('m_Scale', 'm_Offset')) or color is None:
            raise ValueError('source VS letter material is missing texture transform or _Color data')
        settings = {
            'texture_scale': _xy(main_texture['m_Scale']),
            'texture_offset': _xy(main_texture['m_Offset']),
            'tint': tuple(float(color.get(axis, 1)) for axis in ('r', 'g', 'b', 'a')),
        }
        if material_settings is not None and settings != material_settings:
            raise ValueError('source VS letter renderers do not share one texture transform and tint')
        material_settings = settings
        source_texture = ex.imp.reader.pointer(material, main_texture['m_Texture'])
        _, source_texture_identity = _asset_identity(ex, source_texture)
        if texture is not None and (source_texture_identity['cab'], source_texture_identity['pathid']) != (
                texture_identity['cab'], texture_identity['pathid']):
            raise ValueError('source VS letters do not share one _MainTex texture')
        texture, texture_identity = source_texture, source_texture_identity
        if not materials:
            materials.append({'id': 'SOURCE_VS_LETTERS_MATERIAL', **material_identity,
                              'usage': 'source_only', 'cache': None,
                              'main_texture_id': 'UI_VS_LETTERS_ATLAS',
                              **settings,
                              'shader': ex.imp.reader.pointer(material, material_tree['m_Shader']).peek_name()})

    if texture_identity['name'] != 'VS_letters':
        raise ValueError(f'GameStart_VS_Letters _MainTex is not VS_letters: {texture_identity["name"]}')
    texture_settings = _texture_sampling_settings(texture)
    raw_cache = ex.texture(texture)
    ex.registry['UI_VS_LETTERS_ATLAS'] = raw_cache
    atlas = Image.open(ex.folder / raw_cache).convert('RGBA')
    sprite, raster_bounds, triangle_count = rasterize_vs_mesh(
        atlas, mesh_geometry, texture_scale=material_settings['texture_scale'],
        texture_offset=material_settings['texture_offset'], texture_settings=texture_settings,
        tint=material_settings['tint'])
    target = ex.folder / 'UI_VS_LETTERS_geometry.png'
    sprite.save(target, format='PNG', optimize=True)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    ex.registry['UI_VS_LETTERS'] = target.name
    ex.provenance.append({'id': 'UI_VS_LETTERS', 'file': target.name, 'sha256': digest,
                          'derived_from': 'UI_VS_LETTERS_ATLAS', 'mesh_sources': [row['id'] for row in source_meshes],
                          'transform_sources': list(source_transforms),
                          'derivation': 'depth-tested orthographic XZ projection of source Letter_V and Letter_S mesh triangles facing +Y, with original transform hierarchy, UV0, material texture transform and tint',
                          'raster_bounds_supersampled_px': list(raster_bounds),
                          'texture_settings': texture_settings,
                          'source_contract_limit': 'static visible letter mesh geometry with base-level bilinear Repeat sampling only; VS_Shadow sibling, original prefab animation, camera, lighting, mip selection and controller behavior are not claimed'})

    atlas_source = {'id': 'UI_VS_LETTERS_ATLAS', **texture_identity, 'cache': raw_cache,
                    'usage': 'inspected_only', 'texture_settings': texture_settings}
    derived_source = {'id': 'UI_VS_LETTERS', 'name': 'VS_letters original front-facing mesh projection',
                      'type': 'Texture2D', 'bundle': '', 'cab': '', 'pathid': '', 'asset_id': '',
                      'cache': target.name, 'usage': 'runtime', 'derived_from': 'UI_VS_LETTERS_ATLAS',
                      'sha256': digest, 'width': sprite.width, 'height': sprite.height,
                      'alpha_mask_bounds_px': [0, 0, sprite.width, sprite.height],
                      'raster_bounds_supersampled_px': list(raster_bounds), 'source_triangle_count': triangle_count,
                      'texture_settings': texture_settings,
                      'projection': 'depth_tested_orthographic_world_xz_from_positive_y',
                      'source_contract_limit': 'static visible letter mesh geometry with base-level bilinear Repeat sampling only; VS_Shadow sibling, original prefab animation, camera, lighting, mip selection and controller behavior are not claimed',
                      'source_assets': ['SOURCE_GAMESTART_VS_LETTERS'] +
                                       [row['id'] for row in source_meshes + source_components] +
                                       list(source_transforms) + [materials[0]['id']]}
    return [atlas_source, *source_meshes, *source_components, *source_transforms.values(), *materials, derived_source]


def main():
    ex = RuntimeExport()
    for asset_id, name in TEXTURES.items():
        ex.named(asset_id, name)
    for asset_id, name in AUDIO.items():
        ex.named(asset_id, name, 'AudioClip')
    ex.scene('MODEL_MULLIGAN_BANNER', 'BannerMulligan', 'essential_base_global-mesh-0.unity3d')
    ex.scene('MODEL_MULLIGAN_BUTTON', 'MulliganButton', 'essential_base_global-prefab-0.unity3d')
    ex.scene('MODEL_BOARD_TABLE', 'TableTop', 'essential_base_qualityhigh_global-content-0.unity3d')
    for asset_id, name in FONTS.items():
        row = ex.imp.store.db.execute(
            'SELECT * FROM assets WHERE type=? AND name=? AND bundle NOT LIKE ? LIMIT 1',
            ('Font', name, '@%')).fetchone()
        try:
            obj = next(o for o in ex.imp.reader.load(row['bundle']).objects
                       if str(o.path_id) == row['pathid'] and o.assets_file.name == row['cab'])
            target = ex.folder / (name + '.ttf')
            target.write_bytes(bytes(obj.read_typetree()['m_FontData']))
            ex.registry[asset_id] = target.name
            ex.provenance.append({'file': target.name, 'asset_id': row['id'],
                                  'bundle': row['bundle'], 'cab': row['cab'],
                                  'pathid': row['pathid'], 'type': 'Font'})
        except Exception as exc:
            ex.errors.append({'id': asset_id, 'error': str(exc)})
    vs_sources = []
    try:
        vs_sources = export_vs_letters(ex)
    except Exception as exc:
        ex.errors.append({'id': 'UI_VS_LETTERS', 'error': f'{type(exc).__name__}: {exc}'})
    ex.save()
    source = []
    for asset_id, name in {**TEXTURES, **AUDIO, **FONTS}.items():
        kind = 'Font' if asset_id in FONTS else 'AudioClip' if asset_id in AUDIO else 'Texture2D'
        rows = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle',
                                       (name, kind)).fetchall()
        row = next((r for r in rows if kind != 'Font' or not r['bundle'].startswith('@')), None)
        if row:
            source.append({'id': asset_id, 'name': name, 'bundle': row['bundle'],
                           'cab': row['cab'], 'pathid': row['pathid'], 'type': row['type'],
                           'guids': json.loads(row['guids']), 'cache': ex.registry.get(asset_id),
                           'usage': 'inspected_only' if asset_id in INSPECTED_ONLY else 'runtime',
                           'asset_id': row['id']})
    source.extend(vs_sources)
    for asset_id, (name, bundle) in SCENES.items():
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=?',
                                      (name, 'GameObject', bundle)).fetchone()
        if row:
            source.append({'id': asset_id, 'name': name, 'bundle': bundle, 'cab': row['cab'],
                           'pathid': row['pathid'], 'type': row['type'], 'guids': json.loads(row['guids']),
                           'cache': ex.registry.get(asset_id), 'usage': 'runtime', 'asset_id': row['id']})
    for source_id, (name, bundle, guid) in PREFAB_SOURCES.items():
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=? AND guids LIKE ?',
                                      (name, 'GameObject', bundle, '%' + guid + '%')).fetchone()
        if row:
            source.append({'id': source_id, 'name': name, 'bundle': row['bundle'], 'cab': row['cab'],
                           'pathid': row['pathid'], 'type': row['type'], 'guids': json.loads(row['guids']),
                           'cache': None, 'usage': 'source_only', 'asset_id': row['id'],
                           'static_runtime_reference': 'MulliganManager.HandleGameStart'})
    (ex.folder / 'presentation-provenance.json').write_text(
        json.dumps({'assets': source, 'errors': ex.errors}, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest = ex.imp.local.parent / 'manifests'
    manifest.mkdir(exist_ok=True)
    (manifest / 'presentation-assets.json').write_text(
        json.dumps({'assets': source, 'errors': ex.errors}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'assets': len(source), 'errors': ex.errors}, ensure_ascii=False))


if __name__ == '__main__':
    main()
