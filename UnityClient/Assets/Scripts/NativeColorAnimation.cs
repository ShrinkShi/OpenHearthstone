using System;
using UnityEngine;

// Original AnimateColor/AnimateFsmAction timing and per-channel calculation.
public sealed class NativeColorAnimation
{
    readonly NativeParam[] curves=new NativeParam[4];readonly int[] calculations=new int[4];
    readonly Color initial;readonly float duration,speed;readonly bool explicitTime,looping;
    float current,delay;bool start=true,running,finishNext;
    public bool Finished{get;private set;}
    public readonly bool InitialWrite;
    public Color Value{get;private set;}
    public NativeColorAnimation(NativeAction action,Color initial,Func<NativeParam,float> number)
    {
        this.initial=initial;
        var time=action.Param("time");var rate=action.Param("speed");var wait=action.Param("delay");var offset=action.Param("ignoreCurveOffset");
        foreach(var p in new[]{time,rate,wait,offset,action.Param("colorVariable")})if(!p.none_known||p.unresolved)throw new ArgumentException("Unresolved AnimateColor control");
        if(action.Param("realTime").number!=0)throw new ArgumentException("Realtime AnimateColor clock is not bound");
        explicitTime=!time.is_none;duration=explicitTime?number(time):0;speed=rate.is_none?1:number(rate);delay=wait.is_none?0:number(wait);
        if(explicitTime&&duration<=0||speed<0)throw new ArgumentException("Unsupported AnimateColor time/speed");
        float largest=0;
        for(int i=0;i<4;i++)
        {
            string suffix="RGBA"[i].ToString();curves[i]=action.Param("curve"+suffix);var operation=action.Param("calculation"+suffix);
            calculations[i]=(int)operation.number;if(operation.unresolved||calculations[i]<0||calculations[i]>3)throw new ArgumentException("Unrestored AnimateColor calculation");
            var curve=curves[i];if(curve.unresolved||!curve.wrap_known)throw new ArgumentException("Missing AnimateColor curve wrap");
            if(curve.curve==null||curve.curve.Length==0)continue;
            float last=curve.CurveDuration;if(explicitTime&&last<=0)throw new ArgumentException("Zero AnimateColor curve duration");
            current=!offset.is_none&&number(offset)!=0?(explicitTime?duration/last:1)*curve.curve[0].time:0;
            largest=Mathf.Max(largest,explicitTime?duration:last);looping|=curve.post_wrap==0||curve.post_wrap==1;
        }
        if(!explicitTime)duration=largest>0?largest:1;
        InitialWrite=Mathf.Abs(number(wait))<.01f;Evaluate();
    }
    void Evaluate()
    {
        var value=initial;
        for(int i=0;i<4;i++)
        {
            var curve=curves[i];if(calculations[i]==0||curve.curve==null||curve.curve.Length==0)continue;
            float sampled=curve.SampleCurve(explicitTime?current/duration*curve.CurveDuration:current);
            value[i]=calculations[i]==1?sampled:calculations[i]==2?initial[i]+sampled:initial[i]-sampled;
        }
        Value=value;
    }
    public bool Step(float delta)
    {
        if(Finished)return false;
        if(!running&&start){if(delay>=0)delay-=delta;else{running=true;start=false;}}
        current+=delta*speed;Evaluate();
        bool write=running;
        if(finishNext&&!looping){Finished=true;return write;}
        if(running&&!looping&&current>=duration){write=true;running=false;finishNext=true;}
        return write;
    }
}
