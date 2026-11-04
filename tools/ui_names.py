from .importer import Importer

imp = Importer()
for kind, pattern, bundle in [('GameObject', '%Card_InHand%', 'essential_base_global-mesh-0.unity3d'),
                              ('GameObject', '%Card_InPlay%', 'essential_base_global-mesh-0.unity3d'),
                              ('Texture2D', 'Card_Inhand%', 'essential%'),
                              ('Texture2D', '%Gem%', 'essential%'),
                              ('Texture2D', '%Hero%', 'essential%')]:
    names = imp.store.db.execute('SELECT DISTINCT name FROM assets WHERE type=? AND name LIKE ? AND bundle LIKE ? ORDER BY name', (kind, pattern, bundle)).fetchall()
    print(kind, pattern, [r[0] for r in names])
