import json
from pathlib import Path

from tools.native_sound_limits import audit
from tools.export_runtime import RuntimeExport

ROOT=Path(__file__).parents[1]


def test_source_sound_limit_and_mixer_audit_repeats_identically():
    expected=json.loads((ROOT/'manifests/native-sound-limits.json').read_text(encoding='utf-8'))
    assert audit(RuntimeExport())==expected


def test_actual_limit_options_and_explicit_templates():
    data=json.loads((ROOT/'manifests/native-sound-limits.json').read_text(encoding='utf-8'))
    assert len(data['limited_actions'])==19 and len(data['explicit_templates'])==2
    assert all(r['options']['m_InstanceLimited']==1 and r['options']['m_InstanceLimitMaximum']==1 and r['options']['m_LimitMaxOutOption']==0 for r in data['limited_actions'])
    assert {r['options']['m_InstanceLimitedDuration'] for r in data['limited_actions']}=={0.10000000149011612,0.20000000298023224,0.30000001192092896,0.5}
    assets={(r['source']['cab'],r['source']['pathid']):r for r in data['mixer_assets']}
    for template in data['explicit_templates']:
        source=template['mixer_group'];group=assets[source['cab'],source['pathid']]
        assert group['values']['m_Name']=='L_SFX'
        assert any(r['source']['pathid']==str(group['values']['m_AudioMixer']['m_PathID']) for r in data['mixer_assets'])
    assert not data['complete']
