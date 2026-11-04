"""Trace the original Multiply programs and existing material references."""
import json

from .asset_lock import atomic_json
from .importer import PROJECT
from .inspect_compiled_shaders import variant_bindings


def main():
    directory=PROJECT/'local/compiled-shader-audit/Hero_Particle_Particle_Multiply--5773009347599192354'
    audit=json.loads((directory/'audit.json').read_text(encoding='utf-8'))
    parsed=json.loads((directory/'parsed-form.json').read_text(encoding='utf-8'))
    if audit['errors']:raise ValueError('Multiply shader audit has unresolved parsing errors')
    gpu={p['index']:p for p in audit['programs'] if p['platform']==4}
    parameters={p['index']:p for p in audit['parameter_records'] if p['platform']==4}
    bindings=variant_bindings(parsed,[])
    for binding in bindings:
        binding['program_sha256']=gpu[binding['blob']]['sha256']
        binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
    refs=[]
    for path in sorted((PROJECT/'local/runtime').glob('*.native-effect.json')):
        program=json.loads(path.read_text(encoding='utf-8'))
        materials=[dict(node=index,name=node['name'],source=material['source'])
            for index,node in enumerate(program['nodes']) for material in node['materials']
            if material['shader']==audit['name']]
        if materials:refs.append(dict(file=path.name,reference=program['reference'],materials=materials))
    atomic_json(PROJECT/'manifests/native-multiply-variants.json',dict(
        shader={key:audit[key] for key in ('name','bundle','cab','pathid')},bindings=bindings,references=refs,
        notes=['Original base RGBA multiplier and shared Glow pass reimplemented from DXBC.',
            'Internal RenderToTexture uses original Zero/SrcColor blend.',
            'Final board capture does not yet preserve colored destination transmission through the transparent composite.',
            'Material coverage and GPU math do not certify complete card animations.']))
    print('MULTIPLY AUDIT',len(bindings),'stage bindings;',len(refs),'prefabs;',sum(len(r['materials']) for r in refs),'material references')


if __name__=='__main__':main()
