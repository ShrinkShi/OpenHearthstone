"""Build client-readable SuperSpell catalogs for selected base hero powers."""
import argparse
import json
import math

from .asset_lock import asset_lock, atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_audio import bind_program, recover as recover_audio
from .native_effects import export_program, identity, keys as source_curve_keys
from .smoke_source_textures import recover_texture
from .super_spell_source import recover as recover_root
from .super_start_source import recover as recover_start
from .death_knight_hero_power import import_death_knight_source
from .shaman_hero_power import RUNTIME_DURATION, import_shaman_source


PRIEST_ROOT = 'Holy_Targeted_PriestHeroPower_FX.prefab:9a1243a1e217073439fc02475eba1605'
PRIEST_START_PATHID = '2826725205073506016'
PRIEST_IMPACT_PATHID = '-8365228841793198537'
PALADIN_ROOT = 'Holy_Untargeted_FX.prefab:9abe2b276b167a9459cc37d1d50b1660'
PALADIN_AREA = 'ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10'
PALADIN_AREA_PATHID = '8695582295512710279'
ROGUE_ROOT = 'Stealth_Untargeted_AutoSource_FX.prefab:5d1773f0c2f5ec743aba5d24e2d42557'
ROGUE_PROFILE = 'source-root-only-reconstruction; enabled-null-chain-unresolved'
DEMON_HUNTER_ROOT = 'ReuseFX_BuffImpact_DemonHunter_Small_Super.prefab:7d6b21e193de5cc44a280f817f088120'
DEMON_HUNTER_START = 'ReuseFX_Idle_DemonHunter_Small.prefab:52ef5d6ab84e8d947ad46489243e4e9a'
DEMON_HUNTER_RUNTIME_PROFILE = 'source-backed HERO_10bp self-hero Buff route; no-History Impact is immediate per source IL'
DEMON_HUNTER_PARTICLE_NODES = (7, 8, 10, 18, 19, 21, 27, 28, 29, 30, 32, 38, 39)
HUNTER_IMPACT = 'ReuseFX_Arrow_Impact_01.prefab:1c032452d358567449b65ff342bb87ce'
HUNTER_IMPACT_PATHID = '-8397184237054250488'
WARRIOR_IMPACT = 'ReuseFX_Generic_Untargeted_Cast.prefab:0d4da539450290e41b035f93bcd61cfc'
WARRIOR_IMPACT_PATHID = '3606052582550222334'
WARRIOR_IMPACT_FSM_PATHID = '-348367845664475650'
WARRIOR_IMPACT_CAMERA_SHAKER_PATHID = '8622794710674454014'
DEMON_HUNTER_FSMS = {
    1: {
        'events': [('Birth', 'Get Target'), ('Idle', 'Idle'), ('None', 'NoneState'),
                   ('Action', 'ActionState'), ('Death', 'Death'), ('Cancel', 'Cancel')],
        'states': {
            'StartState': ([], []),
            'NoneState': (['SpellStateAction', 'ActivateGameObject'], []),
            'ActionState': (['SpellStateAction', 'SpellFinishAction'], [('FINISHED', 'Death')]),
            'Init FX': (['SpellStateAction', 'ActivateGameObject', 'ParticlePlayAction', 'ParticlePlayAction',
                         'AudioPlayClipAction', 'Wait'], [('FINISHED', 'Idle')]),
            'Death': (['SpellStateAction', 'SpellFinishAction', 'ParticleStopAction', 'ParticleStopAction',
                       'AudioVolumeFadeAction', 'Wait'], [('FINISHED', 'NoneState')]),
            'Cancel': (['SpellStateAction', 'SpellFinishAction', 'ParticleStopAction', 'ParticleStopAction',
                        'AudioVolumeFadeAction', 'AudioPlayClipAction', 'Wait'], [('FINISHED', 'NoneState')]),
            'Idle': (['SpellStateAction', 'AudioVolumeFadeAction', 'AudioSetVolumeAction', 'AudioPlaythroughAction'], []),
            'Get Target': (['SpellStateAction', 'SpellCardTypeAction'],
                           [('Minion', 'Minion'), ('Hero', 'Init FX'), ('Weapon', 'Weapon/HP')]),
            'Minion': (['SetScale', 'SetScale'], [('FINISHED', 'Init FX')]),
            'Weapon/HP': (['SetScale', 'SetScale'], [('FINISHED', 'Init FX')]),
        },
    },
    12: {
        'events': [('Death', 'Get Target'), ('None', 'NoneState')],
        'states': {
            'StartState': ([], []),
            'Spell Finish': (['SpellFinishAction', 'AudioPlayClipAction', 'Wait'], [('FINISHED', 'NoneState')]),
            'NoneState': (['SpellStateAction'], []),
            'Get Target': (['SpellStateAction', 'SpellCardTypeAction'],
                           [('Minion', 'Minion'), ('Hero', 'Hero'), ('Weapon', 'Weapon/HP')]),
            'Minion': (['SetVisibilityRecursiveAction', 'AnimatorPlayAction', 'ActivateGameObject', 'ParticlePlayAction'],
                       [('FINISHED', 'Spell Finish')]),
            'Hero': (['SetVisibilityRecursiveAction', 'SetMaterialTexture', 'SetMaterialTexture', 'SetMaterialTexture',
                      'AnimatorPlayAction', 'ActivateGameObject', 'ParticlePlayAction'], [('FINISHED', 'Spell Finish')]),
            'Weapon/HP': (['SetScale', 'SetVisibilityRecursiveAction', 'AnimatorPlayAction', 'ActivateGameObject',
                           'ParticlePlayAction'], [('FINISHED', 'Spell Finish')]),
        },
    },
}


def hunter_impact_camera_shake_profile(program):
    """Keep H05's delayed StartShake tied to its authored impact root component."""
    if program.get('reference') != HUNTER_IMPACT or len(program.get('nodes', [])) != 21 or \
       str((program.get('source') or {}).get('pathid')) != HUNTER_IMPACT_PATHID or \
       sorted(program.get('unknown_actions', [])) != ['AudioPlayRandomClipAction', 'InvokeMethod']:
        raise ValueError('HERO_05bp Impact identity or source action inventory changed')
    machines = [row for row in program.get('machines', [])
                if row.get('owner_known') and row.get('owner') == 0]
    if len(machines) != 1 or machines[0].get('start') != 'State 1':
        raise ValueError('HERO_05bp Impact requires the reviewed owner-0 FSM')
    states = {row.get('name'): row for row in machines[0].get('states', [])}
    impact = states.get('DeathState 2') or {}
    actions = impact.get('actions') or []
    if [row.get('name') for row in actions] != [
            'SpellFinishAction', 'Wait', 'ParticlePlayAction', 'ParticlePlayAction',
            'ParticlePlayAction', 'ParticlePlayAction', 'ParticlePlayAction',
            'InvokeMethod', 'AnimateFloat', 'SetMaterialFloat', 'AudioPlayRandomClipAction'] or \
       [(row.get('event'), row.get('state')) for row in impact.get('transitions', [])] != [
            ('FINISHED', 'DeathState 3')]:
        raise ValueError('HERO_05bp Impact DeathState 2 lifecycle changed')
    invoke = actions[7]
    params = invoke.get('params') or []
    names = ['gameObject', 'behaviour', 'methodName', 'delay', 'repeating', 'repeatDelay', 'cancelOnExit']
    values = {row.get('name'): row for row in params}
    if invoke.get('name') != 'InvokeMethod' or [row.get('name') for row in params] != names or \
       any(row.get('unresolved') for row in params) or \
       values['gameObject'].get('kind') != 20 or values['gameObject'].get('node') != 0 or \
       values['behaviour'].get('text') != 'CameraShaker' or values['methodName'].get('text') != 'StartShake' or \
       not math.isclose(values['delay'].get('number', -1), .15, abs_tol=1e-6) or \
       values['repeating'].get('number') != 0 or values['repeatDelay'].get('number') != 1 or \
       values['cancelOnExit'].get('number') != 0:
        raise ValueError('HERO_05bp Impact InvokeMethod source profile changed')
    shaker = (program['nodes'][0].get('camera_shaker') or {})
    curve = shaker.get('curve') or []
    if not shaker.get('restored') or shaker.get('source', {}).get('pathid') != '-5851296759909699064' or \
       any(not math.isclose(shaker.get('amount', {}).get(axis, math.nan), .2, abs_tol=1e-6)
           for axis in ('x', 'y', 'z')) or len(curve) != 2 or \
       not math.isclose(curve[0].get('time', -1), 0, abs_tol=1e-6) or \
       not math.isclose(curve[0].get('value', math.nan), 1, abs_tol=1e-6) or \
       not math.isclose(curve[1].get('time', -1), .25, abs_tol=1e-6) or \
       not math.isclose(curve[1].get('value', math.nan), 0, abs_tol=1e-6) or \
       shaker.get('hold') or shaker.get('is_override') or \
       not math.isclose(shaker.get('hold_at_sec', -1), 0, abs_tol=1e-6):
        raise ValueError('HERO_05bp Impact CameraShaker source values changed')
    return 'source-impact-root-use-owner; delayed-one-shot-camera-shake'


