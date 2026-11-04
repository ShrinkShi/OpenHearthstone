using System;
using System.Collections.Generic;

// A scene Zone endpoint. Actual layout and Card-zone updates are separate services.
public sealed class NativeZoneBinding
{
    public Action<bool> BlockInput;public bool IsHand;
    public Action UpdateLayout;public Func<Action<NativeZoneBinding>,bool> AddLayoutComplete;
    public Func<IList<NativeZoneLayoutCard>> Cards;
}
public sealed class NativeZoneChange
{public NativeZoneBinding source,destination;}

public sealed class NativeZoneChangeList:INativeZoneCompletion
{
    readonly List<NativeZoneChange> changes=new List<NativeZoneChange>();
    readonly HashSet<NativeZoneBinding> dirty=new HashSet<NativeZoneBinding>();
    Action<NativeZoneChangeList,object> callback;object callbackData;
    NativePowerExecution taskList;bool complete,cancelledList,ignoreChanges,ignorePurePosition;
    sealed class Wait{public int eligible;public bool busyStage;public Func<bool> busy,cancelled;}
    readonly List<Wait> waits=new List<Wait>();
    int id,predicted,frame,lastTick=-1,runningUpdate=-1;
    public void BeginUpdatePhase(int value){runningUpdate=value;}
    public void EndUpdatePhase(){runningUpdate=-1;}
    public bool IsComplete{get{return complete;}}
    public int DirtyCount{get{return dirty.Count;}}
    public bool Waiting{get{return waits.Count>0;}}
    public int WaitingCount{get{return waits.Count;}}
    public int CancelledWaits{get;private set;}
    public int FaultedWaits{get;private set;}
    public Action<Exception> ExecutionError;
    public int GetId(){return id;}public void SetId(int value){id=value;}
    public NativePowerExecution GetTaskList(){return taskList;}public void SetTaskList(NativePowerExecution value){taskList=value;}
    public bool IsLocal(){return taskList==null;}
    public int GetPredictedPosition(){return predicted;}public void SetPredictedPosition(int value){predicted=value;}
    public void SetIgnoreCardZoneChanges(bool value){ignoreChanges=value;}
    public bool IgnoreCardZoneChanges{get{return ignoreChanges;}}
    public void SetIgnoreCardZonePurePosChanges(bool value){ignorePurePosition=value;}
    public bool ShouldIgnoreCardZonePurePosChanges(){return ignorePurePosition;}
    public bool IsCanceledChangeList(){return cancelledList;}public void SetCanceledChangeList(bool value){cancelledList=value;}
    public List<NativeZoneChange> GetChanges(){return changes;}
    public void AddChange(NativeZoneChange value){changes.Add(value);}public void RemoveChange(NativeZoneChange value){changes.Remove(value);}
    public NativeZoneChange GetLocalTriggerChange(){return !IsLocal()||changes.Count==0?null:changes[0];}
    public void SetZoneInputBlocking(bool value)
    {
        foreach(var change in changes)
        {if(change.source!=null)change.source.BlockInput(value);if(change.destination!=null)change.destination.BlockInput(value);}
    }
    public void SetCompleteCallback(Action<NativeZoneChangeList,object> value){callback=value;}
    public void SetCompleteCallbackUserData(object value){callbackData=value;}
    public void FireCompleteCallback(){if(callback!=null)callback(this,callbackData);}
    public void Finish(){complete=true;}
    public void MarkDirty(NativeZoneBinding zone){dirty.Add(zone);}
    public HashSet<NativeZoneBinding> DirtyZones{get{return dirty;}}
    public void OnUpdateLayoutComplete(NativeZoneBinding zone){dirty.Remove(zone);}
    public void FinishWhenPossible(Func<bool> gameStateBusy,Func<bool> tokenCancelled,int presentationFrame)
    {
        frame=presentationFrame;var wait=new Wait{busy=gameStateBusy,cancelled=tokenCancelled};
        if(!RunAsync(wait))waits.Add(wait);
    }
    bool Yield(Wait wait)
    {
        if(wait.cancelled!=null&&wait.cancelled()){CancelledWaits++;return true;}
        wait.eligible=frame+(lastTick==frame||runningUpdate==frame?1:0);return false;
    }
    bool Run(Wait wait)
    {
        if(!wait.busyStage)
        {if(dirty.Count>0)return Yield(wait);wait.busyStage=true;}
        if(wait.busy==null)throw new NotSupportedException("Original GameState.IsBusy service is unrestored");
        if(wait.busy())return Yield(wait);Finish();return true;
    }
    bool RunAsync(Wait wait)
    {
        try{return Run(wait);}
        catch(Exception error){FaultedWaits++;if(ExecutionError!=null)ExecutionError(error);else UnityEngine.Debug.LogException(error);return true;}
    }
    public void Tick(int presentationFrame,int playerLoopTiming=8)
    {
        if(playerLoopTiming!=8||lastTick==presentationFrame)return;lastTick=presentationFrame;frame=presentationFrame;
        foreach(var wait in waits.ToArray())
        {
            if(frame<wait.eligible)continue;
            if(wait.cancelled!=null&&wait.cancelled()){CancelledWaits++;waits.Remove(wait);continue;}
            if(RunAsync(wait))waits.Remove(wait);
        }
    }
}
