using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class V003R5Checks
{
    [Serializable] sealed class Prefab { public string reference,file; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs;public Prefab[] prefabs; }
    static int passed;
    static readonly List<string> failures=new List<string>();
    static void Check(bool ok,string label)
    {if(ok){passed++;Debug.Log("V003_R5_PASS "+label);}else{failures.Add(label);Debug.LogError("V003_R5_FAIL "+label);}}
    static bool Cause(Exception error,Exception original)
    {return ReferenceEquals(error,original)||error!=null&&(Cause(error.InnerException,original)||error is AggregateException all&&all.InnerExceptions.Any(e=>Cause(e,original)));}

    sealed class Scene : IDisposable
    {
        readonly AssetRegistry assets;readonly Catalog catalog;
        readonly GameObject root=new GameObject("V003R5 root"),source=new GameObject("V003R5 source"),target=new GameObject("V003R5 target");
        readonly List<NativeAreaInstance> made=new List<NativeAreaInstance>();
        readonly List<GameObject> objects=new List<GameObject>();readonly List<RenderTexture> textures=new List<RenderTexture>();
        public readonly NativeSuperSpellTaskHost Host;public readonly NativeSuperSpellHistory History;
        public readonly NativeSuperSpell Flow;
        public string Stage;public bool Stop,DisposeStop,Deferred,Allow=true;
        public Exception Genuine,Escaped;public int Stops,Permissions,Tag49,Metadata,Tag44,CallsAfterStop;
        public Scene(AssetRegistry assets,Catalog catalog)
        {
            this.assets=assets;this.catalog=catalog;
            root.transform.position=new Vector3(23000,11000,23000);source.transform.position=root.transform.position;
            target.transform.position=root.transform.position+new Vector3(3,0,4);
            var input=new NativePowerTaskList{tasks=new[]{
                new NativePowerHistoryTask{type=4,entity=20,tag=49,tagChange=true,payloadType="HistTagChange"},
                new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"},
                new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
            var services=new NativePowerServices{GameplayPresent=true,CanDoTask=task=>{
                Observe();Permissions++;if(Stage=="permission"&&(!Deferred||Allow))Boundary();return Allow;},
                MulliganBusy=()=>{Observe();return false;},EntityType=id=>{Observe();return 4;},
                BeginZoneChanges=(list,start,end,done)=>{Observe();return null;},TimelineEnd=(id,time,end)=>Observe()};
            services.handlers[4]=power=>{Observe();if(power.tag==49)Tag49++;else if(power.tag==44)Tag44++;return true;};
            services.handlers[8]=power=>{Observe();Metadata++;if(Stage=="metadata")Boundary();return true;};
            Host=new NativeSuperSpellTaskHost(input,services,new Dictionary<int,int>{{0,7}},new Dictionary<int,int>{{7,1}},
                new NativeSuperSpellZoneWait{VisualEntities=new HashSet<int>{20},Entity=task=>20,InDestinationZone=task=>true,ActorLoading=task=>false});
            History=Host.Bind();
            Flow=new NativeSuperSpell(catalog.programs.Single(p=>p.card_id=="EX1_134"),new NativeSuperSpellServices{
                Root=root,Source=()=>source,Targets=new List<GameObject>{target},Friendly=true,History=History,
                Range=(a,b)=>a,RandomBool=()=>true,Create=Create,Release=clone=>clone.Dispose()});
        }
        void Observe(){if(Flow!=null&&Flow.Cancelled)CallsAfterStop++;}
        void Boundary()
        {
            if(Stop&&Stops++==0){if(DisposeStop)Flow.Dispose();else Flow.Cancel();}
            if(Genuine!=null)throw Genuine;
        }
        NativeAreaInstance Create(string reference)
        {
            var prefab=catalog.prefabs.Single(p=>p.reference==reference);
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
            var clone=new NativeAreaInstance(program,assets,300+made.Count,locations:value=>source,facings:value=>target,attachTasks:tasks=>true,
                context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?target:source),startAtFsmEntry:true);
            made.Add(clone);objects.Add(clone.Effect.Root);
            textures.AddRange(clone.Effect.Root.GetComponentsInChildren<Camera>(true).Select(c=>c.targetTexture).Where(t=>t!=null));return clone;
        }
        public void Begin(){try{Flow.Begin();}catch(Exception error){Escaped=error;}}
        public void Advance(int start=1,int end=960)
        {try{for(int i=start;i<=end&&!Flow.Cancelled;i++)Flow.Tick(i/120f);}catch(Exception error){Escaped=error;}}
        public int MetadataPending{get{return (int)typeof(NativeSuperSpell).GetField("metadataPending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Flow);}}
        public bool Gone{get{return Flow.ActiveClones==0&&objects.All(o=>o==null)&&textures.All(t=>t==null);}}
        public string CompletedTasks{get{return String.Join(",",Host.Execution.GetTaskList().Select(t=>t.IsCompleted()?"1":"0"));}}
        public void Dispose()
        {Flow.Cancel();Flow.Dispose();foreach(var clone in made)clone.Dispose();AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);}
    }
    static void RunCase(AssetRegistry assets,Catalog catalog,string stage,bool stop,bool dispose,bool deferred,bool error)
    {
        using(var scene=new Scene(assets,catalog))
        {
            scene.Stage=stage;scene.Stop=stop;scene.DisposeStop=dispose;scene.Deferred=deferred;
            if(error)scene.Genuine=new OperationCanceledException("V003R5 genuine external service OCE");
            if(deferred)scene.Allow=false;
            scene.Begin();
            if(deferred)
            {
                scene.Advance(1,8);
                Check(scene.Host.Execution.WaitingRequests==1&&scene.Tag49==0&&scene.MetadataPending==1,"deferred case begins with real blocked Host and metadata ticket");
                scene.Allow=true;scene.Advance(9);
            }
            else scene.Advance();
            string label=stage+"/stop="+stop+"/dispose="+dispose+"/deferred="+deferred+"/oce="+error;
            Debug.Log("V003_R5_SERVICE "+label+" stopped="+scene.Stops+" permissions="+scene.Permissions+" tag49="+scene.Tag49+
                " metadata="+scene.Metadata+" tag44="+scene.Tag44+" calls_after_stop="+scene.CallsAfterStop+" completed_tasks="+scene.CompletedTasks+
                " root_completed="+scene.Flow.Completed+" faulted="+scene.Flow.Faulted+" metadata_pending="+scene.MetadataPending+
                " host_waiting="+scene.Host.Execution.WaitingRequests+" graph_rt_gone="+scene.Gone+" error="+scene.Flow.LastError);
            if(!stop&&!error)
            {
                Check(scene.Escaped==null&&scene.Flow.Completed&&scene.Flow.FinishedNotifications==1&&!scene.Flow.Faulted&&scene.Tag49==1&&scene.Metadata==1&&scene.Tag44==1,
                    "same real Host normally executes and finishes all original tasks "+label);
                Check(scene.CompletedTasks=="1,1,1"&&scene.Host.Execution.IsComplete(),"normal completion follows actual task completion "+label);
            }
            else
            {
                Check(scene.Flow.Cancelled&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,"stopped or faulted root cannot report success "+label);
                Check(error?scene.Flow.Faulted&&scene.Flow.Errors.Any(e=>Cause(e,scene.Genuine)):!scene.Flow.Faulted&&scene.Flow.Errors.Count==0&&scene.Escaped==null,
                    "external OCE retains genuine cause while normal returning stop stays clean "+label);
                if(!error)
                {
                    Check(scene.Stops>=1&&scene.CallsAfterStop==0,"normal returning service stop prevents subsequent external calls "+label);
                    Check(stage=="permission"?scene.Tag49==0&&scene.CompletedTasks=="0,0,0":scene.Tag44==0&&!scene.Host.Execution.GetTaskList()[2].IsCompleted(),
                        "normal returning stop cannot execute or complete not-yet-dispatched tasks "+label);
                }
            }
            Check(scene.Gone&&scene.MetadataPending==0&&scene.Host.Execution.WaitingRequests==0,"actual graph RT Host and private metadata ownership drain separately "+label);
            int mutations=scene.Tag49+scene.Metadata+scene.Tag44;
            scene.Flow.Cancel();scene.Flow.Dispose();Exception lateError=null;
            try{scene.History.Tick(9999);}catch(Exception caught){lateError=caught;}
            Check(error?Cause(lateError,scene.Genuine):lateError==null,"late Host query preserves real diagnostic or clean cancellation "+label);
            Check(scene.Gone&&scene.MetadataPending==0&&scene.Host.Execution.WaitingRequests==0&&mutations==scene.Tag49+scene.Metadata+scene.Tag44,
                "late Host tick and repeated stop add no further mutation or retained ownership "+label);
        }
    }
    public static void Run()
    {
        passed=0;failures.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(string stage in new[]{"permission","metadata"})
        {
            RunCase(assets,catalog,stage,false,false,false,false);
            foreach(bool dispose in new[]{false,true})RunCase(assets,catalog,stage,true,dispose,false,false);
            RunCase(assets,catalog,stage,false,false,false,true);
            RunCase(assets,catalog,stage,true,false,false,true);
        }
        RunCase(assets,catalog,"permission",true,false,true,false);
        Debug.Log("V003_R5_RESULT passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("V003R5 independent checks: "+String.Join(" | ",failures));
    }
}
