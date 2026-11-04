"""Read original Spell/SpellState object bindings; never execute original code."""
import json
import sys
from .importer import PROJECT, Importer
from .native_effects import pointer_key
from .asset_lock import atomic_json

CLASSES = {'Spell', 'SuperSpell', 'SpellState'}


def inspect(importer, path, export_states=False):
    data = json.loads(path.read_text(encoding='utf-8'))
    root, bundle, _ = importer.reader.resolve(data['reference'])
    errors = []
    objects = list(importer.reader.walk(root, include_visual=True, max_nodes=4000, errors=errors))
    source_nodes = []
    node_ids = {}

    def hierarchy(go, parent, path):
        tree = go.read_typetree()
        index = len(source_nodes)
        source_nodes.append((tree['m_Name'], parent, path))
        node_ids[pointer_key(go)] = index
        components = [importer.reader.pointer(go, item['component']) for item in tree['m_Component']]
        transform = next(o for o in components if o is not None and o.type.name == 'Transform')
        for pointer in transform.read_typetree()['m_Children']:
            child = importer.reader.pointer(transform, pointer)
            if child is None:
                raise ValueError('Original child Transform is unresolved')
            child_go = importer.reader.pointer(child, child.read_typetree()['m_GameObject'])
            hierarchy(child_go, index, (path + '/' if path else '') + child_go.read_typetree()['m_Name'])

    hierarchy(root, -1, '')
    cached = [(n['name'], n['parent'], n['path']) for n in data['nodes'][:len(source_nodes)]]
    if cached != source_nodes:
        raise ValueError('Original primary hierarchy differs from cached program: ' + path.name)
    bindings = []
    states = []
    receivers = []
    for obj, tree in objects:
        if obj.type.name != 'MonoBehaviour' or 'm_Script' not in tree:
            continue
        script = importer.reader.pointer(obj, tree['m_Script'])
        if script is None:
            continue
        kind = script.read_typetree()['m_ClassName']
        if export_states and kind in {'SpellState', 'ActorAttackSpell', 'PlayNewParticles', 'SelfAnimUtils', 'TargetAnimUtils'}:
            owner = importer.reader.pointer(obj, tree['m_GameObject'])
            targets = []
            for name in ('m_Target', 'm_Target2', 'm_Target3', 'm_Target4'):
                if kind != 'PlayNewParticles':
                    break
                pointer = tree.get(name, {})
                target = importer.reader.pointer(obj, pointer)
                targets.append(dict(name=name, node=node_ids.get(pointer_key(target), -1),
                                    object_null=pointer.get('m_PathID', 0) == 0))
            receivers.append(dict(node=node_ids.get(pointer_key(owner), -1), kind=kind,
                                  targets=targets, source=dict(cab=obj.assets_file.name, pathid=str(obj.path_id))))
        if kind not in CLASSES:
            continue
        values = {}
        for name, value in tree.items():
            if name in ('m_GameObject', 'm_ObjectContainer') or kind == 'SpellState' and name not in ('m_Script', 'm_Name'):
                values[name] = value
        pointers = {}
        for name in ('m_GameObject', 'm_ObjectContainer'):
            pointer = tree.get(name)
            target = importer.reader.pointer(obj, pointer) if pointer else None
            pointers[name] = dict(cab=pointer_key(target)[0], pathid=str(target.path_id),
                                  name=target.read_typetree().get('m_Name', ''),
                                  node=node_ids.get(pointer_key(target), -1)) if target else None
        bindings.append(dict(kind=kind, cab=obj.assets_file.name, pathid=str(obj.path_id),
                             enabled=tree.get('m_Enabled'), values=values, pointers=pointers))
        if export_states and kind == 'SpellState':
            animated = []
            for item in tree['m_ExternalAnimatedObjects']:
                target = importer.reader.pointer(obj, item['m_GameObject'])
                clip = importer.reader.pointer(obj, item['m_AnimClip'])
                matches = [a for a in data['animations'] if clip is not None
                           and a.get('source', {}).get('cab') == clip.assets_file.name
                           and str(a.get('source', {}).get('pathid')) == str(clip.path_id)]
                animated.append(dict(node=node_ids.get(pointer_key(target), -1),
                    target=item['m_Target'], clip=matches[0]['id'] if len(matches) == 1 else '',
                    clip_null=clip is None and item['m_AnimClip']['m_PathID'] == 0,
                    enabled=bool(item['m_Enabled']), layer=item['m_AnimLayer'],
                    speed=item['m_AnimSpeed'], crossfade=item['m_CrossFadeSec'],
                    particles=bool(item['m_ControlParticles']),
                    unresolved=len(matches) != 1 and item['m_AnimClip']['m_PathID'] != 0
                    or item['m_Target'] == 0 and target is not None and pointer_key(target) not in node_ids))
            states.append(dict(node=pointers['m_GameObject']['node'], type=tree['m_StateType'],
                delay=tree['m_StartDelaySec'], enabled=bool(tree['m_Enabled']),
                audio_count=len(tree['m_AudioSources']), animations=animated,
                source=dict(cab=obj.assets_file.name, pathid=str(obj.path_id), type='MonoBehaviour', bundle=bundle)))
    if export_states and states:
        data['spell_states'] = states
        data['animation_event_receivers'] = receivers
        for clip, _ in objects:
            if clip.type.name != 'AnimationClip':
                continue
            for cached in data['animations']:
                source = cached.get('source', {})
                if source.get('cab') != clip.assets_file.name or str(source.get('pathid')) != str(clip.path_id):
                    continue
                tree = clip.read_typetree()
                events = []
                for event in tree.get('m_Events', []):
                    pointer = event['objectReferenceParameter']
                    target = importer.reader.pointer(clip, pointer)
                    events.append(dict(time=event['time'], function=event['functionName'], text=event['data'],
                        number=event['floatParameter'], integer=event['intParameter'], options=event['messageOptions'],
                        object_null=pointer['m_PathID'] == 0,
                        object_source=dict(cab=target.assets_file.name, pathid=str(target.path_id), type=target.type.name) if target else None,
                        raw=event))
                cached['events'] = events
                # Keep authored curve duration separate from event-only playback horizon.
                cached['event_horizon'] = max((e['time'] for e in events), default=0)
        atomic_json(path, data)
    return dict(file=path.name, reference=data['reference'], bundle=bundle,
                bindings=bindings, dependency_errors=errors)