def warrior_impact_camera_shake_profile(program):
    """Bind H01 Impact's delayed StartShake to its reviewed node-0 component."""
    if program.get('reference') != WARRIOR_IMPACT or \
       str((program.get('source') or {}).get('pathid')) != WARRIOR_IMPACT_PATHID or \
       len(program.get('nodes', [])) != 7 or \
       sorted(program.get('unknown_actions', [])) != ['AudioPlayClipAction', 'InvokeMethod']:
        raise ValueError('HERO_01bp Impact identity or source action inventory changed')
    machines = program.get('machines', [])
    if len(machines) != 1 or not machines[0].get('owner_known') or \
       machines[0].get('owner') != 0 or machines[0].get('start') != 'State 1' or \
       str((machines[0].get('source') or {}).get('pathid')) != WARRIOR_IMPACT_FSM_PATHID:
        raise ValueError('HERO_01bp Impact requires the reviewed owner-0 FSM')
    machine = machines[0]
    expected_actions = {
        'State 1': [],
        'DeathState': ['SpellStateAction', 'ParticlePlayAction', 'ParticlePlayAction',
                       'Wait', 'AudioPlayClipAction'],
        'NoneState': ['SpellStateAction'],
        'DeathState3': ['SpellFinishAction', 'Wait'],
        'DeathState2': ['ParticlePlayAction', 'ParticlePlayAction', 'ParticlePlayAction',
                        'InvokeMethod'],
    }
    states = {row.get('name'): row for row in machine.get('states', [])}
    if set(states) != set(expected_actions):
        raise ValueError('HERO_01bp Impact FSM state graph changed')
    expected_transitions = {
        'State 1': [], 'DeathState': [('FINISHED', 'DeathState2')], 'NoneState': [],
        'DeathState3': [('FINISHED', 'NoneState')],
        'DeathState2': [('FINISHED', 'DeathState3')],
    }
    for name, actions in expected_actions.items():
        state = states[name]
        if [row.get('name') for row in state.get('actions', [])] != actions or \
           [(row.get('event'), row.get('state')) for row in state.get('transitions', [])] != expected_transitions[name]:
            raise ValueError('HERO_01bp Impact FSM profile changed: ' + name)
    events = sorted((row.get('event'), row.get('state')) for row in machine.get('events', []))
    if events != [('Death', 'DeathState'), ('None', 'NoneState')]:
        raise ValueError('HERO_01bp Impact FSM lifecycle changed')

    invoke = states['DeathState2']['actions'][3]
    params = invoke.get('params') or []
    names = ['gameObject', 'behaviour', 'methodName', 'delay', 'repeating', 'repeatDelay', 'cancelOnExit']
    values = {row.get('name'): row for row in params}
    if invoke.get('name') != 'InvokeMethod' or [row.get('name') for row in params] != names or \
       any(row.get('unresolved') for row in params) or \
       values['gameObject'].get('kind') != 20 or values['gameObject'].get('node') != 0 or \
       values['behaviour'].get('text') != 'CameraShaker' or values['methodName'].get('text') != 'StartShake' or \
       not math.isclose(values['delay'].get('number', -1), .1, abs_tol=1e-6) or \
       values['repeating'].get('number') != 0 or values['repeatDelay'].get('number') != 1 or \
       values['cancelOnExit'].get('number') != 0:
        raise ValueError('HERO_01bp Impact InvokeMethod source profile changed')

    shakers = [(index, node.get('camera_shaker')) for index, node in enumerate(program['nodes'])
               if node.get('camera_shaker')]
    if len(shakers) != 1 or shakers[0][0] != 0:
        raise ValueError('HERO_01bp Impact requires one CameraShaker on node 0 only')
    shaker = shakers[0][1]
    curve = shaker.get('curve') or []
    if not shaker.get('restored') or str((shaker.get('source') or {}).get('pathid')) != WARRIOR_IMPACT_CAMERA_SHAKER_PATHID or \
       any(not math.isclose(shaker.get('amount', {}).get(axis, math.nan), .1, abs_tol=1e-6)
           for axis in ('x', 'y', 'z')) or len(curve) != 2 or \
       not math.isclose(curve[0].get('time', -1), 0, abs_tol=1e-6) or \
       not math.isclose(curve[0].get('value', math.nan), 1, abs_tol=1e-6) or \
       not math.isclose(curve[1].get('time', -1), .6, abs_tol=1e-6) or \
       not math.isclose(curve[1].get('value', math.nan), 0, abs_tol=1e-6) or \
       shaker.get('hold') or shaker.get('is_override') or \
       not math.isclose(shaker.get('hold_at_sec', -1), 0, abs_tol=1e-6):
        raise ValueError('HERO_01bp Impact CameraShaker source values changed')
    return 'source-impact-root-use-owner; delayed-one-shot-camera-shake'


def _warrior_impact_camera_shaker_source(ex, reference):
    """Preflight the source component before enabling its node export."""
    if reference != WARRIOR_IMPACT:
        raise ValueError('HERO_01bp Impact reference changed')
    root, _, _ = ex.imp.reader.resolve(reference)
    if root.type.name != 'GameObject' or str(root.path_id) != WARRIOR_IMPACT_PATHID:
        raise ValueError('HERO_01bp Impact source root changed')
    errors = []
    objects = ex.imp.reader.walk(root, include_visual=True, max_nodes=4000, errors=errors)
    if errors:
        raise ValueError('HERO_01bp Impact component preflight could not resolve its source graph')
    found = []
    for obj, _ in objects:
        if obj.type.name != 'GameObject':
            continue
        for row in obj.read_typetree().get('m_Component', []):
            component = ex.imp.reader.pointer(obj, row['component'])
            if component is None or component.type.name != 'MonoBehaviour':
                continue
            component_tree = component.read_typetree()
            script = ex.imp.reader.pointer(component, component_tree.get('m_Script', {}))
            if script is not None and script.read_typetree().get('m_ClassName') == 'CameraShaker':
                found.append((obj, component, component_tree))
    if len(found) != 1 or found[0][0].path_id != root.path_id or \
       str(found[0][1].path_id) != WARRIOR_IMPACT_CAMERA_SHAKER_PATHID:
        raise ValueError('HERO_01bp Impact CameraShaker source component changed')
    _, component, values = found[0]
    amount = values.get('m_Amount') or {}
    intensity = values.get('m_IntensityCurve') or {}
    source_keys = intensity.get('m_Curve', [])
    numeric_fields = ('time', 'value', 'inSlope', 'outSlope', 'inWeight', 'outWeight')
    if not isinstance(source_keys, list) or any(
        not isinstance(key, dict) or any(not isinstance(key.get(field), (int, float)) or
                                         not math.isfinite(key[field]) for field in numeric_fields)
        for key in source_keys):
        raise ValueError('HERO_01bp Impact CameraShaker source curve is incomplete')
    hold, hold_at, override = values.get('m_Hold'), values.get('m_HoldAtSec'), values.get('m_IsOverride')
    if any(not isinstance(amount.get(axis), (int, float)) or not math.isfinite(amount[axis])
           for axis in ('x', 'y', 'z')) or hold not in (0, 1, False, True) or \
       not isinstance(hold_at, (int, float)) or not math.isfinite(hold_at) or \
       override not in (0, 1, False, True):
        raise ValueError('HERO_01bp Impact CameraShaker source parameters are incomplete')
    return dict(restored=True, amount=amount, curve=source_curve_keys(intensity), hold=bool(hold),
                hold_at_sec=hold_at, is_override=bool(override), source=identity(ex, component))


