"""用原版 AssetStudio DLL 对真实样本只读解析，不操作官方文件。"""
import json
import argparse
import sys
from pathlib import Path

import clr
from .importer import DEFAULT_GAME, PROJECT

parser = argparse.ArgumentParser()
parser.add_argument('--game', type=Path, default=DEFAULT_GAME)
parser.add_argument('--assetstudio', type=Path, default=PROJECT / 'tools/thirdparty/AssetStudio.v0.16.0')
args = parser.parse_args()
root = args.assetstudio
sys.path.append(str(root))
clr.AddReference(str(root / 'AssetStudio.dll'))
from AssetStudio import AssetsManager
from System import Array, String

manager = AssetsManager()
sample = str(args.game / 'Data/Win/essential_base_box_global-texture-0.unity3d')
try:
    manager.LoadFiles(Array[String]([sample]))
    files = list(manager.assetsFileList)
    print(json.dumps({'sample': sample, 'files': len(files),
                      'objects': sum(len(list(f.Objects)) for f in files)}, ensure_ascii=False))
    if not files:
        print('AssetStudio did not load this bundle; use the project UnityPy importer for newer formats.', file=sys.stderr)
        sys.exit(1)
except Exception as exc:
    print(json.dumps({'sample': sample, 'error': str(exc)}, ensure_ascii=False))
    sys.exit(1)
