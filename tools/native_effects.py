"""Export original effect hierarchies, Shuriken modules and typed FSM actions.

Original bytes stay in local/. The audit never labels unsupported actions complete.
"""
import argparse
import json
import math
import re
import struct
from collections import Counter

from .asset_lock import atomic_json, asset_lock
from .board_ui_assets import source_bundle
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_animation import decode as decode_mecanim, relative_paths, controller_states


SUPPORTED_ACTIONS = {'SpellStateAction', 'SpellFinishAction', 'SpellCardTypeAction', 'GetBoardAmbientColorAction', 'GetAmbientColorAction',
    'ParticlePlayAction', 'ParticleStopAction', 'SetVisibility', 'SetVisibilityRecursiveAction', 'SetVisibilityRecursiveDelayAction', 'ActivateGameObject', 'SetColorRGBA',
    'Wait', 'SetAnimationSpeed', 'AnimationPlaythroughAction', 'SetMaterialFloat',
    'SetMaterialColor', 'EaseFloat', 'EaseColor', 'AnimateFloat', 'ColorInterpolate', 'AudioSetVolumeAction', 'AudioStopAction', 'AudioVolumeFadeAction', 'AudioLoopingStartAction', 'AudioLoopingEndAction', 'MainCameraShakerAction', 'RandomWaitAction', 'PlayAnimation'}


def keys(curve, component=None):
    result = []
    for key in curve.get('m_Curve', []):
        def value(name, default=0):
            data = key.get(name, default)
            data = data.get(component, 0) if component and isinstance(data, dict) else data
            return data if isinstance(data, (int, float)) and math.isfinite(data) else 0
        result.append(dict(time=key['time'], value=value('value'), inSlope=value('inSlope'),
            outSlope=value('outSlope'), inWeight=value('inWeight',1/3), outWeight=value('outWeight',1/3),
            weightedMode=key.get('weightedMode', 0), step=not math.isfinite(key.get('outSlope', 0))
            if not component else False))
    return result


def curve(data=None):
    data = data or {}
    return dict(mode=data.get('minMaxState', 0), scalar=data.get('scalar', 0),
        minimum=data.get('minScalar', data.get('scalar', 0)), maximum=keys(data.get('maxCurve', {})),
        minimum_keys=keys(data.get('minCurve', {})))


def gradient_keys(data):
    return dict(mode=data.get('m_Mode', 0),
        colors=[dict(time=data['ctime'+str(i)]/65535, value=data['key'+str(i)]) for i in range(data.get('m_NumColorKeys', 0))],
        alpha=[dict(time=data['atime'+str(i)]/65535, value=data['key'+str(i)]['a']) for i in range(data.get('m_NumAlphaKeys', 0))])


def gradient(data=None):
    data=data or {}
    return dict(enabled=True,mode=data.get('minMaxState', 0), maximum=data.get('maxColor', dict(r=1,g=1,b=1,a=1)),
        minimum=data.get('minColor', dict(r=1,g=1,b=1,a=1)), high=gradient_keys(data.get('maxGradient', {})),
        low=gradient_keys(data.get('minGradient', {})))


def color_by_speed(module=None):
    if not module or not module.get('enabled'):return None
    return dict(color=gradient(module['gradient']),range=module['range'])


def shape_radius_value(value):
    if isinstance(value,dict):
        allowed={'value','mode','spread','speed'}
        if set(value)-allowed or 'value' not in value:
            raise ValueError('Unsupported ShapeModule.radius object schema')
        mode=value.get('mode',0);spread=value.get('spread',0)
        if isinstance(mode,bool) or not isinstance(mode,(int,float)) or mode!=0 or \
                isinstance(spread,bool) or not isinstance(spread,(int,float)) or not math.isfinite(spread) or spread!=0:
            raise ValueError('ShapeModule.radius mode/spread cannot be represented by NativeShape.radius')
        speed=value.get('speed')
        if speed is not None:
            if not isinstance(speed,dict) or set(speed)-{'minMaxState','scalar','minScalar','maxCurve','minCurve'}:
                raise ValueError('Unsupported ShapeModule.radius speed schema')
            state=speed.get('minMaxState',0);scalar=speed.get('scalar',1);minimum=speed.get('minScalar',1)
            if any(isinstance(number,bool) or not isinstance(number,(int,float)) or not math.isfinite(number)
                    for number in (state,scalar,minimum)) or state!=0 or scalar!=1 or minimum!=1:
                raise ValueError('ShapeModule.radius speed is not the constant-one source default')
            for curve_name in ('maxCurve','minCurve'):
                curve_data=speed.get(curve_name)
                if curve_data is not None and (not isinstance(curve_data,dict) or
                        set(curve_data)-{'m_Curve','m_PreInfinity','m_PostInfinity','m_RotationOrder'} or
                        curve_data.get('m_Curve',[])!=[]):
                    raise ValueError('ShapeModule.radius speed curve cannot be represented by NativeShape.radius')
        value=value['value']
    if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value):
        raise ValueError('ShapeModule.radius must resolve to a finite scalar')
    return value


def identity(ex, obj):
    return dict(cab=obj.assets_file.name, pathid=str(obj.path_id), type=obj.type.name,
                bundle=source_bundle(ex, obj.assets_file.name))


def source_layer_value(game_object):
    layer=game_object.get('m_Layer',0)
    if isinstance(layer,bool) or not isinstance(layer,int) or not 0<=layer<=31:
        raise ValueError('GameObject.m_Layer must be an integer between 0 and 31')
    return layer


def pointer_key(obj):
    return (obj.assets_file.name, obj.path_id) if obj else None


def machine_owner(component, reader, node_ids):
    go=reader.pointer(component,component.read_typetree().get('m_GameObject',{}))
    index=node_ids.get(pointer_key(go),-1) if go is not None else -1
    return dict(owner=index,owner_known=index>=0)


def dependency(ex, owner, pointer, errors):
    try:
        return ex.imp.reader.pointer(owner,pointer)
    except (KeyError,FileNotFoundError) as error:
        if '内建资源' not in str(error):raise
        errors.append(dict(owner=identity(ex,owner),pointer=pointer,error=str(error)))
        return None


def event_text(data, index, data_version):
    pos=data['paramDataPos'][index]
    if data_version>1:
        strings=data.get('stringParams', [])
        return strings[pos] if 0<=pos<len(strings) else ''
    raw=bytes(data.get('byteData', []));size=data['paramByteDataSize'][index]
    if pos<0 or size<0 or pos+size>len(raw):raise ValueError('Truncated FsmEvent')
    return raw[pos:pos+size].decode('utf-8')


def string_parameter(data, index, data_version):
    pos=data['paramDataPos'][index]
    if data_version>1:
        strings=data.get('stringParams', [])
        if not 0<=pos<len(strings):raise ValueError('String parameter index is out of range')
        return strings[pos]
    raw=bytes(data.get('byteData', []));size=data['paramByteDataSize'][index]
    if pos<0 or size<0 or pos+size>len(raw):raise ValueError('Truncated string parameter')
    return raw[pos:pos+size].decode('utf-8')


