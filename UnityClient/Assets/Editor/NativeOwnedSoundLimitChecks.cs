using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeOwnedSoundLimitChecks
{
    static int passed;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Original owned sound limits: "+message);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
        var sound=definition.nodes.Single(n=>n.path=="").sound_defs[0].values;
        sound.m_InstanceLimited=1;sound.m_InstanceLimitedDuration=.5f;sound.m_InstanceLimitMaximum=1;
        var cue=NativeAudio.Resolve(definition,"",0,1,()=>.5f);
        Check(cue!=null&&cue.options.limited&&cue.options.duration==.5f,"own SoundDef limit options survive resolver");
        var owner=new GameObject("Original limited receiver");var parent=new GameObject("Silent owned voice checks");
        cue.restartKey="owned-check";cue.sourceName="receiver";cue.trackedSource=owner;cue.keepSource=true;
        cue.options=new NativeSoundOptions{limited=true,duration=10,maximum=1};
        try
        {
            using(var playback=new NativeAudioPlayback(assets.Root,parent.transform,true))
            {
                playback.Play(cue);var voice=parent.GetComponentInChildren<AudioSource>();
                Check(voice!=null&&playback.ActiveVoices==1&&playback.StartedVoices==1&&playback.LimitedCount("receiver")==1,"first limited owned source plays and retains identity");
                voice.time=.2f;float before=voice.time;
                playback.Play(cue);
                Check(playback.SuppressedVoices==1&&playback.StartedVoices==1&&parent.GetComponentInChildren<AudioSource>()==voice,"quota rejection keeps owned source and does not restart it");
                Check(voice.time>=before-.01f,"quota rejection preserves playback position");
                cue.options.maxOut=1;playback.Play(cue);
                Check(parent.GetComponentInChildren<AudioSource>()==voice&&playback.StartedVoices==2,"replace-oldest policy never destroys an owned receiver");
                Check(playback.LimitedCount("receiver")==1,"replace-at-limit retains original count");
                cue.options.limited=false;playback.Play(cue);
                Check(playback.StartedVoices==3&&playback.ActiveVoices==1,"disabled action quota overrides SoundDef quota and reuses source");
                var generated=NativeAudio.Resolve(definition,"",0,1,()=>.5f);generated.sourceName="receiver";generated.options=new NativeSoundOptions();
                playback.Play(generated);Check(playback.ActiveVoices==2,"generated and owned sources can share the original quota name");
                cue.options.limited=true;cue.options.maxOut=1;playback.Play(cue);
                Check(playback.ActiveVoices==1&&parent.GetComponentInChildren<AudioSource>()==voice,"replace scans only generated sources when generated and owned names collide");
                cue.restartKey="second-owned-check";cue.options.maxOut=0;playback.Play(cue);
                Check(playback.SuppressedVoices==2&&playback.ActiveVoices==2,"first suppressed owned receiver is retained without stealing another receiver");
            }
        }
        finally{AssetRegistry.ReleaseTransient(owner);AssetRegistry.ReleaseTransient(parent);}
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        action.Param("m_InstanceLimited").number=1;action.Param("m_InstanceLimitedDuration").number=.25f;action.Param("m_InstanceLimitMaximum").number=2;
        using(var effect=new NativeEffectInstance(program,assets,width:32,height:32))
        {
            effect.Seek(.6f);cue=effect.AudioCues.Single();
            Check(effect.UnboundAudioActions==0&&cue.keepSource&&cue.options.limited&&cue.options.duration==.25f&&cue.options.maximum==2,"original own-source action passes its options instead of being rejected");
        }
        Debug.Log("NATIVE OWNED SOUND LIMITS: "+passed+" checks passed; full SoundManager/ducking and all-card verification remain incomplete");
        NativePlaythroughSoundChecks.Run();
    }
}
