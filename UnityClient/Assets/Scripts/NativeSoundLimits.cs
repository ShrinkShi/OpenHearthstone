using System;
using System.Collections.Generic;

[Serializable] public sealed class NativeSoundOptions
{
    public bool limited;public float duration;public int maximum=1,maxOut;
}

// Source PlayImpl counts generated-object names. Release is an independent
// scaled-time coroutine, not tied to voice destruction or PCM completion.
public sealed class NativeSoundLimits
{
    sealed class Release{public string name;public NativeClipAudioDelay delay;}
    readonly Dictionary<string,int> active=new Dictionary<string,int>();
    readonly List<Release> releases=new List<Release>();
    public int Count(string name){int count;return active.TryGetValue(name,out count)?count:0;}
    public bool Acquire(string name,NativeSoundOptions options,float clipLength,float delta,Action<string> finishGenerated)
    {
        if(options==null||!options.limited)return true;
        int count;
        if(active.TryGetValue(name,out count))
        {
            if(count>=Math.Max(1,options.maximum))
            {
                if(options.maxOut!=1)return false;
                // Source scans the generated list backwards, including the new
                // source. It does not increment the existing count in this branch.
                finishGenerated(name);
            }
            else active[name]=count+1;
        }
        else active.Add(name,1);
        var release=new Release{name=name,delay=new NativeClipAudioDelay(options.duration>0?options.duration:clipLength)};
        if(release.delay.Step(delta))ReleaseCount(name);else releases.Add(release);
        return true;
    }
    void ReleaseCount(string name)
    {
        int count;if(!active.TryGetValue(name,out count))return;
        active.Remove(name);if(count>1)active.Add(name,count-1);
    }
    public void Tick(float delta)
    {
        for(int i=0;i<releases.Count;)
        {
            if(releases[i].delay.Step(delta)){ReleaseCount(releases[i].name);releases.RemoveAt(i);}
            else i++;
        }
    }
    public void Clear(){active.Clear();releases.Clear();}
}
