using System;
using UnityEngine;

// AudioVolumeFadeAction IL: captured end time, live target/duration, no exit reset.
public sealed class NativeAudioFade
{
    readonly NativeParam duration,target,stop,realtime;
    readonly Func<NativeParam,float> number;
    readonly Func<float> now;
    readonly float startTime,endTime,startVolume;
    float current;
    public bool Finished{get;private set;}
    public bool InitialWrite{get;private set;}
    public bool StopWhenFinished{get;private set;}
    public float Value{get;private set;}
    public NativeAudioFade(NativeAction action,float volume,Func<NativeParam,float> number,Func<float> now)
    {
        bool loopIn=action.name=="AudioLoopingStartAction",loopOut=action.name=="AudioLoopingEndAction";
        this.number=number;this.now=now;duration=action.Param(loopIn?"m_FadeInTime":loopOut?"m_FadeOutTime":"m_FadeTime");
        target=loopOut?new NativeParam{name="constant zero",none_known=true}:action.Param("m_TargetVolume");
        stop=loopIn?new NativeParam{name="constant false",none_known=true}:action.Param(loopOut?"m_stop":"m_StopWhenDone");
        realtime=loopIn||loopOut?new NativeParam{name="scaled clock",none_known=true}:action.Param("m_RealTime");
        foreach(var p in new[]{duration,target,stop,realtime})
            if(p.name==null||p.unresolved||!p.none_known)throw new ArgumentException("Original audio fade controls are unrestored");
        Finished=!loopIn&&number(duration)<=Mathf.Epsilon;if(Finished)return;
        startVolume=Value=loopIn?0:volume;startTime=current=now();endTime=startTime+number(duration);InitialWrite=!loopIn&&!loopOut;
    }
    public bool Step(float delta)
    {
        if(Finished)return false;
        // Preserve the original realtime branch's relative/absolute comparison.
        current=number(realtime)!=0?now()-startTime:current+delta;
        if(current<endTime)Value=Mathf.Lerp(startVolume,number(target),(current-startTime)/number(duration));
        else{Value=number(target);StopWhenFinished=number(stop)!=0;Finished=true;}
        return true;
    }
}
