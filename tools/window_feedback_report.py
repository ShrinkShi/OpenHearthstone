"""汇总指定测试 Profile 的实际对局证据，不扫描其他用户数据。"""
import argparse
import json
import os
from pathlib import Path

from .asset_lock import atomic_json
from .importer import PROJECT


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--prefixes', default='window_oct09_a_,window_oct09_b_')
    args = parser.parse_args()
    base = Path(os.environ['LOCALAPPDATA']) / 'HearthstoneClone/profiles'
    matches = []
    for prefix in args.prefixes.split(','):
        if not prefix or '/' in prefix or '\\' in prefix or '..' in prefix:
            raise ValueError('测试 profile 前缀无效')
        states = [json.loads((base / f'{prefix}{i}/state.json').read_text(encoding='utf-8')) for i in (1, 2)]
        first, second = states
        assert all(s['phase'] == 'finished' for s in states)
        assert first['match_id'] == second['match_id']
        assert first['version'] == second['version'] and first['public_hash'] == second['public_hash']
        assert sorted(p['result'] for p in first['players']) == ['LOST', 'WON']
        assert any(p['hero']['health'] <= 0 for p in first['players'])
        matches.append(dict(profiles=[prefix + '1', prefix + '2'], match_id=first['match_id'],
            turn=first['turn'], version=first['version'], public_hash=first['public_hash'],
            players=[dict(name=p['name'], hero=p['hero']['card_id'], health=p['hero']['health'],
                          result=p['result']) for p in first['players']],
            performance=[json.loads((base / f'{prefix}{i}/performance.json').read_text()) for i in (1, 2)]))
    report = dict(matches=matches, notes=['真实 Windows Player 自动匹配，不修改状态；不是人工比赛。',
             'batchmode 不绘制完整 GUI，帧率不能代表正式游戏渲染性能。'])
    path = PROJECT / 'work/window-duel-results.json'
    atomic_json(path, report)
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
