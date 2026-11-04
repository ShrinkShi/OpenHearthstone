import pytest
from PIL import Image, ImageDraw
from tools.visual_diff import compare


def test_visual_comparison_detects_reversed_content():
    image = Image.new('RGB', (128, 192), '#473622')
    draw = ImageDraw.Draw(image)
    draw.rectangle((24, 10, 105, 95), fill='#aadeff')
    draw.ellipse((38, 120, 87, 175), fill='#ffcc44')
    _, same = compare(image, image.copy())
    assert same['mean_absolute_error'] == 0
    assert same['perceptual_dhash_distance'] == 0
    _, inverted = compare(image, image.transpose(Image.Transpose.FLIP_TOP_BOTTOM))
    assert inverted['mean_absolute_error'] > 20
    assert inverted['perceptual_dhash_distance'] > 10


def test_visual_comparison_rejects_different_resolution():
    with pytest.raises(ValueError, match='resolution'):
        compare(Image.new('RGB', (16, 16)), Image.new('RGB', (20, 20)))
