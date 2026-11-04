using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;

// 只标记自主宿主生成的停止；外部抛出的 OCE 仍是真实诊断。
internal sealed class NativeSuperSpellStoppedException : OperationCanceledException
{public NativeSuperSpellStoppedException():base("SuperSpell dispatch stopped"){} }

[Serializable] public sealed class NativeSuperSpellConfig
{
    public string card_id,file,reference;public int location,target_behavior;
    public bool start_enabled,start_config_null,chain_enabled,chain_prefab_null,chain_available,reverse_available;
    public string chain_reference,friendly_area_profile,source_root_profile;
    public NativeMissilePathInfo missile;public string missile_reference;
    public NativeImpactInfo impact;public NativeSuperAreaConfig area;
    public NativeSuperStartConfig start;
}
[Serializable] public sealed class NativeSuperStartConfig
{
    public bool enabled,prefab_null,use_super_location,death_after_missiles,adjust_rotation;
    public string prefab_reference;public Vector3 rotation;
}
[Serializable] public sealed class NativeSuperSpellAsset{public string reference,file;}
[Serializable] public sealed class NativeSuperSpellCatalog{public NativeSuperSpellConfig[] programs;public NativeSuperSpellAsset[] prefabs;}

// 真实历史必须独立绑定；普通语义事件不能创建这个对象。
public sealed class NativeSuperSpellHistory
{
    public NativePowerTaskList Tasks;
    public Func<int,int> MetaDataIndex;
    public Func<int,bool> ShouldCompleteUntil;
    public Func<int,IEnumerator> CompleteUntil;
    public Func<int,Action,IEnumerator> CompleteFrom;
    public Func<int> Pending;
    public Func<bool> Failed;
    public Action Cancel;
    public Action<int> Tick;
}
public sealed class NativeSuperSpellServices
{
    public GameObject Root;public Func<GameObject> Source;public IList<GameObject> Targets;
    public bool? Friendly;public NativeSuperSpellHistory History;
    public Func<string,NativeAreaInstance> Create;
    public Action<NativeAreaInstance> Release;
    public Func<GameObject,bool> TargetInHand;
    public Func<GameObject,Vector3> HandSlot;
    public Action<string,int,float> Trace;
    public Func<float,float,float> Range=UnityEngine.Random.Range;
    public Func<bool> RandomBool=()=>UnityEngine.Random.Range(0,2)==0;
}

