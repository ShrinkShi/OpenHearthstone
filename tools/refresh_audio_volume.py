"""Recover original AudioSetVolume owner pointers and FsmFloat None flags."""
import json
import argparse
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import refresh_scale_cache

if __name__ == '__main__':
    exporter = RuntimeExport()
    parser = argparse.ArgumentParser()
    parser.add_argument('--extra-only', action='store_true')
    args = parser.parse_args()
    if args.extra_only:
        refresh_scale_cache(exporter, {'AudioSetVolumeAction'}, label='audio-volume-extra', patterns=('*.actor-spell.json', '*.start-spell.json'))
        raise SystemExit(0)
    refresh_scale_cache(exporter, {'AudioSetVolumeAction'}, label='audio-volume')
    with asset_lock(exporter.imp.local):
        report = json.loads((PROJECT / 'manifests/native-audio-volume-refresh.json').read_text(encoding='utf-8'))
        programs = {p['reference']: json.loads((exporter.folder / p['file']).read_text(encoding='utf-8')) for p in report['programs']}
        path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            if record['reference'] in programs:
                program = programs[record['reference']]
                record['unknown_actions'] = [name for name in record['unknown_actions'] if name != 'AudioSetVolumeAction']
                record['unresolved_parameters'] = sum(bool(p.get('unresolved')) for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
        atomic_json(path, audit)
