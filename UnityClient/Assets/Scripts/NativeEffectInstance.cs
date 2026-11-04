using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;

// Original hierarchy and particle modules, with a bounded compatibility FSM.
// Unknown custom actions remain in the audit; their original code is not executed.
public sealed class NativeEffectInstance : IDisposable
{
    public static Action<string,string,float> H10TimingDiagnostic;
    public struct SubtreeFrame{public Vector3 center;public float orthographicSize,aspect;}
    sealed class CameraShakerInvocation{public Camera camera;public NativeCameraShakerData data;public float due;public bool fired;}
    sealed class MachineClock{public NativeMachine machine;public NativeState state;public float entered,finish,otherFinish,colorSampleAt,pendingAt=float.NaN;public int transitions;public string pending;
        public readonly Dictionary<NativeAction,NativeColorAnimation> colorAnimations=new Dictionary<NativeAction,NativeColorAnimation>();
        public readonly Dictionary<NativeAction,NativeColorInterpolation> colorInterpolations=new Dictionary<NativeAction,NativeColorInterpolation>();
        public readonly Dictionary<NativeAction,AudioFadeWatch> audioFades=new Dictionary<NativeAction,AudioFadeWatch>();
        public readonly Dictionary<NativeAction,NativeMainShakeAction> mainShakes=new Dictionary<NativeAction,NativeMainShakeAction>();
        public readonly Dictionary<NativeAction,NativeEaseAnimation> easeAnimations=new Dictionary<NativeAction,NativeEaseAnimation>();
        public readonly List<AnimationWatch> animationWatches=new List<AnimationWatch>();
        public readonly List<TweenClock> tweenWatches=new List<TweenClock>();
        public readonly List<WaitWatch> waitWatches=new List<WaitWatch>();
        public readonly HashSet<NativeAction> finishedFloats=new HashSet<NativeAction>();
        public readonly Dictionary<NativeAction,bool> delayedVisibility=new Dictionary<NativeAction,bool>();
        public readonly List<RandomWaitWatch> randomWaits=new List<RandomWaitWatch>();
        public readonly List<ClipAudioWatch> clipAudio=new List<ClipAudioWatch>();
        public readonly List<CardIdAudioWatch> cardIdAudio=new List<CardIdAudioWatch>();
        public readonly List<AudioWatch> playthrough=new List<AudioWatch>();
        public readonly List<OrdinaryAudioWatch> ordinaryAudio=new List<OrdinaryAudioWatch>();
        public readonly Dictionary<string,float> floats=new Dictionary<string,float>();public readonly Dictionary<string,Color> colors=new Dictionary<string,Color>();
        public readonly Dictionary<string,Vector3> vectors=new Dictionary<string,Vector3>();
        public readonly Dictionary<string,GameObject> objects=new Dictionary<string,GameObject>();
        public readonly Dictionary<string,Texture> textures=new Dictionary<string,Texture>();
        public readonly List<(GameObject,string)> exitAnimationStops=new List<(GameObject,string)>();
        public readonly List<Dictionary<Renderer,bool>> visibilityReset=new List<Dictionary<Renderer,bool>>();
        public readonly List<Dictionary<NativeActorPresentation,bool>> actorVisibilityReset=new List<Dictionary<NativeActorPresentation,bool>>();}
    public int DelayedVisibilityStarts{get;private set;}
    public int DelayedVisibilityFinishes{get;private set;}
    public int UnboundDelayedVisibilityActions{get;private set;}
    public int ColorInterpolationStarts{get;private set;}
    public int ColorInterpolationFinishes{get;private set;}
    public int UnboundColorInterpolationActions{get;private set;}
    sealed class TrackBinding{public NativeAnimationTrack track;public Transform transform;public Renderer renderer;public Material[] materials;}
    sealed class RotationBinding{public Transform transform;public TrackBinding[] tracks;}
    sealed class ClipClock{public NativeAnimation clip;public GameObject owner;public int node,wrapMode,layer,eventCursor;public string nativeKey;public float entered,speed,offset,fadeStarted,fadeDuration,weightFrom=1,weightTo=1;public bool loop,animator,sourceEvents;public TrackBinding[] bindings;public RotationBinding[] rotations;
        public float Weight(float time){return fadeDuration<=0?weightTo:Mathf.Lerp(weightFrom,weightTo,Mathf.Clamp01((time-fadeStarted)/fadeDuration));}}
    sealed class AnimationWatch{public NativeAction action;public ClipClock clip;public float enteredTime;public bool finished;}
    sealed class WaitWatch{public NativeAction action;public float at;public bool finished;}
    sealed class RandomWaitWatch{public NativeAction action;public NativeRandomWait wait;public float sampledAt;}
    readonly NativeRandomWaitSampler randomWaitSampler=new NativeRandomWaitSampler(117);
    public int RandomWaitStarts {get;private set;}
    public int RandomWaitFinishes {get;private set;}
    public int UnboundRandomWaitActions {get;private set;}
    sealed class AudioWatch{public NativeAction action;public MachineClock clock;public NativePlaythroughDelay delay;public string key;public bool finished,sampled;public float sampledAt;}
    sealed class OrdinaryAudioWatch{public NativeAction action;public MachineClock clock;public NativePlaythroughDelay delay;public string key;public bool played,finished,sampled;public float sampledAt;public GameObject owner;}
    readonly List<OrdinaryAudioWatch> ordinaryAudioCoroutines=new List<OrdinaryAudioWatch>();
    readonly Dictionary<NativeAction,NativePlaythroughDelay> ordinaryAudioTimers=new Dictionary<NativeAction,NativePlaythroughDelay>();
    readonly Dictionary<GameObject,NativeAudioCue> sourceAudioCues=new Dictionary<GameObject,NativeAudioCue>();
    public int OrdinaryAudioStarts{get;private set;}
    public int OrdinaryAudioFinishes{get;private set;}
    public int MissingAudioPlaybackServices{get;private set;}
    public int UnhandledActions{get;private set;}
    readonly Dictionary<string,int> actionExecutions=new Dictionary<string,int>();
    public IReadOnlyDictionary<string,int> ActionExecutions{get{return actionExecutions;}}
    readonly List<string> unhandledActionDetails=new List<string>();
    public IReadOnlyList<string> UnhandledActionDetails{get{return unhandledActionDetails;}}
    readonly Dictionary<NativeAction,NativePlaythroughDelay> playthroughTimers=new Dictionary<NativeAction,NativePlaythroughDelay>();
    readonly Dictionary<GameObject,float> playthroughVolumes=new Dictionary<GameObject,float>();
    readonly Dictionary<GameObject,int> audioStopVersions=new Dictionary<GameObject,int>();
    readonly HashSet<GameObject> generatedAudioOwners=new HashSet<GameObject>();
    readonly HashSet<GameObject> destroyedAudioOwners=new HashSet<GameObject>();
    readonly HashSet<GameObject> loopingAudioOwners=new HashSet<GameObject>();
    public int LoopingAudioStarts{get;private set;}
    public int GeneratedAudioOwnerRemovals{get;private set;}
    public int MainShakeStarts{get;private set;}
    public int MainShakeFinishes{get;private set;}
    public int UnboundMainShakeActions{get;private set;}
    public Action<Vector3,float> MainCameraShake;
    readonly List<CameraShakerInvocation> cameraShakerInvocations=new List<CameraShakerInvocation>();
    public int CameraShakerStarts{get;private set;}
    public int PendingCameraShakerInvocations{get{return cameraShakerInvocations.Count;}}
    public Action<Camera,NativeCameraShakerData> CameraShakerPlayback;
    sealed class AudioFadeWatch{public GameObject owner;public NativeAudioFade fade;public bool sourceExists;}
    readonly Func<float> audioRealtime;
    readonly bool deferEntryEvents;
    public int AudioFadeStarts{get;private set;}
    public int AudioFadeFinishes{get;private set;}
    public int UnboundAudioFadeActions{get;private set;}
    public int AudioSourceStops{get;private set;}
    public int UnboundAudioStopActions{get;private set;}
    public int AudioVolumeWrites{get;private set;}
    public int UnboundAudioVolumeActions{get;private set;}
    public int PlaythroughStarts{get;private set;}
    public int PlaythroughFinishes{get;private set;}
    sealed class ClipAudioWatch{public NativeAction action;public NativeState state;public MachineClock clock;public NativeSoundAsset sound;public NativeSoundAsset[] sounds;public float[] weights;public NativeClipAudioDelay delay;public float sampledAt;public bool sampled,unbound;public string key;}
    sealed class CardIdAudioWatch{public NativeAction action;public MachineClock clock;public NativeClipAudioDelay delay;public NativeSoundAsset sound;public NativeAudioSourceValues source;public int sourceCategory;public GameObject owner;public NativeAudioCue cue;public float sampledAt;public bool sampled,waitForFinish,emitted,finished,missingPlaybackReported;public string key;}
    readonly Dictionary<string,NativeSoundAsset> clipSounds=new Dictionary<string,NativeSoundAsset>();
    // PlayMakerFSM.DoCoroutine is owned by the component, not the current state.
    readonly List<ClipAudioWatch> clipAudioCoroutines=new List<ClipAudioWatch>();
    readonly HashSet<NativeAction> suppressedClipAudioActions=new HashSet<NativeAction>();
    readonly List<CardIdAudioWatch> cardIdAudioCoroutines=new List<CardIdAudioWatch>();
    public int ClipAudioStarts{get;private set;}
    public int ClipAudioFinishes{get;private set;}
    public int SuppressedClipAudioActionEntries{get;private set;}
    public int CardIdAudioStarts{get;private set;}
    public int CardIdAudioFinishes{get;private set;}
    sealed class TweenClock
    {
        public NativeAction action;public MachineClock clock;public NativeState state;public int node,ease;public GameObject owner;
        public float start,duration,alpha,speed;public bool fade,children,started,finished,registered=true,stop;
        public Vector3 from,to;public Material[] materials;public Color[] colors;
    }
    public readonly GameObject Root;
    public readonly NativeProgram Program;
    public readonly GameObject[] Nodes;
    readonly NativeAudioStagingFrame stagingFrame;
    public Vector3 StagingOrigin{get{return stagingFrame.Origin;}}
    public NativeAudioStagingFrame StagingFrame{get{return stagingFrame;}}
    public readonly ParticleSystem[] Particles;
    public int OriginalSpellType{get;private set;}
    public void SetSpellType(int type){OriginalSpellType=type;}
    long StartH10Timing()
    {
        return H10TimingDiagnostic!=null&&Program!=null&&
            (Program.reference==NativeDemonHunterHeroPowerPlayback.RootReference||
             Program.reference==NativeDemonHunterHeroPowerPlayback.StartReference||
             Program.reference==NativeDemonHunterHeroPowerPlayback.ImpactReference)
            ?Stopwatch.GetTimestamp():0;
    }
    void ReportH10Timing(string stage,long started)
    {if(started!=0)H10TimingDiagnostic?.Invoke(Program.reference,stage,(float)(Stopwatch.GetTimestamp()-started)*1000f/Stopwatch.Frequency);}
    readonly List<NativeSpellStatePlayback> sourceStates=new List<NativeSpellStatePlayback>();
    public int SourceStateAnimationStarts{get;private set;}
    public int UnboundSourceStateObjects{get;private set;}
    public int ActiveSourceSpellState{get;private set;}
    public bool SourceSpellFinished{get;private set;}
    public int SourceStateChanges{get;private set;}
    public bool SourceRootFsmCallbacks;
    public event Action<int> SourceStateStarted;
    // 原 Spell 克隆初始 activeState=None，但 Awake 不发送 None FSM 事件。
    public bool SourceRootFsmStartsAtEntry;
    public event Action<int,float> SourceStateFinished;
    public sealed class AnimationEventNotification{public string clip,function;public float time;public int node;public bool handled;}
    readonly List<AnimationEventNotification> animationEvents=new List<AnimationEventNotification>();
    public IReadOnlyList<AnimationEventNotification> AnimationEvents{get{return animationEvents;}}
    public int UnboundAnimationEvents{get;private set;}
    public int SourceParticleEvents{get;private set;}
    readonly NativeActorAttackWait sourceAttackWait=new NativeActorAttackWait();
    int sourcePresentationFrame;
    public bool SourceWaitingToAct{get{return sourceAttackWait.Waiting;}}
    public int SourceAttackActionsStarted{get;private set;}
    public int SourceAttackPreviousState{get;private set;}
    public float SourceAttackStartedTime{get;private set;}
    public int SourceEventAudioStarts{get;private set;}
    public int UnrestoredSourceActionSideEffects{get;private set;}
    NativeSpellTransform sourcePlacement;
    int sourcePlacementNode;
    Func<int,GameObject> sourceLocations,sourceFacings;
    public int UnboundSourcePlacements{get{return sourcePlacement==null?0:sourcePlacement.Unresolved;}}
    public int SourcePositionUpdates{get{return sourcePlacement==null?0:sourcePlacement.PositionUpdates;}}
    // A source placement may parent an owned node outside the staging root.
    void ReleaseDetachedSourceNodes()
    {foreach(var node in Nodes)if(node!=null&&!node.transform.IsChildOf(Root.transform)){node.SetActive(false);AssetRegistry.ReleaseTransient(node);}}
    public void BindSourcePlacement(Func<int,GameObject> locations,Func<int,GameObject> facings=null)
    {
        if(last>=0)throw new InvalidOperationException("Bind original placement before playback");
        sourceLocations=locations;sourceFacings=facings;
    }
    public void BindSourcePlacementRoot(int node)
    {
        if(last>=0||sourcePlacement!=null||node<0||node>=Nodes.Length||
           !(Program.spell_transforms??new NativeSourceSpellTransform[0]).Any(transform=>transform.node==node&&transform.enabled))
            throw new InvalidOperationException("Original Spell placement root must be selected before playback");
        sourcePlacementNode=node;
    }
    NativeSpellTransform RootPlacement()
    {
        if(sourcePlacement!=null)return sourcePlacement;
        var data=(Program.spell_transforms??new NativeSourceSpellTransform[0]).SingleOrDefault(s=>s.node==sourcePlacementNode&&s.enabled);
        if(data==null)throw new NotSupportedException("Original root Spell placement is unrestored");
        sourcePlacement=new NativeSpellTransform(data.Copy(),Nodes[sourcePlacementNode].transform,sourceLocations,sourceFacings);return sourcePlacement;
    }
    public void SetOriginalPosition(Vector3 value){if(last<0)throw new InvalidOperationException("Initialize dormant source Spell first");RootPlacement().SetPosition(value);}
    public void SetOriginalLocation(int location,bool parent){RootPlacement().SetLocationFields(location,parent);}
    public void SetOriginalFacing(int facing,bool rotateX,bool rotateY){RootPlacement().SetFacingFields(facing,rotateX,rotateY);}
    public void UpdateOriginalPosition(){RootPlacement().UpdatePosition();}
    public void UpdateOriginalOrientation(){RootPlacement().UpdateOrientation();}
    void UpdateSourcePlacement(bool action=false)
    {
        if(!(Program.spell_transforms??new NativeSourceSpellTransform[0]).Any(s=>s.node==sourcePlacementNode&&s.enabled)){UnrestoredSourceActionSideEffects++;return;}
        var placement=RootPlacement();
        if(action&&placement.Kind=="SuperSpell")
        {
            // Source SuperSpell.OnAction refreshes these dynamic selectors only.
            if(placement.Location==18)placement.InvalidatePosition();
            if(placement.Facing==9)placement.InvalidateOrientation();
        }
        placement.Update();
    }
    void SourceBaseAction(int previous,float time)
    {
        SourceAttackActionsStarted++;SourceAttackPreviousState=previous;SourceAttackStartedTime=time;
        UpdateSourcePlacement();
        if(sourcePlacement!=null&&sourcePlacement.Unresolved>0)UnrestoredSourceActionSideEffects++;
    }
    GameObject StateAnimationOwner(NativeSpellStateAnimation data)
    {
        if(data.target==0)return data.node>=0&&data.node<Nodes.Length?Nodes[data.node]:null;
        var actor=NativeActorIdentity.FindInThisOrParents(Root);
        return actor==null?null:data.target==1?actor.gameObject:data.target==2?actor.RootObject:null;
    }
    void PlaySourceState(NativeSourceSpellState state,float time)
    {
        if(state.audio_count!=0){UnboundSourceStateObjects+=state.audio_count;}
        foreach(var data in state.animations)
        {
            if(!data.enabled)continue;
            var owner=StateAnimationOwner(data);
            if(data.unresolved||owner==null){UnboundSourceStateObjects++;continue;}
            if(!data.clip_null)
            {
                var clip=Program.animations.FirstOrDefault(c=>c.id==data.clip);
                if(clip==null){UnboundSourceStateObjects++;continue;}
                var bindings=clip.tracks.Select(t=>BindTrack(t,owner)).ToArray();
                if(!Nodes.Contains(owner)&&!BorrowAnimation(owner,bindings)){UnboundSourceStateObjects++;continue;}
                bool cross=!Mathf.Approximately(data.crossfade,0);
                var playing=cross?clips.FirstOrDefault(c=>c.owner==owner&&!c.animator&&c.clip.id==clip.id):null;
                if(cross)foreach(var old in clips.Where(c=>c.owner==owner&&!c.animator&&c.layer==data.layer&&c!=playing))Fade(old,time,data.crossfade,0);
                else clips.RemoveAll(c=>c.owner==owner&&!c.animator&&c.layer==data.layer);
                if(playing==null)
                {
                    int node=Array.IndexOf(Nodes,owner);
                    var actor=NativeActorIdentity.FindInThisOrParents(owner);
                    var sourceNode=node>=0?Program.nodes[node]:actor!=null&&actor.AnimationProgram!=null?actor.AnimationProgram.nodes.FirstOrDefault(n=>n.path==(owner==actor.gameObject?"":owner.name)):null;
                    var component=sourceNode==null?null:sourceNode.legacy_animation;
                    int wrap=NativeLegacyWrap.Resolve(clip.wrap_mode,component!=null&&component.wrap_known?component.wrap_mode:0);
                    playing=new ClipClock{clip=clip,owner=owner,node=Array.IndexOf(Nodes,owner),layer=data.layer,sourceEvents=true,
                        wrapMode=wrap,entered=time,speed=data.speed,loop=wrap==2||wrap==4,bindings=bindings,
                        rotations=bindings.Where(b=>b.transform!=null&&b.track.property.StartsWith("rotation.")).GroupBy(b=>b.transform).Select(g=>new RotationBinding{transform=g.Key,tracks=g.ToArray()}).ToArray(),
                        weightFrom=cross?0:1,weightTo=cross?0:1};clips.Add(playing);
                }
                playing.speed=data.speed;if(cross)Fade(playing,time,data.crossfade,1);
                SourceStateAnimationStarts++;
            }
            if(data.particles&&owner.GetComponent<ParticleSystem>()!=null)
                foreach(var particle in owner.GetComponentsInChildren<ParticleSystem>(true))
                {int node=Array.IndexOf(Particles,particle);if(node>=0){particleStart[node]=time;particleStop.Remove(node);}}
        }
    }
    void StopSourceState(NativeSourceSpellState state,IReadOnlyList<NativeSourceSpellState> next)
    {
        foreach(var data in state.animations)
        {
            if(next==null&&!data.enabled)continue;
            var owner=StateAnimationOwner(data);if(owner==null)continue;
            bool preserve=next!=null&&next.Any(s=>s.animations.Any(a=>a.enabled&&a.layer==data.layer&&StateAnimationOwner(a)==owner));
            if(!data.clip_null&&!preserve)clips.RemoveAll(c=>c.owner==owner&&!c.animator&&c.clip.id==data.clip);
            if(data.particles&&owner.GetComponent<ParticleSystem>()!=null)
                foreach(var particle in owner.GetComponentsInChildren<ParticleSystem>(true))
                {int node=Array.IndexOf(Particles,particle);if(node>=0)particleStop[node]=Mathf.Max(0,last);}
        }
    }
    // Explicit source-state selection; does not synthesize missing SpellController events.
    public void SelectSourceSpellState(int type,float time)
    {
        Seek(time);
        ChangeSourceSpellState(type,time,true);
        SampleClips(time);
    }
    bool HasSourceSpellState(int type){return type!=0&&(sourceStates.Any(s=>s.Source.type==type)||machines.Any(m=>m.machine.owner_known&&m.machine.owner==0&&NativeSpellLifecycle.Destination(m.machine,(NativeSpellStateType)type)!=null));}
    public void ChangeInitializedSourceState(int type,float time)
    {
        if(last<0||time<last)throw new InvalidOperationException("Initialize source playback and preserve callback time");
        ChangeSourceSpellState(type,time,true);SampleClips(time);
    }
    void FinishSourceSpell(float time)
    {
        if(SourceSpellFinished)return;SourceSpellFinished=true;
        finishNotifications.Add(new FinishNotification{time=time,node=0,machine=-1});
    }
    void ChangeSourceSpellState(int type,float time,bool sendFsm)
    {
        if(type<0||type>6)throw new ArgumentOutOfRangeException("type");
        int previous=ActiveSourceSpellState;ActiveSourceSpellState=type;SourceStateChanges++;
        if(type==0)FinishSourceSpell(time);
        var next=sourceStates.Where(s=>s.Source.type==type).Select(s=>s.Source).ToArray();
        if(previous!=0)
        {
            foreach(var state in sourceStates.Where(s=>s.Source.type==previous))state.Stop(next);
            if(SourceStateFinished!=null)SourceStateFinished(previous,time);
        }
        else if(type!=0)SourceSpellFinished=false;
        foreach(var state in sourceStates.Where(s=>s.Source.type==type))state.Play(time);
        if((Program.animation_event_receivers??new NativeAnimationEventReceiver[0]).Any(r=>r.node==sourcePlacementNode&&r.kind=="ActorAttackSpell"))
        {
            if(type==1){sourceAttackWait.Birth();UpdateSourcePlacement();}
            else if(type==3)sourceAttackWait.Begin(previous,sourcePresentationFrame,time,SourceBaseAction);
        }
        else if(type==1||type==3)
        {
            var root=(Program.spell_transforms??new NativeSourceSpellTransform[0]).SingleOrDefault(s=>s.node==sourcePlacementNode&&s.enabled);
            if(root!=null&&(root.kind=="Spell"||root.kind=="SuperSpell"))
            {
                UpdateSourcePlacement(type==3);
                // Target updates, spawn/impact/path and state callbacks are separate
                // SuperSpell side effects, not completed by positioning the root.
                if(root.kind=="SuperSpell")UnrestoredSourceActionSideEffects++;
            }
        }
        if(SourceStateStarted!=null)SourceStateStarted(type);
        if(sendFsm&&ActiveSourceSpellState==type)
            foreach(var clock in machines.Where(m=>m.machine.owner_known&&m.machine.owner==0).ToArray())
            {var destination=NativeSpellLifecycle.Destination(clock.machine,(NativeSpellStateType)type);if(destination!=null)Enter(clock,destination.state,time);}
    }
    void ProcessAnimationEvents(float age)
    {
        foreach(var clip in clips.Where(c=>c.sourceEvents&&c.clip.events!=null).ToArray())
        {
            if(!clips.Contains(clip)||clip.owner==null||!clip.owner.activeInHierarchy)continue;
            while(clip.eventCursor<clip.clip.events.Length)
            {
                var data=clip.clip.events[clip.eventCursor];
                if(clip.speed>0&&clip.entered+(data.time-clip.offset)/clip.speed>age)break;
                clip.eventCursor++;
                float time=clip.speed>0?clip.entered+(data.time-clip.offset)/clip.speed:age;
                bool handled=clip.speed>0&&!clip.loop&&DispatchAnimationEvent(clip,data,time);
                if(!handled)UnboundAnimationEvents++;
                animationEvents.Add(new AnimationEventNotification{clip=clip.clip.id,function=data.function,time=time,node=clip.node,handled=handled});
                // Source callbacks can replace the currently sampled animation/state.
                if(!clips.Contains(clip)||!clip.owner.activeInHierarchy)break;
            }
        }
    }
    bool DispatchAnimationEvent(ClipClock clip,NativeAnimationEvent data,float time)
    {
        if(clip.node<0)return false;
        var receivers=(Program.animation_event_receivers??new NativeAnimationEventReceiver[0]).Where(r=>r.node==clip.node).ToArray();
        if(data.function=="StopWaitingToAct"&&receivers.Any(r=>r.kind=="ActorAttackSpell"))
        {sourceAttackWait.StopWaiting();return true;}
        if(data.function=="PlayDefaultSound"&&receivers.Any(r=>r.kind=="SelfAnimUtils"))
        {
            var cue=NativeAudio.ResolveExistingSource(eventAudioDefinition,Program.nodes[clip.node].path,time,()=> (float)audioRandom.NextDouble());
            if(cue==null||!System.IO.File.Exists(System.IO.Path.Combine(assets.Root,cue.file)))return false;
            cue.sourceName=clip.owner.name;cue.restartKey=Root.GetInstanceID()+":existing-source:"+clip.node;
            cue.sourceParent=clip.owner.transform;
            audioCues.Add(cue);SourceEventAudioStarts++;
            string key="animation-audio:"+clip.node+":"+clip.clip.id+":"+clip.eventCursor+":"+clip.entered.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            if(deliveredAudio.Add(key)&&audioCallback!=null)audioCallback(cue);
            return true;
        }
        if(receivers.Any(r=>r.kind=="SpellState"))
        {
            if(data.function=="OnStateFinished")
            {int next=(int)NativeSpellLifecycle.Next((NativeSpellStateType)ActiveSourceSpellState,t=>HasSourceSpellState((int)t));ChangeSourceSpellState(next,time,true);return true;}
            if(data.function=="OnSpellFinished"){FinishSourceSpell(time);return true;}
            if(data.function=="OnChangeState"){ChangeSourceSpellState(data.integer,time,true);return true;}
        }
        var particles=receivers.FirstOrDefault(r=>r.kind=="PlayNewParticles");
        if(particles!=null&&(data.function=="PlayNewParticles3"||data.function=="PlayNewParticles3andChilds"))
        {
            string name=data.function=="PlayNewParticles3"?"m_Target":"m_Target2";
            var target=particles.targets.FirstOrDefault(t=>t.name==name);if(target==null)return false;
            if(target.object_null)return true;
            if(target.node<0||target.node>=Nodes.Length||Particles[target.node]==null)return false;
            foreach(var particle in Nodes[target.node].GetComponentsInChildren<ParticleSystem>(true))
            {int node=Array.IndexOf(Particles,particle);if(node>=0){particleStart[node]=time;particleStop.Remove(node);}}
            SourceParticleEvents++;return true;
        }
        return false;
    }
    public void ShowSourceSpellStates(float time){Seek(time);foreach(var state in sourceStates)state.Show(time);SampleClips(time);}
    public void HideSourceSpellStates(){foreach(var state in sourceStates)state.Hide();}
    public void AttachToOriginalParent(GameObject parent)
    {
        if(parent==null||last>=0)throw new InvalidOperationException("Original Spell attachment requires a parent before playback");
        Root.transform.SetParent(parent.transform,false);Root.transform.localPosition=Vector3.zero;
        foreach(var material in materialSources.Keys)material.SetVector("_Origin",Root.transform.position);
    }
    public void ReanchorStagingRoot(Vector3 origin)
    {
        if(Root==null||stagingFrame==null)throw new ObjectDisposedException("NativeEffectInstance");
        Root.transform.position=origin;stagingFrame.SetOrigin(origin);
    }
    public int TransitionLimitHits{get;private set;}
    public int UnboundCardTypeActions{get;private set;}
    public int UnboundTextureActions{get;private set;}
    public int TextureBindings{get;private set;}
    public int PortraitTextureBindings{get;private set;}
    public int UnboundPortraitTextureActions{get;private set;}
    public int CustomSpawnInitializations{get;private set;}
    public int CustomSpawnFinalizations{get;private set;}
    public int UnboundCustomSpawnActions{get;private set;}
    public Func<int,NativeAction,string> SpellPlayerSideEventResolver;
    public int SpellPlayerSideEventDispatches{get;private set;}
    public int UnboundSpellPlayerSideEventActions{get;private set;}
    readonly NativeCustomSpawn customSpawn=new NativeCustomSpawn();
    sealed class TimelineClock{public NativeTimeline data;public float entered;public bool finished;public readonly List<Tuple<NativeTimelineClip,ClipClock>> clips=new List<Tuple<NativeTimelineClip,ClipClock>>();public readonly List<TimelineControlClock> controls=new List<TimelineControlClock>();public readonly List<TimelineShakeClock> shakes=new List<TimelineShakeClock>();public readonly List<TimelineSoundClock> sounds=new List<TimelineSoundClock>();}
    sealed class TimelineShakeClock{public NativeTimelineClip clip;public NativeTimelineShaker helper;public Transform target;}
    sealed class TimelineSoundClock{public NativeTimeline timeline;public NativeTimelineTrack track;public NativeTimelineClip clip;public GameObject owner;public int node;public bool playing,fired;public string key;}
    Camera timelineCamera;
    public bool TimelineShakeEnabled=true;
    public int UnboundTimelineCameras{get;private set;}
    public void BindTimelineCamera(Camera target)
    {
        if(last>=0)throw new InvalidOperationException("Original Timeline camera must be bound before playback");
        timelineCamera=target;
    }
    sealed class TimelineControlClock
    {
        public NativeTimelineClip clip;public GameObject target;public bool initialActive,playing;public NativeRandomizedParticleClock[] particles;
        public ParticleSystem[] seeds;public bool[] autoSeeds;
    }
    readonly List<TimelineClock> timelines=new List<TimelineClock>();
    readonly HashSet<int> timelineParticles=new HashSet<int>();
    System.Random timelineRandom=new System.Random(117);
    public int TimelineStarts{get;private set;}
    public int TimelineSoundStarts{get;private set;}
    public int UnboundTimelineSounds{get;private set;}
    public int UnboundTimelineActions{get;private set;}
    public int UnsupportedTimelineTracks{get;private set;}
    public int UnboundScaleActions{get;private set;}
    public int UnboundComponentActions{get;private set;}
    public int UnboundColorActions{get;private set;}
    public int RGBAWrites{get;private set;}
    public int PropertyWrites{get;private set;}
    public int UnboundPropertyActions{get;private set;}
    readonly Dictionary<Tuple<UnityEngine.Object,string>,Action> propertyRestore=new Dictionary<Tuple<UnityEngine.Object,string>,Action>();
    readonly Dictionary<NativeAction,Color> rgbaLocalColors=new Dictionary<NativeAction,Color>();
    public int UnboundEaseActions{get;private set;}
    public int UnboundFinishActions{get;private set;}
    public int UnboundAnimationActions{get;private set;}
    public int UnboundAudioActions{get;private set;}
    readonly List<AudioWatch> audioWatches=new List<AudioWatch>();
    readonly HashSet<string> deliveredAudio=new HashSet<string>();
    readonly List<NativeAudioCue> audioCues=new List<NativeAudioCue>();
    public IReadOnlyList<NativeAudioCue> AudioCues{get{return audioCues;}}
    readonly NativeAudioDefinition audioDefinition;
    readonly Action<NativeAudioCue> audioCallback;
    readonly NativeAudioSourceValues clipAudioTemplate;
    readonly Dictionary<string,NativeAudioTemplateAsset> clipAudioTemplates=new Dictionary<string,NativeAudioTemplateAsset>();
    System.Random audioRandom;
    bool disposed;
    public int StoppedOnExitClips{get;private set;}
    public int CompletedAnimationActions{get;private set;}
    public int UnboundITweenActions{get;private set;}
    public int CompletedITweenActions{get;private set;}
    public int StoppedITweens{get;private set;}
    public int UnboundActorActions{get;private set;}
    public int UnboundActorInitializationActions{get;private set;}
    public int UnboundEliteActions{get;private set;}
    public int UnboundSetParentActions{get;private set;}
    public int UnboundDestroyObjectActions{get;private set;}
    public int SetParentActions{get;private set;}
    public int DestroyObjectActions{get;private set;}
    public int EliteEvents{get;private set;}
    public int UnboundMachineOwnerActions{get;private set;}
    public int ActorObjectBindings{get;private set;}
    public int UnboundActorVisibilityActions{get;private set;}
    public int ActorRootVisibilityBindings{get;private set;}
    public int UnrestoredActorVisibilitySideEffects{get;private set;}
    public int LifecycleChanges{get;private set;}
    public int UnboundLifecycleEvents{get;private set;}
    NativeLifecycleEvent[] lifecycleEvents=new NativeLifecycleEvent[0];
    int lifecycleCursor;
    public sealed class FinishNotification{public float time;public int machine,node;public bool delivered;}
    readonly List<FinishNotification> finishNotifications=new List<FinishNotification>();
    public IReadOnlyList<FinishNotification> FinishNotifications{get{return finishNotifications;}}
    public string[] CurrentStates{get{return machines.Select(m=>m.state==null?null:m.state.name).ToArray();}}
    public GameObject MachineOwner(int index)
    {
        if(index<0||index>=Program.machines.Length)return null;
        var machine=Program.machines[index];return machine.owner_known&&machine.owner>=0&&machine.owner<Nodes.Length?Nodes[machine.owner]:null;
    }
    public int ParticleCount{get{return Particles.Where(p=>p!=null).Sum(p=>p.particleCount);}}
    public int H10SameAgeSeekSkips{get;private set;}
    readonly List<Material> materials=new List<Material>();
    readonly Dictionary<Material,HashSet<string>> declaredProperties=new Dictionary<Material,HashSet<string>>();
    readonly Dictionary<Material,NativeMaterial> materialSources=new Dictionary<Material,NativeMaterial>();
    readonly Dictionary<Material,Dictionary<string,Texture>> initialTextures=new Dictionary<Material,Dictionary<string,Texture>>();
    readonly Dictionary<Material,float> initialTextureFlags=new Dictionary<Material,float>();
    readonly AssetRegistry assets;
    readonly Renderer[] renderers;
    readonly Material[][] nodeMaterials;
    Material[][] initialNodeMaterials;
    readonly NativeGeometryBindings geometry;
    readonly Dictionary<string,Material> replacementMaterials=new Dictionary<string,Material>();
    public int UnboundGeometryActions{get;private set;}
    public int MeshReplacements{get;private set;}
    public int MaterialReplacements{get;private set;}
    readonly bool[] deferred;
    readonly Dictionary<Renderer,bool> desiredVisibility=new Dictionary<Renderer,bool>();
    readonly List<MachineClock> machines=new List<MachineClock>();
    readonly List<ClipClock> clips=new List<ClipClock>();
    readonly List<TweenClock> tweens=new List<TweenClock>();
    readonly Dictionary<Transform,Vector3> borrowedScales=new Dictionary<Transform,Vector3>();
    readonly Dictionary<NativeActorPresentation,bool> borrowedActorVisibility=new Dictionary<NativeActorPresentation,bool>();
    readonly Dictionary<Renderer,bool> borrowedRendererVisibility=new Dictionary<Renderer,bool>();
    readonly Dictionary<GameObject,Animation> legacyMixers=new Dictionary<GameObject,Animation>();
    sealed class BorrowedPose{public Vector3 position,scale;public Quaternion rotation;}
    readonly Dictionary<Transform,BorrowedPose> borrowedAnimationPoses=new Dictionary<Transform,BorrowedPose>();
    static readonly Dictionary<GameObject,NativeEffectInstance> borrowedAnimationOwners=new Dictionary<GameObject,NativeEffectInstance>();
    readonly HashSet<GameObject> borrowedAnimationRoots=new HashSet<GameObject>();
    readonly Dictionary<string,AnimationClip> legacyClips=new Dictionary<string,AnimationClip>();
    readonly Dictionary<(Material,string),Vector2> materialMix=new Dictionary<(Material,string),Vector2>();
    readonly Dictionary<Renderer,Vector2> visibilityMix=new Dictionary<Renderer,Vector2>();
    readonly List<NativeEffectCapture> captures=new List<NativeEffectCapture>();
    readonly List<NativeGeneratedCapture> generatedCaptures=new List<NativeGeneratedCapture>();
    public IEnumerable<NativeGeneratedCapture> GeneratedCaptures{get{return generatedCaptures;}}
    public int UnrestoredGeneratedCaptures{get;private set;}
    readonly List<NativeOffscreenFX> offscreenFX=new List<NativeOffscreenFX>();
    public IReadOnlyList<NativeOffscreenFX> OffscreenFX{get{return offscreenFX;}}
    readonly Dictionary<int,float> particleStart=new Dictionary<int,float>();
    readonly Dictionary<int,float> particleStop=new Dictionary<int,float>();
    readonly Dictionary<int,Dictionary<string,float>> animationSpeed=new Dictionary<int,Dictionary<string,float>>();
    readonly Camera camera;
    readonly List<NativeTrailPlayback> trails=new List<NativeTrailPlayback>();
    readonly RenderTexture output;
    readonly Material captureAlpha;
    readonly Func<Color?> boardAmbient;
    readonly NativeSpellContext spellContext;
    readonly NativeAudioDefinition eventAudioDefinition;
    NativeEffectGlowCapture glowCapture;
    NativeAffineCapture affineCapture;
    public RenderTexture Transmission{get{return affineCapture==null?null:affineCapture.Transmission;}}
    public Camera PreviewCamera{get{return camera;}}
    public void BindCaptureView(NativeSourceCamera view,Vector3 sourceOrigin,Vector3 destinationOrigin)
    {
        if(last>=0)throw new InvalidOperationException("Bind original capture camera before playback");
        view.Apply(camera,sourceOrigin,destinationOrigin,(float)output.width/output.height);
        // A gameplay view belongs to the scene, not a moving Spell/Actor parent.
        camera.transform.SetParent(null,true);
    }
    float last=-1;
    public NativeEffectInstance(NativeProgram program,AssetRegistry assets,int slot=0,int width=512,int height=512,Func<Color?> boardAmbientColor=null,NativeSpellContext context=null,Action<NativeAudioCue> audioCallback=null,Func<float> audioRealtime=null,string[] sourceSimulationSpaceNodes=null,bool deferEntryEvents=false)
    {
        boardAmbient=boardAmbientColor;
        this.assets=assets;
        geometry=new NativeGeometryBindings(RegisterGeometryMaterial,ReleaseGeometryMaterial);
        spellContext=context;
        this.audioCallback=audioCallback;
        this.audioRealtime=audioRealtime??(()=>Time.realtimeSinceStartup);
        this.deferEntryEvents=deferEntryEvents;
        string audioConfig=System.IO.Path.Combine(assets.Root,"native-audio-config.json");
        if(System.IO.File.Exists(audioConfig))clipAudioTemplate=JsonUtility.FromJson<NativeAudioConfig>(System.IO.File.ReadAllText(audioConfig)).values;
        foreach(var param in program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayClipAction"||a.name=="AudioPlayRandomClipAction").Select(a=>a.Param("m_TemplateSource")))
            if(!param.unresolved&&!param.object_null&&!String.IsNullOrEmpty(param.asset_path)&&!clipAudioTemplates.ContainsKey(param.asset_path))
                clipAudioTemplates.Add(param.asset_path,JsonUtility.FromJson<NativeAudioTemplateAsset>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,param.asset_path))));
        foreach(var param in program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayClipAction"||a.name=="AudioPlaythroughAction"||a.name=="AudioPlayAction").Select(a=>a.Param(a.name=="AudioPlayClipAction"?"m_Sound":"m_OneShotSound")))
            if(!param.unresolved&&!String.IsNullOrEmpty(param.asset_path)&&!clipSounds.ContainsKey(param.asset_path))
                clipSounds.Add(param.asset_path,JsonUtility.FromJson<NativeSoundAsset>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,param.asset_path))));
        foreach(var param in program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayRandomClipAction")
            .SelectMany(a=>ArrayParameters(a,"m_Sounds")).Where(p=>p.asset_type=="SoundDef"))
            if(!param.unresolved&&!String.IsNullOrEmpty(param.asset_path)&&!clipSounds.ContainsKey(param.asset_path))
                clipSounds.Add(param.asset_path,JsonUtility.FromJson<NativeSoundAsset>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,param.asset_path))));
        foreach(var param in program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="SpellCardIdAudioPlayAction")
            .SelectMany(a=>ArrayParameters(a,"m_Sounds").Concat(new[]{a.Param("m_DefaultSound")})).Where(p=>p.asset_type=="SoundDef"))
            if(!param.unresolved&&!String.IsNullOrEmpty(param.asset_path)&&!clipSounds.ContainsKey(param.asset_path))
                clipSounds.Add(param.asset_path,JsonUtility.FromJson<NativeSoundAsset>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,param.asset_path))));
        if(!String.IsNullOrEmpty(program.audio_file))audioDefinition=JsonUtility.FromJson<NativeAudioDefinition>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,program.audio_file)));
        if(!String.IsNullOrEmpty(program.event_audio_file))eventAudioDefinition=JsonUtility.FromJson<NativeAudioDefinition>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,program.event_audio_file)));
        if(context!=null&&context.Placement!=null){sourceLocations=context.Placement.Location;sourceFacings=context.Placement.Facing;}
        Program=program;Root=new GameObject("NativeEffect");Root.transform.position=new Vector3(20000+slot*1000,10000,20000);stagingFrame=new NativeAudioStagingFrame(Root.transform.position);
        Nodes=new GameObject[program.nodes.Length];Particles=new ParticleSystem[Nodes.Length];
        renderers=new Renderer[Nodes.Length];nodeMaterials=new Material[Nodes.Length][];deferred=new bool[Nodes.Length];
        try
        {
        for(int i=0;i<Nodes.Length;i++)
        {
            var n=program.nodes[i];var go=new GameObject(n.name);Nodes[i]=go;
            // 保留原版 layer 21 Producer 隔离；其他源节点映射到特效相机专用层。
            go.layer=n.source_layer==21?21:30;go.transform.SetParent(n.parent<0?Root.transform:Nodes[n.parent].transform,false);
            // These branches feed original secondary-camera render targets.
            // Their masks must not be painted directly onto the board.
            deferred[i]=n.name=="Offscreen"||n.parent>=0&&deferred[n.parent];
            go.transform.localPosition=n.position;go.transform.localRotation=n.rotation;go.transform.localScale=n.scale;
            if(n.light!=null&&n.light.restored)n.light.Create(go);
            Renderer renderer=null;
            if(n.trail!=null&&n.trail.restored){var trail=new NativeTrailPlayback(n.trail,go);trails.Add(trail);renderer=trail.Renderer;}
            else if(n.particle!=null&&n.particle.restored){Particles[i]=n.particle.Create(go,assets,(uint)(117+i),sourceSimulationSpaceNodes!=null&&Array.IndexOf(sourceSimulationSpaceNodes,n.name)>=0);renderer=go.GetComponent<ParticleSystemRenderer>();}
            else if(n.skinned_renderer)
            {
                if(n.skin==null||String.IsNullOrEmpty(n.mesh))throw new InvalidOperationException("Skinned source renderer has incomplete mesh/bone metadata: "+n.path);
                var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=assets.MeshFile(n.mesh);
                skin.quality=(SkinQuality)n.skin.quality;skin.updateWhenOffscreen=n.skin.update_when_offscreen;renderer=skin;
            }
            else if(!String.IsNullOrEmpty(n.mesh)){go.AddComponent<MeshFilter>().sharedMesh=assets.MeshFile(n.mesh);renderer=go.AddComponent<MeshRenderer>();}
            nodeMaterials[i]=n.materials.Select(m=>CreateMaterial(m,assets)).ToArray();renderers[i]=renderer;
            if(renderer!=null){desiredVisibility[renderer]=n.visible;renderer.sortingOrder=n.sorting_order;}
            if(renderer!=null){renderer.sharedMaterials=nodeMaterials[i];renderer.enabled=!deferred[i]&&n.visible&&n.materials.Any(m=>m.textures.Length>0);}
            go.SetActive(n.active);
        }
        for(int i=0;i<Nodes.Length;i++)if(program.nodes[i].skinned_renderer)
        {
            var data=program.nodes[i].skin;var skin=renderers[i] as SkinnedMeshRenderer;
            if(skin==null||data.bones==null||data.bones.Length==0||data.bones.Any(index=>index<0||index>=Nodes.Length))
                throw new InvalidOperationException("Skinned source bone binding is incomplete: "+program.nodes[i].path);
            skin.bones=data.bones.Select(index=>Nodes[index].transform).ToArray();
            if(data.root_bone>=0)
            {
                if(data.root_bone>=Nodes.Length)throw new InvalidOperationException("Skinned source root bone is out of range: "+program.nodes[i].path);
                skin.rootBone=Nodes[data.root_bone].transform;
            }
        }
        for(int i=0;i<Nodes.Length;i++)if(Particles[i]!=null)
        {
            var data=program.nodes[i].particle;if(data.collision!=null&&data.collision.restored)data.collision.Apply(Particles[i],Nodes);
            if(data.empty_sub_emitters){var module=Particles[i].subEmitters;module.enabled=true;}
        }
        initialNodeMaterials=nodeMaterials.Select(m=>m.ToArray()).ToArray();
        foreach(var param in program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="SetMaterial").Select(a=>a.Param("material")))
            if(!String.IsNullOrEmpty(param.asset_path)&&!replacementMaterials.ContainsKey(param.asset_path))
                replacementMaterials.Add(param.asset_path,CreateMaterial(JsonUtility.FromJson<NativeMaterial>(System.IO.File.ReadAllText(System.IO.Path.Combine(assets.Root,param.asset_path))),assets));
        var cam=new GameObject("NativeEffectCamera");cam.transform.SetParent(Root.transform,false);camera=cam.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
        cam.transform.localPosition=new Vector3(0,100,0);cam.transform.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
        camera.orthographic=true;camera.orthographicSize=16;camera.nearClipPlane=.1f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        output=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);output.Create();camera.targetTexture=output;camera.aspect=(float)width/height;
        foreach(var capture in program.captures??new NativeCapture[0])
            if(capture.owner>=0&&capture.source>=0)
            {
                if(capture.target>=0)captures.Add(new NativeEffectCapture(capture,Nodes,r=>desiredVisibility.ContainsKey(r)&&desiredVisibility[r],m=>materialSources[m]));
                else if(NativeGeneratedCapture.Supports(capture))
                    generatedCaptures.Add(new NativeGeneratedCapture(capture,Nodes,r=>desiredVisibility.ContainsKey(r)&&desiredVisibility[r],m=>materialSources[m],m=>CreateMaterial(m,assets),
                        m=>{initialTextures[m]=m.GetTexturePropertyNames().ToDictionary(n=>n,n=>m.GetTexture(n));initialTextureFlags[m]=m.GetFloat("_HasSourceTexture");},
                        m=>{ReleaseGeometryMaterial(m);materials.Remove(m);AssetRegistry.ReleaseTransient(m);}));
                else if(capture.original_parameters!=null&&capture.original_parameters.m_CreateRenderPlane==1)
                    UnrestoredGeneratedCaptures++;
            }
        if(captures.Count>0){captureAlpha=new Material(Resources.Load<Shader>("NativeEffectAlpha"));materials.Add(captureAlpha);}
        foreach(var capture in program.offscreen_fx??new NativeOffscreenFXData[0])
            if(capture.enabled)offscreenFX.Add(new NativeOffscreenFX(capture,Nodes,(r,value)=>{desiredVisibility[r]=value;r.enabled=value;}));
        // Capture targets have already bound their RTs. Seeking must restore those
        // bindings as well as literal textures, without replacing them with PNGs.
        foreach(var material in materialSources.Keys)
        {initialTextures[material]=material.GetTexturePropertyNames().ToDictionary(name=>name,name=>material.GetTexture(name));initialTextureFlags[material]=material.GetFloat("_HasSourceTexture");}
        }
        catch{Dispose();throw;}
    }
    Material CreateMaterial(NativeMaterial source,AssetRegistry assets)
    {
        var mat=new Material(Resources.Load<Shader>("NativeEffect")){name=source.name};
        materialSources[mat]=source;
        if(source.render_queue>0)mat.renderQueue=source.render_queue;
        declaredProperties[mat]=new HashSet<string>(source.properties??new string[0]);
        foreach(var f in source.floats)if(OriginalProperty(mat,f.name))mat.SetFloat(f.name,f.value);
        foreach(var c in source.colors)if(OriginalProperty(mat,c.name))mat.SetColor(c.name,c.value);
        bool originalSmoke=source.shader=="Hero/Particle/FX Particle Additive Alpha 2Layer"||source.shader=="Custom/FX/FX_SneakySmoke";
        foreach(var t in source.textures)if(OriginalProperty(mat,t.name)){mat.SetTexture(t.name,originalSmoke?assets.SmokeTextureFile(t.path):assets.FireblastTextureFile(t.path));mat.SetTextureScale(t.name,t.scale);mat.SetTextureOffset(t.name,t.offset);}
        mat.SetFloat("_HasFx",source.textures.Any(t=>t.name=="_FxTex")?1:0);
        mat.SetFloat("_FxWorld",source.shader.Contains("FXWorld")?1:0);mat.SetVector("_Origin",Root.transform.position);
        // Constant-output shaders have no texture dependency to resolve.
        mat.SetFloat("_HasSourceTexture",source.textures.Length>0||source.shader=="Utility/Black"||source.shader=="Custom/UnlitColor"||source.shader=="Custom/FX/FX_HeroMask_VertColorOnly"||source.shader=="Effects/FX_AdditiveVertColor"?1:0);
        mat.SetFloat("_TwoLayer",source.shader.Contains("2Layer")?1:0);
        // Saved material properties may be leftovers from a previous shader.
        // A blue legacy _TintColor must not recolor the orange Hellfire shader.
        mat.SetFloat("_ParticleTint",source.properties!=null&&source.properties.Contains("_TintColor")?1:0);
        // Unrestored multiply families retain the explicit compatibility path.
        // Particle Multiply uses its original RGBA destination factor, including
        // opaque internal captures; colored board transmission is still separate.
        bool multiply=source.shader.Contains("Multiply");
        mat.SetFloat("_AddMultiply",source.shader=="Hero/AddMultiply/AddMultiply"?1:0);
        mat.SetFloat("_OriginalFamily",source.shader=="Hero/Particle/Particle Additive + 1"?1:source.shader=="Hero/Particle/Particle Additive"?2:source.shader=="Hero/AddMultiply/AddMultiply"?3:source.shader=="Hero/AddMultiply/AddMultiplyDistortion"?4:source.shader=="Hero/Particle/Particle Multiply"?7:source.shader=="Hero/AddMultiply/AddMultiplyFXWorld"?8:source.shader=="Hero/AddMultiply/AddMultiplyFXScrolling"?9:0);
        if(source.shader=="Effects/AddMulDissolveSingleTex")mat.SetFloat("_OriginalFamily",10);
        if(source.shader=="Hero/Additive/Additive")mat.SetFloat("_OriginalFamily",16);
        if(source.shader=="Hero/Particle/Particles_Transparent")mat.SetFloat("_OriginalFamily",17);
        if(source.shader=="Hero/Particle/Particles_Transparent_ColorAsAlpha")mat.SetFloat("_OriginalFamily",18);
        if(source.shader=="Utility/Black")mat.SetFloat("_OriginalFamily",11);
        if(source.shader=="Custom/FX/FX_HolyRender")mat.SetFloat("_OriginalFamily",20);
        if(source.shader=="Custom/FX/FX_AreaEffect_Render")mat.SetFloat("_OriginalFamily",43);
        if(source.shader=="Custom/FX/FX_HeroMask_VertColorOnly")mat.SetFloat("_OriginalFamily",21);
        if(source.shader=="Effects/FX_AdditiveVertColor")mat.SetFloat("_OriginalFamily",22);
        if(source.shader=="Hero/Particle/FX Particle Additive Alpha 2Layer")mat.SetFloat("_OriginalFamily",23);
        if(source.shader=="Custom/FX/FX_SneakySmoke")mat.SetFloat("_OriginalFamily",24);
        if(source.shader=="Hero/Unlit/Unlit_TransparentColor")mat.SetFloat("_OriginalFamily",25);
        if(source.shader=="Hero/Unlit/Unlit_Texture")mat.SetFloat("_OriginalFamily",26);
        if(source.shader=="Hero/Unlit/Unlit_Cutout")mat.SetFloat("_OriginalFamily",27);
        if(source.shader=="Custom/Card/Unlit_Portrait")mat.SetFloat("_OriginalFamily",28);
        if(source.shader=="Hero/Unlit/Unlit_TextureColor")mat.SetFloat("_OriginalFamily",29);
        if(source.shader=="Hero/Diffuse/DiffuseAlphaMaskScroller")mat.SetFloat("_OriginalFamily",30);
        if(source.shader=="Custom/Card/Diffuse"||source.shader=="Hero/Diffuse/Diffuse")
        {
            mat.SetFloat("_OriginalFamily",source.shader=="Custom/Card/Diffuse"?31:32);
            // 当前宿主没有原全局灯服务，显式零主/附加灯；不把此输入升级为原运行事实。
            new NativeDiffuseInputs().Apply(mat);
        }
        if(source.shader=="Custom/Selection/Highlight")mat.SetFloat("_OriginalFamily",33);
        if(source.shader=="Hero/Multiply/Multiply")mat.SetFloat("_OriginalFamily",34);
        if(source.shader=="Custom/Card/UnlitTexture")mat.SetFloat("_OriginalFamily",26);
        if(source.shader=="Hero/Text_Unlit")mat.SetFloat("_OriginalFamily",35);
        if(source.shader=="Hidden/TextOutline_Unlit")mat.SetFloat("_OriginalFamily",36);
        if(source.shader=="Hidden/TextOutline_Unlit_NoVertColor")mat.SetFloat("_OriginalFamily",37);
        if(source.shader=="Hidden/Text_Bold")mat.SetFloat("_OriginalFamily",38);
        if(source.shader=="Hidden/TextBoldOutline_Unlit")mat.SetFloat("_OriginalFamily",39);
        if(source.shader=="Hidden/TextAntialiasing")mat.SetFloat("_OriginalFamily",40);
        if(source.shader=="Hidden/R2TAlphaClip")mat.SetFloat("_OriginalFamily",41);
        if(source.shader=="Hidden/R2TAdditive")mat.SetFloat("_OriginalFamily",42);
        if(source.shader=="Hero/AddMultiply/AddMultiplyFXScrollingManual")mat.SetFloat("_OriginalFamily",12);
        if(source.shader=="Hero/AddMultiply/AddMultiplyFXRotation")mat.SetFloat("_OriginalFamily",13);
        if(source.shader=="Hero/AddMultiply/AddMultiplyFXScrolling2Layer")mat.SetFloat("_OriginalFamily",14);
        if(source.shader=="FX/CommonFX"&&source.keywords!=null)
        {
            var keywords=new HashSet<string>(source.keywords);
            var shared=new[]{"_COLORING_RAMP","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_TEXTURETYPE_RGBA"};
            if(keywords.SetEquals(shared.Concat(new[]{"_SECONDTEXTURE_NOISEUV"})))mat.SetFloat("_OriginalFamily",5);
            else if(keywords.SetEquals(shared.Concat(new[]{"_SECONDTEXTURE_MASK"})))mat.SetFloat("_OriginalFamily",6);
            else if(keywords.SetEquals(new[]{"_COLORING_RAMP","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_NONE","_TEXTURETYPE_RGBR"}))mat.SetFloat("_OriginalFamily",15);
            else if(keywords.SetEquals(new[]{"_COLORING_NONE","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_NONE","_TEXTURETYPE_RGBR"}))mat.SetFloat("_OriginalFamily",19);
            else if(keywords.SetEquals(new[]{"_COLORING_NONE","_DISSOLVE_SECONDALPHA","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_MASK","_TEXTURETYPE_RGBA"}))mat.SetFloat("_OriginalFamily",44);
            else if(keywords.SetEquals(new[]{"_COLORING_NONE","_DISSOLVE_SECONDALPHA","_SECONDTEXTURETYPE_RGBA","_SECONDTEXTURE_MASK","_TEXTURETYPE_RGBA"}))mat.SetFloat("_OriginalFamily",45);
            else if(keywords.SetEquals(new[]{"_COLORING_NONE","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_ADD","_TEXTURETYPE_RGBA"}))mat.SetFloat("_OriginalFamily",46);
        }
        mat.SetFloat("_SolidColor",source.shader=="Custom/UnlitColor"?1:0);
        mat.SetFloat("_ZWrite",source.shader=="Custom/UnlitColor"||source.shader=="Utility/Black"?1:0);
        mat.SetFloat("_Cull",source.shader=="Utility/Black"?2:0);
        mat.SetFloat("_OriginalSrcBlend",source.src);mat.SetFloat("_OriginalDstBlend",source.dst);
        mat.SetFloat("_OriginalSrcAlpha",source.src_alpha);mat.SetFloat("_OriginalDstAlpha",source.dst_alpha);
        mat.SetFloat("_LuminanceAlpha",multiply||source.shader.Contains("Additive")?1:0);
        mat.SetInt("_SrcBlend",multiply?5:Mathf.Clamp(source.src,0,10));mat.SetInt("_DstBlend",multiply?10:Mathf.Clamp(source.dst,0,10));
        int family=(int)mat.GetFloat("_OriginalFamily");
        if(family>0)
        {
            mat.SetFloat("_LuminanceAlpha",0);mat.SetInt("_SrcBlend",source.src);mat.SetInt("_DstBlend",source.dst);
            // Preserve C + destination*K in the premultiplied board capture.
            // Additive RGB has K=1, so it must not accumulate background opacity.
            mat.SetInt("_CaptureSrcAlpha",family==7?0:family>=5?(source.dst==1?0:1):family>=3?1:0);mat.SetInt("_CaptureDstAlpha",family==7?3:family>=5?(source.dst==1?1:10):family>=3?10:1);
        }
        if(family==21)
        {
            mat.SetFloat("_ZWrite",1);mat.SetFloat("_Cull",2);
            mat.SetInt("_CaptureSrcAlpha",0);mat.SetInt("_CaptureDstAlpha",3);
        }
        if(family==25||family==26||family==27){mat.SetFloat("_Cull",2);mat.SetFloat("_ZWrite",family==26?1:0);}
        if(family>=28&&family<=32){mat.SetFloat("_Cull",2);mat.SetFloat("_ZWrite",1);}
        if(source.shader=="Custom/Card/UnlitTexture")mat.SetFloat("_Cull",0);
        if(family==34)mat.SetFloat("_Cull",2);
        if(family>=35&&family<=40){mat.SetFloat("_Cull",2);mat.SetFloat("_HasSourceTexture",1);}
        materials.Add(mat);return mat;
    }
    bool OriginalProperty(Material material,string property)
    {HashSet<string> declared;return material!=null&&material.HasProperty(property)&&declaredProperties.TryGetValue(material,out declared)&&declared.Contains(property);}
    void ResetClocks()
    {
        AudioVolumeWrites=0;UnboundAudioVolumeActions=0;AudioSourceStops=0;UnboundAudioStopActions=0;audioStopVersions.Clear();
        AudioFadeStarts=0;AudioFadeFinishes=0;UnboundAudioFadeActions=0;
        generatedAudioOwners.Clear();destroyedAudioOwners.Clear();loopingAudioOwners.Clear();LoopingAudioStarts=0;GeneratedAudioOwnerRemovals=0;
        MainShakeStarts=0;MainShakeFinishes=0;UnboundMainShakeActions=0;
        CameraShakerStarts=0;cameraShakerInvocations.Clear();
        DelayedVisibilityStarts=0;DelayedVisibilityFinishes=0;UnboundDelayedVisibilityActions=0;
        ColorInterpolationStarts=0;ColorInterpolationFinishes=0;UnboundColorInterpolationActions=0;
        geometry.Restore();UnboundGeometryActions=0;MeshReplacements=0;MaterialReplacements=0;
        for(int i=0;i<nodeMaterials.Length;i++)nodeMaterials[i]=initialNodeMaterials[i].ToArray();
        foreach(var pair in materialSources)
        {foreach(var f in pair.Value.floats)if(OriginalProperty(pair.Key,f.name))pair.Key.SetFloat(f.name,f.value);foreach(var c in pair.Value.colors)if(OriginalProperty(pair.Key,c.name))pair.Key.SetColor(c.name,c.value);}
        foreach(var cue in sourceAudioCues.Values)cue.stopPlayback?.Invoke();sourceAudioCues.Clear();ordinaryAudioCoroutines.Clear();ordinaryAudioTimers.Clear();OrdinaryAudioStarts=0;OrdinaryAudioFinishes=0;MissingAudioPlaybackServices=0;UnhandledActions=0;unhandledActionDetails.Clear();actionExecutions.Clear();
        audioWatches.Clear();playthroughTimers.Clear();playthroughVolumes.Clear();PlaythroughStarts=0;PlaythroughFinishes=0;clipAudioCoroutines.Clear();cardIdAudioCoroutines.Clear();audioCues.Clear();UnboundAudioActions=0;audioRandom=new System.Random(117);ClipAudioStarts=0;ClipAudioFinishes=0;SuppressedClipAudioActionEntries=0;CardIdAudioStarts=0;CardIdAudioFinishes=0;
        PortraitTextureBindings=0;UnboundPortraitTextureActions=0;UnboundComponentActions=0;
        UnboundEliteActions=0;EliteEvents=0;
        randomWaitSampler.Reset(117);RandomWaitStarts=0;RandomWaitFinishes=0;UnboundRandomWaitActions=0;
        customSpawn.Restore();CustomSpawnInitializations=0;CustomSpawnFinalizations=0;UnboundCustomSpawnActions=0;
        SpellPlayerSideEventDispatches=0;UnboundSpellPlayerSideEventActions=0;
        ReleaseTimelineControls();timelines.Clear();timelineParticles.Clear();timelineRandom=new System.Random(117);TimelineStarts=0;TimelineSoundStarts=0;UnboundTimelineSounds=0;UnboundTimelineActions=0;UnsupportedTimelineTracks=0;UnboundTimelineCameras=0;
        RestoreBorrowedVisibility();
        RestoreBorrowedAnimationPoses();
        UnboundActorVisibilityActions=0;ActorRootVisibilityBindings=0;UnrestoredActorVisibilitySideEffects=0;
        UnboundAnimationActions=0;
        StoppedOnExitClips=0;
        CompletedAnimationActions=0;
        tweens.Clear();UnboundITweenActions=0;CompletedITweenActions=0;StoppedITweens=0;
        foreach(var pair in borrowedScales)if(pair.Key!=null)pair.Key.localScale=pair.Value;
        UnboundActorActions=0;UnboundActorInitializationActions=0;UnboundMachineOwnerActions=0;ActorObjectBindings=0;
        UnboundSetParentActions=0;UnboundDestroyObjectActions=0;SetParentActions=0;DestroyObjectActions=0;
        lifecycleCursor=0;LifecycleChanges=0;UnboundLifecycleEvents=0;
        foreach(var mixer in legacyMixers.Values)mixer.Stop();
        foreach(var restore in propertyRestore.Values)restore();propertyRestore.Clear();PropertyWrites=0;UnboundPropertyActions=0;
        machines.Clear();clips.Clear();particleStart.Clear();particleStop.Clear();animationSpeed.Clear();finishNotifications.Clear();UnboundFinishActions=0;TransitionLimitHits=0;UnboundCardTypeActions=0;UnboundTextureActions=0;TextureBindings=0;UnboundScaleActions=0;UnboundColorActions=0;UnboundEaseActions=0;RGBAWrites=0;rgbaLocalColors.Clear();
        sourceStates.Clear();SourceStateAnimationStarts=0;UnboundSourceStateObjects=0;ActiveSourceSpellState=0;SourceSpellFinished=false;SourceStateChanges=0;
        animationEvents.Clear();UnboundAnimationEvents=0;SourceParticleEvents=0;
        sourceAttackWait.Reset();sourcePresentationFrame=0;SourceAttackActionsStarted=0;SourceAttackPreviousState=0;SourceAttackStartedTime=0;SourceEventAudioStarts=0;UnrestoredSourceActionSideEffects=0;
        sourcePlacement=null;
        foreach(var pair in initialTextures){foreach(var texture in pair.Value)pair.Key.SetTexture(texture.Key,texture.Value);pair.Key.SetFloat("_HasSourceTexture",initialTextureFlags[pair.Key]);}
        for(int i=0;i<Nodes.Length;i++)
        {
            var n=Program.nodes[i];Nodes[i].transform.SetParent(n.parent<0?Root.transform:Nodes[n.parent].transform,false);Nodes[i].SetActive(n.active);Nodes[i].transform.localPosition=n.position;Nodes[i].transform.localRotation=n.rotation;Nodes[i].transform.localScale=n.scale;
            if(n.light!=null&&n.light.restored)n.light.Create(Nodes[i]);
            var r=renderers[i];if(r!=null){desiredVisibility[r]=n.visible;r.enabled=!deferred[i]&&n.visible&&nodeMaterials[i].Any(m=>m.GetFloat("_HasSourceTexture")>0);for(int j=0;j<nodeMaterials[i].Length;j++){var source=n.materials[j];foreach(var f in source.floats)if(OriginalProperty(nodeMaterials[i][j],f.name))nodeMaterials[i][j].SetFloat(f.name,f.value);foreach(var c in source.colors)if(OriginalProperty(nodeMaterials[i][j],c.name))nodeMaterials[i][j].SetColor(c.name,c.value);}}
            if(Particles[i]!=null){Particles[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var em=Particles[i].emission;em.enabled=n.particle.emission.enabled;if(n.particle.awake)particleStart[i]=0;}
        }
        foreach(var state in Program.spell_states??new NativeSourceSpellState[0])
        {
            if(state.node<0||state.node>=Nodes.Length){UnboundSourceStateObjects++;continue;}
            if(state.type==0||Program.nodes[state.node].parent!=0)continue;
            var playback=new NativeSpellStatePlayback(state,active=>Nodes[state.node].SetActive(active),
                time=>PlaySourceState(state,time),next=>StopSourceState(state,next));
            sourceStates.Add(playback);if(state.enabled)playback.Initialize(0);
        }
        foreach(var capture in offscreenFX)capture.Reset();
        foreach(var machine in Program.machines)
        {
            var clock=new MachineClock{machine=machine};machines.Add(clock);
            foreach(var f in machine.floats??new NativeFloat[0])clock.floats[f.name]=f.value;
            foreach(var c in machine.colors??new NativeColor[0])clock.colors[c.name]=c.value;
            foreach(var v in machine.vectors??new NativeVector[0])clock.vectors[v.name]=v.value;
            foreach(var v in machine.textures??new NativeTextureVariable[0])
                if(!v.unresolved&&(v.object_null||!String.IsNullOrEmpty(v.path)))clock.textures[v.name]=v.object_null?null:assets.TextureFile(v.path);
            // Replay the first available lifecycle entry. Many effects begin at
            // Action/Birth; starting all machines at Death suppresses their emitters.
            var entry=spellContext!=null&&!String.IsNullOrEmpty(spellContext.Lifecycle)?machine.events.FirstOrDefault(e=>String.Equals(e.@event,spellContext.Lifecycle,StringComparison.OrdinalIgnoreCase)):
                NativeSpellLifecycle.Entry(machine);
            if(deferEntryEvents||SourceRootFsmStartsAtEntry&&machine.owner_known&&machine.owner==0)entry=null;
            Enter(clock,entry==null?machine.start:entry.state,0);
        }
    }
    public bool StartHunterImpactExplosionAtAgeZero()
    {
        const string impactReference="ReuseFX_Arrow_Impact_01.prefab:1c032452d358567449b65ff342bb87ce";
        string[] soundPaths={"38dc39eff4ccb3616e7b89ea.native-sound.json","d4e7853ed281083a4e54bf76.native-sound.json","866ce32403a1e84d9b7cced8.native-sound.json"};
        if(!Mathf.Approximately(last,0)||spellContext==null||spellContext.CardId(0)!="HERO_05bp"||Program.reference!=impactReference)return false;
        int machineIndex=Array.FindIndex(Program.machines,m=>m.owner_known&&m.owner==0);
        if(machineIndex<0||machineIndex>=machines.Count)return false;
        var machine=Program.machines[machineIndex];
        var state=machine.states.FirstOrDefault(s=>s.name=="DeathState 2");
        if(state==null||state.actions==null||state.actions.Length<=10||Array.IndexOf(machine.states,state)!=3)return false;
        var action=state.actions[10];
        var sounds=ArrayParameters(action,"m_Sounds");var weights=ArrayParameters(action,"m_Weights");
        if(action.name!="AudioPlayRandomClipAction"||action.Param("m_ParentObject").node!=0||
           !sounds.Select(p=>p.asset_path).SequenceEqual(soundPaths)||
           !weights.Select(p=>p.number).SequenceEqual(new[]{1f,1f,1f})||!Mathf.Approximately(action.Param("m_Delay").number,0))return false;
        if(suppressedClipAudioActions.Contains(action)||clipAudioCoroutines.Any(w=>w.action==action))return false;
        suppressedClipAudioActions.Add(action);
        int executions;actionExecutions.TryGetValue(action.name,out executions);actionExecutions[action.name]=executions+1;
        if(StartRandomClipAudio(machines[machineIndex],action,state,0,"hunter-impact-explode"))return true;
        suppressedClipAudioActions.Remove(action);actionExecutions[action.name]=executions;return false;
    }
    bool StartRandomClipAudio(MachineClock clock,NativeAction action,NativeState state,float when,string keyPrefix=null)
    {
        var soundHeader=action.Param("m_Sounds");var weightHeader=action.Param("m_Weights");
        var soundParams=ArrayParameters(action,"m_Sounds");var weightParams=ArrayParameters(action,"m_Weights");
        var parent=action.Param("m_ParentObject");var minVolume=action.Param("m_MinVolume");var maxVolume=action.Param("m_MaxVolume");
        var minPitch=action.Param("m_MinPitch");var maxPitch=action.Param("m_MaxPitch");var spatial=action.Param("m_SpatialBlend");var template=action.Param("m_TemplateSource");
        var sounds=new List<NativeSoundAsset>();
        foreach(var item in soundParams)
        {NativeSoundAsset definition;if(item.unresolved||item.object_null||item.asset_type!="SoundDef"||!clipSounds.TryGetValue(item.asset_path??"",out definition)){sounds.Clear();break;}sounds.Add(definition);}
        var weights=weightParams.Select(p=>p.number).ToArray();
        bool unsupported=action.@params.Any(p=>p.unresolved)||!soundHeader.array_known||soundHeader.array_type!="SoundDef"||soundParams.Length!=soundHeader.array_count||sounds.Count!=soundHeader.array_count||
            !weightHeader.array_known||weightHeader.array_type!="HutongGames.PlayMaker.FsmFloat"||weightParams.Length!=weightHeader.array_count||
            weightHeader.array_count!=soundHeader.array_count||
            parent.unresolved||minVolume.unresolved||maxVolume.unresolved||minPitch.unresolved||maxPitch.unresolved||spatial.unresolved||
            !minVolume.none_known||!maxVolume.none_known||!minPitch.none_known||!maxPitch.none_known||!spatial.none_known||
            (Number(clock,spatial)!=0&&clipAudioTemplate==null)||template.unresolved||
            !template.object_null&&(!clipAudioTemplates.ContainsKey(template.asset_path??"")||!System.IO.File.Exists(System.IO.Path.Combine(assets.Root,"native-audio-mixer.assetbundle")));
        bool soundBound=!unsupported&&sounds.Count>0&&!weightParams.Any(p=>p.kind!=15||p.unresolved);
        if(!soundBound){UnboundAudioActions++;return false;}
        int machineIndex=machines.IndexOf(clock),actionIndex=Array.IndexOf(state.actions,action);
        string key=String.IsNullOrEmpty(keyPrefix)?"random-clip:"+machineIndex+":"+state.name+":"+actionIndex+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture):
            keyPrefix+":"+machineIndex+":"+state.name+":"+actionIndex;
        var watch=new ClipAudioWatch{action=action,state=state,clock=clock,sounds=sounds.ToArray(),weights=weights,
            delay=new NativeClipAudioDelay(Number(clock,action.Param("m_Delay"))),sampledAt=when,key=key};
        clock.clipAudio.Add(watch);clipAudioCoroutines.Add(watch);ClipAudioStarts++;UpdateClipAudio(watch,when);return deliveredAudio.Contains(watch.key);
    }
    float Enter(MachineClock clock,string state,float when)
    {
        UpdateTweens(when);
        foreach(var tween in clock.tweenWatches)
        {
            tween.registered=false;
            if(tween.stop)StoppedITweens+=tweens.RemoveAll(t=>t.owner==tween.owner&&t.fade==tween.fade&&!t.finished);
        }
        clock.tweenWatches.Clear();clock.waitWatches.Clear();clock.finishedFloats.Clear();clock.delayedVisibility.Clear();clock.colorInterpolations.Clear();clock.randomWaits.Clear();clock.clipAudio.Clear();clock.cardIdAudio.Clear();clock.playthrough.Clear();clock.ordinaryAudio.Clear();clock.audioFades.Clear();clock.mainShakes.Clear();
        if(clock.exitAnimationStops.Count>0)
        {
            SampleClips(when);
            foreach(var stop in clock.exitAnimationStops)
                StoppedOnExitClips+=clips.RemoveAll(c=>!c.animator&&c.owner==stop.Item1&&c.clip.name==stop.Item2);
            clock.exitAnimationStops.Clear();
        }
        foreach(var reset in clock.visibilityReset)foreach(var pair in reset)SetRendererVisible(pair.Key,pair.Value);
        clock.visibilityReset.Clear();
        foreach(var reset in clock.actorVisibilityReset)foreach(var pair in reset)if(pair.Key!=null)pair.Key.SetShown(pair.Value);
        clock.actorVisibilityReset.Clear();
        clock.state=clock.machine.states.FirstOrDefault(s=>s.name==state);clock.entered=when;clock.colorSampleAt=when;clock.finish=float.PositiveInfinity;clock.pending=null;clock.pendingAt=float.NaN;clock.colorAnimations.Clear();clock.easeAnimations.Clear();clock.animationWatches.Clear();
        if(clock.state==null)return 0;
        float duration=0;
        foreach(var action in clock.state.actions)
        {
            if(suppressedClipAudioActions.Contains(action)){SuppressedClipAudioActionEntries++;continue;}
            int executions;actionExecutions.TryGetValue(action.name,out executions);actionExecutions[action.name]=executions+1;
            var target=action.Param("m_GameObject","gameObject","m_SpellObject");int node=target.node;
            if(action.name=="SpellStateAction"&&(sourceStates.Count>0||SourceRootFsmCallbacks))
            {
                var transition=(clock.machine.events??new NativeTransition[0]).FirstOrDefault(e=>e.state==clock.state.name);
                bool internalState=transition==null&&clock.machine.states.Any(s=>(s.transitions??new NativeTransition[0]).Any(t=>t.state==clock.state.name));
                NativeSpellStateType type;
                if(!target.unresolved&&ResolveObject(clock,target)==Nodes[0]&&transition!=null&&Enum.TryParse(transition.@event,true,out type)&&Enum.IsDefined(typeof(NativeSpellStateType),type))
                {if(ActiveSourceSpellState!=(int)type)ChangeSourceSpellState((int)type,when,false);}
                // CardType-selected child states such as "Init FX" are FSM branches, not Spell lifecycle events.
                else if(!target.unresolved&&ResolveObject(clock,target)==Nodes[0]&&internalState){}
                else UnboundSourceStateObjects++;
            }
            else if(action.name=="MainCameraShakerAction")
            {
                try
                {
                    clock.mainShakes[action]=new NativeMainShakeAction(action,p=>Number(clock,p),p=>
                    {Vector3 value=p.vector;if(!String.IsNullOrEmpty(p.variable)&&!clock.vectors.TryGetValue(p.variable,out value))throw new ArgumentException("Original main shake vector is missing");return value;},
                    (amount,time)=>{if(MainCameraShake!=null)MainCameraShake(amount,time);else NativeCameraShakeDriver.Play(timelineCamera!=null?timelineCamera:Camera.main,amount,time);MainShakeStarts++;});
                }
                catch(ArgumentException){UnboundMainShakeActions++;duration=float.PositiveInfinity;}
            }
            else if(action.name=="InvokeMethod")
            {
                if(!ScheduleCameraShaker(clock,action,when))
                {
                    UnhandledActions++;
                    unhandledActionDetails.Add("machine="+machines.IndexOf(clock)+" owner="+
                        (clock.machine.owner_known?clock.machine.owner.ToString():"unknown")+" state="+clock.state.name+
                        " action=InvokeMethod unsupported CameraShaker binding");
                }
            }
            else if(action.name=="SpellPlayerSideEventAction")
            {
                int machineIndex=machines.IndexOf(clock);var targetParameter=action.Param("m_SpellObject");
                var friendly=action.Param("m_FriendlyEvent");var opponent=action.Param("m_OpponentEvent");
                string evt=SpellPlayerSideEventResolver==null?null:SpellPlayerSideEventResolver(machineIndex,action);
                var owner=MachineOwner(machineIndex);var receiver=ResolveObject(clock,targetParameter);
                bool valid=!targetParameter.unresolved&&targetParameter.kind==20&&targetParameter.node>=0&&
                    owner!=null&&receiver==owner&&clock.machine.owner_known&&clock.machine.owner==targetParameter.node&&
                    !friendly.unresolved&&!opponent.unresolved&&!String.IsNullOrEmpty(friendly.text)&&!String.IsNullOrEmpty(opponent.text)&&
                    (evt==friendly.text||evt==opponent.text)&&clock.state.transitions.Any(t=>t.@event==evt);
                if(!valid){UnboundSpellPlayerSideEventActions++;UnhandledActions++;duration=float.PositiveInfinity;}
                else{clock.pending=evt;clock.pendingAt=when;SpellPlayerSideEventDispatches++;}
            }
            else if(action.name=="TimelinePlay")
            {
                var owner=ResolveObject(clock,target);var wait=action.Param("finishOnTimelineStopped");
                var timeline=Program.timelines==null?null:Program.timelines.FirstOrDefault(t=>t.node>=0&&t.node<Nodes.Length&&Nodes[t.node]==owner);
                if(target.unresolved||wait.unresolved||!String.IsNullOrEmpty(wait.variable)||timeline==null||timeline.wrap!=2||timeline.duration<=0){UnboundTimelineActions++;continue;}
                foreach(var old in timelines.Where(t=>t.data==timeline))ReleaseTimelineControls(old);
                timelines.RemoveAll(t=>t.data==timeline);
                var playback=new TimelineClock{data=timeline,entered=when};
                foreach(var track in timeline.tracks)
                {
                    if(track.muted)continue;
                    if(track.kind=="CameraShaker2Track")
                    {
                        foreach(var item in track.clips)
                        {
                            if(item.shaker==null||!item.shaker.compatible){UnsupportedTimelineTracks++;continue;}
                            var main=timelineCamera!=null?timelineCamera:Camera.main;
                            if(main==null){UnboundTimelineCameras++;continue;}
                            playback.shakes.Add(new TimelineShakeClock{clip=item,target=main.transform});
                        }
                        continue;
                    }
                    if(track.kind=="ControlRandomizedTrack")
                    {
                        foreach(var item in track.clips)
                        {
                            var control=item.control;
                            if(control==null||!control.compatible||control.node<0||control.node>=Nodes.Length||item.speed<=0||control.post<0||control.post>2||control.particle_roots==null||control.particle_roots.Any(i=>i<0||i>=Particles.Length||Particles[i]==null)){UnsupportedTimelineTracks++;continue;}
                            var controlled=Nodes[control.node];var seeds=control.particle_roots.SelectMany(i=>Particles[i].GetComponentsInChildren<ParticleSystem>()).Distinct().ToArray();
                            foreach(int root in control.particle_roots)
                                foreach(var particle in Particles[root].GetComponentsInChildren<ParticleSystem>(true)){int index=Array.IndexOf(Particles,particle);if(index>=0)timelineParticles.Add(index);}
                            playback.controls.Add(new TimelineControlClock{clip=item,target=controlled,initialActive=controlled.activeSelf,seeds=seeds,autoSeeds=seeds.Select(p=>p.useAutoRandomSeed).ToArray(),particles=control.particle_roots.Select(i=>new NativeRandomizedParticleClock(Particles[i])).ToArray()});
                        }
                        continue;
                    }
                    if(track.kind=="SoundTimelineTrack")
                    {
                        var source=track.node<0||track.node>=Nodes.Length||audioDefinition==null?null:
                            NativeAudio.Node(audioDefinition,Program.nodes[track.node].path,track.node);
                        bool sourceValid=source!=null&&source.audio_sources!=null&&source.audio_sources.Length==1&&
                            source.sound_defs!=null&&source.sound_defs.Length==1&&source.sound_defs[0].values!=null;
                        if(!sourceValid||track.clips==null||track.clips.Length!=1){UnsupportedTimelineTracks++;UnboundTimelineSounds++;continue;}
                        var item=track.clips[0];
                        if(item.start<0||item.duration<=0||item.speed<=0||item.clip_in!=0||item.pre!=0||item.post!=0||
                           float.IsNaN(item.start)||float.IsInfinity(item.start)||float.IsNaN(item.duration)||float.IsInfinity(item.duration))
                        {UnsupportedTimelineTracks++;UnboundTimelineSounds++;continue;}
                        playback.sounds.Add(new TimelineSoundClock{timeline=timeline,track=track,clip=item,owner=Nodes[track.node],node=track.node,
                            key="timeline-sound:"+track.node+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture)});
                        continue;
                    }
                    if(track.kind!="AnimationTrack"||track.node<0||track.node>=Nodes.Length){UnsupportedTimelineTracks++;continue;}
                    foreach(var item in track.clips)
                    {
                        var animation=Program.animations.FirstOrDefault(a=>a.id==item.animation);
                        if(!item.compatible||animation==null||item.speed<=0){UnsupportedTimelineTracks++;continue;}
                        var bindings=animation.tracks.Select(t=>BindTrack(t,track.node)).ToArray();
                        var rotations=bindings.Where(b=>b.transform!=null&&b.track.property.StartsWith("rotation.")).GroupBy(b=>b.transform).Select(g=>new RotationBinding{transform=g.Key,tracks=g.ToArray()}).ToArray();
                        playback.clips.Add(Tuple.Create(item,new ClipClock{clip=animation,node=track.node,entered=0,speed=1,loop=animation.loop,bindings=bindings,rotations=rotations}));
                    }
                }
                timelines.Add(playback);TimelineStarts++;
                if(wait.number!=0)duration=Mathf.Max(duration,timeline.duration-timeline.initial_time);
            }
            else if(action.name=="AudioPlayClipAction")
            {
                var sound=action.Param("m_Sound");var parent=action.Param("m_ParentObject");var volume=action.Param("m_Volume");var pitch=action.Param("m_Pitch");var spatial=action.Param("m_SpatialBlend");var template=action.Param("m_TemplateSource");NativeSoundAsset definition;
                bool unsupported=action.@params.Any(p=>p.unresolved)||sound.object_null||!String.IsNullOrEmpty(sound.variable)||parent.unresolved||
                    volume.unresolved||pitch.unresolved||spatial.unresolved||!volume.none_known||volume.is_none||!pitch.none_known||pitch.is_none||!spatial.none_known||spatial.is_none||(Number(clock,spatial)!=0&&clipAudioTemplate==null)||
                    template.unresolved||!template.object_null&&(!clipAudioTemplates.ContainsKey(template.asset_path??"")||!System.IO.File.Exists(System.IO.Path.Combine(assets.Root,"native-audio-mixer.assetbundle")))||action.Param("m_IsMinionSummonVO").number!=0||action.Param("m_IsKeywordVO").number!=0;
                if(unsupported||!clipSounds.TryGetValue(sound.asset_path??"",out definition)){UnboundAudioActions++;duration=float.PositiveInfinity;continue;}
                var watch=new ClipAudioWatch{action=action,state=clock.state,clock=clock,sound=definition,delay=new NativeClipAudioDelay(Number(clock,action.Param("m_Delay"))),sampledAt=when,
                    key="clip:"+machines.IndexOf(clock)+":"+clock.state.name+":"+Array.IndexOf(clock.state.actions,action)+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture)};
                clock.clipAudio.Add(watch);clipAudioCoroutines.Add(watch);ClipAudioStarts++;UpdateClipAudio(watch,when);
            }
            else if(action.name=="AudioPlayRandomClipAction")
            {
                StartRandomClipAudio(clock,action,clock.state,when);
            }
            else if(action.name=="SpellCardIdAudioPlayAction")
            {
                CardIdAudioWatch watch;
                if(!TryStartCardIdAudio(clock,action,when,out watch))
                {UnboundAudioActions++;UnhandledActions++;duration=float.PositiveInfinity;unhandledActionDetails.Add("machine="+machines.IndexOf(clock)+" owner="+(clock.machine.owner_known?clock.machine.owner.ToString():"unknown")+" state="+clock.state.name+" action="+action.name+" unsupported selector/source/audio parameters");continue;}
                clock.cardIdAudio.Add(watch);cardIdAudioCoroutines.Add(watch);CardIdAudioStarts++;UpdateCardIdAudio(watch,when);
            }
            else if(action.name=="AudioPlayAction")
            {
                var delay=action.Param("m_Delay");if(delay.unresolved){UnboundAudioActions++;duration=float.PositiveInfinity;continue;}
                NativePlaythroughDelay timer;if(!ordinaryAudioTimers.TryGetValue(action,out timer)){timer=new NativePlaythroughDelay();ordinaryAudioTimers.Add(action,timer);}timer.Reset(Number(clock,delay));
                var watch=new OrdinaryAudioWatch{action=action,clock=clock,delay=timer,sampledAt=when,
                    key=machines.IndexOf(clock)+":"+clock.state.name+":"+Array.IndexOf(clock.state.actions,action)+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture)};
                ordinaryAudioCoroutines.Add(watch);clock.ordinaryAudio.Add(watch);OrdinaryAudioStarts++;
                UpdateOrdinaryAudio(watch,when);
            }
            else if(action.name=="AudioPlaythroughAction")
            {
                var delay=action.Param("m_Delay");
                if(delay.unresolved){UnboundAudioActions++;duration=float.PositiveInfinity;continue;}
                float wait=Number(clock,delay);NativePlaythroughDelay timer=null;
                if(wait>0)
                {if(!playthroughTimers.TryGetValue(action,out timer)){timer=new NativePlaythroughDelay();playthroughTimers.Add(action,timer);}timer.Reset(wait);}
                var watch=new AudioWatch{action=action,clock=clock,delay=timer,sampledAt=when,
                    key=machines.IndexOf(clock)+":"+clock.state.name+":"+Array.IndexOf(clock.state.actions,action)+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture)};
                audioWatches.Add(watch);clock.playthrough.Add(watch);PlaythroughStarts++;
                UpdatePlaythrough(watch,when);
            }
            else if(action.name=="AudioSetVolumeAction")
            {
                if(!SetSourceVolume(clock,action))UnboundAudioVolumeActions++;
                if(action.Param("m_EveryFrame").number!=0)duration=float.PositiveInfinity;
            }
            else if(action.name=="AudioStopAction")
            {if(!StopSource(clock,action))UnboundAudioStopActions++;}
            else if(action.name=="AudioVolumeFadeAction"||action.name=="AudioLoopingEndAction"||action.name=="AudioLoopingStartAction")
            {if(!StartAudioFade(clock,action)){UnboundAudioFadeActions++;duration=float.PositiveInfinity;}}
            else if(action.name=="SpellGetActorAction")
            {
                var which=action.Param("m_WhichActor");var variable=action.Param("m_GameObject");
                var actor=spellContext==null||which.unresolved?null:spellContext.ActorObject((int)Number(clock,which));
                if(actor==null||variable.unresolved||!variable.none_known){UnboundActorActions++;continue;}
                if(!variable.is_none)
                {if(String.IsNullOrEmpty(variable.variable)){UnboundActorActions++;continue;}clock.objects[variable.variable]=actor;ActorObjectBindings++;}
            }
            else if(action.name=="InitializeCustomSpawn"||action.name=="FinalizeCustomSpawn")
            {
                bool initialize=action.name=="InitializeCustomSpawn";var owner=MachineOwner(machines.IndexOf(clock));
                var actorIdentity=initialize?NativeActorIdentity.FindInThisOrParents(owner):null;
                var actor=initialize?(actorIdentity==null?null:actorIdentity.gameObject):ResolveObject(clock,action.Param("actor"));
                var token=initialize?ResolveObject(clock,action.Param("tokenRoot")):null;
                var flags=new[]{action.Param("resetActorPosition"),action.Param("resetActorRotation"),action.Param("resetActorScale")};
                bool unsupported=flags.Any(p=>p.unresolved||!String.IsNullOrEmpty(p.variable)&&!clock.floats.ContainsKey(p.variable));
                var actorOutput=action.Param("actorOutput");var parentOutput=action.Param("actorParentOutput");
                if(initialize)unsupported|=action.Param("tokenRoot").unresolved||!action.Param("tokenRoot").none_known||action.Param("tokenRoot").is_none||actorOutput.unresolved||parentOutput.unresolved||!actorOutput.none_known||!parentOutput.none_known;
                if(!initialize)unsupported|=action.Param("actor").unresolved||!action.Param("actor").none_known||action.Param("actor").is_none;
                var originalParent=actor==null?null:actor.transform.parent;
                bool ok=!unsupported&&(initialize?customSpawn.Initialize(owner,actor,token,Number(clock,flags[0])!=0,Number(clock,flags[1])!=0,Number(clock,flags[2])!=0):customSpawn.Finalize(owner,actor,Number(clock,flags[0])!=0,Number(clock,flags[1])!=0,Number(clock,flags[2])!=0));
                if(!ok){UnboundCustomSpawnActions++;continue;}
                if(initialize)
                {
                    CustomSpawnInitializations++;
                    if(!actorOutput.is_none&&!String.IsNullOrEmpty(actorOutput.variable))clock.objects[actorOutput.variable]=actor;
                    if(!parentOutput.is_none&&!String.IsNullOrEmpty(parentOutput.variable))clock.objects[parentOutput.variable]=originalParent.gameObject;
                }
                else CustomSpawnFinalizations++;
            }
            else if(action.name=="GetCardPortraitTextureAction")
            {
                var parameter=action.Param("portraitObject");var store=action.Param("storedTexture");
                bool defaultOwner=!action.@params.Any(p=>p.name=="portraitObject")||String.IsNullOrEmpty(parameter.variable)&&parameter.node<0;
                var owner=defaultOwner?null:ResolveObject(clock,parameter);
                if(owner==null&&!parameter.unresolved)owner=MachineOwner(machines.IndexOf(clock));
                var actor=parameter.unresolved?null:NativeActorIdentity.FindInThisOrParents(owner);
                var texture=actor==null||!actor.HasCardDef?null:actor.GetStaticPortraitTexture();
                if(texture==null||store.unresolved||String.IsNullOrEmpty(store.variable))UnboundPortraitTextureActions++;
                else{clock.textures[store.variable]=texture;PortraitTextureBindings++;}
            }
            else if(action.name=="ActorEliteEventAction")
            {
                var parameter=action.Param("m_ActorObject");
                if(parameter.unresolved){UnboundEliteActions++;continue;}
                var actor=NativeActorIdentity.FindInThisOrParents(ResolveObject(clock,parameter));
                // Source finishes without sending either event when Actor is absent.
                if(actor==null)continue;
                var evt=action.Param(actor.IsElite()?"m_EliteEvent":"m_NonEliteEvent");
                if(evt.unresolved){UnboundEliteActions++;continue;}
                if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=when;EliteEvents++;}
            }
            else if(action.name=="SpellInitActorVariables")
            {
                var owner=MachineOwner(machines.IndexOf(clock));var actor=NativeActorIdentity.FindInThisOrParents(owner);
                if(owner==null)UnboundMachineOwnerActions++;
                if(actor==null){UnboundActorInitializationActions++;continue;}
                var mesh=actor.GetMeshRenderer(true);
                foreach(var pair in new[]{new{parameter="m_actorObject",value=actor.gameObject},new{parameter="m_rootObject",value=actor.RootObject},new{parameter="m_rootObjectMesh",value=mesh==null?null:mesh.gameObject}})
                {
                    var variable=action.Param(pair.parameter);
                    if(variable.unresolved||!variable.none_known||!variable.is_none&&String.IsNullOrEmpty(variable.variable)){UnboundActorInitializationActions++;continue;}
                    if(!variable.is_none&&(pair.parameter!="m_rootObjectMesh"||mesh!=null)){clock.objects[variable.variable]=pair.value;ActorObjectBindings++;}
                }
            }
            else if(action.name=="SpellCardTypeAction")
            {
                var which=action.Param("m_WhichCard");int type=spellContext==null||which.unresolved?0:spellContext.CardType((int)which.number);
                string parameter=type==4?"m_MinionEvent":type==3?"m_HeroEvent":type==10?"m_HeroPowerEvent":type==7?"m_WeaponEvent":type==5?"m_SpellEvent":type==39?"m_LocationEvent":type==40?"m_BaconQuestRewardEvent":null;
                if(parameter==null){UnboundCardTypeActions++;continue;}
                var evt=action.Param(parameter);if(evt.unresolved){UnboundCardTypeActions++;continue;}
                if(!String.IsNullOrEmpty(evt.text)&&clock.pending==null)clock.pending=evt.text;
            }
            else if(action.name=="GetBoardAmbientColorAction"||action.name=="GetAmbientColorAction")
            {
                SetAmbientVariable(clock,action,true);
                if(action.name=="GetAmbientColorAction"&&action.Param("m_EveryFrame").number!=0)duration=float.PositiveInfinity;
            }
            else if(action.name=="Wait")
            {
                float wait=Number(clock,action.Param("time","m_Time"));duration=Mathf.Max(duration,wait);
                clock.waitWatches.Add(new WaitWatch{action=action,at=when+Mathf.Max(0,wait)});
            }
            else if(action.name=="RandomWaitAction")
            {
                var minimum=action.Param("m_MinTime");var maximum=action.Param("m_MaxTime");var real=action.Param("m_RealTime");var evt=action.Param("m_FinishEvent");
                // Current 37 source actions use scaled time. Realtime requires an
                // independent input clock and must not silently use effect age.
                if(minimum.unresolved||maximum.unresolved||real.unresolved||evt.unresolved||real.number!=0||
                    !String.IsNullOrEmpty(minimum.variable)&&!clock.floats.ContainsKey(minimum.variable)||
                    !String.IsNullOrEmpty(maximum.variable)&&!clock.floats.ContainsKey(maximum.variable))
                {UnboundRandomWaitActions++;duration=float.PositiveInfinity;continue;}
                var wait=new NativeRandomWait(Number(clock,minimum),Number(clock,maximum),false,when,randomWaitSampler.Range);
                clock.randomWaits.Add(new RandomWaitWatch{action=action,wait=wait,sampledAt=when});RandomWaitStarts++;
                if(wait.Finished){RandomWaitFinishes++;if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=when;}}
            }
            else if(action.name=="SpellFinishAction")
            {
                var delay=action.Param("m_Delay");float value=Number(clock,delay);
                if(delay.unresolved||(!String.IsNullOrEmpty(delay.variable)&&!clock.floats.ContainsKey(delay.variable))||float.IsNaN(value)||float.IsInfinity(value))UnboundFinishActions++;
                else finishNotifications.Add(new FinishNotification{time=when+Mathf.Max(0,value),node=node,machine=machines.IndexOf(clock)});
                // Original action Finish is immediate; the delayed Spell callback
                // runs independently of the FSM state and visual tail.
            }
            else if(action.name=="SetAnimationSpeed"&&node>=0)
            {SetClipSpeed(clock,action,when);if(action.Param("everyFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="StopAnimation")
            {
                var named=action.Param("animName");
                if(target.unresolved||node<0||node>=Nodes.Length||named.unresolved||!String.IsNullOrEmpty(named.variable))UnboundAnimationActions++;
                else
                {
                    SampleClips(when);
                    foreach(var clip in clips.Where(c=>c.node==node&&!c.animator&&(String.IsNullOrEmpty(named.text)||c.clip.name==named.text)).ToArray())
                    {
                        clips.Remove(clip);
                    }
                }
                // Original Stop(name)/Stop() finishes immediately and does not
                // clear another object's or Animator's playback state.
            }
            else if(action.name=="AnimationPlaythroughAction"||action.name=="PlayAnimation")
            {
                var owner=ResolveObject(clock,target);var external=owner!=null&&!Nodes.Contains(owner);
                var actor=external?owner.GetComponent<NativeActorIdentity>():null;
                var source=external?(actor==null?null:actor.AnimationProgram):Program;
                if(target.unresolved||owner==null||source==null||(!external&&(node<0||node>=Nodes.Length))){UnboundAnimationActions++;continue;}
                string name=action.Param("m_AnimName","animName").text;
                if(action.name=="AnimationPlaythroughAction"&&String.IsNullOrEmpty(name))continue;
                var sourceNode=source.nodes[external?0:node];var refs=sourceNode.animation_refs;
                var clip=source.animations.FirstOrDefault(c=>c.name==name&&(refs==null||refs.Contains(c.id)));
                if(clip==null){UnboundAnimationActions++;continue;}
                if(clip!=null){float speed=ClipSpeed(node,name);
                    int wrap=clip.loop?2:8;var component=sourceNode.legacy_animation;
                    if(clip.wrap_known)
                    {
                        if(!NativeLegacyWrap.Supported(clip.wrap_mode)||component!=null&&component.wrap_known&&!NativeLegacyWrap.Supported(component.wrap_mode)||clip.wrap_mode==0&&(component==null||!component.wrap_known))
                        {UnboundAnimationActions++;continue;}
                        wrap=NativeLegacyWrap.Resolve(clip.wrap_mode,component!=null&&component.wrap_known?component.wrap_mode:0);
                    }
                    var fade=action.Param("m_CrossFadeSec","blendTime");float blend=Number(clock,fade);
                    var mode=action.Param("m_PlayMode","playMode");float playMode=Number(clock,mode);
                    bool cross=action.name=="PlayAnimation"?blend>=.001f:blend>Mathf.Epsilon;
                    if(fade.unresolved||mode.unresolved||(playMode!=0&&playMode!=4)||float.IsNaN(blend)||float.IsInfinity(blend)){UnboundAnimationActions++;continue;}
                    var bindings=clip.tracks.Select(t=>BindTrack(t,owner)).ToArray();
                    if(external&&!BorrowAnimation(owner,bindings)){UnboundAnimationActions++;continue;}
                    var playing=cross?clips.FirstOrDefault(c=>c.owner==owner&&!c.animator&&c.clip.name==name):null;
                    // Source legacy states currently use their default layer zero.
                    // StopAll (4) and StopSameLayer (0) coincide for this pool.
                    if(cross)foreach(var old in clips.Where(c=>c.owner==owner&&!c.animator&&c!=playing))Fade(old,when,blend,0);
                    else clips.RemoveAll(c=>c.owner==owner&&!c.animator);
                    var rotations=bindings.Where(b=>b.transform!=null&&b.track.property.StartsWith("rotation.")).GroupBy(b=>b.transform).Select(g=>new RotationBinding{transform=g.Key,tracks=g.ToArray()}).ToArray();
                    if(playing==null){playing=new ClipClock{clip=clip,owner=owner,node=external?-1:node,wrapMode=wrap,entered=when,speed=speed,loop=wrap==2||wrap==4,bindings=bindings,rotations=rotations,weightFrom=cross?0:1,weightTo=cross?0:1};clips.Add(playing);}
                    if(cross)Fade(playing,when,blend,1);
                    if(action.name=="PlayAnimation")
                    {
                        clock.animationWatches.Add(new AnimationWatch{action=action,clip=playing,enteredTime=(when-playing.entered)*playing.speed+playing.offset});
                        var stop=action.Param("stopOnExit");
                        if(stop.unresolved)UnboundAnimationActions++;
                        else if(Number(clock,stop)!=0)clock.exitAnimationStops.Add((owner,name));
                    }
                    // Original AnimationPlaythroughAction calls Finish immediately
                    // after Animation.Play/CrossFade (static IL audit).
                    }
            }
            else if(action.name=="AnimatorPlayAction"&&node>=0)
            {
                string name=action.Param("m_StateName").text,layer=action.Param("m_LayerName").text;
                var states=Program.nodes[node].animator_states;
                var stateBinding=states==null?null:states.FirstOrDefault(s=>(s.name==name||s.full_name==name)&&(String.IsNullOrEmpty(layer)||s.layer==layer));
                var clip=stateBinding==null?null:Program.animations.FirstOrDefault(c=>c.id==stateBinding.clip);
                var start=action.Param("m_StartTimePercent");
                if(clip!=null&&!start.unresolved&&String.IsNullOrEmpty(start.variable))
                {
                    clips.RemoveAll(c=>c.node==node);
                    var bindings=clip.tracks.Select(t=>BindTrack(t,node)).ToArray();
                    var rotations=bindings.Where(b=>b.transform!=null&&b.track.property.StartsWith("rotation.")).GroupBy(b=>b.transform).Select(g=>new RotationBinding{transform=g.Key,tracks=g.ToArray()}).ToArray();
                    clips.Add(new ClipClock{clip=clip,node=node,entered=when,speed=stateBinding.speed,loop=stateBinding.loop,animator=true,
                        offset=(stateBinding.offset+start.number*.01f)*clip.duration,bindings=bindings,rotations=rotations});
                }
                // Original AnimatorPlayAction finishes immediately after Animator.Play.
                // Unsupported controllers stay audited; they do not hold the server.
            }
            else if((action.name=="ParticlePlayAction"||action.name=="ParticleStopAction")&&node>=0)
            {
                bool children=action.Param("m_IncludeChildren").number!=0;
                for(int i=0;i<Nodes.Length;i++)if(Particles[i]!=null&&(i==node||children&&Nodes[i].transform.IsChildOf(Nodes[node].transform)))
                {if(action.name=="ParticlePlayAction"){particleStart[i]=when;particleStop.Remove(i);var em=Particles[i].emission;em.enabled=Program.nodes[i].particle.emission.enabled;}else particleStop[i]=when;}
            }
            else if(action.name=="SetPosition"&&TrySetPosition(clock,action))
            {}
            else if(action.name=="SetParent")
            {if(TrySetParent(clock,action))SetParentActions++;else UnboundSetParentActions++;}
            else if(action.name=="DestroyObject")
            {if(TryDestroyObject(clock,action))DestroyObjectActions++;else UnboundDestroyObjectActions++;}
            else if(action.name=="ActorSetVisibilityRecursiveAction")ActorVisibility(clock,action);
            else if(action.name=="SetVisibilityRecursiveDelayAction")
            {
                clock.delayedVisibility.Add(action,false);
                var delay=action.Param("m_Delay");
                if(!delay.none_known||delay.unresolved||!delay.is_none&&!String.IsNullOrEmpty(delay.variable)&&!clock.floats.ContainsKey(delay.variable))UnboundDelayedVisibilityActions++;
                else DelayedVisibilityStarts++;
            }
            else if(action.name=="SetVisibilityRecursiveAction"&&node>=0)
            {
                bool children=action.Param("includeChildren").number!=0,visible=Number(clock,action.Param("visible"))!=0;
                var selected=children?Nodes[node].GetComponentsInChildren<Renderer>():new[]{renderers[node]};var reset=new Dictionary<Renderer,bool>();
                foreach(var r in selected.Where(r=>r!=null))
                {if(action.Param("resetOnExit").number!=0)reset[r]=desiredVisibility[r];SetRendererVisible(r,visible);}
                if(reset.Count>0)clock.visibilityReset.Add(reset);
            }
            else if(action.name=="SetVisibility"&&node>=0)
            {
                var renderer=Nodes[node].GetComponent<Renderer>();
                if(renderer!=null)
                {
                    bool initial=renderer.enabled;
                    if(action.Param("resetOnExit").number!=0)clock.visibilityReset.Add(new Dictionary<Renderer,bool>{{renderer,initial}});
                    SetRendererVisible(renderer,Number(clock,action.Param("toggle"))!=0?!initial:Number(clock,action.Param("visible"))!=0);
                }
            }
            else if(action.name=="ActivateGameObject"&&node>=0)Nodes[node].SetActive(action.Param("activate","m_Visible").number!=0);
            else if(action.name=="SetProperty")
            {if(!SetOriginalProperty(clock,action))UnboundPropertyActions++;if(action.Param("everyFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="SetColorValue")
            {
                var variable=action.Param("colorVariable");var value=action.Param("color");var everyFrame=action.Param("everyFrame");
                bool priest=spellContext!=null&&spellContext.CardId(0)=="HERO_09bp"&&action.@params.Length==3&&
                    action.@params.Select(parameter=>parameter.name).SequenceEqual(new[]{"colorVariable","color","everyFrame"})&&
                    !variable.unresolved&&variable.kind==25&&variable.none_known&&!variable.is_none&&variable.variable=="RaysGlow"&&
                    variable.color==new Color(0f,1f,0f,120f/255f)&&
                    !value.unresolved&&value.kind==25&&value.none_known&&!value.is_none&&String.IsNullOrEmpty(value.variable)&&
                    value.color==Color.clear&&!everyFrame.unresolved&&everyFrame.number==0;
                if(priest)clock.colors[variable.variable]=value.color;
                else{UnhandledActions++;unhandledActionDetails.Add("machine="+machines.IndexOf(clock)+" state="+clock.state.name+" action=SetColorValue outside HERO_09bp source profile");}
            }
            else if(action.name=="SetColorRGBA")
            {if(!SetRGBA(clock,action))UnboundColorActions++;if(action.Param("everyFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="ColorInterpolate")
            {
                try
                {
                    var interpolation=new NativeColorInterpolation(action,p=>
                    {if(!String.IsNullOrEmpty(p.variable)&&!clock.floats.ContainsKey(p.variable))throw new ArgumentException("Unrestored interpolation time variable");return Number(clock,p);},p=>
                    {if(!String.IsNullOrEmpty(p.variable)&&!clock.colors.ContainsKey(p.variable))throw new ArgumentException("Unrestored interpolation color variable");return ColorValue(clock,p);});
                    clock.colorInterpolations.Add(action,interpolation);ColorInterpolationStarts++;
                    if(interpolation.InitialWrite)WriteInterpolatedColor(clock,action,interpolation.Value);
                    if(interpolation.Finished)ColorInterpolationFinishes++;
                }
                catch(ArgumentException){UnboundColorInterpolationActions++;duration=float.PositiveInfinity;}
            }
            else if(action.name=="AnimateColor")
            {
                var variable=action.Param("colorVariable");Color initial=Color.clear;
                bool valid=variable.is_none||(!String.IsNullOrEmpty(variable.variable)&&clock.colors.TryGetValue(variable.variable,out initial));
                try
                {
                    if(!valid)throw new ArgumentException("Missing AnimateColor variable");
                    var animation=new NativeColorAnimation(action,initial,p=>Number(clock,p));clock.colorAnimations[action]=animation;
                    if(animation.InitialWrite&&!variable.is_none)clock.colors[variable.variable]=animation.Value;
                }
                catch(ArgumentException){UnboundColorActions++;}
            }
            else if(action.name=="SetScale")
            {if(!SetScale(clock,action))UnboundScaleActions++;if(action.Param("everyFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="iTweenScaleTo"||action.name=="iTweenScaleFrom"||action.name=="iTweenFadeToAction")
            {if(!StartTween(clock,action,when))UnboundITweenActions++;}
            else if(action.name=="SetMaterialTexture")SetTexture(clock,action,node);
            else if(action.name=="SetMesh"||action.name=="SetMaterial")ReplaceGeometry(clock,action);
            else if(action.name=="EaseFloat"||action.name=="EaseColor")
            {
                bool color=action.name=="EaseColor";var variable=action.Param(color?"colorVariable":"floatVariable");
                try
                {
                    if(!variable.is_none&&(String.IsNullOrEmpty(variable.variable)||(color?!clock.colors.ContainsKey(variable.variable):!clock.floats.ContainsKey(variable.variable))))throw new ArgumentException("Missing easing destination");
                    foreach(string name in new[]{"fromValue","toValue"})
                    {string source=action.Param(name).variable;if(!String.IsNullOrEmpty(source)&&(color?!clock.colors.ContainsKey(source):!clock.floats.ContainsKey(source)))throw new ArgumentException("Missing easing source");}
                    Color from=color?ColorValue(clock,action.Param("fromValue")):new Color(Number(clock,action.Param("fromValue")),0,0,0);
                    Color to=color?ColorValue(clock,action.Param("toValue")):new Color(Number(clock,action.Param("toValue")),0,0,0);
                    var animation=new NativeEaseAnimation(action,from,to,p=>Number(clock,p));clock.easeAnimations[action]=animation;
                    if(!variable.is_none){if(color)clock.colors[variable.variable]=animation.Value;else clock.floats[variable.variable]=animation.Value.r;}
                }
                catch(ArgumentException){UnboundEaseActions++;duration=float.PositiveInfinity;}
            }
            else if(action.name=="AnimateFloat")
            {
                var curve=action.Param("animCurve");var variable=action.Param("floatVariable");
                if(curve.curve!=null&&curve.curve.Length>0&&!String.IsNullOrEmpty(variable.variable))clock.floats[variable.variable]=curve.SampleCurve(0);
                duration=Mathf.Max(duration,curve.CurveDuration);
            }
            else if(action.name=="SetLightIntensity")
            {if(!SetLight(clock,action))UnboundComponentActions++;if(action.Param("everyFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="SetAlphaRecursiveAction")
            {if(!SetSourceAlpha(clock,action))UnboundComponentActions++;if(action.Param("m_EveryFrame").number!=0)duration=float.PositiveInfinity;}
            else if(action.name=="EnableBehaviour")
            {
                var owner=ResolveObject(clock,action.Param("gameObject"));var name=action.Param("behaviour");
                if(owner==null||name.unresolved||name.text!="Light"||!action.Param("component").object_null||action.Param("resetOnExit").number!=0)UnboundComponentActions++;
                else{var light=owner.GetComponent<Light>();if(light!=null)light.enabled=Number(clock,action.Param("enable"))!=0;}
            }
            else if((action.name=="SetMaterialFloat"||action.name=="SetMaterialColor")&&node>=0)
            {
                SetMaterial(clock,action,node);
            }
            else if(action.name!="SpellStateAction")
            {
                UnhandledActions++;
                unhandledActionDetails.Add("machine="+machines.IndexOf(clock)+" owner="+(clock.machine.owner_known?clock.machine.owner.ToString():"unknown")+
                    " state="+clock.state.name+" action="+action.name+" index="+Array.IndexOf(clock.state.actions,action)+" node="+node);
            }
        }
        clock.otherFinish=when+Mathf.Max(.0001f,duration);
        if(clock.colorAnimations.Count==0&&clock.colorInterpolations.Count==0&&clock.easeAnimations.Count==0&&clock.animationWatches.Count==0&&clock.tweenWatches.Count==0&&clock.randomWaits.Count==0&&clock.clipAudio.Count==0&&clock.cardIdAudio.Count==0&&clock.playthrough.Count==0&&clock.ordinaryAudio.Count==0&&clock.delayedVisibility.Count==0&&clock.audioFades.Count==0&&clock.mainShakes.Count==0&&clock.state.transitions.Any(t=>t.@event=="FINISHED"))clock.finish=clock.otherFinish;
        UpdateAudio(when);
        return duration;
    }
    public static bool SupportsCameraShakerInvoke(NativeProgram program,NativeAction action)
    {
        if(program==null||program.nodes==null||action==null||action.name!="InvokeMethod"||action.@params==null||action.@params.Length!=7||
            action.@params.Any(p=>p==null||p.unresolved))return false;
        var target=action.Param("gameObject");var behaviour=action.Param("behaviour");var method=action.Param("methodName");
        var delay=action.Param("delay");var repeating=action.Param("repeating");var repeatDelay=action.Param("repeatDelay");var cancel=action.Param("cancelOnExit");
        if(target.kind!=20||target.node<0||target.node>=program.nodes.Length||behaviour.kind!=18||behaviour.text!="CameraShaker"||
            method.kind!=18||method.text!="StartShake"||delay.kind!=15||String.IsNullOrEmpty(delay.name)||!String.IsNullOrEmpty(delay.variable)||
            delay.number<0||float.IsNaN(delay.number)||float.IsInfinity(delay.number)||repeating.kind!=17||repeating.number!=0||
            !String.IsNullOrEmpty(repeating.variable)||repeatDelay.kind!=15||repeatDelay.number<0||float.IsNaN(repeatDelay.number)||
            float.IsInfinity(repeatDelay.number)||!String.IsNullOrEmpty(repeatDelay.variable)||cancel.kind!=17||cancel.number!=0||
            !String.IsNullOrEmpty(cancel.variable))return false;
        var data=program.nodes[target.node].camera_shaker;
        if(data==null||!data.restored||data.source==null||data.curve==null||data.curve.Length<2||
            !Finite(data.amount)||data.curve.Any(k=>k==null||!Finite(k.time)||!Finite(k.value)||!Finite(k.inSlope)||!Finite(k.outSlope)||
                !Finite(k.inWeight)||!Finite(k.outWeight))||data.curve[0].time!=0||data.curve[data.curve.Length-1].time<=0||
            data.curve.Zip(data.curve.Skip(1),(a,b)=>b.time>a.time).Any(ordered=>!ordered)||
            data.hold&&(data.hold_at_sec<0||data.hold_at_sec>=data.curve[data.curve.Length-1].time))return false;
        return true;
    }
    static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    static bool Finite(Vector3 value){return Finite(value.x)&&Finite(value.y)&&Finite(value.z);}
    bool ScheduleCameraShaker(MachineClock clock,NativeAction action,float when)
    {
        if(!SupportsCameraShakerInvoke(Program,action))return false;
        var target=action.Param("gameObject");
        if(ResolveObject(clock,target)!=Nodes[target.node])return false;
        var camera=timelineCamera!=null?timelineCamera:Camera.main;
        if(camera==null)return false;
        cameraShakerInvocations.Add(new CameraShakerInvocation{camera=camera,data=Program.nodes[target.node].camera_shaker,
            due=when+Number(clock,action.Param("delay"))});
        return true;
    }
    void UpdateCameraShakerInvocations(float age)
    {
        foreach(var invocation in cameraShakerInvocations.Where(row=>!row.fired&&age>=row.due).ToArray())
        {
            invocation.fired=true;
            if(CameraShakerPlayback!=null)CameraShakerPlayback(invocation.camera,invocation.data);
            else NativeCameraShakeDriver.Play(invocation.camera,invocation.data.amount,NativeCurve.Keys(invocation.data.curve),
                invocation.data.hold?(float?)invocation.data.hold_at_sec:null,invocation.data.is_override);
            CameraShakerStarts++;
        }
        cameraShakerInvocations.RemoveAll(row=>row.fired);
    }
    bool TrySetPosition(MachineClock clock,NativeAction action)
    {
        var target=action.Param("gameObject");var space=action.Param("space");
        var every=action.Param("everyFrame");var late=action.Param("lateUpdate");var vector=action.Param("vector");
        var owner=ResolveObject(clock,target);
        if(owner==null||target.unresolved||space.unresolved||!String.IsNullOrEmpty(space.variable)||
           (space.number!=0&&space.number!=1)||every.unresolved||late.unresolved||
           !String.IsNullOrEmpty(every.variable)||!String.IsNullOrEmpty(late.variable)||every.number!=0||late.number!=0||
           vector.unresolved||!vector.none_known)return false;
        Vector3 vectorValue=vector.vector;bool vectorIsNone=vector.is_none;
        if(!String.IsNullOrEmpty(vector.variable))
        {
            if(vector.kind!=28||vector.is_none||!clock.vectors.TryGetValue(vector.variable,out vectorValue))return false;
            vectorIsNone=false;
        }
        var axisNone=new bool[3];var axes=Vector3.zero;
        int axisIndex=0;
        foreach(string axis in new[]{"x","y","z"})
        {
            var component=action.Param(axis);
            if(!component.none_known||component.unresolved)return false;
            axisNone[axisIndex]=component.is_none;
            if(component.is_none){axisIndex++;continue;}
            float coordinate;
            if(!String.IsNullOrEmpty(component.variable))
            {if(!clock.floats.TryGetValue(component.variable,out coordinate))return false;}
            else coordinate=Number(clock,component);
            if(axisIndex==0)axes.x=coordinate;else if(axisIndex==1)axes.y=coordinate;else axes.z=coordinate;
            axisIndex++;
        }
        return ApplySetPosition(owner.transform,(int)space.number,vectorIsNone,vectorValue,axisNone,axes,false,false);
    }
    public static bool ApplySetPosition(Transform target,int space,bool vectorIsNone,Vector3 vector,bool[] axisNone,
        Vector3 axes,bool everyFrame,bool lateUpdate)
    {
        if(target==null||(space!=0&&space!=1)||axisNone==null||axisNone.Length!=3||everyFrame||lateUpdate)return false;
        Vector3 value=vectorIsNone?(space==1?target.localPosition:target.position):vector;
        if(!axisNone[0])value.x=axes.x;if(!axisNone[1])value.y=axes.y;if(!axisNone[2])value.z=axes.z;
        if(space==1)target.localPosition=value;else target.position=value;
        return true;
    }
    bool TrySetParent(MachineClock clock,NativeAction action)
    {
        var targetParameter=action.Param("gameObject");var parentParameter=action.Param("parent");
        var world=action.Param("worldPositionStays");var resetPosition=action.Param("resetLocalPosition");
        var resetRotation=action.Param("resetLocalRotation");var resetScale=action.Param("resetLocalScale");
        var changeLayer=action.Param("changeToParentLayer");
        var flags=new[]{world,resetPosition,resetRotation,resetScale,changeLayer};
        if(targetParameter.unresolved||targetParameter.kind!=20||targetParameter.node<0||targetParameter.node>=Nodes.Length||
           parentParameter.unresolved||!parentParameter.none_known||
           String.IsNullOrEmpty(parentParameter.variable)&&!parentParameter.is_none&&parentParameter.node<0||
           flags.Any(p=>p.unresolved||!String.IsNullOrEmpty(p.variable)))return false;
        var target=ResolveObject(clock,targetParameter);var parent=parentParameter.is_none?null:ResolveObject(clock,parentParameter);
        if(target==null||Array.IndexOf(Nodes,target)<0||!parentParameter.is_none&&parent==null)return false;
        target.transform.SetParent(parent==null?null:parent.transform,Number(clock,world)!=0);
        if(Number(clock,resetPosition)!=0)target.transform.localPosition=Vector3.zero;
        if(Number(clock,resetRotation)!=0)target.transform.localRotation=Quaternion.identity;
        if(Number(clock,resetScale)!=0)target.transform.localScale=Vector3.one;
        if(Number(clock,changeLayer)!=0&&parent!=null)
        {int layer=parent.layer;foreach(var child in target.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;}
        return true;
    }
    bool TryDestroyObject(MachineClock clock,NativeAction action)
    {
        var targetParameter=action.Param("gameObject");var delay=action.Param("delay");var detach=action.Param("detachChildren");
        if(targetParameter.unresolved||(targetParameter.kind!=19&&targetParameter.kind!=20)||targetParameter.node<0||targetParameter.node>=Nodes.Length||
           delay.unresolved||!String.IsNullOrEmpty(delay.variable)||delay.number!=0||
           detach.unresolved||!String.IsNullOrEmpty(detach.variable)||detach.number!=0)return false;
        var target=ResolveObject(clock,targetParameter);
        if(target==null||Array.IndexOf(Nodes,target)<0)return false;
        target.SetActive(false);
        return true;
    }
    public bool DispatchMachineEvent(int machine,string eventName)
    {
        if(disposed)throw new ObjectDisposedException("NativeEffectInstance");
        if(machine<0||machine>=machines.Count||String.IsNullOrEmpty(eventName))return false;
        var clock=machines[machine];if(clock.state==null||clock.pending!=null)return false;
        int transitions=clock.transitions;float time=last<0?0:last;
        clock.pending=eventName;clock.pendingAt=time;AdvanceTransitions(clock,time);
        return clock.transitions>transitions;
    }
    void SetRendererVisible(Renderer renderer,bool visible)
    {
        if(renderer==null)return;
        if(desiredVisibility.ContainsKey(renderer)){desiredVisibility[renderer]=visible;renderer.enabled=visible&&renderer.sharedMaterials.Any(m=>m.GetFloat("_HasSourceTexture")>0);}
        else{if(!borrowedRendererVisibility.ContainsKey(renderer))borrowedRendererVisibility[renderer]=renderer.enabled;renderer.enabled=visible;}
    }
    void RestoreBorrowedVisibility()
    {
        foreach(var pair in borrowedActorVisibility)if(pair.Key!=null)pair.Key.SetShown(pair.Value);
        foreach(var pair in borrowedRendererVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;
    }
    void ActorVisibility(MachineClock clock,NativeAction action)
    {
        var owner=ResolveObject(clock,action.Param("m_GameObject"));var visible=action.Param("m_Visible");
        var flags=new[]{action.Param("m_IncludeChildren"),action.Param("m_IncludeParents"),action.Param("m_ResetOnExit"),action.Param("m_IgnoreSpells")};
        if(owner==null||visible.unresolved||!String.IsNullOrEmpty(visible.variable)&&!clock.floats.ContainsKey(visible.variable)||flags.Any(p=>p.unresolved||!String.IsNullOrEmpty(p.variable)))
        {UnboundActorVisibilityActions++;return;}
        var targets=new List<GameObject>();bool ancestorActor=false;
        if(flags[1].number!=0)for(var parent=owner.transform.parent;parent!=null;parent=parent.parent)
        {targets.Add(parent.gameObject);if(parent.GetComponent<NativeActorPresentation>()!=null){ancestorActor=true;break;}}
        if(!ancestorActor)
        {
            Action<Transform> collect=null;collect=t=>{targets.Add(t.gameObject);if(t.GetComponent<NativeActorPresentation>()!=null||flags[0].number==0)return;foreach(Transform child in t)collect(child);};collect(owner.transform);
        }
        var rendererReset=new Dictionary<Renderer,bool>();var actorReset=new Dictionary<NativeActorPresentation,bool>();
        foreach(var target in targets)
        {
            var actor=target.GetComponent<NativeActorPresentation>();
            if(actor!=null)
            {
                if(ancestorActor)rendererReset.Clear();actorReset[actor]=actor.Shown;
                if(!borrowedActorVisibility.ContainsKey(actor))borrowedActorVisibility[actor]=actor.Shown;
                actor.SetShown(Number(clock,visible)!=0);ActorRootVisibilityBindings++;
                // Only the client-owned visual root is bound; owned spells, armor,
                // shadows and highlight callbacks still require original Actor recovery.
                UnrestoredActorVisibilitySideEffects++;
            }
            else{var renderer=target.GetComponent<Renderer>();if(renderer!=null){rendererReset[renderer]=renderer.enabled;SetRendererVisible(renderer,Number(clock,visible)!=0);}}
        }
        if(flags[2].number!=0){clock.visibilityReset.Add(rendererReset);clock.actorVisibilityReset.Add(actorReset);}
    }
    void SetTexture(MachineClock clock,NativeAction action,int node)
    {
        var material=action.Param("material");var texture=action.Param("texture");var index=action.Param("materialIndex");var named=action.Param("namedTexture");
        // Direct material ownership is not recovered. Texture variables are local to each FSM.
        if(material.unresolved||material.kind!=32||!material.object_null||!String.IsNullOrEmpty(material.variable)||
           texture.unresolved||texture.kind!=33||index.unresolved||!String.IsNullOrEmpty(index.variable)||
           named.unresolved||!String.IsNullOrEmpty(named.variable)||node<0||node>=Nodes.Length)
        {UnboundTextureActions++;return;}
        int slot=(int)index.number;
        if(renderers[node]==null||slot<0||slot>=nodeMaterials[node].Length){UnboundTextureActions++;return;}
        string property=String.IsNullOrEmpty(named.text)?"_MainTex":named.text;
        var target=nodeMaterials[node][slot];
        if(!OriginalProperty(target,property)||!initialTextures[target].ContainsKey(property)){UnboundTextureActions++;return;}
        Texture value=null;
        if(!String.IsNullOrEmpty(texture.variable))
        {
            if(!clock.textures.TryGetValue(texture.variable,out value)){UnboundTextureActions++;return;}
        }
        else if(!texture.object_null)
        {
            if(texture.asset_source==null||texture.asset_source.type!="Texture2D"||String.IsNullOrEmpty(texture.asset_path)) {UnboundTextureActions++;return;}
            value=assets.TextureFile(texture.asset_path);if(value==null){UnboundTextureActions++;return;}
        }
        target.SetTexture(property,value);TextureBindings++;
        if(property=="_MainTex")target.SetFloat("_HasSourceTexture",value==null?0:1);
        var renderer=renderers[node];renderer.enabled=!deferred[node]&&desiredVisibility[renderer]&&nodeMaterials[node].Any(m=>m!=null&&m.GetFloat("_HasSourceTexture")>0);
    }
    static float Number(MachineClock clock,NativeParam p){return !String.IsNullOrEmpty(p.variable)&&clock.floats.ContainsKey(p.variable)?clock.floats[p.variable]:p.number;}
    float ClipSpeed(int node,string name)
    {Dictionary<string,float> speeds;float speed;return animationSpeed.TryGetValue(node,out speeds)&&speeds.TryGetValue(name,out speed)?speed:1;}
    void SetClipSpeed(MachineClock clock,NativeAction action,float when)
    {
        int node=action.Param("gameObject","m_GameObject").node;var named=action.Param("animName");string name=named.text;
        var value=action.Param("speed");float speed=Number(clock,value);
        if(node<0||node>=Nodes.Length||named.unresolved||!String.IsNullOrEmpty(named.variable)||String.IsNullOrEmpty(name)||value.unresolved||
           (!String.IsNullOrEmpty(value.variable)&&!clock.floats.ContainsKey(value.variable))||float.IsNaN(speed)||float.IsInfinity(speed))return;
        var refs=Program.nodes[node].animation_refs;
        if(!Program.animations.Any(c=>c.name==name&&(refs==null||refs.Contains(c.id))))return;
        Dictionary<string,float> speeds;if(!animationSpeed.TryGetValue(node,out speeds))animationSpeed[node]=speeds=new Dictionary<string,float>();
        speeds[name]=speed;
        foreach(var clip in clips.Where(c=>c.node==node&&c.clip.name==name))
        {
            // Rebase at the change time so paused/reversed clips do not jump.
            clip.offset+=(when-clip.entered)*clip.speed;clip.entered=when;clip.speed=speed;
        }
    }
    bool StartTween(MachineClock clock,NativeAction action,float when)
    {
        var owner=action.Param("gameObject");var ownerObject=ResolveObject(clock,owner);bool fade=action.name=="iTweenFadeToAction";
        var time=action.Param("time");var delay=action.Param("delay");var easing=action.Param("easeType");var loop=action.Param("loopType");
        // Original child clocks have no parent FSM callback registration.
        if(ownerObject==null||(fade&&!Nodes.Contains(ownerObject))||
           !TweenNumber(clock,delay)||!TweenNumber(clock,easing)||!TweenNumber(clock,loop)||
           action.Param("stopOnExit").unresolved||action.Param("startEvent").unresolved||action.Param("finishEvent").unresolved||
           Number(clock,loop)!=0||!NativeITween.SupportedEase((int)Number(clock,easing)))return false;
        float pause=delay.is_none?0:Number(clock,delay);
        if(float.IsNaN(pause)||float.IsInfinity(pause))return false;
        var tween=new TweenClock{action=action,clock=clock,state=clock.state,node=Array.IndexOf(Nodes,ownerObject),owner=ownerObject,fade=fade,ease=(int)Number(clock,easing),
            start=when+Mathf.Max(0,pause),duration=1,stop=action.Param("stopOnExit").is_none||Number(clock,action.Param("stopOnExit"))!=0};
        if(fade)
        {
            var alpha=action.Param("m_Alpha");if(!TweenNumber(clock,alpha)||!TweenNumber(clock,action.Param("m_IncludeChildren")))return false;
            tween.alpha=Number(clock,alpha);tween.children=Number(clock,action.Param("m_IncludeChildren"))!=0;
            if(!TweenNumber(clock,time)||time.is_none)return false;tween.duration=Number(clock,time);
            if(float.IsNaN(tween.alpha)||float.IsInfinity(tween.alpha))return false;
        }
        else
        {
            var speed=action.Param("speed");var transform=action.Param("transformScale");var vector=action.Param("vectorScale");
            if(!speed.none_known||!TweenNumber(clock,speed)||!transform.none_known||transform.unresolved||vector.unresolved||!vector.none_known)return false;
            tween.to=vector.is_none?Vector3.zero:vector.vector;
            if(!String.IsNullOrEmpty(vector.variable)&&!clock.vectors.TryGetValue(vector.variable,out tween.to))return false;
            if(!transform.is_none&&!transform.object_null)
            {
                var reference=ResolveObject(clock,transform);if(reference==null)return false;
                tween.to=reference.transform.localScale+tween.to;
            }
            for(int axis=0;axis<3;axis++)if(float.IsNaN(tween.to[axis])||float.IsInfinity(tween.to[axis]))return false;
            if(!speed.is_none)
            {tween.speed=Number(clock,speed);if(tween.speed<=0||float.IsNaN(tween.speed)||float.IsInfinity(tween.speed))return false;}
            else
            {if(!time.none_known||!TweenNumber(clock,time))return false;tween.duration=time.is_none?1:Number(clock,time);}
        }
        if(tween.duration<=0||float.IsNaN(tween.duration)||float.IsInfinity(tween.duration))return false;
        if(tween.fade&&tween.children)
        {
            foreach(int child in Enumerable.Range(0,Nodes.Length).Where(i=>i!=tween.node&&Nodes[i].transform.IsChildOf(Nodes[tween.node].transform)).Reverse())
                tweens.Add(new TweenClock{action=action,clock=clock,state=clock.state,node=child,owner=Nodes[child],fade=true,ease=tween.ease,
                    start=tween.start,duration=tween.duration,alpha=tween.alpha,registered=false});
            tween.children=false;
        }
        RememberScale(ownerObject);
        if(action.name=="iTweenScaleFrom")
        {
            // Original ScaleFrom immediately writes the requested starting scale,
            // then launches ScaleTo with the captured destination (before delay).
            Vector3 destination=ownerObject.transform.localScale;
            ownerObject.transform.localScale=tween.to;tween.to=destination;
        }
        tweens.Add(tween);clock.tweenWatches.Add(tween);return true;
    }
    static bool TweenNumber(MachineClock clock,NativeParam parameter)
    {return !parameter.unresolved&&(String.IsNullOrEmpty(parameter.variable)||clock.floats.ContainsKey(parameter.variable));}
    void TweenEvent(TweenClock tween,string name,float when)
    {
        if(!tween.registered||tween.clock.state!=tween.state)return;
        var evt=tween.action.Param(name);
        if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){tween.clock.pending=evt.text;tween.clock.pendingAt=when;}
    }
    void UpdateTweens(float age)
    {
        foreach(var tween in tweens)
        {
            if(tween.finished||age<tween.start)continue;
            if(tween.owner==null)continue;
            if(!tween.started)
            {
                tween.started=true;tween.from=tween.owner.transform.localScale;
                if(tween.speed>0)tween.duration=Vector3.Distance(tween.from,tween.to)/tween.speed;
                if(tween.fade)
                {
                    var selected=Enumerable.Range(0,Nodes.Length).Where(i=>i==tween.node||tween.children&&Nodes[i].transform.IsChildOf(Nodes[tween.node].transform));
                    tween.materials=selected.SelectMany(i=>nodeMaterials[i]??new Material[0]).Where(m=>OriginalProperty(m,"_Color")).Distinct().ToArray();
                    tween.colors=tween.materials.Select(m=>m.GetColor("_Color")).ToArray();
                }
                TweenEvent(tween,"startEvent",tween.start);
            }
            float fraction=tween.duration==0?1:(age-tween.start)/tween.duration;
            if(tween.fade)
            {
                for(int i=0;i<tween.materials.Length;i++)
                {Color color=tween.colors[i];color.a=fraction>=1?tween.alpha:NativeITween.Sample(color.a,tween.alpha,fraction,tween.ease);tween.materials[i].SetColor("_Color",color);}
            }
            else
            {
                Vector3 scale=new Vector3();for(int i=0;i<3;i++)scale[i]=NativeITween.Sample(tween.from[i],tween.to[i],fraction,tween.ease);
                tween.owner.transform.localScale=fraction>=1?tween.to:scale;
            }
            if(fraction>=1)
            {tween.finished=true;if(tween.registered&&tween.clock.state==tween.state)CompletedITweenActions++;TweenEvent(tween,"finishEvent",tween.start+tween.duration);}
        }
        tweens.RemoveAll(t=>t.finished);
    }
    void RegisterGeometryMaterial(Material original,Material copy)
    {
        NativeMaterial source;if(original==null||!materialSources.TryGetValue(original,out source))return;
        materialSources[copy]=source;declaredProperties[copy]=new HashSet<string>(declaredProperties[original]);
        initialTextures[copy]=copy.GetTexturePropertyNames().ToDictionary(name=>name,name=>copy.GetTexture(name));initialTextureFlags[copy]=copy.GetFloat("_HasSourceTexture");
    }
    void ReleaseGeometryMaterial(Material material)
    {materialSources.Remove(material);declaredProperties.Remove(material);initialTextures.Remove(material);initialTextureFlags.Remove(material);}
    void ReplaceGeometry(MachineClock clock,NativeAction action)
    {
        var target=action.Param("gameObject");var owner=ResolveObject(clock,target);
        if(target.unresolved){UnboundGeometryActions++;return;}
        // Source UpdateCache returns without mutation if the target lacks Renderer.
        if(owner==null||owner.GetComponent<Renderer>()==null)return;
        var value=action.Param(action.name=="SetMesh"?"mesh":"material");
        if(value.unresolved||!String.IsNullOrEmpty(value.variable)){UnboundGeometryActions++;return;}
        if(action.name=="SetMesh")
        {
            if(!value.object_null&&(value.asset_type!="Mesh"||String.IsNullOrEmpty(value.asset_path))){UnboundGeometryActions++;return;}
            if(geometry.SetMesh(owner,value.object_null?null:assets.MeshFile(value.asset_path)))MeshReplacements++;
        }
        else
        {
            var slot=action.Param("materialIndex");Material material=null;
            if(slot.unresolved||!String.IsNullOrEmpty(slot.variable)&&!clock.floats.ContainsKey(slot.variable)||!value.object_null&&!replacementMaterials.TryGetValue(value.asset_path??"",out material)){UnboundGeometryActions++;return;}
            if(!geometry.SetMaterial(owner,(int)Number(clock,slot),material))return;
            MaterialReplacements++;int index=Array.IndexOf(Nodes,owner);
            if(index>=0)nodeMaterials[index]=owner.GetComponent<Renderer>().sharedMaterials;
        }
    }
    bool AudioOwnerRemoved(GameObject owner)
    {if(owner==null)return false;for(var t=owner.transform;t!=null;t=t.parent)if(destroyedAudioOwners.Contains(t.gameObject))return true;return false;}
    GameObject ResolveObject(MachineClock clock,NativeParam parameter)
    {
        if(parameter.unresolved)return null;
        if(!String.IsNullOrEmpty(parameter.variable))
        {
            GameObject value;if(clock.objects.TryGetValue(parameter.variable,out value))return AudioOwnerRemoved(value)?null:value;
            if(parameter.node<0||parameter.node>=Nodes.Length)return null;
            value=Nodes[parameter.node];clock.objects[parameter.variable]=value;return AudioOwnerRemoved(value)?null:value;
        }
        var resolved=parameter.node>=0&&parameter.node<Nodes.Length?Nodes[parameter.node]:null;return AudioOwnerRemoved(resolved)?null:resolved;
    }
    void RememberScale(GameObject owner)
    {if(owner!=null&&!Nodes.Contains(owner)&&!borrowedScales.ContainsKey(owner.transform))borrowedScales[owner.transform]=owner.transform.localScale;}
    bool SetScale(MachineClock clock,NativeAction action)
    {
        var owner=ResolveObject(clock,action.Param("gameObject"));if(owner==null)return false;
        var vector=action.Param("vector");var axes=new[]{action.Param("x"),action.Param("y"),action.Param("z")};
        if(!vector.none_known||vector.unresolved||axes.Any(p=>!p.none_known||p.unresolved))return false;
        Vector3 value=owner.transform.localScale;
        if(!vector.is_none)
        {
            if(String.IsNullOrEmpty(vector.variable))value=vector.vector;
            else if(!clock.vectors.TryGetValue(vector.variable,out value))return false;
        }
        for(int axis=0;axis<3;axis++)
        {
            var p=axes[axis];if(p.is_none)continue;
            float component=p.number;if(!String.IsNullOrEmpty(p.variable)&&!clock.floats.TryGetValue(p.variable,out component))return false;
            if(float.IsNaN(component)||float.IsInfinity(component))return false;value[axis]=component;
        }
        RememberScale(owner);owner.transform.localScale=value;return true;
    }
    bool SetLight(MachineClock clock,NativeAction action)
    {
        var owner=ResolveObject(clock,action.Param("gameObject"));var value=action.Param("lightIntensity");
        if(value.unresolved||!String.IsNullOrEmpty(value.variable)&&!clock.floats.ContainsKey(value.variable))return false;
        var light=owner==null?null:owner.GetComponent<Light>();if(light!=null)light.intensity=Number(clock,value);return true;
    }
    bool SetSourceAlpha(MachineClock clock,NativeAction action)
    {
        var value=action.Param("m_Alpha");if(value.unresolved||!value.none_known)return false;if(value.is_none)return true;
        if(!String.IsNullOrEmpty(value.variable)&&!clock.floats.ContainsKey(value.variable))return false;
        var owner=ResolveObject(clock,action.Param("m_GameObject"));if(owner==null)return !action.Param("m_GameObject").unresolved;
        foreach(var renderer in owner.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
        {
            string property=OriginalProperty(material,"_Color")?"_Color":OriginalProperty(material,"_TintColor")?"_TintColor":null;
            if(property!=null){var color=material.GetColor(property);color.a=Number(clock,value);material.SetColor(property,color);}
        }
        return true;
    }
    void SetAmbientVariable(MachineClock clock,NativeAction action,bool enter)
    {
        var variable=action.Param("m_Color");if(String.IsNullOrEmpty(variable.variable))return;
        // GetBoard prefers Board.m_AmbientColor; GetAmbient OnUpdate reads RenderSettings.
        Color? board=enter&&boardAmbient!=null?boardAmbient():null;
        clock.colors[variable.variable]=board??RenderSettings.ambientLight;
    }
    TrackBinding BindTrack(NativeAnimationTrack track,int node)
    {return BindTrack(track,Nodes[node]);}
    TrackBinding BindTrack(NativeAnimationTrack track,GameObject owner)
    {
        var transform=String.IsNullOrEmpty(track.path)?owner.transform:owner.transform.Find(track.path);
        var renderer=transform==null?null:transform.GetComponent<Renderer>();
        return new TrackBinding{track=track,transform=transform,renderer=renderer,materials=renderer==null?new Material[0]:renderer.sharedMaterials};
    }
    bool BorrowAnimation(GameObject owner,TrackBinding[] bindings)
    {
        NativeEffectInstance writer;if(borrowedAnimationOwners.TryGetValue(owner,out writer)&&writer!=this)return false;
        // m_LocalEulerAnglesHint is an editor serialization hint; quaternion
        // curves are the authored runtime rotation. Do not convert hints to Euler motion.
        if(bindings.Any(b=>b.transform==null||!(b.track.property.StartsWith("position.")||b.track.property.StartsWith("rotation.")||b.track.property.StartsWith("scale.")||b.track.property.StartsWith("euler.")||b.track.property.StartsWith("m_LocalEulerAnglesHint."))))return false;
        if(!borrowedAnimationRoots.Contains(owner)&&owner.GetComponent<Animation>()!=null)return false;
        borrowedAnimationOwners[owner]=this;borrowedAnimationRoots.Add(owner);
        foreach(var binding in bindings)
        {var t=binding.transform;if(!borrowedAnimationPoses.ContainsKey(t))borrowedAnimationPoses[t]=new BorrowedPose{position=t.localPosition,rotation=t.localRotation,scale=t.localScale};}
        return true;
    }
    void RestoreBorrowedAnimationPoses()
    {foreach(var pair in borrowedAnimationPoses)if(pair.Key!=null){pair.Key.localPosition=pair.Value.position;pair.Key.localRotation=pair.Value.rotation;pair.Key.localScale=pair.Value.scale;}}
    void ReleaseBorrowedAnimations()
    {
        RestoreBorrowedAnimationPoses();borrowedAnimationPoses.Clear();
        foreach(var owner in borrowedAnimationRoots)
        {Animation mixer;if(legacyMixers.TryGetValue(owner,out mixer)){if(mixer!=null)AssetRegistry.ReleaseTransient(mixer);legacyMixers.Remove(owner);}borrowedAnimationOwners.Remove(owner);}
        borrowedAnimationRoots.Clear();
    }
    static Color ColorValue(MachineClock clock,NativeParam p){return !String.IsNullOrEmpty(p.variable)&&clock.colors.ContainsKey(p.variable)?clock.colors[p.variable]:p.color;}
    bool SetOriginalProperty(MachineClock clock,NativeAction action)
    {
        var parameter=action.Param("targetProperty");var data=parameter.property_data;
        if(parameter.unresolved||data==null||data.target_unresolved)return false;
        if(data.target_null)return true;
        GameObject owner=null;
        if(!String.IsNullOrEmpty(data.target_variable))
        {if(!clock.objects.TryGetValue(data.target_variable,out owner))return false;if(owner==null)return true;}
        else if(data.target_node>=0&&data.target_node<Nodes.Length)owner=Nodes[data.target_node];
        if(owner==null||!Nodes.Contains(owner))return false; // Borrowed property snapshot/restore is not recovered.
        try
        {
            var target=NativePropertyAction.Target(data,owner);
            var key=Tuple.Create(target,data.property_name);
            if(!propertyRestore.ContainsKey(key))propertyRestore[key]=NativePropertyAction.Restore(target,data.property_name);
            bool written=NativePropertyAction.Set(data,target,(p,type)=>
            {
                if(type==typeof(Color))
                {if(!String.IsNullOrEmpty(p.variable)&&!clock.colors.ContainsKey(p.variable))throw new NotSupportedException("Original color variable missing");return ColorValue(clock,p);}
                if(type==typeof(Vector3)||type==typeof(Vector2))
                {Vector3 vector=p.vector;if(!String.IsNullOrEmpty(p.variable)&&!clock.vectors.TryGetValue(p.variable,out vector))throw new NotSupportedException("Original vector variable missing");return type==typeof(Vector3)?(object)vector:(Vector2)vector;}
                if(type==typeof(Quaternion)||type==typeof(Rect))
                {if(!String.IsNullOrEmpty(p.variable))throw new NotSupportedException("Original quaternion/rect variable binding is unrestored");return type==typeof(Quaternion)?(object)new Quaternion(p.color.r,p.color.g,p.color.b,p.color.a):new Rect(p.color.r,p.color.g,p.color.b,p.color.a);}
                if(type==typeof(string)){if(!String.IsNullOrEmpty(p.variable))throw new NotSupportedException("Original string variable binding is unrestored");return p.text;}
                if(type==typeof(int))
                {if(!String.IsNullOrEmpty(p.variable)||!p.integer_known)throw new NotSupportedException("Exact original integer binding is unrestored");return p.integer;}
                if(!String.IsNullOrEmpty(p.variable)&&!clock.floats.ContainsKey(p.variable))throw new NotSupportedException("Original numeric variable missing");
                float value=Number(clock,p);
                return type==typeof(bool)?(object)(value!=0):type==typeof(int)?(object)(int)value:value;
            });
            if(written)
            {
                PropertyWrites++;
                var renderer=target as Renderer;
                if(renderer!=null&&data.property_name=="enabled")desiredVisibility[renderer]=renderer.enabled;
            }
            return true;
        }
        catch(NotSupportedException){return false;}
        catch(TargetInvocationException){return false;}
        catch(ArgumentException){return false;}
    }
    bool SetRGBA(MachineClock clock,NativeAction action)
    {
        var target=action.Param("colorVariable");if(target.object_null)return true;
        if(target.unresolved)return false;
        Color current;
        if(!String.IsNullOrEmpty(target.variable))
        {if(!clock.colors.TryGetValue(target.variable,out current))return false;}
        else if(!rgbaLocalColors.TryGetValue(action,out current))current=target.color;
        try
        {
            var changed=NativeRGBAAction.Apply(current,action,p=>
            {
                if(!String.IsNullOrEmpty(p.variable)&&!clock.floats.ContainsKey(p.variable))throw new ArgumentException("Original RGBA float variable is unrestored");
                return Number(clock,p);
            }).Value;
            if(!String.IsNullOrEmpty(target.variable))clock.colors[target.variable]=changed;
            else rgbaLocalColors[action]=changed;
            RGBAWrites++;return true;
        }
        catch(ArgumentException){return false;}
    }
    void SetMaterial(MachineClock clock,NativeAction action,int node)
    {
        string name=action.Param("namedFloat","namedColor","property","propertyName").text;if(renderers[node]==null)return;
        foreach(var mat in nodeMaterials[node])if(OriginalProperty(mat,name))
        {if(action.name=="SetMaterialColor")mat.SetColor(name,ColorValue(clock,action.Param("color")));else mat.SetFloat(name,Number(clock,action.Param("floatValue","value")));}
    }
    void WriteInterpolatedColor(MachineClock clock,NativeAction action,Color value)
    {var target=action.Param("storeColor");if(!String.IsNullOrEmpty(target.variable))clock.colors[target.variable]=value;}
    bool StopSource(MachineClock clock,NativeAction action)
    {
        var parameter=action.Param("m_GameObject");if(parameter.unresolved)return false;
        var owner=ResolveObject(clock,parameter);if(owner==null)return true;
        int node=Array.IndexOf(Nodes,owner);if(node<0||audioDefinition==null)return false;
        var source=NativeAudio.Node(audioDefinition,Program.nodes[node].path,node);
        if(source==null||source.audio_sources==null||source.audio_sources.Length==0)return true;
        if(source.audio_sources.Length!=1)return false;
        StopOwnedSource(owner);return true;
    }
    void StopOwnedSource(GameObject owner)
    {
        NativeAudioCue cue;if(sourceAudioCues.TryGetValue(owner,out cue))cue.stopPlayback?.Invoke();
        int version;audioStopVersions.TryGetValue(owner,out version);audioStopVersions[owner]=version+1;AudioSourceStops++;
        // Preserve objects for deterministic rewind, but invalidate all original
        // references and hide the subtree as FinishGeneratedSource would destroy it.
        if(generatedAudioOwners.Contains(owner)&&destroyedAudioOwners.Add(owner)){owner.SetActive(false);GeneratedAudioOwnerRemovals++;}
    }
    bool StartAudioFade(MachineClock clock,NativeAction action)
    {
        bool loopIn=action.name=="AudioLoopingStartAction",loopOut=action.name=="AudioLoopingEndAction";
        var explicitSource=action.Param("m_AudioSource");
        bool explicitBinding=explicitSource.name!=null&&!explicitSource.object_null;
        if(explicitSource.unresolved)return false;
        var parameter=explicitBinding?explicitSource:action.Param("m_GameObject");if(parameter.unresolved)return false;
        if(explicitBinding&&(explicitSource.asset_type!="AudioSource"||explicitSource.asset_source==null))return false;
        var owner=ResolveObject(clock,parameter);if(owner==null)return true;
        int node=Array.IndexOf(Nodes,owner);if(node<0||audioDefinition==null)return false;
        var source=NativeAudio.Node(audioDefinition,Program.nodes[node].path,node);
        bool sourceExists=source!=null&&source.audio_sources!=null&&source.audio_sources.Length>0;
        if(!sourceExists&&!loopOut)return !explicitBinding;
        if(sourceExists&&source.audio_sources.Length!=1)return false;
        if(explicitBinding&&(!sourceExists||source.audio_sources[0].source==null||source.audio_sources[0].source.pathid!=explicitSource.asset_source.pathid||!String.Equals(source.audio_sources[0].source.cab,explicitSource.asset_source.cab,StringComparison.OrdinalIgnoreCase)))return false;
        if(loopIn&&(source.sound_defs==null||source.sound_defs.Length==0))return true;
        float volume;if(!playthroughVolumes.TryGetValue(owner,out volume))volume=1;
        NativeAudioFade fade;
        try{fade=new NativeAudioFade(action,volume,p=>Number(clock,p),audioRealtime);}
        catch(ArgumentException){return false;}
        if(loopIn)
        {
            var random=action.Param("m_randomizeStartPoint");var persist=action.Param("m_dontDestroySource");
            if(random.unresolved||persist.unresolved||!random.none_known||!persist.none_known)return false;
            var cue=NativeAudio.Resolve(audioDefinition,Program.nodes[node].path,clock.entered,volume,()=> (float)audioRandom.NextDouble(),node);
            if(cue==null)return false;
            loopingAudioOwners.Add(owner);if(Number(clock,persist)==0)generatedAudioOwners.Add(owner);
            cue.loop=true;BindOwnedAudioCue(cue,owner);
            cue.randomStart=Number(clock,random)!=0?(float)audioRandom.NextDouble():0;cue.randomizeStart=Number(clock,random)!=0;
            audioCues.Add(cue);if(audioCallback!=null)audioCallback(cue);LoopingAudioStarts++;
        }
        if(fade.InitialWrite){playthroughVolumes[owner]=fade.Value;AudioVolumeWrites++;}
        if(!fade.Finished){clock.audioFades[action]=new AudioFadeWatch{owner=owner,fade=fade,sourceExists=sourceExists};AudioFadeStarts++;}
        return true;
    }
    bool SetSourceVolume(MachineClock clock,NativeAction action)
    {
        var parameter=action.Param("m_GameObject");if(parameter.unresolved)return false;
        var owner=ResolveObject(clock,parameter);if(owner==null)return true;
        int node=Array.IndexOf(Nodes,owner);if(node<0||audioDefinition==null)return false;
        var source=NativeAudio.Node(audioDefinition,Program.nodes[node].path,node);
        if(source==null||source.audio_sources==null||source.audio_sources.Length==0)return true;
        if(source.audio_sources.Length!=1)return false;
        var volume=action.Param("m_Volume");
        if(volume.unresolved||!volume.none_known)return false;
        if(volume.is_none)return true;
        if(!String.IsNullOrEmpty(volume.variable)&&!clock.floats.ContainsKey(volume.variable))return false;
        playthroughVolumes[owner]=Number(clock,volume);AudioVolumeWrites++;return true;
    }
    void UpdateVariables(MachineClock clock,float time)
    {
        if(clock.state==null)return;
        float colorDelta=Mathf.Max(0,clock.entered+time-clock.colorSampleAt);clock.colorSampleAt=clock.entered+time;
        foreach(var a in clock.state.actions)
        {
            NativeMainShakeAction mainShake;
            if(colorDelta>0&&clock.mainShakes.TryGetValue(a,out mainShake)&&mainShake.Step(colorDelta))
            {
                MainShakeFinishes++;var evt=a.Param("m_FinishedEvent");if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=clock.entered+time;}
            }
            AudioFadeWatch audioFade;
            if(colorDelta>0&&clock.audioFades.TryGetValue(a,out audioFade)&&!audioFade.fade.Finished)
            {
                audioFade.fade.Step(colorDelta);if(audioFade.sourceExists&&!AudioOwnerRemoved(audioFade.owner)){playthroughVolumes[audioFade.owner]=audioFade.fade.Value;AudioVolumeWrites++;}
                if(audioFade.fade.Finished){AudioFadeFinishes++;if(audioFade.fade.StopWhenFinished&&audioFade.sourceExists&&!AudioOwnerRemoved(audioFade.owner))StopOwnedSource(audioFade.owner);}
            }
            NativeColorInterpolation interpolation;
            if(clock.colorInterpolations.TryGetValue(a,out interpolation)&&!interpolation.Finished)
            {
                interpolation.Step(colorDelta);WriteInterpolatedColor(clock,a,interpolation.Value);
                if(interpolation.Finished)
                {ColorInterpolationFinishes++;var evt=a.Param("finishEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=clock.entered+time;}}
            }
            if(clock.delayedVisibility.ContainsKey(a)&&!clock.delayedVisibility[a])
            {
                var delay=a.Param("m_Delay");
                if(delay.none_known&&!delay.unresolved&&(delay.is_none||String.IsNullOrEmpty(delay.variable)||clock.floats.ContainsKey(delay.variable))&&!(time<(delay.is_none?0:Number(clock,delay))))
                {
                    var target=ResolveObject(clock,a.Param("gameObject"));
                    if(target!=null)
                    {
                        var selected=a.Param("includeChildren").number!=0?target.GetComponentsInChildren<Renderer>():new[]{target.GetComponent<Renderer>()};
                        foreach(var renderer in selected)if(renderer!=null)SetRendererVisible(renderer,Number(clock,a.Param("visible"))!=0);
                    }
                    clock.delayedVisibility[a]=true;
                    DelayedVisibilityFinishes++;
                }
            }
            NativeColorAnimation colorAnimation;NativeEaseAnimation easeAnimation;
            if(clock.colorAnimations.TryGetValue(a,out colorAnimation))
            {
                bool wasFinished=colorAnimation.Finished;var variable=a.Param("colorVariable");
                if(colorDelta>0&&colorAnimation.Step(colorDelta)&&!variable.is_none)clock.colors[variable.variable]=colorAnimation.Value;
                if(!wasFinished&&colorAnimation.Finished){var evt=a.Param("finishEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=clock.entered+time;}}
            }
            if(clock.easeAnimations.TryGetValue(a,out easeAnimation))
            {
                bool wasFinished=easeAnimation.Finished;bool color=a.name=="EaseColor";var variable=a.Param(color?"colorVariable":"floatVariable");
                if(colorDelta>0&&easeAnimation.Step(colorDelta)&&!variable.is_none){if(color)clock.colors[variable.variable]=easeAnimation.Value;else clock.floats[variable.variable]=easeAnimation.Value.r;}
                if(!wasFinished&&easeAnimation.Finished){var evt=a.Param("finishEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=clock.entered+time;}}
            }
            if(a.name=="GetAmbientColorAction"&&a.Param("m_EveryFrame").number!=0){SetAmbientVariable(clock,a,false);continue;}
            if(a.name=="AnimateFloat")
            {
                var variable=a.Param("floatVariable");var curve=a.Param("animCurve");
                if(!clock.finishedFloats.Contains(a))
                {
                    if(!String.IsNullOrEmpty(variable.variable))clock.floats[variable.variable]=curve.SampleCurve(time);
                    if(time>=curve.CurveDuration)
                    {
                        // 原 AnimateFloat.OnUpdate 在曲线结束发 finishEvent；该事件也能退出含 everyFrame 写入的状态。
                        bool looping=curve.wrap_known&&(curve.post_wrap==0||curve.post_wrap==1);
                        if(!looping)clock.finishedFloats.Add(a);
                        var evt=a.Param("finishEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=clock.entered+time;}
                    }
                }
            }
            if(a.name=="SetLightIntensity"&&a.Param("everyFrame").number!=0)if(!SetLight(clock,a))UnboundComponentActions++;
            if(a.name=="SetAlphaRecursiveAction"&&a.Param("m_EveryFrame").number!=0)if(!SetSourceAlpha(clock,a))UnboundComponentActions++;
            if(a.name=="AudioSetVolumeAction"&&a.Param("m_EveryFrame").number!=0)
                if(!SetSourceVolume(clock,a))UnboundAudioVolumeActions++;
            if(a.name=="SetProperty"&&a.Param("everyFrame").number!=0)
                if(!SetOriginalProperty(clock,a))UnboundPropertyActions++;
            if(a.name=="SetColorRGBA"&&a.Param("everyFrame").number!=0)
                if(!SetRGBA(clock,a))UnboundColorActions++;
            if((a.name=="SetMaterialFloat"||a.name=="SetMaterialColor")&&a.Param("everyFrame").number!=0)
            {int node=a.Param("gameObject","m_GameObject").node;if(node>=0)SetMaterial(clock,a,node);}
            if(a.name=="SetScale"&&a.Param("everyFrame").number!=0&&a.Param("lateUpdate").number==0)SetScale(clock,a);
            if(a.name=="SetAnimationSpeed"&&a.Param("everyFrame").number!=0)SetClipSpeed(clock,a,clock.entered+time);
        }
        UpdateAnimationActions(clock,clock.entered+time);
        foreach(var random in clock.randomWaits)
        {
            float age=clock.entered+time;
            if(random.wait.Step(Mathf.Max(0,age-random.sampledAt),age))
            {RandomWaitFinishes++;var evt=random.action.Param("m_FinishEvent");if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=age;}}
            random.sampledAt=age;
        }
        foreach(var wait in clock.waitWatches)
            if(!wait.finished&&clock.entered+time>=wait.at)
            {wait.finished=true;var evt=wait.action.Param("finishEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=wait.at;}}
        if(clock.colorAnimations.Count+clock.colorInterpolations.Count+clock.easeAnimations.Count+clock.animationWatches.Count+clock.tweenWatches.Count+clock.randomWaits.Count+clock.clipAudio.Count+clock.cardIdAudio.Count+clock.playthrough.Count+clock.ordinaryAudio.Count+clock.delayedVisibility.Count+clock.audioFades.Count+clock.mainShakes.Count>0&&clock.mainShakes.Values.All(a=>a.Finished)&&clock.audioFades.Values.All(a=>a.fade.Finished)&&clock.playthrough.All(a=>a.finished)&&clock.ordinaryAudio.All(a=>a.finished)&&clock.colorInterpolations.Values.All(a=>a.Finished)&&clock.delayedVisibility.Values.All(done=>done)&&clock.colorAnimations.Values.All(a=>a.Finished)&&clock.easeAnimations.Values.All(a=>a.Finished)&&clock.animationWatches.All(a=>a.finished)&&clock.tweenWatches.All(a=>a.finished)&&clock.randomWaits.All(a=>a.wait.Finished)&&clock.clipAudio.All(a=>a.delay.Finished)&&clock.cardIdAudio.All(a=>a.finished)&&clock.state.transitions.Any(t=>t.@event=="FINISHED"))clock.finish=Mathf.Max(clock.otherFinish,clock.entered+time);
    }
    void UpdateAnimationActions(MachineClock clock,float age)
    {
        foreach(var watch in clock.animationWatches)
        {
            if(watch.finished)continue;
            var clip=watch.clip;float time=(age-clip.entered)*clip.speed+clip.offset;
            bool enabled=clips.Contains(clip)&&!(clip.weightTo==0&&age>=clip.fadeStarted+clip.fadeDuration);
            bool onceEnded=enabled&&clip.wrapMode==1&&time>clip.clip.duration;
            if(!enabled||onceEnded||clip.wrapMode==8&&time>clip.clip.duration)
            {
                if(onceEnded){SampleClips(age);clips.Remove(clip);}
                watch.finished=true;CompletedAnimationActions++;
                var evt=watch.action.Param("finishEvent");
                if(evt.unresolved)UnboundAnimationActions++;
                else if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=age;}
            }
            // Native disabled legacy states reset time; their stale clock must
            // not overwrite a stop finishEvent with an unrelated loopEvent.
            if(!enabled||onceEnded)time=0;
            // Source OnUpdate compares against its OnEnter snapshot; it does
            // not update prevAnimtTime. Do not invent a once-per-cycle gate.
            if(clip.wrapMode!=8&&time>clip.clip.duration&&watch.enteredTime<clip.clip.duration)
            {
                var evt=watch.action.Param("loopEvent");
                if(evt.unresolved)UnboundAnimationActions++;
                else if(!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=age;}
            }
        }
    }
    public void Seek(float age)
    {
        long timing=StartH10Timing();
        if(disposed)throw new ObjectDisposedException("NativeEffectInstance");
        age=Mathf.Max(0,age);bool restart=last<0||age<last;
        if(restart){foreach(var trail in trails)trail.Reset();ResetClocks();last=0;UpdateAudio(0);foreach(var clock in machines)AdvanceTransitions(clock,0);SampleClips(0);SampleTimelines(0);}
        int previousAttackActions=SourceAttackActionsStarted;
        sourcePresentationFrame++;sourceAttackWait.Resume(sourcePresentationFrame,age,SourceBaseAction);
        bool demonHunter=Program.reference==NativeDemonHunterHeroPowerPlayback.RootReference||
            Program.reference==NativeDemonHunterHeroPowerPlayback.StartReference||
            Program.reference==NativeDemonHunterHeroPowerPlayback.ImpactReference;
        if(demonHunter&&!restart&&age==last&&SourceAttackActionsStarted==previousAttackActions)
        {H10SameAgeSeekSkips++;ReportH10Timing("Seek.same-age-skip",timing);return;}
        // Fixed sampling makes a late network frame, 2x playback and backwards
        // seeking traverse the same FSM/transform/particle history.
        ApplyLifecycle(last);
        while(last<age)
        {
            float next=Mathf.Min(age,last+1f/120);
            if(lifecycleCursor<lifecycleEvents.Length&&lifecycleEvents[lifecycleCursor].time>last)next=Mathf.Min(next,lifecycleEvents[lifecycleCursor].time);
            foreach(var state in sourceStates)state.Tick(next);
            Advance(next);last=next;ApplyLifecycle(last);
        }
        foreach(var trail in trails)trail.Sample(age);
        // Also enforce isolation at t=0, before the first simulation step.
        for(int i=0;i<Nodes.Length;i++)if(deferred[i]&&renderers[i]!=null)renderers[i].enabled=false;
        foreach(var mat in materials)mat.SetFloat("_Clock",age);
        foreach(var pair in materialSources)
        {
            var source=pair.Value;var mat=pair.Key;
            if(String.IsNullOrEmpty(source.src_property))continue;
            int src=(int)mat.GetFloat(source.src_property),dst=(int)mat.GetFloat(source.dst_property);
            mat.SetInt("_SrcBlend",src);mat.SetInt("_DstBlend",dst);
            mat.SetInt("_OriginalSrcBlend",src);mat.SetInt("_OriginalDstBlend",dst);
            mat.SetInt("_OriginalSrcAlpha",(int)mat.GetFloat(source.src_alpha_property));
            mat.SetInt("_OriginalDstAlpha",(int)mat.GetFloat(source.dst_alpha_property));
            mat.SetInt("_CaptureSrcAlpha",dst==1?0:1);mat.SetInt("_CaptureDstAlpha",dst==1?1:10);
        }
        foreach(var notification in finishNotifications)notification.delivered=notification.time<=age;
        ReportH10Timing("Seek",timing);
    }
    public void SetLifecycleTimeline(IEnumerable<NativeLifecycleEvent> events)
    {
        if(events==null)throw new ArgumentNullException("events");
        var timeline=events.Take(257).ToArray();
        if(timeline.Length>256||timeline.Any(e=>e==null||e.machine<0||e.machine>=Program.machines.Length||e.time<0||float.IsNaN(e.time)||float.IsInfinity(e.time)||(int)e.type<0||(int)e.type>6))throw new ArgumentException("Invalid original lifecycle timeline");
        lifecycleEvents=timeline.OrderBy(e=>e.time).ToArray();last=-1;
    }
    void ApplyLifecycle(float age)
    {
        while(lifecycleCursor<lifecycleEvents.Length&&lifecycleEvents[lifecycleCursor].time<=age)
        {
            var request=lifecycleEvents[lifecycleCursor++];var clock=machines[request.machine];
            var destination=NativeSpellLifecycle.Destination(clock.machine,request.type);
            if(destination==null){UnboundLifecycleEvents++;continue;}
            Enter(clock,destination.state,request.time);AdvanceTransitions(clock,request.time);LifecycleChanges++;
        }
    }
    void AdvanceTransitions(MachineClock clock,float age)
    {
        int transitions=0;
        while((clock.pending!=null||clock.finish<=age)&&clock.state!=null)
        {
            if(++transitions>64){TransitionLimitHits++;clock.finish=float.PositiveInfinity;clock.pending=null;break;}
            bool sent=clock.pending!=null;string evt=clock.pending??"FINISHED";float when=sent?(float.IsNaN(clock.pendingAt)?clock.entered:clock.pendingAt):clock.finish;clock.pending=null;clock.pendingAt=float.NaN;
            var next=clock.state.transitions.FirstOrDefault(t=>t.@event==evt)??clock.machine.events.FirstOrDefault(t=>t.@event==evt);
            if(next==null)continue;
            UpdateVariables(clock,when-clock.entered);
            Enter(clock,next.state,when);clock.transitions++;
        }
    }
    void UpdateClipAudio(ClipAudioWatch watch,float age)
    {
        if(watch.delay.Finished)return;
        float delta=Mathf.Max(0,age-watch.sampledAt);
        if(watch.sampled&&age==watch.sampledAt)return;
        watch.sampled=true;watch.sampledAt=age;if(!watch.delay.Step(delta))return;
        if(watch.unbound){ClipAudioFinishes++;UnboundAudioActions++;return;}
        NativeAudioCue cue;
        if(watch.sounds!=null)
        {
            int selected=watch.weights==null||watch.weights.Length==0?0:NativeAudio.SelectWeightedIndex(watch.weights,(float)audioRandom.NextDouble());
            if(selected<0||selected>=watch.sounds.Length){UnboundAudioActions++;return;}
            float volume=ChooseAudioRange(watch.clock,watch.action.Param("m_MinVolume"),watch.action.Param("m_MaxVolume"));
            float pitch=ChooseAudioRange(watch.clock,watch.action.Param("m_MinPitch"),watch.action.Param("m_MaxPitch"));
            cue=NativeAudio.ResolveClip(watch.sounds[selected],age,volume,pitch,(int)Number(watch.clock,watch.action.Param("m_Category")),()=> (float)audioRandom.NextDouble());
        }
        else cue=NativeAudio.ResolveClip(watch.sound,age,Number(watch.clock,watch.action.Param("m_Volume")),Number(watch.clock,watch.action.Param("m_Pitch")),(int)Number(watch.clock,watch.action.Param("m_Category")),()=> (float)audioRandom.NextDouble());
        ClipAudioFinishes++;if(cue==null){UnboundAudioActions++;return;}
        cue.template=clipAudioTemplate;cue.spatialBlend=Number(watch.clock,watch.action.Param("m_SpatialBlend"));
        var template=watch.action.Param("m_TemplateSource");
        if(!template.object_null)
        {var explicitTemplate=clipAudioTemplates[template.asset_path];cue.template=explicitTemplate.values;cue.mixer=explicitTemplate.mixer;cue.group=explicitTemplate.group;cue.copyTemplate=true;}
        cue.options=new NativeSoundOptions{limited=Number(watch.clock,watch.action.Param("m_InstanceLimited"))!=0,duration=Number(watch.clock,watch.action.Param("m_InstanceLimitedDuration")),maximum=(int)Number(watch.clock,watch.action.Param("m_InstanceLimitMaximum")),maxOut=(int)Number(watch.clock,watch.action.Param("m_LimitMaxOutOption"))};
        NativeAudioTemplate.BindPosition(cue,ResolveObject(watch.clock,watch.action.Param("m_ParentObject")),Nodes,StagingFrame,cue.sourceParent!=null);
        if(cue.template!=null){cue.loop=cue.template.Loop;cue.mute=!cue.copyTemplate&&cue.template.Mute;}
        audioCues.Add(cue);if(deliveredAudio.Add(watch.key)&&audioCallback!=null)audioCallback(cue);
    }
    float ChooseAudioRange(MachineClock clock,NativeParam minimum,NativeParam maximum)
    {
        float min=minimum.is_none?1:Number(clock,minimum),max=maximum.is_none?1:Number(clock,maximum);
        return Mathf.Approximately(min,max)?min:Mathf.Lerp(min,max,(float)audioRandom.NextDouble());
    }
    static NativeParam[] ArrayParameters(NativeAction action,string name)
    {
        var parameters=action.@params??new NativeParam[0];int index=Array.FindIndex(parameters,p=>p.name==name);
        if(index<0||!parameters[index].array_known||parameters[index].array_count<0)return new NativeParam[0];
        int count=parameters[index].array_count;
        return index+1+count<=parameters.Length?parameters.Skip(index+1).Take(count).ToArray():new NativeParam[0];
    }
    bool TryStartCardIdAudio(MachineClock clock,NativeAction action,float when,out CardIdAudioWatch watch)
    {
        watch=null;
        var spell=action.Param("m_SpellObject");var which=action.Param("m_WhichCard");var audioObject=action.Param("m_AudioSourceObject");
        var cardHeader=action.Param("m_CardIds");var soundHeader=action.Param("m_Sounds");var defaultSound=action.Param("m_DefaultSound");
        var volume=action.Param("m_VolumeScale");var delay=action.Param("m_Delay");var wait=action.Param("m_WaitForFinish");
        if(action.@params.Any(p=>p.unresolved)||spell.kind!=20||spell.object_null||spell.node<0||spell.node>=Nodes.Length||ResolveObject(clock,spell)==null||
           which.kind!=7||which.unresolved||!String.IsNullOrEmpty(which.variable)||(which.number!=0&&which.number!=1)||
           audioObject.kind!=20||audioObject.object_null||audioObject.node<0||audioObject.node>=Nodes.Length||ResolveObject(clock,audioObject)==null||
           cardHeader.kind!=12||!cardHeader.array_known||cardHeader.array_type!="System.String"||cardHeader.array_count<0||
           soundHeader.kind!=12||!soundHeader.array_known||soundHeader.array_type!="SoundDef"||soundHeader.array_count!=cardHeader.array_count||
           volume.kind!=15||volume.unresolved||!volume.none_known||(!String.IsNullOrEmpty(volume.variable)&&!clock.floats.ContainsKey(volume.variable))||
           delay.kind!=2||delay.unresolved||!String.IsNullOrEmpty(delay.variable)||delay.number<0||float.IsNaN(delay.number)||float.IsInfinity(delay.number)||
           wait.kind!=17||wait.unresolved||!wait.none_known||!String.IsNullOrEmpty(wait.variable)||(!wait.is_none&&wait.number!=0&&wait.number!=1)||spellContext==null)return false;
        var cardParams=ArrayParameters(action,"m_CardIds");var soundParams=ArrayParameters(action,"m_Sounds");
        if(cardParams.Length!=cardHeader.array_count||soundParams.Length!=soundHeader.array_count||
           cardParams.Any(p=>p.unresolved||p.kind!=3||String.IsNullOrEmpty(p.text)||!String.IsNullOrEmpty(p.variable))||
           soundParams.Any(p=>p.unresolved||p.kind!=5||p.asset_type!="SoundDef"||String.IsNullOrEmpty(p.asset_path))||
           defaultSound.unresolved||(!defaultSound.object_null&&(defaultSound.kind!=5||defaultSound.asset_type!="SoundDef"||String.IsNullOrEmpty(defaultSound.asset_path))))return false;
        int selectedIndex;bool useDefault;
        if(!NativeAudio.TrySelectCardIdSound((int)which.number,spellContext.CardId(0),spellContext.CardId(1),cardParams.Select(p=>p.text).ToArray(),soundParams.Length,out selectedIndex,out useDefault))return false;
        var selected=useDefault?defaultSound:selectedIndex<0||selectedIndex>=soundParams.Length?null:soundParams[selectedIndex];
        if(selected==null)return false;
        NativeSoundAsset definition=null;
        if(!selected.object_null&&!clipSounds.TryGetValue(selected.asset_path??"",out definition))return false;
        var audioNode=NativeAudio.Node(audioDefinition,Program.nodes[audioObject.node].path,audioObject.node);
        if(audioNode==null||audioNode.audio_sources==null||audioNode.audio_sources.Length!=1||audioNode.audio_sources[0].values==null||
           !NativeAudio.SupportsSpellCardIdAudioSource(audioNode.audio_sources[0].values)||audioNode.sound_defs==null||audioNode.sound_defs.Length!=1||
           audioNode.sound_defs[0].values==null)return false;
        watch=new CardIdAudioWatch{action=action,clock=clock,delay=new NativeClipAudioDelay(Number(clock,delay)),sound=definition,
            source=audioNode.audio_sources[0].values,sourceCategory=audioNode.sound_defs[0].values.m_Category,owner=ResolveObject(clock,audioObject),sampledAt=when,
            waitForFinish=!wait.is_none&&wait.number!=0,
            key="card-id-audio:"+machines.IndexOf(clock)+":"+clock.state.name+":"+Array.IndexOf(clock.state.actions,action)+":"+when.ToString("R",System.Globalization.CultureInfo.InvariantCulture)};
        return true;
    }
    void UpdateCardIdAudio(CardIdAudioWatch watch,float age)
    {
        if(watch.finished||watch.sampled&&watch.sampledAt==age)return;
        float delta=Mathf.Max(0,age-watch.sampledAt);watch.sampled=true;watch.sampledAt=age;
        if(!watch.emitted)
        {
            if(!watch.delay.Step(delta))return;
            watch.emitted=true;
            if(watch.sound==null){watch.finished=true;CardIdAudioFinishes++;return;}
            float volume=watch.action.Param("m_VolumeScale").is_none?1:Number(watch.clock,watch.action.Param("m_VolumeScale"));
            watch.cue=NativeAudio.ResolveSpellCardIdSound(watch.source,watch.sound,age,volume,watch.sourceCategory,()=> (float)audioRandom.NextDouble());
            if(watch.cue==null){UnboundAudioActions++;watch.finished=true;CardIdAudioFinishes++;return;}
            watch.cue.sourceParent=watch.owner.transform;
            NativeAudioTemplate.BindPosition(watch.cue,watch.owner,Nodes,StagingFrame,true);
            audioCues.Add(watch.cue);if(deliveredAudio.Add(watch.key)&&audioCallback!=null)audioCallback(watch.cue);
            if(!watch.waitForFinish){watch.finished=true;CardIdAudioFinishes++;return;}
        }
        if(watch.waitForFinish)
        {
            if(watch.cue==null||watch.cue.playbackActive==null)
            {if(!watch.missingPlaybackReported){MissingAudioPlaybackServices++;watch.missingPlaybackReported=true;}return;}
            if(watch.cue.playbackActive())return;
            watch.finished=true;CardIdAudioFinishes++;
        }
    }
    void UpdateAudio(float age)
    {
        foreach(var watch in ordinaryAudioCoroutines.ToArray())UpdateOrdinaryAudio(watch,age);
        ordinaryAudioCoroutines.RemoveAll(watch=>watch.finished);
        foreach(var watch in clipAudioCoroutines)UpdateClipAudio(watch,age);
        clipAudioCoroutines.RemoveAll(watch=>watch.delay.Finished);
        foreach(var watch in cardIdAudioCoroutines)UpdateCardIdAudio(watch,age);
        cardIdAudioCoroutines.RemoveAll(watch=>watch.finished);
        foreach(var watch in audioWatches)
            UpdatePlaythrough(watch,age);
        audioWatches.RemoveAll(watch=>watch.finished);
    }
    void UpdateOrdinaryAudio(OrdinaryAudioWatch watch,float age)
    {
        if(disposed||watch.finished||watch.sampled&&watch.sampledAt==age)return;
        float delta=Mathf.Max(0,age-watch.sampledAt);watch.sampledAt=age;watch.sampled=true;
        if(!watch.played)
        {
            if(!watch.delay.Step(delta))return;
            watch.played=true;var action=watch.action;var clock=watch.clock;
            var owner=ResolveObject(clock,action.Param("m_GameObject"));watch.owner=owner;int node=Array.IndexOf(Nodes,owner);
            var volume=action.Param("m_VolumeScale");var sound=action.Param("m_OneShotSound");
            if(node>=0&&!sound.unresolved&&sound.object_null&&volume.none_known&&!volume.unresolved)
            {
                float codeVolume=1;if(volume.is_none){if(!playthroughVolumes.TryGetValue(owner,out codeVolume))codeVolume=1;}else{codeVolume=Number(clock,volume);playthroughVolumes[owner]=codeVolume;}
                var cue=NativeAudio.Resolve(audioDefinition,Program.nodes[node].path,age,codeVolume,()=> (float)audioRandom.NextDouble(),node);
                if(cue==null)UnboundAudioActions++;
                else{BindOwnedAudioCue(cue,owner);audioCues.Add(cue);if(deliveredAudio.Add(watch.key)&&audioCallback!=null)audioCallback(cue);if(audioCallback!=null&&cue.playbackActive==null)MissingAudioPlaybackServices++;}
            }
            else if(owner!=null||action.Param("m_GameObject").unresolved)UnboundAudioActions++;
        }
        NativeAudioCue receiver;
        bool active=watch.owner!=null&&!AudioOwnerRemoved(watch.owner)&&sourceAudioCues.TryGetValue(watch.owner,out receiver)&&receiver.playbackActive!=null&&receiver.playbackActive();
        if(active)return;
        watch.finished=true;OrdinaryAudioFinishes++;
        var evt=watch.action.Param("m_FinishedEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){watch.clock.pending=evt.text;watch.clock.pendingAt=age;}
    }
    void UpdatePlaythrough(AudioWatch watch,float age)
    {
        if(watch.finished||watch.sampled&&watch.sampledAt==age)return;
        float delta=Mathf.Max(0,age-watch.sampledAt);watch.sampledAt=age;watch.sampled=true;
        if(watch.delay!=null&&!watch.delay.Step(delta))return;
        watch.finished=true;PlaythroughFinishes++;
        var action=watch.action;var clock=watch.clock;var owner=ResolveObject(clock,action.Param("m_GameObject"));int node=Array.IndexOf(Nodes,owner);
        var sound=action.Param("m_OneShotSound");var volume=action.Param("m_VolumeScale");
        // The current original Play method never reads legacy m_OneShotClip.
        bool generated=!sound.object_null;NativeSoundAsset explicitSound=null;
        bool unsupported=node<0||sound.unresolved||volume.unresolved||!volume.none_known||
            generated&&(!String.IsNullOrEmpty(sound.variable)||!clipSounds.TryGetValue(sound.asset_path??"",out explicitSound))||
            action.Param("m_IsMinionSummonVO").number!=0||action.Param("m_IsKeywordVO").number!=0;
        float codeVolume=1;
        if(!unsupported&&!generated)
        {
            if(volume.is_none&&!playthroughVolumes.TryGetValue(owner,out codeVolume))codeVolume=1;
            if(!volume.is_none){codeVolume=Number(clock,volume);playthroughVolumes[owner]=codeVolume;}
        }
        var cue=unsupported?null:generated?
            NativeAudio.ResolvePlaythroughSound(audioDefinition,Program.nodes[node].path,node,explicitSound,age,volume.is_none?(float?)null:Number(clock,volume),()=> (float)audioRandom.NextDouble()):
            NativeAudio.Resolve(audioDefinition,Program.nodes[node].path,age,codeVolume,()=> (float)audioRandom.NextDouble(),node);
        if(cue==null){if(owner!=null||action.Param("m_GameObject").unresolved)UnboundAudioActions++;}
        else
        {
            if(generated)
            {
                NativeAudioTemplate.BindPosition(cue,owner,Nodes,StagingFrame,cue.sourceParent!=null);
            }
            else{cue.loop=cue.loop||loopingAudioOwners.Contains(owner);BindOwnedAudioCue(cue,owner);}
            // Both original Play branches always supply action options.
            cue.options=new NativeSoundOptions{limited=Number(clock,action.Param("m_InstanceLimited"))!=0,duration=Number(clock,action.Param("m_InstanceLimitedDuration")),maximum=(int)Number(clock,action.Param("m_InstanceLimitMaximum")),maxOut=(int)Number(clock,action.Param("m_LimitMaxOutOption"))};
            audioCues.Add(cue);if(deliveredAudio.Add(watch.key)&&audioCallback!=null)audioCallback(cue);
        }
        var evt=action.Param("m_FinishedEvent");if(!evt.unresolved&&!String.IsNullOrEmpty(evt.text)){clock.pending=evt.text;clock.pendingAt=age;}
    }
    void BindOwnedAudioCue(NativeAudioCue cue,GameObject owner)
    {
        sourceAudioCues[owner]=cue;
        NativeAudioTemplate.BindPosition(cue,owner,Nodes,StagingFrame,true);cue.trackedSource=owner;cue.stagingRoot=Root.transform;
        cue.restartKey="Original source "+owner.GetInstanceID();cue.keepSource=true;cue.sourceName=owner.name;
        cue.currentCodeVolume=()=>{float value;return playthroughVolumes.TryGetValue(owner,out value)?value:1;};
        int stopVersion;audioStopVersions.TryGetValue(owner,out stopVersion);
        cue.sourceStopped=()=>{int current;audioStopVersions.TryGetValue(owner,out current);return current!=stopVersion;};
        cue.sourceDestroyed=()=>disposed||AudioOwnerRemoved(owner);
    }
    void Advance(float age)
    {
        UpdateTweens(age);
        UpdateAudio(age);
        ProcessAnimationEvents(age);
        foreach(var clock in machines)
        {
            AdvanceTransitions(clock,age);
            UpdateVariables(clock,age-clock.entered);
            if(clock.pending!=null)AdvanceTransitions(clock,age);
        }
        UpdateAudio(age);
        UpdateCameraShakerInvocations(age);
        SampleClips(age);
        SampleTimelines(age);
        foreach(var clock in machines)if(clock.state!=null)
            foreach(var action in clock.state.actions)if(action.name=="SetScale"&&action.Param("everyFrame").number!=0&&action.Param("lateUpdate").number!=0)SetScale(clock,action);
        foreach(var pair in particleStart)
        {
            int i=pair.Key;if(timelineParticles.Contains(i)||age<pair.Value||!Nodes[i].activeInHierarchy)continue;
            if(particleStop.ContainsKey(i)&&age>=particleStop[i]){var em=Particles[i].emission;em.enabled=false;}
            float from=Mathf.Max(last,pair.Value);
            Particles[i].Simulate(Mathf.Max(0,age-from),false,last<=pair.Value,false);
        }
    }
    static void Fade(ClipClock clip,float when,float duration,float target)
    {clip.weightFrom=clip.Weight(when);clip.weightTo=target;clip.fadeStarted=when;clip.fadeDuration=duration;}
    static float ClipTime(ClipClock clip,float age)
    {float local=(age-clip.entered)*clip.speed+clip.offset;return NativeLegacyWrap.Sample(local,clip.clip.duration,clip.wrapMode);}
    void SampleTimelines(float age)
    {
        foreach(var playback in timelines)
        {
            if(playback.finished)continue;
            float time=Mathf.Clamp(age-playback.entered+playback.data.initial_time,0,playback.data.duration);
            foreach(var clock in playback.shakes)
            {
                var item=clock.clip;bool playing=time>=item.start&&time<item.start+item.duration;
                if(playing&&clock.helper==null&&clock.target!=null)clock.helper=new NativeTimelineShaker(clock.target,item.shaker,item.duration);
                if(playing&&clock.helper!=null)clock.helper.Sample(time-item.start,TimelineShakeEnabled);
                else if(clock.helper!=null){clock.helper.Dispose();clock.helper=null;}
            }
            foreach(var clock in playback.controls)
            {
                var item=clock.clip;bool playing=time>=item.start&&time<item.start+item.duration;
                if(playing&&!clock.playing)
                {
                    if(item.control.active)clock.target.SetActive(true);
                    foreach(var seed in clock.seeds)seed.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    foreach(var seed in clock.seeds)seed.useAutoRandomSeed=false;
                    if(!clock.seeds.All(p=>p.IsAlive()))foreach(var seed in clock.seeds)seed.randomSeed=(uint)timelineRandom.Next(1,9999);
                    foreach(var particle in clock.particles)particle.Play();
                }
                if(playing)foreach(var particle in clock.particles)particle.Sample(item.clip_in+(time-item.start)*item.speed);
                else if(clock.playing){foreach(var particle in clock.particles)particle.Pause();if(item.control.active)clock.target.SetActive(false);}
                clock.playing=playing;
            }
            foreach(var clock in playback.sounds)
            {
                var item=clock.clip;bool playing=time>=item.start&&time<item.start+item.duration;
                if(playing&&!clock.fired)
                {
                    var cue=NativeDeathKnightHeroPowerPlayback.ResolveTimelineSoundCue(Program,audioDefinition,assets.Root,
                        clock.timeline,clock.track,clock.owner,playback.entered+item.start,()=> (float)audioRandom.NextDouble());
                    if(cue==null)
                    {UnboundTimelineSounds++;clock.fired=true;}
                    else
                    {
                        cue.sourceName="Native timeline sound - "+clock.owner.name;
                        NativeAudioTemplate.BindPosition(cue,clock.owner,Nodes,StagingFrame,cue.sourceParent!=null);
                        audioCues.Add(cue);
                        if(deliveredAudio.Add(clock.key)&&audioCallback!=null)audioCallback(cue);
                        TimelineSoundStarts++;clock.fired=true;
                    }
                }
                clock.playing=playing;
            }
            foreach(var pair in playback.clips)
            {
                var item=pair.Item1;float local=time-item.start;
                if(local<0&&(item.pre!=1||-local>item.pre_time))continue;
                if(local>item.duration&&(item.post!=1||!item.post_unlimited&&local-item.duration>item.post_time))continue;
                SampleClip(pair.Item2,item.clip_in+Mathf.Clamp(local,0,item.duration)*item.speed);
            }
            playback.finished=age-playback.entered+playback.data.initial_time>=playback.data.duration;
        }
    }
    void ReleaseTimelineControls(TimelineClock playback)
    {
        foreach(var clock in playback.shakes)if(clock.helper!=null){clock.helper.Dispose();clock.helper=null;}
        foreach(var clock in playback.controls)
        {
            for(int i=0;i<clock.seeds.Length;i++)if(clock.seeds[i]!=null){clock.seeds[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);clock.seeds[i].useAutoRandomSeed=clock.autoSeeds[i];}
            if(clock.target!=null&&clock.clip.control.active)clock.target.SetActive(clock.clip.control.post==0||clock.clip.control.post==2&&clock.initialActive);
        }
    }
    void ReleaseTimelineControls(){foreach(var playback in timelines)ReleaseTimelineControls(playback);}
    void SampleClips(float age)
    {
        materialMix.Clear();visibilityMix.Clear();
        foreach(var mixer in legacyMixers.Values)foreach(AnimationState state in mixer)state.enabled=false;
        foreach(var clip in clips)
        {
            if(clip.animator){SampleClip(clip,age);continue;}
            float weight=clip.Weight(age);if(weight<=0)continue;
            Animation mixer;
            if(!legacyMixers.TryGetValue(clip.owner,out mixer))
            {mixer=clip.owner.AddComponent<Animation>();mixer.enabled=false;mixer.cullingType=AnimationCullingType.AlwaysAnimate;legacyMixers.Add(clip.owner,mixer);}
            string key=clip.nativeKey??(clip.nativeKey=clip.owner.GetInstanceID()+":"+clip.clip.id);AnimationClip native;
            if(!legacyClips.TryGetValue(key,out native))
            {
                native=new AnimationClip{legacy=true,wrapMode=WrapMode.ClampForever};
                foreach(var binding in clip.bindings)
                {
                    string property=binding.track.property,unityProperty=property.StartsWith("position.")?property.Replace("position.","localPosition."):property.StartsWith("scale.")?property.Replace("scale.","localScale."):property.StartsWith("rotation.")?property.Replace("rotation.","localRotation."):property.StartsWith("euler.")?property.Replace("euler.","localEulerAnglesRaw."):null;
                    if(unityProperty==null||binding.transform==null)continue;
                    var target=binding.transform;string path="";
                    while(target!=clip.owner.transform&&target!=null){path=target.name+(path.Length==0?"":"/"+path);target=target.parent;}
                    if(target==null){UnboundAnimationActions++;continue;}
                    native.SetCurve(path,typeof(Transform),unityProperty,NativeCurve.Keys(binding.track.keys));
                }
                mixer.AddClip(native,key);legacyClips.Add(key,native);
            }
            var state=mixer[key];state.enabled=true;state.layer=clip.layer;state.weight=weight;state.time=ClipTime(clip,age);
            foreach(var binding in clip.bindings)
            {
                if(binding.renderer==null)continue;string property=binding.track.property;
                if(property.StartsWith("position.")||property.StartsWith("scale.")||property.StartsWith("rotation.")||property.StartsWith("euler."))continue;
                float value=binding.track.Sample(ClipTime(clip,age));Vector2 sum;
                if(property=="m_Enabled")
                {visibilityMix.TryGetValue(binding.renderer,out sum);visibilityMix[binding.renderer]=sum+new Vector2(value*weight,weight);continue;}
                if(property.StartsWith("material."))property=property.Substring(9);
                foreach(var material in binding.materials)
                {var channel=(material,property);materialMix.TryGetValue(channel,out sum);materialMix[channel]=sum+new Vector2(value*weight,weight);}
            }
        }
        foreach(var mixer in legacyMixers.Values)mixer.Sample();
        foreach(var pair in materialMix)WriteMaterial(pair.Key.Item1,pair.Key.Item2,pair.Value.x/pair.Value.y);
        foreach(var pair in visibilityMix)
        {bool visible=pair.Value.x/pair.Value.y>.5f;desiredVisibility[pair.Key]=visible;pair.Key.enabled=visible&&pair.Key.sharedMaterials.Any(m=>m.GetFloat("_HasSourceTexture")>0);}
        clips.RemoveAll(c=>!c.animator&&(c.weightTo==0&&age>=c.fadeStarted+c.fadeDuration||c.wrapMode==1&&(age-c.entered)*c.speed+c.offset>Mathf.Max(c.clip.duration,c.sourceEvents?c.clip.event_horizon:0)));
    }
    void WriteMaterial(Material mat,string property,float value)
    {
        int dot=property.LastIndexOf('.');
        if(dot<0){if(OriginalProperty(mat,property))mat.SetFloat(property,value);return;}
        string name=property.Substring(0,dot);if(!OriginalProperty(mat,name))return;
        string suffix=property.Substring(dot+1);int component="rgba".IndexOf(suffix);
        if(component>=0){var c=mat.GetColor(name);c[component]=value;mat.SetColor(name,c);return;}
        component="xyzw".IndexOf(suffix);if(component>=0){var v=mat.GetVector(name);v[component]=value;mat.SetVector(name,v);}
    }
    void SampleClip(ClipClock clip,float age)
    {
            float local=(age-clip.entered)*clip.speed+clip.offset;
            float time=clip.loop&&clip.clip.duration>0?Mathf.Repeat(local,clip.clip.duration):Mathf.Clamp(local,0,clip.clip.duration);
            foreach(var binding in clip.bindings)
            {
                var track=binding.track;var transform=binding.transform;if(transform==null)continue;
                float value=track.Sample(time);string property=track.property;
                if(property.StartsWith("position.")||property.StartsWith("scale.")||property.StartsWith("euler."))
                {var v=property.StartsWith("position")?transform.localPosition:property.StartsWith("scale")?transform.localScale:transform.localEulerAngles;int axis=property.EndsWith("x")?0:property.EndsWith("y")?1:2;v[axis]=value;if(property.StartsWith("position"))transform.localPosition=v;else if(property.StartsWith("scale"))transform.localScale=v;else transform.localEulerAngles=v;}
                else if(property.StartsWith("rotation."))continue;
                else
                {
                    var r=binding.renderer;if(r==null)continue;if(property=="m_Enabled"){desiredVisibility[r]=value>.5f;r.enabled=value>.5f&&binding.materials.Any(m=>m.GetFloat("_HasSourceTexture")>0);continue;}
                    if(property.StartsWith("material."))property=property.Substring(9);
                    foreach(var mat in binding.materials)
                    {
                        int dot=property.LastIndexOf('.');if(dot>0){string name=property.Substring(0,dot);if(!OriginalProperty(mat,name))continue;string suffix=property.Substring(dot+1);int component="rgba".IndexOf(suffix);if(component>=0){var c=mat.GetColor(name);c[component]=value;mat.SetColor(name,c);}else{component="xyzw".IndexOf(suffix);if(component>=0){var v=mat.GetVector(name);v[component]=value;mat.SetVector(name,v);}}}
                        else if(OriginalProperty(mat,property))mat.SetFloat(property,value);
                    }
                }
            }
            foreach(var rotation in clip.rotations)
            {
                var q=rotation.transform.localRotation;
                foreach(var binding in rotation.tracks){string p=binding.track.property;int axis=p.EndsWith("x")?0:p.EndsWith("y")?1:p.EndsWith("z")?2:3;q[axis]=binding.track.Sample(time);}
                rotation.transform.localRotation=Quaternion.Normalize(q);
            }
    }
    public RenderTexture Render(float age)
    {
        Seek(age);var previous=RenderTexture.active;
        try
        {
            foreach(var capture in captures)capture.Render();foreach(var capture in generatedCaptures)capture.Render();foreach(var capture in offscreenFX)capture.Render();camera.Render();
            if(captureAlpha!=null)
            {
                var temporary=RenderTexture.GetTemporary(output.width,output.height,0,output.format);
                try{Graphics.Blit(output,temporary,captureAlpha);Graphics.Blit(temporary,output);}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
            }
        }
        finally{RenderTexture.active=previous;}return output;
    }
    public void ApplySourceCamera(NativeSourceCamera source,Vector3 sourceOrigin)
    {source.Apply(camera,sourceOrigin,Root.transform.position,(float)output.width/output.height);}
    public Material ReplaceVisualMaterial(Renderer renderer,NativeMaterial source)
    {var material=CreateMaterial(source,assets);renderer.sharedMaterial=material;return material;}
    public Material CreateVisualMaterial(NativeMaterial source){return CreateMaterial(source,assets);}
    public void SetVisualClock(float time){foreach(var material in materials)material.SetFloat("_Clock",time);}
    public void FitPreviewCamera(Bounds bounds)
    {
        camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.z*.5f,bounds.size.x*.5f/camera.aspect)*1.035f;
        camera.transform.position=new Vector3(bounds.center.x,bounds.max.y+8,bounds.center.z);
        camera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
    }
    public Vector2 PreviewPoint(Vector3 point)
    {var viewport=camera.WorldToViewportPoint(point);return new Vector2(viewport.x,1-viewport.y);}
    readonly Dictionary<NativeAnimation,ClipClock> visualClips=new Dictionary<NativeAnimation,ClipClock>();
    public void SampleVisualAnimation(NativeAnimation animation,float time)
    {
        ClipClock clip;
        if(!visualClips.TryGetValue(animation,out clip))
        {
            var bindings=animation.tracks.Where(t=>!t.property.StartsWith("m_LocalEulerAnglesHint.")).Select(t=>BindTrack(t,Nodes[0])).ToArray();
            if(bindings.Any(b=>b.transform==null))throw new InvalidOperationException("Original UI animation target missing");
            clip=new ClipClock{clip=animation,speed=1,bindings=bindings,rotations=bindings.Where(b=>b.track.property.StartsWith("rotation.")).GroupBy(b=>b.transform).Select(g=>new RotationBinding{transform=g.Key,tracks=g.ToArray()}).ToArray()};
            visualClips.Add(animation,clip);
        }
        SampleClip(clip,Mathf.Clamp(time,0,animation.duration));
    }
    public RenderTexture RenderGlow(float age)
    {
        Seek(age);
        if(glowCapture==null)glowCapture=new NativeEffectGlowCapture(Program,Nodes,output.width,output.height,r=>desiredVisibility.ContainsKey(r)&&desiredVisibility[r]);
        foreach(var capture in offscreenFX)capture.Render();return glowCapture.Render(camera);
    }
    public RenderTexture RenderAffine(float age)
    {
        long total=StartH10Timing();Seek(age);
        long captureTiming=StartH10Timing();foreach(var capture in captures)capture.Render();ReportH10Timing("RenderAffine.native-captures",captureTiming);
        captureTiming=StartH10Timing();foreach(var capture in generatedCaptures)capture.Render();ReportH10Timing("RenderAffine.generated-captures",captureTiming);
        captureTiming=StartH10Timing();foreach(var capture in offscreenFX)capture.Render();ReportH10Timing("RenderAffine.offscreen-captures",captureTiming);
        if(affineCapture==null)affineCapture=new NativeAffineCapture(output.width,output.height);
        captureTiming=StartH10Timing();affineCapture.Render(camera,materialSources);ReportH10Timing("RenderAffine.affine-compose",captureTiming);
        ReportH10Timing("RenderAffine.total",total);return affineCapture.Color;
    }
    public static bool NodeBelongsToSubtree(NativeProgram program,int node,int root)
    {
        if(program==null||program.nodes==null||node<0||root<0||node>=program.nodes.Length||root>=program.nodes.Length)return false;
        for(int current=node,steps=0;current>=0&&current<program.nodes.Length&&steps<=program.nodes.Length;steps++)
        {
            if(current==root)return true;
            current=program.nodes[current].parent;
        }
        return false;
    }
    public RenderTexture RenderAffineSubtree(float age,int root,float orthographicSize)
    {return RenderAffineFiltered(age,root,true,orthographicSize);}
    public RenderTexture RenderAffineSubtree(float age,int root,float orthographicSize,Action afterSeek)
    {return RenderAffineFiltered(age,root,true,orthographicSize,afterSeek);}
    public RenderTexture RenderAffineSubtree(float age,int root,SubtreeFrame frame)
    {return RenderAffineFiltered(age,root,true,frame.orthographicSize,null,frame.center,frame.aspect);}
    public RenderTexture RenderAffineSubtree(float age,int root,SubtreeFrame frame,Action afterSeek)
    {return RenderAffineFiltered(age,root,true,frame.orthographicSize,afterSeek,frame.center,frame.aspect);}
    public SubtreeFrame CaptureSubtreeFrame(float age,int root,Action afterSeek=null,bool fitAspect=false,bool meshOnly=false)
    {
        Seek(age);long timing=StartH10Timing();afterSeek?.Invoke();
        if(root<0||root>=Nodes.Length)return new SubtreeFrame{center=Root.transform.position,orthographicSize=1.25f};
        Bounds bounds=default(Bounds);bool hasBounds=false;
        for(int i=0;i<renderers.Length;i++)
        {
            var renderer=renderers[i];if(renderer==null||!renderer.enabled||!NodeBelongsToSubtree(Program,i,root))continue;
            if(meshOnly&&renderer is ParticleSystemRenderer)continue;
            var currentBounds=renderer.bounds;if(currentBounds.size.sqrMagnitude<.000001f)continue;
            if(!hasBounds){bounds=currentBounds;hasBounds=true;}else bounds.Encapsulate(currentBounds);
        }
        float aspect=fitAspect&&hasBounds?Mathf.Max(.01f,bounds.size.x/Mathf.Max(.01f,bounds.size.z)):camera.aspect;
        var frame=new SubtreeFrame{center=hasBounds?bounds.center:Nodes[root].transform.position,aspect=fitAspect?aspect:0,
            orthographicSize=Mathf.Max(.05f,hasBounds?Mathf.Max(bounds.size.z*.5f,bounds.size.x*.5f/aspect)*1.035f:1.25f)};
        ReportH10Timing("CaptureSubtreeFrame.bounds",timing);return frame;
    }
    public RenderTexture RenderAffineExcludingSubtree(float age,int root)
    {return RenderAffineFiltered(age,root,false,0);}
    public RenderTexture RenderAffineExcludingSubtree(float age,int root,Action afterSeek)
    {return RenderAffineFiltered(age,root,false,0,afterSeek);}
    public RenderTexture RenderAffineExcludingSubtree(float age,int root,float orthographicSize)
    {return RenderAffineFiltered(age,root,false,orthographicSize);}
    public RenderTexture RenderAffineExcludingSubtree(float age,int root,float orthographicSize,Action afterSeek)
    {return RenderAffineFiltered(age,root,false,orthographicSize,afterSeek);}
    RenderTexture RenderAffineFiltered(float age,int root,bool includeSubtree,float orthographicSize,Action afterSeek=null,Vector3? fixedFocus=null,float fixedAspect=0)
    {
        long total=StartH10Timing();Seek(age);long captureTiming=StartH10Timing();afterSeek?.Invoke();
        var previousVisibility=desiredVisibility.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        var previousEnabled=desiredVisibility.Keys.Where(renderer=>renderer!=null).ToDictionary(renderer=>renderer,renderer=>renderer.enabled);
        var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;float previousSize=camera.orthographicSize,previousAspect=camera.aspect;
        try
        {
            if(root>=0)
                for(int i=0;i<renderers.Length;i++)
                {
                    var renderer=renderers[i];if(renderer==null)continue;
                    bool inSubtree=NodeBelongsToSubtree(Program,i,root);
                    if(includeSubtree!=inSubtree){desiredVisibility[renderer]=false;renderer.enabled=false;}
                }
            if(root>=0&&orthographicSize>0)
            {
                var focus=Nodes[root].transform.position;Bounds subtreeBounds=default(Bounds);bool hasBounds=false;
                for(int i=0;i<renderers.Length;i++)
                {
                    var renderer=renderers[i];if(renderer==null||!renderer.enabled||includeSubtree!=NodeBelongsToSubtree(Program,i,root))continue;
                    var bounds=renderer.bounds;if(bounds.size.sqrMagnitude<.000001f)continue;
                    if(!hasBounds){subtreeBounds=bounds;hasBounds=true;}else subtreeBounds.Encapsulate(bounds);
                }
                if(fixedFocus.HasValue)focus=fixedFocus.Value;
                else if(hasBounds)focus=subtreeBounds.center;
                camera.transform.position+=focus-Root.transform.position;
                camera.orthographicSize=orthographicSize;
                if(fixedAspect>0)camera.aspect=fixedAspect;
            }
            foreach(var capture in captures)capture.Render();ReportH10Timing("RenderAffineSubtree.setup-and-native-captures",captureTiming);
            captureTiming=StartH10Timing();foreach(var capture in generatedCaptures)capture.Render();ReportH10Timing("RenderAffineSubtree.generated-captures",captureTiming);
            captureTiming=StartH10Timing();foreach(var capture in offscreenFX)capture.Render();ReportH10Timing("RenderAffineSubtree.offscreen-captures",captureTiming);
            if(affineCapture==null)affineCapture=new NativeAffineCapture(output.width,output.height);
            captureTiming=StartH10Timing();affineCapture.Render(camera,materialSources);ReportH10Timing("RenderAffineSubtree.affine-compose",captureTiming);
            ReportH10Timing("RenderAffineSubtree.total",total);return affineCapture.Color;
        }
        finally
        {
            foreach(var pair in previousVisibility)desiredVisibility[pair.Key]=pair.Value;
            foreach(var pair in previousEnabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            camera.transform.position=cameraPosition;camera.transform.rotation=cameraRotation;camera.orthographicSize=previousSize;camera.aspect=previousAspect;
        }
    }
    // 共享视口一次绘制独立原克隆；合并真实材质集合，保留原深度/排序与彩色透射。
    public RenderTexture RenderTogether(IEnumerable<NativeEffectInstance> instances)
    {
        var sources=new Dictionary<Material,NativeMaterial>();
        foreach(var instance in instances)
        {
            foreach(var capture in instance.captures)capture.Render();
            foreach(var capture in instance.generatedCaptures)capture.Render();
            foreach(var capture in instance.offscreenFX)capture.Render();
            foreach(var pair in instance.materialSources)sources[pair.Key]=pair.Value;
        }
        if(affineCapture==null)affineCapture=new NativeAffineCapture(output.width,output.height);
        affineCapture.Render(camera,sources);return affineCapture.Color;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;geometry.Restore();audioWatches.Clear();clipAudioCoroutines.Clear();cardIdAudioCoroutines.Clear();ordinaryAudioCoroutines.Clear();cameraShakerInvocations.Clear();
        foreach(var cue in sourceAudioCues.Values)cue.stopPlayback?.Invoke();sourceAudioCues.Clear();
        ReleaseTimelineControls();ReleaseBorrowedAnimations();RestoreBorrowedVisibility();customSpawn.Restore();
        borrowedActorVisibility.Clear();borrowedRendererVisibility.Clear();
        foreach(var pair in borrowedScales)if(pair.Key!=null)pair.Key.localScale=pair.Value;
        borrowedScales.Clear();if(Root!=null)Root.SetActive(false);ReleaseDetachedSourceNodes();
        if(affineCapture!=null)affineCapture.Dispose();if(glowCapture!=null)glowCapture.Dispose();
        foreach(var capture in captures)capture.Dispose();captures.Clear();
        foreach(var capture in generatedCaptures)capture.Dispose();generatedCaptures.Clear();
        foreach(var capture in offscreenFX)capture.Dispose();offscreenFX.Clear();
        if(camera!=null)camera.targetTexture=null;if(RenderTexture.active==output)RenderTexture.active=null;
        AssetRegistry.ReleaseTransient(output);foreach(var clip in legacyClips.Values)AssetRegistry.ReleaseTransient(clip);
        foreach(var m in materials)AssetRegistry.ReleaseTransient(m);
        if(camera!=null)AssetRegistry.ReleaseTransient(camera.gameObject);AssetRegistry.ReleaseTransient(Root);
    }
}
