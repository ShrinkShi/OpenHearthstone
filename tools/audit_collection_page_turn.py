"""记录原收藏页翻页蒙皮与动画关联；仅写入本机研究目录。"""
import hashlib
import argparse
import json
from pathlib import Path

from .importer import Importer


PREFAB_GUID = '68e5e09e752e28148ba86ae599559691'
CLIP_BUNDLE = 'initial_base_global-1982ed8a-mesh-0.unity3d'
CLIPS = {'PageTurnLeft': -9151174933718150180, 'PageTurnRight': 4866898618422416348}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--publish-runtime', action='store_true', help='发布经核验的翻页研究数据至本机运行时')
    args = parser.parse_args()
    imp = Importer()
    root, bundle, _ = imp.reader.resolve(PREFAB_GUID)
    if root.read_typetree().get('m_Name') != 'CollectionManager':
        raise RuntimeError('收藏页面资源身份不符')
    files = imp.reader.source_paths()
    skins = {}
    for obj in root.assets_file.objects.values():
        if obj.type.name != 'GameObject':
            continue
        tree = obj.read_typetree()
        name = tree.get('m_Name')
        if name not in ('PageTurnFront', 'PageTurnBack'):
            continue
        renderers = []
        for component in tree.get('m_Component', []):
            part = imp.reader.pointer(obj, component['component'])
            if part is not None and part.type.name == 'SkinnedMeshRenderer':
                renderers.append(part)
        if len(renderers) != 1:
            raise RuntimeError(f'{name} 蒙皮渲染器数量异常')
        renderer = renderers[0]
        data = renderer.read_typetree()
        bones = []
        for pointer in data['m_Bones']:
            transform = imp.reader.pointer(renderer, pointer)
            if transform is None:
                raise RuntimeError(f'{name} 缺失骨骼')
            bone = imp.reader.pointer(transform, transform.read_typetree()['m_GameObject'])
            bones.append({'path_id': str(transform.path_id), 'name': bone.read_typetree()['m_Name']})
        mesh = imp.reader.pointer(renderer, data['m_Mesh'])
        if mesh is None or len(bones) != 9:
            raise RuntimeError(f'{name} 网格或骨骼数量异常')
        skins[name] = {'renderer_path_id': str(renderer.path_id), 'mesh_path_id': str(mesh.path_id),
                       'mesh_bundle': imp.reader.cabs.get(mesh.assets_file.name, ''), 'bones': bones}
    if set(skins) != {'PageTurnFront', 'PageTurnBack'}:
        raise RuntimeError('缺少翻页正反面')
    if skins['PageTurnFront']['bones'] != skins['PageTurnBack']['bones']:
        raise RuntimeError('翻页正反面骨骼映射不一致')
    env = imp.reader.load(CLIP_BUNDLE)
    animations = {}
    for obj in env.objects:
        if obj.type.name != 'AnimationClip' or obj.path_id not in CLIPS.values():
            continue
        tree = obj.read_typetree()
        name = tree.get('m_Name')
        if name not in CLIPS or obj.path_id != CLIPS[name]:
            raise RuntimeError('翻页动画身份不符')
        curves = {field: tree.get(field, []) for field in
                  ('m_PositionCurves', 'm_RotationCurves', 'm_ScaleCurves')}
        animations[name] = {'path_id': str(obj.path_id), 'sample_rate': tree.get('m_SampleRate'),
                            'curves': curves}
    if set(animations) != set(CLIPS):
        raise RuntimeError('缺少翻页动画')
    result = {'prefab_guid': PREFAB_GUID, 'prefab_bundle': bundle,
              'prefab_sha256': sha256(files[bundle]), 'clip_bundle': CLIP_BUNDLE,
              'clip_sha256': sha256(files[CLIP_BUNDLE]), 'skins': skins, 'animations': animations}
    output = imp.local / 'collection-uabea-audit' / 'page-turn-source.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    if args.publish_runtime:
        from .export_runtime import RuntimeExport
        from .asset_lock import atomic_json
        exporter = RuntimeExport(imp)
        runtime_path = exporter.folder / 'collection-page-turn-source.json'
        atomic_json(runtime_path, result)
        exporter.registry['COLLECTION_PAGE_TURN_SOURCE'] = runtime_path.name
        exporter.save()
    print(f'翻页研究记录: {output}; 2 个蒙皮，{len(skins["PageTurnFront"]["bones"])} 根骨骼，2 个片段')


if __name__ == '__main__':
    main()