def main():
    importer = Importer()
    if '--export-states' in sys.argv:
        catalog = json.loads((PROJECT / 'manifests/original-spell-visibility-source.json').read_text(encoding='utf-8'))
        files = [PROJECT / 'local/runtime' / p['file'] for p in catalog['programs']
                 if any(b['kind'] == 'SpellState' for b in p['bindings'])]
        for path in files:
            inspect(importer, path, export_states=True)
        programs = []
        for path in files:
            data = json.loads(path.read_text(encoding='utf-8'))
            programs.append(dict(file=path.name, states=data['spell_states'], receivers=data['animation_event_receivers'],
                animation_events=[dict(clip=a['id'], source=a['source'], events=a['events'], event_horizon=a['event_horizon']) for a in data['animations']]))
        atomic_json(PROJECT / 'manifests/original-spell-state-playback.json',
                    dict(programs=programs, full_spell_controller_restored=False,
                         notes=['Source state selection and supported event callbacks restored; full controller, sound and attack synchronization remain incomplete.']))
        print('SOURCE SPELL STATE EXPORT:', len(programs), 'programs')
        return
    files = sorted((PROJECT / 'local/runtime').glob('*.actor-spell.json'))
    records = []
    for path in files:
        records.append(inspect(importer, path))
    result = dict(programs=records, full_spell_visibility_restored=False,
                  notes=['Exact original component bindings and serialized state data only.',
                         'No official assembly executed; private runtime state is not inferred.',
                         'Spell subclasses other than Spell/SuperSpell/SpellState and original state callbacks remain unbound.'])
    atomic_json(PROJECT / 'manifests/original-spell-visibility-source.json', result)
    print('SPELL VISIBILITY SOURCE:', len(records), 'programs;',
          sum(len(r['bindings']) for r in records), 'components;',
          sum(bool(r['dependency_errors']) for r in records), 'dependency warnings')


if __name__ == '__main__':
    main()
