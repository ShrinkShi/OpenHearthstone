from types import SimpleNamespace

import pytest

from tools.native_effects import refresh_program_assets


@pytest.mark.parametrize('cab,pathid,expected', [('source', 17, 3), ('other', 17, -1), ('source', 19, -1)])
def test_custom_spawn_refresh_resolves_exact_source_owner_without_defaulting_to_root(cab, pathid, expected):
    class Owner:
        def read_typetree(self):
            return {'m_GameObject': {'m_PathID': pathid}, 'fsm': {'states': [], 'variables': {}}}
    owner = Owner()
    class Reader:
        def pointer(self, actual_owner, pointer):
            assert actual_owner is owner and pointer['m_PathID'] == pathid
            return SimpleNamespace(assets_file=SimpleNamespace(name=cab), path_id=pathid)
    source = {'cab': cab, 'pathid': str(pathid)}
    program = {'machines': [{'source': source, 'states': []}]}
    count = refresh_program_assets(program, lambda value: owner, Reader(), None,
                                   {'InitializeCustomSpawn', 'FinalizeCustomSpawn'}, {('source', 17): 3})
    assert count == 0
    assert program['machines'][0]['source'] == source
    assert program['machines'][0]['owner'] == expected
    assert program['machines'][0]['owner_known'] == (expected >= 0)
