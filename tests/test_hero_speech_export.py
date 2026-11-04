import json
import sqlite3
import wave
from pathlib import Path
from types import SimpleNamespace

from PIL import Image, ImageDraw

from tools.export_hero_speech import (
    EMOTE_TYPES, export_hero_speech, metadata_only_manifest, read_string_table, save_tracked_manifest,
)


class FakeAudioObject:
    type = SimpleNamespace(name='AudioClip')

    def __init__(self, index):
        self.assets_file = SimpleNamespace(name='CAB-SPEECH')
        self.path_id = index

    def peek_name(self):
        return f'VO_HERO_08_{self.path_id}'


class FakeReader:
    def __init__(self):
        self.objects = {}
        self.card = {'m_EmoteDefs': []}
        for index, (emote_id, emote_type) in enumerate(EMOTE_TYPES.items(), 1):
            ref = f'Emote_{emote_id}.prefab:{index:032x}'
            self.objects[ref] = FakeAudioObject(index)
            self.card['m_EmoteDefs'].append({
                'm_emoteType': emote_type,
                'm_emoteSoundSpellPath': ref,
                'm_emoteGameStringKey': f'GAMEPLAY_EMOTE_HERO_08_{emote_id}',
                'm_emoteSpellPath': '',
            })

    def definition(self, card_id):
        assert card_id == 'HERO_08'
        return self.card

    def resolve(self, reference, locale):
        return reference, 'source-bundle', locale

    def walk(self, root, **kwargs):
        yield self.objects[root], None, None


def make_exporter(tmp_path):
    game = tmp_path / 'game'
    strings = game / 'Strings' / 'zhCN'
    strings.mkdir(parents=True)
    audio_rows = ['TAG\tTEXT\tCOMMENT']
    audio_rows.extend(f'GAMEPLAY_EMOTE_HERO_08_{emote}\t台词 {emote}\t'
                      for emote in EMOTE_TYPES if emote != 'SORRY')
    (strings / 'GAMEPLAY_AUDIO.txt').write_text('\n'.join(audio_rows) + '\n', encoding='utf-8')
    labels = ['TAG\tTEXT\tCOMMENT']
    labels.extend(f'GAMEPLAY_EMOTE_LABEL_{emote}\t按钮 {emote}\t'
                  for emote in ('THANKS', 'WELL_PLAYED', 'GREETINGS', 'WOW', 'OOPS', 'THREATEN', 'SORRY'))
    (strings / 'GAMEPLAY.txt').write_text('\n'.join(labels) + '\n', encoding='utf-8')

    reader = FakeReader()
    db = sqlite3.connect(':memory:')
    db.row_factory = sqlite3.Row
    db.execute('CREATE TABLE assets(id,bundle,cab,pathid,type,name,metadata,locale)')
    for index in range(1, len(EMOTE_TYPES) + 1):
        db.execute('INSERT INTO assets VALUES (?,?,?,?,?,?,?,?)',
                   (f'audio-{index}', 'source-bundle', 'CAB-SPEECH', str(index), 'AudioClip',
                    f'VO_HERO_08_{index}', json.dumps({'m_Length': 1.25}), 'zhcn'))
    db.execute('INSERT INTO assets VALUES (?,?,?,?,?,?,?,?)',
               ('bubble', 'bubble-bundle', 'CAB-BUBBLE', '99', 'Texture2D', 'Emote_Bubbles',
                json.dumps({'m_Width': 512, 'm_Height': 512}), 'global'))
    db.commit()
    audio_path = tmp_path / 'source.wav'
    with wave.open(str(audio_path), 'wb') as clip:
        clip.setnchannels(1)
        clip.setsampwidth(2)
        clip.setframerate(8000)
        clip.writeframes(b'\0\0' * 80)

    class FakeExport:
        folder = tmp_path / 'runtime'

        def __init__(self):
            self.folder.mkdir()
            self.registry = {}
            self.errors = []
            self.imp = SimpleNamespace(game=game, reader=reader,
                                       store=SimpleNamespace(db=db),
                                       export=lambda asset_id: audio_path)

        def key(self, obj):
            return str(obj.path_id)

        def named(self, logical_id, name, kind='Texture2D'):
            assert name == 'Emote_Bubbles' and kind == 'Texture2D'
            self.registry[logical_id] = 'emote-bubbles.png'
            image = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
            draw = ImageDraw.Draw(image)
            draw.rounded_rectangle((35, 20, 470, 180), radius=30, fill=(240, 230, 210, 255))
            draw.polygon([(350, 135), (420, 209), (435, 172)], fill=(240, 230, 210, 255))
            draw.rounded_rectangle((40, 270, 470, 430), radius=30, fill=(240, 230, 210, 255))
            draw.polygon([(155, 398), (121, 455), (185, 430)], fill=(240, 230, 210, 255))
            image.save(self.folder / 'emote-bubbles.png')

    return FakeExport()


