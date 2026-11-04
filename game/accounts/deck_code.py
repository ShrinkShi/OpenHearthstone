"""炉石卡组代码转换；格式标签不代表服务器已验证标准轮换。"""
from __future__ import annotations

import base64
import binascii
from collections import Counter

from fireplace import cards
from hearthstone.deckstrings import Deck
from hearthstone.enums import FormatType

from ..database import HEROES, initialize

FORMATS = {"wild": FormatType.FT_WILD, "standard": FormatType.FT_STANDARD}


def _check_wire(code: str) -> None:
    if not isinstance(code, str) or not 1 <= len(code) <= 2048:
        raise ValueError("卡组代码无效")
    try:
        data = base64.b64decode(code, validate=True)
    except (ValueError, binascii.Error):
        raise ValueError("卡组代码无效") from None
    at = 0

    def number():
        nonlocal at
        result = 0
        for shift in range(0, 35, 7):
            if at >= len(data):
                raise ValueError("卡组代码不完整")
            byte = data[at]
            at += 1
            result |= (byte & 127) << shift
            if byte < 128:
                return result
        raise ValueError("卡组代码数值无效")

    if number() != 0 or number() != 1 or number() not in (1, 2):
        raise ValueError("卡组代码版本或格式不支持")
    if number() != 1:
        raise ValueError("卡组代码英雄数量不支持")
    number()
    seen = set()
    total = 0
    for group in (1, 2, 0):
        count = number()
        if count > 30:
            raise ValueError("卡组代码卡牌过多")
        for _ in range(count):
            card_id = number()
            copies = group or number()
            if not card_id or card_id in seen or not 1 <= copies <= 30:
                raise ValueError("卡组代码卡牌数量无效")
            seen.add(card_id)
            total += copies
    if not 1 <= total <= 30:
        raise ValueError("卡组代码卡牌数量无效")
    if at < len(data):
        if data[at] != 0:
            raise ValueError("暂不支持包含备牌的卡组代码")
        at += 1
    if at != len(data):
        raise ValueError("卡组代码包含多余数据")


def encode_deck(class_id, card_ids, deck_format):
    initialize()
    if (type(class_id) is not int or class_id not in HEROES
            or not isinstance(deck_format, str) or deck_format not in FORMATS):
        raise ValueError("卡组职业或格式无效")
    if (not isinstance(card_ids, list) or not 1 <= len(card_ids) <= 30
            or any(not isinstance(item, str) for item in card_ids)):
        raise ValueError("卡组卡牌数量无效")
    deck = Deck()
    deck.format = FORMATS[deck_format]
    try:
        deck.heroes = [int(cards.db[HEROES[class_id]].dbf_id)]
        if deck.heroes[0] <= 0 or cards.db.dbf.get(deck.heroes[0]) != HEROES[class_id]:
            raise ValueError()
        deck.cards = [(int(cards.db[card_id].dbf_id), count)
                      for card_id, count in Counter(card_ids).items()]
        if any(dbf <= 0 or not cards.db[card_id].collectible or cards.db.dbf.get(dbf) != card_id
               for card_id, (dbf, _) in zip(Counter(card_ids), deck.cards)):
            raise ValueError()
        return deck.as_deckstring
    except (KeyError, TypeError, ValueError, AttributeError):
        raise ValueError("卡组包含无法编码的卡牌") from None


def decode_deck(code):
    _check_wire(code)
    initialize()
    try:
        deck = Deck.from_deckstring(code)
        hero = cards.db.dbf[deck.heroes[0]]
        class_id = next(key for key, value in HEROES.items() if value == hero)
        card_ids = [cards.db.dbf[dbf] for dbf, count in deck.cards for _ in range(count)]
        if any(not cards.db[card_id].collectible for card_id in card_ids):
            raise ValueError()
        deck_format = next(key for key, value in FORMATS.items() if value == deck.format)
    except (KeyError, TypeError, ValueError, AttributeError, StopIteration, IndexError):
        raise ValueError("卡组代码包含未知英雄或卡牌") from None
    return {"class_id": class_id, "cards": card_ids, "format": deck_format}
