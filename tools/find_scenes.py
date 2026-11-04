import json

from .importer import Importer

imp = Importer()
for kind, pattern in [('GameObject', '%Board%'), ('GameObject', '%Game%'), ('Texture2D', '%Card%Ally%'),
                      ('Texture2D', '%Classic%'), ('Texture2D', '%TheBox%'), ('GameObject', 'Card_InHand%')]:
    rows = imp.store.db.execute('SELECT id,name,bundle,cab,pathid FROM assets WHERE type=? AND name LIKE ? '
                               'AND (bundle LIKE "essential%" OR bundle LIKE "initial_base%") ORDER BY length(name) LIMIT 75',
                               (kind, pattern)).fetchall()
    print(kind, pattern, json.dumps([list(r) for r in rows]))
