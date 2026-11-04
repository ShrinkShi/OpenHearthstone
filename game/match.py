from __future__ import annotations

import copy
import hashlib
import json
import secrets
import uuid

from fireplace.actions import MulliganChoice
from fireplace.exceptions import GameOver
from fireplace.game import Game
from fireplace.managers import BaseObserver
from .death_knight import DKPlayer, record_death, record_damage
from hearthstone.enums import CardType, Zone

from .database import HEROES, initialize, validate_deck


def card_view(c):
    maximum_health = getattr(c, 'max_health', 0)
    maximum_durability = getattr(c, 'max_durability', 0)
    keywords = [k for k in ('taunt', 'divine_shield', 'frozen', 'stealthed', 'windfury', 'charge', 'rush', 'silenced') if getattr(c, k, False)]
    if c.type == CardType.MINION and not c.ignore_scripts and c.data.scripts.events:
        keywords.append('trigger')
    if c.type == CardType.MINION:
        if c.deathrattles:
            keywords.append('deathrattle')
        if c.poisonous:
            keywords.append('poisonous')
    view = {'entity': c.entity_id, 'card_id': c.id, 'cost': c.cost, 'attack': getattr(c, 'atk', 0),
            'health': getattr(c, 'health', 0), 'armor': getattr(c, 'armor', 0),
            'durability': getattr(c, 'durability', 0), 'max_health': maximum_health,
            'max_durability': maximum_durability,
            'health_buffed': maximum_health > getattr(c.data, 'health', 0),
            'durability_buffed': maximum_durability > getattr(c.data, 'durability', 0),
            'attack_buffed': getattr(c, 'atk', 0) > getattr(c.data, 'atk', 0),
            'combo_ready': bool(c.has_combo and c.controller.combo),
            'asleep': c.type == CardType.MINION and c.asleep,
            'used': c.type == CardType.HERO_POWER and c.activations_this_turn > 0 and c.exhausted,
            'keywords': keywords}
    if c.id == 'HERO_05bp':
        view['display_damage'] = c.controller.get_heropower_damage(2)
        view['display_damage_known'] = True
    elif c.id == 'HERO_08bp':
        view['display_damage'] = c.controller.get_heropower_damage(1)
        view['display_damage_known'] = True
    elif c.id == 'HERO_07bp':
        view['display_damage'] = c.controller.get_heropower_damage(2)
        view['display_damage_known'] = True
    elif c.id == 'HERO_09bp':
        view['display_heal'] = c.controller.get_heropower_heal(2)
        view['display_heal_known'] = True
    return view


class EventObserver(BaseObserver):
    def __init__(self):
        self.events = []

    def targeted_action(self, action, source, target, *args):
        kind = type(action).__name__
        if kind == 'Hit':
            return
        if kind == 'Damage':
            record_damage(source, target)
        if kind in ('Summon', 'Draw', 'Give') and args:
            card = args[0]
            if isinstance(card, (list, tuple)):
                card = card[0] if card else target
            target = card
        if kind == 'Damage':
            kind = 'Hit'
        controller = getattr(target, 'controller', None)
        owner = source.game.players.index(controller) if controller in source.game.players else -1
        event = {'type': kind, 'source': getattr(source, 'entity_id', 0),
                 'target': getattr(target, 'entity_id', 0), 'owner': owner,
                 'amount': next((a for a in args if isinstance(a, (int, float))), 0)}
        # 只传已经在场上的公开实体；抽牌事件不得夹带对方手牌名称。
        if (kind in ('Hit', 'Heal', 'GainArmor', 'Buff', 'Summon', 'Morph', 'EquipWeapon', 'Silence')
                and getattr(target, 'zone', None) == Zone.PLAY
                and target.type in (CardType.HERO, CardType.MINION, CardType.WEAPON, CardType.HERO_POWER)):
            event['visual'] = card_view(target)
            event['card_type'] = int(target.type)
        self.events.append(event)

    def game_action(self, action, source, *args):
        kind = type(action).__name__
        if kind == 'Attack':
            actor, target = args[:2]
            self.events.append({'type': 'Attack', 'source': actor.entity_id, 'target': target.entity_id, 'amount': 0})
        elif kind == 'Play':
            card, target = args[:2]
            self.events.append({'type': 'CardPlay', 'source': card.entity_id, 'target': getattr(target, 'entity_id', 0), 'amount': 0, 'card_id': card.id, 'card_type': int(card.type)})
            # Fireplace 的 Play 只广播 Summon，不调用 targeted_action。
            # 此时实体已公开入场，补表现事件以保留战吼前数值，不改变规则结算。
            if card.type in (CardType.MINION, CardType.WEAPON) and card.zone == Zone.PLAY:
                self.events.append({'type': 'Summon', 'source': card.entity_id, 'target': card.entity_id,
                    'owner': source.game.players.index(card.controller), 'amount': 0,
                    'card_type': int(card.type), 'visual': card_view(card),
                    'index': args[2] if len(args) > 2 and args[2] is not None else -1})
        elif kind == 'Death':
            record_death(args[0])
            self.events.append({'type': 'Death', 'source': args[0].entity_id, 'target': args[0].entity_id, 'amount': 0})

    def turn(self, player):
        self.events.append({'type': 'TURN_START', 'source': player.entity_id, 'target': 0, 'amount': 0})


