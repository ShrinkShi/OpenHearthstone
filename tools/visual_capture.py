"""运行真实 Windows Player 的固定场景并检查截图、异常与像素回归。"""
import argparse
import json
import subprocess
import time
from pathlib import Path

from PIL import Image
from .importer import PROJECT
from .visual_diff import compare
from .background_player import startup

SCENES = ['match_start', 'mulligan', 'turn_start', 'hand4', 'hand7', 'minion_on_board',
          'multiple_minions', 'card_hover', 'card_drag', 'minion_summon', 'minion_attack',
          'hero_power', 'spell_cast', 'damage', 'death', 'end_turn', 'victory', 'defeat']
SCENES += ['mulligan_hover', 'power_hover', 'power_disabled', 'minion_hover_left',
           'minion_hover_right', 'end_turn_exhausted', 'history', 'history_hover', 'stat_icons']
SCENES += ['weapon_equipped', 'weapon_summon', 'power_used', 'power_used_hover',
           'fan_of_knives', 'fan_triggers']
SCENES += ['hero_combat', 'hero_combat_trigger', 'status_badges', 'arcane_explosion',
           'frost_nova', 'hellfire',
           'blizzard', 'flamestrike', 'polymorph', 'holy_nova', 'nightblade', 'si7_agent', 'big_game_hunter']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--scenes', default=','.join(SCENES))
    parser.add_argument('--output', type=Path, default=PROJECT / 'tests/visual/current')
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--background', action='store_true', help='后台启动测试窗口，不激活窗口')
    args = parser.parse_args()
    args.output = args.output.resolve()
    if args.reference:
        args.reference = args.reference.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    report = []
    for scene in args.scenes.split(','):
        log = PROJECT / 'work' / ('visual-' + scene + '.log')
        started = time.time()
        command = [str(PROJECT / 'build/client/HearthstoneClone.exe'),
            '--profile', 'visual', '--name', 'Shrink1', '--visual-test', scene, '--visual-exit',
            '--screenshot-dir', str(args.output), '-screen-width', '1920', '-screen-height', '1080',
            '-screen-fullscreen', '0', '-logFile', str(log)]
        process = subprocess.Popen(command, cwd=PROJECT, **startup(args.background))
        try:
            process.wait(timeout=40)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('visual scene timed out: ' + scene)
        screenshots = [args.output / ('visual-' + scene + '.png')]
        if (PROJECT / 'local/visual/states' / (scene + '-after.json')).exists():
            screenshots.insert(0, args.output / ('visual-' + scene + '-action.png'))
        if process.returncode or any(not p.exists() or p.stat().st_mtime < started for p in screenshots):
            raise RuntimeError('visual scene did not produce fresh captures: ' + scene)
        text = log.read_text(encoding='utf-8', errors='replace')
        if 'Exception' in text or 'NullReference' in text:
            raise RuntimeError('visual client exception: ' + str(log))
        for screenshot in screenshots:
            with Image.open(screenshot) as image:
                if image.size != (1920, 1080):
                    raise RuntimeError('unexpected resolution: ' + str(screenshot))
                if max(b - a for a, b in image.convert('RGB').getextrema()) < 30:
                    raise RuntimeError('blank capture; player graphics unavailable: ' + str(screenshot))
                entry = {'scene': scene, 'file': screenshot.name, 'size': image.size}
                if args.reference and (args.reference / screenshot.name).exists():
                    with Image.open(args.reference / screenshot.name) as reference:
                        diff, metrics = compare(reference, image)
                    folder = PROJECT / 'tests/visual/diff'
                    folder.mkdir(parents=True, exist_ok=True)
                    diff.save(folder / screenshot.name)
                    entry['comparison'] = metrics
                report.append(entry)
        print(scene, 'OK', len(screenshots), flush=True)
    (args.output / 'capture-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