def rogue_source_profile(program, config):
    """Review the embedded H03 Impact FSM without executing its parent Chain."""
    nodes = program.get('nodes', [])
    if program.get('reference') != ROGUE_ROOT or len(nodes) != 3 or \
       nodes[1].get('parent') != -1 or nodes[2].get('parent') != 1 or \
       config.get('card_id') != 'HERO_03bp' or config.get('reference') != ROGUE_ROOT or \
       config.get('location') != 2 or config.get('target_behavior') != 6:
        raise ValueError('HERO_03bp source identity or target profile changed')
    if config.get('start_enabled') or not config.get('start_config_null') or config.get('start') is not None:
        raise ValueError('HERO_03bp Start must remain disabled/null')
    missile = config.get('missile') or {}
    if missile.get('m_Enabled') != 0 or config.get('missile_reference') or config.get('reverse_available'):
        raise ValueError('HERO_03bp Missile must remain disabled/null')
    impact = config.get('impact') or {}
    prefab = impact.get('prefab') or {}
    if not impact.get('enabled') or prefab.get('prefab_null') or \
       (prefab.get('prefab_source') or {}).get('pathid') != '-9149849810296163763' or \
       not impact.get('use_super_location') or impact.get('game_delay_min') != 0 or \
       impact.get('game_delay_max') != 0 or impact.get('ranges') != []:
        raise ValueError('HERO_03bp embedded Impact source profile changed')
    for slot in ('friendly', 'opponent'):
        area = (config.get('area') or {}).get(slot) or {}
        if not area.get('enabled') or not area.get('prefab_null') or area.get('prefab_reference'):
            raise ValueError('HERO_03bp ' + slot + ' area must remain enabled/null')
    if not config.get('chain_enabled') or not config.get('chain_prefab_null') or \
       config.get('chain_reference') or config.get('chain_available'):
        raise ValueError('HERO_03bp Chain must remain enabled/null and unresolved')
    if set(program.get('unknown_actions', [])) - {'AudioPlayRandomClipAction'}:
        raise ValueError('HERO_03bp unsupported source action')
    machines = program.get('machines', [])
    if len(machines) != 1 or not machines[0].get('owner_known') or machines[0].get('owner') != 1:
        raise ValueError('HERO_03bp requires one embedded owner-1 FSM')
    machine = machines[0]
    states = {state['name']: state for state in machine['states']}
    if machine.get('start') != 'State 1' or len(states) != 3 or \
       machine.get('events') != [{'event': 'Death', 'state': 'Birth State'}, {'event': 'None', 'state': 'NoneState'}] or \
       set(states) != {'State 1', 'Birth State', 'NoneState'}:
        raise ValueError('HERO_03bp lifecycle changed')
    play = states['Birth State']['actions']
    if [a['name'] for a in play] != ['SpellStateAction', 'ParticlePlayAction',
                                    'AudioPlayRandomClipAction', 'SpellFinishAction', 'Wait'] or \
       states['Birth State']['transitions'] != [{'event': 'FINISHED', 'state': 'NoneState'}] or \
       states['State 1']['actions'] or states['State 1']['transitions'] or \
       [a['name'] for a in states['NoneState']['actions']] != ['SpellStateAction'] or states['NoneState']['transitions']:
        raise ValueError('HERO_03bp action graph changed')
    for state in machine['states']:
        for action in state['actions']:
            if any(p.get('unresolved') or p.get('variable') for p in action['params']):
                raise ValueError('HERO_03bp unresolved source parameter')
    params = lambda action: {p['name']: p for p in action['params'] if p['name']}
    if params(play[0])['m_SpellObject'].get('node') != 1 or \
       params(states['NoneState']['actions'][0])['m_SpellObject'].get('node') != 1 or \
       params(play[1])['m_GameObject'].get('node') != 2 or params(play[1])['m_IncludeChildren'].get('number') != 0 or \
       not (program['nodes'][2].get('particle') or {}).get('restored') or \
       params(play[3])['m_SpellObject'].get('node') != 1 or params(play[3])['m_Delay'].get('number') != 0 or \
       params(play[4])['time'].get('number') != 1 or params(play[4])['realTime'].get('number') != 0 or \
       params(play[4])['finishEvent'].get('text') != 'FINISHED':
        raise ValueError('HERO_03bp particle or lifecycle parameter changed')
    audio = params(play[2])
    array = play[2]['params']
    sounds = next(i for i, p in enumerate(array) if p['name'] == 'm_Sounds')
    weights = next(i for i, p in enumerate(array) if p['name'] == 'm_Weights')
    if audio['m_ParentObject'].get('node') != 1 or audio['m_Sounds'].get('array_count') != 1 or \
       audio['m_Weights'].get('array_count') != 1 or array[sounds + 1].get('asset_path') != 'd7861ddcda8550b8d2114840.native-sound.json' or \
       array[weights + 1].get('number') != 1 or not audio['m_TemplateSource'].get('object_null') or \
       any(audio[name].get('number') != value for name, value in {
           'm_MinVolume': 1, 'm_MaxVolume': 1, 'm_MinPitch': 1, 'm_MaxPitch': 1,
           'm_SpatialBlend': 1, 'm_Category': 1, 'm_Delay': 0, 'm_InstanceLimited': 0,
           'm_InstanceLimitedDuration': 0, 'm_InstanceLimitMaximum': 1, 'm_LimitMaxOutOption': 0,
       }.items()):
        raise ValueError('HERO_03bp single-candidate source audio changed')
    return ROGUE_PROFILE


def paladin_source_profile(program, config):
    """Pin H04's authored branches while keeping its unresolved Chain enabled-null."""
    if program.get('reference') != PALADIN_ROOT or len(program.get('nodes', [])) != 13:
        raise ValueError('HERO_04bp root identity or node graph changed')
    if config.get('card_id') != 'HERO_04bp' or config.get('location') != 3 or \
       config.get('target_behavior') != 6:
        raise ValueError('HERO_04bp target/location source profile changed')
    if config.get('start_enabled') or not config.get('start_config_null') or config.get('start') is not None:
        raise ValueError('HERO_04bp Start must remain authored-disabled/null')
    missile = config.get('missile') or {}
    if missile.get('m_Enabled') != 0 or config.get('missile_reference') or \
       missile.get('m_Prefab') != {'m_FileID': 0, 'm_PathID': 0} or \
       missile.get('m_ReversePrefab') != {'m_FileID': 0, 'm_PathID': 0}:
        raise ValueError('HERO_04bp Missile must remain authored-disabled/null')
    impact = config.get('impact') or {}
    prefab = impact.get('prefab') or {}
    if impact.get('enabled') or not prefab.get('prefab_null') or prefab.get('prefab_reference') or \
       impact.get('ranges') != [] or impact.get('game_delay_min') != .5 or impact.get('game_delay_max') != .5:
        raise ValueError('HERO_04bp Impact must remain authored-disabled/null')
    friendly = (config.get('area') or {}).get('friendly') or {}
    if not friendly.get('enabled') or friendly.get('prefab_null') or \
       friendly.get('prefab_reference') != PALADIN_AREA or \
       (friendly.get('prefab_source') or {}).get('pathid') != PALADIN_AREA_PATHID or \
       not friendly.get('use_super_location') or friendly.get('location') != 0 or \
       friendly.get('parent_to_location') or friendly.get('facing') != 0 or \
       friendly.get('delay_min') != 0 or friendly.get('delay_max') != 0:
        raise ValueError('HERO_04bp FriendlyArea source profile changed')
    opponent = (config.get('area') or {}).get('opponent') or {}
    if not opponent.get('enabled') or not opponent.get('prefab_null') or opponent.get('prefab_reference'):
        raise ValueError('HERO_04bp OpponentArea must remain authored-enabled/null')
    if not config.get('chain_enabled') or not config.get('chain_prefab_null') or \
       config.get('chain_reference') or config.get('chain_available'):
        raise ValueError('HERO_04bp enabled-null Chain source state changed')
    return 'friendly-area-only-reconstruction; enabled-null-chain-unresolved'


