"""Refresh only exact non-null AudioPlayClipAction template parameters."""
import json
from .export_runtime import RuntimeExport
from .native_effects import parameters,fsm_asset
from .asset_lock import atomic_json
from .importer import PROJECT


def refresh(ex):
    records=[];updated=0
    for path in sorted(ex.folder.glob('*.native-effect.json')):
        program=json.loads(path.read_text(encoding='utf-8'))
        if not any(a['name']=='AudioPlayClipAction' and any(p['name']=='m_TemplateSource' and not p.get('object_null') for p in a['params']) for m in program['machines'] for s in m['states'] for a in s['actions']):continue
        before=json.dumps(program,sort_keys=True)
        for machine in program['machines']:
            source=machine['source'];owner=ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
            fsm=owner.read_typetree()['fsm'];variables={v['name']:v for group in fsm.get('variables',{}).values() if isinstance(group,list) for v in group if isinstance(v,dict) and 'name' in v}
            for state,original in zip(machine['states'],fsm['states']):
                if state['name']!=original['name']:raise ValueError('Source state sequence changed')
                data=original.get('actionData',{});enabled=[(i,n.rsplit('.',1)[-1]) for i,n in enumerate(data.get('actionNames',[])) if data.get('actionEnabled',[1]*len(data.get('actionNames',[])))[i]]
                for action,(index,name) in zip(state['actions'],enabled):
                    if action['name']!=name:raise ValueError('Source action sequence changed')
                    if name!='AudioPlayClipAction':continue
                    param=next(p for p in action['params'] if p['name']=='m_TemplateSource')
                    if param.get('object_null'):continue
                    decoded=next(p for p in parameters(data,index,owner,ex.imp.reader,{},variables,fsm.get('dataVersion',0),lambda obj:fsm_asset(ex,obj)) if p['name']=='m_TemplateSource')
                    if decoded.get('unresolved') or not decoded.get('asset_path'):raise ValueError('Original explicit template unresolved')
                    param.clear();param.update(decoded);records.append(dict(file=path.name,state=state['name'],parameter=decoded))
        if before!=json.dumps(program,sort_keys=True):atomic_json(path,program);updated+=1
    atomic_json(PROJECT/'manifests/native-audio-templates.json',dict(templates=records,complete=False))
    print('ORIGINAL AUDIO TEMPLATES',len(records),'actions;',updated,'updated',flush=True)
    return records


if __name__=='__main__':refresh(RuntimeExport())
