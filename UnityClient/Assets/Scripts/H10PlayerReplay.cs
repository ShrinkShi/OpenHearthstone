using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class H10PlayerReplay : MonoBehaviour
{
    const string PowerId="HERO_10bp";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    [Serializable] sealed class EventRow{public string type,cardId;public int source,target,owner;}
    [Serializable] sealed class ActionRow{public string name;public int count;}
    [Serializable] sealed class MoveRow{public string kind,choice;public int entity,target,index;}
    [Serializable] sealed class InputTrace
    {
        public string stage,matchId,phase,status,displayHash;public int seat,active,turn,version,frame,actionCounter,actionCounterDelta;
        public int selected,dragged,hitEntity,moveCount;public float time;public bool pending,connected,connectionLost,busy,inputLocked;
        public bool locationFound,hitRegion,h10PowerMoveLegal,mulliganActive,battleMenu,inputReady;public Rect powerRect;public Vector2 point;public MoveRow[] moves;
    }
    [Serializable] sealed class Snapshot
    {public string publicHash,peerPublicHash,displayHash;public int seat,active,turn,mana,heroAttack,powerEntity,heroEntity;public bool powerUsed;}
    [Serializable] sealed class AudioRecord{public string file,kind;public float time,cueTime;public int frame;public bool playing;}
    [Serializable] sealed class FrameTiming{public int frame;public float time,wallMilliseconds;}
    [Serializable] sealed class RenderTiming{public string key,stage;public int frame;public float time,milliseconds;}
    [Serializable] sealed class Dsp
    {public int listenerCount,rate,channels,dspBuffer,dspBuffers,callbacks,overflows;public long sampleFrames,clippedSamples;public float peak,rms;}
    [Serializable] sealed class Source
    {
        public string rootReference,positionDiagnostics;
        public bool seen,released,hasOriginalHistory,firstArrival;
        public Rect drawRect,targetDrawRect;
        public float startTime,releaseTime,duration;
        public int rootActions,buttonActions,playthroughActions,impactActions,unboundActions,unhandledActions,activeClones;
        public int unboundAudioActions,unboundAnimationActions,unboundMachineOwnerActions,unboundTimelineActions,unboundTimelineSounds;
        public int effectUnhandledActions,spellUnhandledActions,spellUnboundAudioActions,spellUnboundControllerActions,spellUnboundComponentActions;
        public FrameTiming[] frameTimings;public RenderTiming[] renderTimings;
        public string[] spellUnboundControllerDetails;
        public ActionRow[] actions;public AudioRecord[] audio;
    }
    [Serializable] sealed class Unlock
    {public bool sampled,sourceReleased,sourceActive,sourceActiveAtSend,busy,inputLocked,pending,legalEndTurn,displayMatched,sendMoveSubmitted,authorityAccepted;
        public int seat,active,frame,castAcceptedFrame,submissionFrame,version,acceptedVersion,activeAfter,actionCounterDelta;
        public float time,powerAcceptedTime,sendTime,sendLatency,latencyBudget,powerAcceptedWallTime,submissionWallTime;
        public float previousUpdateWallBoundary,submissionUpdateWallBoundary,inputFrameAllowance;public string authorityPublicHash;}
    [Serializable] sealed class Progress
    {public string role,castHash,endHash;public int seat,active;public bool sourceReleased,unlocked;}
    [Serializable] sealed class Report
    {
        public string result,role,dataRoot,indexSha256;public int screenWidth,screenHeight;public bool timingOnly;
        public Snapshot before,cast,afterEndTurn,latest;public EventRow[] sourceEvents;public MoveRow[] readyMoves;public InputTrace[] inputTrace;
        public Source source;public Unlock inputUnlockedAfterSource;public string[] suppressedCaptures;
        public Dsp dsp;public AudioRecord[] audio;public string[] images;
    }

    readonly List<AudioRecord> audio=new List<AudioRecord>();readonly HashSet<string> images=new HashSet<string>();readonly List<InputTrace> inputTrace=new List<InputTrace>();
    readonly List<FrameTiming> frameTimings=new List<FrameTiming>();readonly List<RenderTiming> renderTimings=new List<RenderTiming>();
    readonly Queue<string> captureQueue=new Queue<string>();
    readonly HashSet<string> suppressedCaptureNames=new HashSet<string>();
    readonly System.Diagnostics.Stopwatch wallClock=System.Diagnostics.Stopwatch.StartNew();
    ClientApp app;NativeEffectPlayer effects;NativePowerVisual powers;Presentation presentation;AssetRegistry assets;
    FireblastPcmTap tap;StateView initial,castState,endState;NativeDemonHunterHeroPowerPlayback.Pair pair;
    Report report;Source source=new Source();Unlock unlock=new Unlock();string role,folder,dataRoot,indexHash,failure;bool timingOnly;
    float started,peerDeadline,nextProgress,nextInputTrace,previousUpdateWallBoundary=-1f,updateWallBoundary=-1f,updateFrameWallSeconds;
    int initialActionCounter,hoverFrame,followupSubmitVersion,followupSubmitTurn;bool initialized,resolutionRequested,actionSent,endTurnSent,finalizing,finished;
    bool buttonCaptured,pointerPrepared,hoverCaptured,mouseDownSent,terminalStateCaptured,capturePumpRunning;Vector2 inputPoint;Rect inputRect;
    static string Argument(string name){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:"";}
    static string Required(string name){var value=Argument(name);if(String.IsNullOrWhiteSpace(value))throw new ArgumentException("Required argument missing: "+name);return value;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string selected=Argument("--h10-player-replay");if(selected=="")return;
        if(selected!="driver"&&selected!="observer")throw new ArgumentException("H10 role must be driver or observer");
        var replay=new GameObject("H10 dual Player source evidence").AddComponent<H10PlayerReplay>();
        replay.role=selected;replay.folder=Path.GetFullPath(Required("--h10-evidence-dir"));
        replay.dataRoot=Path.GetFullPath(Required("--data"));replay.timingOnly=Argument("--h10-timing-only")=="1";
        Directory.CreateDirectory(replay.folder);replay.started=Time.realtimeSinceStartup;
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
    void Check(bool value,string message){if(!value)throw new InvalidOperationException("H10 Player replay: "+message);}
    void OnEnable(){Application.logMessageReceived+=RuntimeLog;}
    void OnDisable(){Application.logMessageReceived-=RuntimeLog;}
    void RuntimeLog(string message,string trace,LogType type){if(failure==null&&(type==LogType.Exception||type==LogType.Error))failure=message;}
    void OnDestroy(){if(tap!=null)tap.StopCapture();}
    void OnApplicationQuit(){if(!finished)Finish("FAIL: Player quit before H10 evidence completed",1);}

    void Update()
    {
        if(finished){PollPeer();return;}
        SampleUpdateWallBoundary();
        try
        {
            if(failure!=null)throw new InvalidOperationException("Player runtime error: "+failure);
            if(Now>130)throw new TimeoutException("Bounded H10 replay exceeded 130 seconds");
            if(app==null)app=FindObjectOfType<ClientApp>();if(app==null)return;
            if(!EnsureResolution())return;
            var state=Field<StateView>("state");if(state==null)return;
            if(state.phase!="play")
            {
                if(initialized&&castState==null)
                {
                    if(!terminalStateCaptured)
                    {
                        TraceInput("state-left-play",state);Capture("state-left-play");terminalStateCaptured=true;
                        Finish("FAIL: match left play before the H10 action; "+InputStateSummary(),1);return;
                    }
                }
                return;
            }
            if(!initialized){if(!OpeningSpeechSettled())return;Initialize(state);return;}
            Check(state.match_id==initial.match_id,"client left the prepared H10 match");
            if(castState==null&&(Field<bool>("connectionLost")||Field<Connection>("connection")==null||
                !Field<Connection>("connection").Connected))
            {
                TraceInput("connection-lost-before-cast",state);
                Finish("FAIL: H10 driver lost its authority connection before the action; "+InputStateSummary(),1);return;
            }
            if(!buttonCaptured)CapturePowerButton(state);
            if(role=="driver"&&castState==null)
            {
                TrySendPower();
                if(castState==null&&Now>=nextInputTrace){TraceInput("waiting-for-authority",state);nextInputTrace=Now+1;}
            }
            if(castState==null&&state.version>initial.version&&TryBindCast(state))Capture("cast");
            if(castState==null)return;
            if(Now>=nextProgress){PublishProgress(state);nextProgress=Now+.5f;}
            ObserveSource(state);
            SampleUnlock(state);
            if(role=="driver"&&!endTurnSent)
            {
                var move=(state.moves??new Move[0]).FirstOrDefault(item=>item.kind=="end_turn");
                if(unlock.sampled&&move!=null)
                {
                    Check(state.seat==0&&state.active==0,"driver has no legal end_turn during H10 source playback");
                    Check(unlock.sourceActive&&effects.SourceBusy(SourceKey(castState)),"H10 source ended before the follow-up submission");
                    Check(!presentation.InputLocked&&!Field<bool>("pending"),"H10 follow-up submission was still input-locked");
                    float submitWall=(float)wallClock.Elapsed.TotalSeconds;
                    float latency=submitWall-unlock.powerAcceptedWallTime;
                    float frameAllowance=updateFrameWallSeconds;
                    float budget=.2f+frameAllowance;
                    unlock.sourceActiveAtSend=true;
                    unlock.sendTime=Now;unlock.sendLatency=latency;unlock.latencyBudget=budget;unlock.version=state.version;
                    unlock.submissionFrame=Time.frameCount;unlock.submissionWallTime=submitWall;
                    unlock.previousUpdateWallBoundary=previousUpdateWallBoundary;
                    unlock.submissionUpdateWallBoundary=updateWallBoundary;unlock.inputFrameAllowance=frameAllowance;
                    Check(unlock.submissionFrame==unlock.frame,
                        "H10 follow-up was not submitted in the first Update that observed the open input gate");
                    Check(latency>=0&&updateFrameWallSeconds>0&&submitWall>=updateWallBoundary&&
                          frameAllowance>=updateFrameWallSeconds&&latency<=budget+.001f,
                        "H10 SendMove exceeded the 0.2s input gate plus the measured monotonic Update frame: "+
                        latency.ToString("F3")+"s, frame="+updateFrameWallSeconds.ToString("F3")+"s");
                    followupSubmitVersion=state.version;followupSubmitTurn=state.turn;endTurnSent=true;Invoke("SendMove",move);
                    unlock.sendMoveSubmitted=Field<bool>("pending")&&Field<int>("actionCounter")==initialActionCounter+2;
                    unlock.actionCounterDelta=Field<int>("actionCounter")-initialActionCounter;
                    Check(unlock.sendMoveSubmitted,"ClientApp rejected the legal H10 end-turn SendMove");
                    Capture("end-turn-submitted-during-source");PublishProgress(state);return;
                }
            }
            if(endTurnSent&&!unlock.authorityAccepted&&state.version>followupSubmitVersion&&state.active==1&&state.turn>followupSubmitTurn)
            {unlock.authorityAccepted=true;unlock.acceptedVersion=state.version;unlock.activeAfter=state.active;unlock.authorityPublicHash=state.public_hash;}
            if(endState==null&&state.version>castState.version&&state.active==1&&state.turn>castState.turn)
            {
                if(!source.released||presentation.Busy||presentation.InputLocked||presentation.Display==null||presentation.Display.public_hash!=state.public_hash)return;
                endState=state;report.afterEndTurn=SnapshotOf(state);Capture("after-end-turn");PublishProgress();finalizing=true;
                peerDeadline=Time.realtimeSinceStartup+30;
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
        if(initialized&&castState!=null&&frameTimings.Count<3000)
            frameTimings.Add(new FrameTiming{frame=Time.frameCount,time=Now,wallMilliseconds=updateFrameWallSeconds*1000f});
    }
    bool EnsureResolution()
    {
        int width,height;if(!int.TryParse(Argument("-screen-width"),out width)||!int.TryParse(Argument("-screen-height"),out height))
            throw new InvalidOperationException("Explicit screen dimensions are required");
        if(Screen.width==width&&Screen.height==height)return true;
        if(!resolutionRequested){resolutionRequested=true;Screen.SetResolution(width,height,FullScreenMode.Windowed);return false;}
        if(Time.realtimeSinceStartup-started>15)throw new InvalidOperationException("Player resolution differs from requested dimensions");
        return false;
    }
    void Initialize(StateView state)
    {
        effects=Field<NativeEffectPlayer>("nativeEffects");powers=Field<NativePowerVisual>("powerVisual");
        presentation=Field<Presentation>("presentation");assets=Field<AssetRegistry>("assets");
        Check(effects!=null&&powers!=null&&presentation!=null&&assets!=null,"ClientApp H10 playback services are incomplete");
        Check(Path.GetFullPath(Argument("--data"))==dataRoot,"Player did not use the isolated --data root");
        indexHash=HashFile(Path.Combine(assets.Root,"native-effect-index.json"));
        var entry=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json"))).entries
            .Single(row=>row.card_id==PowerId);
        Check(entry.enabled&&Mathf.Abs(entry.duration-.1f)<.001f,"isolated runtime did not enable the exact H10 source profile");
        int listeners=FindObjectsOfType<AudioListener>().Count(item=>item.enabled);Check(listeners==1,"expected exactly one active AudioListener");
        tap=FindObjectsOfType<AudioListener>().Single(item=>item.enabled).gameObject.AddComponent<FireblastPcmTap>();
        tap.Begin(Path.Combine(folder,"scene-mix.wav"));
        var original=effects.AudioCue;effects.AudioCue=cue=>{RecordAudio(cue,"native-effect");original?.Invoke(cue);};
        var ui=powers.AudioCue;powers.AudioCue=cue=>{RecordAudio(cue,"power-ui");ui?.Invoke(cue);};
        var renderTiming=effects.DemonHunterTimingDiagnostic;
        effects.DemonHunterTimingDiagnostic=(key,stage,milliseconds)=>
        {
            if(renderTimings.Count<6000)renderTimings.Add(new RenderTiming{key=key,stage=stage,frame=Time.frameCount,time=Now,milliseconds=milliseconds});
            renderTiming?.Invoke(key,stage,milliseconds);
        };
        var instanceTiming=NativeEffectInstance.H10TimingDiagnostic;
        NativeEffectInstance.H10TimingDiagnostic=(owner,stage,milliseconds)=>
        {
            if(renderTimings.Count<6000)renderTimings.Add(new RenderTiming{key=owner,stage=stage,frame=Time.frameCount,time=Now,milliseconds=milliseconds});
            instanceTiming?.Invoke(owner,stage,milliseconds);
        };
        initial=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(state));
        initialActionCounter=Field<int>("actionCounter");
        report=new Report{role=role,dataRoot=dataRoot,indexSha256=indexHash,
            screenWidth=Screen.width,screenHeight=Screen.height,timingOnly=timingOnly,before=SnapshotOf(initial),sourceEvents=new EventRow[0],
            source=source,inputUnlockedAfterSource=unlock,readyMoves=(initial.moves??new Move[0]).Select(MoveOf).ToArray()};
        Capture("ready");TraceInput("ready-snapshot",initial);nextInputTrace=Now+1;
        Check(initial.seat==(role=="driver"?0:1),"unexpected Player seat for "+role);
        Check(initial.players!=null&&initial.players.Length==2&&initial.players[0].power.card_id==PowerId&&
              initial.players[1].power.card_id=="HERO_08bp","authority is not Demon Hunter versus Mage");
        Check(initial.active==0&&initial.players[0].mana==10&&initial.players[0].hero.attack==0,"ready snapshot is not the H10 baseline");
        bool legalPower=(initial.moves??new Move[0]).Any(move=>move.kind=="power"&&move.entity==initial.players[0].power.entity&&move.target==0);
        if(role=="driver")Check(legalPower,"driver ready snapshot lacks the real legal untargeted H10 move");
        else Check(!legalPower&&(initial.moves??new Move[0]).Length==0,"observer ready snapshot unexpectedly has an active-seat legal move");
        initialized=true;
    }
    void TrySendPower()
    {
        if(actionSent||role!="driver")return;
        if(mouseDownSent)
        {
            SetField("EvidencePressed",false);Invoke("HandleBattleInput",EventType.MouseUp,0,inputPoint);
            mouseDownSent=false;actionSent=true;TraceInput("mouse-up",Field<StateView>("state"),inputRect,inputPoint);Capture("power-clicked");return;
        }
        var power=initial.players[0].power;
        var state=Field<StateView>("state");var moves=state==null?new Move[0]:state.moves??new Move[0];
        if(!moves.Any(move=>move.kind=="power"&&move.entity==power.entity&&move.target==0))
        {TraceInput("power-move-not-legal",state);throw new InvalidOperationException("H10 is not a legal untargeted action; "+InputStateSummary());}
        var locations=Field<Dictionary<int,Rect>>("locations");Rect rect;
        if(!locations.TryGetValue(power.entity,out rect))
        {
            if(Now>=nextInputTrace){TraceInput("waiting-power-location",state);nextInputTrace=Now+1;}
            if(Now>20)throw new TimeoutException("H10 button was not rendered as a GUI target");
            return;
        }
        CapturePowerButton(state);
        var mulligan=Field<MulliganPresentation>("mulliganPresentation");
        bool inputReady=!Field<bool>("pending")&&!presentation.InputLocked&&
            (mulligan==null||!mulligan.Active)&&!Field<bool>("battleMenu");
        if(!inputReady)
        {
            if(Now>=nextInputTrace){TraceInput("waiting-input-unlock",state,rect,rect.center);nextInputTrace=Now+1;}
            if(Now>35)throw new TimeoutException("H10 input never became available; "+InputStateSummary());
            return;
        }
        var calibration=Field<UICalibration>("calibration");
        if(!pointerPrepared)
        {
            inputRect=rect;inputPoint=rect.center;pointerPrepared=true;SetField("EvidencePointer",(Vector2?)inputPoint);
            SetField("EvidencePressed",false);hoverFrame=Time.frameCount;TraceInput("pointer-on-power",state,inputRect,inputPoint);return;
        }
        if(!hoverCaptured)
        {
            if(Time.frameCount<=hoverFrame)return;
            hoverCaptured=true;hoverFrame=Time.frameCount;Capture("power-hover");
            TraceInput("power-hover",state,rect,inputPoint);return;
        }
        if(Time.frameCount<=hoverFrame)return;
        inputRect=rect;inputPoint=rect.center;SetField("EvidencePointer",(Vector2?)inputPoint);
        bool hit=calibration!=null&&calibration.Hit(power.entity,inputPoint);
        TraceInput("preflight",state,inputRect,inputPoint);
        Check(hit,"power target rect center misses the current UICalibration hit region");
        SetField("EvidencePressed",true);Invoke("HandleBattleInput",EventType.MouseDown,0,inputPoint);mouseDownSent=true;
        Capture("power-pressed");TraceInput("mouse-down",Field<StateView>("state"),inputRect,inputPoint);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialActionCounter+1,
            "real H10 GUI MouseDown submitted no action; "+InputStateSummary());
        hoverFrame=Time.frameCount;
    }
    static MoveRow MoveOf(Move move)
    {return new MoveRow{kind=move.kind,entity=move.entity,target=move.target,index=move.index,choice=move.choice};}
    void CapturePowerButton(StateView state)
    {
        if(buttonCaptured||state==null||state.players==null||state.players.Length==0)return;
        var locations=Field<Dictionary<int,Rect>>("locations");Rect rect;
        if(locations==null||!locations.TryGetValue(state.players[0].power.entity,out rect))return;
        buttonCaptured=true;Capture("power-button");TraceInput("power-button-visible",state,rect,rect.center);
    }
    string InputStateSummary()
    {
        var state=app==null?null:Field<StateView>("state");var connection=app==null?null:Field<Connection>("connection");
        Rect rect=Rect.zero;bool found=app!=null&&Field<Dictionary<int,Rect>>("locations").TryGetValue(initial.players[0].power.entity,out rect);
        if(!found)rect=Rect.zero;
        return "phase="+(state==null?"null":state.phase)+" v="+(state==null?0:state.version)+" rect="+rect+
            " seat="+(state==null?0:state.seat)+" active="+(state==null?0:state.active)+" turn="+(state==null?0:state.turn)+
            " moves="+(state==null||state.moves==null?0:state.moves.Length)+" point="+inputPoint+" pending="+(app!=null&&Field<bool>("pending"))+" actionCounter="+
            (app==null?0:Field<int>("actionCounter"))+" connected="+(connection!=null&&connection.Connected)+
            " connectionLost="+(app!=null&&Field<bool>("connectionLost"))+" status="+(app==null?"":Field<string>("status"));
    }
    void TraceInput(string stage,StateView snapshot=null,Rect? sampledRect=null,Vector2? sampledPoint=null)
    {
        if(report==null)return;
        var state=snapshot??(app==null?null:Field<StateView>("state"));
        var locations=app==null?null:Field<Dictionary<int,Rect>>("locations");Rect rect=Rect.zero;
        bool found=sampledRect.HasValue;
        if(found)rect=sampledRect.Value;
        else found=locations!=null&&initial!=null&&initial.players!=null&&locations.TryGetValue(initial.players[0].power.entity,out rect);
        if(!found)rect=Rect.zero;
        Vector2 point=sampledPoint.HasValue?sampledPoint.Value:found?rect.center:inputPoint;
        int hitEntity=0;var calibration=app==null?null:Field<UICalibration>("calibration");
        if(locations!=null&&calibration!=null&&found)
            hitEntity=locations.Where(row=>calibration.Hit(row.Key,point)).Select(row=>row.Key).LastOrDefault();
        var moves=state==null?new Move[0]:state.moves??new Move[0];
        bool legalPower=state!=null&&state.players!=null&&state.players.Length>0&&moves.Any(move=>
            move.kind=="power"&&move.entity==state.players[0].power.entity&&move.target==0);
        var connection=app==null?null:Field<Connection>("connection");
        var mulligan=app==null?null:Field<MulliganPresentation>("mulliganPresentation");
        bool mulliganActive=mulligan!=null&&mulligan.Active,battleMenu=app!=null&&Field<bool>("battleMenu");
        bool pending=app!=null&&Field<bool>("pending"),inputLocked=presentation!=null&&presentation.InputLocked;
        report.latest=state==null?report.latest:SnapshotOf(state);
        inputTrace.Add(new InputTrace{stage=stage,matchId=state==null?"":state.match_id,phase=state==null?"":state.phase,
            status=app==null?"":Field<string>("status"),displayHash=presentation==null||presentation.Display==null?"":presentation.Display.public_hash,
            seat=state==null?0:state.seat,active=state==null?0:state.active,turn=state==null?0:state.turn,version=state==null?0:state.version,
            frame=Time.frameCount,time=Now,actionCounter=app==null?0:Field<int>("actionCounter"),
            actionCounterDelta=app==null?0:Field<int>("actionCounter")-initialActionCounter,
            selected=app==null?0:Field<int>("selected"),dragged=app==null?0:Field<int>("dragged"),hitEntity=hitEntity,
            moveCount=moves.Length,pending=pending,connected=connection!=null&&connection.Connected,
            connectionLost=app!=null&&Field<bool>("connectionLost"),busy=presentation!=null&&presentation.Busy,
            inputLocked=inputLocked,mulliganActive=mulliganActive,battleMenu=battleMenu,
            inputReady=!pending&&!inputLocked&&!mulliganActive&&!battleMenu,locationFound=found,
            hitRegion=calibration!=null&&found&&calibration.Hit(initial.players[0].power.entity,point),
            h10PowerMoveLegal=legalPower,powerRect=rect,point=point,moves=moves.Select(MoveOf).ToArray()});
        if(inputTrace.Count>400)inputTrace.RemoveAt(0);
        report.inputTrace=inputTrace.ToArray();WriteReport();
    }
    bool TryBindCast(StateView state)
    {
        var powers=(state.events??new SemanticEvent[0]).Where(item=>item.type=="PlayHeroPower"&&item.target==initial.players[0].power.entity).ToArray();
        if(powers.Length==0){if(Now>30)throw new TimeoutException("authority received no real H10 click");return false;}
        Check(powers.Length==1,"authority emitted duplicate H10 PlayHeroPower events");
        var before=Field<StateView>("previous");Check(before!=null&&before.version==initial.version,"H10 event did not follow captured ready state");
        Check(NativeDemonHunterHeroPowerPlayback.TryGetPair(powers[0],before,state,out pair),
              "authority event batch did not pair to the same caster power and self-hero Buff");
        Check(before.players[0].mana-state.players[0].mana==1&&before.players[0].hero.attack==0&&state.players[0].hero.attack==1&&
              state.players[0].power.used,"authoritative H10 mana, power use, or 0 -> 1 attack state differs");
        castState=state;unlock.powerAcceptedTime=Now;unlock.powerAcceptedWallTime=(float)wallClock.Elapsed.TotalSeconds;
        unlock.castAcceptedFrame=Time.frameCount;report.cast=SnapshotOf(state);report.sourceEvents=state.events.Select(item=>new EventRow{type=item.type,cardId=item.card_id,
            source=item.source,target=item.target,owner=item.owner}).ToArray();
        Check(report.cast.publicHash==state.public_hash,"H10 cast hash capture failed");
        Capture("self-hero-buff");PublishProgress();return true;
    }
    string SourceKey(StateView state)
    {return state.match_id+":"+state.version+":"+initial.players[0].power.entity+":0";}
    void ObserveSource(StateView state)
    {
        if(castState!=null&&presentation.Display!=null&&presentation.Display.public_hash==castState.public_hash)
            report.cast.displayHash=presentation.Display.public_hash;
        string key=SourceKey(castState);var spell=effects.SourceSpell(key);
        if(spell!=null)
        {
            source.seen=true;source.hasOriginalHistory=spell.HasOriginalHistory;source.firstArrival=spell.Flow.FirstArrivalTime.HasValue;
            source.activeClones=spell.Flow.ActiveClones;if(source.startTime==0)source.startTime=Now;
            var effect=spell.Effect;source.rootReference=effect.Program.reference;
            source.drawRect=spell.DrawRect;source.targetDrawRect=spell.TargetDrawRect;source.positionDiagnostics=spell.PositionDiagnostics();
            var graphActions=spell.SourceGraphActions();
            source.actions=graphActions.OrderBy(row=>row.Key).Select(row=>new ActionRow{name=row.Key,count=row.Value}).ToArray();
            graphActions.TryGetValue("AudioPlayClipAction",out source.buttonActions);graphActions.TryGetValue("AudioPlaythroughAction",out source.playthroughActions);
            source.rootActions=graphActions.Values.Sum();source.impactActions=spell.ActionExecutions.Values.Sum();
            source.unboundAudioActions=effect.UnboundAudioActions;source.unboundAnimationActions=effect.UnboundAnimationActions;
            source.unboundMachineOwnerActions=effect.UnboundMachineOwnerActions;source.unboundTimelineActions=effect.UnboundTimelineActions;
            source.unboundTimelineSounds=effect.UnboundTimelineSounds;source.effectUnhandledActions=effect.UnhandledActions;
            source.spellUnhandledActions=spell.UnhandledActions;source.spellUnboundAudioActions=spell.UnboundAudioActions;
            source.spellUnboundControllerActions=spell.UnboundControllerActions;source.spellUnboundComponentActions=spell.UnboundComponentActions;
            source.spellUnboundControllerDetails=spell.UnboundControllerDetails.ToArray();
            source.unboundActions=effect.UnboundAudioActions+effect.UnboundAnimationActions+effect.UnboundMachineOwnerActions+
                effect.UnboundTimelineActions+effect.UnboundTimelineSounds+effect.UnhandledActions+spell.UnhandledActions+
                spell.UnboundAudioActions+spell.UnboundControllerActions+spell.UnboundComponentActions;
            if(!HasCaptureRequest("source-active"))Capture("source-active");
            if(spell.Time>=.35f&&!HasCaptureRequest("reference-target-peak"))Capture("reference-target-peak");
            if(spell.Time>=.75f&&!HasCaptureRequest("reference-source-tail"))Capture("reference-source-tail");
            Check(!source.hasOriginalHistory&&!source.firstArrival,"H10 route fabricated History/metadata or a missile Arrival");
            if(!effects.SourceBusy(key))
            {source.released=true;source.releaseTime=Now;source.duration=source.releaseTime-source.startTime;Capture("source-released");if(unlock.sampled)PublishProgress(state);}
            return;
        }
        TrySendPower();
        if(source.seen&&!source.released)
        {source.released=true;source.releaseTime=Now;source.duration=source.releaseTime-source.startTime;Capture("source-released");if(unlock.sampled)PublishProgress(state);}
        else if(!source.seen&&Now>35)throw new TimeoutException("strict H10 native source route never became active");
    }
    static int Count(NativeEffectInstance effect,string action)
    {int value;return effect.ActionExecutions.TryGetValue(action,out value)?value:0;}
    Snapshot SnapshotOf(StateView state)
    {
        var own=state.players[0];return new Snapshot{publicHash=state.public_hash,displayHash=presentation==null?state.public_hash:
            presentation.Display==null?"":presentation.Display.public_hash,seat=state.seat,active=state.active,turn=state.turn,
            mana=own.mana,heroAttack=own.hero.attack,powerEntity=own.power.entity,heroEntity=own.hero.entity,powerUsed=own.power.used};
    }
    void SampleUnlock(StateView state)
    {
        if(unlock.sampled||!source.seen||source.released||Field<bool>("pending")||presentation.InputLocked)return;
        string key=SourceKey(castState);bool sourceActive=effects.SourceBusy(key);if(!sourceActive)return;
        var move=(state.moves??new Move[0]).FirstOrDefault(item=>item.kind=="end_turn");
        bool displayMatched=presentation.Display!=null&&presentation.Display.public_hash==state.public_hash;
        unlock.sampled=true;unlock.sourceReleased=source.released;unlock.sourceActive=sourceActive;unlock.busy=presentation.Busy;unlock.inputLocked=presentation.InputLocked;
        unlock.pending=Field<bool>("pending");unlock.legalEndTurn=move!=null;unlock.displayMatched=displayMatched;unlock.seat=state.seat;
        unlock.active=state.active;unlock.frame=Time.frameCount;unlock.time=Now;
        if(role=="driver")Check(state.seat==0&&state.active==0&&move!=null,"driver did not unlock with legal end_turn while the H10 source remained active");
        else Check(state.seat==1&&state.active==0&&move==null,"observer has an unexpected legal end_turn while the H10 source remained active");
        Capture("input-unlocked-during-source");PublishProgress(state);
    }
    void RecordAudio(NativeAudioCue cue,string kind)
    {if(cue!=null)audio.Add(new AudioRecord{file=cue.file,kind=kind,time=Now,cueTime=cue.time,frame=Time.frameCount,
        playing=cue.playbackActive!=null&&cue.playbackActive()});}
    bool HasCaptureRequest(string name)
    {return timingOnly?suppressedCaptureNames.Contains(name):images.Contains(name+".png");}
    void Capture(string name)
    {
        if(timingOnly){suppressedCaptureNames.Add(name);return;}
        string file=name+".png";if(!images.Add(file))return;captureQueue.Enqueue(file);
        if(!capturePumpRunning){capturePumpRunning=true;StartCoroutine(DrainCaptures());}
    }
    string PeerProgressPath{get{return Path.Combine(Directory.GetParent(folder).FullName,role=="driver"?"observer":"driver","progress.json");}}
    Progress ReadPeerProgress()
    {
        if(!File.Exists(PeerProgressPath))return null;
        try
        {
            using(var stream=new FileStream(PeerProgressPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream))return JsonUtility.FromJson<Progress>(reader.ReadToEnd());
        }
        catch(IOException){return null;}
    }
    static void WriteEvidenceJson(string path,string json)
    {
        string temporary=path+".tmp";File.WriteAllText(temporary,json);
        // Peer readers retain their complete old snapshot while the path is atomically replaced.
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
    }
    void PublishProgress(StateView state=null)
    {
        if(report==null)return;if(state==null)state=app==null?null:Field<StateView>("state");
        var value=new Progress{role=role,castHash=report.cast==null?"":report.cast.publicHash,endHash=report.afterEndTurn==null?"":report.afterEndTurn.publicHash,
            seat=state==null?0:state.seat,active=state==null?0:state.active,sourceReleased=source.released,unlocked=unlock.sampled};
        WriteEvidenceJson(Path.Combine(folder,"progress.json"),JsonUtility.ToJson(value,true));
    }
    void PollPeer()
    {
        if(!finalizing||finished)return;if(Time.realtimeSinceStartup>peerDeadline){Finish("FAIL: peer H10 hashes did not converge",1);return;}
        try
        {
            var peer=ReadPeerProgress();if(peer==null)return;
            if(String.IsNullOrEmpty(peer.castHash))return;
            Check(peer.castHash==report.cast.publicHash,"peer accepted cast authority hash differs");
            if(!peer.sourceReleased||!peer.unlocked||String.IsNullOrEmpty(peer.endHash))return;
            Check(peer.endHash==report.afterEndTurn.publicHash,"post-end-turn authority hashes differ");
            report.cast.peerPublicHash=peer.castHash;report.afterEndTurn.peerPublicHash=peer.endHash;
            tap.StopCapture();report.dsp=new Dsp{listenerCount=FindObjectsOfType<AudioListener>().Count(item=>item.enabled),rate=tap.Rate,
                channels=tap.Channels,dspBuffer=tap.DspBuffer,dspBuffers=tap.DspBuffers,callbacks=tap.Callbacks,overflows=tap.Overflows,
                sampleFrames=tap.SampleFrames,clippedSamples=tap.ClippedSamples,peak=tap.Peak,rms=tap.Rms};report.source=source;
            report.source.audio=audio.ToArray();report.audio=audio.ToArray();report.inputUnlockedAfterSource=unlock;
            Check(source.seen&&source.released&&!source.hasOriginalHistory&&!source.firstArrival&&source.buttonActions>0&&
                  source.playthroughActions>0&&source.unboundActions==0,"H10 source action/audio/metadata counters failed; unbound="+
                  source.unboundActions+" controller="+String.Join(";",source.spellUnboundControllerDetails??new string[0]));
            Check(audio.Any(item=>item.kind=="native-effect"),"H10 native source emitted no audio cue to the mixer");
            Check(unlock.sampled&&unlock.sourceActive&&!unlock.sourceReleased&&!unlock.inputLocked&&!unlock.pending,
                  "input did not unlock while the H10 source was active");
            Check(role!="driver"||unlock.sendMoveSubmitted&&unlock.sourceActiveAtSend&&unlock.authorityAccepted&&unlock.actionCounterDelta==2&&
                  unlock.acceptedVersion>unlock.version&&unlock.activeAfter==1&&!String.IsNullOrEmpty(unlock.authorityPublicHash)&&
                  unlock.sendLatency<=unlock.latencyBudget,
                  "driver H10 follow-up was not submitted and authority-accepted while the source was active");
            Check(report.dsp.listenerCount==1&&report.dsp.channels==2&&report.dsp.sampleFrames>0&&report.dsp.callbacks>0&&
                  report.dsp.overflows==0&&report.dsp.peak>.00001f,"Unity Listener PCM capture was empty or overflowed");
            report.result="PASS H10 authoritative dual Player source/DSP/natural turn";WriteReport();finished=true;StartCoroutine(QuitAfterCaptures(0));
        }
        catch(Exception error){Finish("FAIL: "+error.Message,1);}
    }
    void Finish(string result,int code)
    {
        if(finished)return;finished=true;Environment.ExitCode=code;if(tap!=null)tap.StopCapture();
        if(app!=null)try{SetField("EvidencePressed",false);if(report!=null)TraceInput("finish");}catch{}
        if(report==null)report=new Report{role=role,dataRoot=dataRoot,timingOnly=timingOnly};report.result=result;report.role=role;report.dataRoot=dataRoot;
        report.indexSha256=indexHash;report.source=source;report.source.audio=audio.ToArray();report.audio=audio.ToArray();report.inputUnlockedAfterSource=unlock;
        report.images=images.OrderBy(value=>value).ToArray();WriteReport();Debug.LogError("H10 PLAYER FORMAL "+result);StartCoroutine(QuitAfterCaptures(code));
    }
    IEnumerator QuitAfterCaptures(int code)
    {while(capturePumpRunning||captureQueue.Count>0)yield return null;WriteReport();Application.Quit(code);}
    void WriteReport()
    {
        if(report==null||String.IsNullOrEmpty(folder))return;report.dataRoot=dataRoot;report.indexSha256=indexHash;
        source.frameTimings=frameTimings.ToArray();source.renderTimings=renderTimings.ToArray();report.source=source;
        report.screenWidth=Screen.width;report.screenHeight=Screen.height;report.images=images.OrderBy(value=>value).ToArray();
        report.suppressedCaptures=suppressedCaptureNames.OrderBy(value=>value).ToArray();
        WriteEvidenceJson(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));
    }
    IEnumerator DrainCaptures()
    {
        while(captureQueue.Count>0)
        {
            string file=captureQueue.Dequeue();yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,file));yield return null;
        }
        capturePumpRunning=false;
    }
    static string HashFile(string path)
    {using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
}
