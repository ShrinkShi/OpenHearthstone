"""上市死亡骑士扩展：复用事件 DSL，历史定义来自固定 HearthSim 快照。"""
from pathlib import Path
from functools import lru_cache

from hearthstone import cardxml
from hearthstone.enums import CardType, GameTag, Race, SpellSchool, Zone
from fireplace import actions, cards
from fireplace.cards.utils import buff
from fireplace.dsl import CONTROLLER, SELF, FRIENDLY_HAND, SPELL, ExactCopy, LazyNum
from fireplace.enums import PlayReq
from fireplace.player import Player

SNAPSHOT = Path(__file__).resolve().parents[1] / 'local/historical-159202.xml'
DK_IDS = []


def run(source, *effects):
    return source.game.queue_actions(source, list(effects))


def consume(player, amount, partial=False):
    available = getattr(player, 'corpses', 0)
    used = min(amount, available) if partial else amount if available >= amount else 0
    player.corpses = available - used
    player.corpses_spent = getattr(player, 'corpses_spent', 0) + used
    return used


def enhance(source, target, attack=0, health=0, **tags):
    return run(source, actions.Buff(target, 'HC_STAT_BUFF', atk=attack, max_health=health, **tags))


def random_one(source, collection):
    values = list(collection)
    return source.game.random.choice(values) if values else None


def enemies(source):
    return list(source.controller.opponent.characters)


def corpse_gain(source, amount):
    source.controller.corpses = getattr(source.controller, 'corpses', 0) + amount


def discover(source, rune):
    pool = [i for i in DK_IDS if cards.db[i].tags.get(rune, 0)]
    if pool:
        options = source.game.random.sample(pool, min(3, len(pool)))
        run(source, actions.GenericChoice(source.controller, options))


def draw_filtered(source, predicate):
    card = next((c for c in reversed(source.controller.deck) if predicate(c)), None)
    if card:
        run(source, actions.Draw(source.controller, card))
    return card


def frost_spell(card):
    return card.type == CardType.SPELL and card.data.tags.get(GameTag.SPELL_SCHOOL) == int(SpellSchool.FROST)


def hit_then(source, amount, callback):
    target = source.target
    run(source, actions.Hit(target, amount))
    if target.health <= 0 or target.to_be_destroyed:
        callback(source)


def howling_blast(source):
    target = source.target
    others = [c for c in enemies(source) if c is not target]
    run(source, actions.Hit(target, 3), actions.SetTags(target, (GameTag.FROZEN,)),
        *(actions.Hit(c, 1) for c in others))


def unholy_frenzy(source):
    minions = list(source.controller.field)
    for minion in minions:
        if minion.zone == Zone.PLAY and source.target.zone == Zone.PLAY:
            run(source, actions.Attack(minion, source.target), actions.Deaths())
    for minion in minions:
        if minion.zone == Zone.GRAVEYARD:
            run(source, actions.Summon(source.controller, minion.id))


def plague_grain(source):
    corpse_gain(source, 4)
    for _ in range(4):
        run(source, actions.Shuffle(source.controller, 'RLK_039t'))


def frostwyrm_fury(source):
    run(source, actions.Hit(source.target, 5))
    run(source, *(actions.SetTags(c, (GameTag.FROZEN,)) for c in source.controller.opponent.field),
        actions.Summon(source.controller, 'RLK_063t'))


def patchwerk(source):
    opponent = source.controller.opponent
    for collection in (opponent.hand, opponent.deck, opponent.field):
        target = random_one(source, (c for c in collection if c.type == CardType.MINION))
        if target:
            run(source, actions.Destroy(target))


def marrogaar(source):
    player = source.controller
    amount = consume(player, getattr(player, 'corpses', 0), partial=True)
    golems = []
    for _ in range(min(amount, 7-len(player.field))):
        golem = player.card('RLK_085t')
        run(source, actions.Summon(player, golem))
        golems.append(golem)
    for _ in range(amount-len(golems)):
        target = random_one(source, golems)
        if target:
            enhance(source, target, 2, 2)


def asphyxiate(source):
    minions = source.controller.opponent.field
    if minions:
        maximum = max(c.atk for c in minions)
        run(source, actions.Destroy(random_one(source, (c for c in minions if c.atk == maximum))))


def graveyard_guard(source):
    reborn = bool(consume(source.controller, 4))
    for _ in range(2):
        token = source.controller.card('RLK_118t3')
        if reborn:
            token.reborn = True
        run(source, actions.Summon(source.controller, token))


def scourge(source):
    pool = [i for i in DK_IDS if cards.db[i].type == CardType.MINION and Race.UNDEAD in cards.db[i].races]
    for _ in range(7-len(source.controller.field)):
        if pool:
            run(source, actions.Summon(source.controller, source.game.random.choice(pool)))


