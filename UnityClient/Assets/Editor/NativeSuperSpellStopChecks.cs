using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 真实原克隆的同步停止边界；不以 pending getter 代替物理图/RT 回收。
public static class NativeSuperSpellStopChecks
{
    [Serializable] sealed class Asset{public string reference,file;}
    [Serializable] sealed class Catalog{public NativeSuperSpellConfig[] programs;public Asset[] prefabs;}
    static int passed,cases;
    static void Check(bool value,string label){if(!value)throw new Exception("SuperSpell stop boundary: "+label);passed++;}
    static IEnumerator Empty(){yield break;}
    static IEnumerator Complete(Action callback){callback();yield break;}
    sealed class Scope : IDisposable
    {
        readonly GameObject root,source,target;
        readonly AssetRegistry assets;readonly Catalog catalog;readonly NativeSuperSpellConfig config;
        readonly string kind,stage;readonly bool disposeAtStop;
        string lastKind;bool releaseReentered;
        public readonly NativeSuperSpell Flow;
        public readonly List<NativeAreaInstance> Made=new List<NativeAreaInstance>();
        public readonly List<GameObject> Objects=new List<GameObject>();
        public readonly List<RenderTexture> Textures=new List<RenderTexture>();
        public readonly List<Action> LateCallbacks=new List<Action>();
        public readonly Dictionary<NativeAreaInstance,int> ReleaseAttempts=new Dictionary<NativeAreaInstance,int>();
        public int CallsAfterStop,Releases;public bool StopReached,ReenterRelease,FailFirstRelease;
        public int Attempts{get{return ReleaseAttempts.Values.Sum();}}
        public Scope(AssetRegistry assets,Catalog catalog,string card,string kind,string stage,bool disposeAtStop=false)
        {
            this.assets=assets;this.catalog=catalog;this.kind=kind;this.stage=stage;this.disposeAtStop=disposeAtStop;
            config=catalog.programs.Single(p=>p.card_id==card);
            root=new GameObject("Stop check root");root.transform.position=new Vector3(20000,10000,20000);
            source=new GameObject("Stop check source");source.transform.position=root.transform.position;
            target=new GameObject("Stop check target");target.transform.position=root.transform.position+new Vector3(3,0,4);
            var history=stage=="attach"?new NativeSuperSpellHistory{Tasks=new NativePowerTaskList(),MetaDataIndex=i=>-1,
                ShouldCompleteUntil=i=>false,CompleteUntil=i=>Empty(),CompleteFrom=(i,done)=>Complete(done),
                Pending=()=>0,Failed=()=>false,Cancel=()=>{}}:null;
            Flow=new NativeSuperSpell(config,new NativeSuperSpellServices{Root=root,Source=()=>
                {if(stage=="source-query"&&lastKind==kind)Stop();return source;},Targets=new List<GameObject>{target},
                Friendly=true,Range=(a,b)=>a,RandomBool=()=>true,History=history,Create=Create,Release=Release,Trace=(phase,index,time)=>
                {if(stage=="clone-trace"&&phase==(kind=="area"?"AreaClone":kind=="missile"?"MissileClone":"ImpactClone")||stage=="arrival-trace"&&phase=="Arrival")Stop();}});
        }
        void Stop(){if(StopReached)return;StopReached=true;if(disposeAtStop)Flow.Dispose();else Flow.Cancel();}
        void Write(string actualKind,string operation,Action callback)
        {
            if(Flow.Cancelled)CallsAfterStop++;
            callback();if(actualKind==kind&&operation==stage)Stop();
        }
        NativeAreaInstance Create(string reference)
        {
            if(Flow.Cancelled)CallsAfterStop++;
            string actualKind=reference==config.missile_reference?"missile":reference==config.impact.prefab.prefab_reference?"impact":"area";
            lastKind=actualKind;
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,catalog.prefabs.Single(p=>p.reference==reference).file)));
            var clone=new NativeAreaInstance(program,assets,42+Made.Count,locations:value=>source,facings:value=>target,attachTasks:tasks=>true,
                context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?target:source),startAtFsmEntry:true);
            Made.Add(clone);Objects.Add(clone.Effect.Root);
            Textures.AddRange(clone.Effect.Root.GetComponentsInChildren<Camera>(true).Select(camera=>camera.targetTexture).Where(texture=>texture!=null));
            var endpoint=clone.Endpoint;
            var started=endpoint.AddStateStarted;endpoint.AddStateStarted=callback=>Write(actualKind,"subscribe-state",()=>{LateCallbacks.Add(callback);started(callback);});
            var finished=endpoint.AddFinished;endpoint.AddFinished=callback=>Write(actualKind,"subscribe-finished",()=>{LateCallbacks.Add(callback);finished(callback);});
            var setSource=endpoint.SetSource;endpoint.SetSource=value=>Write(actualKind,"source",()=>setSource(value));
            var position=endpoint.SetPosition;endpoint.SetPosition=value=>Write(actualKind,"position",()=>position(value));
            var location=endpoint.SetLocation;endpoint.SetLocation=(value,parent)=>Write(actualKind,"location",()=>location(value,parent));
            var updatePosition=endpoint.UpdatePosition;endpoint.UpdatePosition=()=>Write(actualKind,"update-position",updatePosition);
            var orientation=endpoint.UpdateOrientation;endpoint.UpdateOrientation=()=>Write(actualKind,"orientation",orientation);
            var attach=endpoint.AttachTasks;endpoint.AttachTasks=tasks=>{bool result=false;Write(actualKind,"attach",()=>result=attach(tasks));return result;};
            var activate=endpoint.Activate;endpoint.Activate=()=>Write(actualKind,"activate",activate);
            clone.Effect.SourceStateStarted+=type=>
            {
                if(actualKind==kind&&(stage=="activation-callback"&&actualKind!="missile"&&type!=0||stage=="birth-callback"&&actualKind=="missile"&&type==1))Stop();
            };
            if(actualKind==kind&&stage=="factory")Stop();return clone;
        }
        void Release(NativeAreaInstance clone)
        {
            int count;ReleaseAttempts.TryGetValue(clone,out count);ReleaseAttempts[clone]=count+1;
            if(ReenterRelease&&!releaseReentered){releaseReentered=true;Flow.Cancel();}
            if(FailFirstRelease&&Attempts==1)throw new InvalidOperationException("stop release retry");
            clone.Dispose();Releases++;
        }
        public void Advance()
        {
            Flow.Begin();for(int i=1;i<=720&&!StopReached;i++)Flow.Tick(i/120f);
        }
        public void Verify(string label)
        {
            Check(StopReached&&Flow.Cancelled,"stop actually reached "+label);
            Check(Made.Count>0&&Flow.ActiveClones==0&&Objects.All(obj=>obj==null),"public call returns after physical graph release "+label);
            Check(Textures.Count>0&&Textures.All(texture=>texture==null),"all real clone output RTs destroyed "+label);
            Check(CallsAfterStop==0&&!Flow.Completed&&Flow.FinishedNotifications==0,"no later configuration/activation or false completion "+label);
            int releases=Releases;
            foreach(var callback in LateCallbacks)callback();Flow.Cancel();Flow.Cancel();
            if(disposeAtStop)
            {bool rejected=false;try{Flow.Tick(8);}catch(ObjectDisposedException){rejected=true;}Check(rejected,"disposed clock remains rejected "+label);}
            else Flow.Tick(8);
            Check(!Flow.Faulted&&Releases==releases&&ReleaseAttempts.All(pair=>pair.Value==1)&&Flow.ActiveClones==0,
                "late callbacks/tick/repeated stop cannot retry successful releases "+label);
        }
        public void Dispose()
        {
            FailFirstRelease=false;Flow.Cancel();Flow.Dispose();foreach(var clone in Made)clone.Dispose();
            AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);
        }
    }
    public static void Run()
    {
        passed=cases=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(string kind in new[]{"area","missile","impact"})
        {
            if(card=="EX1_134"&&kind=="area")continue;
            var stages=new List<string>{"factory","clone-trace","subscribe-state","subscribe-finished","source"};
            stages.AddRange(kind=="impact"?new[]{"location","update-position","orientation"}:new[]{"position"});
            stages.AddRange(kind=="missile"?new[]{"birth-callback"}:new[]{"activate","activation-callback"});
            if(kind=="missile")stages.Add("arrival-trace");
            if(kind=="area")stages.AddRange(new[]{"source-query","attach"});
            foreach(string stage in stages)using(var scope=new Scope(assets,catalog,card,kind,stage))
            {string label=card+"/"+kind+"/"+stage;scope.Advance();scope.Verify(label);cases++;}
            using(var scope=new Scope(assets,catalog,card,kind,"factory",true))
            {scope.Advance();scope.Verify(card+"/"+kind+"/factory Dispose");cases++;}
        }
        using(var scope=new Scope(assets,catalog,"EX1_129","impact","location")){scope.ReenterRelease=true;scope.Advance();
            Check(scope.Made.Count>1,"reentrant release traverses a real multi-clone snapshot");scope.Verify("multi-clone release reentry");cases++;}
        using(var scope=new Scope(assets,catalog,"EX1_129","area","factory"))
        {
            scope.ReenterRelease=true;scope.FailFirstRelease=true;scope.Advance();
            Check(scope.Flow.Cancelled&&scope.Flow.Faulted&&scope.Flow.LastError.Message=="stop release retry"&&scope.Flow.ActiveClones==1&&scope.Attempts==1,
                "failed late return release stays owned and diagnostic");
            scope.Flow.Tick(8);Check(scope.Attempts==1&&scope.Textures.Any(texture=>texture!=null),"stopped sampling cannot hide a failed release by automatic retry");
            scope.Flow.Cancel();Check(scope.Attempts==2&&scope.Releases==1&&scope.Flow.ActiveClones==0&&scope.Objects.All(obj=>obj==null)&&scope.Textures.All(texture=>texture==null),
                "explicit retry physically recovers after release failure");
            foreach(var callback in scope.LateCallbacks)callback();scope.Flow.Cancel();
            Check(scope.Attempts==2&&scope.CallsAfterStop==0&&!scope.Flow.Completed,"recovered release and cancellation remain idempotent");cases++;
        }
        Debug.Log("NATIVE SUPER SPELL STOP: "+passed+" checks / "+cases+" real-clone cases passed");
    }
}
