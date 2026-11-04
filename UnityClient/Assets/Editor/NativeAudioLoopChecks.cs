using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAudioLoopChecks
{
    [Serializable] sealed class Entry{public string file;}
    [Serializable] sealed class Report{public Entry[] programs;}
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original audio loop: "+reason);passed++;}
    static void Near(float value,float expected,string reason){Check(Mathf.Abs(value-expected)<1e-4f,reason);}
    static NativeAction Start(int node,float time=.2f,bool retain=false)
    {return new NativeAction{name="AudioLoopingStartAction",@params=new[]{new NativeParam{name="m_GameObject",node=node},
        new NativeParam{name="m_FadeInTime",number=time,none_known=true},new NativeParam{name="m_TargetVolume",number=.6f,none_known=true},
        new NativeParam{name="m_randomizeStartPoint",number=0,none_known=true},new NativeParam{name="m_dontDestroySource",number=retain?1:0,none_known=true}}};}
    static NativeAction End(int node,float time=.2f,bool stop=true)
    {return new NativeAction{name="AudioLoopingEndAction",@params=new[]{new NativeParam{name="m_GameObject",node=node},
        new NativeParam{name="m_FadeOutTime",number=time,none_known=true},new NativeParam{name="m_stop",number=stop?1:0,none_known=true}}};}
    public static void Run()
    {
        passed=0;var start=Start(0,0);var fade=new NativeAudioFade(start,.8f,p=>p.number,()=>10);
        Check(!fade.Finished&&!fade.InitialWrite,"zero fade-in still plays and waits for first update");
        fade.Step(.01f);Check(fade.Finished&&!fade.StopWhenFinished,"zero fade-in finishes on update without stop");Near(fade.Value,.6f,"zero fade-in target exact");
        var end=End(0,0);fade=new NativeAudioFade(end,.8f,p=>p.number,()=>10);
        Check(fade.Finished&&!fade.InitialWrite&&!fade.StopWhenFinished,"zero fade-out finishes without muting or stopping");
        end=End(0,1);fade=new NativeAudioFade(end,.8f,p=>p.number,()=>10);fade.Step(.25f);Near(fade.Value,.6f,"fade-out starts at remembered gain");
        end.Param("m_stop").number=0;fade.Step(.75f);Check(fade.Finished&&!fade.StopWhenFinished,"live stop=false leaves loop running at zero gain");

        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        int node=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction").Param("m_GameObject").node;
        start=Start(node);end=End(node);
        program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
            new NativeState{name="A",actions=new[]{start},transitions=new[]{new NativeTransition{@event="FINISHED",state="B"}}},
            new NativeState{name="B",actions=new[]{end},transitions=new[]{new NativeTransition{@event="FINISHED",state="C"}}},
            new NativeState{name="C",actions=new[]{start},transitions=new NativeTransition[0]}}}};
        var root=new GameObject("Silent original looping source");
        using(var playback=new NativeAudioPlayback(assets.Root,root.transform,true))
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:playback.Play,audioRealtime:()=>10))
        {
            try
            {
                effect.Seek(0);var voice=root.GetComponentInChildren<AudioSource>();
                Check(voice.loop&&playback.StartedVoices==1,"start plays original source and enables loop");Near(voice.volume,.75f,"OnEnter does not reset playing volume to zero");
                effect.Seek(.1f);playback.Update();Near(voice.volume,.225f,"actual first source fades from internal zero on subsequent frames");
                Check(effect.CurrentStates.Single()=="A","FINISHED waits for fade-in");
                effect.Seek(.45f);playback.Update();
                Check(effect.AudioFadeFinishes==2&&effect.LoopingAudioStarts==1&&effect.UnboundAudioFadeActions==0,"both original fade states complete and removed owner cannot restart");
                Check(effect.GeneratedAudioOwnerRemovals==1&&!effect.Nodes[node].activeSelf&&effect.AudioCues.Single().sourceDestroyed(),"PlayClip(false) source subtree and references become unavailable on stop");
                Check(playback.ActiveVoices==0,"generated SDK voice is released rather than kept idle");
                effect.Seek(.1f);playback.Update();Check(effect.LoopingAudioStarts==1&&effect.GeneratedAudioOwnerRemovals==0&&playback.StartedVoices==2,"rewind restores pooled original owner and source identity");
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        start=Start(node,retain:true);program.machines[0].states[0].actions=new[]{start};program.machines[0].states[2].actions=new NativeAction[0];
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {effect.Seek(.45f);Check(effect.GeneratedAudioOwnerRemovals==0&&effect.Nodes[node].activeSelf&&!effect.AudioCues.Single().sourceDestroyed(),"dontDestroySource branch keeps original emitter available");}
        start=Start(node);end=End(node,stop:false);program.machines[0].states[0].actions=new[]{start};program.machines[0].states[1].actions=new[]{end};
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {effect.Seek(.45f);Check(effect.AudioSourceStops==0&&!effect.AudioCues.Single().sourceStopped()&&effect.AudioCues.Single().currentCodeVolume()==0,"stop=false preserves loop while final gain is zero");}
        var delayed=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json"))).machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        delayed.Param("m_Delay").number=0;delayed.Param("m_FinishedEvent").text="NO_SOURCE";
        program.machines[0].states[1].actions=new[]{End(node)};program.machines[0].states[2].actions=new[]{delayed};
        program.machines[0].states[2].transitions=new[]{new NativeTransition{@event="NO_SOURCE",state="D"}};
        program.machines[0].states=program.machines[0].states.Concat(new[]{new NativeState{name="D",actions=new NativeAction[0],transitions=new NativeTransition[0]}}).ToArray();
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {effect.Seek(.5f);Check(effect.CurrentStates.Single()=="D"&&effect.AudioCues.Count==1&&effect.UnboundAudioActions==0,"playthrough after original emitter removal sends finished event without replaying or diagnosing a valid null source");}
        var randomStart=Start(node,retain:true);randomStart.Param("m_randomizeStartPoint").number=1;
        program.machines[0].states=new[]{new NativeState{name="A",actions=new[]{randomStart},transitions=new NativeTransition[0]}};
        root=new GameObject("Silent original random loop offset");
        using(var playback=new NativeAudioPlayback(assets.Root,root.transform,true))
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:playback.Play,audioRealtime:()=>10))
        {
            try
            {
                effect.Seek(0);var cue=effect.AudioCues.Single();var voice=root.GetComponentInChildren<AudioSource>();
                Check(cue.randomizeStart&&cue.randomStart>0&&cue.randomStart<1,"random-start option uses a separate original playback offset sample");
                Check(Mathf.Abs(voice.time-cue.randomStart*voice.clip.length)<.05f,"random playback offset reaches actual SDK voice");
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        var raw=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"223522c32fa85768f21c4c95.native-effect.json")));
        var rawStart=raw.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioLoopingStartAction");
        var rawAudio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,raw.audio_file)));
        int draws=0;var primary=NativeAudio.Resolve(rawAudio,raw.nodes[rawStart.Param("m_GameObject").node].path,0,1,()=>{draws++;return .5f;},rawStart.Param("m_GameObject").node);
        Check(primary!=null&&primary.file=="e3977c2965afda8c49f73f9d.wav"&&draws==2,"exact primary clip fallback consumes only volume and pitch random draws");
        raw.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{rawStart},transitions=new NativeTransition[0]}}}};
        using(var effect=new NativeEffectInstance(raw,assets,width:32,height:32,audioRealtime:()=>10))
        {effect.Seek(.1f);Check(effect.LoopingAudioStarts==1&&effect.UnboundAudioFadeActions==0&&effect.AudioCues.Single().loop,"actual restored original loop prefab emits original clip");}
        var report=JsonUtility.FromJson<Report>(File.ReadAllText("../manifests/native-audio-loop-refresh.json"));
        foreach(var entry in report.programs)
        {
            var original=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var originalStart=original.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioLoopingStartAction");
            original.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{originalStart},transitions=new NativeTransition[0]}}}};
            using(var effect=new NativeEffectInstance(original,assets,width:32,height:32,audioRealtime:()=>10))
            {effect.Seek(.1f);Check(effect.LoopingAudioStarts==1&&effect.UnboundAudioFadeActions==0&&effect.AudioCues.Single().loop,"original program source/clip must execute: "+entry.file);}
        }
        Debug.Log("NATIVE AUDIO LOOP: "+passed+" checks passed; source looping/state timing, full SoundManager and original destruction callbacks remain incomplete");
        NativeAudioFadeChecks.Run();
    }
}
