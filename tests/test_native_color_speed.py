import json
from pathlib import Path

import pytest

from tools.native_effects import color_by_speed

DATA=json.loads((Path(__file__).parents[1]/'manifests/native-color-speed-refresh.json').read_text(encoding='utf-8'))
MODULES=[m for p in DATA['programs'] for m in p['modules']]


def test_all_known_color_speed_source_modules_are_mapped_without_failure():
    assert len(DATA['programs'])==7 and len(MODULES)==12 and not DATA['failures']
    assert all(m['source']['type']=='ParticleSystem' and m['node']>=0 for m in MODULES)


@pytest.mark.parametrize('module',MODULES,ids=[m['source']['pathid']+'-'+str(i) for i,m in enumerate(MODULES)])
def test_source_color_speed_gradient_values_times_and_ranges_are_preserved(module):
    original=module['original'];value=color_by_speed(original)
    assert value['range']==original['range']
    assert value['color']['mode']==original['gradient']['minMaxState']
    for source_key,target in [('maxGradient','high'),('minGradient','low')]:
        gradient=original['gradient'][source_key];restored=value['color'][target]
        assert restored['mode']==gradient['m_Mode']
        assert len(restored['colors'])==gradient['m_NumColorKeys']
        for i,key in enumerate(restored['colors']):
            assert key['value']==gradient['key'+str(i)]
            assert key['time']==gradient['ctime'+str(i)]/65535
        for i,key in enumerate(restored['alpha']):
            assert key['value']==gradient['key'+str(i)]['a']
            assert key['time']==gradient['atime'+str(i)]/65535


@pytest.mark.parametrize('module',[None,{},dict(enabled=False)])
def test_disabled_color_speed_never_introduces_a_default_gradient(module):
    assert color_by_speed(module) is None
