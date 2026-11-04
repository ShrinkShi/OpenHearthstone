"""用原版 AssetStudio DLL 对真实样本只读解析，不操作官方文件。"""
import json
import sys
from pathlib import Path

import clr

root = Path(r'E:\HS\AssetStudio.v0.16.0')
sys.path.append(str(root))
clr.AddReference(str(root / 'AssetStudio.dll'))
from AssetStudio import AssetsManager
from System import Array, String

manager = AssetsManager()
sample = str(Path(r'D:\Program Files (x86)\Hearthstone\Data\Win') / 'essential_base_box_global-texture-0.unity3d')
try:
    manager.LoadFiles(Array[String]([sample]))
    files = list(manager.assetsFileList)
    print(json.dumps({'sample': sample, 'files': len(files),
                      'objects': sum(len(list(f.Objects)) for f in files)}, ensure_ascii=False))
except Exception as exc:
    print(json.dumps({'sample': sample, 'error': str(exc)}, ensure_ascii=False))
