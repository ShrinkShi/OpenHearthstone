import json
from pathlib import Path

from tools.native_mixer import recover,verify_bundle
from tools.native_audio_templates import refresh
from tools.export_runtime import RuntimeExport

ROOT=Path(__file__).parents[1]


def test_original_mixer_program_repeats_identically():
    expected=json.loads((ROOT/'manifests/native-mixer-program.json').read_text(encoding='utf-8'))
    assert recover()==expected


def test_compiled_mixer_matches_original_dsp_and_snapshot_data():
    result=verify_bundle()
    assert result['native_topology_and_values_match'] and not result['audible_verified']
    assert set(result['ignored_generated_identity_fields'])=={'groupGUIDs','effectGUIDs','snapshotGUIDs'}


def test_explicit_template_parameters_repeat_from_original_source():
    expected=json.loads((ROOT/'manifests/native-audio-templates.json').read_text(encoding='utf-8'))
    assert refresh(RuntimeExport())==expected['templates']


def test_explicit_templates_keep_actual_component_and_mixer_identity():
    templates=json.loads((ROOT/'manifests/native-audio-templates.json').read_text(encoding='utf-8'))['templates']
    assert len(templates)==2
    ex=RuntimeExport()
    for row in templates:
        param=row['parameter'];assert param['asset_type']=='AudioSource' and not param['unresolved']
        data=json.loads((ex.folder/param['asset_path']).read_text(encoding='utf-8'));source=data['source']
        cab=ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower())
        assert cab.objects[int(source['pathid'])].read_typetree()==data['values']
        assert data['mixer']=='LegendaryDucking' and data['group']=='L_SFX'
        assert str(data['values']['OutputAudioMixerGroup']['m_PathID'])==data['group_source']['pathid']
