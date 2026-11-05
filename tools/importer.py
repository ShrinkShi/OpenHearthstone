"""只读索引与按需导出；缓存永远写在项目 local 内。"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sqlite3
from collections import Counter
from pathlib import Path

from UnityPy.helpers import TypeTreeHelper
from UnityPy.helpers.TypeTreeNode import TypeTreeNode

from .vendor.unity import UnityReader
from .audio_cache import publish_audio, valid_audio_file

PROJECT = Path(__file__).resolve().parents[1]


def default_game_root(local=None):
    configured = os.environ.get('HEARTHSTONE_GAME_ROOT')
    if configured:
        return Path(configured)
    report = (local or PROJECT / 'local') / 'asset-audit.json'
    try:
        previous = Path(json.loads(report.read_text(encoding='utf-8'))['installation'])
        if (previous / 'Data' / 'Win' / 'asset_manifest.unity3d').is_file():
            return previous
    except (OSError, ValueError, KeyError, TypeError):
        pass
    return Path(os.environ.get('ProgramFiles(x86)', r'C:\Program Files (x86)')) / 'Hearthstone'


DEFAULT_GAME = default_game_root()


class Store:
    def __init__(self, path):
        path.parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(path)
        self.db.row_factory = sqlite3.Row
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE IF NOT EXISTS cabs(name TEXT PRIMARY KEY, bundle TEXT);
            CREATE TABLE IF NOT EXISTS bundles(name TEXT PRIMARY KEY, stamp TEXT, error TEXT);
            CREATE TABLE IF NOT EXISTS assets(
                id TEXT PRIMARY KEY, bundle TEXT, cab TEXT, pathid TEXT,
                type TEXT, name TEXT, guids TEXT, locale TEXT, metadata TEXT, error TEXT);
            CREATE INDEX IF NOT EXISTS assets_name ON assets(name);
            CREATE INDEX IF NOT EXISTS assets_type ON assets(type);
            CREATE TABLE IF NOT EXISTS exports(
                id TEXT PRIMARY KEY, stamp TEXT, path TEXT, sha256 TEXT, error TEXT);
        ''')

    def get_meta(self, key):
        row = self.db.execute('SELECT value FROM meta WHERE key=?', (key,)).fetchone()
        return json.loads(row[0]) if row else None

    def set_meta(self, key, value):
        with self.db:
            self.db.execute('INSERT OR REPLACE INTO meta VALUES (?,?)', (key, json.dumps(value)))


def header(obj, stop):
    root = obj._get_typetree_node()
    children = []
    for child in root.m_Children:
        children.append(child)
        if child.m_Name == stop:
            break
    prefix = TypeTreeNode(root.m_Level, root.m_Type, root.m_Name, -1, root.m_Version, children)
    obj.reset()
    return TypeTreeHelper.read_typetree(prefix, obj.reader, as_dict=True,
                                      assetsfile=obj.assets_file, check_read=False)


class Importer:
    def __init__(self, game=DEFAULT_GAME, local=None):
        self.game = Path(game).resolve()
        if not (self.game / 'Data' / 'Win' / 'asset_manifest.unity3d').is_file():
            raise FileNotFoundError(
                f'未找到游戏资源清单：{self.game}。请设置 HEARTHSTONE_GAME_ROOT 指向游戏安装目录，'
                '或向 importer 传入 --game；详见 docs/BUILD.md。')
        self.local = Path(local) if local else PROJECT / 'local'
        self.store = Store(self.local / 'asset-index.sqlite')
        manifests = sorted((self.game / 'Data/Win').glob('asset_manifest*.unity3d'))
        stamp = [[str(p), p.stat().st_size, p.stat().st_mtime_ns] for p in manifests]
        if self.store.get_meta('installation') != stamp:
            self.store.set_meta('manifest_v1', None)
            self.store.set_meta('installation', stamp)
        self.reader = UnityReader(self.game, self.store)

    def scan(self, pattern='*'):
        sources = self.reader.source_paths()
        selected = [(n, p) for n, p in sorted(sources.items()) if Path(n).match(pattern)]
        for i, (bundle, path) in enumerate(selected):
            stat = path.stat()
            stamp = f'v2:{stat.st_size}:{stat.st_mtime_ns}'
            old = self.store.db.execute('SELECT stamp,error FROM bundles WHERE name=?', (bundle,)).fetchone()
            if old and old['stamp'] == stamp and not old['error']:
                continue
            try:
                env = self.reader.load(bundle)
                guid_map = {}
                for key, ptr in env.container.items():
                    if not ptr.path_id:
                        continue
                    obj = ptr.deref()
                    guid_map.setdefault((obj.assets_file.name, obj.path_id), []).append(key)
                rows = []
                for obj in env.objects:
                    cab, pid, kind = obj.assets_file.name, str(obj.path_id), obj.type.name
                    asset_id = hashlib.sha256(f'{bundle}:{cab}:{pid}'.encode()).hexdigest()[:24]
                    metadata, error, name = {}, '', ''
                    try:
                        name = obj.peek_name() or ''
                        if kind == 'Texture2D':
                            data = header(obj, 'm_TextureFormat')
                            metadata = {k: data.get(k) for k in ('m_Width', 'm_Height', 'm_TextureFormat')}
                        elif kind == 'AudioClip':
                            data = header(obj, 'm_CompressionFormat')
                            metadata = {k: data.get(k) for k in ('m_Channels', 'm_Frequency', 'm_Length', 'm_CompressionFormat')}
                    except Exception as exc:
                        error = f'{type(exc).__name__}: {exc}'[:500]
                    locale = next(iter(re.findall(r'_(zhcn|enus|zhtw|kokr|jajp)[-_.]', bundle)), 'global')
                    rows.append((asset_id, bundle, cab, pid, kind, name,
                                 json.dumps(guid_map.get((cab, obj.path_id), [])), locale,
                                 json.dumps(metadata), error))
                with self.store.db:
                    self.store.db.execute('DELETE FROM assets WHERE bundle=?', (bundle,))
                    self.store.db.executemany('INSERT INTO assets VALUES (?,?,?,?,?,?,?,?,?,?)', rows)
                    self.store.db.execute('INSERT OR REPLACE INTO bundles VALUES (?,?,?)', (bundle, stamp, ''))
                    self.store.db.executemany('INSERT OR REPLACE INTO cabs VALUES (?,?)', self.reader.cabs.items())
            except Exception as exc:
                with self.store.db:
                    self.store.db.execute('INSERT OR REPLACE INTO bundles VALUES (?,?,?)', (bundle, stamp, str(exc)[:1000]))
            if i % 100 == 0 or i == len(selected) - 1:
                print(json.dumps({'scanned': i + 1, 'selected': len(selected), 'bundle': bundle}), flush=True)
        self.report()

    def query(self, text='', kind='', limit=30):
        return [dict(r) for r in self.store.db.execute(
            'SELECT * FROM assets WHERE (name LIKE ? OR guids LIKE ? OR bundle LIKE ?) '
            'AND (?="" OR type=?) ORDER BY name LIMIT ?',
            (f'%{text}%', f'%{text}%', f'%{text}%', kind, kind, limit))]

    def export(self, asset_id):
        row = self.store.db.execute('SELECT * FROM assets WHERE id=?', (asset_id,)).fetchone()
        if row is None:
            raise KeyError(asset_id)
        bundle = row['bundle']
        source = self.reader.source_paths()[bundle]
        stamp = f'{source.stat().st_size}:{source.stat().st_mtime_ns}'
        old = self.store.db.execute('SELECT * FROM exports WHERE id=?', (asset_id,)).fetchone()
        if old and old['stamp'] == stamp and old['path'] and (self.local / old['path']).exists():
            cached = self.local / old['path']
            if row['type'] != 'AudioClip' or valid_audio_file(cached):
                return cached
        cache = self.local / 'cache'
        cache.mkdir(exist_ok=True)
        try:
            env = self.reader.load(bundle)
            obj = next(o for o in env.objects if o.assets_file.name == row['cab'] and str(o.path_id) == row['pathid'])
            kind = row['type']
            if kind in ('Texture2D', 'Sprite'):
                path = cache / f'{asset_id}.png'
                obj.read().image.save(path)
            elif kind == 'AudioClip':
                samples = obj.read().samples
                if not samples:
                    raise ValueError('AudioClip decoded no samples')
                name, data = next(iter(samples.items()))
                path = cache / (asset_id + Path(name).suffix)
                publish_audio(path, data)
            else:
                path = cache / f'{asset_id}.json'
                path.write_text(json.dumps(obj.read_typetree(), ensure_ascii=False, default=str), encoding='utf-8')
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            duplicate = self.store.db.execute('SELECT path FROM exports WHERE sha256=? AND path<>""', (digest,)).fetchone()
            if duplicate and (self.local / duplicate[0]).exists() and self.local / duplicate[0] != path:
                path.unlink()
                path = self.local / duplicate[0]
            with self.store.db:
                self.store.db.execute('INSERT OR REPLACE INTO exports VALUES (?,?,?,?,?)',
                                      (asset_id, stamp, str(path.relative_to(self.local)), digest, ''))
            return path
        except Exception as exc:
            with self.store.db:
                self.store.db.execute('INSERT OR REPLACE INTO exports VALUES (?,?,?,?,?)', (asset_id, stamp, '', '', str(exc)))
            raise

    def report(self):
        db = self.store.db
        report = {'installation': str(self.game), 'source_bundles': len(self.reader.source_paths()),
                  'indexed_bundles': db.execute('SELECT count(*) FROM bundles WHERE error=""').fetchone()[0],
                  'failed_bundles': [dict(r) for r in db.execute('SELECT * FROM bundles WHERE error<>""')],
                  'types': dict(db.execute('SELECT type,count(*) FROM assets GROUP BY type')),
                  'metadata_errors': db.execute('SELECT count(*) FROM assets WHERE error<>""').fetchone()[0],
                  'exported': db.execute('SELECT count(*) FROM exports WHERE error=""').fetchone()[0],
                  'export_errors': [dict(r) for r in db.execute('SELECT * FROM exports WHERE error<>""')]}
        (self.local / 'asset-audit.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
        print(json.dumps(report, ensure_ascii=False), flush=True)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--game', default=str(DEFAULT_GAME))
    subs = p.add_subparsers(dest='command', required=True)
    scan = subs.add_parser('scan')
    scan.add_argument('--pattern', default='*')
    query = subs.add_parser('query')
    query.add_argument('text', nargs='?', default='')
    query.add_argument('--type', default='')
    query.add_argument('--limit', type=int, default=30)
    export = subs.add_parser('export')
    export.add_argument('id')
    subs.add_parser('report')
    args = p.parse_args()
    importer = Importer(args.game)
    if args.command == 'scan':
        importer.scan(args.pattern)
    elif args.command == 'query':
        print(json.dumps(importer.query(args.text, args.type, args.limit), ensure_ascii=False, indent=2))
    elif args.command == 'export':
        print(importer.export(args.id))
    else:
        importer.report()


if __name__ == '__main__':
    main()
