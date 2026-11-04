import json
from pathlib import Path

from tools.native_audio_config import recover
from tools.export_runtime import RuntimeExport

ROOT=Path(__file__).parents[1]


def test_original_template_matches_readonly_source():
    expected=json.loads((ROOT/'manifests/native-audio-config.json').read_text(encoding='utf-8'))
    ex=RuntimeExport();source=expected['template_source']
    cab=ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower())
    assert cab.objects[int(source['pathid'])].read_typetree()==expected['values']


def test_original_template_export_repeats_identically():
    ex=RuntimeExport()
    expected=json.loads((ROOT/'manifests/native-audio-config.json').read_text(encoding='utf-8'))
    assert recover(ex)==expected


def test_original_template_has_explicit_dependencies_and_curves():
    data=json.loads((ROOT/'manifests/native-audio-config.json').read_text(encoding='utf-8'))
    source=data['values']
    assert data['config_values']['m_PlayClipTemplate']['m_PathID']==int(data['template_source']['pathid'])
    assert source['MinDistance']==100 and source['MaxDistance']==500 and source['rolloffMode']==1
    assert source['OutputAudioMixerGroup']['m_PathID']==source['m_audioClip']['m_PathID']==0
    assert all(source[k]['m_Curve'] for k in ('rolloffCustomCurve','panLevelCustomCurve','spreadCustomCurve','reverbZoneMixCustomCurve'))
    assert not data['complete']
