using System;
using System.Collections.Generic;
using UnityEngine;

public enum Ease { Linear, In, Out, InOut, Overshoot }

public static class Tween
{
    public static float Curve(float t,Ease ease=Ease.Out)
    {
        t=Mathf.Clamp01(t);
        if(ease==Ease.In)return t*t*t;
        if(ease==Ease.Out)return 1-Mathf.Pow(1-t,3);
        if(ease==Ease.InOut)return t<.5f?4*t*t*t:1-Mathf.Pow(-2*t+2,3)/2;
        if(ease==Ease.Overshoot){float u=t-1;return 1+2.70158f*u*u*u+1.70158f*u*u;}
        return t;
    }
    public static Rect Rect(Rect a,Rect b,float t,Ease ease=Ease.Out)
    {
        t=Curve(t,ease);return new Rect(Vector2.LerpUnclamped(a.position,b.position,t),Vector2.LerpUnclamped(a.size,b.size,t));
    }
}

// 同一时轴上的轨道可并行；相邻片段可串行，无协程延迟散落在 UI 内。
public sealed class AnimationTimeline
{
    sealed class Track { public float start,duration;public Ease ease;public Action<float> sample; }
    readonly List<Track> tracks=new List<Track>();
    public float Duration{get;private set;}
    public AnimationTimeline At(float start,float duration,Action<float> sample,Ease ease=Ease.Out)
    {
        tracks.Add(new Track{start=start,duration=duration,sample=sample,ease=ease});Duration=Mathf.Max(Duration,start+duration);return this;
    }
    public AnimationTimeline Then(float duration,Action<float> sample,Ease ease=Ease.Out){return At(Duration,duration,sample,ease);}
    public void Sample(float time){foreach(var t in tracks)t.sample(Tween.Curve((time-t.start)/Mathf.Max(.001f,t.duration),t.ease));}
}
