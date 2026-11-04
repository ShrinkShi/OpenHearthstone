"""Recover the original transparent particle kernel and property-backed blends."""
import json

from .additive_shader_audit import audit, verify_sources
from .asset_lock import asset_lock, atomic_json
from .importer import PROJECT, Importer
from .native_effects import blend_commands

SHADER = 'Hero/Particle/Particles_Transparent'


def refresh(shader_name=SHADER, keywords=None):
    importer = Importer()
    resolved = {}
    programs = []
    updated = 0
    with asset_lock(PROJECT / 'local'):
        for path in sorted((PROJECT / 'local/runtime').glob('*.native-effect.json')):
            data = json.loads(path.read_text(encoding='utf-8'))
            changed = False
            for node in data['nodes']:
                for cached in node['materials']:
                    if cached['shader'] != shader_name or (keywords is not None and set(cached['keywords']) != set(keywords)):
                        continue
                    source = cached['source']
                    key = (source['bundle'], source['cab'], source['pathid'])
                    if key not in resolved:
                        obj = next(o for o in importer.reader.load(key[0]).objects
                                   if o.assets_file.name == key[1] and str(o.path_id) == key[2])
                        tree = obj.read_typetree()
                        shader = importer.reader.pointer(obj, tree['m_Shader'])
                        parsed = shader.read_typetree()['m_ParsedForm']
                        if tree['m_Name'] != cached['name'] or parsed['m_Name'] != shader_name:
                            raise ValueError('Original material or shader identity changed')
                        floats = {p['m_Name']: p.get('m_DefValue[0]', 0)
                                  for p in parsed['m_PropInfo']['m_Props'] if p['m_Type'] in (2, 3)}
                        floats.update(dict(tree['m_SavedProperties']['m_Floats']))
                        blend = parsed['m_SubShaders'][0]['m_Passes'][0]['m_State']['rtBlend0']
                        values = blend_commands(blend, floats)
                        if values.get('unresolved_render_commands'):
                            raise ValueError('Unresolved source blend property')
                        for target, field in [('src', 'srcBlend'), ('dst', 'destBlend'), ('src_alpha', 'srcBlendAlpha'), ('dst_alpha', 'destBlendAlpha')]:
                            values[target + '_property'] = blend[field]['name']
                        resolved[key] = values
                    if any(cached.get(k) != v for k, v in resolved[key].items()):
                        cached.update(resolved[key]); changed = True
                    programs.append(dict(file=path.name, material=source, blend=resolved[key]))
            if changed:
                atomic_json(path, data); updated += 1
    return dict(materials=programs, unique_materials=len(resolved), updated=updated)


def main():
    result = audit('Hero_Particle_Particles_Transparent-1610204260223125505')
    result['family'] = 17
    result['notes'] = ['Original base uses 2*vertex*TintColor, SH lighting without MainLightColor.',
                       'Intensity affects RGB only; Alpha affects alpha only; no saturation.',
                       'Property-backed RGB/alpha blends restored from actual material values.',
                       'Glow retains separate Color.a; full card effects remain unverified.']
    sources = verify_sources(result)
    changes = refresh()
    atomic_json(PROJECT / 'manifests/native-transparent-variants.json', result)
    atomic_json(PROJECT / 'manifests/native-transparent-source-bindings.json', sources)
    atomic_json(PROJECT / 'manifests/native-transparent-blend-refresh.json', changes)
    print('ORIGINAL TRANSPARENT', len(result['references']), 'programs;', len(changes['materials']),
          'references;', changes['updated'], 'updated;', changes['unique_materials'], 'unique materials')


if __name__ == '__main__':
    main()
