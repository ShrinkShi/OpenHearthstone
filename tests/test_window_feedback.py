from fireplace import actions

from game.match import card_view
from tools.visual_scenarios import fixture, transition


def test_effective_deathrattle_and_poison_badges_follow_silence():
    match = fixture(0, 0)
    player = match.game.players[0]
    deathrattle = player.summon('EX1_096')
    poisonous = player.summon('EX1_170')
    assert 'deathrattle' in card_view(deathrattle)['keywords']
    assert 'poisonous' in card_view(poisonous)['keywords']
    for minion in (deathrattle, poisonous):
        match.game.cheat_action(player.hero, [actions.Silence(minion)])
        assert not {'deathrattle', 'poisonous'} & set(card_view(minion)['keywords'])


def test_hero_attack_has_both_damage_and_weapon_wear_in_single_authoritative_action():
    match = fixture(0, 0, classes=(7, 10))
    player = match.game.players[0]
    player.give('CS2_082').play()
    weapon = player.weapon
    defender = player.opponent.field[0]
    before_health = player.hero.health
    match.observer.events.clear()
    after = transition(match, 'attack', lambda move: move['entity'] == player.hero.entity_id
                       and move['target'] == defender.entity_id)
    hits = [event for event in after['events'] if event['type'] == 'Hit']
    assert {event['target'] for event in hits} == {player.hero.entity_id, defender.entity_id, weapon.entity_id}
    assert player.hero.health == before_health - defender.atk
    assert weapon.durability == 1
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']


def test_played_minion_has_public_entry_before_battlecry_with_insertion_index():
    match = fixture(0, 1, classes=(7, 10))
    player = match.game.players[0]
    player.give('EX1_593')
    match.observer.events.clear()
    after = transition(match, 'play', lambda move: match.entity(move.get('entity', 0)).id == 'EX1_593'
                       and move['index'] == 0)
    events = after['events']
    entry = next(e for e in events if e['type'] == 'Summon')
    assert entry['index'] == 0 and entry['owner'] == 0
    assert entry['visual']['card_id'] == 'EX1_593'
    assert entry['visual']['asleep']
    assert events.index(entry) < next(i for i, e in enumerate(events) if e['type'] == 'Hit')
    assert player.field[0].entity_id == entry['target']
    assert player.opponent.hero.health == 27
