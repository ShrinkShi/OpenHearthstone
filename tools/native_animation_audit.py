"""Publish clip/controller recovery coverage without publishing original bytes."""
import json
from collections import Counter

from .asset_lock import atomic_json
from .importer import PROJECT


def main():
    audit=json.loads((PROJECT/'manifests/native-effect-audit.json').read_text(encoding='utf-8'))
    files=sorted({card['cache'] for card in audit['cards']});counts=Counter();recovered=[]
    for path in files:
        program=json.loads((PROJECT/path).read_text(encoding='utf-8'))
        for clip in program['animations']:
            counts['clip_occurrences']+=1
            if not clip.get('tracks'):counts['zero_track_occurrences']+=1
            if clip.get('encoding')!='mecanim':continue
            counts['decoded_mecanim_occurrences']+=1
            counts['decoded_tracks']+=len(clip['tracks'])
            counts['unresolved_bindings']+=len(clip.get('unresolved_bindings',[]))
            recovered.append(dict(cache=path,id=clip['id'],name=clip['name'],source=clip['source'],
                duration=clip['duration'],tracks=len(clip['tracks']),channels=clip['channel_count'],
                properties=sorted({track['property'] for track in clip['tracks']}),
                unresolved_bindings=clip.get('unresolved_bindings',[])))
        for node in program['nodes']:
            counts['direct_controller_states']+=len(node.get('animator_states',[]))
            counts['unsupported_controller_states']+=len(node.get('animator_unsupported',[]))
    report=dict(scope='unique cached prefabs; clip occurrences are not completed cards',
        prefabs=len(files),counts=dict(counts),decoded_mecanim=recovered,
        limitations=['Mecanim property recovery is not complete original shader execution.',
            'Unknown material hashes, blend trees, transitions and parameters stay unsupported.',
            'Most cached clips have not been re-exported through the Mecanim decoder.',
            'AnimatorPlayAction is a partial direct-state implementation, not complete Unity Animator.'])
    atomic_json(PROJECT/'manifests/native-animation-audit.json',report)
    print(json.dumps(dict(prefabs=len(files),counts=dict(counts))))


if __name__=='__main__':main()
