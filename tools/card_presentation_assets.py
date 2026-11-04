"""全卡池效果关联审计和有界按需导出；原脚本/条件分支不会被假装完整执行。"""
import argparse
import json
import re
from PIL import Image

from .asset_lock import asset_lock, atomic_json, merge_effects
from .board_ui_assets import source_bundle
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .audio_cache import publish_audio


def references(value, path=''):
    result = []
    if isinstance(value, dict):
        for key, item in value.items():
            if key == 'm_SpellPath' and item:
                result.append(dict(slot=path, reference=item))
            elif 'Effect' in key or path:
                result.extend(references(item, path + '/' + key))
    elif isinstance(value, list):
        for index, item in enumerate(value):
            result.extend(references(item, path + '/' + str(index)))
    return result


def family(reference):
    name = reference.lower()
    if 'transform' in name or 'polymorph' in name or 'hex' in name:
        return 'transform'
    if 'draw' in name or 'intellect' in name or 'sprint' in name:
        return 'draw'
    if 'heal' in name:
        return 'heal'
    if any(word in name for word in ('buff', 'rage', 'blessing', 'shield', 'enchant')):
        return 'buff'
    if any(word in name for word in ('_ae_', 'nova', 'storm', 'whirlwind', 'flamestrike', 'consecration', 'hellfire')):
        return 'area'
    return 'projectile'


def effect_assets(ex, reference):
    root, _, _ = ex.imp.reader.resolve(reference)
    objects, materials, audio, timing, errors = {}, [], [], [], []
    for obj, tree in ex.imp.reader.walk(root, include_visual=True, errors=errors, max_nodes=4000):
        key = (obj.assets_file.name, obj.path_id)
        objects[key] = obj
        if obj.type.name == 'ParticleSystemRenderer':
            for pointer in (tree or obj.read_typetree()).get('m_Materials', []):
                material = ex.imp.reader.pointer(obj, pointer)
                if material:
                    objects[(material.assets_file.name, material.path_id)] = material
    for obj in objects.values():
        kind = obj.type.name
        if kind not in ('Material', 'AudioClip', 'ParticleSystem', 'AnimationClip'):
            continue
        tree = obj.read_typetree()
        row = dict(name=tree.get('m_Name', ''), type=kind, cab=obj.assets_file.name, pathid=str(obj.path_id),
                   bundle=source_bundle(ex, obj.assets_file.name))
        if kind == 'AudioClip':
            audio.append((obj, row))
        elif kind == 'Material':
            shader_obj = ex.imp.reader.pointer(obj, tree.get('m_Shader', {}))
            shader_tree = shader_obj.read_typetree() if shader_obj else {}
            shader = shader_tree.get('m_ParsedForm', {}).get('m_Name', shader_tree.get('m_Name', ''))
            if any(word in shader.lower() for word in ('additive', 'addmultiply')):
                materials.append((obj, row))
        else:
            filename = ex.key(obj) + '.' + kind.lower() + '.json'
            atomic_json(ex.folder / filename, tree)
            row['cache'] = 'local/runtime/' + filename
            timing.append(row)
    return materials, audio, timing, errors


def audio_asset(ex, obj):
    row = ex.imp.store.db.execute('SELECT * FROM assets WHERE cab=? AND pathid=?',
                                (obj.assets_file.name, str(obj.path_id))).fetchone()
    if row is None:
        raise ValueError('关联 AudioClip 尚未索引')
    path = ex.imp.export(row['id'])
    target = ex.folder / path.name
    publish_audio(target, path.read_bytes())
    logical = 'SFX_ASSOC_' + ex.key(obj).upper()
    ex.registry[logical] = target.name
    return logical


def render_settings(path):
    # 只读取原图边缘；去底在 GPU 完成，缓存中的原始像素不改写。
    with Image.open(path) as source:
        image = source.convert('RGBA')
        corners = [image.getpixel(point) for point in ((0, 0), (image.width - 1, 0),
                   (0, image.height - 1), (image.width - 1, image.height - 1))]
    opaque = any(pixel[3] > 20 and max(pixel[:3]) > 20 for pixel in corners)
    uniform = all(max(abs(pixel[i] - corners[0][i]) for i in range(3)) < 14 for pixel in corners)
    return dict(soft_mask=True, key_background=opaque and uniform,
                background=dict(zip(('r', 'g', 'b', 'a'), [c / 255 for c in corners[0]])))


