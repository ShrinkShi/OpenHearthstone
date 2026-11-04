"""逐张通过服务器动作执行历史卡池，记录异常；不将冒烟测试标为完整核验。"""
import json
from pathlib import Path

from fireplace import actions, cards
from hearthstone.enums import Zone

from game.database import initialize, preset, scope
from game.death_knight import DK_IDS
from game.match import Match


def fixture(card_id, seed=42):
    initialize()
    cls = int(cards.db[card_id].card_class)
    cls = 4 if cls == 12 else cls
    match = Match(['Audit1', 'Audit2'], [cls, cls], [preset(cls), preset(cls)], seed)
    for p in match.game.players:
        p.choice.choose()
        for c in list(p.hand):
            c.zone = Zone.GRAVEYARD
        p.max_mana = 10
        p.used_mana = 0
        p.hero.damage = 16
    p = match.game.current_player
    for owner in (p, p.opponent):
        owner.summon('CS2_120')  # 野兽
        owner.summon('CS2_168')  # 鱼人
        owner.summon('CS2_186')  # 7 攻目标
        owner.summon('CS2_065')  # 恶魔
        owner.field[0].damage = 1
    p.give('CS2_182')
    p.give('CS2_029')
    p.give('CS2_120')
    p.give('EX1_409').zone = Zone.GRAVEYARD
    p.card('CS2_106').zone = Zone.PLAY
    card = p.give(card_id)
    return match, p, card


def execute(card_id, seed=42, choice=None):
    match, p, card = fixture(card_id, seed)
    seat = match.game.players.index(p)
    moves = [m for m in match.legal_moves(seat) if m['kind'] == 'play' and m['entity'] == card.entity_id]
    if choice is not None:
        moves = [m for m in moves if m.get('choice') == choice]
    if not moves:
        raise AssertionError(f'No legal play in prepared fixture: {card_id}')
    # 敌方随从优先；每个抉择分支由测试分别运行。
    moves.sort(key=lambda m: (m.get('target') not in [c.entity_id for c in p.opponent.field], m.get('index', 0)))
    match.apply(seat, dict(moves[0], match_id=match.id, version=match.version, action_id='audit-play'))
    for step in range(12):
        if not p.choice:
            break
        options = match.legal_moves(seat)
        match.apply(seat, dict(options[0], match_id=match.id, version=match.version, action_id=f'audit-choice-{step}'))
    if not match.game.ended:
        match.game.end_turn()
        if not match.game.ended:
            match.game.end_turn()
    if card in p.field and not match.game.ended:
        match.game.cheat_action(p.hero, [actions.Destroy(card)])
    assert match.snapshot(0)['public_hash'] == match.snapshot(1)['public_hash']
    return {'id': card_id, 'version': match.version, 'zone': card.zone.name,
            'events': sorted(set(e['type'] for e in match.observer.events))}


def main():
    results, errors = [], []
    selected = [i for i in scope() if i not in DK_IDS]
    for i, card_id in enumerate(selected):
        try:
            results.append(execute(card_id))
        except Exception as exc:
            errors.append({'id': card_id, 'error': f'{type(exc).__name__}: {exc}'})
        if i % 50 == 0:
            print(f'executed {i}/{len(selected)} errors={len(errors)}', flush=True)
    output = {'scope': len(selected), 'executed': len(results), 'results': results, 'errors': errors,
              'limitation': 'Controlled fixture smoke tests do not verify all numeric effects, branches or edge cases.'}
    path = Path(__file__).resolve().parents[1] / 'work/card-execution-audit.json'
    path.write_text(json.dumps(output, indent=2), encoding='utf-8')
    print(json.dumps({'executed': len(results), 'errors': errors}, indent=2), flush=True)


if __name__ == '__main__':
    main()
