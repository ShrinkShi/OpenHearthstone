"""Trace original Hero/Additive/Additive base and Glow programs."""
import argparse
import hashlib
import json

from .asset_lock import atomic_json
from .importer import PROJECT, Importer
from .inspect_compiled_shaders import programs, variant_bindings


def render_states(parsed):
    return [{**{key: shader_pass['m_State'][key]['val'] for key in ('culling', 'zWrite', 'zTest')},
             'blend': {key: value['val'] for key, value in shader_pass['m_State']['rtBlend0'].items()}}
            for shader_pass in parsed['m_SubShaders'][0]['m_Passes']]


def audit(folder_name='Hero_Additive_Additive--666151408197827071', keywords=None):
    folder = PROJECT / 'local/compiled-shader-audit' / folder_name
    source = json.loads((folder / 'audit.json').read_text(encoding='utf-8'))
    parsed = json.loads((folder / 'parsed-form.json').read_text(encoding='utf-8'))
    if source['errors']:
        raise ValueError('Original Additive shader has unresolved compiled programs')
    programs = {p['index']: p for p in source['programs'] if p['platform'] == 4}
    parameters = {p['index']: p for p in source['parameter_records'] if p['platform'] == 4}
    bindings = variant_bindings(parsed, keywords or [])
    for binding in bindings:
        binding['program_sha256'] = programs[binding['blob']]['sha256']
        binding['parameter_sha256'] = parameters[binding['parameter_blob']]['sha256']
    layouts = []
    for shader_pass in parsed['m_SubShaders'][0]['m_Passes']:
        names = {index: name for name, index in shader_pass['m_NameIndices']}
        layouts.append({stage: {
            names[buffer['m_NameIndex']]: {
                names[value['m_NameIndex']]: value['m_Index']
                for value in buffer['m_VectorParams']}
            for buffer in shader_pass[stage]['m_CommonParameters']['m_ConstantBuffers']}
            for stage in ('progVertex', 'progFragment')})
    references = []
    for path in sorted((PROJECT / 'local/runtime').glob('*.native-effect.json')):
        data = json.loads(path.read_text(encoding='utf-8'))
        materials = [dict(node=index, name=material['name'], source=material['source'])
                     for index, node in enumerate(data['nodes']) for material in node['materials']
                     if material['shader'] == source['name'] and
                     (keywords is None or set(material['keywords']) == set(keywords))]
        if materials:
            references.append(dict(file=path.name, reference=data['reference'], materials=materials))
    result = dict(shader={key: source[key] for key in ('name', 'bundle', 'cab', 'pathid')},
                family=16, bindings=bindings, pass_constant_byte_offsets=layouts,
                pass_render_states=render_states(parsed),
                references=references, full_card_effects_verified=False,
                notes=['Base vertex applies MainTex ST and 2*vertex*TintColor with SH gamma + MainLight lighting.',
                       'Base pixel multiplies texture RGBA and interpolated vertex without saturation.',
                       'Original GlowTransparent shares restored source Glow arithmetic.',
                       'No original binaries executed and no complete card effect enabled by this audit.'])
    if keywords is not None:
        result['keywords'] = sorted(keywords)
    return result


def verify_sources(result):
    importer = Importer()
    unique = {(m['source']['bundle'], m['source']['cab'], m['source']['pathid'])
              for record in result['references'] for m in record['materials']}
    expected = result['shader']
    records = []
    checked = {}
    for bundle, cab, pathid in sorted(unique):
        material = next(o for o in importer.reader.load(bundle).objects
                        if o.assets_file.name == cab and str(o.path_id) == pathid)
        material_tree = material.read_typetree()
        if 'keywords' in result and set(material_tree.get('m_ValidKeywords', material_tree.get('m_ShaderKeywords', '').split())) != set(result['keywords']):
            raise ValueError('Original material keyword branch differs from cache: ' + pathid)
        shader = importer.reader.pointer(material, material_tree['m_Shader'])
        if shader is None:
            raise ValueError('Original material has no Shader dependency: ' + pathid)
        identity = (shader.assets_file.name, str(shader.path_id))
        if identity not in checked:
            parsed = shader.read_typetree()['m_ParsedForm']
            if parsed['m_Name'] != expected['name']:
                raise ValueError('Different source Shader name: ' + pathid)
            decoded = next(p for platform, p in programs(shader.read()) if platform == 4)
            parameter_hashes = {index: hashlib.sha256(raw).hexdigest()
                                for index, kind, raw in decoded.parameter_entries}
            bindings = variant_bindings(parsed, result.get('keywords', []))
            for binding in bindings:
                binding['program_sha256'] = hashlib.sha256(bytes(decoded.m_SubPrograms[binding['blob']].m_ProgramCode)).hexdigest()
                binding['parameter_sha256'] = parameter_hashes[binding['parameter_blob']]
            if bindings != result['bindings'] or render_states(parsed) != result['pass_render_states']:
                raise ValueError('Same-named Shader has different compiled stages or constants: ' + str(identity))
            checked[identity] = dict(cab=identity[0], pathid=identity[1],
                                    bundle=importer.reader.file_bundle.get(identity[0].lower()),
                                    stages=bindings)
        records.append(dict(material=dict(bundle=bundle, cab=cab, pathid=pathid),
                            shader=dict(cab=shader.assets_file.name, pathid=str(shader.path_id))))
    return dict(bindings=records, shaders=list(checked.values()), full_card_effects_verified=False)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-source', action='store_true')
    args = parser.parse_args()
    result = audit()
    atomic_json(PROJECT / 'manifests/native-additive-variants.json', result)
    if args.verify_source:
        bindings = verify_sources(result)
        atomic_json(PROJECT / 'manifests/native-additive-source-bindings.json', bindings)
        print('EXACT SOURCE SHADER BINDINGS:', len(bindings['bindings']))
    print('ORIGINAL ADDITIVE:', len(result['bindings']), 'stages;', len(result['references']),
          'programs;', sum(len(r['materials']) for r in result['references']), 'material references')


if __name__ == '__main__':
    main()
