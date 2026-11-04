import json

from .importer import Importer

imp = Importer()
out = imp.local / 'probes'
out.mkdir(exist_ok=True)
for card in ('CS2_029', 'HERO_08', 'HERO_01'):
    data = imp.reader.definition(card)
    (out / (card + '.json')).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    print(card, list(data.keys()))
env = imp.reader.load('dbf.unity3d')
for table in ('CARD', 'CARD_TAG'):
    data = env.container[f'Assets/Game/DBF-Asset/{table}.asset'].read_typetree()['Records']
    (out / (table + '.json')).write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
    print(table, len(data), data[0])