def priest_source_profile(program, config):
    """Pin H09's targeted Start + Impact source; the disabled Missile stays null."""
    if program.get('reference') != PRIEST_ROOT or len(program.get('nodes', [])) != 22:
        raise ValueError('HERO_09bp root identity or node graph changed')
    if config.get('card_id') != 'HERO_09bp' or config.get('location') != 2 or config.get('target_behavior') != 0:
        raise ValueError('HERO_09bp is no longer the reviewed targeted source route')
    start = config.get('start') or {}
    if not config.get('start_enabled') or config.get('start_config_null') or \
       not start.get('enabled') or start.get('prefab_null') or not start.get('prefab_reference') or \
       (start.get('prefab_source') or {}).get('pathid') != PRIEST_START_PATHID or \
       not start.get('use_super_location') or not start.get('death_after_missiles') or \
       start.get('adjust_rotation') or start.get('rotation') != {'x': 0.0, 'y': 0.0, 'z': 0.0}:
        raise ValueError('HERO_09bp Start source profile changed')
    raw_missile = config.get('missile') or {}
    if raw_missile.get('m_Enabled') != 0 or config.get('missile_reference') or \
       raw_missile.get('m_Prefab') != {'m_FileID': 0, 'm_PathID': 0} or \
       raw_missile.get('m_ReversePrefab') != {'m_FileID': 0, 'm_PathID': 0}:
        raise ValueError('HERO_09bp disabled Missile must remain authored-null')
    impact = config.get('impact') or {}
    prefab = impact.get('prefab') or {}
    if not impact.get('enabled') or prefab.get('prefab_null') or not prefab.get('prefab_reference') or \
       (prefab.get('prefab_source') or {}).get('pathid') != PRIEST_IMPACT_PATHID or \
       impact.get('ranges') != [] or impact.get('use_super_location') or impact.get('location') != 6 or \
       impact.get('parent_to_location') or impact.get('delay_min') != 0 or impact.get('delay_max') != 0 or \
       impact.get('spawn_offset') != 0 or impact.get('game_delay_min') != .5 or impact.get('game_delay_max') != .5 or \
       impact.get('adjust_rotation') or impact.get('rotation') != {'x': 0.0, 'y': 0.0, 'z': 0.0}:
        raise ValueError('HERO_09bp Impact source profile changed')
    for slot in ('friendly', 'opponent'):
        area = (config.get('area') or {}).get(slot) or {}
        if not area.get('enabled') or not area.get('prefab_null'):
            raise ValueError('HERO_09bp enabled-null ' + slot + ' area changed')
    if config.get('chain_available') or config.get('reverse_available'):
        raise ValueError('HERO_09bp chain or reverse source branch became active')


def demon_hunter_source_profile(program, config):
    """Fix the reviewed H10 source graph and the bounded self-hero route."""
    def has_property_data(value):
        if isinstance(value, dict):
            return 'property_data' in value or any(has_property_data(item) for item in value.values())
        if isinstance(value, list):
            return any(has_property_data(item) for item in value)
        return False

    def direct_params(action, names, label):
        params = action.get('params') or []
        if [row.get('name') for row in params] != names or any(row.get('unresolved') for row in params):
            raise ValueError('HERO_10bp ' + label + ' direct parameter profile changed')
        return {row['name']: row for row in params}

    if has_property_data(program):
        raise ValueError('HERO_10bp recursive property_data is outside the reviewed runtime profile')
    if program.get('reference') != DEMON_HUNTER_ROOT or len(program.get('nodes', [])) != 42:
        raise ValueError('HERO_10bp root identity or node graph changed')
    nodes = program['nodes']
    layer_roots = [nodes[index] for index in (0, 1, 12)]
    if any(not isinstance(node, dict) or node.get('parent') != -1 for node in layer_roots) or \
       [node.get('name') if isinstance(node, dict) else None for node in layer_roots] != [
           'ReuseFX_Fel_DemonHunter_BuffImpact_Small_Super_WithIdle',
           'ReuseFX_Idle_DemonHunter_Small', 'ReuseFX_BuffImpact_DemonHunter_Small']:
        raise ValueError('HERO_10bp source/target layer roots changed')
    particle_nodes = [index for index, node in enumerate(nodes)
                      if isinstance(node, dict) and isinstance(node.get('particle'), dict)]
    particles = [node['particle'] for node in nodes
                 if isinstance(node, dict) and isinstance(node.get('particle'), dict)]
    particle_fields_complete = all(isinstance(node, dict) and 'particle' in node for node in nodes)
    machines = program.get('machines', [])
    owners = sorted((machine.get('owner'), len(machine.get('states', [])))
                    for machine in machines if machine.get('owner_known'))
    if not particle_fields_complete or particle_nodes != list(DEMON_HUNTER_PARTICLE_NODES) or \
       len(particles) != 13 or any(not particle.get('restored') for particle in particles) or \
       len(program.get('animations', [])) != 3 or len(program.get('captures', [])) != 3 or \
       len(machines) != 2 or owners != [(1, 10), (12, 7)] or \
       sorted(program.get('unknown_actions', [])) != [
           'AnimatorPlayAction', 'AudioPlayClipAction', 'AudioPlaythroughAction', 'SetMaterialTexture', 'SetScale']:
        raise ValueError('HERO_10bp root runtime profile changed')
    for machine in machines:
        expected = DEMON_HUNTER_FSMS[machine['owner']]
        states = {state['name']: state for state in machine.get('states', [])}
        events = sorted((row['event'], row['state']) for row in machine.get('events', []))
        expected_events = sorted(expected['events'])
        if machine.get('start') != 'StartState' or events != expected_events or set(states) != set(expected['states']):
            raise ValueError('HERO_10bp root FSM profile changed')
        for name, (actions, transitions) in expected['states'].items():
            actual = states[name]
            actual_actions = [action['name'] for action in actual.get('actions', [])]
            actual_transitions = sorted((row['event'], row['state']) for row in actual.get('transitions', []))
            if actual_actions != actions or actual_transitions != sorted(transitions):
                raise ValueError('HERO_10bp root FSM profile changed at ' + name)

    by_owner = {machine['owner']: machine for machine in machines}
    target_hero = next(state for state in by_owner[12]['states'] if state['name'] == 'Hero')
    texture_actions = [action for action in target_hero['actions'] if action['name'] == 'SetMaterialTexture']
    expected_textures = [
        (24, 'cd7ba7885a2d12ab34d1dabf.png', '-8039451771726812956'),
        (25, 'cd7ba7885a2d12ab34d1dabf.png', '-8039451771726812956'),
        (26, 'db598f55f4e4c96b7e32d722.png', '3506897862555594978'),
    ]
    if len(texture_actions) != len(expected_textures):
        raise ValueError('HERO_10bp SetMaterialTexture direct parameter profile changed')
    for action, (node, path, pathid) in zip(texture_actions, expected_textures):
        params = direct_params(action, ['gameObject', 'materialIndex', 'material', 'namedTexture', 'texture'],
                               'SetMaterialTexture')
        game_object, material_index = params['gameObject'], params['materialIndex']
        material, named_texture, texture = params['material'], params['namedTexture'], params['texture']
        source = texture.get('asset_source') or {}
        if game_object.get('kind') != 20 or game_object.get('node') != node or \
           not game_object.get('none_known') or game_object.get('is_none') or game_object.get('variable') or \
           material_index.get('kind') != 16 or material_index.get('number') != 0 or material_index.get('variable') or \
           material.get('kind') != 32 or not material.get('object_null') or material.get('asset_type') != 'Material' or \
           material.get('variable') or named_texture.get('kind') != 18 or named_texture.get('text') != '_MainTex' or \
           named_texture.get('variable') or texture.get('kind') != 33 or texture.get('asset_type') != 'Texture' or \
           texture.get('object_null') or texture.get('variable') or texture.get('asset_path') != path or \
           source.get('cab') != 'CAB-572cbaceb2612afe464bd92ebbf0736a' or source.get('pathid') != pathid or \
           source.get('type') != 'Texture2D' or source.get('bundle') != 'essential_base_global-texture-2.unity3d':
            raise ValueError('HERO_10bp SetMaterialTexture direct parameter profile changed')

    scale_profiles = {
        (1, 'Minion'): [(6, (.7, .85, .85)), (10, (.65, .75, .75))],
        (1, 'Weapon/HP'): [(6, (.7, .7, .7)), (10, (.65, .65, .65))],
        (12, 'Weapon/HP'): [(14, (1.05, 1.05, .75))],
    }
    for (owner, state_name), expected_actions in scale_profiles.items():
        state = next(row for row in by_owner[owner]['states'] if row['name'] == state_name)
        actions = [action for action in state['actions'] if action['name'] == 'SetScale']
        if len(actions) != len(expected_actions):
            raise ValueError('HERO_10bp SetScale direct parameter profile changed')
        for action, (node, scale) in zip(actions, expected_actions):
            params = direct_params(action, ['gameObject', 'vector', 'x', 'y', 'z', 'everyFrame', 'lateUpdate'],
                                   'SetScale')
            game_object, vector = params['gameObject'], params['vector']
            if game_object.get('kind') != 20 or game_object.get('node') != node or \
               not game_object.get('none_known') or game_object.get('is_none') or game_object.get('variable') or \
               vector.get('kind') != 28 or not vector.get('none_known') or not vector.get('is_none') or vector.get('variable'):
                raise ValueError('HERO_10bp SetScale direct parameter profile changed')
            for name, expected in zip(('x', 'y', 'z'), scale):
                value = params[name]
                if value.get('kind') != 15 or not value.get('none_known') or value.get('is_none') or \
                   value.get('variable') or not math.isclose(value.get('number', math.nan), expected, abs_tol=1e-5):
                    raise ValueError('HERO_10bp SetScale direct parameter profile changed')
            for name in ('everyFrame', 'lateUpdate'):
                value = params[name]
                if value.get('kind') != 1 or value.get('number') != 0 or value.get('variable'):
                    raise ValueError('HERO_10bp SetScale direct parameter profile changed')

    if config.get('card_id') != 'HERO_10bp' or config.get('reference') != DEMON_HUNTER_ROOT or \
       config.get('target_behavior') != 0:
        raise ValueError('HERO_10bp CardDef or target source profile changed')
    start = config.get('start') or {}
    if not config.get('start_enabled') or config.get('start_config_null') or \
       not start.get('enabled') or start.get('prefab_null') or \
       start.get('prefab_reference') != DEMON_HUNTER_START or \
       (start.get('prefab_source') or {}).get('pathid') != '-7316641151859032915' or \
       not start.get('use_super_location') or not start.get('death_after_missiles') or \
       start.get('adjust_rotation') or start.get('rotation') != {'x': 0.0, 'y': 0.0, 'z': 0.0}:
        raise ValueError('HERO_10bp Start source profile changed')
    missile = config.get('missile') or {}
    if missile.get('m_Enabled') != 0 or config.get('missile_reference') or \
       missile.get('m_Prefab') != {'m_FileID': 0, 'm_PathID': 0} or \
       missile.get('m_ReversePrefab') != {'m_FileID': 0, 'm_PathID': 0} or config.get('reverse_available'):
        raise ValueError('HERO_10bp Missile must remain authored-disabled/null')
    impact = config.get('impact') or {}
    prefab = impact.get('prefab') or {}
    if not impact.get('enabled') or prefab.get('prefab_null') or \
       (prefab.get('prefab_source') or {}).get('pathid') != '-618183129397526064' or \
       not prefab.get('prefab_reference') or impact.get('ranges') != [] or \
       impact.get('use_super_location') or impact.get('location') != 6 or \
       impact.get('parent_to_location') or impact.get('delay_min') != 0 or \
       impact.get('delay_max') != 0 or impact.get('spawn_offset') != 0 or \
       impact.get('game_delay_min') != .5 or impact.get('game_delay_max') != .5 or \
       impact.get('adjust_rotation') or impact.get('rotation') != {'x': 0.0, 'y': 0.0, 'z': 0.0}:
        raise ValueError('HERO_10bp Impact source profile changed')
    for slot in ('friendly', 'opponent'):
        area = (config.get('area') or {}).get(slot) or {}
        if not area.get('enabled') or not area.get('prefab_null') or area.get('prefab_reference'):
            raise ValueError('HERO_10bp enabled-null ' + slot + ' area changed')
    if not config.get('chain_enabled') or not config.get('chain_prefab_null') or \
       config.get('chain_reference') or config.get('chain_available'):
        raise ValueError('HERO_10bp enabled-null Chain source state changed')
    return DEMON_HUNTER_RUNTIME_PROFILE


