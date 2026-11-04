import copy
import json
from pathlib import Path

import pytest

from tools.timeline_source import animation_compatible


def source():
    data=json.loads((Path(__file__).parents[1]/'manifests/original-timeline-2ac223d9b583771c9eb24338.json').read_text(encoding='utf-8'))
    track=next(t for t in data['timelines'][0]['tracks'] if t['kind']=='AnimationTrack')
    clip=track['clips'][0]
    return data,track,clip


def test_original_director_nested_bindings_and_streamed_clip_are_retained():
    data,track,clip=source()
    assert not data['complete']
    assert data['timelines'][0]['node']==1 and track['node']==2
    assert len(data['timelines'][0]['tracks'])==7
    assert clip['start']==pytest.approx(1/12) and clip['duration']==pytest.approx(4.433333333333334)
    assert clip['compatible'] and clip['post_unlimited']
    assert data['animations'][0]['tracks']==183 and not data['animations'][0]['unresolved']
    assert data['animations'][0]['duration']==2
    assert all(x['node']>=0 for x in data['timelines'][0]['exposed'])


@pytest.mark.parametrize('file', ['2ac223d9b583771c9eb24338','b0cbd0aa93fedbfc28ee3f8e'])
def test_original_control_roots_resolve_distinct_same_named_smoke_and_dependencies(file):
    data=json.loads((Path(__file__).parents[1]/f'manifests/original-timeline-{file}.json').read_text(encoding='utf-8'))
    controls=[c['control'] for t in data['timelines'][0]['tracks'] if t['kind']=='ControlRandomizedTrack' for c in t['clips']]
    assert [c['node'] for c in controls]==[31,34]
    assert [c['particle_roots'] for c in controls]==[[31],[34]]
    assert all(c['compatible'] and c['active'] and c['post']==2 and not c['unsupported_components'] for c in controls)


@pytest.mark.parametrize('part,key,value', [
    ('track','m_Children',[{'m_PathID':1}]),
    ('track','m_Position',{'x':1}),
    ('asset','m_Loop',2),
    ('asset','m_EulerAngles',{'z':10}),
    ('clip','m_EaseInDuration',.2),
    ('clip','m_BlendOutDuration',.2),
    ('clip','m_PostExtrapolationMode',3),
])
def test_unsupported_timeline_offsets_blends_and_extrapolation_are_rejected(part,key,value):
    _,track,clip=source()
    parts={'track':copy.deepcopy(track['original']), 'asset':copy.deepcopy(clip['original_asset']), 'clip':copy.deepcopy(clip['original'])}
    assert animation_compatible(**parts)
    parts[part][key]=value
    assert not animation_compatible(**parts)
