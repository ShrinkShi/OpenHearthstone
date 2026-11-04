import json

from .export_runtime import RuntimeExport

ex = RuntimeExport()
path = ex.folder / 'registry.json'
if path.exists():
    ex.registry = {e['id']: e['path'] for e in json.loads(path.read_text())['entries']}
report = ex.folder / 'export-report.json'
if report.exists():
    data = json.loads(report.read_text(encoding='utf-8'))
    ex.provenance, ex.errors = data['provenance'], data['errors']
ex.scene('SCENE_BOARD', 'Board_STR', 'essential_base_qualityhigh_global-content-0.unity3d')
ex.scene('SCENE_CARD', 'Card_InHand_Ally', 'initial_base_global-c8eab65e-prefab-1.unity3d')
for pattern in ('%Card%Ally%', '%Card%Frame%', '%Gem%', '%Class%'):
    rows = ex.imp.store.db.execute('SELECT name FROM assets WHERE type="Texture2D" AND name LIKE ? AND bundle LIKE "essential%" LIMIT 35', (pattern,)).fetchall()
    print(pattern, [r[0] for r in rows])
ex.save()
