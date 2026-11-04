using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAudioFadeChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original audio fade: "+reason);passed++;}
    static void Near(float value,float expected,string reason){Check(Mathf.Abs(value-expected)<1e-4f,reason);}
    static NativeAction Fade(int node,float duration=1)
    {return new NativeAction{name="AudioVolumeFadeAction",@params=new[]{new NativeParam{name="m_GameObject",node=node},
        new NativeParam{name="m_FadeTime",number=duration,none_known=true},new NativeParam{name="m_TargetVolume",number=0,none_known=true},
        new NativeParam{name="m_StopWhenDone",number=1,none_known=true},new NativeParam{name="m_RealTime",number=0,none_known=true}}};}
    public static void Run()
    {
        passed=0;float now=10;var action=Fade(0);var fade=new NativeAudioFade(action,1,p=>p.number,()=>now);
        Check(fade.InitialWrite&&!fade.Finished,"enter writes remembered code volume and waits");
        fade.Step(.25f);Near(fade.Value,.75f,"scaled delta at nonzero absolute start time");
        action.Param("m_TargetVolume").number=.5f;fade.Step(.25f);Near(fade.Value,.75f,"target reread during fade");
        action.Param("m_FadeTime").number=2;fade.Step(.25f);Near(fade.Value,.8125f,"duration denominator reread");
        fade.Step(.25f);Check(fade.Finished&&fade.StopWhenFinished,"captured end time does not grow with changed duration");Near(fade.Value,.5f,"final target exact");
        var zero=new NativeAudioFade(Fade(0,0),.4f,p=>p.number,()=>now);
        Check(zero.Finished&&!zero.InitialWrite&&!zero.StopWhenFinished,"zero fade finishes without setting volume or stopping source");
        action=Fade(0);action.Param("m_StopWhenDone").number=0;fade=new NativeAudioFade(action,.4f,p=>p.number,()=>now);fade.Step(1);
        Check(fade.Finished&&!fade.StopWhenFinished,"completion obeys live stop flag");
        action=Fade(0);action.Param("m_RealTime").number=1;fade=new NativeAudioFade(action,1,p=>p.number,()=>now);
        now=10.5f;fade.Step(0);Near(fade.Value,1,"original realtime relative/absolute comparison is preserved");
        now=21;fade.Step(0);Check(fade.Finished,"original realtime completion boundary");
        var invalid=Fade(0);invalid.Param("m_FadeTime").none_known=false;bool rejected=false;
        try{new NativeAudioFade(invalid,1,p=>p.number,()=>now);}catch(ArgumentException){rejected=true;}
        Check(rejected,"unknown typed controls are diagnosed");
        var indexed=new NativeAudioDefinition{nodes=new[]{new NativeAudioNode{path="same",node=4,node_known=true},new NativeAudioNode{path="same",node=8,node_known=true}}};
        Check(NativeAudio.Node(indexed,"same",8)==indexed.nodes[1],"detached prefab roots with duplicate paths bind by original node index");
        Check(NativeAudio.Node(indexed,"same",9)==null,"unknown indexed source does not fall back to another root");
        Check(NativeAudio.Node(new NativeAudioDefinition{nodes=new[]{new NativeAudioNode{path="legacy"}}},"legacy",5)!=null,"legacy single-tree source caches remain compatible");

        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var play=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        int node=play.Param("m_GameObject").node;play.Param("m_Delay").number=0;play.Param("m_FinishedEvent").text="";
        action=Fade(node,.2f);program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
            new NativeState{name="A",actions=new[]{play,action},transitions=new[]{new NativeTransition{@event="FINISHED",state="B"}}},
            new NativeState{name="B",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
        var root=new GameObject("Silent original audio fade");
        using(var playback=new NativeAudioPlayback(assets.Root,root.transform,true))
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:playback.Play,audioRealtime:()=>10))
        {
            try
            {
                effect.Seek(.1f);playback.Update();var voice=root.GetComponentInChildren<AudioSource>();
                Near(voice.volume,.375f,"actual voice fades existing source gain with original volume base");
                Check(effect.CurrentStates.Single()=="A"&&effect.AudioFadeStarts==1&&effect.AudioFadeFinishes==0,"FINISHED waits for fade");
                effect.Seek(.24f);playback.Update();
                Check(effect.CurrentStates.Single()=="B"&&effect.AudioFadeFinishes==1&&effect.AudioSourceStops==1,"completion releases FSM and stops only owned source");
                Near(voice.volume,0,"actual final source volume");Check(playback.StartedVoices==1&&!voice.isPlaying,"fade does not restart voice");
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
        var identity=definition.nodes.Single(n=>n.path==program.nodes[node].path).audio_sources.Single().source;
        action=Fade(-1,.2f);action.@params=action.@params.Concat(new[]{new NativeParam{name="m_AudioSource",node=node,asset_type="AudioSource",asset_source=identity}}).ToArray();
        program.machines[0].states[0].actions=new[]{play,action};
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {effect.Seek(.1f);Check(effect.AudioFadeStarts==1&&effect.UnboundAudioFadeActions==0,"explicit original AudioSource overrides null GameObject");}
        action.Param("m_AudioSource").asset_source=new NativeAssetSource{cab=identity.cab,pathid="wrong",type="AudioSource"};
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {effect.Seek(.1f);Check(effect.AudioFadeStarts==0&&effect.UnboundAudioFadeActions==1,"wrong component identity is not replaced with another source");}

        action=Fade(node,1);var wait=new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f},new NativeParam{name="finishEvent",text="NEXT"}}};
        program.machines[0].states[0].actions=new[]{play,action,wait};program.machines[0].states[0].transitions=new[]{new NativeTransition{@event="NEXT",state="B"}};
        using(var effect=new NativeEffectInstance(program,assets,audioRealtime:()=>10))
        {
            effect.Seek(.2f);float volume=effect.AudioCues.Single().currentCodeVolume();effect.Seek(.4f);
            Near(effect.AudioCues.Single().currentCodeVolume(),volume,"state exit stops updates without restoring source volume");
            Check(effect.AudioFadeFinishes==0&&effect.AudioSourceStops==0,"early state exit does not execute final stop");
        }
        var raw=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"0bec2a4e780bcda3d1fe6fe5.native-effect.json")));
        var originalFade=raw.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="AudioVolumeFadeAction"&&!a.Param("m_AudioSource").object_null);
        raw.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{originalFade},transitions=new NativeTransition[0]}}}};
        using(var effect=new NativeEffectInstance(raw,assets,width:32,height:32,audioRealtime:()=>10))
        {
            effect.Seek(.01f);
            Check(effect.AudioFadeStarts==1&&effect.UnboundAudioFadeActions==0,"actual detached original prefab emitter is bound, not only a synthetic source");
        }
        Debug.Log("NATIVE AUDIO FADE: "+passed+" checks passed; original timing/source/finish semantics, full SoundManager and all-card fidelity remain incomplete");
        NativeAudioStopChecks.Run();
    }
}
