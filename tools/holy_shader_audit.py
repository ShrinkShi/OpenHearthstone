"""Trace original HolyRender channel decoding and its source bindings."""
import json

from .additive_shader_audit import audit, verify_sources
from .asset_lock import atomic_json
from .importer import PROJECT

FOLDER = 'Custom_FX_FX_HolyRender--1376513395945010790'


def holy_audit():
    result = audit(FOLDER)
    for path in sorted((PROJECT / 'local/runtime').glob('*.start-spell.json')):
        data = json.loads(path.read_text(encoding='utf-8'))
        materials = [dict(node=index, name=material['name'], source=material['source'])
                     for index, node in enumerate(data['nodes']) for material in node['materials']
                     if material['shader'] == result['shader']['name']]
        if materials:
            result['references'].append(dict(file=path.name, reference=data['reference'], materials=materials))
    result['family'] = 20
    result['notes'] = [
        'Vertex applies MainTex ST and ignores vertex color.',
        'RGB = (2*R*Color.rgb*Color.a + G*Color2.rgb*Color2.a)*Intensity*Alpha.',
        'Output alpha = 2*R*Intensity*Alpha*Alpha; texture B/A and Clip are unused.',
        'Output is unclamped; original source is channel data, not a red final tint.',
        'Full masks, target portrait, source scene and complete card playback remain incomplete.']
    return result


def main():
    result = holy_audit()
    source = verify_sources(result)
    atomic_json(PROJECT / 'manifests/native-holy-shader.json', result)
    atomic_json(PROJECT / 'manifests/native-holy-source-bindings.json', source)
    print('ORIGINAL HOLY:', len(result['bindings']), 'stages;', len(result['references']),
          'programs;', len(source['bindings']), 'unique material bindings')


if __name__ == '__main__':
    main()
