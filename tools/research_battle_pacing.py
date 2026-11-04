"""只读查找武器、技能背面、牌库及刀扇的真实资源关联。"""
import argparse
import json

from .export_runtime import RuntimeExport


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--card', default='EX1_129')
    args = parser.parse_args()
    ex = RuntimeExport()
    for pattern in ('%Weapon%', '%HeroPower%', '%Deck%', '%FanOfKnives%'):
        rows = ex.imp.store.db.execute(
            'SELECT name,bundle,pathid FROM assets WHERE type=? AND name LIKE ? '
            'AND bundle LIKE ? GROUP BY name ORDER BY name',
            ('GameObject', pattern, 'essential%')).fetchall()
        print(pattern, [dict(r) for r in rows])
    definition = ex.imp.reader.definition(args.card)
    reference = definition.get('m_PlayEffectDef', {}).get('m_SpellPath', '')
    print('CARD', args.card, 'EFFECT', reference)
    root, _, _ = ex.imp.reader.resolve(reference)
    for obj, tree in ex.imp.reader.walk(root, include_visual=True):
        if obj.type.name not in ('AudioClip', 'Material', 'AnimationClip', 'ParticleSystem'):
            continue
        tree = tree or obj.read_typetree()
        print(obj.type.name, tree.get('m_Name', ''), obj.assets_file.name, obj.path_id)
        if obj.type.name == 'Material':
            print('MATERIAL', json.dumps(ex.material(obj), ensure_ascii=False))


if __name__ == '__main__':
    main()
