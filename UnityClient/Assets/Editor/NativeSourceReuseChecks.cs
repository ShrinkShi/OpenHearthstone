using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSourceReuseChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original source reuse: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        action.Param("m_Delay").number=0;action.Param("m_FinishedEvent").text="";action.Param("m_VolumeScale").number=.4f;action.Param("m_VolumeScale").is_none=false;
        var second=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(action));second.Param("m_VolumeScale").is_none=true;
        var wait=new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f},new NativeParam{name="finishEvent",text="NEXT"}}};
        program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
            new NativeState{name="A",actions=new[]{action,wait},transitions=new[]{new NativeTransition{@event="NEXT",state="B"}}},
            new NativeState{name="B",actions=new[]{second},transitions=new NativeTransition[0]}}}};
        var playbackRoot=new GameObject("Silent original source reuse");
        using(var effect=new NativeEffectInstance(program,assets))
        using(var playback=new NativeAudioPlayback(assets.Root,playbackRoot.transform,true))
        {
            try
            {
                effect.Seek(.15f);var cues=effect.AudioCues.ToArray();
                Check(cues.Length==2&&cues[0].restartKey==cues[1].restartKey&&cues.All(c=>c.keepSource),"same original owner maps to one persistent SDK source");
                Check(Mathf.Abs(cues[0].volume-.3f)<1e-5f&&cues[0].volume==cues[1].volume,"None preserves previous source code volume rather than resetting to one");
                playback.Play(cues[0]);var voice=playbackRoot.GetComponentInChildren<AudioSource>();
                playback.Play(cues[1]);Check(playbackRoot.GetComponentsInChildren<AudioSource>().Single()==voice&&playback.ActiveVoices==1,"retrigger reuses physical source without stacking voices");
                voice.Stop();playback.Update();Check(playbackRoot.GetComponentInChildren<AudioSource>()==voice,"idle original source remains owned by live emitter");
                playback.Play(cues[1]);Check(playbackRoot.GetComponentInChildren<AudioSource>()==voice&&playback.StartedVoices==3,"idle source replays through the same SDK object");
                effect.Dispose();playback.Update();Check(playback.ActiveVoices==0,"emitter destruction removes retained idle or playing source");
            }
            finally{AssetRegistry.ReleaseTransient(playbackRoot);}
        }
        Debug.Log("NATIVE SOURCE REUSE: "+passed+" checks passed; original owned-source identity and code-volume persistence, complete mixer/voice policies remain incomplete");
        NativeSourceAudioChecks.Run();
    }
}
