import math
import struct
import hashlib
import pytest

from tools.native_effects import curve, gradient, keys, parameters, machines, render_captures, shader_properties, shader_render_commands, refresh_program_events, blend_commands
from tools.verify_native_effects import verify


@pytest.mark.parametrize('cab,pathid,expected', [('cab',12,3),('other-cab',12,-1),('cab',13,-1)])
def test_machine_owner_uses_exact_cab_and_pathid(cab,pathid,expected):
    from tools.native_effects import machine_owner
    class Owner:
        def read_typetree(self):return {'m_GameObject': {'m_PathID':pathid}}
    class Target:
        assets_file=type('Cab',(),{'name':cab})()
        path_id=pathid
    class Reader:
        def pointer(self,owner,pointer):return Target()
    assert machine_owner(Owner(),Reader(),{('cab',12):3})=={'owner':expected,'owner_known':expected>=0}


def test_missing_machine_owner_does_not_default_to_root():
    from tools.native_effects import machine_owner
    class Owner:
        def read_typetree(self):return {}
    class Reader:
        def pointer(self,owner,pointer):return None
    assert machine_owner(Owner(),Reader(),{None:0})=={'owner':-1,'owner_known':False}


@pytest.mark.parametrize('mode', [0,1,2,4,8,99])
def test_source_animation_wrap_preserves_default_pingpong_clamp_and_unknown(mode):
    from tools.native_effects import animation_wrap
    assert animation_wrap({'m_WrapMode':mode})=={'wrap_mode':mode,'wrap_known':True}


def test_absent_animation_wrap_is_not_claimed_as_known_default():
    from tools.native_effects import animation_wrap
    assert animation_wrap({})=={'wrap_mode':0,'wrap_known':False}


def test_clip_export_retains_pingpong_even_though_legacy_loop_boolean_is_false(monkeypatch):
    import tools.native_effects as effects
    monkeypatch.setattr(effects,'identity',lambda ex,obj: {'pathid':'42'})
    class Clip:
        def read_typetree(self):return {'m_Name':'Original','m_Legacy':True,'m_WrapMode':4}
    class Export:
        def key(self,obj):return 'original-id'
    result=effects.animation(Export(),Clip())
    assert result['wrap_known'] and result['wrap_mode']==4 and not result['loop']
    assert result['source']=={'pathid':'42'}


def test_animation_component_export_retains_override_and_exact_default_clip(monkeypatch):
    import tools.native_effects as effects
    default=object()
    monkeypatch.setattr(effects,'identity',lambda ex,obj: {'pathid':'44'})
    monkeypatch.setattr(effects,'dependency',lambda ex,obj,pointer,errors: default if pointer['m_PathID']==45 else None)
    class Component:
        def read_typetree(self):
            return {'m_WrapMode':8,'m_Animation':{'m_PathID':45},'m_PlayAutomatically':True,
                    'm_AnimatePhysics':True,'m_CullingType':2}
    class Export:
        def key(self,obj):
            assert obj is default
            return 'exact-default-id'
    result=effects.legacy_animation_settings(Export(),Component(),[])
    assert result=={'wrap_mode':8,'wrap_known':True,'source':{'pathid':'44'},
                    'default_clip':'exact-default-id','play_automatically':True,
                    'animate_physics':True,'culling_type':2}


@pytest.mark.parametrize('explicit_id', [0, 37])
def test_owner_default_does_not_get_overwritten_by_unused_explicit_pointer(explicit_id):
    class Owner:
        def read_typetree(self):
            return {'m_GameObject': {'m_PathID': 12}}
    class Target:
        assets_file=type('Cab', (), {'name': 'cab'})()
        path_id=12
    class Reader:
        def pointer(self, owner, pointer):
            assert pointer['m_PathID']==12
            return Target()
    data={'actionStartIndex':[0], 'paramName':['gameObject'], 'paramDataType':[20],
          'paramDataPos':[0], 'byteData':[], 'fsmOwnerDefaultParams':[
              {'ownerOption':0, 'gameObject':{'useVariable':False, 'value':{'m_PathID':explicit_id}}}]}
    result=parameters(data,0,Owner(),Reader(),{('cab',12):3},{})[0]
    assert result['node']==3 and not result.get('unresolved')


