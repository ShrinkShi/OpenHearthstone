using System;
using System.Collections.Generic;
using UnityEngine;

// Original ShakerHelper: 30 random positions, nested cosine interpolation,
// normalized falloff and camera-relative displacement. No official DLL executes.
public sealed class NativeTimelineShaker : IDisposable
{
    static readonly Dictionary<Transform,List<NativeTimelineShaker>> owners=new Dictionary<Transform,List<NativeTimelineShaker>>();
    readonly Transform target;
    readonly Vector3 origin;
    readonly Vector3[] positions=new Vector3[30];
    readonly AnimationCurve falloff;
    readonly float duration,interval;
    bool disposed;
    public NativeTimelineShaker(Transform target,NativeTimelineShakeData data,float duration,Func<float,float,float> random=null)
    {
        if(target==null||duration<=0||data==null)throw new ArgumentException("Original shaker requires target, parameters and duration");
        this.target=target;this.duration=duration;interval=data.interval;
        List<NativeTimelineShaker> list;
        if(!owners.TryGetValue(target,out list)){list=new List<NativeTimelineShaker>();owners.Add(target,list);}
        origin=list.Count==0?target.position:list[0].origin;list.Add(this);
        falloff=NativeCurve.Keys(data.curve);falloff.preWrapMode=(WrapMode)data.pre_wrap;falloff.postWrapMode=(WrapMode)data.post_wrap;
        random=random??UnityEngine.Random.Range;
        var amount=new Vector3(Mathf.Abs(data.amount.x),Mathf.Abs(data.amount.y),Mathf.Abs(data.amount.z));
        float angle=0;
        for(int i=0;i<positions.Length;i++)
        {
            angle=i==0?random(0,6.283f):random(angle+1.745f,angle+4.538f);
            float z=i>0&&positions[i-1].z>=0?random(-amount.z,0):random(0,amount.z);
            positions[i]=new Vector3(Mathf.Cos(angle)*amount.x,Mathf.Sin(angle)*amount.y,z);
        }
    }
    public void Sample(float time,bool enabled=true)
    {
        if(disposed||target==null)return;
        float normalized=Mathf.Clamp01(time/duration);
        if(Mathf.Approximately(normalized,0)||Mathf.Approximately(normalized,1)){target.position=origin;return;}
        if(!enabled)return;
        float phase=normalized*(interval>0?duration/interval:500),blend=phase-Mathf.Floor(phase);
        int index=Mathf.FloorToInt(phase);
        blend=1-(Mathf.Cos(blend*Mathf.PI)*.5f+.5f);
        blend=1-(Mathf.Cos(blend*Mathf.PI)*.5f+.5f);
        Vector3 offset=Vector3.Lerp(positions[index%30],positions[(index+1)%30],blend)*falloff.Evaluate(normalized);
        target.position=origin+target.right*offset.x+target.up*offset.y+target.forward*offset.z;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        if(target!=null)target.position=origin;
        List<NativeTimelineShaker> list;
        if(owners.TryGetValue(target,out list)){list.Remove(this);if(list.Count==0)owners.Remove(target);}
    }
}
