"""Source-checked import for the locally verified Death Knight CustomSpawn route."""
import json
from collections import Counter
from pathlib import Path

from .asset_lock import atomic_json
from .native_audio import bind_program, recover as recover_audio
from .native_effects import export_program
from .timeline_source import export_source as export_timeline_source


HERO_POWER_ID = 'HERO_11bp'
ROOT_REFERENCE = (
    'ReuseFX_Necrotic_DeathKnight_OverrideSpawn_HeroPower_Super.prefab:'
    'd71d2c073842bf24a9a24f9cabada171'
)
PROFILE = 'source-bound CustomSpawn; authoritative ghoul Actor route; local Player verified'
RUNTIME_DURATION = 5.0
ACTION_INVENTORY = {
    'SpellPlayerSideEventAction': 1,
    'SpellStateAction': 2,
    'InitializeCustomSpawn': 1,
    'ActorSetVisibilityRecursiveAction': 2,
    'ParticlePlayAction': 1,
    'AudioPlayClipAction': 1,
    'Wait': 4,
    'FinalizeCustomSpawn': 1,
    'SpellFinishAction': 1,
    'DestroyObject': 2,
    'SetParent': 3,
    'TimelinePlay': 1,
    'SetPosition': 2,
}
UNKNOWN_ACTIONS = {
    'ActorSetVisibilityRecursiveAction', 'AudioPlayClipAction', 'DestroyObject',
    'FinalizeCustomSpawn', 'InitializeCustomSpawn', 'SetParent', 'SetPosition',
    'SpellPlayerSideEventAction', 'TimelinePlay',
}
NODE_LAYOUT = [
    ('ReuseFX_Necrotic_DeathKnight_OverrideSpawn_HeroPower_Super', -1),
    ('ReuseFX_Necrotic_DeathKnight_CustomSpawn_GhoulCharge', -1),
    ('Root', 1), ('Token_Root', 2), ('Mesh_SimpleGlow', 3),
    ('Mesh_Initial_Crack', 2), ('Mesh_Cracked_Hole', 2), ('FX_Dust', 2),
    ('FX_Rocks', 7), ('Collider', 8), ('FX_Smoke', 7), ('FX_Motes', 10),
    ('FX_Glow', 7), ('FX_Rocks (1)', 7), ('Collider', 13),
    ('FX_InitialRocks', 2), ('FX_Motes', 15), ('Source_Root', 1),
    ('FX_Smoke', 17), ('FX_Motes', 18),
    ('ReuseFX_Necrotic_DeathKnight_CustomSpawn_GhoulCharge_Token_Sound', 1),
]
STATE_ACTIONS = {
    'State 1': ['SpellPlayerSideEventAction'],
    'ActionState': ['SpellStateAction', 'InitializeCustomSpawn',
                    'ActorSetVisibilityRecursiveAction', 'ParticlePlayAction',
                    'AudioPlayClipAction', 'Wait'],
    'Visibility On': ['ActorSetVisibilityRecursiveAction', 'Wait'],
    'Finish': ['FinalizeCustomSpawn', 'SpellFinishAction', 'Wait'],
    'State 2': ['SpellStateAction', 'DestroyObject', 'DestroyObject'],
    'Minion Spawn': ['SetParent', 'TimelinePlay', 'Wait'],
    'Friendly Position': ['SetParent', 'SetPosition'],
    'Opponent Position': ['SetParent', 'SetPosition'],
}
POSITION_BINDINGS = {
    'Friendly Position': ('FriendlyHP', (-5.786620140075684, 0.30000001192092896, -8.090191841125488)),
    'Opponent Position': ('OpponentHP', (-5.781129837036133, 0.30000001192092896, -1.4199999570846558)),
}


def _param(action, name):
    params = [param for param in action.get('params', []) if param.get('name') == name]
    if len(params) != 1:
        raise ValueError('HERO_11bp expected one {} parameter on {}'.format(name, action.get('name')))
    return params[0]


def _value(param):
    if param.get('unresolved'):
        raise ValueError('HERO_11bp has an unresolved source parameter')
    return param


