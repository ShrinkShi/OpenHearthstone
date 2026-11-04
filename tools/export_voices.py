"""沿 CardDef GUID 的事件引用按需导出中文语音，保留来源与候选列表。"""
import argparse
import hashlib
import json
import math
from pathlib import Path

from .export_runtime import RuntimeExport
from .asset_lock import asset_lock, atomic_json

ANNOUNCER_FIELDS = {
    'ANNOUNCER_BEFORE_VERSUS': 'm_AnnouncerLineBeforeVersusPath',
    'ANNOUNCER_AFTER_VERSUS': 'm_AnnouncerLineAfterVersusPath',
}
VERSUS_ASSET = 'VO_ANNOUNCER_VERSUS_21'
VERSUS_PREFAB_REFERENCE = 'VO_ANNOUNCER_VERSUS_21.prefab:acc34acb15f07ff4ba08025a57a9a458'
VERSUS_PREFAB_BUNDLE = 'essential_base_global-prefab-0.unity3d'


def export(ex, card_id, locale='zhcn', events=('Play', 'Attack', 'Death')):
    definition = ex.imp.reader.definition(card_id)
    report = []
    suppress_play_on_summon = bool(definition.get('m_SuppressPlaySoundsOnSummon', 0))
    for event in events:
        refs = definition.get(f'm_{event}EffectDef', {}).get('m_SoundSpellPaths', [])
        sounds, errors = {}, []
        for reference in refs:
            root, _, _ = ex.imp.reader.resolve(reference, locale)
            for obj, tree, condition, timing in ex.imp.reader.walk(root, locale=locale, errors=errors,
                    with_conditions=True, condition_match=lambda branch: False, with_timing=True):
                if obj.type.name == 'AudioClip':
                    sounds[ex.key(obj)] = (obj, timing)
        # 同事件的随机候选分别登记，不能同时叠放全部音轨。
        for index, (obj, timing) in enumerate(sounds.values()):
            tree = obj.read_typetree()
            row = ex.imp.store.db.execute('SELECT * FROM assets WHERE cab=? AND pathid=?',
                                          (obj.assets_file.name, str(obj.path_id))).fetchone()
            if row is None:
                errors.append(f'AudioClip missing from index: {tree.get("m_Name")}')
                continue
            path = ex.imp.export(row['id'])
            target = ex.folder / path.name
            if not target.exists():
                target.write_bytes(path.read_bytes())
            logical_id = f'VOICE_{card_id}_{event.upper()}_{index}'
            ex.registry[logical_id] = target.name
            item = {'id': logical_id, 'card_id': card_id, 'event': event.upper(), 'reference': refs,
                    'name': tree.get('m_Name'), 'locale': row['locale'], 'timing': timing,
                    'delay': timing.get('seconds', 0) if timing and timing.get('anchor') == 'card_event' else 0,
                    'asset_id': row['id'], 'bundle': row['bundle'], 'cab': row['cab'],
                    'pathid': row['pathid'], 'file': target.name,
                    'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
            if event.lower() == 'play':
                item['suppress_play_sound_on_summon'] = suppress_play_on_summon
            report.append(item)
        if errors:
            ex.errors.append({'card_id': card_id, 'event': event, 'errors': errors})
    return report


def export_announcer_line(ex, card_id, locale, event, field):
    definition = ex.imp.reader.definition(card_id)
    reference = definition.get(field)
    source_field = field
    fallback_used = False
    if not reference:
        reference = definition.get('m_AnnouncerLinePath')
        source_field = 'm_AnnouncerLinePath'
        fallback_used = bool(reference)
    if not reference:
        ex.errors.append({'card_id': card_id, 'event': event, 'field': field,
                          'error': 'CardDef announcer source reference is missing'})
        return []
    try:
        root, _, _ = ex.imp.reader.resolve(reference, locale)
        sounds, errors = {}, []
        for obj, tree, condition, timing in ex.imp.reader.walk(root, locale=locale, errors=errors,
                with_conditions=True, condition_match=lambda branch: False, with_timing=True):
            if obj.type.name == 'AudioClip':
                sounds.setdefault(ex.key(obj), (obj, timing))
        rows = []
        for index, (obj, timing) in enumerate(sounds.values()):
            tree = obj.read_typetree()
            row = ex.imp.store.db.execute('SELECT * FROM assets WHERE cab=? AND pathid=? AND type=?',
                                          (obj.assets_file.name, str(obj.path_id), 'AudioClip')).fetchone()
            if row is None:
                errors.append(f"AudioClip missing from index: {tree.get('m_Name')}")
                continue
            path = ex.imp.export(row['id'])
            target = ex.folder / path.name
            if not target.exists():
                target.write_bytes(path.read_bytes())
            logical_id = f'VOICE_{card_id}_{event}_{index}'
            ex.registry[logical_id] = target.name
            metadata = json.loads(row['metadata'] or '{}')
            rows.append({'id': logical_id, 'card_id': card_id, 'event': event,
                         'field': source_field, 'requested_field': field, 'fallback_used': fallback_used,
                         'reference': reference, 'name': tree.get('m_Name'),
                         'locale': row['locale'], 'duration': float(metadata.get('m_Length') or 0),
                         'timing': timing, 'asset_id': row['id'], 'bundle': row['bundle'],
                         'cab': row['cab'], 'pathid': row['pathid'], 'file': target.name,
                         'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
        if errors:
            ex.errors.append({'card_id': card_id, 'event': event, 'field': field, 'errors': errors})
        if not rows:
            ex.errors.append({'card_id': card_id, 'event': event, 'field': field,
                              'error': 'announcer reference resolved without an AudioClip'})
        return rows
    except Exception as exc:
        ex.errors.append({'card_id': card_id, 'event': event, 'field': field,
                          'reference': reference, 'error': str(exc)})
        return []


def export_versus_clip(ex, locale):
    reader = ex.imp.reader
    prefab_source = {'guid': 'acc34acb15f07ff4ba08025a57a9a458',
                     'reference': VERSUS_PREFAB_REFERENCE, 'bundle': VERSUS_PREFAB_BUNDLE,
                     'sound_defs': [], 'clip_candidates': [], 'mapping_status': 'unresolved'}

    def source_identity(obj):
        bundle = reader.file_bundle.get(obj.assets_file.name.lower(), reader.cabs.get(obj.assets_file.name.lower(), ''))
        rows = ex.imp.store.db.execute('SELECT * FROM assets WHERE cab=? AND pathid=? AND type=?',
                                       (obj.assets_file.name, str(obj.path_id), obj.type.name)).fetchall()
        row = rows[0] if len(rows) == 1 else None
        tree = obj.read_typetree()
        identity = {'name': tree.get('m_Name', ''), 'bundle': bundle, 'cab': obj.assets_file.name,
                    'pathid': str(obj.path_id), 'type': obj.type.name}
        if row is not None:
            identity['asset_id'] = row['id']
        elif len(rows) != 1:
            raise ValueError(f"source identity has {len(rows)} exact asset-index rows: {obj.type.name} {obj.assets_file.name}/{obj.path_id}")
        return identity, row

    try:
        prefab, resolved_bundle, resolved_locale = reader.resolve(VERSUS_PREFAB_REFERENCE, locale)
        if prefab.type.name != 'GameObject':
            raise ValueError(f'GUID resolved to {prefab.type.name}, expected GameObject')
        prefab_identity, prefab_row = source_identity(prefab)
        prefab_source.update(prefab_identity)
        prefab_source.update({'guid': 'acc34acb15f07ff4ba08025a57a9a458',
                              'reference': VERSUS_PREFAB_REFERENCE, 'resolved_locale': resolved_locale})
        if resolved_bundle != VERSUS_PREFAB_BUNDLE or prefab_identity['bundle'] != VERSUS_PREFAB_BUNDLE:
            raise ValueError(f'GUID resolved outside expected prefab bundle: {resolved_bundle}/{prefab_identity["bundle"]}')
        if prefab_row is None:
            raise ValueError('resolved prefab has no unique asset-index row')
    except Exception as exc:
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': f'original versus prefab GUID could not be resolved exactly: {exc}'})
        return [], prefab_source

    graph_errors = []
    sound_defs = []
    try:
        for obj, tree in reader.walk(prefab, locale=locale, errors=graph_errors):
            if obj.type.name != 'MonoBehaviour' or not tree:
                continue
            script = reader.pointer(obj, tree.get('m_Script', {}))
            if script is None or script.read_typetree().get('m_ClassName') != 'SoundDef':
                continue
            identity, _ = source_identity(obj)
            sound_defs.append((obj, tree, identity))
            prefab_source['sound_defs'].append(identity)
    except Exception as exc:
        graph_errors.append(f'prefab sound-reference walk failed: {exc}')
    if graph_errors:
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'prefab source graph has unresolved references', 'details': graph_errors})
    if not sound_defs:
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'resolved versus prefab contains no reachable SoundDef'})
        prefab_source['mapping_status'] = 'no_sound_def'
        return [], prefab_source

    candidates, candidate_errors = [], []
    for _, tree, sound_identity in sound_defs:
        references = []
        direct = tree.get('m_AudioClip', '')
        if direct:
            references.append(('fallback', 'm_AudioClip', -1, direct, None))
        random_rows = tree.get('m_RandomClips') or []
        for index, item in enumerate(random_rows):
            reference = item.get('m_Clip', '') if isinstance(item, dict) else ''
            if reference:
                weight = item.get('m_Weight')
                if isinstance(weight, bool) or not isinstance(weight, (int, float)) or not math.isfinite(weight) or weight < 0:
                    candidate_errors.append({'sound_def': sound_identity, 'field': 'm_RandomClips',
                                             'candidate_index': index, 'error': 'random clip weight is missing, non-finite, or negative',
                                             'weight': weight})
                references.append(('weighted_random', 'm_RandomClips', index, reference, weight))
            elif item:
                candidate_errors.append({'sound_def': sound_identity, 'field': 'm_RandomClips',
                                         'candidate_index': index, 'error': 'random clip row has no m_Clip reference'})
        if not references:
            candidate_errors.append({'sound_def': sound_identity, 'error': 'SoundDef has no direct or random AudioClip references'})
        for selection_role, field, candidate_index, reference, weight in references:
            try:
                clip, clip_bundle, actual_locale = reader.resolve(reference, locale)
                if clip.type.name != 'AudioClip':
                    raise ValueError(f'reference resolved to {clip.type.name}, expected AudioClip')
                identity, row = source_identity(clip)
                if row is None:
                    raise ValueError('resolved AudioClip has no unique asset-index row')
                candidate = {'sound_def': sound_identity, 'selection_role': selection_role,
                             'sound_field': field,
                             'candidate_index': candidate_index, 'sound_reference': reference,
                             'clip_source': identity, 'clip_locale': actual_locale,
                             'resolved_bundle': clip_bundle, 'asset_id': row['id']}
                if weight is not None:
                    candidate['weight'] = float(weight)
                candidates.append(candidate)
            except Exception as exc:
                candidate_errors.append({'sound_def': sound_identity, 'sound_field': field,
                                         'selection_role': selection_role,
                                         'candidate_index': candidate_index, 'reference': reference,
                                         'error': str(exc)})
    prefab_source['clip_candidates'] = candidates
    if graph_errors or candidate_errors:
        prefab_source['mapping_status'] = 'incomplete_source_mapping'
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'one or more direct SoundDef clip candidates could not be resolved',
                          'details': candidate_errors + [{'error': value} for value in graph_errors]})
        return [], prefab_source

    if len(sound_defs) != 1:
        prefab_source['mapping_status'] = 'ambiguous_sound_defs'
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'prefab resolves to multiple SoundDefs; playback source is unresolved',
                          'sound_defs': prefab_source['sound_defs']})
        return [], prefab_source

    random_candidates = [item for item in candidates if item['selection_role'] == 'weighted_random']
    fallback_candidates = [item for item in candidates if item['selection_role'] == 'fallback']
    if len(fallback_candidates) > 1 or (not random_candidates and len(fallback_candidates) != 1):
        prefab_source['mapping_status'] = 'invalid_sounddef_selection_shape'
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'SoundDef must have one m_AudioClip fallback and either a random list or no random candidates',
                          'candidates': candidates})
        return [], prefab_source
    if random_candidates and not fallback_candidates:
        candidate_errors.append({'sound_def': prefab_source['sound_defs'][0],
                                 'error': 'weighted random clips have no m_AudioClip fallback'})
        prefab_source['mapping_status'] = 'incomplete_source_mapping'
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'SoundDef fallback clip is missing', 'details': candidate_errors})
        return [], prefab_source

    if not random_candidates:
        random_candidates = [dict(fallback_candidates[0], selection_role='direct')]
        selection_strategy = 'single_direct_m_AudioClip'
    else:
        selection_strategy = 'weighted_random_clips_then_m_AudioClip_fallback'

    runtime_candidates, exported_by_source = [], {}
    for index, candidate in enumerate(random_candidates):
        logical_id = f'VOICE_ANNOUNCER_VERSUS_21_{index}'
        source = candidate['clip_source']
        source_key = (source['cab'], source['pathid'])
        row = ex.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (candidate['asset_id'],)).fetchone()
        if row is None:
            candidate_errors.append({'candidate': candidate, 'error': 'resolved AudioClip asset-index row disappeared'})
            continue
        path = ex.imp.export(row['id'])
        target = ex.folder / path.name
        if not target.exists():
            target.write_bytes(path.read_bytes())
        ex.registry[logical_id] = target.name
        exported_by_source[source_key] = {'id': logical_id, 'file': target.name, 'row': row,
                                          'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        runtime_candidate = {'id': logical_id, 'weight': candidate.get('weight', 1.0)}
        runtime_candidates.append(runtime_candidate)
        candidate['runtime_id'] = logical_id
        candidate['file'] = target.name
        candidate['sha256'] = exported_by_source[source_key]['sha256']
        candidate['duration'] = float(json.loads(row['metadata'] or '{}').get('m_Length') or 0)

    fallback_id = ''
    fallback = fallback_candidates[0] if fallback_candidates else None
    if fallback is not None:
        source = fallback['clip_source']
        source_key = (source['cab'], source['pathid'])
        exported = exported_by_source.get(source_key)
        if exported is None:
            row = ex.imp.store.db.execute('SELECT * FROM assets WHERE id=?', (fallback['asset_id'],)).fetchone()
            if row is None:
                candidate_errors.append({'candidate': fallback, 'error': 'fallback AudioClip asset-index row disappeared'})
            else:
                path = ex.imp.export(row['id'])
                target = ex.folder / path.name
                if not target.exists():
                    target.write_bytes(path.read_bytes())
                fallback_id = 'VOICE_ANNOUNCER_VERSUS_21_FALLBACK'
                ex.registry[fallback_id] = target.name
                exported = {'id': fallback_id, 'file': target.name, 'row': row,
                            'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
                exported_by_source[source_key] = exported
        if exported is not None:
            fallback_id = exported['id'] if exported['id'].endswith('_FALLBACK') else exported['id']
            fallback['runtime_id'] = fallback_id
            fallback['file'] = exported['file']
            fallback['sha256'] = exported['sha256']
            fallback['duration'] = float(json.loads(exported['row']['metadata'] or '{}').get('m_Length') or 0)

    if candidate_errors or not runtime_candidates:
        ex.errors.append({'asset': VERSUS_ASSET, 'reference': VERSUS_PREFAB_REFERENCE,
                          'error': 'one or more SoundDef clip candidates could not be exported',
                          'details': candidate_errors})
        prefab_source['mapping_status'] = 'candidate_export_failed'
        return [], prefab_source

    prefab_source['mapping_status'] = 'weighted_random_sounddef_candidates' if selection_strategy.startswith('weighted') else 'single_direct_sounddef_clip'
    prefab_source['selection_strategy'] = selection_strategy
    prefab_source['selection_contract'] = {
        'selector': 'SoundUtils.GetRandomClipFromDef(SoundDef)',
        'caller': 'SoundManager.LoadClipForPlayback(AudioSource, SoundDef)',
        'random_field': 'SoundDef.m_RandomClips',
        'weight_field': 'RandomAudioClip.m_Weight',
        'selection': 'UnityEngine.Random.Range(0, sum(weights)); cumulative-weight first match; last item fallback',
        'fallback_field': 'SoundDef.m_AudioClip',
        'fallback_when': 'GetRandomClipFromDef returns null',
        'il_sources': ['local/script-audit/Assembly-CSharp.dll.SoundUtils.il.txt',
                       'local/script-audit/Assembly-CSharp.dll.SoundManager.il.txt'],
    }
    prefab_source['clip_candidates'] = candidates
    prefab_source['runtime_candidates'] = runtime_candidates
    prefab_source['fallback_candidate'] = fallback
    primary_id = runtime_candidates[0]['id'] if runtime_candidates else fallback_id
    row = exported_by_source[(random_candidates[0]['clip_source']['cab'], random_candidates[0]['clip_source']['pathid'])]['row']
    report = [{'id': primary_id, 'card_id': 'GLOBAL', 'event': 'VERSUS',
               'candidates': runtime_candidates, 'fallback_id': fallback_id,
               'selection_strategy': selection_strategy,
               'source_name': VERSUS_ASSET, 'source_reference': VERSUS_PREFAB_REFERENCE,
               'source_prefab': prefab_source, 'source_sound_def': prefab_source['sound_defs'][0],
               'source_sound_field': random_candidates[0]['sound_field'],
               'sound_reference': [candidate['sound_reference'] for candidate in candidates],
               'clip_sources': [candidate['clip_source'] for candidate in candidates],
               'source_semantics': 'MulliganManager.HandleGameStart loads this prefab; '
                                   'WaitForHeroesAndStartAnimations plays versusVo between '
                                   'BEFORE_VERSUS and AFTER_VERSUS hero lines; SoundManager selects m_RandomClips by source weights before m_AudioClip fallback',
               'name': row['name'], 'locale': row['locale'],
               'duration': float(json.loads(row['metadata'] or '{}').get('m_Length') or 0),
               'asset_id': row['id'], 'bundle': row['bundle'], 'cab': row['cab'],
               'pathid': row['pathid'], 'file': exported_by_source[(row['cab'], str(row['pathid']))]['file'],
               'sha256': exported_by_source[(row['cab'], str(row['pathid']))]['sha256']}]
    return report, prefab_source


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='')
    parser.add_argument('--classes', default='')
    parser.add_argument('--locale', default='zhcn')
    parser.add_argument('--base-hero-attacks', action='store_true',
                        help='导出11个基础英雄的原Attack候选并写入metadata-only来源清单')
    parser.add_argument('--base-hero-announcer', action='store_true',
                        help='按CardDef announcer字段导出11个基础英雄的对阵前后播报原声')
    parser.add_argument('--summon-minions', action='store_true',
                        help='导出随从CardDef的PLAY原声与Summon抑制字段；与--ids组合时仅导出指定ID')
    args = parser.parse_args()
    ex = RuntimeExport()
    provenance = []
    selected = set(args.ids.split(','))-{''}
    requested_ids = selected.copy()
    events_by_card = {}
    summon_minions = set()
    summon_sources = []
    if args.classes:
        from game.database import preset
        for cls in args.classes.split(','):
            selected.update(preset(int(cls)))
    base_heroes = {}
    if args.base_hero_attacks or args.base_hero_announcer:
        from game.database import HEROES
        base_heroes = dict(HEROES)
        selected.update(base_heroes.values())
    if args.summon_minions:
        from fireplace import cards as fireplace_cards
        from game.database import initialize
        from hearthstone.enums import CardType
        initialize()
        known_card_defs = set(ex.imp.reader.cards)
        available_minions = {card.id for card in fireplace_cards.db.values()
                             if card.type == CardType.MINION and card.id in known_card_defs}
        summon_minions = available_minions & requested_ids if requested_ids else available_minions
        if (not requested_ids or 'CS2_101t' in requested_ids) and 'CS2_101t' not in summon_minions:
            raise ValueError('CS2_101t Silver Hand Recruit is missing from the available minion CardDef scope')
        selected.update(summon_minions)
    for card_id in sorted(selected):
        if card_id in base_heroes.values():
            if args.base_hero_attacks:
                events_by_card.setdefault(card_id, set()).add('ATTACK')
                provenance.extend(export(ex, card_id, args.locale, ('Attack',)))
        elif card_id in summon_minions:
            source_row = {
                'card_id': card_id,
                'card_type': 4,
                'voice_ids': [],
            }
            events_by_card[card_id] = {'PLAY'}
            try:
                definition = ex.imp.reader.definition(card_id)
                play = definition.get('m_PlayEffectDef') or {}
                refs = play.get('m_SoundSpellPaths') or []
                source_row.update({
                    'carddef_reference': ex.imp.reader.cards.get(card_id, ''),
                    'play_effect': {'spell_path': play.get('m_SpellPath', ''), 'sound_spell_paths': refs},
                    'suppress_play_sound_on_summon': bool(definition.get('m_SuppressPlaySoundsOnSummon', 0)),
                    'suppress_play_sounds_during_mulligan': bool(definition.get('m_SuppressPlaySoundsDuringMulligan', 0)),
                    'custom_summon_spell_path': definition.get('m_CustomSummonSpellPath', ''),
                    'custom_spawn_spell_path': definition.get('m_CustomSpawnSpellPath', ''),
                })
                voices = export(ex, card_id, args.locale, ('Play',))
                source_row['voice_ids'] = [row['id'] for row in voices]
                source_row['source_status'] = 'voice_candidates' if voices else 'no_play_audio_candidates'
            except Exception as exc:
                source_row['source_status'] = 'error'
                source_row['source_error'] = str(exc)
                ex.errors.append({'card_id': card_id, 'event': 'Play', 'error': str(exc)})
                voices = []
            summon_sources.append(source_row)
            provenance.extend(voices)
        else:
            events = ('Play', 'Attack', 'Death')
            events_by_card[card_id] = {event.upper() for event in events}
            provenance.extend(export(ex, card_id, args.locale, events))
        if card_id not in base_heroes.values() or not (args.base_hero_attacks or args.base_hero_announcer):
            continue
        print(card_id, len(provenance), flush=True)
    announcer_heroes = []
    if args.base_hero_announcer:
        for class_id, card_id in sorted(base_heroes.items()):
            source = {'class_id': class_id, 'card_id': card_id, 'lines': []}
            for event, field in ANNOUNCER_FIELDS.items():
                rows = export_announcer_line(ex, card_id, args.locale, event, field)
                provenance.extend(rows)
                events_by_card.setdefault(card_id, set()).add(event)
                source['lines'].append({'event': event, 'field': field,
                                        'source_field': rows[0]['field'] if rows else field,
                                        'fallback_used': bool(rows and rows[0]['fallback_used']),
                                        'reference': rows[0]['reference'] if rows else
                                                     ex.imp.reader.definition(card_id).get(field),
                                        'voices': rows})
            announcer_heroes.append(source)
        versus, versus_prefab = export_versus_clip(ex, args.locale)
        provenance.extend(versus)
        events_by_card.setdefault('GLOBAL', set()).add('VERSUS')
    index_path = ex.folder / 'voice-index.json'
    with asset_lock(ex.imp.local):
        atomic_json(ex.folder / 'voice-report.json', {'items': provenance, 'errors': ex.errors})
        previous = json.loads(index_path.read_text(encoding='utf-8'))['entries'] if index_path.exists() else []
        merged = {row['id']: row for row in previous if row.get('card_id') not in events_by_card or
                  row.get('event') not in events_by_card[row['card_id']]}
        merged.update({row['id']: row for row in provenance})
        atomic_json(index_path, {'entries': list(merged.values())})
    ex.save()
    if args.base_hero_attacks:
        attacks = [row for row in provenance if row['card_id'] in base_heroes.values() and row['event'] == 'ATTACK']
        hero_errors = [row for row in ex.errors if row.get('card_id') in base_heroes.values() and
                       row.get('event') == 'Attack']
        manifest = {'schema_version': 1, 'locale': args.locale,
                    'heroes': [{'class_id': class_id, 'card_id': card_id}
                               for class_id, card_id in sorted(base_heroes.items())],
                    'voices': attacks, 'errors': hero_errors,
                    'actor_binding': 'not applicable: source is CardDef.m_AttackEffectDef audio'}
        manifest_path = Path(__file__).resolve().parents[1] / 'manifests' / 'original-hero-attack-voices.json'
        manifest_path.parent.mkdir(exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.summon_minions:
        voices = [row for row in provenance if row.get('card_id') in summon_minions and row.get('event') == 'PLAY']
        manifest = {
            'schema_version': 1,
            'locale': args.locale,
            'source_field': 'CardDef.m_PlayEffectDef.m_SoundSpellPaths',
            'playback_semantics': 'Resolve the summoned entity from SemanticEvent.visual/after.field and start its PLAY candidate only on that entity\'s first true Summon cue. Do not play the minion line on CardPlay. Honor m_SuppressPlaySoundsOnSummon.',
            'spawn_semantics': 'm_CustomSummonSpellPath and m_CustomSpawnSpellPath are retained as separate source metadata; they are not treated as audio references.',
            'cards': summon_sources,
            'voices': voices,
            'errors': [row for row in ex.errors if row.get('card_id') in summon_minions],
        }
        manifest_path = Path(__file__).resolve().parents[1] / 'manifests' / 'original-minion-summon-voices.json'
        manifest_path.parent.mkdir(exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.base_hero_announcer:
        announcer_voices = [row for row in provenance if row['event'] in ANNOUNCER_FIELDS or
                            row.get('event') == 'VERSUS']
        manifest = {'schema_version': 1, 'locale': args.locale,
                    'heroes': announcer_heroes,
                    'versus': {'prefab_reference': VERSUS_PREFAB_REFERENCE,
                               'prefab': versus_prefab,
                               'asset_name': VERSUS_ASSET,
                               'call_order': ['ANNOUNCER_BEFORE_VERSUS', 'VERSUS',
                                              'ANNOUNCER_AFTER_VERSUS'],
                               'voices': versus},
                    'voices': announcer_voices,
                    'errors': [row for row in ex.errors if row.get('event') in ANNOUNCER_FIELDS or
                               row.get('asset') == VERSUS_ASSET],
                    'actor_binding': 'CardDef m_AnnouncerLineBeforeVersusPath and '
                                     'm_AnnouncerLineAfterVersusPath are resolved by GameEntity.GetAnnouncerLine; '
                                     'the visual animation timing remains a client-side reconstruction.'}
        manifest_path = Path(__file__).resolve().parents[1] / 'manifests' / 'original-hero-announcer-opening.json'
        manifest_path.parent.mkdir(exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'voices': len(provenance), 'errors': ex.errors}, ensure_ascii=False))


if __name__ == '__main__':
    main()
