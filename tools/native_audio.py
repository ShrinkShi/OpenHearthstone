"""Recover source AudioSource/SoundDef and exact referenced clips without playback."""
import argparse
import json
from .asset_lock import atomic_json
from .audio_cache import publish_audio
from .export_runtime import RuntimeExport
from .native_effects import identity, refresh_program_assets, source_program_nodes
from .importer import PROJECT


def export_clip(ex,reference):
    obj,_,_=ex.imp.reader.resolve(reference)
    if obj.type.name!='AudioClip':raise ValueError('Sound reference is not an AudioClip')
    rows=ex.imp.store.db.execute('SELECT id FROM assets WHERE cab=? AND pathid=?',
        (obj.assets_file.name,str(obj.path_id))).fetchall()
    if len(rows)!=1:raise ValueError('Exact AudioClip source must have one asset index row')
    exported=ex.imp.export(rows[0]['id']);target=ex.folder/(ex.key(obj)+'.wav')
    publish_audio(target,exported.read_bytes())
    return dict(reference=reference,source=identity(ex,obj),file=target.name)


def export_sound_def(ex,obj,clip_exporter=export_clip):
    tree=obj.read_typetree();script=ex.imp.reader.pointer(obj,tree.get('m_Script',{}))
    if script is None or script.read_typetree().get('m_ClassName')!='SoundDef':
        return dict(asset_source=identity(ex,obj),unresolved=True)
    clips=[];errors=[]
    for reference in dict.fromkeys([tree.get('m_AudioClip','')]+[c['m_Clip'] for c in tree.get('m_RandomClips',[])]):
        if not reference:continue
        try:clips.append(clip_exporter(ex,reference))
        except (ValueError,KeyError,FileNotFoundError,RuntimeError) as error:errors.append(dict(reference=reference,error=str(error)))
    go=ex.imp.reader.pointer(obj,tree['m_GameObject']);sources=[]
    for item in go.read_typetree()['m_Component']:
        component=ex.imp.reader.pointer(go,item['component'])
        if component and component.type.name=='AudioSource':sources.append(dict(source=identity(ex,component),values=component.read_typetree()))
    filename=ex.key(obj)+'.native-sound.json'
    atomic_json(ex.folder/filename,dict(source=identity(ex,obj),values=tree,audio_sources=sources,clips=clips,errors=errors,complete=False))
    return dict(asset_source=identity(ex,obj),asset_path=filename,unresolved=bool(errors))


def recover(ex,reference,clip_exporter=export_clip,include_program_hierarchy=False):
    root,_,_=ex.imp.reader.resolve(reference);nodes=[];clips={};errors=[]
    def visit(go,path,node=-1,recurse=True):
        transform=None;row=dict(path=path,audio_sources=[],sound_defs=[])
        if node>=0:row.update(node=node,node_known=True)
        for item in go.read_typetree()['m_Component']:
            obj=ex.imp.reader.pointer(go,item['component'])
            if obj is None:continue
            tree=obj.read_typetree()
            if obj.type.name=='Transform':transform=obj
            if obj.type.name=='AudioSource':row['audio_sources'].append(dict(source=identity(ex,obj),values=tree))
            if obj.type.name=='MonoBehaviour':
                script=ex.imp.reader.pointer(obj,tree['m_Script'])
                if script and script.read_typetree()['m_ClassName']=='SoundDef':
                    row['sound_defs'].append(dict(source=identity(ex,obj),values=tree))
                    refs=[tree.get('m_AudioClip','')]+[r['m_Clip'] for r in tree.get('m_RandomClips',[])]
                    for ref in refs:
                        if not ref or ref in clips:continue
                        try:clips[ref]=clip_exporter(ex,ref)
                        except (ValueError,KeyError,FileNotFoundError,RuntimeError) as error:
                            clips[ref]=dict(reference=ref,error=str(error));errors.append(dict(reference=ref,error=str(error)))
        if row['audio_sources'] or row['sound_defs']:nodes.append(row)
        if transform and recurse:
            for pointer in transform.read_typetree()['m_Children']:
                child=ex.imp.reader.pointer(transform,pointer);childgo=ex.imp.reader.pointer(child,child.read_typetree()['m_GameObject'])
                name=childgo.read_typetree()['m_Name'];visit(childgo,path+'/'+name if path else name)
    if include_program_hierarchy:
        source_nodes,_,ordered=source_program_nodes(ex.imp.reader,root)
        for node,(go,(_,_,path)) in enumerate(zip(ordered,source_nodes)):
            visit(go,path,node,recurse=False)
    else:visit(root,'')
    return dict(reference=reference,source=identity(ex,root),nodes=nodes,clips=list(clips.values()),errors=errors,complete=False,
        notes=['Exact source component data and clip provenance; SoundManager selection, ducking, limits, playback and FSM audio callbacks are not restored.'])


def bind_program(ex,root,filename,suffix='.actor-spell.json'):
    path=ex.folder/(ex.key(root)+suffix)
    program=json.loads(path.read_text(encoding='utf-8'))
    if program.get('source')!=identity(ex,root):
        raise ValueError('Source program identity does not match audio root')
    def source_object(source):
        cab=ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower())
        if cab is None:raise ValueError('Audio action source CAB missing')
        return cab.objects[int(source['pathid'])]
    import copy
    decoded=copy.deepcopy(program)
    refresh_program_assets(decoded,source_object,ex.imp.reader,None,{'AudioPlaythroughAction'})
    for machine,new_machine in zip(program['machines'],decoded['machines']):
        for state,new_state in zip(machine['states'],new_machine['states']):
            for action,new_action in zip(state['actions'],new_state['actions']):
                if action['name']=='AudioPlaythroughAction':
                    for param,value in zip(action['params'],new_action['params']):
                        if param['kind']==24:param.clear();param.update(value)
    program['audio_file']=filename
    atomic_json(path,program)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reference',default='Card_Play_Ally_SummonIn.prefab:786da6b44c25c46d39459e873d237292')
    parser.add_argument('--bind-actor-spell',action='store_true')
    parser.add_argument('--opponent',action='store_true',help='Recover the exact opponent summon slot without overwriting the friendly audit')
    args=parser.parse_args()
    if args.opponent:args.reference='Card_Play_Ally_SummonIn_Opponent.prefab:b68ecaea3f52740beac5429761699e02'
    ex=RuntimeExport();result=recover(ex,args.reference)
    root,_,_=ex.imp.reader.resolve(args.reference);filename=ex.key(root)+'.native-audio.json'
    atomic_json(ex.folder/filename,result);result['cache']='local/runtime/'+filename
    if args.bind_actor_spell:bind_program(ex,root,filename)
    atomic_json(PROJECT/('manifests/original-opponent-summon-audio.json' if args.opponent else 'manifests/original-summon-audio.json'),result)
    print(json.dumps(dict(nodes=len(result['nodes']),clips=len(result['clips']),errors=result['errors'],cache=result['cache']),ensure_ascii=False))


if __name__=='__main__':main()
