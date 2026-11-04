"""Read exact original clip modes without mutating reviewed runtime programs."""
import json
from collections import Counter

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import animation_wrap, identity, legacy_animation_settings


def main():
    ex=RuntimeExport();resolved={};records=[];failures=[];occurrences=0;components=[]
    for path in sorted(ex.folder.glob('*.native-effect.json')):
        program=json.loads(path.read_text(encoding='utf-8'))
        original={}
        try:
            root,_,_=ex.imp.reader.resolve(program['reference']);errors=[]
            for obj,tree in ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors):
                if obj.type.name=='AnimationClip':original[ex.key(obj)]=obj
                elif obj.type.name=='Animation':
                    components.append(dict(cache=path.name,**legacy_animation_settings(ex,obj,errors)))
            if errors:failures.append(dict(cache=path.name,error='Dependency walk: '+str(errors)))
        except (KeyError,ValueError,FileNotFoundError) as error:
            failures.append(dict(cache=path.name,error=str(error)))
        for clip in program.get('animations',[]):
            occurrences+=1
            key=clip['id']
            if key in resolved:continue
            resolved[key]=None
            source=clip.get('source')
            try:
                obj=original.get(key)
                if obj is None:
                    if not source:raise ValueError('Original clip not found by exact ID in prefab dependency walk')
                    env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
                    if cab is None:raise ValueError('Source CAB missing')
                    obj=cab.objects[int(source['pathid'])]
                if obj.type.name!='AnimationClip':raise ValueError('Source is not AnimationClip')
                if ex.key(obj)!=key:raise ValueError('Source clip ID changed')
                source=identity(ex,obj)
                tree=obj.read_typetree()
                if tree['m_Name']!=clip['name']:raise ValueError('Source clip name changed')
                record=dict(id=clip['id'],name=clip['name'],source=source,**animation_wrap(tree),
                            legacy=bool(tree.get('m_Legacy',True)),runtime_loop=clip['loop'])
                resolved[key]=record;records.append(record)
            except (KeyError,ValueError,FileNotFoundError) as error:
                failures.append(dict(cache=path.name,id=key,source=source,error=str(error)))
    counts=Counter(str(r['wrap_mode']) if r['wrap_known'] else 'unknown' for r in records)
    report=dict(kind='original-animation-wrap-audit',restored=False,occurrences=occurrences,
                unique_clips=len(records),wrap_modes=dict(sorted(counts.items())),clips=records,components=components,failures=failures,
                notes=['Read-only source and runtime cache audit. No effect automatically enabled.',
                       'Default wrap 0 requires Animation component/state override resolution.',
                       'Production completion still uses fixed duration.'])
    atomic_json(PROJECT/'manifests/native-animation-wrap-audit.json',report)
    print('ANIMATION WRAP AUDIT',occurrences,'occurrences;',len(records),'unique;',dict(counts),';',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('Original animation wrap sources failed validation')


if __name__=='__main__':main()
