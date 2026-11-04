"""固定历史脚本的本项目修正；保持上游参考仓库不变。"""
from functools import lru_cache

from fireplace import actions, cards
from fireplace.dsl import (CONTROLLER, OPPONENT, OWNER, SELF, FRIENDLY_HERO,
                           ENEMY_CHARACTERS, Attr, FuncSelector)
from fireplace.cards.utils import SET
from fireplace.enums import PlayReq
from fireplace.aura import Refresh
from hearthstone.enums import GameTag


@lru_cache(maxsize=1)
def register():
    outermost = FuncSelector(lambda entities, source: [source] if source in
                             source.controller.hand[:1] + source.controller.hand[-1:] else [])
    definitions = {
        'BT_427': {'play': (actions.Draw(CONTROLLER) * Attr(CONTROLLER, 'minions_killed_this_turn'),)},
        'BT_753e': {'events': [actions.BeginTurn(OPPONENT).on(
            actions.ManaThisTurn(OWNER, -2), actions.Destroy(SELF))]},
        'BT_801': {'requirements': {PlayReq.REQ_TARGET_TO_PLAY: 0, PlayReq.REQ_MINION_TARGET: 0},
                   'Hand': type('EyeBeamHand', (), {'update': (Refresh(outermost, {GameTag.COST: SET(1)}),)})},
        'BT_142': {'play': (actions.Buff(FRIENDLY_HERO, 'BT_142e'),)},
        'BT_351': {'events': [actions.Attack(FRIENDLY_HERO).after(actions.Buff(SELF, 'BT_351e'))]},
        'BT_510': {'events': [actions.Attack(None, SELF).after(actions.Hit(ENEMY_CHARACTERS, 1))]},
    }
    for card_id, attributes in definitions.items():
        original = cards.db[card_id]
        cards.db[card_id] = cards.db.merge(card_id, original, type(card_id, (original.scripts,), attributes))
