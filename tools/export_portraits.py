"""供原生客户端按需补取衍生牌原画，跨实例串行合并注册表。"""
import argparse
import json
import re
from pathlib import Path
from .export_runtime import RuntimeExport
from .importer import Importer, DEFAULT_GAME, PROJECT
from .asset_lock import asset_lock


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', required=True)
    parser.add_argument('--local', default=str(PROJECT / 'local'))
    parser.add_argument('--game', default=str(DEFAULT_GAME))
    args = parser.parse_args()
    requested = args.ids.split(',')
    if not requested or any(not re.fullmatch(r'[A-Za-z0-9_]{1,100}', i) for i in requested):
        raise ValueError('invalid portrait id')
    local = Path(args.local)
    local.mkdir(parents=True, exist_ok=True)
    ex = RuntimeExport(Importer(args.game, local))
    for card_id in sorted(set(requested)):
        if 'CARD_'+card_id not in ex.registry:
            ex.portrait(card_id)
    ex.save()
    with asset_lock(local):
        with (local / 'portrait-export.jsonl').open('a', encoding='utf-8') as report:
            report.write(json.dumps({'requested': requested, 'provenance': ex.provenance,
                                     'errors': ex.errors}, ensure_ascii=False)+'\n')
    print(json.dumps({'exported': len(ex.provenance), 'errors': ex.errors}))


if __name__ == '__main__':
    main()
