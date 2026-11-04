"""Refresh only exact-source legacy mode metadata in existing effect programs."""
import json
from collections import Counter

from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import animation_wrap, identity, legacy_animation_settings, pointer_key


def source_nodes(ex,root,objects,program):
    ids={};seen=set();nodes=[]
    def visit(go,parent,path):
        key=pointer_key(go)
        if key in seen:return
        seen.add(key);tree=go.read_typetree()
        components=[ex.imp.reader.pointer(go,item['component']) for item in tree.get('m_Component',[])]
        transform=next((obj for obj in components if obj and obj.type.name=='Transform'),None)
        if transform is None:return
        index=len(nodes);nodes.append((tree['m_Name'],parent,path));ids[key]=index
        for pointer in transform.read_typetree().get('m_Children',[]):
            child=ex.imp.reader.pointer(transform,pointer)
            if child:
                child_go=ex.imp.reader.pointer(child,child.read_typetree()['m_GameObject'])
                if child_go:visit(child_go,index,(path+'/' if path else '')+child_go.read_typetree()['m_Name'])
    visit(root,-1,'')
    game_objects={pointer_key(obj):obj for obj,tree in objects if obj.type.name=='GameObject'}
    for go in game_objects.values():
        if pointer_key(go) in seen:continue
        ancestor=go;ancestry=set()
        while pointer_key(ancestor) not in ancestry:
            ancestry.add(pointer_key(ancestor));transform=None
            for component in ancestor.read_typetree().get('m_Component',[]):
                obj=ex.imp.reader.pointer(ancestor,component['component'])
                if obj and obj.type.name=='Transform':transform=obj;break
            if transform is None:break
            parent=ex.imp.reader.pointer(transform,transform.read_typetree().get('m_Father',{}))
            parent=ex.imp.reader.pointer(parent,parent.read_typetree()['m_GameObject']) if parent else None
            if parent is None or pointer_key(parent) not in game_objects:break
            ancestor=parent
        visit(ancestor,-1,'')
    if nodes!=[(n['name'],n['parent'],n['path']) for n in program['nodes']]:
        raise ValueError('Source hierarchy changed; do not attach mode metadata to guessed nodes')
    return ids


def apply_modes(program,clip_sources,components):
    # Validate everything before mutating this program, so a failure is atomic.
    selected=[]
    for clip in program['animations']:
        source=clip_sources.get(clip['id'])
        if source is None or source['name']!=clip['name']:raise ValueError('Exact source Clip missing or renamed')
        selected.append((clip,source))
    for node,settings in components:
        if not 0<=node<len(program['nodes']):raise ValueError('Animation owner is not in source hierarchy')
        if settings['default_clip'] and settings['default_clip'] not in clip_sources:
            raise ValueError('Default Clip not resolved by exact source ID')
    for clip,source in selected:
        for field in ('wrap_mode','wrap_known','source'):clip[field]=source[field]
    for node,settings in components:program['nodes'][node]['legacy_animation']=settings


def main():
    ex=RuntimeExport();records=[];failures=[];warnings=[];resolved={};updated=0
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'))
            try:
                root,_,_=ex.imp.reader.resolve(program['reference']);errors=[]
                if ex.key(root)!=path.name.split('.')[0]:raise ValueError('Source root ID changed')
                objects=list(ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors))
                ids=source_nodes(ex,root,objects,program);clips={};components=[]
                for obj,tree in objects:
                    if obj.type.name=='AnimationClip':
                        key=ex.key(obj)
                        if key not in resolved:
                            tree=obj.read_typetree();resolved[key]=dict(name=tree['m_Name'],source=identity(ex,obj),**animation_wrap(tree))
                        clips[key]=resolved[key]
                    elif obj.type.name=='Animation':
                        owner=ex.imp.reader.pointer(obj,tree['m_GameObject'])
                        components.append((ids.get(pointer_key(owner),-1),legacy_animation_settings(ex,obj,errors)))
                before=json.dumps(program,sort_keys=True)
                apply_modes(program,clips,components)
                changed=before!=json.dumps(program,sort_keys=True)
                if changed:atomic_json(path,program);updated+=1
                if errors:warnings.append(dict(file=path.name,dependency_errors=errors))
                records.append(dict(file=path.name,reference=program['reference'],updated=changed,clips=len(program['animations']),
                                    components=[dict(node=n,**s) for n,s in components]))
            except (KeyError,ValueError,FileNotFoundError) as error:
                failures.append(dict(file=path.name,error=str(error)))
            if (len(records)+len(failures))%25==0:print('WRAP REFRESH',len(records),'validated;',len(failures),'failed',flush=True)
    atomic_json(PROJECT/'manifests/native-animation-wrap-refresh.json',dict(kind='exact-source-animation-wrap-refresh',
        restored=False,programs=records,updated=updated,failures=failures,dependency_warnings=warnings,
        clip_modes=dict(Counter(str(s['wrap_mode']) for s in resolved.values())),
        notes=['Only mode/source metadata changed. Curves, particles, materials and enable flags preserved.',
               'Reviewed enabled program hashes require fresh GPU capture and local image review before re-registration.',
               'Dependency warnings are retained separately from exact Clip/owner mapping failures.']))
    print('WRAP REFRESH COMPLETE',len(records),'programs;',updated,'updated;',len(failures),'failures;',len(warnings),'dependency warnings',flush=True)
    if failures:raise RuntimeError('Exact source mode refresh failures remain in audit')


if __name__=='__main__':main()