def test_native_two_curves_and_constant_distribution_keep_both_bounds():
    value=curve(dict(minMaxState=2,scalar=3,minScalar=1,maxCurve=dict(m_Curve=[dict(time=0,value=2)]),minCurve=dict(m_Curve=[dict(time=0,value=1)])))
    assert value['mode']==2 and value['minimum']==1 and value['maximum'][0]['value']==2
    assert value['minimum_keys'][0]['value']==1


@pytest.mark.parametrize('radius,expected',[
    (.31,.31),
    ({'value':.17,'mode':0,'spread':0,'speed':{
        'minMaxState':0,'scalar':1,'minScalar':1,
        'maxCurve':{'m_Curve':[],'m_PreInfinity':2,'m_PostInfinity':2,'m_RotationOrder':4},
        'minCurve':{'m_Curve':[],'m_PreInfinity':2,'m_PostInfinity':2,'m_RotationOrder':4}}},.17),
])
def test_particle_shape_radius_exports_legacy_float_and_runtime_minmax_object(radius,expected):
    from tools.native_effects import particle
    tree={'lengthInSec':1,'InitialModule':{},'ShapeModule':{'enabled':True,'type':2,'radius':radius}}
    result=particle(None,None,tree,{},[])
    assert result['shape']['radius']==expected


def test_game_object_source_layer_is_preserved_and_range_checked():
    from tools.native_effects import source_layer_value
    assert source_layer_value({'m_Layer': 21}) == 21
    assert source_layer_value({'m_Layer': 0}) == 0
    assert source_layer_value({}) == 0
    for layer in (True, -1, 32, 1.5):
        with pytest.raises(ValueError, match='m_Layer'):
            source_layer_value({'m_Layer': layer})


def test_particle_shape_radius_rejects_unrecognized_object_schema():
    from tools.native_effects import shape_radius_value
    with pytest.raises(ValueError,match='ShapeModule.radius'):
        shape_radius_value({'mode':0,'spread':0})


@pytest.mark.parametrize('radius',[
    {'value':.17,'mode':1,'spread':0},
    {'value':.17,'mode':0,'spread':.1},
    {'value':.17,'mode':0,'spread':0,'speed':{'minMaxState':1,'scalar':1,'minScalar':.5}},
    {'value':.17,'mode':0,'spread':0,'speed':{'scalar':1,'maxCurve':{'m_Curve':[{'time':0,'value':2}]}}},
])
def test_particle_shape_radius_refuses_lossy_minmax_modes(radius):
    from tools.native_effects import shape_radius_value
    with pytest.raises(ValueError,match='ShapeModule.radius'):
        shape_radius_value(radius)


def test_named_original_blend_state_reads_material_not_zero_placeholder():
    result=blend_commands(dict(srcBlend=dict(val=0,name='_SrcBlendOutline'),destBlend=dict(val=0,name='_DstBlendOutline')),
                          dict(_SrcBlendOutline=1,_DstBlendOutline=10))
    assert result['src']==1 and result['dst']==10
    assert 'unresolved_render_commands' not in result


def test_missing_original_blend_property_is_audited():
    result=blend_commands(dict(srcBlend=dict(val=0,name='_Missing')), {})
    assert result['unresolved_render_commands']==['_Missing']


def test_discontinuous_animation_is_finite_json_with_step_flag():
    value=keys(dict(m_Curve=[dict(time=0,value=1,inSlope=math.inf,outSlope=math.inf)]))[0]
    assert value['step'] and math.isfinite(value['inSlope']) and math.isfinite(value['outSlope'])


def test_native_gradients_keep_random_range_and_alpha_times():
    value=gradient(dict(minMaxState=3,minColor=dict(r=.1,g=.2,b=.3,a=.4),maxColor=dict(r=1,g=1,b=1,a=1),maxGradient=dict(m_NumColorKeys=1,m_NumAlphaKeys=1,key0=dict(r=.1,g=.2,b=.3,a=.4),ctime0=10000,atime0=30000)))
    assert value['minimum']['a']==.4 and value['high']['alpha'][0]['time']==30000/65535
    assert value['high']['colors'][0]['time']!=value['high']['alpha'][0]['time']