// 两个审核根的自主宿主：Birth/Action -> 原克隆 Birth -> 路径 -> Impact/下一发 -> Death。
public sealed class NativeSuperSpell : IDisposable
{
    const int UntargetedBehavior=6;
    sealed class Missile
    {public int index;public bool counted=true,released,arrived;public float born;public NativeAreaInstance clone;public NativeMissileFlight flight;}
    sealed class HistoryEnumerator : IEnumerator,IDisposable
    {
        readonly NativeSuperSpell owner;readonly IEnumerator body;bool closed;
        public object Current{get;private set;}
        public HistoryEnumerator(NativeSuperSpell owner,IEnumerator body){this.owner=owner;this.body=body;}
        public bool MoveNext()
        {
            owner.RequireActive();bool moved=body.MoveNext();owner.RequireActive();
            if(!moved)return false;
            var value=body.Current;
            // Current 正常返回即交付子所有权，停止门拒绝前也必须接管并处置。
            if(value is IEnumerator nested)Current=owner.AdoptHistory(nested);
            else{owner.RequireActive();Current=value;}
            return true;
        }
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){if(closed)return;closed=true;try{(body as IDisposable)?.Dispose();}finally{Current=null;}}
    }
    readonly NativeSuperSpellConfig config;readonly NativeSuperSpellServices services;
    readonly bool hasMissile;
    readonly NativePresentationCoroutines clock;readonly NativeSuperImpact impact;readonly NativeSuperArea area;
    readonly NativeSuperAction action;readonly List<Missile> missiles=new List<Missile>();
    readonly NativeSuperStart startController;NativeAreaInstance startClone;int startPending;
    readonly Dictionary<NativeAreaClone,NativeAreaInstance> clones=new Dictionary<NativeAreaClone,NativeAreaInstance>();
    readonly HashSet<NativeAreaInstance> released=new HashSet<NativeAreaInstance>();
    readonly HashSet<NativeAreaInstance> releasing=new HashSet<NativeAreaInstance>();
    readonly Dictionary<NativeAreaInstance,float> birthTimes=new Dictionary<NativeAreaInstance,float>();
    readonly List<Exception> errors=new List<Exception>();
    readonly HashSet<Action> metadataStops=new HashSet<Action>();
    public Action<string,float> DemonHunterTimingDiagnostic;
    int missilePending,metadataPending,current,dispatchDepth;bool started,disposed,settingUp,finishing,castRequested,targetedSelection;
    public bool TargetingCancelled{get;private set;}
    public bool CastRequested{get{return castRequested;}}
    public float Time{get{return clock.Time;}}
    public int Pending{get{return Cancelled?0:missilePending+impact.Pending+area.Pending+metadataPending+startPending+(services.History==null?0:services.History.Pending());}}
    public int ActiveClones{get{return clones.Count;}}
    public int FinishedNotifications{get;private set;}
    public bool Completed{get;private set;}
    public float? FirstArrivalTime{get;private set;}
    public string AreaReference{get;private set;}
    public bool Cancelled{get;private set;}
    public Exception LastError{get;private set;}
    public IReadOnlyList<Exception> Errors{get{return errors;}}
    public bool Faulted{get{return LastError!=null||impact.Faulted>0||services.History!=null&&services.History.Failed();}}
    bool Stopped{get{if(Cancelled||disposed)return true;bool faulted=Faulted;return Cancelled||disposed||faulted;}}
    public IEnumerable<NativeAreaInstance> Instances{get{return clones.Values;}}
    public IEnumerable<NativeMissileFlight> Flights{get{foreach(var request in missiles)if(request.flight!=null)yield return request.flight;}}
    public NativeSuperSpell(NativeSuperSpellConfig config,NativeSuperSpellServices services,bool selection=false)
    {
        this.config=config??throw new ArgumentNullException(nameof(config));this.services=services??throw new ArgumentNullException(nameof(services));
        targetedSelection=selection&&config.card_id=="HERO_09bp";
        if(config.card_id=="HERO_03bp")throw new NotSupportedException("HERO_03bp only permits its reviewed source reconstruction; parent Chain semantics are unresolved");
        hasMissile=config.missile!=null&&config.missile.m_Enabled==1&&!String.IsNullOrEmpty(config.missile_reference);
        bool demonHunter=config.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&NativeDemonHunterHeroPowerPlayback.SupportsConfig(config);
        bool noMissile=SupportsNoMissileUntargeted(config)||SupportsPriestNoMissileTargeted(config)||demonHunter;
        if(config.missile==null||!hasMissile&&!noMissile||config.missile.m_Enabled==1&&!hasMissile||config.missile.m_Enabled==0&&!String.IsNullOrEmpty(config.missile_reference)||config.area==null||!NativeSuperImpact.HasImpact(config.impact)||
            config.impact.prefab==null||config.impact.prefab.prefab_null||config.impact.ranges!=null&&config.impact.ranges.Length>0||
            config.chain_available||config.reverse_available||hasMissile&&config.target_behavior!=0||
            hasMissile&&config.missile.m_TimesToHitSameTarget>1||hasMissile&&config.missile.m_UseSuperSpellLocation!=1||
            config.start_enabled&&!(config.card_id=="HERO_08bp"&&config.start!=null&&config.start.enabled&&!config.start.prefab_null&&
                config.start.use_super_location&&config.start.death_after_missiles&&!config.start.adjust_rotation&&!String.IsNullOrEmpty(config.start.prefab_reference))&&
                !SupportsPriestNoMissileTargeted(config)&&!demonHunter||
            hasMissile&&!String.IsNullOrEmpty(config.missile.m_TargetJoint)&&
                !((config.card_id=="HERO_08bp"||config.card_id=="HERO_05bp")&&config.missile.m_TargetJoint=="TargetJoint"))
            throw new NotSupportedException("Unreviewed SuperSpell root branch");
        if(services.Root==null||services.Source==null||services.Targets==null||services.Create==null||services.Release==null)
            throw new NotSupportedException("SuperSpell scene host is unbound");
        if(noMissile&&config.card_id=="HERO_09bp")
        {
            var source=services.Source();
            bool validTarget=services.Targets.Count==1&&source!=null&&services.Targets[0]!=null&&services.Targets[0]!=source;
            if(targetedSelection?services.Targets.Count!=0:!validTarget)
                throw new NotSupportedException("HERO_09bp requires one distinct actual target for its no-Missile route");
        }
        else if(demonHunter&&(services.Targets.Count!=1||services.Source()==null||services.Targets[0]==null||services.Targets[0]==services.Source()))
            throw new NotSupportedException("HERO_10bp requires the same-batch self-hero as its sole distinct visual target");
        else if(noMissile&&!demonHunter&&(services.Targets.Count!=1||services.Source()==null||services.Targets[0]!=services.Source()))
            throw new NotSupportedException("Untargeted SuperSpell requires its source as the sole visual target");
        var history=services.History;
        if(history!=null&&(history.Tasks==null||history.MetaDataIndex==null||history.ShouldCompleteUntil==null||history.CompleteUntil==null||
            history.CompleteFrom==null||history.Pending==null||history.Failed==null||history.Cancel==null))
            throw new NotSupportedException("Original SuperSpell history host is incomplete");
        clock=new NativePresentationCoroutines(Fail);
        impact=new NativeSuperImpact(config.impact,new NativeSuperImpactServices{
            Root=services.Root,Source=services.Source,Targets=services.Targets,IsValidTarget=t=>t!=null,
            MetaDataIndex=MetaDataIndex,ShouldCompleteUntil=i=>history!=null&&history.ShouldCompleteUntil(i),
            CompleteUntil=i=>Until(i),CompleteFrom=(i,delay)=>From(i,delay),StartCoroutine=Schedule,
            GetSpell=p=>{var clone=Create(p.prefab_reference);return new NativeImpactClone{Clone=clone.Endpoint,AddTarget=clone.Targets.AddTarget,Rotate=v=>clone.Endpoint.Object.transform.Rotate(v)};},
            Release=(c,reset)=>Release(c),FinishIfPossible=Finish,RandomRange=services.Range,Error=(i,stage,e)=>{
                if(stage=="cancel"&&(Cancelled||disposed)&&e is OperationCanceledException)return;Fail(e);}});
        area=new NativeSuperArea(new NativeSuperAreaServices{Root=services.Root,Source=services.Source,Tasks=history==null?null:history.Tasks,
            GetSpell=info=>Create(info.prefab_reference).Endpoint,ReleaseSpell=(c,reset)=>Release(c),FinishIfPossible=Finish,
            RandomRange=services.Range,WaitScaled=(delay,callback)=>Schedule(Wait(delay,callback))});
        action=new NativeSuperAction(new NativeSuperActionServices{DetermineArea=()=>{var selected=NativeSuperArea.Determine(config.area,services.Friendly);AreaReference=selected?.prefab_reference;return selected;},SpawnArea=area.Spawn,
            HasMissile=()=>hasMissile,HasImpact=()=>NativeSuperImpact.HasImpact(config.impact),HasChain=()=>false,
            MissilesInSequence=()=>hasMissile&&config.missile.m_SpawnInSequence!=0,VisualTargetCount=()=>services.Targets.Count,CurrentTargetIndex=()=>current,
            SpawnMissileInSequence=Sequence,SpawnAllMissiles=All,SpawnAllImpacts=impact.SpawnAll,SpawnImpact=impact.Spawn,DoStartSpellAction=StartAction,
            PendingEffects=()=>Pending,HasStateContent=t=>false,BaseSpellFinished=()=>{RequireActive();FinishedNotifications++;Completed=true;Trace("Finished",-1);},
            BaseStateFinished=()=>{RequireActive();Trace("None",-1);},CleanUp=()=>{RequireActive();Trace("CleanUp",-1);services.Targets.Clear();}});
        if(config.start_enabled)startController=new NativeSuperStart(new NativeSuperStartServices{
            CheckGameEventWait=()=>false,HasStart=()=>true,DelayMin=()=>0,DelayMax=()=>0,
            SpawnStart=SpawnStart,DoActionNow=()=>{RequireActive();action.DoActionNow();},
            AddPending=n=>startPending+=n,StartCoroutine=body=>Schedule(body),RandomRange=services.Range});
    }
    public static bool SupportsNoMissileUntargeted(NativeSuperSpellConfig config)
    {
        return config!=null&&config.target_behavior==UntargetedBehavior&&config.missile!=null&&config.missile.m_Enabled==0&&
            String.IsNullOrEmpty(config.missile_reference)&&!config.start_enabled&&config.start_config_null&&!config.chain_available&&!config.reverse_available&&
            NativeSuperImpact.HasImpact(config.impact)&&config.impact.prefab!=null&&!config.impact.prefab.prefab_null&&
            (config.impact.ranges==null||config.impact.ranges.Length==0);
    }
    public static bool SupportsPaladinFriendlyAreaReconstruction(NativeProgram root,NativeSuperSpellConfig config,NativeProgram areaPrefab)
    {
        if(root==null||config==null||areaPrefab==null||root.reference!="Holy_Untargeted_FX.prefab:9abe2b276b167a9459cc37d1d50b1660"||
            root.nodes==null||root.nodes.Length!=13||config.card_id!="HERO_04bp"||config.reference!=root.reference||
            config.location!=3||config.target_behavior!=6||config.start_enabled||!config.start_config_null||
            config.missile==null||config.missile.m_Enabled!=0||!String.IsNullOrEmpty(config.missile_reference)||
            config.impact==null||config.impact.enabled||config.impact.prefab==null||!config.impact.prefab.prefab_null||
            !String.IsNullOrEmpty(config.impact.prefab.prefab_reference)||config.impact.ranges==null||config.impact.ranges.Length!=0||
            config.area==null||config.area.node!=0||config.area.kind!="SuperSpell"||
            config.area.friendly==null||!config.area.friendly.enabled||config.area.friendly.prefab_null||
            config.area.friendly.prefab_reference!="ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10"||
            config.area.friendly.prefab_source==null||config.area.friendly.prefab_source.pathid!="8695582295512710279"||
            !config.area.friendly.use_super_location||config.area.friendly.location!=0||config.area.friendly.parent_to_location||
            config.area.friendly.facing!=0||!Mathf.Approximately(config.area.friendly.delay_min,0)||
            !Mathf.Approximately(config.area.friendly.delay_max,0)||
            config.area.opponent==null||!config.area.opponent.enabled||!config.area.opponent.prefab_null||
            !String.IsNullOrEmpty(config.area.opponent.prefab_reference)||
            !config.chain_enabled||!config.chain_prefab_null||!String.IsNullOrEmpty(config.chain_reference)||
            config.chain_available||config.reverse_available||
            areaPrefab.reference!=config.area.friendly.prefab_reference||areaPrefab.nodes==null||areaPrefab.nodes.Length!=12||
            areaPrefab.machines==null||areaPrefab.machines.Length!=1||!areaPrefab.machines[0].owner_known||areaPrefab.machines[0].owner!=0||
            config.friendly_area_profile!="friendly-area-only-reconstruction; enabled-null-chain-unresolved")return false;
        return true;
    }
    public static bool SupportsPriestNoMissileTargeted(NativeSuperSpellConfig config)
    {
        const string root="Holy_Targeted_PriestHeroPower_FX.prefab:9a1243a1e217073439fc02475eba1605";
        const string start="ReuseFX_Holy_Idle_HeroPower.prefab:81346ada619d6044eaa929c6ac9f17c0";
        const string impact="ReuseFX_Holy_Impact_01.prefab:72e94a2cb20f5f243acd6d7e4dab17e1";
        var area=config==null?null:config.area;var effect=config==null?null:config.impact;var opening=config==null?null:config.start;
        return config!=null&&config.card_id=="HERO_09bp"&&config.reference==root&&config.location==2&&config.target_behavior==0&&
            config.start_enabled&&!config.start_config_null&&opening!=null&&opening.enabled&&!opening.prefab_null&&
            opening.prefab_reference==start&&opening.use_super_location&&opening.death_after_missiles&&!opening.adjust_rotation&&
            opening.rotation==Vector3.zero&&config.missile!=null&&config.missile.m_Enabled==0&&String.IsNullOrEmpty(config.missile_reference)&&
            config.chain_available==false&&config.reverse_available==false&&area!=null&&
            area.friendly!=null&&area.friendly.enabled&&area.friendly.prefab_null&&String.IsNullOrEmpty(area.friendly.prefab_reference)&&
            area.opponent!=null&&area.opponent.enabled&&area.opponent.prefab_null&&String.IsNullOrEmpty(area.opponent.prefab_reference)&&
            effect!=null&&effect.enabled&&effect.prefab!=null&&!effect.prefab.prefab_null&&effect.prefab.prefab_reference==impact&&
            effect.ranges!=null&&effect.ranges.Length==0&&!effect.use_super_location&&effect.location==6&&!effect.parent_to_location&&
            Mathf.Approximately(effect.delay_min,0)&&Mathf.Approximately(effect.delay_max,0)&&Mathf.Approximately(effect.spawn_offset,0)&&
            Mathf.Approximately(effect.game_delay_min,.5f)&&Mathf.Approximately(effect.game_delay_max,.5f)&&!effect.adjust_rotation&&effect.rotation==Vector3.zero;
    }
    void SpawnStart()
    {
        startPending++;startClone=Create(config.start.prefab_reference);RequireActive();
        var clone=startClone;var endpoint=clone.Endpoint;
        endpoint.Object.transform.SetParent(services.Root.transform,true);
        endpoint.SetSource(Invoke(services.Source));
        foreach(var target in services.Targets){RequireActive();clone.Targets.AddTarget(target);}
        endpoint.SetPosition(services.Root.transform.position);
        endpoint.AddFinished(()=>{startPending--;Trace("StartFinished",-1);Finish();});
        endpoint.AddStateStarted(()=>{if(endpoint.ActiveState()==0){Trace("StartNone",-1);Release(endpoint);}});
        var callbacks=new List<Action<int>>();
        clone.Effect.SourceStateFinished+=(type,at)=>{foreach(var callback in callbacks.ToArray()){RequireActive();callback(type);}};
        bool Usable(int type){return NativeSpellLifecycle.Destination(clone.Effect.Program.machines.Single(m=>m.owner_known&&m.owner==0),(NativeSpellStateType)type)!=null;}
        void Activate(int type){RequireActive();clone.Effect.ChangeInitializedSourceState(type,Time-birthTimes[clone]);RequireActive();}
        startController.Start=new NativeStartSpell{IsConcreteSpell=true,ActiveState=endpoint.ActiveState,HasUsableState=Usable,
            SafeActivateState=type=>{if(endpoint.ActiveState()!=type&&Usable(type))Activate(type);},ActivateState=Activate,
            UpdateTransform=()=>{endpoint.UpdatePosition();endpoint.UpdateOrientation();},
            AddStateFinished=c=>callbacks.Add(c),RemoveStateFinished=c=>callbacks.Remove(c),AddFinished=endpoint.AddFinished};
        Trace("StartBirth",-1);startController.Start.SafeActivateState(1);
    }
    void StartAction(){Trace("StartAction",-1);startController?.DoStartSpellAction();}
    void RequireActive(){if(Stopped)throw new NativeSuperSpellStoppedException();}
    void Invoke(Action callback){RequireActive();callback();RequireActive();}
    T Invoke<T>(Func<T> callback){RequireActive();T result=callback();RequireActive();return result;}
    object Schedule(IEnumerator body)
    {
        try{RequireActive();}catch(Exception error){Reject(body,error);throw;}
        return clock.Start(body);
    }
    void Reject(IEnumerator body,Exception reason)
    {
        Fail(reason);
        try{(body as IDisposable)?.Dispose();}catch(Exception cleanup){Record(cleanup);}
    }
    IEnumerator AdoptHistory(IEnumerator body)
    {
        var owned=body==null?null:new HistoryEnumerator(this,body);
        try{RequireActive();return owned;}catch(Exception error){Reject(owned,error);throw;}
    }
    IEnumerator HistoryRoutine(Func<IEnumerator> factory)
    {
        RequireActive();return AdoptHistory(factory());
    }
    // 宿主回调可同步停止根；返回后不可继续下一项配置或激活，晚通知也不再推进原分派。
    void Protect(NativeAreaClone endpoint)
    {
        var finished=endpoint.AddFinished;var started=endpoint.AddStateStarted;
        endpoint.AddFinished=callback=>Invoke(()=>finished(()=>{if(!Stopped)callback();}));
        endpoint.AddStateStarted=callback=>Invoke(()=>started(()=>{if(!Stopped)callback();}));
        var source=endpoint.SetSource;endpoint.SetSource=value=>Invoke(()=>source(value));
        var tasks=endpoint.AttachTasks;endpoint.AttachTasks=value=>Invoke(()=>tasks(value));
        var position=endpoint.SetPosition;endpoint.SetPosition=value=>Invoke(()=>position(value));
        var location=endpoint.SetLocation;endpoint.SetLocation=(value,parent)=>Invoke(()=>location(value,parent));
        var facing=endpoint.SetFacing;endpoint.SetFacing=(value,x,y)=>Invoke(()=>facing(value,x,y));
        var updatePosition=endpoint.UpdatePosition;endpoint.UpdatePosition=()=>Invoke(updatePosition);
        var updateOrientation=endpoint.UpdateOrientation;endpoint.UpdateOrientation=()=>Invoke(updateOrientation);
        var activate=endpoint.Activate;endpoint.Activate=()=>Invoke(activate);
    }
    void Trace(string phase,int index){services.Trace?.Invoke(phase,index,Time);if(phase!="Cancel")RequireActive();}
    int MetaDataIndex(int index){return services.History==null?-1:Invoke(()=>services.History.MetaDataIndex(index));}
    IEnumerator Until(int index)
    {
        metadataPending++;try{yield return HistoryRoutine(()=>services.History.CompleteUntil(index));RequireActive();}finally{metadataPending--;}
    }
    IEnumerator From(int index,float delay)
    {
        metadataPending++;bool done=false;
        Action settle=()=>{if(done)return;done=true;metadataPending--;};metadataStops.Add(settle);
        yield return new WaitForSeconds(delay);
        yield return HistoryRoutine(()=>services.History.CompleteFrom(index,()=>{if(done||Stopped)return;metadataStops.Remove(settle);settle();Finish();}));RequireActive();
        // 协程结束不等于任务完成；pending 由真正的任务回调归还。
    }
    static IEnumerator Wait(float delay,Action callback){yield return new WaitForSeconds(delay);callback();}
    public void Begin()
    {
        if(targetedSelection)throw new InvalidOperationException("HERO_09bp target-selection flow must begin through BeginSelection");
        BeginCore(false);
    }
    public void BeginSelection()
    {
        if(config.card_id=="HERO_09bp"&&!targetedSelection)
            throw new InvalidOperationException("HERO_09bp target selection requires its source-gated empty-target route");
        if(!config.start_enabled)
            throw new NotSupportedException("A SuperSpell without an authored Start prefab cannot enter targeted selection");
        BeginCore(true);
    }
    void BeginCore(bool selection)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeSuperSpell));
        if(started)throw new InvalidOperationException("SuperSpell already began");started=true;
        dispatchDepth++;
        settingUp=true;action.SettingUpAction=true;
        try
        {
            Trace("Birth",-1);RequireActive();current=0;if(startController!=null)SpawnStart();
            if(!selection)DoAction();
            settingUp=false;action.SettingUpAction=false;Finish();
        }
        catch(NativeSuperSpellStoppedException)when(Stopped){}
        catch(Exception error){Fail(error);Cancel();throw;}
        finally
        {
            settingUp=false;action.SettingUpAction=false;dispatchDepth--;
            if(Stopped)foreach(var clone in clones.Values)released.Add(clone);
            Flush();
        }
        if(Faulted&&!Cancelled){Cancel();throw new InvalidOperationException("SuperSpell host failed",LastError);}
    }
    void DoAction()
    {
        RequireActive();if(castRequested||TargetingCancelled)throw new InvalidOperationException("Selection cannot be cast twice or after cancellation");castRequested=true;
        Trace("Action",-1);action.UpdatePendingStateChangeFlags(3);if(startController==null)action.DoActionNow();else startController.DoAction();
    }
    public void Cast(IList<GameObject> targets)
    {
        if(!started||castRequested||TargetingCancelled||Stopped)throw new InvalidOperationException("Fireblast selection cannot accept cast");
        if(config.card_id=="HERO_09bp"&&(targets==null||targets.Count!=1||targets[0]==null||targets[0]==services.Source()))
            throw new NotSupportedException("HERO_09bp cast requires one distinct actual target");
        dispatchDepth++;settingUp=true;action.SettingUpAction=true;
        try{services.Targets.Clear();foreach(var target in targets){RequireActive();services.Targets.Add(target);}DoAction();}
        catch(Exception error){Fail(error);Cancel();throw;}
        finally{settingUp=false;action.SettingUpAction=false;dispatchDepth--;Flush();}
        Finish();
    }
    public void CancelSelection()
    {
        if(!started||castRequested||Stopped)throw new InvalidOperationException("Fireblast selection cannot accept target cancellation");
        if(TargetingCancelled)return;TargetingCancelled=true;dispatchDepth++;
        try
        {
            action.UpdatePendingStateChangeFlags(4);Trace("TargetCancel",-1);
            if(startController?.Start!=null){startController.Start.SafeActivateState(4);startController.Start=null;}
            Finish();
        }
        catch(Exception error){Fail(error);Cancel();throw;}
        finally{dispatchDepth--;Flush();}
    }
    void All(){for(int i=0;i<services.Targets.Count;i++)Spawn(i);StartAction();}
    void Sequence(){if(current>=services.Targets.Count)return;Spawn(current);current++;}
    void Spawn(int index)
    {
        if(Stopped)return;
        var request=new Missile{index=index};missiles.Add(request);missilePending++;
        Schedule(WaitAndSpawn(request));
    }
    IEnumerator WaitAndSpawn(Missile request)
    {
        var info=config.missile;
        float delay=services.Range(info.m_SpawnDelaySecMin,info.m_SpawnDelaySecMax);
        RequireActive();
        if(info.m_SpawnInSequence==0||request.index==0)yield return new WaitForSeconds(delay);
        if(info.m_SpawnOffset>0&&request.index>0)yield return new WaitForSeconds(info.m_SpawnOffset*request.index);
        int metadata=MetaDataIndex(request.index);
        if(services.History!=null&&Invoke(()=>services.History.ShouldCompleteUntil(metadata)))yield return Schedule(Until(metadata));
        if(Stopped)yield break;
        if(request.index>=services.Targets.Count||services.Targets[request.index]==null){Settle(request);Trace("TargetMissing",request.index);yield break;}
        var source=services.Source();if(Stopped)yield break;
        var target=services.Targets[request.index];
        var clone=Create(config.missile_reference);request.clone=clone;
        if(Stopped){Release(clone.Endpoint);yield break;}
        var endpoint=clone.Endpoint;
        endpoint.AddFinished(()=>{Settle(request);Trace("MissileFinished",request.index);Finish();});
        endpoint.AddStateStarted(()=>{if(endpoint.ActiveState()==0){Trace("MissileNone",request.index);Release(endpoint);}});
        endpoint.Object.transform.SetParent(services.Root.transform,true);
        endpoint.SetSource(source);clone.Targets.AddTarget(target);endpoint.SetPosition(services.Root.transform.position);
        request.born=Time;
        request.flight=new NativeMissileFlight(clone,info,()=>target.transform.position,services.Range,services.RandomBool,()=>Arrived(request),
            look=>Look(request.flight.MovementTransform,look,request.flight.Path.Duration),
            targetInHand:()=>services.TargetInHand!=null&&services.TargetInHand(target),
            handSlot:services.HandSlot==null?null:new Func<Vector3>(()=>services.HandSlot(target)),
            targetTransform:()=>target.transform);
        Trace("MissileBirth",request.index);RequireActive();request.flight.Birth();
    }
    void Arrived(Missile request)
    {
        if(request.arrived||Stopped)return;request.arrived=true;
        if(!FirstArrivalTime.HasValue)FirstArrivalTime=Time;
        Trace("Arrival",request.index);
        if(NativeSuperImpact.HasImpact(config.impact)){Trace("ImpactScheduled",request.index);impact.Spawn(request.index);}
        if(config.missile.m_SpawnInSequence!=0)Sequence();
        Trace("MissileDeath",request.index);
    }
    // iTween.LookUpdate(time): .15*time*.05，速度向量每次调用初始化为零，无 axis 分支。
    void Look(Transform owner,Vector3 point,float duration)
    {
        Vector3 original=owner.eulerAngles;owner.LookAt(point,Vector3.up);Vector3 target=owner.eulerAngles;owner.eulerAngles=original;
        float vx=0,vy=0,vz=0,smooth=duration*.15f*.05f;
        owner.eulerAngles=new Vector3(Mathf.SmoothDampAngle(original.x,target.x,ref vx,smooth,Mathf.Infinity,delta),
            Mathf.SmoothDampAngle(original.y,target.y,ref vy,smooth,Mathf.Infinity,delta),Mathf.SmoothDampAngle(original.z,target.z,ref vz,smooth,Mathf.Infinity,delta));
    }
    float delta;
    void Settle(Missile request){if(!request.counted)return;request.counted=false;missilePending--;}
    NativeAreaInstance Create(string reference)
    {
        RequireActive();
        var clone=services.Create(reference);clones.Add(clone.Endpoint,clone);birthTimes.Add(clone,Time);
        // 即使工厂内已取消，也先接管晚返回实例，交当前公开调用的 finally 回收。
        RequireActive();Protect(clone.Endpoint);
        string kind=reference==config.missile_reference?"Missile":reference==config.impact.prefab.prefab_reference?"Impact":reference==config.start?.prefab_reference?"Start":"Area";
        clone.Endpoint.AddStateStarted(()=>Trace(kind+"State"+clone.Endpoint.ActiveState(),-1));
        clone.Endpoint.AddFinished(()=>Trace(kind+"FinishedNotification",-1));Trace(kind+"Clone",-1);RequireActive();return clone;
    }
    bool Release(NativeAreaClone endpoint)
    {
        NativeAreaInstance clone;if(!clones.TryGetValue(endpoint,out clone))return true;
        released.Add(clone);return true;
    }
    void Flush()
    {
        if(dispatchDepth>0)return;
        foreach(var clone in new List<NativeAreaInstance>(released))
        {
            if(!released.Contains(clone)||!releasing.Add(clone))continue;
            foreach(var missile in missiles)if(missile.clone==clone){missile.flight?.Dispose();missile.released=true;}
            try{services.Release(clone);clones.Remove(clone.Endpoint);birthTimes.Remove(clone);released.Remove(clone);}
            catch(Exception error){Fail(error);}
            finally{releasing.Remove(clone);}
        }
    }
    void Finish()
    {
        if(disposed||Cancelled||Faulted||Completed||settingUp||finishing)return;
        finishing=true;try{action.FinishIfPossible();}finally{finishing=false;}
    }
    void Fail(Exception error)
    {
        if(error is NativeSuperSpellStoppedException)return;
        Record(error);
    }
    void Record(Exception error)
    {
        if(!errors.Contains(error))errors.Add(error);if(LastError==null)LastError=error;
    }
    public void Tick(float time)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeSuperSpell));
        if(Cancelled)return;
        if(time==Time)return;delta=time-Time;
        dispatchDepth++;
        try
        {
            long timing=StartDemonHunterTiming();clock.Tick(time);ReportDemonHunterTiming("demonhunter-clock-tick",timing);RequireActive();
            services.History?.Tick?.Invoke(clock.Frame);RequireActive();
            foreach(var request in missiles.ToArray())if(!request.released&&request.flight!=null&&!Stopped)
                request.flight.Tick(time-request.born);
            foreach(var pair in new List<KeyValuePair<NativeAreaClone,NativeAreaInstance>>(clones))
                if(!missiles.Exists(m=>m.clone==pair.Value))
                {RequireActive();timing=StartDemonHunterTiming();pair.Value.Seek(time-birthTimes[pair.Value]);ReportDemonHunterTiming("demonhunter-clone-seek",timing);}
        }
        catch(NativeSuperSpellStoppedException)when(Stopped){}
        catch(Exception error){Fail(error);Cancel();throw;}
        finally
        {
            dispatchDepth--;
            if(Stopped)foreach(var clone in clones.Values)released.Add(clone);
            Flush();
        }
        if(Faulted&&!Cancelled){Cancel();throw new InvalidOperationException("SuperSpell host failed",LastError);}
    }
    long StartDemonHunterTiming()
    {return config.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&DemonHunterTimingDiagnostic!=null?Stopwatch.GetTimestamp():0;}
    void ReportDemonHunterTiming(string stage,long started)
    {if(started!=0)DemonHunterTimingDiagnostic?.Invoke(stage,(float)(Stopwatch.GetTimestamp()-started)*1000f/Stopwatch.Frequency);}
    public void Cancel()
    {
        bool first=!Cancelled;Cancelled=true;
        if(first)
        {
            // 先停止请求，时钟随后 Dispose Routine 时无需再生成通用 OCE 取消通知。
            try{impact.CancelAll();}catch(Exception error){Fail(error);}
            try{clock.Dispose();}catch(Exception error){Fail(error);}
            try{services.History?.Cancel();}catch(Exception error){Fail(error);}
            // From 可先结束迭代器、后等任务回调；取消归还仍由根持有的票据，不制造完成通知。
            foreach(var settle in new List<Action>(metadataStops))settle();metadataStops.Clear();
        }
        foreach(var request in missiles){Settle(request);request.flight?.Dispose();}
        foreach(var clone in clones.Values)released.Add(clone);Flush();if(first)Trace("Cancel",-1);
    }
    public void Dispose(){if(disposed)return;Cancel();disposed=true;impact.Dispose();}
}
