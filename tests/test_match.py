import copy
import json

import pytest

from game.database import CLASS_NAMES, preset, scope, validate_deck
from game.match import Match


def create(seed=42):
    return Match(['Shrink1', 'Shrink2'], [4, 10], [preset(4), preset(10)], seed)


def act(m, seat, move, action='test'):
    return m.apply(seat, dict(move, action_id=action, match_id=m.id, version=m.version))


@pytest.mark.parametrize('cls', [c for c in CLASS_NAMES if c != 1])
def test_presets(cls):
    validate_deck(cls, preset(cls))


def test_deck_limits_and_class():
    with pytest.raises(ValueError):
        validate_deck(4, ['CS2_029'] * 30)
    deck = preset(4)
    deck[0] = 'CS2_029'
    with pytest.raises(ValueError):
        validate_deck(10, deck)


def test_private_state_and_determinism():
    a, b = create(), create()
    assert [c.id for c in a.game.players[0].hand] == [c.id for c in b.game.players[0].hand]
    assert a.snapshot(0)['players'][1]['hand'] == []
    assert a.snapshot(0)['public_hash'] == a.snapshot(1)['public_hash']


def test_reject_and_replay():
    m = create()
    before = m.dump()
    with pytest.raises(ValueError):
        act(m, 0, {'kind': 'end_turn'})
    assert m.dump() == before
    cmd = {'kind': 'mulligan', 'cards': [], 'action_id': 'one', 'version': m.version, 'match_id': m.id}
    assert m.apply(0, cmd)
    assert not m.apply(0, cmd)
    assert m.version == 1
    with pytest.raises(ValueError):
        m.apply(0, dict(cmd, cards=[999]))


def test_full_match_by_normal_actions():
    m = create()
    act(m, 0, {'kind': 'mulligan', 'cards': []}, 'm0')
    act(m, 1, {'kind': 'mulligan', 'cards': []}, 'm1')
    for count in range(1200):
        if m.game.ended:
            break
        seat = m.game.players.index(m.game.current_player)
        moves = m.legal_moves(seat)
        attacks = [x for x in moves if x['kind'] == 'attack']
        plays = [x for x in moves if x['kind'] == 'play']
        move = (attacks or plays or [x for x in moves if x['kind'] == 'choose'] or
                [x for x in moves if x['kind'] == 'power'] or moves)[0]
        act(m, seat, move, str(count))
        assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']
    assert m.game.ended
    assert sorted(p.playstate.name for p in m.game.players) == ['LOST', 'WON']


def test_minion_insertion_position_changes_adjacent_aura():
    m = create()
    for p in m.game.players:
        p.choice.choose()
    p = m.game.current_player
    p.max_mana = 10
    p.used_mana = 0
    left = p.summon('CS2_200')
    right = p.summon('CS2_182')
    wolf = p.give('EX1_162')
    seat = m.game.players.index(p)
    move = next(x for x in m.legal_moves(seat) if x['kind'] == 'play' and x['entity'] == wolf.entity_id and x['index'] == 1)
    act(m, seat, move)
    assert list(p.field) == [left, wolf, right]
    assert left.atk == 7 and right.atk == 5


def test_out_of_range_minion_position_rejected_without_mutation():
    m = create()
    for p in m.game.players:
        p.choice.choose()
    p = m.game.current_player
    p.max_mana = 10
    p.used_mana = 0
    card = p.give('CS2_182')
    before = m.dump()
    with pytest.raises(ValueError, match='illegal move'):
        act(m, m.game.players.index(p), dict(kind='play', entity=card.entity_id, target=0, index=8))
    assert m.dump() == before


