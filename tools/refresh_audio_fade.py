"""Replay original fade source, timing, variable and stop controls."""
import json
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import refresh_scale_cache

if __name__ == '__main__':
    exporter = RuntimeExport()
    refresh_scale_cache(exporter, {'AudioVolumeFadeAction'}, label='audio-fade',
                        patterns=('*.native-effect.json', '*.actor-spell.json', '*.start-spell.json'))
    with asset_lock(exporter.imp.local):
        report = json.loads((PROJECT / 'manifests/native-audio-fade-refresh.json').read_text(encoding='utf-8'))
        programs = {p['reference']: json.loads((exporter.folder / p['file']).read_text(encoding='utf-8')) for p in report['programs']}
        path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            if record['reference'] in programs:
                program = programs[record['reference']]
                record['unknown_actions'] = [name for name in record['unknown_actions'] if name != 'AudioVolumeFadeAction']
                record['unresolved_parameters'] = sum(bool(p.get('unresolved')) for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
        atomic_json(path, audit)
