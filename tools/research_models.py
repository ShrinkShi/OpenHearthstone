"""查找共享 UI 模型与材质；只读取索引和本地按需导出结果。"""
import json
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
db = sqlite3.connect(ROOT / 'local/asset-index.sqlite')
for fragment in ['Gem_Mana', 'Gem_Attack', 'Gem_Health', 'HeroPower', 'Card_Back', 'Button']:
    rows = db.execute("SELECT DISTINCT name,bundle FROM assets WHERE type='GameObject' AND name LIKE ? LIMIT 15", ('%'+fragment+'%',)).fetchall()
    print(fragment, rows)
print('shared power', db.execute("SELECT DISTINCT name,bundle FROM assets WHERE type='GameObject' AND name LIKE '%Power%' AND bundle LIKE 'essential_base%' LIMIT 35").fetchall())
model = json.loads((ROOT / 'local/runtime/MODEL_CARD_MINION.scene.json').read_text())
print('card parts', [(n['name'], [m['name'] for m in n['materials']]) for n in model['nodes']])
from .importer import Importer
imp = Importer()
row = imp.store.db.execute("SELECT * FROM assets WHERE name='Card_Back_Default' AND type='GameObject'").fetchone()
env = imp.reader.load(row['bundle'])
obj = next(o for o in env.objects if str(o.path_id) == row['pathid'] and o.assets_file.name == row['cab'])
tree = obj.read_typetree()
for c in tree['m_Component']:
    comp = imp.reader.pointer(obj, c['component'])
    print('back component', comp.type.name, json.dumps(comp.read_typetree(), default=str)[:1700])
