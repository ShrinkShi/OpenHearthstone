"""表现字段必须来自权威状态，扳机消息不能改变结算或泄露手牌。"""
from pathlib import Path

from fireplace import actions
from game.database import preset
from game.match import Match


def match():
    m = Match(['Shrink1', 'Shrink2'], [10, 4], [preset(10), preset(4)], 123)
    for p in m.game.players:
        p.choice.choose()
    m.observer.events.clear()
    return m, m.game.current_player


def view(m, entity):
    return next(c for p in m.snapshot(0)['players'] for c in [p['hero'], p['weapon'], *p['field']]
                if c and c['entity'] == entity.entity_id)


def test_health_maximum_survives_damage_and_heal():
    m, p = match()
    minion = p.summon('CS2_182')
    maximum = minion.max_health
    m.game.cheat_action(p.hero, [actions.Hit(minion, 1)])
    assert view(m, minion)['health'] == maximum-1
    assert view(m, minion)['max_health'] == maximum
    assert not view(m, minion)['health_buffed']
    m.game.cheat_action(p.hero, [actions.Heal(minion, 1)])
    assert view(m, minion)['health'] == maximum


def test_health_buff_metadata_updates_after_silence():
    m, p = match()
    minion = p.summon('CS2_182')
    p.max_mana = 10
    p.give('CS2_009').play(target=minion)
    assert view(m, minion)['health_buffed']
    assert view(m, minion)['health'] == view(m, minion)['max_health']
    m.game.cheat_action(p.hero, [actions.Silence(minion)])
    assert not view(m, minion)['health_buffed']


def test_weapon_current_and_maximum_durability_are_separate():
    m, p = match()
    weapon = p.summon('CS2_106')
    assert view(m, weapon)['durability'] == view(m, weapon)['max_durability'] == 2
    weapon.damage = 1
    assert view(m, weapon)['durability'] == 1
    assert view(m, weapon)['max_durability'] == 2
    weapon.max_durability += 1
    assert view(m, weapon)['durability_buffed']


def test_armorsmith_marker_and_actual_trigger():
    m, p = match()
    smith = p.summon('EX1_402')
    other = p.summon('CS2_182')
    assert 'trigger' in view(m, smith)['keywords']
    assert 'trigger' not in view(m, other)['keywords']
    m.observer.events.clear()
    armor = p.hero.armor
    m.game.cheat_action(p.hero, [actions.Hit(other, 1)])
    triggers = [e for e in m.observer.events if e['type'] == 'Trigger']
    assert [e['source'] for e in triggers] == [smith.entity_id]
    assert p.hero.armor == armor+1
    m.observer.events.clear()
    m.game.cheat_action(p.hero, [actions.Hit(p.opponent.hero, 1)])
    assert not any(e['type'] == 'Trigger' for e in m.observer.events)


def test_silenced_listener_has_no_marker_or_flash():
    m, p = match()
    smith = p.summon('EX1_402')
    m.game.cheat_action(p.hero, [actions.Silence(smith)])
    assert 'trigger' not in view(m, smith)['keywords']
    assert 'silenced' in view(m, smith)['keywords']
    m.observer.events.clear()
    m.game.cheat_action(p.hero, [actions.Hit(smith, 1)])
    assert not any(e['type'] == 'Trigger' for e in m.observer.events)


def test_presentation_metadata_is_public_without_private_hand():
    m, p = match()
    p.summon('EX1_402')
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']
    assert m.snapshot(0)['players'][1]['hand'] == []


def test_priest_authority_target_routes_for_observer_without_local_moves():
    root = Path(__file__).resolve().parents[1]
    presentation = (root / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    route = presentation.split('static bool TryGetPriestTarget(', 1)[1].split('bool IsLifeTapSelfDamage(', 1)[0]
    checks = (root / 'UnityClient/Assets/Editor/PresentationChecks.cs').read_text(encoding='utf-8')

    assert 'route.source!=power.target' in route
    assert 'route.owner!=ownerSeat' in route
    assert 'route.card_id!="HERO_09bp"' in route
    assert 'IsCharacterTarget(before,route.target)' in route
    assert 'BattleInteraction.IsLegalTarget' not in route
    assert 'observerPriestBefore.seat=1;observerPriestBefore.moves=new Move[0]' in checks
    assert 'new[]{1,3}' in checks
    assert 'authority target attributed to the wrong owner' in checks


def test_summoning_sleep_ends_when_minion_can_act_next_turn():
    m, p = match()
    minion = p.summon('CS2_182')
    assert view(m, minion)['asleep']
    m.game.end_turn()
    m.game.end_turn()
    assert not view(m, minion)['asleep']
    minion.attack(p.opponent.hero)
    assert minion.exhausted
    assert not view(m, minion)['asleep']


def test_charge_and_rush_do_not_show_summoning_sleep():
    m, p = match()
    charge = p.summon('CS2_171')
    ordinary = p.summon('CS2_182')
    assert charge.charge and not view(m, charge)['asleep']
    ordinary.rush = True
    assert not view(m, ordinary)['asleep']
    m.game.cheat_action(p.hero, [actions.Silence(ordinary)])
    assert view(m, ordinary)['asleep']
    assert m.snapshot(0)['public_hash'] == m.snapshot(1)['public_hash']
