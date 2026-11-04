"""历史 ID/数值与本地显示数据分开，未实现卡不会作为空脚本开放。"""
from __future__ import annotations

import json
import logging
import re
from functools import lru_cache
from collections import Counter
from pathlib import Path

from fireplace import cards
from hearthstone.enums import CardClass, CardType, GameTag

ROOT = Path(__file__).resolve().parents[1]
CLASS_NAMES = {2: '德鲁伊', 3: '猎人', 4: '法师', 5: '圣骑士', 6: '牧师',
               7: '潜行者', 8: '萨满祭司', 9: '术士', 10: '战士', 14: '恶魔猎手', 1: '死亡骑士'}
HEROES = {2: 'HERO_06', 3: 'HERO_05', 4: 'HERO_08', 5: 'HERO_04', 6: 'HERO_09',
          7: 'HERO_03', 8: 'HERO_02', 9: 'HERO_07', 10: 'HERO_01', 14: 'HERO_10', 1: 'HERO_11'}


def initialize():
    logging.getLogger('fireplace').setLevel(logging.ERROR)
    if not cards.db.initialized:
        cards.db.initialize()
    from .death_knight import register
    register()
    from .history_fixes import register as register_history
    register_history()


@lru_cache(maxsize=1)
def scope():
    initialize()
    result = []
    for c in cards.db.values():
        original_hof = int(c.card_set) == 4 and re.match(r'^(CS[12]|EX1|NEW1|DS1|ds1|tt_)', c.id)
        if c.collectible and (int(c.card_set) in (2, 3, 1463) or original_hof):
            if c.type in (CardType.MINION, CardType.SPELL, CardType.WEAPON):
                result.append(c.id)
    from .death_knight import DK_IDS
    result.extend(DK_IDS)
    return sorted(result)


def validate_deck(class_id, deck, *, require_complete=True):
    if class_id not in HEROES:
        raise ValueError('unknown class')
    if (require_complete and len(deck) != 30) or (not require_complete and len(deck) > 30):
        raise ValueError('deck must contain exactly 30 cards')
    allowed = set(scope())
    for card_id, count in Counter(deck).items():
        if card_id not in allowed:
            raise ValueError(f'card outside playable scope: {card_id}')
        c = cards.db[card_id]
        if int(c.card_class) not in (12, class_id):
            raise ValueError(f'class restriction: {card_id}')
        if count > (1 if int(c.rarity) == 5 else 2):
            raise ValueError(f'copy limit: {card_id}')
    if class_id == 1:
        runes = [max((cards.db[i].tags.get(tag, 0) for i in deck), default=0)
                 for tag in (GameTag.COST_BLOOD, GameTag.COST_FROST, GameTag.COST_UNHOLY)]
        if sum(runes) > 3:
            raise ValueError('death knight deck needs more than three runes')


def preset(class_id):
    initialize()
    pool = [cards.db[i] for i in scope() if int(cards.db[i].card_class) in (12, class_id)]
    if class_id == 1:
        pool = [c for c in pool if not c.tags.get(GameTag.COST_BLOOD) and not c.tags.get(GameTag.COST_FROST)]
    # 稳定曲线优先，避免随机生成大量高费或无随从的不可测试卡组。
    priorities = {2: 2, 3: 1, 4: 0, 5: 3, 6: 4, 1: 5, 7: 6, 0: 7}
    pool.sort(key=lambda c: (int(c.card_class) == 12, priorities.get(c.cost, 10),
                             c.type != CardType.MINION, c.id))
    chosen, minions = [], 0
    for c in pool:
        if c.cost > 7 or (c.type != CardType.MINION and len(chosen) > 12):
            continue
        n = 1 if int(c.rarity) == 5 else 2
        chosen.extend([c.id] * min(n, 30 - len(chosen)))
        minions += n if c.type == CardType.MINION else 0
        if len(chosen) == 30:
            break
    return chosen


def local_records():
    path = ROOT / 'local/probes/CARD.json'
    return {r['m_noteMiniGuid']: r for r in json.loads(path.read_text(encoding='utf-8'))} if path.exists() else {}


