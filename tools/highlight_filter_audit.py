"""Trace the original silhouette postprocess and constant replacement shaders."""
import json
from .asset_lock import atomic_json
from .importer import PROJECT
from .inspect_compiled_shaders import variant_bindings


def main():
    keys=[('Custom_Selection_HighlightMultiSample-632205946841644324','MultiSample'),
          ('Custom_Selection_HighlightMultiSampleBlend--5144723844496913212','MultiSampleBlend'),
          ('Custom_Selection_HighlightMaskBlend-7247033804908365759','MaskBlend')]
    keys += [('Custom_Unlit_Color_'+name+'-'+str(pathid),'Constant '+name)
             for name,pathid in [('Grey',134),('LightGrey',139),('DarkGrey',180),('White',189),('BlackOverlay',161)]]
    records=[]
    for key,usage in keys:
        folder=PROJECT/'local/compiled-shader-audit'/key
        audit=json.loads((folder/'audit.json').read_text(encoding='utf-8'))
        parsed=json.loads((folder/'parsed-form.json').read_text(encoding='utf-8'))
        if audit['errors']:raise ValueError(audit['errors'])
        bindings=variant_bindings(parsed,[])
        if [(b['blob'],b['parameter_blob']) for b in bindings]!=[(1,0),(3,2)]:raise ValueError('Unexpected source filter bindings')
        for binding in bindings:
            binding['program_sha256']=next(r['sha256'] for r in audit['programs'] if r['platform']==4 and r['index']==binding['blob'])
            binding['parameter_sha256']=next(r['sha256'] for r in audit['parameter_records'] if r['platform']==4 and r['index']==binding['parameter_blob'])
        source=parsed['m_SubShaders'][0]['m_Passes'][0];names={index:name for name,index in source['m_NameIndices']}
        layouts={stage:{names[b['m_NameIndex']]:{names[v['m_NameIndex']]:v['m_Index'] for v in b['m_VectorParams']}
                       for b in source[stage]['m_CommonParameters']['m_ConstantBuffers']} for stage in ('progVertex','progFragment')}
        records.append(dict(usage=usage,source={k:audit[k] for k in ('name','bundle','cab','pathid')},bindings=bindings,
                            constant_byte_offsets=layouts,state=source['m_State']))
    scripts=json.loads((PROJECT/'manifests/original-script-audit.json').read_text(encoding='utf-8-sig'))['scripts']
    source_script=next(s for s in scripts if s['type']=='HighlightRender')
    pipeline=dict(source_script={k:source_script[k] for k in ('assembly','assembly_sha256','type','cache')},
                  texture_depth=16,texture_format='R8',max_dimension=256,
                  stages=[dict(draw_grey=.25,size=.3,ortho_offset=-.2),dict(draw_grey=.5,size=.3,ortho_offset=-.25),
                          dict(filter='MultiSampleBlend',blur=1.25),dict(draw_grey=.75,size=.5,ortho_offset=-.01),
                          dict(filter='MultiSampleBlend',blur=1.25),dict(draw_grey=.75,size=1,ortho_offset=.05),
                          dict(filter='MultiSampleBlend',blur=1),dict(filter='MultiSample',blur=1.5),
                          dict(draw_grey=1,size=.92,ortho_offset=.1),dict(filter='MaskBlend',blur=.8)],
                  runtime='UnityClient/Assets/Scripts/NativeHighlightSilhouette.cs',complete=False)
    atomic_json(PROJECT/'manifests/original-highlight-filters.json',dict(shaders=records,pipeline=pipeline,complete=False,
        notes=['Exact original three postprocess programs and five constant replacement programs.',
               'UV1 is supplied explicitly by the client-owned fullscreen quad; source global mip bias service is not restored.',
               'Ordinary Actor root normalization, eligible submeshes, four draw sizes and five filter stages are restored and tested offscreen.',
               'Source include/exclude/custom mesh overrides and original clock require restoration. Ordinary minion has no renderer FSM; its immediate state branch is restored separately, without production routing.']))
    print('ORIGINAL HIGHLIGHT FILTERS',len(records),'source shader families')


if __name__=='__main__':main()
