import copy

import pytest

from game.database import preset
from game.match import Match, card_view


def action(match, seat, move, identity):
    return match.apply(seat, dict(move, action_id=identity, version=match.version, match_id=match.id))


def fixture():
    match = Match(['MageA', 'MageB'], [4, 4], [preset(4), preset(4)], 52)
    for seat in range(2):
        action(match, seat, dict(kind='mulligan', cards=[]), f'm{seat}')
    player = match.game.current_player
    player.max_mana = 10
    player.used_mana = 0
    return match, player, match.game.players.index(player)


def class_fixture(class_id):
    match = Match(['PlayerA', 'PlayerB'], [class_id, class_id],
                  [preset(class_id), preset(class_id)], 52)
    for seat in range(2):
        action(match, seat, dict(kind='mulligan', cards=[]), f'class-m{seat}')
    player = match.game.current_player
    player.max_mana = 10
    player.used_mana = 0
    return match, player, match.game.players.index(player)


@pytest.mark.parametrize('replace_weapon', [False, True])
def test_rogue_power_equips_exactly_one_authoritative_dagger(replace_weapon):
    match, player, seat = class_fixture(7)
    old = player.summon('CS2_082') if replace_weapon else None
    before_mana = player.mana
    move = next(m for m in match.legal_moves(seat) if m['kind'] == 'power')
    assert move['target'] == 0
    assert action(match, seat, move, 'rogue-dagger')
    assert player.weapon.id == 'CS2_082'
    assert player.weapon.atk == 1 and player.weapon.durability == 2
    assert player.mana == before_mana - 2
    if old is not None:
        assert player.weapon.entity_id != old.entity_id
    views = [match.snapshot(s) for s in range(2)]
    assert views[0]['public_hash'] == views[1]['public_hash']
    for view in views:
        equips = [e for e in view['events'] if e['type'] == 'Summon' and e.get('card_type') == 7]
        assert len(equips) == 1 and equips[0]['target'] == player.weapon.entity_id
        assert len([e for e in view['events'] if e['type'] == 'PlayHeroPower']) == 1
        assert view['players'][seat]['weapon']['entity'] == player.weapon.entity_id


@pytest.mark.parametrize('target_kind', ['own_hero', 'enemy_hero', 'own_minion', 'enemy_minion'])
def test_fireblast_real_legal_targets_charge_damage_and_both_views(target_kind):
    match, player, seat = fixture()
    target = dict(own_hero=lambda: player.hero, enemy_hero=lambda: player.opponent.hero,
                  own_minion=lambda: player.summon('CS2_182'),
                  enemy_minion=lambda: player.opponent.summon('CS2_182'))[target_kind]()
    move = next(m for m in match.legal_moves(seat) if m['kind'] == 'power' and m['target'] == target.entity_id)
    before_health, before_mana = target.health, player.mana
    command = dict(move, action_id='power', version=match.version, match_id=match.id)
    assert match.apply(seat, command)
    assert target.health == before_health - 1
    assert player.mana == before_mana - 2
    assert player.hero.power.activations_this_turn == 1
    assert not any(m['kind'] == 'power' for m in match.legal_moves(seat))
    assert not match.apply(seat, command)
    views = [match.snapshot(s) for s in range(2)]
    assert views[0]['public_hash'] == views[1]['public_hash']
    assert all(v['players'][seat]['power']['used'] for v in views)
    assert all(v['players'][1-seat]['power']['used'] is False for v in views)
    for _ in range(2):
        active = match.game.players.index(match.game.current_player)
        action(match, active, dict(kind='end_turn'), f'end{_}')
    assert not card_view(player.hero.power)['used']
    assert any(m['kind'] == 'power' for m in match.legal_moves(seat))


def test_fireblast_invalid_target_no_mana_or_state_change():
    match, player, seat = fixture()
    player.opponent.summon('CS2_182').cant_be_targeted_by_hero_powers = True
    forbidden = player.opponent.field[-1]
    before = copy.deepcopy(match.dump())
    for target in (0, 999999, forbidden.entity_id):
        with pytest.raises(ValueError, match='illegal move'):
            action(match, seat, dict(kind='power', entity=player.hero.power.entity_id, target=target), f'invalid{target}')
        assert match.dump() == before


