import json
import sqlite3

db = sqlite3.connect('local/asset-index.sqlite')
for kind, words in [('Texture2D', ('table', 'board', 'frame', 'cardback', 'mana', 'TheBox')),
                    ('AudioClip', ('Main_Title', 'Duel', 'Draw', 'Impact')),
                    ('GameObject', ('GameBoard', 'TheBox', 'Stormwind', 'Board', 'CardBack', 'Neutral'))]:
    for word in words:
        rows = db.execute('SELECT id,name,bundle FROM assets WHERE type=? AND name LIKE ? ORDER BY name LIMIT 12',
                          (kind, '%' + word + '%')).fetchall()
        print(word, json.dumps(rows, ensure_ascii=False))
