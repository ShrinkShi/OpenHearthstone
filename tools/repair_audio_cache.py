"""Audit runtime audio and re-decode invalid exports from the read-only install."""
import hashlib
import json

from .asset_lock import asset_lock, atomic_json
from .audio_cache import publish_audio, valid_audio_file
from .importer import Importer, PROJECT


def main():
    imp = Importer()
    folder = imp.local / 'runtime'
    registry = json.loads((folder / 'registry.json').read_text(encoding='utf-8'))['entries']
    repairs, checked = [], set()
    with asset_lock(imp.local):
        for entry in registry:
            target = folder / entry['path']
            if target.suffix.lower() not in ('.wav', '.ogg', '.m4a') or target in checked:
                continue
            checked.add(target)
            if valid_audio_file(target):
                continue
            row = imp.store.db.execute('SELECT * FROM assets WHERE id=? AND type="AudioClip"',
                                       (target.stem,)).fetchone()
            if row is None:
                raise ValueError('Invalid audio has no indexed source: ' + str(target))
            before = hashlib.sha256(target.read_bytes()).hexdigest() if target.exists() else ''
            source = imp.export(row['id'])
            publish_audio(target, source.read_bytes())
            repairs.append(dict(asset_id=row['id'], name=row['name'], bundle=row['bundle'],
                cab=row['cab'], pathid=row['pathid'], guids=json.loads(row['guids']),
                cache='local/runtime/' + target.name, before_sha256=before,
                after_sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
        manifest = PROJECT / 'manifests/audio-cache-repairs.json'
        history = json.loads(manifest.read_text(encoding='utf-8'))['repairs'] if manifest.exists() else []
        known = {(r['asset_id'], r['before_sha256']) for r in history}
        history.extend(r for r in repairs if (r['asset_id'], r['before_sha256']) not in known)
        atomic_json(manifest, dict(repairs=history))
    print('AUDIO audit:', len(checked), 'files;', len(repairs), 'repaired')


if __name__ == '__main__':
    main()