def _verify_timeline(program):
    timelines = program.get('timelines') or []
    if len(timelines) != 1:
        raise ValueError('HERO_11bp Timeline definition changed')
    timeline = timelines[0]
    source = timeline.get('source') or {}
    if (timeline.get('node') != 1 or timeline.get('initial_time') != 0 or
            timeline.get('wrap') != 2 or abs(timeline.get('duration', 0) - 5) > 1e-5 or
            source.get('pathid') != '-3102792596762892604' or source.get('type') != 'PlayableDirector'):
        raise ValueError('HERO_11bp Timeline source binding changed')
    tracks = timeline.get('tracks') or []
    counts = Counter(track.get('kind') for track in tracks)
    if counts != Counter({
            'AnimationTrack': 1,
            'ControlRandomizedTrack': 2,
            'CameraShaker2Track': 1,
            'SoundTimelineTrack': 1,
    }):
        raise ValueError('HERO_11bp Timeline track inventory changed')
    by_kind = {kind: [track for track in tracks if track.get('kind') == kind]
               for kind in counts}
    animation_track = by_kind['AnimationTrack'][0]
    animation_clips = animation_track.get('clips') or []
    if (animation_track.get('node') != 2 or len(animation_clips) != 1 or
            not animation_clips[0].get('compatible') or
            abs(animation_clips[0].get('duration', 0) - 3) > 1e-5):
        raise ValueError('HERO_11bp Timeline animation binding changed')
    control_nodes = []
    for track in by_kind['ControlRandomizedTrack']:
        clips = track.get('clips') or []
        if len(clips) != 1 or not (clips[0].get('control') or {}).get('compatible'):
            raise ValueError('HERO_11bp Timeline particle control changed')
        control = clips[0]['control']
        if control.get('particle_roots') != [control.get('node')]:
            raise ValueError('HERO_11bp Timeline particle root changed')
        control_nodes.append(control.get('node'))
    if sorted(control_nodes) != [7, 15]:
        raise ValueError('HERO_11bp Timeline particle targets changed')
    shake_clips = [clip for track in by_kind['CameraShaker2Track']
                   for clip in track.get('clips', [])]
    if len(shake_clips) != 2 or any(not (clip.get('shaker') or {}).get('compatible')
                                    for clip in shake_clips):
        raise ValueError('HERO_11bp Timeline camera shake changed')
    sound_track = by_kind['SoundTimelineTrack'][0]
    sound_clips = sound_track.get('clips') or []
    if sound_track.get('node') != 20 or len(sound_clips) != 1:
        raise ValueError('HERO_11bp Timeline sound binding changed')
    asset = sound_clips[0].get('original_asset') or {}
    audio_source = asset.get('m_AudioSource') or {}
    if audio_source.get('exposedName') != '-511187549':
        raise ValueError('HERO_11bp Timeline sound source changed')


