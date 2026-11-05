"""将已验证的 7z 分卷按字节重新分块，不重新压缩素材。"""
import argparse
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--chunk-mib', type=int, default=200)
    args = parser.parse_args()
    if args.chunk_mib <= 0:
        parser.error('--chunk-mib must be positive')
    source = sorted(args.source.glob('openh-full-assets.7z.[0-9][0-9][0-9]'))
    if not source:
        raise RuntimeError('No source archive volumes found')
    expected = [f'openh-full-assets.7z.{i:03d}' for i in range(1, len(source) + 1)]
    if [path.name for path in source] != expected:
        raise RuntimeError('Source archive volumes are not contiguous')
    args.destination.mkdir(parents=True, exist_ok=True)
    if list(args.destination.glob('openh-full-v2.7z.*')):
        raise RuntimeError('Destination already contains archive volumes')
    limit = args.chunk_mib * 1024 * 1024
    entries = []
    target = None
    chunk_hash = None
    chunk_size = 0

    def finish():
        nonlocal target, chunk_size
        if target is None:
            return
        path = Path(target.name)
        target.close()
        entries.append({'name': path.name, 'size': chunk_size, 'sha256': chunk_hash.hexdigest()})
        print(f'Created {path.name}: {chunk_size} bytes', flush=True)
        target = None
        chunk_size = 0

    try:
        for source_path in source:
            with source_path.open('rb') as stream:
                while True:
                    if target is None:
                        path = args.destination / f'openh-full-v2.7z.{len(entries) + 1:03d}'
                        target = path.open('xb')
                        chunk_hash = hashlib.sha256()
                    block = stream.read(min(4 * 1024 * 1024, limit - chunk_size))
                    if not block:
                        break
                    target.write(block)
                    chunk_hash.update(block)
                    chunk_size += len(block)
                    if chunk_size == limit:
                        finish()
        if target is not None and chunk_size == 0:
            path = Path(target.name)
            target.close()
            target = None
            path.unlink()
        finish()
    finally:
        if target is not None:
            target.close()
    if sum(item['size'] for item in entries) != sum(path.stat().st_size for path in source):
        raise RuntimeError('Rechunked size does not match the source archive')
    (args.destination / 'full-chunks.json').write_text(json.dumps(entries, indent=2), encoding='utf-8')
    print(f'Created {len(entries)} volumes', flush=True)


if __name__ == '__main__':
    main()