def native_effect_enabled(config):
    """Enable only source profiles with a reviewed runtime route."""
    card_id = config.get('card_id')
    if card_id == 'HERO_10bp':
        return config.get('source_root_profile') == DEMON_HUNTER_RUNTIME_PROFILE
    return card_id == 'HERO_07bp' or bool(config.get('missile') and config['missile'].get('m_Enabled'))


def update_effect_index(path, card_ids, imported, source_only, preserve_existing=False):
    """Update the runtime index unless the caller is preparing source assets only."""
    if preserve_existing:
        return False
    index = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {'entries': []}
    entries = [entry for entry in index['entries'] if entry['card_id'] not in card_ids]
    for row in imported:
        entries.append(dict(card_id=row['config']['card_id'], file=row['config']['file'],
                            enabled=native_effect_enabled(row['config']),
                            duration=row['source_duration'], replace=False))
    for row in source_only:
        enabled = row['card_id'] in ('HERO_02bp', 'HERO_11bp') and row['source_profile'].get('runtime_route_enabled') is True
        duration = row['source_profile'].get('runtime_duration', RUNTIME_DURATION) if enabled else 0
        entries.append(dict(card_id=row['card_id'], file=row['file'], enabled=enabled,
                            duration=duration, replace=False, impact_delay=-1.0))
    index['entries'] = entries
    atomic_json(path, index)
    return True


