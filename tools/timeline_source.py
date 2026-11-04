"""Read exact original PlayableDirector graphs without executing official scripts."""
import argparse
import json
import re
import math

from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import identity, pointer_key, animation, refresh_scale_cache, keys
from .native_animation import relative_paths


def animation_compatible(track, asset, clip):
    zero=lambda value:all(value.get(axis,0)==0 for axis in ('x','y','z'))
    return (not track.get('m_Children')
            and (not track.get('m_ApplyAvatarMask') or not track.get('m_AvatarMask',{}).get('m_PathID'))
            and track.get('m_TrackOffset',0)==0
            and zero(track.get('m_Position',{})) and zero(track.get('m_EulerAngles',{}))
            and zero(asset.get('m_Position',{})) and zero(asset.get('m_EulerAngles',{}))
            and asset.get('m_Loop',0)==0
            and all(clip.get(key,0)==0 for key in ('m_EaseInDuration','m_EaseOutDuration'))
            and all(clip.get(key,-1)==-1 for key in ('m_BlendInDuration','m_BlendOutDuration'))
            and clip['m_PreExtrapolationMode'] in (0,1) and clip['m_PostExtrapolationMode'] in (0,1))


def control_data(ex, asset, director, exposed, root, program, raw):
    reader=ex.imp.reader;reference=asset['sourceGameObject']
    name=str(reference['exposedName']);binding=next((e for e in exposed if e['name']==name),None)
    target=binding['node'] if binding else node_for(ex,reader.pointer(director,reference['defaultValue']),root,program['nodes'])
    prefab_null=not asset['prefabGameObject']['m_PathID']
    unsupported=[]
    if binding and binding.get('source'):
        source=binding['source'];env=reader.load(source['bundle']);go=env.get_cab(source['cab'].lower()).objects[int(source['pathid'])]
        for obj,_ in reader.walk(go,include_visual=False,max_nodes=500,errors=[]):
            if obj.type.name=='PlayableDirector' and asset['updateDirector']:unsupported.append(identity(ex,obj))
            if obj.type.name=='MonoBehaviour' and asset['updateITimeControl']:unsupported.append(identity(ex,obj))
    else:unsupported.append(dict(reason='unresolved original control hierarchy'))
    # Source roots search stops descending once a ParticleSystem is encountered.
    roots=[]
    def visit(index):
        if program['nodes'][index]['particle'] is not None:roots.append(index);return
        if asset['searchHierarchy']:
            for i,n in enumerate(program['nodes']):
                if n['parent']==index:visit(i)
    if target>=0 and asset['updateParticle']:visit(target)
    compatible=(target>=0 and prefab_null and not unsupported and raw['m_PreExtrapolationMode']==0 and raw['m_PostExtrapolationMode']==0
                and all(raw.get(key,0)==0 for key in ('m_EaseInDuration','m_EaseOutDuration'))
                and all(raw.get(key,-1) in (-1,0) for key in ('m_BlendInDuration','m_BlendOutDuration')))
    return dict(node=target,particle_roots=roots,active=bool(asset['active']),post=asset['postPlayback'],
                search=bool(asset['searchHierarchy']),update_director=bool(asset['updateDirector']),
                update_time_control=bool(asset['updateITimeControl']),prefab_null=prefab_null,compatible=compatible,unsupported_components=unsupported)


def shaker_data(asset, raw):
    template=asset['template'];curve=template['m_IntensityCurve']
    compatible=(raw['m_PreExtrapolationMode']==0 and raw['m_PostExtrapolationMode']==0
                and all(raw.get(k,0)==0 for k in ('m_EaseInDuration','m_EaseOutDuration'))
                and all(raw.get(k,-1) in (-1,0) for k in ('m_BlendInDuration','m_BlendOutDuration'))
                and raw['m_ClipIn']==0 and raw['m_TimeScale']==1 and raw['m_Duration']>0)
    return dict(amount=template['m_Amount'],interval=template['m_interval'],curve=keys(curve),
                pre_wrap=curve['m_PreInfinity'],post_wrap=curve['m_PostInfinity'],compatible=compatible)


