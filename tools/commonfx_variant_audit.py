"""Associate exact material keyword branches with bounded original DXBC entries."""
import json

from .asset_lock import atomic_json
from .importer import PROJECT
from .inspect_compiled_shaders import variant_bindings


def main():
    directory=PROJECT/'local/compiled-shader-audit/FX_CommonFX--402334618889270515'
    parsed=json.loads((directory/'parsed-form.json').read_text(encoding='utf-8'))
    audit=json.loads((directory/'audit.json').read_text(encoding='utf-8'))
    programs={entry['index']:entry for entry in audit['programs'] if entry['platform']==4}
    parameters={entry['index']:entry for entry in audit['parameter_records'] if entry['platform']==4}
    shared=['_COLORING_RAMP','_DISSOLVE_NONE','_SECONDTEXTURETYPE_RGBR','_TEXTURETYPE_RGBA'];variants=[]
    for second in ('_SECONDTEXTURE_NOISEUV','_SECONDTEXTURE_MASK'):
        keywords=sorted(shared+[second]);bindings=variant_bindings(parsed,keywords)
        for binding in bindings:
            if binding['blob'] not in programs:raise ValueError('Variant has no bounded GPU entry')
            binding['program_sha256']=programs[binding['blob']]['sha256']
            if binding['parameter_blob'] is not None:
                binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
        variants.append(dict(keywords=keywords,bindings=bindings))
    keywords=sorted(['_COLORING_RAMP','_DISSOLVE_NONE','_SECONDTEXTURETYPE_RGBR','_SECONDTEXTURE_NONE','_TEXTURETYPE_RGBR'])
    bindings=variant_bindings(parsed,keywords)
    for binding in bindings:
        binding['program_sha256']=programs[binding['blob']]['sha256']
        if binding['parameter_blob'] is not None:
            binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
    variants.append(dict(keywords=keywords,bindings=bindings))
    keywords=sorted(['_COLORING_NONE','_DISSOLVE_NONE','_SECONDTEXTURETYPE_RGBR','_SECONDTEXTURE_NONE','_TEXTURETYPE_RGBR'])
    bindings=variant_bindings(parsed,keywords)
    for binding in bindings:
        binding['program_sha256']=programs[binding['blob']]['sha256']
        if binding['parameter_blob'] is not None:
            binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
    variants.append(dict(keywords=keywords,bindings=bindings))
    atomic_json(PROJECT/'manifests/native-commonfx-variants.json',dict(shader={key:audit[key] for key in ('name','bundle','cab','pathid')},
        variants=variants,notes=['Only these four base variants and the shared Glow pass have been reimplemented.',
            'DXBC inspection is not restoration of every CommonFX keyword branch or original scripts.']))
    print('COMMONFX VARIANT AUDIT',len(variants),'variants;',sum(len(v['bindings']) for v in variants),'stage bindings')


if __name__=='__main__':main()
