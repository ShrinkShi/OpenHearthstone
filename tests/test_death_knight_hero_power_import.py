import json
import pytest
from pathlib import Path

from tools.death_knight_hero_power import (
    ACTION_INVENTORY, HERO_POWER_ID, NODE_LAYOUT, PROFILE, ROOT_REFERENCE,
    STATE_ACTIONS, UNKNOWN_ACTIONS, death_knight_source_profile, verify_runtime_dependencies,
)


def h11_fixture():
    components = [
        {'class_name': 'OverrideCustomSpawnSpell', 'enabled': 1, 'fields': {
            'm_MissileInfo': {'m_Enabled': 1, 'm_Prefab': {'m_FileID': 0, 'm_PathID': 0}},
            'm_TargetInfo': {'m_Behavior': 10, 'm_RandomTargetCountMin': 8, 'm_RandomTargetCountMax': 10},
        }},
        {'class_name': 'Spell', 'enabled': 1, 'fields': {}},
    ]
    source = {'card_id': HERO_POWER_ID, 'effect_references': [
        {'reference': ROOT_REFERENCE, 'spell_components': components}]}
    transitions = {
        'State 1': [('Friendly', 'Friendly Position'), ('Opponent', 'Opponent Position')],
        'ActionState': [('FINISHED', 'Minion Spawn')],
        'Visibility On': [('FINISHED', 'Finish')], 'Finish': [('FINISHED', 'State 2')],
        'State 2': [], 'Minion Spawn': [('FINISHED', 'Visibility On')],
        'Friendly Position': [], 'Opponent Position': [],
    }
    states = []
    for name, action_names in STATE_ACTIONS.items():
        actions = [{'name': action, 'params': []} for action in action_names]
        by_name = {action['name']: action for action in actions}
        if name == 'ActionState':
            by_name['InitializeCustomSpawn']['params'] = [
                {'name': 'tokenRoot', 'node': 3}, {'name': 'actorOutput', 'variable': 'Actor'},
                {'name': 'actorParentOutput', 'variable': 'ActorParent'},
                {'name': 'resetActorPosition', 'number': 1},
                {'name': 'resetActorRotation', 'number': 1},
                {'name': 'resetActorScale', 'number': 0},
            ]
            by_name['AudioPlayClipAction']['params'] = [
                {'name': 'm_ParentObject', 'node': 1},
                {'name': 'm_Sound', 'asset_path': '0a00088bec631cc41f31aebd.native-sound.json'},
            ]
        if name == 'Finish':
            by_name['FinalizeCustomSpawn']['params'] = [
                {'name': 'actor', 'variable': 'Actor'},
                {'name': 'resetActorPosition', 'number': 1},
                {'name': 'resetActorRotation', 'number': 1},
                {'name': 'resetActorScale', 'number': 1},
            ]
        if name in ('Friendly Position', 'Opponent Position'):
            vector_name, vector = (
                ('FriendlyHP', {'x': -5.786620140075684, 'y': 0.30000001192092896, 'z': -8.090191841125488})
                if name == 'Friendly Position' else
                ('OpponentHP', {'x': -5.781129837036133, 'y': 0.30000001192092896, 'z': -1.4199999570846558})
            )
            by_name['SetParent']['params'] = [
                {'name': 'gameObject', 'kind': 20, 'node': 17, 'variable': 'HeroPowerFX'},
                {'name': 'parent', 'none_known': True, 'is_none': True},
            ]
            by_name['SetPosition']['params'] = [
                {'name': 'gameObject', 'kind': 20, 'node': 17, 'variable': 'HeroPowerFX'},
                {'name': 'vector', 'kind': 28, 'variable': vector_name,
                 'none_known': True, 'is_none': False, 'vector': vector},
            ]
        if name == 'Minion Spawn':
            by_name['TimelinePlay']['params'] = [
                {'name': 'gameObject', 'node': 1}, {'name': 'finishOnTimelineStopped', 'number': 0},
            ]
        states.append({'name': name, 'actions': actions,
                       'transitions': [{'event': event, 'state': target}
                                       for event, target in transitions[name]]})
    program = {
        'reference': ROOT_REFERENCE,
        'nodes': [{'name': name, 'parent': parent,
                   'particle': {'restored': True} if 2 <= index < 12 else None}
                  for index, (name, parent) in enumerate(NODE_LAYOUT)],
        'animations': [{'id': 'h11-ghoul-animation'}], 'captures': [],
        'timelines': [{
            'node': 1, 'initial_time': 0, 'wrap': 2, 'duration': 5,
            'source': {'pathid': '-3102792596762892604', 'type': 'PlayableDirector'},
            'tracks': [
                {'kind': 'AnimationTrack', 'node': 2, 'clips': [
                    {'animation': 'h11-ghoul-animation', 'duration': 3, 'compatible': True}]},
                {'kind': 'ControlRandomizedTrack', 'clips': [
                    {'control': {'node': 7, 'particle_roots': [7], 'compatible': True}}]},
                {'kind': 'ControlRandomizedTrack', 'clips': [
                    {'control': {'node': 15, 'particle_roots': [15], 'compatible': True}}]},
                {'kind': 'CameraShaker2Track', 'clips': [
                    {'shaker': {'compatible': True}}, {'shaker': {'compatible': True}}]},
                {'kind': 'SoundTimelineTrack', 'node': 20, 'clips': [
                    {'original_asset': {'m_AudioSource': {'exposedName': '-511187549'}}}]},
            ],
        }],
        'unknown_actions': sorted(UNKNOWN_ACTIONS),
        'machines': [{'owner_known': True, 'owner': 1, 'start': 'State 1',
                      'vectors': [
                          {'name': 'FriendlyHP', 'value': {'x': -5.786620140075684,
                           'y': 0.30000001192092896, 'z': -8.090191841125488}},
                          {'name': 'OpponentHP', 'value': {'x': -5.781129837036133,
                           'y': 0.30000001192092896, 'z': -1.4199999570846558}},
                      ],
                      'events': [{'event': 'Action', 'state': 'ActionState'},
                                 {'event': 'None', 'state': 'State 2'}], 'states': states}],
    }
    return program, source


