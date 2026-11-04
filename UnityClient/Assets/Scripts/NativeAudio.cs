using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class NativeAudioClipRef{public string reference,file;}
[Serializable] public sealed class NativeSoundChoice{public string m_Clip;public float m_Weight;}
[Serializable] public sealed class NativeSoundValues
{
    public string m_AudioClip;public NativeSoundChoice[] m_RandomClips;
    public float m_RandomPitchMin,m_RandomPitchMax,m_RandomVolumeMin,m_RandomVolumeMax;
    public int m_Category,m_InstanceLimited,m_InstanceLimitMaximum,m_LimitMaxOutOption;public float m_InstanceLimitedDuration;public bool m_IgnoreDucking,m_persistPastGameEnd;
}
[Serializable] public sealed class NativeAudioPointer{public int m_FileID;public long m_PathID;}
[Serializable] public sealed class NativeAudioCurve{public NativeKey[] m_Curve;public int m_PreInfinity,m_PostInfinity;}
[Serializable] public sealed class NativeAudioSourceValues
{
    public int m_Enabled;public NativeAudioPointer m_audioClip,OutputAudioMixerGroup;public float m_Volume,m_Pitch;
    public bool Loop,Mute,Spatialize,SpatializePostEffects,BypassEffects,BypassListenerEffects,BypassReverbZones;
    public int Priority,rolloffMode;public float DopplerLevel,MinDistance,MaxDistance,Pan2D;
    public NativeAudioCurve rolloffCustomCurve,panLevelCustomCurve,spreadCustomCurve,reverbZoneMixCustomCurve;
}
[Serializable] public sealed class NativeAudioConfig{public NativeAudioSourceValues values;}
[Serializable] public sealed class NativeAudioTemplateAsset{public NativeAudioSourceValues values;public string mixer,group;}
[Serializable] public sealed class NativeAudioSourceData{public NativeAssetSource source;public NativeAudioSourceValues values;}
[Serializable] public sealed class NativeSoundData{public NativeSoundValues values;}
[Serializable] public sealed class NativeAudioNode{public string path;public int node;public bool node_known;public NativeAudioSourceData[] audio_sources;public NativeSoundData[] sound_defs;}
[Serializable] public sealed class NativeAudioDefinition{public NativeAudioNode[] nodes;public NativeAudioClipRef[] clips;}
[Serializable] public sealed class NativeSoundAsset{public NativeSoundValues values;public NativeAudioClipRef[] clips;}
public sealed class NativeAudioCue
{
    public string file;public float time,volume,pitch,spatialBlend;public int category;public bool loop,mute;
    public NativeAudioSourceValues template;public Vector3 position;public bool positionKnown;
    public string sourceName,restartKey;public Transform sourceParent;public NativeSoundOptions options;
    public string mixer,group;public bool copyTemplate,inheritSpatialCurve,keepSource;
    public GameObject trackedSource;public Transform stagingRoot;public NativeAudioStagingFrame stagingFrame;public bool stagingBindingKnown,stagedCloneSource,stopWhenSourceInactive,sourceInactiveStopSent;public Vector3 stagingOrigin;
    public float volumeBase;public Func<float> currentCodeVolume;public Func<bool> sourceStopped,sourceDestroyed;
    public bool randomizeStart;public float randomStart;
    public Func<bool> playbackActive;public Action stopPlayback;
}
public sealed class NativeAudioStagingFrame
{
    public Vector3 Origin{get;private set;}
    public NativeAudioStagingFrame(Vector3 origin){Origin=origin;}
    public void SetOrigin(Vector3 origin){Origin=origin;}
}
public static class NativeAudioTemplate
{
    public static void Copy(AudioSource voice,NativeAudioSourceValues source)
    {
        // SoundUtils.CopyAudioSource copies these properties, not the template's
        // custom curves, mute, spatialize or listener/reverb bypass flags.
        voice.bypassEffects=source.BypassEffects;voice.loop=source.Loop;voice.priority=source.Priority;
        voice.volume=source.m_Volume;voice.pitch=source.m_Pitch;voice.panStereo=source.Pan2D;
        voice.spatialBlend=source.panLevelCustomCurve.m_Curve[0].value;voice.reverbZoneMix=source.reverbZoneMixCustomCurve.m_Curve[0].value;
        voice.rolloffMode=(AudioRolloffMode)source.rolloffMode;voice.dopplerLevel=source.DopplerLevel;
        voice.minDistance=source.MinDistance;voice.maxDistance=source.MaxDistance;voice.spread=source.spreadCustomCurve.m_Curve[0].value;
    }
    public static void Apply(AudioSource voice,NativeAudioSourceValues source)
    {
        voice.volume=source.m_Volume;voice.pitch=source.m_Pitch;voice.loop=source.Loop;voice.mute=source.Mute;
        voice.priority=source.Priority;voice.dopplerLevel=source.DopplerLevel;voice.minDistance=source.MinDistance;voice.maxDistance=source.MaxDistance;
        voice.panStereo=source.Pan2D;voice.rolloffMode=(AudioRolloffMode)source.rolloffMode;
        voice.spatialize=source.Spatialize;voice.spatializePostEffects=source.SpatializePostEffects;
        voice.bypassEffects=source.BypassEffects;voice.bypassListenerEffects=source.BypassListenerEffects;voice.bypassReverbZones=source.BypassReverbZones;
        Curve(voice,AudioSourceCurveType.CustomRolloff,source.rolloffCustomCurve);
        Curve(voice,AudioSourceCurveType.SpatialBlend,source.panLevelCustomCurve);
        Curve(voice,AudioSourceCurveType.Spread,source.spreadCustomCurve);
        Curve(voice,AudioSourceCurveType.ReverbZoneMix,source.reverbZoneMixCustomCurve);
        voice.rolloffMode=(AudioRolloffMode)source.rolloffMode;
    }
    static void Curve(AudioSource voice,AudioSourceCurveType kind,NativeAudioCurve curve)
    {
        if(curve==null||curve.m_Curve==null||curve.m_Curve.Length==0)return;
        var restored=NativeCurve.Keys(curve.m_Curve);restored.preWrapMode=NativeParam.InfinityMode(curve.m_PreInfinity);restored.postWrapMode=NativeParam.InfinityMode(curve.m_PostInfinity);
        voice.SetCustomCurve(kind,restored);
    }
    // InitSourceTransform snapshots the parent position; it does not follow it.
    // Strip only this client's offscreen staging translation from native nodes.
    public static Vector3 Position(GameObject parent,Transform stagingRoot)
    {return parent==null?Vector3.zero:parent.transform.position-(stagingRoot!=null&&parent.transform.IsChildOf(stagingRoot)?stagingRoot.position:Vector3.zero);}
    public static bool IsStagedCloneNode(GameObject source,GameObject[] nodes)
    {return source!=null&&nodes!=null&&Array.IndexOf(nodes,source)>=0;}
    public static Vector3 Position(GameObject source,bool stagedCloneSource,Vector3 stagingOrigin)
    {return source==null?Vector3.zero:source.transform.position-(stagedCloneSource?stagingOrigin:Vector3.zero);}
    public static Vector3 Position(GameObject source,Transform stagingRoot,bool stagingBindingKnown,bool stagedCloneSource,Vector3 stagingOrigin)
    {return stagingBindingKnown?Position(source,stagedCloneSource,stagingOrigin):Position(source,stagingRoot);}
    public static void BindPosition(NativeAudioCue cue,GameObject source,GameObject[] nodes,NativeAudioStagingFrame stagingFrame,bool followStagedSource)
    {BindPosition(cue,source,nodes,stagingFrame,stagingFrame==null?Vector3.zero:stagingFrame.Origin,followStagedSource);}
    public static void BindPosition(NativeAudioCue cue,GameObject source,GameObject[] nodes,Vector3 stagingOrigin,bool followStagedSource)
    {BindPosition(cue,source,nodes,null,stagingOrigin,followStagedSource);}
    static void BindPosition(NativeAudioCue cue,GameObject source,GameObject[] nodes,NativeAudioStagingFrame stagingFrame,Vector3 stagingOrigin,bool followStagedSource)
    {
        if(cue==null)return;
        cue.stagingBindingKnown=true;
        cue.stagedCloneSource=IsStagedCloneNode(source,nodes);
        cue.stopWhenSourceInactive=cue.stagedCloneSource&&followStagedSource;
        cue.sourceInactiveStopSent=false;
        cue.stagingFrame=cue.stagedCloneSource?stagingFrame:null;
        cue.stagingOrigin=cue.stagedCloneSource?stagingOrigin:Vector3.zero;
        cue.position=Position(source,cue.stagedCloneSource,cue.stagingOrigin);cue.positionKnown=true;
        if(cue.stagedCloneSource&&followStagedSource)
        {cue.trackedSource=source;cue.sourceParent=null;}
    }
}
// Selection boundaries and volume/pitch products follow the statically read source IL.
// Category mixing, ducking, pooling and voice overrides remain outside this adapter.
public static class NativeAudio
{
    public static bool TrySelectCardIdSound(int which,string sourceCardId,string targetCardId,string[] cardIds,int soundCount,out int soundIndex,out bool useDefault)
    {
        soundIndex=-1;useDefault=false;
        if((which!=0&&which!=1)||cardIds==null||soundCount<0||cardIds.Length!=soundCount||cardIds.Any(String.IsNullOrEmpty))return false;
        string selected=which==0?sourceCardId:targetCardId;
        if(!String.IsNullOrEmpty(selected))
            for(int i=0;i<cardIds.Length;i++)if(String.Equals(cardIds[i],selected,StringComparison.Ordinal)){soundIndex=i;return true;}
        useDefault=true;return true;
    }
    public static NativeAudioCue ResolveSpellCardIdSound(NativeAudioSourceValues source,NativeSoundAsset asset,float time,float volumeScale,int sourceCategory,Func<float> random)
    {
        if(!SupportsSpellCardIdAudioSource(source)||float.IsNaN(volumeScale)||float.IsInfinity(volumeScale))return null;
        var cue=ResolveClip(asset,time,source.m_Volume*volumeScale,source.m_Pitch,0,random);
        if(cue==null)return null;
        cue.category=sourceCategory;cue.template=source;cue.inheritSpatialCurve=true;cue.mute=source.Mute;cue.loop=false;
        cue.sourceName="Spell Card ID Audio";
        return cue;
    }
    public static bool SupportsSpellCardIdAudioSource(NativeAudioSourceValues source)
    {
        return source!=null&&source.m_Enabled==1&&source.m_audioClip!=null&&source.m_audioClip.m_FileID==0&&source.m_audioClip.m_PathID==0&&
            source.OutputAudioMixerGroup!=null&&source.OutputAudioMixerGroup.m_FileID==0&&source.OutputAudioMixerGroup.m_PathID==0&&
            HasCurve(source.rolloffCustomCurve)&&HasCurve(source.panLevelCustomCurve)&&HasCurve(source.spreadCustomCurve)&&HasCurve(source.reverbZoneMixCustomCurve);
    }
    static bool HasCurve(NativeAudioCurve curve){return curve!=null&&curve.m_Curve!=null&&curve.m_Curve.Length>0;}
    // SelfAnimUtils.PlayDefaultSound passes null overrides to SoundManager.Play;
    // it uses the receiver's own SoundDef, not AnimationEvent.objectParameter.
    public static NativeAudioCue ResolveExistingSource(NativeAudioDefinition definition,string path,float time,Func<float> random)
    {
        var node=definition==null||definition.nodes==null?null:definition.nodes.SingleOrDefault(n=>n.path==path);
        if(node==null||node.audio_sources==null||node.audio_sources.Length!=1||node.sound_defs==null||node.sound_defs.Length!=1)return null;
        var source=node.audio_sources[0].values;var sound=node.sound_defs[0].values;
        if(source==null||source.m_audioClip==null||source.m_audioClip.m_PathID!=0||sound==null||sound.m_persistPastGameEnd||sound.m_InstanceLimited!=0||source.panLevelCustomCurve==null||source.panLevelCustomCurve.m_Curve==null||source.panLevelCustomCurve.m_Curve.Length==0||source.panLevelCustomCurve.m_Curve[0].value!=0)return null;
        string selected=sound.m_RandomClips!=null&&sound.m_RandomClips.Length>0?Select(sound.m_RandomClips,random()):null;
        if(selected==null)selected=sound.m_AudioClip;
        var clip=(definition.clips??new NativeAudioClipRef[0]).SingleOrDefault(c=>c.reference==selected);
        if(clip==null||String.IsNullOrEmpty(clip.file))return null;
        return new NativeAudioCue{file=clip.file,time=time,category=sound.m_Category,template=source,
            loop=source.Loop,mute=false,spatialBlend=source.panLevelCustomCurve.m_Curve[0].value,
            volume=source.m_Volume*Mathf.Lerp(sound.m_RandomVolumeMin,sound.m_RandomVolumeMax,random()),
            pitch=source.m_Pitch*Mathf.Lerp(sound.m_RandomPitchMin,sound.m_RandomPitchMax,random())};
    }
    public static NativeAudioCue ResolveClip(NativeSoundAsset asset,float time,float volume,float pitch,int category,Func<float> random)
    {
        var sound=asset==null?null:asset.values;
        if(sound==null||sound.m_persistPastGameEnd)return null;
        string selected=sound.m_RandomClips==null||sound.m_RandomClips.Length==0?null:Select(sound.m_RandomClips,random());
        if(selected==null)selected=sound.m_AudioClip;
        var clip=(asset.clips??new NativeAudioClipRef[0]).SingleOrDefault(c=>c.reference==selected);
        if(clip==null||String.IsNullOrEmpty(clip.file))return null;
        return new NativeAudioCue{file=clip.file,time=time,category=category==0?sound.m_Category:category,
            sourceName="Audio Object - "+System.IO.Path.GetFileNameWithoutExtension(sound.m_AudioClip??""),
            options=new NativeSoundOptions{limited=sound.m_InstanceLimited!=0,duration=sound.m_InstanceLimitedDuration,maximum=sound.m_InstanceLimitMaximum,maxOut=sound.m_LimitMaxOutOption},
            volume=volume*Mathf.Lerp(sound.m_RandomVolumeMin,sound.m_RandomVolumeMax,random()),
            pitch=pitch*Mathf.Lerp(sound.m_RandomPitchMin,sound.m_RandomPitchMax,random())};
    }
    public static string Select(NativeSoundChoice[] choices,float sample)
    {
        if(choices==null||choices.Length==0)return null;
        float total=choices.Sum(c=>c.m_Weight),cumulative=0;
        float value=Mathf.Clamp01(sample)*total;
        for(int i=0;i<choices.Length-1;i++){cumulative+=choices[i].m_Weight;if(value<=cumulative)return choices[i].m_Clip;}
        return choices[choices.Length-1].m_Clip;
    }
    public static int SelectWeightedIndex(float[] weights,float sample)
    {
        if(weights==null||weights.Length==0)return 0;
        if(weights.Any(weight=>float.IsNaN(weight)||float.IsInfinity(weight)||weight<0))return -1;
        float total=weights.Sum();if(total<=0)return 0;
        float value=Mathf.Clamp01(sample)*total,cumulative=0;
        for(int i=0;i<weights.Length-1;i++){cumulative+=weights[i];if(value<=cumulative)return i;}
        return weights.Length-1;
    }
    public static NativeAudioNode Node(NativeAudioDefinition definition,string path,int index=-1)
    {
        if(definition==null||definition.nodes==null)return null;
        if(index>=0&&definition.nodes.Any(n=>n.node_known))return definition.nodes.SingleOrDefault(n=>n.node_known&&n.node==index);
        var matches=definition.nodes.Where(n=>n.path==path).ToArray();
        return matches.Length==1?matches[0]:null;
    }
    public static NativeAudioCue ResolvePlaythroughSound(NativeAudioDefinition definition,string path,int index,NativeSoundAsset sound,float time,float? volume,Func<float> random)
    {
        var node=Node(definition,path,index);
        if(node==null||node.audio_sources==null||node.audio_sources.Length!=1)return null;
        var source=node.audio_sources[0].values;
        if(source==null||source.panLevelCustomCurve==null||source.panLevelCustomCurve.m_Curve==null||source.panLevelCustomCurve.m_Curve.Length==0||
            source.spreadCustomCurve==null||source.spreadCustomCurve.m_Curve==null||source.spreadCustomCurve.m_Curve.Length==0||
            source.reverbZoneMixCustomCurve==null||source.reverbZoneMixCustomCurve.m_Curve==null||source.reverbZoneMixCustomCurve.m_Curve.Length==0)return null;
        // PlayClip(true) copies the receiver's template into a separate voice.
        // Its optional volume replaces template volume; it is not a multiplier.
        var cue=ResolveClip(sound,time,volume??source.m_Volume,source.m_Pitch,0,random);
        if(cue==null)return null;
        cue.template=source;cue.copyTemplate=true;cue.inheritSpatialCurve=true;cue.loop=source.Loop;
        return cue;
    }
    public static NativeAudioCue Resolve(NativeAudioDefinition definition,string path,float time,float codeVolume,Func<float> random,int index=-1)
    {
        var node=Node(definition,path,index);
        if(node==null||node.audio_sources==null||node.audio_sources.Length!=1||node.sound_defs==null||node.sound_defs.Length!=1)return null;
        var source=node.audio_sources[0].values;var sound=node.sound_defs[0].values;
        if(source==null||source.m_audioClip==null||source.m_audioClip.m_PathID!=0||sound==null||sound.m_persistPastGameEnd)return null;
        // LoadClipForPlayback first samples a nonempty random list; its null
        // result falls back to the primary reference without another random draw.
        string selected=sound.m_RandomClips==null||sound.m_RandomClips.Length==0?null:Select(sound.m_RandomClips,random());
        if(selected==null)selected=sound.m_AudioClip;
        var clip=(definition.clips??new NativeAudioClipRef[0]).SingleOrDefault(c=>c.reference==selected);
        if(clip==null||String.IsNullOrEmpty(clip.file))return null;
        float volumeBase=source.m_Volume*Mathf.Lerp(sound.m_RandomVolumeMin,sound.m_RandomVolumeMax,random());
        return new NativeAudioCue{file=clip.file,time=time,category=sound.m_Category,loop=source.Loop,mute=source.Mute,template=source,inheritSpatialCurve=true,volumeBase=volumeBase,
            options=new NativeSoundOptions{limited=sound.m_InstanceLimited!=0,duration=sound.m_InstanceLimitedDuration,maximum=sound.m_InstanceLimitMaximum,maxOut=sound.m_LimitMaxOutOption},
            volume=volumeBase*codeVolume,
            pitch=source.m_Pitch*Mathf.Lerp(sound.m_RandomPitchMin,sound.m_RandomPitchMax,random())};
    }
}

