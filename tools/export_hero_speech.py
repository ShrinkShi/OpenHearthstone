"""Export original base-hero emote, opening and concede speech assets."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path

from PIL import Image

from game.database import HEROES
from .asset_lock import asset_lock, atomic_json
from .audio_cache import publish_audio
from .export_runtime import RuntimeExport
from .importer import DEFAULT_GAME, Importer


EMOTE_TYPES = {
    'GREETINGS': 1,
    'WELL_PLAYED': 2,
    'OOPS': 3,
    'THREATEN': 4,
    'THANKS': 5,
    'SORRY': 6,
    'CONCEDE': 7,
    'START': 8,
    'WOW': 33,
}
MENU_LABELS = ('THANKS', 'WELL_PLAYED', 'GREETINGS', 'WOW', 'OOPS', 'THREATEN', 'SORRY')
LOCALE_FOLDERS = {'zhcn': 'zhCN', 'enus': 'enUS', 'zhtw': 'zhTW', 'kokr': 'koKR', 'jajp': 'jaJP'}
BUBBLE_ASSET = 'Emote_Bubbles'
BUBBLE_LOGICAL_ID = 'UI_EMOTE_BUBBLES'
TRACKED_MANIFEST = Path(__file__).resolve().parents[1] / 'manifests' / 'original-hero-speech.json'

# 以原纹理512×512 PNG的可见上下两格为源区域；锚点取alpha>200的尾尖核心。
BUBBLE_REGIONS = [
    {'id': 'upper_right_tail', 'source_rect_px': [0, 0, 512, 256], 'uv': [0, .5, 1, .5],
     'tail_tip_px': [420, 209], 'text_rect_px': [90, 40, 340, 98], 'flip_y': False},
    {'id': 'lower_left_tail', 'source_rect_px': [0, 256, 512, 256], 'uv': [0, 0, 1, .5],
     'tail_tip_px': [121, 455], 'text_rect_px': [88, 282, 336, 80], 'flip_y': True},
]


def read_string_table(path):
    with Path(path).open('r', encoding='utf-8-sig', newline='') as stream:
        rows = csv.DictReader(stream, delimiter='\t')
        if not {'TAG', 'TEXT'}.issubset(rows.fieldnames or []):
            raise ValueError(f'not a TAG/TEXT string table: {Path(path).name}')
        result = {}
        for row in rows:
            key = (row.get('TAG') or '').strip()
            value = (row.get('TEXT') or '').replace('\\n', '\n').strip()
            if key and value:
                result[key.upper()] = value
        return result


def localized_sources(game_root, locale):
    locale = locale.lower()
    folder = LOCALE_FOLDERS.get(locale)
    if folder is None:
        raise ValueError(f'unsupported speech locale: {locale}')
    root = Path(game_root) / 'Strings' / folder
    relative = Path('Strings') / folder
    return (read_string_table(root / 'GAMEPLAY_AUDIO.txt'),
            read_string_table(root / 'GAMEPLAY.txt'),
            {'audio': str(relative / 'GAMEPLAY_AUDIO.txt').replace('\\', '/'),
             'labels': str(relative / 'GAMEPLAY.txt').replace('\\', '/')})


def _audio_row(ex, obj):
    return ex.imp.store.db.execute(
        'SELECT * FROM assets WHERE cab=? AND pathid=? AND type="AudioClip"',
        (obj.assets_file.name, str(obj.path_id))).fetchone()


def _source_for_definition(card_id, emote_id, definition_index, definition):
    return {
        'card_id': card_id,
        'field': f'm_EmoteDefs[{definition_index}]',
        'emote_type': definition.get('m_emoteType'),
        'emote_id': emote_id,
        'sound_ref': definition.get('m_emoteSoundSpellPath', ''),
        'game_string_key': definition.get('m_emoteGameStringKey', ''),
        'bubble_spell_ref': definition.get('m_emoteSpellPath', ''),
    }


def _export_candidate(ex, obj, row, logical_id, index):
    cached = ex.imp.export(row['id'])
    target = ex.folder / cached.name
    if not target.exists():
        publish_audio(target, cached.read_bytes())
    audio_id = f'{logical_id}_{index}'
    ex.registry[audio_id] = target.name
    metadata = json.loads(row['metadata'] or '{}')
    return {
        'id': audio_id,
        'name': row['name'],
        'locale': row['locale'],
        'duration': float(metadata.get('m_Length') or 0),
        'asset_id': row['id'],
        'bundle': row['bundle'],
        'cab': row['cab'],
        'pathid': row['pathid'],
        'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
    }


def _export_bubble_art(ex):
    row = ex.imp.store.db.execute(
        'SELECT * FROM assets WHERE name=? AND type="Texture2D" ORDER BY bundle LIMIT 1',
        (BUBBLE_ASSET,)).fetchone()
    if row is None:
        ex.errors.append({'asset': BUBBLE_ASSET, 'error': 'original emote bubble texture not indexed'})
        return None
    ex.named(BUBBLE_LOGICAL_ID, BUBBLE_ASSET)
    filename = ex.registry.get(BUBBLE_LOGICAL_ID)
    if not filename or not (ex.folder / filename).is_file():
        ex.errors.append({'asset': BUBBLE_ASSET, 'error': 'original emote bubble texture export failed'})
        return None
    texture_path = ex.folder / filename
    with Image.open(texture_path) as image:
        image = image.convert('RGBA')
        if image.size != (512, 512):
            ex.errors.append({'asset': BUBBLE_ASSET, 'error': f'unexpected source dimensions: {image.size}'})
            return None
        alpha = image.getchannel('A')
        for region in BUBBLE_REGIONS:
            x, y = region['tail_tip_px']
            if alpha.getpixel((x, y)) <= 200:
                ex.errors.append({'asset': BUBBLE_ASSET, 'error': f"tail anchor outside source alpha: {region['id']}"})
                return None
    return {
        'logical_id': BUBBLE_LOGICAL_ID,
        'asset_name': BUBBLE_ASSET,
        'asset_id': row['id'],
        'bundle': row['bundle'],
        'cab': row['cab'],
        'pathid': row['pathid'],
        'width': json.loads(row['metadata'] or '{}').get('m_Width'),
        'height': json.loads(row['metadata'] or '{}').get('m_Height'),
        'sha256': hashlib.sha256(texture_path.read_bytes()).hexdigest(),
        'region_origin': 'top-left source pixels; UV values use Unity bottom-left convention',
        'region_method': 'the original image visibly contains two 512x256 bubbles; alpha>200 scanline core points verified both tail tips',
        'regions': BUBBLE_REGIONS,
        'status': 'original_texture_atlas',
        'actor_link': 'CardDef m_emoteSpellPath is empty for these standard emotes; the UI actor binding is not claimed.',
    }


def metadata_only_manifest(manifest):
    """Build a reviewable source map without embedding localized speech text or audio."""
    entries = []
    for entry in manifest['entries']:
        sources = [{key: source.get(key) for key in (
            'card_id', 'field', 'emote_type', 'emote_id', 'sound_ref',
            'game_string_key', 'bubble_spell_ref')}
            for source in entry.get('sources', [])]
        candidates = [{key: candidate.get(key) for key in (
            'id', 'name', 'locale', 'resolved_locale', 'duration', 'asset_id',
            'bundle', 'cab', 'pathid', 'reference', 'source_bundle', 'sha256')}
            for candidate in entry.get('candidates', [])]
        entries.append({'card_id': entry['card_id'], 'emote_id': entry['emote_id'],
                        'game_string_key': entry.get('game_string_key', ''),
                        'subtitle_available': bool(entry.get('text')),
                        'sources': sources, 'candidates': candidates})
    return {
        'schema_version': 1,
        'locale': manifest['locale'],
        'source_files': manifest['source_files'],
        'entry_count': len(entries),
        'candidate_count': sum(len(entry['candidates']) for entry in entries),
        'entries': entries,
        'bubble_art': manifest.get('bubble_art'),
    }


def save_tracked_manifest(manifest, lock_root):
    current = metadata_only_manifest(manifest)
    with asset_lock(lock_root):
        TRACKED_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
        previous = json.loads(TRACKED_MANIFEST.read_text(encoding='utf-8')) if TRACKED_MANIFEST.exists() else {}
        if previous.get('locale') != current['locale']:
            previous = {}
        selected = {entry['card_id'] for entry in current['entries']}
        entries = {f"{entry['card_id']}:{entry['emote_id']}": entry for entry in previous.get('entries', [])
                   if entry.get('card_id') not in selected}
        entries.update({f"{entry['card_id']}:{entry['emote_id']}": entry for entry in current['entries']})
        merged = dict(current, entries=sorted(entries.values(), key=lambda row: (row['card_id'], row['emote_id'])))
        merged['entry_count'] = len(merged['entries'])
        merged['candidate_count'] = sum(len(entry.get('candidates', [])) for entry in merged['entries'])
        merged['complete'] = (merged['entry_count'] == 99 and merged['candidate_count'] >= 99 and
                              len({entry['card_id'] for entry in merged['entries']}) == 11 and
                              all(entry.get('subtitle_available') and entry.get('candidates') for entry in merged['entries']))
        atomic_json(TRACKED_MANIFEST, merged)


def export_hero_speech(ex, locale='zhcn', hero_ids=None):
    audio_strings, ui_strings, string_sources = localized_sources(ex.imp.game, locale)
    selected = set(hero_ids or HEROES.values())
    unknown = selected - set(HEROES.values())
    if unknown:
        raise ValueError('not a base hero card id: ' + ', '.join(sorted(unknown)))

    labels = []
    for emote_id in MENU_LABELS:
        key = 'GAMEPLAY_EMOTE_LABEL_' + emote_id
        value = ui_strings.get(key)
        if value:
            labels.append({'id': emote_id, 'text': value, 'game_string_key': key})
        else:
            ex.errors.append({'label': emote_id, 'game_string_key': key, 'error': 'localized label missing'})

    entries = []
    source_rows = []
    for _, card_id in sorted(HEROES.items()):
        if card_id not in selected:
            continue
        definition = ex.imp.reader.definition(card_id)
        emote_defs = definition.get('m_EmoteDefs', [])
        for emote_id, emote_type in EMOTE_TYPES.items():
            definitions = [(index, row) for index, row in enumerate(emote_defs)
                           if row.get('m_emoteType') == emote_type]
            if not definitions:
                ex.errors.append({'card_id': card_id, 'emote_id': emote_id, 'error': 'CardDef emote type missing'})
                continue
            candidates = []
            sources = []
            game_string_key = ''
            text = ''
            for definition_index, emote in definitions:
                source = _source_for_definition(card_id, emote_id, definition_index, emote)
                sources.append(source)
                if not game_string_key:
                    game_string_key = source['game_string_key']
                    text = audio_strings.get(game_string_key.upper(), '')
                if not source['sound_ref']:
                    ex.errors.append({'card_id': card_id, 'emote_id': emote_id,
                                      'field': source['field'], 'error': 'CardDef sound reference missing'})
                    continue
                walk_errors = []
                try:
                    root, source_bundle, actual_locale = ex.imp.reader.resolve(source['sound_ref'], locale)
                    found = {}
                    for obj, _, _ in ex.imp.reader.walk(root, locale=locale, errors=walk_errors,
                            with_conditions=True, condition_match=lambda branch: False):
                        if obj.type.name == 'AudioClip':
                            found[ex.key(obj)] = obj
                except (KeyError, OSError, ValueError) as exc:
                    ex.errors.append({'card_id': card_id, 'emote_id': emote_id,
                                      'field': source['field'], 'error': str(exc)})
                    continue
                for obj in found.values():
                    row = _audio_row(ex, obj)
                    if row is None:
                        walk_errors.append(f'AudioClip missing from index: {obj.peek_name() or obj.path_id}')
                        continue
                    logical_id = f'HERO_SPEECH_{card_id}_{emote_id}'
                    try:
                        candidate = _export_candidate(ex, obj, row, logical_id, len(candidates))
                        candidate['reference'] = source['sound_ref']
                        candidate['resolved_locale'] = actual_locale
                        candidate['source_bundle'] = source_bundle
                        candidates.append(candidate)
                    except (OSError, ValueError) as exc:
                        ex.errors.append({'card_id': card_id, 'emote_id': emote_id,
                                          'asset_id': row['id'], 'error': str(exc)})
                if walk_errors:
                    ex.errors.extend({'card_id': card_id, 'emote_id': emote_id, 'error': error}
                                     for error in walk_errors)
            if not text:
                ex.errors.append({'card_id': card_id, 'emote_id': emote_id,
                                  'game_string_key': game_string_key, 'error': 'localized subtitle missing'})
            source_rows.extend(sources)
            logical_id = f'HERO_SPEECH_{card_id}_{emote_id}'
            entries.append({
                'id': logical_id,
                'card_id': card_id,
                'emote_id': emote_id,
                'game_string_key': game_string_key,
                'text': text,
                'label': next((row['text'] for row in labels if row['id'] == emote_id), emote_id),
                'audio_id': candidates[0]['id'] if candidates else '',
                'duration': candidates[0]['duration'] if candidates else 0,
                'candidates': candidates,
                'sources': sources,
            })

    bubble_art = _export_bubble_art(ex)
    return {
        'schema_version': 1,
        'locale': locale.lower(),
        'entries': entries,
        'labels': labels,
        'bubble_art': bubble_art,
        'source_files': string_sources,
        'source_rows': source_rows,
        'errors': ex.errors,
    }


def save_report(ex, manifest):
    folder = ex.folder
    with asset_lock(ex.imp.local):
        index_path = folder / 'hero-speech-index.json'
        previous = json.loads(index_path.read_text(encoding='utf-8')) if index_path.exists() else {}
        if previous.get('locale') != manifest['locale']:
            previous = {}
        selected_cards = {row['card_id'] for row in manifest['entries']}
        merged_entries = {row['id']: row for row in previous.get('entries', [])
                          if row.get('card_id') not in selected_cards}
        merged_entries.update({row['id']: row for row in manifest['entries']})
        merged_sources = [row for row in previous.get('source_rows', [])
                          if row.get('card_id') not in selected_cards]
        merged_sources.extend(manifest['source_rows'])
        merged_labels = {row['id']: row for row in previous.get('labels', [])}
        merged_labels.update({row['id']: row for row in manifest['labels']})
        index = dict(manifest, entries=sorted(merged_entries.values(), key=lambda row: (row['card_id'], row['emote_id'])),
                     source_rows=sorted(merged_sources, key=lambda row: (row['card_id'], row['emote_id'], row['field'])),
                     labels=list(merged_labels.values()))
        atomic_json(index_path, index)
        atomic_json(folder / 'hero-speech-source.json', manifest)
        atomic_json(folder / 'hero-speech-report.json', {
            'locale': manifest['locale'],
            'hero_count': len({row['card_id'] for row in manifest['entries']}),
            'entry_count': len(manifest['entries']),
            'candidate_count': sum(len(row['candidates']) for row in manifest['entries']),
            'missing_entries': [row['id'] for row in manifest['entries'] if not row['candidates'] or not row['text']],
            'errors': manifest['errors'],
        })
    save_tracked_manifest(manifest, ex.imp.local)
    ex.save()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--game', default=str(DEFAULT_GAME))
    parser.add_argument('--locale', default='zhcn')
    parser.add_argument('--heroes', default='', help='comma-separated base hero card IDs; defaults to all 11')
    args = parser.parse_args()
    selected = [value for value in args.heroes.split(',') if value] or None
    ex = RuntimeExport(Importer(game=args.game))
    manifest = export_hero_speech(ex, args.locale, selected)
    save_report(ex, manifest)
    print(json.dumps({
        'heroes': len({row['card_id'] for row in manifest['entries']}),
        'entries': len(manifest['entries']),
        'candidates': sum(len(row['candidates']) for row in manifest['entries']),
        'errors': manifest['errors'],
    }, ensure_ascii=False))


if __name__ == '__main__':
    main()
