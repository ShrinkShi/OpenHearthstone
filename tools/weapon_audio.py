"""Recover weapon audio from the original Actor table and enabled FSM actions."""
import hashlib
import json

from .actor_spell_tables import read_table
from .additive_shader_audit import audit as additive_audit, render_states as shader_render_states
from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .inspect_compiled_shaders import programs as shader_programs, variant_bindings
from .native_audio import export_clip, recover
from .native_effects import identity, material as export_material, parameters, pointer_key


ACTOR = 'Card_Play_Weapon.prefab:71f767d4f10681a45ac853936d1db800'
TABLE = 'Card_Play_Weapon_SpellTable.prefab:9eb63fe4f82064b50b443e1b15a4825d'
SLOTS = {
    41: ('b142a0cfb212145268a4ec77aeb2912f', 'State 3', .8),
    42: ('eafae527ed9d24df3b7b34367d2361bc', 'State 3', .8),
    7: ('f5881061a688d4095b81ca3d7a8c9cb7', 'Birth State 1', .6),
    5: ('9583bd3b2ad5a4b9c90ed680d460a12e', 'Birth State 5', 1.),
}


def require(condition, message):
    if not condition:
        raise ValueError('Weapon audio source changed: ' + message)


def components(reader, root, name):
    result = []
    for item in root.read_typetree()['m_Component']:
        obj = reader.pointer(root, item['component'])
        if obj is None or obj.type.name != 'MonoBehaviour':
            continue
        tree = obj.read_typetree()
        script = reader.pointer(obj, tree['m_Script'])
        if script.read_typetree()['m_ClassName'] == name:
            result.append(obj)
    return result


def descendants(reader, root):
    yield root
    transforms = [reader.pointer(root, item['component']) for item in root.read_typetree()['m_Component']]
    transforms = [item for item in transforms if item is not None and item.type.name == 'Transform']
    require(len(transforms) == 1, 'summon visual transform')
    for pointer in transforms[0].read_typetree()['m_Children']:
        transform = reader.pointer(transforms[0], pointer)
        child = reader.pointer(transform, transform.read_typetree()['m_GameObject'])
        yield from descendants(reader, child)


def pair_value(rows, name):
    matches = [value for key, value in rows if key == name]
    require(len(matches) == 1, 'material property ' + name)
    return matches[0]


def named_value(rows, name):
    matches = [row['value'] for row in rows if row['name'] == name]
    require(len(matches) == 1, 'material property ' + name)
    return matches[0]


def constant_offsets(parsed):
    layouts = []
    for shader_pass in parsed['m_SubShaders'][0]['m_Passes']:
        names = {index: name for name, index in shader_pass['m_NameIndices']}
        layouts.append({stage: {
            names[buffer['m_NameIndex']]: {
                names[value['m_NameIndex']]: value['m_Index']
                for value in buffer['m_VectorParams']}
            for buffer in shader_pass[stage]['m_CommonParameters']['m_ConstantBuffers']}
            for stage in ('progVertex', 'progFragment')})
    return layouts


def parsed_shader_name(shader_tree):
    return shader_tree.get('m_ParsedForm', {}).get('m_Name', '')


def verify_additive_shader(shader, keywords):
    parsed = shader.read_typetree().get('m_ParsedForm', {})
    shader_name = parsed.get('m_Name', '')
    baseline = additive_audit()
    require(shader_name == baseline['shader']['name'], 'summon glow shader identity')
    require(baseline['family'] == 16, 'summon glow additive shader family')
    compiled = [program for platform, program in shader_programs(shader.read()) if platform == 4]
    require(len(compiled) == 1, 'single audited D3D shader table')
    program = compiled[0]
    parameter_hashes = {index: hashlib.sha256(raw).hexdigest()
                        for index, kind, raw in program.parameter_entries}
    bindings = variant_bindings(parsed, keywords)
    for binding in bindings:
        code = program.m_SubPrograms[binding['blob']]
        require(code is not None, 'compiled shader binding code')
        binding['program_sha256'] = hashlib.sha256(bytes(code.m_ProgramCode)).hexdigest()
        binding['parameter_sha256'] = parameter_hashes.get(binding['parameter_blob'])
        require(binding['parameter_sha256'] is not None, 'compiled shader binding parameters')
    states = shader_render_states(parsed)
    offsets = constant_offsets(parsed)
    require(bindings == baseline['bindings'], 'summon shader compiled bindings differ from additive audit')
    require(states == baseline['pass_render_states'], 'summon shader pass states differ from additive audit')
    require(offsets == baseline['pass_constant_byte_offsets'], 'summon shader constant offsets differ from additive audit')
    return dict(status='exact-audit-match', baseline_shader=baseline['shader'], family=baseline['family'],
                platform=4, keywords=keywords, bindings=bindings, pass_render_states=states,
                pass_constant_byte_offsets=offsets, full_card_effects_verified=False)


