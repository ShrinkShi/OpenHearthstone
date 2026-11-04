"""Bounded read-only original Gameplay lighting and selected prefab transforms."""
import json
import hashlib
import argparse
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original-root', type=Path, default=Path(r'D:\Program Files (x86)\Hearthstone'))
parser.add_argument('--output', type=Path, default=ROOT / 'work/fireblast-source-review')
args = parser.parse_args()
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
from tools.vendor.unity import UnityReader


class ReadOnlyStore:
    def __init__(self):
        self.db = sqlite3.connect((ROOT / 'local/asset-index.sqlite').as_uri() + '?mode=ro', uri=True)
        self.db.row_factory = sqlite3.Row

    def get_meta(self, key):
        row = self.db.execute('SELECT value FROM meta WHERE key=?', (key,)).fetchone()
        return json.loads(row[0]) if row else None

    def set_meta(self, *args):
        raise RuntimeError('Read-only source research')


reader = UnityReader(args.original_root, ReadOnlyStore())


def identity(obj):
    return dict(bundle=reader.cabs[obj.assets_file.name.lower()], cab=obj.assets_file.name,
                pathid=str(obj.path_id), type=obj.type.name)


records = []
env = reader.load('@player/level3')
for obj in env.objects:
    if obj.type.name == 'RenderSettings':
        records.append(dict(source=identity(obj), raw=obj.read_typetree()))
    elif obj.type.name == 'Light':
        tree = obj.read_typetree()
        owner = reader.pointer(obj, tree['m_GameObject'])
        records.append(dict(source=identity(obj), raw=tree, owner=owner.read_typetree()))
    elif obj.type.name == 'MonoBehaviour':
        tree = obj.read_typetree()
        if 'm_DirectionalLight' in tree and 'm_AmbientColor' in tree:
            records.append(dict(source=identity(obj), raw=tree))

out = OUT / 'scene-lighting-source.json'
out.write_text(json.dumps(records, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps([dict(source=r['source'], name=r.get('owner', {}).get('m_Name'),
                       fields={k: v for k, v in r['raw'].items() if any(n in k.lower() for n in ('ambient', 'directional', 'color', 'intensity', 'type', 'enabled', 'culling'))})
                  for r in records], indent=2))

captures = []
for pathid in (-7966458462323023583, -7915262011419123579):
    env = reader.load('essential_base_global-prefab-0.unity3d')
    obj = env.get_cab('cab-69c88b234dfa427e02d1fcee9337ae34').objects[pathid]
    tree = obj.read_typetree()
    owner = reader.pointer(obj, tree['m_GameObject'])
    transform = next(reader.pointer(owner, item['component']) for item in owner.read_typetree()['m_Component']
                     if reader.pointer(owner, item['component']).type.name == 'Transform')
    chain = []
    while transform is not None:
        raw = transform.read_typetree()
        go = reader.pointer(transform, raw['m_GameObject'])
        chain.append(dict(source=identity(transform), name=go.read_typetree()['m_Name'], raw=raw))
        transform = reader.pointer(transform, raw['m_Father'])
    captures.append(dict(source=identity(obj), raw=tree, owner_chain=chain))
(OUT / 'capture-source.json').write_text(json.dumps(captures, indent=2), encoding='utf-8')
print('Fresh captures:', ', '.join(str(r['source']['pathid']) for r in captures))

art = []
for card_id in ('HERO_08bp', 'CS2_119', 'CS2_179'):
    definition = reader.definition(card_id)
    paths = {k: v for k, v in definition.items() if 'portrait' in k.lower() and isinstance(v, str)}
    entries = []
    for key, reference in paths.items():
        if not reference:
            continue
        obj, _, _ = reader.resolve(reference)
        if obj.type.name == 'Texture2D':
            raw = obj.read_typetree()
            image_data = obj.read().get_image_data()
            entries.append(dict(field=key, reference=reference, source=identity(obj),
                texture_data_bytes=len(image_data), texture_data_sha256=hashlib.sha256(image_data).hexdigest(), raw={k: raw[k] for k in
                ('m_Name','m_Width','m_Height','m_ColorSpace','m_TextureFormat','m_MipCount','m_TextureSettings')}))
    registry = json.loads((ROOT / 'local/runtime/registry.json').read_text(encoding='utf-8'))
    filename = next(entry['path'] for entry in registry['entries'] if entry['id'] == 'CARD_' + card_id)
    database = json.loads((ROOT / 'local/card-database.json').read_text(encoding='utf-8'))
    card = next(row for row in database['cards'] if row['CardId'] == card_id)
    art.append(dict(card_id=card_id, database={k: card[k] for k in ('CardId','Name','Attack','Health')},
        runtime_png=filename, runtime_png_sha256=hashlib.sha256((ROOT / 'local/runtime' / filename).read_bytes()).hexdigest(),
        actor_fields={k: v for k, v in definition.items() if any(s in k.lower() for s in ('actor','prefab','shader'))},
        paths=paths, textures=entries))
(OUT / 'portrait-source.json').write_text(json.dumps(art, indent=2), encoding='utf-8')
print(json.dumps(art, indent=2))
