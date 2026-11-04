"""Recover the actual standard hero-power UI chain and targeting resources."""
import hashlib
import json
import sys

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import export_program, animation, identity, material
from .native_audio import recover as recover_audio
from .smoke_source_textures import recover_texture
from UnityPy.helpers.MeshHelper import MeshHandler


def mesh_colors(data, handler):
    colors = [v for row in (handler.m_Colors or []) for v in row[:4]]
    if not colors:
        return [], 'none'
    channel = data.m_VertexData.m_Channels[3]
    if channel.format == 2:
        # MeshHandler保留UNorm8整数；上传Unity Color(float)前按原顶点格式归一化。
        if any(v < 0 or v > 255 or int(v) != v for v in colors):
            raise ValueError('UNorm8 vertex color outside source byte domain')
        return [v / 255 for v in colors], 'UNorm8_to_float'
    if channel.format == 0:
        return colors, 'Float32'
    raise ValueError(f'Unreviewed Fireblast vertex color format {channel.format}')


def visual_graph(ex, logical, reference):
    graph = json.loads((ex.folder / (logical + '.scene.json')).read_text(encoding='utf-8'))
    root, _, _ = ex.imp.reader.resolve(reference)
    ordered = []
    def visit(go, path):
        components = [ex.imp.reader.pointer(go, item['component']) for item in go.read_typetree()['m_Component']]
        components = [obj for obj in components if obj is not None]
        transform = next(obj for obj in components if obj.type.name == 'Transform')
        ordered.append((go, path, components))
        for pointer in transform.read_typetree()['m_Children']:
            child = ex.imp.reader.pointer(transform, pointer)
            childgo = ex.imp.reader.pointer(child, child.read_typetree()['m_GameObject'])
            name = childgo.read_typetree()['m_Name']
            visit(childgo, (path + '/' if path else '') + name)
    visit(root, '')
    if len(ordered) != len(graph['nodes']):
        raise ValueError('Exact source UI hierarchy differs from scene export')
    texts, errors, highlight = [], [], None
    for index, (node, (go, path, components)) in enumerate(zip(graph['nodes'], ordered)):
        node.update(path=path, visible=True, sorting_order=0, particle=None,
                    animation_refs=[], animator_states=[])
        renderer = next((obj for obj in components if obj.type.name == 'MeshRenderer'), None)
        node['materials'] = [material(ex, ex.imp.reader.pointer(renderer, pointer), errors)
                             for pointer in renderer.read_typetree()['m_Materials']] if renderer else []
        mesh_filter = next((obj for obj in components if obj.type.name == 'MeshFilter'), None)
        if mesh_filter:
            mesh = ex.imp.reader.pointer(mesh_filter, mesh_filter.read_typetree()['m_Mesh'])
            data = mesh.read()
            h = MeshHandler(data)
            h.process()
            colors, encoding = mesh_colors(data, h)
            filename = ex.key(mesh) + '.fireblast.mesh.json'
            atomic_json(ex.folder / filename, dict(
                vertices=[v for row in h.m_Vertices for v in row[:3]],
                uv=[v for row in (h.m_UV0 or []) for v in row[:2]],
                uv2=[v for row in (h.m_UV1 or []) for v in row[:2]],
                normals=[v for row in (h.m_Normals or []) for v in row[:3]],
                source_normals=True,
                colors=colors, color_encoding=encoding,
                submeshes=[dict(triangles=[v for tri in t for v in tri]) for t in h.get_triangles()]))
            node['mesh'] = filename
        for obj in components:
            if obj.type.name != 'MonoBehaviour':
                continue
            raw = obj.read_typetree()
            if 'm_CharacterSize' in raw and 'm_TextColor' in raw:
                texts.append(dict(node=index, name=node['name'], width=raw['m_Width'], height=raw['m_Height'],
                    font_size=raw['m_FontSize'], character_size=raw['m_CharacterSize'],
                    outline=bool(raw['m_Outline']), outline_size=raw['m_OutlineSize'],
                    color=raw['m_TextColor'], word_wrap=bool(raw['m_WordWrap']), raw=raw, source=identity(ex, obj)))
            if 'm_HighlightStates' in raw and 'm_StaticSilouetteTexture' in raw:
                texture = ex.imp.reader.pointer(obj, raw['m_StaticSilouetteTexture'])
                highlight = dict(source=identity(ex, obj), node=index, silhouette=ex.texture(texture),
                    queue=raw['m_RenderQueue'] + raw['m_RenderQueueOffset'],
                    states=[dict(state=state['m_StateType'], offset=state['m_Offset'],
                        material=material(ex, ex.imp.reader.pointer(obj, state['m_Material']), errors)
                            if state['m_Material']['m_PathID'] else None) for state in raw['m_HighlightStates']])
    graph['nodes'][0]['position'] = dict(x=0, y=0, z=0)
    graph.update(reference=reference, source=identity(ex, root), machines=[], animations=[], captures=[], unknown_actions=[])
    filename = logical + '.fireblast-visual.json'
    atomic_json(ex.folder / filename, graph)
    return dict(file=filename, texts=texts, errors=errors, highlight=highlight)


