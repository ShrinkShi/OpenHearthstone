"""Recover original AudioPlayClipAction SoundDef refs and exact audio clips."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_scale_cache
from .asset_lock import atomic_json
from .importer import PROJECT
import argparse
import hashlib
import json
import wave


def audit(ex):
    refresh=json.loads((PROJECT/'manifests/native-clip-audio-refresh.json').read_text(encoding='utf-8'))
    sounds={};programs=[]
    for record in refresh['programs']:
        program=json.loads((ex.folder/record['file']).read_text(encoding='utf-8'));actions=[]
        for machine in program['machines']:
            for state in machine['states']:
                for action in state['actions']:
                    if action['name']!='AudioPlayClipAction':continue
                    param=next(p for p in action['params'] if p['name']=='m_Sound')
                    actions.append(dict(state=state['name'],sound=param.get('asset_path'),source=param.get('asset_source')))
                    filename=param.get('asset_path')
                    if not filename or filename in sounds:continue
                    data=json.loads((ex.folder/filename).read_text(encoding='utf-8'));clips=[]
                    for clip in data['clips']:
                        path=ex.folder/clip['file']
                        with wave.open(str(path),'rb') as pcm:
                            frames=pcm.getnframes();channels=pcm.getnchannels();width=pcm.getsampwidth()
                            if len(pcm.readframes(frames))!=frames*channels*width:raise ValueError('Truncated original PCM cache')
                            info=dict(frames=frames,channels=channels,width=width,rate=pcm.getframerate())
                        clips.append(dict(**clip,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),pcm=info))
                    sounds[filename]=dict(file=filename,source=data['source'],clips=clips,errors=data['errors'])
        programs.append(dict(file=record['file'],reference=record['reference'],actions=actions))
    atomic_json(PROJECT/'manifests/native-clip-audio-assets.json',dict(programs=programs,sounds=list(sounds.values()),complete=False,
        notes=['Exact SoundDef and decoded AudioClip provenance only. Whole SoundManager, category mixer, ducking and voice suppression are not completely restored.']))
    print('CLIP AUDIO ASSET AUDIT',len(programs),'programs;',len(sounds),'SoundDefs;',sum(len(s['clips']) for s in sounds.values()),'clip references',flush=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--audit-only',action='store_true');args=parser.parse_args()
    ex=RuntimeExport()
    if not args.audit_only:refresh_scale_cache(ex, {'AudioPlayClipAction'}, 'clip-audio')
    audit(ex)


if __name__=='__main__':
    main()
