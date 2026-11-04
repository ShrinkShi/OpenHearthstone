"""Recover original texture bytes, mip chains and samplers for restored smoke shaders."""
import hashlib
import json

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import identity


def recover_texture(exporter, texture):
    png = exporter.texture(texture)
    data = texture.read()
    original = texture.read_typetree()
    sampler = original['m_TextureSettings']
    raw = data.get_image_data()
    filename = png[:-4] + '.smoke-texturedata'
    (exporter.folder / filename).write_bytes(raw)
    return dict(png=png, raw=filename, name=original['m_Name'], source=identity(exporter, texture),
                sha256=hashlib.sha256(raw).hexdigest(), width=data.m_Width, height=data.m_Height,
                format=data.m_TextureFormat, mips=data.m_MipCount, color_space=original['m_ColorSpace'],
                filter=sampler['m_FilterMode'], aniso=sampler['m_Aniso'], mip_bias=sampler['m_MipBias'],
                wrap_u=sampler['m_WrapU'], wrap_v=sampler['m_WrapV'], wrap_w=sampler['m_WrapW'])


def main():
    exporter = RuntimeExport()
    textures = {}
    bindings = []
    with asset_lock(exporter.imp.local):
        for manifest in ('native-particle-alpha-source-bindings.json', 'native-sneaky-smoke-source-bindings.json'):
            sources = json.loads((PROJECT / 'manifests' / manifest).read_text(encoding='utf-8'))
            for binding in sources['bindings']:
                source = binding['material']
                material = exporter.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                tree = material.read_typetree()
                shader = exporter.imp.reader.pointer(material, tree['m_Shader']).read_typetree()['m_ParsedForm']
                declared = {p['m_Name'] for p in shader['m_PropInfo']['m_Props'] if p['m_Type'] == 4}
                for name, saved in tree['m_SavedProperties']['m_TexEnvs']:
                    if name not in declared or not saved['m_Texture']['m_PathID']:
                        continue
                    texture = exporter.imp.reader.pointer(material, saved['m_Texture'])
                    if texture is None or texture.type.name != 'Texture2D':
                        raise ValueError('Unrestored original smoke texture type')
                    png = exporter.texture(texture)
                    if png not in textures:
                        textures[png] = recover_texture(exporter, texture)
                    bindings.append(dict(material=source, property=name, png=png))
        report = dict(textures=list(textures.values()), bindings=bindings, whole_effects_verified=False)
        atomic_json(exporter.folder / 'native-smoke-source-textures.json', report)
        atomic_json(PROJECT / 'manifests/native-smoke-source-textures.json', report)
    print('ORIGINAL SMOKE TEXTURES', len(textures), 'textures;', len(bindings), 'bindings;',
        sorted({(t['format'], t['mips'], t['color_space'], t['wrap_u'], t['wrap_v']) for t in textures.values()}))


if __name__ == '__main__':
    main()
