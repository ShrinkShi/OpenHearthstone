"""Decode non-legacy Unity clip data without executing original assemblies.

Stream layout and tangent reconstruction were checked against AssetStudio's
AnimationClip.cs. Unknown bindings remain explicit; no guessed material names.
"""
import math
import struct
import zlib


def unpack(value):
    return value.get('data', value)


def key(time, value, incoming=0, outgoing=0):
    if not math.isfinite(time) or not math.isfinite(value):
        raise ValueError('Non-finite animation sample')
    return dict(time=time, value=value,
                inSlope=incoming if math.isfinite(incoming) else 0,
                outSlope=outgoing if math.isfinite(outgoing) else 0,
                in_step=not math.isfinite(incoming), out_step=not math.isfinite(outgoing),
                inWeight=1/3, outWeight=1/3, weightedMode=0)


def streamed_channels(data, count):
    raw=struct.pack('<'+'I'*len(data), *data)
    offset=0; frames=[]
    while offset<len(raw):
        if offset+8>len(raw):raise ValueError('Truncated streamed frame')
        time, size=struct.unpack_from('<fi', raw, offset);offset+=8
        if size<0 or size>(len(raw)-offset)//20:raise ValueError('Truncated streamed keys')
        frame=[];seen=set()
        for _ in range(size):
            index, *coeff=struct.unpack_from('<i4f', raw, offset);offset+=20
            if index<0 or index>=count or index in seen:raise ValueError('Invalid streamed curve index')
            if not all(math.isfinite(v) for v in coeff):raise ValueError('Non-finite streamed coefficients')
            seen.add(index);frame.append((index,coeff))
        if math.isnan(time):raise ValueError('Invalid streamed time')
        frames.append((time,frame))
    channels=[[] for _ in range(count)]; previous={}
    for time,frame in frames:
        if time==math.inf:continue
        if time<-1e30:
            # Unity's first frame stores pre-roll values, not a key at -FLT_MAX.
            for index,coeff in frame:previous[index]=(0,coeff,None)
            continue
        for index,coeff in frame:
            incoming=0
            if index in previous:
                oldtime,old,oldkey=previous[index]
                if time<oldtime:raise ValueError('Unordered streamed keys')
                if old[0]==old[1]==old[2]==0:
                    incoming=math.inf
                    if oldkey is not None:oldkey['out_step']=True
                else:
                    dx=max(time-oldtime,.0001)
                    incoming=(3*(coeff[3]-old[3])-2*old[2]*dx-old[1]*dx*dx)/dx
                if oldkey is None and time>0:
                    initial=key(0,old[3],0,old[2])
                    initial['out_step']=not math.isfinite(incoming)
                    channels[index].append(initial)
            current=key(time,coeff[3],incoming,coeff[2])
            channels[index].append(current);previous[index]=(time,coeff,current)
    for index,(_,coeff,last) in previous.items():
        if last is None:channels[index].append(key(0,coeff[3]))
    return channels


def channels(tree):
    muscle=tree.get('m_MuscleClip',{})
    clip=unpack(muscle.get('m_Clip',{}))
    stream=clip.get('m_StreamedClip',{})
    result=streamed_channels(stream.get('data',[]),stream.get('curveCount',0))
    dense=clip.get('m_DenseClip',{})
    width=dense.get('m_CurveCount',0);frames=dense.get('m_FrameCount',0)
    samples=dense.get('m_SampleArray',[]);rate=dense.get('m_SampleRate',0)
    if len(samples)!=width*frames or width and rate<=0:raise ValueError('Invalid dense samples')
    for column in range(width):
        values=[key(dense.get('m_BeginTime',0)+row/rate,samples[row*width+column]) for row in range(frames)]
        # Dense frames are uniformly sampled; retain linear interpolation.
        for i,item in enumerate(values):
            if i:item['inSlope']=(item['value']-values[i-1]['value'])*rate
            if i+1<len(values):item['outSlope']=(values[i+1]['value']-item['value'])*rate
        result.append(values)
    stop=muscle.get('m_StopTime',0)
    for value in clip.get('m_ConstantClip',{}).get('data',[]):
        result.append([key(0,value),key(stop,value)] if stop>0 else [key(0,value)])
    return result


def material_property(attribute, names):
    # RendererMaterial stores a CRC28 plus scalar/component flags, not CRC32
    # of the full "material._Name" string (AssetRipper CustomCurveResolver).
    matches={name for name in names if zlib.crc32(name.encode('utf-8'))&0xfffffff==attribute&0xfffffff}
    if len(matches)!=1:return None
    name=next(iter(matches))
    if attribute&0x80000000:return 'material.'+name
    component=('rgba' if attribute&0x40000000 else 'xyzw')[(attribute>>28)&3]
    return 'material.'+name+'.'+component


