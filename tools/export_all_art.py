"""按资源包导出可解码的美术对象；保留来源和失败清单，可重复执行。"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.helpers.ResourceReader import get_resource_data
from UnityPy.export.Texture2DConverter import parse_image_data
from PIL import Image
import texture2ddecoder

from .export_runtime import mesh_data
from .importer import Importer, PROJECT


TYPES = {'Texture2D', 'Texture2DArray', 'Sprite', 'AudioClip', 'Mesh', 'Material', 'AnimationClip',
         'Shader', 'TextAsset', 'VideoClip', 'Font', 'Cubemap'}


def preserve_failed_object(obj, directory):
    identity = hashlib.sha256(f'{obj.assets_file.name}:{obj.path_id}'.encode()).hexdigest()[:24]
    target = directory / obj.type.name
    target.mkdir(parents=True, exist_ok=True)
    path = target / (identity + '.unity-object')
    temporary = path.with_name(path.name + '.tmp')
    try:
        temporary.write_bytes(obj.get_raw_data())
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
    files = [path.name]
    if obj.type.name in ('Texture2D', 'Cubemap'):
        try:
            data = obj.read().get_image_data()
            if data:
                path = target / (identity + '.texturedata')
                temporary = path.with_name(path.name + '.tmp')
                try:
                    temporary.write_bytes(data)
                    temporary.replace(path)
                finally:
                    temporary.unlink(missing_ok=True)
                files.append(path.name)
        except Exception:
            pass
    return files


def export_one(obj, directory):
    kind = obj.type.name
    identity = hashlib.sha256(f'{obj.assets_file.name}:{obj.path_id}'.encode()).hexdigest()[:24]
    target = directory / kind / identity
    target.parent.mkdir(parents=True, exist_ok=True)
    if kind == 'Font':
        data = bytes(obj.read().m_FontData)
        if not data:
            raise ValueError('font has no embedded data')
        suffix = '.otf' if data[:4] == b'OTTO' else '.ttf' if data[:4] in (b'\x00\x01\x00\x00', b'true') else '.font'
        path = target.with_suffix(suffix)
        if not path.exists():
            temporary = path.with_name(path.name + '.tmp')
            try:
                temporary.write_bytes(data)
                temporary.replace(path)
            finally:
                temporary.unlink(missing_ok=True)
        return [path.name]
    if kind == 'Texture2DArray':
        files = []
        for index, image in enumerate(obj.read().images):
            path = target.parent / (identity + '-' + str(index) + '.png')
            if not path.exists():
                temporary = path.with_name(path.name + '.tmp')
                try:
                    image.save(temporary, format='PNG')
                    temporary.replace(path)
                finally:
                    temporary.unlink(missing_ok=True)
            files.append(path.name)
        if not files:
            raise ValueError('texture array has no images')
        return files
    if kind == 'Cubemap':
        cube = obj.read()
        data = cube.get_image_data()
        size = int(cube.m_CompleteImageSize)
        if cube.m_ImageCount != 6 or size <= 0 or len(data) != 6 * size:
            raise ValueError('unrecognized cubemap layout')
        files = []
        if int(cube.m_TextureFormat) == 24:
            raw = target.parent / (identity + '.texturedata')
            if not raw.exists():
                raw.write_bytes(data)
            files.append(raw.name)
        for index in range(6):
            path = target.parent / (identity + '-' + str(index) + '.png')
            if not path.exists():
                face = data[index * size:(index + 1) * size]
                if int(cube.m_TextureFormat) == 24:
                    decoded = texture2ddecoder.decode_bc6(face, cube.m_Width, cube.m_Height)
                    image = Image.frombytes('RGBA', (cube.m_Width, cube.m_Height), decoded, 'raw', 'BGRA')
                    image = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                else:
                    image = parse_image_data(face, cube.m_Width, cube.m_Height, cube.m_TextureFormat,
                                             obj.version, obj.platform, cube.m_PlatformBlob)
                temporary = path.with_name(path.name + '.tmp')
                try:
                    image.save(temporary, format='PNG')
                    temporary.replace(path)
                finally:
                    temporary.unlink(missing_ok=True)
            files.append(path.name)
        return files
    if kind == 'VideoClip':
        video = obj.read()
        resource = video.m_ExternalResources
        if not resource or not resource.m_Source or not resource.m_Size:
            raise ValueError('video has no external stream')
        path = target.with_suffix('.video')
        if not path.exists():
            data = get_resource_data(resource.m_Source, obj.assets_file, resource.m_Offset, resource.m_Size)
            temporary = path.with_name(path.name + '.tmp')
            try:
                temporary.write_bytes(data)
                temporary.replace(path)
            finally:
                temporary.unlink(missing_ok=True)
        return [path.name]
    if kind in ('Texture2D', 'Sprite'):
        if kind == 'Texture2D':
            texture = obj.read()
            if texture.m_Width == 0 and texture.m_Height == 0 and texture.m_CompleteImageSize == 0:
                path = target.with_suffix('.json')
                if not path.exists():
                    temporary = path.with_name(path.name + '.tmp')
                    try:
                        temporary.write_text(json.dumps(obj.read_typetree(), ensure_ascii=True, default=str), encoding='utf-8')
                        temporary.replace(path)
                    finally:
                        temporary.unlink(missing_ok=True)
                return [path.name]
            if int(texture.m_TextureFormat) == 74:
                data = texture.get_image_data()
                required = texture.m_Width * texture.m_Height * 8
                if len(data) < required:
                    raise ValueError('incomplete RGBA64 image data')
                raw = target.with_suffix('.texturedata')
                if not raw.exists():
                    raw.write_bytes(data)
                path = target.with_suffix('.png')
                if not path.exists():
                    image = Image.frombytes('RGBA', (texture.m_Width, texture.m_Height), data[:required][1::2])
                    image = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                    temporary = path.with_name(path.name + '.tmp')
                    try:
                        image.save(temporary, format='PNG')
                        temporary.replace(path)
                    finally:
                        temporary.unlink(missing_ok=True)
                return [raw.name, path.name]
        path = target.with_suffix('.png')
        if not path.exists():
            temporary = path.with_name(path.name + '.tmp')
            try:
                obj.read().image.save(temporary, format='PNG')
                temporary.replace(path)
            finally:
                temporary.unlink(missing_ok=True)
        return [path.name]
    if kind == 'AudioClip':
        samples = obj.read().samples
        if not samples:
            raise ValueError('no decoded audio samples')
        files = []
        for index, (name, data) in enumerate(samples.items()):
            suffix = Path(name).suffix.lower()
            if suffix not in ('.wav', '.ogg', '.mp3', '.m4a'):
                raise ValueError('unsupported audio format: ' + suffix)
            path = target.parent / (identity + '-' + str(index) + suffix)
            if not path.exists():
                temporary = path.with_name(path.name + '.tmp')
                try:
                    temporary.write_bytes(data)
                    temporary.replace(path)
                finally:
                    temporary.unlink(missing_ok=True)
            files.append(path.name)
        return files
    path = target.with_suffix('.json')
    if not path.exists():
        if kind == 'Mesh':
            handler = MeshHandler(obj.read())
            handler.process()
            tree = obj.read_typetree()
            if not handler.m_Vertices and not handler.m_IndexBuffer:
                value = {'empty_mesh': True, 'source': tree}
            else:
                value = mesh_data(handler, tree)
        else:
            value = obj.read_typetree()
        temporary = path.with_name(path.name + '.tmp')
        try:
            temporary.write_text(json.dumps(value, ensure_ascii=True, default=str), encoding='utf-8')
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)
    return [path.name]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--limit', type=int, default=0, help='仅处理前 N 个包，用于验证')
    parser.add_argument('--pattern', default='*.unity3d')
    parser.add_argument('--min-free-gb', type=float, default=10)
    parser.add_argument('--force', action='store_true', help='重新检查已有包')
    args = parser.parse_args()
    imp = Importer()
    output = PROJECT / 'assets' / 'extracted'
    reports = PROJECT / 'assetsresearch' / 'bulk-export'
    output.mkdir(parents=True, exist_ok=True)
    reports.mkdir(parents=True, exist_ok=True)
    bundles = [(name, path) for name, path in sorted(imp.reader.source_paths().items())
               if not name.startswith('@') and Path(name).match(args.pattern)]
    if args.limit:
        bundles = bundles[:args.limit]
    for number, (name, source) in enumerate(bundles, 1):
        if shutil.disk_usage(output).free < args.min_free_gb * 1024**3:
            raise RuntimeError('磁盘剩余空间低于阈值，已停止；可清理空间后重跑')
        bundle_id = hashlib.sha256(name.encode()).hexdigest()[:16]
        report_path = reports / (bundle_id + '.json')
        stat = source.stat()
        fingerprint = {'size': stat.st_size, 'mtime_ns': stat.st_mtime_ns}
        if report_path.exists() and not args.force:
            previous = json.loads(report_path.read_text(encoding='utf-8'))
            if (previous.get('source') == fingerprint and previous.get('object_metadata_complete')
                    and not previous.get('errors')):
                continue
        directory = output / (Path(name).stem + '-' + bundle_id)
        records, errors = [], []
        metadata_count = 0
        try:
            env = imp.reader.load(name)
            directory.mkdir(parents=True, exist_ok=True)
            metadata_path = directory / 'objects.jsonl'
            temporary_metadata = directory / 'objects.jsonl.tmp'
            with temporary_metadata.open('w', encoding='utf-8') as metadata:
                for obj in env.objects:
                    kind = obj.type.name
                    info = {'type': kind, 'cab': obj.assets_file.name, 'pathid': str(obj.path_id)}
                    try:
                        if kind in TYPES:
                            info['name'] = obj.peek_name() or ''
                            info['files'] = export_one(obj, directory)
                            records.append(info)
                        else:
                            info['data'] = obj.read_typetree()
                            metadata.write(json.dumps(info, ensure_ascii=True, default=str) + '\n')
                            metadata_count += 1
                    except Exception as exc:
                        failure = {key: value for key, value in info.items() if key != 'data'}
                        failure['error'] = f'{type(exc).__name__}: {exc}'
                        try:
                            failure['raw_files'] = preserve_failed_object(obj, directory)
                        except Exception as raw_exc:
                            failure['raw_error'] = f'{type(raw_exc).__name__}: {raw_exc}'
                        errors.append(failure)
            temporary_metadata.replace(metadata_path)
        except Exception as exc:
            errors.append({'error': f'{type(exc).__name__}: {exc}'})
        temporary = report_path.with_suffix('.tmp')
        temporary.write_text(json.dumps({'bundle': name, 'source': fingerprint, 'assets': records,
                                         'object_metadata_complete': True, 'object_count': metadata_count,
                                         'errors': errors}, ensure_ascii=True), encoding='utf-8')
        temporary.replace(report_path)
        print(f'{number}/{len(bundles)} {name}: {len(records)} assets, {len(errors)} errors', flush=True)


if __name__ == '__main__':
    main()
