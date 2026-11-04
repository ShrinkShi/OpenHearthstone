"""Server-side economy prices, pack rules, and quest templates."""
from __future__ import annotations

from dataclasses import dataclass


NORMAL_CRAFT_COST = {1: 40, 3: 100, 4: 400, 5: 1600}
NORMAL_DUST_VALUE = {1: 5, 3: 20, 4: 100, 5: 400}
GOLDEN_CRAFT_COST = {1: 400, 3: 800, 4: 1600, 5: 3200}
GOLDEN_DUST_VALUE = {1: 50, 3: 100, 4: 400, 5: 1600}
DEFAULT_PACK_WEIGHTS = {1: 70, 3: 22, 4: 7, 5: 1}
PACK_PRICE = 100
PACK_SIZE = 5


@dataclass(frozen=True)
class QuestTemplate:
    quest_type: str
    title: str
    description: str
    target: int


QUESTS = {
    ("daily", "easy"): [
        QuestTemplate("match_complete", "完成一场对局", "完成1场对局", 1),
        QuestTemplate("attack", "参与战斗", "进行3次攻击", 3),
    ],
    ("daily", "normal"): [
        QuestTemplate("card_play", "打出卡牌", "打出10张卡牌", 10),
        QuestTemplate("attack", "攻击对手", "进行10次攻击", 10),
        QuestTemplate("match_complete", "完成更多对局", "完成2场对局", 2),
    ],
    ("daily", "hard"): [
        QuestTemplate("match_win", "赢得对局", "赢得1场对局", 1),
        QuestTemplate("card_play", "打出更多卡牌", "打出20张卡牌", 20),
    ],
    ("weekly", "easy"): [
        QuestTemplate("match_complete", "完成对局", "完成3场对局", 3),
        QuestTemplate("card_play", "积累实战经验", "打出30张卡牌", 30),
    ],
    ("weekly", "normal"): [
        QuestTemplate("card_play", "打出卡牌", "打出30张卡牌", 30),
        QuestTemplate("attack", "参与战斗", "进行30次攻击", 30),
        QuestTemplate("match_complete", "积累对局经验", "完成5场对局", 5),
    ],
    ("weekly", "hard"): [
        QuestTemplate("match_win", "赢得对局", "赢得3场对局", 3),
        QuestTemplate("match_complete", "完成更多对局", "完成10场对局", 10),
    ],
}


@dataclass(frozen=True)
class QuestSlot:
    quest_id_suffix: str
    difficulty: str
    template: QuestTemplate


QUEST_SLOTS = {
    "daily": (
        QuestSlot("easy", "easy", QUESTS[("daily", "easy")][0]),
        QuestSlot("normal", "normal", QUESTS[("daily", "normal")][0]),
        QuestSlot("hard", "hard", QUESTS[("daily", "hard")][0]),
        QuestSlot("fourth", "normal", QUESTS[("daily", "normal")][2]),
    ),
    "weekly": (
        QuestSlot("easy", "easy", QUESTS[("weekly", "easy")][0]),
        QuestSlot("normal", "normal", QUESTS[("weekly", "normal")][0]),
        QuestSlot("hard", "hard", QUESTS[("weekly", "hard")][0]),
        QuestSlot("fourth", "normal", QUESTS[("weekly", "normal")][2]),
    ),
}

QUEST_REWARDS = {
    ("daily", "easy"): 40,
    ("daily", "normal"): 60,
    ("daily", "hard"): 80,
    ("weekly", "easy"): 80,
    ("weekly", "normal"): 120,
    ("weekly", "hard"): 160,
}
