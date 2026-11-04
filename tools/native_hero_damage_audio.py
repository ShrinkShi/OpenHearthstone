"""Export the exact Hero DamageSplat Health AudioPlayAction binding."""
import json

from .asset_lock import atomic_json
from .export_runtime import RuntimeExport
from .importer import PROJECT
from .native_effects import fsm_asset, identity, parameters, pointer_key


REFERENCE = 'Card_Play_Hero_DamageSplat.prefab:2ecc84d6a2db7461799ad55067409307'
CLIP_REFERENCE = 'Blood_Splat_Damage.wav:4dbf1353322e92443997499cf169c06e'
RUNTIME_FILE = 'native-hero-damage-splat-audio.json'
MANIFEST_FILE = 'native-hero-damage-splat-audio.json'


def export_profile(ex):
    root, _, _ = ex.imp.reader.resolve(REFERENCE)
    components = []
    for item in root.read_typetree().get('m_Component', []):
        obj = ex.imp.reader.pointer(root, item['component'])
        if obj is not None:
            components.append((obj, obj.read_typetree()))
    audio_sources = [(obj, tree) for obj, tree in components if obj.type.name == 'AudioSource']
    machines = []
    for obj, tree in components:
        if obj.type.name != 'MonoBehaviour':
            continue
        script = ex.imp.reader.pointer(obj, tree.get('m_Script', {}))
        if script and script.read_typetree().get('m_ClassName') == 'PlayMakerFSM':
            machines.append((obj, tree))
    if len(audio_sources) != 1 or len(machines) != 1:
        raise ValueError('DamageSplat root must have one AudioSource and one PlayMakerFSM')

    machine, machine_tree = machines[0]
    fsm = machine_tree['fsm']
    state = next((state for state in fsm.get('states', []) if state.get('name') == 'Health'), None)
    if state is None:
        raise ValueError('DamageSplat Health state is missing')
    data = state.get('actionData', {})
    indexes = [i for i, name in enumerate(data.get('actionNames', []))
               if name.rsplit('.', 1)[-1] == 'AudioPlayAction']
    if len(indexes) != 1:
        raise ValueError('DamageSplat Health state must have one AudioPlayAction')

    variables = {value['name']: value for group in fsm.get('variables', {}).values()
                 if isinstance(group, list) for value in group
                 if isinstance(value, dict) and 'name' in value}
    params = parameters(data, indexes[0], machine, ex.imp.reader,
                        {pointer_key(root): 0}, variables, fsm.get('dataVersion', 0),
                        lambda obj: fsm_asset(ex, obj))
    fields = {value['name']: value for value in params}
    game_object = fields.get('m_GameObject', {})
    volume = fields.get('m_VolumeScale', {})
    delay = fields.get('m_Delay', {})
    sound = fields.get('m_OneShotSound', {})
    if (game_object.get('node') != 0 or volume.get('number') != 1 or delay.get('number') != 0 or
            sound.get('asset_type') != 'MonoBehaviour' or sound.get('object_null') or sound.get('unresolved') or
            not sound.get('asset_path')):
        raise ValueError('DamageSplat Health AudioPlayAction source binding changed')

    sound_path = ex.folder / sound['asset_path']
    sound_file = json.loads(sound_path.read_text(encoding='utf-8'))
    sound_values = sound_file.get('values', {})
    if (sound_values.get('m_AudioClip') != CLIP_REFERENCE or sound_values.get('m_RandomClips') or
            sound_values.get('m_RandomVolumeMin') != .75 or sound_values.get('m_RandomVolumeMax') != .75 or
            sound_values.get('m_RandomPitchMin') != 1 or sound_values.get('m_RandomPitchMax') != 1):
        raise ValueError('DamageSplat Health SoundDef source values changed')

    source, source_tree = audio_sources[0]
    action = dict(state='Health', name='AudioPlayAction',
                  game_object_node=game_object['node'], m_VolumeScale=volume['number'],
                  m_Delay=delay['number'], sound_asset=sound['asset_path'], params=params)
    return dict(profile='hero-health-damagesplat-v1', source=identity(ex, root),
                action_source=identity(ex, machine), action=action,
                audio_source=dict(source=identity(ex, source), values=source_tree),
                sound_source=sound.get('asset_source'), sound=sound_file,
                complete=False,
                notes=['Health uses root AudioSource plus the explicit SFX_BloodSplatHealth SoundDef.',
                       'AudioPlayAction has zero delay and unit VolumeScale; SoundDef supplies fixed 0.75 volume and 1.0 pitch.'])


def main():
    ex = RuntimeExport()
    profile = export_profile(ex)
    atomic_json(ex.folder / RUNTIME_FILE, profile)
    manifest = dict(reference=REFERENCE, cache='local/runtime/' + RUNTIME_FILE, **profile)
    atomic_json(PROJECT / 'manifests' / MANIFEST_FILE, manifest)
    print(json.dumps(dict(cache=manifest['cache'], clip=profile['sound']['clips'],
                          action=profile['action'], source=profile['sound_source']), ensure_ascii=False))


if __name__ == '__main__':
    main()
