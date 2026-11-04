"""V005-r1: fresh read-only checks of original assets against private caches."""
import hashlib
import json
import struct
from pathlib import Path

from UnityPy.helpers.MeshHelper import MeshHandler
from tools.importer import Importer, PROJECT
from tools.inspect_compiled_shaders import programs, disassemble
from UnityPy.helpers import CompressionHelper


def main():
    imp = Importer()
    runtime = PROJECT / 'local/runtime'
    catalog = json.loads((runtime / 'fireblast-ui.json').read_text(encoding='utf-8'))
    result = {'checks': [], 'bundles': {}, 'official_fidelity_verified': False}

    def check(value, label):
        assert value, label
        result['checks'].append(label)

    def source(ref):
        ref = ref.get('source', ref)
        bundle = ref.get('bundle') or imp.reader.cabs[ref['cab']]
        obj = imp.reader.load(bundle).get_cab(ref['cab'].lower()).objects[int(ref['pathid'])]
        path = imp.reader.source_paths()[bundle]
        result['bundles'][bundle] = {'file': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        return obj

    card = imp.reader.definition('HERO_08bp')
    root, _, _ = imp.reader.resolve(card['m_PlayEffectDef']['m_SpellPath'])
    current = json.loads((runtime / 'native-fireblast.json').read_text(encoding='utf-8'))
    config = current['programs'][0]
    check(card['m_PlayEffectDef']['m_SpellPath'] == config['reference'], 'CardDef resolves actual Super root reference')
    root_raw = []
    for obj, tree in imp.reader.walk(root, include_visual=False):
        if obj.type.name != 'MonoBehaviour':
            continue
        tree = tree or obj.read_typetree()
        script = imp.reader.pointer(obj, tree['m_Script'])
        if script and script.read_typetree()['m_ClassName'] == 'SuperSpell':
            root_raw.append(tree)
            check(obj.path_id == int(config['source']['pathid']), 'actual SuperSpell component identity')
    check(len(root_raw) == 1, 'one original SuperSpell component')
    check(any(v == config['missile'] for v in root_raw[0].values()), 'full original missile fields match dedicated cache')
    check(config['missile']['m_PathDurationMin'] == config['missile']['m_PathDurationMax'] == .25, 'original fixed quarter-second flight')

    for entry in catalog['roots'] + catalog['arrows']:
        go = source(entry['source'])
        check(go.type.name == 'GameObject', entry.get('id', entry['reference']) + ': original GameObject')
        graph = json.loads((runtime / entry['file']).read_text(encoding='utf-8'))
        check(graph['source']['pathid'] == str(go.path_id), 'cache graph identity ' + entry['file'])
        for node in graph['nodes']:
            for material in node['materials']:
                owner = source(material['source'])
                raw = owner.read_typetree()
                saved_colors = dict(raw['m_SavedProperties']['m_Colors'])
                for prop in material.get('colors', []):
                    if prop['name'] in saved_colors:
                        check(prop['value'] == saved_colors[prop['name']], 'raw material color ' + material['name'] + ':' + prop['name'])
        for text in entry.get('texts', []):
            check(source(text['source']).read_typetree() == text['raw'], 'original text fields ' + entry['file'] + ':' + text['name'])
        if entry.get('highlight'):
            highlight = entry['highlight']
            raw = source(highlight['source']).read_typetree()
            check(len(raw['m_HighlightStates']) == len(highlight['states']) == 11, 'all eleven actual ActorState bindings')
            plane = imp.reader.pointer(source(highlight['source']), raw['m_RenderPlane'])
            components = [imp.reader.pointer(plane, c['component']) for c in plane.read_typetree()['m_Component']]
            mesh_filter = next(c for c in components if c and c.type.name == 'MeshFilter')
            mesh = imp.reader.pointer(mesh_filter, mesh_filter.read_typetree()['m_Mesh'])
            data = mesh.read()
            handler = MeshHandler(data)
            handler.process()
            check(data.m_VertexData.m_Channels[3].format == 2, 'actual highlight COLOR source UNorm8')
            plane_node = next(n for n in graph['nodes'] if n['name'] == 'Highlight_Card_Plane')
            cache = json.loads((runtime / plane_node['mesh']).read_text(encoding='utf-8'))
            expected = [v / 255 for row in handler.m_Colors for v in row[:4]]
            check(cache['colors'] == expected and max(expected) == 1, 'raw byte colors independently normalized and matched')
    for font in catalog['fonts']:
        check(source(font['definition']).type.name == 'MonoBehaviour', 'original font definition ' + font['file'])
        payload = (runtime / font['file']).read_bytes()
        original = source(font['source']).read_typetree()['m_FontData']
        check(payload == bytes(original), 'exported TTF bytes equal original embedded font ' + font['file'])
        result.setdefault('font_files', []).append({'file': font['file'], 'sha256': hashlib.sha256(payload).hexdigest(), 'size': len(payload)})
    for prefab in current['prefabs']:
        original, _, _ = imp.reader.resolve(prefab['reference'])
        check(original.path_id == source(prefab['source']).path_id, 'independent child Prefab identity ' + prefab['reference'])
        graph = json.loads((runtime / prefab['file']).read_text(encoding='utf-8'))
        for node in graph['nodes']:
            for material in node['materials']:
                raw = source(material['source']).read_typetree()['m_SavedProperties']
                for kind, field in [('floats', 'm_Floats'), ('colors', 'm_Colors')]:
                    values = dict(raw[field])
                    for item in material[kind]:
                        check(values[item['name']] == item['value'], 'child material raw ' + material['name'] + ':' + item['name'])
        audio = json.loads((runtime / prefab['audio_file']).read_text(encoding='utf-8'))
        for node in audio['nodes']:
            for audio_source in node['audio_sources']:
                check(source(audio_source['source']).read_typetree() == audio_source['values'], 'original AudioSource fields ' + node['path'])
        for clip in audio['clips']:
            original_clip = source(clip['source']).read_typetree()
            check(original_clip['m_Name'] in clip['reference'], 'original audio clip identity ' + clip['reference'])
    audits = sorted((PROJECT / 'work/r005-indicator').glob('*/audit.json'))
    audits.append(PROJECT / 'local/compiled-shader-audit/Custom_Selection_Highlight--1327719733717699995/audit.json')
    shader_folder = PROJECT / 'work/v005-r1-source-shaders'
    shader_folder.mkdir(exist_ok=True)
    for path in audits:
        audit = json.loads(path.read_text(encoding='utf-8'))
        obj = source(audit)
        shader = obj.read()
        parsed = obj.read_typetree()['m_ParsedForm']
        check(parsed['m_Name'] == audit['name'], 'original Shader identity ' + audit['name'])
        decoded = None
        for record in audit['programs']:
            if record['platform'] != 4:
                continue
            blob = record.get('blob', record.get('index'))
            if record.get('hash_scope') == 'exact_DXBC':
                platform_index = list(shader.platforms).index(4)
                def first(values):
                    value = values[platform_index]
                    return value[0] if isinstance(value, list) else value
                offset, length, size = first(shader.offsets), first(shader.compressedLengths), first(shader.decompressedLengths)
                raw = CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[offset:offset + length], size)
                begin = record['decompressed_segment_offset'] + record['dxbc_offset_in_segment']
                code = raw[begin:begin + record['bytes']]
                check(code[:4] == b'DXBC' and struct.unpack_from('<I', code, 24)[0] == len(code), 'fresh bounded DXBC ' + audit['name'] + ':' + str(blob))
            else:
                if decoded is None:
                    decoded = {platform: program for platform, program in programs(shader)}
                code = bytes(decoded[4].m_SubPrograms[blob].m_ProgramCode)
            check(hashlib.sha256(code).hexdigest() == record['sha256'], 'fresh Shader code hash ' + audit['name'] + ':' + str(blob))
            text = disassemble(code)
            check(text is not None, 'fresh static disassembly ' + audit['name'] + ':' + str(blob))
            (shader_folder / (path.parent.name + '-' + str(blob) + '.asm.txt')).write_text(text, encoding='utf-8')
        result.setdefault('shader_states', []).append({'name': audit['name'], 'passes': [p['m_State'] for s in parsed['m_SubShaders'] for p in s['m_Passes']]})
    output = PROJECT / 'work/v005-r1-source.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('V005_R1_SOURCE', len(result['checks']), 'checks;', len(result['bundles']), 'fresh original bundle hashes;', output)


if __name__ == '__main__':
    main()
