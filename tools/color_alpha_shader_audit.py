"""Trace the original luminance-mask particle program without executing it."""
from .additive_shader_audit import audit, verify_sources
from .asset_lock import atomic_json
from .importer import PROJECT

FOLDER = 'Hero_Particle_Particles_Transparent_ColorAsAlpha-9166864898872631166'


def main():
    result = audit(FOLDER)
    result['family'] = 18
    result['notes'] = [
        'Base vertex: vertex*TintColor*Intensity with SH gamma and no main-light addition.',
        'Base RGB is twice interpolated vertex RGB, independent of texture color.',
        'Alpha: saturate(dot(texture.rgb, (.3,.59,.11))*AlphaIntensity)*interpolated alpha.',
        'Texture alpha is ignored by base; Glow retains its separate original RGBA sampling.',
        'Original binaries are not executed; full card effects remain unverified.']
    sources = verify_sources(result)
    atomic_json(PROJECT / 'manifests/native-color-alpha-variants.json', result)
    atomic_json(PROJECT / 'manifests/native-color-alpha-source-bindings.json', sources)
    print('ORIGINAL COLOR ALPHA:', len(result['references']), 'programs;',
          sum(len(r['materials']) for r in result['references']), 'references;',
          len(sources['bindings']), 'unique materials')


if __name__ == '__main__':
    main()
