"""只读调查交互音效、触发标记和指定卡牌的效果关联。"""
import argparse
import json
from .importer import Importer


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--cards', default='HERO_08bp,EX1_402,CS2_029,CS2_024,CS2_023')
    args = parser.parse_args()
    imp = Importer()
    for label, kind, patterns in [
        ('audio', 'AudioClip', ['FX_%Hover%', 'FX_%Select%', 'FX_%Pickup%', '%Fireblast%', '%FireballEvent%', '%StartingHand%', '%Card_Mouse%', '%Card_Pick%', '%SwapCard%']),
        ('marker', 'GameObject', ['%Trigger%']),
        ('textures', 'Texture2D', ['%Trigger%', '%Frostbolt%']),
    ]:
        rows = imp.store.db.execute('SELECT name,bundle,cab,pathid FROM assets WHERE type=? AND (' +
            ' OR '.join('name LIKE ?' for _ in patterns) + ') LIMIT 100', [kind, *patterns]).fetchall()
        print(label, json.dumps([r['name'] for r in rows], ensure_ascii=False))
    for card_id in args.cards.split(','):
        definition = imp.reader.definition(card_id)
        effects = {key: value for key, value in definition.items() if 'Effect' in key or 'Trigger' in key}
        print(card_id, json.dumps(effects, ensure_ascii=False))


if __name__ == '__main__':
    main()
