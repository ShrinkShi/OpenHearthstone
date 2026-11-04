"""Refresh exact original SetMesh / SetMaterial references and exported assets."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_scale_cache
from .asset_lock import asset_lock, atomic_json
from .importer import PROJECT
import json


def main():
    ex=RuntimeExport();refresh_scale_cache(ex, {'SetMesh','SetMaterial'}, 'geometry')
    audit=json.loads((PROJECT/'manifests/native-geometry-refresh.json').read_text(encoding='utf-8'));records=[]
    with asset_lock(ex.imp.local):
        for record in audit['programs']:
            program=json.loads((ex.folder/record['file']).read_text(encoding='utf-8'));actions=[]
            for machine in program['machines']:
                for state in machine['states']:
                    for action in state['actions']:
                        if action['name'] not in ('SetMesh','SetMaterial'):continue
                        params={p['name']:p for p in action['params']};parameter=params['mesh' if action['name']=='SetMesh' else 'material']
                        value=json.loads((ex.folder/parameter['asset_path']).read_text(encoding='utf-8'))
                        metadata=(dict(vertices=len(value['vertices'])//3,uv=len(value['uv'])//2,
                                       submeshes=[len(s['triangles']) for s in value['submeshes']])
                                  if action['name']=='SetMesh' else value)
                        actions.append(dict(state=state['name'],action=action['name'],node=params['gameObject']['node'],
                                            source=parameter['asset_source'],file=parameter['asset_path'],metadata=metadata))
            records.append(dict(file=record['file'],reference=program['reference'],actions=actions))
        atomic_json(PROJECT/'manifests/native-geometry-assets.json',dict(programs=records,complete=False,
            notes=['Original mesh bytes/textures stay in local/. Material replacement does not prove complete original shader or MaterialService restoration.']))


if __name__=='__main__':
    main()
