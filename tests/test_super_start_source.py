import json
from pathlib import Path
from tools.importer import Importer
from tools.super_start_source import recover

ROOT = Path(__file__).parents[1]


def test_start_configuration_replays_actual_original_components():
    report = json.loads((ROOT / 'manifests/original-super-start-config.json').read_text(encoding='utf-8'))
    assert len(report['programs']) == 232 and not report['full_super_spell_restored']
    importer = Importer()
    for record in report['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program) == record['config']
        start = record['config']['start']
        if start is not None and not start['prefab_null']:
            assert start['prefab_reference'] and start['prefab_node'] >= 0
            assert start['prefab_source']['type'] == 'MonoBehaviour'


def test_independent_start_prefab_has_authored_lifecycle_and_audio():
    report = json.loads((ROOT / 'manifests/original-super-start-prefabs.json').read_text(encoding='utf-8'))
    assert len(report['prefabs']) == 1 and not report['full_super_spell_restored']
    record = report['prefabs'][0]
    program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
    assert program['reference'] == record['reference']
    assert program['audio_file'] == record['audio_file'] and not record['audio_errors']
    assert program['spell_transforms'][0]['kind'] == 'Spell'
    assert program['spell_transforms'][0]['raw']['m_ZonesToDisable'] == []
    machine = next(m for m in program['machines'] if m['owner_known'] and m['owner'] == 0)
    assert {'Birth', 'Idle', 'Action', 'Death', 'None', 'Cancel'} <= {e['event'] for e in machine['events']}