def decode(tree, paths, properties=None):
    """paths maps CRC32 of paths relative to the Animator to original strings."""
    samples=channels(tree);tracks=[];unresolved=[];cursor=0
    for binding in tree.get('m_ClipBindingConstant',{}).get('genericBindings',[]):
        attribute=binding['attribute'];kind=binding['typeID']
        transform=kind==4 and attribute in (1,2,3,4) and not binding.get('isPPtrCurve')
        width=4 if transform and attribute==2 else 3 if transform else 1
        if cursor+width>len(samples):raise ValueError('Binding exceeds animation channels')
        path=paths.get(binding['path']);reason=None;material=None
        if kind in (23,137) and binding.get('customType')==22 and not binding.get('isPPtrCurve'):
            material=material_property(attribute,(properties or {}).get(path,[]))
        if kind in (23,137) and binding.get('customType')==0 and not binding.get('isPPtrCurve') and attribute==zlib.crc32(b'm_Enabled'):
            material='m_Enabled'
        if path is None:reason='unresolved path hash'
        elif not transform and material is None:reason='unsupported property binding'
        if reason:
            unresolved.append(dict(binding=binding,channel=cursor,width=width,reason=reason))
        elif transform:
            property={1:'position',2:'rotation',3:'scale',4:'euler'}[attribute]
            for axis,values in zip('xyzw',samples[cursor:cursor+width]):
                tracks.append(dict(path=path,property=property+'.'+axis,keys=values))
        else:tracks.append(dict(path=path,property=material,keys=samples[cursor]))
        cursor+=width
    if cursor!=len(samples):raise ValueError('Unbound animation channels')
    return dict(tracks=tracks,unresolved_bindings=unresolved,channel_count=cursor,
                duration=tree.get('m_MuscleClip',{}).get('m_StopTime',0),
                loop=bool(tree.get('m_MuscleClip',{}).get('m_LoopTime',False)))


def relative_paths(nodes, roots):
    candidates={0:{''}}
    for root in roots:
        for i,node in enumerate(nodes):
            parts=[];current=i
            while current>=0 and current!=root:
                parts.append(nodes[current]['name']);current=nodes[current]['parent']
            if current!=root:continue
            path='/'.join(reversed(parts));digest=zlib.crc32(path.encode('utf-8'))
            candidates.setdefault(digest,set()).add(path)
    return {digest:next(iter(names)) for digest,names in candidates.items() if len(names)==1}


def controller_states(tree, clip_ids):
    """Only direct, unmirrored leaf states. Transitions/blend trees stay audited."""
    names=dict(tree.get('m_TOS',[]));controller=tree.get('m_Controller',{})
    machines=controller.get('m_StateMachineArray',[]);states=[];unsupported=[]
    for index,raw_layer in enumerate(controller.get('m_LayerArray',[])):
        layer=unpack(raw_layer);machine=unpack(machines[layer['m_StateMachineIndex']])
        for raw_state in machine.get('m_StateConstantArray',[]):
            state=unpack(raw_state);trees=state.get('m_BlendTreeConstantArray',[])
            leaves=unpack(trees[0]).get('m_NodeArray',[]) if len(trees)==1 else []
            leaf=unpack(leaves[0]) if len(leaves)==1 else {}
            clip=leaf.get('m_ClipID',-1)
            reason=None
            if len(controller.get('m_LayerArray',[]))!=1:reason='multiple layers'
            elif len(leaves)!=1 or leaf.get('m_ChildIndices'):reason='blend tree'
            elif state.get('m_Mirror') or leaf.get('m_Mirror'):reason='mirrored state'
            elif any(state.get(p) for p in ('m_SpeedParamID','m_TimeParamID','m_CycleOffsetParamID','m_MirrorParamID')):reason='parameter-driven state'
            elif not 0<=clip<len(clip_ids) or not clip_ids[clip]:reason='missing clip'
            elif state.get('m_TransitionConstantArray') or machine.get('m_AnyStateTransitionConstantArray'):reason='state transitions'
            if reason:unsupported.append(dict(name=names.get(state.get('m_NameID'),''),reason=reason));continue
            states.append(dict(name=names.get(state['m_NameID'],''),full_name=names.get(state['m_FullPathID'],''),
                layer=names.get(layer['m_Binding'],''),layer_index=index,clip=clip_ids[clip],
                speed=state.get('m_Speed',1),loop=bool(state.get('m_Loop')),
                offset=state.get('m_CycleOffset',0)+leaf.get('m_CycleOffset',0)))
    return dict(states=states,unsupported_states=unsupported)