def refresh_render_metadata(folder, registry):
    with asset_lock(folder.parent):
        return _refresh_render_metadata(folder, registry)


def _refresh_render_metadata(folder, registry):
    path = folder / 'effect-index.json'
    index = json.loads(path.read_text(encoding='utf-8'))
    omitted = []
    for profile in index['entries']:
        visible = []
        for sprite in profile.get('visuals', []):
            tint = sprite['tint']
            if tint.get('a', 1) <= 0 or max(tint[k] for k in ('r', 'g', 'b')) <= 0:
                omitted.append(dict(card_id=profile['card_id'], texture=sprite['texture'],
                                    reason='原材质初始化颜色不可见；运行时颜色脚本未恢复'))
                continue
            sprite.update(render_settings(folder / registry[sprite['texture']]))
            visible.append(sprite)
        if 'visuals' in profile:
            profile['visuals'] = visible
    index['entries'] = [p for p in index['entries'] if p.get('visuals')
                        or any(p.get(k) for k in ('start', 'cast', 'hit', 'projectile', 'impact'))]
    atomic_json(path, index)
    return omitted


def write_status_document(audit):
    lines = ['# 现有卡池专属表现状态', '',
             '本表由 `tools.card_presentation_assets` 根据本机 CardDef 引用生成，不表示卡牌规则状态。', '',
             '`[?]`：已有原关联素材及客户端时间线，但未逐帧核验官方效果；',
             '`[ ]`：专属效果未完成，仍可使用原有通用出牌/召唤/伤害表现。', '',
             '原 ParticleSystem/AnimationClip 参数只留本机，尚未执行完整原脚本、Shader 和条件分支。', '',
             '| 状态 | CardId | 名称 | 接入情况 |', '|---|---|---|---|']
    for card in audit['cards']:
        state = card['status']
        description = {'associated_sequence': '原主施放关联素材，运动/时序重实现',
                       'authored_sequence': '已调校的独立时间线，仍有恢复边界',
                       'incomplete': '专属效果未完成'}.get(state, state)
        lines.append('| {} | {} | {} | {} |'.format('[ ]' if state == 'incomplete' else '[?]',
                     card['card_id'], card['name'].replace('|', '\\|').replace('\n', ' '), description))
    (PROJECT / 'docs/CARD_PRESENTATION_STATUS.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')


