import json

import pytest

from tools import verify_runtime


def test_verify_runtime_accepts_complete_relative_assets(tmp_path, monkeypatch):
    tool = tmp_path / 'tools' / 'verify_runtime.py'
    tool.parent.mkdir()
    runtime = tmp_path / 'assets' / 'runtime'
    runtime.mkdir(parents=True)
    (runtime / 'sample.png').write_bytes(b'fixture')
    (runtime / 'registry.json').write_text(json.dumps({'entries': [{'path': 'sample.png'}]}))
    monkeypatch.setattr(verify_runtime, '__file__', str(tool))
    verify_runtime.main()


def test_verify_runtime_rejects_missing_and_parent_paths(tmp_path, monkeypatch):
    tool = tmp_path / 'tools' / 'verify_runtime.py'
    tool.parent.mkdir()
    runtime = tmp_path / 'assets' / 'runtime'
    runtime.mkdir(parents=True)
    (runtime / 'registry.json').write_text(json.dumps({'entries': [{'path': '../outside'}, {'path': 'missing'}]}))
    monkeypatch.setattr(verify_runtime, '__file__', str(tool))
    with pytest.raises(RuntimeError, match='Invalid runtime assets'):
        verify_runtime.main()
