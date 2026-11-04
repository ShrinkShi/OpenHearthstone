"""从实际 CardDef 效果引用恢复小集合；状态机/动画曲线只读存证，不执行原脚本。"""
import json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .asset_lock import asset_lock, atomic_json, merge_effects


SOUNDS = {
    'SFX_CARD_HOVER': 'card_mouse_over',
    'SFX_CARD_PICKUP': 'card_mouse_away',
    'SFX_MULLIGAN_SELECT': 'card_turn_over_normal',
    'SFX_FIRE_START': 'Shared_Fire_Start_1',
    'SFX_FIRE_CAST': 'Shared_Fire_Cast_1',
    'SFX_FIREBLAST_IMPACT': 'Shared_Fire_Impact_Small',
    'SFX_FIREBALL_IMPACT': 'Shared_Fire_Impact',
    'SFX_FROST_START': 'Shared_Frost_Start_1',
    'SFX_FROST_CAST': 'Shared_Frost_Cast_1',
    'SFX_FROST_IMPACT': 'Shared_Frost_Impact_1',
    'SFX_INTELLECT_CAST': 'Mage_ArcaneIntellect_Cast_1',
}
TEXTURES = {
    'VFX_FIRE_CRESCENT': 'GenFX_FireballCrescentAlpha',
    'VFX_FIRE_SPARK': 'GenFX_elecspark',
    'VFX_FROST_MISSILE': 'GenFX_FrostMissles01',
    'VFX_FROST_SHARDS': 'GenFX_Ice_Shards',
    'VFX_INTELLECT_SYMBOL': 'GenFX_Intellect_Symbol',
    'VFX_INTELLECT_RUNES': 'GenFX_Runes2b',
}
EFFECTS = [
    dict(card_id='HERO_08bp', start='SFX_FIRE_START', cast='SFX_FIRE_CAST',
         hit='SFX_FIREBLAST_IMPACT', projectile='VFX_FIRE_CRESCENT', impact='VFX_FIRE_SPARK', kind='fire'),
    dict(card_id='CS2_029', start='SFX_FIRE_START', cast='SFX_FIRE_CAST',
         hit='SFX_FIREBALL_IMPACT', projectile='VFX_FIRE_CRESCENT', impact='VFX_FIRE_SPARK', kind='fire'),
    dict(card_id='CS2_024', start='SFX_FROST_START', cast='SFX_FROST_CAST',
         hit='SFX_FROST_IMPACT', projectile='VFX_FROST_MISSILE', impact='VFX_FROST_SHARDS', kind='frost'),
    dict(card_id='CS2_023', start='SFX_INTELLECT_CAST', cast='', hit='',
         projectile='', impact='VFX_INTELLECT_SYMBOL', kind='draw'),
]


def main():
    ex = RuntimeExport()
    sources, research = [], []
    for logical, name in {**SOUNDS, **TEXTURES}.items():
        kind = 'AudioClip' if logical in SOUNDS else 'Texture2D'
        ex.named(logical, name, kind)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1',
                                      (name, kind)).fetchone()
        if row and logical in ex.registry:
            sources.append(dict(id=logical, name=name, type=kind, bundle=row['bundle'], cab=row['cab'],
                                pathid=row['pathid'], guids=json.loads(row['guids'] or '[]'),
                                cache='local/runtime/' + ex.registry[logical]))
    ex.scene('MODEL_MINION_TRIGGER', 'Trigger', 'essential_base_global-mesh-0.unity3d')
    row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=? LIMIT 1',
                                ('Trigger', 'GameObject', 'essential_base_global-mesh-0.unity3d')).fetchone()
    sources.append(dict(id='MODEL_MINION_TRIGGER', name='Trigger', type='GameObject',
                        bundle=row['bundle'], cab=row['cab'], pathid=row['pathid'],
                        guids=json.loads(row['guids'] or '[]'), cache='local/runtime/MODEL_MINION_TRIGGER.scene.json'))
    for effect in EFFECTS:
        definition = ex.imp.reader.definition(effect['card_id'])
        reference = definition.get('m_PlayEffectDef', {}).get('m_SpellPath', '')
        root, _, _ = ex.imp.reader.resolve(reference)
        evidence = dict(card_id=effect['card_id'], reference=reference, audio=[], animation=[], particles=[])
        for obj, tree in ex.imp.reader.walk(root, include_visual=True):
            kind = obj.type.name
            if kind not in ('AudioClip', 'AnimationClip', 'ParticleSystem', 'ParticleSystemRenderer'):
                continue
            tree = tree or obj.read_typetree()
            identity = dict(cab=obj.assets_file.name, pathid=str(obj.path_id), type=kind, name=tree.get('m_Name', ''),
                            bundle=ex.imp.reader.cabs.get(obj.assets_file.name, ''))
            if kind == 'AudioClip':
                evidence['audio'].append(identity)
            else:
                filename = ex.key(obj) + '.' + kind.lower() + '.json'
                atomic_json(ex.folder / filename, tree)
                identity['cache'] = 'local/runtime/' + filename
                evidence['animation' if kind == 'AnimationClip' else 'particles'].append(identity)
        # 原素材关联与当前重实现的播放曲线分别记录。
        research.append(evidence)
        print(effect['card_id'], len(evidence['audio']), 'audio', len(evidence['animation']),
              'animation', len(evidence['particles']), 'particle components', flush=True)
    ex.save()
    merge_effects(ex.imp.local, EFFECTS)
    with asset_lock(ex.imp.local):
        atomic_json(ex.folder / 'effect-research.json', dict(entries=research))
    atomic_json(PROJECT / 'manifests/feedback-assets.json', dict(assets=sources, effects=EFFECTS,
        references=[dict(card_id=r['card_id'], reference=r['reference'],
                         animation=r['animation'], particle_count=len(r['particles'])) for r in research],
        notes=['交互音轨来自本机；留牌选择沿用翻牌 Foley，尚未证实原版按钮实际引用。',
               '效果关联取自 CardDef，粒子/动画原参数仅在 local 缓存；播放轨迹由表现层重实现。']))
    if ex.errors:
        raise RuntimeError(ex.errors)


if __name__ == '__main__':
    main()
