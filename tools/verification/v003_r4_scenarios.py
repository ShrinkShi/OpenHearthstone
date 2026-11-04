"""Independent legal Match.apply variations for V003-r4."""
import json

from hearthstone.enums import Zone
from tools.importer import PROJECT
from tools.visual_scenarios import fixture, transition


def main():
    folder = PROJECT / 'work/v003-r4-rule-variants'
    folder.mkdir(parents=True, exist_ok=True)
    for name in ('fan-empty', 'fan-lethal', 'agent-no-combo', 'agent-friendly-lethal'):
        match = fixture(0, 0, classes=(7, 10))
        own, enemy = match.game.players
        for item in list(enemy.field):
            item.zone = Zone.GRAVEYARD
        card = 'EX1_129' if name.startswith('fan') else 'EX1_134'
        target = None
        if name == 'fan-lethal':
            enemy.summon('CS2_171')
            enemy.summon('CS2_171')
        if name == 'agent-friendly-lethal':
            own.give('GAME_005').play()
            target = own.summon('CS2_171')
        own.give(card)
        match.observer.events.clear()
        before = [match.snapshot(seat) for seat in (0, 1)]
        transition(match, 'play', lambda move: match.entity(move.get('entity', 0)).id == card
                   and (target is None or move.get('target') == target.entity_id))
        after = [match.snapshot(seat) for seat in (0, 1)]
        plays = [event for event in after[0]['events'] if event['type'] == 'CardPlay' and event.get('card_id') == card]
        assert len(plays) == 1, (name, plays)
        hits = [event for event in after[0]['events'] if event['type'] == 'Hit' and event.get('source') == plays[0]['source']]
        assert len(hits) == (2 if name == 'fan-lethal' else 1 if target else 0), (name, hits)
        for seat in (0, 1):
            for phase, states in (('before', before), ('after', after)):
                (folder / f'{name}-{seat}-{phase}.json').write_text(json.dumps(states[seat], ensure_ascii=False), encoding='utf-8')
        print(name, card, 'version', after[0]['version'], 'target', plays[0].get('target'),
              'hit_targets', [event['target'] for event in hits], 'after_fields', [len(p.field) for p in match.game.players])


if __name__ == '__main__':
    main()
