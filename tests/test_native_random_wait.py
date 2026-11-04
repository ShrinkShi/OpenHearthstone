import json
from pathlib import Path

import pytest

ROOT=Path(__file__).parents[1]
AUDIT=json.loads((ROOT/'manifests/native-random-wait-refresh.json').read_text(encoding='utf-8'))


def test_original_random_wait_refresh_is_repeatable():
    assert len(AUDIT['programs'])==36 and AUDIT['updated']==0 and not AUDIT['failures']
    assert sum(p['actions'] for p in AUDIT['programs'])==37
    assert sum(p['parameters'] for p in AUDIT['programs'])==148


@pytest.mark.parametrize('record',AUDIT['programs'],ids=lambda p:p['file'])
def test_original_wait_parameters_are_bound(record):
    program=json.loads((ROOT/'local/runtime'/record['file']).read_text(encoding='utf-8'))
    actions=[a for m in program['machines'] for s in m['states'] for a in s['actions'] if a['name']=='RandomWaitAction']
    assert len(actions)==record['actions']
    for action in actions:
        params={p['name']:p for p in action['params']}
        assert not any(p.get('unresolved') for p in params.values())
        assert params['m_RealTime']['number']==0
        assert params['m_FinishEvent']['text']=='FINISHED'
        assert params['m_MinTime']['number']<=params['m_MaxTime']['number']
        assert params['m_MaxTime']['number']>0
