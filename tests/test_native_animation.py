import math
import struct

import pytest

from tools.native_animation import streamed_channels, channels, decode, relative_paths, controller_states, material_property


def words(frames):
    raw=b''.join(struct.pack('<fi',time,len(keys))+b''.join(struct.pack('<i4f',index,*coeff) for index,coeff in keys) for time,keys in frames)
    return list(struct.unpack('<'+'I'*(len(raw)//4),raw))


def test_streamed_cubic_tangents_reproduce_polynomial():
    # p(t)=2t^3-3t^2+4t+5; endpoint value=8, derivative=4.
    result=streamed_channels(words([(0,[(0,[2,-3,4,5])]),(1,[(0,[0,0,0,8])]),(math.inf,[])]),1)[0]
    left,right=result
    assert left['outSlope']==4 and right['inSlope']==4
    for t in (.1,.25,.5,.9):
        value=(2*t**3-3*t*t+1)*left['value']+(t**3-2*t*t+t)*left['outSlope']+(-2*t**3+3*t*t)*right['value']+(t**3-t*t)*right['inSlope']
        assert value==pytest.approx(2*t**3-3*t*t+4*t+5)


def test_step_segment_does_not_destroy_next_outgoing_slope():
    values=streamed_channels(words([(0,[(0,[0,0,0,1])]),(1,[(0,[0,0,2,4])]),(2,[(0,[0,0,0,6])])]),1)[0]
    assert values[0]['out_step'] and values[1]['in_step']
    assert not values[1]['out_step'] and values[1]['outSlope']==2
    assert not values[2]['in_step'] and values[2]['inSlope']==2


def test_preroll_and_end_sentinels_never_become_real_key_times():
    values=streamed_channels(words([(-3.402823466e38,[(0,[0,0,0,13])]),(.25,[(0,[0,0,1,10])]),(math.inf,[])]),1)[0]
    assert [k['time'] for k in values]==[0,.25]
    assert values[0]['value']==13 and values[0]['out_step']


@pytest.mark.parametrize('raw,count',[([0],1),(words([(0,[(2,[0,0,0,1])])]),1),(words([(0,[(0,[0,0,0,1]),(0,[0,0,0,2])])]),1)])
def test_invalid_stream_data_is_rejected(raw,count):
    with pytest.raises(ValueError):streamed_channels(raw,count)


def test_dense_constants_and_stop_time_keep_channel_order():
    tree=dict(m_MuscleClip=dict(m_StopTime=2,m_Clip=dict(data=dict(m_StreamedClip=dict(data=[],curveCount=0),
        m_DenseClip=dict(m_CurveCount=2,m_FrameCount=2,m_SampleRate=2,m_BeginTime=.25,m_SampleArray=[1,3,2,5]),m_ConstantClip=dict(data=[7])))))
    result=channels(tree)
    assert [k['value'] for k in result[0]]==[1,2]
    assert [k['time'] for k in result[1]]==[.25,.75]
    assert result[1][0]['outSlope']==4
    assert result[2][1]['time']==2 and result[2][1]['value']==7


def test_transform_binding_width_and_unknown_material_are_audited():
    tree=dict(m_MuscleClip=dict(m_StopTime=1.5,m_Clip=dict(data=dict(m_StreamedClip=dict(data=[],curveCount=0),m_ConstantClip=dict(data=[2,3,4,.5])))),
        m_ClipBindingConstant=dict(genericBindings=[dict(path=12,attribute=3,typeID=4),dict(path=12,attribute=123,typeID=23,customType=22)]))
    result=decode(tree,{12:'mesh'})
    assert result['duration']==1.5 and result['channel_count']==4
    assert [t['property'] for t in result['tracks']]==['scale.x','scale.y','scale.z']
    assert result['unresolved_bindings'][0]['channel']==3
    assert result['unresolved_bindings'][0]['binding']['attribute']==123


@pytest.mark.parametrize('kind,custom,pointer,expected',[(23,0,0,True),(137,0,0,True),(23,22,0,False),(23,0,1,False),(4,0,0,False)])
def test_renderer_enabled_crc_requires_exact_renderer_serialized_binding(kind,custom,pointer,expected):
    tree=dict(m_MuscleClip=dict(m_StopTime=1,m_Clip=dict(data=dict(m_StreamedClip=dict(data=[],curveCount=0),m_ConstantClip=dict(data=[1])))),
              m_ClipBindingConstant=dict(genericBindings=[dict(path=12,attribute=3305885265,typeID=kind,customType=custom,isPPtrCurve=pointer)]))
    result=decode(tree,{12:'mesh'})
    assert bool(result['tracks'])==expected
    if expected:assert result['tracks'][0]['property']=='m_Enabled'


def test_path_hash_is_relative_to_animator_and_verified_against_original():
    nodes=[dict(name='Root',parent=-1),dict(name='MinionRoot',parent=0),dict(name='mesh_DarkBacker',parent=1)]
    assert relative_paths(nodes,[1])[895765657]=='mesh_DarkBacker'
    assert relative_paths(nodes,[1])[0]==''


def test_controller_preserves_original_leaf_and_rejects_blends():
    state=dict(m_NameID=1,m_FullPathID=2,m_Speed=1.25,m_Loop=True,m_CycleOffset=.1,
        m_BlendTreeConstantArray=[dict(data=dict(m_NodeArray=[dict(data=dict(m_ClipID=0,m_ChildIndices=[],m_CycleOffset=.2))]))])
    tree=dict(m_TOS=[(1,'WeaponBoof'),(2,'Base Layer.WeaponBoof'),(3,'Base Layer')],m_Controller=dict(m_LayerArray=[dict(data=dict(m_StateMachineIndex=0,m_Binding=3))],
        m_StateMachineArray=[dict(data=dict(m_StateConstantArray=[dict(data=state)]))]))
    result=controller_states(tree,['original-id'])
    assert result['states'][0]['clip']=='original-id' and result['states'][0]['loop']
    assert result['states'][0]['speed']==1.25 and result['states'][0]['offset']==pytest.approx(.3)
    state['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data']['m_ChildIndices']=[1]
    result=controller_states(tree,['original-id'])
    assert result['states']==[] and result['unsupported_states'][0]['reason']=='blend tree'


def test_original_crc28_material_scalar_and_components():
    import zlib
    assert material_property(2369216915,['_Color','_AlphaMul'])=='material._AlphaMul'
    crc=zlib.crc32(b'_Color')&0xfffffff
    assert material_property(crc|0x70000000,['_Color'])=='material._Color.a'
    assert material_property(crc|0x10000000,['_Color'])=='material._Color.y'
    assert material_property(2369216915,['_Color']) is None
