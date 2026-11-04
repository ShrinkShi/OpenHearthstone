"""Refresh exact source ColorBySpeed modules while preserving other cached fields."""
import json

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import identity, color_by_speed
from .timeline_source import node_for


def main():
    ex=RuntimeExport();records=[];failures=[];updated=0
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'))
            selected={i for i,n in enumerate(program['nodes']) if n.get('particle') and
                      ('ColorBySpeedModule' in n['particle']['unsupported'] or n['particle'].get('color_speed'))}
            if not selected:continue
            before=json.dumps(program,sort_keys=True);errors=[];modules=[];found=set()
            try:
                root,_,_=ex.imp.reader.resolve(program['reference'])
                for obj,tree in ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors):
                    if obj.type.name!='ParticleSystem':continue
                    index=node_for(ex,obj,root,program['nodes'])
                    if index not in selected:continue
                    if index in found:raise ValueError('Ambiguous source particle node')
                    original=(tree or obj.read_typetree())['ColorBySpeedModule'];value=color_by_speed(original)
                    if value is None:raise ValueError('Source ColorBySpeed changed since cache export')
                    found.add(index);data=program['nodes'][index]['particle'];data['color_speed']=value
                    data['unsupported']=[name for name in data['unsupported'] if name!='ColorBySpeedModule']
                    modules.append(dict(node=index,path=program['nodes'][index]['path'],source=identity(ex,obj),original=original))
                if found!=selected:raise ValueError('Unresolved exact source particle nodes: '+str(sorted(selected-found)))
                changed=before!=json.dumps(program,sort_keys=True)
                if changed:atomic_json(path,program);updated+=1
                records.append(dict(file=path.name,reference=program['reference'],modules=modules,updated=changed,dependency_errors=errors))
                print('COLOR BY SPEED',path.name,len(modules),'source modules',flush=True)
            except (KeyError,ValueError,FileNotFoundError) as error:
                failures.append(dict(file=path.name,error=str(error)))
    atomic_json(PROJECT/'manifests/native-color-speed-refresh.json',dict(programs=records,updated=updated,failures=failures,
                notes=['Exact source ColorBySpeed parameters only. Other modules and card enable decisions are unchanged.']))
    print('COLOR BY SPEED REFRESH',len(records),'programs;',updated,'updated;',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('ColorBySpeed source mapping failures remain')


if __name__=='__main__':main()
