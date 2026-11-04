using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Concrete area clone endpoint. Creation starts in original None, allowing
// source/task/position setup before Activate chooses its first usable lifecycle.
public sealed class NativeAreaInstance : IDisposable
{
    readonly List<Action> finished=new List<Action>(),started=new List<Action>();
    readonly HashSet<NativeEffectInstance.FinishNotification> notifications=new HashSet<NativeEffectInstance.FinishNotification>();
    float sampledTime;
    public float PlaybackTime{get{return sampledTime;}}
    public readonly NativeEffectInstance Effect;
    public readonly NativeAreaClone Endpoint;
    public GameObject Source{get;private set;}
    public readonly NativeSpellTargets Targets=new NativeSpellTargets();
    public NativeAreaInstance(NativeProgram program,AssetRegistry assets,int slot=0,
        Func<int,GameObject> locations=null,Func<int,GameObject> facings=null,
        Func<NativePowerTaskList,bool> attachTasks=null,Action<NativeAudioCue> audio=null,NativeSpellContext context=null,int width=512,int height=512,Func<Color?> ambient=null,Camera timelineCamera=null,bool startAtFsmEntry=false,string[] sourceSimulationSpaceNodes=null)
    {
        if(context!=null&&context.Placement!=null)Targets=context.Placement.Targets;
        Effect=new NativeEffectInstance(program,assets,slot,width,height,ambient??(()=>Color.white),
            context==null?new NativeSpellContext(0,lifecycle:"None"):context.WithLifecycle("None"),audio,sourceSimulationSpaceNodes:sourceSimulationSpaceNodes);
        try
        {
            Effect.BindTimelineCamera(timelineCamera);
            Effect.SourceRootFsmStartsAtEntry=startAtFsmEntry;
            Effect.SourceRootFsmCallbacks=true;Effect.BindSourcePlacement(locations,facings);
            Effect.Seek(0);
            // 初始化先于激活和订阅；已有 None 通知不能完成稍后激活的克隆。
            foreach(var notification in Effect.FinishNotifications)notifications.Add(notification);
            Effect.SourceStateStarted+=type=>
            {
                for(int i=started.Count-1;i>=0;i--)started[i]();
                if(Effect.ActiveSourceSpellState==0)started.Clear();
            };
            Endpoint=new NativeAreaClone{Object=Effect.Nodes[0],AddFinished=callback=>finished.Add(callback),
                AddStateStarted=callback=>started.Add(callback),ActiveState=()=>Effect.ActiveSourceSpellState,
                SetSource=value=>Source=value,AttachTasks=tasks=>
                {
                    if(attachTasks==null)throw new NotSupportedException("Original cloned Spell task attachment is unrestored");
                    return attachTasks(tasks);
                },SetPosition=Effect.SetOriginalPosition,SetLocation=Effect.SetOriginalLocation,
                SetFacing=Effect.SetOriginalFacing,UpdatePosition=Effect.UpdateOriginalPosition,
                UpdateOrientation=Effect.UpdateOriginalOrientation,Activate=Activate};
        }
        catch{Effect.Dispose();throw;}
    }
    void Activate()
    {
        var machine=Effect.Program.machines.FirstOrDefault(m=>m.owner_known&&m.owner==0);
        if(machine==null)throw new NotSupportedException("Original root area FSM is unrestored");
        var next=NativeSpellLifecycle.Next((NativeSpellStateType)Effect.ActiveSourceSpellState,
            type=>NativeSpellLifecycle.Destination(machine,type)!=null);
        Effect.SelectSourceSpellState((int)next,0);Deliver();
    }
    void Deliver()
    {
        for(int i=0;i<Effect.FinishNotifications.Count;i++)
        {
            var notification=Effect.FinishNotifications[i];
            if(notification.delivered&&notifications.Add(notification))
            {for(int j=finished.Count-1;j>=0;j--)finished[j]();finished.Clear();}
        }
    }
    void CheckTime(float time)
    {
        // Rewinding Effect rebuilds its graph and would discard clone source setup.
        // Original clone playback is forward only; a replay needs a new clone.
        if(float.IsNaN(time)||float.IsInfinity(time)||time<sampledTime)
            throw new ArgumentOutOfRangeException(nameof(time),"Original area clone playback requires finite monotonic time");
    }
    public void Seek(float time){CheckTime(time);Effect.Seek(time);sampledTime=time;Deliver();}
    public RenderTexture Render(float time){CheckTime(time);var texture=Effect.Render(time);sampledTime=time;Deliver();return texture;}
    public RenderTexture RenderAffine(float time){CheckTime(time);var texture=Effect.RenderAffine(time);sampledTime=time;Deliver();return texture;}
    public void Dispose(){Effect.Dispose();}
}
