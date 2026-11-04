"""按需恢复场上武器、牌库和刀扇素材；官方客户端只读。"""
import json

from .asset_lock import asset_lock, atomic_json
from .board_ui_assets import source_bundle
from .export_runtime import RuntimeExport
from .importer import PROJECT


MODELS = {'MODEL_WEAPON': 'Card_InPlay_HeroWeapon_v2', 'MODEL_HERO_POWER': 'Card_InPlay_HeroPowerV2'}
MODELS.update({'MODEL_DECK_'+size: 'GenFX_Deck_'+size for size in ('1', '25', '50', '75', 'Full')})


def main():
    ex = RuntimeExport()
    sources, research = [], []
    for logical, name in MODELS.items():
        bundle = 'essential_base_global-mesh-0.unity3d'
        ex.scene(logical, name, bundle)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=?',
                                      (name, 'GameObject', bundle)).fetchone()
        sources.append(dict(id=logical, name=name, bundle=bundle, cab=row['cab'], pathid=row['pathid'],
                            type=row['type'], cache='local/runtime/'+ex.registry[logical],
                            usage='equipped_weapon' if logical=='MODEL_WEAPON' else
                                  'hero_power_front_and_used_back' if logical=='MODEL_HERO_POWER' else 'deck_stack'))
        if logical=='MODEL_HERO_POWER':
            back=ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=?',
                ('Hero_Power_D','Texture2D','essential_base_global-texture-0.unity3d')).fetchone()
            texture=next(p for p in ex.provenance if p.get('pathid')==back['pathid'] and p.get('cab')==back['cab'])
            sources.append(dict(id='MODEL_HERO_POWER/used_back',name=back['name'],bundle=back['bundle'],
                cab=back['cab'],pathid=back['pathid'],type=back['type'],guids=json.loads(back['guids']),
                cache='local/runtime/'+texture['file'],usage='original_submesh_2_back_material'))
    definition = ex.imp.reader.definition('EX1_129')
    reference = definition['m_PlayEffectDef']['m_SpellPath']
    root, _, _ = ex.imp.reader.resolve(reference)
    for obj, tree in ex.imp.reader.walk(root, include_visual=True):
        kind = obj.type.name
        if kind not in ('AudioClip', 'Material', 'AnimationClip', 'ParticleSystem'):
            continue
        tree = tree or obj.read_typetree()
        identity = dict(name=tree.get('m_Name', ''), type=kind, cab=obj.assets_file.name,
                        pathid=str(obj.path_id), bundle=source_bundle(ex, obj.assets_file.name))
        if kind in ('AnimationClip', 'ParticleSystem'):
            filename = ex.key(obj)+'.'+kind.lower()+'.json'
            atomic_json(ex.folder/filename, tree)
            identity['cache'] = 'local/runtime/'+filename
        if kind == 'Material' and tree['m_Name'] in ('GenFX_ValeeraDagger', 'GenFX_FanOfKnivesTrail'):
            material = ex.material(obj)
            logical = 'VFX_FAN_DAGGER' if tree['m_Name']=='GenFX_ValeeraDagger' else 'VFX_FAN_TRAIL'
            ex.registry[logical] = material['texture']
            tex = next(p for p in reversed(ex.provenance) if p.get('file')==material['texture'])
            sources.append(dict(id=logical, name=tree['m_Name'], type=tex['type'], cab=tex['cab'],
                                pathid=tex['pathid'], bundle=source_bundle(ex,tex['cab']),
                                cache='local/runtime/'+material['texture'], shader=material['shader'],
                                usage='fan_projectile' if logical=='VFX_FAN_DAGGER' else 'fan_trail'))
        research.append(identity)
    for logical, name in {'SFX_FAN_CAST':'Rogue_FanOfKnives_Cast_1',
                          'SFX_FAN_HIT':'Rogue_FanOfKnives_Impact_1'}.items():
        ex.named(logical,name,'AudioClip')
        row=ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1',
                                    (name,'AudioClip')).fetchone()
        sources.append(dict(id=logical,name=name,type=row['type'],cab=row['cab'],pathid=row['pathid'],
                            bundle=row['bundle'],cache='local/runtime/'+ex.registry[logical],
                            usage='fan_cast' if logical=='SFX_FAN_CAST' else 'fan_impact'))
    ex.save()
    with asset_lock(ex.imp.local):
        path=ex.folder/'effect-index.json'
        effects=json.loads(path.read_text(encoding='utf-8'))
        effects['entries']=[e for e in effects['entries'] if e['card_id']!='EX1_129']
        effects['entries'].append(dict(card_id='EX1_129',start='',cast='SFX_FAN_CAST',hit='SFX_FAN_HIT',
                                      projectile='VFX_FAN_DAGGER',impact='VFX_FAN_TRAIL',kind='fan',cast_duration=.85))
        atomic_json(path,effects)
    atomic_json(PROJECT/'manifests/battle-pacing-assets.json',dict(assets=sources,card='EX1_129',reference=reference,
        research=research,notes=['刀扇原贴图、音轨及动画/粒子参数已恢复；客户端重实现扇形轨迹，不执行原控制脚本。']))
    if ex.errors:
        raise RuntimeError(ex.errors)
    print('sources',len(sources),'research',len(research),flush=True)


if __name__=='__main__':
    main()
