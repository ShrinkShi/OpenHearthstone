"""查看菜单材质的原 Shader/混合参数，不运行官方脚本。"""
from .importer import Importer


def main():
    imp = Importer()
    names = ('SpotLightHaze', 'TheBox_ButtonShadowsDark', 'TheBox_Frame_Shadow',
             'TheBox_Top', 'Title_Logo', 'TheBox_TurnRing')
    for name in names:
        row = imp.store.db.execute('SELECT * FROM assets WHERE type=? AND name=? ORDER BY bundle LIMIT 1',
                                   ('Material', name)).fetchone()
        if row is None:
            continue
        env = imp.reader.load(row['bundle'])
        obj = next(o for o in env.objects if o.assets_file.name == row['cab'] and str(o.path_id) == row['pathid'])
        tree = obj.read_typetree()
        shader = imp.reader.pointer(obj, tree['m_Shader'])
        shader_tree = shader.read_typetree() if shader else {}
        print(name, shader_tree.get('m_ParsedForm', {}).get('m_Name') or shader_tree.get('m_Name', ''),
              tree.get('m_CustomRenderQueue'), tree.get('m_ShaderKeywords'))
    rows = imp.store.db.execute('SELECT name FROM assets WHERE type=? AND bundle LIKE ?',
                                ('AudioClip', 'essential%')).fetchall()
    print('UI_AUDIO', sorted(set(r['name'] for r in rows if any(w in r['name'].lower() for w in
          ('button', 'menu', 'hover', 'ui_', 'click'))))[:80])


if __name__ == '__main__':
    main()
