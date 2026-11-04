using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorSpellTableChecks
{
    sealed class Spell{public int type,shows,hides;public bool attached,released;}
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original SpellTable: "+message);passed++;}
    public static void Run(AssetRegistry assets)
    {
        passed=0;int created=0,released=0;
        var first=new NativeActorSpellEntry{type=2,reference="first"};
        var definition=new NativeActorSpellDefinition{entries=new[]{first,new NativeActorSpellEntry{type=2,reference="duplicate"},new NativeActorSpellEntry{type=3,reference=""},new NativeActorSpellEntry{type=4,reference="missing"},new NativeActorSpellEntry{type=5,reference="broken",error="unsupported inline source"},new NativeActorSpellEntry{type=6,reference="inline",inline_source=new NativeAssetSource{pathid="123"}}}};
        var table=new NativeActorSpellTable<Spell>(definition,e=>{created++;return e.type==4?null:new Spell();},(s,t)=>s.type=t,s=>s.attached=true,s=>{s.released=true;released++;},s=>s!=null&&!s.released);
        Check(table.DuplicateEntries==1&&table.Entry(2)==first,"duplicate source slots keep first entry");
        Check(table.GetLoaded(2)==null&&table.GetSpell(99,true)==null&&created==0,"lookup does not create missing slots");
        var local=table.GetSpell(2,true);Check(local.type==2&&local.attached&&created==1,"local creates, sets exact SpellType, then attaches");
        Check(table.GetSpell(2,true)==local&&created==1,"local source instance is reused");
        var fresh=table.GetSpell(2,false);Check(fresh!=local&&!fresh.attached&&fresh.type==2&&created==2,"nonlocal is fresh without attachment");
        Check(table.GetLoaded(2)==local,"nonlocal creation does not replace local cache");
        Check(table.GetSpell(3,true)==null&&created==2,"empty original GUID does not create");
        Check(table.GetSpell(4,true)==null&&table.GetLoaded(4)==null&&created==3,"missing prefab result is not cached");
        Check(table.GetSpell(5,true)==null&&created==3,"unrestored source binding is rejected");
        Check(table.GetSpell(6,true)==null&&created==3,"unrestored inline live Spell is never replaced by a fabricated prefab");
        local.released=true;Check(table.GetLoaded(2)==null,"destroyed Unity-style cached instance is not returned");
        var replacement=table.GetSpell(2,true);Check(replacement!=local&&created==4&&replacement.attached,"destroyed local is recreated");
        table.ReleaseAll();Check(released==1&&replacement.released&&!fresh.released&&table.GetLoaded(2)==null,"release removes local ownership only");
        table.ReleaseAll();Check(released==1,"repeated release is empty");
        int visibilityCreates=0;
        var visibility=new NativeActorSpellTable<Spell>(new NativeActorSpellDefinition{entries=new[]{new NativeActorSpellEntry{type=0,reference="zero"},new NativeActorSpellEntry{type=7,reference="damage"},new NativeActorSpellEntry{type=42,reference="summon"}}},
            e=>{visibilityCreates++;return new Spell();},(s,t)=>s.type=t,s=>s.attached=true,s=>s.released=true,s=>s!=null&&!s.released,s=>s.shows++,s=>s.hides++);
        visibility.Show();visibility.Hide();Check(visibilityCreates==0,"visibility traversal never instantiates unloaded source slots");
        var zero=visibility.GetSpell(0,true);var damage=visibility.GetSpell(7,true);var outside=visibility.GetSpell(7,false);
        visibility.Show();Check(zero.shows==0&&damage.shows==1&&outside.shows==0,"original local Show skips type zero and nonlocal instances");
        visibility.Hide();Check(zero.hides==1&&damage.hides==1&&outside.hides==0,"original local Hide includes type zero but excludes nonlocal instances");
        damage.released=true;visibility.Show();visibility.Hide();Check(damage.shows==1&&damage.hides==1&&zero.hides==2,"destroyed local instances are ignored without recreating");
        Check(visibilityCreates==3,"Show and Hide preserve lazy source creation");
        visibility.ReleaseAll();visibility.Show();visibility.Hide();Check(zero.hides==2&&visibility.GetLoaded(0)==null,"released table has no visibility callbacks");
        bool unbound=false;try{table.Show();}catch(InvalidOperationException){unbound=true;}Check(unbound,"missing original Show callback is explicit, not a silent root toggle");
        unbound=false;try{table.Hide();}catch(InvalidOperationException){unbound=true;}Check(unbound,"missing original Hide callback is explicit");
        foreach(string model in new[]{"MODEL_ACTOR_MINION","MODEL_ACTOR_HERO"})
        {
            var scene=assets.Scene(model);var actor=scene.GetComponentInChildren<NativeActorIdentity>(true);
            var parent=new GameObject("Original SpellTable attachment fixture");parent.transform.SetParent(actor.transform,false);
            parent.transform.localPosition=new Vector3(2,3,4);parent.transform.localRotation=Quaternion.Euler(0,35,0);parent.transform.localScale=Vector3.one*1.25f;
            NativeActorSpellTable<NativeEffectInstance> native=null;
            try
            {
                Check(actor.SpellDefinition!=null&&actor.SpellDefinition.reference==actor.SpellTable,"exact catalog table binds source Actor "+model);
                native=new NativeActorSpellTable<NativeEffectInstance>(actor.SpellDefinition,e=>new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,e.file))),assets,0,32,32),
                    (s,t)=>s.SetSpellType(t),s=>s.AttachToOriginalParent(parent),s=>s.Dispose(),s=>s!=null&&s.Root!=null);
                Check(native.DuplicateEntries==actor.SpellDefinition.entries.Length-actor.SpellDefinition.entries.Select(e=>e.type).Distinct().Count(),"real duplicate slots follow source first-wins "+model);
                // DAMAGE is present in both source tables; test source instance, not a fabricated FSM.
                var instance=native.GetSpell(7,true);Check(instance!=null&&instance.OriginalSpellType==7,"real DAMAGE source prefab selected "+model);
                Check(instance.Root.transform.parent==parent.transform&&instance.Root.transform.localPosition==Vector3.zero,"capture holder attaches in local coordinates "+model);
                Check(instance.Nodes[0].transform.localPosition==instance.Program.nodes[0].position,"authored source root transform survives attachment "+model);
                Check(Vector3.Distance(instance.Nodes[0].transform.position,parent.transform.TransformPoint(instance.Program.nodes[0].position))<.0001f,"original authored pose follows translated/rotated/scaled parent "+model);
                Check(NativeActorIdentity.FindInThisOrParents(instance.Nodes[0])==actor,"source root resolves exact parent Actor "+model);
                if(instance.Program.machines.Length>0)Check(NativeActorIdentity.FindInThisOrParents(instance.MachineOwner(0))==actor,"source FSM owner resolves exact parent Actor "+model);
                else Check(instance.MachineOwner(0)==null,"source prefab without FSM does not fabricate an owner "+model);
                Check(native.GetSpell(7,true)==instance,"real native local instance reused "+model);
                instance.Seek(0);bool rejected=false;try{instance.AttachToOriginalParent(parent);}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"attachment after playback is explicitly rejected "+model);
            }
            finally
            {
                if(native!=null)native.ReleaseAll();
                foreach(var renderer in scene.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);
                AssetRegistry.ReleaseTransient(scene);
            }
        }
        File.WriteAllText("../work/native-spell-table-checks.json","{\"passed\":"+passed+",\"complete\":false}");
        Debug.Log("NATIVE SPELL TABLE CHECKS PASSED: "+passed);
    }
}
