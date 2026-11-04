from types import SimpleNamespace

import pytest

from tools.native_effects import texture_variables


@pytest.mark.parametrize('pointer,kind,path,expected', [
    ({'m_PathID': 0}, None, '', {'object_null': True, 'unresolved': False}),
    ({'m_PathID': 17}, 'Texture2D', 'source.png', {'object_null': False, 'unresolved': False}),
    ({'m_PathID': 17}, 'RenderTexture', '', {'object_null': False, 'unresolved': True}),
    ({'m_PathID': 17}, None, '', {'object_null': False, 'unresolved': True}),
    ({'m_PathID': 17}, 'Texture2D', '', {'object_null': False, 'unresolved': True}),
    (None, None, '', {'object_null': False, 'unresolved': True}),
])
def test_source_texture_variable_distinguishes_declared_null_and_missing_dependency(pointer, kind, path, expected):
    owner = object()
    class Reader:
        def pointer(self, actual_owner, actual_pointer):
            assert actual_owner is owner and actual_pointer is pointer
            return SimpleNamespace(type=SimpleNamespace(name=kind)) if kind else None
    result = texture_variables({'variables': {'textureVariables': [{'name': 'PortraitTexture', 'value': pointer}]}},
                               owner, Reader(), lambda target: {'asset_path': path, 'asset_source': {'pathid': '17'}})
    assert len(result) == 1 and result[0]['name'] == 'PortraitTexture'
    assert {key: result[0][key] for key in expected} == expected
    if kind == 'Texture2D':
        assert result[0]['source'] == {'pathid': '17'}


def test_source_texture_variable_empty_declarations_remain_empty():
    assert texture_variables({}, None, None, None) == []
