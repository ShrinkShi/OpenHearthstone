"""Static-only source import for the Shaman base hero power."""
import json
import math

from .asset_lock import atomic_json
from .native_audio import bind_program, recover as recover_audio
from .native_effects import export_program


HERO_POWER_ID = 'HERO_02bp'
RUNTIME_DURATION = 2.5
ROOT_REFERENCE = 'Shaman_HeroPower_FX.prefab:e4a069df7e3900b4a878dfb437de270c'
ROOT_SOURCE = {
    'cab': 'CAB-67e4d1eb27ce21c49d64680c42a53700',
    'pathid': '7208523638269036997',
    'type': 'GameObject',
    'bundle': 'initial_base_global-41caf0cf-prefab-10.unity3d',
}
CUSTOM_SPELL_PATHID = '2121424015746561313'
PROFILE = 'source-backed dual-FSM CustomSpawn runtime adapter'

NODE_LAYOUT = [
    ('ReuseFX_Totem_OverrideSpawn_Blue_Super_SourceHeroPower', -1),
    ('MoteBurst', 0),
    ('TotemQuad_mesh', 0),
    ('ReuseFX_Totem_CustomSpawn', -1),
    ('FX_Crack', 3),
    ('FX_ImpactRocks', 4),
    ('Plane Transform 1', 5),
    ('FX_Explode', 3),
    ('FX_Explode2', 7),
    ('TotemGeneral_mesh', 3),
    ('GlowQuad', 9),
    ('totemGeneraGlow_mesh', 9),
    ('totemGeneraGlow2_mesh', 9),
]

STATE_ACTIONS = {
    0: {
        'State 1': [],
        'State0': ['SpellStateAction', 'AnimationPlaythroughAction', 'SpellFinishAction',
                   'AudioPlayClipAction', 'Wait'],
        'NoneState': ['SpellStateAction'],
        'State 2': ['ParticlePlayAction', 'Wait'],
    },
    3: {
        'State 1': [],
        'State 2': ['Wait'],
        'State 3': ['SpellStateAction', 'SpellInitActorVariables',
                    'ActorSetVisibilityRecursiveAction', 'Wait'],
        'State 4': ['AnimationPlaythroughAction', 'AudioPlayClipAction'],
        'State 5': ['ParticlePlayAction', 'ActorSetVisibilityRecursiveAction',
                    'DestroyObject', 'SpellFinishAction', 'Wait'],
        'NoneState': ['SpellStateAction'],
        'State 6': ['SetParent', 'ParticlePlayAction', 'MainCameraShakerAction'],
    },
}

FSM_EVENTS = {
    0: [('Action', 'State0'), ('None', 'NoneState')],
    3: [('None', 'NoneState'), ('PlayFX', 'State 2'),
        ('Action', 'State 3'), ('Die', 'State 5')],
}

STATE_TRANSITIONS = {
    0: {
        'State 1': [], 'State0': [('FINISHED', 'State 2')],
        'NoneState': [], 'State 2': [('FINISHED', 'NoneState')],
    },
    3: {
        'State 1': [], 'State 2': [('FINISHED', 'State 6')],
        'State 3': [('FINISHED', 'State 4')], 'State 4': [],
        'State 5': [('FINISHED', 'NoneState')], 'NoneState': [], 'State 6': [],
    },
}

UNKNOWN_ACTIONS = {
    'ActorSetVisibilityRecursiveAction', 'AudioPlayClipAction', 'DestroyObject',
    'SetParent', 'SpellInitActorVariables',
}

EXPECTED_AUDIO = [
    {
        'owner': 0, 'state': 'State0', 'action_index': 3,
        'asset_path': '028d1b3b3adcdc6c699bb729.native-sound.json',
        'clip': 'Shaman_TotemicCall_Cast_1.wav:ed7cf3849dad97144b7ec338fbdf681f',
        'sound_bundle_sha256': 'a61baf0c4551bb18557641b118388e612a1a7fc302d53758c2ed1a39cecb0f60',
        'clip_bundle_sha256': 'e8984ef825d04cf57412869b5a943a9123b7f93fb26a2cc8d4cc94d89b04ff0a',
        'duration_seconds': 2.467981859410431,
        'sample_rate': 44100, 'channels': 2,
        'volume': 0.5, 'spatial_blend': 1.0,
    },
    {
        'owner': 3, 'state': 'State 4', 'action_index': 1,
        'asset_path': '4c7619106ac52acba1c8a070.native-sound.json',
        'clip': 'Totems_CustomSpawn_1.wav:691e3a1e635383d42a33d91719f4684f',
        'sound_bundle_sha256': 'bea25f757d3a87494adb8efd4bae307283abb2089eabe1da706ca0179bc48a19',
        'clip_bundle_sha256': 'c42c6cbc58732b35b95ea1b084c309f0dc03b682e3018e3fe32dd6e14a2f6a98',
        'duration_seconds': 2.7432427406311035,
        'sample_rate': 44100, 'channels': 1,
        'volume': 0.7, 'spatial_blend': 0.0,
    },
]


