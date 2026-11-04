using System;
using UnityEngine;

// Original RandomWaitAction OnEnter/OnUpdate. Clock values are supplied by
// the presentation simulation rather than consulting wall time while seeking.
public sealed class NativeRandomWait
{
    readonly bool realtime;
    readonly float started;
    public readonly float Duration;
    public float Elapsed {get;private set;}
    public bool Finished {get;private set;}
    public NativeRandomWait(float minimum,float maximum,bool realTime,float start,Func<float,float,float> range)
    {
        realtime=realTime;started=start;
        if(minimum<=0&&maximum<=0){Finished=true;return;}
        Duration=range(minimum,maximum);
    }
    public bool Step(float delta,float realTime)
    {
        if(Finished)return false;
        Elapsed=realtime?realTime-started:Elapsed+delta;
        if(!(Elapsed>Duration))return false;
        Finished=true;return true;
    }
}

// The source uses Unity's float Range. Isolate its state so replay does not
// alter gameplay randomness; the seed is a client replay adapter.
public sealed class NativeRandomWaitSampler
{
    UnityEngine.Random.State state;
    public NativeRandomWaitSampler(int seed){Reset(seed);}
    public void Reset(int seed)
    {
        var saved=UnityEngine.Random.state;
        try{UnityEngine.Random.InitState(seed);state=UnityEngine.Random.state;}
        finally{UnityEngine.Random.state=saved;}
    }
    public float Range(float minimum,float maximum)
    {
        var saved=UnityEngine.Random.state;
        try{UnityEngine.Random.state=state;float result=UnityEngine.Random.Range(minimum,maximum);state=UnityEngine.Random.state;return result;}
        finally{UnityEngine.Random.state=saved;}
    }
}
