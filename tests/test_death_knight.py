import pytest

from hearthstone.enums import CardType, GameTag, Zone
from fireplace import actions, cards
from game.database import initialize, preset, validate_deck
from game.death_knight import DK_IDS, consume
from game.match import Match


initialize()


def fixture():
    match = Match(['DK1', 'DK2'], [1, 1], [preset(1), preset(1)], seed=42)
    for p in match.game.players:
        p.choice.choose()
        for c in list(p.hand):
            c.zone = Zone.GRAVEYARD
        p.max_mana = 10
        p.used_mana = 0
        p.corpses = 10
    return match, match.game.current_player


def play(match, player, card_id, target=None):
    card = player.give(card_id)
    move = next(m for m in match.legal_moves(match.game.players.index(player))
                if m['kind'] == 'play' and m['entity'] == card.entity_id
                and (target is None or m['target'] == target.entity_id))
    match.apply(match.game.players.index(player), dict(move, match_id=match.id,
                version=match.version, action_id=f'play-{match.version}'))
    return card


@pytest.mark.parametrize('card_id', list(DK_IDS))
def test_every_dk_card_can_execute_through_authoritative_moves(card_id):
    match, p = fixture()
    p.summon('RLK_018t')
    p.opponent.summon('RLK_018t')
    p.opponent.summon('CS2_200')
    p.give('RLK_038')
    p.give('RLK_503')
    card = play(match, p, card_id)
    if p.choice:
        choice = p.choice.cards[0]
        match.apply(match.game.players.index(p), dict(kind='choose', entity=choice.entity_id,
                    match_id=match.id, version=match.version, action_id='choice'))
    assert card.zone in (Zone.PLAY, Zone.GRAVEYARD)
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']
    if card.type == CardType.MINION:
        assert card in p.field
        assert card.atk >= card.data.atk
        assert card.health >= card.data.health
    elif card.type == CardType.WEAPON:
        assert p.weapon is card
        assert card.durability == card.data.durability


def test_rune_limits_and_unholy_preset():
    validate_deck(1, preset(1))
    deck = preset(1)
    deck[-2:] = ['RLK_063', 'RLK_085']
    with pytest.raises(ValueError, match='runes'):
        validate_deck(1, deck)


@pytest.mark.parametrize('card_id,damage,freeze', [
    ('RLK_015', 3, True), ('RLK_018', 3, False), ('RLK_024', 6, False),
    ('RLK_025', 3, False), ('RLK_034', 3, False), ('RLK_038', 2, True),
    ('RLK_063', 5, True), ('RLK_512', 4, False)])
def test_targeted_damage(card_id, damage, freeze):
    match, p = fixture()
    target = p.opponent.summon('CS2_200')
    p.hero.damage = 10
    play(match, p, card_id, target)
    assert target.health == 7-damage
    assert target.frozen == freeze
    if card_id == 'RLK_024':
        assert p.hero.health == 26


@pytest.mark.parametrize('card_id,token,count,spent', [
    ('RLK_060', 'RLK_008t', 5, 5), ('RLK_077', 'RLK_077t', 2, 0),
    ('RLK_118', 'RLK_118t3', 2, 4), ('RLK_705', 'RLK_705t', 2, 0),
    ('RLK_752', 'RLK_907t', 2, 0)])
def test_summons_and_corpses(card_id, token, count, spent):
    match, p = fixture()
    play(match, p, card_id)
    summoned = [c for c in p.field if c.id == token]
    assert len(summoned) == count
    assert p.corpses == 10-spent
    assert p.corpses_spent == spent
    if card_id == 'RLK_118':
        assert all(c.taunt and c.reborn for c in summoned)


def test_hero_power_and_corpse_exclusions():
    match, p = fixture()
    p.corpses = 0
    p.hero.power.use()
    ghoul = p.field[0]
    assert ghoul.id == 'HERO_11bpt' and ghoul.charge and ghoul.atk == 1
    match.game.end_turn()
    assert not p.field and p.corpses == 1
    token = p.summon('RLK_008t')
    match.game.cheat_action(token, [actions.Destroy(token)])
    assert p.corpses == 1


def test_marrogaar_overflow_buff_and_no_new_corpses():
    match, p = fixture()
    play(match, p, 'RLK_085')
    golems = [c for c in p.field if c.id == 'RLK_085t']
    assert len(golems) == 6
    assert sum(c.atk for c in golems) == 14
    assert all(c.rush for c in golems)
    assert p.corpses == 0 and p.corpses_spent == 10
    match.game.cheat_action(p.hero, [actions.Destroy(c) for c in golems])
    assert p.corpses == 0


def test_giant_cost_counts_spent_corpses():
    _, p = fixture()
    giant = p.give('RLK_744')
    assert giant.cost == 10
    consume(p, 3)
    assert giant.cost == 7


