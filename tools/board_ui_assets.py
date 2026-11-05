"""按需恢复菜单、回合按钮及随从粒子，并保存原动画四元数采样。"""
import json

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT


MODELS = {
    'MODEL_END_TURN': ('Button_EndTurn', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_GAME_MENU_BUTTON': ('GameMenuButton_Default', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_MANA_ENEMY': ('FrameManaEnemy', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_MANA_PLAYER': ('FrameManaPlayer', 'essential_base_global-mesh-0.unity3d'),
}
TEXTURES = {'UI_TOOLTIP_FRAME': 'TooltipFrame1',
            'VFX_SUMMON_RING': 'AllyInPlaySummonRing_Med',
            'VFX_SUMMON_BURST': 'AllyInPlaySummon_Blue', 'UI_OPTIONS_FRAME': 'OptionsFrame',
            'UI_CONCEDE_BUTTON':'Button_Concede_Red', 'UI_MENU_ACTION':'ButtonsA_Dynamic',
            'UI_MANA_TRAY':'ManaTray', 'UI_BOARD_FRAME':'STR_FRAME'}


def source_bundle(export, cab):
    row = export.imp.store.db.execute('SELECT bundle FROM assets WHERE cab=? LIMIT 1', (cab,)).fetchone()
    return row['bundle'] if row else ''


def main():
    ex = RuntimeExport()
    sources = []
    for logical, (name, bundle) in MODELS.items():
        ex.scene(logical, name, bundle)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type="GameObject" AND bundle=? LIMIT 1', (name, bundle)).fetchone()
        if row:
            sources.append(dict(id=logical, name=name, bundle=row['bundle'], pathid=row['pathid'], cab=row['cab'], type=row['type'], cache='local/runtime/'+ex.registry.get(logical,'')))
    for logical, name in TEXTURES.items():
        ex.named(logical, name)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type="Texture2D" ORDER BY bundle LIMIT 1', (name,)).fetchone()
        if row:
            sources.append(dict(id=logical, name=name, bundle=row['bundle'], pathid=row['pathid'], cab=row['cab'], type=row['type'], cache='local/runtime/'+ex.registry.get(logical,'')))
    clips = []
    for name in ('ENDTURN_NEXT_TURN','ENDTURN_YOUR_TURN','ENDTURN_WAITING','ENDTURN_NO_MORE_PLAYS'):
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type="AnimationClip" AND bundle LIKE "essential%" LIMIT 1', (name,)).fetchone()
        obj = next(o for o in ex.imp.reader.load(row['bundle']).objects if str(o.path_id)==row['pathid'] and o.assets_file.name==row['cab'])
        tree = obj.read_typetree()
        curves = [dict(path=c['path'], keys=[dict(time=k['time'], value=k['value']) for k in c['curve']['m_Curve']]) for c in tree['m_RotationCurves']]
        clip = dict(name=name, duration=tree['m_MuscleClip']['m_StopTime'], rotations=curves)
        clips.append(clip)
        print('CLIP', name, clip['duration'], [(c['path'], len(c['keys'])) for c in curves], flush=True)
        sources.append(dict(id='ANIM_END_TURN',name=name,bundle=row['bundle'],pathid=row['pathid'],cab=row['cab'],type='AnimationClip',cache='local/runtime/turn-button-animation.json'))
    atomic_json(ex.folder/'turn-button-animation.json',dict(clips=clips))
    ex.registry['ANIM_END_TURN']='turn-button-animation.json'
    # 记录原粒子发射和渲染器材质关联；原脚本不在本进程执行。
    particles = []
    for name in ('Card_Play_Ally_Zzz','Card_Play_Ally_SummonIn'):
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type="GameObject" AND bundle LIKE "essential%" LIMIT 1',(name,)).fetchone()
        obj = next(o for o in ex.imp.reader.load(row['bundle']).objects if str(o.path_id)==row['pathid'] and o.assets_file.name==row['cab'])
        for child, tree in ex.imp.reader.walk(obj,include_visual=True):
            if child.type.name not in ('ParticleSystem','ParticleSystemRenderer'):
                continue
            tree = tree or child.read_typetree()
            filename = ex.key(child)+'.particle.json'
            atomic_json(ex.folder/filename,tree)
            particles.append(dict(name=name,type=child.type.name,pathid=str(child.path_id),cab=child.assets_file.name,bundle=source_bundle(ex,child.assets_file.name),cache='local/runtime/'+filename))
            if child.type.name=='ParticleSystemRenderer':
                for pointer in tree.get('m_Materials',[]):
                    mat = ex.imp.reader.pointer(child,pointer)
                    if not mat:
                        continue
                    material = ex.material(mat)
                    logical = 'VFX_SLEEP_Z' if name.endswith('Zzz') else 'VFX_SUMMON_MOTE' if material['name']=='GenFX_BlueMotes_Grave' else 'VFX_SUMMON_DUST'
                    ex.registry[logical] = material['texture']
                    texture_info = next(p for p in reversed(ex.provenance) if p.get('file')==material['texture'])
                    source = dict(texture_info)
                    source.update(id=logical,name=material['name'],shader=material['shader'],
                                  bundle=source_bundle(ex,texture_info['cab']),
                                  cache='local/runtime/'+material['texture'])
                    if source not in sources:
                        sources.append(source)
                    print('PARTICLE MATERIAL',name,material['name'],material['shader'],flush=True)
    ex.save()
    atomic_json(PROJECT/'manifests/board-ui-assets.json',dict(assets=sources,particles=particles,
        notes=['棋盘采用原 Board_STR 网格及贴图；相机和专用 Shader 由本项目重实现。',
               'FrameManaEnemy 裸网格的 Standard 材质没有纹理；计数气泡从棋盘 STR_FRAME 图集恢复蓝色区域。',
               '回合按钮重用原模型与解包的四元数采样；原控制脚本不执行。',
               '召唤及睡觉特效重用原贴图，粒子轨迹在表现层重实现。']))
    if ex.errors:
        print('EXPORT ERRORS',ex.errors)
        raise RuntimeError(ex.errors)


if __name__=='__main__':
    main()
