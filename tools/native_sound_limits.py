"""Audit original limited actions and explicit mixer-template dependencies."""
import json
from .export_runtime import RuntimeExport
from .native_effects import identity,parameters
from .asset_lock import atomic_json
from .importer import PROJECT


def audit(ex):
    rows=json.loads((PROJECT/'manifests/native-clip-audio-refresh.json').read_text(encoding='utf-8'))['programs']
    limited=[];templates=[];mixers={}
    def mixer(obj):
        key=(obj.assets_file.name,obj.path_id)
        if key in mixers:return
        tree=obj.read_typetree();mixers[key]=dict(source=identity(ex,obj),type=obj.type.name,values=tree)
        def visit(value):
            if isinstance(value,dict):
                if 'm_PathID' in value and value['m_PathID']:
                    child=ex.imp.reader.pointer(obj,value)
                    if child is not None and child.type.name.startswith('AudioMixer'):mixer(child)
                else:
                    for child in value.values():visit(child)
            elif isinstance(value,list):
                for child in value:visit(child)
        visit(tree)
    for row in rows:
        program=json.loads((ex.folder/row['file']).read_text(encoding='utf-8'))
        for machine in program['machines']:
            if not any(a['name']=='AudioPlayClipAction' and any(p['name']=='m_InstanceLimited' and p['number'] or p['name']=='m_TemplateSource' and not p.get('object_null') for p in a['params']) for s in machine['states'] for a in s['actions']):continue
            source=machine['source'];owner=ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
            fsm=owner.read_typetree()['fsm'];variables={v['name']:v for group in fsm.get('variables',{}).values() if isinstance(group,list) for v in group if isinstance(v,dict) and 'name' in v}
            for state,original in zip(machine['states'],fsm['states']):
                if state['name']!=original['name']:raise ValueError('Source state sequence changed')
                data=original.get('actionData',{});enabled=[(i,n.rsplit('.',1)[-1]) for i,n in enumerate(data.get('actionNames',[])) if data.get('actionEnabled',[1]*len(data['actionNames']))[i]]
                for action,(index,name) in zip(state['actions'],enabled):
                    if action['name']!=name:raise ValueError('Source action sequence changed')
                    if name!='AudioPlayClipAction':continue
                    params={p['name']:p for p in action['params']};decoded={p['name']:p for p in parameters(data,index,owner,ex.imp.reader,{},variables,fsm.get('dataVersion',0))}
                    for key in ('m_InstanceLimited','m_InstanceLimitedDuration','m_InstanceLimitMaximum','m_LimitMaxOutOption'):
                        if params[key]['number']!=decoded[key]['number']:raise ValueError('Source limit parameter mismatch')
                    record=dict(file=row['file'],reference=row['reference'],state=state['name'],source=source,index=index)
                    if params['m_InstanceLimited']['number']:
                        limited.append(dict(**record,options={k:params[k]['number'] for k in ('m_InstanceLimited','m_InstanceLimitedDuration','m_InstanceLimitMaximum','m_LimitMaxOutOption')}))
                    if not params['m_TemplateSource'].get('object_null'):
                        start=data['actionStartIndex'][index];end=data['actionStartIndex'][index+1] if index+1<len(data['actionStartIndex']) else len(data['paramName'])
                        n=next(n for n in range(start,end) if data['paramName'][n]=='m_TemplateSource')
                        template=ex.imp.reader.pointer(owner,data['unityObjectParams'][data['paramDataPos'][n]])
                        if template is None or template.type.name!='AudioSource':raise ValueError('Original explicit template is not AudioSource')
                        values=template.read_typetree();group=ex.imp.reader.pointer(template,values['OutputAudioMixerGroup'])
                        if group is None:raise ValueError('Expected original explicit mixer group')
                        mixer(group);templates.append(dict(**record,template_source=identity(ex,template),values=values,mixer_group=identity(ex,group)))
    result=dict(limited_actions=limited,explicit_templates=templates,mixer_assets=list(mixers.values()),complete=False,
        notes=['Exact original options and mixer dependencies. Mixer assets are source metadata, not rebuilt Unity mixer resources.'])
    atomic_json(PROJECT/'manifests/native-sound-limits.json',result)
    print('ORIGINAL SOUND LIMIT AUDIT',len(limited),'actions;',len(templates),'explicit templates;',len(mixers),'mixer assets',flush=True)
    return result


if __name__=='__main__':audit(RuntimeExport())
