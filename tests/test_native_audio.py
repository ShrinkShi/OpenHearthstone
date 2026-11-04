from types import SimpleNamespace
from tools.native_audio import recover


def test_attached_sounddef_null_audio_clip_uses_exact_reference_and_retains_ranges(monkeypatch):
    class Object:
        def __init__(self,kind,tree):self.type=SimpleNamespace(name=kind);self.tree=tree
        def read_typetree(self):return self.tree
    root=Object('GameObject',{'m_Component':[{'component':1},{'component':2},{'component':3}]})
    objects={1:Object('Transform',{'m_Children':[]}),2:Object('AudioSource',{'m_audioClip':{'m_PathID':0},'m_Volume':.75}),
        3:Object('MonoBehaviour',{'m_Script':4,'m_AudioClip':'','m_RandomClips':[{'m_Clip':'drop:exact-guid','m_Weight':1}],
            'm_RandomPitchMin':.9,'m_RandomPitchMax':1.1}),4:Object('MonoScript',{'m_ClassName':'SoundDef'})}
    ex=SimpleNamespace(imp=SimpleNamespace(reader=SimpleNamespace(resolve=lambda ref:(root,None,None),pointer=lambda obj,ptr:objects[ptr])))
    monkeypatch.setattr('tools.native_audio.identity',lambda *args:{'type':args[1].type.name})
    calls=[]
    def clip_exporter(ex,ref):calls.append(ref);return {'reference':ref,'file':'original.wav'}
    result=recover(ex,'source-guid',clip_exporter)
    assert calls==['drop:exact-guid'] and not result['errors'] and not result['complete']
    node=result['nodes'][0];assert node['audio_sources'][0]['values']['m_audioClip']['m_PathID']==0
    assert node['sound_defs'][0]['values']['m_RandomPitchMin']==.9
    assert node['sound_defs'][0]['values']['m_RandomPitchMax']==1.1
    def failing(ex,ref):raise FileNotFoundError('missing original clip')
    failed=recover(ex,'source-guid',failing)
    assert failed['clips'][0]['reference']=='drop:exact-guid'
    assert failed['errors']==[{'reference':'drop:exact-guid','error':'missing original clip'}]


def test_wrong_sound_object_type_is_rejected():
    from tools.native_audio import export_clip
    ex=SimpleNamespace(imp=SimpleNamespace(reader=SimpleNamespace(resolve=lambda ref:(SimpleNamespace(type=SimpleNamespace(name='Texture2D')),None,None))))
    import pytest
    with pytest.raises(ValueError,match='AudioClip'):export_clip(ex,'wrong-type')


def test_audio_binding_preserves_visual_params_and_requires_exact_source(tmp_path,monkeypatch):
    import json
    from tools.native_audio import bind_program
    path=tmp_path/'source.actor-spell.json'
    original={'reference':'source:guid','source':{'pathid':'42'},'nodes':[{'position':{'x':4}}],
        'machines':[{'states':[{'actions':[{'name':'AudioPlaythroughAction','params':[
            {'kind':24,'object_null':False},{'kind':20,'node':7}]}]}]}]}
    path.write_text(json.dumps(original),encoding='utf-8')
    ex=SimpleNamespace(folder=tmp_path,key=lambda root:'source',imp=SimpleNamespace(reader=object()))
    monkeypatch.setattr('tools.native_audio.identity',lambda ex,root:{'pathid':'42'})
    def refresh(program,*args):
        params=program['machines'][0]['states'][0]['actions'][0]['params']
        params[0]['object_null']=True;params[1]['node']=-1
    monkeypatch.setattr('tools.native_audio.refresh_program_assets',refresh)
    bind_program(ex,object(),'source.native-audio.json')
    actual=json.loads(path.read_text(encoding='utf-8'))
    assert actual['audio_file']=='source.native-audio.json' and actual['nodes']==original['nodes']
    params=actual['machines'][0]['states'][0]['actions'][0]['params']
    assert params[0]['object_null'] and params[1]['node']==7
    before=path.read_bytes();monkeypatch.setattr('tools.native_audio.identity',lambda ex,root:{'pathid':'43'})
    import pytest
    with pytest.raises(ValueError,match='identity'):bind_program(ex,object(),'wrong.json')
    assert path.read_bytes()==before
