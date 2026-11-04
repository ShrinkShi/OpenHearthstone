import json
from pathlib import Path

from tools.export_runtime import RuntimeExport
from tools.native_audio import recover

ROOT = Path(__file__).parents[1]


def test_original_animation_audio_has_exact_existing_sources_and_primary_attack_sound():
    catalog = json.loads((ROOT / 'manifests/original-animation-event-audio.json').read_text(encoding='utf-8'))
    assert catalog['full_sound_manager_restored'] is False
    assert len(catalog['programs']) == 8
    assert sum(len(p['nodes']) for p in catalog['programs']) == 5
    for record in catalog['programs']:
        assert not record['errors']
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        audio = json.loads((ROOT / 'local/runtime' / record['audio_file']).read_text(encoding='utf-8'))
        assert program['event_audio_file'] == record['audio_file']
        assert audio['nodes'] == record['nodes'] and audio['clips'] == record['clips']
        for node in audio['nodes']:
            assert len(node['audio_sources']) == len(node['sound_defs']) == 1
            assert node['audio_sources'][0]['values']['m_audioClip']['m_PathID'] == 0
            assert node['audio_sources'][0]['values']['OutputAudioMixerGroup']['m_PathID'] == 0
            assert node['sound_defs'][0]['values']['m_InstanceLimited'] == 0
        for clip in audio['clips']:
            assert clip['reference'] in {
                node['sound_defs'][0]['values']['m_AudioClip'] for node in audio['nodes']
            }
            if any(node['path'] == 'ACTION_STATE' for node in audio['nodes']):
                assert clip['reference'] == 'FX_Minion_AttackLaunch.wav:84aa8fcefb2bd49459fc5649299b8d17'
                assert clip['file'] == '2670b50038652a0dec74cfb8.wav'
            else:
                assert clip['reference'] == 'Ice_FrozenMinion_Impact_Sound.wav:8881eb7860e3f43b09b5c384cb3f5250'
            assert (ROOT / 'local/runtime' / clip['file']).exists()


def test_animation_audio_source_recovery_matches_original_install():
    catalog = json.loads((ROOT / 'manifests/original-animation-event-audio.json').read_text(encoding='utf-8'))
    exporter = RuntimeExport()
    for record in catalog['programs']:
        program = json.loads((ROOT / 'local/runtime' / record['file']).read_text(encoding='utf-8'))
        cached = json.loads((ROOT / 'local/runtime' / record['audio_file']).read_text(encoding='utf-8'))
        assert recover(exporter, program['reference']) == cached
