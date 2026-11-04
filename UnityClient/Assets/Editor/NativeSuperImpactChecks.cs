using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativeSuperImpactChecks
{
    static int passed;
    static void Check(bool ok,string reason){if(!ok)throw new Exception("Super impact: "+reason);passed++;}
    static void Reject<T>(Action action,string reason) where T:Exception
    {bool rejected=false;try{action();}catch(T){rejected=true;}Check(rejected,reason);}
    sealed class Scheduler
    {
        public sealed class Job{public IEnumerator body;public object wait;public float due;public int frame;public bool done;}
        readonly List<Job> jobs=new List<Job>();public float time;int frame;
        public int Active{get{return jobs.Count(j=>!j.done);}}
        public object Start(IEnumerator body){var job=new Job{body=body};jobs.Add(job);Step(job);return job.done?null:job;}
        void Step(Job job)
        {
            if(!job.body.MoveNext()){job.done=true;(job.body as IDisposable)?.Dispose();return;}
            job.wait=job.body.Current;job.frame=frame+1;job.due=time;
            if(job.wait is WaitForSeconds wait)
                job.due+= (float)typeof(WaitForSeconds).GetField("m_Seconds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(wait);
        }
        public void Tick(float next)
        {
            time=next;frame++;
            foreach(var job in jobs.ToArray())
                if(!job.done&&frame>=job.frame&&time>=job.due&&(!(job.wait is Job dependency)||dependency.done))Step(job);
        }
        public void StopFirst(){var job=jobs.First(j=>!j.done);(job.body as IDisposable)?.Dispose();job.done=true;}
    }
    sealed class Probe : IDisposable
    {
        public readonly GameObject root=new GameObject("Impact probe root"),source=new GameObject("Impact source"),
            target=new GameObject("Impact target"),model=new GameObject("Impact clone");
        public readonly Scheduler clock=new Scheduler();public readonly List<string> events=new List<string>();
        public NativeImpactInfo info=new NativeImpactInfo{enabled=true,prefab=new NativeImpactPrefab{prefab_reference="probe"}};
        public NativeSuperImpactServices services;public NativeImpactClone impact;public NativeSuperImpact flow;
        public Action finish,started;public int state=1,finishes,releases,errors,draws;
        public Probe()
        {
            impact=new NativeImpactClone{Clone=new NativeAreaClone{Object=model,AddFinished=c=>{events.Add("finish subscription");finish=c;},
                AddStateStarted=c=>{events.Add("state subscription");started=c;},ActiveState=()=>state,
                SetSource=s=>{Check(s==source,"source identity");events.Add("source");},
                SetPosition=p=>{model.transform.position=p;events.Add("position");},SetLocation=(l,p)=>events.Add("location "+l+"/"+p),
                UpdatePosition=()=>events.Add("update position"),UpdateOrientation=()=>events.Add("orientation"),Activate=()=>events.Add("activate")},
                AddTarget=t=>{Check(t==target,"target identity");events.Add("target");},Rotate=r=>events.Add("rotate")};
            services=new NativeSuperImpactServices{Root=root,Source=()=>source,Targets=new List<GameObject>{target},IsValidTarget=t=>t!=null,
                MetaDataIndex=i=>{events.Add("metadata");return -1;},StartCoroutine=clock.Start,
                GetSpell=p=>{events.Add("clone");return impact;},Release=(c,reset)=>{Check(!reset,"release false");releases++;return false;},
                FinishIfPossible=()=>finishes++,Error=(i,stage,e)=>{errors++;events.Add("error "+stage);},
                RandomRange=(a,b)=>{draws++;events.Add("random");return a;}};
        }
        public void Create(){flow=new NativeSuperImpact(info,services);}
        public void Spawn(){Create();flow.Spawn(0);}
        public void None(){state=0;started();}
        public void Dispose(){flow?.Dispose();AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);if(model!=null)AssetRegistry.ReleaseTransient(model);}
    }
    static IEnumerator Work(List<string> events,string name,int frames,bool fail=false)
    {events.Add(name+" start");for(int i=0;i<frames;i++)yield return null;if(fail)throw new InvalidOperationException(name);events.Add(name+" end");}
    static IEnumerator Nested(List<string> events){yield return Work(events,"nested",1,true);}
    public static void Run()
    {
        passed=0;Selection();Ordering();Failures();ReentrantBoundaries();OriginalClones();
        Debug.Log("NATIVE SUPER IMPACT: "+passed+" checks passed; isolated impact lifecycle only, full root/history/official fidelity remain unverified");
    }
    public static void RunRegression(){Run();NativeMissilePathChecks.Run();NativeEffectRenderChecks.Run();}
    public static void RunSupplemental(){passed=0;ReentrantBoundaries();Debug.Log("NATIVE IMPACT SUPPLEMENTAL: "+passed+" checks passed");}
    static void Boundary(List<string> failures,string name,Action test)
    {try{test();}catch(Exception error){failures.Add(name+": "+error.Message);}}
    static void ReentrantBoundaries()
    {
        var failures=new List<string>();
        Boundary(failures,"throwing diagnostic during multi cancel",()=>
        {
            using(var p=new Probe())
            {
                p.services.Targets.Add(p.target);p.Create();p.flow.SpawnAll();
                p.services.Error=(i,stage,error)=>throw new InvalidOperationException("diagnostic observer");
                bool escaped=false;try{p.flow.CancelAll();}catch(InvalidOperationException){escaped=true;}
                finally{p.services.Error=(i,stage,error)=>p.errors++;}
                Check(!escaped&&p.flow.Pending==0&&p.flow.Cancelled==2,"diagnostic observer cannot abort cancellation of other requests");
                Check(p.flow.DiagnosticFailures==2&&p.flow.LastDiagnosticError.Message=="diagnostic observer"&&p.flow.LastError is OperationCanceledException,"diagnostic failure and primary cancellation remain separately observable");
            }
        });
        Boundary(failures,"new work during dispose",()=>
        {
            using(var p=new Probe())
            {
                bool accepted=false,rejected=false;p.Spawn();p.services.Error=(i,stage,error)=>
                {try{p.flow.Spawn(0);accepted=true;}catch(ObjectDisposedException){rejected=true;}};
                p.flow.Dispose();Check(!accepted&&rejected&&p.flow.Pending==0,"Dispose blocks reentrant enqueue before notifying cancellation");
            }
        });
        Boundary(failures,"dispose during factory return",()=>
        {
            using(var p=new Probe())
            {
                p.services.GetSpell=prefab=>{p.flow.Dispose();return p.impact;};p.Spawn();p.clock.Tick(0);
                Check(p.flow.Pending==0&&p.flow.Cancelled==1&&p.releases==1&&p.flow.ActiveClones.Count==0&&!p.events.Contains("activate"),"clone returned after cancellation is released without activation");
            }
        });
        Boundary(failures,"cancel during source resolution",()=>
        {
            using(var p=new Probe())
            {
                p.services.Source=()=>{p.flow.CancelAll();return p.source;};p.Spawn();p.clock.Tick(0);
                Check(p.flow.Pending==0&&p.flow.Cancelled==1&&!p.events.Contains("clone")&&!p.events.Contains("activate"),"source cancellation prevents allocation of a new clone");
            }
        });
        Boundary(failures,"cancel during clone setup",()=>
        {
            using(var p=new Probe())
            {
                p.impact.Clone.SetSource=source=>p.flow.CancelAll();p.Spawn();p.clock.Tick(0);
                Check(p.flow.Pending==0&&p.releases==1&&!p.events.Contains("target")&&!p.events.Contains("activate"),"setup cancellation stops later target and activation writes");
            }
        });
        Boundary(failures,"release callback reentrancy",()=>
        {
            using(var p=new Probe())
            {
                int attempts=0;p.services.Release=(clone,reset)=>{attempts++;if(attempts==1)p.flow.CancelAll();return true;};
                p.Spawn();p.clock.Tick(0);p.None();
                Check(attempts==1&&p.flow.Pending==0&&p.flow.Cancelled==1&&p.flow.ActiveClones.Count==0,"reentrant cancel does not release the same clone twice");
            }
        });
        Boundary(failures,"release failure after factory cancellation",()=>
        {
            using(var p=new Probe())
            {
                p.services.GetSpell=prefab=>{p.flow.Dispose();return p.impact;};
                p.services.Release=(clone,reset)=>throw new InvalidOperationException("release after cancel");p.Spawn();p.clock.Tick(0);
                try{Check(p.flow.Cancelled==1&&p.flow.Pending==0&&p.flow.ActiveClones.Count==1&&p.errors==2&&p.flow.LastError.Message=="release after cancel","late release failure remains observable after cancellation");}
                finally{p.services.Release=(clone,reset)=>{p.releases++;return true;};p.flow.CancelAll();}
                Check(p.releases==1&&p.flow.ActiveClones.Count==0,"late failed release can be explicitly retried after Dispose");
            }
        });
        foreach(string stop in new[]{"cancel","dispose","fault"})
        Boundary(failures,"late None release failure after "+stop,()=>
        {
            using(var p=new Probe())
            {
                int attempts=0;string lastStage=null;
                var initial=new InvalidOperationException("initial release failure");
                var late=new InvalidOperationException("late None release failure");
                var reported=new List<Exception>();
                p.services.Error=(index,stage,error)=>
                {
                    p.errors++;lastStage=stage;reported.Add(error);
                    if(stop=="dispose"&&error==late)throw new InvalidOperationException("late diagnostic observer");
                };
                p.services.Release=(clone,reset)=>
                {
                    Check(!reset,"late None still releases without reset: "+stop);attempts++;
                    if(attempts==2){p.flow.CancelAll();p.None();}
                    if(attempts<=2)throw attempts==1?initial:late;
                    p.releases++;return false;
                };
                p.Spawn();p.clock.Tick(0);
                Check(p.flow.Pending==1&&p.flow.ActiveClones.Count==1,"late None fixture owns an active clone: "+stop);
                if(stop=="cancel")p.flow.CancelAll();
                else if(stop=="dispose")p.flow.Dispose();
                else{p.services.FinishIfPossible=()=>throw new InvalidOperationException("finish failure");p.finish();}
                Check(attempts==1&&p.flow.ActiveClones.Count==1&&reported.Contains(initial),"first failed release is reported and retained: "+stop);
                int before=p.errors;p.None();
                Check(p.errors==before+1&&p.flow.LastError==late&&reported[reported.Count-1]==late&&lastStage=="release","late None reports its new release error after stop: "+stop);
                Check(attempts==2&&p.flow.ActiveClones.Count==1&&p.flow.Pending==0&&p.finishes==0&&p.flow.Cancelled==(stop=="fault"?0:1)&&p.flow.Faulted==(stop=="fault"?1:0),"late failed release isolates reentry and does not settle twice: "+stop);
                Check(p.flow.DiagnosticFailures==(stop=="dispose"?1:0)&&(stop!="dispose"||p.flow.LastDiagnosticError.Message=="late diagnostic observer"),"late diagnostic observer cannot interrupt retained clone cleanup: "+stop);
                p.flow.CancelAll();p.finish();p.None();p.flow.CancelAll();p.flow.Dispose();
                Check(attempts==3&&p.releases==1&&p.flow.ActiveClones.Count==0&&p.flow.Pending==0&&p.finishes==0&&p.flow.Cancelled==(stop=="fault"?0:1)&&p.flow.Faulted==(stop=="fault"?1:0)&&p.errors==before+1,"explicit retry recovers once and later callbacks stay inert: "+stop);
            }
        });
        Check(failures.Count==0,"supplemental boundaries: "+String.Join(" | ",failures));
    }
    static void Selection()
    {
        var fallback=new NativeImpactPrefab();var first=new NativeImpactPrefab();var second=new NativeImpactPrefab();
        var info=new NativeImpactInfo{enabled=true,prefab=fallback,ranges=new[]{new NativeImpactRange{min=2,max=5,prefab=first},new NativeImpactRange{min=4,max=8,prefab=second}}};
        Check(NativeSuperImpact.HasImpact(info),"enabled default prefab");info.enabled=false;Check(!NativeSuperImpact.HasImpact(info),"disabled impact");info.enabled=true;
        Check(NativeSuperImpact.Select(info,4)==first,"first overlapping inclusive range wins");
        Check(NativeSuperImpact.Select(info,2)==first&&NativeSuperImpact.Select(info,8)==second,"inclusive endpoints");
        Check(NativeSuperImpact.Select(info,int.MinValue)==first&&NativeSuperImpact.Select(info,int.MaxValue)==second,"out of bounds uses extreme range");
        Check(NativeSuperImpact.Select(info,null)==fallback,"missing actual DamageInfo uses default");
        info.ranges[1].min=7;Check(NativeSuperImpact.Select(info,6)==fallback,"gap uses default");
        info.ranges[1].prefab.prefab_null=true;Check(NativeSuperImpact.Select(info,9)==fallback,"null selected prefab uses default");
        info.ranges[1].min=2;info.ranges[1].max=5;info.ranges[1].prefab=second;
        Check(NativeSuperImpact.Select(info,1)==first&&NativeSuperImpact.Select(info,6)==first,"equal extremes preserve first range");
        info.prefab=null;info.ranges[0].prefab=null;Check(NativeSuperImpact.HasImpact(info)&&NativeSuperImpact.Select(info,1)==null,"range array can advertise impact with no selectable prefab");
        info.ranges=Array.Empty<NativeImpactRange>();Check(!NativeSuperImpact.HasImpact(info),"empty ranges and null default unavailable");
        Reject<ArgumentNullException>(()=>new NativeSuperImpact(null,null),"null config rejected");
        using(var p=new Probe())
        {
            p.info.delay_min=float.NaN;Reject<ArgumentOutOfRangeException>(p.Create,"nonfinite delay rejected");p.info.delay_min=0;
            p.info.ranges=new NativeImpactRange[]{null};Reject<ArgumentException>(p.Create,"null range rejected");
        }
    }
    static void Ordering()
    {
        using(var p=new Probe())
        {
            p.services.Damage=t=>throw new Exception("must skip without ranges");
            p.info.adjust_rotation=true;p.info.rotation=Vector3.one;p.Spawn();
            Check(p.flow.Pending==1&&p.draws==1&&p.finish==null,"pending precedes first sample; zero seconds still yields");
            p.clock.Tick(0);
            Check(String.Join(",",p.events)=="random,metadata,clone,state subscription,finish subscription,source,target,location 0/False,update position,rotate,orientation,activate","exact clone setup and rotation order including location zero");
            Check(p.flow.ActiveClones.Count==1&&p.model.transform.parent==p.root.transform,"clone parent and active ownership");
            p.finish();p.finish();Check(p.flow.Pending==0&&p.finishes==1&&p.releases==0,"duplicate finish settles once without releasing");
            p.started();Check(p.releases==0,"non None does not release");p.None();p.None();Check(p.releases==1&&p.flow.ActiveClones.Count==0,"None releases once independently");
        }
        using(var p=new Probe())
        {
            p.root.transform.position=Vector3.one;p.info.use_super_location=true;p.info.adjust_rotation=true;p.info.rotation=Vector3.one;
            p.Spawn();p.clock.Tick(0);Check(p.model.transform.position==Vector3.one&&!p.events.Contains("rotate")&&!p.events.Contains("update position"),"super location bypasses location and adjustment");
        }
        using(var p=new Probe())
        {
            p.info.delay_min=p.info.delay_max=.25f;p.info.spawn_offset=.5f;((List<GameObject>)p.services.Targets).Insert(0,p.source);
            p.Create();p.flow.Spawn(1);p.clock.Tick(.24f);Check(p.finish==null,"sampled positive delay waits");p.clock.Tick(.25f);Check(p.finish==null,"offset is a second yield");
            p.clock.Tick(.74f);Check(p.finish==null,"offset uses target index");p.clock.Tick(.75f);Check(p.finish!=null,"spawn resumes after exact offset");
        }
        using(var p=new Probe())
        {
            p.services.MetaDataIndex=i=>3;p.services.ShouldCompleteUntil=i=>true;
            p.services.CompleteUntil=i=>Work(p.events,"until",1);p.services.CompleteFrom=(i,d)=>Work(p.events,"from",2);
            p.Spawn();p.clock.Tick(0);Check(p.events.Contains("until start")&&!p.events.Contains("from start")&&p.finish==null,"Until gates From and cloning");
            p.clock.Tick(0);p.clock.Tick(0);Check(p.finish!=null&&p.events.IndexOf("until end")<p.events.IndexOf("from start")&&!p.events.Contains("from end")&&p.draws==2,"game delay sampled after Until; From runs concurrently with clone");
            p.finish();Check(p.flow.Pending==0&&!p.events.Contains("from end"),"visual pending excludes host metadata pending");
            p.clock.Tick(0);p.clock.Tick(0);Check(p.events.Contains("from end"),"async metadata continues independently");
        }
        using(var p=new Probe())
        {
            p.services.MetaDataIndex=i=>1;p.services.ShouldCompleteUntil=i=>true;
            p.services.CompleteUntil=i=>Work(p.events,"until",0);p.services.CompleteFrom=(i,d)=>Work(p.events,"from",0);
            p.Spawn();p.clock.Tick(0);Check(p.finish==null&&p.events.Contains("until end"),"synchronous Until still yields returned null handle");
            p.clock.Tick(0);Check(p.finish!=null&&p.events.Contains("from end"),"synchronous From completes without blocking cloning");
        }
        using(var p=new Probe())
        {
            p.services.MetaDataIndex=i=>1;p.services.ShouldCompleteUntil=i=>false;p.services.CompleteUntil=i=>throw new Exception("must skip");p.services.CompleteFrom=(i,d)=>Work(p.events,"from",0);
            p.Spawn();p.services.Targets.Clear();p.clock.Tick(0);
            Check(p.flow.Pending==0&&p.finishes==0&&p.finish==null&&p.events.Contains("from end"),"removed target checked after metadata; no fabricated finish");
        }
        using(var p=new Probe())
        {
            p.Spawn();AssetRegistry.ReleaseTransient(p.target);p.clock.Tick(0);Check(p.flow.Pending==0&&p.finish==null&&p.finishes==0,"Unity destroyed target is null after wait");
        }
        using(var p=new Probe())
        {
            p.Spawn();p.services.IsValidTarget=t=>false;p.clock.Tick(0);Check(p.finish!=null,"resume checks null only, not validity predicate");
        }
        using(var p=new Probe())
        {
            p.services.Targets.Add(null);p.services.Targets.Add(p.source);p.services.IsValidTarget=t=>t==p.target;p.Create();p.flow.SpawnAll();
            Check(p.flow.Pending==1&&p.draws==1,"SpawnAll filters invalid targets without resampling");
        }
        using(var p=new Probe())
        {
            p.impact.Clone.Activate=()=>{p.finish();p.None();};p.Spawn();p.clock.Tick(0);
            Check(p.flow.Pending==0&&p.flow.ActiveClones.Count==0&&p.finishes==1&&p.releases==1,"activation may synchronously finish and release");
        }
    }
    static void Failures()
    {
        using(var p=new Probe())
        {
            p.services.MetaDataIndex=null;p.Create();Reject<NotSupportedException>(()=>p.flow.Spawn(0),"unbound metadata mapping rejected before scheduling");Check(p.flow.Pending==0,"rejection adds no pending");
        }
        using(var p=new Probe())
        {
            p.Create();Reject<ArgumentOutOfRangeException>(()=>p.flow.Spawn(-1),"negative target index");Reject<ArgumentOutOfRangeException>(()=>p.flow.Spawn(1),"out of bounds target index");
            p.flow.Dispose();Reject<ObjectDisposedException>(()=>p.flow.Spawn(0),"disposed controller cannot spawn");
        }
        using(var p=new Probe())
        {
            p.info.ranges=new[]{new NativeImpactRange()};p.Create();Reject<NotSupportedException>(()=>p.flow.Spawn(0),"unbound DamageInfo is not a null result");
        }
        using(var p=new Probe())
        {
            p.services.StartCoroutine=c=>throw new InvalidOperationException("schedule");p.Create();Reject<InvalidOperationException>(()=>p.flow.Spawn(0),"host scheduling exception surfaced");
            Check(p.flow.Pending==0&&p.flow.Faulted==1&&p.errors==1,"schedule failure settles and reports once");
        }
        using(var p=new Probe())
        {
            p.services.StartCoroutine=c=>null;p.Create();Reject<InvalidOperationException>(()=>p.flow.Spawn(0),"scheduler must start and return suspended handle");
            Check(p.flow.Pending==0&&p.flow.Faulted==1,"invalid scheduler cannot leak pending");
        }
        foreach(string fault in new[]{"random","nonfinite","metadata","prefab","clone","endpoint","source","activate","until","from","nested"})
        using(var p=new Probe())
        {
            if(fault=="random")p.services.RandomRange=(a,b)=>throw new Exception("random");
            if(fault=="nonfinite")p.services.RandomRange=(a,b)=>float.PositiveInfinity;
            if(fault=="metadata")p.services.MetaDataIndex=i=>0;
            if(fault=="prefab"){p.info.prefab=null;p.info.ranges=new[]{new NativeImpactRange()};p.services.Damage=t=>null;}
            if(fault=="clone")p.services.GetSpell=prefab=>null;
            if(fault=="endpoint")p.impact.AddTarget=null;
            if(fault=="source")p.impact.Clone.SetSource=s=>throw new Exception("source");
            if(fault=="activate")p.impact.Clone.Activate=()=>throw new Exception("activate");
            if(fault=="until"||fault=="from"||fault=="nested")
            {
                p.services.MetaDataIndex=i=>0;p.services.ShouldCompleteUntil=i=>fault=="until";
                p.services.CompleteUntil=i=>Work(p.events,"until",1,true);
                p.services.CompleteFrom=(i,d)=>fault=="nested"?Nested(p.events):Work(p.events,"from",1,true);
            }
            p.Spawn();p.clock.Tick(0);p.clock.Tick(0);p.clock.Tick(0);
            Check(p.flow.Pending==0&&p.flow.Faulted==1&&p.errors==1&&p.finishes==0,"fault settles without success: "+fault);
            Check(p.flow.ActiveClones.Count==0,"fault releases owned clone: "+fault);
            if(p.finish!=null){p.finish();p.None();Check(p.flow.Pending==0&&p.finishes==0&&p.releases==1,"late callbacks inert: "+fault);}
        }
        foreach(string phase in new[]{"spawn","until","from","finished"})
        using(var p=new Probe())
        {
            if(phase=="until"||phase=="from")
            {p.services.MetaDataIndex=i=>0;p.services.ShouldCompleteUntil=i=>phase=="until";p.services.CompleteUntil=i=>Work(p.events,"until",5);p.services.CompleteFrom=(i,d)=>Work(p.events,"from",5);}
            p.Spawn();if(phase!="spawn")p.clock.Tick(0);if(phase=="finished")p.finish();
            p.flow.CancelAll();p.flow.CancelAll();for(int i=0;i<8;i++)p.clock.Tick(0);
            Check(p.flow.Pending==0&&p.flow.Cancelled==1&&p.flow.Faulted==0&&p.clock.Active==0,"cancel owns coroutine tree: "+phase);
            if(p.finish!=null){p.finish();p.None();Check(p.releases==1&&p.finishes==(phase=="finished"?1:0),"cancelled clone ignores late callbacks: "+phase);}
        }
        using(var p=new Probe())
        {p.Spawn();p.clock.StopFirst();Check(p.flow.Pending==0&&p.flow.Cancelled==1,"host Dispose cancels suspended root routine");}
        using(var p=new Probe())
        {
            p.services.MetaDataIndex=i=>0;p.services.ShouldCompleteUntil=i=>false;p.services.CompleteUntil=i=>Work(p.events,"unused",0);
            p.services.CompleteFrom=(i,d)=>Work(p.events,"from",2,true);p.Spawn();p.clock.Tick(0);p.finish();p.None();p.clock.Tick(0);p.clock.Tick(0);
            Check(p.flow.Pending==0&&p.finishes==1&&p.releases==1&&p.flow.Faulted==1,"detached metadata failure remains observable after visual release");
        }
        using(var p=new Probe())
        {
            p.Spawn();p.clock.Tick(0);p.services.Release=(c,reset)=>throw new Exception("release");p.None();
            Check(p.flow.Pending==0&&p.flow.Faulted==1&&p.flow.ActiveClones.Count==1,"failed release retained for explicit retry");
            p.services.Release=(c,reset)=>{p.releases++;return false;};p.flow.CancelAll();Check(p.releases==1&&p.flow.ActiveClones.Count==0,"failed release retried without duplicate settlement");
        }
    }
    static void OriginalClones()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<NativeImpactCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-impact.json")));
        GameObject root=new GameObject("Original impact host"),source=new GameObject("Original impact caster"),target=new GameObject("Original impact target");
        root.transform.position=new Vector3(20000,10000,20000);
        target.transform.position=root.transform.position;
        try
        {
            foreach(var record in catalog.programs)
            using(var factory=new NativeImpactFactory(assets,catalog,(instance,location)=>
            {Check(location==6&&instance.Targets.GetTarget()==target,"actual clone Target location service "+record.card_id);return target;},
                (instance,facing)=>{Check(facing==6,"actual clone target facing service "+record.card_id);return target;}))
            {
                var clock=new Scheduler();int finishes=0;var services=new NativeSuperImpactServices{Root=root,Source=()=>source,Targets=new List<GameObject>{target},
                    IsValidTarget=t=>t!=null,MetaDataIndex=i=>-1,StartCoroutine=clock.Start,RandomRange=(a,b)=>a,GetSpell=factory.GetSpell,Release=factory.Release,FinishIfPossible=()=>finishes++};
                using(var flow=new NativeSuperImpact(record.config.impact,services))
                {
                    flow.Spawn(0);Check(factory.Instances.Count==0&&flow.Pending==1,"actual zero wait not skipped "+record.card_id);clock.Tick(0);
                    Check(factory.Instances.Count==1&&flow.Faulted==0,"actual original impact created "+record.card_id);
                    var instance=factory.Instances.Values.Single();var endpoint=instance.Endpoint;
                    Check(endpoint.Object.activeInHierarchy&&instance.Effect.ActiveSourceSpellState==5,"actual first usable state is Death, no initial None deactivation "+record.card_id);
                    Check(instance.Source==source&&instance.Targets.GetTarget()==target&&endpoint.Object.transform.parent==root.transform,"actual clone identity setup "+record.card_id);
                    var stagingRoot=instance.Effect.Root;
                    var materials=instance.Effect.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                    RenderTexture lastTexture=null;int maxPixels=0;foreach(float time in new[]{.01f,.1f,.3f,.6f,1f,2f,3f,5f})
                    {
                        lastTexture=instance.Render(time);maxPixels=Math.Max(maxPixels,Capture(lastTexture,record.card_id,time));
                    }
                    Check(maxPixels>128,"actual original impact renders visible pixels "+record.card_id);
                    Check(finishes==1&&flow.Pending==0&&flow.Faulted==0,"actual original finish once "+record.card_id);
                    Check(instance.Effect.ActiveSourceSpellState==0&&flow.ActiveClones.Count==0,"actual original None releases "+record.card_id);
                    factory.FlushReleased();Check(factory.Instances.Count==0&&endpoint.Object==null&&stagingRoot==null&&lastTexture==null&&materials.All(m=>m==null),"actual clone, camera staging, render texture and transient materials physically disposed "+record.card_id);
                    File.WriteAllText("../tests/visual/current/native-super-impact/"+record.card_id+"-lifecycle.json",JsonUtility.ToJson(new Evidence{card_id=record.card_id,max_pixels=maxPixels,finishes=finishes,pending=flow.Pending,released=true}));
                    services.Targets.Add(target);flow.SpawnAll();clock.Tick(0);
                    Check(factory.Instances.Count==2&&flow.Pending==2,"simultaneous requests own distinct actual clones "+record.card_id);
                    var objects=factory.Instances.Keys.Select(c=>c.Object).ToArray();flow.CancelAll();factory.FlushReleased();
                    Check(flow.Pending==0&&flow.Cancelled==2&&factory.Instances.Count==0&&objects.All(o=>o==null),"cancel releases both actual GPU graphs "+record.card_id);
                    services.Targets.RemoveAt(1);services.GetSpell=prefab=>{var result=factory.GetSpell(prefab);flow.Dispose();return result;};
                    flow.Spawn(0);clock.Tick(0);
                    var cancelled=factory.Instances.Values.Single();var cancelledRoot=cancelled.Effect.Root;
                    var cancelledTexture=cancelledRoot.GetComponentsInChildren<Camera>(true).Single(c=>c.name=="NativeEffectCamera").targetTexture;
                    Check(flow.Pending==0&&flow.Cancelled==3&&flow.ActiveClones.Count==0&&cancelled.Effect.ActiveSourceSpellState==0&&cancelled.Source==null&&cancelled.Targets.GetTarget()==null,"actual clone returned during Dispose never receives source/target/activation "+record.card_id);
                    factory.FlushReleased();Check(factory.Instances.Count==0&&cancelledRoot==null&&cancelledTexture==null,"actual late factory return releases GPU staging and texture "+record.card_id);
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);}
    }
    [Serializable] sealed class Evidence{public string card_id;public int max_pixels,finishes,pending;public bool released,whole_effect_verified=false;}
    static int Capture(RenderTexture texture,string card,float time)
    {
        var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
            string folder="../tests/visual/current/native-super-impact";Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,card+"-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
            return image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3);
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
}
