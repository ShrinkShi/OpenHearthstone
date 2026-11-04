using System;
using UnityEngine;

// CameraShakeMgr: world-axis random displacement, original replacement/hold gates.
public sealed class NativeCameraShake
{
    readonly Transform target;
    readonly Func<float,float,float> random;
    Vector3 origin,amount;
    AnimationCurve curve;
    float progress,duration;
    float? hold;
    bool overriding;
    public bool Started{get;private set;}
    public Vector3 Offset{get;private set;}
    public NativeCameraShake(Transform target,Func<float,float,float> random=null)
    {this.target=target;this.random=random??UnityEngine.Random.Range;}
    public void Play(Vector3 next,AnimationCurve intensity,float? holdAt=null,bool isOverride=false)
    {
        if(target==null)return;
        if(intensity==null||intensity.length==0||intensity[intensity.length-1].time<=0){Stop();return;}
        if(!isOverride&&next.sqrMagnitude<amount.sqrMagnitude||overriding)return;
        amount=next;curve=intensity;hold=holdAt;overriding=isOverride;
        if(!Started){Started=true;origin=target.position;}
        progress=0;duration=curve[curve.length-1].time;
    }
    public void Play(Vector3 next,float time){Play(next,AnimationCurve.Linear(0,1,time,0));}
    bool Holding{get{return hold.HasValue&&progress>=hold.Value;}}
    public void Tick(float delta)
    {
        if(!Started||target==null)return;
        if(progress>=duration&&!Holding){Stop();return;}
        float intensity=curve==null?0:curve.Evaluate(progress);
        Offset=new Vector3(random(-amount.x*intensity,amount.x*intensity),random(-amount.y*intensity,amount.y*intensity),random(-amount.z*intensity,amount.z*intensity));
        target.position=origin+Offset;
        if(!Holding)progress=Mathf.Min(progress+delta,duration);
    }
    public void Stop()
    {if(Started&&target!=null)target.position=origin;Started=false;Offset=amount=Vector3.zero;curve=null;hold=null;overriding=false;}
}
