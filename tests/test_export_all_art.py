from types import SimpleNamespace

from PIL import Image

from tools.export_all_art import export_one


def test_metadata_export_preserves_identity_and_is_repeatable(tmp_path):
    obj = SimpleNamespace(type=SimpleNamespace(name='Material'),
                          assets_file=SimpleNamespace(name='CAB-example'), path_id=17,
                          read_typetree=lambda: {'m_Name': 'sample'})
    first = export_one(obj, tmp_path)
    path = tmp_path / 'Material' / first[0]
    assert path.read_text(encoding='utf-8') == '{"m_Name": "sample"}'
    assert export_one(obj, tmp_path) == first
    assert not list(tmp_path.rglob('*.tmp'))


def test_rgba64_keeps_original_data_and_produces_preview(tmp_path):
    raw = bytes([0, 10, 0, 20, 0, 30, 0, 255])
    texture = SimpleNamespace(m_Width=1, m_Height=1, m_CompleteImageSize=8,
                              m_TextureFormat=74, get_image_data=lambda: raw)
    obj = SimpleNamespace(type=SimpleNamespace(name='Texture2D'),
                          assets_file=SimpleNamespace(name='CAB-example'), path_id=23,
                          read=lambda: texture)
    files = export_one(obj, tmp_path)
    root = tmp_path / 'Texture2D'
    assert (root / files[0]).read_bytes() == raw
    with Image.open(root / files[1]) as image:
        assert image.getpixel((0, 0)) == (10, 20, 30, 255)
