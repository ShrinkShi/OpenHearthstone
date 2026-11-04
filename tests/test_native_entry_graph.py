import json
from pathlib import Path

import pytest

ROOT = Path(__file__).parents[1]


@pytest.mark.parametrize('key', ['2ac223d9b583771c9eb24338', 'b0cbd0aa93fedbfc28ee3f8e'])
def test_original_entry_sound_delay_outlives_its_animation_state(key):
    """The exact source Wait exits Anim before the delayed impact is due."""
    source = json.loads((ROOT / 'local/runtime' / (key + '.native-effect.json')).read_text(encoding='utf-8'))
    state = next(s for s in source['machines'][0]['states'] if s['name'] == 'Anim')
    sounds = [a for a in state['actions'] if a['name'] == 'AudioPlayClipAction']
    wait = next(a for a in state['actions'] if a['name'] == 'Wait')
    value = lambda action, name: next(p['number'] for p in action['params'] if p['name'] == name)
    assert len(sounds) == 2
    assert value(sounds[0], 'm_Delay') == 0
    assert value(wait, 'time') < value(sounds[1], 'm_Delay')
    assert state['transitions'] == [{'event': 'FINISHED', 'state': 'Actor Visible'}]


def test_source_playmaker_coroutine_survives_state_exit():
    audit = json.loads((ROOT / 'manifests/original-script-audit.json').read_text(encoding='utf-8'))
    scripts = {s['type']: s for s in audit['scripts']}
    base = (ROOT / scripts['HutongGames.PlayMaker.FsmStateAction']['cache']).read_text(encoding='utf-8-sig')
    on_exit = base.split('System.Void HutongGames.PlayMaker.FsmStateAction::OnExit()')[1].split('System.Void')[0]
    assert 'IL_0000: ret' in on_exit
    wrapper = (ROOT / scripts['PlayMakerFSM/<DoCoroutine>d__46']['cache']).read_text(encoding='utf-8-sig')
    assert 'IEnumerator::MoveNext()' in wrapper
    assert 'StopCoroutine' not in wrapper
    assert 'get_ActiveState' not in wrapper
