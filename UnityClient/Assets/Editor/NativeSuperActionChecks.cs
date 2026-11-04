using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSuperActionChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original SuperSpell dispatch: "+reason);passed++;}
    public static void Run()
    {
        passed=0;
        var calls=new List<string>();int pending=0;bool content=false;
        var services=new NativeSuperActionServices{
            DetermineArea=()=>{calls.Add("determine");return null;},SpawnArea=a=>calls.Add("area"),
            HasMissile=()=>false,HasImpact=()=>false,HasChain=()=>false,MissilesInSequence=()=>false,
            VisualTargetCount=()=>0,CurrentTargetIndex=()=>7,SpawnChainMissile=()=>calls.Add("chain missile"),
            SpawnMissileInSequence=()=>calls.Add("sequential missiles"),SpawnAllMissiles=()=>calls.Add("all missiles"),
            SpawnImpact=i=>calls.Add("impact "+i),SpawnAllImpacts=()=>calls.Add("all impacts"),SpawnChain=()=>calls.Add("chain"),
            DoStartSpellAction=()=>calls.Add("start"),PendingEffects=()=>pending,HasStateContent=t=>content,
            BaseSpellFinished=()=>calls.Add("spell finished"),BaseStateFinished=()=>calls.Add("state finished"),CleanUp=()=>calls.Add("cleanup")};
        var flow=new NativeSuperAction(services);
        pending=1;flow.UpdatePendingStateChangeFlags(3);flow.FinishIfPossible();
        Check(calls.Count==0&&flow.PendingSpellFinish&&flow.PendingNoneStateChange,"active effects defer parent completion");
        pending=0;flow.SettingUpAction=true;flow.FinishIfPossible();Check(calls.Count==0,"synchronous child completion cannot finish during setup");
        flow.SettingUpAction=false;flow.FinishIfPossible();
        Check(String.Join(",",calls)=="spell finished,state finished,cleanup"&&!flow.PendingSpellFinish&&!flow.PendingNoneStateChange,"finish before None transition, then cleanup");
        calls.Clear();flow.FinishIfPossible();Check(String.Join(",",calls)=="cleanup","cleared flags do not redeliver completion");
        content=true;flow.UpdatePendingStateChangeFlags(3);Check(!flow.PendingSpellFinish&&!flow.PendingNoneStateChange,"state content clears both pending flags");
        calls.Clear();pending=1;flow.OnStateFinished(2);Check(calls.SequenceEqual(new[]{"state finished"}),"usable next state need not wait for children");
        calls.Clear();flow.OnStateFinished(0);flow.OnSpellFinished();Check(calls.Count==0&&flow.PendingNoneStateChange&&flow.PendingSpellFinish,"terminal state and spell wait independently");
        pending=-1;Check(!flow.AreEffectsActive(),"original activity compares positive count, not nonzero");
        calls.Clear();flow.FinishIfPossible();
        pending=1;content=true;flow.UpdatePendingStateChangeFlags(3);
        flow.OnFsmStateStarted(0,0,()=>calls.Add("fsm started"));
        Check(!flow.PendingSpellFinish&&!flow.PendingNoneStateChange,"FSM ignores matching active change before checking pending children");
        calls.Clear();flow.OnFsmStateStarted(3,0,()=>calls.Add("fsm started"));
        Check(calls.Count==0&&flow.PendingSpellFinish&&flow.PendingNoneStateChange,"unexpected None while effects active defers both completions");
        flow.OnFsmStateStarted(3,2,()=>calls.Add("fsm started"));
        Check(calls.SequenceEqual(new[]{"fsm started"}),"nonterminal FSM state delegates to base even with active effects");
        int basePurge=0;
        Check(!flow.CanPurge(1,()=>{basePurge++;return true;})&&basePurge==0,"live clone blocks purge without calling base");
        Check(flow.CanPurge(0,()=>{basePurge++;return true;})&&basePurge==1,"no clone delegates purge to base");
        pending=0;flow.FinishIfPossible();calls.Clear();
        // Expected branches are derived from the original DoActionNow IL. Missile
        // paths own their start activation; dispatcher must not invoke it again.
        foreach(bool missile in new[]{false,true})foreach(bool impact in new[]{false,true})foreach(bool chain in new[]{false,true})
        foreach(bool sequence in new[]{false,true})foreach(int targets in new[]{0,2})
        {
            calls.Clear();services.HasMissile=()=>missile;services.HasImpact=()=>impact;services.HasChain=()=>chain;
            services.MissilesInSequence=()=>sequence;services.VisualTargetCount=()=>targets;
            flow.DoActionNow();
            string branch=targets==0||(!missile&&!impact&&!chain)?"start":missile?(chain?"chain missile":sequence?"sequential missiles":"all missiles"):
                (impact?(chain?"impact 7,":"all impacts,"):"")+(chain?"chain,":"")+"start";
            Check(String.Join(",",calls)=="determine,"+branch+",cleanup","original target/missile/impact/chain branch");
        }
        VerifyRealAreaCompletion();
        Debug.Log("NATIVE SUPER ACTION: "+passed+" checks passed; original dispatch/completion only, full delays/start/missile/history/scene execution remains incomplete");
        NativeSuperAreaChecks.Run();
    }
    [Serializable] sealed class Prefab{public string reference,file;}
    [Serializable] sealed class Prefabs{public Prefab[] prefabs;}
    static void VerifyRealAreaCompletion()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<NativeSuperAreaCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-config.json")));
        var prefabs=JsonUtility.FromJson<Prefabs>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-prefabs.json"))).prefabs;
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        foreach(string card in new[]{"CS2_025","CS2_062"})
        {
            var root=new GameObject("Original SuperSpell completion host");root.transform.position=new Vector3(20000,10000,20000);
            NativeAreaInstance clone=null;NativeSuperArea area=null;NativeSuperAction action=null;var calls=new List<string>();
            try
            {
                var config=catalog.programs.Single(p=>p.file==index.entries.Single(e=>e.card_id==card).file).config;
                var info=NativeSuperArea.Determine(config,true);var prefab=prefabs.Single(p=>p.reference==info.prefab_reference);
                area=new NativeSuperArea(new NativeSuperAreaServices{Root=root,Source=()=>null,
                    GetSpell=i=>{clone=new NativeAreaInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file))),assets);return clone.Endpoint;},
                    ReleaseSpell=(c,reset)=>false,FinishIfPossible=()=>action.FinishIfPossible()});
                action=new NativeSuperAction(new NativeSuperActionServices{
                    DetermineArea=()=>info,SpawnArea=area.Spawn,HasMissile=()=>false,HasImpact=()=>false,HasChain=()=>false,
                    VisualTargetCount=()=>0,DoStartSpellAction=()=>calls.Add("start"),PendingEffects=()=>area.Pending,HasStateContent=t=>false,
                    BaseSpellFinished=()=>calls.Add("spell finished"),BaseStateFinished=()=>calls.Add("state finished"),CleanUp=()=>calls.Add("cleanup")});
                action.SettingUpAction=true;action.UpdatePendingStateChangeFlags(3);action.DoActionNow();
                Check(calls.SequenceEqual(new[]{"start"})&&area.Pending==1,"real source area blocks parent during setup "+card);
                action.SettingUpAction=false;action.FinishIfPossible();Check(calls.Count==1,"parent remains blocked by live source area "+card);
                clone.Seek(3);
                Check(area.Pending==0&&String.Join(",",calls)=="start,spell finished,state finished,cleanup","real source FSM completion resumes parent once "+card);
            }
            finally{if(clone!=null)clone.Dispose();AssetRegistry.ReleaseTransient(root);}
        }
    }
}