def parameters(data, index, owner, reader, node_ids, variables, data_version=2, export_asset=None):
    start=data['actionStartIndex'][index]
    end=data['actionStartIndex'][index+1] if index+1<len(data['actionStartIndex']) else len(data.get('paramName', []))
    output=[]
    containers={15:'fsmFloatParams',16:'fsmIntParams',17:'fsmBoolParams',18:'fsmStringParams',
        19:'fsmGameObjectParams',20:'fsmOwnerDefaultParams',24:'fsmObjectParams',25:'fsmColorParams',
        28:'fsmVector3Params',32:'fsmObjectParams',33:'fsmObjectParams',5:'unityObjectParams'}
    raw=bytes(data.get('byteData', []))
    for n in range(start,end):
        kind,pos,name=data['paramDataType'][n],data['paramDataPos'][n],data['paramName'][n]
        param=dict(name=name,kind=kind,node=-1,number=0,text='',variable='')
        try:
            if kind in (0,1,2,7):
                fmt={0:'<i',1:'<?',2:'<f',7:'<i'}[kind];param['number']=struct.unpack_from(fmt,raw,pos)[0]
            elif kind==3:param['text']=string_parameter(data,n,data_version)
            elif kind==12:
                count=data['arrayParamSizes'][pos];element_type=data['arrayParamTypes'][pos]
                array_kinds={
                    'HutongGames.PlayMaker.FsmBool':17,
                    'HutongGames.PlayMaker.FsmColor':25,
                    'HutongGames.PlayMaker.FsmFloat':15,
                    'HutongGames.PlayMaker.FsmInt':16,
                    'SoundDef':5,
                    'System.String':3,
                }
                expected_kind=array_kinds.get(element_type)
                if expected_kind is None or count<0 or n+count>=end or any(data['paramDataType'][j]!=expected_kind for j in range(n+1,n+count+1)):
                    raise ValueError('Unrestored array element type or span')
                param.update(array_known=True,array_count=count,array_type=element_type)
            elif kind==23:
                # ActionData.GetStringParam for v2; FsmUtility.ByteArrayToFsmEvent for v1.
                param['text']=event_text(data,n,data_version)
            elif kind==22:
                value=data['animationCurveParams'][pos]
                value=value.get('curve',value);param['curve']=keys(value)
                param['pre_wrap']=value.get('m_PreInfinity',0);param['post_wrap']=value.get('m_PostInfinity',0);param['wrap_known']=True
            elif kind==36:
                from .fsm_property import decode as decode_property
                param['property_data']=decode_property(data['fsmPropertyParams'][pos],owner,reader,node_ids,variables)
            elif kind in (15,16,17,25,28) and data.get('paramByteDataSize',[0]*len(data['paramName']))[n]>0:
                fmt={15:'<f',16:'<i',17:'<?',25:'<ffff',28:'<fff'}[kind];size=struct.calcsize(fmt)
                length=data['paramByteDataSize'][n]
                if pos<0 or length<size+1 or pos+length>len(raw):raise ValueError('Truncated packed named variable')
                values=struct.unpack_from(fmt,raw,pos);name=raw[pos+size+1:pos+length].decode('utf-8')
                param['none_known']=True;param['is_none']=not name and bool(raw[pos+size]);param['variable']=name
                if name:
                    if name not in variables:param['unresolved']=True
                    value=variables.get(name,{}).get('value',None)
                else:value=dict(zip(('r','g','b','a') if kind==25 else ('x','y','z'),values)) if kind in (25,28) else values[0]
                if kind in (25,28) and isinstance(value,dict):param['color' if kind==25 else 'vector']=value
                elif kind not in (25,28) and isinstance(value,(int,float,bool)):param['number']=value
                else:param['unresolved']=True
            elif kind in containers:
                value=data[containers[kind]][pos]
                if kind==20:
                    if value.get('ownerOption')==0:
                        param['node']=node_ids.get(pointer_key(reader.pointer(owner,owner.read_typetree()['m_GameObject'])),-1)
                        # The unused explicit object may be null or stale. Owner
                        # selection must not be overwritten by that pointer.
                        output.append(param)
                        continue
                    value=value.get('gameObject', {})
                if isinstance(value,dict) and value.get('useVariable'):
                    param['none_known']=True;param['is_none']=not bool(value.get('name',''))
                    param['variable']=value.get('name','');value=variables.get(param['variable'],value)
                elif isinstance(value,dict) and 'useVariable' in value:
                    param['none_known']=True;param['is_none']=False
                if isinstance(value,dict) and 'value' in value:value=value['value']
                if isinstance(value,(int,float,bool)):param['number']=value
                elif isinstance(value,str):param['text']=value
                elif isinstance(value,dict) and 'm_PathID' in value:
                    if kind in (32,33):
                        param['asset_type']='Material' if kind==32 else 'Texture'
                        param['object_null']=value['m_PathID']==0
                        if not param['object_null']:
                            target=reader.pointer(owner,value)
                            expected=('Material',) if kind==32 else ('Texture2D','RenderTexture','Cubemap')
                            if target is None or target.type.name not in expected:
                                param['unresolved']=True
                            elif export_asset is None:
                                param['unresolved']=True
                            else:param.update(export_asset(target))
                    else:
                        target=reader.pointer(owner,value)
                        param['node']=node_ids.get(pointer_key(target),-1)
                        if kind==5:
                            param['object_null']=value['m_PathID']==0
                            if target is not None and target.type.name=='AudioSource':
                                if export_asset is None:param['unresolved']=True
                                else:param.update(export_asset(target))
                            elif target is not None and target.type.name=='MonoBehaviour':
                                script=reader.pointer(target,target.read_typetree().get('m_Script',{}))
                                if script and script.read_typetree().get('m_ClassName')=='SoundDef':
                                    param['asset_type']='SoundDef'
                                    if export_asset is None:param['unresolved']=True
                                    else:param.update(export_asset(target))
                                else:param['unresolved']=True
                        if kind==24:
                            param['object_null']=value['m_PathID']==0
                            if target is not None:
                                param['asset_type']=target.type.name
                                if target.type.name=='MonoBehaviour':
                                    script=reader.pointer(target,target.read_typetree().get('m_Script',{}))
                                    if script and script.read_typetree().get('m_ClassName')=='SoundDef' and export_asset is not None:param.update(export_asset(target))
                                    else:param['unresolved']=param['node']<0
                                elif target.type.name=='Mesh':
                                    if export_asset is None:param['unresolved']=True
                                    else:param.update(export_asset(target))
                                elif target.type.name=='AudioSource':
                                    go=reader.pointer(target,target.read_typetree().get('m_GameObject',{}))
                                    param['node']=node_ids.get(pointer_key(go),-1)
                                    param['asset_source']=dict(cab=target.assets_file.name,pathid=str(target.path_id),type='AudioSource')
                                    param['unresolved']=param['node']<0
                                else:param['unresolved']=param['node']<0
                elif isinstance(value,dict):param['color' if kind==25 else 'vector']=value
            else:param['unresolved']=True
        except (IndexError,KeyError,ValueError,struct.error):param['unresolved']=True
        # JsonUtility does not coerce a JSON boolean into a float field.
        # Both PlayMaker formats must use the same numeric bool representation.
        if isinstance(param['number'],bool):param['number']=int(param['number'])
        output.append(param)
    return output


def fsm_asset(ex, obj):
    result=dict(asset_source=identity(ex,obj))
    if obj.type.name=='Texture2D':result['asset_path']=ex.texture(obj)
    elif obj.type.name=='Mesh':result['asset_path']=ex.mesh(obj)
    elif obj.type.name=='Material':
        errors=[];value=material(ex,obj,errors);value['dependency_errors']=errors;filename=ex.key(obj)+'.native-material.json'
        atomic_json(ex.folder/filename,value);result['asset_path']=filename
    elif obj.type.name=='MonoBehaviour':
        from .native_audio import export_sound_def
        result.update(export_sound_def(ex,obj))
    elif obj.type.name=='AudioSource':
        from .native_audio_config import export_template
        result.update(export_template(ex,obj))
    else:result['unresolved']=True
    return result


def texture_variables(fsm, owner, reader, export_asset):
    result=[]
    for variable in fsm.get('variables',{}).get('textureVariables',[]):
        pointer=variable.get('value');record=dict(name=variable['name'],path='',object_null=False,unresolved=False)
        if not isinstance(pointer,dict) or 'm_PathID' not in pointer:
            record['unresolved']=True
        elif pointer['m_PathID']==0:record['object_null']=True
        else:
            target=reader.pointer(owner,pointer)
            if target is None or target.type.name!='Texture2D':record['unresolved']=True
            else:
                asset=export_asset(target);record['source']=asset.get('asset_source');record['path']=asset.get('asset_path','')
                record['unresolved']=bool(asset.get('unresolved')) or not record['path']
        result.append(record)
    return result


