import json
import hashlib
import io
import os
import shutil
import subprocess
import sys

import pytest

from tools.importer import Importer, default_game_root
from tools import prepare_data


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


def test_fireplace_lfs_pointer_is_replaced_only_with_verified_data(tmp_path, monkeypatch):
    content = b'<CardDefs />'
    digest = hashlib.sha256(content).hexdigest()
    path = tmp_path / 'CardDefs.xml'
    pointer = f'version https://git-lfs.github.com/spec/v1\noid sha256:{digest}\nsize {len(content)}\n'
    path.write_text(pointer, encoding='ascii')
    monkeypatch.setattr(prepare_data, 'FIREPLACE_DIGEST', digest)
    monkeypatch.setattr(prepare_data.urllib.request, 'urlopen', lambda *a, **k: io.BytesIO(content))
    prepare_data.prepare_fireplace_carddefs(path)
    assert path.read_bytes() == content
    prepare_data.prepare_fireplace_carddefs(path)


def test_fireplace_unknown_data_is_preserved(tmp_path):
    path = tmp_path / 'CardDefs.xml'
    path.write_bytes(b'user data')
    with pytest.raises(ValueError, match='unexpected Fireplace'):
        prepare_data.prepare_fireplace_carddefs(path)
    assert path.read_bytes() == b'user data'


def test_fireplace_rejects_corrupt_download(tmp_path, monkeypatch):
    path = tmp_path / 'CardDefs.xml'
    pointer = f'version https://git-lfs.github.com/spec/v1\noid sha256:{prepare_data.FIREPLACE_DIGEST}\n'
    path.write_bytes(pointer.encode('ascii'))
    monkeypatch.setattr(prepare_data.urllib.request, 'urlopen', lambda *a, **k: io.BytesIO(b'corrupt'))
    with pytest.raises(ValueError, match='checksum mismatch'):
        prepare_data.prepare_fireplace_carddefs(path)
    assert path.read_bytes() == pointer.encode('ascii')


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


@pytest.mark.skipif(sys.platform != 'win32', reason='Windows junction launcher')
def test_launcher_connects_published_assets_without_overwriting_local_data(tmp_path):
    root = tmp_path / 'portable checkout 测试'
    root.mkdir()
    launcher = root / 'Start-LocalDuel.cmd'
    shutil.copyfile('Start-LocalDuel.cmd', launcher)
    runtime = root / 'assets' / 'runtime'
    runtime.mkdir(parents=True)
    (runtime / 'registry.json').write_text('{"entries": []}', encoding='utf-8')
    (root / 'assets' / 'card-database.json').write_text('published', encoding='utf-8')
    result = subprocess.run(['cmd', '/d', '/c', str(launcher)], cwd=tmp_path,
                            stdin=subprocess.DEVNULL, capture_output=True, text=True)
    assert result.returncode != 0 and 'Python environment is missing' in result.stdout
    assert (root / 'local' / 'runtime' / 'registry.json').is_file()
    assert (root / 'local' / 'card-database.json').read_text() == 'published'
    (root / 'local' / 'card-database.json').write_text('existing')
    subprocess.run(['cmd', '/d', '/c', str(launcher)], cwd=tmp_path,
                   stdin=subprocess.DEVNULL, capture_output=True, text=True)
    assert (root / 'local' / 'card-database.json').read_text() == 'existing'
