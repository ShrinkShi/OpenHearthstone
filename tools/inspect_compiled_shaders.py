"""Export original compiled Shader programs for local, read-only inspection."""
import argparse
import ctypes
import hashlib
import json
import re
import struct
from UnityPy.export.ShaderConverter import ShaderProgram, ShaderSubProgram, export_shader
from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
from .importer import Importer, PROJECT
from .asset_lock import atomic_json


def disassemble(code):
    start = code.find(b'DXBC')
    if start < 0:
        return None
    if len(code) - start < 32:
        raise ValueError('Truncated DXBC header')
    size = struct.unpack_from('<I', code, start + 24)[0]
    binary = code[start:start + size]
    if len(binary) != size:
        raise ValueError('Truncated DXBC program')
    library = ctypes.WinDLL('d3dcompiler_47.dll')
    function = library.D3DDisassemble
    function.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint,
                         ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
    function.restype = ctypes.c_long
    blob = ctypes.c_void_p()
    buffer = ctypes.create_string_buffer(binary)
    status = function(buffer, len(binary), 0, None, ctypes.byref(blob))
    if status < 0:
        raise RuntimeError('D3DDisassemble HRESULT ' + hex(status & 0xffffffff))
    table = ctypes.cast(blob, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    pointer = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(table[3])
    length = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(table[4])
    release = ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(table[2])
    try:
        return ctypes.string_at(pointer(blob), length(blob)).decode('utf-8').rstrip('\0')
    finally:
        release(blob)


def programs(shader):
    if shader.m_SubProgramBlob:
        yield 0, ShaderProgram(EndianBinaryReader(CompressionHelper.decompress_lz4(
            bytes(shader.m_SubProgramBlob), shader.decompressedSize)), shader.object_reader.version)
    elif shader.compressedBlob:
        def first(values, index):
            value = values[index]
            return value[0] if isinstance(value, list) else value
        for index, platform in enumerate(shader.platforms):
            if index >= len(shader.compressedLengths):
                break
            offset = first(shader.offsets, index)
            compressed = first(shader.compressedLengths, index)
            size = first(shader.decompressedLengths, index)
            raw = CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[offset:offset+compressed], size)
            yield platform, BoundedShaderProgram(raw, shader.object_reader.version, platform) if platform==4 else ShaderProgram(EndianBinaryReader(raw,endian='<'),shader.object_reader.version)


def segment_entries(raw, version):
    if len(raw)<4:raise ValueError('Truncated subprogram table')
    count=struct.unpack_from('<I',raw)[0];width=12 if version>=(2019,3) else 8
    end=4+count*width
    if end>len(raw):raise ValueError('Truncated subprogram entries')
    for index in range(count):
        offset,size=struct.unpack_from('<II',raw,4+index*width)
        segment=struct.unpack_from('<I',raw,12+index*width)[0] if width==12 else 0
        if segment:raise ValueError('Unrestored multi-segment subprogram')
        if offset<end or size<8 or offset+size>len(raw):raise ValueError('Invalid subprogram bounds')
        yield index,raw[offset:offset+size]


class BoundedShaderProgram:
    def __init__(self, raw, version, platform=4):
        self.m_SubPrograms=[];self.parameter_entries=[]
        for index,segment in segment_entries(raw,version):
            kind=struct.unpack_from('<I',segment,4)[0]
            # New player blobs put parameter records (types 3/4/5) in the same
            # table. Types 1/2 are empty/short fragment layouts (Black/Multiply).
            # Parsing any of these as code can cross the bounded entry.
            if platform==4 and struct.unpack_from('<I',segment)[0]>=202012090 and kind in (1,2,3,4,5):
                self.m_SubPrograms.append(None)
                self.parameter_entries.append((index,kind,segment));continue
            reader=EndianBinaryReader(segment,endian='<')
            sub=ShaderSubProgram(reader)
            if reader.Position>len(segment):raise ValueError('Subprogram reads outside entry')
            self.m_SubPrograms.append(sub)


