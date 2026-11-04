"""Audit base hero-power CardDef links to original prefabs and audio sources.

The reader opens the installed Hearthstone assets read-only. Derived exports are
written only under local/runtime; the tracked report contains metadata and hashes.
"""
import argparse
import hashlib
import json
import re
import wave
from pathlib import Path

from UnityPy.export.AudioClipConverter import get_resource_data

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import bind_program, export_clip, recover as recover_audio
from .native_effects import export_program, identity
from .timeline_source import export_source as export_timeline_source


HERO_POWERS = (
    'HERO_06bp', 'HERO_05bp', 'HERO_08bp', 'HERO_04bp', 'HERO_09bp',
    'HERO_03bp', 'HERO_02bp', 'HERO_07bp', 'HERO_01bp', 'HERO_10bp',
    'HERO_11bp',
)
PREFAB_REF = re.compile(r'^[^:]+\.prefab:[0-9a-fA-F]{32}$')
def is_audio_action(name):
    return 'Audio' in name and any(token in name for token in ('Play', 'Stop', 'Loop', 'Volume', 'Fade'))


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def walk_prefab_refs(value, path=''):
    if isinstance(value, dict):
        for key, child in value.items():
            yield from walk_prefab_refs(child, f'{path}/{key}')
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from walk_prefab_refs(child, f'{path}/{index}')
    elif isinstance(value, str) and PREFAB_REF.fullmatch(value):
        yield path, value


def source_file_record(bundle, paths, cache):
    if bundle not in cache:
        path = paths.get(bundle)
        cache[bundle] = ({
            'file': path.name,
            'size_bytes': path.stat().st_size,
            'sha256': sha256_file(path),
        } if path and path.is_file() else {'file': bundle, 'sha256': None})
    return cache[bundle]


def audio_clip_record(ex, clip, paths, bundle_hashes):
    source = clip.get('source')
    record = dict(clip)
    if source:
        record['source_file'] = source_file_record(source['bundle'], paths, bundle_hashes)
        clip_obj, _, _ = ex.imp.reader.resolve(clip['reference'])
        audio = clip_obj.read()
        if audio.m_AudioData:
            encoded = bytes(audio.m_AudioData)
        elif audio.m_Resource:
            resource = audio.m_Resource
            encoded = get_resource_data(
                resource.m_Source, clip_obj.assets_file,
                resource.m_Offset, resource.m_Size,
            )
        else:
            encoded = b''
        magic = encoded[:4]
        record['original_audio_stream'] = {
            'sha256': hashlib.sha256(encoded).hexdigest() if encoded else None,
            'size_bytes': len(encoded),
            'container': {
                b'OggS': 'ogg', b'RIFF': 'wav', b'ftyp': 'm4a',
            }.get(magic, 'Unity AudioClip payload'),
            'payload_decoding': 'UnityPy AudioClip resource bytes; raw payloads are converted by fmod_toolkit.raw_to_wav',
            'clip_metadata': {
                'name': audio.m_Name,
                'length_seconds': audio.m_Length,
                'frequency': audio.m_Frequency,
                'channels': audio.m_Channels,
                'bits_per_sample': audio.m_BitsPerSample,
                'compression_format': audio.m_CompressionFormat,
                'load_type': audio.m_LoadType,
                'resource_source': audio.m_Resource.m_Source if audio.m_Resource else None,
            },
        }
    filename = clip.get('file')
    if filename:
        path = ex.folder / filename
        decoded = {'file': filename, 'sha256': sha256_file(path), 'size_bytes': path.stat().st_size}
        if path.suffix.lower() == '.wav':
            with wave.open(str(path), 'rb') as wav:
                frames = wav.getnframes()
                decoded.update({
                    'sample_rate': wav.getframerate(),
                    'channels': wav.getnchannels(),
                    'sample_width_bytes': wav.getsampwidth(),
                    'frames': frames,
                    'duration_seconds': frames / wav.getframerate(),
                })
        record['decoded_export'] = decoded
    return record


def sound_identity_map(audio):
    return {
        item['source']['pathid']: item
        for node in audio.get('nodes', [])
        for item in node.get('sound_defs', [])
    }


def object_from_identity(reader, source):
    env = reader.load(source['bundle'])
    cab = env.get_cab(source['cab'].lower())
    if cab is None:
        raise KeyError('CAB missing for source identity: ' + source['cab'])
    return cab.objects[int(source['pathid'])]


