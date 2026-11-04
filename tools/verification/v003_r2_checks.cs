using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 独立探针只挂公开宿主边界，保留真实原配置和图；运行时复制到临时 Editor。
public static class V003R2Checks
{
    [Serializable] sealed class Asset { public string reference,file; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs; public Asset[] prefabs; }
    static int passed;static readonly List<string> failures=new List<string>();
    static void Check(bool value,string label)
    {
        if(value){passed++;Debug.Log("V003_R2_PASS "+label);}
        else{failures.Add(label);Debug.LogError("V003_R2_FAIL "+label);}
    }
    static void Case(string label,Action run)
    {try{run();}catch(Exception error){failures.Add(label+": "+error);Debug.LogError("V003_R2_CASE "+label+" "+error);}}
    static IEnumerator Empty(){yield break;}
    sealed class Scene : IDisposable
    {
        public readonly GameObject Root=new GameObject("V003R2 root"),Source=new GameObject("V003R2 source"),Target=new GameObject("V003R2 target");
        public readonly List<NativeAreaInstance> Made=new List<NativeAreaInstance>();
        public readonly List<GameObject> Objects=new List<GameObject>();public readonly List<RenderTexture> Textures=new List<RenderTexture>();
        public readonly List<Action> Notifications=new List<Action>();
        public NativeSuperSpell Flow;public NativeSuperSpellServices Services;public readonly NativeSuperSpellConfig Config;
        public Action<NativeAreaInstance> Created;public Action<NativeAreaInstance> Releasing;
        public int Writes,Activations,Releases;public bool SimulatedHistoryFailure;
        readonly AssetRegistry assets;readonly Catalog catalog;
        public Scene(AssetRegistry assets,Catalog catalog,string card)
        {
            this.assets=assets;this.catalog=catalog;Config=catalog.programs.Single(c=>c.card_id==card);
            Root.transform.position=new Vector3(20000,10000,20000);Source.transform.position=Root.transform.position;
            Target.transform.position=Root.transform.position+new Vector3(3,0,4);
            Services=new NativeSuperSpellServices{Root=Root,Source=()=>Source,Targets=new List<GameObject>{Target},Friendly=true,
                Range=(min,max)=>min,RandomBool=()=>true,Create=Create,Release=Release};
        }
        NativeAreaInstance Create(string reference)
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,catalog.prefabs.Single(p=>p.reference==reference).file)));
            var instance=new NativeAreaInstance(program,assets,71+Made.Count,locations:kind=>Source,facings:kind=>Target,
                attachTasks:tasks=>true,context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?Target:Source),startAtFsmEntry:true);
            Made.Add(instance);Objects.Add(instance.Effect.Root);
            Textures.AddRange(instance.Effect.Root.GetComponentsInChildren<Camera>(true).Select(c=>c.targetTexture).Where(t=>t!=null));
            var endpoint=instance.Endpoint;
            var source=endpoint.SetSource;endpoint.SetSource=value=>{Writes++;source(value);};
            var activate=endpoint.Activate;endpoint.Activate=()=>{Activations++;activate();};
            var started=endpoint.AddStateStarted;endpoint.AddStateStarted=callback=>{Notifications.Add(callback);started(callback);};
            var finished=endpoint.AddFinished;endpoint.AddFinished=callback=>{Notifications.Add(callback);finished(callback);};
            Created?.Invoke(instance);return instance;
        }
        void Release(NativeAreaInstance instance){Releases++;Releasing?.Invoke(instance);instance.Dispose();}
        public void Begin(){Flow=new NativeSuperSpell(Config,Services);Flow.Begin();}
        public void Advance(){for(int i=1;i<=720&&!Flow.Cancelled&&!Flow.Faulted&&!Flow.Completed;i++)Flow.Tick(i/120f);}
        public bool Gone{get{return Flow.ActiveClones==0&&Objects.All(o=>o==null)&&Textures.Count>0&&Textures.All(t=>t==null);}}
        public void Dispose()
        {
            Releasing=null;Flow?.Cancel();Flow?.Dispose();foreach(var item in Made)item.Dispose();
            AssetRegistry.ReleaseTransient(Root);AssetRegistry.ReleaseTransient(Source);AssetRegistry.ReleaseTransient(Target);
        }
    }
    static NativeSuperSpellHistory History(Scene scene)
    {
        return new NativeSuperSpellHistory{Tasks=new NativePowerTaskList(),MetaDataIndex=index=>-1,ShouldCompleteUntil=index=>false,
            CompleteUntil=index=>Empty(),CompleteFrom=(index,done)=>Empty(),Pending=()=>0,Failed=()=>scene.SimulatedHistoryFailure,Cancel=()=>{}};
    }
    static NativeSuperSpellTaskHost RealHost(Action task)
    {
        var input=new NativePowerTaskList{tasks=new[]{
            new NativePowerHistoryTask{type=4,tag=44,tagChange=true,payloadType="HistTagChange"},
            new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"}}};
        var services=new NativePowerServices{GameplayPresent=true,CanDoTask=item=>true,MulliganBusy=()=>false,TimelineEnd=(id,time,end)=>{}};
        services.handlers[4]=power=>{task();return true;};services.handlers[8]=power=>true;
        return new NativeSuperSpellTaskHost(input,services,new Dictionary<int,int>{{0,4}},new Dictionary<int,int>{{4,1}});
    }
    static void MetadataStop(AssetRegistry assets,Catalog catalog,string stage,bool dispose)
    {
        using(var scene=new Scene(assets,catalog,"EX1_134"))
        {
            int mapped=0,queried=0,until=0,tasks=0;bool stopped=false;var host=RealHost(()=>tasks++);var history=host.Bind();
            var map=history.MetaDataIndex;var predicate=history.ShouldCompleteUntil;var completeUntil=history.CompleteUntil;
            Action stop=()=>{
                stopped=true;if(dispose)scene.Flow.Dispose();else scene.Flow.Cancel();
                Debug.Log("V003_R2_AT_STOP stage="+stage+" dispose="+dispose+" cancelled="+scene.Flow.Cancelled+" fault="+scene.Flow.Faulted+" error="+(scene.Flow.LastError==null?"null":scene.Flow.LastError.GetType().Name));
            };
            history.MetaDataIndex=index=>{mapped++;int result=map(index);if(stage=="mapping")stop();return result;};
            history.ShouldCompleteUntil=index=>{queried++;bool result=predicate(index);if(stage=="predicate")stop();return result;};
            history.CompleteUntil=index=>{until++;return completeUntil(index);};scene.Services.History=history;
            scene.Begin();scene.Flow.Tick(.01f);
            Debug.Log("V003_R2_METADATA stage="+stage+" dispose="+dispose+" stopped="+stopped+" mapped="+mapped+" queried="+queried+" until="+until+" fault="+scene.Flow.Faulted+" error="+scene.Flow.LastError);
            Check(stopped&&mapped==1&&scene.Flow.Cancelled,"metadata callback actually stops "+stage+"/"+dispose);
            Check(!scene.Flow.Faulted&&scene.Flow.LastError==null,"clean metadata stop does not create a host failure "+stage+"/"+dispose);
            Check((stage!="mapping"||queried==0)&&until==0,"stopped metadata dispatch cannot invoke a later host stage "+stage+"/"+dispose);
            Check(scene.Made.Count==0&&scene.Flow.ActiveClones==0&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,"metadata stop cannot spawn or manufacture success "+stage+"/"+dispose);
            Check(tasks==0&&host.Execution.GetTaskList().All(item=>!item.IsCompleted()),"stopped metadata never completes unexecuted tasks "+stage+"/"+dispose);
        }
    }
    static void RealHostControls(AssetRegistry assets,Catalog catalog,bool cancelInHandler)
    {
        using(var scene=new Scene(assets,catalog,"EX1_134"))
        {
            int tasks=0;bool stopped=false;
            var host=RealHost(()=>{tasks++;if(cancelInHandler){stopped=true;scene.Flow.Cancel();}});
            var history=host.Bind();scene.Services.History=history;
            Check(history.MetaDataIndex(0)==1&&history.ShouldCompleteUntil(1),"real history control has valid mapping and an incomplete prior task "+cancelInHandler);
            scene.Begin();for(int i=1;i<=720&&!scene.Flow.Cancelled;i++)scene.Flow.Tick(i/120f);
            Check(tasks==1&&!scene.Flow.Faulted,"real PowerExecution handler runs without a false host failure "+cancelInHandler);
            if(cancelInHandler)
            {
                Check(stopped&&scene.Flow.Cancelled&&scene.Made.Count==0&&!scene.Flow.Completed,"real handler cancellation stops clone creation and completion");
                Check(!host.Execution.GetTaskList()[1].IsCompleted(),"real handler cancellation leaves later metadata incomplete");
            }
            else
            {
                Check(scene.Flow.Completed&&scene.Gone&&host.Execution.IsComplete(),"same real history without stop completes and physically releases");
                Check(scene.Writes>0&&scene.Activations>0&&scene.Flow.FinishedNotifications==1,"real history positive control activates actual graphs exactly once at root");
            }
        }
    }
    static void DisposeReleaseRetry(AssetRegistry assets,Catalog catalog,string card)
    {
        using(var scene=new Scene(assets,catalog,card))
        {
            bool stop=false,fail=true;scene.Created=instance=>{stop=true;scene.Flow.Dispose();};
            scene.Releasing=instance=>{if(fail)throw new InvalidOperationException("V003R2 dispose release failure");};
            scene.Begin();if(!stop)scene.Flow.Tick(.01f);
            Check(stop&&scene.Flow.Cancelled&&scene.Flow.Faulted&&scene.Flow.LastError.Message=="V003R2 dispose release failure","disposed late return retains release failure "+card);
            Check(scene.Flow.ActiveClones==1&&scene.Releases==1&&scene.Textures.Any(t=>t!=null)&&scene.Writes==0&&scene.Activations==0,"failed disposed clone stays owned and inactive "+card);
            bool rejected=false;try{scene.Flow.Tick(8);}catch(ObjectDisposedException){rejected=true;}
            scene.Flow.Dispose();Check(rejected&&scene.Releases==1,"disposed tick and Dispose do not silently retry "+card);
            fail=false;scene.Flow.Cancel();
            Check(scene.Gone&&scene.Releases==2&&scene.Flow.Faulted&&!scene.Flow.Completed,"explicit Cancel recovers disposed clone without erasing failure "+card);
            foreach(var notification in scene.Notifications)notification();scene.Flow.Cancel();
            Check(scene.Releases==2&&scene.Flow.FinishedNotifications==0,"late callbacks after disposed retry are inert "+card);
        }
    }
    static void FactoryFault(AssetRegistry assets,Catalog catalog,string card)
    {
        using(var scene=new Scene(assets,catalog,card))
        {
            scene.Services.History=History(scene);scene.Created=instance=>scene.SimulatedHistoryFailure=true;
            bool threw=false;try{scene.Begin();if(scene.Made.Count==0)scene.Flow.Tick(.01f);}catch(InvalidOperationException){threw=true;}
            Check(threw&&scene.Flow.Faulted&&scene.Flow.Cancelled,"late return observes existing history failure "+card);
            Check(scene.Gone&&scene.Writes==0&&scene.Activations==0,"faulted factory return is owned and physically released "+card);
            Check(!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,"faulted factory cannot manufacture completion "+card);
        }
    }
    public static void Run()
    {
        passed=0;failures.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(string stage in new[]{"mapping","predicate"})foreach(bool dispose in new[]{false,true})
            Case("metadata stop "+stage+"/"+dispose,()=>MetadataStop(assets,catalog,stage,dispose));
        foreach(bool cancelInHandler in new[]{false,true})Case("real host control "+cancelInHandler,()=>RealHostControls(assets,catalog,cancelInHandler));
        foreach(string card in new[]{"EX1_129","EX1_134"})
        {
            Case("disposed release retry "+card,()=>DisposeReleaseRetry(assets,catalog,card));
            Case("factory fault "+card,()=>FactoryFault(assets,catalog,card));
        }
        Debug.Log("V003_R2_RESULT passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("Independent V003-R2 failures: "+String.Join(" | ",failures));
    }
}