def death_knight_source_profile(program, source_card):
    """Verify the exact H11 graph before considering the reviewed CustomSpawn route."""
    if source_card.get('card_id') != HERO_POWER_ID:
        raise ValueError('HERO_11bp source card identity changed')
    effects = source_card.get('effect_references') or []
    if len(effects) != 1 or effects[0].get('reference') != ROOT_REFERENCE:
        raise ValueError('HERO_11bp CardDef root reference changed')
    components = effects[0].get('spell_components') or []
    overrides = [row for row in components if row.get('class_name') == 'OverrideCustomSpawnSpell']
    if len(overrides) != 1 or overrides[0].get('enabled') != 1:
        raise ValueError('HERO_11bp must retain one enabled OverrideCustomSpawnSpell component')
    if any(row.get('class_name') == 'SuperSpell' for row in components):
        raise ValueError('HERO_11bp source type must not be promoted to SuperSpell')
    fields = overrides[0].get('fields') or {}
    missile = fields.get('m_MissileInfo') or {}
    if missile.get('m_Enabled') != 1 or missile.get('m_Prefab') != {'m_FileID': 0, 'm_PathID': 0}:
        raise ValueError('HERO_11bp authored enabled-null Missile source changed')
    target = fields.get('m_TargetInfo') or {}
    if target.get('m_Behavior') != 10 or target.get('m_RandomTargetCountMin') != 8 or \
       target.get('m_RandomTargetCountMax') != 10:
        raise ValueError('HERO_11bp source target fields changed')

    if program.get('reference') != ROOT_REFERENCE:
        raise ValueError('HERO_11bp source program identity changed')
    nodes = program.get('nodes') or []
    if [(node.get('name'), node.get('parent')) for node in nodes] != NODE_LAYOUT:
        raise ValueError('HERO_11bp source node hierarchy changed')
    if sum(bool(node.get('particle')) for node in nodes) != 10 or \
       len(program.get('animations') or []) != 1 or len(program.get('captures') or []) != 0:
        raise ValueError('HERO_11bp source visual inventory changed')
    if set(program.get('unknown_actions') or []) != UNKNOWN_ACTIONS:
        raise ValueError('HERO_11bp source action inventory changed')

    machines = program.get('machines') or []
    if len(machines) != 1:
        raise ValueError('HERO_11bp source FSM count changed')
    machine = machines[0]
    states = machine.get('states') or []
    if not machine.get('owner_known') or machine.get('owner') != 1 or machine.get('start') != 'State 1' or \
       [state.get('name') for state in states] != list(STATE_ACTIONS):
        raise ValueError('HERO_11bp source FSM owner or state order changed')
    if machine.get('events') != [
        {'event': 'Action', 'state': 'ActionState'},
        {'event': 'None', 'state': 'State 2'},
    ]:
        raise ValueError('HERO_11bp source FSM entry events changed')
    for state in states:
        if [action.get('name') for action in state.get('actions', [])] != STATE_ACTIONS[state['name']]:
            raise ValueError('HERO_11bp action sequence changed in ' + state['name'])
    action_counts = Counter(action['name'] for state in states for action in state['actions'])
    if dict(action_counts) != ACTION_INVENTORY:
        raise ValueError('HERO_11bp source action counts changed')
    expected_transitions = {
        'State 1': [('Friendly', 'Friendly Position'), ('Opponent', 'Opponent Position')],
        'ActionState': [('FINISHED', 'Minion Spawn')],
        'Visibility On': [('FINISHED', 'Finish')],
        'Finish': [('FINISHED', 'State 2')],
        'State 2': [],
        'Minion Spawn': [('FINISHED', 'Visibility On')],
        'Friendly Position': [], 'Opponent Position': [],
    }
    for state in states:
        transitions = [(row.get('event'), row.get('state')) for row in state.get('transitions', [])]
        if transitions != expected_transitions[state['name']]:
            raise ValueError('HERO_11bp transition graph changed in ' + state['name'])

    by_name = {state['name']: state for state in states}
    actions = {state: {action['name']: action for action in by_name[state]['actions']}
               for state in by_name}
    vectors = program.get('machines', [{}])[0].get('vectors') or []
    if len(vectors) != 2:
        raise ValueError('HERO_11bp position vector inventory changed')
    for state_name, (variable, expected) in POSITION_BINDINGS.items():
        position_state = by_name[state_name]
        set_parent = actions[state_name]['SetParent']
        parent_target = _value(_param(set_parent, 'gameObject'))
        parent = _value(_param(set_parent, 'parent'))
        set_position = actions[state_name]['SetPosition']
        target = _value(_param(set_position, 'gameObject'))
        vector = _value(_param(set_position, 'vector'))
        if (parent_target.get('kind') != 20 or parent_target.get('node') != 17 or
                parent_target.get('variable') != 'HeroPowerFX' or
                parent.get('none_known') is not True or parent.get('is_none') is not True or
                target.get('kind') != 20 or target.get('node') != 17 or
                target.get('variable') != 'HeroPowerFX' or vector.get('kind') != 28 or
                vector.get('variable') != variable or vector.get('none_known') is not True or
                vector.get('is_none') is not False):
            raise ValueError('HERO_11bp position state binding changed in ' + state_name)
        bound_vectors = [row for row in vectors if row.get('name') == variable]
        actual = bound_vectors[0].get('value') if len(bound_vectors) == 1 else None
        authored = vector.get('vector')
        if actual is None or authored is None or any(
                abs(actual.get(axis, float('inf')) - value) > 1e-5 or
                abs(authored.get(axis, float('inf')) - value) > 1e-5
                for axis, value in zip(('x', 'y', 'z'), expected)):
            raise ValueError('HERO_11bp position vector value changed in ' + state_name)
    initialize = actions['ActionState']['InitializeCustomSpawn']
    if _value(_param(initialize, 'tokenRoot')).get('node') != 3 or \
       _value(_param(initialize, 'actorOutput')).get('variable') != 'Actor' or \
       _value(_param(initialize, 'actorParentOutput')).get('variable') != 'ActorParent':
        raise ValueError('HERO_11bp source Actor loan bindings changed')
    for name, expected in [('resetActorPosition', 1), ('resetActorRotation', 1), ('resetActorScale', 0)]:
        if _value(_param(initialize, name)).get('number') != expected:
            raise ValueError('HERO_11bp InitializeCustomSpawn flags changed')
    finalize = actions['Finish']['FinalizeCustomSpawn']
    if _value(_param(finalize, 'actor')).get('variable') != 'Actor':
        raise ValueError('HERO_11bp source Finalize Actor binding changed')
    for name in ('resetActorPosition', 'resetActorRotation', 'resetActorScale'):
        if _value(_param(finalize, name)).get('number') != 1:
            raise ValueError('HERO_11bp FinalizeCustomSpawn flags changed')
    timeline = actions['Minion Spawn']['TimelinePlay']
    if _value(_param(timeline, 'gameObject')).get('node') != 1 or \
       _value(_param(timeline, 'finishOnTimelineStopped')).get('number') != 0:
        raise ValueError('HERO_11bp TimelinePlay source parameters changed')
    _verify_timeline(program)
    audio = actions['ActionState']['AudioPlayClipAction']
    if _value(_param(audio, 'm_ParentObject')).get('node') != 1 or \
       _value(_param(audio, 'm_Sound')).get('asset_path') != '0a00088bec631cc41f31aebd.native-sound.json':
        raise ValueError('HERO_11bp root SoundDef identity changed')

    return {
        'profile': PROFILE,
        'card_id': HERO_POWER_ID,
        'reference': ROOT_REFERENCE,
        'spell_type': 'OverrideCustomSpawnSpell',
        'node_count': len(nodes),
        'particle_count': 10,
        'animation_count': 1,
        'timeline_count': 1,
        'timeline_node': 1,
        'timeline_duration': 5,
        'action_inventory': dict(ACTION_INVENTORY),
        'runtime_route_enabled': True,
        'runtime_duration': RUNTIME_DURATION,
        'known_gaps': [
            'Official matching-camera visual and isolated original-audio fidelity remain unverified.',
        ],
    }


