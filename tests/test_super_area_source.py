import json
from pathlib import Path
from tools.importer import Importer
from tools.super_area_source import recover

ROOT = Path(__file__).parents[1]


def test_area_configuration_and_prefab_identity_replay_original_components():
    catalog = json.loads((ROOT / 'manifests/original-super-area-config.json').read_text(encoding='utf-8'))
    assert not catalog['full_super_spell_restored']
    importer = Importer()
    assert len(catalog['programs']) == 232
    for record in catalog['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program) == record['config']
        for info in (record['config']['friendly'], record['config']['opponent']):
            if info is not None and not info['prefab_null']:
                assert info['prefab_reference'] and info['prefab_source']['type'] == 'MonoBehaviour'
                assert info['prefab_node'] >= 0


def test_reviewed_area_programs_are_independent_source_prefabs_with_original_audio():
    report = json.loads((ROOT / 'manifests/original-super-area-prefabs.json').read_text(encoding='utf-8'))
    assert len(report['prefabs']) == 3 and not report['full_super_spell_restored']
    importer = Importer()
    for record in report['prefabs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert program['reference'] == record['reference']
        assert program['audio_file'] == record['audio_file'] and not record['audio_errors']
        root, _, _ = importer.reader.resolve(record['reference'])
        assert (root.assets_file.name, str(root.path_id)) == (program['source']['cab'], program['source']['pathid'])
        assert len(program['machines']) == 1 and program['machines'][0]['owner'] == 0
