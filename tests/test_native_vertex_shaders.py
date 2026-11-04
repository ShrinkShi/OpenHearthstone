import hashlib
import json
from pathlib import Path

import pytest

from tools.additive_shader_audit import verify_sources
from tools.vertex_shader_audit import FAMILIES, vertex_audit

ROOT = Path(__file__).parents[1]


@pytest.mark.parametrize('index', [0, 1])
def test_vertex_shader_original_bytes_bindings_and_render_commands(index):
    manifest = json.loads((ROOT / 'manifests/native-vertex-shaders.json').read_text(encoding='utf-8'))[index]
    source = json.loads((ROOT / 'manifests/native-vertex-source-bindings.json').read_text(encoding='utf-8'))[index]
    folder, family = FAMILIES[index]
    assert vertex_audit(folder, family) == manifest
    assert verify_sources(manifest) == source
    assert not manifest['full_card_effects_verified']
    for binding in manifest['bindings']:
        cache = ROOT / 'local/compiled-shader-audit' / folder
        assert hashlib.sha256((cache / f"4-{binding['blob']}.bin").read_bytes()).hexdigest() == binding['program_sha256']
        assert hashlib.sha256((cache / f"4-{binding['parameter_blob']}.parameters.bin").read_bytes()).hexdigest() == binding['parameter_sha256']
    state = manifest['pass_render_states'][0]
    assert state['zWrite'] == (1 if family == 21 else 0)
    assert state['culling'] == (2 if family == 21 else 0)
    assert state['zTest'] == 4
    assert (state['blend']['srcBlend'], state['blend']['destBlend']) == ((0, 3) if family == 21 else (5, 1))
    fragment = (ROOT / 'local/compiled-shader-audit' / folder / '4-3.asm.txt').read_text(encoding='utf-8')
    assert 'mov o0.xyzw, v1.xyzw' in fragment
    assert 'sample' not in fragment and '_sat' not in fragment