def test_h11_source_profile_enables_only_the_reviewed_custom_spawn_contract():
    program, source = h11_fixture()
    profile = death_knight_source_profile(program, source)
    assert profile['profile'] == PROFILE
    assert profile['runtime_route_enabled'] is True
    assert profile['runtime_duration'] == 5.0
    assert profile['action_inventory'] == ACTION_INVENTORY
    assert profile['timeline_count'] == 1
    assert profile['timeline_node'] == 1
    assert profile['timeline_duration'] == 5
    assert len(profile['known_gaps']) == 1


def test_h11_dependency_closure_rejects_missing_nested_assets(tmp_path):
    program = {'audio_file': 'audio.json'}
    (tmp_path / 'audio.json').write_text(json.dumps({'clips': [{'file': 'token.wav'}]}), encoding='utf-8')
    with pytest.raises(ValueError, match='runtime dependency: token.wav'):
        verify_runtime_dependencies(program, tmp_path)
    (tmp_path / 'token.wav').write_bytes(b'fixture')
    assert verify_runtime_dependencies(program, tmp_path) == ['audio.json', 'token.wav']


def test_h11_dependency_closure_rejects_missing_binding_and_escape(tmp_path):
    with pytest.raises(ValueError, match='audio binding'):
        verify_runtime_dependencies({}, tmp_path)
    with pytest.raises(ValueError, match='escaped runtime dependency'):
        verify_runtime_dependencies({'audio_file': '../outside.json'}, tmp_path)


def test_h11_dependency_closure_rejects_partial_source_exports(tmp_path):
    (tmp_path / 'audio.json').write_text(json.dumps({'errors': ['unresolved SoundDef']}), encoding='utf-8')
    with pytest.raises(ValueError, match='unresolved source errors: audio.json'):
        verify_runtime_dependencies({'audio_file': 'audio.json'}, tmp_path)
    with pytest.raises(ValueError, match='unresolved runtime dependencies'):
        verify_runtime_dependencies({'audio_file': 'audio.json', 'dependency_errors': ['missing particle texture']}, tmp_path)