def test_fireblast_snapshot_damage_uses_existing_hero_power_modifier_not_spellpower():
    match, player, seat = fixture()
    modifier = player.summon('CS2_182')
    modifier.spellpower = 4
    assert card_view(player.hero.power)['display_damage'] == 1
    modifier.heropower_damage = 2
    snapshot = match.snapshot(seat)['players'][seat]['power']
    assert snapshot['display_damage_known'] and snapshot['display_damage'] == 3
    target = player.opponent.hero
    health = target.health
    move = next(m for m in match.legal_moves(seat) if m['kind'] == 'power' and m['target'] == target.entity_id)
    action(match, seat, move, 'modified-power')
    assert target.health == health - snapshot['display_damage']


@pytest.mark.parametrize('modifier_id', ['EX1_350', 'GIL_646'])
def test_priest_snapshot_healing_matches_hero_power_heal_helper(modifier_id):
    match, player, _ = class_fixture(6)
    power = player.hero.power
    base = card_view(power)
    assert base['display_heal_known'] and base['display_heal'] == player.get_heropower_heal(2) == 2

    player.summon(modifier_id)
    modified = match.snapshot(match.game.players.index(player))['players'][match.game.players.index(player)]['power']
    expected = player.get_heropower_heal(2)
    assert modified['display_heal_known'] and modified['display_heal'] == expected == 4

    player.hero.damage = 5
    before = player.hero.health
    power.use(target=player.hero)
    assert player.hero.health - before == expected


def test_warlock_snapshot_damage_matches_hero_power_damage_helper_and_effect():
    match, player, seat = class_fixture(9)
    power = player.hero.power
    base = card_view(power)
    assert base['display_damage_known'] and base['display_damage'] == player.get_heropower_damage(2) == 2

    player.summon('GIL_646')
    modified = match.snapshot(seat)['players'][seat]['power']
    expected = player.get_heropower_damage(2)
    assert modified['display_damage_known'] and modified['display_damage'] == expected == 4

    before = player.hero.health
    power.use()
    assert before - player.hero.health == expected


def test_hunter_snapshot_damage_matches_hero_power_damage_helper_and_effect():
    match, player, seat = class_fixture(3)
    power = player.hero.power
    base = card_view(power)
    assert base['display_damage_known'] and base['display_damage'] == player.get_heropower_damage(2) == 2

    player.summon('GIL_646')
    modified = match.snapshot(seat)['players'][seat]['power']
    expected = player.get_heropower_damage(2)
    assert modified['display_damage_known'] and modified['display_damage'] == expected == 4

    before = player.opponent.hero.health
    power.use()
    assert before - player.opponent.hero.health == expected


@pytest.mark.parametrize('cost', [0, 1, 3])
def test_fireblast_snapshot_cost_and_authority_charge_follow_existing_modifier(cost):
    match, player, seat = fixture()
    player.hero.power.cost = cost
    snapshot = match.snapshot(seat)['players'][seat]['power']
    assert snapshot['cost'] == cost
    mana = player.mana
    move = next(m for m in match.legal_moves(seat) if m['kind'] == 'power')
    action(match, seat, move, 'modified-cost')
    assert player.mana == mana - snapshot['cost']


def test_human_review_fixture_uses_warlock_turtle_and_real_target_moves():
    from tools.fireblast_evidence import prepare
    match = prepare(Match(['Mage', 'Warlock'], [4, 9], [preset(4), preset(9)], 52), review=True)
    state = match.snapshot(0)
    assert [p['power']['card_id'] for p in state['players']] == ['HERO_08bp', 'HERO_07bp']
    assert all(p['field'][0]['card_id'] == 'CS2_119' for p in state['players'])
    assert state['players'][1]['hero']['health'] == 28
    moves = match.legal_moves(0)
    target = state['players'][1]['hero']['entity']
    for card_id in ('CS2_029', 'CS2_189'):
        entity = next(c['entity'] for c in state['players'][0]['hand'] if c['card_id'] == card_id)
        assert any(m['kind'] == 'play' and m['entity'] == entity and m['target'] == target for m in moves)
    assert any(m['kind'] == 'attack' and m['entity'] == state['players'][0]['field'][0]['entity'] and m['target'] == target for m in moves)


