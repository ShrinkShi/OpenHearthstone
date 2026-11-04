using System;
using System.Collections.Generic;

// ActorAttackSpell.WaitThenDoAction yields null while waiting. A released
// request resumes on a later presentation frame, with its original prevState.
public sealed class NativeActorAttackWait
{
    sealed class Request{public int previous,frame;}
    readonly List<Request> requests=new List<Request>();
    public bool Waiting{get;private set;}=true;
    public int Pending{get{return requests.Count;}}
    public void Birth(){Waiting=true;}
    public void StopWaiting(){Waiting=false;}
    public void Begin(int previous,int frame,float time,Action<int,float> action)
    {if(!Waiting)action(previous,time);else requests.Add(new Request{previous=previous,frame=frame+1});}
    public void Resume(int frame,float time,Action<int,float> action)
    {
        if(Waiting)return;
        foreach(var request in requests.ToArray())
            if(frame>=request.frame){requests.Remove(request);action(request.previous,time);}
    }
    public void Reset(){requests.Clear();Waiting=true;}
}
