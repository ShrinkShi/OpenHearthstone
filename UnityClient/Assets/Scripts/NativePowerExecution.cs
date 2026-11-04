using System;
using System.Collections.Generic;
using System.Linq;

public interface INativeZoneCompletion{bool IsComplete{get;}}
public sealed class NativePowerServices
{
    public readonly Dictionary<int,Func<NativePowerHistoryTask,bool>> handlers=new Dictionary<int,Func<NativePowerHistoryTask,bool>>();
    public Action EntityRevealed;
    public Func<int,int?> EntityType;
    public Func<NativePowerTask,bool> CanDoTask;
    public Func<bool> MulliganBusy,Cancelled;
    public Func<NativePowerExecution,int,int,Action,INativeZoneCompletion> BeginZoneChanges;
    public bool GameplayPresent;
    public Func<float> Clock;
    public Func<int> PresentationFrame;
    public Action<Exception> ExecutionError;
    public Action<int,float,bool> TimelineEnd;
}

public sealed class NativePowerTask
{
    NativePowerHistoryTask power;bool completed;Action onCompleted;
    public NativePowerTask(NativePowerHistoryTask power){this.power=power;}
    public NativePowerHistoryTask GetPower(){return power;}
    public void SetPower(NativePowerHistoryTask value){power=value;}
    public bool IsCompleted(){return completed;}
    public void SetTaskCompleteCallback(Action value){onCompleted=value;}
    public void SetCompleted(bool value){completed=value;if(completed&&onCompleted!=null)onCompleted();}
    static readonly Dictionary<int,string> payloads=new Dictionary<int,string>
    {{1,"HistFullEntity"},{2,"HistShowEntity"},{3,"HistHideEntity"},{4,"HistTagChange"},{8,"HistMetaData"},{9,"HistChangeEntity"},{10,"HistResetGame"},{13,"HistVoSpell"},{14,"HistCachedTagForDormantChange"},{15,"HistShuffleDeck"},{16,"HistVoBanter"},{17,"HistTagListChange"}};
    public void DoTask(NativePowerServices services)
    {
        if(completed)return;
        string kind;
        if(payloads.TryGetValue(power.type,out kind))
        {
            bool valid=power.payloadType==kind||power.type==4&&power.tagChange&&String.IsNullOrEmpty(power.payloadType);
            if(!valid)throw new InvalidCastException("Original history payload requires "+kind);
            Func<NativePowerHistoryTask,bool> handler;
            if(!services.handlers.TryGetValue(power.type,out handler))throw new NotSupportedException("Original GameState handler is unrestored: "+kind);
            handler(power); // Original DoTask ignores the returned GameState boolean.
            if((power.type==1||power.type==2)&&services.EntityRevealed!=null)services.EntityRevealed();
            else if((power.type==1||power.type==2)&&services.EntityRevealed==null)throw new NotSupportedException("Original HistoryManager revelation is unrestored");
        }
        SetCompleted(true);
    }
}

public static class NativeZonePowerRouting
{
    static bool ZoneTag(int tag){return tag==49||tag==263||tag==50||tag==1702||tag==1703||tag==2032;}
    public static bool IsHandledPower(NativePowerHistoryTask power,Func<int,int?> entityType)
    {
        if(power.type==1)
        {
            if(power.payloadType!="HistFullEntity")throw new NullReferenceException("Original FullEntity payload is missing");
            bool handled=false;
            foreach(var tag in power.tags)
            {if(tag.name==202&&(tag.value==1||tag.value==2))return false;if(ZoneTag(tag.name))handled=true;}
            return handled;
        }
        if(power.type==2||power.type==3)return true;
        if(power.type!=4)return false;
        if(!power.tagChange)throw new NullReferenceException("Original TagChange payload is missing");
        if(!ZoneTag(power.tag))return false;
        int? type=entityType==null?null:entityType(power.entity);
        return type!=1&&type!=2;
    }
}

