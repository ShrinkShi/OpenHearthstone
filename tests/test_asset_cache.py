import json
import multiprocessing
from pathlib import Path
from types import SimpleNamespace

from tools.asset_lock import asset_lock, atomic_json, merge_effects
from tools.export_runtime import RuntimeExport


def test_two_stale_exporters_preserve_new_ids_and_updated_mapping(tmp_path):
    imp = SimpleNamespace(local=tmp_path)
    original = RuntimeExport(imp)
    original.registry['HERO'] = 'old.png'
    original.save()
    first, second = RuntimeExport(imp), RuntimeExport(imp)
    first.registry.update(HERO='new.png', CARD='card.png')
    first.errors.append({'id': 'BAD', 'error': 'failed export'})
    first.save()
    second.registry['VOICE'] = 'voice.wav'
    second.save()
    assert RuntimeExport(imp).registry == {'HERO': 'new.png', 'CARD': 'card.png', 'VOICE': 'voice.wav'}
    history = [json.loads(line) for line in (tmp_path/'runtime/export-history.jsonl').read_text().splitlines()]
    assert any(row['errors'] for row in history)
    assert history[-1]['errors'] == []


def test_effect_importers_preserve_other_cards_when_rerun(tmp_path):
    (tmp_path/'runtime').mkdir()
    merge_effects(tmp_path, [dict(card_id='FIREBALL', cast='fire')])
    merge_effects(tmp_path, [dict(card_id='FAN', cast='fan')])
    merge_effects(tmp_path, [dict(card_id='FIREBALL', cast='updated')])
    result = json.loads((tmp_path/'runtime/effect-index.json').read_text())['entries']
    assert {e['card_id']: e['cast'] for e in result} == {'FIREBALL': 'updated', 'FAN': 'fan'}


def increment_cache(directory):
    local = Path(directory)
    for _ in range(20):
        with asset_lock(local):
            path = local/'counter.json'
            value = json.loads(path.read_text())['count']
            atomic_json(path, {'count': value+1})


def test_asset_write_lock_serializes_real_processes(tmp_path):
    atomic_json(tmp_path/'counter.json', {'count': 0})
    context = multiprocessing.get_context('spawn')
    workers = [context.Process(target=increment_cache, args=(str(tmp_path),)) for _ in range(2)]
    for worker in workers:
        worker.start()
    for worker in workers:
        worker.join(15)
        assert worker.exitcode == 0
    assert json.loads((tmp_path/'counter.json').read_text())['count'] == 40
    assert not list(tmp_path.glob('*.tmp'))
