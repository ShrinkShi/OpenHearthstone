using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class NativePlaythroughSoundChecks
{
    [Serializable] sealed class SourceRow{public string file;}
    [Serializable] sealed class SourceAudit{public SourceRow[] programs;}
    static int passed;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Original explicit playthrough: "+message);passed++;}
    public static void RunAllSourceAudio()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var report=JsonUtility.FromJson<SourceAudit>(File.ReadAllText("../manifests/native-playthrough-audio-refresh.json"));
        int starts=0,finishes=0,unbound=0,cues=0,played=0,programs=0;
        var unsupported=new List<string>();
        var parent=new GameObject("Silent original playthrough census");
        try
        {
            foreach(var row in report.programs)
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,row.file)));
                program.nodes=program.nodes.Select(n=>new NativeNode{name=n.name,path=n.path,parent=n.parent,position=n.position,rotation=n.rotation,scale=n.scale,active=n.active,materials=new NativeMaterial[0]}).ToArray();
                program.captures=new NativeCapture[0];program.timelines=new NativeTimeline[0];program.offscreen_fx=new NativeOffscreenFXData[0];
                program.machines=program.machines.Where(m=>m.states.Any(s=>s.actions.Any(a=>a.name=="AudioPlaythroughAction"))).Select(m=>new NativeMachine{owner=m.owner,owner_known=m.owner_known,floats=m.floats,start="Audio",events=new NativeTransition[0],states=new[]{new NativeState{name="Audio",actions=m.states.SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlaythroughAction").ToArray(),transitions=new NativeTransition[0]}}}).ToArray();
                using(var playback=new NativeAudioPlayback(assets.Root,parent.transform,true))
                using(var effect=new NativeEffectInstance(program,assets,width:16,height:16,audioCallback:playback.Play))
                {
                    effect.Seek(10);starts+=effect.PlaythroughStarts;finishes+=effect.PlaythroughFinishes;unbound+=effect.UnboundAudioActions;cues+=effect.AudioCues.Count;played+=playback.StartedVoices;programs++;
                    if(effect.UnboundAudioActions>0)unsupported.Add(row.file+":"+effect.UnboundAudioActions);
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(parent);}
        if(programs!=173||starts!=280||finishes!=280||unbound+cues!=280)throw new Exception("Incomplete original playthrough census: "+programs+"/"+starts+"/"+finishes+"/"+unbound+"/"+cues);
        File.WriteAllText("../work/native-playthrough-source-census.json","{\"programs\":"+programs+",\"starts\":"+starts+",\"finishes\":"+finishes+",\"cues\":"+cues+",\"unbound\":"+unbound+",\"started_voices\":"+played+",\"unsupported_programs\":["+String.Join(",",unsupported.Select(s=>"\""+s+"\""))+"],\"whole_effects_verified\":false,\"audible_playback_verified\":false}");
        Debug.Log("NATIVE PLAYTHROUGH SOURCE CENSUS: "+programs+" programs; "+cues+" cues; "+unbound+" explicit unsupported branches");
    }
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var original=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,original.audio_file)));
        var source=definition.nodes[0].audio_sources[0].values;
        var sound=new NativeSoundAsset{values=new NativeSoundValues{m_AudioClip="primary",m_RandomClips=new NativeSoundChoice[0],m_RandomVolumeMin=.5f,m_RandomVolumeMax=.5f,m_RandomPitchMin=2,m_RandomPitchMax=2,m_Category=3},clips=new[]{new NativeAudioClipRef{reference="primary",file="fixture.wav"}}};
        source.m_Volume=.25f;source.m_Pitch=.75f;source.Mute=true;source.Loop=true;int draws=0;
        var cue=NativeAudio.ResolvePlaythroughSound(definition,"",-1,sound,.2f,.8f,()=>{draws++;return .5f;});
        Check(cue!=null&&Mathf.Abs(cue.volume-.4f)<1e-5f,"explicit volume replaces template volume rather than scaling it");
        Check(draws==2&&cue.pitch==1.5f,"empty random list does not consume a selection draw; pitch retains template factor");
        Check(cue.copyTemplate&&cue.inheritSpatialCurve&&cue.template==source&&cue.loop&&!cue.mute&&cue.restartKey==null,"generated voice copies template fields, not mute or source identity");
        cue=NativeAudio.ResolvePlaythroughSound(definition,"",-1,sound,0,null,()=>.5f);
        Check(cue.volume==.125f&&cue.category==3,"None volume retains template volume and SoundDef category");
        sound.values.m_RandomClips=new[]{new NativeSoundChoice{m_Clip=null,m_Weight=1}};draws=0;
        Check(NativeAudio.ResolvePlaythroughSound(definition,"",-1,sound,0,null,()=>{draws++;return .5f;}).file=="fixture.wav"&&draws==3,"null weighted choice falls back to primary with no additional selection");
        sound.values.m_AudioClip="missing";
        Check(NativeAudio.ResolvePlaythroughSound(definition,"",-1,sound,0,null,()=>.5f)==null,"missing original clip cannot silently substitute another sound");
        // Exercise the FSM branch with a real exported SoundDef and AudioSource.
        string file="01eff56f6da3bdf3da744151.native-sound.json";
        var real=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,file)));
        var action=original.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        action.Param("m_OneShotSound").object_null=false;action.Param("m_OneShotSound").unresolved=false;action.Param("m_OneShotSound").asset_path=file;
        action.Param("m_Delay").number=0;action.Param("m_FinishedEvent").text="DONE";
        action.Param("m_VolumeScale").number=.8f;action.Param("m_VolumeScale").is_none=false;action.Param("m_VolumeScale").none_known=true;
        original.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="A",events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{action},transitions=new[]{new NativeTransition{@event="DONE",state="B"}}},new NativeState{name="B",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
        int calls=0;
        using(var effect=new NativeEffectInstance(original,assets,width:32,height:32,audioCallback:c=>calls++))
        {
            effect.Seek(.01f);
            Check(calls==1&&effect.AudioCues.Count==1&&effect.UnboundAudioActions==0,"explicit exported SoundDef produces a native cue");
            cue=effect.AudioCues[0];
            Check(real.clips.Any(c=>c.file==cue.file)&&File.Exists(Path.Combine(assets.Root,cue.file)),"cue resolves the exact SoundDef PCM cache");
            Check(effect.CurrentStates.Single()=="B"&&cue.copyTemplate&&cue.positionKnown&&cue.trackedSource==null&&!cue.stopWhenSourceInactive,"Play finishes immediately; generated voice snapshots source position and does not track owner activation");
            Check(cue.options!=null&&!cue.options.limited,"action options override SoundDef options even with limiting disabled");
            effect.Seek(0);effect.Seek(.01f);Check(calls==1,"rewind keeps physical callback deduplication");
        }
        action.Param("m_InstanceLimited").number=1;action.Param("m_InstanceLimitedDuration").number=.25f;action.Param("m_InstanceLimitMaximum").number=2;
        using(var effect=new NativeEffectInstance(original,assets,width:32,height:32))
        {
            effect.Seek(.01f);cue=effect.AudioCues.Single();
            Check(effect.UnboundAudioActions==0&&cue.options.limited&&cue.options.duration==.25f&&cue.options.maximum==2,"generated playthrough preserves action instance-limit settings");
        }
        Debug.Log("NATIVE EXPLICIT PLAYTHROUGH: "+passed+" checks passed; VO, full SoundManager and mixer routing remain incomplete");
        NativeMainShakeChecks.Run();
    }
}
