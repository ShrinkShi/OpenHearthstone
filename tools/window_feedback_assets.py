"""恢复全局机制图标，并记录来源；不修改官方客户端。"""
import json

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT

ICONS = {'UI_MINION_DEATHRATTLE': 'GenFX_Deathrattle_Skull', 'UI_MINION_POISONOUS': 'Poisonous'}
MODELS = {'MODEL_MINION_DEATHRATTLE': ('Card_Play_Ally_DeathRattle_Idle', 'initial_base_global-2ebe64b8-prefab-4.unity3d'),
          'MODEL_MINION_POISONOUS': ('Poisonous', 'initial_base_global-45e37529-mesh-0.unity3d')}


def main():
    ex = RuntimeExport()
    sources = []
    for logical, name in ICONS.items():
        ex.named(logical, name)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1',
                                     (name, 'Texture2D')).fetchone()
        sources.append(dict(id=logical, name=name, type=row['type'], bundle=row['bundle'], cab=row['cab'],
                            pathid=row['pathid'], guids=json.loads(row['guids']),
                            cache='local/runtime/' + ex.registry[logical]))
    for logical, (name, bundle) in MODELS.items():
        ex.scene(logical, name, bundle)
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=? LIMIT 1',
                                     (name, 'GameObject', bundle)).fetchone()
        sources.append(dict(id=logical, name=name, type=row['type'], bundle=bundle, cab=row['cab'],
                            pathid=row['pathid'], guids=json.loads(row['guids']), cache='local/runtime/' + ex.registry[logical]))
    if ex.errors:
        raise RuntimeError(ex.errors)
    ex.save()
    atomic_json(PROJECT / 'manifests/window-feedback-assets.json', dict(assets=sources))
    print('Imported original status textures:', len(sources), flush=True)


if __name__ == '__main__':
    main()