def variant_bindings(parsed, keywords, platform_index=3):
    names=parsed.get('m_KeywordNames',[]);result=[]
    for subshader_index,subshader in enumerate(parsed.get('m_SubShaders',[])):
        for pass_index,shader_pass in enumerate(subshader.get('m_Passes',[])):
            for stage in ('progVertex','progFragment'):
                program=shader_pass.get(stage,{})
                groups=program.get('m_PlayerSubPrograms',[])
                if len(groups)<=platform_index:continue
                mask=program.get('m_SerializedKeywordStateMask',[])
                expected=set(keywords)&{names[index] for index in mask}
                parameters=program.get('m_ParameterBlobIndices',[])
                for index,entry in enumerate(groups[platform_index]):
                    actual={names[key] for key in entry.get('m_KeywordIndices',[])}
                    if actual!=expected:continue
                    result.append(dict(subshader=subshader_index,pass_index=pass_index,pass_name=shader_pass.get('m_State',{}).get('m_Name',''),
                        stage=stage,blob=entry['m_BlobIndex'],gpu_type=entry['m_GpuProgramType'],keywords=sorted(actual),
                        parameter_blob=parameters[platform_index][index] if len(parameters)>platform_index and index<len(parameters[platform_index]) else None))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--pattern', default='%R2TAlpha%')
    parser.add_argument('--card')
    parser.add_argument('--bundle')
    args = parser.parse_args()
    imp = Importer()
    rows = imp.store.db.execute('SELECT * FROM assets WHERE type=? AND name LIKE ?',
                               ('Shader', args.pattern)).fetchall()
    folder = PROJECT / 'local/compiled-shader-audit'
    folder.mkdir(exist_ok=True)
    objects = []
    for row in rows:
        env = imp.reader.load(row['bundle'])
        obj = next((o for o in env.objects if str(o.path_id) == row['pathid']
                    and o.assets_file.name == row['cab']), None)
        if obj is None:
            raise LookupError(row['id'])
        objects.append((obj, dict(row)))
    if args.card:
        audit = json.loads((PROJECT / 'manifests/native-effect-audit.json').read_text(encoding='utf-8'))
        ref = next(r['reference'] for r in audit['cards'] if r['key'] == args.card)
        root, _, _ = imp.reader.resolve(ref)
        for obj, tree in imp.reader.walk(root, include_visual=True, max_nodes=4000):
            if obj.type.name != 'Material':
                continue
            shader = imp.reader.pointer(obj, (tree or obj.read_typetree()).get('m_Shader', {}))
            if shader:
                objects.append((shader, dict(id='', bundle=imp.reader.file_bundle.get(shader.assets_file.name.lower(), ''),
                                            pathid=str(shader.path_id), cab=shader.assets_file.name,
                                            name=shader.read_typetree()['m_ParsedForm']['m_Name'])))
    if args.bundle:
        for obj in imp.reader.load(args.bundle).objects:
            if obj.type.name != 'Shader':
                continue
            tree = obj.read_typetree()
            name = tree.get('m_ParsedForm', {}).get('m_Name') or tree.get('m_Name', '')
            if args.pattern.strip('%').lower() in name.lower():
                objects.append((obj, dict(id='', bundle=args.bundle, pathid=str(obj.path_id),
                                         cab=obj.assets_file.name, name=name)))
    records = []
    seen = set()
    for obj, row in objects:
        identity = (obj.assets_file.name, obj.path_id)
        if identity in seen:
            continue
        seen.add(identity)
        shader = obj.read()
        original = obj.read_typetree()
        name = re.sub(r'[^a-zA-Z0-9_-]', '_', row['name']) + '-' + str(obj.path_id)
        target = folder / name
        target.mkdir(exist_ok=True)
        atomic_json(target / 'parsed-form.json', original.get('m_ParsedForm', {}))
        errors = []
        try:
            (target / 'serialized.shader.txt').write_text(export_shader(shader), encoding='utf-8')
        except (ValueError, struct.error, IndexError) as error:
            errors.append('UnityPy serialized conversion: ' + str(error))
        entries = []
        try:
            decoded = list(programs(shader))
        except (ValueError, struct.error, IndexError) as error:
            errors.append('UnityPy subprogram parsing: ' + str(error))
            decoded = []
            # Keep the exact DXBC container even when UnityPy cannot decode a
            # newer Unity subprogram header. Offsets refer to decompressed data.
            for index, platform in enumerate(shader.platforms):
                def first(values):
                    value = values[index]
                    return value[0] if isinstance(value, list) else value
                offset = first(shader.offsets)
                raw = CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[offset:offset+first(shader.compressedLengths)], first(shader.decompressedLengths))
                cursor = 0
                while True:
                    start = raw.find(b'DXBC', cursor)
                    if start < 0:
                        break
                    if len(raw)-start<32:
                        errors.append('Truncated DXBC header at '+str(start));break
                    size = struct.unpack_from('<I', raw, start + 24)[0]
                    code = raw[start:start+size]
                    key = f'{platform}-offset-{start}'
                    try:
                        text = disassemble(code)
                    except (ValueError, RuntimeError) as error:
                        errors.append(f'{key}: {error}');cursor=start+4;continue
                    (target / (key + '.bin')).write_bytes(code)
                    (target / (key + '.asm.txt')).write_text(text, encoding='utf-8')
                    entries.append(dict(platform=int(platform), offset=start, bytes=len(code),
                                        sha256=hashlib.sha256(code).hexdigest(), disassembled=True))
                    cursor = start + max(size, 4)
        for platform, program in decoded:
            for index,kind,raw in getattr(program,'parameter_entries',[]):
                (target / f'{platform}-{index}.parameters.bin').write_bytes(raw)
            for index, sub in enumerate(program.m_SubPrograms):
                if sub is None:continue
                code = bytes(sub.m_ProgramCode)
                key = f'{platform}-{index}'
                (target / (key + '.bin')).write_bytes(code)
                try:
                    text = disassemble(code)
                except (ValueError, RuntimeError) as error:
                    errors.append(f'{key}: {error}')
                    text = None
                if text is not None:
                    (target / (key + '.asm.txt')).write_text(text, encoding='utf-8')
                entries.append(dict(platform=int(platform), index=index,
                                    program_type=int(sub.m_ProgramType), bytes=len(code),
                                    sha256=hashlib.sha256(code).hexdigest(), disassembled=text is not None))
        record = dict(name=row['name'], asset_id=row['id'], bundle=row['bundle'],
                      cab=row['cab'], pathid=row['pathid'], programs=entries, errors=errors,
                      parameter_records=[dict(platform=int(platform),index=index,kind=kind,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())
                          for platform,program in decoded for index,kind,raw in getattr(program,'parameter_entries',[])])
        atomic_json(target / 'audit.json', record)
        records.append(record)
        print(row['name'], len(entries), 'programs', sum(e['disassembled'] for e in entries), 'DXBC', flush=True)
    atomic_json(folder / 'latest.json', records)
    audit_path=PROJECT / 'manifests/original-shader-audit.json'
    previous=json.loads(audit_path.read_text(encoding='utf-8')) if audit_path.exists() else {'shaders': []}
    by_identity={(s['cab'],s['pathid']): s for s in previous['shaders']}
    by_identity.update({(s['cab'],s['pathid']): s for s in records})
    atomic_json(audit_path, dict(kind='compiled-program-audit-not-restored-source',
                                shaders=sorted(by_identity.values(),key=lambda s:(s['name'],s['pathid']))))
    print('SHADER AUDIT', len(records), flush=True)


if __name__ == '__main__':
    main()
