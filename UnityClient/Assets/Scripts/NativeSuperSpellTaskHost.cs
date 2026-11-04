using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class NativeSuperSpellZoneWait
{
    // GameState/Card/Zone 的真实查询；null entity 表示原查询确实没有 Card。
    public Func<NativePowerTask,int?> Entity;
    public Func<NativePowerTask,bool> InDestinationZone,ActorLoading;
    public ISet<int> VisualEntities;
}

// 只接受显式原历史与两级映射；GameState/ZoneMgr 副作用仍由现有 PowerExecution 服务执行。
public sealed class NativeSuperSpellTaskHost
{
    readonly IDictionary<int,int> visualToTarget,targetToMetadata;
    readonly NativeSuperSpellZoneWait zone;
    bool cancelled;Exception lastError;
    public readonly NativePowerExecution Execution;
    public NativeSuperSpellTaskHost(NativePowerTaskList input,NativePowerServices services,
        IDictionary<int,int> visualToTarget,IDictionary<int,int> targetToMetadata,NativeSuperSpellZoneWait zone=null)
    {
        if(input==null||services==null||visualToTarget==null||targetToMetadata==null)throw new ArgumentNullException();
        foreach(var task in input.tasks)if(task.type==8&&(task.payloadType!="HistMetaData"||!task.metaType.HasValue||!task.metaData.HasValue))
            throw new NotSupportedException("Original metadata fields are unavailable");
        this.visualToTarget=visualToTarget;this.targetToMetadata=targetToMetadata;this.zone=zone;
        var owned=new NativePowerServices{
            EntityRevealed=services.EntityRevealed==null?null:new Action(()=>Guard(services.EntityRevealed)),
            EntityType=services.EntityType==null?null:new Func<int,int?>(id=>Guard(()=>services.EntityType(id))),
            CanDoTask=services.CanDoTask==null?null:new Func<NativePowerTask,bool>(task=>Guard(()=>services.CanDoTask(task))),
            MulliganBusy=services.MulliganBusy==null?null:new Func<bool>(()=>Guard(services.MulliganBusy)),
            Cancelled=()=>cancelled||(services.Cancelled!=null&&Guard(services.Cancelled)),
            BeginZoneChanges=services.BeginZoneChanges==null?null:new Func<NativePowerExecution,int,int,Action,INativeZoneCompletion>((list,start,end,done)=>Guard(()=>services.BeginZoneChanges(list,start,end,done))),
            GameplayPresent=services.GameplayPresent,
            Clock=services.Clock==null?null:new Func<float>(()=>Guard(services.Clock)),
            PresentationFrame=services.PresentationFrame==null?null:new Func<int>(()=>Guard(services.PresentationFrame)),
            TimelineEnd=services.TimelineEnd==null?null:new Action<int,float,bool>((id,time,end)=>Guard(()=>services.TimelineEnd(id,time,end))),
            ExecutionError=e=>{lastError=e;try{services.ExecutionError?.Invoke(e);}catch(Exception observer){if(ReferenceEquals(e,observer))throw;throw new AggregateException("Original metadata diagnostic failed",e,observer);}}};
        foreach(var pair in services.handlers){var handler=pair.Value;owned.handlers.Add(pair.Key,power=>Guard(()=>handler(power)));}
        Execution=input.CreateExecution(owned);
    }
    // 每个服务/handler 调用前后守停止门；真正外部异常仍保留，不替换成自主停止。
    T Guard<T>(Func<T> callback)
    {
        Active();T value;
        try{value=callback();}
        catch(OperationCanceledException error)when(!(error is NativeSuperSpellStoppedException))
        {throw new InvalidOperationException("Original metadata service threw OperationCanceledException",error);}
        Active();return value;
    }
    void Guard(Action callback){Guard(()=>{callback();return true;});}
    int Index(int visual)
    {int target=0,metadata=0;return Guard(()=>visualToTarget.TryGetValue(visual,out target))&&Guard(()=>targetToMetadata.TryGetValue(target,out metadata))?metadata:-1;}
    bool ShouldUntil(int index)
    {
        Active();
        var tasks=Execution.GetTaskList();
        // 保留原 IsBatchedTargetInfo 的 count < index 分支；不擅自反转源条件。
        if(tasks.Count<index)
        {
            var power=tasks[index].GetPower();
            if(power.type==8&&power.metaType==0&&power.metaData!=0)return false;
        }
        for(int i=index-1;i>=0;i--)if(!tasks[i].IsCompleted())return true;
        return false;
    }
    void CheckFailure(){if(lastError!=null)throw new InvalidOperationException("Original metadata execution failed",lastError);}
    void Active(){CheckFailure();if(cancelled)throw new NativeSuperSpellStoppedException();}
    void Tick(int frame){Execution.Tick(frame);CheckFailure();}
    IEnumerator Until(int index)
    {
        Active();Execution.DoTasks(0,index);Active();
        var queue=new List<NativePowerTask>();var identities=new Dictionary<NativePowerTask,int>();
        foreach(var task in Execution.GetTaskList().Take(index))
        {
            var power=task.GetPower();
            if(power.type!=1&&power.type!=2&&!(power.type==4&&power.tag==49))continue;
            if(zone==null||zone.Entity==null||zone.VisualEntities==null)
                throw new NotSupportedException("Original metadata zone/entity wait service is unbound");
            int? entity=Guard(()=>zone.Entity(task));if(!entity.HasValue||!Guard(()=>zone.VisualEntities.Contains(entity.Value)))continue;
            queue.RemoveAll(previous=>identities[previous]==entity.Value);queue.Add(task);identities[task]=entity.Value;
        }
        foreach(var task in queue)
        {
            while(!task.IsCompleted()){Active();yield return null;}
            if(zone.InDestinationZone==null||zone.ActorLoading==null)
                throw new NotSupportedException("Original metadata zone/loading wait service is unbound");
            while(!Guard(()=>zone.InDestinationZone(task)))yield return null;
            while(Guard(()=>zone.ActorLoading(task)))yield return null;
        }
        Active();
    }
    IEnumerator From(int index,Action callback)
    {
        Active();var tasks=Execution.GetTaskList();int count=1;
        for(int i=index+1;i<tasks.Count;i++)
        {var power=tasks[i].GetPower();if(power.type==8&&power.metaType==0)break;count++;}
        Execution.DoTasks(index,count,(list,start,length,data)=>{if(!cancelled)callback();});Active();yield break;
    }
    public NativeSuperSpellHistory Bind()
    {
        return new NativeSuperSpellHistory{Tasks=Execution.Source,MetaDataIndex=Index,ShouldCompleteUntil=ShouldUntil,
            CompleteUntil=Until,CompleteFrom=From,Pending=()=>0,Failed=()=>lastError!=null||Execution.FaultedRequests>0,
            Cancel=()=>{cancelled=true;Execution.CancelWaitingRequests();},Tick=Tick};
    }
}