def object_parameter(reader, owner, data, action_index, name):
    start = data['actionStartIndex'][action_index]
    end = data['actionStartIndex'][action_index + 1] if action_index + 1 < len(data['actionStartIndex']) else len(data['paramName'])
    matches = [i for i in range(start, end) if data['paramName'][i] == name]
    require(len(matches) == 1, 'object parameter ' + name)
    i = matches[0]
    require(data['paramDataType'][i] == 24, 'object parameter type')
    value = data['fsmObjectParams'][data['paramDataPos'][i]]
    require(not value['useVariable'], 'dynamic object parameter')
    return reader.pointer(owner, value['value'])


def glow_delay(reader, root):
    clips = []
    def visit(go):
        for item in go.read_typetree()['m_Component']:
            obj = reader.pointer(go, item['component'])
            if obj is None:
                continue
            tree = obj.read_typetree()
            if obj.type.name == 'Animation':
                for ptr in tree.get('m_Animations', []):
                    clip = reader.pointer(obj, ptr)
                    if clip is not None and clip.read_typetree()['m_Name'] == 'AllyInPlay_SummonMinionGlowFadeIn':
                        clips.append(clip)
            if obj.type.name == 'Transform':
                for ptr in tree['m_Children']:
                    child = reader.pointer(obj, ptr)
                    visit(reader.pointer(child, child.read_typetree()['m_GameObject']))
    visit(root)
    require(len(clips) == 1, 'summon glow animation')
    clip = clips[0]
    tree = clip.read_typetree()
    require(not tree['m_Events'], 'summon glow has animation events')
    times = [key['time'] for field in ('m_PositionCurves', 'm_ScaleCurves', 'm_RotationCurves', 'm_EulerCurves', 'm_FloatCurves')
             for track in tree.get(field, []) for key in track['curve'].get('m_Curve', [])]
    return max(times), clip


