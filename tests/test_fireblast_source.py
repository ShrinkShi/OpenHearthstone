from types import SimpleNamespace

import pytest

from tools.fireblast_ui_assets import mesh_colors


def source(fmt, values):
    data = SimpleNamespace(m_VertexData=SimpleNamespace(m_Channels=[None, None, None, SimpleNamespace(format=fmt)]))
    return data, SimpleNamespace(m_Colors=[values])


def test_unorm8_channel_normalizes_source_bytes():
    values, encoding = mesh_colors(*source(2, [255, 0, 128, 255]))
    assert values == [1, 0, 128 / 255, 1]
    assert encoding == 'UNorm8_to_float'


def test_float_channel_preserves_values_without_range_guess():
    values, encoding = mesh_colors(*source(0, [255., .5, 0., 1.]))
    assert values == [255., .5, 0., 1.]
    assert encoding == 'Float32'


@pytest.mark.parametrize('fmt,values', [(2, [256, 0, 0, 255]), (2, [.5, 0, 0, 255]), (7, [1, 0, 0, 1])])
def test_unreviewed_source_color_domain_rejected(fmt, values):
    with pytest.raises(ValueError):
        mesh_colors(*source(fmt, values))
