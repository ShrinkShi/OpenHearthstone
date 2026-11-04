using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class NativePowerExecutionChecks
{
    sealed class Zone:INativeZoneCompletion{public bool complete;public bool IsComplete{get{return complete;}}}
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original task execution: "+message);passed++;}
    static NativePowerHistoryTask Tag(int id,int tag=44){return new NativePowerHistoryTask{type=4,entity=id,tag=tag,tagChange=true};}
    static NativePowerServices Services(List<string> trace)
    {
        var services=new NativePowerServices{Clock=()=>3.25f,TimelineEnd=(id,time,end)=>trace.Add("timeline:"+id+":"+end),
            EntityRevealed=()=>trace.Add("revealed"),CanDoTask=task=>true,MulliganBusy=()=>false};
        foreach(int type in new[]{1,2,3,4,8,9,10,13,14,15,16,17})
            services.handlers[type]=power=>{trace.Add("task:"+power.type+":"+power.entity);return false;};
        return services;
    }
    public static void Run()
    {
        passed=0;var trace=new List<string>();var services=Services(trace);int callbacks=0;
        var one=new NativePowerTask(Tag(1));one.SetTaskCompleteCallback(()=>callbacks++);
        one.SetCompleted(false);one.SetCompleted(true);one.SetCompleted(true);Check(callbacks==2&&one.IsCompleted(),"repeated true invokes callbacks, false does not");
        one.DoTask(services);Check(trace.Count==0,"completed DoTask returns before dispatch");
        one.SetCompleted(false);one.DoTask(services);Check(callbacks==3&&trace.SequenceEqual(new[]{"task:4:1"}),"false setter allows execution again, GameState false return still completes");
        trace.Clear();
        var payloads=new Dictionary<int,string>{{1,"HistFullEntity"},{2,"HistShowEntity"},{3,"HistHideEntity"},{4,"HistTagChange"},{8,"HistMetaData"},{9,"HistChangeEntity"},{10,"HistResetGame"},{13,"HistVoSpell"},{14,"HistCachedTagForDormantChange"},{15,"HistShuffleDeck"},{16,"HistVoBanter"},{17,"HistTagListChange"}};
        foreach(var pair in payloads)
        {
            var task=new NativePowerTask(new NativePowerHistoryTask{type=pair.Key,payloadType=pair.Value});task.DoTask(services);
            Check(task.IsCompleted(),"original dispatch completes type "+pair.Key);
        }
        Check(trace.Take(4).SequenceEqual(new[]{"task:1:0","revealed","task:2:0","revealed"}),"Full/Show revelation happens after state update");
        foreach(int type in new[]{0,5,6,7,11,12,18}){var noop=new NativePowerTask(new NativePowerHistoryTask{type=type});noop.DoTask(services);Check(noop.IsCompleted(),"original no-op type completes "+type);}
        bool badCast=false;try{new NativePowerTask(new NativePowerHistoryTask{type=1}).DoTask(services);}catch(InvalidCastException){badCast=true;}Check(badCast,"known payload type mismatch rejected");
        var missingServices=Services(new List<string>());missingServices.handlers.Remove(4);var missing=new NativePowerTask(Tag(1));bool unsupported=false;
        try{missing.DoTask(missingServices);}catch(NotSupportedException){unsupported=true;}Check(unsupported&&!missing.IsCompleted(),"unrestored GameState handler cannot mark task complete");
        trace.Clear();var raw=new NativePowerTaskList{id=27,historyBlockEnd=true,tasks=new[]{Tag(1),Tag(2),Tag(3)}};var list=raw.CreateExecution(services);
        list.GetTaskList()[0].SetCompleted(true);trace.Clear();int ranges=0;object marker=new object();
        list.DoTasks(0,100,(owner,start,count,data)=>{Check(owner==list&&start==0&&count==100&&ReferenceEquals(data,marker),"callback preserves requested untrimmed range and user object");ranges++;},marker);
        Check(trace.SequenceEqual(new[]{"task:4:2","task:4:3","timeline:27:True"})&&ranges==1,"starts at first incomplete and clamps execution end");
        Check(list.PendingTasks==0&&list.IsComplete()&&list.ProcessEnds==1&&list.ProcessEndTime==3.25f,"pending zero stamps process end separately from range callback");
        list.GetTaskList()[2].SetCompleted(true);Check(list.PendingTasks==-1&&list.IsComplete()&&list.ProcessEnds==1,"repeated completion preserves original negative pending count");
        list.DoTasks(10,2,(owner,start,count,data)=>ranges++);Check(ranges==2,"range beyond end still completes callback");
        list.DoTasks(0,0,(owner,start,count,data)=>ranges++);Check(ranges==3,"zero range callback still runs");
        bool negative=false;try{list.DoTasks(-1,2);}catch(ArgumentOutOfRangeException){negative=true;}Check(negative,"negative index not silently clamped");
        var copied=new NativePowerExecution(new NativePowerTaskList(),services);copied.AddTasks(list);Check(copied.PendingTasks==-1&&copied.GetTaskList()[0]==list.GetTaskList()[0],"AddTasks preserves task objects and pending counter without callback rewiring");
        var added=new NativePowerTask(Tag(9));added.SetCompleted(true);copied.AddTask(added);Check(copied.PendingTasks==0,"AddTask increments even if task already complete");
        trace.Clear();bool canDo=false,busy=false,cancelled=false;int frame=20;
        services.GameplayPresent=true;services.CanDoTask=task=>canDo;services.MulliganBusy=()=>busy;services.Cancelled=()=>cancelled;services.PresentationFrame=()=>frame;
        var waiting=new NativePowerTaskList{id=28,tasks=new[]{Tag(10),Tag(11)}}.CreateExecution(services);ranges=0;
        waiting.Tick(frame);waiting.DoAllTasks((owner,start,count,data)=>ranges++);Check(waiting.WaitingRequests==1&&trace.Count==0,"CanDoTask gate yields before first task");
        canDo=true;waiting.Tick(20);waiting.Tick(21,12);Check(trace.Count==0,"same-frame and non-Update phase do not resume");
        frame=21;busy=true;waiting.Tick(frame);Check(trace.SequenceEqual(new[]{"task:4:10"})&&waiting.WaitingRequests==1&&ranges==0,"MulliganBusy yields after task execution");
        waiting.Tick(frame);Check(trace.Count==1,"repeat Tick cannot resume twice in same Update");
        frame=22;busy=false;waiting.Tick(frame);Check(waiting.IsComplete()&&waiting.WaitingRequests==0&&ranges==1,"next Update finishes remaining task and range");
        trace.Clear();canDo=false;frame=30;var abort=new NativePowerTaskList{tasks=new[]{Tag(12)}}.CreateExecution(services);ranges=0;
        abort.DoAllTasks((owner,start,count,data)=>ranges++);cancelled=true;frame=31;abort.Tick(frame);
        Check(abort.CancelledRequests==1&&abort.WaitingRequests==0&&!abort.IsComplete()&&ranges==0,"yield cancellation does not complete task or range");
        trace.Clear();canDo=true;var immediate=new NativePowerTaskList{id=29,tasks=new[]{Tag(13)}}.CreateExecution(services);immediate.DoAllTasks();
        Check(immediate.IsComplete(),"cancelled token is not consulted when no Yield occurs");
        cancelled=false;canDo=false;frame=35;var beforeUpdate=new NativePowerTaskList{id=35,tasks=new[]{Tag(35)}}.CreateExecution(services);beforeUpdate.DoAllTasks();
        canDo=true;beforeUpdate.Tick(frame,6);Check(!beforeUpdate.IsComplete(),"pre-Update phase cannot resume Update Yield");
        beforeUpdate.Tick(frame,8);Check(beforeUpdate.IsComplete(),"Yield created before Update may resume in the same frame");
        cancelled=true;
        canDo=false;var alreadyCancelled=new NativePowerTaskList{tasks=new[]{Tag(15)}}.CreateExecution(services);alreadyCancelled.DoAllTasks();
        Check(alreadyCancelled.CancelledRequests==1&&alreadyCancelled.WaitingRequests==0&&!alreadyCancelled.IsComplete(),"Yield creation observes an already cancelled token immediately");
        cancelled=false;int errors=0;services.ExecutionError=error=>errors++;services.handlers.Remove(4);frame=40;canDo=false;
        var fault=new NativePowerTaskList{tasks=new[]{Tag(14)}}.CreateExecution(services);fault.DoAllTasks();canDo=true;frame=41;fault.Tick(frame);fault.Tick(42);
        Check(errors==1&&fault.FaultedRequests==1&&fault.WaitingRequests==0&&!fault.IsComplete(),"async failure reports once and does not retry unsupported task");
        services.handlers[4]=power=>true;services.GameplayPresent=false;
        Check(NativeZonePowerRouting.IsHandledPower(new NativePowerHistoryTask{type=2},null)&&NativeZonePowerRouting.IsHandledPower(new NativePowerHistoryTask{type=3},null),"Show/Hide always routed to zones");
        foreach(int tag in new[]{49,263,50,1702,1703,2032})Check(NativeZonePowerRouting.IsHandledPower(Tag(1,tag),id=>4),"original zone tag "+tag);
        Check(!NativeZonePowerRouting.IsHandledPower(Tag(1,49),id=>1)&&!NativeZonePowerRouting.IsHandledPower(Tag(1,49),id=>2)&&NativeZonePowerRouting.IsHandledPower(Tag(1,49),id=>null),"Game/Player excluded while missing Entity is routed");
        Check(!NativeZonePowerRouting.IsHandledPower(new NativePowerHistoryTask{type=1,payloadType="HistFullEntity",tags=new[]{new NativePowerTag{name=49},new NativePowerTag{name=202,value=1}}},null),"FullEntity Game type overrides earlier zone tag");
        var zone=new Zone();Action finish=null;services.BeginZoneChanges=(owner,first,end,callback)=>{Check(first==0&&end==0,"zone service gets inclusive actual range");finish=callback;return zone;};
        var zoned=new NativePowerTaskList{id=30,tasks=new[]{Tag(1,49)}}.CreateExecution(services);ranges=0;zoned.DoAllTasks((owner,start,count,data)=>ranges++);
        Check(!zoned.AreTasksComplete()&&!zoned.AreZoneChangesComplete()&&ranges==0,"zone ownership prevents direct task dispatch");
        zoned.GetTaskList()[0].SetCompleted(true);Check(!zoned.IsComplete(),"zone completion is independent of task flags");zone.complete=true;finish();Check(zoned.IsComplete()&&ranges==1,"zone callback preserves range completion timing");
        services.BeginZoneChanges=(owner,first,end,callback)=>null;var fallthrough=new NativePowerTaskList{id=31,tasks=new[]{Tag(1,49)}}.CreateExecution(services);fallthrough.DoAllTasks();Check(fallthrough.IsComplete(),"null ZoneChangeList falls through to direct execution");
        services.BeginZoneChanges=null;var unavailable=new NativePowerTaskList{tasks=new[]{Tag(1,49)}}.CreateExecution(services);bool zoneMissing=false;
        try{unavailable.DoAllTasks();}catch(NotSupportedException){zoneMissing=true;}Check(zoneMissing&&!unavailable.IsComplete(),"missing zone service never bypasses original route");
        bool absent=false;try{new NativeSpellContext(4).CreatePowerExecution(services);}catch(InvalidOperationException){absent=true;}Check(absent,"no semantic history is invented for unattached context");
        Debug.Log("NATIVE POWER TASK EXECUTION: "+passed+" checks passed; complete GameState/ZoneMgr, raw transport and official task timeline remain incomplete");
        NativePowerTargetChecks.Run();
    }
}
