"""Trace the restored Actor portrait pass to the installed compiled shader."""
import json
from .importer import PROJECT
from .asset_lock import atomic_json
from .inspect_compiled_shaders import variant_bindings
from .importer import Importer


def main():
    folder=PROJECT/'local/compiled-shader-audit/Custom_Card_Unlit_Desaturate_2Texture2uv-6322982158558142423'
    audit=json.loads((folder/'audit.json').read_text(encoding='utf-8'))
    parsed=json.loads((folder/'parsed-form.json').read_text(encoding='utf-8'))
    if audit['errors']:raise ValueError('Source shader program audit has errors')
    programs={r['index']:r for r in audit['programs'] if r['platform']==4}
    parameters={r['index']:r for r in audit['parameter_records'] if r['platform']==4}
    bindings=variant_bindings(parsed,[])
    if len(bindings)!=2 or any(b['pass_index']!=0 for b in bindings):raise ValueError('Unexpected original portrait pass layout')
    for binding in bindings:
        binding['program_sha256']=programs[binding['blob']]['sha256']
        binding['parameter_sha256']=parameters[binding['parameter_blob']]['sha256']
    shader_pass=parsed['m_SubShaders'][0]['m_Passes'][0]
    names={index:name for name,index in shader_pass['m_NameIndices']}
    layout={stage:{names[b['m_NameIndex']]:{names[v['m_NameIndex']]:v['m_Index'] for v in b['m_VectorParams']}
        for b in shader_pass[stage]['m_CommonParameters']['m_ConstantBuffers']} for stage in ('progVertex','progFragment')}
    atomic_json(PROJECT/'manifests/original-actor-portrait-shader.json',dict(
        source={k:audit[k] for k in ('name','bundle','cab','pathid')},bindings=bindings,constant_byte_offsets=layout,
        restored_shader='UnityClient/Assets/Resources/OriginalActorPortrait.shader',complete=False,
        notes=['Exact base vertex/fragment arithmetic and two UV channels; original binaries are never executed.',
               'Original Gameplay baked SH coefficients populate per-renderer shader uniforms; dynamic board lighting/probe/main-light service is not restored.',
               'Main light enabled input and mip bias default to zero until source global services are restored.',
               'User UV calibration remains an explicit additional adapter; frame/premium/shadow shaders are not restored by this pass.']))
    print('ORIGINAL ACTOR PORTRAIT SHADER',len(bindings),'exact base program bindings')
    imp=Importer();settings=next(o for o in imp.reader.load('@player/globalgamemanagers').objects if o.type.name=='BuildSettings')
    scenes=settings.read_typetree()['scenes'];scene='Assets/Game/Gameplay/Gameplay.unity'
    bundle='@player/level'+str(scenes.index(scene));objects=[o for o in imp.reader.load(bundle).objects if o.type.name=='RenderSettings']
    if len(objects)!=1:raise ValueError('Gameplay must have exactly one original RenderSettings')
    obj=objects[0];tree=obj.read_typetree();probe=tree['m_AmbientProbe']
    values=[value for _,value in sorted(probe.items(),key=lambda row:int(row[0][row[0].index('[')+1:row[0].index(']')]))]
    if len(values)!=27:raise ValueError('Original ambient SH must contain 27 coefficients')
    lighting=dict(scene=scene,bundle=bundle,cab=obj.assets_file.name,pathid=str(obj.path_id),ambient_mode=tree['m_AmbientMode'],sh=values,
                  complete=False,notes=['Exact baked Gameplay ambient probe; runtime board lighting changes and main light selection remain unrecovered.'])
    atomic_json(imp.local/'runtime/original-gameplay-lighting.json',lighting)
    atomic_json(PROJECT/'manifests/original-gameplay-lighting.json',lighting)
    print('ORIGINAL GAMEPLAY LIGHTING',len(values),'SH coefficients')


if __name__=='__main__':main()
