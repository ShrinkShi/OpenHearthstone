import hashlib
import json
from pathlib import Path

from tools.additive_shader_audit import verify_sources
from tools.holy_shader_audit import FOLDER, holy_audit

ROOT = Path(__file__).parents[1]


def test_holy_shader_replays_original_program_and_source_bindings():
    manifest = json.loads((ROOT / 'manifests/native-holy-shader.json').read_text(encoding='utf-8'))
    assert holy_audit() == manifest
    assert len(manifest['references']) == 27
    assert manifest['family'] == 20
    assert not manifest['full_card_effects_verified']
    source = json.loads((ROOT / 'manifests/native-holy-source-bindings.json').read_text(encoding='utf-8'))
    assert verify_sources(manifest) == source
    assert len(source['bindings']) == 8
    folder = ROOT / 'local/compiled-shader-audit' / FOLDER
    for binding in manifest['bindings']:
        assert hashlib.sha256((folder / f"4-{binding['blob']}.bin").read_bytes()).hexdigest() == binding['program_sha256']
        assert hashlib.sha256((folder / f"4-{binding['parameter_blob']}.parameters.bin").read_bytes()).hexdigest() == binding['parameter_sha256']
    layout = manifest['pass_constant_byte_offsets'][0]['progFragment']['UnityPerMaterial']
    assert layout == {'_Color': 16, '_Color2': 32, '_Intensity': 48, '_Alpha': 56}
    fragment = (folder / '4-3.asm.txt').read_text(encoding='utf-8')
    assert 'add r0.x, r0.x, r0.x' in fragment
    assert 'mul r0.w, r0.x, cb1[3].z' in fragment
    assert 'mul o0.xyzw, r0.xyzw, cb1[3].zzzz' in fragment
    assert '_sat' not in fragment
