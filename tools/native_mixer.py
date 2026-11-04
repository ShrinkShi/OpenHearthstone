"""Normalize exact compiled source mixer topology and snapshot parameters."""
import json
import hashlib
import UnityPy
from .asset_lock import atomic_json
from .importer import PROJECT


def recover():
    audit=json.loads((PROJECT/'manifests/native-sound-limits.json').read_text(encoding='utf-8'))
    assets=audit['mixer_assets'];controller=next(r for r in assets if r['type']=='AudioMixerController')
    tree=controller['values'];constant=tree['m_MixerConstant'];snap=constant['snapshots']
    if len(snap)!=1 or constant['exposedParameterIndices'] or tree['m_OutputGroup']['m_PathID']:raise ValueError('Unsupported additional mixer routing or snapshots')
    names=[n.decode('utf-8') for n in bytes(constant['groupNameBuffer']).split(b'\0') if n]
    groups=[];effects=[];values=snap[0]['values']
    for i,g in enumerate(constant['groups']):
        source=next(r['source'] for r in assets if r['type']=='AudioMixerGroupController' and r['values']['m_GroupID']==constant['groupGUIDs'][i])
        groups.append(dict(index=i,name=names[i],parent=g['parentConstantIndex'],volume=values[g['volumeIndex']],pitch=values[g['pitchIndex']],mute=g['mute'],solo=g['solo'],bypass=g['bypassEffects'],source=source))
    types={-2:'Attenuation',-5:'Duck Volume',-3:'Send'}
    for i,e in enumerate(constant['effects']):
        if e['type'] not in types:raise ValueError('Unrestored native DSP effect type')
        effects.append(dict(index=i,group=e['groupConstantIndex'],name=types[e['type']],target=-1 if e['sendTargetEffectIndex']==4294967295 else e['sendTargetEffectIndex'],bypass=e['bypass'],parameters=[values[j] for j in e['parameterIndices']],mix=0 if e['wetMixLevelIndex']==4294967295 else values[e['wetMixLevelIndex']]))
    result=dict(source=controller['source'],name=tree['m_Name'],snapshot='Snapshot',suspend=tree['m_EnableSuspend'],threshold=tree['m_SuspendThreshold'],update_mode=tree['m_UpdateMode'],groups=groups,effects=effects,complete=False,
        notes=['Original compiled topology and snapshot values. Derived Unity mixer uses newly generated editor asset GUIDs. Audible equivalence unverified.'])
    atomic_json(PROJECT/'manifests/native-mixer-program.json',result)
    atomic_json(PROJECT/'local/runtime/native-mixer-program.json',result)
    print('ORIGINAL MIXER PROGRAM',len(groups),'groups;',len(effects),'effects;',len(values),'snapshot values',flush=True)
    return result


def verify_bundle():
    audit=json.loads((PROJECT/'manifests/native-sound-limits.json').read_text(encoding='utf-8'))
    original=next(r['values'] for r in audit['mixer_assets'] if r['type']=='AudioMixerController')
    path=PROJECT/'local/runtime/native-audio-mixer.assetbundle'
    env=UnityPy.load(str(path));rebuilt=next(o.read_typetree() for o in env.objects if o.type.name=='AudioMixerController')
    excluded={'groupGUIDs','effectGUIDs','snapshotGUIDs'}
    source={k:v for k,v in original['m_MixerConstant'].items() if k not in excluded}
    target={k:v for k,v in rebuilt['m_MixerConstant'].items() if k not in excluded}
    if source!=target:raise ValueError('Compiled original mixer topology or snapshot values differ')
    for key in ('m_Name','m_EnableSuspend','m_SuspendThreshold','m_UpdateMode'):
        if original[key]!=rebuilt[key]:raise ValueError('Original mixer controller setting differs: '+key)
    result=dict(bundle=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),compared_fields=list(source),
        ignored_generated_identity_fields=sorted(excluded),native_topology_and_values_match=True,audible_verified=False,complete=False)
    atomic_json(PROJECT/'manifests/native-mixer-bundle-audit.json',result)
    print('COMPILED ORIGINAL MIXER MATCH',len(source),'constant fields; generated GUIDs excluded',flush=True)
    return result


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--verify-bundle',action='store_true');args=parser.parse_args()
    verify_bundle() if args.verify_bundle else recover()
