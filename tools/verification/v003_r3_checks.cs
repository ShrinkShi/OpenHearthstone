using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Independent public-host probes. Copy into Editor only for verification.
public static class V003R3Checks
{
    [Serializable] sealed class Asset { public string reference,file; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs; public Asset[] prefabs; }
    static int passed; static readonly List<string> failures=new List<string>();
    static void Check(bool value,string label)
    { if(value){passed++;Debug.Log("V003_R3_PASS "+label);}else{failures.Add(label);Debug.LogError("V003_R3_FAIL "+label);} }
    static void Case(string label,Action run)
    { try{run();}catch(Exception error){failures.Add(label+": "+error);Debug.LogError("V003_R3_CASE "+label+" "+error);} }

    sealed class Relay : IEnumerator,IDisposable
    {
        readonly IEnumerator inner; readonly bool nested; int step;
        public int Moves,Currents,Disposals; public Action Read,Close;
        public Relay(IEnumerator inner,bool nested){this.inner=inner;this.nested=nested;}
        public bool MoveNext(){Moves++;return nested?step++==0:inner.MoveNext();}
        public object Current { get { Currents++;Read?.Invoke();return nested?(object)inner:inner.Current; } }
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){Disposals++;if(!nested)(inner as IDisposable)?.Dispose();Close?.Invoke();}
    }
    sealed class Scene : IDisposable
    {
        readonly AssetRegistry assets; readonly Catalog catalog;
        public readonly GameObject Root=new GameObject("V003R3 root"),Source=new GameObject("V003R3 source"),Target=new GameObject("V003R3 target");
        public readonly List<NativeAreaInstance> Made=new List<NativeAreaInstance>();
        public readonly List<GameObject> Objects=new List<GameObject>(); public readonly List<RenderTexture> Textures=new List<RenderTexture>();
        public readonly List<Relay> UntilRelays=new List<Relay>(),FromRelays=new List<Relay>();
        public readonly NativeSuperSpellTaskHost Host;public readonly NativeSuperSpellHistory History;public readonly NativeSuperSpell Flow;
        public bool AllowTask,Destination,Loading=true;public int MissileCreates,Tasks;public float Time;
        public Exception Escaped;
        public Scene(AssetRegistry assets,Catalog catalog,string card)
        {
            this.assets=assets;this.catalog=catalog;Root.transform.position=new Vector3(20000,10000,20000);
            Source.transform.position=Root.transform.position;Target.transform.position=Root.transform.position+new Vector3(3,0,4);
            var input=new NativePowerTaskList{tasks=new[]{
                new NativePowerHistoryTask{type=4,entity=20,tag=49,tagChange=true,payloadType="HistTagChange"},
                new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"},
                new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
            var power=new NativePowerServices{GameplayPresent=true,CanDoTask=task=>AllowTask,MulliganBusy=()=>false,EntityType=id=>4,
                BeginZoneChanges=(list,start,end,callback)=>null,TimelineEnd=(id,time,end)=>{}};
            power.handlers[4]=item=>{Tasks++;return true;};power.handlers[8]=item=>true;
            Host=new NativeSuperSpellTaskHost(input,power,new Dictionary<int,int>{{0,7}},new Dictionary<int,int>{{7,1}},
                new NativeSuperSpellZoneWait{VisualEntities=new HashSet<int>{20},Entity=task=>20,
                    InDestinationZone=task=>Destination,ActorLoading=task=>Loading});
            History=Host.Bind();var until=History.CompleteUntil;var from=History.CompleteFrom;
            History.CompleteUntil=index=>Wrap(until(index),UntilRelays);
            History.CompleteFrom=(index,done)=>Wrap(from(index,done),FromRelays);
            var config=catalog.programs.Single(p=>p.card_id==card);
            Flow=new NativeSuperSpell(config,new NativeSuperSpellServices{Root=Root,Source=()=>Source,Targets=new List<GameObject>{Target},
                Friendly=true,History=History,Range=(min,max)=>min,RandomBool=()=>true,Create=reference=>{
                    if(reference==config.missile_reference)MissileCreates++;
                    var file=catalog.prefabs.Single(p=>p.reference==reference).file;
                    var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file)));
                    var clone=new NativeAreaInstance(program,assets,301+Made.Count,locations:value=>Source,facings:value=>Target,
                        attachTasks:tasks=>true,context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?Target:Source),startAtFsmEntry:true);
                    Made.Add(clone);Objects.Add(clone.Effect.Root);Textures.AddRange(clone.Effect.Root.GetComponentsInChildren<Camera>(true).Select(c=>c.targetTexture).Where(t=>t!=null));return clone;
                },Release=clone=>clone.Dispose()});
        }
        static IEnumerator Wrap(IEnumerator body,List<Relay> list)
        {
            var leaf=new Relay(body,false);var middle=new Relay(leaf,true);var outer=new Relay(middle,true);
            list.Add(leaf);list.Add(middle);list.Add(outer);return outer;
        }
        public void Begin(){Flow.Begin();}
        public void Frames(int count)
        { for(int i=0;i<count&&!Flow.Cancelled;i++){Time+=1/120f;try{Flow.Tick(Time);}catch(Exception error){Escaped=error;break;}} }
        public bool Gone { get {return Flow.ActiveClones==0&&Objects.All(o=>o==null)&&Textures.All(t=>t==null);} }
        public void Dispose()
        {
            Flow.Cancel();Flow.Dispose();
            foreach(var relay in UntilRelays.Concat(FromRelays))if(relay.Disposals==0){relay.Close=null;relay.Dispose();}
            foreach(var clone in Made)clone.Dispose();AssetRegistry.ReleaseTransient(Root);AssetRegistry.ReleaseTransient(Source);AssetRegistry.ReleaseTransient(Target);
        }
    }
    static void Gates(AssetRegistry assets,Catalog catalog,string card)
    {
        using(var scene=new Scene(assets,catalog,card))
        {
            scene.Begin();scene.Frames(8);
            Check(scene.UntilRelays.Count==3&&scene.MissileCreates==0&&scene.Tasks==0,"task gate precedes real missile creation "+card);
            scene.AllowTask=true;scene.Frames(8);
            Check(scene.Tasks==1&&scene.MissileCreates==0,"destination gate remains after actual task completion "+card);
            scene.Destination=true;scene.Frames(8);
            Check(scene.MissileCreates==0,"loading gate remains after destination reached "+card);
            scene.Loading=false;scene.Frames(960);
            Check(scene.Escaped==null&&!scene.Flow.Faulted&&scene.Flow.Completed&&scene.Flow.FinishedNotifications==1&&scene.MissileCreates==1,"same three gates release to one real completed root "+card);
            Check(scene.Host.Execution.IsComplete()&&scene.Tasks==2&&scene.Flow.Pending==0&&scene.Gone,"actual Until/From tasks and physical graph resources drain "+card);
            Check(scene.UntilRelays.Concat(scene.FromRelays).All(r=>r.Disposals==1)&&scene.FromRelays.Count==3,"each nested iterator closes once during normal completion "+card);
        }
    }
    static void WaitingStop(AssetRegistry assets,Catalog catalog,string card,bool dispose)
    {
        using(var scene=new Scene(assets,catalog,card))
        {
            scene.Begin();scene.Frames(8);Check(scene.UntilRelays.Count==3&&scene.Host.Execution.WaitingRequests==1,"deep wait is genuinely suspended "+card+"/"+dispose);
            if(dispose)scene.Flow.Dispose();else scene.Flow.Cancel();
            Check(scene.Flow.Cancelled&&!scene.Flow.Faulted&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,"deep waiting stop stays clean "+card+"/"+dispose);
            Check(scene.UntilRelays.All(r=>r.Disposals==1)&&scene.Host.Execution.WaitingRequests==0&&scene.Gone,"deep waiting stop closes actual stack and resources "+card+"/"+dispose);
            int moves=scene.UntilRelays.Sum(r=>r.Moves);scene.AllowTask=scene.Destination=true;scene.Loading=false;
            scene.History.Tick(9999);scene.Flow.Cancel();
            Check(scene.Tasks==0&&scene.MissileCreates==0&&scene.UntilRelays.Sum(r=>r.Moves)==moves&&scene.UntilRelays.All(r=>r.Disposals==1),"late host tick and repeated cancellation are inert "+card+"/"+dispose);
        }
    }
    static void CurrentFailure(AssetRegistry assets,Catalog catalog,bool stop)
    {
        using(var scene=new Scene(assets,catalog,"EX1_134"))
        {
            scene.Begin();scene.Frames(8);var genuine=new IOException("V003R3 genuine Current I/O failure");
            scene.UntilRelays[0].Read=()=>{if(stop)scene.Flow.Cancel();throw genuine;};scene.Frames(2);
            Check(ReferenceEquals(scene.Flow.LastError,genuine)&&scene.Flow.Faulted&&scene.Flow.Cancelled,"genuine nested Current error retains original instance stop="+stop);
            Check(scene.UntilRelays.All(r=>r.Disposals==1)&&scene.Gone&&!scene.Flow.Completed,"Current error closes nested stack without success stop="+stop);
        }
    }
    static void DisposeFailure(AssetRegistry assets,Catalog catalog,bool dispose)
    {
        using(var scene=new Scene(assets,catalog,"EX1_134"))
        {
            scene.Begin();scene.Frames(8);var genuine=new IOException("V003R3 genuine active iterator Dispose failure");
            Check(scene.UntilRelays.Count==3&&scene.Host.Execution.WaitingRequests==1&&scene.UntilRelays.All(r=>r.Moves>0),"Dispose-fault case reached an actual suspended real-host stack "+dispose);
            scene.UntilRelays[0].Close=()=>{throw genuine;};
            if(dispose)scene.Flow.Dispose();else scene.Flow.Cancel();
            Debug.Log("V003_R3_DISPOSE_STACK dispose="+dispose+" disposed="+String.Join(",",scene.UntilRelays.Select(r=>r.Disposals))+" last_error="+scene.Flow.LastError+" host_waiting="+scene.Host.Execution.WaitingRequests);
            Check(ReferenceEquals(scene.Flow.LastError,genuine)&&scene.Flow.Faulted&&scene.Flow.Cancelled,"active nested Dispose fault keeps real diagnostic "+dispose);
            Check(scene.UntilRelays.All(r=>r.Disposals==1),"one nested Dispose fault does not abandon remaining owned stack "+dispose);
            scene.Flow.Cancel();
            Debug.Log("V003_R3_DISPOSE_RETRY dispose="+dispose+" disposed="+String.Join(",",scene.UntilRelays.Select(r=>r.Disposals)));
            Check(scene.UntilRelays.All(r=>r.Disposals==1),"explicit Cancel can finish outstanding iterator cleanup "+dispose);
            scene.Flow.Dispose();
            Debug.Log("V003_R3_DISPOSE_AFTER_REPEAT dispose="+dispose+" disposed="+String.Join(",",scene.UntilRelays.Select(r=>r.Disposals))+" metadata_pending="+typeof(NativeSuperSpell).GetField("metadataPending",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(scene.Flow)+" public_pending="+scene.Flow.Pending);
            Check(scene.UntilRelays.All(r=>r.Disposals==1),"repeated Dispose finishes outstanding iterator cleanup "+dispose);
            Check(scene.Gone&&scene.Host.Execution.WaitingRequests==0&&!scene.Flow.Completed,"disposal failure stops real host and physically clears clones "+dispose);
        }
    }
    public static void Run()
    {
        passed=0;failures.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(string card in new[]{"EX1_129","EX1_134"})
        {Case("three gates "+card,()=>Gates(assets,catalog,card));foreach(bool dispose in new[]{false,true})Case("deep stop "+card+"/"+dispose,()=>WaitingStop(assets,catalog,card,dispose));}
        foreach(bool stop in new[]{false,true})Case("Current error "+stop,()=>CurrentFailure(assets,catalog,stop));
        foreach(bool dispose in new[]{false,true})Case("active nested Dispose failure "+dispose,()=>DisposeFailure(assets,catalog,dispose));
        Debug.Log("V003_R3_RESULT passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("Independent V003-R3 failures: "+String.Join(" | ",failures));
    }
}
