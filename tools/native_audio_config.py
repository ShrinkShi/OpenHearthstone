"""Export the original SoundConfig PlayClip template by exact identity."""
from .export_runtime import RuntimeExport
from .native_effects import identity
from .asset_lock import atomic_json
from .importer import PROJECT


def export_template(ex,obj):
    if obj.type.name!='AudioSource':raise ValueError('Expected original AudioSource template')
    values=obj.read_typetree();group=ex.imp.reader.pointer(obj,values['OutputAudioMixerGroup'])
    if values['m_audioClip']['m_PathID']:raise ValueError('Unrestored explicit template clip dependency')
    mixer=ex.imp.reader.pointer(group,group.read_typetree()['m_AudioMixer']) if group else None
    filename=ex.key(obj)+'.native-audio-template.json'
    result=dict(source=identity(ex,obj),values=values,group=group.read_typetree()['m_Name'] if group else '',mixer=mixer.read_typetree()['m_Name'] if mixer else '',
        group_source=identity(ex,group) if group else None,mixer_source=identity(ex,mixer) if mixer else None,complete=False)
    atomic_json(ex.folder/filename,result)
    return dict(asset_source=identity(ex,obj),asset_path=filename,asset_type='AudioSource',unresolved=False)


def recover(ex):
    bundle='essential_base_global-prefab-0.unity3d'
    cab=ex.imp.reader.load(bundle).get_cab('cab-69c88b234dfa427e02d1fcee9337ae34')
    go=cab.objects[3460946133557208792]
    configs=[]
    for item in go.read_typetree()['m_Component']:
        obj=ex.imp.reader.pointer(go,item['component'])
        if obj.type.name!='MonoBehaviour':continue
        tree=obj.read_typetree();script=ex.imp.reader.pointer(obj,tree['m_Script'])
        if script.read_typetree().get('m_ClassName')=='SoundConfig':configs.append(obj)
    if len(configs)!=1:raise ValueError('Exact SoundConfig component required')
    config=configs[0];tree=config.read_typetree();source=ex.imp.reader.pointer(config,tree['m_PlayClipTemplate'])
    if source is None or source.type.name!='AudioSource':raise ValueError('Original PlayClip template missing')
    values=source.read_typetree()
    if values['OutputAudioMixerGroup']['m_PathID'] or values['m_audioClip']['m_PathID']:
        raise ValueError('Template has additional mixer/clip dependencies')
    result=dict(config_source=identity(ex,config),config_values=tree,template_source=identity(ex,source),values=values,
        complete=False,notes=['Source template component only; full SoundManager mixer, limits and ducking are not restored.'])
    atomic_json(ex.folder/'native-audio-config.json',result)
    atomic_json(PROJECT/'manifests/native-audio-config.json',result)
    print('ORIGINAL AUDIO TEMPLATE',source.path_id,'min/max',values['MinDistance'],values['MaxDistance'],flush=True)
    return result


if __name__=='__main__':recover(RuntimeExport())
