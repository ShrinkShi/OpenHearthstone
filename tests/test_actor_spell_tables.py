import json
from types import SimpleNamespace
import pytest
from tools.actor_spell_tables import read_table, refresh_positions


class Object:
    assets_file=SimpleNamespace(name='cab')
    def __init__(self,pathid,kind,tree):
        self.path_id=pathid;self.type=SimpleNamespace(name=kind);self.tree=tree
    def read_typetree(self):return self.tree


def fixture_exporter(tmp_path,components=1):
    root=Object(1,'GameObject',{'m_Component':[{'component':i+2} for i in range(components)]})
    script=Object(8,'MonoScript',{'m_ClassName':'SpellTable'})
    table=Object(2,'MonoBehaviour',{'m_Script':8,'m_Table':[
        {'m_Type':4,'m_Spell':0,'m_SpellPrefabName':'first-guid'},
        {'m_Type':4,'m_Spell':0,'m_SpellPrefabName':'duplicate-guid'}]})
    objects={0:None,2:table,3:table,8:script}
    reader=SimpleNamespace(resolve=lambda ref:(root,'bundle','global'),pointer=lambda obj,ptr:objects[ptr])
    return SimpleNamespace(imp=SimpleNamespace(reader=reader),folder=tmp_path)


def test_source_order_duplicates_and_null_inline_are_preserved(tmp_path,monkeypatch):
    monkeypatch.setattr('tools.actor_spell_tables.identity',lambda ex,obj:{'pathid':str(obj.path_id)})
    result=read_table(fixture_exporter(tmp_path),'table-guid')
    assert [row['reference'] for row in result['entries']]==['first-guid','duplicate-guid']
    assert [row['index'] for row in result['entries']]==[0,1]
    assert all(row['type']==4 and row['inline_source'] is None for row in result['entries'])
    assert result['source']['pathid']=='2'


def test_inline_live_spell_keeps_source_identity(tmp_path,monkeypatch):
    monkeypatch.setattr('tools.actor_spell_tables.identity',lambda ex,obj:{'pathid':str(obj.path_id)})
    exporter=fixture_exporter(tmp_path)
    root=exporter.imp.reader.resolve('guid')[0]
    table=exporter.imp.reader.pointer(root,2);table.tree['m_Table'][0]['m_Spell']=8
    assert read_table(exporter,'guid')['entries'][0]['inline_source']=={'pathid':'8'}


@pytest.mark.parametrize('components',[0,2])
def test_ambiguous_or_missing_source_component_rejected(tmp_path,components):
    with pytest.raises(ValueError,match='exactly one SpellTable'):
        read_table(fixture_exporter(tmp_path,components),'table-guid')


def test_root_position_refresh_is_deduplicated_and_preserves_program(tmp_path):
    root=Object(1,'GameObject',{'m_Component':[{'component':2}]})
    transform=Object(2,'Transform',{'m_LocalPosition':{'x':3,'y':4,'z':5}})
    resolved=[]
    def resolve(ref):resolved.append(ref);return root,'bundle','global'
    exporter=SimpleNamespace(folder=tmp_path,imp=SimpleNamespace(reader=SimpleNamespace(resolve=resolve,pointer=lambda *args:transform)))
    program={'nodes':[{'position':{'x':0,'y':0,'z':0}}],'machines':[{'original':'unchanged'}]}
    (tmp_path/'sample.actor-spell.json').write_text(json.dumps(program))
    rows=[{'file':'sample.actor-spell.json','reference':'guid'}]*2+[{'file':'','reference':'unresolved'}]
    (tmp_path/'actor-spell-tables.json').write_text(json.dumps({'tables':[{'entries':rows}]}))
    refresh_positions(exporter)
    result=json.loads((tmp_path/'sample.actor-spell.json').read_text())
    assert resolved==['guid'] and result['nodes'][0]['position']=={'x':3,'y':4,'z':5}
    assert result['machines']==program['machines']
    before=(tmp_path/'sample.actor-spell.json').read_bytes();refresh_positions(exporter)
    assert (tmp_path/'sample.actor-spell.json').read_bytes()==before
