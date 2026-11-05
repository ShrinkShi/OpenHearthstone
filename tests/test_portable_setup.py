import json
import os
import shutil
import subprocess
import sys

import pytest

from tools.importer import Importer, default_game_root


def test_missing_game_reports_configuration_without_creating_cache(tmp_path):
    local = tmp_path / 'cache'
    with pytest.raises(FileNotFoundError, match='HEARTHSTONE_GAME_ROOT'):
        Importer(tmp_path / 'missing game', local)
    assert not local.exists()


def test_game_root_environment_is_used(tmp_path):
    game = tmp_path / 'Game folder'
    env = dict(os.environ, HEARTHSTONE_GAME_ROOT=str(game))
    result = subprocess.run(
        [sys.executable, '-c', 'from tools.importer import DEFAULT_GAME; print(DEFAULT_GAME)'],
        env=env, capture_output=True, text=True, check=True,
    )
    assert result.stdout.strip() == str(game)


def test_previous_scan_path_is_reused_and_environment_takes_priority(tmp_path, monkeypatch):
    game = tmp_path / 'previous game'
    manifest = game / 'Data' / 'Win' / 'asset_manifest.unity3d'
    manifest.parent.mkdir(parents=True)
    manifest.touch()
    (tmp_path / 'asset-audit.json').write_text(json.dumps({'installation': str(game)}))
    monkeypatch.delenv('HEARTHSTONE_GAME_ROOT', raising=False)
    assert default_game_root(tmp_path) == game
    monkeypatch.setenv('HEARTHSTONE_GAME_ROOT', str(tmp_path / 'override'))
    assert default_game_root(tmp_path) == tmp_path / 'override'


@pytest.mark.skipif(sys.platform != 'win32', reason='Windows batch launcher')
def test_launcher_reports_each_missing_prerequisite_in_path_with_spaces(tmp_path):
    root = tmp_path / 'checkout with spaces 测试'
    root.mkdir()
    launcher = root / 'Start-LocalDuel.cmd'
    shutil.copyfile('Start-LocalDuel.cmd', launcher)

    def run():
        return subprocess.run(['cmd', '/d', '/c', str(launcher)], cwd=tmp_path,
                              stdin=subprocess.DEVNULL, capture_output=True, text=True)

    result = run()
    assert result.returncode != 0 and 'Python environment is missing' in result.stdout
    python = root / '.venv' / 'Scripts' / 'python.exe'
    python.parent.mkdir(parents=True)
    python.touch()
    result = run()
    assert result.returncode != 0 and 'Client build is missing' in result.stdout
    client = root / 'build' / 'client' / 'HearthstoneClone.exe'
    client.parent.mkdir(parents=True)
    client.touch()
    result = run()
    assert result.returncode != 0 and 'asset registry is missing' in result.stdout