def node_for(ex, obj, root, nodes):
    if obj is None:return -1
    if obj.type.name!='GameObject':
        obj=ex.imp.reader.pointer(obj,obj.read_typetree().get('m_GameObject',{}))
    if obj is None:return -1
    target_key=pointer_key(obj);parts=[];seen=set()
    while True:
        if pointer_key(obj) in seen:return -1
        seen.add(pointer_key(obj));tree=obj.read_typetree();parts.append(tree['m_Name'])
        components=[ex.imp.reader.pointer(obj,c['component']) for c in tree['m_Component']]
        transform=next((c for c in components if c and c.type.name=='Transform'),None)
        parent=ex.imp.reader.pointer(transform,transform.read_typetree().get('m_Father',{})) if transform else None
        if parent is None:break
        obj=ex.imp.reader.pointer(parent,parent.read_typetree()['m_GameObject'])
        if obj is None:return -1
    root_name=parts.pop();path='/'.join(reversed(parts))
    roots=[i for i,n in enumerate(nodes) if n['parent']==-1 and n['name']==root_name]
    if len(roots)!=1:return -1
    def belongs(index):
        while nodes[index]['parent']>=0:index=nodes[index]['parent']
        return index==roots[0]
    matches=[i for i,n in enumerate(nodes) if n['path']==path and belongs(i)]
    if len(matches)==1:return matches[0]
    # Siblings can share names; bind by source PPtr and original child order.
    def find(go,index):
        if pointer_key(go)==target_key:return index
        components=[ex.imp.reader.pointer(go,c['component']) for c in go.read_typetree()['m_Component']]
        transform=next(c for c in components if c and c.type.name=='Transform')
        children=[ex.imp.reader.pointer(transform,p) for p in transform.read_typetree()['m_Children']]
        child_objects=[ex.imp.reader.pointer(c,c.read_typetree()['m_GameObject']) for c in children]
        indices=[i for i,n in enumerate(nodes) if n['parent']==index]
        if len(indices)!=len(child_objects):raise ValueError('Source hierarchy changed')
        for child,current in zip(child_objects,indices):
            if child.read_typetree()['m_Name']!=nodes[current]['name']:raise ValueError('Source child order changed')
            found=find(child,current)
            if found>=0:return found
        return -1
    return find(obj,roots[0])