def source_visual_duration(program):
    """Bound the observed Warrior source cue to its authored particles and shake."""
    if sorted(set(program.get('unknown_actions', []))) != ['AudioPlayClipAction', 'InvokeMethod']:
        raise ValueError('HERO_01bp root includes an unsupported source action')
    machines = [row for row in program.get('machines', [])
                if row.get('owner_known') and row.get('owner') == 1]
    if len(machines) != 1:
        raise ValueError('HERO_01bp requires one reviewed owner-1 root FSM')
    machine = machines[0]
    expected_states = {'State 1', 'DeathState', 'DeathState2', 'DeathState3', 'NoneState'}
    states = {row['name']: row for row in machine.get('states', [])}
    if machine.get('start') != 'State 1' or set(states) != expected_states:
        raise ValueError('HERO_01bp root FSM state graph is outside the reviewed source profile')
    expected_actions = {
        'State 1': [],
        'DeathState': ['SpellStateAction', 'ParticlePlayAction', 'ParticlePlayAction', 'Wait', 'AudioPlayClipAction'],
        'DeathState2': ['ParticlePlayAction', 'ParticlePlayAction', 'ParticlePlayAction', 'InvokeMethod'],
        'DeathState3': ['SpellFinishAction', 'Wait'],
        'NoneState': ['SpellStateAction'],
    }
    for name, actions in expected_actions.items():
        actual = [row.get('name') for row in states[name].get('actions', [])]
        if sorted(actual) != sorted(actions):
            raise ValueError('HERO_01bp state action profile is unsupported: ' + name)
    expected_transitions = {
        'State 1': [],
        'DeathState': [('FINISHED', 'DeathState2')],
        'DeathState2': [('FINISHED', 'DeathState3')],
        'DeathState3': [('FINISHED', 'NoneState')],
        'NoneState': [],
    }
    for name, transitions in expected_transitions.items():
        actual = [(row.get('event'), row.get('state')) for row in states[name].get('transitions', [])]
        if actual != transitions:
            raise ValueError('HERO_01bp state transition profile is unsupported: ' + name)
    death = [row.get('state') for row in machine.get('events', []) if row.get('event') == 'Death']
    if death != ['DeathState'] or sorted((row.get('event'), row.get('state')) for row in machine.get('events', [])) != sorted([
        ('Death', 'DeathState'), ('None', 'NoneState')
    ]):
        raise ValueError('HERO_01bp root FSM lifecycle entries are outside the reviewed source profile')
    nodes = program.get('nodes', [])

    def parameter(action, name):
        matches = [row for row in action.get('params', []) if row.get('name') == name]
        if len(matches) != 1 or matches[0].get('unresolved'):
            raise ValueError('HERO_01bp has unresolved ' + action.get('name', '') + '.' + name)
        return matches[0]

    def upper_curve(value):
        if not isinstance(value, dict) or value.get('mode') not in (0, 1, 2, 3):
            raise ValueError('HERO_01bp particle timing curve is not restored')
        mode = value['mode']
        if mode == 0:
            samples = [value.get('scalar', 0)]
        elif mode == 3:
            samples = [value.get('scalar', 0), value.get('minimum', 0)]
        else:
            raise ValueError('HERO_01bp particle timing curve mode is outside the reviewed constant profile')
        if any(not isinstance(sample, (int, float)) or not math.isfinite(sample) for sample in samples):
            raise ValueError('HERO_01bp particle timing curve contains invalid values')
        upper = max(samples)
        if upper < 0:
            raise ValueError('HERO_01bp particle timing curve has a negative duration')
        return upper

    elapsed = 0.0
    visual_end = 0.0
    state_name = death[0]
    visited = set()
    for _ in range(16):
        if state_name in visited or state_name not in states:
            raise ValueError('HERO_01bp Death FSM path is cyclic or unresolved')
        visited.add(state_name)
        state = states[state_name]
        wait_time = 0.0
        for action in state.get('actions', []):
            if action.get('name') == 'Wait':
                time = parameter(action, 'time')
                realtime = parameter(action, 'realTime')
                if realtime.get('number') != 0 or time.get('variable') or \
                   not isinstance(time.get('number'), (int, float)) or not math.isfinite(time['number']) or time['number'] < 0:
                    raise ValueError('HERO_01bp uses an unsupported root Wait')
                wait_time = max(wait_time, time['number'])
            elif action.get('name') == 'ParticlePlayAction':
                target = parameter(action, 'm_GameObject')
                include = parameter(action, 'm_IncludeChildren')
                if include.get('number') != 0 or target.get('node', -1) not in range(len(nodes)):
                    raise ValueError('HERO_01bp uses an unsupported particle target')
                particle = nodes[target['node']].get('particle') or {}
                if not particle.get('restored') or not isinstance(particle.get('duration'), (int, float)) or \
                   not math.isfinite(particle['duration']) or particle['duration'] < 0:
                    raise ValueError('HERO_01bp particle action has no restored source system')
                visual_end = max(visual_end, elapsed + upper_curve(particle.get('delay', {})) +
                                 particle.get('duration', 0) + upper_curve(particle.get('life', {})))
            elif action.get('name') == 'InvokeMethod':
                target = parameter(action, 'gameObject')
                behaviour = parameter(action, 'behaviour')
                delay = parameter(action, 'delay')
                method = parameter(action, 'methodName')
                repeating = parameter(action, 'repeating')
                repeat_delay = parameter(action, 'repeatDelay')
                cancel = parameter(action, 'cancelOnExit')
                if behaviour.get('text') != 'CameraShaker' or method.get('text') != 'StartShake' or \
                   target.get('node', -1) != 1 or target.get('node', -1) not in range(len(nodes)) or \
                   not math.isclose(delay.get('number', -1), 0.1, abs_tol=1e-6) or \
                   repeating.get('number') != 0 or repeat_delay.get('number') != 1 or cancel.get('number') != 0:
                    raise ValueError('HERO_01bp uses an unsupported root method invocation')
                shaker = nodes[target['node']].get('camera_shaker') or {}
                curve = shaker.get('curve') or []
                if not shaker.get('restored') or len(curve) != 2 or shaker.get('hold') or \
                   shaker.get('is_override') or not math.isclose(shaker.get('hold_at_sec', -1), 0, abs_tol=1e-6) or \
                   not all(math.isclose(shaker.get('amount', {}).get(axis, math.nan), .1, abs_tol=1e-6)
                           for axis in ('x', 'y', 'z')) or \
                   not math.isclose(curve[0].get('time', -1), 0, abs_tol=1e-6) or \
                   not math.isclose(curve[0].get('value', math.nan), 1, abs_tol=1e-6) or \
                   not math.isclose(curve[1].get('time', -1), .6, abs_tol=1e-5) or \
                   not math.isclose(curve[1].get('value', math.nan), 0, abs_tol=1e-6):
                    raise ValueError('HERO_01bp camera shake timing is unresolved')
                visual_end = max(visual_end, elapsed + delay['number'] + curve[-1]['time'])
        transition = next((row for row in state.get('transitions', []) if row.get('event') == 'FINISHED'), None)
        if transition is None:
            break
        elapsed += wait_time
        state_name = transition['state']
    else:
        raise ValueError('HERO_01bp Death FSM path exceeds the reviewed limit')
    if visual_end <= 0 or not math.isfinite(visual_end):
        raise ValueError('HERO_01bp visual source duration is not finite')
    return visual_end


def warlock_source_duration(program):
    """Validate the one reviewed H07 root-only FSM and retain its authored wait."""
    reference = ('ReuseFX_BuffImpact_Void_SwirlIntake_Small_HeroPower_Super.prefab:'
                '5c3f93a27c9cb434ea6e0c37005a2c0d')
    if program.get('reference') != reference or len(program.get('nodes', [])) != 21:
        raise ValueError('HERO_07bp root identity or node graph changed')
    if sorted(set(program.get('unknown_actions', []))) != ['AudioPlayClipAction']:
        raise ValueError('HERO_07bp root includes an unsupported source action')
    machines = [row for row in program.get('machines', [])
                if row.get('owner_known') and row.get('owner') == 1]
    if len(machines) != 1:
        raise ValueError('HERO_07bp requires one reviewed owner-1 root FSM')
    machine = machines[0]
    expected = {
        'Play FX': ['SpellStateAction', 'SpellFinishAction', 'ActivateGameObject',
                    'ActivateGameObject', 'ParticlePlayAction', 'AudioPlayClipAction',
                    'ParticlePlayAction', 'Wait'],
        'StartState': [],
        'NoneState': ['SpellStateAction'],
    }
    states = {row.get('name'): row for row in machine.get('states', [])}
    if machine.get('start') != 'StartState' or set(states) != set(expected):
        raise ValueError('HERO_07bp root FSM state graph is outside the reviewed profile')
    if sorted((row.get('event'), row.get('state')) for row in machine.get('events', [])) != sorted([
            ('Death', 'Play FX'), ('None', 'NoneState')]):
        raise ValueError('HERO_07bp root FSM lifecycle entries changed')
    for name, actions in expected.items():
        state = states[name]
        if [row.get('name') for row in state.get('actions', [])] != actions:
            raise ValueError('HERO_07bp state action order changed: ' + name)
        transitions = [(row.get('event'), row.get('state')) for row in state.get('transitions', [])]
        wanted = [('FINISHED', 'NoneState')] if name == 'Play FX' else []
        if transitions != wanted:
            raise ValueError('HERO_07bp state transition changed: ' + name)

    def params(action, names):
        rows = action.get('params', [])
        if [row.get('name') for row in rows] != names or any(row.get('unresolved') for row in rows):
            raise ValueError('HERO_07bp action parameters are unresolved or changed: ' + action['name'])
        return {row['name']: row for row in rows}

    def node(action, name, value):
        values = params(action, [name])
        if values[name].get('node') != value:
            raise ValueError('HERO_07bp action target changed: ' + action['name'])

    play = states['Play FX']['actions']
    node(play[0], 'm_SpellObject', 1)
    finish = params(play[1], ['m_SpellObject', 'm_Delay'])
    if finish['m_SpellObject'].get('node') != 1 or finish['m_Delay'].get('number') != 0:
        raise ValueError('HERO_07bp SpellFinishAction changed')
    for action, target in zip(play[2:4], (3, 8)):
        values = params(action, ['gameObject', 'activate', 'recursive', 'resetOnExit', 'everyFrame'])
        if values['gameObject'].get('node') != target or values['activate'].get('number') != 1 or \
           any(values[name].get('number') != 0 for name in ('recursive', 'resetOnExit', 'everyFrame')):
            raise ValueError('HERO_07bp activated node profile changed')
    for action, target in ((play[4], 4), (play[6], 9)):
        values = params(action, ['m_GameObject', 'm_IncludeChildren'])
        if values['m_GameObject'].get('node') != target or values['m_IncludeChildren'].get('number') != 1:
            raise ValueError('HERO_07bp particle target profile changed')
        particle = program['nodes'][target].get('particle') or {}
        if not particle.get('restored'):
            raise ValueError('HERO_07bp particle target is not restored')
    audio = params(play[5], ['m_ParentObject', 'm_Sound', 'm_Volume', 'm_Pitch',
                             'm_SpatialBlend', 'm_Category', 'm_Delay', 'm_TemplateSource',
                             'm_IsMinionSummonVO', 'm_InstanceLimited',
                             'm_InstanceLimitedDuration', 'm_InstanceLimitMaximum',
                             'm_LimitMaxOutOption'])
    sound = audio['m_Sound']
    if audio['m_ParentObject'].get('node') != 1 or sound.get('asset_type') != 'MonoBehaviour' or \
       sound.get('asset_path') != 'ce406cfd04976a99ebc44463.native-sound.json' or sound.get('object_null') or \
       audio['m_TemplateSource'].get('object_null') is not True or \
       any(not math.isclose(audio[name].get('number', math.nan), value, abs_tol=1e-6) for name, value in {
           'm_Volume': .8, 'm_Pitch': 1, 'm_SpatialBlend': 0, 'm_Category': 1,
           'm_Delay': 0, 'm_IsMinionSummonVO': 0, 'm_InstanceLimited': 1,
           'm_InstanceLimitedDuration': .5, 'm_InstanceLimitMaximum': 1,
           'm_LimitMaxOutOption': 0,
       }.items()):
        raise ValueError('HERO_07bp AudioPlayClipAction is outside the reviewed clip profile')
    wait = params(play[7], ['time', 'finishEvent', 'realTime'])
    if wait['time'].get('number') != 4 or wait['finishEvent'].get('text') != 'FINISHED' or \
       wait['realTime'].get('number') != 0:
        raise ValueError('HERO_07bp root wait changed')
    return 4.0


