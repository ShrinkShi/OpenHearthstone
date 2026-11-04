using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// 独立补查 FullEntity revelation 与无 Gameplay 同步路径，不构造私有状态。
public static class V003R6Checks
{
    [Serializable] sealed class Prefab { public string reference,file; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs;public Prefab[] prefabs; }
    static int passed,cases;
    static readonly List<string> failures=new List<string>();
    static void Check(bool value,string label)
    {if(value){passed++;Debug.Log("V003_R6_PASS "+label);}else{failures.Add(label);Debug.LogError("V003_R6_FAIL "+label);}}
    static bool Cause(Exception error,Exception original)
    {return ReferenceEquals(error,original)||error!=null&&(Cause(error.InnerException,original)||error is AggregateException all&&all.InnerExceptions.Any(e=>Cause(e,original)));}

    sealed class Scene : IDisposable
    {
        readonly AssetRegistry assets;readonly Catalog catalog;
        readonly GameObject root=new GameObject("V003R6 root"),source=new GameObject("V003R6 source"),target=new GameObject("V003R6 target");
        readonly List<NativeAreaInstance> made=new List<NativeAreaInstance>();
        readonly List<GameObject> objects=new List<GameObject>();readonly List<RenderTexture> textures=new List<RenderTexture>();
        public readonly NativeSuperSpellTaskHost Host;public readonly NativeSuperSpellHistory History;
        public readonly NativeSuperSpell Flow;
        public string Stage="none",Hold="none";public bool Stop,DisposeStop,Allow=true,Fired;
        public Exception Genuine,Escaped;public int Full,Reveal,Metadata,Tail,AfterStop;
        public int[] AtBoundary;public string FlagsAtBoundary;
        public Scene(AssetRegistry assets,Catalog catalog,bool gameplay)
        {
            this.assets=assets;this.catalog=catalog;
            root.transform.position=new Vector3(24000,12000,24000);source.transform.position=root.transform.position;
            target.transform.position=root.transform.position+new Vector3(3,0,4);
            var input=new NativePowerTaskList{tasks=new[]{
                new NativePowerHistoryTask{type=1,entity=20,payloadType="HistFullEntity",tags=new[]{new NativePowerTag{name=49,value=1}}},
                new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"},
                new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
            var services=new NativePowerServices{GameplayPresent=gameplay,
                CanDoTask=task=>{Observe();return Allow||(Hold=="reveal"?task.GetPower().type!=1:task.GetPower().type!=8);},
                MulliganBusy=()=>{Observe();return false;},EntityType=id=>{Observe();return 4;},
                BeginZoneChanges=(list,start,end,done)=>{Observe();return null;},
                EntityRevealed=()=>{Observe();Reveal++;Boundary("reveal");},
                Clock=()=>{Observe();return 1;},TimelineEnd=(id,time,end)=>Observe()};
            // 原 DoTask 忽略 handler 的 bool；正常 false 必须仍然完成。
            services.handlers[1]=power=>{Observe();Full++;return false;};
            services.handlers[8]=power=>{Observe();Metadata++;Boundary("metadata");return false;};
            services.handlers[4]=power=>{Observe();Tail++;return false;};
            Host=new NativeSuperSpellTaskHost(input,services,new Dictionary<int,int>{{0,7}},new Dictionary<int,int>{{7,1}},
                new NativeSuperSpellZoneWait{VisualEntities=new HashSet<int>{20},Entity=task=>{Observe();return 20;},
                    InDestinationZone=task=>{Observe();return true;},ActorLoading=task=>{Observe();return false;}});
            History=Host.Bind();
            Flow=new NativeSuperSpell(catalog.programs.Single(p=>p.card_id=="EX1_134"),new NativeSuperSpellServices{
                Root=root,Source=()=>source,Targets=new List<GameObject>{target},Friendly=true,History=History,
                Range=(a,b)=>a,RandomBool=()=>true,Create=Create,Release=clone=>clone.Dispose()});
        }
        void Observe(){if(Flow!=null&&Flow.Cancelled)AfterStop++;}
        void Boundary(string stage)
        {
            if(stage!=Stage||Fired)return;Fired=true;
            AtBoundary=Mutations;FlagsAtBoundary=Flags;
            if(Stop){if(DisposeStop)Flow.Dispose();else Flow.Cancel();}
            if(Genuine!=null)throw Genuine;
        }
        NativeAreaInstance Create(string reference)
        {
            Observe();var prefab=catalog.prefabs.Single(p=>p.reference==reference);
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
            var clone=new NativeAreaInstance(program,assets,400+made.Count,locations:value=>source,facings:value=>target,attachTasks:tasks=>true,
                context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?target:source),startAtFsmEntry:true);
            made.Add(clone);objects.Add(clone.Effect.Root);
            textures.AddRange(clone.Effect.Root.GetComponentsInChildren<Camera>(true).Select(c=>c.targetTexture).Where(t=>t!=null));return clone;
        }
        public void Begin(){try{Flow.Begin();}catch(Exception error){Escaped=error;}}
        public void Advance(int start,int end)
        {try{for(int i=start;i<=end&&!Flow.Cancelled;i++)Flow.Tick(i/120f);}catch(Exception error){Escaped=error;}}
        public int[] Mutations{get{return new[]{Full,Reveal,Metadata,Tail};}}
        public string Flags{get{return String.Join(",",Host.Execution.GetTaskList().Select(t=>t.IsCompleted()?"1":"0"));}}
        public int MetadataPending{get{return (int)typeof(NativeSuperSpell).GetField("metadataPending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Flow);}}
        public bool Gone{get{return Flow.ActiveClones==0&&objects.All(o=>o==null)&&textures.All(t=>t==null);}}
        public void Dispose()
        {Flow.Cancel();Flow.Dispose();foreach(var clone in made)clone.Dispose();AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);}
    }
    static void RunCase(AssetRegistry assets,Catalog catalog,bool gameplay,string stage,bool stop,bool dispose,bool deferred,Exception genuine=null)
    {
        cases++;string label="gameplay="+gameplay+"/stage="+stage+"/stop="+stop+"/dispose="+dispose+"/deferred="+deferred+"/error="+genuine?.GetType().Name;
        using(var scene=new Scene(assets,catalog,gameplay))
        {
            scene.Stage=stage;scene.Stop=stop;scene.DisposeStop=dispose;scene.Genuine=genuine;
            if(deferred){scene.Hold=stage=="reveal"?"reveal":"metadata";scene.Allow=false;}
            scene.Begin();int frame=1;
            if(deferred)
            {
                for(;frame<=600&&scene.Host.Execution.WaitingRequests==0&&!scene.Flow.Cancelled;frame++)scene.Advance(frame,frame);
                Check(scene.Host.Execution.WaitingRequests==1&&scene.MetadataPending==1&&!scene.Fired,"deferred boundary owns actual waiting request and metadata ticket "+label);
                Check(stage=="reveal"?scene.Flags=="0,0,0":scene.Flags=="1,0,0","deferred boundary precedes the intended real task "+label);
                scene.Allow=true;
            }
            scene.Advance(frame,960);
            Debug.Log("V003_R6_CASE "+label+" fired="+scene.Fired+" mutations="+String.Join(",",scene.Mutations)+" at_boundary="+(scene.AtBoundary==null?"none":String.Join(",",scene.AtBoundary))+
                " flags="+scene.Flags+" flags_at_boundary="+scene.FlagsAtBoundary+" after_stop="+scene.AfterStop+" root_completed="+scene.Flow.Completed+
                " cancelled="+scene.Flow.Cancelled+" faulted="+scene.Flow.Faulted+" metadata="+scene.MetadataPending+" waiting="+scene.Host.Execution.WaitingRequests+" gone="+scene.Gone);
            if(stage=="none")
            {
                Check(scene.Flow.Completed&&scene.Flow.FinishedNotifications==1&&!scene.Flow.Faulted&&scene.Escaped==null,"normal false handler result preserves original full completion "+label);
                Check(scene.Flags=="1,1,1"&&scene.Mutations.SequenceEqual(new[]{1,1,1,1})&&scene.Host.Execution.IsComplete(),"real FullEntity revelation and From tail both execute exactly once "+label);
            }
            else
            {
                Check(scene.Fired&&scene.Flow.Cancelled&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,"selected real boundary stops without root completion "+label);
                Check(scene.Mutations.SequenceEqual(scene.AtBoundary)&&scene.Flags==scene.FlagsAtBoundary&&scene.AfterStop==0,"already occurred mutation remains and no new task or flag starts "+label);
                Check(stage=="reveal"?scene.Full==1&&scene.Reveal==1&&scene.Metadata==0&&scene.Flags=="0,0,0":scene.Full==1&&scene.Reveal==1&&scene.Metadata==1&&scene.Tail==0&&scene.Flags=="1,0,0",
                    "actual task flags distinguish started revelation/handler from later completion "+label);
                Check(genuine==null?!scene.Flow.Faulted&&scene.Flow.Errors.Count==0&&scene.Escaped==null:scene.Flow.Faulted&&scene.Flow.Errors.Any(e=>Cause(e,genuine)),"clean stop or original external cause remains distinct "+label);
            }
            Check(scene.Gone&&scene.MetadataPending==0&&scene.Host.Execution.WaitingRequests==0,"physical graphs RT and private metadata drain independently "+label);
            Check(scene.Host.Execution.PendingTasks==scene.Host.Execution.GetTaskList().Count(t=>!t.IsCompleted()),"pending count matches actual remaining tasks "+label);
            var mutations=scene.Mutations;string flags=scene.Flags;
            scene.Flow.Cancel();scene.Flow.Dispose();Exception late=null;
            try{scene.History.Tick(9999);}catch(Exception error){late=error;}
            Check(mutations.SequenceEqual(scene.Mutations)&&flags==scene.Flags&&scene.Gone&&scene.AfterStop==0,"late tick and repeated stop preserve observed task state "+label);
            Check(late==null||genuine!=null&&Cause(late,genuine),"late tick introduces no new stop error "+label);
        }
    }
    public static void Run()
    {
        passed=cases=0;failures.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(bool gameplay in new[]{false,true})
        {
            RunCase(assets,catalog,gameplay,"none",false,false,false);
            foreach(bool dispose in new[]{false,true})RunCase(assets,catalog,gameplay,"reveal",true,dispose,false);
        }
        foreach(bool dispose in new[]{false,true})RunCase(assets,catalog,false,"metadata",true,dispose,false);
        RunCase(assets,catalog,true,"reveal",true,false,true);
        RunCase(assets,catalog,true,"metadata",true,true,true);
        RunCase(assets,catalog,false,"reveal",false,false,false,new OperationCanceledException("V003R6 external revelation OCE"));
        RunCase(assets,catalog,false,"metadata",true,true,false,new OperationCanceledException("V003R6 stopped external metadata OCE"));
        RunCase(assets,catalog,true,"reveal",true,true,true,new IOException("V003R6 delayed external revelation I/O"));
        RunCase(assets,catalog,true,"metadata",false,false,true,new OperationCanceledException("V003R6 delayed external metadata OCE"));
        Debug.Log("V003_R6_RESULT cases="+cases+" passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("V003R6 independent checks: "+String.Join(" | ",failures));
    }
}