def obliterate(source):
    health = source.target.health
    run(source, actions.Destroy(source.target), actions.Hit(source.controller.hero, health))


def frigidara(source):
    drawn = [draw_filtered(source, lambda c: c.type == CardType.SPELL) for _ in range(2)]
    if all(c and frost_spell(c) for c in drawn):
        run(source, *(actions.Hit(c, 2) for c in enemies(source)))


def corpse_bride(source):
    amount = consume(source.controller, 8, partial=True)
    if amount:
        token = source.controller.card('RLK_506t')
        enhance(source, token, amount-1, amount-1)
        run(source, actions.Summon(source.controller, token))


def marrowgar_magician(source):
    for _ in range(consume(source.controller, 5, partial=True)):
        target = random_one(source, enemies(source))
        if target:
            run(source, actions.Hit(target, 2), actions.Deaths())


def grave_strength(source):
    amount = 3 if consume(source.controller, 5) else 1
    for c in source.controller.field:
        enhance(source, c, amount)


def blood_tap(source):
    amount = 2 if consume(source.controller, 3) else 1
    for c in source.controller.hand:
        if c.type == CardType.MINION:
            enhance(source, c, amount, amount)


def runeforging(source):
    card = draw_filtered(source, lambda c: c.type == CardType.WEAPON)
    if card and consume(source.controller, 1):
        enhance(source, card, cost=-1)


def gnome_muncher(source):
    pool = enemies(source)
    if pool:
        lowest = min(c.health for c in pool)
        run(source, actions.Attack(source, random_one(source, (c for c in pool if c.health == lowest))))


def menethil(source):
    count = consume(source.controller, 3, partial=True)
    pool = list(source.controller.opponent.field)
    for target in source.game.random.sample(pool, min(count, len(pool))):
        run(source, actions.SetTags(target, (GameTag.FROZEN,)))


def malignant_horror(source):
    if consume(source.controller, 5):
        run(source, actions.Summon(source.controller, ExactCopy(SELF)))


def vicious_bloodworm(source):
    options = [c for c in source.controller.hand if c.type == CardType.MINION]
    if options:
        run(source, actions.Choice(source.controller, options).then(
            actions.Buff(actions.Choice.CARD, 'HC_STAT_BUFF', atk=source.atk)))


def saurfang_death(source):
    card = source.controller.card(source.id)
    card.costs_health = True
    run(source, actions.Give(source.controller, card))


def frostmourne_death(source):
    for card_id in getattr(source, 'frostmourne_kills', []):
        run(source, actions.Summon(source.controller, card_id))


def rimefang_attack(source):
    card = random_one(source, (c for c in source.controller.hand if c.type == CardType.SPELL))
    if card:
        enhance(source, card, cost=-1)


def deathchiller(source):
    targets = enemies(source)
    for target in source.game.random.sample(targets, min(2, len(targets))):
        run(source, actions.Hit(target, 1))


class DKAction(actions.TargetedAction):
    def __init__(self, callback):
        super().__init__(SELF)
        self.callback_fn = callback

    def do(self, source, target):
        self.callback_fn(source)


class CorpsesSpent(LazyNum):
    def evaluate(self, source):
        return -getattr(source.controller, 'corpses_spent', 0)


class DKPlayer(Player):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.corpses = 0
        self.corpses_spent = 0

    def can_pay_cost(self, card):
        if getattr(card, 'costs_health', False):
            return self.hero.health > card.cost
        return super().can_pay_cost(card)

    def pay_cost(self, source, amount):
        if getattr(source, 'costs_health', False):
            self.hero.damage += amount
            return amount
        return super().pay_cost(source, amount)


def record_death(card):
    if card.type == CardType.MINION and int(card.controller.hero.card_class) == 1:
        # 不产生尸体的复活 token 在历史文字中明确注明。
        if '不会留下' not in card.data.description:
            card.controller.corpses += 1


def record_damage(source, target):
    if source.type == CardType.HERO and target.type == CardType.MINION and target.health <= 0:
        weapon = source.controller.weapon
        if weapon and weapon.id == 'RLK_086':
            if not hasattr(weapon, 'frostmourne_kills'):
                weapon.frostmourne_kills = []
            weapon.frostmourne_kills.append(target.id)


def _heal_guard(original):
    def heal(action, source, target, amount):
        blocked = any(c.id == 'RLK_115' and not c.silenced and not c.dead for c in target.controller.opponent.field)
        if blocked and not source.controller.healing_as_damage:
            return 0
        return original(action, source, target, amount)
    return heal


