import json
import sqlite3

from .importer import Importer

imp = Importer()
db = imp.store.db
for word in ('Table', 'Orgrimmar', 'Pandaria', 'GameBoard', 'TheBox', 'Card_InHand', 'ManaTray'):
    rows = db.execute('SELECT id,name,bundle FROM assets WHERE type="GameObject" AND name LIKE ? '
                      'AND bundle LIKE "%base%" ORDER BY length(name) LIMIT 16', ('%' + word + '%',)).fetchall()
    print(word, json.dumps([list(r) for r in rows]))
rows = db.execute('SELECT id,name,bundle FROM assets WHERE type="GameObject" AND '
                  '(name LIKE "%board%" OR name LIKE "%wind%") AND length(name)<25 '
                  'ORDER BY length(name) LIMIT 60').fetchall()
print('BOARDS', json.dumps([list(r) for r in rows]))
for bundle in ('essential_base_box_global-prefab-0.unity3d', 'essential_base_global-prefab-0.unity3d'):
    env = imp.reader.load(bundle)
    out = []
    for obj in env.objects:
        if obj.type.name == 'Camera':
            out.append(obj.read_typetree())
    for obj in env.objects:
        if obj.type.name == 'GameObject' and obj.peek_name() == 'TheBoxCamera':
            tree = obj.read_typetree()
            for component in tree['m_Component']:
                child = imp.reader.pointer(obj, component['component'])
                if child and child.type.name == 'Transform':
                    print('CAMERA_TRANSFORM', json.dumps(child.read_typetree()))
