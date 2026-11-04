"""为已有素材层绑定原 ParticleSystem 的颜色、尺寸和旋转通道。

这是有界的模块恢复；不宣称执行 PlayMaker、碰撞或全套原 Shader。
"""
import argparse
import json

from .asset_lock import atomic_json
from .board_ui_assets import source_bundle
from .export_runtime import RuntimeExport
from .importer import PROJECT


def curve(data):
    return dict(enabled=True,scalar=data.get('scalar', 1), mode=data.get('minMaxState', 0),
        keys=data.get('maxCurve', {}).get('m_Curve', []))


def gradient(data):
    source = data.get('maxGradient', {})
    return dict(enabled=True,mode=data.get('minMaxState', 0), constant=data.get('maxColor', dict(r=1,g=1,b=1,a=1)),
        colors=[dict(time=source['ctime'+str(n)]/65535, value=source['key'+str(n)])
                for n in range(source.get('m_NumColorKeys', 0))],
        alpha=[dict(time=source['atime'+str(n)]/65535, value=source['key'+str(n)]['a'])
               for n in range(source.get('m_NumAlphaKeys', 0))])


def tracks(tree):
    initial=tree.get('InitialModule', {})
    color=tree.get('ColorModule', {})
    size=tree.get('SizeModule', {})
    rotation=tree.get('RotationModule', {})
    return dict(restored=True,color=gradient(color['gradient']) if color.get('enabled') else None,
                start_color=gradient(initial.get('startColor', {})),
                size=curve(size.get('curve', {})) if size.get('enabled') else None,
                rotation=curve(rotation.get('curve', {})) if rotation.get('enabled') else None,
                initial_rotation=curve(initial.get('startRotation', {})),
                original_duration=tree.get('lengthInSec', 1))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ids', default='EX1_134,CS2_025,CS2_028,CS2_032,CS1_112,EX1_593')
    args=parser.parse_args()
    ex=RuntimeExport()
    index=json.loads((ex.folder/'effect-index.json').read_text(encoding='utf-8'))
    profiles={p['card_id']:p for p in index['entries']}
    sources=[]
    for card_id in args.ids.split(','):
        profile=profiles.get(card_id)
        if not profile or not profile.get('visuals'):
            continue
        reference=ex.imp.reader.definition(card_id)['m_PlayEffectDef']['m_SpellPath']
        root,_,_=ex.imp.reader.resolve(reference)
        bound=set()
        for obj,tree in ex.imp.reader.walk(root,include_visual=True,max_nodes=4000):
            if obj.type.name!='ParticleSystemRenderer':
                continue
            tree=tree or obj.read_typetree()
            go=ex.imp.reader.pointer(obj,tree['m_GameObject'])
            if not go:
                continue
            particle=None
            for component in go.read_typetree().get('m_Component', []):
                node=ex.imp.reader.pointer(go,component['component'])
                if node and node.type.name=='ParticleSystem':
                    particle=node
                    break
            if not particle:
                continue
            for pointer in tree.get('m_Materials', []):
                material=ex.imp.reader.pointer(obj,pointer)
                if not material:
                    continue
                logical='VFX_ASSOC_'+ex.key(material).upper()
                sprite=next((s for s in profile['visuals'] if s['texture']==logical),None)
                if not sprite or logical in bound:
                    continue
                bound.add(logical)
                raw=particle.read_typetree()
                sprite['native']=tracks(raw)
                path=ex.key(particle)+'.particlesystem.json'
                atomic_json(ex.folder/path,raw)
                sources.append(dict(card_id=card_id,texture=logical,type='ParticleSystem',cab=particle.assets_file.name,
                    pathid=str(particle.path_id),bundle=source_bundle(ex,particle.assets_file.name),cache='local/runtime/'+path,
                    renderer_pathid=str(obj.path_id),restored=['startColor','ColorModule','SizeModule','RotationModule']))
        print(card_id,'native layers',sum(bool(s.get('native')) for s in profile['visuals']),flush=True)
    atomic_json(ex.folder/'effect-index.json',index)
    atomic_json(PROJECT/'manifests/original-particle-tracks.json',dict(assets=sources,
        limitations=['normalized lifetime','max curve deterministic sampling','no script FSM','no emission shape/collision restoration']))


if __name__=='__main__':
    main()
