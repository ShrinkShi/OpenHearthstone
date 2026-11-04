"""按需恢复悬停技能卡与历史图标框，官方安装只读。"""
import json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .asset_lock import asset_lock, atomic_json

ASSETS = {
    'MODEL_POWER_DETAIL': ('Lettuce_Ability_Frame', 'essential_base_global-mesh-0.unity3d'),
    'MODEL_HISTORY_CARD': ('HistoryTile_Card', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_HISTORY_ATTACK': ('HistoryTile_Attack', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_GEM_ARMOR': ('Hero_Armor', 'essential_base_global-prefab-0.unity3d'),
    'MODEL_GEM_DURABILITY': ('Wep_SheildBroken', 'essential_base_global-mesh-0.unity3d'),
}


def main():
    exporter = RuntimeExport()
    sources = []
    exporter.named('UI_POWER_DETAIL_FRAME', 'Lettuce_Ability_SpellCaster_Frame')
    texture = exporter.imp.store.db.execute(
        'SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1',
        ('Lettuce_Ability_SpellCaster_Frame', 'Texture2D')).fetchone()
    sources.append(dict(id='UI_POWER_DETAIL_FRAME', name=texture['name'],
                        bundle=texture['bundle'], pathid=texture['pathid'], cab=texture['cab'],
                        guids=json.loads(texture['guids'] or '[]'),
                        type='Texture2D', usage='runtime',
                        cache='local/runtime/' + exporter.registry['UI_POWER_DETAIL_FRAME']))
    for asset_id, (name, bundle) in ASSETS.items():
        exporter.scene(asset_id, name, bundle)
        row = exporter.imp.store.db.execute(
            'SELECT * FROM assets WHERE name=? AND bundle=? AND type=? LIMIT 1',
            (name, bundle, 'GameObject')).fetchone()
        sources.append(dict(id=asset_id, name=name, bundle=bundle, pathid=row['pathid'],
                            cab=row['cab'], guids=json.loads(row['guids'] or '[]'), type='GameObject', usage='runtime',
                            cache=f'local/runtime/{asset_id}.scene.json'))
    portraits = []
    for key in sorted(exporter.registry):
        if not key.startswith('CARD_'):
            continue
        card_id = key[5:]
        definition = exporter.imp.reader.definition(card_id)
        item = dict(card_id=card_id)
        for field, variant in [('m_HistoryTileFullPortraitPath', 'full'),
                               ('m_HistoryTileHalfPortraitPath', 'half')]:
            if not definition.get(field):
                continue
            obj, _, _ = exporter.imp.reader.resolve(definition[field])
            props = obj.read_typetree()['m_SavedProperties']
            values = dict(props.get('m_Floats', []))
            item[variant] = dict(scale=values.get('_Scale', 1), x=values.get('_OffsetX', 0),
                                 y=values.get('_OffsetY', 0), source_cab=obj.assets_file.name,
                                 source_pathid=str(obj.path_id), source_guid=definition[field].split(':')[-1])
        portraits.append(item)
    with asset_lock(exporter.imp.local):
        atomic_json(exporter.folder / 'history-portraits.json', dict(entries=portraits))
    exporter.save()
    (PROJECT / 'manifests/interaction-assets.json').write_text(
        json.dumps(dict(assets=sources), ensure_ascii=False, indent=2), encoding='utf-8')
    if exporter.errors:
        raise RuntimeError(exporter.errors)
    print('Imported', len(sources), 'interaction assets,', len(portraits), 'history portrait parameters')


if __name__ == '__main__':
    main()
