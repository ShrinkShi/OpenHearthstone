"""Recover only the three shaders selected by this card's automatic RT planes."""
import hashlib
import argparse
import json
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original-root', type=Path, default=Path(r'D:\Program Files (x86)\Hearthstone'))
parser.add_argument('--output', type=Path, default=ROOT / 'work/fireblast-source-review')
args = parser.parse_args()
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
from tools.vendor.unity import UnityReader
from tools.inspect_compiled_shaders import programs, variant_bindings, disassemble


class Store:
    def __init__(self):
        self.db = sqlite3.connect((ROOT / 'local/asset-index.sqlite').as_uri() + '?mode=ro', uri=True)
        self.db.row_factory = sqlite3.Row

    def get_meta(self, key):
        row = self.db.execute('SELECT value FROM meta WHERE key=?', (key,)).fetchone()
        return json.loads(row[0]) if row else None

    def set_meta(self, *args):
        raise RuntimeError('Read only')


reader = UnityReader(args.original_root, Store())
wanted = {'Hidden/R2TAdditive', 'Hidden/R2TAlphaClip', 'Hidden/R2TAlphaClipBloom'}
found = []
for obj in reader.load('@player/resources.assets').objects:
    if obj.type.name != 'Shader':
        continue
    tree = obj.read_typetree()
    parsed = tree.get('m_ParsedForm', {})
    if parsed.get('m_Name') not in wanted:
        continue
    folder = OUT / parsed['m_Name'].replace('/', '_')
    folder.mkdir(exist_ok=True)
    (folder / 'parsed-form.json').write_text(json.dumps(parsed, indent=2), encoding='utf-8')
    record = dict(name=parsed['m_Name'], source=dict(bundle='@player/resources.assets', cab=obj.assets_file.name,
                  pathid=str(obj.path_id)), bindings=variant_bindings(parsed, []), programs=[])
    for platform, program in programs(obj.read()):
        if platform != 4:
            continue
        for index, sub in enumerate(program.m_SubPrograms):
            if sub is None:
                continue
            raw = bytes(sub.m_ProgramCode)
            start = raw.find(b'DXBC')
            if start < 0:
                continue
            size = int.from_bytes(raw[start+24:start+28], 'little')
            code = raw[start:start+size]
            assert len(code) == size
            (folder / f'4-{index}.asm.txt').write_text(disassemble(code), encoding='utf-8')
            record['programs'].append(dict(blob=index, bytes=len(code), sha256=hashlib.sha256(code).hexdigest(), hash_scope='exact_DXBC'))
    (folder / 'audit.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    found.append(record)
print(json.dumps(found, indent=2))
if {r['name'] for r in found} != wanted:
    raise RuntimeError('A required source RT shader was not found')
