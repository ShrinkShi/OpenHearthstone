"""Refresh original typed color-array spans and source action parameters."""
import json
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import refresh_scale_cache

if __name__ == '__main__':
    exporter = RuntimeExport()
    refresh_scale_cache(exporter, {'ColorInterpolate'}, label='color-interpolate')
    with asset_lock(exporter.imp.local):
        report = json.loads((PROJECT / 'manifests/native-color-interpolate-refresh.json').read_text(encoding='utf-8'))
        references = {p['reference'] for p in report['programs']}
        programs = {p['reference']: json.loads((exporter.folder / p['file']).read_text(encoding='utf-8'))
                    for p in report['programs']}
        path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            if record['reference'] in references:
                record['unknown_actions'] = [name for name in record['unknown_actions'] if name != 'ColorInterpolate']
                record['unresolved_parameters'] = sum(bool(p.get('unresolved'))
                    for m in programs[record['reference']]['machines'] for s in m['states']
                    for a in s['actions'] for p in a['params'])
        atomic_json(path, audit)
