using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 显式规范化历史/真实TaskHost；适配器返回时停止，不伪装为原raw历史或UI自发事件。
public static class NativeSuperSpellHistoryChecks
{
    [Serializable] sealed class Asset{public string reference,file;}
    [Serializable] sealed class Catalog{public NativeSuperSpellConfig[] programs;public Asset[] prefabs;}
    static int passed,cases;
    static void Check(bool value,string label){if(!value)throw new Exception("SuperSpell history boundary: "+label);passed++;}
    static IEnumerator Nested(IEnumerator body){yield return body;}
    sealed class Observed : IEnumerator,IDisposable
    {
        readonly IEnumerator inner;readonly Scene scene;readonly Action move,current;readonly bool before;
        public Exception DisposeError;
        public Observed(IEnumerator inner,Scene scene,Action move=null,bool before=false,Action current=null){this.inner=inner;this.scene=scene;this.move=move;this.before=before;this.current=current;}
        public bool MoveNext(){scene.Moves++;if(before){move();return true;}bool value=inner.MoveNext();move?.Invoke();return value;}
        public object Current{get{scene.Currents++;var value=inner.Current;current?.Invoke();return value;}}
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){scene.Disposals++;(inner as IDisposable)?.Dispose();if(DisposeError!=null)throw DisposeError;}
    }
    sealed class Owned : IEnumerator,IDisposable
    {
        readonly IEnumerator inner;readonly bool nested;int step;
        public int Moves,Disposals;public Action Close;
        public Owned(IEnumerator inner,bool nested){this.inner=inner;this.nested=nested;}
        public bool MoveNext(){Moves++;return nested?step++==0:inner.MoveNext();}
        public object Current{get{return nested?(object)inner:inner.Current;}}
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){Disposals++;if(!nested)(inner as IDisposable)?.Dispose();Close?.Invoke();}
    }
    sealed class Lease : IEnumerator,IDisposable
    {
        public readonly GameObject Object=new GameObject("History returned iterator lease");
        public readonly RenderTexture Texture=new RenderTexture(8,8,0);
        public int Moves,Reads,Disposals;public Action Closing;public Exception Error;
        public Lease(){Texture.Create();}
        public bool MoveNext(){return ++Moves==1;}
        public object Current{get{Reads++;return null;}}
        public void Reset(){throw new NotSupportedException();}
        public bool Gone{get{return Object==null&&Texture==null;}}
        public void Dispose()
        {
            Disposals++;try{Closing?.Invoke();}finally{Texture.Release();AssetRegistry.ReleaseTransient(Texture);AssetRegistry.ReleaseTransient(Object);}
            if(Error!=null)throw Error;
        }
    }
    sealed class Delivery : IEnumerator,IDisposable
    {
        readonly IEnumerator inner,child;bool entered;
        public Action Read;public int Moves,Reads,Disposals;
        public Delivery(IEnumerator inner,IEnumerator child){this.inner=inner;this.child=child;}
        public bool MoveNext(){Moves++;if(entered)return inner!=null&&inner.MoveNext();entered=true;inner?.MoveNext();return true;}
        public object Current{get{Reads++;Read?.Invoke();return child;}}
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){Disposals++;(inner as IDisposable)?.Dispose();}
    }
    sealed class Scene : IDisposable
    {
        readonly AssetRegistry assets;readonly Catalog catalog;
        readonly GameObject root=new GameObject("History check root"),source=new GameObject("History check source"),target=new GameObject("History check target"),otherTarget=new GameObject("History check other target");
        readonly List<NativeAreaInstance> made=new List<NativeAreaInstance>();
        readonly List<GameObject> objects=new List<GameObject>();readonly List<RenderTexture> textures=new List<RenderTexture>();
        public readonly NativeSuperSpellTaskHost Host;public readonly NativeSuperSpellHistory History;
        public NativeSuperSpell Flow;public Action<int> Handler;public Action Permission,Mulligan;
        public int Mapped,Queried,Until,From,Tasks,AfterStop,Moves,Currents,Disposals,Destination,Loading;
        public bool Triggered,AllowTask=true,AllowFrom=true;public Exception Escaped,HandlerError;
        string zoneStage;Action zoneAction;bool yieldDestination;
        public Scene(AssetRegistry assets,Catalog catalog,string card="EX1_134",int targets=1,Action<NativePowerServices,NativeSuperSpellZoneWait> configure=null)
        {
            this.assets=assets;this.catalog=catalog;
            root.transform.position=new Vector3(20000,10000,20000);source.transform.position=root.transform.position;target.transform.position=root.transform.position+new Vector3(3,0,4);otherTarget.transform.position=target.transform.position+Vector3.right;
            var input=new NativePowerTaskList{tasks=new[]{
                new NativePowerHistoryTask{type=4,entity=20,tag=49,tagChange=true,payloadType="HistTagChange"},
                new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"},
                new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
            var services=new NativePowerServices{GameplayPresent=true,CanDoTask=task=>{Permission?.Invoke();return AllowTask&&(AllowFrom||task.GetPower().tag!=44);},MulliganBusy=()=>{Mulligan?.Invoke();return false;},EntityType=id=>4,
                BeginZoneChanges=(list,start,end,callback)=>null,TimelineEnd=(id,time,end)=>{},ExecutionError=error=>HandlerError=error};
            services.handlers[4]=power=>{Tasks++;Handler?.Invoke(Tasks);return true;};services.handlers[8]=power=>true;
            var wait=new NativeSuperSpellZoneWait{VisualEntities=new HashSet<int>{20},Entity=task=>{Hit();if(zoneStage=="entity")zoneAction();return 20;},
                    InDestinationZone=task=>{Hit();Destination++;if(zoneStage=="destination")zoneAction();return !yieldDestination;},
                    ActorLoading=task=>{Hit();Loading++;if(zoneStage=="loading")zoneAction();return false;}};
            configure?.Invoke(services,wait);
            Host=new NativeSuperSpellTaskHost(input,services,new Dictionary<int,int>{{0,4},{1,4}},new Dictionary<int,int>{{4,1}},wait);
            History=Host.Bind();var map=History.MetaDataIndex;var predicate=History.ShouldCompleteUntil;var until=History.CompleteUntil;var from=History.CompleteFrom;
            History.MetaDataIndex=i=>{Hit();Mapped++;return map(i);};History.ShouldCompleteUntil=i=>{Hit();Queried++;return predicate(i);};
            History.CompleteUntil=i=>{Hit();Until++;return until(i);};History.CompleteFrom=(i,done)=>{Hit();From++;return from(i,done);};
            Flow=new NativeSuperSpell(catalog.programs.Single(p=>p.card_id==card),new NativeSuperSpellServices{Root=root,Source=()=>source,
                Targets=targets==1?new List<GameObject>{target}:new List<GameObject>{target,otherTarget},Friendly=true,History=History,Range=(min,max)=>min,RandomBool=()=>true,Create=Create,Release=clone=>clone.Dispose()});
        }
        void Hit(){if(Flow!=null&&Flow.Cancelled)AfterStop++;}
        NativeAreaInstance Create(string reference)
        {
            Hit();var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,catalog.prefabs.Single(p=>p.reference==reference).file)));
            var clone=new NativeAreaInstance(program,assets,90+made.Count,locations:value=>source,facings:value=>target,attachTasks:tasks=>true,
                context:new NativeSpellContext(5,4,heroType:3,lifecycle:"None",actorObjects:role=>role==1?target:source),startAtFsmEntry:true);
            made.Add(clone);objects.Add(clone.Effect.Root);textures.AddRange(clone.Effect.Root.GetComponentsInChildren<Camera>(true).Select(c=>c.targetTexture).Where(t=>t!=null));return clone;
        }
        public void Stop(bool dispose){Triggered=true;if(dispose)Flow.Dispose();else Flow.Cancel();}
        public void Hook(string stage,Action action)
        {
            var map=History.MetaDataIndex;var predicate=History.ShouldCompleteUntil;var until=History.CompleteUntil;var from=History.CompleteFrom;
            int occurrence=stage.EndsWith("-impact")?2:1;
            if(stage.StartsWith("mapping"))History.MetaDataIndex=i=>{int value=map(i);if(Mapped==occurrence)action();return value;};
            else if(stage.StartsWith("predicate"))History.ShouldCompleteUntil=i=>{bool value=predicate(i);if(Queried==occurrence)action();return value;};
            else if(stage=="until-factory")History.CompleteUntil=i=>{var body=new Observed(until(i),this);action();return body;};
            else if(stage=="from-factory")History.CompleteFrom=(i,done)=>{var body=new Observed(from(i,done),this);action();return body;};
            else if(stage=="until-body")History.CompleteUntil=i=>new Observed(until(i),this,action);
            else if(stage=="from-body")History.CompleteFrom=(i,done)=>new Observed(from(i,done),this,action,true);
            else if(stage=="until-nested")History.CompleteUntil=i=>Nested(new Observed(until(i),this,action));
            else if(stage=="from-nested")History.CompleteFrom=(i,done)=>Nested(new Observed(from(i,done),this,action,true));
            else if(stage=="until-current"){yieldDestination=true;History.CompleteUntil=i=>new Observed(until(i),this,current:action);}
            else if(stage=="tick"){var tick=History.Tick;History.Tick=frame=>{tick(frame);action();};}
            else if(stage=="failed"){var failed=History.Failed;History.Failed=()=>{bool value=failed();if(!Triggered)action();return value;};}
            else if(stage=="pending"){var pending=History.Pending;History.Pending=()=>{int value=pending();if(!Triggered)action();return value;};}
            else throw new ArgumentException(stage);
        }
        public void ZoneHook(string stage,Action action)
        {
            zoneStage=stage;zoneAction=action;
        }
        public void Advance()
        {
            try{Flow.Begin();for(int i=1;i<=840&&!Flow.Cancelled;i++)Flow.Tick(i/120f);}catch(Exception error){Escaped=error;}
        }
        public void Verify(string label)
        {
            Check(Triggered&&Flow.Cancelled,"stop actually reached "+label+" escaped="+Escaped+" root_error="+Flow.LastError);
            Check(Escaped==null&&!Flow.Faulted&&Flow.LastError==null,"clean stop stays clean "+label);
            Check(AfterStop==0&&!Flow.Completed&&Flow.FinishedNotifications==0,"no later stage or fake completion "+label);
            Check(Flow.ActiveClones==0&&objects.All(o=>o==null)&&textures.All(t=>t==null),"actual graph/RT ownership drains "+label);
            if(label.Contains("factory"))Check(Moves==0&&Disposals==1,"late factory iterator disposed without execution "+label);
            if(label.Contains("body")||label.Contains("nested"))Check(Moves==1&&Currents==0&&Disposals==1,"stopped MoveNext has no later Current or duplicate Dispose "+label+" moves="+Moves+" current="+Currents+" disposed="+Disposals);
            if(label.Contains("current"))Check(Moves==1&&Currents==1&&Disposals==1,"Current stop disposes its actual waiting iterator once "+label);
        }
        public bool Gone{get{return Flow.ActiveClones==0&&objects.All(o=>o==null)&&textures.All(t=>t==null);}}
        public void Dispose(){Flow.Cancel();Flow.Dispose();foreach(var clone in made)clone.Dispose();AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);AssetRegistry.ReleaseTransient(otherTarget);}
    }
    static IEnumerator Own(IEnumerator body,List<Owned> owned)
    {
        var leaf=new Owned(body,false);var middle=new Owned(leaf,true);var outer=new Owned(middle,true);
        owned.Add(leaf);owned.Add(middle);owned.Add(outer);return outer;
    }
    static int MetadataPending(Scene scene)
    {return (int)typeof(NativeSuperSpell).GetField("metadataPending",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(scene.Flow);}
    static bool Cause(Exception error,Exception original)
    {return ReferenceEquals(error,original)||error is AggregateException aggregate&&aggregate.InnerExceptions.Any(e=>Cause(e,original));}
    static IEnumerator Deliver(IEnumerator inner,Lease lease,int depth,List<Delivery> parents,Action read)
    {
        IEnumerator child=lease;
        for(int i=0;i<depth;i++)
        {
            var parent=new Delivery(i==depth-1?inner:null,child);if(i==0)parent.Read=read;
            parents.Add(parent);child=parent;
        }
        return child;
    }
    static void ReturnedOwnership(AssetRegistry assets,Catalog catalog)
    {
        foreach(bool from in new[]{false,true})foreach(int depth in new[]{1,3})foreach(string stop in new[]{"none","cancel","dispose"})foreach(bool fault in new[]{false,true})
        using(var scene=new Scene(assets,catalog))
        {
            var lease=new Lease();var parents=new List<Delivery>();var genuine=new IOException("returned child Dispose failure");int waiting=-1;
            scene.AllowTask=from;scene.AllowFrom=false;
            Action read=()=>{waiting=scene.Host.Execution.WaitingRequests;if(stop!="none")scene.Stop(stop=="dispose");};
            var until=scene.History.CompleteUntil;var completeFrom=scene.History.CompleteFrom;
            if(from)scene.History.CompleteFrom=(i,done)=>Deliver(completeFrom(i,done),lease,depth,parents,read);
            else scene.History.CompleteUntil=i=>Deliver(until(i),lease,depth,parents,read);
            lease.Closing=()=>{scene.Flow.Cancel();scene.Flow.Dispose();};if(fault)lease.Error=genuine;
            try
            {
                scene.Flow.Begin();for(int i=1;i<=600&&waiting<0&&!scene.Flow.Cancelled;i++)scene.Flow.Tick(i/120f);
                string label=(from?"From":"Until")+"/"+depth+"/"+stop+"/"+fault;
                Check(waiting==1&&parents.Count==depth&&parents.All(p=>p.Moves==1&&p.Reads==1),"actual waiting Host delivered every Current layer "+label);
                if(stop=="none")
                {
                    Check(lease.Moves==1&&lease.Disposals==0&&!scene.Flow.Cancelled,"normal History transfers and executes nested child before stop "+label);
                    scene.Flow.Cancel();
                }
                Check(lease.Moves==(stop=="none"?1:0)&&lease.Reads==(stop=="none"?1:0)&&lease.Disposals==1&&lease.Gone,
                    "returned child is physically released once, stopped child body never executes "+label);
                Check(parents.All(p=>p.Disposals==1)&&scene.Gone&&MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0,
                    "parent stack, original graphs and real task tickets drain separately "+label);
                Check(scene.Flow.Cancelled&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0&&
                    (fault?scene.Flow.Faulted&&ReferenceEquals(scene.Flow.LastError,genuine):!scene.Flow.Faulted&&scene.Flow.Errors.Count==0),
                    "reentrant child cleanup preserves real error or clean stop without success "+label);
                scene.Flow.Cancel();scene.Flow.Dispose();scene.History.Tick(9999);
                Check(lease.Disposals==1&&parents.All(p=>p.Disposals==1)&&lease.Gone,"late ticks and repeated stop keep transferred ownership idempotent "+label);cases++;
            }
            finally{if(lease.Disposals==0)lease.Dispose();}
        }
        foreach(bool factory in new[]{false,true})using(var scene=new Scene(assets,catalog))
        {
            var lease=new Lease();var gate=new IOException("genuine post-return host gate failure");var cleanup=new IOException("genuine rejected body cleanup failure");
            var parents=new List<Delivery>();bool armed=false;scene.AllowTask=false;lease.Error=cleanup;
            var failed=scene.History.Failed;scene.History.Failed=()=>{if(armed){armed=false;throw gate;}return failed();};
            var until=scene.History.CompleteUntil;
            scene.History.CompleteUntil=i=>factory?Arm():Deliver(until(i),lease,2,parents,()=>armed=true);
            IEnumerator Arm(){armed=true;return lease;}
            try
            {
                scene.Advance();
                Check(lease.Moves==0&&lease.Disposals==1&&lease.Gone&&parents.All(p=>p.Disposals==1),"genuine gate failure also returns owned body before scheduler transfer, factory="+factory);
                Check(ReferenceEquals(scene.Flow.LastError,gate)&&scene.Flow.Errors.Any(e=>ReferenceEquals(e,cleanup)),"gate and cleanup retain distinct original diagnostics, factory="+factory);
                Check(scene.Flow.Cancelled&&!scene.Flow.Completed&&MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0&&scene.Gone,"faulted post-return gate drains actual ownership and tasks");cases++;
            }
            finally{if(lease.Disposals==0)lease.Dispose();}
        }
        foreach(bool from in new[]{false,true})foreach(bool dispose in new[]{false,true})foreach(bool fault in new[]{false,true})
        using(var scene=new Scene(assets,catalog))
        {
            var lease=new Lease();var genuine=new IOException("late factory returned lease Dispose failure");if(fault)lease.Error=genuine;
            lease.Closing=()=>{scene.Flow.Cancel();scene.Flow.Dispose();};
            IEnumerator Return(){scene.Stop(dispose);return lease;}
            if(from)scene.History.CompleteFrom=(i,done)=>Return();else scene.History.CompleteUntil=i=>Return();
            try
            {
                scene.Advance();Check(lease.Moves==0&&lease.Reads==0&&lease.Disposals==1&&lease.Gone,"late factory lease is disposed without execution, from="+from+" dispose="+dispose);
                Check((fault?ReferenceEquals(scene.Flow.LastError,genuine):!scene.Flow.Faulted)&&scene.Gone&&MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0&&!scene.Flow.Completed,
                    "factory rejection retains genuine disposal error and returns real resources/tickets");cases++;
            }
            finally{if(lease.Disposals==0)lease.Dispose();}
        }
        foreach(bool factory in new[]{false,true})using(var scene=new Scene(assets,catalog))
        {
            var lease=new Lease();var genuine=new OperationCanceledException("genuine rejected child Dispose error");lease.Error=genuine;
            var parents=new List<Delivery>();scene.AllowTask=false;var until=scene.History.CompleteUntil;
            IEnumerator Return(){scene.Stop(false);return lease;}
            scene.History.CompleteUntil=i=>factory?Return():Deliver(until(i),lease,2,parents,()=>scene.Stop(false));
            try
            {
                scene.Advance();Check(lease.Disposals==1&&lease.Moves==0&&lease.Gone&&parents.All(p=>p.Disposals==1),"rejected lease ownership drains regardless of disposal exception type");
                Check(scene.Flow.Faulted&&ReferenceEquals(scene.Flow.LastError,genuine),"external Dispose OperationCanceledException is a real diagnostic, not the root stop sentinel, factory="+factory);cases++;
            }
            finally{if(lease.Disposals==0)lease.Dispose();}
        }
        foreach(bool stop in new[]{false,true})
        {
            var lease=new Lease();NativePresentationCoroutines clock=null;var diagnostics=new List<Exception>();
            using(clock=new NativePresentationCoroutines(diagnostics.Add))
            {
                var parent=new Delivery(null,lease){Read=()=>{if(stop)clock.Dispose();}};
                try
                {
                    clock.Start(parent);if(!stop){Check(lease.Moves==1&&lease.Disposals==0,"direct scheduler normal child is actually running");clock.Dispose();}
                    Check(lease.Moves==(stop?0:1)&&lease.Disposals==1&&lease.Gone&&parent.Disposals==1&&clock.Active==0&&diagnostics.Count==0,
                        "direct scheduler independently returns actual child resources, stop="+stop);cases++;
                }
                finally{if(lease.Disposals==0)lease.Dispose();}
            }
        }
    }
    static void Cleanup(AssetRegistry assets,Catalog catalog)
    {
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(bool dispose in new[]{false,true})foreach(int faults in new[]{0,1,3})
        using(var scene=new Scene(assets,catalog,card))
        {
            scene.AllowTask=false;var owned=new List<Owned>();var until=scene.History.CompleteUntil;
            scene.History.CompleteUntil=i=>Own(until(i),owned);scene.Flow.Begin();for(int i=1;i<=8;i++)scene.Flow.Tick(i/120f);
            Check(owned.Count==3&&owned.All(o=>o.Moves>0)&&scene.Host.Execution.WaitingRequests==1,"real host entered all owned waiting layers "+card);
            var genuine=Enumerable.Range(0,faults).Select(i=>new IOException("real owned layer "+i)).ToArray();
            for(int i=0;i<faults;i++){var error=genuine[i];owned[i].Close=()=>{scene.Flow.Cancel();throw error;};}
            scene.Stop(dispose);
            Check(owned.All(o=>o.Disposals==1)&&MetadataPending(scene)==0,"all layer cleanup/finally returns actual metadata ticket "+card+"/"+dispose+"/"+faults);
            Check(scene.Flow.Cancelled&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0&&scene.Gone&&scene.Host.Execution.WaitingRequests==0,
                "real host stop drains graph/RT and tasks without success "+card);
            Check(faults==0?!scene.Flow.Faulted&&scene.Flow.Errors.Count==0:scene.Flow.Faulted&&genuine.All(e=>scene.Flow.Errors.Any(error=>Cause(error,e))),
                "every genuine layer failure stays diagnostic "+card);
            if(faults==1)Check(ReferenceEquals(scene.Flow.LastError,genuine[0]),"single original error identity remains stable");
            int moves=owned.Sum(o=>o.Moves);scene.Flow.Cancel();scene.Flow.Dispose();scene.History.Tick(9999);
            Check(owned.All(o=>o.Disposals==1)&&owned.Sum(o=>o.Moves)==moves&&MetadataPending(scene)==0,"repeated/late stop cannot redo frames or retain tickets");cases++;
        }
        foreach(bool dispose in new[]{false,true})using(var scene=new Scene(assets,catalog,"EX1_129",2))
        {
            scene.AllowTask=false;var owned=new List<Owned>();var until=scene.History.CompleteUntil;scene.History.CompleteUntil=i=>Own(until(i),owned);
            scene.Flow.Begin();for(int i=1;i<=8;i++)scene.Flow.Tick(i/120f);
            Check(owned.Count==6&&scene.Host.Execution.WaitingRequests==2&&MetadataPending(scene)==2,"two real concurrent metadata jobs are owned");
            var first=new IOException("first real job cleanup");var second=new IOException("second real job cleanup");
            owned[0].Close=()=>throw first;owned[4].Close=()=>throw second;scene.Stop(dispose);
            Check(owned.All(o=>o.Disposals==1)&&MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0&&scene.Gone,"first job fault does not abandon second real job or physical area");
            Check(scene.Flow.Errors.Any(e=>Cause(e,first))&&scene.Flow.Errors.Any(e=>Cause(e,second))&&!scene.Flow.Completed,"both real-job failures are retained without success");cases++;
        }
        using(var scene=new Scene(assets,catalog))
        {
            var owned=new List<Owned>();var until=scene.History.CompleteUntil;var from=scene.History.CompleteFrom;
            scene.History.CompleteUntil=i=>Own(until(i),owned);scene.History.CompleteFrom=(i,done)=>Own(from(i,done),owned);scene.Advance();
            Check(scene.Escaped==null&&scene.Flow.Completed&&scene.Flow.Errors.Count==0&&scene.Host.Execution.IsComplete()&&scene.Gone,"normal real nested Until/From finishes tasks and resources");
            Check(owned.Count==6&&owned.All(o=>o.Disposals==1)&&MetadataPending(scene)==0,"normal nested callback returns actual metadata tickets once");cases++;
        }
        foreach(bool dispose in new[]{false,true})using(var scene=new Scene(assets,catalog))
        {
            scene.AllowFrom=false;var owned=new List<Owned>();var from=scene.History.CompleteFrom;Action late=null;
            scene.History.CompleteFrom=(i,done)=>{late=done;return Own(from(i,done),owned);};
            scene.Flow.Begin();for(int i=1;i<=600;i++)scene.Flow.Tick(i/120f);
            Check(owned.Count==3&&owned.All(o=>o.Disposals==1)&&scene.Host.Execution.WaitingRequests==1&&MetadataPending(scene)==1&&!scene.Flow.Completed,
                "real From iterator can end while its actual task callback is pending");
            scene.Stop(dispose);late();late();scene.History.Tick(9999);scene.Flow.Cancel();
            Check(MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0&&scene.Gone&&!scene.Flow.Faulted&&!scene.Flow.Completed&&scene.Flow.FinishedNotifications==0,
                "stop returns callback-owned ticket without faking completion or accepting late duplicate callback");cases++;
        }
        using(var scene=new Scene(assets,catalog))
        {
            scene.AllowTask=false;var owned=new List<Owned>();var until=scene.History.CompleteUntil;scene.History.CompleteUntil=i=>Own(until(i),owned);
            scene.Flow.Begin();for(int i=1;i<=8;i++)scene.Flow.Tick(i/120f);
            var first=new IOException("iterator cleanup cause");var second=new IOException("host cancellation cause");var cancel=scene.History.Cancel;
            owned[0].Close=()=>throw first;scene.History.Cancel=()=>{cancel();throw second;};scene.Flow.Cancel();
            Check(ReferenceEquals(scene.Flow.LastError,first)&&scene.Flow.Errors.Any(e=>ReferenceEquals(e,second)),"later host cleanup fault cannot overwrite or disappear behind first error");
            Check(owned.All(o=>o.Disposals==1)&&MetadataPending(scene)==0&&scene.Host.Execution.WaitingRequests==0&&scene.Gone,"multiple stop failures still drain real host and every owned layer");cases++;
        }
    }
    static void PowerOce(AssetRegistry assets,Catalog catalog)
    {
        foreach(string stage in new[]{"permission","mulligan","handler","deferred-permission"})foreach(bool stop in new[]{false,true})
        using(var scene=new Scene(assets,catalog))
        {
            var genuine=new OperationCanceledException("genuine power service failure "+stage);
            Action fail=()=>{if(stop)scene.Flow.Cancel();throw genuine;};
            if(stage=="deferred-permission")
            {
                scene.AllowTask=false;scene.Flow.Begin();for(int i=1;i<=8;i++)scene.Flow.Tick(i/120f);
                Check(scene.Host.Execution.WaitingRequests==1,"deferred service fault starts from a real waiting request");
                scene.Permission=fail;try{scene.Flow.Tick(9/120f);}catch(Exception error){scene.Escaped=error;}
            }
            else
            {
                if(stage=="permission")scene.Permission=fail;else if(stage=="mulligan")scene.Mulligan=fail;else scene.Handler=count=>fail();
                scene.Advance();
            }
            Check(scene.Flow.Faulted&&ReferenceEquals(scene.Flow.LastError?.GetBaseException(),genuine),"real Power service OCE retains its original cause, stage="+stage+" stop="+stop);
            Check(scene.Flow.Cancelled&&!scene.Flow.Completed&&scene.Host.Execution.WaitingRequests==0&&MetadataPending(scene)==0&&scene.Gone,
                "real service fault does not become a waiting request or success "+stage);cases++;
        }
    }
    sealed class ReadSet : HashSet<int>,ISet<int>
    {
        public Func<int,bool> Read;
        bool ICollection<int>.Contains(int value){return Read(value);}
    }
    sealed class ReadMap : Dictionary<int,int>,IDictionary<int,int>
    {
        public Action Read;
        bool IDictionary<int,int>.TryGetValue(int key,out int value){Read?.Invoke();return base.TryGetValue(key,out value);}
    }
    sealed class ServiceProbe
    {
        public Scene Scene;public string Stage="";public bool Armed=true,Stop=true,DisposeStop,Value=true,Fired;
        public Exception Error;public int AfterStop,Diagnostics;public bool[] AtBoundary;
        public readonly List<int> Mutations=new List<int>();public readonly List<string> Calls=new List<string>();public int BoundaryMutations;int frames;
        void Call(string stage)
        {
            Calls.Add(stage);
            if(Scene.Flow.Cancelled)AfterStop++;
            if(stage!=Stage||!Armed||Fired)return;
            Fired=true;AtBoundary=Scene.Host.Execution.GetTaskList().Select(t=>t.IsCompleted()).ToArray();BoundaryMutations=Mutations.Count;
            if(Stop)Scene.Stop(DisposeStop);if(Error!=null)throw Error;
        }
        bool Result(string stage,bool original)
        {
            Call(stage);if(stage==Stage&&stage.StartsWith("permission")&&!Armed)return false;
            return stage==Stage&&Armed?Value:original;
        }
        static string TaskPhase(NativePowerTask task){var power=task.GetPower();return power.type==8?"from":power.tag==49?"until":"tail";}
        public void Bind(NativePowerServices power,NativeSuperSpellZoneWait zone)
        {
            var permission=power.CanDoTask;power.CanDoTask=task=>Result("permission-"+TaskPhase(task),permission(task));
            var mulligan=power.MulliganBusy;power.MulliganBusy=()=>Result("mulligan-"+(Mutations.LastOrDefault()==49?"until":"from"),mulligan());
            var entity=power.EntityType;power.EntityType=id=>{var result=entity(id);Call("entity-type");return result;};
            var begin=power.BeginZoneChanges;power.BeginZoneChanges=(list,start,end,done)=>{var result=begin(list,start,end,done);Call("zone-begin");return result;};
            power.PresentationFrame=()=>{Call("frame-"+(frames++==0?"until":"from"));return Mathf.RoundToInt(Scene.Flow.Time*120);};
            power.Clock=()=>{Call("clock");return Scene.Flow.Time;};
            var timeline=power.TimelineEnd;power.TimelineEnd=(id,time,end)=>{timeline(id,time,end);Call("timeline");};
            power.Cancelled=()=>Result("cancel-query",false);
            var diagnostic=power.ExecutionError;power.ExecutionError=error=>{Diagnostics++;diagnostic?.Invoke(error);};
            foreach(int key in power.handlers.Keys.ToArray())
            {
                var handler=power.handlers[key];power.handlers[key]=task=>{
                    bool result=handler(task);Mutations.Add(task.type==8?8:task.tag);Call("handler-"+(task.type==8?"from":task.tag==49?"until":"tail"));return result;};
            }
            var card=zone.Entity;zone.Entity=task=>{var result=card(task);Call("zone-entity");return result;};
            var destination=zone.InDestinationZone;zone.InDestinationZone=task=>Result("destination",destination(task));
            var loading=zone.ActorLoading;zone.ActorLoading=task=>Result("loading",loading(task));
            var members=zone.VisualEntities;zone.VisualEntities=new ReadSet{Read=id=>Result("membership",members.Contains(id))};
        }
        public void Verify(bool genuine)
        {
            Check(Fired&&Scene.Flow.Cancelled&&!Scene.Flow.Completed&&Scene.Flow.FinishedNotifications==0,"actual service boundary stops root "+Stage+" value="+Value+" armed="+Armed+" fired="+Fired+" cancelled="+Scene.Flow.Cancelled+" complete="+Scene.Flow.Completed+" error="+Scene.Flow.LastError+" escaped="+Scene.Escaped+" calls="+String.Join(",",Calls)+" mutations="+String.Join(",",Mutations));
            Check(AfterStop==0&&Mutations.Count==BoundaryMutations,"no later business call or not-yet-dispatched handler "+Stage);
            Check(AtBoundary.SequenceEqual(Scene.Host.Execution.GetTaskList().Select(t=>t.IsCompleted())),"preserve completed tasks without advancing or rolling back later flags "+Stage);
            Check(Scene.Host.Execution.PendingTasks==Scene.Host.Execution.GetTaskList().Count(t=>!t.IsCompleted()),"native pending count still describes actual uncompleted tasks "+Stage);
            Check(genuine?Scene.Flow.Faulted&&Scene.Flow.Errors.Any(e=>ContainsCause(e,Error)):!Scene.Flow.Faulted&&Scene.Flow.Errors.Count==0&&Scene.Escaped==null,
                "true cause is distinct from normally returned internal stop "+Stage);
            Check(Scene.Gone&&MetadataPending(Scene)==0&&Scene.Host.Execution.WaitingRequests==0,"actual graphs, RT, Host and tickets independently drain "+Stage);
            int count=Mutations.Count;Scene.Flow.Cancel();Scene.Flow.Dispose();try{Scene.History.Tick(9999);}catch(Exception error){Check(genuine&&ContainsCause(error,Error),"late fault keeps original cause");}
            Check(Mutations.Count==count&&AfterStop==0&&Scene.Gone,"repeated stop/late tick cannot dispatch another handler "+Stage);
        }
    }
    static bool ContainsCause(Exception error,Exception original)
    {return ReferenceEquals(error,original)||error!=null&&(ContainsCause(error.InnerException,original)||error is AggregateException all&&all.InnerExceptions.Any(e=>ContainsCause(e,original)));}
    static void ServiceBoundaries(AssetRegistry assets,Catalog catalog)
    {
        string[] stages={"frame-until","entity-type","zone-begin","permission-until","handler-until","mulligan-until","zone-entity","membership","destination","loading",
            "frame-from","permission-from","handler-from","mulligan-from","permission-tail","handler-tail","clock","timeline","cancel-query"};
        foreach(string stage in stages)foreach(bool dispose in new[]{false,true})
        foreach(bool value in stage.StartsWith("permission")||stage.StartsWith("mulligan")||stage=="destination"||stage=="loading"||stage=="membership"||stage=="cancel-query"?new[]{false,true}:new[]{true})
        {
            var probe=new ServiceProbe{Stage=stage,DisposeStop=dispose,Value=value};
            using(var scene=new Scene(assets,catalog,configure:probe.Bind))
            {probe.Scene=scene;if(stage=="cancel-query")scene.AllowTask=false;scene.Advance();probe.Verify(false);cases++;}
        }
        foreach(string stage in new[]{"permission-until","permission-from"})foreach(bool dispose in new[]{false,true})foreach(bool value in new[]{false,true})
        {
            var probe=new ServiceProbe{Stage=stage,DisposeStop=dispose,Value=value,Armed=false};
            using(var scene=new Scene(assets,catalog,configure:probe.Bind))
            {
                probe.Scene=scene;if(stage.EndsWith("until"))scene.AllowTask=false;else scene.AllowFrom=false;
                scene.Flow.Begin();int frame=1;for(;frame<=600&&scene.Host.Execution.WaitingRequests==0;frame++)scene.Flow.Tick(frame/120f);
                Check(scene.Host.Execution.WaitingRequests==1&&MetadataPending(scene)==1,"deferred "+stage+" starts from a real waiting task/ticket");
                probe.Armed=true;scene.AllowTask=scene.AllowFrom=true;
                try{scene.Flow.Tick(frame/120f);}catch(Exception error){scene.Escaped=error;}
                probe.Verify(false);cases++;
            }
        }
        foreach(string stage in new[]{"permission-until","handler-from","clock","zone-entity"})foreach(bool stop in new[]{false,true})foreach(bool oce in new[]{false,true})
        {
            var probe=new ServiceProbe{Stage=stage,Stop=stop,Error=oce?new OperationCanceledException("genuine service OCE"):new IOException("genuine service I/O error")};
            using(var scene=new Scene(assets,catalog,configure:probe.Bind)){probe.Scene=scene;scene.Advance();probe.Verify(true);cases++;}
        }
        foreach(string stage in new[]{"permission-until","mulligan-until","destination","loading"})
        {
            var probe=new ServiceProbe{Stage=stage,Stop=false,Value=stage.StartsWith("mulligan")||stage=="loading"};
            using(var scene=new Scene(assets,catalog,configure:probe.Bind))
            {
                probe.Scene=scene;scene.Flow.Begin();for(int i=1;i<=8;i++)scene.Flow.Tick(i/120f);
                bool waiting=stage.StartsWith("mulligan")?scene.Host.Execution.WaitingRequests==1:scene.Flow.ActiveClones==0;
                Check(probe.Fired&&!scene.Flow.Cancelled&&!scene.Flow.Completed&&waiting,"normal true/false gate really waits "+stage);
                probe.Value=!probe.Value;for(int i=9;i<=960;i++)scene.Flow.Tick(i/120f);
                Check(scene.Flow.Completed&&scene.Flow.FinishedNotifications==1&&!scene.Flow.Faulted&&scene.Host.Execution.IsComplete()&&scene.Gone,
                    "releasing the same real gate completes original Until/From once "+stage);cases++;
            }
        }
        foreach(bool first in new[]{false,true})
        {
            var visual=new ReadMap{{0,7}};var metadata=new ReadMap{{7,1}};int otherReads=0;
            var host=new NativeSuperSpellTaskHost(new NativePowerTaskList{tasks=new NativePowerHistoryTask[0]},new NativePowerServices(),visual,metadata);var history=host.Bind();
            if(first){visual.Read=()=>history.Cancel();metadata.Read=()=>otherReads++;}else{visual.Read=()=>otherReads++;metadata.Read=()=>history.Cancel();}
            Exception stop=CaptureStop(()=>history.MetaDataIndex(0));
            Check(stop is OperationCanceledException&&!history.Failed()&&otherReads==(first?0:1),"two-level lookup observes each actual map return stop "+first);cases++;
        }
    }
    static Exception CaptureStop(Action run){try{run();return null;}catch(Exception error){return error;}}
    public static void Run()
    {
        passed=cases=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        foreach(bool dispose in new[]{false,true})
        {
            foreach(string stage in new[]{"mapping","mapping-impact","predicate","predicate-impact","until-factory","until-body","until-nested","until-current","from-factory","from-body","from-nested","tick","failed","pending"})
                using(var scene=new Scene(assets,catalog)){scene.Hook(stage,()=>scene.Stop(dispose));scene.Advance();scene.Verify(stage+"/"+dispose);
                    if(stage=="mapping")Check(scene.Queried==0&&scene.Until==0,"mapping stop precedes predicate and Until "+dispose);cases++;}
            foreach(string stage in new[]{"entity","destination","loading"})using(var scene=new Scene(assets,catalog))
            {scene.ZoneHook(stage,()=>scene.Stop(dispose));scene.Advance();scene.Verify("zone-"+stage+"/"+dispose);
                Check((stage!="entity"||scene.Destination==0)&&(stage=="loading"||scene.Loading==0),"zone stop does not query next service "+stage);cases++;}
            foreach(int stopTask in new[]{1,2})using(var scene=new Scene(assets,catalog))
            {scene.Handler=count=>{if(count==stopTask)scene.Stop(dispose);};scene.Advance();scene.Verify("real handler "+stopTask+"/"+dispose);
                Check(scene.Tasks==stopTask&&scene.Host.Execution.FaultedRequests==0,"real handler executes once without false execution failure");cases++;}
        }
        using(var scene=new Scene(assets,catalog))
        {Check(scene.History.MetaDataIndex(0)==1&&scene.History.ShouldCompleteUntil(1),"positive real host has valid two-level mapping and earlier incomplete task");
            scene.Advance();Check(scene.Escaped==null&&scene.Flow.Completed&&scene.Flow.FinishedNotifications==1&&scene.Tasks==2&&scene.Host.Execution.IsComplete(),"same real host completes original Until/From without stop");cases++;}
        foreach(string stage in new[]{"mapping","predicate","until-factory","until-body","until-nested","from-factory","from-body","from-nested","tick","failed","pending"})foreach(bool stop in new[]{false,true})foreach(bool oce in new[]{false,true})
            using(var scene=new Scene(assets,catalog))
            {Exception genuine=oce?new OperationCanceledException("genuine external history failure"):new ObjectDisposedException("genuine history adapter failure");scene.Hook(stage,()=>{scene.Triggered=true;if(stop)scene.Flow.Cancel();throw genuine;});scene.Advance();
                Check(scene.Flow.Faulted&&ReferenceEquals(scene.Flow.LastError,genuine),"genuine external exception is retained, stage="+stage+" stop="+stop+" type="+genuine.GetType().Name);
                Check(scene.Flow.Cancelled&&scene.Flow.ActiveClones==0&&!scene.Flow.Completed,"real failure cleans without success "+stage);cases++;}
        foreach(int failTask in new[]{1,2})using(var scene=new Scene(assets,catalog))
        {var genuine=new InvalidOperationException("genuine PowerExecution handler failure");scene.Handler=count=>{if(count==failTask)throw genuine;};scene.Advance();
            Check(scene.Flow.Faulted&&ReferenceEquals(scene.HandlerError,genuine)&&ReferenceEquals(scene.Flow.LastError.GetBaseException(),genuine),"real handler failure keeps original diagnostic cause");cases++;}
        foreach(bool dispose in new[]{false,true})foreach(bool from in new[]{false,true})using(var scene=new Scene(assets,catalog))
        {
            var genuine=new ObjectDisposedException("genuine iterator disposal failure");var until=scene.History.CompleteUntil;var completeFrom=scene.History.CompleteFrom;
            if(from)scene.History.CompleteFrom=(i,done)=>{var body=new Observed(completeFrom(i,done),scene){DisposeError=genuine};scene.Stop(dispose);return body;};
            else scene.History.CompleteUntil=i=>{var body=new Observed(until(i),scene){DisposeError=genuine};scene.Stop(dispose);return body;};
            scene.Advance();Check(scene.Flow.Faulted&&ReferenceEquals(scene.Flow.LastError,genuine)&&scene.Disposals==1&&scene.Moves==0,
                "late returned iterator disposal failure remains diagnostic, dispose="+dispose+" from="+from);cases++;
        }
        Cleanup(assets,catalog);ReturnedOwnership(assets,catalog);PowerOce(assets,catalog);ServiceBoundaries(assets,catalog);
        Debug.Log("NATIVE SUPER SPELL HISTORY: "+passed+" checks / "+cases+" real-host boundary cases passed");
    }
}