def test_runtime_mesh_export_preserves_skin_weights_indices_and_bindposes():
    from tools.export_runtime import mesh_data

    class Handler:
        m_Vertices=[(0, 1, 2), (3, 4, 5)]
        m_UV0=[(0, 0), (1, 1)]
        m_Normals=[]
        m_Colors=[]
        m_BoneIndices=[(0, 1), (1, 2)]
        m_BoneWeights=[(0.75, 0.25), (0.5, 0.5)]
        def get_triangles(self):return [[(0, 1, 0)]]

    matrix={f'e{row}{column}':float(row*4+column) for row in range(4) for column in range(4)}
    result=mesh_data(Handler(), {'m_BindPose':[matrix]})
    assert result['bone_indices']==[0, 1, 0, 0, 1, 2, 0, 0]
    assert result['bone_weights']==[0.75, 0.25, 0, 0, 0.5, 0.5, 0, 0]
    assert result['bindposes']==[float(i) for i in range(16)]


def test_runtime_mesh_export_rejects_unknown_bindpose_layout():
    from tools.export_runtime import mesh_data

    class Handler:
        m_Vertices=[];m_UV0=[];m_Normals=[];m_Colors=[];m_BoneIndices=None;m_BoneWeights=None
        def get_triangles(self):return []

    with pytest.raises(ValueError, match='bindpose'):
        mesh_data(Handler(), {'m_BindPose':[{'unexpected':1}]})


def test_legacy_packed_fsm_values_do_not_use_empty_new_arrays():
    data=dict(actionStartIndex=[0],paramName=['visible','speed'],paramDataType=[17,15],
              paramDataPos=[0,2],paramByteDataSize=[2,5],byteData=list(b'\x01\x00'+struct.pack('<f',.6)+b'\x00'))
    result=parameters(data,0,None,None,{}, {})
    assert type(result[0]['number']) is int and result[0]['number']==1 and abs(result[1]['number']-.6)<1e-6
    assert not any(p.get('unresolved') for p in result)


def test_new_fsm_variables_use_exact_source_defaults_and_retain_name():
    data=dict(actionStartIndex=[0],paramName=['floatValue'],paramDataType=[15],paramDataPos=[0],
              paramByteDataSize=[0],byteData=[],fsmFloatParams=[dict(useVariable=True,name='Intensity',value=0)])
    result=parameters(data,0,None,None,{},dict(Intensity=dict(value=2.5)))
    assert result[0]['variable']=='Intensity' and result[0]['number']==2.5


def test_unknown_serialization_is_recorded_instead_of_guessing():
    data=dict(actionStartIndex=[0],paramName=['custom'],paramDataType=[999],paramDataPos=[0],byteData=[])
    assert parameters(data,0,None,None,{}, {})[0]['unresolved']


def test_color_animation_curves_retain_serialized_infinity_not_wrapmode_enum():
    data=dict(actionStartIndex=[0],paramName=['curveR'],paramDataType=[22],paramDataPos=[0],byteData=[],
              animationCurveParams=[dict(curve=dict(m_Curve=[dict(time=0,value=1)],m_PreInfinity=2,m_PostInfinity=1))])
    result=parameters(data,0,None,None,{}, {})[0]
    assert result['wrap_known'] and result['pre_wrap']==2 and result['post_wrap']==1


def test_original_packed_color_recovers_named_rgba_default():
    raw=struct.pack('<ffff',0,0,0,0)+b'\x01Glow'
    data=dict(actionStartIndex=[0],paramName=['colorVariable'],paramDataType=[25],paramDataPos=[0],paramByteDataSize=[len(raw)],byteData=list(raw))
    result=parameters(data,0,None,None,{},dict(Glow=dict(value=dict(r=2,g=3,b=4,a=.5))))[0]
    assert result['variable']=='Glow' and result['color']['r']==2 and not result['is_none']


