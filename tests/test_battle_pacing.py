from hearthstone.enums import Zone

from tools.visual_scenarios import fixture, transition


def fan_match(smiths=1):
    match = fixture(0, 0, classes=(7, 10))
    player, opponent = match.game.players
    for card in list(opponent.field):
        card.zone = Zone.GRAVEYARD
    for _ in range(smiths):
        opponent.summon('EX1_402')
    if smiths == 1:
        opponent.summon('CS2_171')
        opponent.summon('CS2_172')
    player.give('EX1_129')
    match.observer.events.clear()
    before = match.snapshot(0)
    after = transition(match, 'play', lambda m: match.entity(m.get('entity', 0)).id == 'EX1_129')
    return match, before, after


def test_fan_damage_and_each_armorsmith_trigger_have_public_intermediate_values():
    match, before, after = fan_match()
    assert before['players'][1]['hero']['armor'] == 0
    assert after['players'][1]['hero']['armor'] == 3
    armor = [e for e in after['events'] if e['type'] == 'GainArmor']
    assert [e['visual']['armor'] for e in armor] == [1, 2, 3]
    assert len([e for e in after['events'] if e['type'] == 'Trigger']) == 3
    hits = [e for e in after['events'] if e['type'] == 'Hit']
    assert len(hits) == 3 and all(e['visual']['health'] >= 0 for e in hits)
    assert all(e['owner'] == 1 for e in hits)
    draws = [e for e in after['events'] if e['type'] == 'Draw']
    assert len(draws) == 1 and 'visual' not in draws[0] and 'card_id' not in draws[0]
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']


def test_full_armorsmith_board_preserves_action_start_and_all_trigger_events():
    _, _, after = fan_match(7)
    events = after['events']
    assert len(events) > 80
    assert any(e['type'] == 'CardPlay' and e['card_id'] == 'EX1_129' for e in events)
    assert len([e for e in events if e['type'] == 'Trigger']) == 49
    assert after['players'][1]['hero']['armor'] == 49


def test_used_hero_power_is_distinct_from_insufficient_mana_and_resets():
    match = fixture(0, 0, classes=(7, 4))
    player = match.game.players[0]
    player.used_mana = player.max_mana
    assert not match.snapshot(0)['players'][0]['power']['used']
    player.used_mana = 0
    after = transition(match, 'power')
    assert after['players'][0]['power']['used']
    assert after['players'][0]['weapon']['card_id'] == 'CS2_082'
    match.game.end_turn()
    match.game.end_turn()
    assert not match.snapshot(0)['players'][0]['power']['used']
