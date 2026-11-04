"""Refresh original property descriptors through the existing FSM source pipeline."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_color_cache
from .asset_lock import asset_lock, atomic_json
import argparse
import json


def normalize_schema(ex):
    """Derive added literal/null schema fields from the preserved source payload."""
    updated=0
    with asset_lock(ex.imp.local):
        for path in sorted(ex.folder.glob('*.native-effect.json')):
            program=json.loads(path.read_text(encoding='utf-8'));changed=False
            for machine in program['machines']:
                for state in machine['states']:
                    for action in state['actions']:
                        for parameter in action['params']:
                            data=parameter.get('property_data')
                            if not data:continue
                            before=json.dumps(data,sort_keys=True)
                            target=data['raw']['TargetObject'];var=target.get('name','') if target.get('useVariable') else ''
                            data['target_null']=not var and target.get('value',{}).get('m_PathID')==0
                            data['target_unresolved']=not var and not data['target_null'] and data['target_source'] is None
                            for value in data['values']:
                                if value['name']=='IntParameter' and not value['variable']:
                                    raw=data['raw']['IntParameter']['value']
                                    if isinstance(raw,int):value['integer_known']=True;value['integer']=raw
                            changed=changed or before!=json.dumps(data,sort_keys=True)
            if changed:atomic_json(path,program);updated+=1
    print('PROPERTY SCHEMA NORMALIZED:',updated,flush=True)


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--normalize-schema',action='store_true');args=parser.parse_args()
    ex=RuntimeExport()
    if args.normalize_schema:normalize_schema(ex)
    else:refresh_color_cache(ex, {'SetProperty', 'GetProperty'}, label='property')
