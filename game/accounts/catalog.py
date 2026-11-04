"""Read-only Hearthstone metadata used by collection and shop rules."""
from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache

from fireplace import cards
from hearthstone.enums import CardSet, CardType, GameTag

from ..database import CLASS_NAMES, HEROES, initialize, scope, validate_deck


SET_NAMES_ZH = {
    CardSet.EXPERT1: "经典卡包",
    CardSet.NAXX: "纳克萨玛斯的诅咒",
    CardSet.GVG: "地精大战侏儒",
    CardSet.BRM: "黑石山的火焰",
    CardSet.TGT: "冠军的试炼",
    CardSet.LOE: "探险者协会",
    CardSet.OG: "上古之神的低语",
    CardSet.KARA: "卡拉赞之夜",
    CardSet.GANGS: "龙争虎斗加基森",
    CardSet.UNGORO: "勇闯安戈洛",
    CardSet.ICECROWN: "冰封王座的骑士",
    CardSet.LOOTAPALOOZA: "狗头人与地下世界",
    CardSet.GILNEAS: "女巫森林",
    CardSet.BOOMSDAY: "砰砰计划",
    CardSet.TROLL: "拉斯塔哈的大乱斗",
    CardSet.DALARAN: "暗影崛起",
    CardSet.ULDUM: "奥丹姆奇兵",
    CardSet.DRAGONS: "巨龙降临",
    CardSet.YEAR_OF_THE_DRAGON: "迦拉克隆的觉醒",
    CardSet.BLACK_TEMPLE: "外域的灰烬",
    CardSet.SCHOLOMANCE: "通灵学园",
    CardSet.DEMON_HUNTER_INITIATE: "恶魔猎手新兵",
    CardSet.CORE: "核心系列",
    CardSet.PATH_OF_ARTHAS: "阿尔萨斯之路",
}

_NON_PACK_SETS = {0, 2, 4, 1453, 1586, 1637, 1705}
# 冒险与固定合集没有普通随机卡包，卡牌仍留在收藏和制作目录。
_NON_PACK_SETS.update({CardSet.NAXX, CardSet.BRM, CardSet.LOE, CardSet.KARA,
                      CardSet.YEAR_OF_THE_DRAGON, CardSet.DEMON_HUNTER_INITIATE,
                      CardSet.PATH_OF_ARTHAS})
_BASE_FREE_SETS = {2, 1635, 1637}
_PACK_CARD_TYPES = {int(CardType.MINION), int(CardType.SPELL), int(CardType.WEAPON)}


@dataclass(frozen=True)
class CardInfo:
    card_id: str
    name: str
    class_id: int
    set_id: int
    rarity: int
    collectible: bool
    cost: int
    card_type: int
    blood: int
    frost: int
    unholy: int


class CardCatalog:
    def __init__(self):
        initialize()
        self.playable_ids = frozenset(scope())
        self.cards: dict[str, CardInfo] = {}
        by_set: dict[int, list[CardInfo]] = {}
        pack_by_set: dict[int, list[CardInfo]] = {}
        for card in cards.db.values():
            if not card.collectible:
                continue
            localized = card.strings[GameTag.CARDNAME].get("zhCN")
            info = CardInfo(card.id, localized or card.name, int(card.card_class),
                            int(card.card_set), int(card.rarity), True, int(card.cost),
                            int(card.type), card.tags.get(GameTag.COST_BLOOD, 0),
                            card.tags.get(GameTag.COST_FROST, 0),
                            card.tags.get(GameTag.COST_UNHOLY, 0))
            self.cards[info.card_id] = info
            by_set.setdefault(info.set_id, []).append(info)
            if info.card_type in _PACK_CARD_TYPES and info.set_id not in _NON_PACK_SETS:
                pack_by_set.setdefault(info.set_id, []).append(info)
        self.by_set = {set_id: tuple(items) for set_id, items in by_set.items()}
        self.pack_by_set = {set_id: tuple(items) for set_id, items in pack_by_set.items()}
        self.pack_sets = tuple(sorted(self.pack_by_set))
        self.class_names = dict(CLASS_NAMES)

    @staticmethod
    def set_name(set_id: int) -> str:
        return SET_NAMES_ZH.get(set_id, f"扩展包 {set_id}")

    def starter_grants(self) -> dict[str, int]:
        return {card.card_id: (1 if card.rarity == 5 else 2)
                for card in self.cards.values() if card.set_id in _BASE_FREE_SETS}

    def starter_decks(self) -> dict[int, list[str]]:
        """Build only legal decks from the BASIC/CORE cards granted at registration."""
        result = {}
        for class_id in HEROES:
            pool = [card for card in self.cards.values()
                    if card.set_id in _BASE_FREE_SETS
                    and card.card_id in self.playable_ids
                    and card.class_id in (12, class_id)
                    and not (class_id == 1 and (card.blood or card.frost))]
            pool.sort(key=lambda card: (card.class_id == 12, card.cost,
                                       card.card_type != int(CardType.MINION), card.card_id))
            deck = []
            for card in pool:
                copies = 1 if card.rarity == 5 else 2
                deck.extend([card.card_id] * min(copies, 30 - len(deck)))
                if len(deck) == 30:
                    break
            if len(deck) == 30:
                try:
                    validate_deck(class_id, deck)
                except (TypeError, ValueError, KeyError):
                    continue
                result[class_id] = deck
        return result


@lru_cache(maxsize=1)
def get_catalog() -> CardCatalog:
    return CardCatalog()
