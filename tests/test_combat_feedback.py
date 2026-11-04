from fireplace import actions
from hearthstone.enums import Zone

from game.match import card_view
from tools.visual_scenarios import fixture, transition


def test_combo_glow_follows_authoritative_condition_and_resets_each_turn():
    match = fixture(0, 0, classes=(7, 10))
    player = match.game.players[0]
    card = player.give('EX1_131')
    assert not card_view(card)['combo_ready']
    player.give('GAME_005').play()
    assert card_view(card)['combo_ready']
    assert not card_view(player.give('CS2_182'))['combo_ready']
    match.game.end_turn()
    match.game.end_turn()
    assert not card_view(card)['combo_ready']
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']


def test_combo_target_is_required_only_when_si7_combo_is_active():
    match = fixture(0, 0, classes=(7, 10))
    player = match.game.players[0]
    agent = player.give('EX1_134')
    moves = [m for m in match.legal_moves(0) if m.get('entity') == agent.entity_id]
    assert moves and all(m.get('target', 0) == 0 for m in moves)
    player.give('GAME_005').play()
    moves = [m for m in match.legal_moves(0) if m.get('entity') == agent.entity_id]
    assert moves and all(m['target'] != 0 for m in moves)
    target = player.opponent.hero
    before = target.health
    transition(match, 'play', lambda m: m['entity'] == agent.entity_id and m['target'] == target.entity_id)
    assert target.health == before - 2


def test_targeted_battlecry_uses_the_same_legal_target_protocol():
    match = fixture(0, 0)
    player = match.game.players[0]
    archer = player.give('CS2_189')
    moves = [m for m in match.legal_moves(0) if m.get('entity') == archer.entity_id]
    assert moves and all(m['target'] != 0 for m in moves)
    target = player.opponent.hero
    transition(match, 'play', lambda m: m['entity'] == archer.entity_id and m['target'] == target.entity_id)
    assert target.health == 29


def test_attack_buff_flag_is_removed_after_silence():
    match = fixture(0, 0)
    player = match.game.players[0]
    minion = player.summon('CS2_182')
    assert not card_view(minion)['attack_buffed']
    player.give('CS2_073').play(target=minion)
    assert card_view(minion)['attack_buffed']
    assert minion.atk > minion.data.atk
    match.game.cheat_action(player.hero, [actions.Silence(minion)])
    assert not card_view(minion)['attack_buffed']


def test_ordinary_combat_settles_both_damage_and_deaths_in_one_server_action():
    match = fixture(0, 0)
    player = match.game.players[0]
    for card in list(player.opponent.field):
        card.zone = Zone.GRAVEYARD
    attacker = player.summon('CS2_171')
    target = player.opponent.summon('CS2_171')
    match.observer.events.clear()
    after = transition(match, 'attack', lambda m: m['entity'] == attacker.entity_id and m['target'] == target.entity_id)
    assert {e['target'] for e in after['events'] if e['type'] == 'Hit'} == {attacker.entity_id, target.entity_id}
    assert {e['target'] for e in after['events'] if e['type'] == 'Death'} == {attacker.entity_id, target.entity_id}
    assert not any(e['type'] == 'Trigger' for e in after['events'])
    assert not after['players'][0]['field'] and not after['players'][1]['field']
