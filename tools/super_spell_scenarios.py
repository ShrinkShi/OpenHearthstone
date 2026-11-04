"""Generate two real Match.apply flows for the autonomous root player checks."""
import json

from .importer import PROJECT
from .visual_scenarios import fixture, transition


def main():
    folder = PROJECT / 'work/f003-rule-scenes'
    folder.mkdir(parents=True, exist_ok=True)
    for card in ('EX1_129', 'EX1_134'):
        match = fixture(0, 0, classes=(7, 10))
        if card == 'EX1_129':
            match.game.players[1].summon('EX1_402')
        else:
            match.game.players[0].give('GAME_005').play()
        match.game.players[0].give(card)
        match.observer.events.clear()
        before = [match.snapshot(seat) for seat in (0, 1)]
        transition(match, 'play', lambda move: match.entity(move.get('entity', 0)).id == card
                   and (not move.get('target') or match.entity(move['target']) == match.game.players[1].hero))
        after = [match.snapshot(seat) for seat in (0, 1)]
        for seat in (0, 1):
            for phase, states in (('before', before), ('after', after)):
                (folder / f'{card}-{seat}-{phase}.json').write_text(json.dumps(states[seat], ensure_ascii=False), encoding='utf-8')
        print(card, 'Match.apply version', after[0]['version'], 'events', [event['type'] for event in after[0]['events']])


if __name__ == '__main__':
    main()