def localized_text(record, field, locale_index=12):
    values = record.get(field, {}).get('m_locValues', [])
    if not isinstance(values, (list, tuple)) or locale_index < 0 or locale_index >= len(values):
        return ''
    value = values[locale_index]
    return value if isinstance(value, str) else ''


def label(record, field, fallback):
    return localized_text(record, field) or fallback


def local_relationships(local=None, tag_rows=None):
    if local is None:
        local = local_records()
    if tag_rows is None:
        path = ROOT / 'local/probes/CARD_TAG.json'
        tag_rows = json.loads(path.read_text(encoding='utf-8')) if path.exists() else []

    dbf_to_card_id = {}
    for card_id, record in local.items():
        try:
            dbf_to_card_id[int(record['m_ID'])] = card_id
        except (KeyError, TypeError, ValueError):
            continue

    relation_tags = {
        int(GameTag.QUEST_REWARD_DATABASE_ID),
        int(GameTag.COLLECTION_RELATED_CARD_DATABASE_ID),
    }
    relationships = {}
    for row in tag_rows:
        try:
            if int(row['m_tagId']) not in relation_tags:
                continue
            source_id = dbf_to_card_id[int(row['m_cardId'])]
            target_id = dbf_to_card_id[int(row['m_tagValue'])]
        except (KeyError, TypeError, ValueError):
            continue
        targets = relationships.setdefault(source_id, [])
        if target_id not in targets:
            targets.append(target_id)
    return relationships


def card_detail_metadata(card_id, local, relationships):
    record = local.get(card_id) or {}
    artist = record.get('m_artistName') or ''
    return {
        'FlavorText': localized_text(record, 'm_flavorText'),
        'ArtistName': artist if isinstance(artist, str) else '',
        'RelatedCardIds': list(relationships.get(card_id, [])),
    }


def build_database():
    initialize()
    local = local_records()
    relationships = local_relationships(local)
    allowed = set(scope())
    definitions = []
    for c in cards.db.values():
        r = local.get(c.id, {})
        detail = card_detail_metadata(c.id, local, relationships)
        definitions.append({'CardId': c.id, 'Name': c.strings[GameTag.CARDNAME].get('zhCN') or label(r, 'm_name', c.name),
                            'Class': int(c.card_class), 'Set': int(c.card_set), 'Type': int(c.type),
                            'Cost': c.cost, 'Attack': c.atk, 'Health': c.health,
                            'Durability': c.durability, 'Race': int(c.race), 'Rarity': int(c.rarity),
                            'EliteTag': int(c.tags.get(GameTag.ELITE, 0)), 'MercenaryTag': int(c.tags.get(1665, 0)),
                            'Text': c.strings[GameTag.CARDTEXT_INHAND].get('zhCN') or c.description,
                            'FlavorText': detail['FlavorText'], 'ArtistName': detail['ArtistName'],
                            'RelatedCardIds': detail['RelatedCardIds'],
                            'Blood': c.tags.get(GameTag.COST_BLOOD, 0), 'Frost': c.tags.get(GameTag.COST_FROST, 0), 'Unholy': c.tags.get(GameTag.COST_UNHOLY, 0),
                            'Collectible': bool(c.collectible), 'Mechanics': [int(k) for k, v in c.tags.items() if v],
                            'Targeting': {int(k): v for k, v in c.requirements.items()},
                            'ArtAsset': 'CARD_' + c.id, 'AudioAsset': 'VOICE_' + c.id,
                            'VisualAsset': 'VFX_' + c.id, 'ScriptId': c.id,
                            'Playable': c.id in allowed})
    output = {'cards': definitions, 'classes': [
        {'id': i, 'name': n, 'hero': HEROES[i], 'deck': preset(i), 'available': i != 1 or 'HERO_11' in cards.db}
        for i, n in CLASS_NAMES.items()]}
    (ROOT / 'local/card-database.json').write_text(json.dumps(output, ensure_ascii=False), encoding='utf-8', newline='\n')
    (ROOT / 'data').mkdir(exist_ok=True)
    (ROOT / 'data/scope.json').write_text(json.dumps({'snapshot': 53261, 'death_knight_snapshot': 159202, 'playable_ids': scope()}, indent=2), encoding='utf-8', newline='\n')
    print('Database:', len(definitions), 'scope:', len(scope()))
    return output


if __name__ == '__main__':
    build_database()
