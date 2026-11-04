"""读取固定历史快照，保留上市时死亡骑士 ID、数值、符文和文字。"""
import json
from xml.etree import ElementTree
from hearthstone import cardxml
from hearthstone.enums import GameTag
from .importer import PROJECT

db, version = cardxml.load(path=str(PROJECT / 'local/historical-159202.xml'), locale='zhCN')
selected = [c for c in db.values() if int(c.card_class) == 1 and c.collectible and int(c.card_set) in (1637, 1869)]
if not selected:
    print('DK sets', sorted(set(int(c.card_set) for c in db.values() if int(c.card_class) == 1 and c.collectible)))
    selected = [c for c in db.values() if int(c.card_class) == 1 and c.collectible]
for c in selected:
    print(c.id, int(c.card_set), c.cost, c.atk, c.health, c.name, c.description, {k.name: v for k, v in c.tags.items() if hasattr(k, 'name') and 'RUNE' in k.name})
for c in db.values():
    if (c.id.startswith('RLK_') and not c.collectible and int(c.card_class) == 1) or c.id in ('HERO_11', 'HERO_11bp', 'HERO_11bpt'):
        print('TOKEN', c.id, c.cost, c.atk, c.health, c.name, c.description, {getattr(k, 'name', str(k)): v for k, v in c.tags.items() if v and ('CORPSE' in getattr(k, 'name', '') or 'HERO_POWER' in getattr(k, 'name', ''))})
print('COUNT', len(selected), 'version', version)
root = ElementTree.parse(PROJECT / 'local/historical-159202.xml').getroot()
node = root.find("Entity[@CardID='RLK_015']")
print('RAW', ElementTree.tostring(node, encoding='unicode')[-2200:])
for c in db.values():
    if c.id.startswith(('RLK_039', 'RLK_018', 'RLK_118', 'RLK_063', 'HERO_11')):
        print('RELATED', c.id, int(c.card_class), c.name, c.description)
    if c.name == '亡灵农夫':
        print('FARMER', c.id)
