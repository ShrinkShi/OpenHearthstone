using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;

[Serializable] public sealed class NativeLifecyclePlan{public int machine,state;public float time;}
[Serializable] public sealed class NativeEffectEntry{public string card_id,file,initial_lifecycle;public bool enabled,replace;public float duration;public float impact_delay=-1;public NativeLifecyclePlan[] lifecycle;}
[Serializable] public sealed class NativeEffectIndex{public NativeEffectEntry[] entries;}
public sealed class NativeEffectPlayer : IDisposable
{
    public const float DruidHeroPowerButtonScale=.9f;
    [Serializable] sealed class RootIdentity{public string reference;}
    public Camera TimelineCamera;
    public Action<NativeAudioCue> AudioCue;
    public Func<int,bool> EntityPresentationPresent;
    public Action<string,NativeSuperSpellPlayback,RenderTexture,RenderTexture> SourceSpellRenderDiagnostic;
    public Action<string,NativeSuperSpellPlayback,Rect,Matrix4x4,int,int,float,bool> SourceSpellDrawDiagnostic;
    public Action<string,string,float> DemonHunterTimingDiagnostic;
    readonly AssetRegistry assets;
    readonly Dictionary<string,NativeEffectEntry> index=new Dictionary<string,NativeEffectEntry>();
    readonly Dictionary<string,NativeProgram> programs=new Dictionary<string,NativeProgram>();
    readonly Dictionary<string,NativeEffectInstance> active=new Dictionary<string,NativeEffectInstance>();
    readonly Dictionary<string,NativeAreaPlayback> sourceAreas=new Dictionary<string,NativeAreaPlayback>();
    readonly Dictionary<string,NativeSuperSpellPlayback> sourceSpells=new Dictionary<string,NativeSuperSpellPlayback>();
    readonly Dictionary<string,float> sourceSampleAges=new Dictionary<string,float>();
    readonly Dictionary<string,NativeEffectInstance.SubtreeFrame> heroPowerButtonFrames=new Dictionary<string,NativeEffectInstance.SubtreeFrame>();
    readonly Dictionary<string,NativeEffectInstance.SubtreeFrame> shamanButtonFrames=new Dictionary<string,NativeEffectInstance.SubtreeFrame>();
    readonly Dictionary<string,bool> warriorRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> warlockRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> priestRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> paladinRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> rogueRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> shamanRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,bool> demonHunterRootSupport=new Dictionary<string,bool>();
    readonly Dictionary<string,float> shamanRootAges=new Dictionary<string,float>();
    readonly HashSet<string> shamanRootStarted=new HashSet<string>();
    readonly Dictionary<string,NativeDeathKnightHeroPowerPlayback.Pair> deathKnightPairs=new Dictionary<string,NativeDeathKnightHeroPowerPlayback.Pair>();
    readonly Dictionary<string,float> deathKnightAges=new Dictionary<string,float>();
    readonly Dictionary<string,float> deathKnightSourceAges=new Dictionary<string,float>();
    readonly Dictionary<string,bool> deathKnightTokenAudioAtMap=new Dictionary<string,bool>();
    readonly Dictionary<string,int> deathKnightSampleFrames=new Dictionary<string,int>();
    NativeSuperSpellPlayback powerSelection;
    bool selectionSubmitted;
    public bool HasPowerSelection{get{return powerSelection!=null;}}
    public NativeSuperSpellPlayback PowerSelection{get{return powerSelection;}}
    public Action PowerSelectionCancelled;
    public Func<SemanticEvent,StateView,StateView,Rect,Vector3,NativeSuperSpellScene> SuperSpellScene;
    public NativeSuperSpellPlayback SourceSpell(string key){NativeSuperSpellPlayback spell;return sourceSpells.TryGetValue(key,out spell)?spell:null;}
    public bool SourceBusy(string key)
    {
        NativeSuperSpellPlayback spell;NativeAreaPlayback area;NativeEffectInstance shamanRoot;
        if(sourceSpells.TryGetValue(key,out spell)&&(spell.Busy||spell.SourceRootPending))return true;
        if(sourceAreas.TryGetValue(key,out area)&&area.Pending>0)return true;
        NativeEffectInstance deathKnightRoot;float deathKnightAge;NativeEffectEntry deathKnightEntry;
        if(deathKnightPairs.ContainsKey(key)&&active.TryGetValue(key,out deathKnightRoot)&&
           deathKnightAges.TryGetValue(key,out deathKnightAge)&&
           index.TryGetValue(NativeDeathKnightHeroPowerPlayback.HeroPowerId,out deathKnightEntry)&&
           deathKnightAge<deathKnightEntry.duration)return true;
        return shamanRootStarted.Contains(key)&&active.TryGetValue(key,out shamanRoot)&&
            shamanRoot.CurrentStates.Length>0&&shamanRoot.CurrentStates[0]!="NoneState";
    }
    public float GetDeathKnightPlaybackAge(string key,float age)
    {
        bool tokenAudioPlaying;
        return GetDeathKnightPlaybackAge(key,age,out tokenAudioPlaying);
    }
    public static bool ShouldIncludeDeathKnightBorrowedActor(int entity,Func<int,bool> entityPresentationPresent)
    {return entity>0&&(entityPresentationPresent==null||entityPresentationPresent(entity));}
    float GetDeathKnightPlaybackAge(string key,float age,out bool tokenAudioPlaying)
    {
        float sampledSourceAge,sampledPlaybackAge;int sampledFrame;
        if(deathKnightSampleFrames.TryGetValue(key,out sampledFrame)&&sampledFrame==Time.frameCount&&
           deathKnightSourceAges.TryGetValue(key,out sampledSourceAge)&&Mathf.Abs(sampledSourceAge-age)<.0001f&&
           deathKnightAges.TryGetValue(key,out sampledPlaybackAge)&&
           deathKnightTokenAudioAtMap.TryGetValue(key,out tokenAudioPlaying))return sampledPlaybackAge;
        NativeEffectInstance effect;
        tokenAudioPlaying=active.TryGetValue(key,out effect)&&NativeDeathKnightHeroPowerPlayback.TokenAudioPlaying(effect);
        return NativeDeathKnightHeroPowerPlayback.PlaybackAge(age,tokenAudioPlaying);
    }
    public bool SourceVisualTailActive(string key){NativeAreaPlayback area;return sourceAreas.TryGetValue(key,out area)&&area.VisualTailActive;}
    public float? SourceImpactTime(string key){NativeSuperSpellPlayback spell;return sourceSpells.TryGetValue(key,out spell)&&spell.Flow!=null&&spell.Flow.FirstArrivalTime.HasValue?spell.Flow.FirstArrivalTime.Value-spell.CastOffset:(float?)null;}
    static bool IsSuper(string card){return card=="EX1_129"||card=="EX1_134"||card=="HERO_08bp"||card=="HERO_05bp"||card=="HERO_06bp"||card=="HERO_01bp"||card=="HERO_07bp"||card=="HERO_09bp"||card=="HERO_04bp"||card=="HERO_03bp"||card==NativeDemonHunterHeroPowerPlayback.HeroPowerId;}
    void ReportDemonHunterTiming(string key,string stage,long started)
    {if(started!=0)DemonHunterTimingDiagnostic?.Invoke(key,stage,(float)(Stopwatch.GetTimestamp()-started)*1000f/Stopwatch.Frequency);}
    bool RogueRootSupported(NativeEffectEntry entry)
    {
        bool supported;if(rogueRootSupport.TryGetValue(entry.file,out supported))return supported;
        try
        {
            var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            var config=catalog.programs.Single(row=>row.card_id==entry.card_id&&row.file==entry.file&&row.reference==root.reference);
            supported=NativeRogueHeroPowerProfile.Supports(root,config,assets.Root);
        }
        catch{supported=false;}
        rogueRootSupport[entry.file]=supported;return supported;
    }
    bool WarriorRootSupported(NativeEffectEntry entry)
    {
        bool supported;if(warriorRootSupport.TryGetValue(entry.file,out supported))return supported;
        try
        {
            string path=Path.Combine(assets.Root,entry.file);
            var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path));
            var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            var config=catalog.programs.Single(row=>row.card_id==entry.card_id&&row.file==entry.file&&row.reference==root.reference);
            supported=File.Exists(Path.Combine(assets.Root,root.audio_file))&&NativeSuperSpell.SupportsNoMissileUntargeted(config)&&
                NativeSuperSpellPlayback.SupportsWarriorHeroPowerRoot(root);
        }
        catch{supported=false;}
        warriorRootSupport[entry.file]=supported;return supported;
    }
    bool WarlockRootSupported(NativeEffectEntry entry)
    {
        bool supported;if(warlockRootSupport.TryGetValue(entry.file,out supported))return supported;
        try
        {
            var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            var config=catalog.programs.Single(row=>row.card_id==entry.card_id&&row.file==entry.file&&row.reference==root.reference);
            var action=root.machines.Single(machine=>machine.owner_known&&machine.owner==1).states
                .SelectMany(state=>state.actions).Single(row=>row.name=="AudioPlayClipAction");
            string soundPath=action.Param("m_Sound").asset_path;
            var sound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,soundPath)));
            supported=NativeSuperSpellPlayback.SupportsWarlockHeroPowerRoot(root,config)&&sound.clips!=null&&sound.clips.Length==1&&
                File.Exists(Path.Combine(assets.Root,sound.clips[0].file));
        }
        catch{supported=false;}
        warlockRootSupport[entry.file]=supported;return supported;
    }
    bool PriestRootSupported(NativeEffectEntry entry)
    {
        bool supported;if(priestRootSupport.TryGetValue(entry.file,out supported))return supported;
        try
        {
            var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            var config=catalog.programs.Single(row=>row.card_id==entry.card_id&&row.file==entry.file&&row.reference==root.reference);
            var startAsset=catalog.prefabs.Single(asset=>asset.reference==config.start.prefab_reference);
            var impactAsset=catalog.prefabs.Single(asset=>asset.reference==config.impact.prefab.prefab_reference);
            var start=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,startAsset.file)));
            var impact=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,impactAsset.file)));
            supported=entry.card_id=="HERO_09bp"&&
                NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,start,impact,assets.Root);
        }
        catch{supported=false;}
        priestRootSupport[entry.file]=supported;return supported;
    }
    bool PaladinAreaSupported(NativeEffectEntry entry)
    {
        bool supported;if(paladinRootSupport.TryGetValue(entry.file,out supported))return supported;
        try
        {
            var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            var config=catalog.programs.Single(row=>row.card_id==entry.card_id&&row.file==entry.file&&row.reference==root.reference);
            var prefab=catalog.prefabs.Single(row=>row.reference==config.area.friendly.prefab_reference);
            var area=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
            var areaCatalog=JsonUtility.FromJson<NativeSuperAreaCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-config.json")));
            var areaRecord=areaCatalog.programs.Single(row=>row.file==entry.file&&row.reference==root.reference);
            var prefabCatalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-prefabs.json")));
            var registered=prefabCatalog.prefabs.Single(row=>row.reference==config.area.friendly.prefab_reference);
            supported=entry.card_id=="HERO_04bp"&&registered.file==prefab.file&&
                areaRecord.config.friendly!=null&&areaRecord.config.friendly.prefab_reference==config.area.friendly.prefab_reference&&
                File.Exists(Path.Combine(assets.Root,area.audio_file))&&
                NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,config,area);
        }
        catch{supported=false;}
        paladinRootSupport[entry.file]=supported;return supported;
    }
    bool Enabled(NativeEffectEntry entry)
    {
        if(entry.card_id=="HERO_02bp")
        {
            bool supported;if(shamanRootSupport.TryGetValue(entry.file,out supported))return entry.enabled&&supported;
            supported=NativeShamanHeroPowerPlayback.Supports(assets,entry);shamanRootSupport[entry.file]=supported;
            return entry.enabled&&supported;
        }
        if(entry.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
            return entry.enabled&&NativeDeathKnightHeroPowerPlayback.Supports(assets,entry);
        if(entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId)
        {
            if(!entry.enabled||SuperSpellScene==null)return false;
            bool supported;if(!demonHunterRootSupport.TryGetValue(entry.file,out supported))
            {
                supported=NativeDemonHunterHeroPowerPlayback.Supports(assets,entry);
                demonHunterRootSupport[entry.file]=supported;
            }
            return supported;
        }
        if(entry.card_id=="HERO_03bp")return SuperSpellScene!=null&&RogueRootSupported(entry);
        if(entry.card_id=="HERO_04bp")return useSourceAreas&&SuperSpellScene!=null&&PaladinAreaSupported(entry);
        if(entry.card_id=="HERO_07bp")return SuperSpellScene!=null&&entry.enabled&&WarlockRootSupported(entry);
        if(entry.card_id=="HERO_01bp")return SuperSpellScene!=null&&WarriorRootSupported(entry);
        if(entry.card_id=="HERO_09bp")return SuperSpellScene!=null&&PriestRootSupported(entry);
        return entry.enabled||IsSuper(entry.card_id)&&SuperSpellScene!=null;
    }
    readonly bool useSourceAreas;
    public int SourceAreaCount{get{return sourceAreas.Count;}}
    public NativeAreaPlayback SourceArea(string key){NativeAreaPlayback area;return sourceAreas.TryGetValue(key,out area)?area:null;}
    readonly Dictionary<string,HashSet<GameObject>> actorBindings=new Dictionary<string,HashSet<GameObject>>();
    readonly HashSet<string> used=new HashSet<string>();
    readonly Dictionary<string,int> slots=new Dictionary<string,int>();
    readonly HashSet<string> failed=new HashSet<string>();
    Material composite;
    Material affineComposite;
    // Original Board constructor initializes m_AmbientColor to white.
    // Special-event overrides can supply a different board color explicitly.
    public Color BoardAmbientColor=Color.white;
    public int ActiveCount{get{return active.Count;}}
    public bool SelectPower(StateView state,int entity)
    {
        var power=BattleInteraction.Entity(state,entity);
        bool targeted=state!=null&&(state.moves??new Move[0]).Any(move=>move.kind=="power"&&move.entity==entity&&move.target!=0);
        bool priest=power!=null&&power.card_id=="HERO_09bp";
        if(power==null||(power.card_id!="HERO_08bp"&&!priest)||!targeted||SuperSpellScene==null||!BattleInteraction.Ready(state,entity,"power"))return false;
        NativeEffectEntry entry;if(!index.TryGetValue(power.card_id,out entry)||priest&&!Enabled(entry))return false;
        if(powerSelection!=null){if(!selectionSubmitted&&powerSelection.SourceEntity==entity&&!powerSelection.Flow.TargetingCancelled)return true;CancelPowerSelection(false);}
        var data=new SemanticEvent{type=priest?"HeroPowerSelection":"SpellEffect",source=entity,card_id=power.card_id,owner=state.seat};
        var scene=SuperSpellScene(data,state,state,new Rect(0,0,1920,1080),new Vector3(24000,10000,20000));
        try
        {
            var root=JsonUtility.FromJson<RootIdentity>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            powerSelection=new NativeSuperSpellPlayback(new NativeProgram{reference=root.reference},entry,assets,4,scene,true,AudioCue,()=>BoardAmbientColor,TimelineCamera,true);
            powerSelection.IdentifySelection(state.match_id);selectionSubmitted=false;return true;
        }
        catch{scene.Dispose?.Invoke();throw;}
    }
    public void SubmitPowerSelection(int entity)
    {if(powerSelection!=null&&powerSelection.SourceEntity==entity&&!powerSelection.Flow.TargetingCancelled)selectionSubmitted=true;}
    public void ObservePowerState(StateView state)
    {
        if(powerSelection==null)return;
        if(powerSelection.Flow.TargetingCancelled&&state.match_id==powerSelection.MatchId&&state.phase=="play")return;
        bool accepted=selectionSubmitted&&state.match_id==powerSelection.MatchId&&(state.events??new SemanticEvent[0]).Any(e=>e.type=="PlayHeroPower"&&e.target==powerSelection.SourceEntity);
        if(!accepted)CancelPowerSelection(false);
    }
    public void TickPowerSelection(float delta)
    {
        if(powerSelection==null)return;
        powerSelection.Seek(powerSelection.Time+Mathf.Max(0,delta));
        if(powerSelection.Flow.TargetingCancelled&&!powerSelection.Busy){powerSelection.Dispose();powerSelection=null;selectionSubmitted=false;}
    }
    public void CancelPowerSelection(bool animate)
    {
        if(powerSelection==null)return;
        // 已发送的权威请求由接受/拒绝接管；晚到的UI清理不能撤销同一实例。
        if(animate&&selectionSubmitted)return;
        selectionSubmitted=false;
        if(animate&&!powerSelection.Flow.TargetingCancelled){powerSelection.Flow.CancelSelection();PowerSelectionCancelled?.Invoke();return;}
        if(animate)return;
        powerSelection.Dispose();powerSelection=null;
    }
    public void RejectPowerSelection(){selectionSubmitted=false;CancelPowerSelection(true);}
    public void DrawPowerSelection()
    {
        if(powerSelection==null||Event.current.type!=EventType.Repaint)return;
        RenderAffine(new Rect(0,0,1920,1080),powerSelection.RenderAffine(powerSelection.Time),powerSelection.Effect.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
    }
    public NativeEffectPlayer(AssetRegistry registry,bool useSourceAreas=true)
    {
        this.useSourceAreas=useSourceAreas;
        assets=registry;string path=Path.Combine(assets.Root,"native-effect-index.json");
        if(File.Exists(path))foreach(var entry in JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(path)).entries)index[entry.card_id]=entry;
    }
    public float Duration(string card)
    {
        NativeEffectEntry entry;if(!index.TryGetValue(card??"",out entry)||!Enabled(entry)||failed.Contains(entry.file))return 0;
        if(IsSuper(card))
        {
            if(card==NativeDemonHunterHeroPowerPlayback.HeroPowerId)return Mathf.Clamp(entry.duration,.1f,8);
            if(card=="HERO_07bp"||card=="HERO_03bp")return Mathf.Clamp(entry.duration,.1f,8);
            if(card=="HERO_04bp")return Mathf.Clamp(entry.duration,.1f,8);
            var profile=CardEffectRegistry.Find(card);
            // 此值仅开启表现队列；实际到达/结束始终取原根时钟与Busy。
            if(card=="HERO_08bp")return Mathf.Max(.65f,profile==null?0:profile.cast_duration);
            return profile!=null&&profile.cast_duration>0?profile.cast_duration:Mathf.Clamp(entry.duration,.1f,8);
        }
        return Mathf.Clamp(entry.duration,.1f,8);
    }
    public float ImpactDelay(string card){NativeEffectEntry entry;return index.TryGetValue(card??"",out entry)&&entry.enabled&&!failed.Contains(entry.file)&&entry.impact_delay>=0?Mathf.Clamp(entry.impact_delay,0,entry.duration):-1;}
    public void BeginFrame(IEnumerable<string> upcoming=null)
    {
        used.Clear();
        if(upcoming==null||Event.current.type!=EventType.Repaint)return;
        foreach(var key in upcoming)used.Add(key);
        // Release expired writers before sampling newly arriving effects.
        EndFrame();used.Clear();
    }
    public Func<string,int> CardType;
    public Func<EntityView,GameObject> ActorObject;
    public Func<SemanticEvent,StateView,StateView,NativePowerTaskList> OriginalPowerTasks;
    public Func<int,bool> RemovedHistoryEntity;
    public IEnumerable<GameObject> BorrowedActors
    {get{foreach(var pair in actorBindings)if(active.ContainsKey(pair.Key))foreach(var actor in pair.Value)if(actor!=null)yield return actor;}}
    NativeSpellContext Context(string key,SemanticEvent data,StateView before,StateView after,bool includeOriginalTasks=true)
    {
        if(CardType==null||active.ContainsKey(key))return null;
        var bindings=new HashSet<GameObject>();actorBindings[key]=bindings;
        Func<EntityView,GameObject> resolve=ActorObject==null?null:new Func<EntityView,GameObject>(entity=>
        {
            var actor=ActorObject(entity);
            if(actor!=null)
            {
                // Shared original Actor/Spell writer arbitration is not recovered.
                foreach(var pair in actorBindings)if(pair.Key!=key&&active.ContainsKey(pair.Key)&&pair.Value.Contains(actor))return null;
                bindings.Add(actor);
            }
            return actor;
        });
        NativeEffectEntry entry;index.TryGetValue(data.card_id??"",out entry);
        return NativeSpellContext.FromEvent(data,before,after,CardType,resolve,entry==null?null:entry.initial_lifecycle,
            powerTasks:includeOriginalTasks&&OriginalPowerTasks!=null?OriginalPowerTasks(data,before,after):null,entityRemoved:RemovedHistoryEntity);
    }
    public void Prepare(string key,SemanticEvent data,StateView before,StateView after,bool area,float age)
    {
        if(Event.current==null||Event.current.type!=EventType.Repaint)return;
        PrepareSource(key,data,before,after,area,age);
    }
    // Source/history preparation has no GUI dependency; rendering remains OnGUI.
    public bool PrepareSource(string key,SemanticEvent data,StateView before,StateView after,bool area,float age)
    {
        if(data==null||data.card_id!=NativeDemonHunterHeroPowerPlayback.HeroPowerId)
            return PrepareSourceCore(key,data,before,after,area,age);
        bool cold=!active.ContainsKey(key);long started=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
        bool prepared=PrepareSourceCore(key,data,before,after,area,age);
        ReportDemonHunterTiming(key,cold?"prepare-cold-create-and-seek":"prepare-hot-seek",started);
        return prepared;
    }
    bool PrepareSourceCore(string key,SemanticEvent data,StateView before,StateView after,bool area,float age)
    {
        NativeEffectEntry entry;
        if(!index.TryGetValue(data.card_id??"",out entry))return false;
        long enabledStarted=entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&DemonHunterTimingDiagnostic!=null?
            Stopwatch.GetTimestamp():0;
        bool enabled=Enabled(entry);
        ReportDemonHunterTiming(key,"prepare-enabled-check",enabledStarted);
        if(!enabled||failed.Contains(entry.file))return false;
        if(entry.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
            return PrepareDeathKnightSource(key,data,before,after,age,entry);
        if(entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId)
        {
            NativeDemonHunterHeroPowerPlayback.Pair pair;
            long profileStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
            if(age<0||float.IsNaN(age)||float.IsInfinity(age)||
               !NativeDemonHunterHeroPowerPlayback.TryGetPairForPowerCue(data,before,after,out pair))
            {ReportDemonHunterTiming(key,"prepare-pair-validation",profileStarted);return false;}
            age=NativeDemonHunterHeroPowerPlayback.PlaybackAge(age);
            ReportDemonHunterTiming(key,"prepare-pair-validation",profileStarted);
        }
        try
        {
            long setupStarted=data.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&DemonHunterTimingDiagnostic!=null?
                Stopwatch.GetTimestamp():0;
            var context=Context(key,data,before,after,entry.card_id!=NativeDemonHunterHeroPowerPlayback.HeroPowerId);
            var instance=Instance(key,entry,area||entry.card_id=="HERO_04bp",context,age,Friendly(data,before,after),data,before,after);
            ReportDemonHunterTiming(key,"prepare-instance",setupStarted);if(instance==null)return false;
            used.Add(key);NativeAreaPlayback sourceArea;NativeSuperSpellPlayback spell;
            if(sourceSpells.TryGetValue(key,out spell))
            {
                long seekStarted=data.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&DemonHunterTimingDiagnostic!=null?
                    Stopwatch.GetTimestamp():0;
                spell.Seek(age+spell.CastOffset);sourceSampleAges[key]=age;
                ReportDemonHunterTiming(key,"prepare-spell-seek",seekStarted);
            }
            else if(sourceAreas.TryGetValue(key,out sourceArea))sourceArea.Seek(age);
            else if(entry.card_id=="HERO_02bp")
            {
                float previous;bool restart=!shamanRootAges.TryGetValue(key,out previous)||age<previous;
                if(restart)
                {
                    shamanRootStarted.Remove(key);instance.Seek(0);
                    if(!instance.DispatchMachineEvent(0,"Action"))throw new InvalidOperationException("H02 root Action source event did not transition");
                    shamanRootStarted.Add(key);
                }
                else if(!shamanRootStarted.Contains(key))throw new InvalidOperationException("H02 root event state was lost");
                instance.Seek(age);shamanRootAges[key]=age;
                if(instance.UnhandledActions!=0||instance.UnboundAudioActions!=0||instance.UnboundAnimationActions!=0||
                   instance.UnboundFinishActions!=0||instance.UnboundMachineOwnerActions!=0)
                    throw new InvalidOperationException("H02 root has unbound source actions");
            }
            else instance.Seek(age);
            return true;
        }
        catch(Exception error){failed.Add(entry.file);Debug.LogWarning("Native effect prepare fallback "+data.card_id+": "+error.GetBaseException().Message);return false;}
    }
    bool PrepareDeathKnightSource(string key,SemanticEvent data,StateView before,StateView after,float age,NativeEffectEntry entry)
    {
        NativeDeathKnightHeroPowerPlayback.Pair pair;
        if(age<0||float.IsNaN(age)||float.IsInfinity(age)||
           !NativeDeathKnightHeroPowerPlayback.TryGetPairForPowerCue(data,before,after,out pair))return false;
        float playbackAge=NativeDeathKnightHeroPowerPlayback.PlaybackAge(age);
        bool tokenAudioPlaying=false;
        NativeEffectInstance effect=null;
        try
        {
            NativeDeathKnightHeroPowerPlayback.Pair existingPair;
            if(active.TryGetValue(key,out effect))
            {
                float previousSourceAge,previousPlaybackAge;
                if(!deathKnightPairs.TryGetValue(key,out existingPair)||!NativeDeathKnightHeroPowerPlayback.SamePair(existingPair,pair)||
                   !deathKnightSourceAges.TryGetValue(key,out previousSourceAge)||age<previousSourceAge||
                   !deathKnightAges.TryGetValue(key,out previousPlaybackAge)||playbackAge<previousPlaybackAge)
                    throw new InvalidOperationException("H11 pair or playback time regressed");
                playbackAge=GetDeathKnightPlaybackAge(key,age,out tokenAudioPlaying);
                if(playbackAge<previousPlaybackAge)throw new InvalidOperationException("H11 audio-aware playback time regressed");
                effect.Seek(playbackAge);ValidateDeathKnightEffect(effect);
                deathKnightSourceAges[key]=age;deathKnightAges[key]=playbackAge;deathKnightTokenAudioAtMap[key]=tokenAudioPlaying;
                deathKnightSampleFrames[key]=Time.frameCount;
                used.Add(key);return true;
            }
            if(active.Count>=4||ActorObject==null) return false;
            var resolvedActor=ActorObject(pair.ghoul);var identity=resolvedActor==null?null:NativeActorIdentity.FindInThisOrParents(resolvedActor);
            if(identity==null||identity.GetComponent<NativeActorPresentation>()==null||
               active.Values.Any(current=>NativeActorIdentity.FindInThisOrParents(current.Root)==identity))return false;
            var actor=identity.gameObject;
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            int slot=0;while(slots.ContainsValue(slot))slot++;
            effect=new NativeEffectInstance(program,assets,slot,512,512,()=>BoardAmbientColor,audioCallback:AudioCue,deferEntryEvents:true);
            effect.BindTimelineCamera(TimelineCamera);
            effect.SpellPlayerSideEventResolver=(machine,action)=>
            {
                string side;
                if(machine!=0||action==null||action.name!="SpellPlayerSideEventAction"||
                   !NativeDeathKnightHeroPowerPlayback.TryGetSideEvent(pair,after,out side))return null;
                var expected=side=="Friendly"?action.Param("m_FriendlyEvent").text:action.Param("m_OpponentEvent").text;
                return expected==side?side:null;
            };
            effect.AttachToOriginalParent(actor);
            effect.Seek(0);
            if(effect.SpellPlayerSideEventDispatches!=1||effect.UnboundSpellPlayerSideEventActions!=0||
               !effect.DispatchMachineEvent(0,"Action"))throw new InvalidOperationException("H11 source side/Action FSM events did not dispatch");
            if(!deathKnightPairs.ContainsKey(key))deathKnightPairs.Add(key,pair);
            deathKnightSourceAges[key]=0;deathKnightAges[key]=0;deathKnightTokenAudioAtMap[key]=false;
            actorBindings[key]=new HashSet<GameObject>{actor};slots[key]=slot;active[key]=effect;
            float bootstrapAge=age>=NativeDeathKnightHeroPowerPlayback.IdentityThroughAge?
                Mathf.Min(age,NativeDeathKnightHeroPowerPlayback.AuthoredTimelineDuration-.001f):playbackAge;
            effect.Seek(bootstrapAge);
            playbackAge=GetDeathKnightPlaybackAge(key,age,out tokenAudioPlaying);
            if(playbackAge<bootstrapAge)throw new InvalidOperationException("H11 audio bootstrap playback time regressed");
            if(playbackAge>bootstrapAge)effect.Seek(playbackAge);
            ValidateDeathKnightEffect(effect);
            deathKnightSourceAges[key]=age;deathKnightAges[key]=playbackAge;deathKnightTokenAudioAtMap[key]=tokenAudioPlaying;
            deathKnightSampleFrames[key]=Time.frameCount;
            used.Add(key);return true;
        }
        catch(Exception error)
        {
            if(active.Remove(key)&&effect!=null)effect.Dispose();else if(effect!=null)effect.Dispose();
            deathKnightPairs.Remove(key);deathKnightAges.Remove(key);deathKnightSourceAges.Remove(key);deathKnightTokenAudioAtMap.Remove(key);
            deathKnightSampleFrames.Remove(key);
            actorBindings.Remove(key);slots.Remove(key);
            failed.Add(entry.file);Debug.LogWarning("H11 CustomSpawn fallback: "+error.GetBaseException().Message);return false;
        }
    }
    static void ValidateDeathKnightEffect(NativeEffectInstance effect)
    {
        if(effect.SpellPlayerSideEventDispatches!=1||effect.UnboundSpellPlayerSideEventActions!=0||
           effect.UnboundCustomSpawnActions!=0||effect.UnboundActorVisibilityActions!=0||
           effect.UnboundSetParentActions!=0||effect.UnboundDestroyObjectActions!=0||
           effect.UnboundTimelineActions!=0||effect.UnboundTimelineSounds!=0||effect.UnsupportedTimelineTracks!=0||
           effect.UnboundTimelineCameras!=0||effect.UnboundAudioActions!=0||effect.UnboundAnimationActions!=0||
           effect.UnboundMachineOwnerActions!=0||effect.UnboundMainShakeActions!=0||effect.UnhandledActions!=0||
           effect.CustomSpawnInitializations!=1||effect.TimelineStarts>1||effect.TimelineSoundStarts>1||
           effect.TimelineStarts>0&&effect.TimelineSoundStarts!=1)
            throw new InvalidOperationException(DeathKnightEffectDiagnostic(effect));
    }
    static string DeathKnightEffectDiagnostic(NativeEffectInstance effect)
    {
        var actions=String.Join(",",effect.ActionExecutions.OrderBy(pair=>pair.Key).Select(pair=>pair.Key+"="+pair.Value).ToArray());
        var unhandled=String.Join(" | ",effect.UnhandledActionDetails.ToArray());
        return "H11 source CustomSpawn/Timeline contains an unbound action; " +
            "SideEvent="+effect.SpellPlayerSideEventDispatches+"/"+effect.UnboundSpellPlayerSideEventActions+"; " +
            "CustomSpawn="+effect.CustomSpawnInitializations+"/"+effect.CustomSpawnFinalizations+"/"+effect.UnboundCustomSpawnActions+"; " +
            "ActorVisibility="+effect.ActorRootVisibilityBindings+"/"+effect.UnboundActorVisibilityActions+"; " +
            "SetParent="+effect.SetParentActions+"/"+effect.UnboundSetParentActions+"; " +
            "DestroyObject="+effect.DestroyObjectActions+"/"+effect.UnboundDestroyObjectActions+"; " +
            "Timeline="+effect.TimelineStarts+"/"+effect.TimelineSoundStarts+"/"+effect.UnboundTimelineActions+"/"+
                effect.UnboundTimelineSounds+"/"+effect.UnsupportedTimelineTracks+"/"+effect.UnboundTimelineCameras+"; " +
            "Audio="+effect.UnboundAudioActions+"; Animation="+effect.UnboundAnimationActions+"; " +
            "MachineOwner="+effect.UnboundMachineOwnerActions+"; MainShake="+effect.UnboundMainShakeActions+"; " +
            "Unhandled="+effect.UnhandledActions+" ["+unhandled+"]; Actions=["+actions+"]";
    }
    public bool HasDeathKnightRouteFor(NativeDeathKnightHeroPowerPlayback.Pair pair)
    {return pair!=null&&deathKnightPairs.Any(row=>active.ContainsKey(row.Key)&&NativeDeathKnightHeroPowerPlayback.SamePair(row.Value,pair));}
    static bool? Friendly(SemanticEvent data,StateView before,StateView after)
    {var state=after??before;return state==null?(bool?)null:(IsSuper(data.card_id)?NativeSuperSpellBoard.Controller(data,before,after):data.owner)==state.seat;}
    NativeEffectInstance Instance(string key,NativeEffectEntry entry,bool area,NativeSpellContext context,float age=0,bool? friendly=null,SemanticEvent data=null,StateView before=null,StateView after=null)
    {
        NativeEffectInstance instance;NativeAreaPlayback previous;
        NativeSuperSpellPlayback oldSpell;
        // 比较同一表现时钟的输入值。跨时钟加offset再比较会受Mono扩展精度影响，
        // 同帧Prepare/Draw可能把相同float误判为倒带并强制取消刚移交的根。
        float sampledAge;
        if(sourceSpells.TryGetValue(key,out oldSpell)&&sourceSampleAges.TryGetValue(key,out sampledAge)&&age<sampledAge)
        {oldSpell.Dispose();sourceSpells.Remove(key);sourceSampleAges.Remove(key);active.Remove(key);slots.Remove(key);}
        if(sourceAreas.TryGetValue(key,out previous)&&age<previous.Time)
        {context=previous.Context;previous.Dispose();sourceAreas.Remove(key);active.Remove(key);slots.Remove(key);}
        if(active.TryGetValue(key,out instance))return instance;
        if(active.Count>=4)return null;
        if(!String.IsNullOrEmpty(entry.initial_lifecycle))
        {
            NativeSpellStateType initial;
            if(!Enum.TryParse(entry.initial_lifecycle,true,out initial)||!Enum.IsDefined(typeof(NativeSpellStateType),initial))throw new ArgumentException("Invalid reviewed initial lifecycle");
        }
        NativeProgram program;if(!programs.TryGetValue(entry.file,out program))
        {
            string json=File.ReadAllText(Path.Combine(assets.Root,entry.file));
            program=entry.card_id=="HERO_02bp"?NativeShamanHeroPowerPlayback.ForOwner(assets,entry,0):
                IsSuper(entry.card_id)&&entry.card_id!="HERO_01bp"&&entry.card_id!="HERO_07bp"&&entry.card_id!="HERO_04bp"&&entry.card_id!="HERO_03bp"?
                new NativeProgram{reference=JsonUtility.FromJson<RootIdentity>(json).reference}:JsonUtility.FromJson<NativeProgram>(json);
            if(programs.Count>=32){var keys=new List<string>(programs.Keys);programs.Remove(keys[0]);}programs[entry.file]=program;
        }
        int slot=0;while(slots.ContainsValue(slot))slot++;
        if(powerSelection!=null&&powerSelection.CardId==entry.card_id&&selectionSubmitted&&powerSelection.SourceEntity==data.source&&powerSelection.MatchId==after.match_id)
        {
            var adopted=powerSelection;powerSelection=null;selectionSubmitted=false;
            try{adopted.Cast(data,after);sourceSpells[key]=adopted;instance=adopted.Effect;active[key]=instance;slots[key]=4;return instance;}
            catch{adopted.Dispose();throw;}
        }
        if(entry.card_id=="HERO_04bp")
        {
            if(!useSourceAreas)throw new NotSupportedException("HERO_04bp FriendlyArea-only reconstruction is disabled");
            if(SuperSpellScene==null||data==null)throw new NotSupportedException("HERO_04bp source scene is unbound");
            var scene=SuperSpellScene(data,before,after,new Rect(0,0,1920,1080),new Vector3(20000+slot*1000,10000,20000));
            NativeAreaPlayback sourceArea;
            try{sourceArea=new NativeAreaPlayback(program,entry,assets,slot,friendly,area,context,AudioCue,()=>BoardAmbientColor,TimelineCamera,scene);}
            catch{scene?.Dispose?.Invoke();throw;}
            if(!sourceArea.FriendlyAreaOnlyReconstruction)throw new NotSupportedException("HERO_04bp did not enter its explicit area-only reconstruction path");
            sourceAreas[key]=sourceArea;instance=sourceArea.Effect;
        }
        else if(IsSuper(entry.card_id))
        {
            if(SuperSpellScene==null||data==null)throw new NotSupportedException("SuperSpell presentation scene input is unbound");
            var scene=SuperSpellScene(data,before,after,new Rect(0,0,1920,1080),new Vector3(20000+slot*1000,10000,20000));
            if(context!=null&&context.PowerTargets!=null&&(scene.History==null||scene.History.Tasks!=context.PowerTargets.TaskList))
            {scene.Dispose?.Invoke();throw new NotSupportedException("Explicit original history is missing its SuperSpell metadata host");}
            var spell=new NativeSuperSpellPlayback(program,entry,assets,slot,scene,friendly,AudioCue,()=>BoardAmbientColor,TimelineCamera,
                demonHunterProfileAlreadyValidated:entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId);
            if(entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&DemonHunterTimingDiagnostic!=null)
                spell.DemonHunterTimingDiagnostic=(stage,milliseconds)=>DemonHunterTimingDiagnostic?.Invoke(key,stage,milliseconds);
            sourceSpells[key]=spell;instance=spell.Effect;
        }
        else if(useSourceAreas&&(entry.card_id=="CS2_025"||entry.card_id=="CS2_062"))
        {
            var sourceArea=new NativeAreaPlayback(program,entry,assets,slot,friendly,area,context,AudioCue,()=>BoardAmbientColor,TimelineCamera);
            sourceAreas[key]=sourceArea;instance=sourceArea.Effect;
        }
        else instance=new NativeEffectInstance(program,assets,slot,512,area?256:512,()=>BoardAmbientColor,context,AudioCue,deferEntryEvents:entry.card_id=="HERO_02bp");
        if(!sourceAreas.ContainsKey(key)&&!sourceSpells.ContainsKey(key))instance.BindTimelineCamera(TimelineCamera);
        try
        {
            if(entry.lifecycle!=null&&!sourceSpells.ContainsKey(key))
            {
                var timeline=new List<NativeLifecycleEvent>();foreach(var cue in entry.lifecycle)timeline.Add(new NativeLifecycleEvent(cue.machine,cue.time,(NativeSpellStateType)cue.state));
                instance.SetLifecycleTimeline(timeline);
            }
            active[key]=instance;slots[key]=slot;return instance;
        }
        catch{NativeAreaPlayback owned;NativeSuperSpellPlayback spell;if(sourceSpells.TryGetValue(key,out spell)){spell.Dispose();sourceSpells.Remove(key);}else if(sourceAreas.TryGetValue(key,out owned)){owned.Dispose();sourceAreas.Remove(key);}else instance.Dispose();throw;}
    }
    public bool Draw(string key,SemanticEvent data,StateView before,StateView after,Rect rect,float age)
    {
        if(data!=null&&data.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
        {
            NativeEffectEntry h11Entry;
            if(!index.TryGetValue(data.card_id,out h11Entry)||!Enabled(h11Entry)||failed.Contains(h11Entry.file))return false;
            if(Event.current==null||Event.current.type!=EventType.Repaint)return false;
            if(!PrepareSource(key,data,before,after,false,age))return true;
            var h11=active[key];used.Add(key);float mappedAge;
            if(!deathKnightAges.TryGetValue(key,out mappedAge))return true;
            NativeDeathKnightHeroPowerPlayback.Pair pair;
            HashSet<GameObject> boundActors;
            NativeActorPresentation captureActor=null;
            if(deathKnightPairs.TryGetValue(key,out pair)&&
               !ShouldIncludeDeathKnightBorrowedActor(pair.ghoul.entity,EntityPresentationPresent)&&
               actorBindings.TryGetValue(key,out boundActors)&&boundActors.Count==1)
            {
                var actor=boundActors.First();
                if(actor!=null)captureActor=actor.GetComponent<NativeActorPresentation>();
            }
            if(captureActor!=null)
            {
                bool wasShown=captureActor.Shown;
                try
                {
                    var deathEmission=h11.RenderAffineExcludingSubtree(mappedAge,17,2.5f,()=>captureActor.SetShown(false));
                    return RenderAffine(rect,deathEmission,h11.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
                }
                finally{captureActor.SetShown(wasShown);}
            }
            var emission=h11.RenderAffineExcludingSubtree(mappedAge,17,2.5f);
            return RenderAffine(rect,emission,h11.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
        }
        NativeEffectEntry entry;
        if(!index.TryGetValue(data.card_id??"",out entry)||!Enabled(entry)||failed.Contains(entry.file)||Event.current.type!=EventType.Repaint)return false;
        if(IsSuper(data.card_id))
        {
            if(!PrepareSource(key,data,before,after,false,age))return false;
            NativeAreaPlayback sourceArea;
            if(sourceAreas.TryGetValue(key,out sourceArea))
            {
                var areaEmission=sourceArea.RenderAffine(age);var areaRect=new Rect(0,0,1920,1080);
                return RenderAffine(areaRect,areaEmission,sourceArea.Effect.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
            }
            var spell=sourceSpells[key];var matrix=GUI.matrix;int width=Screen.width,height=Screen.height;
            if(data.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId)
            {
                float sampleAge=NativeDemonHunterHeroPowerPlayback.PlaybackAge(age)+spell.CastOffset;
                long drawStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
                long layerStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
                var source=spell.RenderDemonHunterSourceLayer(sampleAge);
                ReportDemonHunterTiming(key,"source-seek-flow-and-affine-capture",layerStarted);
                SourceSpellRenderDiagnostic?.Invoke(key,spell,source,spell.DemonHunterSourceTransmission);
                layerStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
                bool sourcePass=RenderAffine(spell.DrawRect,source,spell.DemonHunterSourceTransmission,matrix,width,height,GUI.color);
                ReportDemonHunterTiming(key,"source-gui-composite",layerStarted);
                SourceSpellDrawDiagnostic?.Invoke(key,spell,spell.DrawRect,matrix,width,height,sampleAge,sourcePass);
                layerStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
                var target=spell.RenderDemonHunterTargetLayer(sampleAge);
                ReportDemonHunterTiming(key,"target-seek-flow-and-affine-capture",layerStarted);
                layerStarted=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
                RenderAffine(spell.DemonHunterTargetBodyRect,target,spell.DemonHunterTargetTransmission,matrix,width,height,GUI.color);
                ReportDemonHunterTiming(key,"target-gui-composite",layerStarted);
                ReportDemonHunterTiming(key,"draw-total",drawStarted);
                return true;
            }
            var emission=spell.RenderAffine(age+spell.CastOffset);SourceSpellRenderDiagnostic?.Invoke(key,spell,emission,spell.Effect.Transmission);
            var screenRect=spell.DrawRect;
            bool passes=RenderAffine(screenRect,emission,spell.Effect.Transmission,matrix,width,height,GUI.color);
            SourceSpellDrawDiagnostic?.Invoke(key,spell,screenRect,matrix,width,height,age+spell.CastOffset,passes);return true;
        }
        var context=Context(key,data,before,after);
        return Draw(key,data.card_id,rect,age,context,Friendly(data,before,after));
    }
    public bool Draw(string key,string card,Rect rect,float age,NativeSpellContext context=null,bool? friendly=null)
    {
        if(Event.current==null||Event.current.type!=EventType.Repaint)return false;
        return Draw(key,card,rect,age,context,friendly,GUI.matrix,Screen.width,Screen.height,GUI.color);
    }
    public bool Draw(string key,string card,Rect rect,float age,NativeSpellContext context,bool? friendly,
        Matrix4x4 matrix,int width,int height,Color tint)
    {
        if(card==NativeDeathKnightHeroPowerPlayback.HeroPowerId||width<=0||height<=0)return false;
        NativeEffectEntry entry;if(!index.TryGetValue(card??"",out entry)||!Enabled(entry)||failed.Contains(entry.file))return false;
        try
        {
            var instance=Instance(key,entry,rect.width>rect.height*1.5f,context,age,friendly);if(instance==null)return false;
            NativeAreaPlayback sourceArea;used.Add(key);RenderTexture emission;
            if(card=="HERO_02bp")
            {
                if(!NativeShamanHeroPowerPlayback.SupportsButtonRoot(instance.Program)||instance.Nodes==null||instance.Nodes.Length==0)return false;
                NativeEffectInstance.SubtreeFrame framing;
                if(!shamanButtonFrames.TryGetValue(key,out framing))
                    shamanButtonFrames[key]=framing=new NativeEffectInstance.SubtreeFrame
                    {center=instance.Nodes[0].transform.position,orthographicSize=NativeShamanHeroPowerPlayback.ButtonOrthographicSize};
                emission=instance.RenderAffineSubtree(age,0,framing);
            }
            else emission=sourceAreas.TryGetValue(key,out sourceArea)?sourceArea.RenderAffine(age):instance.RenderAffine(age);
            RenderAffine(rect,emission,instance.Transmission,matrix,width,height,tint);return entry.replace;
        }
        catch(Exception error){failed.Add(entry.file);Debug.LogWarning("Native effect fallback "+card+": "+error.Message);return false;}
    }
    public bool DrawDeathKnightButton(string key,SemanticEvent data,StateView before,StateView after,Rect rect,float age)
    {
        if(data==null||data.card_id!=NativeDeathKnightHeroPowerPlayback.HeroPowerId||
           Event.current==null||Event.current.type!=EventType.Repaint)return false;
        NativeEffectEntry entry;
        if(!index.TryGetValue(data.card_id,out entry)||!Enabled(entry)||failed.Contains(entry.file)||
           !PrepareSource(key,data,before,after,false,age))return false;
        NativeEffectInstance effect;
        if(!active.TryGetValue(key,out effect)||effect.Program.nodes==null||effect.Program.nodes.Length<=19)return false;
        float mappedAge;if(!deathKnightAges.TryGetValue(key,out mappedAge))return false;
        var source=effect.RenderAffineSubtree(mappedAge,17,1.25f);
        return RenderAffine(rect,source,effect.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
    }
    public bool DrawHeroPowerButton(string key,SemanticEvent data,StateView before,StateView after,Rect rect,float age)
    {
        if(data==null||Event.current==null||Event.current.type!=EventType.Repaint)return false;
        return DrawHeroPowerButton(key,data,before,after,rect,age,GUI.matrix,Screen.width,Screen.height,GUI.color);
    }
    public bool DrawHeroPowerButton(string key,SemanticEvent data,StateView before,StateView after,Rect rect,float age,
        Matrix4x4 matrix,int width,int height,Color tint)
    {
        if(data==null||width<=0||height<=0)return false;
        if(data.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
            return DrawDeathKnightButton(key,data,before,after,rect,age);
        if(data.card_id!="HERO_01bp"&&data.card_id!="HERO_06bp")return false;
        NativeEffectEntry entry;
        if(!index.TryGetValue(data.card_id,out entry)||!Enabled(entry)||failed.Contains(entry.file)||
           !PrepareSource(key,data,before,after,false,age))return false;
        NativeSuperSpellPlayback spell;
        if(!sourceSpells.TryGetValue(key,out spell)||spell.Effect==null)return false;
        NativeEffectInstance effect=spell.Effect;float sourceAge=age+spell.CastOffset;
        if(data.card_id=="HERO_06bp")
        {
            var claw=spell.DruidHeroPowerButtonClone;
            if(claw==null||!NativeSuperSpellPlayback.SupportsDruidFangButtonProgram(claw.Effect.Program))return false;
            effect=claw.Effect;sourceAge=claw.PlaybackTime;
        }
        else
        {
            if(spell.Effect.Program.nodes==null||spell.Effect.Program.nodes.Length<=1)return false;
            spell.Seek(sourceAge);
        }
        used.Add(key);
        if(data.card_id=="HERO_01bp")
        {
            // Source placement already projects the button into this full-board camera.
            // Fitting the complete explosion into the button shrinks its authored world scale.
            var board=effect.RenderAffineSubtree(sourceAge,1,0);
            return RenderAffine(new Rect(0,0,1920,1080),board,effect.Transmission,matrix,width,height,tint);
        }
        NativeEffectInstance.SubtreeFrame framing;
        if(!heroPowerButtonFrames.TryGetValue(key,out framing))
        {
            framing=effect.CaptureSubtreeFrame(sourceAge,1);
            if(data.card_id=="HERO_06bp")framing.orthographicSize/=DruidHeroPowerButtonScale;
            heroPowerButtonFrames[key]=framing;
        }
        var source=effect.RenderAffineSubtree(sourceAge,1,framing);
        return RenderAffine(rect,source,effect.Transmission,matrix,width,height,tint);
    }
    public void Render(Rect rect,Texture texture,Matrix4x4 matrix,int width,int height,Color tint)
    {
        if(composite==null)composite=new Material(Resources.Load<Shader>("NativeEffectComposite"));
        composite.mainTexture=texture;composite.SetColor("_Tint",tint);GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,width,height,0);GL.MultMatrix(matrix);composite.SetPass(0);GL.Begin(GL.QUADS);
            GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
            GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
        }
        finally{GL.PopMatrix();}
    }
    public bool RenderAffine(Rect rect,Texture emission,Texture transmission,Matrix4x4 matrix,int width,int height,Color tint)
    {
        if(affineComposite==null)affineComposite=new Material(Resources.Load<Shader>("NativeEffectAffineComposite"));
        affineComposite.SetColor("_Tint",tint);bool passes=true;GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,width,height,0);GL.MultMatrix(matrix);
            for(int pass=0;pass<2;pass++)
            {
                affineComposite.mainTexture=pass==0?transmission:emission;passes=affineComposite.SetPass(pass)&&passes;GL.Begin(GL.QUADS);
                GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
                GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
            }
        }
        finally{GL.PopMatrix();}
        return passes;
    }
    public void EndFrame()
    {
        if(Event.current.type!=EventType.Repaint)return;
        var remove=new List<string>();
        foreach(var pair in active)
        {
            if(used.Contains(pair.Key))continue;
            if(deathKnightPairs.ContainsKey(pair.Key)&&NativeDeathKnightHeroPowerPlayback.TokenAudioPlaying(pair.Value))
            {
                float sourceAge,mappedAge;NativeEffectEntry entry;
                if(deathKnightSourceAges.TryGetValue(pair.Key,out sourceAge)&&deathKnightAges.TryGetValue(pair.Key,out mappedAge)&&
                   index.TryGetValue(NativeDeathKnightHeroPowerPlayback.HeroPowerId,out entry)&&mappedAge>=entry.duration)
                {
                    mappedAge=Mathf.Min(sourceAge,entry.duration-.001f);pair.Value.Seek(mappedAge);deathKnightAges[pair.Key]=mappedAge;
                }
                deathKnightTokenAudioAtMap[pair.Key]=true;used.Add(pair.Key);continue;
            }
            NativeAreaPlayback area;NativeSuperSpellPlayback spell;
            if(sourceSpells.TryGetValue(pair.Key,out spell))
            {if(spell.Busy)Debug.LogWarning("Native source owner expired while busy "+pair.Key+" time="+spell.Time.ToString("R"));spell.Dispose();sourceSpells.Remove(pair.Key);}
            else if(sourceAreas.TryGetValue(pair.Key,out area)){area.Dispose();sourceAreas.Remove(pair.Key);}
            else pair.Value.Dispose();
            remove.Add(pair.Key);
        }
        foreach(var key in remove){active.Remove(key);sourceSampleAges.Remove(key);heroPowerButtonFrames.Remove(key);shamanButtonFrames.Remove(key);shamanRootAges.Remove(key);shamanRootStarted.Remove(key);deathKnightPairs.Remove(key);deathKnightAges.Remove(key);deathKnightSourceAges.Remove(key);deathKnightTokenAudioAtMap.Remove(key);deathKnightSampleFrames.Remove(key);slots.Remove(key);actorBindings.Remove(key);}
    }
    public void Dispose(){CancelPowerSelection(false);foreach(var pair in active){NativeAreaPlayback area;NativeSuperSpellPlayback spell;if(sourceSpells.TryGetValue(pair.Key,out spell))spell.Dispose();else if(sourceAreas.TryGetValue(pair.Key,out area))area.Dispose();else pair.Value.Dispose();}sourceSpells.Clear();sourceSampleAges.Clear();heroPowerButtonFrames.Clear();shamanButtonFrames.Clear();shamanRootAges.Clear();shamanRootStarted.Clear();demonHunterRootSupport.Clear();deathKnightPairs.Clear();deathKnightAges.Clear();deathKnightSourceAges.Clear();deathKnightTokenAudioAtMap.Clear();deathKnightSampleFrames.Clear();sourceAreas.Clear();active.Clear();actorBindings.Clear();slots.Clear();programs.Clear();AssetRegistry.ReleaseTransient(composite);composite=null;AssetRegistry.ReleaseTransient(affineComposite);affineComposite=null;}
}
