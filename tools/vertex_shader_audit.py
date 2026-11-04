"""Recover original vertex-only mask/glow compiled stages and bindings."""
import json

from .additive_shader_audit import audit, verify_sources
from .asset_lock import atomic_json
from .importer import PROJECT

FAMILIES = (
    ('Custom_FX_FX_HeroMask_VertColorOnly-5264888736503454533', 21),
    ('Effects_FX_AdditiveVertColor--64755960916519659', 22),
)


def vertex_audit(folder, family):
    result = audit(folder)
    for path in sorted((PROJECT / 'local/runtime').glob('*.start-spell.json')):
        data = json.loads(path.read_text(encoding='utf-8'))
        materials = [dict(node=index, name=material['name'], source=material['source'])
                     for index, node in enumerate(data['nodes']) for material in node['materials']
                     if material['shader'] == result['shader']['name']]
        if materials:
            result['references'].append(dict(file=path.name, reference=data['reference'], materials=materials))
    result['family'] = family
    result['notes'] = [
        'Output = 1 + vertex.a*(vertex-1); Src=Zero/Dst=SrcColor, depth write, backface cull.'
        if family == 21 else 'Output = 2*vertex*Color; Src=SrcAlpha/Dst=One, depth write off, cull off.',
        'No texture samples; saved texture/tint properties not declared by the program are unused.',
        'Full actor meshes, scene depth/order, original Card bindings and complete card playback remain incomplete.']
    return result


def main():
    manifests, sources = [], []
    for folder, family in FAMILIES:
        result = vertex_audit(folder, family)
        source = verify_sources(result)
        manifests.append(result)
        sources.append(source)
        print('ORIGINAL VERTEX', family, len(result['references']), 'programs;',
              len(source['bindings']), 'unique material bindings')
    atomic_json(PROJECT / 'manifests/native-vertex-shaders.json', manifests)
    atomic_json(PROJECT / 'manifests/native-vertex-source-bindings.json', sources)


if __name__ == '__main__':
    main()
