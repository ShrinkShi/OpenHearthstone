"""Decode original FsmProperty descriptors without invoking managed scripts."""
from .native_effects import pointer_key


def decode(raw, owner, reader, node_ids, variables):
    target = raw['TargetObject']
    variable = target.get('name', '') if target.get('useVariable') else ''
    value = variables.get(variable, target) if variable else target
    value = value.get('value', value)
    obj = reader.pointer(owner, value) if isinstance(value, dict) and 'm_PathID' in value else None
    node = -1
    source = None
    if obj is not None:
        source = dict(cab=obj.assets_file.name, pathid=str(obj.path_id), type=obj.type.name)
        go = obj if obj.type.name == 'GameObject' else reader.pointer(obj, obj.read_typetree().get('m_GameObject', {}))
        node = node_ids.get(pointer_key(go), -1)
    values = []
    for name, data in raw.items():
        if name in ('TargetObject', 'TargetTypeName', 'PropertyName', 'setProperty'):
            continue
        if not isinstance(data, dict):
            continue
        var = data.get('name', '') if data.get('useVariable') else ''
        named = variables.get(var, data) if var else data
        val = named.get('value', named.get('intValue', 0))
        param = dict(name=name, variable=var, none_known=True,
                     is_none=bool(data.get('useVariable')) and not var, number=0, node=-1, text='')
        if name=='IntParameter' and not var and isinstance(val,int):
            param['integer_known']=True;param['integer']=val
        if var and var not in variables:
            param['unresolved'] = True
        if isinstance(val, (int, float, bool)):
            param['number'] = int(val) if isinstance(val, bool) else val
        elif isinstance(val, str):
            param['text'] = val
        elif isinstance(val, dict):
            if 'm_PathID' in val:
                obj_value = reader.pointer(owner, val)
                param['object_null'] = val['m_PathID'] == 0
                if obj_value is not None:
                    param['node'] = node_ids.get(pointer_key(obj_value), -1)
                    param['asset_type'] = obj_value.type.name
            elif 'r' in val:
                param['color'] = val
            elif 'x' in val:
                param['vector'] = dict(x=val.get('x', 0), y=val.get('y', 0), z=val.get('z', 0))
                param['color'] = dict(r=val.get('x', 0), g=val.get('y', 0), b=val.get('width', val.get('z', 0)), a=val.get('height', val.get('w', 0)))
        values.append(param)
    literal_null=isinstance(value,dict) and value.get('m_PathID')==0 and not variable
    return dict(target_node=node, target_variable=variable, target_null=literal_null,
                target_unresolved=obj is None and not variable and not literal_null,
                target_type=raw['TargetTypeName'], property_name=raw['PropertyName'], set_property=bool(raw['setProperty']),
                target_source=source, values=values, raw=raw)