def summon_glow(ex, reader, root, clip, slot, duration):
    expected = ('FX_Weapon_SummonGlowblue', 'WeaponSummonBlue') if slot == 41 else (
        'FX_Weapon_SummonGlowRed', 'WeaponSummonRed')
    quads = [go for go in descendants(reader, root) if go.read_typetree()['m_Name'] == 'Summon_quad']
    require(len(quads) == 1, 'single Summon_quad')
    quad = quads[0]
    renderers = [reader.pointer(quad, item['component']) for item in quad.read_typetree()['m_Component']]
    renderers = [item for item in renderers if item is not None and item.type.name == 'MeshRenderer']
    require(len(renderers) == 1, 'single summon glow renderer')
    renderer = renderers[0]
    renderer_tree = renderer.read_typetree()
    require(not renderer_tree['m_Enabled'] and len(renderer_tree['m_Materials']) == 1,
            'summon glow starts disabled with one material')
    material = reader.pointer(renderer, renderer_tree['m_Materials'][0])
    require(material is not None, 'summon glow material')
    material_tree = material.read_typetree()
    require(material_tree['m_Name'] == expected[0], 'side-specific summon glow material')
    shader = reader.pointer(material, material_tree['m_Shader'])
    require(shader is not None, 'summon glow shader')
    shader_tree = shader.read_typetree()
    shader_name = parsed_shader_name(shader_tree)
    raw_keywords = material_tree.get('m_ValidKeywords', material_tree.get('m_ShaderKeywords', []))
    keywords = sorted(raw_keywords.split() if isinstance(raw_keywords, str) else raw_keywords)
    shader_verification = verify_additive_shader(shader, keywords)
    texture_pointer = pair_value(material_tree['m_SavedProperties']['m_TexEnvs'], '_MainTex')['m_Texture']
    texture = reader.pointer(material, texture_pointer)
    texture_tree = texture.read_typetree() if texture is not None else {}
    require(texture is not None and texture_tree['m_Name'] == expected[1], 'side-specific glow texture')
    image = texture.read().image.convert('RGBA')
    alpha_min, alpha_max = image.getchannel('A').getextrema()
    texture_alpha_opaque = alpha_min == alpha_max == 255
    require(texture_alpha_opaque, 'additive summon glow texture alpha')
    texture_file = ex.texture(texture)
    tint = pair_value(material_tree['m_SavedProperties']['m_Colors'], '_TintColor')
    material_errors = []
    material_definition = export_material(ex, material, material_errors)
    require(not material_errors and material_definition['shader'] == shader_name and
            material_definition['keywords'] == keywords, 'summon material source definition')
    require((material_definition['src'], material_definition['dst'], material_definition['src_alpha'],
             material_definition['dst_alpha']) == (5, 1, 5, 1), 'summon material blend factors')
    main_textures = [row for row in material_definition['textures'] if row['name'] == '_MainTex']
    require(len(main_textures) == 1 and main_textures[0]['path'] == texture_file and
            main_textures[0]['scale'] == {'x': 1.0, 'y': 1.0} and
            main_textures[0]['offset'] == {'x': 0.0, 'y': 0.0}, 'summon MainTex transform')
    require(all(named_value(material_definition['floats'], name) == 0
                for name in ('_LightingBlend', '_GlowIntensity', '_GlowAddMul')),
            'summon material lighting and glow controls')
    material_tint = named_value(material_definition['colors'], '_TintColor')
    require(abs(material_tint['a'] - tint['a']) < 1e-6, 'summon material tint alpha')

    clip_tree = clip.read_typetree()
    curves = [row for row in clip_tree['m_FloatCurves'] if row['path'] == '']
    by_attribute = {row['attribute']: row for row in curves}
    require(len(curves) == 4 and set(by_attribute) == {'_TintColor.r', '_TintColor.g', '_TintColor.b', 'm_Enabled'},
            'summon glow animation bindings')
    enabled = by_attribute['m_Enabled']
    enabled_keys = enabled['curve']['m_Curve']
    require(enabled['classID'] == 23 and len(enabled_keys) == 2 and
            all(key['value'] == 1 and key['time'] in (0, duration) for key in enabled_keys),
            'summon renderer enable curve')
    color_curves = {}
    for channel in 'rgb':
        row = by_attribute['_TintColor.' + channel]
        keys = row['curve']['m_Curve']
        require(row['classID'] == 21 and len(keys) == 4 and
                abs(max(key['time'] for key in keys) - duration) < 1e-6,
                'summon tint curve ' + channel)
        color_curves[channel] = [{key: value for key, value in item.items()
                                  if key in ('time', 'value', 'inSlope', 'outSlope')}
                                 for item in keys]

    transform = next(reader.pointer(quad, item['component']) for item in quad.read_typetree()['m_Component']
                     if reader.pointer(quad, item['component']).type.name == 'Transform').read_typetree()
    return dict(profile='source-additive-tint-v2', texture_name=texture_tree['m_Name'], texture_file=texture_file,
                texture_source=identity(ex, texture), material_source=identity(ex, material),
                renderer_source=identity(ex, renderer), material=material_tree['m_Name'],
                shader_known=shader_verification['status'] == 'exact-audit-match', shader_name=shader_name,
                shader_source=identity(ex, shader), compiled_source_verified=True,
                shader_verification=shader_verification, material_definition=material_definition,
                texture_alpha_opaque=texture_alpha_opaque,
                texture_alpha=dict(min=alpha_min, max=alpha_max, pixels=image.width * image.height),
                duration=duration,
                tint_alpha=tint['a'], tint_curves=color_curves,
                transform={'position': transform['m_LocalPosition'], 'scale': transform['m_LocalScale']})


