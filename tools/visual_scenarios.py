"""仅供视觉测试的固定状态；后续动作通过现有 Match.apply 执行。"""
import argparse
import json
from pathlib import Path

from hearthstone.enums import Zone
from game.database import preset
from game.match import Match
from .importer import PROJECT

HAND = ['CS2_182', 'CS2_029', 'CS2_024', 'CS2_023', 'EX1_020', 'CS2_172', 'CS2_027', 'CS2_179', 'CS2_120', 'CS2_171']


def fixture(hand=4, minions=1, classes=(4, 10)):
    match = Match(['Shrink1', 'Shrink2'], list(classes), [preset(c) for c in classes], 42)
    match.id = 'visual-fixed-match'
    for player in match.game.players:
        player.choice.choose()
    if match.game.current_player != match.game.players[0]:
        match.game.end_turn()
    p = match.game.players[0]
    for card in list(p.hand):
        card.zone = Zone.GRAVEYARD
    for card_id in HAND[:hand]:
        p.give(card_id)
    for _ in range(minions):
        card = p.summon('CS2_172')
        card.turns_in_play = 1
    p.opponent.summon('CS2_172')
    p.max_mana = 10
    p.used_mana = 0
    match.observer.events.clear()
    return match


def transition(match, kind, predicate=lambda m: True):
    move = next(m for m in match.legal_moves(0) if m['kind'] == kind and predicate(m))
    match.apply(0, dict(move, version=match.version, match_id=match.id, action_id='visual-' + kind))
    return match.snapshot(0)


