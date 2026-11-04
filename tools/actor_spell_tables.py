"""Recover exact catalog SpellTable slots and their original effect programs."""
import json
import argparse
from .export_runtime import RuntimeExport
from .native_effects import identity, export_program
from .asset_lock import atomic_json
from .importer import PROJECT

TABLES=('Card_Play_Ally_SpellTable.prefab:81be4c58f5a9d454e815f1b0703d8179',
        'Card_Play_Hero_SpellTable.prefab:c7908ae61ab34412aa7f5a9bf3c89cf2')


def read_table(ex,reference):
    root,_,_=ex.imp.reader.resolve(reference)
    components=[]
    for item in root.read_typetree()['m_Component']:
        obj=ex.imp.reader.pointer(root,item['component'])
        if obj.type.name!='MonoBehaviour':continue
        tree=obj.read_typetree();script=ex.imp.reader.pointer(obj,tree['m_Script'])
        if script.read_typetree()['m_ClassName']=='SpellTable':components.append((obj,tree))
    if len(components)!=1:raise ValueError('Catalog root must have exactly one SpellTable')
    obj,tree=components[0];rows=[]
    for i,row in enumerate(tree['m_Table']):
        inline=ex.imp.reader.pointer(obj,row['m_Spell'])
        rows.append(dict(index=i,type=row['m_Type'],reference=row['m_SpellPrefabName'],
                         inline_source=identity(ex,inline) if inline else None,file='',error=''))
    return dict(reference=reference,source=identity(ex,obj),entries=rows)


def refresh_positions(ex):
    catalog=json.loads((ex.folder/'actor-spell-tables.json').read_text(encoding='utf-8'))
    seen=set();changed=0
    for table in catalog['tables']:
        for row in table['entries']:
            if not row['file'] or row['file'] in seen:continue
            seen.add(row['file']);root,_,_=ex.imp.reader.resolve(row['reference'])
            transforms=[ex.imp.reader.pointer(root,item['component']) for item in root.read_typetree()['m_Component']]
            transform=next(obj for obj in transforms if obj and obj.type.name=='Transform')
            position=transform.read_typetree()['m_LocalPosition'];path=ex.folder/row['file']
            program=json.loads(path.read_text(encoding='utf-8'))
            if program['nodes'][0]['position']!=position:
                program['nodes'][0]['position']=position;atomic_json(path,program);changed+=1
    print('ACTOR SPELL ROOT POSITIONS',len(seen),'checked,',changed,'updated',flush=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--refresh-positions',action='store_true');args=parser.parse_args()
    if args.refresh_positions:refresh_positions(RuntimeExport());return
    ex=RuntimeExport();tables=[read_table(ex,r) for r in TABLES];programs={};failures=[];done=0
    for table in tables:
        for row in table['entries']:
            ref=row['reference']
            try:
                if row['inline_source'] is not None:raise ValueError('Inline live Spell binding not restored')
                if not ref:continue
                if ref not in programs:
                    file,audit=export_program(ex,ref,suffix='.actor-spell.json');programs[ref]=audit;programs[ref]['file']=file
                row['file']=programs[ref]['file']
            except (ValueError,KeyError,FileNotFoundError,NotImplementedError) as exc:
                row['error']=str(exc);failures.append(dict(table=table['reference'],index=row['index'],reference=ref,error=str(exc)))
            done+=1
            atomic_json(ex.folder/'actor-spell-tables.json',dict(tables=tables,complete=False))
            print('ACTOR SPELL',done,'/',sum(len(t['entries']) for t in tables),ref, 'FAIL' if row['error'] else 'exported',flush=True)
    ex.save()
    atomic_json(PROJECT/'manifests/original-actor-spell-tables.json',dict(tables=tables,programs=list(programs.values()),failures=failures,complete=False,
        notes=['Original source tables and programs; no slots enabled, full Actor/Spell lifecycle, audio and shader fidelity remain incomplete.']))


if __name__=='__main__':main()
