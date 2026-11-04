"""Recover Actor HighlightState dependencies without enabling an incomplete silhouette driver."""
import json
import re
import hashlib
from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import material, identity
from .inspect_compiled_shaders import variant_bindings
from UnityPy.helpers.MeshHelper import MeshHandler


def main():
    ex=RuntimeExport();root=ex.imp.reader.resolve('23b7de16184fa8042bf6b734e7ca4d60')[0]
    matches=[(o,t) for o,t in ex.imp.reader.walk(root,include_visual=True,max_nodes=4000)
             if o.type.name=='MonoBehaviour' and ex.imp.reader.pointer(o,t['m_Script']).read_typetree().get('m_ClassName')=='HighlightState']
    if len(matches)!=1:raise ValueError('Expected one source minion HighlightState')
    obj,tree=matches[0];errors=[];states=[]
    flares={}
    enum_text=(PROJECT/'local/script-audit/Assembly-CSharp.dll.ActorStateType.il.txt').read_text(encoding='utf-8-sig')
    state_names={int(value):name for name,value in re.findall(r'ActorStateType::(\w+) = (\d+)',enum_text)}
    for entry in tree['m_HighlightStates']:
        source=ex.imp.reader.pointer(obj,entry['m_Material']) if entry['m_Material']['m_PathID'] else None
        states.append(dict(state=entry['m_StateType'],name=state_names[entry['m_StateType']],offset=entry['m_Offset'],material=material(ex,source,errors) if source else None))
        if source:
            pointer=next(v['m_Texture'] for k,v in source.read_typetree()['m_SavedProperties']['m_TexEnvs'] if k=='_FlareTex')
            texture=ex.imp.reader.pointer(source,pointer);name=ex.texture(texture)
            if name not in flares:
                data=texture.read();tt=texture.read_typetree();sampler=tt['m_TextureSettings'];raw=data.get_image_data();raw_name=name[:-4]+'.texturedata'
                if data.m_TextureFormat not in (3,10) or data.m_MipCount!=1:raise ValueError('Unverified original flare texture format/mips')
                (ex.folder/raw_name).write_bytes(raw)
                flares[name]=dict(png=name,raw=raw_name,name=tt['m_Name'],source=identity(ex,texture),sha256=hashlib.sha256(raw).hexdigest(),
                                  width=data.m_Width,height=data.m_Height,format=data.m_TextureFormat,mips=data.m_MipCount,color_space=tt['m_ColorSpace'],
                                  filter=sampler['m_FilterMode'],aniso=sampler['m_Aniso'],mip_bias=sampler['m_MipBias'],
                                  wrap_u=sampler['m_WrapU'],wrap_v=sampler['m_WrapV'],wrap_w=sampler['m_WrapW'])
    plane=ex.imp.reader.pointer(obj,tree['m_RenderPlane'])
    components=[ex.imp.reader.pointer(plane,c['component']) for c in plane.read_typetree()['m_Component']]
    scripts=[(c,ex.imp.reader.pointer(c,c.read_typetree()['m_Script']).read_typetree().get('m_ClassName')) for c in components if c.type.name=='MonoBehaviour']
    dynamic=[c for c,name in scripts if name=='HighlightRender']
    owner=ex.imp.reader.pointer(obj,tree['m_GameObject'])
    transform=next(c for c in [ex.imp.reader.pointer(owner,e['component']) for e in owner.read_typetree()['m_Component']] if c.type.name=='Transform')
    parent=ex.imp.reader.pointer(transform,transform.read_typetree()['m_Father'])
    parent_owner=ex.imp.reader.pointer(parent,parent.read_typetree()['m_GameObject'])
    parent_scripts=[ex.imp.reader.pointer(c,c.read_typetree()['m_Script']).read_typetree().get('m_ClassName') for e in parent_owner.read_typetree()['m_Component'] for c in [ex.imp.reader.pointer(parent_owner,e['component'])] if c.type.name=='MonoBehaviour']
    resolved_type=tree['m_highlightType'] or (1 if 'ActorStateMgr' in parent_scripts else 2)
    if len(dynamic)!=1:raise ValueError('Expected one original HighlightRender on render plane')
    render=dynamic[0];render_tree=render.read_typetree()
    mesh_filter=next(c for c in [ex.imp.reader.pointer(plane,e['component']) for e in plane.read_typetree()['m_Component']] if c.type.name=='MeshFilter')
    mesh=ex.imp.reader.pointer(mesh_filter,mesh_filter.read_typetree()['m_Mesh'])
    handler=MeshHandler(mesh.read());handler.process()
    mesh_path=ex.folder/ex.mesh(mesh)
    mesh_data=json.loads(mesh_path.read_text(encoding='utf-8'))
    channels=mesh.read_typetree()['m_VertexData']['m_Channels']
    if channels[3]['format']!=2:raise ValueError('Expected original highlight UNorm8 color stream')
    mesh_data['uv2']=[v for row in handler.m_UV1 for v in row[:2]]
    mesh_data['colors']=[v/255 for row in handler.m_Colors for v in row]
    atomic_json(mesh_path,mesh_data)
    render_il=(PROJECT/'local/script-audit/Assembly-CSharp.dll.HighlightRender.il.txt').read_text(encoding='utf-8-sig')
    dynamic_source=dict(source=identity(ex,render),root_transform=identity(ex,ex.imp.reader.pointer(render,render_tree['m_RootTransform'])),
                        silhouette_render_size=render_tree['m_SilouetteRenderSize'],silhouette_clip_size=render_tree['m_SilouetteClipSize'],
                        shader_dependencies=sorted(set(re.findall(r'ldstr "(Custom/[^"\n]+)"',render_il))),complete=False,
                        mesh=dict(source=identity(ex,mesh),cache=mesh_path.name,color_format='UNorm8',uv2_vertices=len(handler.m_UV1)))
    record=dict(source=identity(ex,obj),render_plane=identity(ex,ex.imp.reader.pointer(obj,tree['m_RenderPlane'])),
                render_queue=tree['m_RenderQueue'],queue_offset=tree['m_RenderQueueOffset'],states=states,
                state_driver=dict(has_fsm=any(name=='PlayMakerFSM' for _,name in scripts),source_type=tree['m_highlightType'],resolved_type=resolved_type,
                                  parent_scripts=parent_scripts,history_translation=tree['m_HistoryTranslation'],owner=identity(ex,owner)),
                static_silhouette=tree['m_StaticSilouetteTexture'],dynamic_render=dynamic_source,complete=False,errors=errors,
                notes=['Exact source state materials, textures and renderer FSM presence. Ordinary minion has no renderer FSM; original immediate branch applies. Production routing remains disabled.'])
    if errors:raise ValueError(errors)
    atomic_json(ex.folder/'original-minion-highlight.json',record)
    atomic_json(PROJECT/'manifests/original-minion-highlight.json',record)
    flare_record=dict(textures=list(flares.values()),complete=False,notes=['Exact original RGB24/DXT1 payload, one mip and sampler for the two ordinary minion flare dependencies. Other source textures remain outside this restoration.'])
    atomic_json(ex.folder/'original-highlight-flare.json',flare_record)
    atomic_json(PROJECT/'manifests/original-highlight-flare.json',flare_record)
    folder=PROJECT/'local/compiled-shader-audit/Custom_Selection_Highlight--1327719733717699995'
    audit=json.loads((folder/'audit.json').read_text(encoding='utf-8'))
    parsed=json.loads((folder/'parsed-form.json').read_text(encoding='utf-8'))
    if audit['errors']:raise ValueError(audit['errors'])
    bindings=variant_bindings(parsed,[])
    if [(b['blob'],b['parameter_blob']) for b in bindings]!=[(1,0),(3,2)]:raise ValueError('Unexpected Highlight source programs')
    for binding in bindings:
        binding['program_sha256']=next(r['sha256'] for r in audit['programs'] if r['platform']==4 and r['index']==binding['blob'])
        binding['parameter_sha256']=next(r['sha256'] for r in audit['parameter_records'] if r['platform']==4 and r['index']==binding['parameter_blob'])
    source=parsed['m_SubShaders'][0]['m_Passes'][0];names={index:name for name,index in source['m_NameIndices']}
    layouts={stage:{names[b['m_NameIndex']]:{names[v['m_NameIndex']]:v['m_Index'] for v in b['m_VectorParams']}
                   for b in source[stage]['m_CommonParameters']['m_ConstantBuffers']} for stage in ('progVertex','progFragment')}
    atomic_json(PROJECT/'manifests/original-actor-highlight-shader.json',dict(
        source={k:audit[k] for k in ('name','bundle','cab','pathid')},bindings=bindings,constant_byte_offsets=layouts,
        state=source['m_State'],restored_shader='UnityClient/Assets/Resources/OriginalActorHighlight.shader',complete=False,
        notes=['Source nonlinear RGBA, second UV, seed/time flare and alpha blend math; no invented outline fallback.',
               'Source global ShaderTime must be bound by the original presentation clock service before production activation.']))
    print('ORIGINAL MINION HIGHLIGHT',len(states),'states;',len(bindings),'shader programs')


if __name__=='__main__':main()
