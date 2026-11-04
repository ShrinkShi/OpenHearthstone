import json

import pytest

from tools.migrate_shape_radius_cache import migrate_files, normalize_program, resolve_files


def _runtime(tmp_path):
    runtime = tmp_path / 'local' / 'runtime'
    runtime.mkdir(parents=True)
    return runtime


def _h02_radius():
    return {
        'value': 0.17000000178813934,
        'mode': 0,
        'spread': 0.0,
        'speed': {
            'minMaxState': 0,
            'scalar': 1.0,
            'minScalar': 1.0,
            'maxCurve': {'m_Curve': [], 'm_PreInfinity': 2, 'm_PostInfinity': 2, 'm_RotationOrder': 4},
            'minCurve': {'m_Curve': [], 'm_PreInfinity': 2, 'm_PostInfinity': 2, 'm_RotationOrder': 4},
        },
    }


def test_normalize_program_converts_wrapper_value_and_keeps_other_shape_fields():
    program = {'nodes': [{'particle': {'shape': {'radius': _h02_radius(), 'angle': 25, 'enabled': True}}}]}
    counts = normalize_program(program)
    assert counts == {'converted': 1, 'already_scalar': 0}
    assert program['nodes'][0]['particle']['shape'] == {'radius': _h02_radius()['value'], 'angle': 25, 'enabled': True}


def test_normalize_program_keeps_existing_float_cache_radius():
    program = {'nodes': [{'particle': {'shape': {'radius': .31}}}]}
    assert normalize_program(program) == {'converted': 0, 'already_scalar': 1}
    assert program['nodes'][0]['particle']['shape']['radius'] == .31


def test_card_id_migration_touches_only_explicitly_indexed_cache(tmp_path):
    runtime = _runtime(tmp_path)
    index = {'entries': [
        {'card_id': 'HERO_02bp', 'file': 'h02.json'},
        {'card_id': 'HERO_08bp', 'file': 'mage.json'},
    ]}
    (runtime / 'native-effect-index.json').write_text(json.dumps(index), encoding='utf-8')
    h02 = {'nodes': [{'particle': {'shape': {'radius': _h02_radius()}}}]}
    mage = {'nodes': [{'particle': {'shape': {'radius': _h02_radius()}}}]}
    h02_path = runtime / 'h02.json'
    mage_path = runtime / 'mage.json'
    h02_path.write_text(json.dumps(h02), encoding='utf-8')
    original_mage = json.dumps(mage)
    mage_path.write_text(original_mage, encoding='utf-8')

    report = migrate_files(runtime, card_ids=['HERO_02bp'])

    assert report == [{'file': 'h02.json', 'converted': 1, 'already_scalar': 0}]
    assert json.loads(h02_path.read_text(encoding='utf-8'))['nodes'][0]['particle']['shape']['radius'] == pytest.approx(.17)
    assert mage_path.read_text(encoding='utf-8') == original_mage
    assert resolve_files(runtime, card_ids=['HERO_02bp']) == [h02_path.resolve()]


def test_migration_requires_explicit_selection_and_rejects_lossy_data_without_writing(tmp_path):
    runtime = _runtime(tmp_path)
    with pytest.raises(ValueError, match='--files or --card-ids'):
        resolve_files(runtime)

    path = runtime / 'h02.json'
    data = {'nodes': [{'particle': {'shape': {'radius': _h02_radius()}}},
                      {'particle': {'shape': {'radius': {'value': .3, 'mode': 1, 'spread': 0}}}}]}
    original = json.dumps(data)
    path.write_text(original, encoding='utf-8')
    with pytest.raises(ValueError, match='ShapeModule.radius'):
        migrate_files(runtime, files=['h02.json'])
    assert path.read_text(encoding='utf-8') == original