def main():
    ex = RuntimeExport()
    if '--font-settings' in sys.argv:
        report = json.loads((ex.folder / 'fireblast-ui.json').read_text(encoding='utf-8'))
        for font in report['fonts']:
            source = font['definition']
            owner = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
            raw = owner.read_typetree()
            font.update(character_size_modifier=raw['m_CharacterSizeModifier'], single_line_adjustment=raw['m_SingleLineAdjustment'])
        manifest = json.loads((PROJECT / 'manifests/fireblast-ui-source.json').read_text(encoding='utf-8'))
        manifest['fonts'] = report['fonts']
        with asset_lock(ex.imp.local):
            atomic_json(ex.folder / 'fireblast-ui.json', report)
            atomic_json(PROJECT / 'manifests/fireblast-ui-source.json', manifest)
        print('FIREBLAST FONT SETTINGS', len(report['fonts']))
        return
    roots = (
        ('MODEL_MAGE_POWER_BUTTON', 'Card_Play_HeroPower', 'a3794839abb947146903a26be13e09af'),
        ('MODEL_MAGE_POWER_DETAIL', 'History_HeroPower', 'e73edf8ccea2b11429093f7a448eef53'),
        ('MODEL_MAGE_POWER_DETAIL_ENEMY', 'History_HeroPower_Opponent', 'a99d23d6e8630f94b96a8e096fffb16f'),
    )
    report = dict(roots=[], fonts=[], arrows=[], animations=[], sounds=[], textures=[],
                  mesh_color_schema='source-channel-v2', official_fidelity_verified=False)
    with asset_lock(ex.imp.local):
        for logical, name, guid in roots:
            reference = name + '.prefab:' + guid
            ex.scene(logical, name, source_ref=reference)
            obj, _, _ = ex.imp.reader.resolve(reference)
            report['roots'].append(dict(id=logical, reference=reference, source=identity(ex, obj),
                                        **visual_graph(ex, logical, reference)))
        for reference in ('Target_Arrow_Bullseye.prefab:7afe007e5f455b04b9407307d8df1983',
                          'Target_Arrow_Link.prefab:eb929158148ae954881c5684d27a1aa2'):
            filename, audit = export_program(ex, reference, suffix='.target-arrow.json')
            report['arrows'].append(dict(reference=reference, file=filename, source=audit))
        textures = {}
        portrait, _, _ = ex.imp.reader.resolve('R6_a075_D.png:ffec72f995c63e34d8b0919a4b492a1d')
        definition = recover_texture(ex, portrait)
        textures[definition['png']] = definition
        for entry in report['arrows'] + report['roots']:
            graph = json.loads((ex.folder / entry['file']).read_text(encoding='utf-8'))
            materials = [m for node in graph['nodes'] for m in node['materials']]
            if entry.get('highlight'):
                materials += [state['material'] for state in entry['highlight']['states'] if state['material']]
                source = entry['highlight']['source']
                owner = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                texture = ex.imp.reader.pointer(owner, owner.read_typetree()['m_StaticSilouetteTexture'])
                definition = recover_texture(ex, texture)
                textures[definition['png']] = definition
            for material in materials:
                    source = material['source']
                    owner = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                    for name, saved in owner.read_typetree()['m_SavedProperties']['m_TexEnvs']:
                        if saved['m_Texture']['m_PathID']:
                            texture = ex.imp.reader.pointer(owner, saved['m_Texture'])
                            definition = recover_texture(ex, texture)
                            textures[definition['png']] = definition
        report['textures'] = list(textures.values())
        atomic_json(ex.folder / 'native-fireblast-ui-textures.json', dict(textures=report['textures']))
        for key, reference in (
                ('cancel', 'CancelAttack.prefab:9cde7207a78024e46aa5a0a657807845'),
                ('used', 'hero_power_icon_flip_off.prefab:621ead6ff672f5b4bbfd6578ee217a42'),
                ('restore', 'hero_power_icon_flip_on.prefab:e1491b367801f6b4395dc63ce0b08f0a')):
            audio = recover_audio(ex, reference)
            if audio['errors']:
                raise ValueError(audio['errors'])
            filename = 'fireblast-' + key + '-audio.json'
            atomic_json(ex.folder / filename, audio)
            report['sounds'].append(dict(key=key, reference=reference, file=filename, source=audio['source']))
        for filename, reference in (
                ('GLEI00M_t_Outline.ttf', 'Belwe_Outline.asset:3b6d04aee23313140a4436f2db238cfd'),
                ('BlizzardGlobal.ttf', 'FranklinGothic.asset:743e4aa53999fb14aa887581546e7b4c')):
            definition, _, _ = ex.imp.reader.resolve(reference, locale='zhcn')
            raw = definition.read_typetree()
            font = ex.imp.reader.pointer(definition, raw['m_Font'])
            values = font.read_typetree()
            data = bytes(values['m_FontData'])
            if data[:4] != b'\x00\x01\x00\x00':
                raise ValueError('Source font is not the reviewed TrueType format')
            (ex.folder / filename).write_bytes(data)
            report['fonts'].append(dict(reference=reference, definition=identity(ex, definition),
                source=identity(ex, font), file=filename, sha256=hashlib.sha256(data).hexdigest(),
                font_size_modifier=raw['m_FontSizeModifier'], line_space_modifier=raw['m_LineSpaceModifier']))
            report['fonts'][-1].update(character_size_modifier=raw['m_CharacterSizeModifier'], single_line_adjustment=raw['m_SingleLineAdjustment'])
        for reference in ('HeroPower_Used.anim:7b4108b611b892d4286da129d02c3ada',
                          'HeroPower_Restore.anim:666e46aae0320a84488c5b1a338a7977'):
            obj, _, _ = ex.imp.reader.resolve(reference)
            report['animations'].append(animation(ex, obj))
        atomic_json(ex.folder / 'fireblast-ui.json', report)
        manifest = dict(report)
        manifest['roots'] = [{key: value for key, value in entry.items() if key != 'texts'} for entry in report['roots']]
        manifest['animations'] = [dict(id=clip['id'], name=clip['name'], duration=clip['duration'], tracks=len(clip['tracks'])) for clip in report['animations']]
        atomic_json(PROJECT / 'manifests/fireblast-ui-source.json', manifest)
    ex.save()
    if ex.errors:
        raise RuntimeError(ex.errors)
    print('FIREBLAST UI', len(report['roots']), 'roots;', len(report['arrows']), 'arrows;', len(report['fonts']), 'fonts')


if __name__ == '__main__':
    main()
