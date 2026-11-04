import json
from types import SimpleNamespace
from tools.export_runtime import RuntimeExport
from tools.actor_templates import export


def test_scene_source_guid_selects_exact_object_among_same_name_rows(tmp_path):
    class Object:
        assets_file=SimpleNamespace(name='cab')
        def __init__(self,pathid,kind,tree):
            self.path_id=pathid;self.type=SimpleNamespace(name=kind);self.tree=tree
        def read_typetree(self):return self.tree
    transform=Object(20,'Transform',{'m_LocalPosition':{'x':2,'y':0,'z':0},
        'm_LocalRotation':{'x':0,'y':0,'z':0,'w':1},'m_LocalScale':{'x':1,'y':1,'z':1},'m_Children':[]})
    chosen=Object(2,'GameObject',{'m_Name':'SameName','m_IsActive':1,'m_Component':[{'component':{'m_PathID':20}}]})
    decoy=Object(1,'GameObject',{'m_Name':'SameName','m_Component':[]})
    rows=[{'id':i,'name':'SameName','bundle':'bundle','cab':'cab','pathid':str(i)} for i in (1,2)]
    class Reader:
        def resolve(self,ref):assert ref=='catalog-guid';return chosen,'bundle','global'
        def load(self,bundle):return SimpleNamespace(objects=[decoy,chosen])
        def pointer(self,obj,ptr):assert ptr['m_PathID']==20;return transform
    exporter=RuntimeExport.__new__(RuntimeExport)
    exporter.imp=SimpleNamespace(reader=Reader(),store=SimpleNamespace(db=SimpleNamespace(execute=lambda *args:SimpleNamespace(fetchall=lambda:rows))))
    exporter.folder=tmp_path;exporter.registry={};exporter.provenance=[];exporter.errors=[];exporter.key=lambda obj:str(obj.path_id)
    exporter.scene('EXACT','SameName','bundle',source_ref='catalog-guid')
    assert exporter.provenance[0]['pathid']=='2'
    assert json.loads((tmp_path/'EXACT.scene.json').read_text())['nodes'][0]['position']['x']==2
    assert not exporter.errors


def test_actor_template_retains_exact_animation_cache_and_audit(tmp_path,monkeypatch):
    class Object:
        assets_file=SimpleNamespace(name='cab')
        def __init__(self,pathid,kind,tree):self.path_id=pathid;self.type=SimpleNamespace(name=kind);self.tree=tree
        def read_typetree(self):return self.tree
    root=Object(1,'GameObject',{'m_Name':'Actor','m_Component':[{'component':2},{'component':3}]})
    transform=Object(2,'Transform',{'m_Children':[]})
    actor=Object(3,'MonoBehaviour',{'m_Script':4,'m_portraitMesh':0,'m_portraitMatIdx':1,'m_spellTablePrefab':'table-guid'})
    script=Object(4,'MonoScript',{'m_ClassName':'Actor'});objects={0:None,2:transform,3:actor,4:script}
    reader=SimpleNamespace(resolve=lambda ref:(root,'bundle','global'),pointer=lambda obj,ptr:objects[ptr])
    scene={'nodes':[{'name':'Actor','parent':-1}]}
    def write_scene(logical,name,bundle,source_ref):
        assert source_ref=='catalog-guid';(tmp_path/'scene.json').write_text(json.dumps(scene))
    exporter=SimpleNamespace(imp=SimpleNamespace(reader=reader,local=tmp_path),folder=tmp_path,registry={'EXACT':'scene.json'},scene=write_scene)
    monkeypatch.setattr('tools.actor_templates.identity',lambda ex,obj:{'pathid':str(obj.path_id)})
    audit={'animations':32,'errors':['unresolved built-in']}
    def export_animation(ex,ref,suffix):
        assert ex is exporter and ref=='catalog-guid' and suffix=='.actor-animation.json'
        return 'original.actor-animation.json',audit
    monkeypatch.setattr('tools.actor_templates.export_program',export_animation)
    result=export(exporter,'EXACT','catalog-guid')
    saved=json.loads((tmp_path/'scene.json').read_text())
    assert saved['actors'][0]['animation_file']=='original.actor-animation.json'
    assert saved['actors'][0]['spell_table']=='table-guid' and saved['actors'][0]['portrait']==-1
    assert result['animation']==audit and result['actors']==saved['actors']
