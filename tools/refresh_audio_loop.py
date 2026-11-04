"""Replay both original looping audio actions and their typed controls."""
import json
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import refresh_scale_cache

if __name__ == '__main__':
    exporter = RuntimeExport()
    names = {'AudioLoopingStartAction', 'AudioLoopingEndAction'}
    refresh_scale_cache(exporter, names, label='audio-loop',
                        patterns=('*.native-effect.json', '*.actor-spell.json', '*.start-spell.json'))
    with asset_lock(exporter.imp.local):
        report = json.loads((PROJECT / 'manifests/native-audio-loop-refresh.json').read_text(encoding='utf-8'))
        programs = {p['reference']: json.loads((exporter.folder / p['file']).read_text(encoding='utf-8')) for p in report['programs']}
        path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            if record['reference'] in programs:
                program = programs[record['reference']]
                record['unknown_actions'] = [name for name in record['unknown_actions'] if name not in names]
                record['unresolved_parameters'] = sum(bool(p.get('unresolved')) for m in program['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
        atomic_json(path, audit)
