"""Replay source AudioPlaythrough controls and explicit SoundDef dependencies."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_scale_cache
from .native_audio import recover
from .asset_lock import asset_lock, atomic_json
from .importer import PROJECT
import argparse
import json


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--audio-only', action='store_true')
    parser.add_argument('--rebind-existing', action='store_true',
        help='Rebind already recovered audio after checking root and node identities; do not decode PCM again.')
    args = parser.parse_args()
    exporter = RuntimeExport()
    if not args.audio_only and not args.rebind_existing:
        refresh_scale_cache(exporter, {'AudioPlaythroughAction'}, 'playthrough-audio',
            patterns=('*.native-effect.json', '*.actor-spell.json', '*.start-spell.json', '*.area-spell.json'))
    report = json.loads((PROJECT / 'manifests/native-playthrough-audio-refresh.json').read_text(encoding='utf-8'))
    records = []
    recovered = {}
    fade_report = json.loads((PROJECT / 'manifests/native-audio-fade-bindings.json').read_text(encoding='utf-8'))
    original_bindings = {r['file']: r['audio_file'] for r in fade_report['programs']}
    existing_report_path = PROJECT / 'manifests/native-playthrough-audio-assets.json'
    existing_bindings = {r['file']: r['audio_file'] for r in json.loads(existing_report_path.read_text(encoding='utf-8'))['programs']} if args.rebind_existing else {}
    with asset_lock(exporter.imp.local):
        for row in report['programs']:
            path = exporter.folder / row['file']
            program = json.loads(path.read_text(encoding='utf-8'))
            reference = program['reference']
            if reference not in recovered:
                if args.rebind_existing:
                    cached = existing_bindings[row['file']]
                    audio = json.loads((exporter.folder / cached).read_text(encoding='utf-8'))
                    if audio['reference'] != reference or audio['source'] != program['source']:
                        raise ValueError('Cached source identity differs from program: ' + row['file'])
                    for node in audio['nodes']:
                        if not node.get('node_known') or program['nodes'][node['node']]['path'] != node['path']:
                            raise ValueError('Cached source node differs from program: ' + row['file'])
                    filename = original_bindings.get(row['file'], cached)
                else:
                    root, _, _ = exporter.imp.reader.resolve(reference)
                    filename = original_bindings.get(row['file'], program.get('audio_file') or exporter.key(root) + '.native-audio.json')
                    audio = recover(exporter, reference, include_program_hierarchy=True)
                atomic_json(exporter.folder / filename, audio)
                recovered[reference] = (filename, audio)
            filename, audio = recovered[reference]
            program['audio_file'] = filename
            atomic_json(path, program)
            records.append(dict(file=row['file'], audio_file=filename, nodes=len(audio['nodes']),
                clips=len(audio['clips']), errors=audio['errors']))
        atomic_json(PROJECT / 'manifests/native-playthrough-audio-assets.json',
            dict(programs=records, complete=False, notes=['Exact source audio dependencies, not whole-effect certification.']))
        audit_path = PROJECT / 'manifests/native-effect-audit.json'
        audit = json.loads(audit_path.read_text(encoding='utf-8'))
        programs = {p['reference']: json.loads((exporter.folder / p['file']).read_text(encoding='utf-8')) for p in report['programs']}
        for entry in audit['cards']:
            if entry['reference'] in programs:
                entry['unresolved_parameters'] = sum(bool(p.get('unresolved')) for m in programs[entry['reference']]['machines'] for s in m['states'] for a in s['actions'] for p in a['params'])
        atomic_json(audit_path, audit)
    print('PLAYTHROUGH AUDIO SOURCES', len(records), 'programs;', len(recovered), 'roots;',
        sum(len(r['errors']) for r in records), 'clip errors', flush=True)