[Serializable] public sealed class NativeHeroDamageAudioAction
{public string state,name,sound_asset;public int game_object_node;public float m_VolumeScale,m_Delay;}
[Serializable] public sealed class NativeHeroDamageAudioProfile
{public string profile;public NativeAssetSource source,action_source,sound_source;public NativeAudioSourceData audio_source;public NativeSoundAsset sound;public NativeHeroDamageAudioAction action;}

// The source DamageSplat Health state passes its root AudioSource and the
// explicit child SoundDef to AudioPlayAction. Keep both bindings in the export.
public sealed class NativeHeroDamageAudio
{
    public const string RuntimeFile="native-hero-damage-splat-audio.json";
    public const string HealthClipReference="Blood_Splat_Damage.wav:4dbf1353322e92443997499cf169c06e";
    const string ProfileId="hero-health-damagesplat-v1";
    const string SourceCab="CAB-69c88b234dfa427e02d1fcee9337ae34",SourceBundle="essential_base_global-prefab-0.unity3d";
    readonly NativeHeroDamageAudioProfile profile;
    readonly Action<NativeAudioCue> audioCue;
    readonly Func<float> random;
    readonly HashSet<SemanticEvent> played=new HashSet<SemanticEvent>();
    readonly System.Collections.Generic.Queue<SemanticEvent> playedOrder=new System.Collections.Generic.Queue<SemanticEvent>();
    public bool Enabled{get;private set;}
    public int Started{get;private set;}
    public int Failures{get;private set;}

