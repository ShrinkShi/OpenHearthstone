using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAudioStopChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original AudioStop: "+reason);passed++;}
    static NativeAction Wait(float time,string next)
    {return new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=time},new NativeParam{name="finishEvent",text=next}}};}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var play=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        int node=play.Param("m_GameObject").node;play.Param("m_Delay").number=0;play.Param("m_FinishedEvent").text="";
        var stop=new NativeAction{name="AudioStopAction",@params=new[]{new NativeParam{name="m_GameObject",node=node}}};
        var replay=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(play));
        program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
            new NativeState{name="A",actions=new[]{play,Wait(.1f,"STOP")},transitions=new[]{new NativeTransition{@event="STOP",state="B"}}},
            new NativeState{name="B",actions=new[]{stop,Wait(.1f,"REPLAY")},transitions=new[]{new NativeTransition{@event="REPLAY",state="C"}}},
            new NativeState{name="C",actions=new[]{replay},transitions=new NativeTransition[0]}}}};
        var root=new GameObject("Silent original stop and replay");
        using(var playback=new NativeAudioPlayback(assets.Root,root.transform,true))
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:playback.Play))
        {
            try
            {
                effect.Seek(.05f);var first=effect.AudioCues.Single();var voice=root.GetComponentInChildren<AudioSource>();
                Check(!first.sourceStopped()&&voice!=null,"initial play retains original owned source");
                effect.Seek(.15f);playback.Update();
                Check(effect.AudioSourceStops==1&&effect.UnboundAudioStopActions==0&&first.sourceStopped(),"stop state marks only current source play");
                Check(!voice.isPlaying&&playback.ActiveVoices==1,"SDK source stopped while original emitter remains reusable");
                effect.Seek(.25f);playback.Update();var second=effect.AudioCues.Last();
                Check(first.sourceStopped()&&!second.sourceStopped()&&playback.StartedVoices==2,"later play survives earlier stop");
                Check(root.GetComponentInChildren<AudioSource>()==voice,"stop does not destroy or duplicate the physical voice");
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        play.Param("m_Delay").number=.1f;
        program.machines[0].states=new[]{new NativeState{name="A",actions=new[]{play,stop},transitions=new NativeTransition[0]}};
        using(var effect=new NativeEffectInstance(program,assets))
        {
            effect.Seek(.2f);
            Check(effect.AudioSourceStops==1&&effect.AudioCues.Count==1&&!effect.AudioCues[0].sourceStopped(),"stop leaves pending component-owned Play coroutine alive");
        }
        stop.Param("m_GameObject").node=-1;
        using(var effect=new NativeEffectInstance(program,assets))
        {effect.Seek(.05f);Check(effect.AudioSourceStops==0&&effect.UnboundAudioStopActions==0,"null owner finishes without stopping other sounds");}
        stop.Param("m_GameObject").node=node;stop.Param("m_GameObject").unresolved=true;
        using(var effect=new NativeEffectInstance(program,assets))
        {effect.Seek(.05f);Check(effect.AudioSourceStops==0&&effect.UnboundAudioStopActions==1,"unknown source binding remains diagnosed");}
        Debug.Log("NATIVE AUDIO STOP: "+passed+" checks passed; source stop and delayed replay, full original SoundManager remains incomplete");
        NativeAudioVolumeChecks.Run();
    }
}