def direct_sound_def(ex, source, paths, bundle_hashes):
    reader = ex.imp.reader
    obj = object_from_identity(reader, source)
    tree = obj.read_typetree()
    script = reader.pointer(obj, tree.get('m_Script', {}))
    script_tree = script.read_typetree() if script else {}
    if script_tree.get('m_ClassName') != 'SoundDef':
        raise ValueError('FSM audio object is not a SoundDef: ' + str(source))
    owner = reader.pointer(obj, tree.get('m_GameObject', {}))
    audio_sources = []
    if owner:
        for item in owner.read_typetree().get('m_Component', []):
            component = reader.pointer(owner, item['component'])
            if component and component.type.name == 'AudioSource':
                audio_sources.append({
                    'source': identity(ex, component),
                    'values': component.read_typetree(),
                })
    clips = []
    references = [tree.get('m_AudioClip', '')]
    references.extend(row.get('m_Clip', '') for row in tree.get('m_RandomClips', []))
    for reference in dict.fromkeys(ref for ref in references if ref):
        clip = export_clip(ex, reference)
        clips.append(audio_clip_record(ex, clip, paths, bundle_hashes))
    return {
        'source': identity(ex, obj),
        'class_name': script_tree.get('m_ClassName'),
        'source_file': source_file_record(source['bundle'], paths, bundle_hashes),
        'values': tree,
        'audio_sources': audio_sources,
        'clips': clips,
    }


def playback_actions(ex, program, audio, paths, bundle_hashes):
    sounds = sound_identity_map(audio)
    result = []
    for machine_index, machine in enumerate(program.get('machines', [])):
        for state_index, state in enumerate(machine.get('states', [])):
            for action_index, action in enumerate(state.get('actions', [])):
                if not is_audio_action(action.get('name', '')):
                    continue
                params = action.get('params', [])
                sound_source = next((
                    p.get('asset_source') for p in params
                    if p.get('name') in ('m_Sound', 'm_AudioClip') and p.get('asset_source')
                ), None)
                sound = sounds.get(sound_source['pathid']) if sound_source else None
                if sound_source:
                    sound = direct_sound_def(ex, sound_source, paths, bundle_hashes)
                sound_header = next((p for p in params if p.get('name') == 'm_Sounds'), None)
                sound_def_sources = []
                sound_array_unresolved = False
                if sound_header is not None:
                    start = params.index(sound_header) + 1
                    count = sound_header.get('array_count', 0)
                    sound_array_unresolved = not sound_header.get('array_known') or sound_header.get('array_type') != 'SoundDef'
                    if sound_header.get('array_known'):
                        members = params[start:start + count]
                        sound_array_unresolved |= len(members) != count or any(
                            p.get('unresolved') or p.get('asset_type') != 'SoundDef' or not p.get('asset_source')
                            for p in members
                        )
                        if not sound_array_unresolved:
                            sound_def_sources = [p['asset_source'] for p in members]
                action_record = {
                    'machine_index': machine_index,
                    'machine_source': machine.get('source'),
                    'machine_owner_node': machine.get('owner', -1),
                    'state_index': state_index,
                    'state': state.get('name'),
                    'action_index': action_index,
                    'action': action.get('name'),
                    'params': params,
                    'sound_def': sound,
                    'sound_def_sources': sound_def_sources,
                    'sound_defs': [direct_sound_def(ex, source, paths, bundle_hashes)
                                   for source in sound_def_sources],
                    'sound_array_unresolved': sound_array_unresolved,
                    'sound_def_source_file': source_file_record(
                        sound_source['bundle'], paths, bundle_hashes
                    ) if sound_source else None,
                }
                result.append(action_record)
    return result


