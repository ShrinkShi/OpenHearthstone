"""Trace original CommonFX RGBR without ramp and its live blend properties."""
from .additive_shader_audit import audit, verify_sources
from .transparent_shader_audit import refresh
from .asset_lock import atomic_json
from .importer import PROJECT

KEYWORDS = ['_COLORING_NONE', '_DISSOLVE_NONE', '_SECONDTEXTURETYPE_RGBR',
            '_SECONDTEXTURE_NONE', '_TEXTURETYPE_RGBR']
FOLDER = 'FX_CommonFX--402334618889270515'


def main():
    result = audit(FOLDER, KEYWORDS)
    result['family'] = 19
    result['notes'] = [
        'Original Windows VS114/PS1202; red provides alpha, stored texture alpha ignored.',
        'Base RGB: (texture.rgb+ColorAdd)*max(texture.r,PremultipliedRGBTexture)*ColorMul*vertex.rgb.',
        'Vertex HDR transform retained; no ramp, second texture or ambient lighting.',
        'Source SrcBlendOutline/DstBlendOutline are property-backed, not Zero/Zero placeholders.',
        'No original binaries executed; full card effects remain unverified.']
    sources = verify_sources(result)
    changes = refresh('FX/CommonFX', KEYWORDS)
    atomic_json(PROJECT / 'manifests/native-commonfx-none-variants.json', result)
    atomic_json(PROJECT / 'manifests/native-commonfx-none-source-bindings.json', sources)
    atomic_json(PROJECT / 'manifests/native-commonfx-none-blend-refresh.json', changes)
    print('ORIGINAL COMMONFX NONE:', len(result['references']), 'programs;',
          len(changes['materials']), 'references;', changes['unique_materials'],
          'materials;', changes['updated'], 'updated')


if __name__ == '__main__':
    main()