def save_audit(audit):
    path = PROJECT / 'manifests/card-presentation-audit.json'
    old = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
    omitted = {(x['card_id'], x['texture']): x for x in old.get('omitted_visual_layers', [])}
    omitted.update({(x['card_id'], x['texture']): x for x in audit.get('omitted_visual_layers', [])})
    audit['omitted_visual_layers'] = list(omitted.values())
    effects = json.loads((PROJECT / 'local/runtime/effect-index.json').read_text(encoding='utf-8'))['entries']
    available = {entry['card_id'] for entry in effects}
    for card in audit['cards']:
        if card['status'] == 'associated_sequence' and card['card_id'] not in available:
            card.update(status='incomplete', reason='关联材质初始化不可见，运行时颜色脚本未恢复；专属播放器未完成')
    atomic_json(path, audit)
    write_status_document(audit)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--audit-only', action='store_true')
    parser.add_argument('--limit', type=int, default=60)
    parser.add_argument('--ids', default='')
    parser.add_argument('--all-types', action='store_true')
    parser.add_argument('--refresh-associated', action='store_true')
    parser.add_argument('--render-metadata-only', action='store_true')
    args = parser.parse_args()
    ex = RuntimeExport()
    if args.render_metadata_only:
        omitted = refresh_render_metadata(ex.folder, ex.registry)
        path = PROJECT / 'manifests/card-presentation-audit.json'
        audit = json.loads(path.read_text(encoding='utf-8'))
        audit['omitted_visual_layers'] = omitted
        save_audit(audit)
        print('RENDER METADATA refreshed; omitted invisible original layers:', len(omitted))
        return
    database = json.loads((ex.imp.local / 'card-database.json').read_text(encoding='utf-8'))
    existing = json.loads((ex.folder / 'effect-index.json').read_text(encoding='utf-8'))['entries']
    existing_ids = {entry['card_id'] for entry in existing}
    prior = {entry['card_id']: entry for entry in existing}
    previous_path = PROJECT / 'manifests/card-presentation-audit.json'
    previous = json.loads(previous_path.read_text(encoding='utf-8')) if previous_path.exists() else {}
    audit, sources, profiles, resolved = [], [], [], {}
    selected = set(args.ids.split(',')) - {''}
    cards = sorted((c for c in database['cards'] if c['Playable']), key=lambda c: (c['Type'] != 5, c['CardId']))
    for index, card in enumerate(cards):
        card_id = card['CardId']
        entry = dict(card_id=card_id, name=card['Name'], type=card['Type'], references=[], status='incomplete')
        try:
            definition = ex.imp.reader.definition(card_id)
            entry['references'] = references(definition)
            if card_id in existing_ids and not (args.refresh_associated and 'visuals' in prior[card_id]):
                entry['status'] = 'associated_sequence' if 'visuals' in prior[card_id] else 'authored_sequence'
            else:
                reference = definition.get('m_PlayEffectDef', {}).get('m_SpellPath', '')
                eligible = reference and (card_id in selected if selected else args.all_types or card['Type'] == 5)
                if not args.audit_only and eligible and (len(profiles) < args.limit or card_id in selected):
                    if reference not in resolved:
                        materials, sounds, timing, errors = effect_assets(ex, reference)
                        visuals = []
                        for obj, identity in materials[:3]:
                            material = ex.material(obj)
                            if not material['texture']:
                                continue
                            logical = 'VFX_ASSOC_' + ex.key(obj).upper()
                            ex.registry[logical] = material['texture']
                            visuals.append(dict(texture=logical, tint=material['tint'], soft_mask=False))
                            tex = next(p for p in reversed(ex.provenance) if p.get('file') == material['texture'])
                            sources.append(dict(id=logical, type='Texture2D', bundle=source_bundle(ex, tex['cab']),
                                cab=tex['cab'], pathid=tex['pathid'], material=identity['name'], reference=reference,
                                cache='local/runtime/' + material['texture']))
                        audio = {}
                        for phase, pattern in [('cast', r'cast|start|launch'), ('hit', r'impact|hit')]:
                            candidates = [(obj, row) for obj, row in sounds if re.search(pattern, row['name'], re.I)]
                            if candidates:
                                obj, row = candidates[0]
                                logical = audio_asset(ex, obj)
                                audio[phase] = logical
                                sources.append(dict(id=logical, **row, reference=reference, cache='local/runtime/' + ex.registry[logical]))
                        resolved[reference] = dict(visuals=visuals, audio=audio, timing=timing, errors=errors)
                    data = resolved[reference]
                    if data['visuals'] or data['audio']:
                        profile = dict(card_id=card_id, kind=family(reference), cast_duration=.8,
                                       visuals=data['visuals'], **data['audio'])
                        profiles.append(profile)
                        entry.update(status='associated_sequence', original_timing=data['timing'], dependency_errors=data['errors'])
                    else:
                        entry['reason'] = '效果引用存在，但无当前播放器可用的加法材质或施放/命中声'
        except Exception as exc:
            entry['reason'] = str(exc)
        audit.append(entry)
        if index % 25 == 0:
            print('audit', index + 1, '/', len(cards), 'new profiles', len(profiles), flush=True)
    ex.save()
    if profiles:
        merge_effects(ex.imp.local, profiles)
    omitted = refresh_render_metadata(ex.folder, ex.registry)
    merged_sources = {item['id']: item for item in previous.get('assets', [])}
    merged_sources.update({item['id']: item for item in sources})
    save_audit(dict(cards=audit, assets=list(merged_sources.values()), omitted_visual_layers=omitted,
        notes=['associated_sequence 是原关联素材的客户端重实现；尚未逐帧核验原版。',
               '原粒子/动画参数留本机，未执行专用 Shader、条件 FSM 和所有分支。']))
    available = {p['card_id'] for p in json.loads((ex.folder / 'effect-index.json').read_text(encoding='utf-8'))['entries']}
    print('RESULT', len(audit), 'audited;', sum(p['card_id'] in available for p in profiles), 'usable new sequences;', len(sources), 'sources', flush=True)


if __name__ == '__main__':
    main()