def card_source(ex, card_id, paths, bundle_hashes, program_cache):
    reader = ex.imp.reader
    card_guid = reader.cards[card_id]
    card_object, card_bundle, locale = reader.resolve(card_guid, 'global')
    card_tree = card_object.read_typetree()
    definition = None
    for item in card_tree.get('m_Component', []):
        component = reader.pointer(card_object, item['component'])
        if component and component.type.name == 'MonoBehaviour':
            data = component.read_typetree()
            if 'm_PortraitTexturePath' in data:
                definition = (component, data)
                break
    if definition is None:
        raise ValueError(f'{card_id} has no CardDef component')
    carddef_component, data = definition
    portrait_reference = data.get('m_PortraitTexturePath', '')
    portrait = None
    if portrait_reference:
        portrait_obj, portrait_bundle, _ = reader.resolve(portrait_reference)
        portrait_file = ex.folder / ex.texture(portrait_obj)
        portrait = {
            'reference': portrait_reference,
            'source': identity(ex, portrait_obj),
            'source_file': source_file_record(portrait_bundle, paths, bundle_hashes),
            'decoded_png': {
                'file': portrait_file.name,
                'sha256': sha256_file(portrait_file),
                'size_bytes': portrait_file.stat().st_size,
            },
        }

    effects = []
    for field_path, reference in walk_prefab_refs(data):
        if reference in program_cache:
            effect_record = dict(program_cache[reference])
        else:
            root, root_bundle, _ = reader.resolve(reference)
            filename, summary = export_program(
                ex, reference, suffix='.hero-power-source.json',
                include_component_effects=True,
            )
            if card_id == 'HERO_11bp':
                export_timeline_source(ex, ex.key(root), suffix='.hero-power-source.json')
            program_path = ex.folder / filename
            program = json.loads(program_path.read_text(encoding='utf-8'))
            if card_id == 'HERO_11bp':
                summary['timelines'] = len(program.get('timelines') or [])
                summary['timeline_duration'] = max(
                    (timeline.get('duration', 0) for timeline in program.get('timelines') or []),
                    default=0,
                )
            audio = recover_audio(ex, reference, include_program_hierarchy=True)
            if card_id == 'HERO_11bp':
                audio_file = ex.key(root) + '.hero-power-audio.json'
                atomic_json(ex.folder / audio_file, audio)
                bind_program(ex, root, audio_file, suffix='.hero-power-source.json')
                program = json.loads(program_path.read_text(encoding='utf-8'))
            audio_clips = [audio_clip_record(ex, clip, paths, bundle_hashes)
                           for clip in audio.get('clips', [])]
            audio['clips'] = audio_clips
            spell_components = []
            for obj, tree in reader.walk(root, include_visual=True, max_nodes=4000, errors=[]):
                if obj.type.name != 'MonoBehaviour' or tree is None:
                    continue
                script = reader.pointer(obj, tree.get('m_Script', {}))
                if script is None:
                    continue
                script_tree = script.read_typetree()
                class_name = script_tree.get('m_ClassName', '')
                if not class_name.endswith('Spell'):
                    continue
                values = obj.read_typetree()
                spell_components.append({
                    'source': identity(ex, obj),
                    'class_name': class_name,
                    'namespace': script_tree.get('m_Namespace'),
                    'assembly': script_tree.get('m_AssemblyName'),
                    'enabled': values.get('m_Enabled'),
                    'fields': {
                        key: value for key, value in values.items()
                        if key.endswith('Info') or key in ('m_ActionInfo', 'm_StartDelay')
                    },
                })
            effect_record = {
                'reference': reference,
                'root': identity(ex, root),
                'source_file': source_file_record(root_bundle, paths, bundle_hashes),
                'program': {
                    'file': filename,
                    'sha256': sha256_file(program_path),
                    'summary': summary,
                    'audio_actions': playback_actions(
                        ex, program, audio, paths, bundle_hashes
                    ),
                },
                'audio': {
                    'complete': audio.get('complete', False),
                    'nodes': audio.get('nodes', []),
                    'clips': audio.get('clips', []),
                    'errors': audio.get('errors', []),
                    'notes': audio.get('notes', []),
                },
                'spell_components': spell_components,
            }
            program_cache[reference] = effect_record
        effect_record['carddef_field_path'] = field_path
        effects.append(effect_record)

    return {
        'card_id': card_id,
        'card_guid': card_guid,
        'carddef_source': identity(ex, card_object),
        'carddef_component_source': identity(ex, carddef_component),
        'carddef_source_file': source_file_record(card_bundle, paths, bundle_hashes),
        'resolved_locale': locale,
        'portrait': portrait,
        'effect_references': effects,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default=','.join(HERO_POWERS))
    parser.add_argument('--product-version', default='')
    args = parser.parse_args()
    ids = [value.strip() for value in args.ids.split(',') if value.strip()]
    unknown = sorted(set(ids) - set(HERO_POWERS))
    if unknown:
        parser.error('IDs must be base hero powers: ' + ','.join(unknown))

    ex = RuntimeExport()
    reader = ex.imp.reader
    paths = reader.source_paths()
    bundle_hashes = {}
    program_cache = {}
    source_manifests = []
    manifest_hashes = []
    for path in sorted(reader.root.glob('asset_manifest*.unity3d')):
        manifest_hashes.append({
            'file': path.name,
            'size_bytes': path.stat().st_size,
            'sha256': sha256_file(path),
        })
    for card_id in ids:
        try:
            card = card_source(ex, card_id, paths, bundle_hashes, program_cache)
        except Exception as error:
            card = {'card_id': card_id, 'error': str(error)}
        source_manifests.append(card)
        print(json.dumps({
            'card_id': card_id,
            'effect_references': len(card.get('effect_references', [])),
            'error': card.get('error'),
        }, ensure_ascii=False), flush=True)

    effect_index_path = ex.folder / 'effect-index.json'
    native_index_path = ex.folder / 'native-effect-index.json'
    effect_index = json.loads(effect_index_path.read_text(encoding='utf-8')) if effect_index_path.exists() else {'entries': []}
    native_index = json.loads(native_index_path.read_text(encoding='utf-8')) if native_index_path.exists() else {'entries': []}
    original_super_path = PROJECT / 'manifests/original-super-spell.json'
    original_super = json.loads(original_super_path.read_text(encoding='utf-8')) if original_super_path.exists() else {}
    effect_entries = {entry['card_id']: entry for entry in effect_index.get('entries', [])}
    native_entries = {entry['card_id']: entry for entry in native_index.get('entries', [])}
    original_super_cards = {
        program.get('card_id') for program in original_super.get('programs', [])
    }
    script_audit_path = PROJECT / 'manifests/original-script-audit.json'
    script_audit = json.loads(script_audit_path.read_text(encoding='utf-8')) if script_audit_path.exists() else {}
    audio_il = next((
        row for row in script_audit.get('scripts', [])
        if row.get('type', '').endswith('AudioPlayClipAction')
    ), None)
    static_type_path = ex.folder / 'hero-power-script-metadata.json'
    static_type_metadata = json.loads(static_type_path.read_text(encoding='utf-8-sig')) if static_type_path.exists() else None
    current_types = static_type_metadata.get('types', []) if static_type_metadata else []
    current_audio_action = next((
        row for row in current_types if row.get('type', '').endswith('AudioPlayClipAction')
    ), None)
    current_random_audio_action = next((
        row for row in current_types if row.get('type', '').endswith('AudioPlayRandomClipAction')
    ), None)
    current_super_spell = next((row for row in current_types if row.get('type') == 'SuperSpell'), None)
    audio_methods = current_audio_action.get('selected_method_il', []) if current_audio_action else []
    on_enter_il = next((row.get('instructions', []) for row in audio_methods if row.get('method') == 'OnEnter'), [])
    play_il = next((row.get('instructions', []) for row in audio_methods if row.get('method') == 'Play'), [])
    random_audio_methods = current_random_audio_action.get('selected_method_il', []) if current_random_audio_action else []
    random_on_enter = next((row.get('instructions', []) for row in random_audio_methods if row.get('method') == 'OnEnter'), [])
    random_play = next((row.get('instructions', []) for row in random_audio_methods if row.get('method') == 'Play'), [])
    random_choose_clip = next((row.get('instructions', []) for row in random_audio_methods if row.get('method') == 'ChooseClip'), [])
    random_choose_volume = next((row.get('instructions', []) for row in random_audio_methods if row.get('method') == 'ChooseVolume'), [])
    random_choose_pitch = next((row.get('instructions', []) for row in random_audio_methods if row.get('method') == 'ChoosePitch'), [])
    audio_il_evidence = {
        'static_delay_dispatch': bool(
            any('m_Delay' in row for row in on_enter_il)
            and any('AudioPlayClipAction::Play()' in row for row in on_enter_il)
        ),
        'static_sound_manager_call': any('SoundManager::PlayClip(SoundPlayClipArgs,System.Boolean,SoundManager/SoundOptions)' in row for row in play_il),
    }
    random_audio_il_evidence = {
        'static_delay_or_immediate_dispatch': (
            any('m_Delay' in row for row in random_on_enter)
            and any('AudioPlayRandomClipAction::Delay()' in row for row in random_on_enter)
            and any('AudioPlayRandomClipAction::Play()' in row for row in random_on_enter)
        ),
        'static_weighted_clip_selection': any('ActionHelpers::GetRandomWeightedIndex(HutongGames.PlayMaker.FsmFloat[])' in row for row in current_random_audio_action.get('calls', [])),
        'static_volume_pitch_selection': (
            any('AudioPlayRandomClipAction::ChooseVolume()' in row for row in random_play)
            and any('AudioPlayRandomClipAction::ChoosePitch()' in row for row in random_play)
        ),
        'static_sound_manager_call': any('SoundManager::PlayClip(SoundPlayClipArgs,System.Boolean,SoundManager/SoundOptions)' in row for row in random_play),
        'static_parent_and_template_forwarded': (
            any('SoundPlayClipArgs::m_parentObject' in row for row in random_play)
            and any('SoundPlayClipArgs::m_templateSource' in row for row in random_play)
        ),
    }
    for card in source_manifests:
        if 'error' in card:
            continue
        card_id = card['card_id']
        card['registry_presence'] = {
            'project_effect_index': effect_entries.get(card_id),
            'project_native_effect_index': native_entries.get(card_id),
            'existing_original_super_spell_audit': card_id in original_super_cards,
            'source_spell_component_count': sum(
                len(effect.get('spell_components', []))
                for effect in card['effect_references']
            ),
        }

    output = {
        'schema_version': 1,
        'generated_by': 'python -m tools.hero_power_sources',
        'installation': {
            'root': 'configured local Hearthstone installation',
            'product_version_from_file_metadata': args.product_version or None,
            'unitypy_version_fallback': '6000.3.11f1',
            'asset_manifests': manifest_hashes,
        },
        'read_only_sources': True,
        'derived_exports': 'local/runtime only',
        'source_bundle_hashes': bundle_hashes,
        'static_il_evidence': {
            'audit': 'manifests/original-script-audit.json',
            'current_official_assembly_sha256': static_type_metadata.get('assembly_sha256') if static_type_metadata else None,
            'previous_audit_assembly_sha256': audio_il.get('assembly_sha256') if audio_il else None,
            'previous_audit_matches_current_binary': bool(
                audio_il and static_type_metadata
                and audio_il.get('assembly_sha256', '').lower() == static_type_metadata.get('assembly_sha256', '').lower()
            ),
            'audio_action_type': current_audio_action.get('type') if current_audio_action else None,
            'audio_action_calls': current_audio_action.get('calls', []) if current_audio_action else [],
            'audio_action_selected_method_il': audio_methods,
            'audio_action_il_checks': audio_il_evidence,
            'random_audio_action_type': current_random_audio_action.get('type') if current_random_audio_action else None,
            'random_audio_action_calls': current_random_audio_action.get('calls', []) if current_random_audio_action else [],
            'random_audio_action_selected_method_il': random_audio_methods,
            'random_audio_action_il_checks': random_audio_il_evidence,
            'super_spell_type': current_super_spell.get('type') if current_super_spell else None,
            'spell_type_inheritance': [
                {'type': row.get('type'), 'base_type': row.get('base_type')}
                for row in current_types if row.get('type') in (
                    'OverrideCustomSpawnSpell', 'SuperSpell', 'Spell'
                )
            ],
            'notes': [
                'IL is static evidence only; the official assembly was not executed.',
                'The PlayMaker action is configured in the referenced FSM. Its state entry and the global client playback schedule were not observed in a running Hearthstone client.',
                'Current installation assembly metadata is read independently; the prior project script-audit hash may belong to a different client build.',
            ],
        },
        'static_type_metadata': static_type_metadata,
        'cards': source_manifests,
        'limitations': [
            'The original Hearthstone process and official assemblies were not executed.',
            'This is source-graph and static action evidence, not observed in-client playback.',
            'Resource Lab effect previews approximate 2D parameters and do not execute game scripts, dedicated shaders, or combat timing.',
            'Project effect registries are separate from the original client registry; absent entries do not prove that the source asset is absent.',
        ],
    }
    atomic_json(PROJECT / 'manifests/hero-power-native-sources.json', output)
    errors = [card for card in source_manifests if 'error' in card]
    print(json.dumps({
        'cards': len(source_manifests),
        'errors': errors,
        'unique_prefabs': len(program_cache),
        'source_bundles_hashed': len(bundle_hashes),
        'manifest': 'manifests/hero-power-native-sources.json',
    }, ensure_ascii=False))
    if errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
