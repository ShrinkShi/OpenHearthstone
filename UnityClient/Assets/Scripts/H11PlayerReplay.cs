using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class H11PlayerReplay : MonoBehaviour
{
    const string HeroPowerId="HERO_11bp",GhoulId="HERO_11bpt";
    const string ProgramReference="ReuseFX_Necrotic_DeathKnight_OverrideSpawn_HeroPower_Super.prefab:d71d2c073842bf24a9a24f9cabada171";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    [Serializable] sealed class EventRow{public string type,cardId;public int source,target,owner;}
    [Serializable] sealed class ActionRow{public string name;public int count;}
    [Serializable] sealed class Snapshot
    {
        public string publicHash,peerPublicHash,displayHash,heroPower,powerCardId,ghoulCardId,actorCardId;
        public int seat,turn,powerEntity,mana,powerManaSpent,playHeroPowerCount,summonCount,playHeroPowerTarget,summonSource,summonTarget;
        public int ghoulEntity,ghoulAttack,ghoulHealth,actorEntity;
        public bool powerUsed,ghoulCharge,actorPresent,actorVisible,actorBound,ghoulPresent,displayMatchedAtAcceptance;
        public EventRow[] deathEvents;
    }
    [Serializable] sealed class SourceSnapshot
    {
        public bool sourceSeen,released,genericSummonFallback;
        public float startTime,releaseTime,duration,releaseFrameAllowance;
        public bool tokenCueSeen,tokenPlaybackObserved,tokenPlaybackActive,tokenAudioActiveBeyondIdentity,tokenPlaybackEnded;
        public float tokenCueCallbackTime,tokenCuePitch,tokenPlaybackFirstActiveTime,tokenPlaybackEndTime,tokenPlaybackEndFrameAllowance;
        public float tokenCallbackToEndSeconds,lastPlaybackSourceAge;
        public int activeEffectCount,genericSummonActiveCount,customSpawnInitializations,sideEventDispatches;
        public int timelineStarts,timelineSoundStarts,unboundActions,unhandledActions,unsupportedTimelineTracks;
        public ActionRow[] actionExecutions;
        public ProjectionSample[] projections;
        public PlaybackClockSnapshot playbackClock;
        public PlaybackClockSample[] playbackClockSamples;
    }
    [Serializable] sealed class PlaybackClockSnapshot
    {
        public float timelineStartAge,tokenAudioDuration,audioGraceSeconds,identityThroughAge,authoredDuration;
    }
    [Serializable] sealed class PlaybackClockSample
    {
        public int frame;
        public float wallAge,sourceAge,playbackAge;
        public bool tokenAudioActiveAtMap;
    }
    [Serializable] sealed class ProjectionSample
    {
        public int index,viewerSeat,ownerSeat,frame,sourceLayer,boardCameraMask,nativeCameraMask;
        public float observedAge,sourceAge,playbackAge,cameraFieldOfView,cameraOrthographicSize;
        public string cameraName,nodeName,nodePath,nodeParent,actorName,actorParent;
        public float[] cameraPosition,cameraRotation,nodeWorld,nodeScreen,actorWorld,actorScreen,powerRect,ghoulRect;
        public bool boardCameraSeesSourceLayer,actorPresent,actorBound,hasPowerRect,hasGhoulRect;
    }
    [Serializable] sealed class UnlockSnapshot
    {
        public bool sampled,sourceReleased,busy,inputLocked,pending,legalEndTurn,displayHashMatched,unlockedDuringSource;
        public bool sourceActiveAtSample,sourceActiveAtSubmit,sourceTailActiveAtSubmit,presentationBusyAtSubmit,inputLockedAtSubmit,displayHashMatchedAtSubmit;
        public bool authorityResponseMatched,authorityResponseWhileSourceActive,sourceTailClearedAfterResponse;
        public int seat,active,actionCounterAtSubmit,actionCounterAfterSend,submitVersion,responseVersion,responseActive;
        public int sourceTailCountAtResponse,castAcceptedFrame;
        public bool responseDisplayHashMatched;
        public int sampleFrame,submitFrame,responseFrame,sourceTailCountAtSample,sourceTailCountAtSubmit,sourceTailCountAfterResponse;
        public float sampleTime,castAcceptedTime,submitTime,submitWallTime,castToSubmitSeconds,inputFrameAllowance,responseTime;
        public float previousUpdateWallBoundary,submissionUpdateWallBoundary;
        public string responseHash,displayHashAtSample,displayHashAtSubmit;
    }
    [Serializable] sealed class AudioRecord{public string kind,file;public float time,cueTime,pitch;public int frame;public bool playing;}
    [Serializable] sealed class AudioSnapshot{public string buttonFile,tokenFile;public AudioRecord[] records;}
    [Serializable] sealed class DspSnapshot
    {
        public int listenerCount,rate,channels,dspBuffer,dspBuffers,callbacks,overflows;
        public long sampleFrames,clippedSamples;public float peak,rms;
    }
    [Serializable] sealed class Report
    {
        public string result,role,dataRoot,dataIndexSha256,castDisplaySettleHash;
        public int screenWidth,screenHeight,initialActionCounter,castAcceptedFrame,castDisplaySettleFrame;
        public float castAcceptedWallTime,castDisplaySettleTime;
        public Snapshot before,cast,afterEndTurn;public SourceSnapshot source;public UnlockSnapshot inputUnlockedDuringSource;
        public AudioSnapshot audio;public DspSnapshot dsp;public string[] images;
    }
    [Serializable] sealed class Progress
    {
        public string role,authorityHash,displayHash;public Snapshot cast,afterEndTurn;
        public UnlockSnapshot unlock;
        public int authorityVersion,displayVersion,queueCount,seat,active;public float presentationTime;
        public bool presentationBusy,inputLocked,pending,legalEndTurn,sourceReleased;
    }

    readonly List<AudioRecord> audioRecords=new List<AudioRecord>();readonly HashSet<string> images=new HashSet<string>();
    readonly HashSet<string> captureRequests=new HashSet<string>();readonly Queue<string> captureQueue=new Queue<string>();
    readonly List<ProjectionSample> projections=new List<ProjectionSample>();
    readonly List<PlaybackClockSample> playbackClockSamples=new List<PlaybackClockSample>();
    readonly System.Diagnostics.Stopwatch wallClock=System.Diagnostics.Stopwatch.StartNew();
    ClientApp app;NativeEffectPlayer effects;NativePowerVisual powers;NativeBattleSummons summons;Presentation presentation;
    AssetRegistry assets;FireblastPcmTap tap;StateView initial,castState,endState;NativeDeathKnightHeroPowerPlayback.Pair pair;
    Report report;SourceSnapshot source=new SourceSnapshot();UnlockSnapshot unlock=new UnlockSnapshot();
    string role,folder,dataRoot,indexHash,buttonFile,tokenFile,failure;float started;int initialActionCounter;
    float castAcceptedTime,castAcceptedWallTime,previousUpdateWallBoundary=-1f,updateWallBoundary=-1f,updateFrameWallSeconds;
    int castAcceptedFrame;
    float previousPlaybackWallAge=-1,previousPlaybackSourceAge=-1,previousPlaybackAge=-1;
    NativeAudioCue tokenAudioCue;
    bool tokenPlaybackWasActive,capturePumpActive,initialized,actionSent,endTurnSent,finalizing,finished;
    string captureFailure;float peerDeadline,nextProgressPublish;

    static string Argument(string name)
    {var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:"";}
    static string RequiredArgument(string name)
    {string value=Argument(name);if(String.IsNullOrWhiteSpace(value))throw new ArgumentException("Required argument missing: "+name);return value;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string selectedRole=Argument("--h11-player-replay");if(selectedRole=="")return;
        if(selectedRole!="driver"&&selectedRole!="observer")throw new ArgumentException("H11 replay role must be driver or observer");
        var replay=new GameObject("H11 dual Player evidence").AddComponent<H11PlayerReplay>();
        replay.role=selectedRole;replay.folder=Path.GetFullPath(RequiredArgument("--h11-evidence-dir"));
        replay.dataRoot=Path.GetFullPath(RequiredArgument("--data"));Directory.CreateDirectory(replay.folder);
        replay.started=Time.realtimeSinceStartup;
    }
    T Field<T>(string name){return (T)typeof(ClientApp).GetField(name,Private).GetValue(app);}
    bool OpeningSpeechSettled()
    {
        var field=typeof(ClientApp).GetField("heroSpeech",Private);
        Check(field!=null,"ClientApp does not expose HeroSpeech for fixture setup");
        var speech=field.GetValue(app);Check(speech!=null,"ClientApp HeroSpeech is unavailable");
        var type=speech.GetType();var opening=type.GetProperty("OpeningComplete",Private);var hasBubble=type.GetMethod("HasBubble",Private);
        Check(opening!=null&&hasBubble!=null,"HeroSpeech lacks its opening/bubble completion state");
        if(!(bool)opening.GetValue(speech,null))return false;
        return !(bool)hasBubble.Invoke(speech,new object[]{0})&&!(bool)hasBubble.Invoke(speech,new object[]{1});
    }
    void SetField(string name,object value){typeof(ClientApp).GetField(name,Private).SetValue(app,value);}
    object Invoke(string name,params object[] values){return typeof(ClientApp).GetMethod(name,Private).Invoke(app,values);}
    float Now{get{return Time.realtimeSinceStartup-started;}}
    void Check(bool value,string message){if(!value)throw new InvalidOperationException("H11 Player replay: "+message);}
    void OnEnable(){Application.logMessageReceived+=RuntimeLog;}
    void OnDisable(){Application.logMessageReceived-=RuntimeLog;}
    void RuntimeLog(string message,string trace,LogType type)
    {if(failure==null&&(type==LogType.Exception||type==LogType.Error))failure=message;}
    void OnDestroy(){if(tap!=null)tap.StopCapture();}
    void OnApplicationQuit(){if(!finished)Finish("FAIL: Player quit before H11 evidence completed",1);}

    void Update()
    {
        if(finished){PollPeer();return;}
        if(!String.IsNullOrEmpty(captureFailure)){Finish("FAIL: H11 screenshot capture failed: "+captureFailure,1);return;}
        SampleUpdateWallBoundary();
        try
        {
            if(failure!=null)throw new InvalidOperationException("Player runtime error: "+failure);
            if(Now>130)throw new TimeoutException("Bounded H11 replay exceeded 130 seconds");
            if(app==null)app=FindObjectOfType<ClientApp>();if(app==null)return;
            if(!EnsureResolution())return;
            StateView state=Field<StateView>("state");if(state==null||state.phase!="play")return;
            if(!initialized){if(!OpeningSpeechSettled())return;Initialize(state);return;}
            if(state.match_id!=initial.match_id)throw new InvalidOperationException("Player left the prepared H11 match");
            if(role=="driver"&&castState==null)TrySendPower();
            if(castState==null&&state.version>initial.version&&TryBindCast(state))Capture("cast");
            if(castState==null)return;
            ObserveCastDisplay();
            if(Now>=nextProgressPublish){PublishProgress(state);nextProgressPublish=Now+.5f;}
            ObserveSource(state);
            ObserveTokenAudioPlayback();
            if(source.sourceSeen&&!source.released)
            {
                SampleUnlock(state);
                if(role=="driver"&&unlock.sampled&&!endTurnSent)SubmitEndTurn(state);
                ObserveEndTurnResponse(state);
            }
            if(!source.released)return;
            if(!unlock.sampled)throw new InvalidOperationException("Input did not unlock while the H11 source was active");
            if(endState==null&&state.version>castState.version&&
               (state.events??new SemanticEvent[0]).Any(item=>item.type=="Death"&&item.target==pair.ghoul.entity))
            {
                if(presentation.Busy||effects.HasDeathKnightRouteFor(pair)||presentation.Display==null||presentation.Display.public_hash!=state.public_hash)return;
                if(role=="driver")
                    Check(unlock.authorityResponseMatched&&unlock.sourceActiveAtSubmit,
                        "the real End Turn did not receive an authority response while H11 source was active");
                unlock.sourceTailClearedAfterResponse=true;unlock.sourceTailCountAfterResponse=presentation.NativeVisualTailCount;
                endState=state;report.afterEndTurn=EndSnapshot(state);Capture("after-end-turn");
                PublishProgress();finalizing=true;peerDeadline=Time.realtimeSinceStartup+30;
            }
            if(finalizing)PollPeer();
        }
        catch(Exception error){Debug.LogException(error);Finish("FAIL: "+error.Message,1);}
    }
    void SampleUpdateWallBoundary()
    {
        previousUpdateWallBoundary=updateWallBoundary;
        updateWallBoundary=(float)wallClock.Elapsed.TotalSeconds;
        updateFrameWallSeconds=previousUpdateWallBoundary<0?0:Mathf.Max(0,updateWallBoundary-previousUpdateWallBoundary);
    }
    bool EnsureResolution()
    {
        int width,height;
        if(!int.TryParse(Argument("-screen-width"),out width)||!int.TryParse(Argument("-screen-height"),out height))
            throw new InvalidOperationException("Explicit -screen-width and -screen-height are required");
        if(Screen.width==width&&Screen.height==height)return true;
        if(!resolutionRequested){resolutionRequested=true;Screen.SetResolution(width,height,FullScreenMode.Windowed);return false;}
        if(Time.realtimeSinceStartup-started>15)throw new InvalidOperationException("Player resolution differs from requested size: "+Screen.width+"x"+Screen.height);
        return false;
    }
    bool resolutionRequested;
    void Initialize(StateView state)
    {
        effects=Field<NativeEffectPlayer>("nativeEffects");powers=Field<NativePowerVisual>("powerVisual");
        summons=Field<NativeBattleSummons>("nativeSummons");presentation=Field<Presentation>("presentation");assets=Field<AssetRegistry>("assets");
        Check(effects!=null&&powers!=null&&summons!=null&&presentation!=null&&assets!=null,"ClientApp playback services are incomplete");
        Check(Path.GetFullPath(Argument("--data"))==dataRoot,"Player runtime root differs from --data");
        indexHash=HashFile(Path.Combine(assets.Root,"native-effect-index.json"));
        var h11=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json"))).entries.Single(entry=>entry.card_id==HeroPowerId);
        Check(h11.enabled&&Mathf.Abs(h11.duration-5f)<.001f,"isolated runtime did not enable only the expected H11 playback contract");
        Check(Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(.499f)-.499f)<.00001f&&
              Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(.5f)-.5f)<.00001f&&
              Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(1f)-1f)<.00001f&&
              NativeDeathKnightHeroPowerPlayback.PlaybackAge(NativeDeathKnightHeroPowerPlayback.IdentityThroughAge)==
                  NativeDeathKnightHeroPowerPlayback.AuthoredTimelineDuration,
              "H11 clock must preserve the authored reveal/finalize phases and only skip the empty post-audio tail");
        int listeners=FindObjectsOfType<AudioListener>().Count(listener=>listener.enabled);
        Check(listeners==1,"expected exactly one active Unity AudioListener, got "+listeners);
        tap=FindObjectsOfType<AudioListener>().Single(listener=>listener.enabled).gameObject.AddComponent<FireblastPcmTap>();
        tap.Begin(Path.Combine(folder,"scene-mix.wav"));
        float measuredAudioGrace=tap.DspBuffer>0&&tap.DspBuffers>0&&tap.Rate>0?
            tap.DspBuffer*(float)tap.DspBuffers/tap.Rate:0;
        Check(Mathf.Abs(measuredAudioGrace-NativeDeathKnightHeroPowerPlayback.AudioGraceSeconds)<.00001f,
            "H11 source clock grace differs from the actual Unity DSP queue configuration");
        var native=effects.AudioCue;effects.AudioCue=cue=>{RecordAudio(cue,"native-effect");native?.Invoke(cue);};
        var button=powers.AudioCue;powers.AudioCue=cue=>{RecordAudio(cue,"power-ui");button?.Invoke(cue);};
        ResolveExpectedAudio();
        source.playbackClock=new PlaybackClockSnapshot
        {
            timelineStartAge=NativeDeathKnightHeroPowerPlayback.TimelineStartAge,
            tokenAudioDuration=NativeDeathKnightHeroPowerPlayback.TokenAudioDuration,
            audioGraceSeconds=NativeDeathKnightHeroPowerPlayback.AudioGraceSeconds,
            identityThroughAge=NativeDeathKnightHeroPowerPlayback.IdentityThroughAge,
            authoredDuration=NativeDeathKnightHeroPowerPlayback.AuthoredTimelineDuration
        };
        initial=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(state));
        Check(initial.seat==(role=="driver"?0:1),"unexpected seat for "+role);
        Check(initial.players!=null&&initial.players.Length==2&&initial.players[0].power.card_id==HeroPowerId&&
              initial.players[1].power.card_id=="HERO_08bp","authoritative match is not Death Knight versus Mage");
        Check(initial.active==0,"Death Knight is not the active seat");
        initialActionCounter=Field<int>("actionCounter");
        report=new Report{role=role,dataRoot=dataRoot,dataIndexSha256=indexHash,screenWidth=Screen.width,screenHeight=Screen.height,
            before=BeforeSnapshot(initial),source=source,inputUnlockedDuringSource=unlock,initialActionCounter=initialActionCounter,
            audio=new AudioSnapshot{buttonFile=buttonFile,tokenFile=tokenFile},dsp=new DspSnapshot{listenerCount=listeners}};
        Capture("ready");initialized=true;
        if(role=="driver")TrySendPower();
    }
    void ResolveExpectedAudio()
    {
        string programPath=Path.Combine(assets.Root,"40ac8d77701fdbccc4033483.hero-power-source.json");
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(programPath));
        var action=program.machines.SelectMany(machine=>machine.states).SelectMany(state=>state.actions).Single(row=>row.name=="AudioPlayClipAction");
        var sound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,action.Param("m_Sound").asset_path)));
        var button=NativeAudio.ResolveClip(sound,0,action.Param("m_Volume").number,(int)action.Param("m_Pitch").number,
            (int)action.Param("m_Category").number,()=>0);
        var audio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
        var soundTrack=program.timelines.Single().tracks.Single(track=>track.kind=="SoundTimelineTrack");
        var token=NativeDeathKnightHeroPowerPlayback.ResolveTimelineSoundCue(program,audio,assets.Root,program.timelines.Single(),soundTrack,null,0,()=>0);
        Check(button!=null&&token!=null&&button.file!=token.file,"H11 source Button/Timeline Token audio resolution failed");
        buttonFile=button.file;tokenFile=token.file;
    }
    void TrySendPower()
    {
        if(actionSent||role!="driver")return;
        var power=initial.players[0].power;
        Check((initial.moves??new Move[0]).Any(move=>move.kind=="power"&&move.entity==power.entity&&move.target==0),
              "ready snapshot has no real legal HERO_11bp move");
        var locations=Field<Dictionary<int,Rect>>("locations");Rect rect;
        if(!locations.TryGetValue(power.entity,out rect))
        {if(Now>20)throw new TimeoutException("HERO_11bp was not rendered as a real GUI input target");return;}
        var point=rect.center;
        SetField("EvidencePointer",(Vector2?)point);SetField("EvidencePressed",true);
        Invoke("HandleBattleInput",EventType.MouseDown,0,point);
        SetField("EvidencePressed",false);Invoke("HandleBattleInput",EventType.MouseUp,0,point);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")>initialActionCounter,"real GUI hero-power click did not submit an action");
        actionSent=true;
    }
    bool TryBindCast(StateView state)
    {
        var powerEvents=(state.events??new SemanticEvent[0]).Where(item=>item.type=="PlayHeroPower"&&item.target==initial.players[0].power.entity).ToArray();
        if(powerEvents.Length==0){if(Now>25)throw new TimeoutException("Authority did not receive the real HERO_11bp move");return false;}
        Check(powerEvents.Length==1,"authority emitted duplicate PlayHeroPower events");
        StateView before=Field<StateView>("previous");
        Check(before!=null&&before.version==initial.version,"PlayHeroPower did not follow the captured ready snapshot");
        Check(NativeDeathKnightHeroPowerPlayback.TryGetPair(powerEvents[0],before,state,out pair),
              "PlayHeroPower.target did not pair with exactly one real Summon.source/HERO_11bpt");
        castState=state;
        castAcceptedTime=Now;castAcceptedWallTime=(float)wallClock.Elapsed.TotalSeconds;castAcceptedFrame=Time.frameCount;
        report.castAcceptedWallTime=castAcceptedWallTime;report.castAcceptedFrame=castAcceptedFrame;
        report.cast=CastSnapshot(before,state,pair);
        Check(report.cast.playHeroPowerTarget==report.cast.summonSource&&report.cast.ghoulEntity==report.cast.summonTarget,
              "authoritative H11 event/entity pairing differs");
        if(role=="driver")Check(Field<int>("actionCounter")==initialActionCounter+1,"driver did not submit exactly one action");
        PublishProgress();return true;
    }
    void ObserveSource(StateView state)
    {
        if(source.released)return;
        NativeEffectInstance effect=H11Instance();
        bool routeActive=effect!=null&&effects.HasDeathKnightRouteFor(pair);
        if(routeActive)
        {
            source.sourceSeen=true;source.activeEffectCount=Math.Max(source.activeEffectCount,1);
            if(source.startTime==0)source.startTime=Now;
            var live=Field<NativeBattleActors>("nativeActors").Find(pair.ghoul);
            if(live!=null)
            {
                var identity=NativeActorIdentity.FindInThisOrParents(live.ActorObject);
                bool bound=identity!=null&&effect.Root!=null&&effect.Root.transform.parent==identity.transform;
                var sample=CastSnapshot(castState,castState,pair);sample.actorPresent=true;sample.actorEntity=pair.ghoul.entity;
                sample.actorCardId=live.CardId;sample.actorVisible=live.Visible;sample.actorBound=bound;
                sample.actorPresent=identity!=null;report.cast.actorPresent=sample.actorPresent;report.cast.actorEntity=sample.actorEntity;
                report.cast.actorCardId=sample.actorCardId;report.cast.actorVisible=sample.actorVisible;report.cast.actorBound=sample.actorBound;
                if(!sourceActorChecked){Check(bound,"source root is not parented to the authoritative HERO_11bpt Actor");sourceActorChecked=true;}
            }
            float sourceAge=Now-source.startTime;
            var clockSample=SamplePlaybackClock(effect,sourceAge);
            source.lastPlaybackSourceAge=clockSample.sourceAge;
            playbackClockSamples.Add(clockSample);source.playbackClockSamples=playbackClockSamples.ToArray();
            SampleProjection(effect,live,sourceAge,clockSample.sourceAge,clockSample.playbackAge);
            source.genericSummonActiveCount=summons.ActiveCount;
            source.genericSummonFallback=source.genericSummonActiveCount!=0;
            source.customSpawnInitializations=Math.Max(source.customSpawnInitializations,effect.CustomSpawnInitializations);
            source.sideEventDispatches=Math.Max(source.sideEventDispatches,effect.SpellPlayerSideEventDispatches);
            source.timelineStarts=Math.Max(source.timelineStarts,effect.TimelineStarts);
            source.timelineSoundStarts=Math.Max(source.timelineSoundStarts,effect.TimelineSoundStarts);
            source.unhandledActions=Math.Max(source.unhandledActions,effect.UnhandledActions);
            source.unsupportedTimelineTracks=Math.Max(source.unsupportedTimelineTracks,effect.UnsupportedTimelineTracks);
            source.unboundActions=Math.Max(source.unboundActions,UnboundCount(effect));
            source.actionExecutions=ActionCounts(effect);
            if(!images.Contains("source-active.png"))Capture("source-active");
            if(sourceAge>=.12f&&!images.Contains("source-button-0.12s.png"))Capture("source-button-0.12s");
            if(sourceAge>=.28f&&!images.Contains("source-button-0.28s.png"))Capture("source-button-0.28s");
            if(sourceAge>=.55f&&!images.Contains("source-eruption-0.55s.png"))Capture("source-eruption-0.55s");
            if(sourceAge>=1f&&!images.Contains("source-eruption-1.0s.png"))Capture("source-eruption-1.0s");
            return;
        }
        TrySendPower();
        if(source.sourceSeen)
        {
            source.released=true;source.releaseTime=Now;source.duration=source.releaseTime-source.startTime;
            source.releaseFrameAllowance=Mathf.Max(updateFrameWallSeconds,.001f)*2;
            source.genericSummonActiveCount=summons.ActiveCount;
            Capture("source-released");
        }
        else if(Now>30)throw new TimeoutException("H11 source route never became active");
    }
    static readonly float[] ProjectionAges={.04f,.12f,.28f,.55f,1f};
    void SampleProjection(NativeEffectInstance effect,CardVisuals.LiveActor live,float age,float sourceAge,float playbackAge)
    {
        if(projections.Count>=ProjectionAges.Length||age<ProjectionAges[projections.Count]||effect==null||
           effect.Nodes==null||effect.Nodes.Length<=17||effect.Program==null||effect.Program.nodes==null||effect.Program.nodes.Length<=17)return;
        var camera=effects.TimelineCamera;var sourceNode=effect.Nodes[17];
        var actor=live!=null?live.ActorObject:effect.Root==null||effect.Root.transform.parent==null?null:effect.Root.transform.parent.gameObject;
        var identity=actor==null?null:NativeActorIdentity.FindInThisOrParents(actor);
        var locations=Field<Dictionary<int,Rect>>("locations");Rect powerRect=default(Rect),ghoulRect=default(Rect);
        bool hasPower=locations!=null&&locations.TryGetValue(pair.powerEntity.entity,out powerRect);
        bool hasGhoul=locations!=null&&locations.TryGetValue(pair.ghoul.entity,out ghoulRect);
        var nativeCamera=effect.Root==null?null:effect.Root.GetComponentsInChildren<Camera>(true).FirstOrDefault(item=>item.gameObject.name=="NativeEffectCamera");
        var sourceNodeData=effect.Program.nodes[17];int layer=sourceNode.layer;int cameraMask=camera==null?0:camera.cullingMask;
        Check(camera!=null&&(cameraMask&(1<<30))==0,"board PresentationCamera still directly renders native source layer 30");
        Vector3 nodeScreen=camera==null?Vector3.zero:camera.WorldToScreenPoint(sourceNode.transform.position);
        Vector3 actorWorld=actor==null?Vector3.zero:actor.transform.position;
        Vector3 actorScreen=camera==null||actor==null?Vector3.zero:camera.WorldToScreenPoint(actorWorld);
        var sample=new ProjectionSample{index=projections.Count,viewerSeat=Field<StateView>("state").seat,ownerSeat=pair.ownerIndex,
            frame=Time.frameCount,observedAge=age,sourceAge=sourceAge,playbackAge=playbackAge,cameraName=camera==null?"":camera.name,
            cameraPosition=camera==null?new float[0]:VectorValues(camera.transform.position),
            cameraRotation=camera==null?new float[0]:VectorValues(camera.transform.eulerAngles),
            cameraFieldOfView=camera==null?0:camera.fieldOfView,cameraOrthographicSize=camera==null?0:camera.orthographicSize,
            boardCameraMask=cameraMask,nativeCameraMask=nativeCamera==null?0:nativeCamera.cullingMask,sourceLayer=layer,
            boardCameraSeesSourceLayer=(cameraMask&(1<<layer))!=0,
            nodeName=sourceNodeData.name,nodePath=sourceNodeData.path,nodeParent=sourceNodeData.parent<0?"":effect.Program.nodes[sourceNodeData.parent].name,
            nodeWorld=VectorValues(sourceNode.transform.position),nodeScreen=ScreenValues(nodeScreen),
            actorPresent=actor!=null,actorName=actor==null?"":actor.name,actorParent=actor==null||actor.transform.parent==null?"":actor.transform.parent.name,
            actorWorld=VectorValues(actorWorld),actorScreen=ScreenValues(actorScreen),actorBound=identity!=null&&effect.Root.transform.parent==identity.transform,
            hasPowerRect=hasPower,hasGhoulRect=hasGhoul,powerRect=hasPower?RectValues(powerRect):new float[0],ghoulRect=hasGhoul?RectValues(ghoulRect):new float[0]};
        projections.Add(sample);this.source.projections=projections.ToArray();PublishProgress();
    }
    PlaybackClockSample SamplePlaybackClock(NativeEffectInstance effect,float wallAge)
    {
        var activeField=typeof(NativeEffectPlayer).GetField("active",Private);
        var sourceField=typeof(NativeEffectPlayer).GetField("deathKnightSourceAges",Private);
        var playbackField=typeof(NativeEffectPlayer).GetField("deathKnightAges",Private);
        var tokenField=typeof(NativeEffectPlayer).GetField("deathKnightTokenAudioAtMap",Private);
        var active=activeField==null?null:activeField.GetValue(effects) as IDictionary;
        var sourceAges=sourceField==null?null:sourceField.GetValue(effects) as Dictionary<string,float>;
        var playbackAges=playbackField==null?null:playbackField.GetValue(effects) as Dictionary<string,float>;
        var tokenStates=tokenField==null?null:tokenField.GetValue(effects) as Dictionary<string,bool>;
        Check(active!=null&&sourceAges!=null&&playbackAges!=null&&tokenStates!=null,"H11 source clock diagnostics are unavailable");
        foreach(DictionaryEntry item in active)
        {
            if(!ReferenceEquals(item.Value,effect))continue;
            string key=item.Key as string;float sourceAge,playbackAge;bool tokenAudioActive;
            if(key==null||!sourceAges.TryGetValue(key,out sourceAge)||!playbackAges.TryGetValue(key,out playbackAge)||
               !tokenStates.TryGetValue(key,out tokenAudioActive))
                throw new InvalidOperationException("active H11 source has no paired raw/mapped clock sample");
            Check(Mathf.Abs(playbackAge-NativeDeathKnightHeroPowerPlayback.PlaybackAge(sourceAge,tokenAudioActive))<.0001f,
                "H11 PrepareSource did not use the active-token-aware shared source-age mapping");
            if(tokenAudioActive&&sourceAge>=NativeDeathKnightHeroPowerPlayback.IdentityThroughAge)
                source.tokenAudioActiveBeyondIdentity=true;
            Check(sourceAge>=previousPlaybackSourceAge&&playbackAge>=previousPlaybackAge,
                "H11 source playback clock regressed across Prepare/Draw samples");
            Check(wallAge>=previousPlaybackWallAge,"H11 Player wall-time samples regressed");
            previousPlaybackWallAge=wallAge;previousPlaybackSourceAge=sourceAge;previousPlaybackAge=playbackAge;
            return new PlaybackClockSample{frame=Time.frameCount,wallAge=wallAge,sourceAge=sourceAge,
                playbackAge=playbackAge,tokenAudioActiveAtMap=tokenAudioActive};
        }
        throw new InvalidOperationException("H11 active source is missing from the source-age diagnostics");
    }
    void ObserveTokenAudioPlayback()
    {
        if(tokenAudioCue==null||tokenAudioCue.playbackActive==null)return;
        bool playing=tokenAudioCue.playbackActive();source.tokenPlaybackActive=playing;
        if(playing)
        {
            source.tokenPlaybackObserved=true;
            if(!tokenPlaybackWasActive)source.tokenPlaybackFirstActiveTime=Now;
            tokenPlaybackWasActive=true;
            if(source.lastPlaybackSourceAge>=NativeDeathKnightHeroPowerPlayback.IdentityThroughAge)
                source.tokenAudioActiveBeyondIdentity=true;
        }
        else if(tokenPlaybackWasActive&&!source.tokenPlaybackEnded)
        {
            source.tokenPlaybackEnded=true;source.tokenPlaybackEndTime=Now;
            source.tokenPlaybackEndFrameAllowance=Mathf.Max(0,updateFrameWallSeconds);
            source.tokenCallbackToEndSeconds=source.tokenPlaybackEndTime-source.tokenCueCallbackTime;
            tokenPlaybackWasActive=false;
        }
    }
    static float[] VectorValues(Vector3 value){return new[]{value.x,value.y,value.z};}
    float[] ScreenValues(Vector3 value){return new[]{value.x,Screen.height-value.y,value.z};}
    static float[] RectValues(Rect value){return new[]{value.x,value.y,value.width,value.height};}
    bool sourceActorChecked;
    NativeEffectInstance H11Instance()
    {
        var field=typeof(NativeEffectPlayer).GetField("active",Private);var values=field.GetValue(effects) as IDictionary;
        if(values==null)return null;
        foreach(DictionaryEntry item in values)
        {var effect=item.Value as NativeEffectInstance;if(effect!=null&&effect.Program!=null&&effect.Program.reference==ProgramReference)return effect;}
        return null;
    }
    static int UnboundCount(NativeEffectInstance effect)
    {
        return effect.UnboundSpellPlayerSideEventActions+effect.UnboundCustomSpawnActions+effect.UnboundActorVisibilityActions+
            effect.UnboundSetParentActions+effect.UnboundDestroyObjectActions+effect.UnboundTimelineActions+effect.UnboundTimelineSounds+
            effect.UnsupportedTimelineTracks+effect.UnboundTimelineCameras+effect.UnboundAudioActions+effect.UnboundAnimationActions+
            effect.UnboundMachineOwnerActions+effect.UnboundMainShakeActions+effect.UnhandledActions;
    }
    static ActionRow[] ActionCounts(NativeEffectInstance effect)
    {return effect.ActionExecutions.OrderBy(row=>row.Key).Select(row=>new ActionRow{name=row.Key,count=row.Value}).ToArray();}
    Snapshot BeforeSnapshot(StateView state)
    {
        var own=state.players[0];return new Snapshot{publicHash=state.public_hash,displayHash=state.public_hash,
            seat=state.seat,turn=state.turn,heroPower=own.power.card_id,powerEntity=own.power.entity,mana=own.mana,
            powerUsed=own.power.used,ghoulPresent=(own.field??new EntityView[0]).Any(entity=>entity.card_id==GhoulId)};
    }
    Snapshot CastSnapshot(StateView before,StateView after,NativeDeathKnightHeroPowerPlayback.Pair actualPair)
    {
        var own=after.players[0];var events=after.events??new SemanticEvent[0];var plays=events.Where(item=>item.type=="PlayHeroPower").ToArray();
        var summonsForPower=events.Where(item=>item.type=="Summon"&&item.source==actualPair.powerEntity.entity).ToArray();
        var actor=Field<NativeBattleActors>("nativeActors").Find(actualPair.ghoul);
        var identity=actor==null?null:NativeActorIdentity.FindInThisOrParents(actor.ActorObject);
        var effect=H11Instance();bool bound=effect!=null&&identity!=null&&effect.Root.transform.parent==identity.transform;
        string displayHash=presentation.Display==null?"":presentation.Display.public_hash;
        return new Snapshot{publicHash=after.public_hash,displayHash=displayHash,displayMatchedAtAcceptance=displayHash==after.public_hash,
            seat=after.seat,turn=after.turn,powerEntity=actualPair.powerEntity.entity,powerCardId=actualPair.powerEntity.card_id,
            powerUsed=own.power.used,powerManaSpent=before.players[0].mana-own.mana,mana=own.mana,
            playHeroPowerCount=plays.Count(item=>item.target==actualPair.powerEntity.entity),summonCount=summonsForPower.Length,
            playHeroPowerTarget=plays.Single(item=>item.target==actualPair.powerEntity.entity).target,
            summonSource=summonsForPower.Single().source,summonTarget=summonsForPower.Single().target,
            ghoulEntity=actualPair.ghoul.entity,ghoulCardId=actualPair.ghoul.card_id,ghoulAttack=actualPair.ghoul.attack,
            ghoulHealth=actualPair.ghoul.health,ghoulCharge=(actualPair.ghoul.keywords??new string[0]).Contains("charge"),
            ghoulPresent=own.field.Any(entity=>entity.entity==actualPair.ghoul.entity&&entity.card_id==GhoulId),
            actorEntity=actor==null?0:actualPair.ghoul.entity,actorCardId=actor==null?"":actor.CardId,
            actorPresent=identity!=null,actorVisible=actor!=null&&actor.Visible,actorBound=bound};
    }
    Snapshot EndSnapshot(StateView state)
    {
        var events=state.events??new SemanticEvent[0];var own=state.players[0];
        return new Snapshot{publicHash=state.public_hash,displayHash=presentation.Display==null?"":presentation.Display.public_hash,
            seat=state.seat,turn=state.turn,ghoulEntity=pair.ghoul.entity,ghoulCardId=pair.ghoul.card_id,
            ghoulPresent=own.field.Any(entity=>entity.entity==pair.ghoul.entity&&entity.card_id==GhoulId),
            deathEvents=events.Where(item=>item.type=="Death").Select(ToEvent).ToArray()};
    }
    static EventRow ToEvent(SemanticEvent item)
    {return new EventRow{type=item.type,cardId=item.card_id,source=item.source,target=item.target,owner=item.owner};}
    void ObserveCastDisplay()
    {
        if(report==null||!String.IsNullOrEmpty(report.castDisplaySettleHash)||castState==null||presentation.Display==null||
           presentation.Display.public_hash!=castState.public_hash)return;
        report.castDisplaySettleHash=presentation.Display.public_hash;report.castDisplaySettleTime=Now;
        report.castDisplaySettleFrame=Time.frameCount;PublishProgress();
    }
    void SampleUnlock(StateView state)
    {
        if(unlock.sampled||!source.sourceSeen||source.released)return;
        bool sourceActive=effects.HasDeathKnightRouteFor(pair)&&!source.released;
        if(!sourceActive)return;
        var move=(state.moves??new Move[0]).FirstOrDefault(item=>item.kind=="end_turn");
        bool pending=Field<bool>("pending");string displayHash=presentation.Display==null?"":presentation.Display.public_hash;
        bool matched=displayHash==castState.public_hash;
        if(pending||presentation.InputLocked)return;
        unlock.sampled=true;unlock.sourceReleased=false;unlock.busy=presentation.Busy;unlock.inputLocked=presentation.InputLocked;
        unlock.pending=pending;unlock.legalEndTurn=move!=null;unlock.displayHashMatched=matched;unlock.displayHashAtSample=displayHash;
        unlock.sourceActiveAtSample=sourceActive;
        unlock.seat=state.seat;unlock.active=state.active;unlock.sampleTime=Now;unlock.sampleFrame=Time.frameCount;
        unlock.unlockedDuringSource=true;
        unlock.sourceTailCountAtSample=presentation.NativeVisualTailCount;
        if(role=="driver")Check(state.seat==0&&state.active==0&&move!=null,"driver did not expose a legal end_turn after the short H11 input gate");
        ObserveCastDisplay();PublishProgress();Capture("input-unlocked-during-source");
    }
    void SubmitEndTurn(StateView state)
    {
        var move=(state.moves??new Move[0]).FirstOrDefault(item=>item.kind=="end_turn");
        bool active=effects.HasDeathKnightRouteFor(pair)&&!source.released;
        bool pending=Field<bool>("pending");string displayHash=presentation.Display==null?"":presentation.Display.public_hash;
        bool matched=displayHash==castState.public_hash;
        Check(active,"H11 source was not active when the driver tried the follow-up action");
        Check(!pending&&!presentation.InputLocked&&move!=null&&state.seat==0&&state.active==0,
            "short H11 input gate did not admit the driver's legal end_turn");
        unlock.sourceActiveAtSubmit=active;unlock.sourceTailActiveAtSubmit=presentation.NativeVisualTailCount>0;
        unlock.presentationBusyAtSubmit=presentation.Busy;unlock.inputLockedAtSubmit=presentation.InputLocked;
        unlock.displayHashAtSubmit=displayHash;unlock.displayHashMatchedAtSubmit=matched;
        unlock.sourceTailCountAtSubmit=presentation.NativeVisualTailCount;unlock.castAcceptedTime=castAcceptedTime;
        float submitWall=(float)wallClock.Elapsed.TotalSeconds;
        unlock.castAcceptedFrame=castAcceptedFrame;unlock.castToSubmitSeconds=submitWall-castAcceptedWallTime;
        unlock.inputFrameAllowance=updateFrameWallSeconds;unlock.submitWallTime=submitWall;
        unlock.previousUpdateWallBoundary=previousUpdateWallBoundary;unlock.submissionUpdateWallBoundary=updateWallBoundary;
        unlock.submitTime=Now;unlock.submitFrame=Time.frameCount;
        unlock.submitVersion=state.version;unlock.actionCounterAtSubmit=Field<int>("actionCounter");
        Check(unlock.submitFrame==unlock.sampleFrame,
            "H11 follow-up was not submitted in the first Update that observed the open input gate");
        Check(unlock.inputFrameAllowance>0&&updateWallBoundary>previousUpdateWallBoundary&&submitWall>=updateWallBoundary,
            "H11 monotonic Update wall boundaries are incomplete");
        Check(unlock.castToSubmitSeconds>=0&&unlock.castToSubmitSeconds<=.2f+unlock.inputFrameAllowance+.005f,
            "driver follow-up submission exceeded the 0.2s input gate plus one frame");
        Check(unlock.actionCounterAtSubmit==initialActionCounter+1,"driver action counter did not include exactly the accepted power before follow-up");
        endTurnSent=true;Invoke("SendMove",move);
        unlock.actionCounterAfterSend=Field<int>("actionCounter");
        Check(Field<bool>("pending")&&unlock.actionCounterAfterSend==initialActionCounter+2,
            "production SendMove did not submit End Turn while H11 source remained active");
        PublishProgress(state);
    }
    void ObserveEndTurnResponse(StateView state)
    {
        if(role!="driver"||!endTurnSent||unlock.authorityResponseMatched||state.version<=unlock.submitVersion)return;
        bool advanced=state.active==1&&state.public_hash!=castState.public_hash;
        bool ghoulDied=(state.events??new SemanticEvent[0]).Any(item=>item.type=="Death"&&item.target==pair.ghoul.entity);
        if(!advanced||!ghoulDied)return;
        unlock.authorityResponseMatched=true;unlock.responseVersion=state.version;unlock.responseActive=state.active;
        unlock.responseHash=state.public_hash;unlock.responseTime=Now;unlock.responseFrame=Time.frameCount;
        unlock.authorityResponseWhileSourceActive=effects.HasDeathKnightRouteFor(pair)&&!source.released;
        unlock.sourceTailCountAtResponse=presentation.NativeVisualTailCount;
        unlock.responseDisplayHashMatched=presentation.Display!=null&&presentation.Display.public_hash==state.public_hash;
        PublishProgress(state);
    }
    void RecordAudio(NativeAudioCue cue,string kind)
    {
        if(cue==null)return;
        if(kind=="native-effect"&&cue.file==buttonFile)kind="native-effect-button";
        else if(kind=="native-effect"&&cue.file==tokenFile)
        {
            kind="native-effect-token";tokenAudioCue=cue;source.tokenCueSeen=true;source.tokenCueCallbackTime=Now;source.tokenCuePitch=cue.pitch;
        }
        audioRecords.Add(new AudioRecord{kind=kind,file=cue.file,time=Now,cueTime=cue.time,pitch=cue.pitch,frame=Time.frameCount,
            playing=cue.playbackActive!=null&&cue.playbackActive()});
    }
    void Capture(string name)
    {
        string file=name+".png";if(!captureRequests.Add(file))return;
        captureQueue.Enqueue(file);
        if(!capturePumpActive){capturePumpActive=true;StartCoroutine(DrainCaptures());}
    }
    System.Collections.IEnumerator DrainCaptures()
    {
        capturePumpActive=true;
        while(captureQueue.Count>0)
        {
            string file=captureQueue.Dequeue(),path=Path.Combine(folder,file);
            try{ScreenCapture.CaptureScreenshot(path);}
            catch(Exception error){captureFailure=file+": "+error.GetBaseException().Message;break;}
            float deadline=Time.realtimeSinceStartup+10f;bool ready=false;
            while(Time.realtimeSinceStartup<deadline&&!(ready=ValidScreenshot(path)))yield return null;
            if(!ready){captureFailure=file+" was not fully written as a PNG before timeout";break;}
            images.Add(file);report.images=images.OrderBy(value=>value).ToArray();
            try{WriteReport();}
            catch(Exception error){captureFailure=file+" report update: "+error.GetBaseException().Message;break;}
            yield return null;
        }
        capturePumpActive=false;
    }
    bool ValidScreenshot(string path)
    {
        if(!File.Exists(path))return false;
        try
        {
            byte[] header=new byte[24];
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                if(stream.Length<header.Length)return false;
                int offset=0;
                while(offset<header.Length)
                {int read=stream.Read(header,offset,header.Length-offset);if(read==0)return false;offset+=read;}
            }
            return header[0]==0x89&&header[1]==0x50&&header[2]==0x4e&&header[3]==0x47&&
                header[4]==0x0d&&header[5]==0x0a&&header[6]==0x1a&&header[7]==0x0a&&
                ((header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19])==report.screenWidth&&
                ((header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23])==report.screenHeight;
        }
        catch(IOException){return false;}
    }
    void PublishProgress(StateView state=null)
    {
        if(report==null)return;
        if(state==null)state=app==null?null:Field<StateView>("state");
        var display=presentation==null?null:presentation.Display;
        var progress=new Progress{role=role,cast=report.cast,afterEndTurn=report.afterEndTurn,unlock=unlock,
            authorityHash=state==null?"":state.public_hash,displayHash=display==null?"":display.public_hash,
            authorityVersion=state==null?0:state.version,displayVersion=display==null?0:display.version,
            queueCount=presentation==null?0:presentation.QueueCount,presentationTime=presentation==null?0:presentation.TimeInBatch,
            presentationBusy=presentation!=null&&presentation.Busy,inputLocked=presentation!=null&&presentation.InputLocked,
            pending=app!=null&&Field<bool>("pending"),seat=state==null?0:state.seat,active=state==null?0:state.active,
            legalEndTurn=state!=null&&(state.moves??new Move[0]).Any(move=>move.kind=="end_turn"),sourceReleased=source.released};
        string path=Path.Combine(folder,"progress.json"),temporary=path+".tmp";
        File.WriteAllText(temporary,JsonUtility.ToJson(progress,true));if(File.Exists(path))File.Delete(path);File.Move(temporary,path);
    }
    string PeerProgressPath
    {get{return Path.Combine(Directory.GetParent(folder).FullName,role=="driver"?"observer":"driver","progress.json");}}
    Progress ReadPeerProgress()
    {
        if(!File.Exists(PeerProgressPath))return null;
        try{return JsonUtility.FromJson<Progress>(File.ReadAllText(PeerProgressPath));}
        catch{return null;}
    }
    void PollPeer()
    {
        if(!finalizing||finished)return;
        if(!String.IsNullOrEmpty(captureFailure)){Finish("FAIL: H11 screenshot capture failed: "+captureFailure,1);return;}
        if(Time.realtimeSinceStartup>peerDeadline){Finish("FAIL: Peer did not publish matching H11 state hashes",1);return;}
        try
        {
            var peer=ReadPeerProgress();
            Check(peer!=null&&peer.role==(role=="driver"?"observer":"driver")&&peer.cast!=null&&peer.afterEndTurn!=null,
                  "peer H11 progress report is incomplete");
            if(String.IsNullOrEmpty(peer.afterEndTurn.publicHash))return;
            Check(peer.cast.publicHash==report.cast.publicHash&&peer.afterEndTurn.publicHash==report.afterEndTurn.publicHash,
                  "dual Player authoritative state hashes differ");
            if(capturePumpActive||captureQueue.Count>0)return;
            string[] requiredImages={"ready.png","cast.png","source-active.png","input-unlocked-during-source.png","source-released.png","after-end-turn.png"};
            Check(requiredImages.All(file=>images.Contains(file)&&ValidScreenshot(Path.Combine(folder,file))),
                  "H11 required phase screenshots were not fully written before final validation");
            report.cast.peerPublicHash=peer.cast.publicHash;report.afterEndTurn.peerPublicHash=peer.afterEndTurn.publicHash;
            report.source=source;report.source.projections=projections.ToArray();report.inputUnlockedDuringSource=unlock;
            report.audio.records=audioRecords.ToArray();tap.StopCapture();
            report.dsp=new DspSnapshot{listenerCount=FindObjectsOfType<AudioListener>().Count(listener=>listener.enabled),rate=tap.Rate,
                channels=tap.Channels,dspBuffer=tap.DspBuffer,dspBuffers=tap.DspBuffers,callbacks=tap.Callbacks,overflows=tap.Overflows,
                sampleFrames=tap.SampleFrames,clippedSamples=tap.ClippedSamples,peak=tap.Peak,rms=tap.Rms};
            Check(report.cast.actorPresent&&report.cast.actorCardId==GhoulId&&report.cast.actorBound,
                  "H11 source did not bind the actual authoritative HERO_11bpt Actor");
            Check(source.sourceSeen&&source.released&&!source.genericSummonFallback&&source.customSpawnInitializations==1&&
                  source.sideEventDispatches==1&&source.timelineStarts==1&&source.timelineSoundStarts==1&&source.unboundActions==0,
                  "H11 source, CustomSpawn, side event, Timeline or binding counters did not match");
            var clockSamples=source.playbackClockSamples??new PlaybackClockSample[0];
            Check(source.playbackClock!=null&&clockSamples.Length>1&&
                  clockSamples.All(item=>Mathf.Abs(item.playbackAge-NativeDeathKnightHeroPowerPlayback.PlaybackAge(
                      item.sourceAge,item.tokenAudioActiveAtMap))<.0001f)&&
                  clockSamples.Zip(clockSamples.Skip(1),(left,right)=>right.sourceAge>=left.sourceAge&&right.playbackAge>=left.playbackAge).All(value=>value)&&
                  clockSamples.Any(item=>item.sourceAge<source.playbackClock.identityThroughAge&&
                      Mathf.Abs(item.sourceAge-item.playbackAge)<.0001f)&&
                  clockSamples.Any(item=>item.sourceAge>=source.playbackClock.identityThroughAge&&item.tokenAudioActiveAtMap&&
                      item.playbackAge<source.playbackClock.authoredDuration),
                  "H11 Prepare/Draw samples did not preserve the authored clock while the actual Token audio remained active");
            Check(source.tokenCueSeen&&source.tokenPlaybackObserved&&source.tokenPlaybackEnded&&source.tokenAudioActiveBeyondIdentity,
                  "H11 source did not retain its unique token cue while the actual audio source was still playing");
            Check(source.tokenPlaybackEndTime>=source.tokenCueCallbackTime&&
                  source.tokenCallbackToEndSeconds+.001f>=source.playbackClock.tokenAudioDuration&&
                  source.releaseTime+source.releaseFrameAllowance>=source.tokenPlaybackEndTime,
                  "H11 source ended before the actual token WAV finished playing");
            Check(unlock.sampled&&!unlock.sourceReleased&&!unlock.inputLocked&&!unlock.pending&&
                  unlock.sourceActiveAtSample&&unlock.unlockedDuringSource&&unlock.busy,
                  "input did not unlock during the active H11 source");
            Check(projections.Count==ProjectionAges.Length&&projections.All(item=>item.sourceLayer==30&&
                  (item.boardCameraMask&(1<<30))==0&&(item.nativeCameraMask&(1<<30))!=0&&item.hasPowerRect&&
                  item.nodeWorld.Length==3&&item.nodeScreen.Length==3)&&
                  projections[0].actorPresent&&projections[0].actorBound&&projections[0].hasGhoulRect&&
                  projections[0].actorWorld.Length==3&&projections[0].actorScreen.Length==3,
                  "H11 world/Actor projection samples or the two-camera layer separation were incomplete");
            if(role=="driver")
            {
                Check(unlock.legalEndTurn&&unlock.seat==0&&unlock.active==0&&unlock.sourceActiveAtSubmit&&
                      unlock.presentationBusyAtSubmit&&!unlock.inputLockedAtSubmit&&
                      unlock.submitFrame==unlock.sampleFrame,
                      "driver did not submit its real legal End Turn at the open input gate while H11 source was active");
                Check(unlock.authorityResponseMatched&&unlock.authorityResponseWhileSourceActive&&
                      unlock.responseVersion>unlock.submitVersion&&unlock.responseActive==1&&
                      unlock.actionCounterAfterSend==initialActionCounter+2,
                      "the End Turn submission did not receive the expected authority response during the source");
                Check(unlock.sourceTailClearedAfterResponse&&unlock.sourceTailCountAfterResponse==0&&
                      presentation.NativeVisualTailCount==0,
                      "the old source tail did not clean up after the accepted End Turn");
            }
            else Check(!unlock.legalEndTurn&&unlock.seat==1&&unlock.active==0,
                       "observer move availability or active seat changed during the H11 source");
            Check(report.afterEndTurn.deathEvents.Any(item=>item.type=="Death"&&item.target==pair.ghoul.entity)&&
                  !report.afterEndTurn.ghoulPresent,"the real summoned Ghoul did not die at end turn");
            Check(report.dsp.listenerCount==1&&report.dsp.channels==2&&report.dsp.sampleFrames>0&&report.dsp.callbacks>0&&
                  report.dsp.overflows==0&&report.dsp.peak>.00001f,"Unity AudioListener PCM capture was empty or overflowed");
            Check(audioRecords.Any(item=>item.kind=="native-effect-button"&&item.file==buttonFile)&&
                  audioRecords.Any(item=>item.kind=="native-effect-token"&&item.file==tokenFile)&&buttonFile!=tokenFile,
                  "source Button and Timeline Token sounds were not both observed");
            report.result="PASS bounded H11 authoritative dual Player source/DSP";WriteReport();finished=true;
            Debug.Log("H11 PLAYER FORMAL PASS role="+role+" ghoul="+pair.ghoul.entity+" source="+source.duration.ToString("F3"));
            Application.Quit(0);
        }
        catch(Exception error){Finish("FAIL: "+error.Message,1);}
    }
    void Finish(string result,int exitCode)
    {
        if(finished)return;finished=true;Environment.ExitCode=exitCode;
        if(tap!=null)tap.StopCapture();
        if(report==null)report=new Report{role=role,dataRoot=dataRoot};
        report.result=result;report.role=role;report.dataRoot=dataRoot;report.dataIndexSha256=indexHash;
        report.screenWidth=Screen.width;report.screenHeight=Screen.height;report.source=source;report.source.projections=projections.ToArray();
        report.initialActionCounter=initialActionCounter;
        report.inputUnlockedDuringSource=unlock;
        report.audio=report.audio??new AudioSnapshot{buttonFile=buttonFile,tokenFile=tokenFile};report.audio.records=audioRecords.ToArray();
        report.images=images.OrderBy(value=>value).ToArray();WriteReport();Debug.LogError("H11 PLAYER FORMAL "+result);Application.Quit(exitCode);
    }
    void WriteReport()
    {
        if(report==null||String.IsNullOrEmpty(folder))return;
        report.dataRoot=dataRoot;report.dataIndexSha256=indexHash;report.screenWidth=Screen.width;report.screenHeight=Screen.height;
        string path=Path.Combine(folder,"report.json"),temporary=path+".tmp";
        File.WriteAllText(temporary,JsonUtility.ToJson(report,true));if(File.Exists(path))File.Delete(path);File.Move(temporary,path);
    }
    static string HashFile(string path)
    {using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
}
