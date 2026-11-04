"""Activate only explicitly reviewed original-prefab captures, never an entire pool."""
import argparse
import hashlib
import json
import math

from .asset_lock import asset_lock, atomic_json
from .importer import PROJECT


def verify(entry, capture, root):
    if capture['visible_samples']<2 or capture['max_pixels']<128:
        raise ValueError('Original effect has no usable visible interval: '+entry['card_id'])
    if capture.get('black_alpha_pixels',0)>capture['max_pixels']*.1+32:
        raise ValueError('Opaque black mask in capture: '+entry['card_id'])
    path=root/entry['file']
    if hashlib.sha256(path.read_bytes()).hexdigest()!=capture['program_sha256']:
        raise ValueError('Export changed since GPU capture: '+entry['card_id'])
    duration=capture['review_duration']
    if not .1<=duration<=8:
        raise ValueError('Invalid reviewed presentation interval')
    delay=capture.get('impact_delay',-1)
    if not math.isfinite(delay) or delay!=-1 and not 0<=delay<=duration:
        raise ValueError('Invalid reviewed impact delay')
    return dict(entry,enabled=True,duration=duration,replace=capture.get('replace',False),
                impact_delay=delay,
                program_sha256=capture['program_sha256'],verification='offscreen-gpu-and-local-image-review')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--review',required=True);args=parser.parse_args()
    review=json.loads((PROJECT/args.review).read_text(encoding='utf-8'))
    report=json.loads((PROJECT/'tests/visual/current/native-effects/render-report.json').read_text(encoding='utf-8'))
    captures={c['card']:c for c in report['cards']};chosen={c['card']:c for c in review['cards']}
    root=PROJECT/'local/runtime';path=root/'native-effect-index.json'
    with asset_lock(PROJECT/'local'):
        index=json.loads(path.read_text(encoding='utf-8'))
        for n,entry in enumerate(index['entries']):
            if entry['card_id'] in chosen:
                capture=dict(captures[entry['card_id']],**chosen[entry['card_id']])
                index['entries'][n]=verify(entry,capture,root)
            else:
                index['entries'][n]=dict(entry,enabled=False)
        if not chosen.keys()<=set(e['card_id'] for e in index['entries']):raise ValueError('Missing export')
        atomic_json(path,index)
    print('VERIFIED ACTIVE:',', '.join(chosen))


if __name__=='__main__':main()
