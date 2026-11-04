using System;
using System.Collections.Generic;
using UnityEngine;

// SpellState's component-owned delay is cancelled when StopImpl deactivates it.
// Hide preserves m_playing, so Show starts playback again without selecting a state.
public sealed class NativeSpellStatePlayback
{
    public readonly NativeSourceSpellState Source;
    readonly Action<bool> active;
    readonly Action<float> play;
    readonly Action<IReadOnlyList<NativeSourceSpellState>> stop;
    bool initialized,shown=true,playing,pending;
    float scheduled;
    public bool Playing{get{return playing;}}
    public bool Shown{get{return shown;}}
    public NativeSpellStatePlayback(NativeSourceSpellState source,Action<bool> active,
        Action<float> play,Action<IReadOnlyList<NativeSourceSpellState>> stop)
    {Source=source;this.active=active;this.play=play;this.stop=stop;}
    public void Initialize(float time)
    {if(initialized)return;initialized=true;if(shown&&playing)Start(time);else StopImpl(null);}
    public void Play(float time)
    {if(playing||!shown)return;playing=true;if(initialized)Start(time);}
    public void Stop(IReadOnlyList<NativeSourceSpellState> next=null)
    {if(!playing)return;playing=false;if(initialized)StopImpl(next);}
    public void Show(float time)
    {if(shown)return;shown=true;if(initialized&&playing)Start(time);}
    public void Hide()
    {if(!shown)return;shown=false;if(initialized&&playing)StopImpl(null);}
    void Start(float time)
    {
        active(true);
        if(Mathf.Approximately(Source.delay,0))play(time);
        else{pending=true;scheduled=time+Mathf.Max(0,Source.delay);}
    }
    void StopImpl(IReadOnlyList<NativeSourceSpellState> next)
    {pending=false;stop(next);active(false);}
    public void Tick(float time)
    {if(pending&&time>=scheduled){pending=false;play(scheduled);}}
}