def generate(folder):
    folder.mkdir(parents=True, exist_ok=True)
    def save(name, state):
        (folder / (name + '.json')).write_text(json.dumps(state, ensure_ascii=False), encoding='utf-8')
    opening = Match(['Shrink1', 'Shrink2'], [4, 10], [preset(4), preset(10)], 42)
    opening.id = 'visual-opening'
    save('mulligan', opening.snapshot(0)); save('match_start', opening.snapshot(0))
    save('mulligan_hover', opening.snapshot(0))
    save('battle_entrance', opening.snapshot(0))
    save('power_hover', fixture(classes=(10, 4)).snapshot(0))
    for name in ('game_menu','game_options','deck_hover_own','deck_hover_enemy','hand_hover_enemy'):
        save(name,fixture().snapshot(0))
    sleeping=fixture()
    sleeping.game.players[0].summon('EX1_402')
    save('minion_sleep',sleeping.snapshot(0))
    icons = fixture(classes=(10, 4))
    icons.game.players[0].hero.armor = 8
    icons.game.players[0].give('EX1_567')
    transition(icons, 'play', lambda m: icons.entity(m.get('entity', 0)).id == 'EX1_567')
    save('stat_icons', icons.snapshot(0))
    for side in ('left', 'right'):
        save('minion_hover_' + side, fixture(minions=2).snapshot(0))
    exhausted = fixture(0, 0)
    exhausted.game.players[0].used_mana = exhausted.game.players[0].max_mana
    save('end_turn_exhausted', exhausted.snapshot(0))
    power_used = fixture(0, 0, classes=(10, 4))
    save('power_disabled', transition(power_used, 'power'))
    rogue=fixture(0,0,classes=(7,10))
    rogue.game.players[0].give('EX1_129')
    rogue.game.players[1].summon('EX1_402')
    rogue.game.players[1].summon('CS2_171')
    rogue.observer.events.clear()
    fan_before=rogue.snapshot(0)
    fan_after=transition(rogue,'play',lambda m: rogue.entity(m.get('entity',0)).id=='EX1_129')
    for name in ('fan_of_knives','fan_triggers'):
        save(name,fan_before);save(name+'-after',fan_after)
    weapon=fixture(0,0,classes=(7,4))
    save('weapon_summon',weapon.snapshot(0))
    save('weapon_equipped',transition(weapon,'power'))
    save('weapon_summon-after',weapon.snapshot(0))
    save('power_used',weapon.snapshot(0))
    save('power_used_hover',weapon.snapshot(0))
    taunt=fixture(0,0)
    taunt.game.players[0].summon('CS2_179')
    save('taunt',taunt.snapshot(0))
    combo=fixture(0,0,classes=(7,10))
    for card_id in ('EX1_131','EX1_134','EX1_124'):
        combo.game.players[0].give(card_id)
    save('combo_inactive',combo.snapshot(0))
    combo.game.players[0].give('GAME_005').play()
    combo.observer.events.clear()
    save('combo_ready',combo.snapshot(0))
    for name,card_id in (('backstab','CS2_072'),('eviscerate','EX1_124'),('cold_blood','CS2_073')):
        effect=fixture(0,1,classes=(7,10))
        effect.game.players[0].give(card_id)
        target=effect.game.players[0 if name=='cold_blood' else 1].field[0]
        before=effect.snapshot(0)
        after=transition(effect,'play',lambda m: effect.entity(m.get('entity',0)).id==card_id and m['target']==target.entity_id)
        save(name,before);save(name+'-after',after)
    for name,card_id in [('combo_target_select','EX1_134'),('weapon_target_select','EX1_133')]:
        sample=fixture(0,1,classes=(7,10));player=sample.game.players[0]
        player.give('GAME_005').play();player.give(card_id);sample.observer.events.clear()
        save(name,sample.snapshot(0))
    hero_target=fixture(0,0,classes=(7,10));hero_target.game.players[0].give('CS2_082').play()
    hero_target.observer.events.clear();save('hero_attack_select',hero_target.snapshot(0));save('attack_arrow',hero_target.snapshot(0))
    dying=fixture(1,0);dying.game.players[1].hero.damage=29;dying.game.players[0].give('CS2_029')
    dying.observer.events.clear();save('hero_death',dying.snapshot(0))
    after=transition(dying,'play',lambda m:dying.entity(m.get('entity',0)).id=='CS2_029' and m.get('target')==dying.game.players[1].hero.entity_id)
    save('hero_death-after',after)
    combat=fixture(0,0)
    for card in list(combat.game.players[1].field):
        card.zone=Zone.GRAVEYARD
    attacker=combat.game.players[0].summon('CS2_171')
    target=combat.game.players[1].summon('CS2_171')
    combat.observer.events.clear();before=combat.snapshot(0)
    after=transition(combat,'attack',lambda m:m['entity']==attacker.entity_id and m['target']==target.entity_id)
    save('ordinary_combat',before);save('ordinary_combat-after',after)
    reacting=fixture(0,1)
    for card in list(reacting.game.players[1].field):
        card.zone=Zone.GRAVEYARD
    target=reacting.game.players[1].summon('EX1_402')
    attacker=reacting.game.players[0].field[0]
    reacting.observer.events.clear();before=reacting.snapshot(0)
    after=transition(reacting,'attack',lambda m:m['entity']==attacker.entity_id and m['target']==target.entity_id)
    save('combat_trigger',before);save('combat_trigger-after',after)
    for name, reacts in [('hero_combat',False),('hero_combat_trigger',True)]:
        match=fixture(0,0,classes=(7,10));player=match.game.players[0]
        player.give('CS2_082').play()
        target=player.opponent.summon('EX1_402') if reacts else player.opponent.field[0]
        match.observer.events.clear();before=match.snapshot(0)
        after=transition(match,'attack',lambda m:m['entity']==player.hero.entity_id and m['target']==target.entity_id)
        save(name,before);save(name+'-after',after)
    badges=fixture(0,0);player=badges.game.players[0]
    player.summon('EX1_096');player.summon('EX1_170');player.summon('EX1_402')
    save('status_badges',badges.snapshot(0))
    for name, card_id, class_id in [('arcane_explosion','CS2_025',4),('frost_nova','CS2_026',4),('hellfire','CS2_062',9),('blizzard','CS2_028',4),
            ('flamestrike','CS2_032',4),('polymorph','CS2_022',4),('holy_nova','CS1_112',6),
            ('nightblade','EX1_593',7),('si7_agent','EX1_134',7),('big_game_hunter','EX1_005',4)]:
        sample=fixture(0,0,classes=(class_id,10));player=sample.game.players[0]
        if name=='si7_agent':
            player.give('GAME_005').play()
        if name=='big_game_hunter':
            player.opponent.summon('CS2_201')
        player.give(card_id);sample.observer.events.clear();before=sample.snapshot(0)
        after=transition(sample,'play',lambda m:sample.entity(m.get('entity',0)).id==card_id
                         and (not m.get('target') or sample.entity(m['target']).controller==player.opponent))
        save(name,before);save(name+'-after',after)
    log_match = fixture()
    log_before = log_match.snapshot(0)
    log_events = []
    for kind, predicate in [('play', lambda m: log_match.entity(m.get('entity', 0)).id == 'CS2_182'),
                            ('power', lambda m: m.get('target') == log_match.game.players[1].hero.entity_id),
                            ('attack', lambda m: m.get('target') == log_match.game.players[1].field[0].entity_id)]:
        logged = transition(log_match, kind, predicate)
        log_events.extend(logged['events'])
    logged['events'] = log_events
    for name in ('history', 'history_hover'):
        save(name, log_before); save(name + '-after', logged)
    for count in range(1, 11):
        match = fixture(count)
        save('hand' + str(count), match.snapshot(0))
    for count in range(1, 8):
        save('minions' + str(count), fixture(4, count).snapshot(0))
    for name in ['board', 'card_hover', 'card_drag', 'minion_on_board', 'multiple_minions']:
        save(name, fixture(4, 4 if name == 'multiple_minions' else 1).snapshot(0))
    for name, kind, card_id in [('minion_summon', 'play', 'CS2_182'), ('spell_cast', 'play', 'CS2_029'),
                                 ('hero_power', 'power', None), ('minion_attack', 'attack', None),
                                 ('death', 'attack', None), ('damage', 'power', None), ('end_turn', 'end_turn', None)]:
        match = fixture()
        before = match.snapshot(0)
        target = match.game.players[1].field[0].entity_id
        def choose(move):
            entity = match.entity(move.get('entity', 0))
            return (not card_id or entity.id == card_id) and (kind == 'end_turn' or move.get('target', 0) in (0, target))
        after = transition(match, kind, choose)
        save(name, before); save(name + '-after', after)
    match = fixture()
    before = match.snapshot(0); after = transition(match, 'end_turn')
    match.observer.events.clear();match.game.end_turn(); match.version += 1
    save('card_draw', after); save('card_draw-after', match.snapshot(0))
    save('turn_start', after); save('turn_start-after', match.snapshot(0))
    for name, seat in [('victory', 1), ('defeat', 0)]:
        match = fixture()
        match.apply(seat, dict(kind='concede', version=match.version, match_id=match.id, action_id='result'))
        save(name, match.snapshot(0))
    save('ondemand', fixture().snapshot(0))
    print(folder)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', default=str(PROJECT / 'local/visual/states'))
    generate(Path(parser.parse_args().output))


if __name__ == '__main__':
    main()
