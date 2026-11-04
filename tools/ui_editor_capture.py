"""通过真实 Windows Player 验证各校对页；原始截图仅保留本地。"""
import argparse
import json
import subprocess
import time
from pathlib import Path

from PIL import Image
from .importer import PROJECT
from .background_player import startup

VIEWS = ['card.minion', 'card.spell', 'card.weapon', 'weapon', 'minion', 'hero', 'power',
         'power.detail', 'scene', 'hand', 'mulligan', 'card_back', 'mana', 'attack', 'health', 'armor', 'durability',
         'battle.speech', 'battle.emotes']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--views', default=','.join(VIEWS))
    parser.add_argument('--output', type=Path, default=PROJECT / 'tests/visual/current/ui-editor')
    parser.add_argument('--layout', type=Path)
    parser.add_argument('--counts', action='store_true', help='额外检查 1~10 手牌 / 1~7 随从')
    parser.add_argument('--background', action='store_true', help='后台启动测试窗口，不激活窗口')
    args = parser.parse_args()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    cases = [(view, 4, 3, view) for view in args.views.split(',')]
    if any(view not in VIEWS for view, *_ in cases):
        parser.error('unknown editor view')
    if args.counts:
        cases.extend(('hand', n, 3, 'hand' + str(n)) for n in range(1, 11))
        cases.extend(('scene', 4, n, 'field' + str(n)) for n in range(1, 8))
    report = []
    for view, hand, field, name in cases:
        profile = 'uicapture-' + name.replace('.', '-')
        log = PROJECT / 'work' / ('ui-capture-' + name + '.log')
        command = [str(PROJECT / 'build/client/HearthstoneClone.exe'), '--ui-editor',
            '--profile', profile, '--name', 'UICapture', '--ui-view', view, '--ui-count', str(hand),
            '--ui-field-count', str(field), '--ui-capture', '--ui-exit', '--screenshot-dir', str(args.output),
            '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)]
        if args.layout:
            command.extend(['--ui-layout', str(args.layout.resolve())])
        started = time.time()
        process = subprocess.Popen(command, cwd=PROJECT, **startup(args.background))
        try:
            process.wait(timeout=40)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('UI editor timed out: ' + name)
        screenshot = args.output / (profile + '-ui-' + view + '.png')
        if process.returncode or not screenshot.exists() or screenshot.stat().st_mtime < started:
            raise RuntimeError('UI editor did not produce a fresh capture: ' + name)
        text = log.read_text(encoding='utf-8', errors='replace')
        if 'Exception' in text or 'NullReference' in text:
            raise RuntimeError('UI editor exception: ' + str(log))
        with Image.open(screenshot) as image:
            if image.size != (1920, 1080):
                raise RuntimeError('UI editor resolution mismatch: ' + name)
            if max(b - a for a, b in image.convert('RGB').getextrema()) < 30:
                raise RuntimeError('blank UI capture; player graphics unavailable: ' + name)
        report.append({'view': view, 'hand': hand, 'field': field, 'file': screenshot.name, 'exit': process.returncode})
        print(name, 'OK', flush=True)
    (args.output / 'ui-capture-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
