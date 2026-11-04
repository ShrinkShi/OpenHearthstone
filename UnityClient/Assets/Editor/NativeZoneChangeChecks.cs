using System;
using System.Collections.Generic;
using UnityEngine;

public static class NativeZoneChangeChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original zone list: "+message);passed++;}
    public static void Run()
    {
        passed=0;var list=new NativeZoneChangeList();Check(list.IsLocal()&&!list.IsComplete,"new list is local and incomplete");
        list.SetId(12);list.SetPredictedPosition(4);list.SetCanceledChangeList(true);list.SetIgnoreCardZoneChanges(true);list.SetIgnoreCardZonePurePosChanges(true);
        Check(list.GetId()==12&&list.GetPredictedPosition()==4&&list.IsCanceledChangeList()&&list.IgnoreCardZoneChanges&&list.ShouldIgnoreCardZonePurePosChanges(),"independent original fields retained");
        var calls=new List<bool>();var zone=new NativeZoneBinding{BlockInput=value=>calls.Add(value)};var change=new NativeZoneChange{source=zone,destination=zone};
        list.AddChange(change);list.AddChange(change);Check(list.GetChanges().Count==2&&list.GetLocalTriggerChange()==change,"change duplicates and first local trigger retained");
        list.SetZoneInputBlocking(true);Check(calls.Count==4&&calls.TrueForAll(value=>value),"source and destination receive blocking even when same Zone");
        list.RemoveChange(change);Check(list.GetChanges().Count==1,"remove deletes first occurrence");
        int completions=0;var data=new object();list.SetCompleteCallback((owner,actual)=>{Check(owner==list&&ReferenceEquals(actual,data),"callback identity and opaque data preserved");completions++;});list.SetCompleteCallbackUserData(data);
        list.Finish();Check(list.IsComplete&&completions==0,"Finish does not fire complete callback");list.FireCompleteCallback();list.FireCompleteCallback();Check(completions==2,"complete callback is not deduplicated");
        var wait=new NativeZoneChangeList();bool busy=true,cancelled=false;wait.MarkDirty(zone);wait.MarkDirty(zone);Check(wait.DirtyCount==1,"dirty zone set deduplicates independently of changes");
        wait.Tick(10);wait.FinishWhenPossible(()=>busy,()=>cancelled,10);Check(wait.Waiting&&!wait.IsComplete,"dirty layout waits after Update");
        wait.OnUpdateLayoutComplete(zone);wait.Tick(10);Check(wait.Waiting&&!wait.IsComplete,"same Update cannot resume again");wait.Tick(11);Check(wait.Waiting,"GameState busy waits after dirty clears");
        wait.MarkDirty(zone);busy=false;wait.Tick(12);Check(wait.IsComplete&&wait.DirtyCount==1,"busy stage does not return to dirty stage");
        var beforeUpdate=new NativeZoneChangeList();beforeUpdate.MarkDirty(zone);beforeUpdate.FinishWhenPossible(()=>false,()=>false,20);beforeUpdate.OnUpdateLayoutComplete(zone);beforeUpdate.Tick(20,6);Check(!beforeUpdate.IsComplete,"wrong player loop timing does not resume");beforeUpdate.Tick(20);Check(beforeUpdate.IsComplete,"pre-Update yield can finish in same frame");
        var abort=new NativeZoneChangeList();abort.MarkDirty(zone);abort.FinishWhenPossible(()=>false,()=>true,30);Check(abort.CancelledWaits==1&&!abort.Waiting&&!abort.IsComplete,"already cancelled Yield does not finish list");
        var immediate=new NativeZoneChangeList();immediate.FinishWhenPossible(()=>false,()=>true,30);Check(immediate.IsComplete,"no-Yield completion does not consult token");
        var parallel=new NativeZoneChangeList();bool cancelFirst=false;parallel.MarkDirty(zone);parallel.FinishWhenPossible(()=>false,()=>cancelFirst,31);parallel.FinishWhenPossible(()=>false,()=>false,31);
        Check(parallel.WaitingCount==2,"multiple original async calls retain independent waiters");cancelFirst=true;parallel.OnUpdateLayoutComplete(zone);parallel.Tick(32);
        Check(parallel.CancelledWaits==1&&parallel.IsComplete&&parallel.WaitingCount==0,"one cancellation does not replace or cancel other request");
        int failures=0;var unavailable=new NativeZoneChangeList{ExecutionError=error=>failures++};unavailable.MarkDirty(zone);unavailable.FinishWhenPossible(null,()=>false,33);unavailable.OnUpdateLayoutComplete(zone);unavailable.Tick(34);unavailable.Tick(35);
        Check(failures==1&&unavailable.FaultedWaits==1&&!unavailable.IsComplete&&!unavailable.Waiting,"missing GameState terminates and reports once, never pretends complete");
        var services=new NativePowerServices{TimelineEnd=(id,time,end)=>{},Clock=()=>1};services.handlers[4]=power=>true;
        NativeZoneChangeList actual=null;Action done=null;
        services.BeginZoneChanges=(execution,start,end,callback)=>{actual=new NativeZoneChangeList();actual.SetTaskList(execution);actual.SetCompleteCallback((owner,user)=>callback());done=actual.FireCompleteCallback;return actual;};
        var execution=new NativePowerTaskList{id=22,tasks=new[]{new NativePowerHistoryTask{type=4,entity=1,tag=49,tagChange=true}}}.CreateExecution(services);int ranges=0;
        execution.DoAllTasks((owner,start,count,user)=>ranges++);Check(actual!=null&&!actual.IsLocal()&&actual.GetLocalTriggerChange()==null&&!execution.IsComplete(),"actual zone list accepted by task execution interface");
        execution.GetTaskList()[0].SetCompleted(true);actual.FinishWhenPossible(()=>false,()=>false,40);Check(execution.IsComplete()&&ranges==0,"task and zone flags complete independently of range callback");done();Check(ranges==1,"actual zone callback delivers task range completion");
        Debug.Log("NATIVE ZONE CHANGE LIST: "+passed+" checks passed; actual layout, server zone changes and original animations remain incomplete");
        NativePowerExecutionChecks.Run();
    }
}