@pytest.mark.parametrize('use_variable,name,is_none',[(True,'',True),(False,'',False),(True,'Scale',False)])
def test_original_named_variable_none_is_distinct_from_literal_zero(use_variable,name,is_none):
    data=dict(actionStartIndex=[0],paramName=['x'],paramDataType=[15],paramDataPos=[0],
              paramByteDataSize=[0],byteData=[],fsmFloatParams=[dict(useVariable=use_variable,name=name,value=0)])
    result=parameters(data,0,None,None,{},dict(Scale=dict(value=2)))[0]
    assert result['none_known'] and result['is_none']==is_none
    assert result['number']==(2 if name=='Scale' else 0)


def test_original_vector_none_preserves_marker_instead_of_becoming_zero_scale():
    data=dict(actionStartIndex=[0],paramName=['vector'],paramDataType=[28],paramDataPos=[0],
              byteData=[],fsmVector3Params=[dict(useVariable=True,name='',value=dict(x=0,y=0,z=0))])
    result=parameters(data,0,None,None,{}, {})[0]
    assert result['none_known'] and result['is_none']


def test_original_packed_vector_none_and_named_utf8_variable():
    raw=struct.pack('<fff',0,0,0)+b'\x01'
    data=dict(actionStartIndex=[0],paramName=['vector'],paramDataType=[28],paramDataPos=[0],
              paramByteDataSize=[len(raw)],byteData=list(raw))
    result=parameters(data,0,None,None,{}, {})[0]
    assert result['none_known'] and result['is_none'] and not result.get('unresolved')
    data['byteData']+=list('缩放'.encode('utf-8'));data['paramByteDataSize']=[len(data['byteData'])]
    result=parameters(data,0,None,None,{}, {'缩放':dict(value=dict(x=2,y=3,z=4))})[0]
    assert result['variable']=='缩放' and not result['is_none'] and result['vector']['z']==4


def test_original_packed_named_variable_cannot_read_next_parameter_bytes():
    raw=struct.pack('<f',1)+b'\x00'+b'next'
    data=dict(actionStartIndex=[0],paramName=['x'],paramDataType=[15],paramDataPos=[0],paramByteDataSize=[4],byteData=list(raw))
    assert parameters(data,0,None,None,{}, {})[0]['unresolved']
    data['paramByteDataSize']=[5]
    result=parameters(data,0,None,None,{}, {})[0]
    assert result['number']==1 and result['variable']=='' and not result.get('unresolved')


def typed_asset_fixture(kind, pointer):
    return dict(actionStartIndex=[0],paramName=['texture'],paramDataType=[kind],
                paramDataPos=[0],byteData=[],fsmObjectParams=[dict(useVariable=False,value=pointer)])


@pytest.mark.parametrize('pathid',[0,45])
def test_generic_fsm_object_keeps_null_distinct_from_external_sounddef(pathid):
    from types import SimpleNamespace
    target=SimpleNamespace(type=SimpleNamespace(name='MonoBehaviour'),path_id=45,
                           assets_file=SimpleNamespace(name='sound-cab'),
                           read_typetree=lambda:dict(m_Script={'m_PathID':45},m_ClassName='SoundDef'))
    reader=SimpleNamespace(pointer=lambda owner,pointer:target if pointer['m_PathID'] else None)
    result=parameters(typed_asset_fixture(24,dict(m_FileID=0,m_PathID=pathid)),0,None,reader,{}, {})[0]
    assert result['object_null']==(pathid==0)
    assert result['node']==-1
    if pathid:assert result['unresolved'] and result['asset_type']=='MonoBehaviour'
    else:assert not result.get('unresolved')


