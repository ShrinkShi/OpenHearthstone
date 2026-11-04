"""Read original effect components and compiled shader dependencies without execution."""
import argparse
import json
from .export_runtime import RuntimeExport
from .native_effects import identity
from .asset_lock import atomic_json
from .importer import PROJECT


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--ids',default='CS2_026');args=parser.parse_args()
    audit=json.loads((PROJECT/'manifests/native-effect-audit.json').read_text(encoding='utf-8'))
    ex=RuntimeExport();folder=PROJECT/'local/effect-source-audit';folder.mkdir(exist_ok=True)
    for row in audit['cards']:
        if row['key'] not in args.ids.split(','):continue
        root,_,_=ex.imp.reader.resolve(row['reference']);errors=[];objects=list(ex.imp.reader.walk(root,include_visual=True,max_nodes=4000,errors=errors))
        records=[]
        for obj,tree in objects:
            if obj.type.name=='Material':
                shader=ex.imp.reader.pointer(obj,(tree or obj.read_typetree()).get('m_Shader',{}))
                if shader and not any(r['source']==identity(ex,shader) for r in records):
                    records.append(dict(source=identity(ex,shader),tree=shader.read_typetree().get('m_ParsedForm',{})))
            if obj.type.name not in ('MonoBehaviour','Camera','RenderTexture','Shader'):continue
            tree=tree or obj.read_typetree();record=dict(source=identity(ex,obj),tree=tree)
            if obj.type.name=='MonoBehaviour':
                script=ex.imp.reader.pointer(obj,tree.get('m_Script',{}))
                if script:record['script']=script.read_typetree()
            records.append(record)
        atomic_json(folder/(row['key']+'.json'),dict(reference=row['reference'],objects=records,errors=errors))
        for record in records:
            t=record['tree'];print(record['source']['type'],record.get('script',{}).get('m_ClassName',t.get('m_Name','')),list(t)[:18])


if __name__=='__main__':main()
