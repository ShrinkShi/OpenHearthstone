"""Validate decoded audio before publishing it; retain invalid derived files."""
import hashlib
import io
import os
import wave


def validate_audio(data, suffix):
    if suffix.lower() == '.wav':
        with wave.open(io.BytesIO(data)) as clip:
            frames = clip.getnframes()
            expected = frames * clip.getnchannels() * clip.getsampwidth()
            if frames <= 0 or len(clip.readframes(frames)) != expected:
                raise ValueError('Decoded WAV has empty or truncated samples')
    elif suffix.lower() == '.ogg':
        if not data.startswith(b'OggS'):
            raise ValueError('Decoded OGG has no container header')
    elif suffix.lower() == '.m4a':
        if data[4:8] != b'ftyp':
            raise ValueError('Decoded M4A has no container header')
    else:
        raise ValueError('Unsupported decoded audio extension: ' + suffix)


def valid_audio_file(path):
    try:
        validate_audio(path.read_bytes(), path.suffix)
        return True
    except (OSError, EOFError, ValueError, wave.Error):
        return False


def publish_audio(path, data):
    validate_audio(data, path.suffix)
    if path.exists():
        previous = path.read_bytes()
        if previous == data:
            return
        if not valid_audio_file(path):
            archive = path.parent / 'invalid-audio'
            archive.mkdir(exist_ok=True)
            backup = archive / (path.name + '.' + hashlib.sha256(previous).hexdigest()[:12] + '.bin')
            if not backup.exists():
                backup.write_bytes(previous)
    temporary = path.with_name(path.name + '.' + str(os.getpid()) + '.tmp')
    temporary.write_bytes(data)
    temporary.replace(path)