def verify_runtime_dependencies(program, folder):
    """Reject missing or escaped files throughout the exported dependency graph."""
    root = Path(folder).resolve()
    checked = set()
    pending = []
    def collect(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in ('file', 'path', 'mesh', 'asset_path', 'audio_file', 'event_audio_file') and isinstance(child, str) and child.endswith(('.json', '.png', '.wav')):
                    pending.append(child)
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)
    if not program.get('audio_file'):
        raise ValueError('HERO_11bp source audio binding is missing')
    if program.get('dependency_errors'):
        raise ValueError('HERO_11bp source has unresolved runtime dependencies')
    collect(program)
    while pending:
        filename = pending.pop()
        if filename in checked:
            continue
        path = (root / filename).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError('HERO_11bp missing or escaped runtime dependency: ' + filename)
        checked.add(filename)
        if path.suffix == '.json':
            document = json.loads(path.read_text(encoding='utf-8'))
            if isinstance(document, dict) and (document.get('errors') or document.get('dependency_errors')):
                raise ValueError('HERO_11bp dependency has unresolved source errors: ' + filename)
            collect(document)
    return sorted(checked)


def import_death_knight_source(ex, source_card):
    """Export H11 source and audio assets without creating a playable SuperSpell config."""
    root = source_card['effect_references'][0]['reference']
    root_object, _, _ = ex.imp.reader.resolve(root)
    root_file, root_source = export_program(
        ex, root, suffix='.hero-power-source.json', include_component_effects=True
    )
    export_timeline_source(ex, ex.key(root_object), suffix='.hero-power-source.json')
    program = json.loads((ex.folder / root_file).read_text(encoding='utf-8'))
    profile = death_knight_source_profile(program, source_card)
    audio_file = ex.key(root_object) + '.hero-power-audio.json'
    atomic_json(ex.folder / audio_file, recover_audio(ex, root, include_program_hierarchy=True))
    bind_program(ex, root_object, audio_file, suffix='.hero-power-source.json')
    bound_program = json.loads((ex.folder / root_file).read_text(encoding='utf-8'))
    profile['runtime_dependencies'] = verify_runtime_dependencies(bound_program, ex.folder)
    return {
        'card_id': HERO_POWER_ID,
        'reference': root,
        'file': root_file,
        'root_source': root_source,
        'audio_file': audio_file,
        'source_profile': profile,
    }
