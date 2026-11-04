"""Record original frame DXBC passes, hashes and constant layouts."""
import json
from .importer import PROJECT
from .asset_lock import atomic_json
from .inspect_compiled_shaders import variant_bindings


def main():
    folder=PROJECT/'local/compiled-shader-audit/Custom_Card_UnlitTexture--4440234706065032223'
    audit=json.loads((folder/'audit.json').read_text(encoding='utf-8'))
    parsed=json.loads((folder/'parsed-form.json').read_text(encoding='utf-8'))
    if audit['errors']:raise ValueError('Source frame shader audit has errors')
    programs={r['index']:r for r in audit['programs'] if r['platform']==4}
    parameters={r['index']:r for r in audit['parameter_records'] if r['platform']==4}
    bindings=variant_bindings(parsed,[])
    if [(b['pass_index'],b['blob']) for b in bindings]!=[(0,1),(0,3),(1,5),(1,7)]:
        raise ValueError('Unexpected source frame pass layout')
    for binding in bindings:
        binding['program_sha256']=programs[binding['blob']]['sha256']
        binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
    passes=[]
    for source in parsed['m_SubShaders'][0]['m_Passes']:
        names={index:name for name,index in source['m_NameIndices']}
        layouts={stage:{names[b['m_NameIndex']]:{names[v['m_NameIndex']]:v['m_Index']
            for v in b['m_VectorParams']+b['m_MatrixParams']} for b in source[stage]['m_CommonParameters']['m_ConstantBuffers']}
            for stage in ('progVertex','progFragment')}
        passes.append(dict(state={k:source['m_State'][k] for k in ('m_Name','culling','zWrite','zTest','rtBlend0','m_Tags')},constant_byte_offsets=layouts))
    atomic_json(PROJECT/'manifests/original-actor-frame-shader.json',dict(
        source={k:audit[k] for k in ('name','bundle','cab','pathid')},bindings=bindings,passes=passes,
        restored_shader='UnityClient/Assets/Resources/OriginalActorFrame.shader',complete=False,
        notes=['Original two-pass D3D11 math/render states; official binaries are not executed.',
               'Main pass uses the original Gameplay SH; unlike the portrait shader, main light is not gated by LightData.z.',
               'Built-in pipeline tags adapt the original URP pass dispatch. Original shadow-map scheduling/global light bindings are not restored.',
               'Shadow GPU probe verifies bias and clip math; it does not certify a complete original board shadow pipeline.',
               'Premium reflection/highlight materials, global mip bias and dynamic main light service remain incomplete.']))
    print('ORIGINAL ACTOR FRAME SHADER',len(bindings),'exact source program bindings')


if __name__=='__main__':main()