def _warlock_start_is_enabled_null(ex, program):
    transform = next(row for row in program.get('spell_transforms', []) if row.get('node') == 0)
    source = transform['source']
    obj = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
    raw = obj.read_typetree()
    start = raw.get('m_StartInfo') or {}
    prefab = start.get('m_Prefab') or {}
    return start.get('m_Enabled') == 1 and prefab.get('m_FileID') == 0 and prefab.get('m_PathID') == 0


def _merge_by(rows, additions, key):
    merged = {row[key]: row for row in rows or []}
    merged.update({row[key]: row for row in additions})
    return list(merged.values())


def referenced_prefabs(config):
    references = set()
    start = config.get('start') or {}
    if config.get('start_enabled'):
        if config.get('card_id') == 'HERO_07bp' and not start:
            pass
        elif not start.get('enabled') or start.get('prefab_null'):
            raise ValueError(config['card_id'] + ' has an enabled Start without a source prefab')
        else:
            reference = start.get('prefab_reference')
            if not reference:
                raise ValueError(config['card_id'] + ' has an enabled Start without a source identity')
            references.add(reference)
    missile = config.get('missile') or {}
    if missile.get('m_Enabled'):
        reference = config.get('missile_reference')
        if not reference:
            raise ValueError(config['card_id'] + ' has an enabled missile without a source prefab')
        references.add(reference)

    impact = config.get('impact') or {}
    if impact.get('enabled'):
        prefab = impact.get('prefab') or {}
        if not prefab.get('prefab_null'):
            reference = prefab.get('prefab_reference')
            if not reference:
                raise ValueError(config['card_id'] + ' has an unresolved enabled impact prefab')
            references.add(reference)

    for slot in ('friendly', 'opponent'):
        area = (config.get('area') or {}).get(slot)
        if area and area.get('enabled') and not area.get('prefab_null'):
            reference = area.get('prefab_reference')
            if not reference:
                raise ValueError(config['card_id'] + ' has an unresolved ' + slot + ' area prefab')
            references.add(reference)
    return references


def paladin_area_registration(row):
    """Return the independently reviewed H04 area-only runtime registration."""
    config = row['config']
    if config.get('card_id') != 'HERO_04bp':
        return None, None
    area = config['area']
    friendly = area['friendly']
    prefab = next((item for item in row['prefabs'] if item['reference'] == friendly['prefab_reference']), None)
    if prefab is None:
        raise ValueError('HERO_04bp FriendlyArea prefab was not imported')
    return dict(file=config['file'], reference=config['reference'], config=area), prefab


def merge_paladin_area_catalogs(area_catalog, prefab_catalog, rows):
    registrations = [paladin_area_registration(row) for row in rows
                    if row['config'].get('card_id') == 'HERO_04bp']
    registrations = [(record, prefab) for record, prefab in registrations if record is not None]
    if not registrations:
        return area_catalog, prefab_catalog
    area_catalog['programs'] = _merge_by(area_catalog.get('programs', []),
                                         [record for record, _ in registrations], 'file')
    prefab_catalog['prefabs'] = _merge_by(prefab_catalog.get('prefabs', []),
                                          [prefab for _, prefab in registrations], 'reference')
    area_catalog.setdefault('full_super_spell_restored', False)
    return area_catalog, prefab_catalog


def resolve_card_effect_reference(card_id, definition):
    play_effect = definition.get('m_PlayEffectDef') or {}
    direct_reference = play_effect.get('m_SpellPath') if isinstance(play_effect, dict) else None
    if isinstance(direct_reference, str) and direct_reference.strip():
        return direct_reference

    subspell_effects = definition.get('m_SubSpellEffectDefs') or []
    candidates = []
    if isinstance(subspell_effects, (list, tuple)):
        for index, effect in enumerate(subspell_effects):
            if not isinstance(effect, dict):
                continue
            reference = effect.get('m_SpellPath')
            if isinstance(reference, str) and reference.strip():
                candidates.append((index, reference))

    if len(candidates) == 1:
        return candidates[0][1]
    if candidates:
        details = ', '.join(
            'm_SubSpellEffectDefs/{}/m_SpellPath={!r}'.format(index, reference)
            for index, reference in candidates
        )
        raise ValueError(card_id + ' has multiple CardDef SuperSpell references: ' + details)
    raise ValueError(
        card_id + ' has no CardDef SuperSpell reference '
        '(checked m_PlayEffectDef/m_SpellPath and '
        'm_SubSpellEffectDefs/*/m_SpellPath)'
    )


