"""Recover catalog Actor templates separately from the existing GenFX display meshes."""
import json
from .asset_lock import atomic_json, asset_lock
from .export_runtime import RuntimeExport
from .native_effects import identity, pointer_key, export_program, material
from UnityPy.helpers.MeshHelper import MeshHandler
from .importer import PROJECT

TEMPLATES = {'MODEL_ACTOR_MINION':'23b7de16184fa8042bf6b734e7ca4d60',
             'MODEL_ACTOR_HERO':'42cbbd2c4969afb46b3887bb628de19d'}


def export(ex, logical, guid):
    root,bundle,_=ex.imp.reader.resolve(guid)
    name=root.read_typetree()['m_Name']
    ex.scene(logical,name,bundle,source_ref=guid)
    path=ex.folder/ex.registry[logical]
    scene=json.loads(path.read_text(encoding='utf-8'))
    originals=[];nodes={};actors=[]
    def visit(go,parent):
        tree=go.read_typetree();index=len(originals);nodes[pointer_key(go)]=index
        originals.append((tree['m_Name'],parent));transform=None
        for c in tree['m_Component']:
            component=ex.imp.reader.pointer(go,c['component'])
            if component.type.name=='Transform':transform=component
            if component.type.name=='MonoBehaviour':
                values=component.read_typetree();script=ex.imp.reader.pointer(component,values['m_Script'])
                if script.read_typetree()['m_ClassName']=='Actor':actors.append((index,component,values))
        for child in transform.read_typetree()['m_Children']:
            t=ex.imp.reader.pointer(transform,child)
            visit(ex.imp.reader.pointer(t,t.read_typetree()['m_GameObject']),index)
    visit(root,-1)
    if originals!=[(n['name'],n['parent']) for n in scene['nodes']]:raise ValueError('Actor source hierarchy differs from scene export')
    bindings=[]
    for index,component,values in actors:
        portrait=ex.imp.reader.pointer(component,values['m_portraitMesh'])
        portrait_index=nodes.get(pointer_key(portrait),-1)
        if portrait is not None and portrait_index<0:raise ValueError('Actor portrait lies outside exported hierarchy')
        bindings.append(dict(owner=index,portrait=portrait_index,portrait_material_index=values['m_portraitMatIdx'],spell_table=values.get('m_spellTablePrefab',''),source=identity(ex,component)))
    if len(bindings)!=1:raise ValueError('Catalog template must contain exactly one Actor')
    animation_file,animation_audit=export_program(ex,guid,suffix='.actor-animation.json')
    bindings[0]['animation_file']=animation_file
    scene['actors']=bindings
    material_audit=[];material_errors=[]
    for index,component,values in actors:
        portrait=ex.imp.reader.pointer(component,values['m_portraitMesh'])
        if portrait is None:continue
        node=scene['nodes'][nodes[pointer_key(portrait)]]
        for entry in portrait.read_typetree()['m_Component']:
            source=ex.imp.reader.pointer(portrait,entry['component'])
            if source.type.name=='MeshRenderer':
                for slot,pointer in enumerate(source.read_typetree()['m_Materials']):
                    original=ex.imp.reader.pointer(source,pointer)
                    recovered=material(ex,original,material_errors)
                    node['materials'][slot]['native']=recovered
                    material_audit.append(dict(node=nodes[pointer_key(portrait)],slot=slot,material=recovered))
            if source.type.name=='MeshFilter':
                mesh=ex.imp.reader.pointer(source,source.read_typetree()['m_Mesh'])
                handler=MeshHandler(mesh.read());handler.process()
                mesh_path=ex.folder/node['mesh'];data=json.loads(mesh_path.read_text(encoding='utf-8'))
                data['uv2']=[v for row in (handler.m_UV1 or []) for v in row[:2]]
                atomic_json(mesh_path,data)
    with asset_lock(ex.imp.local):atomic_json(path,scene)
    return dict(id=logical,guid=guid,source=identity(ex,root),actors=bindings,nodes=len(scene['nodes']),cache='local/runtime/'+path.name,animation=animation_audit,portrait_materials=material_audit,material_errors=material_errors)


def main():
    ex=RuntimeExport();rows=[export(ex,logical,guid) for logical,guid in TEMPLATES.items()];ex.save()
    atomic_json(PROJECT/'manifests/original-actor-templates.json',dict(templates=rows,complete=False,errors=ex.errors,
        notes=['Original catalog Actor structure and references; original Actor scripts, materials and SpellTable scheduling are not fully restored.']))


if __name__=='__main__':main()
