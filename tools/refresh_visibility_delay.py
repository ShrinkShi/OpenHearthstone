"""Refresh actual delayed visibility parameters, including FsmFloat None."""
import json

from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import refresh_scale_cache

if __name__ == '__main__':
    exporter = RuntimeExport()
    refresh_scale_cache(exporter, {'SetVisibilityRecursiveDelayAction'}, label='visibility-delay')
    with asset_lock(exporter.imp.local):
        report = json.loads((PROJECT / 'manifests/native-visibility-delay-refresh.json').read_text(encoding='utf-8'))
        references = {p['reference'] for p in report['programs']}
        path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        for record in audit['cards']:
            if record['reference'] in references:
                record['unknown_actions'] = [name for name in record['unknown_actions']
                                             if name != 'SetVisibilityRecursiveDelayAction']
        atomic_json(path, audit)
