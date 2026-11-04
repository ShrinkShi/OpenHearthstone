"""按 CardDef 关联按需恢复盗贼效果及原版嘲讽模型，不修改官方安装。"""
import argparse
import json

from .asset_lock import atomic_json, merge_effects
from .board_ui_assets import source_bundle
from .export_runtime import RuntimeExport
from .importer import PROJECT

CARDS = ('CS2_072', 'EX1_124', 'CS2_073')
SOUNDS = {
    'SFX_BACKSTAB_CAST': 'Shared_SneakyMissileStandard2_Cast_1',
    'SFX_BACKSTAB_HIT': 'Shared_SneakyAttackImpactStandard_1',
    'SFX_EVISCERATE_CAST': 'Rogue_Eviscerate_Cast_1',
    'SFX_EVISCERATE_HIT': 'Rogue_Eviscerate_Impact_1',
    'SFX_COLD_BLOOD_START': 'Shared_Rage_PreCast_1',
    'SFX_COLD_BLOOD_CAST': 'Shared_Rage_Start_1',
}
MATERIALS = {'GenFX_EviscerateTrail': 'VFX_EVISCERATE_TRAIL',
             'GenFX_ValeeraDagger': 'VFX_ROGUE_DAGGER',
             'GenFX_RageFuryPhase1': 'VFX_COLD_BLOOD_FURY',
             'GenFX_RageFace1_Top': 'VFX_COLD_BLOOD_FACE'}
EFFECTS = [
    dict(card_id='CS2_072', cast='SFX_BACKSTAB_CAST', hit='SFX_BACKSTAB_HIT',
         projectile='VFX_ROGUE_DAGGER', impact='VFX_EVISCERATE_TRAIL', kind='slash', cast_duration=.38),
    dict(card_id='EX1_124', cast='SFX_EVISCERATE_CAST', hit='SFX_EVISCERATE_HIT',
         projectile='VFX_ROGUE_DAGGER', impact='VFX_EVISCERATE_TRAIL', kind='slash', cast_duration=.55),
    dict(card_id='CS2_073', start='SFX_COLD_BLOOD_START', cast='SFX_COLD_BLOOD_CAST',
         projectile='VFX_COLD_BLOOD_FURY', impact='VFX_COLD_BLOOD_FACE', kind='rage', cast_duration=.7),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--research', action='store_true')
    args = parser.parse_args()
    ex = RuntimeExport()
    research, sources = [], []
    for card_id in CARDS:
        reference = ex.imp.reader.definition(card_id)['m_PlayEffectDef']['m_SpellPath']
        root, _, _ = ex.imp.reader.resolve(reference)
        evidence = dict(card_id=card_id, reference=reference, assets=[])
        for obj, tree in ex.imp.reader.walk(root, include_visual=True):
            kind = obj.type.name
            if kind not in ('AudioClip', 'Material', 'AnimationClip', 'ParticleSystem'):
                continue
            tree = tree or obj.read_typetree()
            identity = dict(name=tree.get('m_Name', ''), type=kind, cab=obj.assets_file.name,
                            pathid=str(obj.path_id), bundle=source_bundle(ex, obj.assets_file.name))
            if kind == 'Material':
                identity['material'] = ex.material(obj)
                logical = MATERIALS.get(identity['name'])
                texture = identity['material']['texture']
                if logical and texture and not args.research:
                    ex.registry[logical] = texture
                    tex = next(p for p in reversed(ex.provenance) if p.get('file') == texture)
                    sources.append(dict(id=logical, card_id=card_id, name=identity['name'], type='Texture2D',
                                        cab=tex['cab'], pathid=tex['pathid'], bundle=source_bundle(ex, tex['cab']),
                                        shader=identity['material']['shader'], cache='local/runtime/'+texture))
            elif kind in ('AnimationClip', 'ParticleSystem') and not args.research:
                filename = ex.key(obj) + '.' + kind.lower() + '.json'
                atomic_json(ex.folder / filename, tree)
                identity['cache'] = 'local/runtime/' + filename
            evidence['assets'].append(identity)
        research.append(evidence)
        print(card_id, reference, flush=True)
        for asset in evidence['assets']:
            if asset['type'] == 'AudioClip':
                print('  AUDIO', asset['name'], flush=True)
            elif asset['type'] == 'Material':
                print('  MATERIAL', asset['name'], asset['material']['texture'], flush=True)
    if args.research:
        return
    bundle = 'essential_base_global-mesh-0.unity3d'
    ex.scene('MODEL_MINION_TAUNT', 'GenFX_Taunt_Shield', bundle)
    row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=?',
                                ('GenFX_Taunt_Shield', 'GameObject', bundle)).fetchone()
    sources.append(dict(id='MODEL_MINION_TAUNT', name=row['name'], type=row['type'], bundle=bundle,
                        cab=row['cab'], pathid=row['pathid'], guids=json.loads(row['guids']),
                        cache='local/runtime/'+ex.registry['MODEL_MINION_TAUNT']))
    scene = json.loads((ex.folder/ex.registry['MODEL_MINION_TAUNT']).read_text(encoding='utf-8'))
    sources[-1]['dependencies'] = []
    for node in scene['nodes']:
        sources[-1]['dependencies'].append(dict(type='Mesh', cache='local/runtime/'+node['mesh']))
        for material in node['materials']:
            texture = next(p for p in reversed(ex.provenance) if p.get('file') == material['texture'])
            asset = ex.imp.store.db.execute('SELECT * FROM assets WHERE cab=? AND pathid=?',
                                           (texture['cab'], texture['pathid'])).fetchone()
            sources[-1]['dependencies'].append(dict(type='Texture2D', name=asset['name'], cab=asset['cab'],
                pathid=asset['pathid'], guids=json.loads(asset['guids']), bundle=asset['bundle'],
                material=material['name'], cache='local/runtime/'+material['texture']))
    linked_audio = {a['name'] for r in research for a in r['assets'] if a['type'] == 'AudioClip'}
    for logical, name in SOUNDS.items():
        if name not in linked_audio:
            raise RuntimeError('音频未出现在 CardDef 效果关联中：'+name)
        ex.named(logical, name, 'AudioClip')
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1',
                                    (name, 'AudioClip')).fetchone()
        sources.append(dict(id=logical, name=name, type=row['type'], bundle=row['bundle'], cab=row['cab'],
                            pathid=row['pathid'], guids=json.loads(row['guids']),
                            cache='local/runtime/'+ex.registry[logical]))
    if ex.errors:
        raise RuntimeError(ex.errors)
    ex.save()
    merge_effects(ex.imp.local, EFFECTS)
    # 原材质参数和曲线仅本机留存，源码仓库只保存关联元数据。
    atomic_json(ex.folder/'combat-effect-research.json', dict(entries=research))
    for evidence in research:
        for asset in evidence['assets']:
            asset.pop('material', None)
    atomic_json(PROJECT/'manifests/combat-feedback-assets.json', dict(assets=sources, effects=EFFECTS, research=research,
        notes=['嘲讽盾牌使用原网格和材质；背刺音效来自实际关联，贴图复用刺骨匕首/刀痕。',
               '原粒子与 AnimationClip 参数在 local 缓存；播放轨迹由客户端重实现，不声称恢复原脚本。']))
    print('sources', len(sources), 'research', sum(len(r['assets']) for r in research), flush=True)


if __name__ == '__main__':
    main()