def test_original_sounddef_and_fsm_float_arrays_decode_contiguous_members():
    from types import SimpleNamespace

    script=SimpleNamespace(type=SimpleNamespace(name='MonoScript'),read_typetree=lambda:dict(m_ClassName='SoundDef'))
    sounds={path:SimpleNamespace(type=SimpleNamespace(name='MonoBehaviour'),path_id=path,
        assets_file=SimpleNamespace(name='sounds-cab'),
        read_typetree=lambda:dict(m_Script=dict(m_PathID=999))) for path in (101,102,103)}
    def pointer(owner,value):
        if value['m_PathID']==999:return script
        return sounds.get(value['m_PathID'])
    reader=SimpleNamespace(pointer=pointer)
    data=dict(actionStartIndex=[0],
        paramName=['m_Sounds','','','','m_Weights','','','m_MinVolume'],
        paramDataType=[12,5,5,5,12,15,15,15],paramDataPos=[0,0,1,2,1,0,1,2],
        arrayParamSizes=[3,3],arrayParamTypes=['SoundDef','HutongGames.PlayMaker.FsmFloat'],
        unityObjectParams=[dict(m_FileID=0,m_PathID=path) for path in (101,102,103)],
        fsmFloatParams=[dict(useVariable=False,value=value) for value in (1.0,.5,.25)],
        paramByteDataSize=[0]*8,byteData=[])
    result=parameters(data,0,None,reader,{}, {},export_asset=lambda obj:dict(
        asset_source=dict(cab=obj.assets_file.name,pathid=str(obj.path_id),type='MonoBehaviour'),
        asset_path='sound-'+str(obj.path_id)+'.native-sound.json'))
    assert result[0]['array_known'] and result[0]['array_type']=='SoundDef' and result[0]['array_count']==3
    assert [p['asset_source']['pathid'] for p in result[1:4]]==['101','102','103']
    assert [p['asset_path'] for p in result[1:4]]==['sound-101.native-sound.json','sound-102.native-sound.json','sound-103.native-sound.json']
    assert result[4]['array_known'] and result[4]['array_type']=='HutongGames.PlayMaker.FsmFloat'
    assert [p['number'] for p in result[5:8]]==[1.0,.5,.25]
    assert not any(p.get('unresolved') for p in result)


def test_playmaker_string_array_decodes_v1_byte_data_and_v2_string_table():
    v1=dict(actionStartIndex=[0],paramName=['m_CardIds',''],paramDataType=[12,3],
        paramDataPos=[0,0],paramByteDataSize=[0,7],arrayParamSizes=[1],
        arrayParamTypes=['System.String'],stringParams=[],byteData=list(b'ONY_008'))
    result=parameters(v1,0,None,None,{}, {},data_version=1)
    assert result[0]['array_known'] and result[0]['array_type']=='System.String' and result[0]['array_count']==1
    assert result[1]['text']=='ONY_008' and not any(param.get('unresolved') for param in result)

    v2=dict(actionStartIndex=[0],paramName=['m_CardIds',''],paramDataType=[12,3],
        paramDataPos=[0,0],paramByteDataSize=[0,0],arrayParamSizes=[1],
        arrayParamTypes=['System.String'],stringParams=['ONY_008'],byteData=[])
    result=parameters(v2,0,None,None,{}, {},data_version=2)
    assert result[0]['array_known'] and result[1]['text']=='ONY_008'
    assert not any(param.get('unresolved') for param in result)


def test_playmaker_v1_string_array_keeps_truncated_member_unresolved():
    data=dict(actionStartIndex=[0],paramName=['m_CardIds',''],paramDataType=[12,3],
        paramDataPos=[0,0],paramByteDataSize=[0,7],arrayParamSizes=[1],
        arrayParamTypes=['System.String'],stringParams=[],byteData=list(b'ONY'))
    result=parameters(data,0,None,None,{}, {},data_version=1)
    assert result[0]['array_known'] and result[0]['array_count']==1
    assert result[1]['unresolved']


def test_original_array_with_wrong_element_type_or_member_span_stays_unresolved():
    data=dict(actionStartIndex=[0],paramName=['sounds','',''],paramDataType=[12,15,15],
              paramDataPos=[0,0,1],arrayParamSizes=[2],arrayParamTypes=['SoundDef'],
              fsmFloatParams=[dict(useVariable=False,value=1),dict(useVariable=False,value=1)],
              paramByteDataSize=[0,0,0],byteData=[])
    assert parameters(data,0,None,None,{}, {})[0]['unresolved']
    data['paramDataType']=[12,5,15]
    assert parameters(data,0,None,None,{}, {})[0]['unresolved']