def export(ex):
    reader = ex.imp.reader
    root, _, _ = reader.resolve(ACTOR)
    actors = components(reader, root, 'Actor')
    require(len(actors) == 1 and actors[0].read_typetree()['m_spellTablePrefab'] == TABLE, 'Actor table binding')
    card = reader.definition('CS2_082')
    require(not card['m_SpellTableOverrides'] and not card['m_CustomSpawnSpellPath'] and
            not card['m_CustomDeathSpellPath'] and not card['m_PlayEffectDef']['m_SpellPath'], 'dagger overrides')
    table = read_table(ex, TABLE)
    result = dict(actor_reference=ACTOR, table_reference=TABLE, actor_source=identity(ex, actors[0]),
                  table_source=table['source'], profile='source-fsm-audio-and-summon-glow-v1', entries=[],
                  audio_scope={'card_type': 'WEAPON', 'trigger': 'first true Summon for the equipped weapon entity',
                               'friendly_slot': 41, 'opponent_slot': 42,
                               'damage_slot': 7, 'death_slot': 5},
                  limitations=['The summon glow uses the exact locally audited Hero/Additive/Additive shader and source material; the wider Actor/Spell lifecycle remains unresolved.'])
    for slot, (guid, state_name, volume) in SLOTS.items():
        row = next(item for item in table['entries'] if item['type'] == slot)
        require(row['reference'].endswith(':' + guid) and row['inline_source'] is None, 'slot reference')
        spell, _, _ = reader.resolve(row['reference'])
        fsms = components(reader, spell, 'PlayMakerFSM')
        require(len(fsms) == 1, 'single root FSM')
        owner = fsms[0]
        fsm = owner.read_typetree()['fsm']
        states = {state['name']: state for state in fsm['states']}
        birth = next(item['toState'] for item in fsm['globalTransitions'] if item['fsmEvent']['name'] == 'Birth')
        if slot in (41, 42):
            require(birth == 'State 2' and states[birth]['transitions'][0]['toState'] == state_name, 'summon entry')
            entry_data = states[birth]['actionData']
            animations = [i for i, name in enumerate(entry_data['actionNames'])
                          if name.endswith('.AnimationPlaythroughAction') and entry_data['actionEnabled'][i]]
            require(len(animations) == 1, 'summon entry animation action')
            animation_params = parameters(entry_data, animations[0], owner, reader, {}, {}, fsm.get('dataVersion', 0))
            animation_values = {param['name']: param for param in animation_params}
            require(animation_values['m_AnimName']['text'] == 'AllyInPlay_SummonMinionGlowFadeIn' and
                    animation_values['m_CrossFadeSec']['number'] == 0, 'summon audio preceding animation')
        elif slot == 5:
            require(birth == 'State 3' and any(t['fsmEvent']['name'] == 'NotLegendary' and t['toState'] == state_name
                                             for t in states[birth]['transitions']), 'nonlegendary death entry')
        else:
            require(birth == state_name, 'damage entry')
        state = states[state_name]
        data = state['actionData']
        indexes = [i for i, name in enumerate(data['actionNames']) if 'AudioPlay' in name and data['actionEnabled'][i]]
        require(len(indexes) == 1, 'single enabled audio action')
        index = indexes[0]
        action = data['actionNames'][index].rsplit('.', 1)[-1]
        require(action == ('AudioPlayAction' if slot == 7 else 'AudioPlaythroughAction'), 'audio action')
        values = parameters(data, index, owner, reader, {pointer_key(spell): 0}, {}, fsm.get('dataVersion', 0))
        params = {param['name']: param for param in values}
        require(abs(params['m_VolumeScale']['number'] - volume) < 1e-6 and params['m_Delay']['number'] == 0,
                'audio volume or delay')
        require(params['m_GameObject']['node'] == 0, 'audio receiver must be source root')
        sound = object_parameter(reader, owner, data, index, 'm_OneShotSound')
        override = None
        if slot == 7:
            require(sound is None, 'damage uses receiver SoundDef')
        else:
            require(sound is not None, 'explicit SoundDef required')
            tree = sound.read_typetree()
            require(not tree['m_RandomClips'] and bool(tree['m_AudioClip']), 'single explicit clip')
            direct = object_parameter(reader, owner, data, index, 'm_OneShotClip')
            resolved, _, _ = reader.resolve(tree['m_AudioClip'])
            require(pointer_key(direct) == pointer_key(resolved), 'legacy clip and SoundDef disagree')
            override = dict(source=identity(ex, sound), values=tree, clips=[export_clip(ex, tree['m_AudioClip'])])
        definition = recover(ex, row['reference'])
        require(not definition['errors'] and len(definition['nodes']) == 1, 'audio receiver inventory')
        receiver = definition['nodes'][0]
        require(receiver['path'] == '' and len(receiver['audio_sources']) == 1 and len(receiver['sound_defs']) == 1,
                'root audio receiver')
        animation_duration, clip = glow_delay(reader, spell) if slot in (41, 42) else (0, None)
        entry = dict(slot=slot, reference=row['reference'], source=identity(ex, spell), fsm_source=identity(ex, owner),
                     state=state_name, action=action, volume=volume, delay=0,
                     definition=definition, sound=override)
        if clip is not None:
            entry['preceding_animation_source'] = identity(ex, clip)
            entry['preceding_animation_duration'] = animation_duration
            entry['timing_note'] = 'AnimationPlaythroughAction.OnEnter calls Finish immediately after StartAnimation; duration is not an audio delay.'
            entry['summon_glow'] = summon_glow(ex, reader, spell, clip, slot, animation_duration)
        result['entries'].append(entry)
    atomic_json(ex.folder / 'weapon-audio.json', result)
    atomic_json(PROJECT / 'manifests/original-weapon-audio.json', result)
    return result


if __name__ == '__main__':
    result = export(RuntimeExport())
    print(json.dumps({'slots': [row['slot'] for row in result['entries']], 'profile': result['profile']}))
