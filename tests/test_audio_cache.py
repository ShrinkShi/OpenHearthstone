import io
import wave

import pytest

from tools.audio_cache import publish_audio, valid_audio_file, validate_audio


def pcm_wav():
    stream = io.BytesIO()
    with wave.open(stream, 'wb') as clip:
        clip.setparams((2, 2, 48000, 0, 'NONE', 'not compressed'))
        clip.writeframes(b'\x01\x00\x02\x00' * 12)
    return stream.getvalue()


def test_reject_zero_buffer_and_preserve_it_when_repairing(tmp_path):
    path = tmp_path / 'clip.wav'
    previous = bytes(202052)
    path.write_bytes(previous)
    assert not valid_audio_file(path)
    with pytest.raises(wave.Error):
        publish_audio(path, previous)
    assert path.read_bytes() == previous
    publish_audio(path, pcm_wav())
    assert valid_audio_file(path)
    assert list((tmp_path / 'invalid-audio').iterdir())[0].read_bytes() == previous
    assert not list(tmp_path.glob('*.tmp'))


def test_reject_valid_header_with_truncated_samples():
    with pytest.raises(ValueError, match='truncated'):
        validate_audio(pcm_wav()[:-4], '.wav')


def test_idempotent_publish_does_not_rewrite_valid_audio(tmp_path):
    path = tmp_path / 'clip.wav'
    data = pcm_wav()
    publish_audio(path, data)
    stamp = path.stat().st_mtime_ns
    publish_audio(path, data)
    assert path.stat().st_mtime_ns == stamp
    assert not (tmp_path / 'invalid-audio').exists()