def test_original_tsv_strings_require_tag_text_columns(tmp_path):
    path = tmp_path / 'GAMEPLAY_AUDIO.txt'
    path.write_text('TAG\tTEXT\tCOMMENT\nVOICE_TEST\t第一行\\n第二行\t\n', encoding='utf-8')
    assert read_string_table(path) == {'VOICE_TEST': '第一行\n第二行'}

    path.write_text('key\tvalue\nVOICE_TEST\t不接受\n', encoding='utf-8')
    try:
        read_string_table(path)
    except ValueError as error:
        assert 'TAG/TEXT' in str(error)
    else:
        raise AssertionError('non-source localization table was accepted')


def test_export_preserves_wow_and_legacy_sorry_sources_without_filling_missing_text(tmp_path):
    ex = make_exporter(tmp_path)
    manifest = export_hero_speech(ex, 'zhcn', ['HERO_08'])
    entries = {entry['emote_id']: entry for entry in manifest['entries']}

    assert set(entries) == set(EMOTE_TYPES)
    assert EMOTE_TYPES['WOW'] != EMOTE_TYPES['SORRY']
    assert entries['WOW']['text'] == '台词 WOW'
    assert entries['SORRY']['text'] == ''
    assert entries['WOW']['sources'][0]['emote_type'] == 33
    assert entries['SORRY']['sources'][0]['emote_type'] == 6
    assert entries['SORRY']['sources'][0]['bubble_spell_ref'] == ''
    assert len(entries['THANKS']['candidates']) == 1
    assert entries['THANKS']['candidates'][0]['bundle'] == 'source-bundle'
    assert entries['THANKS']['candidates'][0]['locale'] == 'zhcn'
    assert manifest['bubble_art']['asset_name'] == 'Emote_Bubbles'
    assert manifest['bubble_art']['status'] == 'original_texture_atlas'
    assert manifest['bubble_art']['regions'][0]['tail_tip_px'] == [420, 209]
    assert manifest['bubble_art']['regions'][1]['tail_tip_px'] == [121, 455]
    assert manifest['bubble_art']['regions'][0]['text_rect_px'] == [90, 40, 340, 98]
    assert manifest['bubble_art']['regions'][1]['text_rect_px'] == [88, 282, 336, 80]
    assert len(manifest['bubble_art']['sha256']) == 64
    assert any(error.get('emote_id') == 'SORRY' and error.get('error') == 'localized subtitle missing'
               for error in manifest['errors'])
    assert 'D:' not in json.dumps(manifest)

    provenance = metadata_only_manifest(manifest)
    assert provenance['candidate_count'] == 9
    assert all('text' not in entry and 'label' not in entry for entry in provenance['entries'])
    assert '台词 WOW' not in json.dumps(provenance, ensure_ascii=False)
    assert provenance['entries'][0]['sources'][0]['sound_ref'].startswith('Emote_')
    assert len(provenance['entries'][0]['candidates'][0]['sha256']) == 64


def test_tracked_manifest_saves_provenance_without_localized_text_or_audio(tmp_path, monkeypatch):
    ex = make_exporter(tmp_path)
    manifest = export_hero_speech(ex, 'zhcn', ['HERO_08'])
    path = tmp_path / 'manifests' / 'original-hero-speech.json'
    monkeypatch.setattr('tools.export_hero_speech.TRACKED_MANIFEST', path)

    save_tracked_manifest(manifest, tmp_path / 'cache')
    serialized = path.read_text(encoding='utf-8')
    provenance = json.loads(serialized)

    assert provenance['complete'] is False
    assert provenance['entry_count'] == 9
    assert all('text' not in entry and 'label' not in entry for entry in provenance['entries'])
    assert '台词 WOW' not in serialized
    assert str(tmp_path) not in serialized
    assert provenance['bubble_art']['regions'][0]['tail_tip_px'] == [420, 209]
    assert len(provenance['entries'][0]['candidates'][0]['sha256']) == 64