@pytest.mark.parametrize('kind',[32,33])
def test_typed_fsm_null_asset_is_not_an_unresolved_reference(kind):
    result=parameters(typed_asset_fixture(kind,dict(m_FileID=0,m_PathID=0)),0,None,None,{}, {})[0]
    assert result['object_null'] and not result.get('unresolved')
    assert result['asset_type']==('Material' if kind==32 else 'Texture')


@pytest.mark.parametrize('kind,type_name',[(32,'Material'),(33,'Texture2D')])
def test_typed_fsm_assets_use_object_table_and_preserve_exact_identity(kind,type_name):
    from types import SimpleNamespace
    target=SimpleNamespace(type=SimpleNamespace(name=type_name))
    class Reader:
        def pointer(self,owner,pointer):
            assert pointer==dict(m_FileID=52,m_PathID=-19)
            return target
    result=parameters(typed_asset_fixture(kind,dict(m_FileID=52,m_PathID=-19)),0,None,Reader(),{}, {},
        export_asset=lambda obj:dict(asset_source=dict(cab='original',pathid='-19',type=obj.type.name)))[0]
    assert result['asset_source']['pathid']=='-19' and not result['object_null']
    assert not result.get('unresolved')


def test_typed_fsm_missing_exporter_and_wrong_object_type_remain_unresolved():
    from types import SimpleNamespace
    class Reader:
        def pointer(self,owner,pointer):return SimpleNamespace(type=SimpleNamespace(name='Texture2D'))
    data=typed_asset_fixture(33,dict(m_FileID=1,m_PathID=10))
    assert parameters(data,0,None,Reader(),{}, {})[0]['unresolved']
    data['paramDataType']=[32]
    assert parameters(data,0,None,Reader(),{}, {},export_asset=lambda obj:{})[0]['unresolved']


def test_fsm_event_v2_uses_string_table_and_preserves_null_event():
    data=dict(actionStartIndex=[0],paramName=['minion','none','invalid'],paramDataType=[23]*3,
              paramDataPos=[0,1,-1],byteData=[255],stringParams=['Minion',''])
    result=parameters(data,0,None,None,{}, {},2)
    assert [p['text'] for p in result]==['Minion','','']
    assert not any(p.get('unresolved') for p in result)


def test_fsm_event_v1_reads_exact_utf8_byte_range():
    raw='x触发Minion'.encode('utf-8')
    data=dict(actionStartIndex=[0],paramName=['event'],paramDataType=[23],paramDataPos=[1],
              paramByteDataSize=[len(raw)-1],byteData=list(raw),stringParams=['wrong'])
    result=parameters(data,0,None,None,{}, {},1)[0]
    assert result['text']=='触发Minion' and not result.get('unresolved')


def test_fsm_event_v1_truncated_or_invalid_utf8_is_audited():
    data=dict(actionStartIndex=[0],paramName=['event'],paramDataType=[23],paramDataPos=[0],
              paramByteDataSize=[3],byteData=[255])
    assert parameters(data,0,None,None,{}, {},1)[0]['unresolved']
    data['paramByteDataSize']=[1]
    assert parameters(data,0,None,None,{}, {},1)[0]['unresolved']


def event_refresh_fixture():
    data=dict(actionNames=['Other.Disabled','Other.SpellCardTypeAction'],actionEnabled=[False,True],
              actionStartIndex=[0,0],paramName=['m_MinionEvent','m_WhichCard'],paramDataType=[23,7],
              paramDataPos=[0,0],stringParams=['Minion'])
    fsm=dict(dataVersion=2,states=[dict(name='Read',actionData=data)])
    source=dict(bundle='source',cab='cab',pathid='17')
    program=dict(nodes=[dict(mesh='keep.mesh')],unknown_actions=['SpellCardTypeAction'],machines=[dict(source=source,states=[dict(name='Read',
        actions=[dict(name='SpellCardTypeAction',params=[dict(name='m_MinionEvent',kind=23,text='',node=-1,unresolved=True),dict(name='m_WhichCard',kind=7,number=1,node=8)])])])])
    return program,fsm,source


