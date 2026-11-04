"""只读恢复英雄破碎和结算模型；运动由客户端时间线重实现。"""
import json

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT


MODELS = {'MODEL_RESULT_CROWN': 'Victory_Crown', 'MODEL_RESULT_BANNER': 'VictoryBanner'}
TEXTURES = {'VFX_HERO_CRACKS': 'HeroDeathCracks'}
AUDIO = {'SFX_HERO_SHATTER': 'GlassShatter_Underlay_Death', 'SFX_RESULT_START': 'victory_screen_start'}


def main():
    ex = RuntimeExport()
    sources = []
    for logical, name in MODELS.items():
        ex.scene(logical, name, 'essential_base_global-mesh-0.unity3d')
    for logical, name in TEXTURES.items():
        ex.named(logical, name)
    for logical, name in AUDIO.items():
        ex.named(logical, name, 'AudioClip')
    for logical, name in dict(MODELS, **TEXTURES, **AUDIO).items():
        kind = 'GameObject' if logical in MODELS else 'AudioClip' if logical in AUDIO else 'Texture2D'
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? ORDER BY bundle LIMIT 1', (name, kind)).fetchone()
        if logical in MODELS:
            row = ex.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? AND bundle=?',
                (name, kind, 'essential_base_global-mesh-0.unity3d')).fetchone()
        if row and logical in ex.registry:
            sources.append(dict(id=logical, asset_id=row['id'], name=name, type=kind, bundle=row['bundle'],
                cab=row['cab'], pathid=row['pathid'], guids=json.loads(row['guids']), cache='local/runtime/' + ex.registry[logical]))
    if ex.errors:
        raise RuntimeError(ex.errors)
    ex.save()
    atomic_json(PROJECT / 'manifests/lifecycle-assets.json', dict(assets=sources, motion='client_reimplementation'))
    print('LIFECYCLE original assets:', len(sources), flush=True)


if __name__ == '__main__':
    main()
