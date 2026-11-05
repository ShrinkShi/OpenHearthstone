"""检查已下载的运行时注册表及其相对文件引用。"""
import json
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[1] / 'assets' / 'runtime'
    entries = json.loads((root / 'registry.json').read_text(encoding='utf-8'))['entries']
    missing = []
    for entry in entries:
        relative = Path(entry['path'])
        if relative.is_absolute() or '..' in relative.parts or not (root / relative).is_file():
            missing.append(entry['path'])
    print('Runtime assets:', len(entries), 'missing or invalid:', len(missing))
    if missing:
        raise RuntimeError('Invalid runtime assets: ' + ', '.join(missing[:10]))


if __name__ == '__main__':
    main()