class ObservedGame(Game):
    def trigger(self, source, actions, event_args):
        # 只记录实际监听器回调，普通广播和空战吼不能冒充扳机触发。
        if (event_args is not None and actions and source.type == CardType.MINION
                and source.zone == Zone.PLAY and not source.ignore_scripts):
            for observer in self.manager.observers:
                if isinstance(observer, EventObserver):
                    observer.events.append({'type': 'Trigger', 'source': source.entity_id,
                                            'target': source.entity_id, 'amount': 0})
        return super().trigger(source, actions, event_args)


class Match:
    def __init__(self, names, classes, decks, seed=None):
        initialize()
        for cls, deck in zip(classes, decks):
            validate_deck(cls, deck)
        self.id = uuid.uuid4().hex
        self.seed = seed if seed is not None else secrets.randbits(64)
        self.game = ObservedGame(players=[DKPlayer(n, list(d), HEROES[c]) for n, c, d in zip(names, classes, decks)], seed=self.seed)
        self.observer = EventObserver()
        self.game.manager.register(self.observer)
        self.version = 0
        self.accepted = {}
        self.game.start()

    @property
    def mulligan(self):
        return any(isinstance(p.choice, MulliganChoice) for p in self.game.players)

    def entity(self, entity_id):
        return next((e for e in self.game if e.entity_id == entity_id), None)

    def legal_moves(self, seat):
        p = self.game.players[seat]
        if self.game.ended:
            return []
        if self.mulligan:
            return [{'kind': 'mulligan', 'cards': []}] if isinstance(p.choice, MulliganChoice) else []
        if p.choice:
            return [{'kind': 'choose', 'entity': c.entity_id} for c in p.choice.cards]
        if p != self.game.current_player:
            return []
        result = []
        for c in p.hand:
            if not c.is_playable():
                continue
            options = c.choose_cards if c.must_choose_one else [c]
            for option in options:
                targets = option.play_targets if option.requires_target() else [None]
                for target in targets:
                    move = {'kind': 'play', 'entity': c.entity_id, 'target': getattr(target, 'entity_id', 0)}
                    if c.must_choose_one:
                        move['choice'] = option.id
                    if c.type == CardType.MINION:
                        result.extend(dict(move, index=i) for i in range(len(p.field)+1))
                    else:
                        result.append(move)
        power = p.hero.power
        if power.is_usable():
            targets = power.play_targets if power.requires_target() else [None]
            result.extend({'kind': 'power', 'entity': power.entity_id, 'target': getattr(t, 'entity_id', 0)} for t in targets)
        for c in p.characters:
            if c.can_attack():
                result.extend({'kind': 'attack', 'entity': c.entity_id, 'target': t.entity_id}
                              for t in c.attack_targets if c.can_attack(t))
        result.append({'kind': 'end_turn'})
        return result

    def apply(self, seat, command, *, cause='player'):
        if cause not in ('player', 'disconnect'):
            raise ValueError('invalid action cause')
        action_id = command.get('action_id', '')
        if not isinstance(action_id, str) or not 1 <= len(action_id) <= 80:
            raise ValueError('invalid action_id')
        fingerprint = json.dumps(command, sort_keys=True)
        key = (seat, action_id)
        if key in self.accepted:
            if self.accepted[key] != fingerprint:
                raise ValueError('action_id reused with different command')
            return False
        if command.get('match_id') != self.id:
            raise ValueError('match mismatch')
        p = self.game.players[seat]
        kind = command.get('kind')
        version = command.get('version')
        # 双方可以同时确认起手；另一人的确认不会改变本人的起手实体。
        pending_mulligan = kind == 'mulligan' and isinstance(p.choice, MulliganChoice)
        if type(version) is not int or (version != self.version and not (
                pending_mulligan and 0 <= version < self.version)):
            raise ValueError('stale state version')
        move = {'kind': kind}
        if kind in ('play', 'attack', 'power', 'choose'):
            move['entity'] = command.get('entity', 0)
        if kind in ('play', 'attack', 'power'):
            move['target'] = command.get('target', 0)
        if kind == 'play':
            entity = self.entity(command.get('entity', 0))
            if entity and entity.type == CardType.MINION:
                move['index'] = command.get('index', len(p.field))
                if type(move['index']) is not int:
                    raise ValueError('invalid minion index')
        if command.get('choice'):
            move['choice'] = command['choice']
        if command.get('kind') == 'mulligan':
            selected = command.get('cards', [])
            if not isinstance(p.choice, MulliganChoice) or not isinstance(selected, list) or len(set(selected)) != len(selected):
                raise ValueError('invalid mulligan')
            options = {c.entity_id: c for c in p.choice.cards}
            if any(i not in options for i in selected):
                raise ValueError('invalid mulligan card')
        elif command.get('kind') != 'concede' and move not in self.legal_moves(seat):
            raise ValueError('illegal move')
        if self.game.ended:
            raise ValueError('match already ended')
        backup = copy.deepcopy(self.game)
        self.observer.events.clear()
        hero_power_target_event = None
        concede_event = None
        try:
            kind = command['kind']
            if kind == 'mulligan':
                p.choice.choose(*(options[i] for i in selected))
            elif kind == 'choose':
                p.choice.choose(next(c for c in p.choice.cards if c.entity_id == command['entity']))
            elif kind == 'play':
                self.entity(command['entity']).play(target=self.entity(command.get('target', 0)), choose=command.get('choice'), index=move.get('index'))
            elif kind == 'attack':
                self.entity(command['entity']).attack(self.entity(command['target']))
            elif kind == 'power':
                power = p.hero.power
                target = self.entity(command.get('target', 0))
                if power.id == 'HERO_09bp' and command.get('target', 0) != 0 and target is not None:
                    hero_power_target_event = {
                        'type': 'HeroPowerTarget', 'source': power.entity_id,
                        'target': target.entity_id, 'owner': seat,
                        'card_id': power.id, 'event_id': action_id,
                    }
                power.use(target=target)
            elif kind == 'end_turn':
                self.game.end_turn()
            elif kind == 'concede':
                if cause == 'player':
                    concede_event = {
                        'type': 'Concede', 'source': p.hero.entity_id,
                        'target': p.hero.entity_id, 'owner': seat,
                        'card_id': p.hero.id, 'event_id': action_id,
                    }
                p.concede()
        except GameOver:
            pass
        except Exception:
            self.game = backup
            self.observer = self.game.manager.observers[0]
            raise
        if hero_power_target_event:
            self.observer.events.append(hero_power_target_event)
        if concede_event:
            self.observer.events.append(concede_event)
        self.version += 1
        self.accepted[key] = fingerprint
        return True

    def snapshot(self, seat):
        p = self.game.players[seat]
        card = card_view
        players = []
        for i, player in enumerate(self.game.players):
            players.append({'name': player.name, 'seat': i, 'hero': card(player.hero),
                            'power': card(player.hero.power), 'mana': player.mana, 'max_mana': player.max_mana,
                            'deck_count': len(player.deck), 'hand_count': len(player.hand),
                            'hand': [card(c) for c in player.hand] if seat == i else [],
                            'field': [card(c) for c in player.field],
                            'weapon': card(player.weapon) if player.weapon else None,
                            'secrets': [card(c) for c in player.secrets] if seat == i else [],
                            'secret_count': len(player.secrets), 'result': player.playstate.name,
                            'corpses': getattr(player, 'corpses', 0), 'corpses_spent': getattr(player, 'corpses_spent', 0)})
        public = [{'name': x['name'], 'hero': x['hero'], 'power': x['power'], 'mana': x['mana'], 'max_mana': x['max_mana'], 'field': x['field'],
                   'hand_count': x['hand_count'], 'deck_count': x['deck_count'], 'result': x['result'],
                   'weapon': x['weapon'], 'secret_count': x['secret_count'], 'corpses': x['corpses'], 'corpses_spent': x['corpses_spent']} for x in players]
        public.append({'turn': self.game.turn, 'active': self.game.players.index(self.game.current_player) if self.game.current_player else -1})
        digest = hashlib.sha256(json.dumps(public, sort_keys=True).encode()).hexdigest()
        return {'type': 'state', 'match_id': self.id, 'version': self.version, 'seat': seat,
                'turn': self.game.turn, 'active': self.game.players.index(self.game.current_player) if self.game.current_player else -1,
                'phase': 'finished' if self.game.ended else 'mulligan' if self.mulligan else 'play',
                'players': players, 'moves': self.legal_moves(seat), 'public_hash': digest,
                'choices': [card(c) for c in p.choice.cards] if p.choice and not self.mulligan else [],
                'events': self.observer.events[-512:]}

    def dump(self):
        return {'match_id': self.id, 'seed': self.seed, 'version': self.version,
                'views': [self.snapshot(0), self.snapshot(1)],
                'decks': [[c.id for c in p.deck] for p in self.game.players],
                'rng_state': repr(self.game.random.getstate())}
