using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAudioVolumeChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original AudioSetVolume: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var play=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        int node=play.Param("m_GameObject").node;play.Param("m_Delay").number=0;play.Param("m_FinishedEvent").text="";
        var set=new NativeAction{name="AudioSetVolumeAction",@params=new[]{new NativeParam{name="m_GameObject",node=node},new NativeParam{name="m_Volume",variable="volume",none_known=true},new NativeParam{name="m_EveryFrame",number=1}}};
        var curve=new NativeAction{name="AnimateFloat",@params=new[]{new NativeParam{name="floatVariable",variable="volume"},new NativeParam{name="animCurve",curve=new[]{new NativeKey{time=0,value=1,inSlope=-1,outSlope=-1},new NativeKey{time=1,value=0,inSlope=-1,outSlope=-1}}}}};
        program.machines=new[]{new NativeMachine{start="A",floats=new[]{new NativeFloat{name="volume",value=1}},events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{play,curve,set},transitions=new NativeTransition[0]}}}};
        var root=new GameObject("Silent dynamic original volume");
        using(var playback=new NativeAudioPlayback(assets.Root,root.transform,true))
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:playback.Play))
        {
            try
            {
                effect.Seek(.25f);playback.Update();var voice=root.GetComponentInChildren<AudioSource>();
                Check(Mathf.Abs(voice.volume-.5625f)<1e-4f,"playing physical source uses original curve at quarter time");
                effect.Seek(.5f);playback.Update();Check(Mathf.Abs(voice.volume-.375f)<1e-4f,"same voice fades every frame rather than only changing future cues");
                Check(playback.StartedVoices==1&&effect.AudioVolumeWrites>30&&effect.UnboundAudioVolumeActions==0,"volume writes do not restart or allocate audio voices");
                set.Param("m_Volume").is_none=true;effect.Seek(.75f);playback.Update();Check(Mathf.Abs(voice.volume-.375f)<1e-4f,"None does not overwrite remembered source volume");
                set.Param("m_Volume").is_none=false;set.Param("m_GameObject").node=-1;
                effect.Seek(.8f);Check(effect.UnboundAudioVolumeActions==0,"null target returns before writing as original");
                set.Param("m_GameObject").node=node;set.Param("m_Volume").none_known=false;
                effect.Seek(.85f);Check(effect.UnboundAudioVolumeActions>0,"unresolved None control is diagnosed without guessing");
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        Debug.Log("NATIVE AUDIO VOLUME: "+passed+" checks passed; original live source volume/everyFrame semantics, complete mixer/scene remain incomplete");
        NativeSourceReuseChecks.Run();
    }
}
