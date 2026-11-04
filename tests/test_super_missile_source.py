import json
from pathlib import Path

from tools.importer import Importer
from tools.super_missile_source import recover

ROOT = Path(__file__).parents[1]


def test_missile_configs_replay_exact_original_root_components():
    importer = Importer()
    report = json.loads((ROOT / 'manifests/original-super-missile-config.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 232 and not report['full_super_spell_restored']
    for record in report['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program) == record['config']


def test_missile_prefabs_are_distinct_source_graphs_with_native_audio():
    importer = Importer()
    from tools.native_effects import source_program_nodes
    report = json.loads((ROOT / 'manifests/original-super-missile-prefabs.json').read_text(encoding='utf-8'))
    assert len(report['prefabs']) == 2 and not report['full_super_spell_restored']
    for record in report['prefabs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(record['reference'])
        nodes, _, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in program['nodes']]
        assert program['source'] == record['source']['source']
        assert program['audio_file'] == record['audio_file'] and not record['audio_errors']
        assert any(m['owner'] == 0 and m['owner_known'] for m in program['machines'])
