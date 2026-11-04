from game.database import preset
from game.match import Match
from tools.fireblast_evidence import ROGUE_WEAPONS_SHAMAN_SEED, prepare_rogue_weapons_shaman


def apply(match, seat, move, action_id):
    return match.apply(seat, dict(move, action_id=action_id,
                                  version=match.version, match_id=match.id))


def move(match, seat, kind, *, entity=None, target=None):
    return next(item for item in match.legal_moves(seat)
                if item['kind'] == kind and
                (entity is None or item.get('entity') == entity) and
                (target is None or item.get('target') == target))


def test_rogue_attacks_with_first_dagger_then_reequips_next_turn_and_concedes_cleanly():
    match = Match(['RogueA', 'MageB'], [7, 4], [preset(7), preset(4)], 52)
    rogue, opponent = match.game.players
    for seat in range(2):
        assert apply(match, seat, {'kind': 'mulligan', 'cards': []}, f'mulligan-{seat}')

    # Reach Rogue's first affordable power turn without relying on coin or
    # opening order. The hero power is not available with only one mana.
    turn = 0
    while match.game.current_player is not rogue or not any(
            item['kind'] == 'power' and item['entity'] == rogue.hero.power.entity_id
            for item in match.legal_moves(0)):
        active = match.game.players.index(match.game.current_player)
        assert apply(match, active, move(match, active, 'end_turn'), f'opening-end-{turn}')
        turn += 1

    first_power = rogue.hero.power.entity_id
    initial_weapon = rogue.weapon
    assert initial_weapon is None
    assert any(item['kind'] == 'power' and item['entity'] == first_power
               for item in match.legal_moves(0))
    assert apply(match, 0, move(match, 0, 'power', entity=first_power), 'rogue-power-first')
    first_weapon = rogue.weapon
    assert first_weapon.id == 'CS2_082' and first_weapon.durability == 2
    assert not any(item['kind'] == 'power' for item in match.legal_moves(0))

    # Use the first dagger once, then finish the turn. A second power is
    # available only after the turn refreshes.
    first_attack = move(match, 0, 'attack', entity=rogue.hero.entity_id,
                        target=opponent.hero.entity_id)
    assert apply(match, 0, first_attack, 'rogue-attack-first-dagger')
    assert rogue.weapon is first_weapon and rogue.weapon.durability == 1
    first_attack_events = list(match.observer.events)
    first_wear = [event for event in first_attack_events
                  if event['type'] == 'Hit' and event['source'] == event['target'] == first_weapon.entity_id]
    assert len(first_wear) == 1 and first_wear[0]['visual']['durability'] == 1
    assert not any(item['kind'] == 'power' for item in match.legal_moves(0))
    assert apply(match, 0, move(match, 0, 'end_turn'), 'rogue-end-first-turn')

    opponent_turn = 0
    while match.game.current_player is not rogue:
        active = match.game.players.index(match.game.current_player)
        assert apply(match, active, move(match, active, 'end_turn'), f'opponent-end-{opponent_turn}')
        opponent_turn += 1

    # The refreshed hero attack spends the dagger's last durability. The
    # legal power on this same turn equips a new dagger once the old one is gone.
    assert rogue.weapon is first_weapon and rogue.weapon.durability == 1
    assert any(item['kind'] == 'attack' and item['entity'] == rogue.hero.entity_id
               for item in match.legal_moves(0))
    second_attack = move(match, 0, 'attack', entity=rogue.hero.entity_id,
                         target=opponent.hero.entity_id)
    assert apply(match, 0, second_attack, 'rogue-attack-first-dagger-again')
    second_attack_events = list(match.observer.events)
    assert rogue.weapon is None
    assert len([event for event in second_attack_events
                if event['type'] == 'Death' and event['target'] == first_weapon.entity_id]) == 1
    assert any(item['kind'] == 'power' and item['entity'] == first_power
               for item in match.legal_moves(0))

    mana_before_replacement = rogue.mana
    before_hashes = [match.snapshot(seat)['public_hash'] for seat in range(2)]
    assert before_hashes[0] == before_hashes[1]
    assert apply(match, 0, move(match, 0, 'power', entity=first_power), 'rogue-power-second')
    replacement = rogue.weapon
    assert replacement.id == 'CS2_082' and replacement.entity_id != first_weapon.entity_id
    assert replacement.atk == 1 and replacement.durability == 2
    assert rogue.hero.atk == 1 and rogue.mana == mana_before_replacement - 2
    assert not any(item['kind'] == 'power' for item in match.legal_moves(0))
    replacement_events = list(match.observer.events)
    assert len([event for event in replacement_events if event['type'] == 'PlayHeroPower']) == 1
    summons = [event for event in replacement_events
               if event['type'] == 'Summon' and event.get('card_type') == 7]
    assert len(summons) == 1 and summons[0]['target'] == replacement.entity_id

    own, peer = match.snapshot(0), match.snapshot(1)
    assert own['public_hash'] == peer['public_hash']
    assert any(item['kind'] == 'end_turn' for item in own['moves'])
    assert apply(match, 0, move(match, 0, 'end_turn'), 'rogue-end-after-replacement')
    assert match.game.current_player is opponent

    # Concession remains valid while the opponent owns the active turn; both
    # peers receive the same finished authority state.
    assert apply(match, 0, {'kind': 'concede'}, 'rogue-concede')
    final_own, final_peer = match.snapshot(0), match.snapshot(1)
    assert final_own['phase'] == final_peer['phase'] == 'finished'
    assert final_own['public_hash'] == final_peer['public_hash']
    assert final_own['players'][0]['result'] == 'LOST'
    assert final_own['players'][1]['result'] == 'WON'
    assert [event['type'] for event in final_own['events']] == ['Concede']


def test_saved_rogue_shaman_totem_sequence_replays_to_synchronized_concede():
    match = Match(['RogueA', 'ShamanB'], [7, 8], [preset(7), preset(8)], ROGUE_WEAPONS_SHAMAN_SEED)
    prepare_rogue_weapons_shaman(match, finish=True)

    own, peer = match.snapshot(0), match.snapshot(1)
    assert own['version'] == 19 and own['turn'] == 7
    assert own['players'][0]['hero']['card_id'] == 'HERO_03'
    assert own['players'][0]['power']['card_id'] == 'HERO_03bp'
    assert own['players'][1]['hero']['card_id'] == 'HERO_02'
    assert own['players'][1]['power']['card_id'] == 'HERO_02bp'
    assert own['public_hash'] == peer['public_hash']
    assert [player['result'] for player in own['players']] == ['LOST', 'WON']


def test_rogue_shaman_gui_fixture_starts_at_saved_v6_before_player_actions():
    match = Match(['RogueA', 'ShamanB'], [7, 8], [preset(7), preset(8)], ROGUE_WEAPONS_SHAMAN_SEED)
    prepare_rogue_weapons_shaman(match)

    state = match.snapshot(0)
    assert match.version == state['version'] == 6
    assert [player.hero.power.id for player in match.game.players] == ['HERO_03bp', 'HERO_02bp']
    assert match.game.players[0].hero.entity_id == 4
    assert match.game.players[0].hero.power.entity_id == 5
    assert match.game.players[1].hero.power.entity_id == 38
    assert {entity.entity_id for entity in match.game.players[1].field} == {71}
    assert match.game.players[1].field[-1].id == 'CS2_050'
    assert any(item['kind'] == 'power' and item['entity'] == 5 and item['target'] == 0
               for item in match.legal_moves(0))