// Client-only execution bridge. Original services are explicit dependencies;
// unavailable GameState/ZoneMgr work cannot be silently marked complete.
public sealed class NativePowerExecution
{
    public delegate void CompleteCallback(NativePowerExecution list,int start,int count,object data);
    sealed class Request{public int current,end,start,count,frame;public bool afterTask;public CompleteCallback callback;public object data;}
    readonly List<NativePowerTask> tasks=new List<NativePowerTask>();
    readonly List<Request> requests=new List<Request>();readonly NativePowerServices services;
    public readonly NativePowerTaskList Source;
    INativeZoneCompletion zone;int frame,lastTick=-1;
    public int PendingTasks{get;private set;}
    public int WaitingRequests{get{return requests.Count;}}
    public void CancelWaitingRequests(){CancelledRequests+=requests.Count;requests.Clear();}
    public int CancelledRequests{get;private set;}
    public int FaultedRequests{get;private set;}
    public int ProcessEnds{get;private set;}
    public float ProcessEndTime{get;private set;}
    public NativePowerExecution(NativePowerTaskList source,NativePowerServices services){Source=source;this.services=services;}
    public List<NativePowerTask> GetTaskList(){return tasks;}
    public NativePowerTask CreateTask(NativePowerHistoryTask power)
    {PendingTasks++;var task=new NativePowerTask(power);task.SetTaskCompleteCallback(OnTaskCompleted);tasks.Add(task);return task;}
    public void AddTask(NativePowerTask task){tasks.Add(task);PendingTasks++;}
    public void AddTasks(NativePowerExecution other){tasks.AddRange(other.tasks);PendingTasks+=other.PendingTasks;}
    void OnTaskCompleted()
    {
        PendingTasks--;
        if(PendingTasks==0)
        {
            ProcessEnds++;ProcessEndTime=services.Clock==null?UnityEngine.Time.realtimeSinceStartup:services.Clock();
            if(services.TimelineEnd==null)throw new NotSupportedException("Original PowerProcessor timeline-end service is unrestored");
            services.TimelineEnd(Source.id,ProcessEndTime,Source.historyBlockEnd);
        }
    }
    public bool AreTasksComplete(){return tasks.All(t=>t.IsCompleted());}
    public bool AreZoneChangesComplete(){return zone==null||zone.IsComplete;}
    public bool IsComplete(){return AreTasksComplete()&&AreZoneChangesComplete();}
    public void DoAllTasks(CompleteCallback callback=null){DoTasks(0,tasks.Count,callback,null);}
    public void DoTasks(int start,int count,CompleteCallback callback=null,object data=null)
    {
        if(services.PresentationFrame!=null)frame=services.PresentationFrame();
        int end=Math.Min(unchecked(start+count-1),tasks.Count-1),first=-1;bool handled=false;
        for(int i=start;i<=end;i++)
        {
            if(tasks[i].IsCompleted())continue;
            if(first<0)first=i;
            if(NativeZonePowerRouting.IsHandledPower(tasks[i].GetPower(),services.EntityType)){handled=true;break;}
        }
        if(first<0)first=start;
        if(handled)
        {
            if(services.BeginZoneChanges==null)throw new NotSupportedException("Original ZoneMgr execution is unrestored");
            zone=services.BeginZoneChanges(this,first,end,()=>{if(callback!=null)callback(this,start,count,data);});
            if(zone!=null)return;
        }
        var request=new Request{current=first,end=end,start=start,count=count,callback=callback,data=data};
        if(!services.GameplayPresent)
        {for(int i=first;i<=end;i++)tasks[i].DoTask(services);if(callback!=null)callback(this,start,count,data);return;}
        if(!RunAsync(request))requests.Add(request);
    }
    bool RunAsync(Request request)
    {
        try{return Run(request);}
        catch(OperationCanceledException){CancelledRequests++;return true;}
        catch(Exception error){FaultedRequests++;if(services.ExecutionError!=null)services.ExecutionError(error);else UnityEngine.Debug.LogException(error);return true;}
    }
    bool Yield(Request request)
    {
        if(services.Cancelled!=null&&services.Cancelled())throw new OperationCanceledException();
        request.frame=frame+(lastTick==frame?1:0);return false;
    }
    bool Run(Request request)
    {
        while(request.current<=request.end)
        {
            if(!request.afterTask)
            {
                if(services.CanDoTask==null)throw new NotSupportedException("Original PowerProcessor.CanDoTask is unrestored");
                if(!services.CanDoTask(tasks[request.current]))return Yield(request);
                tasks[request.current].DoTask(services);request.afterTask=true;
            }
            if(services.MulliganBusy==null)throw new NotSupportedException("Original GameState.IsMulliganBusy is unrestored");
            if(services.MulliganBusy())return Yield(request);
            request.afterTask=false;request.current++;
        }
        if(request.callback!=null)request.callback(this,request.start,request.count,request.data);return true;
    }
    public void Tick(int presentationFrame,int playerLoopTiming=8)
    {
        if(playerLoopTiming!=8||lastTick==presentationFrame)return;lastTick=presentationFrame;frame=presentationFrame;
        foreach(var request in requests.ToArray())
        {
            if(frame<request.frame)continue;
            if(services.Cancelled!=null&&services.Cancelled()){requests.Remove(request);CancelledRequests++;continue;}
            if(RunAsync(request))requests.Remove(request);
        }
    }
}
