"""只读调查棋盘控制、菜单、提示框和粒子资源；按需导出而不扫描整包缓存。"""
import json
import argparse

from .export_runtime import RuntimeExport


def main():
    export = RuntimeExport()
    parser = argparse.ArgumentParser()
    parser.add_argument('--inspect', nargs='*')
    args = parser.parse_args()
    if args.inspect:
        for name in args.inspect:
            rows = export.imp.store.db.execute('SELECT * FROM assets WHERE name=? AND bundle LIKE "essential%" ORDER BY bundle', (name,)).fetchall()
            for row in rows[:2]:
                objects = export.imp.reader.load(row['bundle']).objects
                obj = next(o for o in objects if str(o.path_id) == row['pathid'] and o.assets_file.name == row['cab'])
                data = obj.read_typetree()
                path = export.imp.local / (name + '.' + row['type'] + '.audit.json')
                path.write_text(json.dumps(data, ensure_ascii=False, default=str), encoding='utf-8')
                print(name, row['type'], row['bundle'], row['pathid'], list(data), str(path))
                if obj.type.name == 'AnimationClip':
                    print('CURVES', [(key, [(c.get('path'), c.get('attribute'), len(c['curve']['m_Curve']), c['curve']['m_Curve'][0], c['curve']['m_Curve'][-1]) for c in data.get(key,[])]) for key in ('m_RotationCurves','m_PositionCurves','m_ScaleCurves','m_FloatCurves')])
                if obj.type.name == 'GameObject':
                    children = []
                    for child, tree in export.imp.reader.walk(obj, include_visual=True):
                        if tree is None:
                            tree = child.read_typetree()
                        if child.type.name in ('Texture2D','Material','ParticleSystem','AnimationClip','Mesh','Camera'):
                            children.append((child.type.name, tree.get('m_Name',''), child.path_id))
                        if child.type.name == 'MonoBehaviour':
                            print('BEHAVIOUR', {k: v for k,v in tree.items() if k not in ('m_GameObject','m_EditorClassIdentifier','m_Name','m_Script') and ('menu' in k.lower() or 'button' in k.lower() or 'frame' in k.lower() or 'background' in k.lower())})
                            if name=='GameMenu':
                                print('MENU FIELDS', tree)
                    print('DEPENDENCIES', children[:65])
        return
    patterns = ('%mana%',)
    for pattern in patterns:
        rows = export.imp.store.db.execute(
            'SELECT name,type,bundle,pathid,id FROM assets WHERE '
            'type="GameObject" AND bundle LIKE "essential%" AND name LIKE ? '
            'ORDER BY name', (pattern,)).fetchall()
        unique = {r['name'] + ':' + r['type']: dict(r) for r in rows}
        print(pattern, ', '.join(unique)[-8000:])
    board = json.loads((export.folder / 'SCENE_BOARD.scene.json').read_text(encoding='utf-8'))
    print('BOARD', json.dumps([{k: n.get(k) for k in ('name','mesh','position','rotation','camera')}
                              for n in board['nodes'] if n.get('camera') or any(
                                  x in n['name'].lower() for x in ('turn', 'menu', 'camera', 'play'))], ensure_ascii=False))


if __name__ == '__main__':
    main()
