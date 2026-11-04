import json
from .importer import PROJECT

folder = PROJECT / 'local/runtime'
for model in ['MODEL_HERO', 'MODEL_MINION']:
    for n in json.loads((folder / (model + '.scene.json')).read_text())['nodes']:
        if not n['mesh']:
            continue
        m = json.loads((folder / n['mesh']).read_text())
        for i, part in enumerate(m['submeshes']):
            used = sorted(set(part['triangles']))
            uv = [m['uv'][j*2:j*2+2] for j in used]
            xyz = [m['vertices'][j*3:j*3+3] for j in used]
            print(model, n['name'], i, 'uv', [min(v[k] for v in uv) for k in range(2)], [max(v[k] for v in uv) for k in range(2)], 'size', [max(v[k] for v in xyz)-min(v[k] for v in xyz) for k in range(3)])
def inspect_card_portrait_parts():
    for model in ['MODEL_CARD_MINION', 'MODEL_CARD_SPELL', 'MODEL_CARD_WEAPON']:
        data = json.loads((folder / (model + '.scene.json')).read_text())
        for node in data['nodes']:
            if 'portrait' not in node['name'].lower():
                continue
            mesh = json.loads((folder / node['mesh']).read_text())
            for j, submesh in enumerate(mesh['submeshes']):
                indices = set(submesh['triangles'])
                print(model, node['name'], j, 'bounds', [(min(mesh['vertices'][i*3+a] for i in indices), max(mesh['vertices'][i*3+a] for i in indices)) for a in range(3)], 'uv', [(min(mesh['uv'][i*2+a] for i in indices), max(mesh['uv'][i*2+a] for i in indices)) for a in range(2)])