def import_card(ex, card_id):
    definition = ex.imp.reader.definition(card_id)
    reference = resolve_card_effect_reference(card_id, definition)

    root_file, root_source = export_program(
        ex, reference, suffix='.hero-power-source.json', include_component_effects=True,
        include_camera_shakers=card_id == 'HERO_01bp'
    )
    root_program = json.loads((ex.folder / root_file).read_text(encoding='utf-8'))
    source_duration = (source_visual_duration(root_program) if card_id == 'HERO_01bp' else
                       warlock_source_duration(root_program) if card_id == 'HERO_07bp' else
                       .1 if card_id in ('HERO_04bp', 'HERO_10bp') else 1.0)
    config = recover_root(ex.imp, root_program, card_id, root_file)
    start_source = recover_start(ex.imp, root_program)
    if card_id == 'HERO_09bp':
        config['start'] = start_source['start'] if start_source else None
        config['start_config_null'] = config['start'] is None
        priest_source_profile(root_program, config)
    elif card_id == 'HERO_04bp':
        config['start'] = None
        config['start_config_null'] = True
        config['friendly_area_profile'] = paladin_source_profile(root_program, config)
    elif card_id == 'HERO_03bp':
        config['start'] = None
        config['start_config_null'] = True
        config['source_root_profile'] = rogue_source_profile(root_program, config)
        source_duration = 2.552
    elif card_id == 'HERO_10bp':
        config['start'] = start_source['start'] if start_source else None
        config['start_config_null'] = config['start'] is None
        config['source_root_profile'] = demon_hunter_source_profile(root_program, config)
    else:
        config['start'] = None
        config['start_config_null'] = True
    if card_id == 'HERO_07bp' and not _warlock_start_is_enabled_null(ex, root_program):
        raise ValueError('HERO_07bp must keep its authored enabled-null Start; do not invent a prefab')
    if config['start_enabled'] and card_id != 'HERO_07bp':
        if card_id not in ('HERO_09bp', 'HERO_10bp'):
            raise NotImplementedError(card_id + ' has an unreviewed enabled Start prefab')
    if config['missile'] is None or config['impact'] is None:
        raise NotImplementedError(card_id + ' is missing a supported missile or impact branch')

    root_audio_file = ex.key(ex.imp.reader.resolve(reference)[0]) + '.hero-power-audio.json'
    root_audio = recover_audio(ex, reference, include_program_hierarchy=True)
    atomic_json(ex.folder / root_audio_file, root_audio)
    bind_program(ex, ex.imp.reader.resolve(reference)[0], root_audio_file,
                 suffix='.hero-power-source.json')

    prefab_records = []
    references = referenced_prefabs(config)
    impact_ref = ((config.get('impact') or {}).get('prefab') or {}).get('prefab_reference')
    start_ref = ((config.get('start') or {}).get('prefab_reference'))

    for prefab_ref in sorted(references):
        hunter_impact = card_id == 'HERO_05bp' and prefab_ref == impact_ref
        warrior_impact = card_id == 'HERO_01bp' and prefab_ref == impact_ref
        warrior_camera_profile = None
        if warrior_impact:
            preflight_file, _ = export_program(
                ex, prefab_ref, suffix='.hero-power-prefab.json', include_trails=True,
                include_component_effects=True, include_camera_shakers=False,
            )
            preflight = json.loads((ex.folder / preflight_file).read_text(encoding='utf-8'))
            candidate = dict(preflight)
            candidate['nodes'] = list(preflight['nodes'])
            candidate['nodes'][0] = dict(
                candidate['nodes'][0], camera_shaker=_warrior_impact_camera_shaker_source(ex, prefab_ref))
            warrior_camera_profile = warrior_impact_camera_shake_profile(candidate)
        filename, source = export_program(
            ex, prefab_ref, suffix='.hero-power-prefab.json', include_trails=True,
            include_component_effects=True,
            include_camera_shakers=hunter_impact or warrior_impact,
        )
        if hunter_impact:
            impact_program = json.loads((ex.folder / filename).read_text(encoding='utf-8'))
            config['impact']['camera_shake_profile'] = hunter_impact_camera_shake_profile(impact_program)
        elif warrior_impact:
            impact_program = json.loads((ex.folder / filename).read_text(encoding='utf-8'))
            if warrior_impact_camera_shake_profile(impact_program) != warrior_camera_profile:
                raise ValueError('HERO_01bp Impact CameraShaker export differs from its source preflight')
            config['impact']['camera_shake_profile'] = warrior_camera_profile
        obj, _, _ = ex.imp.reader.resolve(prefab_ref)
        audio_file = ex.key(obj) + '.hero-power-audio.json'
        audio = recover_audio(ex, prefab_ref, include_program_hierarchy=True)
        atomic_json(ex.folder / audio_file, audio)
        bind_program(ex, obj, audio_file, suffix='.hero-power-prefab.json')
        prefab_records.append(dict(reference=prefab_ref, file=filename, source=source,
                                   audio_file=audio_file, audio_errors=audio['errors']))

    config['file'] = root_file
    config['reference'] = reference
    config['card_id'] = card_id
    for prefab in prefab_records:
        if prefab['reference'] == start_ref:
            config['start']['file'] = prefab['file']
        if prefab['reference'] == impact_ref:
            config['impact']['prefab']['file'] = prefab['file']
    for slot in ('friendly', 'opponent'):
        area = config['area'].get(slot)
        if area:
            for prefab in prefab_records:
                if area['prefab_reference'] == prefab['reference']:
                    area['file'] = prefab['file']

    textures = {}
    for prefab in prefab_records:
        graph = json.loads((ex.folder / prefab['file']).read_text(encoding='utf-8'))
        for node in graph['nodes']:
            if not node.get('trail'):
                continue
            for material in node['materials']:
                source = material['source']
                obj = ex.imp.reader.load(source['bundle']).get_cab(source['cab'].lower()).objects[int(source['pathid'])]
                for _, saved in obj.read_typetree()['m_SavedProperties']['m_TexEnvs']:
                    if not saved['m_Texture']['m_PathID']:
                        continue
                    texture = ex.imp.reader.pointer(obj, saved['m_Texture'])
                    if texture is not None and texture.type.name == 'Texture2D':
                        row = recover_texture(ex, texture)
                        textures[row['png']] = row

    return dict(config=config, root_source=root_source, root_audio_file=root_audio_file,
                prefabs=prefab_records, source_duration=source_duration,
                textures=list(textures.values()))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='HERO_05bp')
    parser.add_argument('--preserve-index', action='store_true',
                        help='refresh source assets/manifests while leaving native-effect-index.json byte-for-byte unchanged')
    args = parser.parse_args()
    card_ids = [value.strip() for value in args.ids.split(',') if value.strip()]
    invalid = sorted(set(card_ids) - {'HERO_06bp', 'HERO_05bp', 'HERO_08bp', 'HERO_04bp',
                                     'HERO_09bp', 'HERO_03bp', 'HERO_02bp', 'HERO_07bp',
                                     'HERO_01bp', 'HERO_10bp', 'HERO_11bp'})
    if invalid:
        parser.error('IDs must be base hero powers: ' + ','.join(invalid))

    ex = RuntimeExport()
    source_index = json.loads((PROJECT / 'manifests/hero-power-native-sources.json').read_text(encoding='utf-8'))
    source_cards = {row['card_id']: row for row in source_index['cards']}
    imported = []
    source_only = []
    with asset_lock(ex.imp.local):
        for card_id in card_ids:
            source = source_cards.get(card_id)
            if source is None or source.get('error'):
                raise ValueError(card_id + ' has no successful source manifest row')
            if card_id == 'HERO_11bp':
                output = import_death_knight_source(ex, source)
                source_only.append(output)
                print(card_id, 'root=', output['reference'], 'profile=',
                      output['source_profile']['profile'], 'runtime=enabled', flush=True)
                continue
            if card_id == 'HERO_02bp':
                output = import_shaman_source(ex, source)
                source_only.append(output)
                print(card_id, 'root=', output['reference'], 'profile=',
                      output['source_profile']['profile'],
                      'runtime=' + ('enabled' if output['source_profile']['runtime_route_enabled'] else 'disabled'), flush=True)
                continue
            output = import_card(ex, card_id)
            component = next(row for effect in source['effect_references']
                             for row in effect['spell_components'] if row['class_name'] == 'SuperSpell')
            if output['config']['reference'] != source['effect_references'][0]['reference']:
                raise ValueError(card_id + ' source manifest reference mismatch')
            imported.append(output)
            print(card_id, 'root=', output['config']['reference'],
                  'prefabs=', len(output['prefabs']), 'start=', output['config']['start_enabled'], flush=True)

        catalog_path = ex.folder / 'native-super-spell.json'
        catalog = json.loads(catalog_path.read_text(encoding='utf-8')) if catalog_path.exists() else {'programs': [], 'prefabs': []}
        catalog['programs'] = _merge_by(catalog.get('programs', []),
                                        [row['config'] for row in imported], 'card_id')
        catalog['prefabs'] = _merge_by(catalog.get('prefabs', []),
                                       [prefab for row in imported for prefab in row['prefabs']], 'reference')
        catalog['official_geometry_verified'] = False
        catalog['whole_effect_verified'] = False
        atomic_json(catalog_path, catalog)

        if any(row['config']['card_id'] == 'HERO_04bp' for row in imported):
            area_config_path = ex.folder / 'native-super-area-config.json'
            area_catalog = json.loads(area_config_path.read_text(encoding='utf-8')) if area_config_path.exists() else {'programs': []}
            area_prefabs_path = ex.folder / 'native-super-area-prefabs.json'
            area_prefabs = json.loads(area_prefabs_path.read_text(encoding='utf-8')) if area_prefabs_path.exists() else {'prefabs': []}
            area_catalog, area_prefabs = merge_paladin_area_catalogs(area_catalog, area_prefabs, imported)
            atomic_json(area_config_path, area_catalog)
            atomic_json(area_prefabs_path, area_prefabs)

        if source_only:
            custom_spawn_path = ex.folder / 'native-custom-spawn.json'
            custom_spawn = json.loads(custom_spawn_path.read_text(encoding='utf-8')) if custom_spawn_path.exists() else {'programs': []}
            custom_spawn['programs'] = _merge_by(custom_spawn.get('programs', []), source_only, 'card_id')
            custom_spawn['whole_effect_verified'] = False
            atomic_json(custom_spawn_path, custom_spawn)

        index_path = ex.folder / 'native-effect-index.json'
        update_effect_index(index_path, card_ids, imported, source_only, args.preserve_index)

        atomic_json(ex.folder / 'native-hero-power-import.json', {
            'source_manifest': 'manifests/hero-power-native-sources.json',
            'cards': [dict(card_id=row['config']['card_id'], reference=row['config']['reference'],
                           root_file=row['config']['file'], root_source=row['root_source'],
                           root_audio_file=row['root_audio_file'],
                           presentation_profile=row['config'].get('source_root_profile',
                               row['config'].get('friendly_area_profile', 'native-super-spell')),
                           prefabs=[dict(reference=p['reference'], file=p['file'], source=p['source'])
                                    for p in row['prefabs']]) for row in imported] +
                     [dict(card_id=row['card_id'], reference=row['reference'],
                           root_file=row['file'], root_source=row['root_source'],
                           root_audio_file=row['audio_file'],
                           presentation_profile=row['source_profile']['profile'],
                           runtime_route_enabled=row['source_profile'].get('runtime_route_enabled') is True,
                           custom_spawn_completion_age_seconds=row['source_profile'].get('custom_spawn_completion_age_seconds'),
                           known_gaps=row['source_profile']['known_gaps']) for row in source_only],
            'whole_effect_verified': False,
        })
    ex.save()
    print('HERO POWER CLIENT IMPORT:', len(imported), 'cards', flush=True)


if __name__ == '__main__':
    main()