def _require(condition, message):
    if not condition:
        raise ValueError('HERO_02bp ' + message)


def _one(rows, description):
    _require(len(rows) == 1, 'expected one ' + description)
    return rows[0]


def _action(machine, state_name, action_name):
    state = _one([row for row in machine['states'] if row.get('name') == state_name],
                 'state ' + state_name)
    return _one([row for row in state.get('actions', []) if row.get('name') == action_name],
                action_name + ' in ' + state_name)


def _param(action, name):
    return _one([row for row in action.get('params', []) if row.get('name') == name],
                name + ' parameter on ' + action.get('name', 'action'))


def _audio_profile(effect):
    rows = effect.get('program', {}).get('audio_actions') or []
    _require(len(rows) == len(EXPECTED_AUDIO), 'static audio action count changed')
    result = []
    for actual, expected in zip(rows, EXPECTED_AUDIO):
        _require(actual.get('action') == 'AudioPlayClipAction' and
                 actual.get('machine_owner_node') == expected['owner'] and
                 actual.get('state') == expected['state'] and
                 actual.get('action_index') == expected['action_index'],
                 'static audio FSM branch changed')
        sound_param = _param(actual, 'm_Sound')
        sound = actual.get('sound_def') or {}
        clips = sound.get('clips') or []
        clip = _one(clips, 'audio clip for ' + expected['clip'])
        source_file = sound.get('source_file') or {}
        clip_source_file = clip.get('source_file') or {}
        decoded = clip.get('decoded_export') or {}
        metadata = (sound.get('values') or {}).get('m_AudioClip')
        _require(sound_param.get('asset_path') == expected['asset_path'] and
                 sound.get('class_name') == 'SoundDef' and metadata == expected['clip'] and
                 source_file.get('sha256') == expected['sound_bundle_sha256'] and
                 clip.get('reference') == expected['clip'] and
                 clip_source_file.get('sha256') == expected['clip_bundle_sha256'] and
                 decoded.get('sample_rate') == expected['sample_rate'] and
                 decoded.get('channels') == expected['channels'] and
                 abs(decoded.get('duration_seconds', -1) - expected['duration_seconds']) < 1e-6,
                 'SoundDef or AudioClip source identity changed')
        result.append({
            'fsm_owner': expected['owner'], 'state': expected['state'],
            'sound_def': expected['asset_path'], 'clip': expected['clip'],
            'sound_bundle_sha256': expected['sound_bundle_sha256'],
            'clip_bundle_sha256': expected['clip_bundle_sha256'],
            'duration_seconds': expected['duration_seconds'],
        })
    return result


def _spawn_completion_age(program, machine):
    wait = _param(_action(machine, 'State 3', 'Wait'), 'time')
    wait_seconds = wait.get('number')
    _require(not wait.get('unresolved') and not wait.get('variable') and
             isinstance(wait_seconds, (int, float)) and math.isfinite(wait_seconds) and
             wait_seconds >= 0,
             'CustomSpawn Action wait is not a finite source-bound duration')
    state3 = _one([row for row in machine.get('states', [])
                   if row.get('name') == 'State 3'], 'state State 3')
    _require([(row.get('event'), row.get('state'))
              for row in state3.get('transitions', [])] ==
             [('FINISHED', 'State 4')],
             'CustomSpawn Action wait no longer leads to TotemSpawn')

    animation_action = _action(machine, 'State 4', 'AnimationPlaythroughAction')
    animation_name = _param(animation_action, 'm_AnimName').get('text')
    animation_node = _param(animation_action, 'm_GameObject').get('node')
    clip = _one([row for row in program.get('animations', [])
                 if row.get('name') == animation_name],
                'TotemSpawn animation clip')
    duration = clip.get('duration')
    _require(animation_name == 'TotemSpawn' and animation_node == 3 and
             clip.get('id') in (program['nodes'][3].get('animation_refs') or []) and
             clip.get('loop') is False and isinstance(duration, (int, float)) and
             math.isfinite(duration) and duration > 0,
             'TotemSpawn completion timing is not bound to its source clip')
    return wait_seconds + duration


