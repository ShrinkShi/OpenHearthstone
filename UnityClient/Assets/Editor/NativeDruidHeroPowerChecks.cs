using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeDruidHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Druid hero power source root: "+reason);passed++;}

    public static void RunSourceRoot()
    {
        passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_06bp");
        Check(!entry.enabled,"unsupported no-missile runtime entry stays disabled");

        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_06bp");
        Check(config.target_behavior==6&&config.missile!=null&&config.missile.m_Enabled==0&&
            String.IsNullOrEmpty(config.missile_reference),"source config retains target behavior 6 and disabled missile");
        Check(config.impact!=null&&config.impact.enabled&&config.impact.prefab!=null&&
            !config.impact.prefab.prefab_null,"source impact prefab remains independently bound");
        Check(NativeSuperSpell.SupportsNoMissileUntargeted(config),
            string.Format("reviewed no-missile behavior-6 config passes the constrained dispatcher gate; target={0} missile={1}/{2} start={3} configNull={4} chain={5} reverse={6} impact={7} prefabNull={8} ranges={9}",
                config.target_behavior,config.missile==null?-1:config.missile.m_Enabled,config.missile_reference,
                config.start_enabled,config.start_config_null,config.chain_available,config.reverse_available,
                NativeSuperImpact.HasImpact(config.impact),config.impact==null||config.impact.prefab==null?"null":config.impact.prefab.prefab_null.ToString(),
                config.impact==null||config.impact.ranges==null?-1:config.impact.ranges.Length));

        var sourceObject=new GameObject("Druid target behavior source");
        var otherObject=new GameObject("Druid target behavior rejected target");
        try
        {
            var sourceRecord=new NativeSpellEntityObject{entity=91,resolve=()=>sourceObject};
            var otherRecord=new NativeSpellEntityObject{entity=92,resolve=()=>otherObject};
            var placement=new NativeSpellObjectSelection{source=sourceRecord};placement.SetTargets(new[]{otherRecord});
            var boundIds=new int[0];
            var scene=new NativeSuperSpellScene{Placement=placement,SetTargets=ids=>
            {
                boundIds=ids;
                placement.SetTargets(ids.Select(id=>id==sourceRecord.entity?sourceRecord:id==otherRecord.entity?otherRecord:null));
            }};
            NativeSuperSpellPlayback.BindVisualTargets(config,scene);
            var visual=placement.Targets.GetTargets().ToArray();
            Check(boundIds.SequenceEqual(new[]{sourceRecord.entity})&&visual.Length==1&&visual[0]==sourceObject,
                "official target behavior 6 binds source as sole visual target, dropping unrelated entities");

            var root=new GameObject("Druid no-missile dispatcher host");
            try
            {
                var dispatchConfig=JsonUtility.FromJson<NativeSuperSpellConfig>(JsonUtility.ToJson(config));
                dispatchConfig.impact.delay_min=dispatchConfig.impact.delay_max=1f;
                int creates=0;var phases=new List<string>();
                using(var flow=new NativeSuperSpell(dispatchConfig,new NativeSuperSpellServices{Root=root,Source=()=>sourceObject,
                    Targets=new List<GameObject>{sourceObject},Create=_=>{creates++;throw new Exception("dispatcher fixture must stop before prefab creation");},
                    Release=_=>{},Trace=(phase,index,time)=>phases.Add(phase)}))
                {
                    flow.Begin();
                    Check(!flow.Faulted&&flow.Pending==1&&creates==0&&phases.Contains("Action")&&phases.Contains("StartAction"),
                        "source-only H06 dispatch schedules impact before its fixture-only delay without a missile");
                }
            }
            finally{AssetRegistry.ReleaseTransient(root);}
        }
        finally{AssetRegistry.ReleaseTransient(sourceObject);AssetRegistry.ReleaseTransient(otherObject);}

        var dispatchCalls=new System.Collections.Generic.List<string>();
        var dispatch=new NativeSuperAction(new NativeSuperActionServices{DetermineArea=()=>null,SpawnArea=_=>{},
            HasMissile=()=>false,HasImpact=()=>true,HasChain=()=>false,MissilesInSequence=()=>false,
            VisualTargetCount=()=>1,CurrentTargetIndex=()=>0,SpawnAllImpacts=()=>dispatchCalls.Add("impact"),
            SpawnImpact=_=>dispatchCalls.Add("impact-indexed"),DoStartSpellAction=()=>dispatchCalls.Add("start"),
            PendingEffects=()=>0,HasStateContent=_=>false,BaseSpellFinished=()=>{},BaseStateFinished=()=>{},CleanUp=()=>{}});
        dispatch.DoActionNow();
        Check(dispatchCalls.SequenceEqual(new[]{"impact","start"}),
            "no-missile impact dispatch preserves SpawnAllImpacts before DoStartSpellAction");

        int previousBehavior=config.target_behavior;string previousReference=config.missile_reference;
        var previousStart=config.start;
        config.target_behavior=0;
        Check(!NativeSuperSpell.SupportsNoMissileUntargeted(config),"unsupported target behavior cannot use no-missile branch");
        config.target_behavior=previousBehavior;config.start_config_null=false;config.start=new NativeSuperStartConfig();
        Check(!NativeSuperSpell.SupportsNoMissileUntargeted(config),"a disabled but present Start config is outside the reviewed branch");
        config.start=previousStart;config.start_config_null=true;config.missile_reference="unexpected.prefab";
        Check(!NativeSuperSpell.SupportsNoMissileUntargeted(config),"disabled missile cannot retain a prefab reference");
        config.missile_reference=previousReference;

        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        program.audio_file=entry.file.Replace(".hero-power-source.json",".hero-power-audio.json");
        var sourceAudio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
        var audioSource=NativeAudio.Node(sourceAudio,string.Empty,1);
        Check(audioSource!=null&&audioSource.audio_sources!=null&&audioSource.audio_sources.Length==1&&audioSource.audio_sources[0].values!=null,
            "source AudioSource object on node 1 parses from the matching root audio definition");
        Check(NativeAudio.SupportsSpellCardIdAudioSource(audioSource.audio_sources[0].values)&&audioSource.sound_defs!=null&&
            audioSource.sound_defs.Length==1&&audioSource.sound_defs[0].values!=null&&audioSource.sound_defs[0].values.m_Category==1,
            "native AudioSource curves and its attached SoundDef category parse for the PlayOneShot source");
        var unsupportedSource=JsonUtility.FromJson<NativeAudioSourceValues>(JsonUtility.ToJson(audioSource.audio_sources[0].values));
        unsupportedSource.OutputAudioMixerGroup.m_PathID=1;
        Check(!NativeAudio.SupportsSpellCardIdAudioSource(unsupportedSource),
            "an AudioSource routed through an unmapped mixer group remains unsupported");
        int selectedIndex;bool useDefault;
        Check(NativeAudio.TrySelectCardIdSound(0,"ONY_008",null,new[]{"ONY_008"},1,out selectedIndex,out useDefault)&&selectedIndex==0&&!useDefault,
            "SOURCE card-id selector maps matching m_CardIds entry to the same m_Sounds index");
        Check(NativeAudio.TrySelectCardIdSound(0,"HERO_06bp",null,new[]{"ONY_008"},1,out selectedIndex,out useDefault)&&selectedIndex==-1&&useDefault,
            "Druid SOURCE HERO_06bp mismatch selects m_DefaultSound instead of ONY_008 SoundDef");
        Check(NativeAudio.TrySelectCardIdSound(1,"HERO_06bp","ONY_008",new[]{"ONY_008"},1,out selectedIndex,out useDefault)&&selectedIndex==0&&!useDefault,
            "TARGET selector compares the target card id while preserving the same m_Sounds index");
        Check(NativeAudio.TrySelectCardIdSound(0,null,null,new[]{"ONY_008"},1,out selectedIndex,out useDefault)&&selectedIndex==-1&&useDefault,
            "an unresolved selected card id follows the native default-sound fallback path");
        Check(!NativeAudio.TrySelectCardIdSound(2,"HERO_06bp",null,new[]{"ONY_008"},1,out selectedIndex,out useDefault)&&
            !NativeAudio.TrySelectCardIdSound(0,"HERO_06bp",null,new[]{"ONY_008"},0,out selectedIndex,out useDefault),
            "unsupported WhichCard values and misaligned source arrays stay unbound");
        var machine=program.machines.Single(row=>row.owner_known&&row.owner==1);
        var death=machine.states.Single(row=>row.name=="DeathState");
        Check(death.actions.Any(action=>action.name=="ParticlePlayAction")&&
            death.actions.Any(action=>action.name=="AnimationPlaythroughAction")&&
            death.actions.Any(action=>action.name=="SpellCardIdAudioPlayAction"),
            "owner 1 DeathState contains source animation, particle and card-id audio action");

        var cues=new List<NativeAudioCue>();
        var effect=new NativeEffectInstance(program,assets,27,512,512,()=>Color.white,
            new NativeSpellContext(0,lifecycle:"Death",sourceCardId:"HERO_06bp"),cues.Add);
        try
        {
            var image=effect.Render(.5f);
            int pixels=CountPixels(image);
            Check(effect.ActionExecutions.ContainsKey("AnimationPlaythroughAction")&&
                effect.ActionExecutions["AnimationPlaythroughAction"]==1,"direct Death lifecycle enters source animation action");
            Check(effect.ActionExecutions.ContainsKey("ParticlePlayAction")&&
                effect.ActionExecutions["ParticlePlayAction"]==1,"direct Death lifecycle enters source particle action");
            Check(pixels>0,"source root produces non-transparent render output");
            Check(effect.ActionExecutions.ContainsKey("SpellCardIdAudioPlayAction")&&effect.ActionExecutions["SpellCardIdAudioPlayAction"]==1&&
                !effect.UnhandledActionDetails.Any(detail=>detail.Contains("action=SpellCardIdAudioPlayAction")),
                "card-id action executes through the reviewed SOURCE/default branch");
            Check(cues.Count==1&&cues[0].file=="74537c37a8e26deff87c5a4a.wav"&&cues[0].category==1&&Mathf.Approximately(cues[0].volume,1f),
                "HERO_06bp SOURCE does not match ONY_008 and emits a cue for the native Druid default SoundDef");
            Debug.Log("DRUID SOURCE ROOT: "+passed+" checks; pixels="+pixels+
                " cues="+cues.Count+" unhandled="+String.Join(";",effect.UnhandledActionDetails.ToArray()));
        }
        finally{effect.Dispose();}

        var matchedProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        matchedProgram.audio_file=program.audio_file;
        matchedProgram.machines.Single(row=>row.owner_known&&row.owner==1).states.Single(row=>row.name=="DeathState").actions.Single(row=>row.name=="SpellCardIdAudioPlayAction").Param("m_VolumeScale").number=.5f;
        var matchedCues=new List<NativeAudioCue>();
        using(var matched=new NativeEffectInstance(matchedProgram,assets,28,32,32,()=>Color.white,
            new NativeSpellContext(0,lifecycle:"Death",sourceCardId:"ONY_008"),matchedCues.Add))
        {
            matched.Render(.25f);
            Check(matchedCues.Count==1&&matchedCues[0].file=="3077d544e79ee517d6a269f0.wav"&&Mathf.Approximately(matchedCues[0].volume,.5f),
                "matching ONY_008 SOURCE uses m_Sounds[0] and applies m_VolumeScale");
        }

        var defaultSound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,"d8d196b39b893dec0e89fda7.native-sound.json")));
        var sourceScaledCue=NativeAudio.ResolveSpellCardIdSound(audioSource.audio_sources[0].values,defaultSound,0,.5f,4,()=>0f);
        Check(sourceScaledCue!=null&&Mathf.Approximately(sourceScaledCue.volume,.5f)&&sourceScaledCue.category==4,
            "PlayOneShot applies AudioSource volume times action scale and uses the attached source SoundDef category");

        var waitingProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        waitingProgram.audio_file=program.audio_file;
        var waitAction=waitingProgram.machines.Single(row=>row.owner_known&&row.owner==1).states.Single(row=>row.name=="DeathState").actions.Single(row=>row.name=="SpellCardIdAudioPlayAction");
        waitAction.Param("m_Delay").number=.2f;waitAction.Param("m_WaitForFinish").number=1;
        bool playing=true;NativeAudioCue waitingCue=null;
        using(var waiting=new NativeEffectInstance(waitingProgram,assets,29,32,32,()=>Color.white,
            new NativeSpellContext(0,lifecycle:"Death",sourceCardId:"HERO_06bp"),cue=>{waitingCue=cue;cue.playbackActive=()=>playing;}))
        {
            for(int frame=1;frame<=5&&waitingCue==null;frame++)waiting.Render(frame*.1f);
            Check(waitingCue!=null&&waitingCue.time>=.2f&&waitingCue.file=="74537c37a8e26deff87c5a4a.wav"&&waiting.CardIdAudioFinishes==0,
                "configured delay emits the default cue and wait=true remains active while callback playback is active");
            playing=true;waiting.Render(.7f);
            Check(waiting.CardIdAudioFinishes==0,"wait=true does not finish while the selected audio source is active");
            playing=false;waiting.Render(.8f);
            Check(waiting.CardIdAudioFinishes==1,"wait=true finishes after the callback reports the source inactive");
        }
    }

    static int CountPixels(RenderTexture image)
    {
        var previous=RenderTexture.active;var readback=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=image;readback.ReadPixels(new Rect(0,0,image.width,image.height),0,0,false);readback.Apply(false,false);
            return readback.GetPixels32().Count(pixel=>pixel.r!=0||pixel.g!=0||pixel.b!=0||pixel.a!=0);
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(readback);}
    }
}