def timeline(ex, director, root, program):
    reader=ex.imp.reader;tree=director.read_typetree();asset=reader.pointer(director,tree['m_PlayableAsset'])
    if asset is None:raise ValueError('Original playable asset missing')
    bindings={pointer_key(reader.pointer(director,b['key'])):reader.pointer(director,b['value']) for b in tree['m_SceneBindings']}
    exposed=[]
    for name,pointer in tree['m_ExposedReferences']['m_References']:
        obj=reader.pointer(director,pointer)
        exposed.append(dict(name=name,node=node_for(ex,obj,root,program['nodes']),source=identity(ex,obj) if obj else None))
    result=dict(source=identity(ex,director),node=node_for(ex,director,root,program['nodes']),initial_time=tree['m_InitialTime'],wrap=tree['m_WrapMode'],exposed=exposed,tracks=[],duration=0)
    for pointer in asset.read_typetree()['m_Tracks']:
        track=reader.pointer(asset,pointer);data=track.read_typetree();script=reader.pointer(track,data['m_Script']).read_typetree()['m_ClassName']
        binding=bindings.get(pointer_key(track));node=node_for(ex,binding,root,program['nodes'])
        row=dict(source=identity(ex,track),kind=script,node=node,muted=bool(data['m_Muted']),clips=[],original=data)
        for raw in data['m_Clips']:
            clip_asset=reader.pointer(track,raw['m_Asset']);clip_tree=clip_asset.read_typetree()
            clip=dict(start=raw['m_Start'],duration=raw['m_Duration'],clip_in=raw['m_ClipIn'],speed=raw['m_TimeScale'],pre=raw['m_PreExtrapolationMode'],post=raw['m_PostExtrapolationMode'],pre_time=raw['m_PreExtrapolationTime'],post_time=raw['m_PostExtrapolationTime'],source=identity(ex,clip_asset),original_asset=clip_tree,original=raw,animation='',compatible=False)
            clip['post_unlimited']=not math.isfinite(float(clip['post_time']))
            if clip['post_unlimited']:clip['post_time']=0
            if script=='ControlRandomizedTrack':
                clip['control']=control_data(ex,clip_tree,clip_asset,exposed,root,program,raw)
            if script=='CameraShaker2Track':
                clip['shaker']=shaker_data(clip_tree,raw)
            if script=='AnimationTrack' and node>=0:
                original=reader.pointer(clip_asset,clip_tree['m_Clip']);paths=relative_paths(program['nodes'],[node]);properties={}
                for current,item in enumerate(program['nodes']):
                    parts=[];ancestor=current
                    while ancestor>=0 and ancestor!=node:parts.append(program['nodes'][ancestor]['name']);ancestor=program['nodes'][ancestor]['parent']
                    if ancestor==node:properties['/'.join(reversed(parts))]={name for m in item['materials'] for name in m['properties']}
                restored=animation(ex,original,paths,properties);clip['animation']=restored['id'];clip['compatible']=animation_compatible(data,clip_tree,raw)
                previous=next((i for i,a in enumerate(program['animations']) if a['id']==restored['id']),None)
                if previous is None:program['animations'].append(restored)
                else:program['animations'][previous]=restored
            row['clips'].append(clip);result['duration']=max(result['duration'],clip['start']+clip['duration'])
        result['tracks'].append(row)
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--file',default='2ac223d9b583771c9eb24338');parser.add_argument('--refresh-actions',action='store_true');args=parser.parse_args()
    if not re.fullmatch(r'[0-9a-f]{24}',args.file):parser.error('--file must be a native cache ID')
    ex=RuntimeExport()
    if args.refresh_actions:
        refresh_scale_cache(ex,{'TimelinePlay'},'timeline')
    with asset_lock(ex.imp.local):export_source(ex,args.file)


def export_source(ex,file,suffix='.native-effect.json'):
    program_path=PROJECT/'local/runtime'/f'{file}{suffix}'
    program=json.loads(program_path.read_text(encoding='utf-8'))
    root,_,_=ex.imp.reader.resolve(program['reference']);errors=[];records=[]
    objects=list(ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors))
    for obj,tree in objects:
        if obj.type.name not in ('PlayableDirector','MonoBehaviour','AnimationClip'):continue
        tree=tree or obj.read_typetree();record=dict(source=identity(ex,obj),tree=tree)
        if obj.type.name=='MonoBehaviour':
            script=ex.imp.reader.pointer(obj,tree.get('m_Script',{}))
            record['script']=script.read_typetree() if script else None
        records.append(record)
        kind=record.get('script') or {}
        print(obj.type.name,kind.get('m_ClassName',tree.get('m_Name','')),obj.path_id,list(tree)[:14],flush=True)
    folder=PROJECT/'local/timeline-source-audit';folder.mkdir(exist_ok=True)
    atomic_json(folder/f'{file}.json',dict(reference=program['reference'],objects=records,errors=errors))
    program['timelines']=[timeline(ex,o,root,program) for o,t in objects if o.type.name=='PlayableDirector']
    atomic_json(program_path,program)
    atomic_json(PROJECT/'manifests'/f'original-timeline-{file}.json',dict(reference=program['reference'],timelines=program['timelines'],animations=[dict(id=a['id'],source=a['source'],tracks=len(a['tracks']),duration=a['duration'],unresolved=a.get('unresolved_bindings',[])) for a in program['animations']],complete=False,dependency_errors=errors))
    print('TIMELINE SOURCE',len(records),'records;',len(errors),'dependency errors',flush=True)


if __name__=='__main__':main()
