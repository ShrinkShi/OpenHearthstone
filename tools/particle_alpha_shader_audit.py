"""Trace original two-layer particle alpha-noise stages and source materials."""
from .additive_shader_audit import audit, verify_sources
from .asset_lock import atomic_json
from .importer import PROJECT

FOLDER = 'Hero_Particle_FX_Particle_Additive_Alpha_2Layer--1238988257466644464'
SMOKE_FOLDER = 'Custom_FX_FX_SneakySmoke--1607209025186432915'


def particle_alpha_audit():
    result = audit(FOLDER)
    result['family'] = 23
    result['notes'] = [
        'Vertex: MainTex ST and original SH + MainLight lighting affect interpolated vertex RGB; vertex alpha is unchanged.',
        'Fragment: noiseUV=(mainUV+vertex.r-vertex.a*float2(USpeed,VSpeed))*Scale.',
        'RGB=Color.rgb; A=AlphaNoise.r*MainTex.a*AlphaIntensity*vertex.a. No saturation or clock.',
        'AlphaNoise ST, tint alpha, main RGB, noise GBA and extra serialized properties are unused.',
        'Source parameter blobs, two samplers and SrcAlpha/One RGBA blend are retained as provenance.',
        'Whole card playback, original scene lighting and all shader/script services remain incomplete.']
    return result


def sneaky_smoke_audit():
    result = audit(SMOKE_FOLDER)
    result['family'] = 24
    result['notes'] = [
        'Vertex: MainTex ST, vertex RGB passed unused, vertex alpha multiplied by Color.a; no lighting.',
        'Fragment mask=MainTex.r*vertex.a*Color.a; RGB=lerp(Color.rgb,SecondColor.rgb,mask); RGBA multiplied by Intensity.',
        'Texture GBA, vertex RGB and SecondColor.a are unused. No saturation or clock.',
        'Source RGBA SrcAlpha/OneMinusSrcAlpha blend is distinct from premultiplied affine capture.',
        'Whole card playback, scene capture and complete script services remain incomplete.']
    return result


if __name__ == '__main__':
    result = particle_alpha_audit()
    sources = verify_sources(result)
    atomic_json(PROJECT / 'manifests/native-particle-alpha-shader.json', result)
    atomic_json(PROJECT / 'manifests/native-particle-alpha-source-bindings.json', sources)
    print('ORIGINAL PARTICLE ALPHA', len(result['references']), 'effect programs;',
        len(sources['bindings']), 'unique material bindings')
    result = sneaky_smoke_audit()
    sources = verify_sources(result)
    atomic_json(PROJECT / 'manifests/native-sneaky-smoke-shader.json', result)
    atomic_json(PROJECT / 'manifests/native-sneaky-smoke-source-bindings.json', sources)
    print('ORIGINAL SNEAKY SMOKE', len(result['references']), 'effect programs;',
        len(sources['bindings']), 'unique material bindings')