def shaman_source_profile(program, source_card):
    """Validate H02 source facts and return the reviewed local playback profile."""
    _require(source_card.get('card_id') == HERO_POWER_ID, 'source card identity changed')
    effects = source_card.get('effect_references') or []
    effect = _one(effects, 'CardDef effect reference')
    _require(effect.get('reference') == ROOT_REFERENCE and
             effect.get('root') == ROOT_SOURCE and program.get('reference') == ROOT_REFERENCE and
             program.get('source') == ROOT_SOURCE, 'CardDef root source changed')

    components = effect.get('spell_components') or []
    override = _one([row for row in components if row.get('class_name') == 'OverrideCustomSpawnSpell'],
                    'OverrideCustomSpawnSpell component')
    spell = _one([row for row in components if row.get('class_name') == 'Spell'],
                 'CustomSpawn Spell component')
    _require(override.get('enabled') == 1 and spell.get('enabled') == 1,
             'source component enabled state changed')
    fields = override.get('fields') or {}
    target = fields.get('m_TargetInfo') or {}
    _require(target.get('m_Behavior') == 10 and target.get('m_RandomTargetCountMin') == 8 and
             target.get('m_RandomTargetCountMax') == 10,
             'authored target fields changed')
    null_slots = ('m_StartInfo', 'm_MissileInfo', 'm_ImpactInfo',
                  'm_FriendlyAreaEffectInfo', 'm_OpponentAreaEffectInfo', 'm_ChainInfo')
    for slot in null_slots:
        value = fields.get(slot) or {}
        _require(value.get('m_Enabled') == 1 and
                 value.get('m_Prefab') == {'m_FileID': 0, 'm_PathID': 0},
                 'enabled-null ' + slot + ' source changed')
    _require(fields['m_MissileInfo'].get('m_ReversePrefab') == {'m_FileID': 0, 'm_PathID': 0},
             'enabled-null reverse Missile source changed')

    spell_source = spell.get('source') or {}
    transforms = [row for row in program.get('spell_transforms', [])
                  if row.get('kind') == 'OverrideCustomSpawnSpell']
    transform = _one(transforms, 'OverrideCustomSpawnSpell source transform')
    custom_ref = ((transform.get('raw') or {}).get('m_CustomSpawnSpell') or {})
    _require(custom_ref == {'m_FileID': 5, 'm_PathID': int(CUSTOM_SPELL_PATHID)} and
             spell_source.get('pathid') == CUSTOM_SPELL_PATHID,
             'm_CustomSpawnSpell source binding changed')

    nodes = program.get('nodes') or []
    _require([(row.get('name'), row.get('parent')) for row in nodes] == NODE_LAYOUT,
             'root/CustomSpawn prefab hierarchy changed')
    _require(sum(bool(row.get('particle')) for row in nodes) == 5,
             'root particle inventory changed')
    _require([row.get('name') for row in (program.get('animations') or [])] ==
             ['FX_TotemFadeIn', 'TotemSpawn'], 'AnimationClip inventory changed')
    _require(sum(len(row.get('animation_refs') or []) for row in nodes) == 2,
             'Animation component references changed')
    _require(set(program.get('unknown_actions') or []) == UNKNOWN_ACTIONS,
             'unknown action inventory changed')

    machines = program.get('machines') or []
    _require([row.get('owner') if row.get('owner_known') else None for row in machines] == [0, 3],
             'FSM owner/order changed')
    for machine in machines:
        owner = machine['owner']
        states = machine.get('states') or []
        expected_states = STATE_ACTIONS[owner]
        _require(machine.get('start') == 'State 1' and
                 [row.get('name') for row in states] == list(expected_states),
                 'FSM start/state order changed for owner ' + str(owner))
        _require([(row.get('event'), row.get('state')) for row in machine.get('events', [])] ==
                 FSM_EVENTS[owner], 'FSM event branches changed for owner ' + str(owner))
        for state in states:
            name = state['name']
            _require([row.get('name') for row in state.get('actions', [])] == expected_states[name],
                     'FSM action order changed at owner {} {}'.format(owner, name))
            _require([(row.get('event'), row.get('state')) for row in state.get('transitions', [])] ==
                     STATE_TRANSITIONS[owner][name],
                     'FSM transition changed at owner {} {}'.format(owner, name))

    machine0, machine3 = machines
    spawn_completion_age = _spawn_completion_age(program, machine3)
    machines_by_owner = {row['owner']: row for row in machines}
    for expected in EXPECTED_AUDIO:
        action = _action(machines_by_owner[expected['owner']], expected['state'], 'AudioPlayClipAction')
        _require(_param(action, 'm_Sound').get('asset_path') == expected['asset_path'] and
                 _param(action, 'm_ParentObject').get('node') == expected['owner'] and
                 math.isclose(_param(action, 'm_Volume').get('number', -1), expected['volume'], abs_tol=1e-6) and
                 math.isclose(_param(action, 'm_SpatialBlend').get('number', -1), expected['spatial_blend'], abs_tol=1e-6),
                 'source FSM audio action binding changed')

    fade = _action(machine0, 'State0', 'AnimationPlaythroughAction')
    spawn = _action(machine3, 'State 4', 'AnimationPlaythroughAction')
    _require(_param(fade, 'm_AnimName').get('text') == 'FX_TotemFadeIn' and
             _param(fade, 'm_GameObject').get('node') == 2 and
             _param(spawn, 'm_AnimName').get('text') == 'TotemSpawn' and
             _param(spawn, 'm_GameObject').get('node') == 3,
             'source animation action bindings changed')
    init = _action(machine3, 'State 3', 'SpellInitActorVariables')
    _require(_param(init, 'm_actorObject').get('variable') == 'Actor' and
             _param(init, 'm_rootObject').get('is_none') and
             _param(init, 'm_rootObjectMesh').get('is_none'),
             'source Actor variable initialization changed')
    camera = _action(machine3, 'State 6', 'MainCameraShakerAction')
    _require(_param(camera, 'm_Amount').get('vector') == {'x': 0.02500000037252903,
             'y': 0.02500000037252903, 'z': 0.02500000037252903} and
             _param(camera, 'm_Time').get('number') == 0.5,
             'source camera shake action changed')
    for state_name, visibility in (('State 3', 0), ('State 5', 1)):
        action = _action(machine3, state_name, 'ActorSetVisibilityRecursiveAction')
        _require(_param(action, 'm_GameObject').get('variable') == 'Actor' and
                 _param(action, 'm_Visible').get('number') == visibility and
                 _param(action, 'm_IncludeChildren').get('number') == 1,
                 'source Actor visibility action changed at ' + state_name)
    destroy = _action(machine3, 'State 5', 'DestroyObject')
    _require(_param(destroy, 'gameObject').get('node') == 4 and
             _param(destroy, 'delay').get('number') == 0 and
             _param(destroy, 'detachChildren').get('number') == 0,
             'source CustomSpawn destroy action changed')
    reparent = _action(machine3, 'State 6', 'SetParent')
    _require(_param(reparent, 'gameObject').get('node') == 4 and
             _param(reparent, 'parent').get('is_none') and
             _param(reparent, 'worldPositionStays').get('number') == 1,
             'source CustomSpawn parent action changed')

    return {
        'profile': PROFILE,
        'card_id': HERO_POWER_ID,
        'reference': ROOT_REFERENCE,
        'spell_type': 'OverrideCustomSpawnSpell',
        'node_count': 13,
        'particle_count': 5,
        'animation_count': 2,
        'fsm_owners': [0, 3],
        'custom_spawn_completion_age_seconds': round(spawn_completion_age, 3),
        'audio_sources': _audio_profile(effect),
        'runtime_route_enabled': True,
        'known_gaps': [
            'Actor restoration at the Action wait plus TotemSpawn clip duration is source-derived; the original runtime completion signal has not been captured.',
            'Static SoundDef references do not prove final Listener mix or official loudness.',
            'Official same-camera animation and appearance comparison remains unavailable.',
        ],
    }


def import_shaman_source(ex, source_card):
    """Export only the reviewed H02 source graph and its statically referenced audio."""
    root = source_card['effect_references'][0]['reference']
    root_object, _, _ = ex.imp.reader.resolve(root)
    root_file, root_source = export_program(
        ex, root, suffix='.hero-power-source.json', include_component_effects=True
    )
    program = json.loads((ex.folder / root_file).read_text(encoding='utf-8'))
    profile = shaman_source_profile(program, source_card)
    audio_file = ex.key(root_object) + '.hero-power-audio.json'
    audio = recover_audio(ex, root, include_program_hierarchy=True)
    atomic_json(ex.folder / audio_file, audio)
    bind_program(ex, root_object, audio_file, suffix='.hero-power-source.json')
    return {
        'card_id': HERO_POWER_ID,
        'reference': root,
        'file': root_file,
        'root_source': root_source,
        'audio_file': audio_file,
        'source_profile': profile,
    }
