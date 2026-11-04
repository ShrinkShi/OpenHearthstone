"""对固定分辨率的实际客户端截图执行像素与感知回归比较。"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageStat


def dhash(image):
    small = image.convert('L').resize((9, 8), Image.Resampling.LANCZOS)
    values = list(small.get_flattened_data())
    return sum((values[y*9+x] > values[y*9+x+1]) << (y*8+x) for y in range(8) for x in range(8))


def compare(reference, actual):
    if reference.size != actual.size:
        raise ValueError(f'resolution differs: {reference.size} / {actual.size}')
    difference = ImageChops.difference(reference.convert('RGB'), actual.convert('RGB'))
    return difference, {'size': reference.size, 'mean_absolute_error': sum(ImageStat.Stat(difference).mean)/3,
                        'perceptual_dhash_distance': (dhash(reference)^dhash(actual)).bit_count(),
                        'different_pixels_bbox': difference.getbbox()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('reference', type=Path)
    parser.add_argument('actual', type=Path)
    parser.add_argument('--output', type=Path, default=Path('local/visual/diff'))
    args = parser.parse_args()
    difference, report = compare(Image.open(args.reference), Image.open(args.actual))
    args.output.mkdir(parents=True, exist_ok=True)
    difference.save(args.output / 'difference.png')
    (args.output / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
