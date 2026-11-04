import hashlib
import json
from pathlib import Path

from tools.additive_shader_audit import verify_sources
from tools.particle_alpha_shader_audit import FOLDER, SMOKE_FOLDER, particle_alpha_audit, sneaky_smoke_audit

ROOT = Path(__file__).parents[1]


def test_particle_alpha_original_programs_and_source_materials():
    manifest = json.loads((ROOT / 'manifests/native-particle-alpha-shader.json').read_text(encoding='utf-8'))
    sources = json.loads((ROOT / 'manifests/native-particle-alpha-source-bindings.json').read_text(encoding='utf-8'))
    assert particle_alpha_audit() == manifest
    assert verify_sources(manifest) == sources
    assert manifest['family'] == 23 and len(manifest['references']) == 35
    assert len(sources['bindings']) == 8 and not manifest['full_card_effects_verified']
    for binding in manifest['bindings']:
        cache = ROOT / 'local/compiled-shader-audit' / FOLDER
        assert hashlib.sha256((cache / f"4-{binding['blob']}.bin").read_bytes()).hexdigest() == binding['program_sha256']
        assert hashlib.sha256((cache / f"4-{binding['parameter_blob']}.parameters.bin").read_bytes()).hexdigest() == binding['parameter_sha256']


def test_particle_alpha_source_samplers_constants_and_commands():
    manifest = particle_alpha_audit()
    layout = manifest['pass_constant_byte_offsets'][0]
    assert layout['progVertex']['UnityPerMaterial'] == {'_MainTex_ST': 0, '_LightingBlend': 48}
    assert layout['progFragment']['UnityPerMaterial'] == {'_Color': 16, '_USpeed': 32, '_VSpeed': 36, '_Scale': 40, '_AlphaIntensity': 44}
    parsed = json.loads((ROOT / 'local/compiled-shader-audit' / FOLDER / 'parsed-form.json').read_text(encoding='utf-8'))
    shader_pass = parsed['m_SubShaders'][0]['m_Passes'][0]
    names = {index: name for name, index in shader_pass['m_NameIndices']}
    assert [(names[t['m_NameIndex']], t['m_Index'], t['m_SamplerIndex']) for t in shader_pass['progFragment']['m_CommonParameters']['m_TextureParams']] == [('_MainTex', 0, 0), ('_AlphaNoise', 1, 1)]
    state = manifest['pass_render_states'][0]
    assert (state['culling'], state['zWrite'], state['zTest']) == (0, 0, 4)
    assert (state['blend']['srcBlend'], state['blend']['destBlend'], state['blend']['srcBlendAlpha'], state['blend']['destBlendAlpha']) == (5, 1, 5, 1)
    fragment = (ROOT / 'local/compiled-shader-audit' / FOLDER / '4-3.asm.txt').read_text(encoding='utf-8')
    assert 'add r0.xy, v1.xyxx, v2.xxxx' in fragment
    assert 'mad r0.xy, -v2.wwww, cb1[2].xyxx, r0.xyxx' in fragment
    assert 'mul r0.xy, r0.xyxx, cb1[2].zzzz' in fragment
    assert 'mov o0.xyz, cb1[1].xyzx' in fragment
    assert 'mul o0.w, r0.x, v2.w' in fragment and '_sat' not in fragment


def test_sneaky_smoke_source_color_mask_and_blend():
    manifest = json.loads((ROOT / 'manifests/native-sneaky-smoke-shader.json').read_text(encoding='utf-8'))
    sources = json.loads((ROOT / 'manifests/native-sneaky-smoke-source-bindings.json').read_text(encoding='utf-8'))
    assert sneaky_smoke_audit() == manifest and verify_sources(manifest) == sources
    assert manifest['family'] == 24 and len(manifest['references']) == 5 and len(sources['bindings']) == 2
    assert not manifest['full_card_effects_verified']
    for binding in manifest['bindings']:
        folder = ROOT / 'local/compiled-shader-audit' / SMOKE_FOLDER
        assert hashlib.sha256((folder / f"4-{binding['blob']}.bin").read_bytes()).hexdigest() == binding['program_sha256']
        assert hashlib.sha256((folder / f"4-{binding['parameter_blob']}.parameters.bin").read_bytes()).hexdigest() == binding['parameter_sha256']
    assert manifest['pass_constant_byte_offsets'][0]['progVertex']['UnityPerMaterial'] == {'_MainTex_ST': 0, '_Color': 16}
    assert manifest['pass_constant_byte_offsets'][0]['progFragment']['UnityPerMaterial'] == {'_Color': 16, '_SecondColor': 32, '_Intensity': 48}
    state = manifest['pass_render_states'][0]
    assert (state['blend']['srcBlend'], state['blend']['destBlend'], state['blend']['srcBlendAlpha'], state['blend']['destBlendAlpha']) == (5, 10, 5, 10)
    fragment = (ROOT / 'local/compiled-shader-audit' / SMOKE_FOLDER / '4-3.asm.txt').read_text(encoding='utf-8')
    assert 'mul r1.w, r1.x, v1.w' in fragment and 'mad r1.xyz, r1.wwww, r0.xyzx, cb1[1].xyzx' in fragment
    assert 'mul o0.xyzw, r1.xyzw, cb1[3].xxxx' in fragment and '_sat' not in fragment


def test_smoke_texture_raw_mips_and_sampler_match_original_assets():
    from tools.importer import Importer
    importer = Importer()
    report = json.loads((ROOT / 'manifests/native-smoke-source-textures.json').read_text(encoding='utf-8'))
    assert len(report['textures']) == 8 and len(report['bindings']) == 17
    assert not report['whole_effects_verified']
    assert json.loads((ROOT / 'local/runtime/native-smoke-source-textures.json').read_text(encoding='utf-8')) == report
    for row in report['textures']:
        source = row['source']
        obj = importer.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
        data = obj.read()
        tree = obj.read_typetree()
        raw = (ROOT / 'local/runtime' / row['raw']).read_bytes()
        assert raw == data.get_image_data() and hashlib.sha256(raw).hexdigest() == row['sha256']
        assert (row['width'], row['height'], row['format'], row['mips'], row['color_space']) == (data.m_Width, data.m_Height, data.m_TextureFormat, data.m_MipCount, tree['m_ColorSpace'])
        sampler = tree['m_TextureSettings']
        assert (row['filter'], row['aniso'], row['mip_bias'], row['wrap_u'], row['wrap_v'], row['wrap_w']) == (sampler['m_FilterMode'], sampler['m_Aniso'], sampler['m_MipBias'], sampler['m_WrapU'], sampler['m_WrapV'], sampler['m_WrapW'])
    assert any(row['mips'] == 7 for row in report['textures'])
