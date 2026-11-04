import json
from pathlib import Path

from tools.importer import Importer
from tools.spell_visibility_source import inspect

ROOT = Path(__file__).parents[1]


def test_spell_visibility_catalog_keeps_exact_source_binding_and_gaps():
    catalog = json.loads((ROOT / 'manifests/original-spell-visibility-source.json').read_text(encoding='utf-8'))
    assert catalog['full_spell_visibility_restored'] is False
    assert len(catalog['programs']) == 205
    assert sum(len(p['bindings']) for p in catalog['programs']) == 215
    assert sum(bool(p['dependency_errors']) for p in catalog['programs']) == 44
    for record in catalog['programs']:
        for binding in record['bindings']:
            assert binding['kind'] in {'Spell', 'SuperSpell', 'SpellState'}
            assert binding['pointers']['m_GameObject'] is not None
            assert 'm_shown' not in binding['values']
            nodes = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))['nodes']
            for pointer in binding['pointers'].values():
                if pointer is not None and pointer['node'] >= 0:
                    assert pointer['node'] < len(nodes)
                    assert nodes[pointer['node']]['name'] == pointer['name']


def test_original_spell_container_and_state_source_repeat_exactly():
    catalog = json.loads((ROOT / 'manifests/original-spell-visibility-source.json').read_text(encoding='utf-8'))
    importer = Importer()
    selected = []
    for kind in ('Spell', 'SuperSpell', 'SpellState'):
        matches = [p for p in catalog['programs'] if any(b['kind'] == kind for b in p['bindings'])]
        if matches:
            selected.append(matches[0])
    assert selected
    for record in selected:
        assert inspect(importer, ROOT / 'local/runtime' / record['file']) == record


def test_source_state_playback_exports_exact_runtime_bindings():
    catalog = json.loads((ROOT / 'manifests/original-spell-state-playback.json').read_text(encoding='utf-8'))
    assert catalog['full_spell_controller_restored'] is False
    assert len(catalog['programs']) == 8
    states = [s for p in catalog['programs'] for s in p['states']]
    assert len(states) == 23
    assert sum(len(s['animations']) for s in states) == 51
    for record in catalog['programs']:
        runtime = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert runtime['spell_states'] == record['states']
        for state in record['states']:
            assert state['node'] >= 0 and state['audio_count'] == 0
            assert state['delay'] == 0
            for animation in state['animations']:
                assert not animation['unresolved']
                assert animation['target'] in (0, 1)
                if not animation['clip_null']:
                    assert sum(c['id'] == animation['clip'] for c in runtime['animations']) == 1
    assert any(a['layer'] == 1 for s in states for a in s['animations'])
    assert any(a['particles'] for s in states for a in s['animations'])


def test_original_animation_events_preserve_receivers_parameters_and_event_only_horizon():
    catalog = json.loads((ROOT / 'manifests/original-spell-state-playback.json').read_text(encoding='utf-8'))
    events = []
    for record in catalog['programs']:
        runtime = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        assert runtime['animation_event_receivers'] == record['receivers']
        for animation in record['animation_events']:
            cached = next(a for a in runtime['animations'] if a['id'] == animation['clip'])
            assert cached['events'] == animation['events']
            assert cached['event_horizon'] == max((e['time'] for e in animation['events']), default=0)
            events.extend(animation['events'])
            for event in animation['events']:
                assert event['function'] == event['raw']['functionName']
                assert event['time'] == event['raw']['time']
                assert event['integer'] == event['raw']['intParameter']
                assert event['number'] == event['raw']['floatParameter']
                assert event['object_null'] == (event['raw']['objectReferenceParameter']['m_PathID'] == 0)
    assert events
    assert {'OnStateFinished', 'OnSpellFinished', 'PlayDefaultSound', 'PlayNewParticles3'} <= {e['function'] for e in events}
    assert any(a['duration'] == 0 and a['event_horizon'] > 0 for p in catalog['programs']
               for a in json.loads((ROOT / 'local/runtime' / p['file']).read_text(encoding='utf-8'))['animations'])


def test_animation_event_export_replays_original_client_data(tmp_path):
    importer = Importer()
    catalog = json.loads((ROOT / 'manifests/original-spell-state-playback.json').read_text(encoding='utf-8'))
    for record in catalog['programs']:
        source = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        fixture = tmp_path / record['file']
        fixture.write_text(json.dumps(source), encoding='utf-8')
        inspect(importer, fixture, export_states=True)
        assert json.loads(fixture.read_text(encoding='utf-8')) == source