def test_next_spell_discount_consumed_once():
    match, p = fixture()
    other = p.give('RLK_038')
    play(match, p, 'RLK_512', p.opponent.hero)
    assert other.cost == 0
    p.give('RLK_042').play()
    assert other.cost == 1


def test_grain_casts_on_draw_and_replaces_itself():
    match, p = fixture()
    play(match, p, 'RLK_039')
    assert p.corpses == 14
    assert sum(c.id == 'RLK_039t' for c in p.deck) == 4
    top = p.card('RLK_039t')
    top.zone = Zone.DECK
    before = len(p.hand)
    p.draw()
    assert top.zone == Zone.GRAVEYARD
    assert any(c.id == 'RLK_070t' for c in p.field)
    assert len(p.hand) == before+1


def test_deathchiller_and_spell_order():
    match, p = fixture()
    chiller = p.summon('RLK_083')
    target = p.opponent.summon('CS2_200')
    play(match, p, 'RLK_038', target)
    assert p.opponent.hero.health == 29
    assert target.health == 4
    assert chiller.health == 3


def test_abomination_blocks_opponent_healing_until_silenced():
    match, p = fixture()
    abomination = p.summon('RLK_115')
    p.opponent.hero.damage = 8
    match.game.cheat_action(p.opponent.hero, [actions.Heal(p.opponent.hero, 5)])
    assert p.opponent.hero.health == 22
    match.game.cheat_action(p.hero, [actions.Silence(abomination)])
    match.game.cheat_action(p.opponent.hero, [actions.Heal(p.opponent.hero, 5)])
    assert p.opponent.hero.health == 27


def test_corpse_value_is_part_of_public_hash():
    match, p = fixture()
    before = match.snapshot(0)['public_hash']
    p.corpses += 1
    assert match.snapshot(0)['public_hash'] != before
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']


def test_frostmourne_remembers_kill_and_weapon_death_summons():
    match, p = fixture()
    weapon = play(match, p, 'RLK_086')
    target = p.opponent.summon('RLK_503')
    p.hero.attack(target)
    assert target.zone == Zone.GRAVEYARD
    match.game.cheat_action(p.hero, [actions.Destroy(weapon)])
    assert len(p.field) == 1 and p.field[0].id == 'RLK_503'


def test_saurfang_returns_and_pays_health_instead_of_mana():
    match, p = fixture()
    card = play(match, p, 'RLK_082')
    match.game.cheat_action(p.hero, [actions.Destroy(card)])
    returned = next(c for c in p.hand if c.id == 'RLK_082')
    assert returned.costs_health
    mana = p.mana
    returned.play()
    assert p.mana == mana and p.hero.health == 25


def test_bloodworm_chooses_real_hand_card():
    match, p = fixture()
    in_hand = p.give('RLK_503')
    play(match, p, 'RLK_711')
    assert in_hand in p.choice.cards
    p.choice.choose(in_hand)
    assert in_hand.atk == 4 and in_hand.health == 3


def test_patchwerk_removes_one_minion_from_each_opponent_zone():
    match, p = fixture()
    hand = p.opponent.give('CS2_200')
    field = p.opponent.summon('CS2_200')
    before = len(p.opponent.deck)
    play(match, p, 'RLK_071')
    assert hand.zone == Zone.GRAVEYARD and field.zone == Zone.GRAVEYARD
    assert len(p.opponent.deck) == before-1


def test_blood_boil_end_turn_and_lifesteal():
    match, p = fixture()
    target = p.opponent.summon('CS2_200')
    p.hero.damage = 10
    play(match, p, 'RLK_730')
    assert target.health == 7
    match.game.end_turn()
    assert target.health == 5 and p.hero.health == 22, [(b.id, b.source.id, b.source.lifesteal) for b in target.buffs]


@pytest.mark.parametrize('seed', [42, 99, 271])
def test_death_knight_completes_match_with_normal_commands(seed):
    match = Match(['DK', 'DH'], [1, 14], [preset(1), preset(14)], seed=seed)
    for seat in (0, 1):
        match.apply(seat, dict(kind='mulligan', cards=[], match_id=match.id,
                    version=match.version, action_id=f'mulligan-{seat}'))
    for count in range(1500):
        if match.game.ended:
            break
        seat = match.game.players.index(match.game.current_player)
        moves = match.legal_moves(seat)
        ordered = sorted(moves, key=lambda m: ['choose', 'attack', 'play', 'power', 'end_turn'].index(m['kind']))
        match.apply(seat, dict(ordered[0], match_id=match.id, version=match.version, action_id=f'a{count}'))
        assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']
    assert match.game.ended
    assert sorted(p.playstate.name for p in match.game.players) == ['LOST', 'WON']