def refresh_program_assets(program, source_object, reader, export_asset, action_names=None, node_ids=None):
    count=0
    for machine in program['machines']:
        owner=source_object(machine['source']);fsm=owner.read_typetree()['fsm']
        if action_names is not None and action_names.intersection({'SpellInitActorVariables','GetCardPortraitTextureAction','InitializeCustomSpawn','FinalizeCustomSpawn','ActorEliteEventAction','SetMesh','SetMaterial','AudioPlayClipAction'}) and node_ids is not None:
            machine.update(machine_owner(owner,reader,node_ids))
        if action_names is not None and 'GetCardPortraitTextureAction' in action_names:
            machine['textures']=texture_variables(fsm,owner,reader,export_asset)
        variables={v['name']:v for group in fsm.get('variables',{}).values()
                   if isinstance(group,list) for v in group if isinstance(v,dict) and 'name' in v}
        if len(machine['states'])!=len(fsm['states']):raise ValueError('Source FSM state count changed')
        for state,original in zip(machine['states'],fsm['states']):
            if state['name']!=original['name']:raise ValueError('Source FSM state sequence changed')
            data=original.get('actionData',{})
            actions=[(i,name.rsplit('.',1)[-1]) for i,name in enumerate(data.get('actionNames',[]))
                     if data.get('actionEnabled',[1]*len(data['actionNames']))[i]]
            if [a['name'] for a in state['actions']]!=[name for _,name in actions]:
                raise ValueError('Source FSM action sequence changed')
            for action,(index,_) in zip(state['actions'],actions):
                decoded=parameters(data,index,owner,reader,node_ids or {},variables,fsm.get('dataVersion',0),export_asset)
                if len(action['params'])!=len(decoded):raise ValueError('Source FSM parameter count changed')
                for param,value in zip(action['params'],decoded):
                    if (param['name'],param['kind'])!=(value['name'],value['kind']):
                        raise ValueError('Source FSM parameter identity changed')
                    if (action_names is None and param['kind'] in (32,33)) or (action_names is not None and action['name'] in action_names):
                        param.clear();param.update(value);count+=1
        if action_names is not None:
            machine['vectors']=[dict(name=v['name'],value=v['value']) for v in fsm.get('variables',{}).get('vector3Variables',[])]
    return count


def source_spell_placements(reader, root, node, bundle):
    result = []
    for item in root.read_typetree()['m_Component']:
        obj = reader.pointer(root, item['component'])
        if obj is None or obj.type.name != 'MonoBehaviour':
            continue
        tree = obj.read_typetree()
        if 'm_Location' not in tree or 'm_Facing' not in tree:
            continue
        script = reader.pointer(obj, tree['m_Script'])
        result.append(dict(kind=script.read_typetree()['m_ClassName'], node=node,
            enabled=bool(tree['m_Enabled']), location=tree['m_Location'],
            location_transform=tree['m_LocationTransformName'],
            parent_to_location=bool(tree['m_SetParentToLocation']), facing=tree['m_Facing'],
            rotate_x=bool(tree['m_FacingOptions']['m_RotateX']),
            rotate_y=bool(tree['m_FacingOptions']['m_RotateY']),
            source_null=tree['m_source']['m_PathID'] == 0,
            source=dict(cab=pointer_key(obj)[0], pathid=str(obj.path_id), bundle=bundle), raw=tree))
    return result


def source_program_nodes(reader, root):
    """Replay the exporter hierarchy, including detached referenced prefab roots."""
    node_ids, seen, source_nodes, ordered = {}, set(), [], []
    def transform_for(go):
        return next((obj for item in go.read_typetree().get('m_Component', [])
                     if (obj := reader.pointer(go, item['component']))
                     and obj.type.name == 'Transform'), None)
    def visit(go, parent, path):
        key = pointer_key(go)
        if key in seen:
            return
        seen.add(key)
        tree = go.read_typetree()
        transform = transform_for(go)
        if transform is None:
            return
        index = len(source_nodes)
        source_nodes.append((tree['m_Name'], parent, path))
        ordered.append(go)
        node_ids[key] = node_ids[pointer_key(transform)] = index
        for pointer in transform.read_typetree().get('m_Children', []):
            child = reader.pointer(transform, pointer)
            child_go = reader.pointer(child, child.read_typetree()['m_GameObject']) if child else None
            if child_go:
                visit(child_go, index, (path + '/' if path else '') + child_go.read_typetree()['m_Name'])
    visit(root, -1, '')
    objects = list(reader.walk(root, include_visual=True, max_nodes=4000, errors=[]))
    game_objects = {pointer_key(obj): obj for obj, tree in objects if obj.type.name == 'GameObject'}
    for go in game_objects.values():
        if pointer_key(go) in seen:
            continue
        ancestor, ancestry = go, set()
        while pointer_key(ancestor) not in ancestry:
            ancestry.add(pointer_key(ancestor))
            transform = transform_for(ancestor)
            if transform is None:
                break
            parent = reader.pointer(transform, transform.read_typetree().get('m_Father', {}))
            parent = reader.pointer(parent, parent.read_typetree()['m_GameObject']) if parent else None
            if parent is None or pointer_key(parent) not in game_objects:
                break
            ancestor = parent
        visit(ancestor, -1, '')
    return source_nodes, node_ids, ordered


