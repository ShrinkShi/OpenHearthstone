using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeRogueHeroPowerProfile
{
    public const string Reference="Stealth_Untargeted_AutoSource_FX.prefab:5d1773f0c2f5ec743aba5d24e2d42557";
    public const string Reconstruction="source-root-only-reconstruction; enabled-null-chain-unresolved";
    public static bool Supports(NativeProgram root,NativeSuperSpellConfig config,string folder)
    {try{Validate(root,config,folder);return true;}catch{return false;}}
    static void Require(bool value,string reason)
    {if(!value)throw new NotSupportedException("HERO_03bp source reconstruction: "+reason);}
    public static void Validate(NativeProgram root,NativeSuperSpellConfig config,string folder)
    {
        Require(root!=null&&config!=null&&root.reference==Reference&&config.reference==Reference&&
            config.card_id=="HERO_03bp"&&config.source_root_profile==Reconstruction&&root.nodes!=null&&root.nodes.Length==3,
            "source identity or reconstruction marker changed");
        Require(config.location==2&&config.target_behavior==6&&!config.start_enabled&&config.start_config_null&&
            config.missile!=null&&config.missile.m_Enabled==0&&String.IsNullOrEmpty(config.missile_reference)&&!config.reverse_available,
            "SourceAuto/no-Missile profile changed");
        var impact=config.impact;
        Require(impact!=null&&impact.enabled&&impact.prefab!=null&&!impact.prefab.prefab_null&&impact.prefab.prefab_node==1&&
            impact.use_super_location&&!impact.parent_to_location&&!impact.adjust_rotation&&impact.ranges!=null&&impact.ranges.Length==0&&
            impact.location==6&&impact.delay_min==0&&impact.delay_max==0&&impact.spawn_offset==0&&
            impact.game_delay_min==0&&impact.game_delay_max==0,"embedded Impact changed");
        Require(config.area!=null&&config.area.friendly!=null&&config.area.opponent!=null,"area facts missing");
        foreach(var area in new[]{config.area.friendly,config.area.opponent})
            Require(area.enabled&&area.prefab_null&&String.IsNullOrEmpty(area.prefab_reference),"area must remain enabled-null");
        Require(config.chain_enabled&&config.chain_prefab_null&&!config.chain_available&&String.IsNullOrEmpty(config.chain_reference),
            "Chain must remain enabled-null; its original runtime semantics are unresolved");
        Require(root.unknown_actions!=null&&root.unknown_actions.All(name=>name=="AudioPlayRandomClipAction")&&
            root.machines!=null&&root.machines.Length==1,"unknown action or machine inventory");
        Require(root.nodes[1].parent==-1&&root.nodes[2].parent==1&&root.nodes[2].particle!=null&&root.nodes[2].particle.restored,
            "embedded particle hierarchy changed");
        var machine=root.machines[0];
        Require(machine.owner_known&&machine.owner==1&&machine.start=="State 1"&&machine.states!=null&&machine.states.Length==3&&
            machine.events!=null&&machine.events.Select(e=>e.@event+":"+e.state).SequenceEqual(new[]{"Death:Birth State","None:NoneState"}),
            "Death entry changed");
        var birth=machine.states.Single(s=>s.name=="Birth State");
        var none=machine.states.Single(s=>s.name=="NoneState");var idle=machine.states.Single(s=>s.name=="State 1");
        Require(birth.actions.Select(a=>a.name).SequenceEqual(new[]{"SpellStateAction","ParticlePlayAction","AudioPlayRandomClipAction","SpellFinishAction","Wait"})&&
            birth.transitions.Select(t=>t.@event+":"+t.state).SequenceEqual(new[]{"FINISHED:NoneState"})&&
            idle.actions.Length==0&&idle.transitions.Length==0&&none.actions.Length==1&&none.actions[0].name=="SpellStateAction"&&none.transitions.Length==0,
            "action graph changed");
        Require(machine.states.SelectMany(s=>s.actions).SelectMany(a=>a.@params).All(p=>!p.unresolved&&String.IsNullOrEmpty(p.variable)),
            "unresolved parameter");
        var actions=birth.actions;
        Require(actions[0].Param("m_SpellObject").node==1&&none.actions[0].Param("m_SpellObject").node==1&&
            actions[1].Param("m_GameObject").node==2&&actions[1].Param("m_IncludeChildren").number==0&&
            actions[3].Param("m_SpellObject").node==1&&actions[3].Param("m_Delay").number==0&&
            actions[4].Param("time").number==1&&actions[4].Param("realTime").number==0&&actions[4].Param("finishEvent").text=="FINISHED",
            "particle or lifecycle parameter changed");
        var audio=actions[2];var parameters=audio.@params;
        int sounds=Array.FindIndex(parameters,p=>p.name=="m_Sounds"),weights=Array.FindIndex(parameters,p=>p.name=="m_Weights");
        Require(audio.Param("m_ParentObject").node==1&&audio.Param("m_Sounds").array_known&&audio.Param("m_Sounds").array_count==1&&
            audio.Param("m_Weights").array_known&&audio.Param("m_Weights").array_count==1&&sounds>=0&&weights>=0&&
            parameters[sounds+1].asset_path=="d7861ddcda8550b8d2114840.native-sound.json"&&parameters[weights+1].number==1&&
            audio.Param("m_TemplateSource").object_null,"single-candidate SoundDef changed");
        foreach(var name in new[]{"m_MinVolume","m_MaxVolume","m_MinPitch","m_MaxPitch","m_SpatialBlend","m_Category","m_InstanceLimitMaximum"})
            Require(audio.Param(name).number==1,"audio constant changed: "+name);
        foreach(var name in new[]{"m_Delay","m_InstanceLimited","m_InstanceLimitedDuration","m_LimitMaxOutOption"})
            Require(audio.Param(name).number==0,"audio constant changed: "+name);
        Require(!String.IsNullOrEmpty(root.audio_file)&&File.Exists(Path.Combine(folder,root.audio_file)),"root audio binding missing");
        var sound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(folder,parameters[sounds+1].asset_path)));
        Require(sound.values!=null&&sound.values.m_AudioClip=="Sneaky_Target_Start_01.wav:833fe1c0f3d98e546b204037df07011f"&&
            sound.values.m_RandomClips!=null&&sound.values.m_RandomClips.Length==0&&sound.clips!=null&&sound.clips.Length==1&&
            sound.clips[0].reference==sound.values.m_AudioClip&&File.Exists(Path.Combine(folder,sound.clips[0].file)),"source clip export missing or changed");
    }
}
