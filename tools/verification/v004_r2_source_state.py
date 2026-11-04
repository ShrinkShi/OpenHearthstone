"""Fresh full pass state, tags and fragment layout check for V004-r2."""
import json

from tools import task4_visual_evidence as evidence
from tools.importer import Importer


def main():
    work = evidence.PROJECT / 'work/v004-r2'
    audit = evidence.read(work / 'manifests/task-0004-shader-audit.json')
    source = audit['source']
    importer = Importer()
    obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    parsed = json.loads(json.dumps(obj.read_typetree()['m_ParsedForm']))
    shader_pass = parsed['m_SubShaders'][0]['m_Passes'][0]
    assert shader_pass['m_State'] == audit['source_state']
    assert dict(parsed['m_SubShaders'][0]['m_Tags']['tags']) == audit['source_tags']
    assert shader_pass['progFragment']['m_CommonParameters'] == audit['samplers_and_constant_layout']
    evidence.write(work / 'fresh-source-state.json', {
        'source': source, 'full_pass_state': shader_pass['m_State'],
        'tags': dict(parsed['m_SubShaders'][0]['m_Tags']['tags']),
        'fragment_parameters': shader_pass['progFragment']['m_CommonParameters'],
        'fresh_original_matches_audit': True,
        'product_version': 'Unknown', 'original_dll_executed': False})
    print('V004-r2 FRESH STATE PASS: original full pass state including Stencil, tags and fragment layout match audit')


if __name__ == '__main__':
    main()
