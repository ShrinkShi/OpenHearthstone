import hashlib
import json
import wave
from pathlib import Path
from types import SimpleNamespace

import pytest

from tools.native_effects import parameters

ROOT=Path(__file__).parents[1]
AUDIT=json.loads((ROOT/'manifests/native-clip-audio-assets.json').read_text(encoding='utf-8'))


def test_all_original_clip_audio_refs_have_provenance():
    assert len(AUDIT['programs'])==183 and len(AUDIT['sounds'])==130
    assert sum(len(p['actions']) for p in AUDIT['programs'])==347
    assert len({c['file'] for s in AUDIT['sounds'] for c in s['clips']})==132
    assert not any(s['errors'] for s in AUDIT['sounds'])


@pytest.mark.parametrize('record',AUDIT['programs'],ids=lambda p:p['file'])
def test_original_audio_action_uses_audited_sound_ref(record):
    data=json.loads((ROOT/'local/runtime'/record['file']).read_text(encoding='utf-8'))
    actual=[a for m in data['machines'] for s in m['states'] for a in s['actions'] if a['name']=='AudioPlayClipAction']
    assert len(actual)==len(record['actions'])
    for action,source in zip(actual,record['actions']):
        sound=next(p for p in action['params'] if p['name']=='m_Sound')
        assert sound['asset_path']==source['sound'] and sound['asset_source']==source['source']
        assert not sound.get('unresolved')


@pytest.mark.parametrize('sound',AUDIT['sounds'],ids=lambda s:s['file'])
def test_original_sound_pcm_has_exact_audited_hash_and_samples(sound):
    data=json.loads((ROOT/'local/runtime'/sound['file']).read_text(encoding='utf-8'))
    assert data['source']==sound['source'] and not data['errors']
    assert [c['reference'] for c in data['clips']]==[c['reference'] for c in sound['clips']]
    for clip in sound['clips']:
        path=ROOT/'local/runtime'/clip['file'];info=clip['pcm']
        assert hashlib.sha256(path.read_bytes()).hexdigest()==clip['sha256']
        with wave.open(str(path),'rb') as pcm:
            assert (pcm.getnframes(),pcm.getnchannels(),pcm.getsampwidth(),pcm.getframerate())==(info['frames'],info['channels'],info['width'],info['rate'])
            assert info['frames']>0 and len(pcm.readframes(info['frames']))==info['frames']*info['channels']*info['width']


def test_sounddef_object_parameter_exports_external_definition():
    script=SimpleNamespace(read_typetree=lambda:dict(m_ClassName='SoundDef'))
    obj=SimpleNamespace(type=SimpleNamespace(name='MonoBehaviour'),assets_file=SimpleNamespace(name='source-cab'),path_id=17,read_typetree=lambda:dict(m_Script={'m_PathID':9}))
    reader=SimpleNamespace(pointer=lambda owner,pointer:script if pointer['m_PathID']==9 else obj)
    data=dict(actionStartIndex=[0],paramName=['m_Sound'],paramDataType=[24],paramDataPos=[0],fsmObjectParams=[dict(useVariable=False,value=dict(m_FileID=1,m_PathID=17))])
    result=parameters(data,0,None,reader,{}, {},export_asset=lambda target:dict(asset_path='source.native-sound.json'))[0]
    assert result['asset_path']=='source.native-sound.json' and not result.get('unresolved')


def test_null_audio_template_pointer_is_explicit():
    data=dict(actionStartIndex=[0],paramName=['m_TemplateSource'],paramDataType=[5],paramDataPos=[0],unityObjectParams=[dict(m_FileID=0,m_PathID=0)])
    result=parameters(data,0,None,SimpleNamespace(pointer=lambda owner,pointer:None),{}, {})[0]
    assert result['object_null'] and not result.get('unresolved')