def test_h11_position_states_are_locked_to_the_two_source_vector_names():
    program, source = h11_fixture()
    death_knight_source_profile(program, source)

    opponent = next(state for state in program['machines'][0]['states'] if state['name'] == 'Opponent Position')
    set_position = next(action for action in opponent['actions'] if action['name'] == 'SetPosition')
    next(param for param in set_position['params'] if param['name'] == 'vector')['variable'] = 'UnknownHP'
    with pytest.raises(ValueError, match='position state binding changed in Opponent Position'):
        death_knight_source_profile(program, source)

    program, source = h11_fixture()
    program['machines'][0]['vectors'].pop()
    with pytest.raises(ValueError, match='position vector inventory changed'):
        death_knight_source_profile(program, source)


def test_h11_enabled_index_is_rejected_before_native_effect_factory():
    root = Path(__file__).resolve().parents[1]
    player = (root / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    assert 'return entry.enabled&&NativeDeathKnightHeroPowerPlayback.Supports(assets,entry);' in player

    prepare = player.split('public bool PrepareSource(', 1)[1].split('static bool? Friendly(', 1)[0]
    assert prepare.index('!Enabled(entry)') < prepare.index('var context=Context(') < prepare.index('var instance=Instance(')

    draw_effect = player.split('public bool Draw(string key,SemanticEvent', 1)[1].split(
        'public bool Draw(string key,string card', 1)[0]
    h11_draw = draw_effect.split('if(data!=null&&data.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)', 1)[1].split(
        'NativeEffectEntry entry;', 1)[0]
    assert h11_draw.index('!Enabled(h11Entry)') < h11_draw.index('Event.current==null') < h11_draw.index('PrepareSource(')
    ordinary_draw = draw_effect.split('NativeEffectEntry entry;', 1)[1]
    assert ordinary_draw.index('!Enabled(entry)') < ordinary_draw.index('var context=Context(')

    draw_instance = player.split('public bool Draw(string key,string card', 1)[1].split(
        'public void Render(', 1)[0]
    assert draw_instance.index('!Enabled(entry)') < draw_instance.index('Instance(')


def test_h11_pairing_and_duplicate_summon_suppression_are_exact_and_fail_closed():
    root = Path(__file__).resolve().parents[1]
    route = (root / 'UnityClient/Assets/Scripts/NativeDeathKnightHeroPowerPlayback.cs').read_text(encoding='utf-8')
    presentation = (root / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    summons = (root / 'UnityClient/Assets/Scripts/NativeBattleSummons.cs').read_text(encoding='utf-8')

    pair = route.split('public static bool TryGetPair(', 1)[1].split(
        'public static bool TryGetPairForSummon(', 1)[0]
    assert 'powerEvent.type!="PlayHeroPower"' in pair
    assert 'e.type=="Summon"&&e.source==powerEvent.target' in pair
    assert "ghoul.card_id!=GhoulId" in pair
    assert 'targetOwner!=afterOwner' in pair
    assert 'routeEnabled&&TryGetPairForSummon' in route

    accept = presentation.split('public void Accept(', 1)[1].split('void Begin(', 1)[0]
    assert 'card==NativeDeathKnightHeroPowerPlayback.HeroPowerId' in accept
    assert 'TryGetPair(e,before,after,out pair)' in accept
    assert 'target=pair.ghoul.entity' in accept
    assert 'NativeSummonSuppressed!=null&&NativeSummonSuppressed(e,current.before,current.after)' in presentation
    assert 'Func<SemanticEvent,StateView,StateView,bool> SkipSpell' in summons
    assert 'SkipSpell(data,before,after)' in summons


def test_h11_runtime_route_pairs_before_borrow_and_keeps_failed_route_generic():
    root = Path(__file__).resolve().parents[1]
    route = (root / 'UnityClient/Assets/Scripts/NativeDeathKnightHeroPowerPlayback.cs').read_text(encoding='utf-8')
    instance = (root / 'UnityClient/Assets/Scripts/NativeEffectInstance.cs').read_text(encoding='utf-8')
    player = (root / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    presentation = (root / 'UnityClient/Assets/Scripts/Presentation.cs').read_text(encoding='utf-8')
    client = (root / 'UnityClient/Assets/Scripts/ClientApp.cs').read_text(encoding='utf-8')

    prepare = player.split('bool PrepareDeathKnightSource(', 1)[1].split('static void ValidateDeathKnightEffect', 1)[0]
    assert prepare.index('TryGetPairForPowerCue(') < prepare.index('ActorObject(pair.ghoul)')
    assert 'deferEntryEvents:true' in prepare
    assert prepare.index('BindTimelineCamera(') < prepare.index('SpellPlayerSideEventResolver=') < prepare.index('AttachToOriginalParent(actor)') < prepare.index('effect.Seek(0)')
    assert prepare.index('effect.AttachToOriginalParent(actor)') < prepare.index('effect.Seek(0)')
    assert 'if(deferEntryEvents||SourceRootFsmStartsAtEntry' in instance
    assert prepare.index('effect.Dispose()') < prepare.rindex('return false;')
    assert 'if(!NativeDeathKnightHeroPowerPlayback.TryGetPairForSummon(data,before,after,out pair)||!nativeEffects.HasDeathKnightRouteFor(pair))return false;' not in client
    assert 'TryGetPairForSummon(data,before,after,out pair)&&nativeEffects.HasDeathKnightRouteFor(pair)' in client
    assert 'NativeDeathKnightHeroPowerPlayback.HeroPowerId)' in presentation
    prepare_native = presentation.split('public void PrepareNative()', 1)[1].split('public void Draw(', 1)[0]
    assert prepare_native.index('Cues.Where(cue=>cue.data.type=="SpellEffect"') < prepare_native.index('foreach(var cue in ordered)')


def test_h11_fsm_timeline_sound_and_actor_restore_are_source_bound():
    root = Path(__file__).resolve().parents[1]
    route = (root / 'UnityClient/Assets/Scripts/NativeDeathKnightHeroPowerPlayback.cs').read_text(encoding='utf-8')
    instance = (root / 'UnityClient/Assets/Scripts/NativeEffectInstance.cs').read_text(encoding='utf-8')
    player = (root / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    editor = (root / 'UnityClient/Assets/Editor/NativeDeathKnightHeroPowerChecks.cs').read_text(encoding='utf-8')

    assert 'evt==friendly.text||evt==opponent.text' in instance
    assert 'clock.pending=evt;clock.pendingAt=when;SpellPlayerSideEventDispatches++' in instance
    assert 'cue.sourceParent=owner.transform' in route
    assert 'soundTrack.node!=20' in route and 'clip.start!=0' in route and 'clip.duration!=timeline.duration' in route
    assert 'tokenCue.file!=buttonCue.file' in route
    assert 'program.nodes[2].parent!=1||program.nodes[3].parent!=2' in route
    assert 'customSpawn.Restore();' in instance
    assert 'ReleaseTimelineControls();ReleaseBorrowedAnimations();RestoreBorrowedVisibility();customSpawn.Restore();' in instance
    assert 'CustomSpawn restores original Actor and Spell hierarchy on route disposal' in editor
    assert 'Timeline node 20 resolves its own SoundDef and binds the cue to its source parent' in editor
    assert 'deferred NativeEffectInstance construction executes no FSM actions before attachment' in editor
    assert 'first post-attachment seek dispatches exactly one source Friendly/Opponent side event' in editor
    assert 'var oldState=State(0,0,false);var newState=State(0,0,false);newState.events=Events(0);' in editor
    assert 'RunDeferredEntryChecks(root);' in editor
    assert 'Check(Event.current==null,"disabled H11 draw contract runs without a GUI Event");' in editor
    assert 'clock.vectors.TryGetValue(vector.variable,out vectorValue)' in instance
    assert 'SupportsPositionState(machine,"Friendly Position","FriendlyHP"' in route
    assert 'SupportsPositionState(machine,"Opponent Position","OpponentHP"' in route
    assert 'public static float PlaybackAge(float age)' in route
    assert 'TokenAudioFrames = 192634' in route and 'TokenAudioSampleRate = 48000' in route
    assert 'AudioSettings.GetDSPBufferSize(out bufferLength,out buffers)' in route
    assert 'IdentityThroughAge{get{return AudioCompleteAge+AudioGraceSeconds;}}' in route
    assert 'WavDurationSeconds(Path.Combine(assets.Root,tokenCue.file))' in route
    assert 'TokenAudioCue(NativeEffectInstance effect)' in route and 'TokenAudioPlaying(effect)' in player
    assert 'source.audio_sources[0].values.Loop' in route
    assert 'GetDeathKnightPlaybackAge(key,age,out tokenAudioPlaying)' in player
    assert 'effect.Seek(playbackAge)' in player and 'deathKnightSourceAges' in player
    assert 'RenderAffineExcludingSubtree(mappedAge,17,2.5f)' in player
    assert 'RenderAffineSubtree(mappedAge,17,1.25f)' in player
    assert 'RunPositionVectorChecks(root);' in editor
    assert 'source " + side.Item1 + " side state resolves its defined position vector' in editor
    assert 'unknown SetPosition vector names fail closed without moving the target' in editor


def test_h11_positive_editor_route_uses_real_cache_in_isolated_enabled_root():
    root = Path(__file__).resolve().parents[1]
    editor = (root / 'UnityClient/Assets/Editor/NativeDeathKnightHeroPowerChecks.cs').read_text(encoding='utf-8')
    production_index = json.loads((root / 'local/runtime/native-effect-index.json').read_text(encoding='utf-8'))
    production_entry = next(entry for entry in production_index['entries'] if entry['card_id'] == HERO_POWER_ID)

    assert production_entry['enabled'] and production_entry['duration'] == 5
    assert 'public static void RunEnabledRoute()' in editor
    assert '40ac8d77701fdbccc4033483.hero-power-source.json' in editor
    assert 'CopyPositiveRouteCache(sourceRoot,runtime,programFile,sourceProgram);' in editor
    assert '"enabled\\":true,\\"duration\\":5' in editor
    assert 'player.PrepareSource("h11-positive",cue,before,after,false,0)' in editor
    assert 'player.PrepareSource("h11-missing-pair",cue,before,missing,false,0)' in editor
    assert 'player.PrepareSource("h11-duplicate-pair",cue,before,duplicate,false,0)' in editor
    assert 'var sourceWait=actionState.actions.Single(action=>action.name=="Wait").Param("time").number;' in editor
    assert 'float sampleAge=sourceWait+1f/120;' in editor
    assert 'player.PrepareSource("h11-positive",cue,before,after,false,sampleAge)' in editor
    assert 'tokenCues[0].sourceParent==effect.Nodes[20].transform' in editor
    assert 'buttonCues[0].file==buttonFile' in editor and 'tokenCues[0].time==sourceWait' in editor

    player = (root / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    assert 'DeathKnightEffectDiagnostic(effect)' in player
    assert 'effect.UnhandledActionDetails.ToArray()' in player
    assert 'effect.UnboundTimelineSounds' in player and 'effect.UnsupportedTimelineTracks' in player
    assert 'effect.UnboundCustomSpawnActions' in player and 'effect.UnboundAudioActions' in player


@pytest.mark.parametrize('mutation, message', [
    (lambda p, s: s['effect_references'][0]['spell_components'][0].update(class_name='SuperSpell'),
     'OverrideCustomSpawnSpell'),
    (lambda p, s: p.update(reference='Changed.prefab'), 'program identity'),
    (lambda p, s: p['nodes'].__setitem__(3, dict(p['nodes'][3], parent=1)), 'node hierarchy'),
    (lambda p, s: p['machines'][0]['states'][5]['actions'][1]['params'][1].update(number=1),
     'TimelinePlay source parameters'),
    (lambda p, s: p['timelines'][0].update(duration=4), 'Timeline source binding'),
])
def test_h11_profile_rejects_changed_source(mutation, message):
    program, source = h11_fixture()
    mutation(program, source)
    with pytest.raises(ValueError, match=message):
        death_knight_source_profile(program, source)
