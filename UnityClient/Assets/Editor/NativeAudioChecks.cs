using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAudioChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original audio: "+message);passed++;}
    public static void Run(AssetRegistry assets)
    {
        passed=0;
        var weights=new[]{new NativeSoundChoice{m_Clip="zero",m_Weight=0},new NativeSoundChoice{m_Clip="first",m_Weight=1},new NativeSoundChoice{m_Clip="last",m_Weight=3}};
        Check(NativeAudio.Select(weights,0)=="zero","source inclusive zero-weight boundary");
        Check(NativeAudio.Select(weights,.25f)=="first","source inclusive cumulative boundary");
        Check(NativeAudio.Select(weights,.25001f)=="last","next weighted interval");
        Check(NativeAudio.Select(weights,1)=="last","last element fallback");
        Check(NativeAudio.Select(new NativeSoundChoice[0],.5f)==null,"empty list has no invented sound");
        var path=Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json");
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path));
        var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
        var cue=NativeAudio.Resolve(definition,"",.5f,1,()=>.5f);
        Check(cue!=null&&cue.file=="bc42c1af01f7c7c5aef6f81d.wav","exact original source SoundDef clip");
        Check(Mathf.Abs(cue.volume-.75f)<.00001f&&Mathf.Abs(cue.pitch-1)<.00001f,"source, code and randomized volume/pitch products");
        Check(File.Exists(Path.Combine(assets.Root,cue.file)),"source PCM cache available");
        definition.nodes[0].audio_sources[0].values.m_audioClip.m_PathID=42;
        Check(NativeAudio.Resolve(definition,"",0,1,()=>.5f)==null,"unrestored direct clip branch cannot silently use a random fallback");
        definition.nodes[0].audio_sources[0].values.m_audioClip.m_PathID=0;
        definition.nodes[0].sound_defs[0].values.m_AudioClip="unrestored-primary";
        Check(NativeAudio.Resolve(definition,"",0,1,()=>.5f)!=null,"original random list takes precedence over primary reference");
        var choices=definition.nodes[0].sound_defs[0].values.m_RandomClips;definition.nodes[0].sound_defs[0].values.m_RandomClips=new NativeSoundChoice[0];
        Check(NativeAudio.Resolve(definition,"",0,1,()=>.5f)==null,"missing primary cache is not replaced with an invented clip");
        definition.nodes[0].sound_defs[0].values.m_RandomClips=choices;
        definition.nodes[0].sound_defs[0].values.m_AudioClip="";
        var audioParent=new GameObject("Silent native PCM verification");
        var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true);
        try
        {
            var pcm=playback.Load(cue.file);Check(pcm.frequency==48000&&pcm.channels==1&&pcm.samples==57536,"exact source PCM shape reaches Unity AudioClip");
            Check(playback.Load(cue.file)==pcm&&playback.CachedClips==1,"PCM is decoded once and reused");
            var samples=new float[pcm.samples];Check(pcm.GetData(samples,0)&&samples.Any(s=>Mathf.Abs(s)>.01f),"source PCM is not silent or empty");
            playback.Play(cue);var voice=audioParent.GetComponentInChildren<AudioSource>();
            Check(voice!=null&&voice.clip==pcm&&voice.mute&&voice.volume==cue.volume&&voice.pitch==cue.pitch,"muted verification configures actual independent playback voice");
            playback.StopAll();Check(playback.ActiveVoices==0&&playback.CachedClips==1,"battle exit stops voices while retaining decoded cache");
        }
        finally{playback.Dispose();AssetRegistry.ReleaseTransient(audioParent);}
        Check(playback.ActiveVoices==0&&playback.CachedClips==0,"shutdown releases voices and PCM cache");
        var calls=0;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>calls++))
        {
            instance.Seek(.49f);Check(calls==0,"source wait does not play landing sound early");
            instance.Seek(.6f);Check(calls==1&&instance.AudioCues.Count==1&&instance.UnboundAudioActions==0,"source wait invokes one physical callback");
            Check(Mathf.Abs(instance.AudioCues[0].time-.5f)<.001f,"landing cue retains original half-second action time");
            float pitch=instance.AudioCues[0].pitch;Check(pitch>=.9f&&pitch<=1.1f,"original randomized pitch limits");
            instance.Seek(.1f);instance.Seek(.6f);Check(calls==1&&instance.AudioCues.Count==1&&instance.AudioCues[0].pitch==pitch,"rewind reconstructs decision without duplicate playback");
            instance.Seek(2);Check(calls==1,"large seek does not repeat sound");
        }
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        var volume=action.Param("m_VolumeScale");volume.number=.5f;
        cue=NativeAudio.Resolve(definition,"",0,volume.number,()=>.5f);
        Check(Mathf.Abs(cue.volume-.375f)<.00001f,"code volume scales source volume rather than replacing it");
        action.Param("m_IsMinionSummonVO").number=1;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>calls++))
        {instance.Seek(.6f);Check(instance.UnboundAudioActions==1&&instance.AudioCues.Count==0&&calls==1,"unsupported VO is diagnosed without guessed fallback");}
        action.Param("m_IsMinionSummonVO").number=0;
        var disposed=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>calls++);disposed.Seek(.49f);disposed.Dispose();disposed.Dispose();
        bool rejected=false;try{disposed.Seek(.6f);}catch(ObjectDisposedException){rejected=true;}
        Check(rejected&&calls==1,"release cancels pending playback and rejects later seeking");
        action.Param("m_Delay").number=.2f;action.Param("m_FinishedEvent").text="SOUND_DONE";
        program.machines=new[]{new NativeMachine{start="sound",states=new[]{
            new NativeState{name="sound",actions=new[]{action},transitions=new[]{new NativeTransition{@event="SOUND_DONE",state="done"}}},
            new NativeState{name="done",actions=new NativeAction[0],transitions=new NativeTransition[0]}},events=new NativeTransition[0]}};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>calls++))
        {
            instance.Seek(.19f);Check(calls==1&&instance.CurrentStates[0]=="sound","delayed action waits on presentation clock");
            instance.Seek(.21f);Check(calls==2&&instance.CurrentStates[0]=="done","finished event is sent at playback start, without waiting for WAV duration");
            Check(instance.AudioCues[0].time>.2f&&instance.AudioCues[0].time<=.21f,"original delay plays on the iteration after subtracting remaining time to zero");
        }
        action.Param("m_Delay").number=0;action.Param("m_FinishedEvent").text="";
        program.machines[0].states[0].transitions=new[]{new NativeTransition{@event="FINISHED",state="done"}};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>calls++))
        {instance.Seek(1);Check(calls==3&&instance.AudioCues.Count==1,"instant audio action plays before its immediate FINISHED transition");}
        File.WriteAllText("../work/native-audio-checks.json","{\"passed\":"+passed+",\"complete\":false,\"audible_playback_verified\":false}");
        Debug.Log("NATIVE AUDIO CHECKS PASSED: "+passed);
    }
}
