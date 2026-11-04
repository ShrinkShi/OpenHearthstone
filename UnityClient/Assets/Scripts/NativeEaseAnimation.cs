using System;
using UnityEngine;

// EaseFsmAction and EaseFloat/EaseColor, transcribed from the local static IL audit.
public sealed class NativeEaseAnimation
{
    readonly Color from,to;readonly float duration,speed;readonly int kind;readonly bool reverse;
    float delay,time;bool running,finishNext;
    public bool Finished{get;private set;}
    public Color Value{get;private set;}
    public static bool Supports(int type){return type>=0&&type<=4||type==19||type==21;}
    public static float Evaluate(int type,float from,float to,float t)
    {
        float difference=to-from;
        switch(type)
        {
            case 0:return difference*t*t+from;
            case 1:return -difference*t*(t-2)+from;
            case 2:t/= .5f;if(t<1)return difference/2*t*t+from;t-=1;return -difference/2*(t*(t-2)-1)+from;
            case 3:return difference*t*t*t+from;
            case 4:t-=1;return difference*(t*t*t+1)+from;
            case 19:t-=1;return difference*Mathf.Sqrt(1-t*t)+from;
            case 21:return Mathf.Lerp(from,to,t);
            default:throw new ArgumentException("Unrestored original easing function");
        }
    }
    public NativeEaseAnimation(NativeAction action,Color from,Color to,Func<NativeParam,float> number)
    {
        this.from=from;this.to=to;
        foreach(string name in new[]{"time","speed","delay","reverse",action.name=="EaseColor"?"colorVariable":"floatVariable","fromValue","toValue"})
        {var p=action.Param(name);if(!p.none_known||p.unresolved)throw new ArgumentException("Unresolved easing parameter");}
        foreach(string name in new[]{"time","speed","delay","reverse"})
            if(!String.IsNullOrEmpty(action.Param(name).variable))throw new ArgumentException("Dynamic easing control is not bound");
        if(action.Param("realTime").unresolved||action.Param("realTime").number!=0)throw new ArgumentException("Realtime easing clock is not bound");
        var type=action.Param("easeType");kind=(int)type.number;if(type.unresolved||!Supports(kind))throw new ArgumentException("Unrestored easing type");
        duration=number(action.Param("time"));speed=action.Param("speed").is_none?1:number(action.Param("speed"));
        delay=action.Param("delay").is_none?0:number(action.Param("delay"));reverse=!action.Param("reverse").is_none&&number(action.Param("reverse"))!=0;
        if(duration<=0||speed<0)throw new ArgumentException("Unsupported easing time/speed");
        Value=reverse?to:from;
    }
    public bool Step(float delta)
    {
        if(Finished)return false;
        if(finishNext){Finished=true;return false;}
        // Unlike AnimateFsmAction, the original Ease clock does not advance during delay.
        if(!running){if(delay>=0){delay-=delta;return false;}running=true;}
        time+=delta*speed;float percentage=reverse?1-time/duration:time/duration;
        if(reverse?percentage<=0:percentage>=1){Value=reverse?from:to;running=false;finishNext=true;return true;}
        Color value=Color.clear;for(int i=0;i<4;i++)value[i]=Evaluate(kind,from[i],to[i],percentage);Value=value;return true;
    }
}
