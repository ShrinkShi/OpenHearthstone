"""仅读取指定材质和卡牌模板，定位原始纹理槽和组件。"""
import json
from .importer import Importer


def inspect():
    imp = Importer()
    for name, kind in [('STR_FRAME', 'Material'), ('STR_Rainbow', 'Material'),
                       ('STR_RuinsGodRays', 'Material'), ('STR_WaterfallWater', 'GameObject')]:
        rows = imp.store.db.execute('SELECT * FROM assets WHERE name=? AND type=? LIMIT 1', (name, kind)).fetchall()
        for row in rows:
            env = imp.reader.load(row['bundle'])
            obj = next(o for o in env.objects if str(o.path_id) == row['pathid'] and o.assets_file.name == row['cab'])
            tree = obj.read_typetree()
            print(name, json.dumps(tree.get('m_SavedProperties', tree), default=str)[:12000])
    for pattern, kind in [('Card_InHand', 'Texture2D'), ('Card_InPlay', 'Texture2D'),
                          ('CardBack', 'Texture2D'), ('Mana', 'Texture2D'),
                          ('GenFX_Card_InHand_Ability', 'GameObject'), ('GenFX_Card_InHand_Weapon', 'GameObject'),
                          ('Card_Back', 'Texture2D'), ('Rarity', 'Texture2D'), ('Card_InPlay_Ally', 'GameObject'),
                          ('GenFX_Hero', 'GameObject')]:
        rows = imp.store.db.execute('SELECT DISTINCT name,bundle FROM assets WHERE name LIKE ? AND type=? LIMIT 28', ('%' + pattern + '%', kind)).fetchall()
        print(pattern, json.dumps([dict(r) for r in rows]))


if __name__ == '__main__':
    inspect()
