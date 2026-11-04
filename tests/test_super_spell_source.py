import json
import hashlib
from pathlib import Path

from tools.importer import Importer
from tools.native_effects import source_program_nodes
from tools.super_spell_source import recover

ROOT = Path(__file__).parents[1]


def test_two_root_branches_match_readonly_original_components():
    importer = Importer()
    report = json.loads((ROOT / 'manifests/original-super-spell.json').read_text(encoding='utf-8'))
    assert not report['official_geometry_verified'] and not report['whole_effect_verified']
    assert [r['card_id'] for r in report['programs']] == ['EX1_129', 'EX1_134']
    for record in report['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert recover(importer, program, record['card_id'], record['file']) == record
        assert not record['start_enabled'] and not record['chain_available'] and not record['reverse_available']
        assert record['target_behavior'] == 0 and record['missile']['m_TimesToHitSameTarget'] == 0
        assert record['missile']['m_SpawnInSequence'] == (record['card_id'] == 'EX1_134')
        assert not [m for m in program['machines'] if m['owner_known'] and m['owner'] == 0]
        assert not [s for s in program.get('spell_states', []) if s['node'] == 0 and s['enabled']]


def test_root_catalog_references_independent_source_prefabs():
    importer = Importer()
    report = json.loads((ROOT / 'manifests/original-super-spell.json').read_text(encoding='utf-8'))
    assert len(report['prefabs']) == len({p['reference'] for p in report['prefabs']}) == 6
    assert len(report['trail_textures']) == 3
    for texture in report['trail_textures']:
        source = texture['source']
        original = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
        assert hashlib.sha256(original.read().get_image_data()).hexdigest() == texture['sha256']
        assert original.read_typetree()['m_TextureFormat'] == texture['format']
    for record in report['prefabs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        obj, _, _ = importer.reader.resolve(record['reference'])
        nodes, _, _ = source_program_nodes(importer.reader, obj)
        assert nodes == [(n['name'], n['parent'], n['path']) for n in program['nodes']]
        assert program['reference'] == record['reference'] and program['source'] == record['source']['source']
        assert not record['source']['errors']
        for node in program['nodes']:
            trail = node.get('trail')
            if not trail:
                continue
            source = trail['source']
            original = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
            assert trail['raw'] == original.read_typetree()
            assert node['materials'] and trail['alignment'] == trail['texture_mode'] == 0
            assert not trail['autodestruct'] and trail['texture_scale'] == dict(x=1.0, y=1.0)
