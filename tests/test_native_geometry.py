import json
from pathlib import Path
from types import SimpleNamespace

import pytest

from tools.native_effects import parameters


ROOT=Path(__file__).parents[1]
AUDIT=json.loads((ROOT/'manifests/native-geometry-assets.json').read_text(encoding='utf-8'))
ACTIONS=[a for p in AUDIT['programs'] for a in p['actions']]


def test_all_original_geometry_refs_are_audited():
    refresh=json.loads((ROOT/'manifests/native-geometry-refresh.json').read_text(encoding='utf-8'))
    assert len(refresh['programs'])==4 and refresh['updated']==0 and not refresh['failures']
    assert len(ACTIONS)==18 and sum(a['action']=='SetMesh' for a in ACTIONS)==8
    assert len({a['file'] for a in ACTIONS if a['action']=='SetMesh'})==4
    assert len({a['file'] for a in ACTIONS if a['action']=='SetMaterial'})==2


@pytest.mark.parametrize('action',ACTIONS,ids=[str(i) for i in range(len(ACTIONS))])
def test_exact_geometry_source_metadata_matches_export(action):
    assert action['source']['type']==('Mesh' if action['action']=='SetMesh' else 'Material')
    value=json.loads((ROOT/'local/runtime'/action['file']).read_text(encoding='utf-8'))
    if action['action']=='SetMesh':
        assert len(value['vertices'])//3==action['metadata']['vertices']>0
        assert [len(s['triangles']) for s in value['submeshes']]==action['metadata']['submeshes']
    else:
        assert value==action['metadata'] and value['source']==action['source']


def test_mesh_object_param_uses_asset_export_not_gameobject_node():
    target=SimpleNamespace(type=SimpleNamespace(name='Mesh'),assets_file=SimpleNamespace(name='exact-cab'),path_id=-19)
    reader=SimpleNamespace(pointer=lambda owner,pointer:target)
    data=dict(actionStartIndex=[0],paramName=['mesh'],paramDataType=[24],paramDataPos=[0],
              fsmObjectParams=[dict(useVariable=False,value=dict(m_FileID=1,m_PathID=-19))])
    actual=parameters(data,0,None,reader,{}, {},export_asset=lambda obj:dict(asset_path='exact.mesh.json',asset_source={'pathid':'-19'}))[0]
    assert actual['asset_type']=='Mesh' and actual['asset_path']=='exact.mesh.json' and not actual.get('unresolved')
    assert parameters(data,0,None,reader,{}, {})[0]['unresolved']