def test_hunter_evidence_fixture_uses_authoritative_automatic_hero_target():
    from tools.fireblast_evidence import prepare
    match = prepare(Match(['HunterA', 'HunterB'], [3, 3], [preset(3), preset(3)], 52), hunter=True)
    state = match.snapshot(0)
    assert [p['power']['card_id'] for p in state['players']] == ['HERO_05bp', 'HERO_05bp']
    assert state['players'][0]['power']['used'] is False
    assert state['players'][1]['power']['used'] is True
    assert state['players'][0]['hero']['health'] == 28
    move = next(m for m in match.legal_moves(0) if m['kind'] == 'power')
    assert move['target'] == 0
    target_health = match.game.players[0].opponent.hero.health
    action(match, 0, move, 'hunter-auto-target')
    assert match.game.players[0].opponent.hero.health == target_health - 2
    views = [match.snapshot(seat) for seat in range(2)]
    assert views[0]['public_hash'] == views[1]['public_hash']


def test_hunter_power_event_links_hero_source_to_power_hit_source():
    from tools.fireblast_evidence import prepare
    match = prepare(Match(['HunterA', 'HunterB'], [3, 3], [preset(3), preset(3)], 52), hunter=True)
    player, opponent = match.game.players
    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power')
    action(match, 0, move, 'hunter-event-contract')

    events = match.snapshot(0)['events']
    power_event = next(event for event in events if event['type'] == 'PlayHeroPower')
    hit_event = next(event for event in events if event['type'] == 'Hit')
    assert player.hero.power.id == 'HERO_05bp'
    assert (power_event['source'], power_event['target'], power_event['owner']) == (
        player.entity_id, player.hero.power.entity_id, 0)
    assert (hit_event['source'], hit_event['target'], hit_event['owner'], hit_event['amount']) == (
        player.hero.power.entity_id, opponent.hero.entity_id, 1, 2)


def test_priest_evidence_fixture_mage_hits_before_single_own_hero_heal():
    from tools.fireblast_evidence import prepare
    match = prepare(Match(['PriestA', 'MageB'], [6, 4], [preset(6), preset(4)], 52), priest=True)
    priest, mage = match.game.players
    priest_view = match.snapshot(0)

    assert [p['power']['card_id'] for p in priest_view['players']] == ['HERO_09bp', 'HERO_08bp']
    assert priest.hero.health == 28
    assert priest_view['players'][0]['power']['used'] is False
    assert priest_view['players'][1]['power']['used'] is True
    assert mage.get_heropower_damage(1) == 2

    move = next(move for move in match.legal_moves(0) if move['kind'] == 'power')
    assert move['target'] == priest.hero.entity_id
    mana = priest.mana
    action(match, 0, move, 'priest-own-hero-heal')

    events = match.snapshot(0)['events']
    power_events = [event for event in events if event['type'] == 'PlayHeroPower']
    spend_events = [event for event in events if event['type'] == 'SpendMana'
                    and event['source'] == priest.hero.power.entity_id]
    heals = [event for event in events if event['type'] == 'Heal']
    assert len(power_events) == 1
    assert len(spend_events) == 1
    assert (spend_events[0]['source'], spend_events[0]['target'], spend_events[0]['owner'], spend_events[0]['amount']) == (
        priest.hero.power.entity_id, priest.entity_id, 0, 2)
    assert (power_events[0]['source'], power_events[0]['target'], power_events[0]['owner'], power_events[0]['amount']) == (
        priest.entity_id, priest.hero.power.entity_id, 0, 0)
    assert len(heals) == 1
    assert (heals[0]['source'], heals[0]['target'], heals[0]['owner'], heals[0]['amount']) == (
        priest.hero.power.entity_id, priest.hero.entity_id, 0, 2)
    assert priest.hero.health == 30 and priest.mana == mana - 2
    views = [match.snapshot(seat) for seat in range(2)]
    assert all(view['players'][0]['power']['used'] for view in views)
    assert views[0]['public_hash'] == views[1]['public_hash']
