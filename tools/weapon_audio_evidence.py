"""Generate real authority batches for the bounded weapon audio editor check."""
import copy

from game.database import preset
from game.match import Match
from .asset_lock import atomic_json
from .importer import PROJECT


def batches():
    result = []
    for replace in (False, True):
        match = Match(['WeaponA', 'WeaponB'], [7, 7], [preset(7), preset(7)], 52)
        def act(seat, move):
            match.apply(seat, dict(move, action_id='weapon-' + str(match.version), version=match.version, match_id=match.id))
        for seat in range(2):
            act(seat, dict(kind='mulligan', cards=[]))
        player = match.game.current_player
        seat = match.game.players.index(player)
        player.max_mana = 10
        player.used_mana = 0
        if replace:
            player.summon('CS2_082')
        def record(kind, move):
            before = copy.deepcopy([match.snapshot(s) for s in range(2)])
            act(seat, move)
            after = copy.deepcopy([match.snapshot(s) for s in range(2)])
            for perspective in range(2):
                result.append(dict(kind=kind, owner=seat, before=before[perspective], after=after[perspective]))
        record('replace' if replace else 'equip', next(x for x in match.legal_moves(seat) if x['kind'] == 'power'))
        if replace:
            continue
        for attack in range(2):
            record('wear' if attack == 0 else 'break', next(x for x in match.legal_moves(seat)
                   if x['kind'] == 'attack' and x['entity'] == player.hero.entity_id and x['target'] == player.opponent.hero.entity_id))
            if attack == 0:
                act(seat, dict(kind='end_turn'))
                act(1 - seat, dict(kind='end_turn'))
    return result


if __name__ == '__main__':
    rows = batches()
    path = PROJECT / 'work/weapon-audio-authority.json'
    atomic_json(path, dict(batches=rows))
    print('WEAPON AUDIO AUTHORITY', len(rows), path)