def test_refresh_events_preserves_non_event_nodes_and_disabled_action_order():
    program,fsm,source=event_refresh_fixture()
    visited=[]
    def load(identity):
        visited.append(identity)
        return fsm
    assert refresh_program_events(program,load)==1 and visited==[source]
    params=program['machines'][0]['states'][0]['actions'][0]['params']
    assert params[0]['text']=='Minion' and 'unresolved' not in params[0]
    assert params[1]==dict(name='m_WhichCard',kind=7,number=1,node=8)
    assert program['nodes']==[dict(mesh='keep.mesh')] and program['unknown_actions']==[]


def test_refresh_events_rejects_source_action_drift():
    program,fsm,_=event_refresh_fixture()
    fsm['states'][0]['actionData']['actionNames'][1]='Other.DifferentAction'
    with pytest.raises(ValueError,match='sequence changed'):refresh_program_events(program,lambda _:fsm)


def test_refresh_events_rejects_source_parameter_drift():
    program,fsm,_=event_refresh_fixture()
    fsm['states'][0]['actionData']['paramDataType'][0]=3
    with pytest.raises(ValueError,match='identity changed'):refresh_program_events(program,lambda _:fsm)


def test_refresh_events_keeps_duplicate_named_states_in_source_order():
    import copy
    program,fsm,_=event_refresh_fixture()
    fsm['states'].append(copy.deepcopy(fsm['states'][0]))
    fsm['states'][1]['actionData']['stringParams']=['Hero']
    program['machines'][0]['states'].append(copy.deepcopy(program['machines'][0]['states'][0]))
    assert refresh_program_events(program,lambda _:fsm)==2
    assert [s['actions'][0]['params'][0]['text'] for s in program['machines'][0]['states']]==['Minion','Hero']


def test_fsm_animation_curve_wrapper_preserves_seconds_and_tangents():
    data=dict(actionStartIndex=[0],paramName=['animCurve'],paramDataType=[22],paramDataPos=[0],byteData=[],
              animationCurveParams=[dict(curve=dict(m_Curve=[dict(time=.7,value=1,outSlope=2)]))])
    result=parameters(data,0,None,None,{}, {})[0]
    assert result['curve'][0]['time']==.7 and result['curve'][0]['outSlope']==2


def test_native_activation_rejects_blank_frames_and_stale_exports(tmp_path):
    path=tmp_path/'effect.json';path.write_bytes(b'{}')
    entry=dict(card_id='test',file=path.name,enabled=False)
    capture=dict(visible_samples=1,max_pixels=500,program_sha256='',review_duration=1)
    with pytest.raises(ValueError,match='visible'):verify(entry,capture,tmp_path)
    capture['visible_samples']=2
    with pytest.raises(ValueError,match='changed'):verify(entry,capture,tmp_path)
    capture['program_sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    result=verify(entry,capture,tmp_path)
    assert result['enabled'] and not result['replace'] and result['duration']==1


def test_component_fsm_reference_is_not_an_embedded_state_machine():
    assert machines(None,[(None,dict(fsm=dict(m_FileID=0,m_PathID=17)))],{})==[]


@pytest.mark.parametrize('delay',[float('nan'),float('inf'),-.5,2])
def test_native_activation_rejects_invalid_reviewed_finish_delay(tmp_path,delay):
    path=tmp_path/'effect.json';path.write_bytes(b'{}')
    entry=dict(card_id='test',file=path.name,enabled=False)
    capture=dict(visible_samples=2,max_pixels=500,program_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),review_duration=1,impact_delay=delay)
    with pytest.raises(ValueError,match='impact delay'):verify(entry,capture,tmp_path)