    public NativeHeroDamageAudio(AssetRegistry assets,Action<NativeAudioCue> audioCue)
    {
        this.audioCue=audioCue;random=()=>UnityEngine.Random.value;
        if(assets==null||audioCue==null)return;
        string path=Path.Combine(assets.Root,RuntimeFile);
        if(!File.Exists(path))return;
        try
        {
            profile=JsonUtility.FromJson<NativeHeroDamageAudioProfile>(File.ReadAllText(path));
            if(!Supports(profile)||!File.Exists(Path.Combine(assets.Root,profile.sound.clips[0].file)))
                throw new InvalidDataException("Hero DamageSplat source binding or PCM cache mismatch");
            Enabled=true;
        }
        catch(Exception error){profile=null;Debug.LogWarning("Original hero health impact audio unavailable: "+error.Message);}
    }
    public NativeHeroDamageAudio(NativeHeroDamageAudioProfile profile,Action<NativeAudioCue> audioCue,Func<float> random)
    {
        this.profile=profile;this.audioCue=audioCue;this.random=random;
        Enabled=audioCue!=null&&random!=null&&Supports(profile);
    }
    static bool Supports(NativeHeroDamageAudioProfile value)
    {
        if(value==null||value.profile!=ProfileId||!HasSource(value.source,SourceCab,"-6173526402183834210","GameObject")||
            !HasSource(value.action_source,SourceCab,"188442603838002590","MonoBehaviour")||
            !HasSource(value.sound_source,SourceCab,"8997213827551090078","MonoBehaviour")||value.audio_source==null||
            value.audio_source.source==null||!HasSource(value.audio_source.source,SourceCab,"-4717049972804004450","AudioSource")||
            value.audio_source.values==null||value.sound==null||value.sound.values==null||value.action==null)return false;
        var source=value.audio_source.values;var sound=value.sound.values;
        if(value.action.state!="Health"||value.action.name!="AudioPlayAction"||value.action.game_object_node!=0||
            !Mathf.Approximately(value.action.m_VolumeScale,1)||!Mathf.Approximately(value.action.m_Delay,0)||
            !String.Equals(sound.m_AudioClip,HealthClipReference,StringComparison.Ordinal)||
            sound.m_RandomClips!=null&&sound.m_RandomClips.Length!=0||
            !Mathf.Approximately(sound.m_RandomVolumeMin,.75f)||!Mathf.Approximately(sound.m_RandomVolumeMax,.75f)||
            !Mathf.Approximately(sound.m_RandomPitchMin,1)||!Mathf.Approximately(sound.m_RandomPitchMax,1)||
            sound.m_Category!=1||sound.m_InstanceLimited!=0||sound.m_persistPastGameEnd||
            value.sound.clips==null||value.sound.clips.Length!=1||value.sound.clips[0].reference!=HealthClipReference||
            String.IsNullOrEmpty(value.sound.clips[0].file)||Path.GetFileName(value.sound.clips[0].file)!=value.sound.clips[0].file||
            !value.sound.clips[0].file.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)||
            source.m_Enabled!=1||source.m_audioClip==null||source.m_audioClip.m_FileID!=0||source.m_audioClip.m_PathID!=0||
            !Mathf.Approximately(source.m_Volume,1)||!Mathf.Approximately(source.m_Pitch,1)||source.Loop||source.Mute||
            !HasCurve(source.rolloffCustomCurve)||!HasCurve(source.panLevelCustomCurve)||
            !HasCurve(source.spreadCustomCurve)||!HasCurve(source.reverbZoneMixCustomCurve))return false;
        return NativeAudio.ResolveClip(value.sound,value.action.m_Delay,
            source.m_Volume*value.action.m_VolumeScale,source.m_Pitch,0,()=>.5f)!=null;
    }
    static bool HasSource(NativeAssetSource value,string cab,string pathid,string type)
    {return value!=null&&String.Equals(value.cab,cab,StringComparison.OrdinalIgnoreCase)&&value.pathid==pathid&&value.type==type&&
        String.Equals(value.bundle,SourceBundle,StringComparison.OrdinalIgnoreCase);}
    static bool HasCurve(NativeAudioCurve value){return value!=null&&value.m_Curve!=null&&value.m_Curve.Length>0;}
    public static bool ShouldPlay(SemanticEvent e,string sourceCardId,int armorAbsorbed,StateView state)
    {
        if(e==null||e.type!="Hit"||e.amount<=0||e.card_type!=3||e.visual==null||e.visual.entity!=e.target||
            e.amount<=Mathf.Max(0,armorAbsorbed)||state==null||state.players==null)return false;
        var target=state.players.FirstOrDefault(player=>player!=null&&player.hero!=null&&player.hero.entity==e.target);
        if(target==null)return false;
        if(sourceCardId=="HERO_07bp")
        {
            var source=state.players.FirstOrDefault(player=>player!=null&&player.power!=null&&player.power.entity==e.source);
            if(source!=null&&source.seat==target.seat)return false;
        }
        return true;
    }
    public static NativeAudioCue ResolveCue(NativeHeroDamageAudioProfile value,Func<float> random)
    {
        if(!Supports(value)||random==null)return null;
        var source=value.audio_source.values;
        var cue=NativeAudio.ResolveClip(value.sound,value.action.m_Delay,
            source.m_Volume*value.action.m_VolumeScale,source.m_Pitch,0,random);
        if(cue==null)return null;
        cue.template=source;cue.inheritSpatialCurve=true;cue.mute=source.Mute;cue.loop=source.Loop;
        cue.sourceName="Hero DamageSplat Health";
        return cue;
    }
    public bool TryPlay(SemanticEvent e,string sourceCardId,int armorAbsorbed,StateView state)
    {
        if(!Enabled||!ShouldPlay(e,sourceCardId,armorAbsorbed,state)||played.Contains(e))return false;
        var cue=ResolveCue(profile,random);if(cue==null){Failures++;return false;}
        played.Add(e);playedOrder.Enqueue(e);
        if(playedOrder.Count>256)played.Remove(playedOrder.Dequeue());
        try{audioCue(cue);Started++;return true;}
        catch(Exception error){Failures++;Debug.LogWarning("Hero DamageSplat playback failed: "+error.Message);return false;}
    }
}

// Source Delay coroutine tests remaining first, subtracts scaled delta, then
// yields. Play runs on the following iteration after the remaining time hits 0.
public sealed class NativeClipAudioDelay
{
    public float Remaining {get;private set;}
    public bool Finished {get;private set;}
    public NativeClipAudioDelay(float delay){Remaining=delay;}
    public bool Step(float delta)
    {
        if(Finished)return false;
        if(Remaining>0){Remaining-=delta;return false;}
        Finished=true;return true;
    }
}