@lru_cache(maxsize=1)
def register():
    if not SNAPSHOT.exists():
        return False
    db, _ = cardxml.load(path=str(SNAPSHOT), locale='zhCN')
    DK_IDS[:] = sorted(c.id for c in db.values() if int(c.card_class) == 1 and c.collectible and int(c.card_set) in (1637, 1869))
    if len(DK_IDS) != 58:
        raise ValueError('historical death knight snapshot must contain exactly 58 cards')
    play = {
        'RLK_015': howling_blast,
        'RLK_018': lambda s: hit_then(s, 3, lambda x: run(x, actions.Summon(x.controller, 'RLK_018t'))),
        'RLK_024': lambda s: run(s, actions.Hit(s.target, 6)),
        'RLK_025': lambda s: hit_then(s, 3, lambda x: discover(x, GameTag.COST_FROST)),
        'RLK_034': lambda s: hit_then(s, 3, lambda x: corpse_gain(x, 1)),
        'RLK_038': lambda s: run(s, actions.Hit(s.target, 2), actions.SetTags(s.target, (GameTag.FROZEN,))),
        'RLK_039': plague_grain,
        'RLK_042': lambda s: run(s, actions.SpendMana(s.controller, -2)),
        'RLK_048': lambda s: [enhance(s, c, 1, 1, cant_be_targeted_by_abilities=True, cant_be_targeted_by_hero_powers=True) for c in s.controller.field],
        'RLK_056': unholy_frenzy,
        'RLK_057': lambda s: run(s, actions.Morph(s.target, 'RLK_057t')),
        'RLK_060': lambda s: run(s, actions.Summon(s.controller, 'RLK_008t')*consume(s.controller, 5, partial=True)),
        'RLK_062': lambda s: run(s, actions.Summon(s.controller, ExactCopy(SELF))*2),
        'RLK_063': frostwyrm_fury,
        'RLK_066': lambda s: discover(s, GameTag.COST_BLOOD) if consume(s.controller, 1) else None,
        'RLK_071': patchwerk,
        'RLK_077': lambda s: run(s, actions.Summon(s.controller, 'RLK_077t')*2),
        'RLK_079': lambda s: run(s, actions.Hit(s.target, 2), actions.Hit(s.controller.hero, 2)) if s.target else run(s, actions.Hit(s.controller.hero, 2)),
        'RLK_085': marrogaar,
        'RLK_087': asphyxiate,
        'RLK_101': lambda s: run(s, actions.Draw(s.controller)*(2 if consume(s.controller, 2) else 1)),
        'RLK_110': lambda s: enhance(s, s, sum(frost_spell(c) for c in s.controller.hand)),
        'RLK_118': graveyard_guard,
        'RLK_122': scourge,
        'RLK_125': obliterate,
        'RLK_223': lambda s: run(s, actions.Hit(random_one(s, enemies(s)), 2)),
        'RLK_224': frigidara,
        'RLK_503': lambda s: corpse_gain(s, 1),
        'RLK_504': corpse_bride,
        'RLK_505': marrowgar_magician,
        'RLK_512': lambda s: run(s, actions.Hit(s.target, 4), actions.Buff(s.controller, 'HC_NEXT_SPELL')),
        'RLK_705': lambda s: run(s, actions.Summon(s.controller, 'RLK_705t')*2),
        'RLK_707': grave_strength,
        'RLK_708': lambda s: run(s, actions.Draw(s.controller)),
        'RLK_709': lambda s: run(s, *(actions.Hit(c, 2) for c in enemies(s)), actions.Draw(s.controller)),
        'RLK_711': vicious_bloodworm,
        'RLK_712': blood_tap,
        'RLK_715': runeforging,
        'RLK_730': lambda s: run(s, *(actions.Buff(c, 'RLK_730e') for c in s.controller.opponent.field)),
        'RLK_731': lambda s: [enhance(s, c, 2) for c in s.controller.hand if c.type == CardType.MINION] if consume(s.controller, 2) else None,
        'RLK_740': menethil,
        'RLK_752': lambda s: run(s, actions.Summon(s.controller, 'RLK_907t')*2),
        'RLK_753': lambda s: enhance(s, s, 1, 2) if consume(s.controller, 1) else None,
        'RLK_958': lambda s: enhance(s, s.target, 2) if s.target else None,
        'HERO_11bp': lambda s: run(s, actions.Summon(s.controller, 'HERO_11bpt')),
        'HERO_11bp2': lambda s: run(s, actions.Summon(s.controller, 'HERO_11bp2t')),
    }
    deathrattle = {
        'RLK_082': saurfang_death, 'RLK_086': frostmourne_death,
        'RLK_223': play['RLK_223'], 'RLK_226': lambda s: run(s, actions.Summon(s.controller, 'RLK_226t')) if consume(s.controller, 3) else None,
        'RLK_511': lambda s: draw_filtered(s, frost_spell), 'RLK_708': play['RLK_708'],
        'RLK_713': lambda s: run(s, *(actions.Give(s.controller, c.id) for c in list(s.controller.hand) if frost_spell(c))),
        'RLK_907t': play['RLK_223'],
    }
    events = {
        'RLK_061': [actions.EndTurn(CONTROLLER).on(DKAction(lambda s: run(s, actions.Summon(s.controller, 'RLK_061t')) if consume(s.controller, 1) else None))],
        'RLK_083': [actions.Play(CONTROLLER, SPELL).after(DKAction(deathchiller))],
        'RLK_720': [actions.EndTurn(CONTROLLER).on(DKAction(gnome_muncher))],
        'RLK_745': [actions.EndTurn(CONTROLLER).on(DKAction(malignant_horror))],
        'HERO_11bpt': [actions.EndTurn().on(actions.Destroy(SELF))],
        'HERO_11bp2t': [actions.EndTurn().on(actions.Destroy(SELF))],
        'RLK_730e': [actions.EndTurn(CONTROLLER).on(DKAction(lambda s: run(s.source, actions.Hit(s.owner, 2))))],
    }
    from fireplace.dsl import FRIENDLY_HERO, ENEMY_MINIONS
    events['RLK_516'] = [actions.Attack(FRIENDLY_HERO, ENEMY_MINIONS).after(DKAction(lambda s: run(s, actions.Hit(s.controller.opponent.hero, 2))))]
    events['RLK_710'] = [actions.Attack(FRIENDLY_HERO).after(DKAction(rimefang_attack))]
    targets = {
        'RLK_015': ('enemy',), 'RLK_018': ('minion',), 'RLK_024': ('minion',), 'RLK_025': ('minion',),
        'RLK_034': ('minion',), 'RLK_038': ('enemy',), 'RLK_056': ('enemy', 'minion'),
        'RLK_057': ('minion', 'undead'), 'RLK_063': (), 'RLK_079': ('enemy', 'optional'),
        'RLK_125': ('minion',), 'RLK_512': (), 'RLK_958': ('friendly', 'minion', 'undead', 'optional'),
    }
    for c in db.values():
        if c.id in DK_IDS or ((c.id.startswith('RLK_') and not c.collectible and not c.id.startswith('RLK_Prologue')) or c.id.startswith('HERO_11')):
            attributes = {}
            if c.id in play:
                attributes['activate' if c.type == CardType.HERO_POWER else 'play'] = (DKAction(play[c.id]),)
            if c.id in deathrattle:
                attributes['deathrattle'] = (DKAction(deathrattle[c.id]),)
            if c.id in events:
                attributes['events'] = events[c.id]
            if c.id == 'RLK_039t':
                attributes['draw'] = (actions.Destroy(SELF), actions.Summon(CONTROLLER, 'RLK_070t'), actions.Draw(CONTROLLER))
            if c.id == 'RLK_744':
                attributes['cost_mod'] = CorpsesSpent()
            if c.id == 'RLK_730':
                # 历史 XML 在主法术上省略该标签，持续伤害仍具有吸血。
                attributes['tags'] = {GameTag.LIFESTEAL: 1}
            if c.id in targets:
                flags = targets[c.id]
                req = {PlayReq.REQ_TARGET_IF_AVAILABLE if 'optional' in flags else PlayReq.REQ_TARGET_TO_PLAY: 0}
                for flag, requirement in [('minion', PlayReq.REQ_MINION_TARGET), ('enemy', PlayReq.REQ_ENEMY_TARGET), ('friendly', PlayReq.REQ_FRIENDLY_TARGET)]:
                    if flag in flags:
                        req[requirement] = 0
                if 'undead' in flags:
                    req[PlayReq.REQ_TARGET_WITH_RACE] = int(Race.UNDEAD)
                attributes['requirements'] = req
            if c.id in ('HERO_11bp', 'HERO_11bp2'):
                attributes['requirements'] = {PlayReq.REQ_NUM_MINION_SLOTS: 1}
            cards.db[c.id] = cards.db.merge(c.id, c, type(c.id, (), attributes))
            cards.db.dbf[c.dbf_id] = c.id
    for name, definition in [('HC_STAT_BUFF', buff()), ('HC_NEXT_SPELL', type('NextSpell', (), {}))]:
        data = cardxml.CardXML(name, locale='zhCN')
        data.tags[GameTag.CARDTYPE] = int(CardType.ENCHANTMENT)
        if name == 'HC_NEXT_SPELL':
            from fireplace.aura import Refresh
            definition.update = (Refresh(FRIENDLY_HAND + SPELL, tags={GameTag.COST: -2}),)
            definition.events = [actions.Play(CONTROLLER, SPELL).on(actions.Destroy(SELF)), actions.EndTurn(CONTROLLER).on(actions.Destroy(SELF))]
        cards.db[name] = cards.db.merge(name, data, definition)
    actions.Heal.do = _heal_guard(actions.Heal.do)
    return True