def test_totemic_call_summon_event_links_the_real_random_totem_to_its_power():
    m = Match(['Shaman', 'Warrior'], [8, 10], [preset(8), preset(10)], seed=20261021)
    for player in m.game.players:
        player.choice.choose()
    shaman = m.game.players[0]
    step = 0
    while m.game.current_player is not shaman:
        seat = m.game.players.index(m.game.current_player)
        act(m, seat, {'kind': 'end_turn'}, 'handoff-' + str(step))
        step += 1
    shaman.max_mana = 10
    shaman.used_mana = 0
    power = shaman.hero.power
    move = next(row for row in m.legal_moves(0)
                if row['kind'] == 'power' and row['entity'] == power.entity_id)

    act(m, 0, move, 'totemic-call')

    events = m.observer.events
    power_events = [row for row in events if row['type'] == 'PlayHeroPower']
    summons = [row for row in events
               if row['type'] == 'Summon' and row['source'] == power.entity_id]
    assert len(power_events) == 1
    assert power_events[0]['target'] == power.entity_id
    assert len(summons) == 1
    summon = summons[0]
    assert summon['target'] != 0
    assert summon['visual']['entity'] == summon['target']
    assert summon['visual']['card_id'] in {'CS2_050', 'CS2_051', 'CS2_052', 'NEW1_009'}
    assert any(card.entity_id == summon['target'] for card in shaman.field)
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']


def test_death_knight_ghoul_power_event_links_the_real_summon_entity_to_its_power():
    m = Match(['DeathKnight', 'Mage'], [1, 4], [preset(1), preset(4)], seed=20261021)
    for player in m.game.players:
        player.choice.choose()
    death_knight = m.game.players[0]
    step = 0
    while m.game.current_player is not death_knight:
        seat = m.game.players.index(m.game.current_player)
        act(m, seat, {'kind': 'end_turn'}, 'dk-handoff-' + str(step))
        step += 1
    death_knight.max_mana = 10
    death_knight.used_mana = 0
    power = death_knight.hero.power
    move = next(row for row in m.legal_moves(0)
                if row['kind'] == 'power' and row['entity'] == power.entity_id)

    act(m, 0, move, 'ghoul-charge')

    power_event = next(row for row in m.observer.events if row['type'] == 'PlayHeroPower')
    summon = next(row for row in m.observer.events
                  if row['type'] == 'Summon' and row['source'] == power.entity_id)
    ghoul = next(card for card in death_knight.field if card.entity_id == summon['target'])
    assert power_event['target'] == summon['source'] == power.entity_id
    assert summon['target'] == summon['visual']['entity'] == ghoul.entity_id
    assert summon['visual']['card_id'] == ghoul.id == 'HERO_11bpt'
    assert ghoul.atk == ghoul.health == 1 and ghoul.charge
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']

    act(m, 0, {'kind': 'end_turn'}, 'ghoul-end-turn')
    assert not any(card.entity_id == ghoul.entity_id for card in death_knight.field)
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']


def test_demon_hunter_power_pairs_the_real_self_hero_buff_and_cost():
    m = Match(['DemonHunter', 'Mage'], [14, 4], [preset(14), preset(4)], seed=20261023)
    for player in m.game.players:
        player.choice.choose()
    demon_hunter = m.game.players[0]
    step = 0
    while m.game.current_player is not demon_hunter:
        seat = m.game.players.index(m.game.current_player)
        act(m, seat, {'kind': 'end_turn'}, 'h10-handoff-' + str(step))
        step += 1
    demon_hunter.max_mana = 10
    demon_hunter.used_mana = 0
    power = demon_hunter.hero.power
    hero = demon_hunter.hero
    before_attack = hero.atk
    before_mana = demon_hunter.mana
    move = next(row for row in m.legal_moves(0)
                if row['kind'] == 'power' and row['entity'] == power.entity_id)
    assert move['target'] == 0
    m.observer.events.clear()

    act(m, 0, move, 'h10-self-hero-buff')

    events = m.observer.events
    assert [row['type'] for row in events] == ['SpendMana', 'PlayHeroPower', 'Buff']
    spend, play, buff = events
    assert spend['source'] == power.entity_id and spend['target'] > 0
    assert spend['owner'] == 0 and spend['amount'] == 1
    assert play['source'] == spend['target'] and play['target'] == power.entity_id
    assert 'card_id' not in play
    assert buff['source'] == power.entity_id and buff['target'] == hero.entity_id
    assert buff['owner'] == 0 and buff['visual']['entity'] == hero.entity_id
    assert buff['visual']['card_id'] == 'HERO_10' and buff['visual']['attack'] == 1
    assert buff['visual']['attack_buffed'] is True
    assert before_attack == 0 and hero.atk == 1
    assert before_mana == 10 and demon_hunter.mana == 9
    assert m.snapshot(0)['players'][0]['power']['used']
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']