def test_native_activation_preserves_independent_finish_delay(tmp_path):
    path=tmp_path/'effect.json';path.write_bytes(b'{}')
    entry=dict(card_id='test',file=path.name,enabled=False)
    capture=dict(visible_samples=2,max_pixels=500,program_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),review_duration=2.1,impact_delay=.35)
    result=verify(entry,capture,tmp_path)
    assert result['duration']==2.1 and result['impact_delay']==.35


def test_vector_clip_weights_are_exported_per_component():
    result=keys(dict(m_Curve=[dict(time=0,value=dict(x=1,y=2,z=3),inWeight=dict(x=.1,y=.2,z=.3))]),'y')[0]
    assert result['value']==2 and result['inWeight']==.2 and result['outWeight']==1/3


def test_capture_mapping_uses_full_pointer_identity_and_preserves_signed_clip():
    from types import SimpleNamespace
    owner=SimpleNamespace(assets_file=SimpleNamespace(name='cab-a'),path_id=7,type=SimpleNamespace(name='MonoBehaviour'))
    source=SimpleNamespace(assets_file=SimpleNamespace(name='cab-b'),path_id=7)
    reader=SimpleNamespace(pointer=lambda obj, ptr: source if ptr.get('m_PathID') else None,
                           file_bundle={})
    db=SimpleNamespace(execute=lambda *args: SimpleNamespace(fetchone=lambda: None))
    ex=SimpleNamespace(imp=SimpleNamespace(reader=reader,store=SimpleNamespace(db=db)))
    tree=dict(m_ObjectToRender=dict(m_PathID=7),m_RenderToObject={},m_GameObject={},
              m_Resolution=350,m_Width=13.96,m_Height=7.33,m_NearClip=-.54,m_FarClip=1.84,
              m_PositionOffset={},m_CameraOffset={},m_ClearColor={},m_ShaderTextureName='',m_UniformWorldScale=0)
    result=render_captures(ex,[(owner,tree)],{('cab-a',7):2,('cab-b',7):9})[0]
    assert result['source']==9 and result['target']==-1
    assert result['near']==-.54 and result['far']==1.84
    assert result['original_parameters']==tree


def test_compiled_shader_disassembly_rejects_truncated_dxbc_before_native_call():
    from tools.inspect_compiled_shaders import disassemble
    assert disassemble(b'no compiled bytecode') is None
    code=b'DXBC'+bytes(20)+struct.pack('<I',100)+bytes(4)
    with pytest.raises(ValueError,match='Truncated'):
        disassemble(code)


def test_compiled_uniforms_are_not_limited_to_shader_inspector_properties():
    parsed=dict(m_PropInfo=dict(m_Props=[dict(m_Name='_Color'),dict(m_Name='_MainTex')]),
                m_SubShaders=[dict(m_Passes=[dict(m_NameIndices=[['_MainTex_ST',2],['_Color',3]])])])
    properties=shader_properties(parsed)
    assert '_MainTex_ST' in properties and properties.count('_Color')==1
    assert '_Intensity' not in properties


def test_original_queue_offset_and_pass_tags_keep_glow_separate():
    parsed=dict(m_SubShaders=[dict(m_Tags=dict(tags=[['QUEUE','Transparent+99']]),m_Passes=[
        dict(m_State=dict(m_Tags=dict(tags=[]))),
        dict(m_State=dict(m_Tags=dict(tags=[['LIGHTMODE','GlowTransparent']]))),
        dict(m_Tags=dict(tags=[['LightMode','UniversalForward']]))])])
    result=shader_render_commands(parsed)
    assert result['render_queue']==3099
    assert result['passes']==[dict(index=0,light_mode='SRPDefaultUnlit'),
                             dict(index=1,light_mode='GlowTransparent'),dict(index=2,light_mode='UniversalForward')]
    assert shader_render_commands(parsed,2451)['render_queue']==2451


def test_unknown_queue_tag_is_audited_without_guessing():
    parsed=dict(m_SubShaders=[dict(m_Tags=dict(tags=[['QUEUE','UnknownQueue']]))])
    assert shader_render_commands(parsed)['render_queue'] is None
    assert shader_render_commands({})['render_queue']==2000
