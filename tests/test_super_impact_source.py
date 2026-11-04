import json
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import source_program_nodes
from tools.super_impact_source import recover

ROOT = Path(__file__).parents[1]


def test_sample_impact_configs_replay_original_components_and_prefab_identity():
    importer = Importer()
    report = json.loads((ROOT / 'manifests/original-super-impact.json').read_text(encoding='utf-8'))
    assert not report['full_super_spell_restored']
    assert [r['card_id'] for r in report['programs']] == ['EX1_129', 'EX1_134']
    for record in report['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program) == record['config']
        impact = record['config']['impact']
        assert impact['enabled'] and impact['location'] == 6 and not impact['use_super_location']
        assert impact['ranges'] == []
        obj, _, _ = importer.reader.resolve(impact['prefab']['prefab_reference'])
        assert str(obj.path_id) != impact['prefab']['prefab_source']['pathid']  # GO and Spell component differ.


def test_sample_impact_clones_replay_source_hierarchy_fsm_and_native_audio():
    importer = Importer()
    report = json.loads((ROOT / 'manifests/original-super-impact.json').read_text(encoding='utf-8'))
    assert len(report['prefabs']) == 2
    for record in report['prefabs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(record['reference'])
        nodes, _, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in program['nodes']]
        assert program['source'] == record['source']['source']
        assert program['reference'] == record['reference']
        assert program['audio_file'] == record['audio_file'] and not record['audio_errors']
        audio = json.loads((ROOT / 'local/runtime' / record['audio_file']).read_text(encoding='utf-8'))
        assert not audio['errors']
        assert any(m['owner'] == 0 and m['owner_known'] for m in program['machines'])
        assert record['source']['errors'] == []
        root_fsm = next(m for m in program['machines'] if m['owner'] == 0 and m['owner_known'])
        assert next(s for s in root_fsm['states'] if s['name'] == root_fsm['start'])['actions'] == []
        assert program['nodes'][0]['active']
