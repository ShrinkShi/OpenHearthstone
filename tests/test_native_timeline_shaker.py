import copy
import json
from pathlib import Path

import pytest

from tools.timeline_source import shaker_data


ROOT=Path(__file__).parents[1]
FILES=['2ac223d9b583771c9eb24338','b0cbd0aa93fedbfc28ee3f8e']


@pytest.mark.parametrize('file',FILES)
def test_exact_original_camera_shaker_template_is_exported(file):
    source=json.loads((ROOT/'manifests'/f'original-timeline-{file}.json').read_text(encoding='utf-8'))
    track=next(t for t in source['timelines'][0]['tracks'] if t['kind']=='CameraShaker2Track')
    assert not track['muted'] and track['node']==-1
    clip=track['clips'][0];raw=clip['original_asset']['template'];data=clip['shaker']
    assert data==shaker_data(clip['original_asset'],clip['original'])
    assert data['compatible'] and data['amount']==raw['m_Amount'] and data['interval']==raw['m_interval']
    assert len(data['curve'])==7 and clip['duration']==pytest.approx(5/6)
    for restored,original in zip(data['curve'],raw['m_IntensityCurve']['m_Curve']):
        for name in ['time','value','inSlope','outSlope','inWeight','outWeight','weightedMode']:
            assert restored[name]==original[name]


@pytest.mark.parametrize('key,value',[('m_PreExtrapolationMode',1),('m_PostExtrapolationMode',1),
    ('m_EaseInDuration',.1),('m_EaseOutDuration',.1),('m_BlendInDuration',.1),('m_BlendOutDuration',.1),
    ('m_ClipIn',.1),('m_TimeScale',2),('m_Duration',0)])
def test_unrestored_timeline_shaker_branches_are_not_silently_accepted(key,value):
    source=json.loads((ROOT/'manifests'/f'original-timeline-{FILES[0]}.json').read_text(encoding='utf-8'))
    clip=next(t for t in source['timelines'][0]['tracks'] if t['kind']=='CameraShaker2Track')['clips'][0]
    raw=copy.deepcopy(clip['original']);raw[key]=value
    assert not shaker_data(clip['original_asset'],raw)['compatible']