def refresh_scale_cache(ex, action_names=None, label='scale', patterns=None):
    action_names=action_names or {'SetScale'}
    def source_object(source):
        env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
        if cab is None:raise ValueError('Source CAB missing: '+source['cab'])
        return cab.objects[int(source['pathid'])]
    records=[];failures=[];updated=0
    with asset_lock(ex.imp.local):
        for path in sorted({path for pattern in (patterns or ('*.native-effect.json',)) for path in ex.folder.glob(pattern)}):
            program=json.loads(path.read_text(encoding='utf-8'))
            selected=[a for m in program['machines'] for s in m['states'] for a in s['actions'] if a['name'] in action_names]
            if not selected:continue
            before=json.dumps(program,sort_keys=True)
            try:
                root,_,_=ex.imp.reader.resolve(program['reference'])
                source_nodes,node_ids,_=source_program_nodes(ex.imp.reader,root)
                if source_nodes!=[(n['name'],n['parent'],n['path']) for n in program['nodes']]:raise ValueError('Source hierarchy changed')
                count=refresh_program_assets(program,source_object,ex.imp.reader,lambda obj:fsm_asset(ex,obj),action_names,node_ids)
                if label=='visibility-delay':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='SetVisibilityRecursiveDelayAction']
                if label=='color-interpolate':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='ColorInterpolate']
                if label in ('audio-volume','audio-volume-extra'):program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='AudioSetVolumeAction']
                if label=='audio-stop':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='AudioStopAction']
                if label=='audio-fade':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='AudioVolumeFadeAction']
                if label=='audio-loop':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name not in action_names]
                if label=='main-shake':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name not in action_names]
                changed=before!=json.dumps(program,sort_keys=True)
                if changed:atomic_json(path,program);updated+=1
                records.append(dict(file=path.name,actions=len(selected),parameters=count,updated=changed,
                    reference=program['reference'],
                    machine_owners=[dict(index=i,node=m.get('owner',-1),known=m.get('owner_known',False),source=m['source']) for i,m in enumerate(program['machines'])] if label in ('actor-owner','portrait-texture','custom-spawn') else [],
                    sources=[m['source'] for m in program['machines'] if any(a['name'] in action_names for s in m['states'] for a in s['actions'])],
                    owner_targets=[dict(action=a['name'],parameter=p['name'],node=p['node']) for a in selected for p in a['params'] if p['kind'] in ((19,20) if label=='custom-spawn' else (20,24) if label=='audio-fade' else (20,))],
                    unresolved=sum(bool(p.get('unresolved')) for a in selected for p in a['params']),
                    none_parameters=sum(bool(p.get('is_none')) for a in selected for p in a['params'])))
            except (KeyError,ValueError,FileNotFoundError) as error:failures.append(dict(file=path.name,error=str(error)))
    atomic_json(PROJECT/('manifests/native-'+label+'-refresh.json'),dict(programs=records,updated=updated,failures=failures,
        notes=['Exact source '+', '.join(sorted(action_names))+' parameters and owner targets only. Unbound target variables remain unsupported.']))
    print('NATIVE '+label.upper()+' REFRESH',len(records),'programs;',updated,'updated;',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('Scale refresh failures remain in audit')


def refresh_asset_cache(ex):
    def source_object(source):
        env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
        if cab is None:raise ValueError('Source CAB missing: '+source['cab'])
        return cab.objects[int(source['pathid'])]
    records=[];failures=[];updated=0
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'))
            if not any(p['kind'] in (32,33) for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params']):continue
            before=json.dumps(program,sort_keys=True)
            try:
                count=refresh_program_assets(program,source_object,ex.imp.reader,lambda obj:fsm_asset(ex,obj))
                changed=before!=json.dumps(program,sort_keys=True)
                if changed:atomic_json(path,program);updated+=1
                params=[p for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params'] if p['kind'] in (32,33)]
                records.append(dict(file=path.name,parameters=count,updated=changed,
                    unresolved=sum(bool(p.get('unresolved')) for p in params),
                    assets=[dict(source=p['asset_source'],path=p.get('asset_path','')) for p in params if 'asset_source' in p]))
            except (KeyError,ValueError,FileNotFoundError) as error:failures.append(dict(file=path.name,error=str(error)))
    atomic_json(PROJECT/'manifests/native-texture-parameter-refresh.json',dict(programs=records,updated=updated,failures=failures,
        notes=['Typed original FSM references. Runtime supports literal Texture2D/null on indexed renderer materials only.',
               'Activation index and geometry are unchanged. RenderTexture/Cubemap bindings remain unresolved.']))
    print('NATIVE ASSET PARAMETER REFRESH',len(records),'programs;',updated,'updated;',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('Asset parameter refresh failures remain in audit')


def refresh_color_cache(ex, action_names=None, label=None):
    action_names=action_names or {'AnimateColor'}
    label=label or ('ease' if action_names=={'EaseFloat','EaseColor'} else 'color')
    def source_object(source):
        env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
        if cab is None:raise ValueError('Source CAB missing: '+source['cab'])
        return cab.objects[int(source['pathid'])]
    records=[];failures=[]
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'))
            selected=[a for m in program['machines'] for s in m['states'] for a in s['actions'] if a['name'] in action_names]
            if not selected:continue
            before=json.dumps(program,sort_keys=True)
            try:
                count=refresh_program_assets(program,source_object,ex.imp.reader,lambda obj:fsm_asset(ex,obj),action_names)
                if label=='rgba':program['unknown_actions']=[name for name in program.get('unknown_actions',[]) if name!='SetColorRGBA']
                changed=before!=json.dumps(program,sort_keys=True)
                if changed:atomic_json(path,program)
                records.append(dict(file=path.name,reference=program['reference'],sources=[m['source'] for m in program['machines'] if any(a['name'] in action_names for s in m['states'] for a in s['actions'])],actions=len(selected),parameters=count,updated=changed,
                    unresolved=sum(bool(p.get('unresolved')) for a in selected for p in a['params'])))
            except (KeyError,ValueError,FileNotFoundError) as error:failures.append(dict(file=path.name,error=str(error)))
    atomic_json(PROJECT/('manifests/native-'+label+'-refresh.json'),dict(programs=records,failures=failures,
        notes=['Exact '+', '.join(sorted(action_names))+' parameters and None semantics. No activation change.']))
    print('NATIVE '+label.upper()+' REFRESH',len(records),'programs;',sum(r['updated'] for r in records),'updated;',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('Color refresh failures remain in audit')


def machines(ex, objects, node_ids):
    result=[]
    for obj,tree in objects:
        # Other behaviours store a PPtr named fsm; only the embedded PlayMaker
        # definition contains startState. The referenced component is walked separately.
        if not tree or 'fsm' not in tree or 'startState' not in tree['fsm']:continue
        fsm=tree['fsm'];variables={v['name']:v for group in fsm.get('variables',{}).values()
                                if isinstance(group,list) for v in group if isinstance(v,dict) and 'name' in v}
        states=[]
        for state in fsm.get('states',[]):
            data=state.get('actionData',{});actions=[]
            for index,name in enumerate(data.get('actionNames',[])):
                if not data.get('actionEnabled',[1]*len(data['actionNames']))[index]:continue
                actions.append(dict(name=name.rsplit('.',1)[-1],params=parameters(data,index,obj,ex.imp.reader,node_ids,variables,fsm.get('dataVersion',0),lambda target:fsm_asset(ex,target))))
            states.append(dict(name=state['name'],actions=actions,
                transitions=[dict(event=t['fsmEvent']['name'],state=t['toState']) for t in state.get('transitions',[])]))
        result.append(dict(**machine_owner(obj,ex.imp.reader,node_ids),start=fsm['startState'],states=states,
            textures=texture_variables(fsm,obj,ex.imp.reader,lambda target:fsm_asset(ex,target)),
            vectors=[dict(name=v['name'],value=v['value']) for v in fsm.get('variables',{}).get('vector3Variables',[])],
            floats=[dict(name=v['name'],value=v['value']) for v in fsm.get('variables',{}).get('floatVariables',[])],
            colors=[dict(name=v['name'],value=v['value']) for v in fsm.get('variables',{}).get('colorVariables',[])],
            events=[dict(event=t['fsmEvent']['name'],state=t['toState']) for t in fsm.get('globalTransitions',[])],
            source=identity(ex,obj)))
    return result


def refresh_program_events(program, source_fsm):
    refreshed=0
    for machine in program['machines']:
        fsm=source_fsm(machine['source'])
        if len(machine['states'])!=len(fsm['states']):raise ValueError('Source FSM state count changed')
        # Some official FSMs contain duplicate state names. Preserve serialized order.
        for state,source_state in zip(machine['states'],fsm['states']):
            if state['name']!=source_state['name']:raise ValueError('Source FSM state sequence changed')
            data=source_state.get('actionData', {})
            actions=[(i,name.rsplit('.',1)[-1]) for i,name in enumerate(data.get('actionNames', []))
                     if data.get('actionEnabled',[1]*len(data['actionNames']))[i]]
            if [a['name'] for a in state['actions']]!=[name for _,name in actions]:
                raise ValueError('Source FSM action sequence changed: '+state['name'])
            for action,(index,_) in zip(state['actions'],actions):
                start=data['actionStartIndex'][index]
                end=data['actionStartIndex'][index+1] if index+1<len(data['actionStartIndex']) else len(data.get('paramName', []))
                if len(action['params'])!=end-start:raise ValueError('Source FSM parameter count changed')
                for param,n in zip(action['params'],range(start,end)):
                    if param['name']!=data['paramName'][n] or param['kind']!=data['paramDataType'][n]:
                        raise ValueError('Source FSM parameter identity changed')
                    if param['kind']!=23:continue
                    param['text']=event_text(data,n,fsm.get('dataVersion',0));param.pop('unresolved',None);refreshed+=1
    actions={a['name'] for m in program['machines'] for s in m['states'] for a in s['actions']}
    program['unknown_actions']=sorted(actions-SUPPORTED_ACTIONS)
    return refreshed


def refresh_event_cache(ex):
    # Exact CAB + PathID avoids a costly texture/mesh re-export and preserves all other data.
    def source_fsm(source):
        env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
        if cab is None:raise ValueError('Source CAB missing: '+source['cab'])
        obj=cab.objects[int(source['pathid'])];tree=obj.read_typetree()
        if not isinstance(tree.get('fsm'),dict) or 'startState' not in tree['fsm']:
            raise ValueError('Source component no longer contains FSM')
        return tree['fsm']
    records=[];failures=[];events=0;updated=0;by_file={}
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'))
            try:
                count=refresh_program_events(program,source_fsm);events+=count
                before=path.read_bytes();atomic_json(path,program);updated+=before!=path.read_bytes()
                by_file[path.name]=program
                records.append(dict(cache='local/runtime/'+path.name,events=count,sources=[m['source'] for m in program['machines']]))
            except (KeyError,ValueError,IndexError,FileNotFoundError) as error:
                failures.append(dict(cache='local/runtime/'+path.name,error=str(error)))
        audit_path=PROJECT/'manifests/native-effect-audit.json'
        audit=json.loads(audit_path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            program=by_file.get(record['cache'].rsplit('/',1)[-1])
            if program is None:continue
            record['unknown_actions']=program['unknown_actions']
            record['unresolved_parameters']=sum(p.get('unresolved',False) for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
        atomic_json(audit_path,audit)
    atomic_json(PROJECT/'manifests/native-event-refresh.json',dict(kind='exact-source-fsm-event-refresh',programs=records,failures=failures,
        events=events,updated=updated,notes=['Only event parameters and supported-action audit changed. No card automatically enabled.']))
    print('NATIVE EVENT REFRESH',len(records),'programs;',events,'events;',updated,'updated;',len(failures),'failures',flush=True)
    if failures:raise RuntimeError('Event refresh failures remain in native-event-refresh.json')


def particle(ex, obj, tree, renderer, errors):
    initial=tree['InitialModule'];shape=tree.get('ShapeModule',{});emission=tree.get('EmissionModule',{})
    result=dict(restored=True,duration=tree['lengthInSec'],speed=tree.get('simulationSpeed',1),loop=tree.get('looping',False),
        space=tree.get('moveWithTransform',0),scaling=tree.get('scalingMode',0),awake=tree.get('playOnAwake',False),
        delay=curve(tree.get('startDelay')),max_particles=initial.get('maxNumParticles',1000),
        size3d=initial.get('size3D',False),rotation3d=initial.get('rotation3D',False),start_color=gradient(initial.get('startColor')))
    for key,name in [('startLifetime','life'),('startSpeed','velocity'),('startSize','size'),('startSizeY','size_y'),
                     ('startSizeZ','size_z'),('startRotation','rotation'),('startRotationX','rotation_x'),
                     ('startRotationY','rotation_y'),('gravityModifier','gravity')]:result[name]=curve(initial.get(key))
    result['emission']=dict(enabled=emission.get('enabled',False),rate=curve(emission.get('rateOverTime')),
        distance=curve(emission.get('rateOverDistance')),bursts=[dict(time=b['time'],count=curve(b['countCurve']),
        cycles=b.get('cycleCount',1),interval=b.get('repeatInterval',.01),probability=b.get('probability',1)) for b in emission.get('m_Bursts',[])])
    result['shape']=dict(enabled=shape.get('enabled',False),kind=shape.get('type',0),radius=shape_radius_value(shape.get('radius',1)),
        angle=shape.get('angle',25),length=shape.get('length',5),position=shape.get('m_Position',dict(x=0,y=0,z=0)),
        rotation=shape.get('m_Rotation',dict(x=0,y=0,z=0)),scale=shape.get('m_Scale',dict(x=1,y=1,z=1)),
        radius_thickness=shape.get('radiusThickness',1),mesh='')
    if shape.get('m_Mesh',{}).get('m_PathID'):
        mesh=dependency(ex,obj,shape['m_Mesh'],errors)
        if mesh:result['shape']['mesh']=ex.mesh(mesh)
    result['modules']=[]
    for module,fields in [('VelocityModule',['x','y','z','orbitalX','orbitalY','orbitalZ','radial']),
        ('ForceModule',['x','y','z']),('SizeModule',['curve','y','z']),('RotationModule',['curve','x','y']),
        ('ClampVelocityModule',['x','y','z','magnitude','drag']),('NoiseModule',['strength','strengthY','strengthZ','scrollSpeed']),
        ('InheritVelocityModule',['m_Curve']),('SizeBySpeedModule',['curve','y','z']),('RotationBySpeedModule',['curve','x','y'])]:
        value=tree.get(module,{})
        if value.get('enabled'):result['modules'].append(dict(name=module,separate=value.get('separateAxes',value.get('separateAxis',False)),
            world=value.get('inWorldSpace',False),dampen=value.get('dampen',0),frequency=value.get('frequency',.5),
            octaves=value.get('octaves',1),mode=value.get('m_Mode',0),range=value.get('range',dict(x=0,y=1)),
            curves=[dict(name=k,value=curve(value[k])) for k in fields if k in value]))
    result['color']=gradient(tree.get('ColorModule',{}).get('gradient')) if tree.get('ColorModule',{}).get('enabled') else None
    result['color_speed']=color_by_speed(tree.get('ColorBySpeedModule'))
    uv=tree.get('UVModule',{});result['uv']=dict(enabled=uv.get('enabled',False),x=uv.get('tilesX',1),y=uv.get('tilesY',1),
        mode=uv.get('animationType',0),row=uv.get('rowIndex',0),cycles=uv.get('cycles',1),frame=curve(uv.get('frameOverTime')),start=curve(uv.get('startFrame')))
    result['render']=dict(mode=renderer.get('m_RenderMode',0),alignment=renderer.get('m_RenderAlignment',0),
        length=renderer.get('m_LengthScale',2),velocity=renderer.get('m_VelocityScale',0),maximum=renderer.get('m_MaxParticleSize',.5),mesh='')
    if renderer.get('m_Mesh',{}).get('m_PathID'):
        mesh=dependency(ex,obj,renderer['m_Mesh'],errors)
        if mesh:result['render']['mesh']=ex.mesh(mesh)
    result['unsupported']=[name for name in ('CollisionModule','TriggerModule','SubModule','LightsModule','TrailModule','CustomDataModule','ExternalForcesModule') if tree.get(name,{}).get('enabled')]
    return result


def shader_properties(parsed):
    names={p['m_Name'] for p in parsed.get('m_PropInfo',{}).get('m_Props',[])}
    for subshader in parsed.get('m_SubShaders',[]):
        for shader_pass in subshader.get('m_Passes',[]):
            # Compiled uniforms such as _MainTex_ST need not be exposed in the
            # inspector Properties block. Saved legacy floats are not uniforms.
            names.update(name for name,index in shader_pass.get('m_NameIndices',[]))
    return sorted(names)


def shader_render_commands(parsed, custom_queue=-1):
    subshader=(parsed.get('m_SubShaders') or [{}])[0]
    tags={k.upper():v for k,v in subshader.get('m_Tags',{}).get('tags',[])}
    queue=tags.get('QUEUE','Geometry')
    match=re.fullmatch(r'(Background|Geometry|AlphaTest|Transparent|Overlay)([+-]\d+)?',queue)
    bases=dict(Background=1000,Geometry=2000,AlphaTest=2450,Transparent=3000,Overlay=4000)
    default=bases[match[1]]+int(match[2] or 0) if match else None
    passes=[]
    for index,shader_pass in enumerate(subshader.get('m_Passes',[])):
        pass_tags=dict(shader_pass.get('m_State',{}).get('m_Tags',{}).get('tags',[]))
        pass_tags.update(dict(shader_pass.get('m_Tags',{}).get('tags',[])))
        mode=next((v for k,v in pass_tags.items() if k.upper()=='LIGHTMODE'),'SRPDefaultUnlit')
        passes.append(dict(index=index,light_mode=mode))
    return dict(render_queue=custom_queue if custom_queue>=0 else default,
                queue_tag=queue,passes=passes)


def material(ex, obj, errors):
    tree=obj.read_typetree();shader=dependency(ex,obj,tree['m_Shader'],errors);parsed=shader.read_typetree().get('m_ParsedForm',{}) if shader else {}
    result=dict(name=tree['m_Name'],shader=parsed.get('m_Name','Builtin/Unrestored'),textures=[],floats=[],colors=[],source=identity(ex,obj),missing_shader=shader is None)
    defaults=parsed.get('m_PropInfo',{}).get('m_Props',[])
    result['properties']=shader_properties(parsed)
    result.update(shader_render_commands(parsed,tree.get('m_CustomRenderQueue',-1)))
    floats={p['m_Name']:p.get('m_DefValue[0]',0) for p in defaults if p['m_Type'] in (2,3)}
    floats.update(dict(tree['m_SavedProperties'].get('m_Floats',[])))
    result['floats']=[dict(name=k,value=v) for k,v in floats.items()]
    colors={p['m_Name']:dict(zip('rgba',(p.get('m_DefValue['+str(i)+']',1) for i in range(4)))) for p in defaults if p['m_Type']==0}
    colors.update(dict(tree['m_SavedProperties'].get('m_Colors',[])))
    result['colors']=[dict(name=k,value=v) for k,v in colors.items()]
    for slot,value in tree['m_SavedProperties'].get('m_TexEnvs',[]):
        pointer=dependency(ex,obj,value['m_Texture'],errors) if value['m_Texture']['m_PathID'] else None
        if pointer and pointer.type.name=='Texture2D':result['textures'].append(dict(name=slot,path=ex.texture(pointer),scale=value['m_Scale'],offset=value['m_Offset']))
    subshaders=parsed.get('m_SubShaders') or [{}]
    passes=subshaders[0].get('m_Passes') or [{}];state=passes[0].get('m_State',{})
    blend=state.get('rtBlend0',{})
    result['keywords']=tree.get('m_ValidKeywords',tree.get('m_ShaderKeywords','').split())
    result.update(blend_commands(blend,floats))
    if result['shader']=='Hero/Particle/Particles_Transparent' or (result['shader']=='FX/CommonFX' and
            set(result['keywords'])=={'_COLORING_NONE','_DISSOLVE_NONE','_SECONDTEXTURETYPE_RGBR','_SECONDTEXTURE_NONE','_TEXTURETYPE_RGBR'}):
        for target,key in [('src','srcBlend'),('dst','destBlend'),('src_alpha','srcBlendAlpha'),('dst_alpha','destBlendAlpha')]:
            name=blend.get(key,{}).get('name','')
            if name and name!='<noninit>':result[target+'_property']=name
    result['compiled_only']=True
    return result


def blend_commands(blend, floats):
    result={}
    for target,key,default in [('src','srcBlend',5),('dst','destBlend',1),('src_alpha','srcBlendAlpha',1),('dst_alpha','destBlendAlpha',10)]:
        command=blend.get(key,{});name=command.get('name','')
        result[target]=int(floats[name] if name in floats else command.get('val',default))
        if name and name!='<noninit>' and name not in floats:
            result.setdefault('unresolved_render_commands',[]).append(name)
    return result


def refresh_commonfx_cache(ex):
    records=[];failures=[];resolved={};updated=0
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'));materials=[m for n in program['nodes'] for m in n['materials'] if m['shader']=='FX/CommonFX']
            if not materials:continue
            try:
                for cached in materials:
                    source=cached['source'];key=(source['cab'],source['pathid'])
                    if key not in resolved:
                        env=ex.imp.reader.load(source['bundle']);cab=env.get_cab(source['cab'].lower())
                        if cab is None:raise ValueError('Missing material CAB')
                        obj=cab.objects[int(source['pathid'])];tree=obj.read_typetree()
                        if obj.type.name!='Material' or tree['m_Name']!=cached['name']:raise ValueError('Material identity changed')
                        shader=ex.imp.reader.pointer(obj,tree['m_Shader']);parsed=shader.read_typetree()['m_ParsedForm']
                        if parsed['m_Name']!='FX/CommonFX':raise ValueError('Shader identity changed')
                        floats={p['m_Name']:p.get('m_DefValue[0]',0) for p in parsed.get('m_PropInfo',{}).get('m_Props',[]) if p['m_Type'] in (2,3)}
                        floats.update(dict(tree['m_SavedProperties'].get('m_Floats',[])))
                        state=parsed['m_SubShaders'][0]['m_Passes'][0]['m_State']
                        resolved[key]=dict(keywords=tree.get('m_ValidKeywords',tree.get('m_ShaderKeywords','').split()),**blend_commands(state.get('rtBlend0',{}),floats))
                    cached.pop('unresolved_render_commands',None);cached.update(resolved[key])
                before=path.read_bytes();atomic_json(path,program);updated+=before!=path.read_bytes()
                records.append(dict(cache='local/runtime/'+path.name,materials=len(materials),sources=[m['source'] for m in materials]))
            except (KeyError,ValueError,IndexError,FileNotFoundError) as error:
                failures.append(dict(cache='local/runtime/'+path.name,error=str(error)))
    atomic_json(PROJECT/'manifests/native-commonfx-refresh.json',dict(programs=records,failures=failures,updated=updated,unique_materials=len(resolved),
        notes=['Exact source keywords and property-backed blend states only. Geometry, textures, clips and enable flags preserved.']))
    print('COMMONFX REFRESH',len(records),'programs;',sum(r['materials'] for r in records),'material occurrences;',len(resolved),'unique materials;',updated,'updated;',len(failures),'failures',flush=True)


def animation_wrap(tree):
    # Default (0) is a real inherited mode, not evidence for Once or Loop.
    return dict(wrap_mode=tree.get('m_WrapMode',0),wrap_known='m_WrapMode' in tree)


def legacy_animation_settings(ex, obj, errors):
    tree=obj.read_typetree()
    default=dependency(ex,obj,tree.get('m_Animation',{}),errors) if tree.get('m_Animation',{}).get('m_PathID') else None
    return dict(**animation_wrap(tree),source=identity(ex,obj),default_clip=ex.key(default) if default else '',
                play_automatically=bool(tree.get('m_PlayAutomatically',False)),
                animate_physics=bool(tree.get('m_AnimatePhysics',False)),culling_type=tree.get('m_CullingType',0))


def animation(ex, obj, paths=None, properties=None):
    tree=obj.read_typetree();result=dict(id=ex.key(obj),name=tree['m_Name'],tracks=[],duration=0,loop=tree.get('m_WrapMode')==2)
    result.update(animation_wrap(tree))
    for key,attribute,components in [('m_PositionCurves','position','xyz'),('m_ScaleCurves','scale','xyz'),('m_RotationCurves','rotation','xyzw'),('m_EulerCurves','euler','xyz')]:
        for entry in tree.get(key,[]):
            for component in components:result['tracks'].append(dict(path=entry['path'],property=attribute+'.'+component,keys=keys(entry['curve'],component)))
    for entry in tree.get('m_FloatCurves',[]):
        result['tracks'].append(dict(path=entry['path'],property=entry['attribute'],keys=keys(entry['curve'])))
    result['duration']=max((k['time'] for track in result['tracks'] for k in track['keys']),default=0)
    result['compressed_unrestored']=len(tree.get('m_CompressedRotationCurves',[]))>0
    result['source']=identity(ex,obj)
    if not tree.get('m_Legacy',True):
        result.update(decode_mecanim(tree,paths or {0:''},properties))
        result['encoding']='mecanim'
    return result


def render_captures(ex, objects, node_ids):
    result=[]
    for obj,tree in objects:
        if not tree or not all(k in tree for k in ('m_ObjectToRender','m_RenderToObject','m_Resolution','m_Width')):continue
        def node(key):return node_ids.get(pointer_key(ex.imp.reader.pointer(obj,tree.get(key,{}))),-1)
        explicit_material=None
        if tree.get('m_CreateRenderPlane') and tree.get('m_RenderMaterial')==0:
            material_object=ex.imp.reader.pointer(obj,tree.get('m_Material',{}))
            if material_object is not None:
                material_errors=[]
                explicit_material=material(ex,material_object,material_errors)
                if material_errors:raise ValueError('RenderToTexture explicit material: '+str(material_errors))
        result.append(dict(owner=node('m_GameObject'),source=node('m_ObjectToRender'),target=node('m_RenderToObject'),
            resolution=tree['m_Resolution'],width=tree['m_Width'],height=tree['m_Height'],near=tree['m_NearClip'],far=tree['m_FarClip'],
            position=tree['m_PositionOffset'],camera_offset=tree['m_CameraOffset'],clear=tree['m_ClearColor'],
            texture_name=tree['m_ShaderTextureName'],uniform_scale=bool(tree['m_UniformWorldScale']),
            source_identity=identity(ex,obj),original_parameters=tree,material=explicit_material,
            hide_source=bool(tree.get('m_HideRenderObject',False))))
    return result


def offscreen_fx(ex, objects, node_ids):
    result=[]
    for obj,tree in objects:
        if not tree or not all(k in tree for k in ('ObjectToRender','RenderResolutionX','RenderResolutionY','AboveClip')):continue
        script=ex.imp.reader.pointer(obj,tree.get('m_Script',{}))
        if script is None or script.read_typetree().get('m_ClassName')!='OffScreenRenderFX':continue
        def node(key):return node_ids.get(pointer_key(ex.imp.reader.pointer(obj,tree.get(key,{}))),-1)
        result.append(dict(owner=node('m_GameObject'),source=node('ObjectToRender'),enabled=bool(tree['m_Enabled']),
            use_bounds=bool(tree['UseBounds']),width=tree['RenderResolutionX'],height=tree['RenderResolutionY'],
            above=tree['AboveClip'],below=tree['BelowClip'],force_size=tree['ForceSize'],rect=tree['CameraRect'],
            source_identity=identity(ex,obj),raw=tree))
    return result


def export_program(ex, reference, suffix='.native-effect.json', include_trails=False,
                   include_component_effects=False, include_camera_shakers=False):
    root,_,_=ex.imp.reader.resolve(reference);errors=[]
    objects=list(ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors))
    nodes=[];node_ids={};seen=set();controllers={};pending_skin=[]
    def hierarchy(go,parent,path):
        key=pointer_key(go)
        if key in seen:return
        seen.add(key);tree=go.read_typetree();components={}
        for item in tree.get('m_Component',[]):
            value=ex.imp.reader.pointer(go,item['component'])
            if value:components[value.type.name]=value
        transform=components.get('Transform')
        if not transform:return
        t=transform.read_typetree();index=len(nodes);node_ids[key]=index;node_ids[pointer_key(transform)]=index
        node=dict(name=tree['m_Name'],path=path,parent=parent,source_layer=source_layer_value(tree),active=bool(tree.get('m_IsActive',1)),
            position=t['m_LocalPosition'] if parent>=0 or suffix=='.actor-spell.json' else dict(x=0,y=0,z=0),rotation=t['m_LocalRotation'],scale=t['m_LocalScale'],materials=[],mesh='',skinned_renderer=False,particle=None,animation_refs=[],animator_states=[])
        nodes.append(node)
        if include_camera_shakers:
            shakers=[]
            for item in tree.get('m_Component',[]):
                component=ex.imp.reader.pointer(go,item['component'])
                if component is None or component.type.name!='MonoBehaviour':continue
                component_tree=component.read_typetree()
                script=ex.imp.reader.pointer(component,component_tree.get('m_Script',{}))
                if script is not None and script.read_typetree().get('m_ClassName')=='CameraShaker':
                    shakers.append((component,component_tree))
            if len(shakers)>1:
                raise ValueError('Multiple CameraShaker components on source node '+path)
            if shakers:
                shaker,values=shakers[0]
                amount=values.get('m_Amount');intensity=values.get('m_IntensityCurve')
                source_keys=(intensity or {}).get('m_Curve',[])
                numeric_fields=('time','value','inSlope','outSlope','inWeight','outWeight')
                if not isinstance(source_keys,list) or any(
                    not isinstance(key,dict) or any(not isinstance(key.get(field), (int,float)) or not math.isfinite(key[field])
                        for field in numeric_fields) for key in source_keys):
                    raise ValueError('CameraShaker source curve is incomplete on '+path)
                curve=keys(intensity or {})
                hold=values.get('m_Hold');hold_at=values.get('m_HoldAtSec');override=values.get('m_IsOverride')
                if (not isinstance(amount,dict) or not all(isinstance(amount.get(axis), (int,float)) and math.isfinite(amount[axis]) for axis in ('x','y','z')) or
                    len(curve)<2 or any(not math.isfinite(key['time']) for key in curve) or curve[-1]['time']<=0 or
                    hold not in (0,1,False,True) or not isinstance(hold_at,(int,float)) or not math.isfinite(hold_at) or
                    override not in (0,1,False,True)):
                    raise ValueError('CameraShaker source parameters are incomplete on '+path)
                node['camera_shaker']=dict(restored=True,amount=amount,curve=curve,hold=bool(hold),
                    hold_at_sec=hold_at,is_override=bool(override),source=identity(ex,shaker))
        animator=components.get('Animation')
        if animator:
            node['legacy_animation']=legacy_animation_settings(ex,animator,errors)
            for ptr in animator.read_typetree().get('m_Animations',[]):
                clip=dependency(ex,animator,ptr,errors)
                if clip:node['animation_refs'].append(ex.key(clip))
        animator=components.get('Animator')
        if animator:
            controller=dependency(ex,animator,animator.read_typetree().get('m_Controller',{}),errors)
            if controller:
                controllers[index]=controller
                node['animator_source']=identity(ex,animator)
        ps=components.get('ParticleSystem');trail=components.get('TrailRenderer') if include_trails else None
        skinned=components.get('SkinnedMeshRenderer')
        renderer=components.get('ParticleSystemRenderer') or skinned or components.get('MeshRenderer') or trail
        if renderer:
            r=renderer.read_typetree();node['visible']=r.get('m_Enabled',True)
            node['sorting_order']=r.get('m_SortingOrder',0)
            for ptr in r.get('m_Materials',[]):
                mat=ex.imp.reader.pointer(renderer,ptr)
                if mat:node['materials'].append(material(ex,mat,errors))
            if trail:
                parameters=r['m_Parameters']
                node['trail']=dict(restored=True,time=r['m_Time'],min_distance=r['m_MinVertexDistance'],emitting=r['m_Emitting'],
                    autodestruct=r['m_Autodestruct'],width=parameters['widthMultiplier'],width_curve=keys(parameters['widthCurve']),
                    gradient=gradient_keys(parameters['colorGradient']),corners=parameters['numCornerVertices'],caps=parameters['numCapVertices'],
                    alignment=parameters['alignment'],texture_mode=parameters['textureMode'],texture_scale=parameters['textureScale'],
                    lighting=parameters['generateLightingData'],shadow_bias=parameters['shadowBias'],
                    cast_shadows=r['m_CastShadows'],receive_shadows=r['m_ReceiveShadows'],raw=r,source=identity(ex,trail))
            if ps:
                node_ids[pointer_key(ps)]=index;node['particle']=particle(ex,ps,ps.read_typetree(),r,errors)
            if skinned and renderer is skinned:
                node['skinned_renderer']=True
                mesh=dependency(ex,skinned,r.get('m_Mesh',{}),errors)
                if mesh:node['mesh']=ex.mesh(mesh)
                pending_skin.append((index,skinned,r))
            else:
                mesh=components.get('MeshFilter')
                if mesh:
                    mesh=dependency(ex,mesh,mesh.read_typetree()['m_Mesh'],errors)
                    if mesh:node['mesh']=ex.mesh(mesh)
        for ptr in t.get('m_Children',[]):
            child=ex.imp.reader.pointer(transform,ptr)
            if child:
                child_go=ex.imp.reader.pointer(child,child.read_typetree()['m_GameObject'])
                if child_go:hierarchy(child_go,index,(path+'/' if path else '')+child_go.read_typetree()['m_Name'])
    hierarchy(root,-1,'')
    # Spell/FSM references can point at detached prefab roots, not Transform children.
    game_objects={pointer_key(o):o for o,t in objects if o.type.name=='GameObject'}
    for go in game_objects.values():
        if pointer_key(go) in seen:continue
        ancestor=go;ancestry=set()
        while pointer_key(ancestor) not in ancestry:
            ancestry.add(pointer_key(ancestor));transform=None
            for component in ancestor.read_typetree().get('m_Component',[]):
                obj=ex.imp.reader.pointer(ancestor,component['component'])
                if obj and obj.type.name=='Transform':transform=obj;break
            if not transform:break
            parent=ex.imp.reader.pointer(transform,transform.read_typetree().get('m_Father',{}))
            parent=ex.imp.reader.pointer(parent,parent.read_typetree()['m_GameObject']) if parent else None
            if not parent or pointer_key(parent) not in game_objects:break
            ancestor=parent
        hierarchy(ancestor,-1,'')
    for index,renderer,tree in pending_skin:
        def bone_index(pointer):
            transform=ex.imp.reader.pointer(renderer,pointer)
            return node_ids.get(pointer_key(transform),-1) if transform else -1
        root_bone=tree.get('m_RootBone',{})
        nodes[index]['skin']=dict(
            bones=[bone_index(pointer) for pointer in tree.get('m_Bones',[])],
            root_bone=bone_index(root_bone) if root_bone.get('m_PathID',0) else -1,
            quality=tree.get('m_Quality',0),
            update_when_offscreen=bool(tree.get('m_UpdateWhenOffscreen',False)),
            source=identity(ex,renderer),
        )
    if include_component_effects:
        for obj, tree in objects:
            if obj.type.name not in ('Light', 'ParticleSystem'):
                continue
            tree = tree or obj.read_typetree()
            go = ex.imp.reader.pointer(obj, tree['m_GameObject'])
            index = node_ids.get(pointer_key(go), -1)
            if index < 0:
                raise ValueError('Source component owner is outside recovered hierarchy')
            if obj.type.name == 'Light':
                nodes[index]['light'] = dict(restored=True, raw=tree, source=identity(ex, obj),
                    enabled=bool(tree['m_Enabled']), kind=tree['m_Type'], color=tree['m_Color'],
                    intensity=tree['m_Intensity'], range=tree['m_Range'], spot=tree['m_SpotAngle'])
                continue
            ps = nodes[index]['particle']
            collision = tree.get('CollisionModule', {})
            if collision.get('enabled'):
                if collision['type'] != 0 or collision['collisionMode'] != 0 or collision['colliderForce'] != 0:
                    raise NotImplementedError('Unreviewed source particle collision branch')
                planes = []
                for pointer in collision['m_Planes']:
                    target = ex.imp.reader.pointer(obj, pointer)
                    plane = node_ids.get(pointer_key(target), -1)
                    if target is not None and plane < 0:
                        raise ValueError('Collision plane is outside source graph')
                    planes.append(plane)
                ps['collision'] = dict(restored=True, raw=collision, planes=planes,
                    dampen=curve(collision['m_Dampen']), bounce=curve(collision['m_Bounce']),
                    lifetime_loss=curve(collision['m_EnergyLossOnCollision']),
                    minimum=collision['minKillSpeed'], maximum=collision['maxKillSpeed'], radius=collision['radiusScale'])
                ps['unsupported'].remove('CollisionModule')
            sub = tree.get('SubModule', {})
            if sub.get('enabled'):
                if any(item['emitter']['m_PathID'] for item in sub['subEmitters']):
                    raise NotImplementedError('Unreviewed nonnull source subemitter')
                ps['empty_sub_emitters'] = True
                ps['unsupported'].remove('SubModule')
    fsm=machines(ex,objects,node_ids)
    animation_objects={ex.key(o):o for o,t in objects if o.type.name=='AnimationClip'}
    for index,controller in controllers.items():
        tree=controller.read_typetree();clip_ids=[]
        for ptr in tree.get('m_AnimationClips',[]):
            clip=dependency(ex,controller,ptr,errors)
            clip_ids.append(ex.key(clip) if clip else '')
            if clip:animation_objects[ex.key(clip)]=clip
        data=controller_states(tree,clip_ids)
        nodes[index]['animator_states']=data['states']
        nodes[index]['animator_unsupported']=data['unsupported_states']
        nodes[index]['controller_source']=identity(ex,controller)
    paths=relative_paths(nodes,controllers.keys())
    properties={}
    for index in controllers:
        for current,node in enumerate(nodes):
            parts=[];ancestor=current
            while ancestor>=0 and ancestor!=index:
                parts.append(nodes[ancestor]['name']);ancestor=nodes[ancestor]['parent']
            if ancestor==index:
                names={name for m in node['materials'] for name in m['properties']}
                properties.setdefault('/'.join(reversed(parts)),set()).update(names)
    anims=[animation(ex,o,paths,properties) for o in animation_objects.values()]
    actions=Counter(a['name'] for machine in fsm for state in machine['states'] for a in state['actions'])
    filename=ex.key(root)+suffix
    captures=render_captures(ex,objects,node_ids)
    program=dict(nodes=nodes,machines=fsm,animations=anims,captures=captures,offscreen_fx=offscreen_fx(ex,objects,node_ids),reference=reference,source=identity(ex,root),
                 unknown_actions=sorted(set(actions)-SUPPORTED_ACTIONS),dependency_errors=errors)
    placement_objects = {pointer_key(obj): obj for obj, tree in objects
                         if obj.type.name == 'GameObject' and pointer_key(obj) in node_ids}
    program['spell_transforms'] = [placement
        for key, obj in sorted(placement_objects.items(), key=lambda pair: node_ids[pair[0]])
        for placement in source_spell_placements(ex.imp.reader, obj, node_ids[key],
                                                 source_bundle(ex, obj.assets_file.name))]
    atomic_json(ex.folder/filename,program)
    return filename,dict(source=identity(ex,root),reference=reference,cache='local/runtime/'+filename,nodes=len(nodes),
        particles=sum(n['particle'] is not None for n in nodes),animations=len(anims),captures=len(captures),actions=dict(actions),
        unresolved_parameters=sum(p.get('unresolved',False) for m in fsm for s in m['states'] for a in s['actions'] for p in a['params']),
        shader_families=sorted({m['shader'] for n in nodes for m in n['materials']}),unknown_actions=program['unknown_actions'],
        unsupported_modules=sorted({m for n in nodes if n['particle'] for m in n['particle']['unsupported']}),errors=errors)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--ids',default='CS2_025,CS2_026,CS2_028,CS2_032,CS2_062,CS1_112,EX1_129,EX1_134')
    parser.add_argument('--all',action='store_true');parser.add_argument('--retry-failed',action='store_true')
    parser.add_argument('--normalize-cache',action='store_true');parser.add_argument('--refresh-event-cache',action='store_true');parser.add_argument('--refresh-commonfx-cache',action='store_true');parser.add_argument('--refresh-asset-cache',action='store_true');parser.add_argument('--refresh-scale-cache',action='store_true');parser.add_argument('--refresh-color-cache',action='store_true')
    parser.add_argument('--refresh-ease-cache',action='store_true');parser.add_argument('--refresh-animation-cache',action='store_true');parser.add_argument('--refresh-itween-cache',action='store_true');parser.add_argument('--refresh-actor-cache',action='store_true');parser.add_argument('--refresh-actor-visibility-cache',action='store_true');parser.add_argument('--refresh-actor-owner-cache',action='store_true');parser.add_argument('--refresh-portrait-texture-cache',action='store_true');parser.add_argument('--refresh-custom-spawn-cache',action='store_true');args=parser.parse_args();ex=RuntimeExport()
    if args.refresh_custom_spawn_cache:
        refresh_scale_cache(ex,{'InitializeCustomSpawn','FinalizeCustomSpawn'},'custom-spawn');return
    if args.refresh_portrait_texture_cache:
        refresh_scale_cache(ex,{'GetCardPortraitTextureAction','SetMaterialTexture'},'portrait-texture');return
    if args.refresh_actor_owner_cache:
        refresh_scale_cache(ex,{'SpellInitActorVariables'},'actor-owner');return
    if args.refresh_actor_visibility_cache:
        refresh_scale_cache(ex,{'ActorSetVisibilityRecursiveAction'},'actor-visibility');return
    if args.refresh_actor_cache:
        refresh_scale_cache(ex,{'SpellGetActorAction','SpellInitActorVariables'},'actor');return
    if args.refresh_itween_cache:
        refresh_scale_cache(ex,{'iTweenScaleTo','iTweenScaleFrom','iTweenFadeToAction'},'itween');return
    if args.refresh_animation_cache:
        refresh_scale_cache(ex,{'AnimationPlaythroughAction','PlayAnimation','StopAnimation','SetAnimationSpeed'},'animation');return
    if args.refresh_ease_cache:
        refresh_color_cache(ex,{'EaseFloat','EaseColor'});return
    if args.refresh_color_cache:
        refresh_color_cache(ex);return
    if args.refresh_scale_cache:
        refresh_scale_cache(ex);return
    if args.refresh_asset_cache:
        refresh_asset_cache(ex);return
    if args.refresh_commonfx_cache:
        refresh_commonfx_cache(ex);return
    if args.refresh_event_cache:
        refresh_event_cache(ex);return
    if args.normalize_cache:
        changed=0
        for filename in ex.folder.glob('*.native-effect.json'):
            program=json.loads(filename.read_text(encoding='utf-8'));dirty=False
            for machine in program['machines']:
                for state in machine['states']:
                    for action in state['actions']:
                        for param in action['params']:
                            if isinstance(param['number'],bool):param['number']=int(param['number']);dirty=True
            for animation_data in program['animations']:
                for track in animation_data['tracks']:
                    component=track['property'].rsplit('.',1)[-1]
                    for key in track['keys']:
                        for weight in ('inWeight','outWeight'):
                            if isinstance(key[weight],dict):key[weight]=key[weight].get(component,1/3);dirty=True
            if dirty:atomic_json(filename,program);changed+=1
        print('NORMALIZED numeric FSM booleans:',changed);return
    path=PROJECT/'manifests/native-effect-audit.json'
    old=json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
    retry={(p['card_id'],p['slot']) for p in old.get('failures',[])}
    audit=json.loads((PROJECT/'manifests/card-presentation-audit.json').read_text(encoding='utf-8'));chosen=set(args.ids.split(','))
    previous=ex.folder/'native-effect-index.json';entries={e['card_id']:e for e in json.loads(previous.read_text(encoding='utf-8'))['entries']} if previous.exists() else {}
    sources=[];resolved={};failures=[]
    for card in audit['cards']:
        if not args.all and not args.retry_failed and card['card_id'] not in chosen:continue
        for item in card['references']:
            slot,ref=item['slot'],item['reference']
            if args.retry_failed and (card['card_id'],slot) not in retry:continue
            if not args.all and not args.retry_failed and slot!='/m_PlayEffectDef':continue
            key=card['card_id'] if slot=='/m_PlayEffectDef' else card['card_id']+'@'+slot.lstrip('/').replace('/',':')
            try:
                if ref not in resolved:resolved[ref]=export_program(ex,ref)
                filename,source=resolved[ref];entries[key]=dict(card_id=key,file=filename,enabled=False)
                sources.append(dict(card_id=card['card_id'],key=key,slot=slot,**source));print(key,'particles',source['particles'],'actions',sum(source['actions'].values()),flush=True)
            except Exception as error:failures.append(dict(card_id=card['card_id'],slot=slot,error=str(error)));print('FAILED',key,str(error),flush=True)
    with asset_lock(ex.imp.local):atomic_json(previous,dict(entries=list(entries.values())))
    ex.save()
    if not args.all:
        attempted={(p['card_id'],p['slot']) for p in sources+failures}
        failures += [p for p in old.get('failures',[]) if (p['card_id'],p['slot']) not in attempted]
    merged={p.get('key',p['card_id']):p for p in old.get('cards',[])};merged.update({p['key']:p for p in sources})
    atomic_json(path,dict(cards=list(merged.values()),failures=failures,notes=['Exported is not verified or complete.','Compiled shaders use a compatibility renderer; original source/scripts are not reconstructed.']))
    print('NATIVE EXPORT',len(sources),'cards;',len(resolved),'unique prefabs;',len(failures),'failures',flush=True)


if __name__=='__main__':main()
