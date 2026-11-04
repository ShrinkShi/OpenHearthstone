import pytest

from fireplace import actions
from fireplace import cards
from hearthstone.enums import Zone

from game.database import scope
from game.death_knight import DK_IDS
from tools.audit_card_execution import execute, fixture


@pytest.mark.parametrize('card_id', [c for c in scope() if c not in DK_IDS])
def test_every_history_card_executes(card_id):
    execute(card_id)


@pytest.mark.parametrize('card_id,choice', [(c, choice) for c in scope() if c not in DK_IDS
                                          for choice in cards.db[c].choose_cards])
def test_every_choose_one_branch_executes(card_id, choice):
    execute(card_id, choice=choice)


def play(match, p, card, target=None):
    seat = match.game.players.index(p)
    move = next(m for m in match.legal_moves(seat)
                if m['kind'] == 'play' and m['entity'] == card.entity_id
                and (target is None or m['target'] == target.entity_id))
    match.apply(seat, dict(move, match_id=match.id, version=match.version, action_id=f'play-{match.version}'))


def test_feast_of_souls_counts_only_friendly_deaths_this_turn():
    match, p, card = fixture('BT_427')
    for owner in (p, p.opponent):
        match.game.cheat_action(owner.hero, [actions.Destroy(owner.field[0])])
    assert p.minions_killed_this_turn == 1
    before = len(p.hand)
    play(match, p, card)
    assert len(p.hand) == before
    match.game.end_turn()
    match.game.end_turn()
    p.used_mana = 0
    second = p.give('BT_427')
    before = len(p.hand)
    play(match, p, second)
    assert len(p.hand) == before-1


def test_mana_burn_only_reduces_next_enemy_turn_and_stacks():
    match, p, card = fixture('BT_753')
    enemy = p.opponent
    play(match, p, card)
    play(match, p, p.give('BT_753'))
    assert enemy.mana == 10
    match.game.end_turn()
    assert enemy.max_mana == 10 and enemy.mana == 6
    assert not any(b.id == 'BT_753e' for b in enemy.buffs)
    match.game.end_turn()
    match.game.end_turn()
    assert enemy.mana == 10


def test_eye_beam_target_lifesteal_and_outcast_discount():
    match, p, card = fixture('BT_801')
    match.game.refresh_auras()
    target = p.opponent.field[2]
    seat = match.game.players.index(p)
    moves = [m for m in match.legal_moves(seat) if m['kind'] == 'play' and m['entity'] == card.entity_id]
    assert moves and all(match.entity(m['target']) in p.field or match.entity(m['target']) in p.opponent.field for m in moves)
    assert card.cost == 1
    p.give('CS2_182')
    assert card.cost == 3
    p.hand[-1].zone = Zone.GRAVEYARD
    match.game.refresh_auras()
    hero_health = p.hero.health
    health = target.health
    play(match, p, card, target)
    assert target.health == health-3
    assert p.hero.health == hero_health+3


def test_shadowhoof_slayers_hero_buff_expires():
    match, p, card = fixture('BT_142')
    attack = p.hero.atk
    play(match, p, card)
    assert p.hero.atk == attack+1
    match.game.end_turn()
    assert not any(b.id == 'BT_142e' for b in p.hero.buffs)


def test_battlefiend_buffs_itself_after_hero_attack():
    match, p, card = fixture('BT_351')
    play(match, p, card)
    attack = card.atk
    hero_attack = p.hero.atk
    p.hero.attack(p.opponent.field[-1])
    assert card.atk == attack+1
    assert p.hero.atk == hero_attack


def test_wrathspike_only_triggers_when_attacked():
    match, p, card = fixture('BT_510')
    play(match, p, card)
    enemy = p.opponent
    hero_health = enemy.hero.health
    minions = list(enemy.field)
    health = {c.entity_id: c.health for c in minions}
    match.game.end_turn()
    enemy.field[0].attack(card)
    assert enemy.hero.health == hero_health-1
    for c in minions[1:]:
        if c.zone == Zone.PLAY:
            assert c.health == health[c.entity_id]-1
        else:
            assert c.zone == Zone.GRAVEYARD


def test_wrathspike_does_not_trigger_on_its_own_attack():
    match, p, card = fixture('BT_510')
    play(match, p, card)
    match.game.end_turn()
    match.game.end_turn()
    before = p.opponent.hero.health
    card.attack(p.opponent.field[-1])
    assert p.opponent.hero.health == before


def test_eye_beam_discount_on_both_edges_for_second_player():
    match, p, card = fixture('BT_801')
    match.game.end_turn()
    other = p.opponent
    for c in list(other.hand):
        c.zone = Zone.GRAVEYARD
    left = other.give('BT_801')
    middle = other.give('BT_801')
    right = other.give('BT_801')
    match.game.refresh_auras()
    assert [c.cost for c in (left, middle, right)] == [1, 3, 1]
