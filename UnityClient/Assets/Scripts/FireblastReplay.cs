using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

// Captures source-declared values held by a live Hunter renderer for final-draw replay.
[Serializable] public sealed class HunterRuntimeTextureSnapshot
{
    public string name,path,runtimeName;
    public int width,height;
    public bool isNull;
    public Vector2 scale,offset;
}
[Serializable] public sealed class HunterRuntimeMaterialSnapshot
{
    public string name,shader;
    public bool sourceBound;
    public NativeFloat[] floats;
    public NativeColor[] colors;
    public HunterRuntimeTextureSnapshot[] textures;
    public string[] unboundProperties;

    public static HunterRuntimeMaterialSnapshot Capture(Material material,NativeMaterial source)
    {
        if(material==null)throw new ArgumentNullException(nameof(material));
        var missing=new List<string>();
        var snapshot=new HunterRuntimeMaterialSnapshot{name=material.name,shader=material.shader==null?"":material.shader.name,
            sourceBound=source!=null,floats=new NativeFloat[0],colors=new NativeColor[0],textures=new HunterRuntimeTextureSnapshot[0]};
        if(source==null){snapshot.unboundProperties=new[]{"<source material mapping>"};return snapshot;}
        snapshot.floats=(source.floats??new NativeFloat[0]).Where(value=>
        {
            if(value!=null&&!String.IsNullOrEmpty(value.name)&&material.HasProperty(value.name))return true;
            missing.Add(value==null?"<null float>":value.name);return false;
        }).Select(value=>new NativeFloat{name=value.name,value=material.GetFloat(value.name)}).ToArray();
        snapshot.colors=(source.colors??new NativeColor[0]).Where(value=>
        {
            if(value!=null&&!String.IsNullOrEmpty(value.name)&&material.HasProperty(value.name))return true;
            missing.Add(value==null?"<null color>":value.name);return false;
        }).Select(value=>new NativeColor{name=value.name,value=material.GetColor(value.name)}).ToArray();
        snapshot.textures=(source.textures??new NativeTexture[0]).Where(value=>
        {
            if(value!=null&&!String.IsNullOrEmpty(value.name)&&material.HasProperty(value.name))return true;
            missing.Add(value==null?"<null texture>":value.name);return false;
        }).Select(value=>
        {
            var texture=material.GetTexture(value.name);
            return new HunterRuntimeTextureSnapshot{name=value.name,path=value.path,runtimeName=texture==null?"":texture.name,
                width=texture==null?0:texture.width,height=texture==null?0:texture.height,isNull=texture==null,
                scale=material.GetTextureScale(value.name),offset=material.GetTextureOffset(value.name)};
        }).ToArray();
        snapshot.unboundProperties=missing.ToArray();return snapshot;
    }

    public bool Apply(Material material,AssetRegistry assets,out string error)
    {
        error="";
        if(!sourceBound){error="runtime source material mapping is missing";return false;}
        if(unboundProperties!=null&&unboundProperties.Length>0){error="runtime source properties are unbound: "+String.Join(",",unboundProperties);return false;}
        if(material==null||material.name!=name||material.shader==null||material.shader.name!=shader)
        {error="material identity changed: expected "+name+" / "+shader;return false;}
        foreach(var value in floats??new NativeFloat[0])
        {
            if(value==null||!material.HasProperty(value.name)){error="float property is unavailable: "+(value==null?"<null>":value.name);return false;}
            material.SetFloat(value.name,value.value);
        }
        foreach(var value in colors??new NativeColor[0])
        {
            if(value==null||!material.HasProperty(value.name)){error="color property is unavailable: "+(value==null?"<null>":value.name);return false;}
            material.SetColor(value.name,value.value);
        }
        foreach(var value in textures??new HunterRuntimeTextureSnapshot[0])
        {
            if(value==null||!material.HasProperty(value.name)){error="texture property is unavailable: "+(value==null?"<null>":value.name);return false;}
            Texture texture=null;
            if(!value.isNull)
            {
                if(String.IsNullOrEmpty(value.path)||assets==null){error="runtime texture has no resolvable source path: "+value.name;return false;}
                texture=assets.FireblastTextureFile(value.path);
                if(texture==null||texture.width!=value.width||texture.height!=value.height)
                {error="runtime texture source does not match captured dimensions: "+value.name;return false;}
            }
            material.SetTexture(value.name,texture);material.SetTextureScale(value.name,value.scale);material.SetTextureOffset(value.name,value.offset);
        }
        return true;
    }
}
[Serializable] public sealed class HunterActionCount{public string name;public int count;}

// 仅由显式诊断参数启动，重放正式输入方法和真实TCP对局，不控制桌面。
[DefaultExecutionOrder(1000)]
public sealed class FireblastReplay:MonoBehaviour
{
    [Serializable] sealed class Record{public string phase,message,file,audioSource,serverStatus;public float time,volume,pitch,spatial,sourceTime,sourceClock,frameDelta,speed;public bool loop,playing,paused,pending,hasSelection,targetingCancelled,selectionBusy,flowCompleted,flowCancelled;public int version,actionCount,voices,activeClones,sourcePending;public bool positionKnown,voiceFound,voiceMute;public Vector3 cuePosition,voicePosition,listenerPosition;public float voiceVolume,voiceMinDistance,voiceMaxDistance,listenerDistance;}
    [Serializable] sealed class RogueRules{public string powerCardId,weaponCardId,publicHash;public int seat,powerEntity,heroEntity,heroHealth,heroAttack,mana,maxMana,weaponEntity,weaponAttack,weaponDurability;public bool powerUsed,legalPowerMove,legalEndTurn;}
    [Serializable] sealed class RogueWeaponAttack{public int attackIndex,frame,version,heroEntity,targetEntity,targetHealthBefore,targetHealth,weaponEntity,weaponAttack,weaponDurability,attackEvents,targetHitEvents,weaponHitEvents,weaponDeathEvents,slot7Events,slot5Events;public string targetCardId,publicHash,displayHash;public bool targetDeath,inputUnlocked,hashMatched,legalNextAttack,legalEndTurn;}
    [Serializable] sealed class RogueLifecyclePoint{public string stage,phase,publicHash,displayHash,result;public int frame,version,turn,activePlayer,actionCounter,queueCount,weaponEntity,weaponDurability;public float time,batchTime,nativeTime;public bool pending,busy,inputLocked,nativeBusy,sourcePresent,resultReady,legalPowerMove,legalEndTurn,endTurnReady,endTurnGreen,onlyEndTurn,hashMatched;}
    [Serializable] sealed class RogueWeaponLifecycleEvidence{public string role,finalHash;public int replacementSubmitVersion,replacementAcceptedVersion,endTurnAcceptedVersion,concedeAcceptedVersion,emptySlotEquipVersion,occupiedReplacementVersion;public bool replacementSubmitted,replacementAccepted,replacementSourceSeen,replacementSourceReleased,replacementInputUnlocked,endTurnSubmitted,endTurnAccepted,concedeSubmitted,concessionSynchronized,resultReadyOnBothPeers;public RogueLifecyclePoint[] points;}
    [Serializable] sealed class WarriorRules{public string powerCardId,publicHash;public int seat,version,powerEntity,heroEntity,heroHealth,heroArmor,mana,maxMana;public bool powerUsed,legalPowerMove,legalEndTurn;}
    [Serializable] sealed class DruidRules{public string powerCardId,publicHash;public int seat,powerEntity,heroEntity,heroHealth,heroAttack,heroArmor,mana,maxMana;public bool powerUsed,legalPowerMove,legalEndTurn;}
    [Serializable] sealed class WarlockRules{public string powerCardId,publicHash;public int seat,powerEntity,heroEntity,heroHealth,mana,maxMana,handCount,deckCount;public bool powerUsed,legalPowerMove,legalEndTurn;}
    [Serializable] sealed class PaladinRules{public string powerCardId,publicHash,recruitCardId;public int seat,powerEntity,mana,maxMana,recruitCount,recruitEntity,recruitAttack,recruitHealth;public bool powerUsed,legalPowerMove,legalEndTurn;}
    [Serializable] sealed class WarlockEvidence{public string result,role,publicHash,displayHash,heroPower;public int checks,width,height,rate,channels,dspBuffer,dspBuffers,callbacks,overflows,listenerCount,warlockSpendManaCount,warlockPlayHeroPowerCount,warlockHitCount,warlockDrawCount,warlockSourceCueCount,warlockSourceWaitActionCount,warlockSourceSpellStateActionCount,warlockSourceAudioActionCount,warlockSourceAudioPlayCount,warlockUnhandledActions,warlockUnboundAudioActions,warlockMissileEventCount,warlockArrivalEventCount,warlockInputUnlockSampleFrame;public long sampleFrames,clippedSamples;public float peak,rms,warlockInputUnlockSampleTime;public bool warlockEventOrderValid,warlockSourceCueSemanticsValid,warlockSourceRootCompleted,warlockSourceRootReleased,warlockInputUnlockedAfterSource;public WarlockRules warlockBefore,warlockAfter;public Record[] warlockSourceAudioRecords;}
    [Serializable] sealed class PaladinEvidence{public string result,role,publicHash,displayHash,heroPower,paladinAreaReference,paladinNextTurnPublicHash,paladinNextTurnDisplayHash;public int checks,width,height,rate,channels,dspBuffer,dspBuffers,callbacks,overflows,listenerCount,paladinSpendManaCount,paladinPlayHeroPowerCount,paladinSummonCount,paladinRecruitSummonCount,paladinSourceCueCount,paladinAreaCloneCreations,paladinAreaFinishedNotifications,paladinAreaPending,paladinAudioActionCount,paladinAudioPlayCount,paladinUnhandledActions,paladinUnboundAudioActions,paladinInputUnlockSampleFrame,paladinTurnSubmitVersion,paladinTailSubmitFrame,paladinNextTurnVersion,paladinNextTurnAppliedFrame,paladinNextTurnFrame,paladinTailReleaseFrame;public long sampleFrames,clippedSamples;public float peak,rms,paladinInputUnlockSampleTime;public bool paladinEventOrderValid,paladinSourceCueSemanticsValid,paladinFriendlyAreaOnlyReconstruction,paladinParentFlowConstructed,paladinAreaCompleted,paladinAreaReleased,paladinInputUnlockedAfterArea,paladinTailEndTurnSubmitted,paladinNextTurnAppliedDuringTail,paladinInputOpenDuringNextTurnTail,paladinTailReleasedAfterNextTurn;public PaladinRules paladinBefore,paladinAfter;public Record[] paladinSourceAudioRecords;}
    [Serializable] sealed class ShamanEvidence{public string result,role,publicHash,displayHash,heroPower,totemCardId,diagnostics;public int checks,width,height,rate,channels,dspBuffer,dspBuffers,callbacks,overflows,listenerCount,spendManaCount,playHeroPowerCount,summonCount,totemEntity,nativeActiveCount,runtimeFailures,sourceAudioPlayCount,inputUnlockSampleFrame;public long sampleFrames,clippedSamples;public float inputUnlockSampleTime,inputUnlockBatchTime;public bool eventOrderValid,eventLinkValid,nativeActorVisible,actorVisualHiddenDuringSource,actorVisualRestoredAfterSource,inputUnlockedDuringSource,displayHashMatched;public Record[] sourceAudioRecords;}
    [Serializable] sealed class WarriorFollowup
    {
        public bool inputUnlockedDuringSource,sendMoveSubmitted,authorityAccepted,sourceActiveAtSend,sourceTailCleanedAfterFollowup;
        public int sampleFrame,submitVersion,acceptedVersion,actionCounterDelta,activeAfter,castAuthorityVersion;
        public float powerAcceptedTime,inputUnlockSampleTime,sendTime,sendLatency,latencyBudget;
        public string publicHash,displayHash;
        public SemanticEvent[] castAuthorityEvents;
    }
    [Serializable] sealed class Report{public string result,role,publicHash,displayHash,heroPower,diffuseLightingInputs,priestMode,priestNativeEvents,rogueObserverShamanTotemSequence;public int checks,width,height,rate,channels,dspBuffer,dspBuffers,callbacks,overflows,listenerCount,targetEntity,initialTargetHealth,finalTargetHealth,healEventCount,healCallbacks,missileEventCount,arrivalEventCount,sourceAudioPlayCount,sourceAudioStopCount,inputUnlockSampleFrame,priestTargetEventCount,priestImpactTargetEntity,priestDamageDecorations;public long sampleFrames,clippedSamples;public float peak,rms,inputUnlockSampleTime,inputUnlockBatchTime,priestTargetGuiX,priestTargetGuiY,priestTargetGuiPositionError;public bool inputUnlockedAfterSource,priestImpactVisualCaptured,priestTargetGuiPositionValid;public int roguePlayHeroPowerCount,rogueSummonCount,rogueDaggerSummonCount,rogueSummonTarget,rogueUnexpectedHeroCueCount,rogueSourceCueCount,rogueSourceRootNodeCount,rogueSourceStateActions,rogueInputUnlockSampleFrame,rogueSourceAudioPlayCount,rogueWeaponPowerUseCount,rogueObserverShamanPowerCount,rogueWeaponReplacementCount,rogueWeaponEmptySlotEquipVersion,rogueWeaponOccupiedReplacementVersion;public float rogueInputUnlockSampleTime;public bool rogueSourceCueSemanticsValid,rogueSourceOnly,rogueParentFlowConstructed,rogueSourceRootReleased,rogueInputUnlockedAfterSource,rogueWeaponsMode,rogueWeaponsShamanMode,rogueWeaponEndTurnGreenAfterOccupiedReplacement,rogueWeaponOnlyEndTurnAfterOccupiedReplacement,rogueWeaponResultReadyOnBothPeers;public string rogueDisplayHash;public RogueRules rogueBefore,rogueAfter;public int rogueWeaponSlot7Events,rogueWeaponSlot5Events,rogueWeaponSlot7AudioPlayCount,rogueWeaponSlot5AudioPlayCount,rogueWeaponInputUnlockSampleFrame;public bool rogueWeaponLifecycleComplete,rogueWeaponInputUnlockedAfterAttack;public string rogueWeaponDisplayHash;public RogueWeaponAttack[] rogueWeaponAttacks;public Record[] rogueWeaponAudioRecords;public int warriorPlayHeroPowerCount,warriorGainArmorCount,warriorSourceCueCount,warriorUnexpectedHitCueCount,warriorMissileEventCount,warriorArrivalEventCount,warriorCameraShakerStarts,warriorParentCameraShakerStarts,warriorImpactCloneCameraShakerStarts,warriorUnhandledActions,warriorParentUnhandledActions,warriorImpactCloneUnhandledActions,warriorUnboundAudioActions,warriorInputUnlockSampleFrame,warriorSourceAudioPlayCount;public string[] warriorParentUnhandledActionDetails,warriorImpactCloneUnhandledActionDetails;public bool warriorEventOrderValid,warriorSourceCueSemanticsValid,warriorSourceRootCompleted,warriorSourceRootReleased,warriorInputUnlockedAfterSource;public float warriorInputUnlockSampleTime;public WarriorRules warriorBefore,warriorAfter;public WarriorFollowup warriorFollowup;public int druidSpendManaCount,druidPlayHeroPowerCount,druidBuffCount,druidGainArmorCount,druidHitEventCount,druidUnexpectedHeroCueCount,druidMissileEventCount,druidArrivalEventCount,druidSourceCueCount,druidSourceStartActionCount,druidImpactCloneCount,druidImpactFinishedNotificationCount,druidSourceAnimationActionCount,druidSourceParticleActionCount,druidSourceAudioActionCount,druidSourceAudioPlayCount,druidUnhandledActions,druidUnboundAudioActions,druidMissingAudioPlaybackServices,druidInputUnlockSampleFrame;public bool druidEventOrderValid,druidSourceCueSemanticsValid,druidCastRequested,druidCastCompleted,druidFirstArrivalTimeMissing,druidSourceRootCompleted,druidSourceRootReleased,druidInputUnlockedAfterSource;public float druidInputUnlockSampleTime;public string druidDisplayHash;public DruidRules druidBefore,druidAfter;public Record[] druidSourceAudioRecords,records;}
    [Serializable] sealed class Frame{public int frame,version,target,activePlayer,authorityHealth,displayHealth,authorityArmor,displayArmor,authorityAttack,displayAttack,hitCallbacks,healCallbacks,healEvents,damageDecorations,actionCounter,actionCounterDelta;public float time,batchTime,hitStart,hitDuration,tint,motion,shake,speed,captureDelta,lateDelta,followupSendLatency;public Vector3 frameDeltas;public bool ready,applied,busy,inputLocked,arrival,sourceCompleted,sourceReleased,sourceTailActive,turnStart,hashMatched,endTurnAvailable,sourceActive,followupSent,followupAccepted,followupTailCleaned;public string publicHash,displayHash,nativeEvents;}
    [Serializable] sealed class Timeline{public Frame[] frames;}
    [Serializable] sealed class FlightVisual
    {
        public string owner,component,materials,shaderStatus;
        public bool activeSelf,activeInHierarchy,enabled,isVisible,hasBounds,particleAlive,particlePlaying,particleEmitting,trailEmitting,trailMeshBaked;
        public int particleCount,trailPositionCount,trailMeshVertices,trailMeshTriangles,layer;
        public float particleTime,trailTime,trailWidth,trailVertexAlphaMin,trailVertexAlphaMax;
        public Vector3 worldPosition,screenPosition,sourceViewport,boundsCenter,boundsSize,trailColorMin,trailColorMax;
        public Vector2 trailUvMin,trailUvMax;
        public Vector3[] trailWorldPoints,trailViewportPoints;public string sourceMaterials;
        public HunterRuntimeMaterialSnapshot[] materialState;
    }
    [Serializable] sealed class FlightDiagnostic
    {
        public string stage,nodeSummary,captureCamera,eventKey;public int frame,eventType,nodeCount,rootChildCount,cameraCullingMask,sampleIndex,targetAgeMs;public float replayTime,flowTime,firedAt,duration,elapsed,cameraOrthographicSize,arrivalAge,arrivalTime;
        public bool reached,rootActive,cameraOrthographic;public int sourceState,sourceStateChanges,propertyWrites,unhandledActions;
        public Vector3 movementPosition,targetPosition,targetViewport;public FlightVisual[] visuals;public HunterActionCount[] actionExecutions;public string[] unhandledActionDetails;
    }
    [Serializable] sealed class PixelStats
    {
        public int width,height,alphaPixels,colorPixels,minAlphaX,minAlphaY,maxAlphaX,maxAlphaY,minColorX,minColorY,maxColorX,maxColorY,requestFrame,callbackFrame;
        public float sourceArrivalAge,callbackArrivalAge;public bool readbackSucceeded;public string readbackError;
        [NonSerialized] public float[] rawPixels;
        public float maxAlpha,maxRgb;
    }
    [Serializable] sealed class HunterImpactDiagnostic
    {
        public string stage,eventKey,image;public int frame,eventType,targetParticleAgeMs;
        public float arrivalAge,cloneAge,particleAge,targetAgeErrorMs,orthographicSize,intensity;
        public Vector3 planeScale;public PixelStats pixels;
        public bool done,written;
    }
    [Serializable] sealed class HunterImpactSampleMiss
    {public int targetParticleAgeMs,frame;public float observedParticleAge;public string reason;}
    [Serializable] sealed class HunterImpactSampleSummary
    {
        public int[] expectedTargetParticleAgesMs,capturedTargetParticleAgesMs,missingTargetParticleAgesMs;
        public int capturedCount,lastObservedParticleCount,toleranceMs;public float lastObservedParticleAge;
        public bool complete;public string lastUnavailableReason;public HunterImpactDiagnostic[] samples;public HunterImpactSampleMiss[] misses;
    }
    [Serializable] sealed class RenderDiagnostic
    {
        public string stage,cardId,eventKey,error;public int frame,eventType,screenWidth,screenHeight,sampleIndex,targetAgeMs,pendingReadbacks;public float sampleTime,flowTime,arrivalAge,arrivalTime,targetAgeErrorMs;public bool readbacksComplete,readbackSucceeded;public PixelStats emission,transmission;
        [NonSerialized] public bool written;
    }
    [Serializable] sealed class SourceDrawDiagnostic
    {
        public string stage,cardId,eventKey;public int frame,eventType,screenWidth,screenHeight,sampleIndex,targetAgeMs;public Vector4 logicalRect,screenRect,trailRoi;public float[] guiMatrix;public float sampleTime,effectAge,flowTime,castOffset,arrivalAge,arrivalTime;public bool passesBound,trailRoiAvailable;
    }
    [Serializable] sealed class FinalScreenDiagnostic
    {
        public string stage,eventKey,image,roiImage,error;public int repaintFrame,captureFrame,readbackCallbackFrame,width,height,eventType,sampleIndex,targetAgeMs,roiPixels,roiChangedPixels,roiComparedPixels;public float sampleTime,callbackTime,arrivalAge,callbackArrivalAge,arrivalTime,targetAgeErrorMs,roiMeanAbsDelta,roiMaxAbsDelta;public Vector4 trailRoi;public bool sameFrame,readbackSucceeded,withinTargetAgeTolerance,trailRoiAvailable,roiComparedToArrivalZero,readbackRowsStartAtTop,pngRowsFlipped;
    }
    [Serializable] sealed class TrailSampleIndex
    {public string eventKey,trailMeshError;public int sampleIndex,targetAgeMs,frame,eventType,drawFrame,screenRequestFrame,screenCallbackFrame,trailMeshFrame,trailMeshVertices,trailMeshTriangles;public float actualArrivalAge,targetAgeErrorMs,flowTimeAtRequest;public bool withinTargetAgeTolerance,guiSameFrame,guiPassesBound,screenSameFrame,renderReadbackSucceeded,screenReadbackSucceeded,trailMeshSameFrame,trailMeshBaked,trailMeshDiagnosticWritten;}
    [Serializable] sealed class HunterTrailMeshDiagnostic
    {
        public string stage,eventKey,error,trailObject,camera;
        public int sampleIndex,targetAgeMs,repaintFrame,screenRequestFrame,bakeFrame,vertexCount,triangleCount,vertexColorCount,trailPositionCount;
        public float arrivalAge,flowTime,trailTime,trailWidth,vertexAlphaMin,vertexAlphaMax;
        public bool screenReadbackRequested,sameRepaintFrame,bakeAttempted,bakeSucceeded,trailEnabled,trailEmitting,hasVertexColors;
        public Vector3 meshBoundsCenter,meshBoundsSize,meshBoundsMin,meshBoundsMax;
        public HunterRuntimeMaterialSnapshot[] materialState;
    }
    [Serializable] sealed class HunterAgeCandidate
    {public string eventKey;public int frame,eventType,targetAgeMs;public float realtime,flowTime,arrivalAge;}
    [Serializable] sealed class HunterSampleWindowMiss
    {public string eventKey,reason;public int targetAgeMs,lowerAgeMs,upperAgeMs,frame;public float firstObservedAge;}
    [Serializable] sealed class TrailSampleSummary
    {public int[] expectedTargetAgesMs,missingTargetAgesMs;public int toleranceMs,capturedCount,droppedAgeCandidates;public bool complete;public TrailSampleIndex[] samples;public HunterAgeCandidate[] ageCandidates;public HunterSampleWindowMiss[] sampleWindowMisses;}
    sealed class PendingHunterScreen
    {
        public SourceDrawDiagnostic repaint;public int width,height,requestFrame,callbackFrame;public float requestTime,callbackTime,callbackArrivalAge;public byte[] rgba;public string error;public bool readbackRequested,done,processed,rowsStartAtTop;
    }
    sealed class PendingHunterFlight{public string path;public FlightDiagnostic diagnostic;public bool written;}
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    readonly List<Record> records=new List<Record>();readonly HashSet<GameObject> owned=new HashSet<GameObject>();readonly HashSet<string> positionDiagnosticSamples=new HashSet<string>();
    ClientApp app;NativeEffectPlayer effects;NativeAudioPlayback audio;NativePowerVisual powers;Presentation presentation;
    NativeSuperSpellPlayback cast,rejectedSelection;StateView initial;FireblastPcmTap tap;string role,folder,phase="boot";float entered,started,staleRejectionReceivedAt=-1,staleRejectionLastNoteAt=-1;int step,checks,initialCounter;
    Vector2 powerPoint,targetPoint;bool finished,capturedCast,resolutionRequested,capturedFlightMid,capturedFlightLate;
    bool referenceWeaponGlowCaptured,referenceWarriorBurstCaptured,referenceWarriorRingsCaptured,referencePriestHealCaptured;
    readonly bool[] referencePaladinPhases=new bool[3],referenceWarlockPhases=new bool[3];
    static readonly float[] PaladinReferencePhaseAges={.15f,.4f,1.15f};
    static readonly float[] WarlockReferencePhaseAges={.15f,.8f,2.2f};
    static readonly string[] HeroPowerReferencePhaseNames={"early","mid","tail"};
    bool staleServerRejectionSeen;
    // The source Bang system emits for 0.7s; sample three useful points inside that life.
    static readonly float[] HunterImpactSampleAges={.25f,.45f,.65f};
    const int HunterImpactSampleToleranceMs=50;
    const float PriestStaleResponseTimeout=10f,PriestStaleCancellationTimeout=8f,PriestStaleProgressInterval=.5f;
    int nextHunterImpactSample;
    float hunterImpactLastObservedParticleAge=-1;int hunterImpactLastObservedParticleCount=-1;
    string hunterImpactLastUnavailableReason="no impact sample was observed";
    readonly List<HunterImpactSampleMiss> hunterImpactSampleMisses=new List<HunterImpactSampleMiss>();
    readonly List<HunterImpactDiagnostic> pendingHunterImpacts=new List<HunterImpactDiagnostic>();
    Report finishedReport;string finishResult;int finishExit;float peerFinishDeadline,peerReportSharingNoteAt=-1;bool completionConfirmed;
    string publicHash,lastCapture,runtimeError;float bootLog;bool bootCaptured;int cuesBeforeDisconnect,targetEntity,initialTargetHealth,listenerCount;
    int priestFinalHealth=-1,priestHealEventCount,priestHealCallbacks,priestMissileEventCount,priestArrivalEventCount,priestInputUnlockSampleFrame=-1;string priestPublicHash,priestDisplayHash;float priestInputUnlockSampleTime,priestInputUnlockBatchTime;bool priestInputUnlockedAfterSource;
    int priestTargetEventCount,priestImpactTargetEntity,priestDamageDecorations;string priestNativeEvents;float priestTargetGuiX,priestTargetGuiY,priestTargetGuiPositionError;bool priestImpactVisualCaptured,priestTargetGuiPositionValid,priestImpactCaptureRequested;
    int roguePlayHeroPowerCount,rogueSummonCount,rogueDaggerSummonCount,rogueSummonTarget,rogueUnexpectedHeroCueCount,rogueSourceCueCount,rogueSourceRootNodeCount,rogueSourceStateActions,rogueInputUnlockSampleFrame=-1,rogueSourceAudioPlayCount,rogueWeaponPowerUseCount,rogueLastPowerCountedVersion=-1;string rogueSourceKey,rogueDisplayHash;float rogueInputUnlockSampleTime;bool rogueSourceCueSemanticsValid,rogueSourceOnly,rogueParentFlowConstructed,rogueSourceRootReleased,rogueInputUnlockedAfterSource;RogueRules rogueBefore,rogueAfter;
    int rogueWeaponSlot7Events,rogueWeaponSlot5Events,rogueWeaponSlot7AudioPlayCount,rogueWeaponSlot5AudioPlayCount,rogueWeaponInputUnlockSampleFrame=-1,rogueWeaponPendingVersion=-1,rogueWeaponPendingSlot7Audio,rogueWeaponPendingSlot5Audio,rogueWeaponTurnVersion=-1,rogueWeaponAttackTargetEntity,rogueWeaponReplacementOrdinal,rogueWeaponReplacementCount,rogueWeaponReplacementOldEntity,rogueWeaponReplacementOldDurability,rogueWeaponEmptySlotEquipVersion=-1,rogueWeaponOccupiedReplacementVersion=-1;string rogueWeaponDisplayHash;bool rogueWeaponLifecycleComplete,rogueWeaponInputUnlockedAfterAttack,rogueWeaponLifecycleStarted,rogueDaggerSnapshotCaptured,rogueWeaponReplacementStartedOccupied,rogueWeaponReplacementSourceAudioSeen,rogueWeaponEndTurnGreenAfterOccupiedReplacement,rogueWeaponOnlyEndTurnAfterOccupiedReplacement,rogueWeaponResultReadyOnBothPeers;StateView rogueWeaponBeforeAttack;readonly List<RogueWeaponAttack> rogueWeaponAttacks=new List<RogueWeaponAttack>();
    readonly RogueWeaponLifecycleEvidence rogueWeaponLifecycle=new RogueWeaponLifecycleEvidence();readonly List<RogueLifecyclePoint> rogueWeaponLifecyclePoints=new List<RogueLifecyclePoint>();
    int rogueWeaponReplacementSubmitVersion=-1,rogueWeaponReplacementAcceptedVersion=-1,rogueWeaponEndTurnSubmitVersion=-1,rogueWeaponEndTurnAcceptedVersion=-1,rogueWeaponConcedeSubmitVersion=-1,rogueWeaponConcedeAcceptedVersion=-1;
    bool rogueWeaponReplacementAccepted,rogueWeaponReplacementSourceSeen,rogueWeaponReplacementSourceReleased,rogueWeaponReplacementInputUnlocked,rogueWeaponEndTurnAccepted,rogueWeaponConcedeSubmitted,rogueWeaponConcessionSynchronized,rogueObserverEndTurnSent,rogueWeaponStopMarkerWritten;
    int rogueObserverShamanPowerCount,rogueObserverShamanStage,rogueObserverShamanPowerSubmitVersion=-1,rogueObserverShamanEndTurnSubmitVersion=-1;string rogueObserverShamanTotemSequence="";float rogueObserverShamanActionStartedAt=-1;
    string rogueWeaponReplacementSourceKey;float lastRogueLifecycleSample=-1;
    int warriorPlayHeroPowerCount,warriorGainArmorCount,warriorSourceCueCount,warriorUnexpectedHitCueCount,warriorMissileEventCount,warriorArrivalEventCount,warriorCameraShakerStarts,warriorParentCameraShakerStarts,warriorImpactCloneCameraShakerStarts,warriorUnhandledActions,warriorParentUnhandledActions,warriorImpactCloneUnhandledActions,warriorUnboundAudioActions,warriorInputUnlockSampleFrame=-1,warriorSourceAudioPlayCount,warriorCastVersion=-1;string[] warriorParentUnhandledActionDetails=new string[0],warriorImpactCloneUnhandledActionDetails=new string[0];string warriorSourceKey,warriorDisplayHash;float warriorInputUnlockSampleTime;bool warriorEventOrderValid,warriorSourceCueSemanticsValid,warriorSourceRootCompleted,warriorSourceRootReleased,warriorInputUnlockedAfterSource;WarriorRules warriorBefore,warriorAfter;readonly WarriorFollowup warriorFollowup=new WarriorFollowup();
    SemanticEvent[] warriorCastAuthorityEvents;
    bool warriorFollowupSent;int warriorFollowupSubmitVersion=-1,warriorFollowupSubmitTurn=-1;
    int druidSpendManaCount,druidPlayHeroPowerCount,druidBuffCount,druidGainArmorCount,druidHitEventCount,druidUnexpectedHeroCueCount,druidMissileEventCount,druidArrivalEventCount,druidSourceCueCount,druidSourceStartActionCount,druidImpactCloneCount,druidImpactFinishedNotificationCount,druidSourceAnimationActionCount,druidSourceParticleActionCount,druidSourceAudioActionCount,druidSourceAudioPlayCount,druidUnhandledActions,druidUnboundAudioActions,druidMissingAudioPlaybackServices,druidInputUnlockSampleFrame=-1,druidAudioRecordStartIndex=-1;string druidSourceKey,druidDisplayHash;float druidInputUnlockSampleTime;bool druidEventOrderValid,druidSourceCueSemanticsValid,druidCastRequested,druidCastCompleted,druidFirstArrivalTimeMissing,druidSourceRootCompleted,druidSourceRootReleased,druidInputUnlockedAfterSource;DruidRules druidBefore,druidAfter;
    int warlockSpendManaCount,warlockPlayHeroPowerCount,warlockHitCount,warlockDrawCount,warlockSourceCueCount,warlockSourceWaitActionCount,warlockSourceSpellStateActionCount,warlockSourceAudioActionCount,warlockSourceAudioPlayCount,warlockUnhandledActions,warlockUnboundAudioActions,warlockMissileEventCount,warlockArrivalEventCount,warlockInputUnlockSampleFrame=-1,warlockAudioRecordStartIndex=-1;string warlockSourceKey,warlockDisplayHash;float warlockInputUnlockSampleTime;bool warlockEventOrderValid,warlockSourceCueSemanticsValid,warlockSourceRootCompleted,warlockSourceRootReleased,warlockInputUnlockedAfterSource;WarlockRules warlockBefore,warlockAfter;
    int paladinSpendManaCount,paladinPlayHeroPowerCount,paladinSummonCount,paladinRecruitSummonCount,paladinSourceCueCount,paladinAreaCloneCreations,paladinAreaFinishedNotifications,paladinAreaPending,paladinAudioActionCount,paladinAudioPlayCount,paladinUnhandledActions,paladinUnboundAudioActions,paladinInputUnlockSampleFrame=-1,paladinAudioRecordStartIndex=-1,paladinTurnSubmitVersion=-1,paladinTailSubmitFrame=-1,paladinNextTurnVersion=-1,paladinNextTurnAppliedFrame=-1,paladinNextTurnFrame=-1,paladinTailReleaseFrame=-1;string paladinSourceKey,paladinDisplayHash,paladinAreaReference,paladinNextTurnPublicHash,paladinNextTurnDisplayHash;float paladinInputUnlockSampleTime;bool paladinEventOrderValid,paladinSourceCueSemanticsValid,paladinFriendlyAreaOnlyReconstruction,paladinParentFlowConstructed,paladinAreaSeen,paladinAreaCompleted,paladinAreaReleased,paladinInputUnlockedAfterArea,paladinCastEvidenceLocked,paladinTailEndTurnSubmitted,paladinNextTurnAppliedDuringTail,paladinInputOpenDuringNextTurnTail,paladinTailReleasedAfterNextTurn;PaladinRules paladinBefore,paladinAfter;
    int shamanSpendManaCount,shamanPlayHeroPowerCount,shamanSummonCount,shamanTotemEntity,shamanNativeActiveCount,shamanRuntimeFailures,shamanSourceAudioPlayCount,shamanInputUnlockSampleFrame=-1,shamanAudioRecordStartIndex=-1;string shamanTotemCardId,shamanDisplayHash,shamanDiagnostics;float shamanInputUnlockSampleTime,shamanInputUnlockBatchTime;bool shamanEventOrderValid,shamanEventLinkValid,shamanNativeActorVisible,shamanActorVisualHiddenDuringSource,shamanActorVisualRestoredAfterSource,shamanInputUnlockedDuringSource,shamanDisplayHashMatched;
    readonly List<Frame> frames=new List<Frame>();int reviewTarget,hitCallbacks,healCallbacks,sequenceIndex;float lastFrameCapture,observedHitAge,firstMotionAge=float.MaxValue,lastMotionAge;bool sawWaiting,earlyFeedback,sawReactionStart,sawReactionEnd,invalidReactionWindow;EntityView previewCard;
    bool Hunter{get{return Argument("--hero-power-replay-id")=="HERO_05bp";}}
    bool Priest{get{return Argument("--hero-power-replay-id")=="HERO_09bp";}}
    bool PriestFullHealth{get{return Priest&&Environment.GetCommandLineArgs().Contains("--priest-full-health");}}
    bool Rogue{get{return Argument("--hero-power-replay-id")=="HERO_03bp";}}
    bool RogueWeapons{get{return Rogue&&(Environment.GetCommandLineArgs().Contains("--rogue-weapons")||RogueWeaponsShaman);}}
    bool RogueWeaponsShaman{get{return Environment.GetCommandLineArgs().Contains("--rogue-weapons-shaman");}}
    bool Warrior{get{return Argument("--hero-power-replay-id")=="HERO_01bp";}}
    bool Druid{get{return Argument("--hero-power-replay-id")=="HERO_06bp";}}
    bool Warlock{get{return Argument("--hero-power-replay-id")=="HERO_07bp";}}
    bool Paladin{get{return Argument("--hero-power-replay-id")=="HERO_04bp";}}
    bool Shaman{get{return Argument("--hero-power-replay-id")=="HERO_02bp";}}
    string TestedPower{get{return Hunter?"HERO_05bp":Priest?"HERO_09bp":Rogue?"HERO_03bp":Warrior?"HERO_01bp":Druid?"HERO_06bp":Warlock?"HERO_07bp":Paladin?"HERO_04bp":Shaman?"HERO_02bp":"HERO_08bp";}}
    bool Review{get{return Environment.GetCommandLineArgs().Contains("--fireblast-review");}}
    bool hunterFlightMid,hunterArrivalCapture,capturedRenderMid,capturedRenderArrival;
    const int HunterSampleToleranceMs=40;
    const int MaxHunterAgeCandidates=512;
    static readonly float[] HunterArrivalSampleAges={0f,.1f,.2f};
    readonly List<TrailSampleIndex> hunterTrailSamples=new List<TrailSampleIndex>();
    readonly List<HunterAgeCandidate> hunterAgeCandidates=new List<HunterAgeCandidate>();
    readonly List<HunterSampleWindowMiss> hunterSampleWindowMisses=new List<HunterSampleWindowMiss>();
    int droppedHunterAgeCandidates;
    readonly List<RenderDiagnostic> pendingHunterRenderDiagnostics=new List<RenderDiagnostic>();
    readonly List<PendingHunterScreen> pendingHunterScreens=new List<PendingHunterScreen>();
    readonly List<PendingHunterFlight> pendingHunterFlights=new List<PendingHunterFlight>();
    int nextHunterArrivalSample,pendingTrailSampleIndex=-1,pendingTrailTargetAgeMs=-1;
    float pendingTrailArrivalAge=-1;
    bool hunterDiagnosticFinalizing,hunterDiagnosticFinished;float hunterDiagnosticDeadline;
    RectInt hunterArrivalTrailRoi,pendingTrailRoi;bool hunterArrivalTrailRoiAvailable,pendingTrailRoiAvailable;
    Color32[] hunterArrivalRoiBaseline;
    string pendingSourceDrawStage;
    bool clockProbeDone,castChecksDone,followupSent,followupTailExpired;int followupVersion;float heldTime,rateTime;
    float observerHashWaitStarted=-1;
    bool DisconnectProbe{get{return Environment.GetCommandLineArgs().Contains("--fireblast-disconnect");}}
    bool UnaffordableProbe{get{return Environment.GetCommandLineArgs().Contains("--fireblast-unaffordable");}}
    T Field<T>(string name){return (T)typeof(ClientApp).GetField(name,Private).GetValue(app);}
    void Set(string name,object value){typeof(ClientApp).GetField(name,Private).SetValue(app,value);}
    object Invoke(string name,params object[] values){return typeof(ClientApp).GetMethod(name,Private).Invoke(app,values);}
    static string Argument(string name){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:"";}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string role=Argument("--hero-power-replay");if(role=="")role=Argument("--fireblast-replay");if(role=="")return;
        string folder=Argument("--fireblast-evidence-dir");if(folder=="")throw new ArgumentException("Explicit Fireblast evidence directory required");
        var replay=new GameObject("Fireblast formal input evidence").AddComponent<FireblastReplay>();replay.role=role;replay.folder=Path.GetFullPath(folder);Directory.CreateDirectory(replay.folder);
        replay.started=Time.realtimeSinceStartup;
    }
    void Check(bool value,string label){if(!value)throw new InvalidOperationException("Fireblast formal replay: "+label);checks++;Note(label);}
    static string EventTuple(SemanticEvent value)
    {return value.type+"(source="+value.source+",target="+value.target+",owner="+value.owner+",amount="+value.amount.ToString("F0",System.Globalization.CultureInfo.InvariantCulture)+")";}
    bool PriestSourceUnlockReady(StateView current)
    {
        return Priest&&role=="driver"&&cast!=null&&cast.Flow.Completed&&!presentation.Busy&&!presentation.InputLocked&&
            current!=null&&current.moves!=null&&current.moves.Any(move=>move.kind=="end_turn")&&
            presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    bool RogueWeaponIsEmpty(EntityView weapon)
    {
        return weapon==null||(weapon.entity==0&&String.IsNullOrEmpty(weapon.card_id));
    }
    RogueRules RogueState(StateView current)
    {
        var player=current.players[0];var weapon=player.weapon;
        bool weaponEmpty=RogueWeaponIsEmpty(weapon);
        var moves=current.seat==0?(current.moves??new Move[0]):new Move[0];
        return new RogueRules{seat=current.seat,powerEntity=player.power.entity,powerCardId=player.power.card_id,powerUsed=player.power.used,
            heroEntity=player.hero.entity,heroHealth=player.hero.health,heroAttack=player.hero.attack,mana=player.mana,maxMana=player.max_mana,
            weaponEntity=weaponEmpty?0:weapon.entity,weaponCardId=weaponEmpty?"":weapon.card_id,
            weaponAttack=weaponEmpty?0:weapon.attack,weaponDurability=weaponEmpty?0:weapon.durability,
            legalPowerMove=moves.Any(move=>move.kind=="power"&&move.entity==player.power.entity&&move.target==0),
            legalEndTurn=moves.Any(move=>move.kind=="end_turn"),publicHash=current.public_hash};
    }
    bool RogueSourceUnlockReady(StateView current)
    {
        return Rogue&&role=="driver"&&cast!=null&&rogueSourceRootReleased&&!presentation.Busy&&!presentation.InputLocked&&
            current!=null&&current.moves!=null&&current.moves.Any(move=>move.kind=="end_turn")&&
            presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    static int RogueWeaponAudioSlot(NativeAudioCue cue)
    {
        const string prefix="Weapon slot ";if(cue==null||String.IsNullOrEmpty(cue.sourceName)||!cue.sourceName.StartsWith(prefix,StringComparison.Ordinal))return -1;
        string value=cue.sourceName.Substring(prefix.Length);int end=value.IndexOf(' ');if(end>=0)value=value.Substring(0,end);
        int slot;return int.TryParse(value,out slot)?slot:-1;
    }
    EntityView RogueWeaponAttackTarget(StateView current)
    {
        if(!RogueWeaponsShaman)return current.players[1].hero;
        var ids=new[]{"CS2_050","NEW1_009","CS2_051"};
        return (current.players[1].field??new EntityView[0]).Where(item=>ids.Contains(item.card_id))
            .OrderBy(item=>item.entity).LastOrDefault();
    }
    string RogueExpectedTotem(int attackIndex)
    {return attackIndex==1?"CS2_050":attackIndex==2?"NEW1_009":attackIndex==3?"CS2_051":"";}
    void StartRogueWeaponAttack(StateView current,int attackIndex)
    {
        var ownHero=current.players[0].hero;var target=RogueWeaponAttackTarget(current);
        Check(target!=null,"Rogue weapon reproduction has the next live authority target");
        if(RogueWeaponsShaman)
            Check(target.card_id==RogueExpectedTotem(attackIndex),"saved Shaman totem order selects the matching Rogue attack target");
        var legal=(current.moves??new Move[0]).Where(move=>move.kind=="attack"&&move.entity==ownHero.entity&&move.target==target.entity).ToArray();
        Check(RogueWeapons&&role=="driver"&&current.seat==0&&legal.Length==1,
            "Rogue weapon harness has one real authority hero-attack move to its selected target");
        var locations=Field<Dictionary<int,Rect>>("locations");
        Check(locations.ContainsKey(ownHero.entity)&&locations.ContainsKey(target.entity),"actual Player lays out both hero attack endpoints");
        rogueWeaponBeforeAttack=current;rogueWeaponPendingVersion=current.version;rogueWeaponAttackTargetEntity=target.entity;
        rogueWeaponPendingSlot7Audio=rogueWeaponSlot7AudioPlayCount;rogueWeaponPendingSlot5Audio=rogueWeaponSlot5AudioPlayCount;
        rogueWeaponLifecycleStarted=true;
        Pointer(locations[ownHero.entity].center);Click(locations[ownHero.entity].center);
        Check(Field<int>("selected")==ownHero.entity,"actual Player input selects the Rogue hero for attack");
        Check(BattleInteraction.IsLegalTarget(current,ownHero.entity,target.entity),"selected target is an authority-listed Rogue hero-attack target");
        Click(locations[target.entity].center);
        int expectedCounter=initialCounter+(RogueWeaponsShaman&&attackIndex==3?7:attackIndex==1?2:4);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==expectedCounter,
            "actual Player target click submits one legal Rogue hero attack");
        Advance(attackIndex==3?41:30+attackIndex,"rogue-weapon-attack-"+attackIndex);
    }
    void EndRogueWeaponTurn(StateView current)
    {
        var endTurn=(current.moves??new Move[0]).FirstOrDefault(move=>move.kind=="end_turn");
        Check(endTurn!=null&&current.active==0,"first Rogue attack leaves the real legal end-turn move available");
        rogueWeaponTurnVersion=current.version;Invoke("SendMove",endTurn);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+3,"Rogue submits its actual legal end-turn action after the first attack");
        Advance(33,"rogue-weapon-opponent-turn");
    }
    void CompleteRogueWeaponTurn(StateView current)
    {
        bool ready=current.version>rogueWeaponTurnVersion&&current.seat==0&&current.active==0&&!Field<bool>("pending")&&
            !presentation.Busy&&!presentation.InputLocked&&presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
        if(!ready)
        {
            if(Age>25)throw new TimeoutException("Mage Player did not pass its legal end turn back to Rogue; version="+current.version+
                " active="+current.active+" turn="+current.turn+" pending="+Field<bool>("pending")+" busy="+presentation.Busy+
                " inputLocked="+presentation.InputLocked+" publicHash="+current.public_hash+" displayHash="+
                (presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        var ownHero=current.players[0].hero;var target=RogueWeaponAttackTarget(current);var weapon=current.players[0].weapon;
        Check(current.turn>initial.turn&&ownHero.used==false&&weapon!=null&&weapon.card_id=="CS2_082"&&weapon.durability==1,
            "after one real turn cycle Rogue keeps its 1-durability CS2_082 and refreshes hero attack");
        Check((current.moves??new Move[0]).Count(move=>move.kind=="attack"&&move.entity==ownHero.entity&&move.target==target.entity)==1,
            "next Rogue turn exposes exactly one authority-listed hero attack against its current target");
        StartRogueWeaponAttack(current,2);
    }
    void CompleteRogueWeaponAttack(StateView current,int attackIndex)
    {
        int expectedSlot5Audio=attackIndex==2?1:0;
        bool weaponAudioReady=rogueWeaponSlot7AudioPlayCount-rogueWeaponPendingSlot7Audio==1&&
            rogueWeaponSlot5AudioPlayCount-rogueWeaponPendingSlot5Audio==expectedSlot5Audio;
        if(current.version<=rogueWeaponPendingVersion||Field<bool>("pending")||presentation.Busy||presentation.InputLocked||
            presentation.Display==null||current.public_hash!=presentation.Display.public_hash||!weaponAudioReady)
        {
            if(Age>15)throw new TimeoutException("Rogue hero attack did not settle to an unlocked, hash-matched authority state; version="+
                current.version+" pending="+Field<bool>("pending")+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+
                " audio7="+(rogueWeaponSlot7AudioPlayCount-rogueWeaponPendingSlot7Audio)+" audio5="+
                (rogueWeaponSlot5AudioPlayCount-rogueWeaponPendingSlot5Audio)+" publicHash="+current.public_hash+" displayHash="+
                (presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        var ownHero=current.players[0].hero;var beforeTarget=BattleInteraction.Entity(rogueWeaponBeforeAttack,rogueWeaponAttackTargetEntity);
        var afterTarget=BattleInteraction.Entity(current,rogueWeaponAttackTargetEntity);
        var beforeWeapon=rogueWeaponBeforeAttack.players[0].weapon;var weapon=RogueWeaponIsEmpty(current.players[0].weapon)?null:current.players[0].weapon;
        int weaponEntity=RogueWeaponIsEmpty(beforeWeapon)?0:beforeWeapon.entity;
        var events=current.events??new SemanticEvent[0];
        int targetDeaths=events.Count(e=>e.type=="Death"&&e.target==rogueWeaponAttackTargetEntity);
        int targetHealth=afterTarget==null?(targetDeaths>0?0:-1):afterTarget.health;
        int attacks=events.Count(e=>e.type=="Attack"&&e.source==ownHero.entity&&e.target==rogueWeaponAttackTargetEntity);
        int targetHits=events.Count(e=>e.type=="Hit"&&e.source==ownHero.entity&&e.target==rogueWeaponAttackTargetEntity&&Mathf.Abs(e.amount-1)<.001f);
        int weaponHits=events.Count(e=>e.type=="Hit"&&e.source==weaponEntity&&e.target==weaponEntity&&e.amount>0);
        int deaths=events.Count(e=>e.type=="Death"&&e.target==weaponEntity);
        int slot7=events.Count(e=>NativeBattleWeapons.Slot(e,rogueWeaponBeforeAttack,current)==7);
        int slot5=events.Count(e=>NativeBattleWeapons.Slot(e,rogueWeaponBeforeAttack,current)==5);
        rogueWeaponSlot7Events+=slot7;rogueWeaponSlot5Events+=slot5;
        int audio7=rogueWeaponSlot7AudioPlayCount-rogueWeaponPendingSlot7Audio;
        int audio5=rogueWeaponSlot5AudioPlayCount-rogueWeaponPendingSlot5Audio;
        bool nextAttack=(current.moves??new Move[0]).Any(move=>move.kind=="attack"&&move.entity==ownHero.entity);
        bool endTurn=(current.moves??new Move[0]).Any(move=>move.kind=="end_turn");
        bool unlocked=!Field<bool>("pending")&&!presentation.InputLocked&&!presentation.Busy;
        var observation=new RogueWeaponAttack{attackIndex=attackIndex,frame=Time.frameCount,version=current.version,heroEntity=ownHero.entity,targetEntity=rogueWeaponAttackTargetEntity,
            targetCardId=beforeTarget==null?"":beforeTarget.card_id,targetHealthBefore=beforeTarget==null?-1:beforeTarget.health,targetHealth=targetHealth,targetDeath=targetDeaths>0,
            weaponEntity=weapon==null?0:weapon.entity,weaponAttack=weapon==null?0:weapon.attack,
            weaponDurability=weapon==null?0:weapon.durability,attackEvents=attacks,targetHitEvents=targetHits,
            weaponHitEvents=weaponHits,weaponDeathEvents=deaths,slot7Events=slot7,slot5Events=slot5,
            publicHash=current.public_hash,displayHash=presentation.Display.public_hash,inputUnlocked=unlocked,
            hashMatched=current.public_hash==presentation.Display.public_hash,legalNextAttack=nextAttack,legalEndTurn=endTurn};
        rogueWeaponAttacks.Add(observation);rogueWeaponDisplayHash=observation.displayHash;publicHash=current.public_hash;
        frames.Add(new Frame{frame=observation.frame,version=current.version,target=rogueWeaponAttackTargetEntity,authorityHealth=targetHealth,
            displayHealth=BattleInteraction.Entity(presentation.Display,rogueWeaponAttackTargetEntity)?.health??(targetDeaths>0?0:-1),authorityAttack=ownHero.attack,
            displayAttack=BattleInteraction.Entity(presentation.Display,ownHero.entity)?.attack??-1,time=Time.realtimeSinceStartup-started,
            batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,hashMatched=observation.hashMatched,
            endTurnAvailable=endTurn,nativeEvents=String.Join(";",events.Select(EventTuple).ToArray())});
        int expectedDurability=attackIndex==2?0:1;
        Check(attacks==1&&targetHits==1&&targetHealth==beforeTarget.health-1,
            "authority applies one real Rogue hero attack and one damage to the selected target");
        Check(weaponHits==1&&deaths==(attackIndex==2?1:0)&&slot7==1&&slot5==(attackIndex==2?1:0),
            "authority weapon events map to one slot 7 durability hit and, when exhausted, slot 5 destruction");
        Check(attackIndex==2?weapon==null&&RogueWeaponIsEmpty(current.players[0].weapon)&&ownHero.attack==0:
            weapon!=null&&weapon.card_id=="CS2_082"&&weapon.attack==1&&weapon.durability==expectedDurability&&ownHero.attack==1,
            attackIndex==2?"CS2_082 authority weapon is destroyed and its hero attack expires":"CS2_082 authority durability decreases while hero attack remains 1");
        Check(audio7==1&&audio5==(attackIndex==2?1:0),"actual NativeAudioPlayback starts the expected slot 7/slot 5 source clips");
        Check(observation.hashMatched&&unlocked&&(attackIndex==1?endTurn&&!nextAttack:!nextAttack&&endTurn),
            "Rogue weapon attack settles with authority/display hash and the next legal input unlocked");
        if(attackIndex==1){EndRogueWeaponTurn(current);return;}
        if(attackIndex==2)
        {
            rogueWeaponInputUnlockedAfterAttack=observation.inputUnlocked&&observation.hashMatched&&observation.legalEndTurn;
            rogueWeaponInputUnlockSampleFrame=Time.frameCount;
            if(!RogueWeaponsShaman)Check(rogueWeaponAttacks.Count==2&&rogueWeaponSlot7Events==2&&rogueWeaponSlot5Events==1&&
                rogueWeaponSlot7AudioPlayCount==2&&rogueWeaponSlot5AudioPlayCount==1,
                "two durability-hit source clips and one destruction source clip play across the real 2-to-1-to-destroy lifecycle");
            Check(rogueWeaponInputUnlockedAfterAttack,"Rogue input unlocks after weapon destruction with matching hashes and legal end turn");
            StartRogueWeaponReplacementPower(current,2);return;
        }
        if(attackIndex==3)
        {
            rogueWeaponInputUnlockedAfterAttack=observation.inputUnlocked&&observation.hashMatched&&observation.legalEndTurn;
            rogueWeaponInputUnlockSampleFrame=Time.frameCount;
            StartRogueWeaponReplacementPower(current,3);return;
        }
    }
    void StartRogueWeaponReplacementPower(StateView current,int afterAttackIndex)
    {
        var player=current.players[0];var move=(current.moves??new Move[0]).SingleOrDefault(item=>
            item.kind=="power"&&item.entity==player.power.entity&&item.target==0);
        bool emptySlot=RogueWeaponIsEmpty(player.weapon);
        int ordinal=afterAttackIndex==3?2:1;
        Check(current.seat==0&&current.active==0&&(RogueWeaponsShaman?emptySlot==(ordinal==1):emptySlot)&&move!=null,
            ordinal==1?"after the refreshed attack breaks the dagger, authority exposes one legal target=0 Rogue replacement power":
            "after a refreshed attack with one durability remaining, authority permits the non-empty-slot Rogue weapon replacement");
        if(ordinal==2)
        {
            rogueWeaponReplacementAccepted=false;rogueWeaponReplacementSourceSeen=false;rogueWeaponReplacementSourceReleased=false;
            rogueWeaponReplacementInputUnlocked=false;rogueWeaponStopMarkerWritten=false;rogueWeaponReplacementSourceKey=null;
        }
        rogueWeaponReplacementOrdinal=ordinal;
        rogueWeaponReplacementStartedOccupied=!emptySlot;
        rogueWeaponReplacementOldEntity=emptySlot?0:player.weapon.entity;
        rogueWeaponReplacementOldDurability=emptySlot?0:player.weapon.durability;
        rogueWeaponReplacementSourceAudioSeen=false;
        var locations=Field<Dictionary<int,Rect>>("locations");
        Check(locations.ContainsKey(player.power.entity),"actual Player lays out the replacement Rogue power button");
        rogueWeaponReplacementSubmitVersion=current.version;
        Pointer(locations[player.power.entity].center);Click(locations[player.power.entity].center);
        int expectedCounter=initialCounter+(RogueWeaponsShaman&&ordinal==2?8:5);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==expectedCounter,
            "actual replacement power button click submits one legal HERO_03bp request");
        rogueWeaponLifecycle.replacementSubmitted=true;rogueWeaponLifecycle.replacementSubmitVersion=current.version;
        RecordRogueWeaponLifecyclePoint(current,"replacement-power-submitted");
        Capture("rogue-replacement-power-submitted");Advance(34,"rogue-replacement-power");
    }
    string RogueWeaponReplacementKey(StateView current)
    {
        var cues=presentation.Cues.Where(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_03bp"&&
            item.data.source==current.players[0].power.entity).ToArray();
        if(cues.Length==0)return null;
        Check(cues.Length==1,"replacement batch exposes one actual HERO_03bp source cue");
        var cue=cues[0];
        Check(cue.data.target==0&&cue.apply==float.MaxValue,"replacement HERO_03bp cue remains an untargeted source-only action");
        return cue==null?null:current.match_id+":"+current.version+":"+cue.data.source+":"+cue.start;
    }
    void RecordRogueWeaponLifecyclePoint(StateView current,string stage)
    {
        if(current==null)return;
        if(String.IsNullOrEmpty(rogueWeaponReplacementSourceKey))rogueWeaponReplacementSourceKey=RogueWeaponReplacementKey(current);
        var source=String.IsNullOrEmpty(rogueWeaponReplacementSourceKey)?null:effects.SourceSpell(rogueWeaponReplacementSourceKey);
        bool nativeBusy=!String.IsNullOrEmpty(rogueWeaponReplacementSourceKey)&&effects.SourceBusy(rogueWeaponReplacementSourceKey);
        if(source!=null)rogueWeaponReplacementSourceSeen=true;
        if(source!=null&&source.Effect!=null&&source.Effect.AudioCues.Any(cue=>!String.IsNullOrEmpty(cue.file)))
            rogueWeaponReplacementSourceAudioSeen=true;
        if(rogueWeaponReplacementSourceSeen&&!nativeBusy)rogueWeaponReplacementSourceReleased=true;
        bool pending=Field<bool>("pending"),locked=presentation.InputLocked;
        bool ready=current.phase=="play"&&current.active==current.seat&&!Field<bool>("battleMenu")&&!pending&&
            !locked&&!Field<MulliganPresentation>("mulliganPresentation").Active;
        var shown=presentation.Display;bool hashMatched=shown!=null&&current.public_hash==shown.public_hash;
        var ownWeapon=RogueWeaponIsEmpty(current.players[0].weapon)?null:current.players[0].weapon;
        bool onlyEndTurn=(current.moves??new Move[0]).Length==1&&(current.moves??new Move[0])[0].kind=="end_turn";
        var point=new RogueLifecyclePoint{stage=stage,phase=current.phase,version=current.version,turn=current.turn,activePlayer=current.active,
            frame=Time.frameCount,time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,
            actionCounter=Field<int>("actionCounter"),queueCount=presentation.QueueCount,busy=presentation.Busy,inputLocked=locked,
            pending=pending,nativeBusy=nativeBusy,sourcePresent=source!=null,nativeTime=source==null?-1:source.Time,
            resultReady=Field<BattleLifecycle>("lifecycle").ResultReady,publicHash=current.public_hash,displayHash=shown==null?null:shown.public_hash,
            hashMatched=hashMatched,weaponEntity=ownWeapon==null?0:ownWeapon.entity,weaponDurability=ownWeapon==null?0:ownWeapon.durability,
            result=current.players[current.seat].result,legalPowerMove=current.seat==0&&(current.moves??new Move[0]).Any(item=>item.kind=="power"),
            legalEndTurn=current.seat==0&&(current.moves??new Move[0]).Any(item=>item.kind=="end_turn"),endTurnReady=ready,
            endTurnGreen=ready&&BattleInteraction.NoActions(current),onlyEndTurn=onlyEndTurn};
        rogueWeaponLifecyclePoints.Add(point);
        rogueWeaponLifecycle.points=rogueWeaponLifecyclePoints.ToArray();rogueWeaponLifecycle.role=role;
        Note("Rogue lifecycle "+stage+" version="+point.version+" active="+point.activePlayer+" pending="+point.pending+
            " queue="+point.queueCount+" busy="+point.busy+" inputLocked="+point.inputLocked+" nativeBusy="+point.nativeBusy+
            " resultReady="+point.resultReady+" hashMatched="+point.hashMatched+" endTurnGreen="+point.endTurnGreen);
    }
    string RogueWeaponMarker(string matchId,string stage)
    {return Path.Combine(Directory.GetParent(folder).FullName,"rogue-"+matchId+"-"+stage+".txt");}
    bool RogueObserverSawReplacement(StateView current,int ordinal=0)
    {
        string stage=RogueWeaponsShaman?"observer-saw-replacement-"+ordinal:"observer-saw-replacement";
        string path=RogueWeaponMarker(current.match_id,stage);if(!File.Exists(path))return false;
        try{return File.ReadAllText(path)==current.match_id+":"+current.version+":"+current.public_hash;}
        catch(IOException){return false;}
    }
    void CompleteRogueWeaponReplacement(StateView current)
    {
        if(!rogueWeaponReplacementAccepted)
        {
            var weapon=RogueWeaponIsEmpty(current.players[0].weapon)?null:current.players[0].weapon;
            if(current.version<=rogueWeaponReplacementSubmitVersion||Field<bool>("pending")||weapon==null||
               weapon.card_id!="CS2_082"||weapon.durability!=2||!current.players[0].power.used)return;
            var events=current.events??new SemanticEvent[0];
            int oldWeapon=rogueWeaponReplacementOldEntity;
            var summons=events.Where(item=>item.type=="Summon"&&item.card_type==7&&item.target==weapon.entity&&item.owner==0).ToArray();
            var powers=events.Where(item=>item.type=="PlayHeroPower"&&item.target==current.players[0].power.entity&&item.owner==0).ToArray();
            var spends=events.Where(item=>item.type=="SpendMana"&&item.source==current.players[0].power.entity&&item.owner==0&&Mathf.Abs(item.amount-2)<.001f).ToArray();
            int oldDestroy=events.Count(item=>item.type=="Destroy"&&item.target==oldWeapon);
            int oldDeath=events.Count(item=>item.type=="Death"&&item.target==oldWeapon);
            bool occupiedReplacement=rogueWeaponReplacementStartedOccupied;
            Check(weapon.entity!=oldWeapon&&weapon.attack==1&&current.players[0].hero.attack==1&&
                summons.Length==1&&powers.Length==1&&spends.Length==1,
                "authority accepts the legal Rogue power, creates a new CS2_082, and spends two mana exactly once");
            if(RogueWeaponsShaman&&rogueWeaponReplacementOrdinal==2)
                Check(oldWeapon>0&&rogueWeaponReplacementOldDurability==1&&oldDestroy==1&&oldDeath==1,
                    "v18 authority destroys and removes the one-durability CS2_082 before summoning its replacement");
            else Check(oldDestroy==0&&oldDeath==0,
                    "empty-slot equip does not fabricate destruction events for an already exhausted dagger");
            if(RogueWeaponsShaman)
                Check(rogueWeaponReplacementOrdinal==2?
                    events.Select(item=>item.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Destroy","Death","Summon"}):
                    events.Select(item=>item.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Summon"}),
                    rogueWeaponReplacementOrdinal==2?
                    "v18 event order is SpendMana -> PlayHeroPower -> Destroy -> Death -> Summon":
                    "v13 empty-slot equip event order is SpendMana -> PlayHeroPower -> Summon");
            Check(!(current.moves??new Move[0]).Any(item=>item.kind=="power")&&
                (current.moves??new Move[0]).Any(item=>item.kind=="end_turn"),
                "after replacement the authority removes the power move and retains the legal end-turn move");
            rogueWeaponReplacementAccepted=true;rogueWeaponReplacementAcceptedVersion=current.version;
            rogueWeaponReplacementCount++;
            if(!occupiedReplacement)rogueWeaponEmptySlotEquipVersion=current.version;
            else rogueWeaponOccupiedReplacementVersion=current.version;
            rogueWeaponLifecycle.emptySlotEquipVersion=rogueWeaponEmptySlotEquipVersion;
            rogueWeaponLifecycle.occupiedReplacementVersion=rogueWeaponOccupiedReplacementVersion;
            rogueWeaponLifecycle.replacementAccepted=true;rogueWeaponLifecycle.replacementAcceptedVersion=current.version;
            rogueWeaponReplacementSourceKey=RogueWeaponReplacementKey(current);
            RecordRogueWeaponLifecyclePoint(current,RogueWeaponsShaman?(occupiedReplacement?"occupied-replacement-power-accepted":"empty-slot-equip-accepted"):"replacement-power-accepted");
            Capture(occupiedReplacement?"rogue-occupied-replacement-accepted":"rogue-empty-slot-equip-accepted");
        }
        if(Time.realtimeSinceStartup-lastRogueLifecycleSample>=.25f)
        {lastRogueLifecycleSample=Time.realtimeSinceStartup;RecordRogueWeaponLifecyclePoint(current,"replacement-power-progress");}
        if(String.IsNullOrEmpty(rogueWeaponReplacementSourceKey))
        {
            if(Age>20)throw new TimeoutException("Rogue authority accepted the replacement power but Presentation did not expose its HERO_03bp source cue; version="+
                current.version+" queue="+presentation.QueueCount+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+
                " publicHash="+current.public_hash+" displayHash="+(presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        bool sourceAudioSeen=RogueWeaponsShaman?rogueWeaponReplacementSourceAudioSeen:
            records.Count(item=>item.audioSource=="native-effect"&&item.message=="source play")>=2;
        bool currentAuthority=current.version==rogueWeaponReplacementAcceptedVersion&&current.seat==0&&current.active==0&&
            current.players[0].power.used&&!RogueWeaponIsEmpty(current.players[0].weapon)&&
            current.players[0].weapon.durability==2&&(current.moves??new Move[0]).Any(item=>item.kind=="end_turn");
        bool inputReady=currentAuthority&&!Field<bool>("pending")&&!presentation.InputLocked&&presentation.Display!=null&&
            current.public_hash==presentation.Display.public_hash&&rogueWeaponReplacementSourceSeen&&
            rogueWeaponReplacementSourceReleased&&sourceAudioSeen;
        if(!inputReady)
        {
            if(Age>20)throw new TimeoutException("Rogue replacement power did not reach the first legal unlocked follow-up; version="+
                current.version+" pending="+Field<bool>("pending")+" queue="+presentation.QueueCount+" busy="+presentation.Busy+
                " inputLocked="+presentation.InputLocked+" nativeBusy="+(!String.IsNullOrEmpty(rogueWeaponReplacementSourceKey)&&effects.SourceBusy(rogueWeaponReplacementSourceKey))+
                " sourceSeen="+rogueWeaponReplacementSourceSeen+" sourceReleased="+rogueWeaponReplacementSourceReleased+
                " sourceAudio="+sourceAudioSeen+" publicHash="+current.public_hash+" displayHash="+
                (presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        rogueWeaponReplacementInputUnlocked=true;rogueWeaponLifecycle.replacementSourceSeen=true;
        rogueWeaponLifecycle.replacementSourceReleased=true;rogueWeaponLifecycle.replacementInputUnlocked=true;
        if(RogueWeaponsShaman&&rogueWeaponReplacementOrdinal==2)
        {
            rogueWeaponEndTurnGreenAfterOccupiedReplacement=!Field<bool>("battleMenu")&&
                !Field<bool>("pending")&&!presentation.InputLocked&&BattleInteraction.NoActions(current);
            rogueWeaponOnlyEndTurnAfterOccupiedReplacement=(current.moves??new Move[0]).Length==1&&
                current.moves[0].kind=="end_turn";
            Check(rogueWeaponEndTurnGreenAfterOccupiedReplacement&&rogueWeaponOnlyEndTurnAfterOccupiedReplacement,
                "after v18 source unlock, only the legal end-turn move remains and ClientApp's green-tint condition is true");
        }
        string unlockedStage=RogueWeaponsShaman?(rogueWeaponReplacementOrdinal==2?"occupied-replacement-input-unlocked":"empty-slot-equip-input-unlocked"):
            "replacement-input-unlocked";
        RecordRogueWeaponLifecyclePoint(current,unlockedStage);
        if(rogueWeaponStopMarkerWritten)return;
        string markerStage=RogueWeaponsShaman?"replacement-"+rogueWeaponReplacementOrdinal+"-request":"stop-after-endturn";
        string stopMarker=RogueWeaponMarker(current.match_id,markerStage);
        File.WriteAllText(stopMarker,current.match_id+":"+current.version+":"+current.public_hash);
        rogueWeaponStopMarkerWritten=true;Capture(RogueWeaponsShaman&&rogueWeaponReplacementOrdinal==2?
            "rogue-occupied-replacement-input-unlocked":"rogue-replacement-input-unlocked");
        Advance(RogueWeaponsShaman&&rogueWeaponReplacementOrdinal==2?40:37,
            RogueWeaponsShaman&&rogueWeaponReplacementOrdinal==2?"rogue-occupied-replacement-peer-observe":"rogue-replacement-peer-observe");
    }
    void SubmitRogueWeaponReplacementEndTurn(StateView current)
    {
        if(!RogueObserverSawReplacement(current,1))
        {
            if(Age>5)throw new TimeoutException("Mage observer did not acknowledge the accepted Rogue replacement state before end turn; version="+
                current.version+" hash="+current.public_hash);
            return;
        }
        if(current.version!=rogueWeaponReplacementAcceptedVersion||current.active!=0||current.seat!=0||Field<bool>("pending")||
           !(current.moves??new Move[0]).Any(item=>item.kind=="end_turn")||presentation.InputLocked)
        {
            if(Age>5)throw new TimeoutException("Rogue replacement follow-up changed during the bounded peer-observation window; version="+
                current.version+" pending="+Field<bool>("pending")+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);
            return;
        }
        var endTurn=(current.moves??new Move[0]).SingleOrDefault(item=>item.kind=="end_turn");
        rogueWeaponEndTurnSubmitVersion=current.version;
        Invoke("SendMove",endTurn);
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+6,
            "Rogue submits legal end turn at the first input-unlocked frame, even if the current visual batch remains busy");
        rogueWeaponLifecycle.endTurnSubmitted=true;
        RecordRogueWeaponLifecyclePoint(current,"replacement-end-turn-submitted");
        Capture("rogue-replacement-end-turn-submitted");Advance(35,"rogue-replacement-end-turn");
    }
    void CompleteRogueWeaponEndTurn(StateView current)
    {
        if(!rogueWeaponEndTurnAccepted)
        {
            if(current.version<=rogueWeaponEndTurnSubmitVersion||Field<bool>("pending"))
            {
                if(Age>15)throw new TimeoutException("Rogue end-turn request did not receive an authoritative response; version="+
                    current.version+" pending="+Field<bool>("pending")+" queue="+presentation.QueueCount+
                    " busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);
                return;
            }
            Check(current.phase=="play"&&current.active==1,
                "authority accepts replacement-turn end turn and transfers active seat to Mage");
            rogueWeaponEndTurnAccepted=true;rogueWeaponEndTurnAcceptedVersion=current.version;
            rogueWeaponLifecycle.endTurnAccepted=true;rogueWeaponLifecycle.endTurnAcceptedVersion=current.version;
            RecordRogueWeaponLifecyclePoint(current,"replacement-end-turn-accepted");
            Capture("rogue-replacement-end-turn-accepted");Advance(RogueWeaponsShaman?39:38,
                RogueWeaponsShaman?"rogue-shaman-second-turn-observe":"rogue-peer-turn-observe");return;
        }
    }
    void CompleteRogueWeaponShamanTurn(StateView current)
    {
        bool ready=current.phase=="play"&&current.version>rogueWeaponEndTurnAcceptedVersion&&current.seat==0&&current.active==0&&
            !Field<bool>("pending")&&!presentation.Busy&&presentation.QueueCount==0&&!presentation.InputLocked&&presentation.Display!=null&&
            current.public_hash==presentation.Display.public_hash;
        if(!ready)
        {
            if(Age>25)throw new TimeoutException("Shaman did not complete its second actual Totemic Call turn; version="+current.version+
                " active="+current.active+" pending="+Field<bool>("pending")+" queue="+presentation.QueueCount+
                " busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+" resultReady="+
                Field<BattleLifecycle>("lifecycle").ResultReady+" publicHash="+current.public_hash+
                " displayHash="+(presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        var weapon=current.players[0].weapon;var target=RogueWeaponAttackTarget(current);
        Check(current.players[0].hero.used==false&&weapon!=null&&weapon.card_id=="CS2_082"&&weapon.durability==2,
            "after Shaman's second legal Totemic Call, Rogue's replacement CS2_082 remains at durability 2 and attack refreshes");
        Check(target!=null&&target.card_id=="CS2_051"&&target.entity==75,
            "authority board contains the second live Shaman totem CS2_051/75");
        string peerMarker=RogueWeaponMarker(current.match_id,"observer-shaman-turn-complete");
        string expectedPeer=current.version+"|"+current.public_hash+"|2|73:NEW1_009;75:CS2_051";
        string peerEvidence=null;
        if(File.Exists(peerMarker))
        {
            try{peerEvidence=File.ReadAllText(peerMarker);}
            catch(IOException){return;}
        }
        if(peerEvidence==null)
        {
            if(Age>25)throw new TimeoutException("Shaman observer did not attest its two actual Totemic Call actions and settled turn; version="+
                current.version+" hash="+current.public_hash);
            return;
        }
        Check(peerEvidence==expectedPeer,
            "Shaman Player attests the settled v16 turn and NEW1_009/73 then CS2_051/75 sequence");
        RecordRogueWeaponLifecyclePoint(current,"after-shaman-second-turn-before-attack");
        Check((current.moves??new Move[0]).Any(move=>move.kind=="attack"&&move.entity==current.players[0].hero.entity&&move.target==75),
            "authority accepts the v17 Rogue hero attack against the second live Shaman totem");
        StartRogueWeaponAttack(current,3);
    }
    void SubmitRogueWeaponConcede(StateView current)
    {
        if(Age<.35f)return;
        if(current.version!=rogueWeaponEndTurnAcceptedVersion||current.phase!="play"||current.active!=1||Field<bool>("pending"))
        {
            if(Age>5)throw new TimeoutException("Mage active turn was not retained for Rogue concession; phase="+current.phase+
                " version="+current.version+" active="+current.active+" pending="+Field<bool>("pending"));
            return;
        }
        RecordRogueWeaponLifecyclePoint(current,"mage-turn-before-concede");
        if(!rogueWeaponConcedeSubmitted)
        {
            rogueWeaponConcedeSubmitVersion=current.version;
            RecordRogueWeaponLifecyclePoint(current,"before-concede");
            Invoke("SendMove",new Move{kind="concede"});
            Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+7,
                "Rogue submits concede while Mage owns the active turn, including through any remaining local visual lock");
            rogueWeaponConcedeSubmitted=true;rogueWeaponLifecycle.concedeSubmitted=true;
            RecordRogueWeaponLifecyclePoint(current,"concede-submitted");
            Capture("rogue-concede-submitted");Advance(36,"rogue-concede-finalize");return;
        }
    }
    void SubmitRogueWeaponConcedeAfterReplacement(StateView current)
    {
        if(!RogueObserverSawReplacement(current,2))
        {
            if(Age>5)throw new TimeoutException("Shaman observer did not acknowledge the v18 occupied-slot replacement");
            return;
        }
        if(current.version!=rogueWeaponOccupiedReplacementVersion||current.phase!="play"||current.active!=0||current.seat!=0||
            Field<bool>("pending")||presentation.InputLocked||presentation.Display==null||current.public_hash!=presentation.Display.public_hash)
        {
            if(Age>8)throw new TimeoutException("Rogue v18 replacement did not retain its unlocked turn before concede; version="+
                current.version+" active="+current.active+" pending="+Field<bool>("pending")+
                " inputLocked="+presentation.InputLocked+" hash="+current.public_hash+" display="+
                (presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        bool onlyEndTurn=(current.moves??new Move[0]).Length==1&&current.moves[0].kind=="end_turn";
        Check(onlyEndTurn&&BattleInteraction.NoActions(current)&&!Field<bool>("battleMenu"),
            "v18 has only legal end turn; the shared EndTurnContent green-tint condition is true before concede");
        rogueWeaponEndTurnGreenAfterOccupiedReplacement=true;rogueWeaponOnlyEndTurnAfterOccupiedReplacement=true;
        RecordRogueWeaponLifecyclePoint(current,"before-concede-after-occupied-replacement");
        rogueWeaponConcedeSubmitVersion=current.version;
        Invoke("SendMove",new Move{kind="concede"});
        Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+9,
            "Rogue submits the original v19 concede from the same turn after v18's non-empty weapon replacement");
        rogueWeaponConcedeSubmitted=true;rogueWeaponLifecycle.concedeSubmitted=true;
        RecordRogueWeaponLifecyclePoint(current,"concede-submitted");
        Capture("rogue-v19-concede-submitted");Advance(36,"rogue-v19-result-sync");
    }
    void AcknowledgeRogueWeaponReplacement(StateView current,int ordinal)
    {
        string request=RogueWeaponMarker(current.match_id,"replacement-"+ordinal+"-request");
        if(!File.Exists(request))return;
        string payload=current.match_id+":"+current.version+":"+current.public_hash;
        if(File.ReadAllText(request)!=payload)return;
        string acknowledgement=RogueWeaponMarker(current.match_id,"observer-saw-replacement-"+ordinal);
        if(File.Exists(acknowledgement))return;
        var events=current.events??new SemanticEvent[0];var weapon=current.players[0].weapon;
        bool accepted=weapon!=null&&weapon.card_id=="CS2_082"&&weapon.durability==2&&
            events.Any(item=>item.type=="PlayHeroPower"&&item.target==current.players[0].power.entity&&item.owner==0)&&
            events.Any(item=>item.type=="Summon"&&item.card_type==7&&item.target==weapon.entity&&item.owner==0);
        if(!accepted)throw new InvalidOperationException("Observer received replacement request without the matching Rogue power/weapon summon");
        File.WriteAllText(acknowledgement,payload);
        rogueWeaponReplacementAccepted=true;rogueWeaponReplacementAcceptedVersion=current.version;
        rogueWeaponReplacementCount++;
        if(ordinal==1)rogueWeaponEmptySlotEquipVersion=current.version;
        else rogueWeaponOccupiedReplacementVersion=current.version;
        rogueWeaponLifecycle.replacementAccepted=true;rogueWeaponLifecycle.replacementAcceptedVersion=current.version;
        if(ordinal==1)rogueWeaponLifecycle.emptySlotEquipVersion=current.version;
        else rogueWeaponLifecycle.occupiedReplacementVersion=current.version;
        RecordRogueWeaponLifecyclePoint(current,ordinal==1?"observer-empty-slot-equip-accepted":"observer-occupied-replacement-accepted");
    }
    void TickRogueWeaponShamanObserver(StateView current)
    {
        if(current==null)return;
        if(current.phase=="finished")
        {
            if(rogueWeaponResultReadyOnBothPeers)return;
            if(!RogueConcessionReady(current))return;
            string ready=RogueWeaponMarker(current.match_id,"peer-result-ready");
            string expected=current.version+":"+current.public_hash;
            if(!File.Exists(ready))File.WriteAllText(ready,expected);
            if(File.ReadAllText(ready)!=expected)return;
            string driverReady=RogueWeaponMarker(current.match_id,"driver-result-ready");
            if(!File.Exists(driverReady)||File.ReadAllText(driverReady)!=expected)return;
            rogueWeaponResultReadyOnBothPeers=true;rogueWeaponLifecycle.resultReadyOnBothPeers=true;
            rogueWeaponLifecycle.finalHash=current.public_hash;rogueWeaponLifecycle.concessionSynchronized=true;
            rogueWeaponConcessionSynchronized=true;rogueWeaponLifecycleComplete=true;
            rogueWeaponDisplayHash=presentation.Display.public_hash;publicHash=current.public_hash;
            RecordRogueWeaponLifecyclePoint(current,"observer-both-peers-result-ready");return;
        }
        if(rogueObserverShamanStage==3&&current.version>rogueObserverShamanEndTurnSubmitVersion&&!Field<bool>("pending")&&
            current.active==0&&!presentation.Busy&&presentation.QueueCount==0&&!presentation.InputLocked&&presentation.Display!=null&&
            current.public_hash==presentation.Display.public_hash)
        {
            Note("Shaman observer completed Totemic Call turn powerCount="+rogueObserverShamanPowerCount+" version="+current.version);
            if(rogueObserverShamanPowerCount==2)
            {
                Check(rogueObserverShamanTotemSequence=="73:NEW1_009;75:CS2_051",
                    "Shaman observer confirms both actual Totemic Call summons in authority entity order");
                RecordRogueWeaponLifecyclePoint(current,"observer-shaman-turn-complete");
                string marker=RogueWeaponMarker(current.match_id,"observer-shaman-turn-complete");
                string payload=current.version+"|"+current.public_hash+"|"+rogueObserverShamanPowerCount+"|"+
                    rogueObserverShamanTotemSequence;
                if(File.Exists(marker))Check(File.ReadAllText(marker)==payload,
                    "Shaman settled-turn marker is stable and bound to this authority version/hash");
                else File.WriteAllText(marker,payload);
            }
            rogueObserverShamanStage=0;rogueObserverShamanActionStartedAt=-1;return;
        }
        if(current.phase!="play"||current.seat!=1||current.active!=1)return;
        if(rogueObserverShamanStage!=0&&rogueObserverShamanActionStartedAt>0&&
            Time.realtimeSinceStartup-rogueObserverShamanActionStartedAt>25)
            throw new TimeoutException("Shaman observer did not settle its actual Totemic Call turn; stage="+
                rogueObserverShamanStage+" version="+current.version+" powerCount="+rogueObserverShamanPowerCount+
                " pending="+Field<bool>("pending")+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+
                " publicHash="+current.public_hash+" displayHash="+(presentation.Display==null?"":presentation.Display.public_hash));
        if(rogueObserverShamanStage==0)
        {
            if(rogueObserverShamanPowerCount>=2)return;
            var power=current.players[1].power;
            Check(power.card_id=="HERO_02bp"&&!power.used,"saved observer turn exposes a refreshed Shaman Totemic Call power");
            var move=(current.moves??new Move[0]).SingleOrDefault(item=>item.kind=="power"&&item.entity==power.entity&&item.target==0);
            if(move==null||Field<bool>("pending")||presentation.InputLocked||presentation.Display==null||
                current.public_hash!=presentation.Display.public_hash)return;
            var locations=Field<Dictionary<int,Rect>>("locations");
            Check(locations.ContainsKey(power.entity),"actual Shaman Player lays out the legal Totemic Call button");
            rogueObserverShamanPowerSubmitVersion=current.version;rogueObserverShamanActionStartedAt=Time.realtimeSinceStartup;
            Pointer(locations[power.entity].center);Click(locations[power.entity].center);
            int expectedCounter=initialCounter+rogueObserverShamanPowerCount*2+1;
            Check(Field<bool>("pending")&&Field<int>("actionCounter")==expectedCounter,
                "actual Shaman button click submits one legal Totemic Call request");
            rogueObserverShamanStage=1;return;
        }
        if(rogueObserverShamanStage==1)
        {
            if(current.version<=rogueObserverShamanPowerSubmitVersion||Field<bool>("pending"))return;
            var power=current.players[1].power;var events=current.events??new SemanticEvent[0];
            var powers=events.Where(item=>item.type=="PlayHeroPower"&&item.source!=0&&item.target==power.entity&&item.owner==1).ToArray();
            var summons=events.Where(item=>item.type=="Summon"&&item.source==power.entity&&item.owner==1&&item.card_type==4&&item.target!=0).ToArray();
            string expectedCard=rogueObserverShamanPowerCount==0?"NEW1_009":"CS2_051";
            var totem=summons.Length==1?BattleInteraction.Entity(current,summons[0].target):null;
            Check(power.used&&powers.Length==1&&summons.Length==1&&totem!=null&&totem.card_id==expectedCard,
                "Shaman authority accepts its power and summons the saved real totem "+expectedCard);
            rogueObserverShamanPowerCount++;
            rogueObserverShamanTotemSequence+=(rogueObserverShamanTotemSequence==""?"":";")+totem.entity+":"+totem.card_id;
            rogueObserverShamanStage=2;rogueObserverShamanActionStartedAt=Time.realtimeSinceStartup;
            RecordRogueWeaponLifecyclePoint(current,"observer-shaman-power-"+rogueObserverShamanPowerCount+"-accepted");return;
        }
        if(rogueObserverShamanStage==2)
        {
            if(Field<bool>("pending")||presentation.Busy||presentation.InputLocked||presentation.Display==null||
                current.public_hash!=presentation.Display.public_hash)return;
            var endTurn=(current.moves??new Move[0]).SingleOrDefault(item=>item.kind=="end_turn");
            Check(endTurn!=null,"Shaman power leaves the real legal end-turn action available");
            rogueObserverShamanEndTurnSubmitVersion=current.version;Invoke("SendMove",endTurn);
            Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+rogueObserverShamanPowerCount*2,
                "Shaman observer submits its authority-listed end turn after the authored source settles");
            rogueObserverShamanStage=3;rogueObserverShamanActionStartedAt=Time.realtimeSinceStartup;
            RecordRogueWeaponLifecyclePoint(current,"observer-shaman-end-turn-submitted");return;
        }
    }
    bool RogueConcessionReady(StateView current)
    {
        return current!=null&&current.phase=="finished"&&!Field<bool>("pending")&&!presentation.Busy&&
            !presentation.InputLocked&&presentation.Display!=null&&current.public_hash==presentation.Display.public_hash&&
            Field<BattleLifecycle>("lifecycle").ResultReady&&current.players.Length==2&&
            current.players[0].result=="LOST"&&current.players[1].result=="WON";
    }
    void CompleteRogueWeaponConcede(StateView current)
    {
        if(current.phase=="finished"&&!Field<bool>("pending")&&rogueWeaponConcedeAcceptedVersion<0)
        {
            var events=current.events??new SemanticEvent[0];
            Check(current.version>rogueWeaponConcedeSubmitVersion&&events.Any(item=>item.type=="Concede"&&item.owner==0),
                "authority accepts Rogue concession after the replacement-turn end turn");
            rogueWeaponConcedeAcceptedVersion=current.version;rogueWeaponLifecycle.concedeAcceptedVersion=current.version;
            RecordRogueWeaponLifecyclePoint(current,"concede-authority-accepted");
            Capture("rogue-concede-authority-accepted");
        }
        if(Time.realtimeSinceStartup-lastRogueLifecycleSample>=.25f)
        {lastRogueLifecycleSample=Time.realtimeSinceStartup;RecordRogueWeaponLifecyclePoint(current,"concede-finalizing");}
        if(!RogueConcessionReady(current))
        {
            if(Age>35)throw new TimeoutException("Concession did not converge to a ready, hash-matched result on the Rogue Player; phase="+
                current.phase+" version="+current.version+" pending="+Field<bool>("pending")+" queue="+presentation.QueueCount+
                " busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+" resultReady="+
                Field<BattleLifecycle>("lifecycle").ResultReady+" publicHash="+current.public_hash+" displayHash="+
                (presentation.Display==null?"":presentation.Display.public_hash));
            return;
        }
        if(RogueWeaponsShaman)
        {
            string peerReady=RogueWeaponMarker(current.match_id,"peer-result-ready");
            string expected=current.version+":"+current.public_hash;
            if(!File.Exists(peerReady)||File.ReadAllText(peerReady)!=expected)
            {
                if(Age>35)throw new TimeoutException("Shaman peer has not reported ResultReady with the same v19 hash; expected="+expected);
                return;
            }
            string driverReady=RogueWeaponMarker(current.match_id,"driver-result-ready");
            if(!File.Exists(driverReady))File.WriteAllText(driverReady,expected);
            rogueWeaponResultReadyOnBothPeers=true;rogueWeaponLifecycle.resultReadyOnBothPeers=true;
        }
        Check(rogueWeaponReplacementAccepted&&rogueWeaponReplacementInputUnlocked&&rogueWeaponEndTurnAccepted&&
            rogueWeaponEndTurnAcceptedVersion<current.version&&rogueWeaponConcedeAcceptedVersion==current.version,
            "replacement power, end-turn authority, and concession remain ordered in the accepted lifecycle");
        rogueWeaponConcessionSynchronized=true;rogueWeaponLifecycle.concessionSynchronized=true;
        rogueWeaponLifecycle.finalHash=current.public_hash;rogueWeaponLifecycleComplete=true;
        rogueWeaponDisplayHash=presentation.Display.public_hash;publicHash=current.public_hash;
        RecordRogueWeaponLifecyclePoint(current,"concede-finished-synchronized");
        Check(current.players[0].result=="LOST"&&current.players[1].result=="WON"&&
            current.public_hash==presentation.Display.public_hash&&Field<BattleLifecycle>("lifecycle").ResultReady&&
            (!RogueWeaponsShaman||rogueWeaponResultReadyOnBothPeers),
            RogueWeaponsShaman?"both Rogue and Shaman Players reach ResultReady with the same finished v19 hash":
            "both local authority and result lifecycle converge after Rogue concedes while the Mage owns the turn");
        Capture("rogue-concede-final");tap.StopCapture();Invoke("ResetMenu");Advance(8,"scene-exit");
    }
    WarriorRules WarriorState(StateView current)
    {
        var player=current.players[0];var moves=current.seat==0?(current.moves??new Move[0]):new Move[0];
        return new WarriorRules{seat=current.seat,version=current.version,powerEntity=player.power.entity,powerCardId=player.power.card_id,powerUsed=player.power.used,
            heroEntity=player.hero.entity,heroHealth=player.hero.health,heroArmor=player.hero.armor,mana=player.mana,maxMana=player.max_mana,
            legalPowerMove=moves.Any(move=>move.kind=="power"&&move.entity==player.power.entity&&move.target==0),
            legalEndTurn=moves.Any(move=>move.kind=="end_turn"),publicHash=current.public_hash};
    }
    bool WarriorSourceUnlockReady(StateView current)
    {
        return Warrior&&role=="driver"&&cast!=null&&warriorFollowup.authorityAccepted&&warriorSourceRootReleased&&
            warriorFollowup.sourceTailCleanedAfterFollowup&&!presentation.Busy&&!presentation.InputLocked&&
            current!=null&&presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    void TrySubmitWarriorFollowup(StateView current)
    {
        if(warriorFollowupSent||current==null||cast==null||warriorFollowup.powerAcceptedTime<=0||
           presentation.InputLocked||Field<bool>("pending"))return;
        if(current.version!=warriorCastVersion)return;
        var move=(current.moves??new Move[0]).FirstOrDefault(item=>item.kind=="end_turn");
        if(move==null)return;
        bool sourceActive=effects.SourceBusy(warriorSourceKey);
        if(!sourceActive)return;
        float now=Time.realtimeSinceStartup-started;
        float latency=now-warriorFollowup.powerAcceptedTime;
        float budget=.2f+Mathf.Max(Time.unscaledDeltaTime,1f/60f);
        Check(presentation.Busy&&sourceActive,"Warrior input opened before its source/batch cleanup");
        Check(latency>=0&&latency<=budget+.001f,"Warrior follow-up exceeded the 0.2s input gate plus one frame: "+latency.ToString("F3"));
        warriorInputUnlockedAfterSource=true;warriorInputUnlockSampleFrame=Time.frameCount;warriorInputUnlockSampleTime=now;
        warriorFollowup.inputUnlockedDuringSource=true;warriorFollowup.sourceActiveAtSend=true;
        warriorFollowup.sampleFrame=Time.frameCount;warriorFollowup.inputUnlockSampleTime=now;
        warriorFollowup.sendTime=now;warriorFollowup.sendLatency=latency;warriorFollowup.latencyBudget=budget;
        warriorFollowup.submitVersion=current.version;warriorFollowupSubmitVersion=current.version;warriorFollowupSubmitTurn=current.turn;
        warriorFollowupSent=true;Invoke("SendMove",move);
        warriorFollowup.sendMoveSubmitted=Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+2;
        warriorFollowup.actionCounterDelta=Field<int>("actionCounter")-initialCounter;
        Check(warriorFollowup.sendMoveSubmitted,"ClientApp rejected the legal Warrior end-turn SendMove");
        int ownHero=initial.players[0].hero.entity;
        frames.RemoveAll(frame=>frame.frame==Time.frameCount);
        frames.Add(new Frame{frame=Time.frameCount,version=current.version,target=0,authorityHealth=current.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1,authorityArmor=current.players[0].hero.armor,
            displayArmor=BattleInteraction.Entity(presentation.Display,ownHero)?.armor??-1,time=now,batchTime=presentation.TimeInBatch,
            busy=presentation.Busy,inputLocked=presentation.InputLocked,sourceCompleted=warriorSourceRootCompleted,
            sourceReleased=warriorSourceRootReleased,hashMatched=presentation.Display!=null&&current.public_hash==presentation.Display.public_hash,
            endTurnAvailable=true,sourceActive=sourceActive,followupSent=warriorFollowup.sendMoveSubmitted,
            actionCounter=Field<int>("actionCounter"),actionCounterDelta=Field<int>("actionCounter")-initialCounter,
            followupSendLatency=latency,publicHash=current.public_hash,displayHash=presentation.Display==null?null:presentation.Display.public_hash,
            nativeEvents=String.Join(";",cast.Events)});
        Note("Warrior end-turn SendMove submitted while source active at "+now.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            "s after power acceptance; latency="+latency.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            " sourceActive="+sourceActive+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);
    }
    DruidRules DruidState(StateView current)
    {
        var player=current.players[0];var moves=current.seat==0?(current.moves??new Move[0]):new Move[0];
        return new DruidRules{seat=current.seat,powerEntity=player.power.entity,powerCardId=player.power.card_id,powerUsed=player.power.used,
            heroEntity=player.hero.entity,heroHealth=player.hero.health,heroAttack=player.hero.attack,heroArmor=player.hero.armor,
            mana=player.mana,maxMana=player.max_mana,
            legalPowerMove=moves.Any(move=>move.kind=="power"&&move.entity==player.power.entity&&move.target==0),
            legalEndTurn=moves.Any(move=>move.kind=="end_turn"),publicHash=current.public_hash};
    }
    bool DruidSourceUnlockReady(StateView current)
    {
        return Druid&&role=="driver"&&cast!=null&&druidSourceRootReleased&&!presentation.Busy&&!presentation.InputLocked&&
            current!=null&&current.moves!=null&&current.moves.Any(move=>move.kind=="end_turn")&&
            presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    WarlockRules WarlockState(StateView current)
    {
        var player=current.players[0];var moves=current.seat==0?(current.moves??new Move[0]):new Move[0];
        return new WarlockRules{seat=current.seat,powerEntity=player.power.entity,powerCardId=player.power.card_id,powerUsed=player.power.used,
            heroEntity=player.hero.entity,heroHealth=player.hero.health,mana=player.mana,maxMana=player.max_mana,
            handCount=player.hand==null?0:player.hand.Length,deckCount=player.deck_count,
            legalPowerMove=moves.Any(move=>move.kind=="power"&&move.entity==player.power.entity&&move.target==0),
            legalEndTurn=moves.Any(move=>move.kind=="end_turn"),publicHash=current.public_hash};
    }
    bool WarlockSourceUnlockReady(StateView current)
    {
        return Warlock&&role=="driver"&&cast!=null&&warlockSourceRootReleased&&!presentation.Busy&&!presentation.InputLocked&&
            current!=null&&current.moves!=null&&current.moves.Any(move=>move.kind=="end_turn")&&
            presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    PaladinRules PaladinState(StateView current)
    {
        var player=current.players[0];var recruits=(player.field??new EntityView[0]).Where(entity=>entity.card_id=="CS2_101t").ToArray();
        var recruit=recruits.FirstOrDefault();var moves=current.seat==0?(current.moves??new Move[0]):new Move[0];
        return new PaladinRules{seat=current.seat,powerEntity=player.power.entity,powerCardId=player.power.card_id,powerUsed=player.power.used,
            mana=player.mana,maxMana=player.max_mana,recruitCount=recruits.Length,recruitEntity=recruit==null?0:recruit.entity,
            recruitCardId=recruit==null?"":recruit.card_id,recruitAttack=recruit==null?0:recruit.attack,recruitHealth=recruit==null?0:recruit.health,
            legalPowerMove=moves.Any(move=>move.kind=="power"&&move.entity==player.power.entity&&move.target==0),
            legalEndTurn=moves.Any(move=>move.kind=="end_turn"),publicHash=current.public_hash};
    }
    bool PaladinTailInputReady(StateView current)
    {
        return Paladin&&role=="driver"&&paladinAreaCompleted&&presentation.Busy&&!presentation.InputLocked&&
            effects!=null&&!String.IsNullOrEmpty(paladinSourceKey)&&effects.SourceVisualTailActive(paladinSourceKey)&&
            current!=null&&current.moves!=null&&current.moves.Any(move=>move.kind=="end_turn")&&
            presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
    }
    void CompletePaladinTailTurn(StateView current)
    {
        if(current.version<=paladinTurnSubmitVersion)
        {
            if(Age>15f)throw new TimeoutException("Paladin end-turn did not produce a newer authority state while its FriendlyArea tail was active");
            return;
        }
        if(!paladinNextTurnAppliedDuringTail)
        {
            var events=current.events??new SemanticEvent[0];
            bool turnStarted=events.Any(e=>e.type=="TURN_START");
            bool tailActive=effects!=null&&effects.SourceVisualTailActive(paladinSourceKey);
            Check(current.active==1&&turnStarted&&!Field<bool>("pending"),
                "real Paladin end-turn action advances authority to the Mage TURN_START");
            Check(tailActive,"next authority turn arrives before the previous Paladin visual tail ends");
            paladinNextTurnAppliedDuringTail=true;paladinNextTurnVersion=current.version;paladinNextTurnAppliedFrame=Time.frameCount;
            Note("Paladin next authority turn arrived during source visual tail frame="+paladinNextTurnAppliedFrame+
                " version="+paladinNextTurnVersion+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);
        }
        bool sourceTailActive=effects!=null&&effects.SourceVisualTailActive(paladinSourceKey);
        bool hashMatched=presentation.Display!=null&&current.public_hash==presentation.Display.public_hash;
        if(!paladinInputOpenDuringNextTurnTail)
        {
            if(sourceTailActive&&hashMatched&&!presentation.InputLocked)
            {
                paladinInputOpenDuringNextTurnTail=true;paladinNextTurnPublicHash=current.public_hash;
                paladinNextTurnDisplayHash=presentation.Display.public_hash;paladinNextTurnFrame=Time.frameCount;
                Check(sourceTailActive&&hashMatched&&!presentation.InputLocked,
                    "next turn display hash catches authority and input stays unlocked while the Paladin tail remains active");
            }
            else if(!sourceTailActive)
                throw new InvalidOperationException("Paladin tail ended before the next authority turn display synchronized and input stayed open");
            else if(Age>15f)
                throw new TimeoutException("Paladin next authority turn did not synchronize while the previous source tail was active");
            return;
        }
        if(sourceTailActive)return;
        if(!paladinAreaReleased||presentation.InputLocked||presentation.Display==null||current.public_hash!=presentation.Display.public_hash)
        {
            if(Age>15f)throw new TimeoutException("Paladin visual tail stopped without a released area and synchronized unlocked display");
            return;
        }
        paladinTailReleasedAfterNextTurn=true;paladinTailReleaseFrame=Time.frameCount;
        Check(paladinTailReleasedAfterNextTurn&&paladinNextTurnFrame<paladinTailReleaseFrame,
            "Paladin visual tail releases after the next authority turn has already synchronized");
        tap.StopCapture();Invoke("ResetMenu");Advance(8,"scene-exit");
    }
    void Note(string message,NativeAudioCue cue=null,string audioSource=null)
    {
        var currentState=app==null?null:Field<StateView>("state");var currentSelection=effects?.PowerSelection;
        var observedSelection=currentSelection??rejectedSelection;var flow=observedSelection==null?null:observedSelection.Flow;
        var voice=cue==null||audio==null?null:audio.Sources.LastOrDefault(source=>source!=null&&source.clip!=null&&source.clip.name==cue.file);
        var listener=cue==null?null:FindObjectsOfType<AudioListener>().FirstOrDefault(value=>value.enabled);
        records.Add(new Record{phase=phase,message=message,time=Time.realtimeSinceStartup-started,file=cue?.file,audioSource=audioSource,volume=cue==null?0:cue.volume,pitch=cue==null?0:cue.pitch,
            spatial=voice!=null?voice.spatialBlend:cue==null?0:cue.spatialBlend,loop=cue!=null&&cue.loop,playing=cue?.playbackActive!=null&&cue.playbackActive(),
            positionKnown=cue!=null&&cue.positionKnown,cuePosition=cue==null?Vector3.zero:cue.position,voiceFound=voice!=null,
            voicePosition=voice==null?Vector3.zero:voice.transform.position,listenerPosition=listener==null?Vector3.zero:listener.transform.position,
            voiceMute=voice!=null&&voice.mute,voiceVolume=voice==null?0:voice.volume,voiceMinDistance=voice==null?0:voice.minDistance,
            voiceMaxDistance=voice==null?0:voice.maxDistance,listenerDistance=voice==null||listener==null?0:Vector3.Distance(voice.transform.position,listener.transform.position),
            version=currentState==null?0:currentState.version,actionCount=app==null?0:Field<int>("actionCounter"),voices=audio==null?0:audio.ActiveVoices,
            pending=app!=null&&Field<bool>("pending"),serverStatus=app==null?null:Field<string>("status"),hasSelection=effects!=null&&effects.HasPowerSelection,
            targetingCancelled=flow!=null&&flow.TargetingCancelled,selectionBusy=observedSelection!=null&&observedSelection.Busy,
            flowCompleted=flow!=null&&flow.Completed,flowCancelled=flow!=null&&flow.Cancelled,activeClones=flow==null?0:flow.ActiveClones,sourcePending=flow==null?0:flow.Pending,
            sourceTime=currentSelection==null?0:currentSelection.Time,sourceClock=observedSelection==null?0:observedSelection.Time,
            frameDelta=app==null?0:app.EvidenceFrameDeltas.y,speed=presentation?.Speed??0,paused=presentation?.Paused??false});
    }
    string PriestStaleCancellationState()
    {
        var flow=rejectedSelection==null?null:rejectedSelection.Flow;
        return "status="+Field<string>("status")+" pending="+Field<bool>("pending")+" stateVersion="+Field<StateView>("state").version+
            " hasSelection="+(effects!=null&&effects.HasPowerSelection)+" targetingCancelled="+(flow!=null&&flow.TargetingCancelled)+
            " busy="+(rejectedSelection!=null&&rejectedSelection.Busy)+" completed="+(flow!=null&&flow.Completed)+
            " activeClones="+(flow==null?0:flow.ActiveClones)+" sourcePending="+(flow==null?0:flow.Pending)+
            " sourceClock="+(rejectedSelection==null?0:rejectedSelection.Time).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
    }
    void Advance(int next,string name){step=next;phase=name;entered=Time.realtimeSinceStartup;Note("enter");}
    float Age{get{return Time.realtimeSinceStartup-entered;}}
    void Pointer(Vector2 point){app.EvidencePointer=point;}
    void Input(EventType type,int button,Vector2 point){Pointer(point);app.EvidencePressed=type==EventType.MouseDown&&button==0;Invoke("HandleBattleInput",type,button,point);}
    void Click(Vector2 point){Input(EventType.MouseDown,0,point);Input(EventType.MouseUp,0,point);}
    bool HoverReviewHand(StateView state)
    {
        if(Field<HandPresentation>("handPresentation").Hovered==previewCard.entity)return true;
        var locations=Field<Dictionary<int,Rect>>("locations");var calibration=Field<UICalibration>("calibration");var rect=locations[previewCard.entity];
        // 扇形手牌按真实当前命中区域找露出位置；卡片中心可能被下一张遮住。
        for(float y=rect.y+5;y<rect.yMax-5;y+=10)for(float x=rect.x+5;x<rect.xMax-5;x+=10)
        {
            var point=new Vector2(x,y);int last=locations.Where(p=>calibration.Hit(p.Key,point)).Select(p=>p.Key).LastOrDefault();
            if(last==previewCard.entity){Pointer(point);return false;}
        }
        throw new InvalidOperationException("Review card has no actual exposed input area: "+previewCard.card_id);
    }
    void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));Note("capture "+name);}
    string CaptureHunterOffscreenImpact(string eventKey,NativeSuperSpellPlayback spell)
    {
        if(nextHunterImpactSample>=HunterImpactSampleAges.Length)return null;
        var impact=spell.Flow.Instances.FirstOrDefault(clone=>clone.Effect.Program.reference.Contains("Arrow_Impact_01"));
        if(impact==null||impact.Effect.OffscreenFX.Count==0)
        {hunterImpactLastUnavailableReason="impact clone absent or released before the remaining particle ages were observed";return null;}
        var bang=impact.Effect.Particles.FirstOrDefault(particle=>particle!=null&&particle.name=="Bang");
        // Death先播放箭体动画和随机等待；Arrival不是爆炸粒子的出生时刻。
        if(bang==null){hunterImpactLastUnavailableReason="Bang particle system is unavailable";return null;}
        hunterImpactLastObservedParticleAge=bang.time;hunterImpactLastObservedParticleCount=bang.particleCount;
        if(bang.particleCount==0){hunterImpactLastUnavailableReason="Bang has no live particles at the observed age";return null;}
        while(nextHunterImpactSample<HunterImpactSampleAges.Length&&
            bang.time*1000>Mathf.RoundToInt(HunterImpactSampleAges[nextHunterImpactSample]*1000)+HunterImpactSampleToleranceMs)
        {
            int missedTarget=Mathf.RoundToInt(HunterImpactSampleAges[nextHunterImpactSample++]*1000);
            hunterImpactSampleMisses.Add(new HunterImpactSampleMiss{targetParticleAgeMs=missedTarget,frame=Time.frameCount,
                observedParticleAge=bang.time,reason="first source Repaint after target tolerance window"});
            hunterImpactLastUnavailableReason="one or more requested particle ages were skipped between source repaints";
        }
        if(nextHunterImpactSample>=HunterImpactSampleAges.Length)return null;
        int targetMs=Mathf.RoundToInt(HunterImpactSampleAges[nextHunterImpactSample]*1000);
        if(Mathf.Abs(bang.time*1000-targetMs)>HunterImpactSampleToleranceMs)
        {hunterImpactLastUnavailableReason="remaining requested particle age has not been reached";return null;}
        nextHunterImpactSample++;
        string stage="impact-particle-"+targetMs.ToString("D4",System.Globalization.CultureInfo.InvariantCulture)+"ms";
        var effect=impact.Effect.OffscreenFX[0];
        var plane=impact.Effect.Nodes.First(node=>node.name=="RenderPlane");var material=plane.GetComponent<Renderer>().sharedMaterial;
        var row=new HunterImpactDiagnostic{stage=stage,eventKey=eventKey,image="hunter-offscreen-"+stage+".png",
            frame=Time.frameCount,eventType=(int)Event.current.type,targetParticleAgeMs=targetMs,
            arrivalAge=HunterArrivalAge(spell),cloneAge=impact.PlaybackTime,particleAge=bang.time,
            targetAgeErrorMs=bang.time*1000-targetMs,orthographicSize=effect.CaptureCamera.orthographicSize,
            planeScale=plane.transform.localScale,intensity=material.GetFloat("_Intensity")};
        pendingHunterImpacts.Add(row);
        // 读取本帧生产RT，不重绘、不修改相机；最终屏幕沿用同帧Draw诊断链。
        RequestHunterPixelReadback(effect.Texture,row.arrivalAge,spell,pixels=>{row.pixels=pixels;row.done=true;});
        Note("Hunter impact sample "+stage+" particleAge="+row.particleAge+" arrivalAge="+row.arrivalAge+" intensity="+row.intensity);
        return stage;
    }
    string MaterialSnapshot(Material material)
    {
        if(material==null)return "<null material>";
        string shader=material.shader==null?"<null shader>":material.shader.name;
        string textures=String.Join(",",material.GetTexturePropertyNames().Select(name=>name+"="+TextureSnapshot(material.GetTexture(name))).ToArray());
        string color=material.HasProperty("_Color")?material.GetColor("_Color").ToString():"<absent>";
        string tint=material.HasProperty("_TintColor")?material.GetColor("_TintColor").ToString():"<absent>";
        string alpha=material.HasProperty("_Color")?material.GetColor("_Color").a.ToString("R",System.Globalization.CultureInfo.InvariantCulture):"<absent>";
        string hasTexture=material.HasProperty("_HasSourceTexture")?material.GetFloat("_HasSourceTexture").ToString("R",System.Globalization.CultureInfo.InvariantCulture):"<absent>";
        string family=material.HasProperty("_OriginalFamily")?material.GetFloat("_OriginalFamily").ToString("R",System.Globalization.CultureInfo.InvariantCulture):"<absent>";
        return material.name+" shader="+shader+" supported="+(material.shader!=null&&material.shader.isSupported)+" passes="+material.passCount+" main="+TextureSnapshot(material.mainTexture)+" textures="+textures+
            " color="+color+" tint="+tint+" alpha="+alpha+" _HasSourceTexture="+hasTexture+" _OriginalFamily="+family;
    }
    static string TextureSnapshot(Texture texture)
    {return texture==null?"<null>":texture.GetType().Name+"#"+texture.GetInstanceID()+" "+texture.width+"x"+texture.height;}
    static float HunterArrivalAge(NativeSuperSpellPlayback spell)
    {return spell==null||spell.Flow==null||!spell.Flow.FirstArrivalTime.HasValue?-1:spell.Flow.Time-spell.Flow.FirstArrivalTime.Value;}
    void RequestHunterPixelReadback(RenderTexture texture,float sourceArrivalAge,NativeSuperSpellPlayback spell,Action<PixelStats> complete)
    {
        int requestFrame=Time.frameCount;var stats=new PixelStats{width=texture==null?0:texture.width,height=texture==null?0:texture.height,
            requestFrame=requestFrame,sourceArrivalAge=sourceArrivalAge,readbackSucceeded=false};
        if(texture==null){stats.readbackError="render texture is null";stats.callbackFrame=Time.frameCount;complete(stats);return;}
        if(!SystemInfo.supportsAsyncGPUReadback){stats.readbackError="AsyncGPUReadback is unsupported";stats.callbackFrame=Time.frameCount;complete(stats);return;}
        try
        {
            AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat,request=>
            {
                stats.callbackFrame=Time.frameCount;stats.callbackArrivalAge=HunterArrivalAge(spell);
                if(request.hasError){stats.readbackError="AsyncGPUReadback request failed";complete(stats);return;}
                var source=request.GetData<float>();stats.rawPixels=new float[source.Length];source.CopyTo(stats.rawPixels);stats.readbackSucceeded=true;complete(stats);
            });
        }
        catch(Exception error){stats.callbackFrame=Time.frameCount;stats.callbackArrivalAge=HunterArrivalAge(spell);stats.readbackError=error.Message;complete(stats);}
    }
    static void CalculatePixelStats(PixelStats stats)
    {
        if(stats==null||!stats.readbackSucceeded||stats.rawPixels==null)return;
        var pixels=stats.rawPixels;stats.minAlphaX=stats.width;stats.minAlphaY=stats.height;stats.minColorX=stats.width;stats.minColorY=stats.height;
        stats.maxAlphaX=stats.maxAlphaY=stats.maxColorX=stats.maxColorY=-1;
        for(int i=0;i+3<pixels.Length;i+=4)
        {
            int pixel=i/4,x=pixel%stats.width,y=pixel/stats.width;float r=pixels[i],g=pixels[i+1],b=pixels[i+2],a=pixels[i+3];
            float rgb=Mathf.Max(r,Mathf.Max(g,b));stats.maxAlpha=Mathf.Max(stats.maxAlpha,a);stats.maxRgb=Mathf.Max(stats.maxRgb,rgb);
            if(a>.002f){stats.alphaPixels++;stats.minAlphaX=Mathf.Min(stats.minAlphaX,x);stats.minAlphaY=Mathf.Min(stats.minAlphaY,y);stats.maxAlphaX=Mathf.Max(stats.maxAlphaX,x);stats.maxAlphaY=Mathf.Max(stats.maxAlphaY,y);}
            if(rgb>.002f){stats.colorPixels++;stats.minColorX=Mathf.Min(stats.minColorX,x);stats.minColorY=Mathf.Min(stats.minColorY,y);stats.maxColorX=Mathf.Max(stats.maxColorX,x);stats.maxColorY=Mathf.Max(stats.maxColorY,y);}
        }
        if(stats.alphaPixels==0){stats.minAlphaX=stats.minAlphaY=stats.maxAlphaX=stats.maxAlphaY=-1;}
        if(stats.colorPixels==0){stats.minColorX=stats.minColorY=stats.maxColorX=stats.maxColorY=-1;}
        stats.rawPixels=null;
    }
    void RecordHunterAgeCandidate(string eventKey,NativeSuperSpellPlayback spell,float arrivalAge,int targetAgeMs)
    {
        if(hunterAgeCandidates.Count>=MaxHunterAgeCandidates){droppedHunterAgeCandidates++;return;}
        hunterAgeCandidates.Add(new HunterAgeCandidate{eventKey=eventKey,frame=Time.frameCount,eventType=(int)Event.current.type,
            targetAgeMs=targetAgeMs,realtime=Time.realtimeSinceStartup-started,flowTime=spell.Flow.Time,arrivalAge=arrivalAge});
    }
    void RecordHunterSampleWindowMiss(string eventKey,int sampleIndex,float arrivalAge)
    {
        int targetAgeMs=Mathf.RoundToInt(HunterArrivalSampleAges[sampleIndex]*1000),frame=Time.frameCount;
        hunterSampleWindowMisses.Add(new HunterSampleWindowMiss{eventKey=eventKey,targetAgeMs=targetAgeMs,
            lowerAgeMs=targetAgeMs-HunterSampleToleranceMs,upperAgeMs=targetAgeMs+HunterSampleToleranceMs,
            frame=frame,firstObservedAge=arrivalAge,reason="first Repaint arrived after the target tolerance window"});
    }
    void CompleteHunterRenderReadback(RenderDiagnostic diagnostic,bool emission,PixelStats stats)
    {
        if(emission)diagnostic.emission=stats;else diagnostic.transmission=stats;
        diagnostic.pendingReadbacks--;
        if(diagnostic.pendingReadbacks<=0)
        {
            diagnostic.pendingReadbacks=0;diagnostic.readbacksComplete=true;
            diagnostic.readbackSucceeded=diagnostic.emission!=null&&diagnostic.emission.readbackSucceeded&&
                diagnostic.transmission!=null&&diagnostic.transmission.readbackSucceeded;
        }
    }
    void CaptureSuperSpellRender(string eventKey,NativeSuperSpellPlayback spell,RenderTexture emission,RenderTexture transmission)
    {
        if(role!="driver"||cast==null||!ReferenceEquals(spell,cast)||Event.current.type!=EventType.Repaint)return;
        if(Hunter||Druid||Priest||Warlock)
        {
            int bucket=Mathf.Clamp(Mathf.FloorToInt(spell.Time*4),0,20);string sample=spell.CardId+":"+bucket;
            if(positionDiagnosticSamples.Add(sample))Note("native position age="+spell.Time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " "+spell.CardId+" "+spell.PositionDiagnostics());
        }
        if(!Hunter)return;
        string stage=null;int sampleIndex=-1,targetAgeMs=-1;float arrivalAge=-1;
        if(nextHunterArrivalSample<HunterArrivalSampleAges.Length&&spell.Flow.FirstArrivalTime.HasValue)
        {
            arrivalAge=spell.Flow.Time-spell.Flow.FirstArrivalTime.Value;
            int targetAgeMsForWindow=Mathf.RoundToInt(HunterArrivalSampleAges[nextHunterArrivalSample]*1000);
            int lowerAgeMs=targetAgeMsForWindow-HunterSampleToleranceMs,upperAgeMs=targetAgeMsForWindow+HunterSampleToleranceMs;
            float lowerAge=lowerAgeMs/1000f,upperAge=upperAgeMs/1000f;
            RecordHunterAgeCandidate(eventKey,spell,arrivalAge,targetAgeMsForWindow);
            if(arrivalAge>upperAge)
            {
                RecordHunterSampleWindowMiss(eventKey,nextHunterArrivalSample,arrivalAge);
                nextHunterArrivalSample++;
            }
            else if(arrivalAge>=lowerAge)
            {
                sampleIndex=nextHunterArrivalSample++;targetAgeMs=targetAgeMsForWindow;
                stage=sampleIndex==0?"arrival":"arrival-"+targetAgeMs.ToString("D3",System.Globalization.CultureInfo.InvariantCulture)+"ms";
                if(sampleIndex==0)capturedRenderArrival=true;
            }
        }
        if(stage==null&&hunterFlightMid&&!capturedRenderMid){capturedRenderMid=true;stage="mid";}
        else if(stage==null&&hunterArrivalCapture&&!capturedRenderArrival){capturedRenderArrival=true;stage="arrival";}
        if(stage==null)stage=CaptureHunterOffscreenImpact(eventKey,spell);
        if(stage==null)return;
        if(arrivalAge<0)arrivalAge=HunterArrivalAge(spell);
        pendingSourceDrawStage=stage;pendingTrailSampleIndex=sampleIndex;pendingTrailTargetAgeMs=targetAgeMs;pendingTrailArrivalAge=arrivalAge;
        pendingTrailRoiAvailable=false;
        if(sampleIndex>=0)
        {
            if(sampleIndex==0)hunterArrivalTrailRoiAvailable=TryTrailScreenRoi(spell,Screen.width,Screen.height,out hunterArrivalTrailRoi);
            pendingTrailRoi=hunterArrivalTrailRoi;pendingTrailRoiAvailable=hunterArrivalTrailRoiAvailable;
            var flight=spell.Flow.Flights.FirstOrDefault(item=>item!=null&&item.Path!=null&&item.Effect!=null);
            if(flight!=null)CaptureFlightDiagnostic("trail-"+stage,flight,eventKey,(int)Event.current.type,sampleIndex,targetAgeMs,arrivalAge);
        }
        var diagnostic=new RenderDiagnostic{stage=stage,cardId=spell.CardId,eventKey=eventKey,frame=Time.frameCount,eventType=(int)Event.current.type,
            screenWidth=Screen.width,screenHeight=Screen.height,sampleIndex=sampleIndex,targetAgeMs=targetAgeMs,sampleTime=Time.realtimeSinceStartup-started,
            flowTime=spell.Flow.Time,arrivalAge=arrivalAge,arrivalTime=spell.Flow.FirstArrivalTime.HasValue?spell.Flow.FirstArrivalTime.Value:-1,
            targetAgeErrorMs=(arrivalAge-targetAgeMs/1000f)*1000f};
        diagnostic.pendingReadbacks=2;pendingHunterRenderDiagnostics.Add(diagnostic);
        if(sampleIndex>=0)hunterTrailSamples.Add(new TrailSampleIndex{eventKey=eventKey,sampleIndex=sampleIndex,targetAgeMs=targetAgeMs,
            frame=diagnostic.frame,eventType=diagnostic.eventType,actualArrivalAge=arrivalAge,targetAgeErrorMs=diagnostic.targetAgeErrorMs,
            flowTimeAtRequest=diagnostic.flowTime,withinTargetAgeTolerance=Mathf.Abs(diagnostic.targetAgeErrorMs)<=HunterSampleToleranceMs});
        RequestHunterPixelReadback(emission,arrivalAge,spell,value=>CompleteHunterRenderReadback(diagnostic,true,value));
        RequestHunterPixelReadback(transmission,arrivalAge,spell,value=>CompleteHunterRenderReadback(diagnostic,false,value));
        Note("Hunter RenderAffine "+stage+" flow="+diagnostic.flowTime.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
            " targetAgeErrorMs="+diagnostic.targetAgeErrorMs.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" readback=async");
    }
    void CaptureSuperSpellDraw(string eventKey,NativeSuperSpellPlayback spell,Rect rect,Matrix4x4 matrix,int width,int height,float age,bool passesBound)
    {
        if(!Hunter||role!="driver"||cast==null||!ReferenceEquals(spell,cast)||String.IsNullOrEmpty(pendingSourceDrawStage)||Event.current.type!=EventType.Repaint)return;
        var corners=new[]{matrix.MultiplyPoint3x4(new Vector3(rect.xMin,rect.yMin,0)),matrix.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMin,0)),
            matrix.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMax,0)),matrix.MultiplyPoint3x4(new Vector3(rect.xMin,rect.yMax,0))};
        var screen=Rect.MinMaxRect(corners.Min(point=>point.x),corners.Min(point=>point.y),corners.Max(point=>point.x),corners.Max(point=>point.y));
        var row=new SourceDrawDiagnostic{stage=pendingSourceDrawStage,cardId=spell.CardId,eventKey=eventKey,frame=Time.frameCount,eventType=(int)Event.current.type,
            screenWidth=width,screenHeight=height,logicalRect=new Vector4(rect.x,rect.y,rect.width,rect.height),screenRect=new Vector4(screen.x,screen.y,screen.width,screen.height),
            guiMatrix=new[]{matrix.m00,matrix.m01,matrix.m02,matrix.m03,matrix.m10,matrix.m11,matrix.m12,matrix.m13,
                matrix.m20,matrix.m21,matrix.m22,matrix.m23,matrix.m30,matrix.m31,matrix.m32,matrix.m33},
            sampleTime=Time.realtimeSinceStartup-started,effectAge=age,flowTime=spell.Flow.Time,castOffset=spell.CastOffset,passesBound=passesBound,
            sampleIndex=pendingTrailSampleIndex,targetAgeMs=pendingTrailTargetAgeMs,arrivalAge=pendingTrailArrivalAge,
            arrivalTime=spell.Flow.FirstArrivalTime.HasValue?spell.Flow.FirstArrivalTime.Value:-1,
            trailRoiAvailable=pendingTrailRoiAvailable,trailRoi=pendingTrailRoiAvailable?new Vector4(pendingTrailRoi.x,pendingTrailRoi.y,pendingTrailRoi.width,pendingTrailRoi.height):Vector4.zero};
        string drawFile="hunter-gui-draw-"+row.stage+(row.sampleIndex<0?"":"-frame-"+row.frame)+".json";
        File.WriteAllText(Path.Combine(folder,drawFile),JsonUtility.ToJson(row,true));
        if(row.sampleIndex>=0)
        {
            var sample=hunterTrailSamples.FirstOrDefault(item=>item.sampleIndex==row.sampleIndex&&item.eventKey==row.eventKey);
            if(sample!=null){sample.drawFrame=row.frame;sample.guiSameFrame=sample.frame==row.frame;sample.guiPassesBound=passesBound;}
        }
        Note("Hunter GUI draw "+row.stage+" frame="+row.frame+" key="+row.eventKey+" arrivalAge="+row.arrivalAge.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" passesBound="+passesBound);pendingSourceDrawStage=null;
        // 爆炸尾段仍可见时，missile对象已可能回收；Impact截图不再访问flight。
        if(row.sampleIndex<0&&!row.stage.StartsWith("impact-particle-",StringComparison.Ordinal))
        {
            var flights=spell.Flow==null?new NativeMissileFlight[0]:spell.Flow.Flights.Where(flight=>flight!=null&&flight.Effect!=null).ToArray();
            for(int i=0;i<flights.Length;i++)CaptureFlightDiagnostic("repaint-"+row.stage+"-frame-"+row.frame+"-flight-"+i,flights[i],row.eventKey,row.eventType,row.sampleIndex,row.targetAgeMs,row.arrivalAge);
        }
        pendingTrailSampleIndex=-1;pendingTrailTargetAgeMs=-1;pendingTrailArrivalAge=-1;pendingTrailRoiAvailable=false;
        StartCoroutine(CaptureFinalHunterScreen(row));
    }
    bool TryTrailScreenRoi(NativeSuperSpellPlayback spell,int width,int height,out RectInt roi)
    {
        roi=new RectInt();
        var flight=spell==null||spell.Flow==null?null:spell.Flow.Flights.FirstOrDefault(item=>item!=null&&item.Path!=null&&item.Effect!=null);
        var camera=flight?.Effect?.PreviewCamera;
        var trail=flight?.Effect?.Nodes.Select(node=>node==null?null:node.GetComponent<TrailRenderer>()).FirstOrDefault(item=>item!=null&&item.gameObject.name=="Arrow Trail");
        if(camera==null||trail==null||width<=0||height<=0)return false;
        var bounds=trail.bounds;var corners=new Vector3[8];
        for(int i=0;i<8;i++)corners[i]=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z);
        float minX=width,minY=height,maxX=0,maxY=0;int projected=0;
        foreach(var corner in corners)
        {
            var point=camera.WorldToViewportPoint(corner);if(point.z<=0)continue;
            float x=point.x*width,y=point.y*height;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);projected++;
        }
        if(projected==0)return false;
        const int padding=12;
        int x0=Mathf.Clamp(Mathf.FloorToInt(minX)-padding,0,width-1),y0=Mathf.Clamp(Mathf.FloorToInt(minY)-padding,0,height-1);
        int x1=Mathf.Clamp(Mathf.CeilToInt(maxX)+padding,0,width),y1=Mathf.Clamp(Mathf.CeilToInt(maxY)+padding,0,height);
        roi=new RectInt(x0,y0,Mathf.Max(1,x1-x0),Mathf.Max(1,y1-y0));return true;
    }
    System.Collections.IEnumerator CaptureFinalHunterScreen(SourceDrawDiagnostic repaint)
    {
        yield return new WaitForEndOfFrame();
        if(!Hunter||repaint==null||Screen.width<=0||Screen.height<=0)yield break;
        var target=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
        var capture=new PendingHunterScreen{repaint=repaint,width=Screen.width,height=Screen.height,requestFrame=Time.frameCount,
            requestTime=Time.realtimeSinceStartup-started,rowsStartAtTop=SystemInfo.graphicsUVStartsAtTop};pendingHunterScreens.Add(capture);
        var sample=repaint.sampleIndex<0?null:hunterTrailSamples.FirstOrDefault(item=>item.sampleIndex==repaint.sampleIndex&&item.eventKey==repaint.eventKey);
        if(sample!=null){sample.screenRequestFrame=capture.requestFrame;sample.screenSameFrame=sample.frame==capture.requestFrame;}
        try
        {
            if(!SystemInfo.supportsAsyncGPUReadback){capture.error="AsyncGPUReadback is unsupported";capture.done=true;target.Release();Destroy(target);}
            else
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32,request=>
                {
                    capture.callbackFrame=Time.frameCount;capture.callbackTime=Time.realtimeSinceStartup-started;capture.callbackArrivalAge=HunterArrivalAge(cast);
                    if(request.hasError)capture.error="AsyncGPUReadback request failed";
                    else
                    {
                        var source=request.GetData<byte>();capture.rgba=new byte[source.Length];source.CopyTo(capture.rgba);
                    }
                    capture.done=true;target.Release();Destroy(target);
                });
                capture.readbackRequested=true;
            }
        }
        catch(Exception error){capture.error=error.Message;capture.done=true;target.Release();Destroy(target);}
        if(repaint.sampleIndex>=0)
        {
            var meshDiagnostic=CaptureHunterTrailMesh(repaint,capture,sample);
            if(sample!=null)
            {
                sample.trailMeshFrame=meshDiagnostic.bakeFrame;sample.trailMeshVertices=meshDiagnostic.vertexCount;sample.trailMeshTriangles=meshDiagnostic.triangleCount;
                sample.trailMeshSameFrame=meshDiagnostic.sameRepaintFrame;sample.trailMeshBaked=meshDiagnostic.bakeSucceeded;
                sample.trailMeshError=meshDiagnostic.error;
            }
            string meshPath=Path.Combine(folder,"hunter-trail-mesh-"+repaint.stage+"-frame-"+repaint.frame+".json");
            try{File.WriteAllText(meshPath,JsonUtility.ToJson(meshDiagnostic,true));if(sample!=null)sample.trailMeshDiagnosticWritten=true;}
            catch(Exception error){if(sample!=null){sample.trailMeshBaked=false;sample.trailMeshError="diagnostic write failed: "+error.Message;}}
        }
    }
    HunterTrailMeshDiagnostic CaptureHunterTrailMesh(SourceDrawDiagnostic repaint,PendingHunterScreen capture,TrailSampleIndex sample)
    {
        var diagnostic=new HunterTrailMeshDiagnostic{stage=repaint.stage,eventKey=repaint.eventKey,sampleIndex=repaint.sampleIndex,targetAgeMs=repaint.targetAgeMs,
            repaintFrame=repaint.frame,screenRequestFrame=capture.requestFrame,arrivalAge=repaint.arrivalAge,flowTime=repaint.flowTime,
            screenReadbackRequested=capture.readbackRequested,materialState=new HunterRuntimeMaterialSnapshot[0]};
        if(sample==null||sample.sampleIndex!=repaint.sampleIndex||sample.eventKey!=repaint.eventKey)
        {diagnostic.error="matching target sample record not found";return diagnostic;}
        if(!capture.readbackRequested){diagnostic.error="same-frame screen readback request was not issued";return diagnostic;}
        if(capture.repaint!=repaint||capture.requestFrame!=repaint.frame||Time.frameCount!=repaint.frame)
        {diagnostic.error="WaitForEndOfFrame or screen request did not remain on the repaint frame";return diagnostic;}
        var flight=cast==null||cast.Flow==null?null:cast.Flow.Flights.FirstOrDefault(item=>item!=null&&item.Path!=null&&item.Effect!=null);
        var effect=flight==null?null:flight.Effect;
        if(effect==null){diagnostic.error="Hunter flight effect is unavailable";return diagnostic;}
        var trailNode=effect.Nodes.Select((node,index)=>new{node,index}).FirstOrDefault(item=>item.node!=null&&item.node.name=="Arrow Trail"&&item.node.GetComponent<TrailRenderer>()!=null);
        var trail=trailNode==null?null:trailNode.node.GetComponent<TrailRenderer>();
        if(trail==null){diagnostic.error="Arrow Trail TrailRenderer is unavailable";return diagnostic;}
        var camera=effect.PreviewCamera;
        if(camera==null){diagnostic.error="Hunter effect capture camera is unavailable";return diagnostic;}
        diagnostic.trailObject=trail.gameObject.name;diagnostic.camera=camera.name;diagnostic.trailEnabled=trail.enabled;
        diagnostic.trailEmitting=trail.emitting;diagnostic.trailPositionCount=trail.positionCount;diagnostic.trailTime=trail.time;diagnostic.trailWidth=trail.widthMultiplier;
        try
        {
            var sourceMaterials=effect.Program.nodes[trailNode.index].materials??new NativeMaterial[0];
            diagnostic.materialState=trail.sharedMaterials.Select((material,index)=>material==null?null:HunterRuntimeMaterialSnapshot.Capture(material,index<sourceMaterials.Length?sourceMaterials[index]:null)).ToArray();
        }
        catch(Exception error){diagnostic.error="Trail material snapshot failed: "+error.Message;return diagnostic;}
        var baked=new Mesh{name="Hunter target-frame TrailRenderer diagnostic"};
        try
        {
            diagnostic.bakeFrame=Time.frameCount;
            if(diagnostic.bakeFrame!=repaint.frame){diagnostic.error="BakeMesh frame no longer matches the selected repaint";return diagnostic;}
            diagnostic.bakeAttempted=true;
            trail.BakeMesh(baked,camera,true);diagnostic.bakeSucceeded=true;diagnostic.vertexCount=baked.vertexCount;
            diagnostic.triangleCount=baked.triangles.Length/3;var colors=baked.colors;diagnostic.vertexColorCount=colors.Length;
            diagnostic.hasVertexColors=colors.Length==baked.vertexCount&&colors.Length>0;
            if(colors.Length>0){diagnostic.vertexAlphaMin=colors.Min(color=>color.a);diagnostic.vertexAlphaMax=colors.Max(color=>color.a);}
            var bounds=baked.bounds;diagnostic.meshBoundsCenter=bounds.center;diagnostic.meshBoundsSize=bounds.size;
            diagnostic.meshBoundsMin=bounds.min;diagnostic.meshBoundsMax=bounds.max;
        }
        catch(Exception error){diagnostic.error=error.Message;}
        finally{AssetRegistry.ReleaseTransient(baked);}
        diagnostic.sameRepaintFrame=sample.eventKey==repaint.eventKey&&sample.sampleIndex==repaint.sampleIndex&&capture.repaint==repaint&&
            capture.requestFrame==repaint.frame&&diagnostic.bakeFrame==repaint.frame&&Time.frameCount==repaint.frame;
        if(!diagnostic.sameRepaintFrame&&String.IsNullOrEmpty(diagnostic.error))diagnostic.error="Trail mesh capture did not remain on the selected repaint frame";
        return diagnostic;
    }
    void ProcessHunterScreen(PendingHunterScreen capture)
    {
        var repaint=capture.repaint;float ageError=(repaint.arrivalAge-repaint.targetAgeMs/1000f)*1000f;
        var row=new FinalScreenDiagnostic{stage=repaint.stage,eventKey=repaint.eventKey,image="hunter-screen-"+repaint.stage+"-frame-"+repaint.frame+".png",
            repaintFrame=repaint.frame,captureFrame=capture.requestFrame,readbackCallbackFrame=capture.callbackFrame,width=capture.width,height=capture.height,eventType=repaint.eventType,
            sameFrame=capture.requestFrame==repaint.frame,sampleIndex=repaint.sampleIndex,targetAgeMs=repaint.targetAgeMs,sampleTime=capture.requestTime,callbackTime=capture.callbackTime,
            arrivalAge=repaint.arrivalAge,callbackArrivalAge=capture.callbackArrivalAge,arrivalTime=repaint.arrivalTime,targetAgeErrorMs=ageError,
            withinTargetAgeTolerance=Mathf.Abs(ageError)<=HunterSampleToleranceMs,trailRoi=repaint.trailRoi,trailRoiAvailable=repaint.trailRoiAvailable,
            readbackRowsStartAtTop=capture.rowsStartAtTop,pngRowsFlipped=capture.rowsStartAtTop,
            readbackSucceeded=capture.rgba!=null&&capture.rgba.Length==capture.width*capture.height*4,error=capture.error};
        var sample=repaint.sampleIndex<0?null:hunterTrailSamples.FirstOrDefault(item=>item.sampleIndex==repaint.sampleIndex&&item.eventKey==repaint.eventKey);
        if(capture.rgba!=null&&capture.rgba.Length==capture.width*capture.height*4)
        {
            var pixels=new Texture2D(capture.width,capture.height,TextureFormat.RGBA32,false,false);
            try
            {
                // ScreenCapture的D3D读回首行在顶部；Texture2D原始数据首行在底部。
                // 仅归一化诊断PNG及其ROI，不修改实际屏幕/生产RT。
                byte[] pngPixels=capture.rgba;
                if(capture.rowsStartAtTop)
                {
                    pngPixels=new byte[capture.rgba.Length];int stride=capture.width*4;
                    for(int y=0;y<capture.height;y++)Buffer.BlockCopy(capture.rgba,y*stride,pngPixels,(capture.height-1-y)*stride,stride);
                }
                pixels.LoadRawTextureData(pngPixels);pixels.Apply(false,false);File.WriteAllBytes(Path.Combine(folder,row.image),pixels.EncodeToPNG());
                if(repaint.sampleIndex>=0&&repaint.trailRoiAvailable)
                {
                    int x=Mathf.Clamp(Mathf.RoundToInt(repaint.trailRoi.x),0,capture.width-1),y=Mathf.Clamp(Mathf.RoundToInt(repaint.trailRoi.y),0,capture.height-1);
                    int w=Mathf.Clamp(Mathf.RoundToInt(repaint.trailRoi.z),1,capture.width-x),h=Mathf.Clamp(Mathf.RoundToInt(repaint.trailRoi.w),1,capture.height-y);
                    var screenPixels=pixels.GetPixels32();var roiPixelsData=new Color32[w*h];
                    for(int rowIndex=0;rowIndex<h;rowIndex++)Array.Copy(screenPixels,(y+rowIndex)*capture.width+x,roiPixelsData,rowIndex*w,w);
                    var roiTexture=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
                    try
                    {
                        roiTexture.SetPixels32(roiPixelsData);roiTexture.Apply(false,false);Directory.CreateDirectory(Path.Combine(folder,"hunter-trail-roi"));
                        row.roiImage=Path.Combine("hunter-trail-roi","hunter-screen-"+repaint.stage+"-frame-"+repaint.frame+"-roi.png");
                        File.WriteAllBytes(Path.Combine(folder,row.roiImage),roiTexture.EncodeToPNG());
                    }
                    finally{Destroy(roiTexture);}
                    row.roiPixels=roiPixelsData.Length;
                    if(repaint.sampleIndex==0)hunterArrivalRoiBaseline=roiPixelsData;
                    else if(hunterArrivalRoiBaseline!=null&&hunterArrivalRoiBaseline.Length==roiPixelsData.Length)
                    {
                        row.roiComparedToArrivalZero=true;row.roiComparedPixels=roiPixelsData.Length;long totalDelta=0;
                        for(int i=0;i<roiPixelsData.Length;i++)
                        {
                            var a=hunterArrivalRoiBaseline[i];var b=roiPixelsData[i];int delta=Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
                            totalDelta+=delta;row.roiMaxAbsDelta=Mathf.Max(row.roiMaxAbsDelta,delta);if(delta>10)row.roiChangedPixels++;
                        }
                        row.roiMeanAbsDelta=row.roiComparedPixels==0?0:(float)totalDelta/row.roiComparedPixels;
                    }
                }
            }
            finally{Destroy(pixels);}
        }
        if(sample!=null){sample.screenCallbackFrame=capture.callbackFrame;sample.screenReadbackSucceeded=row.readbackSucceeded;sample.screenSameFrame=row.sameFrame;}
        File.WriteAllText(Path.Combine(folder,"hunter-screen-"+repaint.stage+"-frame-"+repaint.frame+".json"),JsonUtility.ToJson(row,true));capture.processed=true;
        Note("Hunter final screen requestFrame="+row.captureFrame+" callbackFrame="+row.readbackCallbackFrame+" repaintFrame="+row.repaintFrame+
            " sameFrame="+row.sameFrame+" key="+row.eventKey+" sourceArrivalAge="+row.arrivalAge.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
            " callbackArrivalAge="+row.callbackArrivalAge.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" roi="+row.trailRoi+
            " changed="+row.roiChangedPixels+"/"+row.roiComparedPixels);
    }
    void PumpHunterDiagnostics()
    {
        if(!Hunter||role!="driver"||hunterDiagnosticFinished)return;
        bool processReady=nextHunterArrivalSample>=HunterArrivalSampleAges.Length||hunterDiagnosticFinalizing;
        if(processReady)
        {
            foreach(var impact in pendingHunterImpacts.Where(item=>item.done&&!item.written))
            {
                var stats=impact.pixels;
                if(stats!=null&&stats.readbackSucceeded&&stats.rawPixels!=null)
                {
                    var colors=new Color[stats.width*stats.height];var raw=stats.rawPixels;
                    for(int i=0;i<colors.Length;i++)colors[i]=new Color(raw[i*4],raw[i*4+1],raw[i*4+2],raw[i*4+3]);
                    var texture=new Texture2D(stats.width,stats.height,TextureFormat.RGBA32,false);
                    try{texture.SetPixels(colors);texture.Apply(false,false);File.WriteAllBytes(Path.Combine(folder,impact.image),texture.EncodeToPNG());}
                    finally{Destroy(texture);}
                    CalculatePixelStats(stats);
                }
                File.WriteAllText(Path.Combine(folder,"hunter-offscreen-"+impact.stage+".json"),JsonUtility.ToJson(impact,true));impact.written=true;
            }
            foreach(var flight in pendingHunterFlights.Where(item=>!item.written))
            {File.WriteAllText(flight.path,JsonUtility.ToJson(flight.diagnostic,true));flight.written=true;}
            foreach(var diagnostic in pendingHunterRenderDiagnostics.Where(item=>item.readbacksComplete&&!item.written))
            {
                CalculatePixelStats(diagnostic.emission);CalculatePixelStats(diagnostic.transmission);
                diagnostic.written=true;var sample=hunterTrailSamples.FirstOrDefault(item=>item.sampleIndex==diagnostic.sampleIndex&&item.eventKey==diagnostic.eventKey);
                if(sample!=null)sample.renderReadbackSucceeded=diagnostic.readbackSucceeded;
                File.WriteAllText(Path.Combine(folder,"hunter-rendertexture-"+diagnostic.stage+".json"),JsonUtility.ToJson(diagnostic,true));
            }
            foreach(var capture in pendingHunterScreens.Where(item=>item.done&&!item.processed))ProcessHunterScreen(capture);
        }
        if(!hunterDiagnosticFinalizing)return;
        bool pending=pendingHunterRenderDiagnostics.Any(item=>!item.readbacksComplete)||pendingHunterScreens.Any(item=>!item.done)||pendingHunterImpacts.Any(item=>!item.done);
        bool timedOut=pending&&Time.realtimeSinceStartup>=hunterDiagnosticDeadline;
        if(pending&&!timedOut)return;
        if(timedOut)
        {
            foreach(var impact in pendingHunterImpacts.Where(item=>!item.done))
            {
                impact.pixels=new PixelStats{readbackSucceeded=false,readbackError="AsyncGPUReadback timed out"};impact.done=true;
                File.WriteAllText(Path.Combine(folder,"hunter-offscreen-"+impact.stage+".json"),JsonUtility.ToJson(impact,true));impact.written=true;
            }
            foreach(var diagnostic in pendingHunterRenderDiagnostics.Where(item=>!item.readbacksComplete))
            {diagnostic.readbacksComplete=true;diagnostic.readbackSucceeded=false;diagnostic.error="AsyncGPUReadback timed out";}
            foreach(var capture in pendingHunterScreens.Where(item=>!item.done)){capture.done=true;capture.error="AsyncGPUReadback timed out";}
            foreach(var diagnostic in pendingHunterRenderDiagnostics.Where(item=>!item.written))
            {diagnostic.written=true;File.WriteAllText(Path.Combine(folder,"hunter-rendertexture-"+diagnostic.stage+".json"),JsonUtility.ToJson(diagnostic,true));}
            foreach(var capture in pendingHunterScreens.Where(item=>!item.processed))ProcessHunterScreen(capture);
        }
        bool impactSamplingComplete=WriteHunterImpactSampleSummary();
        WriteHunterSampleSummary();var summary=JsonUtility.FromJson<TrailSampleSummary>(File.ReadAllText(Path.Combine(folder,"hunter-trail-sample-summary.json")));
        if(summary==null||!summary.complete){finishResult="FAIL: Hunter trail diagnostic sampling contract incomplete";finishExit=1;}
        PublishReportResult(finishResult);hunterDiagnosticFinished=true;
        Debug.Log("FIREBLAST FORMAL "+finishResult+" checks="+checks+" hunterTrailSamplingComplete="+(summary!=null&&summary.complete)+
            " hunterImpactSamplingComplete="+impactSamplingComplete);Application.Quit(finishExit);
    }
    bool WriteHunterImpactSampleSummary()
    {
        var expected=HunterImpactSampleAges.Select(age=>Mathf.RoundToInt(age*1000)).ToArray();
        var captured=pendingHunterImpacts.Where(sample=>sample.done&&sample.pixels!=null&&sample.pixels.readbackSucceeded)
            .Where(sample=>Mathf.Abs(sample.targetAgeErrorMs)<=HunterImpactSampleToleranceMs).Select(sample=>sample.targetParticleAgeMs).ToArray();
        var missing=expected.Except(captured).ToArray();
        var summary=new HunterImpactSampleSummary{expectedTargetParticleAgesMs=expected,capturedTargetParticleAgesMs=captured,
            missingTargetParticleAgesMs=missing,capturedCount=captured.Length,toleranceMs=HunterImpactSampleToleranceMs,
            complete=missing.Length==0&&captured.Length==expected.Length&&hunterImpactSampleMisses.Count==0,
            lastObservedParticleAge=hunterImpactLastObservedParticleAge,lastObservedParticleCount=hunterImpactLastObservedParticleCount,
            lastUnavailableReason=missing.Length==0?"":hunterImpactLastUnavailableReason,samples=pendingHunterImpacts.ToArray(),misses=hunterImpactSampleMisses.ToArray()};
        File.WriteAllText(Path.Combine(folder,"hunter-impact-sample-summary.json"),JsonUtility.ToJson(summary,true));return summary.complete;
    }
    void WriteHunterSampleSummary()
    {
        var expected=HunterArrivalSampleAges.Select(age=>Mathf.RoundToInt(age*1000)).ToArray();
        var missing=expected.Where(age=>!hunterTrailSamples.Any(sample=>sample.targetAgeMs==age)).ToArray();
        foreach(int age in missing.Where(age=>!hunterSampleWindowMisses.Any(item=>item.targetAgeMs==age)))
            hunterSampleWindowMisses.Add(new HunterSampleWindowMiss{eventKey=hunterAgeCandidates.FirstOrDefault()?.eventKey??"",targetAgeMs=age,
                lowerAgeMs=age-HunterSampleToleranceMs,upperAgeMs=age+HunterSampleToleranceMs,frame=-1,firstObservedAge=-1,
                reason="no Repaint candidate was observed in the target tolerance window before finalization"});
        var summary=new TrailSampleSummary{expectedTargetAgesMs=expected,toleranceMs=HunterSampleToleranceMs,samples=hunterTrailSamples.ToArray(),capturedCount=hunterTrailSamples.Count,
            missingTargetAgesMs=missing,droppedAgeCandidates=droppedHunterAgeCandidates,ageCandidates=hunterAgeCandidates.ToArray(),
            sampleWindowMisses=hunterSampleWindowMisses.ToArray()};
        summary.complete=summary.capturedCount==expected.Length&&summary.missingTargetAgesMs.Length==0&&summary.droppedAgeCandidates==0&&
            summary.samples.Select(sample=>sample.sampleIndex).Distinct().Count()==expected.Length&&summary.samples.Select(sample=>sample.eventKey).Distinct().Count()==1&&
            summary.samples.All(sample=>sample.withinTargetAgeTolerance&&sample.guiSameFrame&&sample.guiPassesBound&&sample.screenSameFrame&&
                sample.renderReadbackSucceeded&&sample.screenReadbackSucceeded&&sample.trailMeshSameFrame&&sample.trailMeshBaked&&sample.trailMeshDiagnosticWritten);
        File.WriteAllText(Path.Combine(folder,"hunter-trail-sample-summary.json"),JsonUtility.ToJson(summary,true));
    }
    void CaptureFlightDiagnostic(string stage,NativeMissileFlight flight,string eventKey="",int eventType=-1,int sampleIndex=-1,int targetAgeMs=-1,float arrivalAge=-1)
    {
        if(flight==null||flight.Effect==null)return;
        var effect=flight.Effect;var camera=effect.PreviewCamera;
        // Flights保留完成记录；Effect引用非null不代表Unity对象仍存活。
        if(effect.Root==null||camera==null){Note("Hunter flight visual diagnostic unavailable after release: "+stage);return;}
        var visuals=effect.Nodes.Select((node,index)=>new{node,index}).Where(item=>item.node!=null).SelectMany(item=>item.node.GetComponents<Renderer>().Select(renderer=>new{renderer,item.index})).Distinct().Select(item=>
        {
            var renderer=item.renderer;
            var owner=renderer.gameObject;var particle=owner.GetComponent<ParticleSystem>();var trail=owner.GetComponent<TrailRenderer>();
            var bounds=renderer.bounds;var materialText=String.Join(";",renderer.sharedMaterials.Select(MaterialSnapshot).ToArray());
            var sourceMats=effect.Program.nodes[item.index].materials??new NativeMaterial[0];
            var materialState=renderer.sharedMaterials.Select((material,materialIndex)=>HunterRuntimeMaterialSnapshot.Capture(material,
                materialIndex<sourceMats.Length?sourceMats[materialIndex]:null)).ToArray();
            var sourceMaterialText=String.Join(";",sourceMats.Select((source,materialIndex)=>source.name+" shader="+source.shader+
                " sourceTextures="+String.Join(",",(source.textures??new NativeTexture[0]).Select(t=>t.name+"="+t.path).ToArray())+
                " runtimeTextures="+(materialIndex<renderer.sharedMaterials.Length?String.Join(",",(source.textures??new NativeTexture[0]).Select(t=>t.name+"="+
                    TextureSnapshot(renderer.sharedMaterials[materialIndex]==null?null:renderer.sharedMaterials[materialIndex].GetTexture(t.name))).ToArray()):"<missing runtime material>")).ToArray());
            var worldPoints=new Vector3[trail==null?0:trail.positionCount];if(trail!=null&&worldPoints.Length>0)trail.GetPositions(worldPoints);
            int meshVertices=0,meshTriangles=0;bool trailMeshBaked=false;float vertexAlphaMin=0,vertexAlphaMax=0;
            Vector3 vertexColorMin=Vector3.zero,vertexColorMax=Vector3.zero;Vector2 uvMin=Vector2.zero,uvMax=Vector2.zero;
            if(trail!=null&&sampleIndex<0)
            {
                var baked=new Mesh();
                try
                {
                    trail.BakeMesh(baked,camera,true);trailMeshBaked=true;meshVertices=baked.vertexCount;meshTriangles=baked.triangles.Length/3;
                    var colors=baked.colors;
                    if(colors.Length>0)
                    {
                        vertexAlphaMin=colors.Min(color=>color.a);vertexAlphaMax=colors.Max(color=>color.a);
                        vertexColorMin=new Vector3(colors.Min(color=>color.r),colors.Min(color=>color.g),colors.Min(color=>color.b));
                        vertexColorMax=new Vector3(colors.Max(color=>color.r),colors.Max(color=>color.g),colors.Max(color=>color.b));
                    }
                    var uv=baked.uv;
                    if(uv.Length>0){uvMin=new Vector2(uv.Min(value=>value.x),uv.Min(value=>value.y));uvMax=new Vector2(uv.Max(value=>value.x),uv.Max(value=>value.y));}
                }
                finally{AssetRegistry.ReleaseTransient(baked);}
            }
            var viewPoints=worldPoints.Select(point=>{var view=camera.WorldToViewportPoint(point);return new Vector3(view.x,1-view.y,view.z);}).ToArray();
            return new FlightVisual{owner=owner.name,component=renderer.GetType().Name,materials=materialText,
                shaderStatus=String.Join(";",renderer.sharedMaterials.Select(material=>material==null||material.shader==null?"<null>":material.shader.name+" supported="+material.shader.isSupported+" passes="+material.passCount).ToArray()),
                activeSelf=owner.activeSelf,activeInHierarchy=owner.activeInHierarchy,enabled=renderer.enabled,isVisible=renderer.isVisible,
                hasBounds=bounds.size.sqrMagnitude>0,layer=owner.layer,worldPosition=owner.transform.position,
                screenPosition=camera.WorldToScreenPoint(owner.transform.position),sourceViewport=effect.PreviewPoint(owner.transform.position),
                boundsCenter=bounds.center,boundsSize=bounds.size,
                particleAlive=particle!=null&&particle.IsAlive(true),particlePlaying=particle!=null&&particle.isPlaying,
                particleEmitting=particle!=null&&particle.isEmitting,particleCount=particle==null?0:particle.particleCount,
                particleTime=particle==null?0:particle.time,trailEmitting=trail!=null&&trail.emitting,
                trailPositionCount=trail==null?0:trail.positionCount,trailTime=trail==null?0:trail.time,trailWidth=trail==null?0:trail.widthMultiplier,
                trailMeshBaked=trailMeshBaked,trailMeshVertices=meshVertices,trailMeshTriangles=meshTriangles,trailVertexAlphaMin=vertexAlphaMin,trailVertexAlphaMax=vertexAlphaMax,
                trailColorMin=vertexColorMin,trailColorMax=vertexColorMax,trailUvMin=uvMin,trailUvMax=uvMax,
                trailWorldPoints=worldPoints,trailViewportPoints=viewPoints,sourceMaterials=sourceMaterialText,materialState=materialState};
        }).ToArray();
        var path=flight.Path;var nodeSummary=String.Join(" | ",effect.Nodes.Select((node,index)=>
            index+":"+(node==null?"<null>":node.name+" active="+node.activeInHierarchy+" renderers="+String.Join(",",node.GetComponents<Renderer>().Select(r=>r.GetType().Name+"="+r.enabled).ToArray())+
                " particle="+(index<effect.Particles.Length&&effect.Particles[index]!=null?effect.Particles[index].particleCount.ToString()+"/"+effect.Particles[index].isEmitting:"none")+
                " sourceMesh="+effect.Program.nodes[index].mesh+" trail="+(effect.Program.nodes[index].trail!=null&&effect.Program.nodes[index].trail.restored)+
                " particleData="+(effect.Program.nodes[index].particle!=null&&effect.Program.nodes[index].particle.restored)+
                " sourceMaterials="+String.Join(",",(effect.Program.nodes[index].materials??new NativeMaterial[0]).Select(m=>m.name+"/"+m.shader+"/"+String.Join(";",(m.textures??new NativeTexture[0]).Select(t=>t.name+"="+t.path).ToArray())).ToArray()))).ToArray());
        var diagnostic=new FlightDiagnostic{stage=stage,eventKey=eventKey,eventType=eventType,sampleIndex=sampleIndex,targetAgeMs=targetAgeMs,arrivalAge=arrivalAge,
            arrivalTime=cast!=null&&cast.Flow.FirstArrivalTime.HasValue?cast.Flow.FirstArrivalTime.Value:-1,
            frame=Time.frameCount,nodeCount=effect.Nodes.Length,rootChildCount=effect.Root.transform.childCount,
            nodeSummary=nodeSummary,replayTime=Time.realtimeSinceStartup-started,
            flowTime=cast==null?0:cast.Flow.Time,firedAt=flight.FiredAt,duration=path==null?0:path.Duration,
            elapsed=path==null?0:cast.Flow.Time-flight.FiredAt,reached=flight.Reached,
            rootActive=effect.Root.activeInHierarchy,movementPosition=flight.MovementTransform==null?Vector3.zero:flight.MovementTransform.position,
            targetPosition=flight.TargetPosition,targetViewport=effect.PreviewPoint(flight.TargetPosition),captureCamera=camera.name+" orthographic="+camera.orthographic+" size="+camera.orthographicSize+
                " position="+camera.transform.position+" rotation="+camera.transform.rotation+" cullingMask="+camera.cullingMask,
            cameraOrthographic=camera.orthographic,cameraOrthographicSize=camera.orthographicSize,cameraCullingMask=camera.cullingMask,visuals=visuals,
            sourceState=effect.ActiveSourceSpellState,sourceStateChanges=effect.SourceStateChanges,propertyWrites=effect.PropertyWrites,unhandledActions=effect.UnhandledActions,
            actionExecutions=effect.ActionExecutions.Select(pair=>new HunterActionCount{name=pair.Key,count=pair.Value}).ToArray(),
            unhandledActionDetails=effect.UnhandledActionDetails.ToArray()};
        var diagnosticPath=Path.Combine(folder,"hunter-flight-"+stage+".json");
        if(sampleIndex>=0)pendingHunterFlights.Add(new PendingHunterFlight{path=diagnosticPath,diagnostic=diagnostic});
        else File.WriteAllText(diagnosticPath,JsonUtility.ToJson(diagnostic,true));
        Note("Hunter flight visual diagnostic "+stage+" renderers="+visuals.Length+" active="+visuals.Count(v=>v.activeInHierarchy&&v.enabled)+
            " visible="+visuals.Count(v=>v.isVisible)+" particles="+visuals.Sum(v=>v.particleCount)+" trailPoints="+visuals.Sum(v=>v.trailPositionCount)+
            " nodes="+effect.Nodes.Length+" rootChildren="+effect.Root.transform.childCount);
    }
    bool CaptureReady(string name){if(lastCapture==name)return true;lastCapture=name;Capture(name);return false;}
    void WrapAudio(Action<NativeAudioCue> original,NativeAudioCue cue,string source)
    {
        original(cue);Note("source play",cue,source);var stop=cue.stopPlayback;
        cue.stopPlayback=()=>{Note("source stop",cue,source);stop?.Invoke();};
    }
    string PowerViewSummary()
    {
        var views=(System.Collections.IDictionary)typeof(NativePowerVisual).GetField("views",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(powers);
        return String.Join(";",views.Keys.Cast<object>().Select(key=>
        {
            object view=views[key];var effectField=view.GetType().GetField("effect",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            var effect=effectField==null?null:effectField.GetValue(view) as NativeEffectInstance;var root=effect==null?null:effect.Root;
            return key+"#"+(root==null?0:root.GetInstanceID())+" active="+(root!=null&&root.activeInHierarchy);
        }).ToArray());
    }
    void CloseReviewBattle()
    {
        var connection=Field<Connection>("connection");connection?.Dispose();Set("connection",null);Invoke("ResetMenu");
    }
    void Update()
    {
        PumpHunterDiagnostics();
        if(finished){PollPriestPeerCompletion();return;}
        try{if(runtimeError!=null)throw new InvalidOperationException("Actual Player runtime: "+runtimeError);CheckPriestPeerFailure();Tick();PublishHeroOpeningReady();CaptureReferencePhases();}
        catch(Exception exception){Debug.LogException(exception);Finish("FAIL: "+exception.Message,1);}
    }
    bool HeroOpeningSettled()
    {
        var speech=Field<HeroSpeech>("heroSpeech");
        return speech!=null&&speech.OpeningComplete&&!speech.HasBubble(0)&&!speech.HasBubble(1);
    }
    string HeroOpeningMarker(string markerRole)
    {return Path.Combine(Directory.GetParent(folder).FullName,markerRole+"-opening-complete.txt");}
    bool HeroOpeningReady(string matchId,string markerRole)
    {
        string path=HeroOpeningMarker(markerRole);
        return File.Exists(path)&&String.Equals(File.ReadAllText(path),matchId,StringComparison.Ordinal);
    }
    void PublishHeroOpeningReady()
    {
        if((!Paladin&&!Warlock)||initial==null||(role!="driver"&&role!="observer"))return;
        if(!HeroOpeningSettled())return;
        string path=HeroOpeningMarker(role);
        if(File.Exists(path))return;
        string temp=path+".tmp";File.WriteAllText(temp,initial.match_id);File.Move(temp,path);
        Note("natural HeroSpeech opening and bubble completed for match="+initial.match_id);
    }
    void CaptureReferencePhases()
    {
        if(finished||initial==null||presentation==null)return;
        if((Paladin||Warlock)&&(!HeroOpeningReady(initial.match_id,"driver")||!HeroOpeningReady(initial.match_id,"observer")))return;
        if(Paladin&&!String.IsNullOrEmpty(paladinSourceKey)&&effects!=null)
        {
            const string friendlyArea="ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10";
            var area=effects.SourceArea(paladinSourceKey);
            if(area!=null&&area.FriendlyAreaOnlyReconstruction&&area.Reference==friendlyArea&&area.Effect.Program.reference==friendlyArea)
                CaptureHeroPowerReferencePhase("paladin",area.Effect,area.Time,referencePaladinPhases,PaladinReferencePhaseAges,
                    area.Effect.ActiveSourceSpellState!=0);
        }
        if(Warlock&&cast!=null&&cast.CardId=="HERO_07bp"&&cast.Effect!=null&&
           cast.Effect.Program.reference=="ReuseFX_BuffImpact_Void_SwirlIntake_Small_HeroPower_Super.prefab:5c3f93a27c9cb434ea6e0c37005a2c0d")
            CaptureHeroPowerReferencePhase("warlock",cast.Effect,cast.Time,referenceWarlockPhases,WarlockReferencePhaseAges,
                cast.Effect.CurrentStates.Contains("Play FX"));
        if(role=="driver"&&Rogue&&!referenceWeaponGlowCaptured)
        {
            var weapons=Field<NativeBattleWeapons>("nativeWeapons");NativeBattleWeapons.GlowSample glow;
            if(weapons!=null&&weapons.TrySummonGlow(initial.seat,out glow)&&glow.age>=.2f&&glow.age<.5f)
            {referenceWeaponGlowCaptured=true;Capture("reference-rogue-equip-glow");}
        }
        if(role=="driver"&&Warrior&&step==7&&presentation.Cues.Any(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_01bp"))
        {
            if(!referenceWarriorBurstCaptured&&presentation.TimeInBatch>=.68f)
            {referenceWarriorBurstCaptured=true;Capture("reference-warrior-armor-burst");}
            if(!referenceWarriorRingsCaptured&&presentation.TimeInBatch>=1.02f)
            {referenceWarriorRingsCaptured=true;Capture("reference-warrior-button-rings");}
        }
        if(role=="driver"&&Priest&&step==7&&!referencePriestHealCaptured&&presentation.Cues.Any(cue=>cue.data.type=="Heal"&&cue.data.amount>0&&
            cue.fired&&presentation.TimeInBatch-cue.start>=.08f&&presentation.TimeInBatch-cue.start<.3f))
        {referencePriestHealCaptured=true;Capture("reference-priest-heal");}
    }
    void CaptureHeroPowerReferencePhase(string hero,NativeEffectInstance effect,float sourceAge,bool[] captured,float[] phaseAges,bool sourceStateActive)
    {
        if(effect==null||captured==null||phaseAges==null||!sourceStateActive||effect.ParticleCount<=0||
           float.IsNaN(sourceAge)||float.IsInfinity(sourceAge))return;
        int phase=-1;
        for(int i=0;i<phaseAges.Length&&i<captured.Length&&i<HeroPowerReferencePhaseNames.Length;i++)
            if(!captured[i]&&sourceAge>=phaseAges[i]){phase=i;break;}
        if(phase<0)return;
        captured[phase]=true;
        string name="reference-"+hero+"-"+HeroPowerReferencePhaseNames[phase];
        Capture(name);
        Note(name+" sourceAge="+sourceAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            " activeState="+effect.ActiveSourceSpellState+" states="+String.Join("/",effect.CurrentStates)+" particles="+effect.ParticleCount);
        if(hero=="paladin"&&effect.Nodes!=null&&effect.Nodes.Length>11&&effect.Nodes[11]!=null)
        {
            var node=effect.Nodes[11];var renderer=node.GetComponent<Renderer>();var material=renderer==null?null:renderer.sharedMaterial;
            var texture=material!=null&&material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
            Camera boardCamera=app==null?null:Field<Camera>("cameraView");
            Vector3 viewport=boardCamera==null?Vector3.zero:boardCamera.WorldToViewportPoint(node.transform.position);
            bool inBoardView=boardCamera!=null&&viewport.z>0&&viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1;
            Note("paladin Card_Hero runtime producer layer="+node.layer+" rendererEnabled="+(renderer!=null&&renderer.enabled)+
                " isVisible="+(renderer!=null&&renderer.isVisible)+" world="+node.transform.position.ToString("F3")+
                " bounds="+(renderer==null?"<none>":renderer.bounds.center.ToString("F3")+"/"+renderer.bounds.size.ToString("F3"))+
                " boardCameraMask="+(boardCamera==null?-1:boardCamera.cullingMask)+" includesSourceLayer="+
                (boardCamera!=null&&(boardCamera.cullingMask&(1<<node.layer))!=0)+" viewport="+viewport.ToString("F3")+" inBoardView="+inBoardView+
                " material="+(material==null?"<none>":material.name+"/"+effect.Program.nodes[11].materials[0].shader)+
                " texture="+(texture==null?"<null>":texture.name+" "+texture.width+"x"+texture.height)+
                " tintAlpha="+(material!=null&&material.HasProperty("_TintColor")?material.GetColor("_TintColor").a.ToString("F3"):"<none>"));
        }
    }
    bool CapturePriestImpact()
    {
        if(!Priest||priestImpactCaptureRequested||cast==null||cast.Flow==null)return false;
        const string impactReference="ReuseFX_Holy_Impact_01.prefab:72e94a2cb20f5f243acd6d7e4dab17e1";
        var impacts=cast.Flow.Instances.Where(instance=>instance.Effect.Program.reference==impactReference).ToArray();
        if(impacts.Length==0)return false;
        priestImpactCaptureRequested=true;
        if(impacts.Length!=1)throw new InvalidOperationException("Priest source graph produced "+impacts.Length+" live Impact clones");
        var instance=impacts[0];var target=instance.Targets.GetTarget();
        string expectedName="SuperSpell scene entity "+targetEntity;
        priestImpactTargetEntity=target!=null&&target.name==expectedName?targetEntity:0;
        var locations=Field<Dictionary<int,Rect>>("locations");
        var targetPoint=cast.TargetScreenPoint;
        if(targetPoint.HasValue&&locations.ContainsKey(targetEntity))
        {
            var expectedPoint=locations[targetEntity].center;
            priestTargetGuiX=targetPoint.Value.x;priestTargetGuiY=targetPoint.Value.y;
            priestTargetGuiPositionError=Vector2.Distance(targetPoint.Value,expectedPoint);
            priestTargetGuiPositionValid=priestTargetGuiPositionError<.5f;
        }
        Check(priestImpactTargetEntity==targetEntity,"live Priest Impact clone is bound to the HeroPowerTarget entity on this GUI side");
        Check(priestTargetGuiPositionValid,"Priest source target point matches this GUI side's authoritative target location");
        Check(cast.Events.Any(line=>line.Contains("StartBirth"))&&cast.Events.Count(line=>line.Contains("ImpactClone"))==1,
            "same accepted Priest graph reaches its authored Start and unique target Impact stages");
        priestImpactVisualCaptured=priestImpactTargetEntity==targetEntity&&priestTargetGuiPositionValid;
        priestNativeEvents=String.Join(";",cast.Events);
        Note("Priest live Impact screen capture target="+priestImpactTargetEntity+" gui="+priestTargetGuiX.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+","+
            priestTargetGuiY.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" error="+priestTargetGuiPositionError.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        Capture("05-priest-impact-active");
        return true;
    }
    bool IsPairedPriestRole{get{return Priest&&(role=="driver"||role=="observer");}}
    string PeerRole{get{return role=="driver"?"observer":"driver";}}
    string PeerReportPath{get{return Path.Combine(Directory.GetParent(folder).FullName,PeerRole,"report.json");}}
    Report ReadPeerReport()
    {
        string path=PeerReportPath;if(!File.Exists(path))return null;
        string json;
        try{json=File.ReadAllText(path);}
        catch(IOException exception)
        {
            int win32Code=exception.HResult&0xffff;
            if(win32Code!=32&&win32Code!=33)throw;
            if(Time.realtimeSinceStartup-peerReportSharingNoteAt>=1f)
            {peerReportSharingNoteAt=Time.realtimeSinceStartup;Note("peer report is temporarily locked for writing; retrying next frame");}
            return null;
        }
        var report=JsonUtility.FromJson<Report>(json);
        if(report==null||String.IsNullOrEmpty(report.result))throw new InvalidDataException("Peer "+PeerRole+" report has no result");
        return report;
    }
    void CheckPriestPeerFailure()
    {
        if(!IsPairedPriestRole)return;
        var report=ReadPeerReport();
        if(report!=null&&report.result.StartsWith("FAIL",StringComparison.Ordinal))
            throw new InvalidOperationException("Peer "+PeerRole+" formal replay failed: "+report.result);
    }
    void PollPriestPeerCompletion()
    {
        if(!IsPairedPriestRole||finishExit!=0)return;
        try
        {
            var report=ReadPeerReport();
            if(report!=null)
            {
                if(report.result.StartsWith("FAIL",StringComparison.Ordinal))
                {FailPriestCompletion("Peer "+PeerRole+" formal replay failed: "+report.result);return;}
                if(!report.result.StartsWith("PASS",StringComparison.Ordinal))
                {FailPriestCompletion("Peer "+PeerRole+" formal replay has an unrecognized result: "+report.result);return;}
                if(report.role!=PeerRole||!String.Equals(report.publicHash,finishedReport.publicHash,StringComparison.Ordinal))
                {FailPriestCompletion("Peer "+PeerRole+" report role/hash mismatch: role="+report.role+" publicHash="+report.publicHash+" local="+finishedReport.publicHash);return;}
                completionConfirmed=true;Debug.Log("FIREBLAST FORMAL "+finishResult+" checks="+checks+" peer="+PeerRole+" PASS");Application.Quit(0);return;
            }
            if(Time.realtimeSinceStartup>=peerFinishDeadline)FailPriestCompletion("Peer "+PeerRole+" did not publish a replay report before the 15s completion bound");
        }
        catch(Exception exception){FailPriestCompletion("Unable to confirm peer replay result: "+exception.Message);}
    }
    void FailPriestCompletion(string reason)
    {
        finishExit=1;finishResult="FAIL: "+reason;PublishReportResult(finishResult);
        Debug.LogError("FIREBLAST FORMAL "+finishResult+" checks="+checks);Application.Quit(1);
    }
    void OnApplicationQuit()
    {
        if(!IsPairedPriestRole)return;
        if(!finished)
        {
            Environment.ExitCode=1;Finish("FAIL: Player application quit before its replay report completed",1);return;
        }
        if(finishExit==0&&!completionConfirmed)
        {
            Environment.ExitCode=1;FailPriestCompletion("Player application quit before the peer PASS report was confirmed");
        }
    }
    void OnEnable(){Application.logMessageReceived+=RuntimeLog;}
    void OnDisable(){Application.logMessageReceived-=RuntimeLog;}
    void RuntimeLog(string message,string trace,LogType type){if(runtimeError==null&&(type==LogType.Exception||type==LogType.Error))runtimeError=message;}
    void Tick()
    {
        if(Time.realtimeSinceStartup-started>(role=="duel"?600:90))throw new TimeoutException("Formal replay did not finish within bounded observation");
        if(app==null)app=FindObjectOfType<ClientApp>();if(app==null)return;
        int requestedWidth,requestedHeight;
        if(!int.TryParse(Argument("-screen-width"),out requestedWidth)||!int.TryParse(Argument("-screen-height"),out requestedHeight)||requestedWidth<=0||requestedHeight<=0)
            throw new InvalidOperationException("Explicit positive -screen-width and -screen-height are required for replay");
        if(!resolutionRequested){resolutionRequested=true;Screen.SetResolution(requestedWidth,requestedHeight,FullScreenMode.Windowed);return;}
        if(Screen.width!=requestedWidth||Screen.height!=requestedHeight)
        {
            if(Time.realtimeSinceStartup-started>15)throw new InvalidOperationException("Actual Player resolution differs from explicit request: "+Screen.width+"x"+Screen.height);
            return;
        }
        if(role=="duel")
        {
            var view=Field<StateView>("state");if(view==null)return;publicHash=view.public_hash;
            if(Time.realtimeSinceStartup>bootLog+2){bootLog=Time.realtimeSinceStartup;File.WriteAllText(Path.Combine(folder,"duel-progress.txt"),"phase="+view.phase+" version="+view.version+" turn="+view.turn);}
            if(view.phase!="finished"||Field<Presentation>("presentation").Busy)return;
            if(!CaptureReady("natural-finished"))return;
            Check(view.players.All(p=>!String.IsNullOrEmpty(p.result)),"real authority natural terminal results");
            Check(Field<Presentation>("presentation").Display.public_hash==view.public_hash,"natural duel display catches final authority");
            Invoke("ResetMenu");Finish("PASS natural authoritative GUI duel",0);return;
        }
        if(step==83)
        {
            var currentState=Field<StateView>("state");if(currentState!=null)publicHash=currentState.public_hash;
            string marker=Path.Combine(Directory.GetParent(folder).FullName,"driver-complete.txt");
            if(RogueWeapons&&role=="observer")
            {
                var observed=Field<StateView>("state");var shown=Field<Presentation>("presentation").Display;
                if(RogueWeaponsShaman)
                {
                    if(observed!=null)
                    {
                        AcknowledgeRogueWeaponReplacement(observed,1);AcknowledgeRogueWeaponReplacement(observed,2);
                        string firstRequest=RogueWeaponMarker(observed.match_id,"replacement-1-request");
                        if(File.Exists(firstRequest)&&observed.active==1&&!Field<bool>("pending")&&!rogueWeaponEndTurnAccepted)
                        {
                            rogueWeaponEndTurnAccepted=true;rogueWeaponEndTurnAcceptedVersion=observed.version;
                            rogueWeaponLifecycle.endTurnAccepted=true;rogueWeaponLifecycle.endTurnAcceptedVersion=observed.version;
                            RecordRogueWeaponLifecyclePoint(observed,"observer-replacement-end-turn-accepted");
                        }
                        TickRogueWeaponShamanObserver(observed);
                    }
                }
                else
                {
                string stopMarker=observed==null?null:RogueWeaponMarker(observed.match_id,"stop-after-endturn");
                if(File.Exists(stopMarker)&&observed!=null&&!rogueWeaponReplacementAccepted&&observed.active==0&&
                   (observed.events??new SemanticEvent[0]).Any(item=>item.type=="PlayHeroPower")&&
                   (observed.events??new SemanticEvent[0]).Any(item=>item.type=="Summon"&&item.card_type==7&&item.visual!=null&&item.visual.card_id=="CS2_082"))
                {
                    rogueWeaponReplacementAccepted=true;rogueWeaponReplacementAcceptedVersion=observed.version;
                    rogueWeaponLifecycle.replacementAccepted=true;rogueWeaponLifecycle.replacementAcceptedVersion=observed.version;
                    RecordRogueWeaponLifecyclePoint(observed,"observer-replacement-power-accepted");
                    File.WriteAllText(RogueWeaponMarker(observed.match_id,"observer-saw-replacement"),
                        observed.match_id+":"+observed.version+":"+observed.public_hash);
                }
                if(File.Exists(stopMarker)&&observed!=null&&observed.active==1&&!Field<bool>("pending")&&!rogueWeaponEndTurnAccepted)
                {
                    rogueWeaponEndTurnAccepted=true;rogueWeaponEndTurnAcceptedVersion=observed.version;
                    rogueWeaponLifecycle.endTurnAccepted=true;rogueWeaponLifecycle.endTurnAcceptedVersion=observed.version;
                    RecordRogueWeaponLifecyclePoint(observed,"observer-replacement-end-turn-accepted");
                }
                if(observed!=null&&observed.active==1&&!rogueObserverEndTurnSent&&!File.Exists(stopMarker)&&!Field<bool>("pending")&&
                    !Field<Presentation>("presentation").Busy&&!Field<Presentation>("presentation").InputLocked&&shown!=null&&
                    observed.public_hash==shown.public_hash)
                {
                    var endTurn=(observed.moves??new Move[0]).FirstOrDefault(move=>move.kind=="end_turn");
                    if(endTurn==null)throw new InvalidOperationException("Mage Player became active without its legal end-turn move");
                    Invoke("SendMove",endTurn);
                    Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+1,
                        "Mage observer Player submits its own authority-listed end-turn move between Rogue attacks");
                    rogueObserverEndTurnSent=true;
                    Note("Mage Player passed its authority-listed end turn after the first Rogue attack");return;
                }
                }
            }
            if(File.Exists(marker))
            {
                bool pass=File.ReadAllText(marker).StartsWith("PASS",StringComparison.Ordinal);
                if(RogueWeapons&&pass)
                {
                    var observed=Field<StateView>("state");var shown=Field<Presentation>("presentation").Display;
                    if(!RogueConcessionReady(observed)||shown==null)return;
                    if(RogueWeaponsShaman&&!rogueWeaponResultReadyOnBothPeers)return;
                    rogueWeaponConcessionSynchronized=true;rogueWeaponLifecycle.concessionSynchronized=true;
                    rogueWeaponLifecycle.concedeAcceptedVersion=observed.version;rogueWeaponLifecycle.finalHash=observed.public_hash;
                    rogueWeaponLifecycleComplete=true;rogueWeaponDisplayHash=shown.public_hash;publicHash=observed.public_hash;
                    RecordRogueWeaponLifecyclePoint(observed,"concede-finished-synchronized");
                    Check(rogueWeaponEndTurnAccepted&&rogueWeaponEndTurnAcceptedVersion<observed.version&&
                        rogueWeaponConcessionSynchronized&&(!RogueWeaponsShaman||rogueWeaponResultReadyOnBothPeers),
                        RogueWeaponsShaman?"observer receives v19 after both Players report ResultReady":
                        "observer receives Rogue concession as a synchronized finished result after the Mage turn begins");
                    Capture("rogue-concede-final");Invoke("ResetMenu");Advance(8,"scene-exit");return;
                }
                Finish(pass?RogueWeapons?"PASS bounded formal Rogue weapon lifecycle observer GUI/authority state; official fidelity unverified":"PASS bounded formal "+TestedPower+" observer GUI/authority state; official fidelity unverified":"FAIL driver evidence did not complete",pass?0:1);
            }return;
        }
        if(role=="observer")
        {
            string marker=Path.Combine(Directory.GetParent(folder).FullName,"driver-complete.txt");
            if(File.Exists(marker)&&!File.ReadAllText(marker).StartsWith("PASS",StringComparison.Ordinal))
            {Finish("FAIL driver evidence did not complete",1);return;}
        }
        if(DisconnectProbe&&role=="observer"&&Field<bool>("connectionLost"))
        {effects=Field<NativeEffectPlayer>("nativeEffects");Check(!effects.HasPowerSelection,"actual observer transport disconnect has no retained selection");Invoke("ResetMenu");Advance(83,"observer-complete-wait");return;}
        if(step==21)
        {
            if(!Field<bool>("connectionLost")||Age<1.3f)return;
            Check(cast.Flow.Cancelled&&!cast.Flow.CastRequested&&!effects.HasPowerSelection,"actual server transport close hard cancels selected source without Cast");
            Check(records.Count(r=>r.message=="source play")==cuesBeforeDisconnect,"late source audio never restarts after actual disconnect");
            Check(!audio.Sources.Any(s=>s!=null&&s.loop&&s.isPlaying)&&Field<int>("actionCounter")==initialCounter,"disconnect stops actual precast loop and emits no power request");
            tap.StopCapture();Invoke("ResetMenu");Advance(8,"scene-exit");return;
        }
        if(step==90&&Age>.5f)
        {
            if(Review){CloseReviewBattle();Advance(8,"scene-exit");return;}
            Advance(83,"observer-complete-wait");return;
        }
        if(step==23&&Age>.3f)
        {Check(effects.PowerSelection.Time==heldTime,"actual paused selection retains source time while frame baseline advances");presentation.Paused=false;presentation.Speed=2;heldTime=effects.PowerSelection.Time;rateTime=Time.realtimeSinceStartup;Advance(24,"selection-speed2");return;}
        if(step==24&&Age>.3f)
        {float ratio=(effects.PowerSelection.Time-heldTime)/(Time.realtimeSinceStartup-rateTime);Check(ratio>1.7f&&ratio<2.3f,"actual source 2x uses frame delta once ratio="+ratio);presentation.Speed=.5f;heldTime=effects.PowerSelection.Time;rateTime=Time.realtimeSinceStartup;Advance(25,"selection-speed-half");return;}
        if(step==25&&Age>.3f)
        {float ratio=(effects.PowerSelection.Time-heldTime)/(Time.realtimeSinceStartup-rateTime);Check(ratio>.35f&&ratio<.65f,"actual source half speed uses frame delta once ratio="+ratio);presentation.Speed=1;clockProbeDone=true;Advance(2,"selection-after-clock");return;}
        if(step==71)
        {
            string peer=Path.Combine(Directory.GetParent(folder).FullName,"observer-ready.txt");
            string report=Path.Combine(Directory.GetParent(folder).FullName,"observer","report.json");
            string completed=Path.Combine(Directory.GetParent(folder).FullName,"observer-complete.txt");
            if(!File.Exists(completed))return;
            if(File.Exists(report)&&JsonUtility.FromJson<Report>(File.ReadAllText(report)).result.StartsWith("FAIL",StringComparison.Ordinal))throw new InvalidOperationException("Observer reported an actual evidence failure; retain its report");
            if(!File.Exists(peer))return;Note("scene exit pre-reset powerViews="+powers.InstanceCount+" ["+PowerViewSummary()+"]");CloseReviewBattle();Note("scene exit post-reset powerViews="+powers.InstanceCount+" ["+PowerViewSummary()+"]");Advance(8,"scene-exit");return;
        }
        if(step==8&&Age>.5f)
        {
            if(RogueWeapons&&audio.ActiveVoices>0&&Age<5)return;
            Check(owned.All(go=>go==null)&&!effects.HasPowerSelection&&effects.ActiveCount==0,"actual scene exit releases observed source roots");
            Note("scene exit resource counts powerViews="+powers.InstanceCount+" ["+PowerViewSummary()+"] nativeVoices="+audio.ActiveVoices);
            Check(powers.InstanceCount==0,"scene exit releases power views count="+powers.InstanceCount+" ["+PowerViewSummary()+"]");
            Check(audio.ActiveVoices==0,"scene exit releases native audio sources count="+audio.ActiveVoices);Finish(DisconnectProbe?"PASS actual transport disconnect/source release":Priest?"PASS bounded formal Priest GUI/input/source/DSP; official fidelity unverified":RogueWeapons?"PASS bounded formal Rogue weapon lifecycle dual-Player GUI/authority/source audio; official fidelity unverified":Rogue?"PASS bounded formal Rogue dagger GUI/input/source/DSP; official fidelity unverified":Warrior?"PASS bounded formal Warrior armor GUI/input/source/DSP; official fidelity unverified":Druid?"PASS bounded formal Druid Shapeshift GUI/rules/source/DSP; official fidelity unverified":Warlock?"PASS bounded formal Warlock Life Tap GUI/rules/source/DSP; official fidelity unverified":Paladin?"PASS bounded formal Paladin Recruit GUI/FriendlyArea/rules/DSP plus end-turn during visual tail; original Chain/audio/animation unverified":"PASS bounded formal GUI/input/DSP; official fidelity unverified",0);return;
        }
        var state=Field<StateView>("state");
        if(state!=null&&state.phase=="finished"&&RogueWeapons&&role=="driver"&&step==36)
        {publicHash=state.public_hash;CompleteRogueWeaponConcede(state);return;}
        if(state==null||state.phase!="play")
        {
            if(Time.realtimeSinceStartup>bootLog+2)
            {
                bootLog=Time.realtimeSinceStartup;
                string detail="page="+Field<string>("page")+" initialized="+Field<bool>("initialized")+" status="+Field<string>("status")+" autoMatch="+Field<bool>("autoMatch")+" time="+Time.time;
                File.WriteAllText(Path.Combine(folder,"boot.txt"),detail);Debug.Log("FIREBLAST BOOT "+detail);
            }
            if(!bootCaptured&&Time.realtimeSinceStartup-started>15){Capture("boot");bootCaptured=true;}
            return;
        }
        publicHash=state.public_hash;
        if(step==9&&Shaman)
        {
            var playback=Field<NativeShamanHeroPowerPlayback>("nativeShaman");
            if(presentation.Busy||presentation.Display==null||presentation.Display.public_hash!=state.public_hash)
            {if(Age>10)throw new TimeoutException("H02 final display did not settle to authority hash; busy="+presentation.Busy+" display="+(presentation.Display==null?"":presentation.Display.public_hash)+" authority="+state.public_hash);return;}
            shamanDisplayHash=presentation.Display.public_hash;shamanDisplayHashMatched=true;
            var totem=BattleInteraction.Entity(state,shamanTotemEntity);var live=Field<NativeBattleActors>("nativeActors").Find(totem);
            var actorPresentation=live==null?null:live.Model.GetComponentInChildren<NativeActorPresentation>(true);
            shamanNativeActorVisible=live!=null&&live.Visible;shamanActorVisualRestoredAfterSource=actorPresentation!=null&&actorPresentation.Shown;
            var audioRecords=records.Skip(shamanAudioRecordStartIndex<0?records.Count:shamanAudioRecordStartIndex)
                .Where(record=>record.audioSource=="native-effect"&&record.message=="source play").ToArray();
            shamanSourceAudioPlayCount=audioRecords.Length;
            shamanDiagnostics+=" final="+playback.Diagnostics+" actorShown="+(actorPresentation!=null&&actorPresentation.Shown)+" actorVisible="+shamanNativeActorVisible;
            if(!CaptureReady("06-shaman-power-used"))return;
            Check(shamanDisplayHashMatched,"Shaman displayed public state settles to the authoritative post-Summon hash");
            Check(shamanNativeActiveCount==1&&playback.ActiveCount==1&&shamanRuntimeFailures==0,
                "CustomSpawn Actor remains bound to the summoned Totem after its authored cast and PlayFX sequence");
            Check(shamanActorVisualRestoredAfterSource&&shamanNativeActorVisible,
                "CustomSpawn completion restores the real Totem Actor visual root after its source spawn clip");
            Check(shamanSourceAudioPlayCount==2&&audioRecords.All(record=>!String.IsNullOrEmpty(record.file)),
                "both source-authored Shaman cast and CustomSpawn audio actions reach the scene Listener");
            shamanDiagnostics+=" final="+playback.Diagnostics;Note("H02 final runtime "+shamanDiagnostics);
            publicHash=state.public_hash;tap.StopCapture();castChecksDone=true;Invoke("ResetMenu");Advance(8,"scene-exit");return;
        }
        if(effects==null)
        {
            app.ServerActionRejected+=message=>
            {
                if(step==5&&message=="stale state version")
                {staleServerRejectionSeen=true;staleRejectionReceivedAt=Time.realtimeSinceStartup;Note("stale server response observed on transport");}
            };
            effects=Field<NativeEffectPlayer>("nativeEffects");audio=Field<NativeAudioPlayback>("nativeAudio");powers=Field<NativePowerVisual>("powerVisual");presentation=Field<Presentation>("presentation");
            listenerCount=FindObjectsOfType<AudioListener>().Count(listener=>listener.enabled);
            effects.SourceSpellRenderDiagnostic=CaptureSuperSpellRender;
            effects.SourceSpellDrawDiagnostic=CaptureSuperSpellDraw;
            var original=effects.AudioCue;effects.AudioCue=cue=>WrapAudio(original,cue,"native-effect");
            var ui=powers.AudioCue;powers.AudioCue=cue=>WrapAudio(ui,cue,"power-ui");
            if(RogueWeapons&&role=="driver")
            {
                var weapons=Field<NativeBattleWeapons>("nativeWeapons");Check(weapons!=null&&weapons.Enabled&&weapons.AudioCue!=null,"original CS2_082 weapon audio profile loads all four source slots");
                var originalWeaponAudio=weapons.AudioCue;
                weapons.AudioCue=cue=>
                {
                    int prior=audio.StartedVoices;originalWeaponAudio(cue);
                    bool started=audio.StartedVoices==prior+1;int slot=RogueWeaponAudioSlot(cue);
                    Note("weapon source play slot="+slot+" actualStarted="+started,cue,"native-weapon");
                    if(slot==7&&started)rogueWeaponSlot7AudioPlayCount++;
                    if(slot==5&&started)rogueWeaponSlot5AudioPlayCount++;
                };
            }
            if(Shaman)
            {
                var shaman=Field<NativeShamanHeroPowerPlayback>("nativeShaman");var shamanAudio=shaman.AudioCue;
                shaman.AudioCue=cue=>WrapAudio(shamanAudio,cue,"native-effect");
            }
            if(Hunter)
            {
                var feedback=presentation.AudioCue;presentation.AudioCue=(e,id)=>
                {if(e.type=="Hit"&&e.target==targetEntity){hitCallbacks++;Note("actual Hit callback target="+e.target+" batch="+presentation.TimeInBatch);}feedback?.Invoke(e,id);};
            }
            if(Review||Priest)
            {
                var feedback=presentation.AudioCue;presentation.AudioCue=(e,id)=>
                {
                    if(Review&&e.type=="Hit"&&e.target==reviewTarget){hitCallbacks++;Note("actual Hit callback target="+e.target+" batch="+presentation.TimeInBatch);}
                    if(Priest&&e.type=="Heal"&&initial!=null&&e.source==initial.players[0].power.entity&&e.target==targetEntity)
                    {healCallbacks++;Note("actual Priest Heal callback target="+e.target+" batch="+presentation.TimeInBatch);}
                    feedback?.Invoke(e,id);
                };
            }
        }
        if(Review&&initial!=null)TrackReview(state);
        if(Hunter&&initial!=null&&state.version>initial.version&&cast==null)BindHunterCast(state);
        if(Hunter&&initial!=null)TrackHunter(state);
        if(Priest&&initial!=null&&state.version>initial.version&&cast==null)BindPriestCast(state);
        if(Priest&&initial!=null)TrackPriest(state);
        if(Rogue&&initial!=null&&state.version>initial.version&&cast==null)BindRogueCast(state);
        if(Rogue&&initial!=null)TrackRogue(state);
        if(Warrior&&initial!=null&&state.version>initial.version&&cast==null)BindWarriorCast(state);
        if(Warrior&&initial!=null)TrackWarrior(state);
        if(Warrior&&role=="driver"&&cast!=null&&!warriorFollowupSent)
        {TrySubmitWarriorFollowup(state);if(warriorFollowupSent)return;}
        if(Druid&&initial!=null&&state.version>initial.version&&cast==null)BindDruidCast(state);
        if(Druid&&initial!=null)TrackDruid(state);
        if(Warlock&&initial!=null&&state.version>initial.version&&cast==null)BindWarlockCast(state);
        if(Warlock&&initial!=null)TrackWarlock(state);
        if(Paladin&&initial!=null&&state.version>initial.version&&String.IsNullOrEmpty(paladinSourceKey))BindPaladinArea(state);
        if(Paladin&&initial!=null)TrackPaladin(state);
        if(Paladin&&role=="driver"&&step==72){CompletePaladinTailTurn(state);return;}
        if(cast!=null&&cast.Flow!=null)foreach(var clone in cast.Flow.Instances)owned.Add(clone.Effect.Root);
        if(RogueWeapons&&role=="driver"&&step==33){CompleteRogueWeaponTurn(state);return;}
        if(RogueWeapons&&role=="driver"&&(step==31||step==32)){CompleteRogueWeaponAttack(state,step-30);return;}
        if(RogueWeaponsShaman&&role=="driver"&&step==41){CompleteRogueWeaponAttack(state,3);return;}
        if(RogueWeapons&&role=="driver"&&step==34){CompleteRogueWeaponReplacement(state);return;}
        if(RogueWeapons&&role=="driver"&&step==35){CompleteRogueWeaponEndTurn(state);return;}
        if(RogueWeapons&&role=="driver"&&step==36){CompleteRogueWeaponConcede(state);return;}
        if(RogueWeapons&&role=="driver"&&step==37){SubmitRogueWeaponReplacementEndTurn(state);return;}
        if(RogueWeapons&&role=="driver"&&step==38){SubmitRogueWeaponConcede(state);return;}
        if(RogueWeaponsShaman&&role=="driver"&&step==39){CompleteRogueWeaponShamanTurn(state);return;}
        if(RogueWeaponsShaman&&role=="driver"&&step==40){SubmitRogueWeaponConcedeAfterReplacement(state);return;}
        if(Hunter&&cast!=null)
        {
            var flight=cast.Flow.Flights.FirstOrDefault(item=>item.Path!=null);
            if(!hunterFlightMid&&flight!=null&&cast.Flow.Time>=flight.FiredAt+flight.Path.Duration*.5f&&!flight.Reached)
            {CaptureFlightDiagnostic("mid",flight);Capture("hunter-missile-mid");hunterFlightMid=true;}
            if(!hunterArrivalCapture&&flight!=null&&flight.Reached)
            {Capture("hunter-arrival");hunterArrivalCapture=true;}
        }
        if(tap!=null)Field<AudioSource>("music").Stop();
        var locations=Field<Dictionary<int,Rect>>("locations");
        Vector2 reviewArrowPoint=locations.ContainsKey(state.players[1].hero.entity)?locations[state.players[1].hero.entity].center:targetPoint;
        if(step==0)
        {
            if(presentation.Busy||Field<MulliganPresentation>("mulliganPresentation").Active||!locations.ContainsKey(state.players[state.seat].power.entity))return;
            Note("battle baseline powerViews="+powers.InstanceCount+" ["+PowerViewSummary()+"]");
            initial=state;initialCounter=Field<int>("actionCounter");
            Check(state.players[0].power.card_id==TestedPower&&state.players[1].power.card_id==(RogueWeaponsShaman?"HERO_02bp":Review?"HERO_07bp":Priest||Rogue||Warrior||Druid||Warlock||Paladin||Shaman?"HERO_08bp":TestedPower),RogueWeaponsShaman?"real Rogue versus Shaman":Review?"real Mage versus Warlock":Hunter?"two Hunter powers":Priest?"real Priest versus Mage":Rogue?"real Rogue versus Mage":Warrior?"real Warrior versus Mage":Druid?"real Druid versus Mage":Warlock?"real Warlock versus Mage":Paladin?"real Paladin versus Mage":Shaman?"real Shaman versus Mage":"two standard mages");
            Check(state.players[0].power.used!=state.players[1].power.used,"one used and one ready in real authority snapshot");
            if(Priest&&Argument("--fireblast-replay-target")!="own-hero")throw new InvalidOperationException("HERO_09bp formal replay requires --target own-hero");
            reviewTarget=Priest?state.players[0].hero.entity:Rogue||Warrior||Druid||Warlock||Paladin||Shaman?0:Argument("--fireblast-replay-target")=="own-minion"?state.players[0].field[0].entity:state.players[1].hero.entity;
            powerPoint=locations[state.players[state.seat].power.entity].center;
            int target=Hunter?state.players[1].hero.entity:Priest?state.players[0].hero.entity:Rogue||Warrior||Druid||Warlock||Paladin||Shaman?0:Argument("--fireblast-replay-target")=="own-minion"?state.players[state.seat].field[0].entity:state.players[1-state.seat].hero.entity;
            targetEntity=target;initialTargetHealth=BattleInteraction.Entity(state,target)?.health??-1;
            if(Priest)Check(initialTargetHealth==(PriestFullHealth?30:28),"Priest target starts at "+(PriestFullHealth?"30 full health":"28 after the prior Mage power hit"));
            targetPoint=target==0?powerPoint:locations[target].center;Note("source="+state.players[state.seat].power.entity+" target="+target+" design="+targetPoint+" targetHealth="+initialTargetHealth);
            if(Rogue){rogueBefore=RogueState(state);Check(RogueWeaponIsEmpty(state.players[0].weapon)&&!state.players[0].power.used,"Rogue authority snapshot starts with no weapon and an unused HERO_03bp power");
                if(role=="driver")Check(state.seat==0&&state.moves.Any(move=>move.kind=="power"&&move.entity==state.players[0].power.entity&&move.target==0),"Rogue authority offers the legal untargeted dagger power before use");}
            if(RogueWeaponsShaman)
                Check(state.version==6&&state.players[0].hero.card_id=="HERO_03"&&state.players[1].hero.card_id=="HERO_02"&&
                    BattleInteraction.Entity(state,71)!=null&&
                    BattleInteraction.Entity(state,71).card_id=="CS2_050",
                    "exact Rogue/Shaman GUI fixture starts at v6 with saved CS2_050 totem entity 71");
            if(Warrior){warriorBefore=WarriorState(state);Check(warriorBefore.powerCardId=="HERO_01bp"&&warriorBefore.heroArmor==0&&!warriorBefore.powerUsed,"Warrior authority snapshot starts with zero armor and an unused HERO_01bp power");
                if(role=="driver")Check(state.seat==0&&warriorBefore.legalPowerMove,"Warrior authority offers the legal untargeted target=0 power move");}
            if(Druid){druidBefore=DruidState(state);druidAudioRecordStartIndex=records.Count;Check(druidBefore.powerCardId=="HERO_06bp"&&druidBefore.heroAttack==0&&druidBefore.heroArmor==0&&!druidBefore.powerUsed,"Druid authority snapshot starts with zero attack/armor and an unused HERO_06bp power");
                if(role=="driver")Check(state.seat==0&&druidBefore.legalPowerMove,"Druid authority offers the legal untargeted target=0 power move");}
            if(Warlock){warlockBefore=WarlockState(state);warlockAudioRecordStartIndex=records.Count;Check(warlockBefore.powerCardId=="HERO_07bp"&&warlockBefore.heroHealth==29&&!warlockBefore.powerUsed&&warlockBefore.deckCount>0&&warlockBefore.handCount<10,"Warlock authority snapshot starts with 29 health, a drawable deck and an unused HERO_07bp power");
                if(role=="driver")Check(state.seat==0&&warlockBefore.legalPowerMove,"Warlock authority offers the legal untargeted target=0 power move");}
            if(Paladin){paladinBefore=PaladinState(state);paladinAudioRecordStartIndex=records.Count;Check(paladinBefore.powerCardId=="HERO_04bp"&&paladinBefore.recruitCount==0&&!paladinBefore.powerUsed,"Paladin authority snapshot starts without a recruit and with an unused HERO_04bp power");
                if(role=="driver")Check(state.seat==0&&paladinBefore.legalPowerMove,"Paladin authority offers the legal untargeted target=0 power move");}
            if(Shaman)shamanAudioRecordStartIndex=records.Count;
            Pointer(new Vector2(40,1040));Capture(Hunter?"00-hunter-ready":Priest?"00-priest-ready":Rogue?"00-rogue-ready":Warrior?"00-warrior-ready":Druid?"00-druid-ready":Warlock?"00-warlock-ready":Paladin?"00-paladin-ready":Shaman?"00-shaman-ready":"00-two-mages-ready");
            if(role=="observer"){Advance(Review?-11:80,"observer-hover");Pointer(Review?locations[state.players[state.seat].field.Single(c=>c.card_id=="CS2_119").entity].center:powerPoint);return;}
            Check(state.seat==0&&(UnaffordableProbe?!state.moves.Any(m=>m.kind=="power"):state.moves.Any(m=>m.kind=="power")),"driver authoritative power availability");
            var listener=FindObjectsOfType<AudioListener>().Where(l=>l.enabled).ToArray();listenerCount=listener.Length;Check(listenerCount==1,"one actual scene AudioListener");
            tap=listener[0].gameObject.AddComponent<FireblastPcmTap>();tap.Begin(Path.Combine(folder,"scene-mix.wav"));
            Advance(Review?-10:1,"hover");Pointer(Review?locations[state.players[state.seat].field.Single(c=>c.card_id=="CS2_119").entity].center:powerPoint);return;
        }
        if(step==-11&&Age>.6f){if(!CaptureReady("review-turtle-card-and-field"))return;Pointer(powerPoint);Advance(80,"observer-hover");return;}
        if(step==-10&&Age>.6f)
        {
            if(!CaptureReady("review-turtle-card-and-field"))return;
            int turtle=state.players[0].field.Single(c=>c.card_id=="CS2_119").entity;Click(locations[turtle].center);Pointer(reviewArrowPoint);
            Check(Field<int>("selected")==turtle&&BattleInteraction.IsLegalTarget(state,turtle,state.players[1].hero.entity),"real turtle attack selects legal target");Advance(-9,"review-attack-arrow");return;
        }
        if(step==-9&&Age>.25f)
        {
            if(!CaptureReady("review-attack-arrow"))return;Check(Field<NativeTargetArrow>("targetArrow").BullseyeVisible,"attack uses original legal bullseye");Input(EventType.MouseDown,1,reviewArrowPoint);
            previewCard=state.players[0].hand.First(c=>c.card_id=="CS2_029");Pointer(locations[previewCard.entity].center);Advance(-8,"review-spell-pickup");return;
        }
        if(step==-8&&Age>.25f)
        {if(!HoverReviewHand(state))return;Input(EventType.MouseDown,0,app.EvidencePointer.Value);Check(Field<int>("dragged")==previewCard.entity,"actual exposed Fireball hand hit selects expected card");Input(EventType.MouseDrag,0,reviewArrowPoint);Advance(-7,"review-spell-arrow");return;}
        if(step==-7&&Age>.25f)
        {
            if(!CaptureReady("review-spell-arrow"))return;Check(Field<int>("selected")==previewCard.entity&&Field<NativeTargetArrow>("targetArrow").BullseyeVisible,"targeted Fireball uses original arrow and legal bullseye");Input(EventType.MouseDown,1,reviewArrowPoint);
            previewCard=state.players[0].hand.First(c=>c.card_id=="CS2_189");Pointer(locations[previewCard.entity].center);Advance(-6,"review-battlecry-pickup");return;
        }
        if(step==-6&&Age>.25f)
        {if(!HoverReviewHand(state))return;Input(EventType.MouseDown,0,app.EvidencePointer.Value);Check(Field<int>("dragged")==previewCard.entity,"actual exposed Elven Archer hand hit selects expected card");Input(EventType.MouseDrag,0,new Vector2(960,570));Input(EventType.MouseUp,0,new Vector2(960,570));Pointer(reviewArrowPoint);Advance(-5,"review-battlecry-arrow");return;}
        if(step==-5&&Age>.25f)
        {
            if(!CaptureReady("review-battlecry-arrow"))return;Check(Field<int>("selected")==previewCard.entity&&Field<int>("placementIndex")>=0&&Field<NativeTargetArrow>("targetArrow").BullseyeVisible,"placed Elven Archer uses original arrow and legal bullseye");
            Invoke("HandleBattleEscape");Check(Field<int>("actionCounter")==initialCounter&&state.version==initial.version,"attack/spell/battlecry preview cancellation sends zero actions");Pointer(powerPoint);Advance(1,"hover");return;
        }
        if(step==80&&Age>.5f){if(!CaptureReady(Hunter?"01-hunter-observer-own-used-hover":"01-own-used-hover"))return;Pointer(locations[state.players[1-state.seat].power.entity].center);Advance(81,"observer-enemy-hover");return;}
        if(step==81&&Age>.5f){if(!CaptureReady(Hunter?"02-hunter-observer-ready-hover":"02-enemy-ready-hover"))return;Pointer(new Vector2(40,1040));Advance(UnaffordableProbe?83:82,"observer-wait");return;}
        if(step==82&&state.version>initial.version&&(!Review||sawReactionEnd&&state.public_hash==presentation.Display.public_hash)&&(!Priest||!presentation.Busy)&&(!Rogue||rogueSourceRootReleased)&&(!Warrior||warriorSourceRootReleased)&&(!Druid||druidSourceRootReleased)&&(!Warlock||warlockSourceRootReleased)&&(!Paladin||paladinAreaCompleted)&&Age>2)
        {
            if(!Review&&state.public_hash!=presentation.Display.public_hash)
            {
                if(observerHashWaitStarted<0){observerHashWaitStarted=Time.realtimeSinceStartup;Note("observer authority advanced; bounded presentation hash wait started busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);}
                if(Time.realtimeSinceStartup-observerHashWaitStarted<12)return;
                throw new TimeoutException("Observer presentation did not reach authority hash within 12s; state="+state.version+
                    " authority="+state.public_hash+" display="+presentation.Display.public_hash+" busy="+presentation.Busy+
                    " inputLocked="+presentation.InputLocked+" batchTime="+presentation.TimeInBatch.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            }
            if(Review)
            {
                string followup=Path.Combine(Directory.GetParent(folder).FullName,"followup-accepted.txt");
                if(!File.Exists(followup)||presentation.DamageDecorationCount!=0)return;
            }
            Capture("03-authority-used");Check(state.public_hash==presentation.Display.public_hash,"observer displayed public state reaches authority");
            if(Rogue){rogueAfter=RogueState(state);rogueDisplayHash=presentation.Display.public_hash;}
            if(Warrior)
            {
                Check(warriorCastAuthorityEvents!=null&&warriorAfter!=null&&warriorCastVersion==warriorAfter.version,
                    "Warrior observer retains the accepted cast authority snapshot across later batches");
                warriorDisplayHash=presentation.Display.public_hash;
            }
            if(Druid){druidAfter=DruidState(state);druidDisplayHash=presentation.Display.public_hash;}
            if(Warlock){warlockAfter=WarlockState(state);warlockDisplayHash=presentation.Display.public_hash;}
            if(Paladin){paladinAfter=PaladinState(state);paladinDisplayHash=presentation.Display.public_hash;}
            if(Review){CheckReview();string peer=Path.Combine(Directory.GetParent(folder).FullName,"observer-ready.txt");File.WriteAllText(peer+".tmp",publicHash);File.Move(peer+".tmp",peer);}
            Advance(90,"observer-flush");return;
        }
        if(step==1&&Age>.5f)
        {
            if((Paladin||Warlock)&&(!HeroOpeningReady(state.match_id,"driver")||!HeroOpeningReady(state.match_id,"observer")))return;
            if(!CaptureReady(UnaffordableProbe?"01-unaffordable-hover-detail":Hunter?"01-hunter-power-hover":Priest?"01-priest-power-hover":Rogue?"01-rogue-power-hover":Warrior?"01-warrior-power-hover":Druid?"01-druid-power-hover":Warlock?"01-warlock-power-hover":Paladin?"01-paladin-power-hover":"01-ready-hover-detail"))return;Click(powerPoint);
            bool automaticPower=Paladin||Shaman;
            if(Hunter||Rogue||Warrior||Druid)automaticPower=true;
            if(Warlock)automaticPower=true;
            if(automaticPower)
            {
                int power=state.players[state.seat].power.entity;
                var moves=state.moves.Where(move=>move.kind=="power"&&move.entity==power).ToArray();
                Check(moves.Length==1&&moves[0].target==0, (Hunter?"Hunter":Rogue?"Rogue":Warrior?"Warrior":Druid?"Druid":Warlock?"Warlock":Shaman?"Shaman":"Paladin")+" authority offers exactly one automatic target=0 power move");
                var arrow=Field<NativeTargetArrow>("targetArrow");
                bool legalTarget=BattleInteraction.IsLegalTarget(state,power,targetEntity),arrowVisible=arrow!=null&&arrow.BullseyeVisible;
                bool selectedTarget=Field<int>("selected")!=0||effects.HasPowerSelection;
                Check((Rogue||Shaman||!legalTarget)&&(Rogue||Shaman||!arrowVisible)&&!selectedTarget,
                    (Hunter?"Hunter":Rogue?"Rogue":Warrior?"Warrior":Druid?"Druid":Warlock?"Warlock":Shaman?"Shaman":"Paladin")+" automatic power has no manual target selection");
                Check(!effects.HasPowerSelection&&Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+1,
                    "actual "+(Hunter?"Hunter":Rogue?"Rogue":Warrior?"Warrior":Druid?"Druid":Warlock?"Warlock":Shaman?"Shaman":"Paladin")+" button click sends one direct power request without a synthetic selection");
                Advance(7,Hunter?"hunter-direct-cast":Rogue?"rogue-direct-cast":Warrior?"warrior-direct-cast":Druid?"druid-direct-cast":Warlock?"warlock-direct-cast":Shaman?"shaman-direct-cast":"paladin-friendly-area");return;
            }
            if(UnaffordableProbe)
            {
                Check(!effects.HasPowerSelection&&Field<int>("actionCounter")==initialCounter,"actual insufficient mana button cannot select or send");
                tap.StopCapture();Invoke("ResetMenu");Advance(8,"scene-exit");return;
            }
            Check(effects.HasPowerSelection,"formal button input starts source selection");
            Pointer(targetPoint);Advance(2,"selected");return;
        }
        if(step==2&&Review)
        {
            var times=new[]{.05f,.15f,.3f,.6f,1.5f,3f};if(sequenceIndex<times.Length&&Age>=times[sequenceIndex])
            {Capture("review-selection-"+sequenceIndex+"-"+Age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));sequenceIndex++;return;}
            if(sequenceIndex<times.Length||!clockProbeDone&&Age<3.1f)return;
            if(!clockProbeDone){heldTime=effects.PowerSelection.Time;presentation.Paused=true;Advance(23,"selection-paused");return;}
        }
        if(step==2&&Age>.5f)
        {
            if(!CaptureReady(Hunter?"02-hunter-selection-legal-arrow":Priest?"02-priest-selection-own-hero":"02-selected-legal-arrow"))return;
            Check(BattleInteraction.IsLegalTarget(state,state.players[state.seat].power.entity,targetEntity)&&Field<NativeTargetArrow>("targetArrow").BullseyeVisible,
                "selected hero power arrow reflects an authoritative legal move");
            if(DisconnectProbe)
            {
                cast=effects.PowerSelection;owned.UnionWith(cast.Flow.Instances.Select(c=>c.Effect.Root));cuesBeforeDisconnect=records.Count(r=>r.message=="source play");
                File.WriteAllText(Path.Combine(Directory.GetParent(folder).FullName,"driver-disconnect-request.txt"),"actual selected source owns transport-close test");Advance(21,"disconnect-wait");return;
            }
            var invalidPoint=Hunter?locations[state.players[state.seat].hero.entity].center:new Vector2(1900,1040);
            if(Hunter)Check(!BattleInteraction.IsLegalTarget(state,state.players[state.seat].power.entity,state.players[state.seat].hero.entity),"friendly hero is absent from the authoritative target moves");
            Click(invalidPoint);Check(Field<int>("actionCounter")==initialCounter,"illegal target emits no power request");Advance(3,"invalid-hover");return;
        }
        if(step==3&&Age>.3f)
        {
            if(!CaptureReady("03-invalid-arrow"))return;Input(EventType.MouseDown,1,targetPoint);Check(Field<int>("actionCounter")==initialCounter,"right cancellation emits no request");Advance(4,"cancel-tail");return;
        }
        if(step==4&&Age>2)
        {
            if(effects.HasPowerSelection)return;
            Check(!effects.HasPowerSelection,"graceful cancellation reaches source None");Check(!audio.Sources.Any(s=>s!=null&&s.loop&&s.isPlaying),"cancel actually stops precast AudioSource loop");
            Click(powerPoint);Invoke("HandleBattleEscape");Check(Field<int>("actionCounter")==initialCounter&&effects.PowerSelection.Flow.TargetingCancelled,"formal Esc cancels without request");Advance(41,"escape-tail");return;
        }
        if(step==41&&Age>2)
        {
            if(effects.HasPowerSelection)return;
            Check(!effects.HasPowerSelection,"formal Esc source tail releases before reselect");
            Click(powerPoint);rejectedSelection=effects.PowerSelection;
            Check(rejectedSelection!=null,"stale rejection probe owns its selected source before submit");
            staleServerRejectionSeen=false;
            effects.SubmitPowerSelection(state.players[state.seat].power.entity);Set("pending",true);Set("status","");
            var move=state.moves.First(m=>m.kind=="power"&&m.target==targetEntity);
            Field<Connection>("connection").Send(JsonUtility.ToJson(new ActionMessage{kind="power",entity=move.entity,target=move.target,match_id=state.match_id,version=state.version-1,action_id="f005-expected-stale-rejection"}));
            Advance(5,"expected-rejection");return;
        }
        if(step==5)
        {
            if(state.version!=initial.version)
            {
                Note("stale rejection unexpectedly changed authority state "+PriestStaleCancellationState());
                throw new InvalidOperationException("stale power probe mutated state before cancellation completed: "+PriestStaleCancellationState());
            }
            bool staleRejected=staleServerRejectionSeen;
            if(!staleRejected)
            {
                if(Age>=PriestStaleResponseTimeout)
                {
                    Note("stale server rejection response timed out "+PriestStaleCancellationState());
                    throw new TimeoutException("server did not reject the stale power probe within "+PriestStaleResponseTimeout+"s: "+PriestStaleCancellationState());
                }
                if(Age>=PriestStaleProgressInterval&&(staleRejectionLastNoteAt<0||Time.realtimeSinceStartup-staleRejectionLastNoteAt>=PriestStaleProgressInterval))
                {staleRejectionLastNoteAt=Time.realtimeSinceStartup;Note("waiting for stale server rejection "+PriestStaleCancellationState());}
                return;
            }
            if(staleRejectionReceivedAt<0)
            {
                staleRejectionReceivedAt=Time.realtimeSinceStartup;staleRejectionLastNoteAt=staleRejectionReceivedAt;
                Note("stale rejection received; waiting for source cancellation tail "+PriestStaleCancellationState());
            }
            if(Field<bool>("pending"))
            {
                if(Time.realtimeSinceStartup-staleRejectionReceivedAt>=PriestStaleCancellationTimeout)
                {
                    Note("stale rejection did not clear pending "+PriestStaleCancellationState());
                    throw new TimeoutException("stale rejection left the request pending after "+PriestStaleCancellationTimeout+"s: "+PriestStaleCancellationState());
                }
                return;
            }
            var rejectedFlow=rejectedSelection==null?null:rejectedSelection.Flow;
            bool released=!effects.HasPowerSelection&&rejectedSelection!=null&&rejectedFlow!=null&&
                rejectedFlow.TargetingCancelled&&!rejectedSelection.Busy&&rejectedFlow.Completed&&
                rejectedFlow.ActiveClones==0&&rejectedFlow.Pending==0;
            if(!released)
            {
                if(Time.realtimeSinceStartup-staleRejectionReceivedAt>=PriestStaleCancellationTimeout)
                {
                    Note("stale rejection cancellation tail timed out "+PriestStaleCancellationState());
                    throw new TimeoutException("stale rejection cancellation did not release its source graph within "+PriestStaleCancellationTimeout+"s: "+PriestStaleCancellationState());
                }
                if(Time.realtimeSinceStartup-staleRejectionLastNoteAt>=PriestStaleProgressInterval)
                {staleRejectionLastNoteAt=Time.realtimeSinceStartup;Note("waiting for source cancellation tail "+PriestStaleCancellationState());}
                return;
            }
            Check(staleRejected&&
                state.version==initial.version&&!Field<bool>("pending")&&!effects.HasPowerSelection,
                "actual stale server rejection cancels selection without state mutation");
            Check(rejectedFlow.TargetingCancelled&&rejectedFlow.Completed&&rejectedFlow.ActiveClones==0&&rejectedFlow.Pending==0&&!rejectedSelection.Busy,
                "stale rejection cancellation releases the completed source graph");
            Note("stale rejection cancellation tail released "+PriestStaleCancellationState());
            Check(Field<int>("actionCounter")==initialCounter,"rejected probe is separate from formal input requests");
            Click(powerPoint);var old=effects.PowerSelection;var roots=old.Flow.Instances.Select(c=>c.Effect.Root).ToArray();
            Input(EventType.MouseDown,1,targetPoint);Input(EventType.MouseDown,0,powerPoint);Check(effects.HasPowerSelection&&!ReferenceEquals(old,effects.PowerSelection),"rapid cancel and reselect owns a fresh source graph");
            owned.UnionWith(roots);cast=effects.PowerSelection;Input(EventType.MouseDrag,0,targetPoint);Advance(6,"final-selection");return;
        }
        if(step==6&&Age>1.2f)
        {
            if(!CaptureReady(Hunter?"04-hunter-final-selection":Priest?"04-priest-final-selection":"04-final-selection"))return;Check(cast.Flow.FirstArrivalTime==null&&!cast.Flow.CastRequested,"held selection does not fly or hit");
            Check(Field<NativeTargetArrow>("targetArrow").BullseyeVisible&&BattleInteraction.IsLegalTarget(state,state.players[state.seat].power.entity,targetEntity),"release target remains in authoritative legal moves");
            Input(EventType.MouseUp,0,targetPoint);Check(Field<int>("actionCounter")==initialCounter+1&&Field<bool>("pending"),"formal drag release emits exactly one real power request");
            Invoke("HandleBattleEscape");Invoke("HandleBattleEscape");Check(ReferenceEquals(cast,effects.PowerSelection)&&!cast.Flow.TargetingCancelled,"pending formal Esc preserves submitted instance");Advance(7,"cast");return;
        }
        if(step==7)
        {
            if(Priest&&CapturePriestImpact())return;
            if(Age>.3f&&!capturedCast){Capture(Shaman?"05-shaman-cast":Paladin?"05-paladin-friendly-area":"05-cast");capturedCast=true;}
            if(Shaman)
            {
                if(state.version==initial.version)return;
                var playback=Field<NativeShamanHeroPowerPlayback>("nativeShaman");
                shamanDiagnostics="active="+playback.Diagnostics+" failure="+playback.LastFailure;
                if(presentation.TimeInBatch<1.15f||presentation.TimeInBatch>2.45f||playback.ActiveCount!=1)
                {
                    if(Age>10){Note("H02 source window failure "+shamanDiagnostics);throw new TimeoutException("H02 source window did not expose one live CustomSpawn actor while the input should be released; batch="+presentation.TimeInBatch+" active="+playback.ActiveCount+" failures="+playback.Failures+" detail="+playback.LastFailure);}
                    return;
                }
                var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int playerEntity=0;
                var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                if(spends.Length==1)playerEntity=spends[0].target;
                var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0).ToArray();
                var summons=currentEvents.Where(e=>e.type=="Summon").ToArray();
                var totems=summons.Where(e=>e.source==ownPower&&e.owner==0&&e.card_type==4&&e.visual!=null&&e.target!=0).ToArray();
                int totemEntity=totems.Length==1?totems[0].target:0;var totem=BattleInteraction.Entity(state,totemEntity);
                var displayedTotem=BattleInteraction.Entity(presentation.Display,totemEntity);
                shamanNativeActiveCount=playback.ActiveCount;shamanRuntimeFailures=playback.Failures;
                var live=Field<NativeBattleActors>("nativeActors").Find(totem);
                shamanNativeActorVisible=live!=null&&live.Visible;
                var actorPresentation=live==null?null:live.Model.GetComponentInChildren<NativeActorPresentation>(true);
                shamanActorVisualHiddenDuringSource=actorPresentation!=null&&!actorPresentation.Shown;
                var ordered=currentEvents.Where(e=>e.type=="SpendMana"||e.type=="PlayHeroPower"||e.type=="Summon").Select(e=>e.type).ToArray();
                shamanEventOrderValid=ordered.SequenceEqual(new[]{"SpendMana","PlayHeroPower","Summon"});
                shamanEventLinkValid=spends.Length==1&&playerEntity!=0&&powers.Length==1&&linkedPowers.Length==1&&summons.Length==1&&totems.Length==1&&
                    totems[0].visual.entity==totemEntity&&totems[0].visual.card_id==totem.card_id&&displayedTotem!=null&&displayedTotem.card_id==totem.card_id;
                shamanSpendManaCount=spends.Length;shamanPlayHeroPowerCount=powers.Length;shamanSummonCount=summons.Length;
                shamanTotemEntity=totemEntity;shamanTotemCardId=totem==null?"":totem.card_id;
                shamanDisplayHash=presentation.Display==null?"":presentation.Display.public_hash;
                shamanDisplayHashMatched=state.public_hash==shamanDisplayHash;
                shamanInputUnlockSampleFrame=Time.frameCount;shamanInputUnlockSampleTime=Time.realtimeSinceStartup-started;
                shamanInputUnlockBatchTime=presentation.TimeInBatch;
                shamanInputUnlockedDuringSource=!presentation.InputLocked&&!Field<bool>("pending")&&state.moves.Any(move=>move.kind=="end_turn")&&playback.ActiveCount==1;
                shamanDiagnostics=playback.Diagnostics+" actorVisible="+shamanNativeActorVisible;
                Note("H02 source window "+shamanDiagnostics);
                if(!CaptureReady("05-shaman-active-source-fx"))return;
                Check(shamanEventOrderValid&&shamanEventLinkValid,
                    "authority records one SpendMana -> PlayHeroPower -> Summon chain linked to the actual random Totem entity "+shamanTotemEntity+"/"+shamanTotemCardId);
                Check(state.players[0].power.card_id=="HERO_02bp"&&state.players[0].power.used&&state.players[0].mana==initial.players[0].mana-2&&
                    totem!=null&&state.players[0].field.Count(entity=>entity.entity==totemEntity)==1,
                    "HERO_02bp spends two mana once and the authority board contains its single real Totemic Call result");
                Check(shamanNativeActiveCount==1&&shamanRuntimeFailures==0&&shamanNativeActorVisible&&shamanActorVisualHiddenDuringSource,
                    "source CustomSpawn is attached to the real Summon Actor and hides its original visuals during the authored spawn sequence");
                Check(shamanInputUnlockedDuringSource&&shamanInputUnlockBatchTime>=1.15f&&shamanInputUnlockBatchTime<2.5f,
                    "legal input unlocks while the source-authored Shaman effect is still active");
                Check(tap.SampleFrames>0&&tap.Overflows==0,"Shaman scene Listener records complete DSP blocks; source clips are reported separately");
                Advance(9,"shaman-settle");return;
            }
            if(Paladin)
            {
                if(state.version==initial.version)return;
                if(String.IsNullOrEmpty(paladinSourceKey))
                {if(Age>10)throw new TimeoutException("Paladin PlayHeroPower has no HERO_04bp FriendlyArea source cue");return;}
                if(!paladinAreaCompleted)
                {if(Age>15)throw new TimeoutException("Paladin FriendlyArea-only reconstruction did not finish its source area clone; clones="+
                    paladinAreaCloneCreations+" finished="+paladinAreaFinishedNotifications+" pending="+paladinAreaPending+" reference="+paladinAreaReference);return;}
                if(!PaladinTailInputReady(state)||!paladinInputUnlockedAfterArea)
                {if(Age>15)throw new TimeoutException("Paladin input did not unlock while the FriendlyArea visual tail was active with a legal end-turn action and matching hashes");return;}
                Capture("06-paladin-power-used");
                var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;
                var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                int playerEntity=spends.Length==1?spends[0].target:0;
                var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
                var summons=currentEvents.Where(e=>e.type=="Summon").ToArray();
                var recruits=summons.Where(e=>e.source==ownPower&&e.target==paladinAfter.recruitEntity&&e.card_type==4&&e.owner==0&&e.visual!=null&&e.visual.card_id=="CS2_101t").ToArray();
                var displayedRecruit=BattleInteraction.Entity(presentation.Display,paladinAfter.recruitEntity);
                Check(paladinBefore!=null&&paladinBefore.legalPowerMove&&!paladinBefore.powerUsed&&paladinBefore.recruitCount==0&&
                    spends.Length==1&&playerEntity!=0&&powers.Length==1&&linkedPowers.Length==1&&summons.Length==1&&recruits.Length==1&&paladinEventOrderValid,
                    "authority records one two-mana payment, HERO_04bp use and one same-batch CS2_101 recruit Summon");
                Check(paladinAfter.powerUsed&&paladinAfter.mana==paladinBefore.mana-2&&paladinAfter.recruitCount==1&&
                    paladinAfter.recruitCardId=="CS2_101t"&&paladinAfter.recruitAttack==1&&paladinAfter.recruitHealth==1&&
                    displayedRecruit!=null&&displayedRecruit.card_id=="CS2_101t"&&displayedRecruit.attack==1&&displayedRecruit.health==1,
                    "Paladin authority and displayed board contain the single 1/1 Silver Hand Recruit");
                Check(paladinSourceCueCount==1&&paladinSourceCueSemanticsValid&&paladinFriendlyAreaOnlyReconstruction&&
                    paladinAreaReference=="ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10"&&
                    paladinAreaCloneCreations==1&&paladinAreaFinishedNotifications==1&&paladinAreaPending==0&&
                    !paladinParentFlowConstructed,
                    "HERO_04bp plays one verified FriendlyArea clone without constructing the enabled-null parent Chain Flow");
                Check(paladinUnhandledActions==0&&paladinUnboundAudioActions==0,
                    "FriendlyArea source clone has no unhandled or unbound audio actions");
                Check(PaladinTailInputReady(state)&&paladinInputUnlockedAfterArea,
                    "Paladin input unlocks while the FriendlyArea visual tail is still active, with a legal end-turn action and matching hashes");
                Check(state.public_hash==presentation.Display.public_hash,"Paladin displayed public state reaches authority");
                Check(tap.SampleFrames>0&&tap.Overflows==0,"Paladin scene Listener records complete DSP blocks; source audio records remain separately reported");
                var endTurn=state.moves.FirstOrDefault(move=>move.kind=="end_turn");
                Check(endTurn!=null&&presentation.Busy&&effects.SourceVisualTailActive(paladinSourceKey),
                    "real legal end-turn move remains available while the source visual tail is active");
                paladinTurnSubmitVersion=state.version;paladinTailSubmitFrame=Time.frameCount;paladinCastEvidenceLocked=true;
                Invoke("SendMove",endTurn);
                Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+2,
                    "ClientApp accepts Paladin end-turn through SendMove before the prior source visual tail ends");
                paladinTailEndTurnSubmitted=true;Note("Paladin end-turn accepted during source visual tail submitFrame="+paladinTailSubmitFrame+
                    " version="+paladinTurnSubmitVersion+" busy="+presentation.Busy+" inputLocked="+presentation.InputLocked);
                Advance(72,"paladin-turn-during-source-tail");return;
            }
            if((Hunter||Priest||Rogue||Warrior||Warlock)&&state.version>initial.version&&cast==null)
            {if(Age>10)throw new InvalidOperationException("Accepted "+TestedPower+" action has no bound SpellEffect source playback; check native cue preparation");return;}
            if(state.version==initial.version||(!Review&&!Priest&&presentation.Busy)||(!Review&&Age<4))return;
            if(Priest&&(!PriestSourceUnlockReady(state)||!priestInputUnlockedAfterSource))
            {
                if(Age>15f)throw new TimeoutException("Priest source did not reach a fully released, unlocked, hash-matched state with a legal end-turn move; busy="+
                    presentation.Busy+" inputLocked="+presentation.InputLocked+" publicHash="+state.public_hash+" displayHash="+
                    presentation.Display.public_hash+" endTurn="+(state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn")));
                return;
            }
            if(Rogue&&(!RogueSourceUnlockReady(state)||!rogueInputUnlockedAfterSource))
            {
                if(Age>15f)throw new TimeoutException("Rogue source root did not release with input open, a legal end-turn move, and matching hashes; busy="+
                    presentation.Busy+" inputLocked="+presentation.InputLocked+" publicHash="+state.public_hash+" displayHash="+
                    presentation.Display.public_hash+" endTurn="+(state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn")));
                return;
            }
            if(Warrior&&(!WarriorSourceUnlockReady(state)||!warriorInputUnlockedAfterSource))
            {
                if(Age>15f)throw new TimeoutException("Warrior source root did not release with input open, a legal next action, and matching hashes; busy="+
                    presentation.Busy+" inputLocked="+presentation.InputLocked+" publicHash="+state.public_hash+" displayHash="+
                    presentation.Display.public_hash+" endTurn="+(state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn")));
                return;
            }
            if(Druid&&(!DruidSourceUnlockReady(state)||!druidInputUnlockedAfterSource))
            {
                if(Age>15f)throw new TimeoutException("Druid source root did not release with input open, a legal end-turn move, and matching hashes; busy="+
                    presentation.Busy+" inputLocked="+presentation.InputLocked+" publicHash="+state.public_hash+" displayHash="+
                    presentation.Display.public_hash+" endTurn="+(state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn")));
                return;
            }
            if(Warlock&&(!WarlockSourceUnlockReady(state)||!warlockInputUnlockedAfterSource))
            {
                if(Age>15f)throw new TimeoutException("Warlock source root did not release with input open, a legal end-turn action, and matching hashes; busy="+
                    presentation.Busy+" inputLocked="+presentation.InputLocked+" publicHash="+state.public_hash+" displayHash="+
                    presentation.Display.public_hash+" endTurn="+(state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn")));
                return;
            }
            if(Review&&(!sawReactionEnd||state.public_hash!=presentation.Display.public_hash))return;
            if((Review||Hunter||Priest||Rogue||Warrior||Druid||Warlock)&&!castChecksDone)
            {
                if(!CaptureReady(Hunter?"06-hunter-power-used":Priest?"06-priest-power-used":Rogue?"06-rogue-power-used":Warrior?"06-warrior-power-used":Druid?"06-druid-power-used":Warlock?"06-warlock-power-used":"06-power-used"))return;
                Note(cast.Flow==null?"accepted source-root-only playback card="+cast.CardId:
                    "accepted graph cast="+cast.Flow.CastRequested+" arrival="+cast.Flow.FirstArrivalTime+" completed="+cast.Flow.Completed+" cancelled="+cast.Flow.Cancelled+" events="+String.Join(";",cast.Events));
                if(Hunter)Note("Hunter source counters unhandled="+cast.UnhandledActions+" unboundAudio="+cast.UnboundAudioActions+
                    " missingAudioPlayback="+cast.MissingAudioPlaybackServices+" cameraShakerStarts="+cast.CameraShakerStarts+
                    " component="+cast.UnboundComponentActions+
                    " actions="+String.Join(";",cast.UnhandledActionDetails)+
                    " executions="+String.Join(";",cast.ActionExecutions.Select(pair=>pair.Key+"="+pair.Value).ToArray()));
                if(Warrior)Note("Warrior source counters parentCameraShakerStarts="+cast.ParentCameraShakerStarts+
                    " impactCloneCameraShakerStarts="+cast.ImpactCloneCameraShakerStarts+
                    " parentUnhandled="+cast.ParentUnhandledActions+" impactCloneUnhandled="+cast.ImpactCloneUnhandledActions+
                    " unhandledDetails="+String.Join(";",cast.UnhandledActionDetails));
                if(Rogue)
                {
                    int ownPower=initial.players[0].power.entity;
                    var currentEvents=state.events??new SemanticEvent[0];
                    var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                    int playerEntity=spends.Length==1?spends[0].target:0;
                    var allPowerEvents=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                    var powerEvents=allPowerEvents.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0).ToArray();
                    var allSummons=currentEvents.Where(e=>e.type=="Summon").ToArray();
                    var daggerSummons=allSummons.Where(e=>e.visual!=null&&e.visual.card_id=="CS2_082"&&e.card_type==7&&e.owner==0).ToArray();
                    var rogueWeapon=state.players[0].weapon;
                    int weapon=RogueWeaponIsEmpty(rogueWeapon)?0:rogueWeapon.entity;
                    Check(rogueBefore!=null&&rogueBefore.legalPowerMove&&rogueBefore.weaponEntity==0&&rogueBefore.powerUsed==false,
                        "Rogue authority snapshot begins with a legal unused power and no weapon");
                    Check(initial.players[0].power.card_id=="HERO_03bp"&&spends.Length==1&&playerEntity!=0&&
                        allPowerEvents.Length==1&&powerEvents.Length==1&&allSummons.Length==1&&daggerSummons.Length==1,
                        "authority records one HERO_03bp use, its two-mana payment, and one CS2_082 weapon Summon");
                    Check(!RogueWeaponIsEmpty(rogueWeapon)&&rogueWeapon.card_id=="CS2_082"&&
                        rogueWeapon.attack==1&&rogueWeapon.durability==2&&state.players[0].hero.attack==1&&weapon==daggerSummons[0].target&&
                        state.players[0].power.used&&!state.moves.Any(move=>move.kind=="power"),
                        "authority equips the summoned 1/2 CS2_082, spends two mana, and consumes the power exactly once");
                    Check(cast.CardId=="HERO_03bp"&&cast.Flow==null&&cast.Effect.Program.nodes.Length==3,
                        "H03 playback is the three-node source root without a parent Flow or Chain dispatcher");
                    Check(rogueSourceCueCount==1&&rogueSourceCueSemanticsValid&&rogueUnexpectedHeroCueCount==0,
                        "one source-only HERO_03bp cue has no target and creates no synthetic Hit/EquipWeapon cue");
                    Check(rogueSourceRootReleased&&rogueSourceStateActions>=2,
                        "authored source root reaches its terminal source action and releases after its cue expires");
                    Check(rogueSourceAudioPlayCount==1&&records.Any(record=>record.audioSource=="native-effect"&&record.message=="source play"&&!String.IsNullOrEmpty(record.file)),
                        "one original native source clip reaches the scene audio listener");
                    Check(RogueSourceUnlockReady(state)&&rogueInputUnlockedAfterSource,
                        "Rogue input unlocks after source release with a legal next action and matching hashes");
                    rogueAfter=RogueState(state);rogueDisplayHash=presentation.Display.public_hash;rogueSummonTarget=weapon;
                }
                else if(Priest)
                {
                    Check(cast.Flow.CastRequested&&cast.Flow.Completed,"actual Priest source-only graph completes after its authored Start/Impact sequence");
                    Check(cast.Events.Count(e=>e.Contains(" Birth "))==1&&cast.Events.Any(e=>e.Contains(" Action ")),"same submitted Priest graph records its source Birth and authored actions");
                    Check(cast.Flow.FirstArrivalTime==null&&cast.Flow.Flights.Count()==0&&
                        !cast.Events.Any(e=>e.Contains(" MissileBirth ")||e.Contains(" Arrival ")),
                        "Priest source-only playback emits no Missile or Arrival event");
                }
                else if(Warrior)
                {
                    var currentEvents=warriorCastAuthorityEvents??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
                    var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                    int playerEntity=spends.Length==1?spends[0].target:0;
                    var allPowerEvents=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                    var powerEvents=allPowerEvents.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
                    var allArmorEvents=currentEvents.Where(e=>e.type=="GainArmor").ToArray();
                    var armorEvents=allArmorEvents.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f).ToArray();
                    var powerSequence=currentEvents.Where(e=>e.type=="SpendMana"||e.type=="PlayHeroPower"||e.type=="GainArmor")
                        .Select(e=>e.type).ToArray();
                    warriorEventOrderValid=powerSequence.SequenceEqual(new[]{"SpendMana","PlayHeroPower","GainArmor"});
                    warriorPlayHeroPowerCount=allPowerEvents.Length;warriorGainArmorCount=allArmorEvents.Length;
                    Note("Warrior authority tuples spend="+String.Join(";",spends.Select(EventTuple).ToArray())+
                        " play="+String.Join(";",allPowerEvents.Select(EventTuple).ToArray())+
                        " armor="+String.Join(";",allArmorEvents.Select(EventTuple).ToArray()));
                    var rules=warriorAfter;int displayedHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1;
                    int displayedArmor=BattleInteraction.Entity(presentation.Display,ownHero)?.armor??-1;
                    Check(warriorCastAuthorityEvents!=null&&warriorAfter!=null&&warriorCastVersion>initial.version&&
                        warriorCastVersion==warriorAfter.version&&warriorFollowup.castAuthorityVersion==warriorCastVersion&&
                        warriorFollowup.castAuthorityEvents!=null&&warriorFollowup.castAuthorityEvents.Length==currentEvents.Length&&
                        initial.players[0].power.card_id=="HERO_01bp"&&warriorBefore!=null&&!warriorBefore.powerUsed&&
                        warriorBefore.heroArmor==0&&spends.Length==1&&playerEntity!=0&&allPowerEvents.Length==1&&powerEvents.Length==1&&
                        allArmorEvents.Length==1&&armorEvents.Length==1&&warriorEventOrderValid,
                        "accepted cast snapshot orders one two-mana payment, untargeted HERO_01bp use and one two-armor gain");
                    Check(rules.powerUsed&&rules.heroArmor==2&&rules.heroHealth==warriorBefore.heroHealth&&
                        warriorBefore.mana-rules.mana==2&&displayedHealth==rules.heroHealth&&displayedArmor==2,
                        "Warrior gains exactly two displayed armor without changing health and spends two mana");
                    Check(warriorSourceCueCount==1&&warriorSourceCueSemanticsValid&&warriorUnexpectedHitCueCount==0,
                        "HERO_01bp creates one source-only cue with no targeted Hit presentation");
                    Check(cast.Flow!=null&&cast.Flow.CastRequested&&cast.Flow.Completed&&cast.Flow.FirstArrivalTime==null&&
                        cast.Flow.Flights.Count()==0&&!cast.Events.Any(e=>e.Contains(" MissileBirth ")||e.Contains(" Arrival ")),
                        "Warrior source playback completes without a missile or Arrival target");
                    Check(warriorSourceRootReleased&&warriorParentCameraShakerStarts==1&&
                        warriorImpactCloneCameraShakerStarts==1&&warriorCameraShakerStarts==2,
                        "the authored Warrior parent and Impact each complete one 0.1-second delayed CameraShaker.StartShake");
                    Check(warriorUnhandledActions==0&&warriorUnboundAudioActions==0&&warriorSourceAudioPlayCount==2&&
                        records.Any(record=>record.phase=="warrior-direct-cast"&&record.audioSource=="native-effect"&&record.message=="source play"&&!String.IsNullOrEmpty(record.file)),
                        "both source-authored Warrior audio actions reach the scene Listener without unhandled actions");
                    Check(WarriorSourceUnlockReady(state)&&warriorInputUnlockedAfterSource,
                        "Warrior input unlocks after source release with a legal next action and matching hashes");
                    warriorDisplayHash=presentation.Display.public_hash;
                    warriorMissileEventCount=cast.Events.Count(e=>e.Contains("MissileBirth"));
                    warriorArrivalEventCount=cast.Events.Count(e=>e.Contains(" Arrival "));
                    var liveRules=WarriorState(state);
                    frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=liveRules.heroHealth,displayHealth=displayedHealth,
                        authorityArmor=liveRules.heroArmor,displayArmor=displayedArmor,time=Time.realtimeSinceStartup-started,
                        batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
                        sourceCompleted=warriorSourceRootCompleted,sourceReleased=warriorSourceRootReleased,
                        hashMatched=state.public_hash==presentation.Display.public_hash,endTurnAvailable=liveRules.legalEndTurn,
                        nativeEvents=String.Join(";",cast.Events)});
                }
                else if(Druid)
                {
                    Check(cast.Flow!=null&&cast.Flow.CastRequested&&cast.Flow.Completed&&cast.Flow.FirstArrivalTime==null&&
                        cast.Flow.Flights.Count()==0&&druidMissileEventCount==0&&druidArrivalEventCount==0,
                        "actual accepted HERO_06bp cast completes with missing FirstArrivalTime and zero Arrival events");
                }
                else if(Warlock)
                {
                    Check(cast.CardId=="HERO_07bp"&&cast.Flow==null,
                        "Warlock Life Tap uses the authored source-only root without constructing a target Flow");
                }
                else
                {
                    Check(cast.Flow.CastRequested&&cast.Flow.FirstArrivalTime.HasValue&&cast.Flow.Completed,"actual accepted graph completes cast and arrival");
                    Check(cast.Events.Count(e=>e.Contains(" Birth "))==1&&cast.Events.Count(e=>e.Contains(" Action "))==1,"same submitted graph has one Birth and Action");
                }
                Check(cast.UnhandledActions==0&&cast.UnboundAudioActions==0&&cast.MissingAudioPlaybackServices==0,"actual source action/audio services bound");
                Check(Warrior?warriorAfter!=null&&warriorAfter.powerUsed&&initial.players[1].power.used:
                    state.players[0].power.used&&state.players[1].power.used,"accepted authority consumes own power");
                if(Hunter)
                {
                    Check(cast.CameraShakerStarts==1,"authored Hunter Impact invokes CameraShaker.StartShake once after its source delay");
                    int health=BattleInteraction.Entity(state,targetEntity)?.health??-1;
                    Check(initialTargetHealth-health==2,"authoritative Hunter hero power deals exactly 2 damage: "+initialTargetHealth+" -> "+health);
                    Check(hitCallbacks==1,"one actual Hunter Hit presentation callback reaches the chosen target");
                    Check(cast.Events.Any(line=>line.Contains(" MissileBirth "))&&cast.Events.Any(line=>line.Contains(" Arrival "))&&cast.Events.Any(line=>line.Contains(" ImpactScheduled ")),
                        "native Hunter flight reaches target and schedules source impact");
                    var flight=cast.Flow.Flights.FirstOrDefault(item=>item.Path!=null);
                    int ownPower=initial.players[0].power.entity;
                    Check(initial.players[0].power.card_id=="HERO_05bp"&&
                        (state.events??new SemanticEvent[0]).Any(e=>e.type=="PlayHeroPower"&&e.target==ownPower&&e.owner==0)&&
                        (state.events??new SemanticEvent[0]).Any(e=>e.type=="Hit"&&e.source==ownPower&&e.target==targetEntity&&e.owner==1&&Mathf.Abs(e.amount-2)<.001f),
                        "authority links HERO_05bp PlayHeroPower and 2-damage Hit to the opponent hero");
                    Check(flight!=null&&Vector3.Distance(flight.Path.Points[2],flight.TargetPosition)<.002f,
                        "native root missile path terminates at the target resolved from the authoritative Hit");
                    Check(frames.Any(frame=>!frame.ready&&frame.hitCallbacks==0&&!frame.applied&&frame.displayHealth==initialTargetHealth),
                        "Hunter target stays unchanged before the source impact is ready");
                    Check(frames.Any(frame=>frame.ready&&frame.applied&&frame.hitCallbacks==1),"Hunter Hit callback and display application follow source readiness");
                    Check(frames.Where(frame=>!frame.ready).All(frame=>frame.inputLocked),"existing presentation input lock remains active before the original Hit resolves");
                    Check(!presentation.InputLocked&&!Field<bool>("pending")&&state.moves.Any(move=>move.kind=="end_turn"),
                        "after authoritative Hit feedback the GUI unlocks and exposes the next legal end-turn input");
                    frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=targetEntity,authorityHealth=health,
                        displayHealth=BattleInteraction.Entity(presentation.Display,targetEntity)?.health??-1,hitCallbacks=hitCallbacks,
                        damageDecorations=presentation.DamageDecorationCount,time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,
                        busy=presentation.Busy,inputLocked=presentation.InputLocked,arrival=true,nativeEvents=String.Join(";",cast.Events)});
                }
                if(Priest)
                {
                    int ownPower=initial.players[0].power.entity;
                    var currentEvents=state.events??new SemanticEvent[0];
                    var spendEvents=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                    var allPowerEvents=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                    int playerEntity=spendEvents.Length==1?spendEvents[0].target:0;
                    var powerEvents=allPowerEvents.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0).ToArray();
                    Note("Priest authority tuples spend="+String.Join(";",spendEvents.Select(EventTuple).ToArray())+
                        " play="+String.Join(";",allPowerEvents.Select(EventTuple).ToArray()));
                    var allHeals=currentEvents.Where(e=>e.type=="Heal").ToArray();
                    var heals=allHeals.Where(e=>e.source==ownPower&&e.target==targetEntity&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f).ToArray();
                    var allTargetEvents=currentEvents.Where(e=>e.type=="HeroPowerTarget").ToArray();
                    var targetEvents=allTargetEvents.Where(e=>e.source==ownPower&&e.target==targetEntity&&e.owner==0&&e.card_id=="HERO_09bp").ToArray();
                    int health=BattleInteraction.Entity(state,targetEntity)?.health??-1;
                    Check(initialTargetHealth==(PriestFullHealth?30:28)&&health==30,
                        PriestFullHealth?"authoritative full-health Priest target stays at 30 without a fabricated heal":
                        "authoritative Priest own-hero heal changes health exactly 28 to 30");
                    Check(initial.players[0].power.card_id=="HERO_09bp"&&spendEvents.Length==1&&playerEntity!=0&&
                        allPowerEvents.Length==1&&powerEvents.Length==1&&allTargetEvents.Length==1&&targetEvents.Length==1&&
                        allHeals.Length==(PriestFullHealth?0:1)&&heals.Length==(PriestFullHealth?0:1),
                        PriestFullHealth?"authority links exactly one HERO_09bp target and no Heal to the own-hero Impact":
                        "authority links one HERO_09bp use to exactly one target and its 2-point own-hero Heal");
                    Check(healCallbacks==(PriestFullHealth?0:1),"actual GUI dispatches only the authoritative Priest Heal, with no +0 callback");
                    Check(!PriestFullHealth||presentation.DamageDecorationCount==0&&
                        !presentation.Cues.Any(cue=>cue.data.type=="Heal"&&cue.data.target==targetEntity),
                        "full-health Priest target has no Heal cue or +0 damage decoration");
                    Check(priestImpactVisualCaptured&&priestImpactTargetEntity==targetEntity&&priestTargetGuiPositionValid,
                        "actual Start-to-Impact source clone renders at the authority target on both GUI-side layouts");
                    var sourceCues=records.Where(record=>record.audioSource=="native-effect"&&record.message=="source play").ToArray();
                    Check(sourceCues.Any(record=>!String.IsNullOrEmpty(record.file)),"actual Priest native source audio cue reaches Listener playback");
                    Check(PriestSourceUnlockReady(state)&&priestInputUnlockedAfterSource,
                        "actual Priest GUI unlocks with the next legal action after the source fully releases and hashes match");
                    priestFinalHealth=health;priestHealEventCount=allHeals.Length;priestHealCallbacks=healCallbacks;
                    priestTargetEventCount=allTargetEvents.Length;priestDamageDecorations=presentation.DamageDecorationCount;
                    priestNativeEvents=String.Join(";",cast.Events);
                    priestMissileEventCount=cast.Events.Count(e=>e.Contains("MissileBirth"));priestArrivalEventCount=cast.Events.Count(e=>e.Contains(" Arrival "));
                    priestPublicHash=state.public_hash;priestDisplayHash=presentation.Display.public_hash;
                    frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=targetEntity,authorityHealth=health,
                        displayHealth=BattleInteraction.Entity(presentation.Display,targetEntity)?.health??-1,healEvents=allHeals.Length,
                        healCallbacks=healCallbacks,damageDecorations=presentation.DamageDecorationCount,time=Time.realtimeSinceStartup-started,
                        batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
                        arrival=cast.Flow.FirstArrivalTime.HasValue,nativeEvents=String.Join(";",cast.Events)});
                }
                else if(Druid)
                {
                    var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
                    var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                    int playerEntity=spends.Length==1?spends[0].target:0;
                    var allPowers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                    var powers=allPowers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
                    var buffs=currentEvents.Where(e=>e.type=="Buff").ToArray();
                    var linkedBuffs=buffs.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&e.visual!=null&&e.visual.attack==1).ToArray();
                    var allArmor=currentEvents.Where(e=>e.type=="GainArmor").ToArray();
                    var armor=allArmor.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-1)<.001f).ToArray();
                    var forbiddenEvents=currentEvents.Where(e=>e.type=="Hit"||e.type=="Summon"||e.type=="EquipWeapon"||
                        e.type=="MissileBirth"||e.type=="Arrival").ToArray();
                    var rules=DruidState(state);int displayedHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1;
                    int displayedAttack=BattleInteraction.Entity(presentation.Display,ownHero)?.attack??-1;
                    int displayedArmor=BattleInteraction.Entity(presentation.Display,ownHero)?.armor??-1;
                    Note("Druid authority tuples spend="+String.Join(";",spends.Select(EventTuple).ToArray())+
                        " play="+String.Join(";",allPowers.Select(EventTuple).ToArray())+
                        " buff="+String.Join(";",buffs.Select(EventTuple).ToArray())+
                        " armor="+String.Join(";",allArmor.Select(EventTuple).ToArray()));
                    Check(druidBefore!=null&&initial.players[0].power.card_id=="HERO_06bp"&&druidBefore.heroAttack==0&&
                        druidBefore.heroArmor==0&&!druidBefore.powerUsed&&spends.Length==1&&playerEntity!=0&&
                        allPowers.Length==1&&powers.Length==1&&buffs.Length==1&&linkedBuffs.Length==1&&
                        allArmor.Length==1&&armor.Length==1&&currentEvents.Length==4&&druidEventOrderValid,
                        "authority orders one two-mana payment, untargeted HERO_06bp use, +1 hero attack and +1 armor");
                    Check(rules.powerUsed&&rules.heroAttack==1&&rules.heroArmor==1&&rules.heroHealth==druidBefore.heroHealth&&
                        druidBefore.mana-rules.mana==2&&displayedAttack==1&&displayedArmor==1&&displayedHealth==rules.heroHealth,
                        "Druid gains exactly one displayed attack and armor without changing health and spends two mana");
                    Check(forbiddenEvents.Length==0&&druidSourceCueCount==1&&druidSourceCueSemanticsValid&&druidUnexpectedHeroCueCount==0,
                        "HERO_06bp uses one no-target source cue without Hit, target, weapon or summon cues");
                    Check(druidCastRequested&&druidCastCompleted&&druidFirstArrivalTimeMissing&&
                        cast.Flow.FirstArrivalTime==null&&cast.Flow.Flights.Count()==0&&druidMissileEventCount==0&&druidArrivalEventCount==0&&
                        !cast.Events.Any(e=>e.Contains("StartBirth")||e.Contains("MissileBirth")||e.Contains(" Arrival ")),
                        "Druid source playback completes with missing FirstArrivalTime and zero Missile or Arrival events");
                    Check(druidSourceStartActionCount==1&&druidImpactCloneCount==1&&druidImpactFinishedNotificationCount==1&&
                        druidSourceAnimationActionCount==1&&druidSourceParticleActionCount==1,
                        "HERO_06bp dispatches one StartAction and completed Impact source with its authored animation and particle actions");
                    Check(druidSourceRootCompleted&&druidSourceRootReleased,
                        "Druid source root completes and releases after its source cue");
                    Check(druidUnhandledActions==0&&druidUnboundAudioActions==0&&druidMissingAudioPlaybackServices==0&&
                        druidSourceAudioActionCount==1&&druidSourceAudioPlayCount==1&&
                        druidAudioRecordStartIndex>=0&&records.Skip(druidAudioRecordStartIndex).Any(record=>record.audioSource=="native-effect"&&
                            record.message=="source play"&&record.file=="74537c37a8e26deff87c5a4a.wav"),
                        "one authored Druid audio action invokes the native default clip through the scene Listener");
                    Check(DruidSourceUnlockReady(state)&&druidInputUnlockedAfterSource,
                        "Druid input unlocks after source release with a legal end-turn action and matching hashes");
                    Check(state.public_hash==presentation.Display.public_hash,"Druid displayed public state reaches authority");
                    druidAfter=rules;druidDisplayHash=presentation.Display.public_hash;
                    frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=rules.heroHealth,
                        displayHealth=displayedHealth,authorityAttack=rules.heroAttack,displayAttack=displayedAttack,
                        authorityArmor=rules.heroArmor,displayArmor=displayedArmor,time=Time.realtimeSinceStartup-started,
                        batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
                        sourceCompleted=druidSourceRootCompleted,sourceReleased=druidSourceRootReleased,
                        hashMatched=state.public_hash==presentation.Display.public_hash,endTurnAvailable=rules.legalEndTurn,
                        nativeEvents=String.Join(";",cast.Events)});
                }
                else if(Warlock)
                {
                    var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
                    var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
                    int playerEntity=spends.Length==1?spends[0].target:0;
                    var allPowers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
                    var powers=allPowers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
                    var allHits=currentEvents.Where(e=>e.type=="Hit").ToArray();
                    var hits=allHits.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f).ToArray();
                    var allDraws=currentEvents.Where(e=>e.type=="Draw").ToArray();
                    var draws=allDraws.Where(e=>e.source==ownPower&&e.target!=0&&e.owner==0).ToArray();
                    warlockEventOrderValid=currentEvents.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Hit","Draw"})&&
                        spends.Length==1&&powers.Length==1&&hits.Length==1&&draws.Length==1;
                    warlockSpendManaCount=spends.Length;warlockPlayHeroPowerCount=allPowers.Length;warlockHitCount=allHits.Length;warlockDrawCount=allDraws.Length;
                    var rules=WarlockState(state);int displayedHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1;
                    int displayedHand=presentation.Display.players[0].hand==null?0:presentation.Display.players[0].hand.Length;
                    Note("Warlock authority tuples spend="+String.Join(";",spends.Select(EventTuple).ToArray())+
                        " play="+String.Join(";",allPowers.Select(EventTuple).ToArray())+
                        " hit="+String.Join(";",allHits.Select(EventTuple).ToArray())+
                        " draw="+String.Join(";",allDraws.Select(EventTuple).ToArray()));
                    Check(warlockBefore!=null&&initial.players[0].power.card_id=="HERO_07bp"&&warlockBefore.powerUsed==false&&
                        spends.Length==1&&playerEntity!=0&&allPowers.Length==1&&powers.Length==1&&allHits.Length==1&&hits.Length==1&&
                        allDraws.Length==1&&draws.Length==1&&warlockEventOrderValid,
                        "authority orders one two-mana payment, untargeted HERO_07bp use, self Hit and one Draw");
                    Check(rules.powerUsed&&warlockBefore.heroHealth-rules.heroHealth==2&&warlockBefore.mana-rules.mana==2&&
                        rules.handCount==warlockBefore.handCount+1&&rules.deckCount==warlockBefore.deckCount-1&&
                        displayedHealth==rules.heroHealth&&displayedHand==rules.handCount,
                        "Warlock Life Tap deals exactly two self-damage, draws one card and spends two mana");
                    Check(warlockSourceCueCount==1&&warlockSourceCueSemanticsValid,
                        "HERO_07bp routes one untargeted source-only visual cue without fabricated target selection");
                    Check(cast.Flow==null&&warlockSourceRootCompleted&&warlockSourceWaitActionCount==1&&
                        warlockSourceSpellStateActionCount==2&&warlockSourceRootReleased&&
                        warlockMissileEventCount==0&&warlockArrivalEventCount==0,
                        "Warlock source FSM completes its authored Wait and enters None before release, without a parent SuperSpell missile or Arrival");
                    Check(warlockUnhandledActions==0&&warlockUnboundAudioActions==0&&
                        warlockSourceAudioActionCount==1&&warlockSourceAudioPlayCount==1&&
                        records.Skip(warlockAudioRecordStartIndex).Any(record=>record.audioSource=="native-effect"&&
                            record.message=="source play"&&record.file=="54ef1e4a536ef9d7d3565210.wav"),
                        "one authored Warlock AudioPlayClipAction reaches the scene Listener once from its native clip");
                    Check(WarlockSourceUnlockReady(state)&&warlockInputUnlockedAfterSource,
                        "Warlock input unlocks after source release with a legal end-turn action and matching hashes");
                    Check(state.public_hash==presentation.Display.public_hash,"Warlock displayed public state reaches authority");
                    warlockAfter=rules;warlockDisplayHash=presentation.Display.public_hash;
                    frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=rules.heroHealth,
                        displayHealth=displayedHealth,time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,
                        busy=presentation.Busy,inputLocked=presentation.InputLocked,sourceCompleted=warlockSourceRootCompleted,
                        sourceReleased=warlockSourceRootReleased,hashMatched=state.public_hash==presentation.Display.public_hash,
                        endTurnAvailable=rules.legalEndTurn,nativeEvents=String.Join(";",cast.Events)});
                }
                Check(state.public_hash==presentation.Display.public_hash,"driver displayed public state reaches authority");
                if(Review)CheckReview();
                Check(!audio.Sources.Any(s=>s!=null&&s.isPlaying),"all native audio sources stop after source completion");
                Check(tap.SampleFrames>0&&tap.Peak>.00001f,"actual scene DSP callback contains nonzero PCM");
                Check(tap.Overflows==0,"audio callback buffers retain every captured block");
                if(RogueWeapons&&role=="driver"){castChecksDone=true;StartRogueWeaponAttack(state,1);return;}
                tap.StopCapture();castChecksDone=true;
                if(Hunter||Priest||Rogue||Warrior||Druid||Warlock)
                {
                    Invoke("ResetMenu");
                    Advance(8,"scene-exit");
                }
                return;
            }
            if(Review&&!followupSent)
            {
                if(presentation.InputLocked||presentation.DamageDecorationCount==0)return;
                if(!CaptureReady("07-input-unlocked-damage-splat"))return;
                var followup=state.moves.FirstOrDefault(m=>m.kind=="end_turn");
                Check(followup!=null,"a legal follow-up move exists after Fireblast Hit feedback");
                Check(!presentation.InputLocked&&presentation.DamageDecorationCount==1,"real GUI reports input open while Fireblast DamageSplat remains active");
                Note("follow-up accepted while busy="+presentation.Busy+" damageDecorations="+presentation.DamageDecorationCount+" nativeActive="+presentation.Cues.Any(c=>c.data.type=="SpellEffect"&&effects.SourceBusy(state.match_id+":"+state.version+":"+c.data.source+":"+c.start)));
                Invoke("SendMove",followup);
                Check(Field<bool>("pending")&&Field<int>("actionCounter")==initialCounter+2,"ClientApp accepts a legal follow-up through SendMove before decorative tail ends");
                followupVersion=state.version;followupSent=true;return;
            }
            if(Review)
            {
                if(state.version<=followupVersion||presentation.Busy||state.public_hash!=presentation.Display.public_hash||presentation.DamageDecorationCount!=0)return;
                if(!followupTailExpired)
                {
                    if(!CaptureReady("08-damage-splat-expired"))return;
                    Check(!presentation.InputLocked&&presentation.DamageDecorationCount==0,"DamageSplat expires independently after accepted follow-up");
                    Note("follow-up authority version="+state.version+"; display hash matched; DamageSplat expired");
                    string marker=Path.Combine(Directory.GetParent(folder).FullName,"followup-accepted.txt");
                    File.WriteAllText(marker+".tmp",state.version+":"+state.public_hash);File.Move(marker+".tmp",marker);
                    followupTailExpired=true;
                }
                string peer=Path.Combine(Directory.GetParent(folder).FullName,"observer-ready.txt");if(!File.Exists(peer))return;
                Advance(71,"peer-completion-wait");return;
            }
            if(!CaptureReady("06-power-used"))return;
            Note("accepted graph cast="+cast.Flow.CastRequested+" arrival="+cast.Flow.FirstArrivalTime+" completed="+cast.Flow.Completed+" cancelled="+cast.Flow.Cancelled+" events="+String.Join(";",cast.Events));
            Check(cast.Flow.CastRequested&&cast.Flow.FirstArrivalTime.HasValue&&cast.Flow.Completed,"actual accepted graph completes cast and arrival");
            Check(cast.Events.Count(e=>e.Contains(" Birth "))==1&&cast.Events.Count(e=>e.Contains(" Action "))==1,"same submitted graph has one Birth and Action");
            Check(cast.UnhandledActions==0&&cast.UnboundAudioActions==0&&cast.MissingAudioPlaybackServices==0,"actual source action/audio services bound");
            Check(state.players[0].power.used&&state.players[1].power.used,"accepted authority consumes own power");
            Check(state.public_hash==presentation.Display.public_hash,"driver displayed public state reaches authority");
            Check(!audio.Sources.Any(s=>s!=null&&s.isPlaying),"all native audio sources stop after source completion");
            Check(tap.SampleFrames>0&&tap.Peak>.00001f,"actual scene DSP callback contains nonzero PCM");
            Check(tap.Overflows==0,"audio callback buffers retain every captured block");
            tap.StopCapture();Invoke("ResetMenu");Advance(8,"scene-exit");return;
        }
    }
    void TrackReview(StateView state)
    {
        var hit=presentation.Cues.FirstOrDefault(c=>c.data.type=="Hit"&&c.data.target==reviewTarget);
        if(hit==null)return;
        float tint=presentation.Tint(reviewTarget);if(!hit.Ready){sawWaiting=true;if(tint!=0||hit.fired||hit.applied)earlyFeedback=true;}
        float age=presentation.TimeInBatch-hit.start,motion=Mathf.Abs(presentation.Animate(reviewTarget,new Rect(0,0,100,100)).x),shake=presentation.Shake.magnitude;
        if(hit.Ready)
        {
            if(Mathf.Abs(age)<.00001f)sawReactionStart=true;
            if(tint>0)observedHitAge=Mathf.Max(observedHitAge,age);
            if(motion>.00001f){firstMotionAge=Mathf.Min(firstMotionAge,age);lastMotionAge=Mathf.Max(lastMotionAge,age);}
            if(age>=.28f&&tint!=0||age>=hit.duration&&motion>.00001f||age>=.16f&&shake>.00001f)invalidReactionWindow=true;
            if(age>=hit.duration&&tint==0&&motion==0&&shake==0)sawReactionEnd=true;
        }
        var source=presentation.Cues.FirstOrDefault(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_08bp");
        var key=source==null?null:state.match_id+":"+state.version+":"+source.data.source+":"+source.start;
        var root=key==null?null:effects.SourceSpell(key);
        if(role=="driver"&&root!=null&&root.Events.Any(e=>e.Contains(" MissileBirth "))&&!root.Flow.FirstArrivalTime.HasValue)
        {
            if(!capturedFlightMid&&root.Flow.Time>=1.33f){Capture("review-flight-mid");capturedFlightMid=true;}
            if(!capturedFlightLate&&root.Flow.Time>=1.43f){Capture("review-flight-late");capturedFlightLate=true;}
        }
        frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=reviewTarget,authorityHealth=BattleInteraction.Entity(state,reviewTarget)?.health??-1,displayHealth=BattleInteraction.Entity(presentation.Display,reviewTarget)?.health??-1,
            hitCallbacks=hitCallbacks,damageDecorations=presentation.DamageDecorationCount,time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,hitStart=hit.start,hitDuration=hit.duration,tint=tint,motion=motion,shake=shake,speed=presentation.Speed,captureDelta=Time.captureDeltaTime,frameDeltas=app.EvidenceFrameDeltas,lateDelta=Time.unscaledDeltaTime,ready=hit.Ready,applied=hit.applied,busy=presentation.Busy,inputLocked=presentation.InputLocked,arrival=root?.Flow.FirstArrivalTime.HasValue??false,nativeEvents=root==null?null:String.Join(";",root.Events)});
        if(Time.realtimeSinceStartup-lastFrameCapture>.35f){lastFrameCapture=Time.realtimeSinceStartup;Capture("review-cast-frame-"+Time.frameCount);}
    }
    void BindHunterCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_05bp"&&item.data.source==source);
        if(cue==null)return;
        string key=state.match_id+":"+state.version+":"+source+":"+cue.start;
        cast=effects.SourceSpell(key);
        if(cast!=null)Note("bound authority HERO_05bp playback key="+key+" events="+String.Join(";",cast.Events));
    }
    void BindPriestCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_09bp"&&item.data.source==source);
        if(cue==null)return;
        string key=state.match_id+":"+state.version+":"+source+":"+cue.start;
        cast=effects.SourceSpell(key);
        if(cast!=null)Note("bound authority HERO_09bp playback key="+key+" events="+String.Join(";",cast.Events));
    }
    void BindRogueCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_03bp"&&item.data.source==source);
        if(cue==null)return;
        rogueSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start;
        cast=effects.SourceSpell(rogueSourceKey);
        if(cast!=null)Note("bound authority HERO_03bp source-only playback key="+rogueSourceKey);
    }
    void BindWarriorCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_01bp"&&item.data.source==source);
        if(cue==null)return;
        warriorSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start;
        if(warriorCastVersion<0)
        {warriorCastVersion=state.version;warriorFollowup.powerAcceptedTime=Time.realtimeSinceStartup-started;}
        cast=effects.SourceSpell(warriorSourceKey);
        if(cast!=null)
        {
            Note("bound authority HERO_01bp source-only playback key="+warriorSourceKey+
                " powerAcceptedTime="+warriorFollowup.powerAcceptedTime.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    void BindDruidCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_06bp"&&item.data.source==source);
        if(cue==null)return;
        druidSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start;
        cast=effects.SourceSpell(druidSourceKey);
        if(cast!=null)Note("bound authority HERO_06bp source-only playback key="+druidSourceKey);
    }
    void BindWarlockCast(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cue=presentation.Cues.FirstOrDefault(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_07bp"&&item.data.source==source);
        if(cue==null)return;
        warlockSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start;
        cast=effects.SourceSpell(warlockSourceKey);
        if(cast!=null)Note("bound authority HERO_07bp source-only playback key="+warlockSourceKey);
    }
    void BindPaladinArea(StateView state)
    {
        if(initial==null||presentation==null||effects==null)return;
        int source=initial.players[0].power.entity;
        var cues=presentation.Cues.Where(item=>item.data.type=="SpellEffect"&&item.data.card_id=="HERO_04bp"&&item.data.source==source).ToArray();
        if(cues.Length==0)return;
        var cue=cues[0];paladinSourceKey=state.match_id+":"+state.version+":"+source+":"+cue.start;
        paladinSourceCueCount=cues.Length;
        paladinSourceCueSemanticsValid=cues.Length==1&&cue.data.target==0&&cue.data.owner==0&&cue.apply==float.MaxValue;
        Note("bound authority HERO_04bp FriendlyArea cue key="+paladinSourceKey+" cues="+cues.Length);
    }
    void TrackPaladin(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;
        if(!paladinCastEvidenceLocked)
        {
            var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
            int playerEntity=spends.Length==1?spends[0].target:0;
            var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
            var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
            var summons=currentEvents.Where(e=>e.type=="Summon").ToArray();
            var recruits=summons.Where(e=>e.source==ownPower&&e.target!=0&&e.card_type==4&&e.owner==0&&e.visual!=null&&e.visual.card_id=="CS2_101t").ToArray();
            paladinSpendManaCount=spends.Length;paladinPlayHeroPowerCount=powers.Length;paladinSummonCount=summons.Length;paladinRecruitSummonCount=recruits.Length;
            var ruleEvents=currentEvents.Where(e=>e.type=="SpendMana"||e.type=="PlayHeroPower"||e.type=="Summon").Select(e=>e.type).ToArray();
            paladinEventOrderValid=currentEvents.Length==3&&ruleEvents.SequenceEqual(new[]{"SpendMana","PlayHeroPower","Summon"})&&
                spends.Length==1&&linkedPowers.Length==1&&summons.Length==1&&recruits.Length==1;
            paladinAfter=PaladinState(state);paladinDisplayHash=presentation.Display==null?null:presentation.Display.public_hash;
        }
        var sourceCues=presentation.Cues.Where(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_04bp"&&c.data.source==ownPower).ToArray();
        paladinSourceCueCount=Math.Max(paladinSourceCueCount,sourceCues.Length);
        if(sourceCues.Length==1)paladinSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].data.owner==0&&sourceCues[0].apply==float.MaxValue;
        else if(sourceCues.Length>1)paladinSourceCueSemanticsValid=false;
        if(!String.IsNullOrEmpty(paladinSourceKey))
        {
            var area=effects.SourceArea(paladinSourceKey);
            paladinParentFlowConstructed=paladinParentFlowConstructed||effects.SourceSpell(paladinSourceKey)!=null;
            if(area!=null)
            {
                paladinAreaSeen=true;paladinAreaReference=area.Reference;
                paladinFriendlyAreaOnlyReconstruction=area.FriendlyAreaOnlyReconstruction;
                paladinAreaCloneCreations=area.AreaCloneCreations;paladinAreaFinishedNotifications=area.FinishedNotifications;
                paladinAreaPending=area.Pending;paladinUnhandledActions=area.Effect==null?paladinUnhandledActions:area.Effect.UnhandledActions;
                paladinUnboundAudioActions=area.Effect==null?paladinUnboundAudioActions:area.Effect.UnboundAudioActions;
                if(area.Effect!=null)
                {
                    var actions=area.Effect.ActionExecutions;int clip,oneShot,random;
                    actions.TryGetValue("AudioPlayClipAction",out clip);actions.TryGetValue("AudioPlayAction",out oneShot);
                    actions.TryGetValue("AudioPlayRandomClipAction",out random);paladinAudioActionCount=clip+oneShot+random;
                }
                paladinAudioPlayCount=paladinAudioRecordStartIndex<0?0:records.Skip(paladinAudioRecordStartIndex).Count(record=>
                    record.audioSource=="native-effect"&&record.message=="source play");
                paladinAreaCompleted=paladinFriendlyAreaOnlyReconstruction&&paladinAreaCloneCreations==1&&
                    paladinAreaFinishedNotifications==1&&paladinAreaPending==0;
            }
            else if(paladinAreaSeen)paladinAreaReleased=true;
        }
        bool hashMatched=presentation.Display!=null&&state.public_hash==presentation.Display.public_hash;
        bool endTurnAvailable=state.seat==0&&state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        bool sourceTailActive=!String.IsNullOrEmpty(paladinSourceKey)&&effects!=null&&effects.SourceVisualTailActive(paladinSourceKey);
        if(state.version<=initial.version||(step!=72&&Time.realtimeSinceStartup-lastFrameCapture<.06f))return;
        lastFrameCapture=Time.realtimeSinceStartup;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=0,activePlayer=state.active,authorityHealth=state.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,initial.players[0].hero.entity)?.health??-1,
            time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            sourceCompleted=paladinAreaCompleted,sourceReleased=paladinAreaReleased,sourceTailActive=sourceTailActive,
            turnStart=currentEvents.Any(e=>e.type=="TURN_START"),hashMatched=hashMatched,endTurnAvailable=endTurnAvailable,
            nativeEvents=String.Join(",",currentEvents.Select(e=>e.type).ToArray())};
        frames.Add(frame);
        if(PaladinTailInputReady(state)&&!paladinInputUnlockedAfterArea)
        {
            paladinInputUnlockedAfterArea=true;paladinInputUnlockSampleFrame=frame.frame;paladinInputUnlockSampleTime=frame.time;
            Note("Paladin input unlock during FriendlyArea visual tail frame="+frame.frame+" busy="+frame.busy+" inputLocked="+frame.inputLocked+
                " tailActive="+frame.sourceTailActive+" hashMatched="+frame.hashMatched+" endTurn="+frame.endTurnAvailable);
        }
    }
    void TrackWarrior(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
        var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
        int playerEntity=spends.Length==1?spends[0].target:0;
        var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
        var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
        var armorEvents=currentEvents.Where(e=>e.type=="GainArmor").ToArray();
        var linkedArmor=armorEvents.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f).ToArray();
        var powerSequence=currentEvents.Where(e=>e.type=="SpendMana"||e.type=="PlayHeroPower"||e.type=="GainArmor")
            .Select(e=>e.type).ToArray();
        if(warriorCastAuthorityEvents==null&&state.version>initial.version&&state.players[0].power.used&&
           powers.Any(e=>e.target==ownPower&&e.owner==0))
        {
            warriorCastAuthorityEvents=currentEvents.Select(item=>JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(item))).ToArray();
            warriorCastVersion=state.version;warriorFollowup.castAuthorityVersion=state.version;
            warriorFollowup.castAuthorityEvents=warriorCastAuthorityEvents;
            warriorEventOrderValid=powerSequence.SequenceEqual(new[]{"SpendMana","PlayHeroPower","GainArmor"})&&
                spends.Length==1&&linkedPowers.Length==1&&armorEvents.Length==1&&linkedArmor.Length==1;
            warriorPlayHeroPowerCount=powers.Length;warriorGainArmorCount=armorEvents.Length;
            warriorAfter=WarriorState(state);
            Note("captured accepted Warrior cast authority snapshot version="+warriorCastVersion+" events="+
                String.Join(";",powerSequence));
        }
        var sourceCues=presentation.Cues.Where(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_01bp"&&c.data.source==ownPower).ToArray();
        warriorSourceCueCount=Math.Max(warriorSourceCueCount,sourceCues.Length);
        if(sourceCues.Length==1)
            warriorSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].data.owner==0&&
                Mathf.Approximately(sourceCues[0].apply,sourceCues[0].start)&&Mathf.Abs(sourceCues[0].duration-1.55f)<.001f;
        else if(sourceCues.Length>1)warriorSourceCueSemanticsValid=false;
        warriorUnexpectedHitCueCount=Math.Max(warriorUnexpectedHitCueCount,presentation.Cues.Count(c=>c.data.type=="Hit"&&c.data.source==ownPower));
        warriorDisplayHash=presentation.Display==null?null:presentation.Display.public_hash;
        if(cast!=null&&cast.CardId=="HERO_01bp"&&cast.Flow!=null)
        {
            warriorSourceRootCompleted=cast.Flow.Completed;
            warriorSourceRootReleased=warriorSourceRootCompleted&&effects.SourceSpell(warriorSourceKey)==null;
            warriorMissileEventCount=cast.Events.Count(e=>e.Contains("MissileBirth"));
            warriorArrivalEventCount=cast.Events.Count(e=>e.Contains(" Arrival "));
            warriorParentCameraShakerStarts=cast.ParentCameraShakerStarts;
            warriorImpactCloneCameraShakerStarts=cast.ImpactCloneCameraShakerStarts;
            warriorCameraShakerStarts=cast.CameraShakerStarts;
            warriorParentUnhandledActions=cast.ParentUnhandledActions;
            warriorImpactCloneUnhandledActions=cast.ImpactCloneUnhandledActions;
            warriorParentUnhandledActionDetails=cast.ParentUnhandledActionDetails.ToArray();
            warriorImpactCloneUnhandledActionDetails=cast.ImpactCloneUnhandledActionDetails.ToArray();
            warriorUnhandledActions=cast.UnhandledActions;
            warriorUnboundAudioActions=cast.UnboundAudioActions;
            warriorSourceAudioPlayCount=records.Count(record=>record.phase=="warrior-direct-cast"&&record.audioSource=="native-effect"&&record.message=="source play");
        }
        if(warriorFollowup.sendMoveSubmitted&&!warriorFollowup.authorityAccepted&&state.version>warriorFollowupSubmitVersion)
        {
            warriorFollowup.authorityAccepted=state.active==1&&state.turn>warriorFollowupSubmitTurn&&state.seat==0;
            if(warriorFollowup.authorityAccepted)
            {
                warriorFollowup.acceptedVersion=state.version;warriorFollowup.activeAfter=state.active;
                warriorFollowup.publicHash=state.public_hash;
            }
        }
        bool hashMatched=state.public_hash==warriorDisplayHash;
        bool endTurnAvailable=state.seat==0&&state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        if(warriorFollowup.authorityAccepted&&warriorSourceRootReleased&&!presentation.Busy&&!presentation.InputLocked&&hashMatched)
        {
            warriorFollowup.sourceTailCleanedAfterFollowup=true;
            warriorFollowup.displayHash=presentation.Display.public_hash;
        }
        if(state.version<=initial.version||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        int authorityArmor=state.players[0].hero.armor;
        int displayArmor=BattleInteraction.Entity(presentation.Display,ownHero)?.armor??-1;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=state.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1,authorityArmor=authorityArmor,displayArmor=displayArmor,
            time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            sourceCompleted=warriorSourceRootCompleted,sourceReleased=warriorSourceRootReleased,hashMatched=hashMatched,endTurnAvailable=endTurnAvailable,
            sourceActive=effects.SourceBusy(warriorSourceKey),followupSent=warriorFollowup.sendMoveSubmitted,
            followupAccepted=warriorFollowup.authorityAccepted,followupTailCleaned=warriorFollowup.sourceTailCleanedAfterFollowup,
            actionCounter=Field<int>("actionCounter"),followupSendLatency=warriorFollowup.sendLatency,
            publicHash=state.public_hash,displayHash=presentation.Display==null?null:presentation.Display.public_hash,
            nativeEvents=cast==null?null:String.Join(";",cast.Events)};
        frames.Add(frame);
    }
    void TrackDruid(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
        var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
        int playerEntity=spends.Length==1?spends[0].target:0;
        var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
        var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
        var buffs=currentEvents.Where(e=>e.type=="Buff").ToArray();
        var linkedBuffs=buffs.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&e.visual!=null&&e.visual.attack==1).ToArray();
        var armorEvents=currentEvents.Where(e=>e.type=="GainArmor").ToArray();
        var linkedArmor=armorEvents.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-1)<.001f).ToArray();
        druidEventOrderValid=currentEvents.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Buff","GainArmor"})&&
            spends.Length==1&&linkedPowers.Length==1&&buffs.Length==1&&linkedBuffs.Length==1&&armorEvents.Length==1&&linkedArmor.Length==1;
        druidSpendManaCount=spends.Length;druidPlayHeroPowerCount=powers.Length;druidBuffCount=buffs.Length;druidGainArmorCount=armorEvents.Length;
        druidHitEventCount=currentEvents.Count(e=>e.type=="Hit");
        var sourceCues=presentation.Cues.Where(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_06bp"&&c.data.source==ownPower).ToArray();
        druidSourceCueCount=Math.Max(druidSourceCueCount,sourceCues.Length);
        if(sourceCues.Length==1)
            druidSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].data.owner==0&&
                Mathf.Approximately(sourceCues[0].apply,sourceCues[0].start)&&
                Mathf.Abs(sourceCues[0].duration-effects.Duration("HERO_06bp"))<.001f;
        else if(sourceCues.Length>1)druidSourceCueSemanticsValid=false;
        druidUnexpectedHeroCueCount=Math.Max(druidUnexpectedHeroCueCount,presentation.Cues.Count(c=>
            c.data.type=="Hit"&&c.data.source==ownPower||c.data.type=="SpellEffect"&&c.data.card_id=="HERO_06bp"&&
            !(c.data.source==ownPower&&c.data.target==0&&c.data.owner==0&&Mathf.Approximately(c.apply,c.start))));
        druidAfter=DruidState(state);druidDisplayHash=presentation.Display==null?null:presentation.Display.public_hash;
        if(cast!=null&&cast.CardId=="HERO_06bp"&&cast.Flow!=null)
        {
            druidCastRequested=cast.Flow.CastRequested;druidCastCompleted=cast.Flow.Completed;
            druidFirstArrivalTimeMissing=!cast.Flow.FirstArrivalTime.HasValue;
            druidSourceRootCompleted=cast.Flow.Completed;
            druidSourceRootReleased=druidSourceRootCompleted&&effects.SourceSpell(druidSourceKey)==null;
            druidSourceStartActionCount=cast.Events.Count(e=>e.Contains(" StartAction "));
            druidImpactCloneCount=cast.Events.Count(e=>e.Contains(" ImpactClone "));
            druidImpactFinishedNotificationCount=cast.Events.Count(e=>e.Contains(" ImpactFinishedNotification "));
            druidMissileEventCount=cast.Events.Count(e=>e.Contains("MissileBirth"));
            druidArrivalEventCount=cast.Events.Count(e=>e.Contains(" Arrival "));
            cast.ActionExecutions.TryGetValue("AnimationPlaythroughAction",out druidSourceAnimationActionCount);
            cast.ActionExecutions.TryGetValue("ParticlePlayAction",out druidSourceParticleActionCount);
            cast.ActionExecutions.TryGetValue("SpellCardIdAudioPlayAction",out druidSourceAudioActionCount);
            druidUnhandledActions=cast.UnhandledActions;
            druidUnboundAudioActions=cast.UnboundAudioActions;
            druidMissingAudioPlaybackServices=cast.MissingAudioPlaybackServices;
            druidSourceAudioPlayCount=druidAudioRecordStartIndex<0?0:records.Skip(druidAudioRecordStartIndex).Count(record=>
                record.audioSource=="native-effect"&&record.message=="source play"&&record.file=="74537c37a8e26deff87c5a4a.wav");
        }
        bool hashMatched=state.public_hash==druidDisplayHash;
        bool endTurnAvailable=state.seat==0&&state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        if(state.version<=initial.version||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        int authorityAttack=state.players[0].hero.attack,authorityArmor=state.players[0].hero.armor;
        int displayAttack=BattleInteraction.Entity(presentation.Display,ownHero)?.attack??-1;
        int displayArmor=BattleInteraction.Entity(presentation.Display,ownHero)?.armor??-1;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=state.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1,authorityAttack=authorityAttack,displayAttack=displayAttack,
            authorityArmor=authorityArmor,displayArmor=displayArmor,time=Time.realtimeSinceStartup-started,
            batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            sourceCompleted=druidSourceRootCompleted,sourceReleased=druidSourceRootReleased,hashMatched=hashMatched,endTurnAvailable=endTurnAvailable,
            nativeEvents=cast==null?null:String.Join(";",cast.Events)};
        frames.Add(frame);
        if(role=="driver"&&druidSourceRootReleased&&!presentation.InputLocked&&endTurnAvailable&&hashMatched&&!druidInputUnlockedAfterSource)
        {
            druidInputUnlockedAfterSource=true;druidInputUnlockSampleFrame=frame.frame;druidInputUnlockSampleTime=frame.time;
            Note("Druid post-source unlock sample frame="+frame.frame+" time="+frame.time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " busy="+frame.busy+" inputLocked="+frame.inputLocked+" hashMatched="+frame.hashMatched+" endTurn="+frame.endTurnAvailable);
        }
    }
    void TrackWarlock(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];int ownPower=initial.players[0].power.entity;int ownHero=initial.players[0].hero.entity;
        var spends=currentEvents.Where(e=>e.type=="SpendMana"&&e.source==ownPower&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f&&e.target!=0).ToArray();
        int playerEntity=spends.Length==1?spends[0].target:0;
        var powers=currentEvents.Where(e=>e.type=="PlayHeroPower").ToArray();
        var linkedPowers=powers.Where(e=>e.source==playerEntity&&e.target==ownPower&&e.owner==0&&String.IsNullOrEmpty(e.card_id)).ToArray();
        var hits=currentEvents.Where(e=>e.type=="Hit").ToArray();
        var linkedHits=hits.Where(e=>e.source==ownPower&&e.target==ownHero&&e.owner==0&&Mathf.Abs(e.amount-2)<.001f).ToArray();
        var draws=currentEvents.Where(e=>e.type=="Draw").ToArray();
        var linkedDraws=draws.Where(e=>e.source==ownPower&&e.target!=0&&e.owner==0).ToArray();
        warlockEventOrderValid=currentEvents.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Hit","Draw"})&&
            spends.Length==1&&linkedPowers.Length==1&&linkedHits.Length==1&&linkedDraws.Length==1;
        warlockSpendManaCount=spends.Length;warlockPlayHeroPowerCount=powers.Length;warlockHitCount=hits.Length;warlockDrawCount=draws.Length;
        var sourceCues=presentation.Cues.Where(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_07bp"&&c.data.source==ownPower).ToArray();
        warlockSourceCueCount=Math.Max(warlockSourceCueCount,sourceCues.Length);
        if(sourceCues.Length==1)warlockSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].data.owner==0&&
            Mathf.Approximately(sourceCues[0].apply,float.MaxValue)&&Mathf.Abs(sourceCues[0].duration-effects.Duration("HERO_07bp"))<.001f;
        else if(sourceCues.Length>1)warlockSourceCueSemanticsValid=false;
        warlockAfter=WarlockState(state);warlockDisplayHash=presentation.Display==null?null:presentation.Display.public_hash;
        if(cast!=null&&cast.CardId=="HERO_07bp"&&cast.Effect!=null)
        {
            cast.Effect.ActionExecutions.TryGetValue("Wait",out warlockSourceWaitActionCount);
            cast.Effect.ActionExecutions.TryGetValue("SpellStateAction",out warlockSourceSpellStateActionCount);
            // The reviewed root enters Play FX, waits four seconds, then enters NoneState.
            // SourceSpellFinished belongs to SpellController callbacks and is not this FSM's completion signal.
            warlockSourceRootCompleted=warlockSourceWaitActionCount==1&&warlockSourceSpellStateActionCount==2;
            warlockSourceRootReleased=warlockSourceRootCompleted&&effects.SourceSpell(warlockSourceKey)==null;
            warlockUnhandledActions=cast.UnhandledActions;warlockUnboundAudioActions=cast.UnboundAudioActions;
            cast.Effect.ActionExecutions.TryGetValue("AudioPlayClipAction",out warlockSourceAudioActionCount);
            warlockSourceAudioPlayCount=warlockAudioRecordStartIndex<0?0:records.Skip(warlockAudioRecordStartIndex).Count(record=>
                record.audioSource=="native-effect"&&record.message=="source play"&&record.file=="54ef1e4a536ef9d7d3565210.wav");
        }
        warlockMissileEventCount=cast==null?0:cast.Events.Count(e=>e.Contains("MissileBirth"));
        warlockArrivalEventCount=cast==null?0:cast.Events.Count(e=>e.Contains(" Arrival "));
        bool hashMatched=state.public_hash==warlockDisplayHash;
        bool endTurnAvailable=state.seat==0&&state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        if(state.version<=initial.version||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=0,authorityHealth=state.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,ownHero)?.health??-1,
            time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            sourceCompleted=warlockSourceRootCompleted,sourceReleased=warlockSourceRootReleased,hashMatched=hashMatched,
            endTurnAvailable=endTurnAvailable,nativeEvents=cast==null?null:String.Join(";",cast.Events)};
        frames.Add(frame);
        if(role=="driver"&&warlockSourceRootReleased&&!presentation.InputLocked&&endTurnAvailable&&hashMatched&&!warlockInputUnlockedAfterSource)
        {
            warlockInputUnlockedAfterSource=true;warlockInputUnlockSampleFrame=frame.frame;warlockInputUnlockSampleTime=frame.time;
            Note("Warlock post-source unlock sample frame="+frame.frame+" time="+frame.time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " busy="+frame.busy+" inputLocked="+frame.inputLocked+" hashMatched="+frame.hashMatched+" endTurn="+frame.endTurnAvailable);
        }
    }
    void TrackRogue(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];
        roguePlayHeroPowerCount=currentEvents.Count(e=>e.type=="PlayHeroPower");
        if(RogueWeaponsShaman&&state.version!=rogueLastPowerCountedVersion&&
            currentEvents.Count(e=>e.type=="PlayHeroPower"&&e.target==initial.players[0].power.entity&&e.owner==0)==1)
        {rogueWeaponPowerUseCount++;rogueLastPowerCountedVersion=state.version;}
        var summons=currentEvents.Where(e=>e.type=="Summon").ToArray();
        var daggers=summons.Where(e=>e.visual!=null&&e.visual.card_id=="CS2_082"&&e.card_type==7&&e.owner==0).ToArray();
        rogueSummonCount=summons.Length;rogueDaggerSummonCount=daggers.Length;
        if(daggers.Length==1)rogueSummonTarget=daggers[0].target;
        if(!RogueWeapons||!rogueDaggerSnapshotCaptured)
        {
            var sourceCues=presentation.Cues.Where(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_03bp"&&c.data.source==initial.players[0].power.entity).ToArray();
            rogueSourceCueCount=Math.Max(rogueSourceCueCount,sourceCues.Length);
            if(sourceCues.Length==1)
                rogueSourceCueSemanticsValid=sourceCues[0].data.target==0&&sourceCues[0].apply==float.MaxValue;
            else if(sourceCues.Length>1)rogueSourceCueSemanticsValid=false;
            rogueUnexpectedHeroCueCount=Math.Max(rogueUnexpectedHeroCueCount,presentation.Cues.Count(c=>
                c.data.type=="Hit"||c.data.type=="EquipWeapon"||c.data.card_id=="HERO_03bp"&&
                !(c.data.type=="SpellEffect"&&c.data.source==initial.players[0].power.entity&&c.data.target==0&&c.apply==float.MaxValue)));
        }
        if(!RogueWeapons||!rogueWeaponLifecycleStarted&&!rogueDaggerSnapshotCaptured)
        {
            rogueAfter=RogueState(state);rogueDisplayHash=presentation.Display==null?null:presentation.Display.public_hash;
            rogueDaggerSnapshotCaptured=rogueAfter.powerUsed&&rogueAfter.weaponCardId=="CS2_082"&&presentation.Display!=null&&
                state.public_hash==presentation.Display.public_hash;
        }
        int stateActions=0;
        if(cast!=null)
        {
            rogueSourceRootNodeCount=cast.Effect.Program.nodes.Length;
            rogueSourceOnly=cast.CardId=="HERO_03bp"&&cast.Flow==null&&rogueSourceRootNodeCount==3;
            rogueParentFlowConstructed=cast.Flow!=null;
            cast.Effect.ActionExecutions.TryGetValue("SpellStateAction",out stateActions);
            rogueSourceStateActions=stateActions;
            rogueSourceAudioPlayCount=records.Count(record=>record.audioSource=="native-effect"&&record.message=="source play");
            rogueSourceRootReleased=stateActions>=2&&effects.SourceSpell(rogueSourceKey)==null;
        }
        bool hashMatched=state.public_hash==rogueDisplayHash;
        bool endTurnAvailable=state.seat==0&&state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        bool sourceCompleted=stateActions>=2;
        if(state.version<=initial.version||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=rogueSummonTarget,authorityHealth=state.players[0].hero.health,
            displayHealth=BattleInteraction.Entity(presentation.Display,initial.players[0].hero.entity)?.health??-1,
            time=Time.realtimeSinceStartup-started,batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            sourceCompleted=sourceCompleted,sourceReleased=rogueSourceRootReleased,hashMatched=hashMatched,endTurnAvailable=endTurnAvailable};
        frames.Add(frame);
        if(role=="driver"&&rogueSourceRootReleased&&!presentation.InputLocked&&endTurnAvailable&&hashMatched&&!rogueInputUnlockedAfterSource)
        {
            rogueInputUnlockedAfterSource=true;rogueInputUnlockSampleFrame=frame.frame;rogueInputUnlockSampleTime=frame.time;
            Note("Rogue post-source unlock sample frame="+frame.frame+" time="+frame.time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " busy="+frame.busy+" inputLocked="+frame.inputLocked+" hashMatched="+frame.hashMatched+" endTurn="+frame.endTurnAvailable);
        }
    }
    void TrackHunter(StateView state)
    {
        var hit=presentation.Cues.FirstOrDefault(c=>c.data.type=="Hit"&&c.data.target==targetEntity);
        if(hit==null||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        frames.Add(new Frame{frame=Time.frameCount,version=state.version,target=targetEntity,
            authorityHealth=BattleInteraction.Entity(state,targetEntity)?.health??-1,
            displayHealth=BattleInteraction.Entity(presentation.Display,targetEntity)?.health??-1,
            hitCallbacks=hitCallbacks,damageDecorations=presentation.DamageDecorationCount,time=Time.realtimeSinceStartup-started,
            batchTime=presentation.TimeInBatch,hitStart=hit.start,hitDuration=hit.duration,ready=hit.Ready,applied=hit.applied,
            busy=presentation.Busy,inputLocked=presentation.InputLocked,arrival=cast!=null&&cast.Flow.FirstArrivalTime.HasValue,
            nativeEvents=cast==null?null:String.Join(";",cast.Events)});
    }
    void TrackPriest(StateView state)
    {
        var currentEvents=state.events??new SemanticEvent[0];
        priestFinalHealth=BattleInteraction.Entity(state,targetEntity)?.health??-1;
        priestHealEventCount=currentEvents.Count(e=>e.type=="Heal");
        priestHealCallbacks=healCallbacks;priestPublicHash=state.public_hash;priestDisplayHash=presentation.Display?.public_hash;
        bool sourceCompleted=cast!=null&&cast.Flow.Completed;
        bool sourceReleased=sourceCompleted&&!presentation.Busy;
        bool hashMatched=state.public_hash==presentation.Display.public_hash;
        bool endTurnAvailable=state.moves!=null&&state.moves.Any(move=>move.kind=="end_turn");
        if(cast!=null)
        {
            priestMissileEventCount=cast.Events.Count(e=>e.Contains("MissileBirth"));
            priestArrivalEventCount=cast.Events.Count(e=>e.Contains(" Arrival "));
        }
        if(state.version<=initial.version||Time.realtimeSinceStartup-lastFrameCapture<.06f)return;
        lastFrameCapture=Time.realtimeSinceStartup;
        var frame=new Frame{frame=Time.frameCount,version=state.version,target=targetEntity,
            authorityHealth=BattleInteraction.Entity(state,targetEntity)?.health??-1,
            displayHealth=BattleInteraction.Entity(presentation.Display,targetEntity)?.health??-1,
            healCallbacks=healCallbacks,healEvents=priestHealEventCount,
            damageDecorations=presentation.DamageDecorationCount,time=Time.realtimeSinceStartup-started,
            batchTime=presentation.TimeInBatch,busy=presentation.Busy,inputLocked=presentation.InputLocked,
            arrival=cast!=null&&cast.Flow.FirstArrivalTime.HasValue,sourceCompleted=sourceCompleted,sourceReleased=sourceReleased,
            hashMatched=hashMatched,endTurnAvailable=endTurnAvailable,nativeEvents=cast==null?null:String.Join(";",cast.Events)};
        frames.Add(frame);
        if(role=="driver"&&sourceReleased&&!presentation.InputLocked&&endTurnAvailable&&hashMatched&&!priestInputUnlockedAfterSource)
        {
            priestInputUnlockedAfterSource=true;priestInputUnlockSampleFrame=frame.frame;
            priestInputUnlockSampleTime=frame.time;priestInputUnlockBatchTime=frame.batchTime;
            Note("Priest post-source unlock sample frame="+frame.frame+" time="+frame.time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " batchTime="+frame.batchTime.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " busy="+frame.busy+" inputLocked="+frame.inputLocked+" hashMatched="+frame.hashMatched+" endTurn="+frame.endTurnAvailable);
        }
    }
    void CheckReview()
    {
        Check(sawWaiting&&!earlyFeedback,"no damage tint/callback/HP application before native Arrival in actual GUI");
        Check(hitCallbacks==1,"one actual Hit callback at Arrival");
        Check(sawReactionStart&&sawReactionEnd&&!invalidReactionWindow&&observedHitAge>0&&lastMotionAge-firstMotionAge>=.25f,"actual GUI brackets full Hit duration with correct tint/motion/shake windows");
        int originalHealth=BattleInteraction.Entity(initial,reviewTarget).health;
        Check(frames.Any(f=>!f.ready&&f.displayHealth==originalHealth)&&frames.Any(f=>f.ready&&f.applied&&f.displayHealth==originalHealth-1),"displayed HP transitions once from original to authoritative damage");
    }
    void Finish(string result,int exit)
    {
        if(finished)return;finished=true;finishResult=result;finishExit=exit;tap?.StopCapture();
        if(Review||Hunter||Priest||Rogue||Warrior||Druid||Warlock||Paladin||Shaman)File.WriteAllText(Path.Combine(folder,"timeline.json"),JsonUtility.ToJson(new Timeline{frames=frames.ToArray()},true));
        if(RogueWeapons)
        {
            rogueWeaponLifecycle.role=role;rogueWeaponLifecycle.points=rogueWeaponLifecyclePoints.ToArray();
            rogueWeaponLifecycle.concessionSynchronized=rogueWeaponConcessionSynchronized;
            File.WriteAllText(Path.Combine(folder,"rogue-weapon-lifecycle.json"),JsonUtility.ToJson(rogueWeaponLifecycle,true));
        }
        if(effects!=null&&effects.PowerSelection!=null)
        {
            var selection=effects.PowerSelection;
            Note("selection diagnostic time="+selection.Flow.Time+" cancelled="+selection.Flow.TargetingCancelled+" busy="+selection.Busy+" events="+String.Join(";",selection.Events));
        }
        finishedReport=new Report{result=result,role=role,publicHash=publicHash,displayHash=Priest?priestDisplayHash:RogueWeapons?publicHash:Rogue?rogueDisplayHash:Warrior?warriorDisplayHash:Druid?druidDisplayHash:Warlock?warlockDisplayHash:Paladin?paladinDisplayHash:null,heroPower=TestedPower,diffuseLightingInputs=NativeDiffuseInputs.LocalStatus,
            priestMode=Priest?(PriestFullHealth?"full-health-no-heal":"damaged-target-heal"):null,priestNativeEvents=priestNativeEvents??(cast==null?"":String.Join(";",cast.Events)),
            priestTargetEventCount=priestTargetEventCount,priestImpactTargetEntity=priestImpactTargetEntity,priestImpactVisualCaptured=priestImpactVisualCaptured,
            priestTargetGuiPositionValid=priestTargetGuiPositionValid,priestTargetGuiX=priestTargetGuiX,priestTargetGuiY=priestTargetGuiY,
            priestTargetGuiPositionError=priestTargetGuiPositionError,priestDamageDecorations=priestDamageDecorations,checks=checks,width=Screen.width,height=Screen.height,rate=tap==null?0:tap.Rate,channels=tap==null?0:tap.Channels,
            dspBuffer=tap==null?0:tap.DspBuffer,dspBuffers=tap==null?0:tap.DspBuffers,callbacks=tap==null?0:tap.Callbacks,overflows=tap==null?0:tap.Overflows,
            sampleFrames=tap==null?0:tap.SampleFrames,clippedSamples=tap==null?0:tap.ClippedSamples,peak=tap==null?0:tap.Peak,rms=tap==null?0:tap.Rms,
            listenerCount=role=="driver"?listenerCount:0,targetEntity=targetEntity,initialTargetHealth=initialTargetHealth,finalTargetHealth=priestFinalHealth,
            healEventCount=priestHealEventCount,healCallbacks=priestHealCallbacks,missileEventCount=priestMissileEventCount,arrivalEventCount=priestArrivalEventCount,
            sourceAudioPlayCount=records.Count(record=>record.audioSource=="native-effect"&&record.message=="source play"),
            sourceAudioStopCount=records.Count(record=>record.audioSource=="native-effect"&&record.message=="source stop"),
            inputUnlockedAfterSource=Warlock?warlockInputUnlockedAfterSource:priestInputUnlockedAfterSource,inputUnlockSampleFrame=Warlock?warlockInputUnlockSampleFrame:priestInputUnlockSampleFrame,
            inputUnlockSampleTime=priestInputUnlockSampleTime,inputUnlockBatchTime=priestInputUnlockBatchTime,
            roguePlayHeroPowerCount=roguePlayHeroPowerCount,rogueSummonCount=rogueSummonCount,rogueDaggerSummonCount=rogueDaggerSummonCount,
            rogueSummonTarget=rogueSummonTarget,rogueUnexpectedHeroCueCount=rogueUnexpectedHeroCueCount,rogueSourceCueCount=rogueSourceCueCount,
            rogueSourceRootNodeCount=rogueSourceRootNodeCount,rogueSourceStateActions=rogueSourceStateActions,
            rogueSourceAudioPlayCount=rogueSourceAudioPlayCount,rogueInputUnlockSampleFrame=rogueInputUnlockSampleFrame,
            rogueInputUnlockSampleTime=rogueInputUnlockSampleTime,rogueSourceOnly=rogueSourceOnly,rogueParentFlowConstructed=rogueParentFlowConstructed,
            rogueSourceCueSemanticsValid=rogueSourceCueSemanticsValid,
            rogueSourceRootReleased=rogueSourceRootReleased,rogueInputUnlockedAfterSource=rogueInputUnlockedAfterSource,
             rogueDisplayHash=rogueDisplayHash,rogueBefore=rogueBefore,rogueAfter=rogueAfter,rogueWeaponsMode=RogueWeapons,
             rogueWeaponsShamanMode=RogueWeaponsShaman,rogueWeaponPowerUseCount=rogueWeaponPowerUseCount,
             rogueObserverShamanPowerCount=rogueObserverShamanPowerCount,rogueObserverShamanTotemSequence=rogueObserverShamanTotemSequence,
             rogueWeaponReplacementCount=rogueWeaponReplacementCount,rogueWeaponEmptySlotEquipVersion=rogueWeaponEmptySlotEquipVersion,
             rogueWeaponOccupiedReplacementVersion=rogueWeaponOccupiedReplacementVersion,
             rogueWeaponEndTurnGreenAfterOccupiedReplacement=rogueWeaponEndTurnGreenAfterOccupiedReplacement,
             rogueWeaponOnlyEndTurnAfterOccupiedReplacement=rogueWeaponOnlyEndTurnAfterOccupiedReplacement,
             rogueWeaponResultReadyOnBothPeers=rogueWeaponResultReadyOnBothPeers,
             rogueWeaponSlot7Events=rogueWeaponSlot7Events,rogueWeaponSlot5Events=rogueWeaponSlot5Events,
             rogueWeaponSlot7AudioPlayCount=rogueWeaponSlot7AudioPlayCount,rogueWeaponSlot5AudioPlayCount=rogueWeaponSlot5AudioPlayCount,
             rogueWeaponInputUnlockSampleFrame=rogueWeaponInputUnlockSampleFrame,rogueWeaponInputUnlockedAfterAttack=rogueWeaponInputUnlockedAfterAttack,
             rogueWeaponLifecycleComplete=rogueWeaponLifecycleComplete,rogueWeaponDisplayHash=rogueWeaponDisplayHash,
             rogueWeaponAttacks=rogueWeaponAttacks.ToArray(),rogueWeaponAudioRecords=records.Where(record=>record.audioSource=="native-weapon"&&record.message.StartsWith("weapon source play",StringComparison.Ordinal)).ToArray(),
            warriorPlayHeroPowerCount=warriorPlayHeroPowerCount,warriorGainArmorCount=warriorGainArmorCount,
            warriorSourceCueCount=warriorSourceCueCount,
            warriorUnexpectedHitCueCount=warriorUnexpectedHitCueCount,warriorMissileEventCount=warriorMissileEventCount,
            warriorArrivalEventCount=warriorArrivalEventCount,warriorCameraShakerStarts=warriorCameraShakerStarts,
            warriorParentCameraShakerStarts=warriorParentCameraShakerStarts,warriorImpactCloneCameraShakerStarts=warriorImpactCloneCameraShakerStarts,
            warriorUnhandledActions=warriorUnhandledActions,warriorParentUnhandledActions=warriorParentUnhandledActions,
            warriorImpactCloneUnhandledActions=warriorImpactCloneUnhandledActions,
            warriorParentUnhandledActionDetails=warriorParentUnhandledActionDetails,
            warriorImpactCloneUnhandledActionDetails=warriorImpactCloneUnhandledActionDetails,warriorUnboundAudioActions=warriorUnboundAudioActions,
            warriorInputUnlockSampleFrame=warriorInputUnlockSampleFrame,warriorInputUnlockSampleTime=warriorInputUnlockSampleTime,
            warriorSourceAudioPlayCount=warriorSourceAudioPlayCount,warriorEventOrderValid=warriorEventOrderValid,
            warriorSourceCueSemanticsValid=warriorSourceCueSemanticsValid,warriorSourceRootCompleted=warriorSourceRootCompleted,
            warriorSourceRootReleased=warriorSourceRootReleased,warriorInputUnlockedAfterSource=warriorInputUnlockedAfterSource,
            warriorBefore=warriorBefore,warriorAfter=warriorAfter,warriorFollowup=warriorFollowup,
            druidSpendManaCount=druidSpendManaCount,druidPlayHeroPowerCount=druidPlayHeroPowerCount,
            druidBuffCount=druidBuffCount,druidGainArmorCount=druidGainArmorCount,
            druidHitEventCount=druidHitEventCount,druidUnexpectedHeroCueCount=druidUnexpectedHeroCueCount,druidMissileEventCount=druidMissileEventCount,
            druidArrivalEventCount=druidArrivalEventCount,druidSourceCueCount=druidSourceCueCount,
            druidSourceStartActionCount=druidSourceStartActionCount,druidImpactCloneCount=druidImpactCloneCount,
            druidImpactFinishedNotificationCount=druidImpactFinishedNotificationCount,
            druidSourceAnimationActionCount=druidSourceAnimationActionCount,druidSourceParticleActionCount=druidSourceParticleActionCount,
            druidSourceAudioActionCount=druidSourceAudioActionCount,druidSourceAudioPlayCount=druidSourceAudioPlayCount,
            druidUnhandledActions=druidUnhandledActions,druidUnboundAudioActions=druidUnboundAudioActions,
            druidMissingAudioPlaybackServices=druidMissingAudioPlaybackServices,
            druidInputUnlockSampleFrame=druidInputUnlockSampleFrame,druidInputUnlockSampleTime=druidInputUnlockSampleTime,
            druidEventOrderValid=druidEventOrderValid,druidSourceCueSemanticsValid=druidSourceCueSemanticsValid,
            druidCastRequested=druidCastRequested,druidCastCompleted=druidCastCompleted,druidFirstArrivalTimeMissing=druidFirstArrivalTimeMissing,
            druidSourceRootCompleted=druidSourceRootCompleted,druidSourceRootReleased=druidSourceRootReleased,
            druidInputUnlockedAfterSource=druidInputUnlockedAfterSource,druidDisplayHash=druidDisplayHash,
            druidBefore=druidBefore,druidAfter=druidAfter,
            druidSourceAudioRecords=druidAudioRecordStartIndex<0?new Record[0]:records.Skip(druidAudioRecordStartIndex).Where(record=>
                record.audioSource=="native-effect"&&record.message=="source play"&&record.file=="74537c37a8e26deff87c5a4a.wav").ToArray(),
            records=records.ToArray()};
        if(Warlock)
        {
            var warlockReport=new WarlockEvidence{result=result,role=role,publicHash=publicHash,displayHash=warlockDisplayHash,heroPower=TestedPower,
                checks=checks,width=Screen.width,height=Screen.height,rate=tap==null?0:tap.Rate,channels=tap==null?0:tap.Channels,
                dspBuffer=tap==null?0:tap.DspBuffer,dspBuffers=tap==null?0:tap.DspBuffers,callbacks=tap==null?0:tap.Callbacks,
                overflows=tap==null?0:tap.Overflows,listenerCount=role=="driver"?listenerCount:0,sampleFrames=tap==null?0:tap.SampleFrames,
                clippedSamples=tap==null?0:tap.ClippedSamples,peak=tap==null?0:tap.Peak,rms=tap==null?0:tap.Rms,
                warlockSpendManaCount=warlockSpendManaCount,warlockPlayHeroPowerCount=warlockPlayHeroPowerCount,
                warlockHitCount=warlockHitCount,warlockDrawCount=warlockDrawCount,warlockSourceCueCount=warlockSourceCueCount,
                warlockSourceWaitActionCount=warlockSourceWaitActionCount,warlockSourceSpellStateActionCount=warlockSourceSpellStateActionCount,
                warlockSourceAudioActionCount=warlockSourceAudioActionCount,warlockSourceAudioPlayCount=warlockSourceAudioPlayCount,
                warlockUnhandledActions=warlockUnhandledActions,warlockUnboundAudioActions=warlockUnboundAudioActions,
                warlockMissileEventCount=warlockMissileEventCount,warlockArrivalEventCount=warlockArrivalEventCount,
                warlockInputUnlockSampleFrame=warlockInputUnlockSampleFrame,warlockInputUnlockSampleTime=warlockInputUnlockSampleTime,
                warlockEventOrderValid=warlockEventOrderValid,warlockSourceCueSemanticsValid=warlockSourceCueSemanticsValid,
                warlockSourceRootCompleted=warlockSourceRootCompleted,warlockSourceRootReleased=warlockSourceRootReleased,
                warlockInputUnlockedAfterSource=warlockInputUnlockedAfterSource,warlockBefore=warlockBefore,warlockAfter=warlockAfter,
                warlockSourceAudioRecords=warlockAudioRecordStartIndex<0?new Record[0]:records.Skip(warlockAudioRecordStartIndex).Where(record=>
                    record.audioSource=="native-effect"&&record.message=="source play").ToArray()};
            File.WriteAllText(Path.Combine(folder,"warlock-evidence.json"),JsonUtility.ToJson(warlockReport,true));
        }
        if(Paladin)
        {
            var paladinReport=new PaladinEvidence{result=result,role=role,publicHash=paladinAfter==null?publicHash:paladinAfter.publicHash,displayHash=paladinDisplayHash,heroPower=TestedPower,
                paladinAreaReference=paladinAreaReference,checks=checks,width=Screen.width,height=Screen.height,
                rate=tap==null?0:tap.Rate,channels=tap==null?0:tap.Channels,dspBuffer=tap==null?0:tap.DspBuffer,dspBuffers=tap==null?0:tap.DspBuffers,
                callbacks=tap==null?0:tap.Callbacks,overflows=tap==null?0:tap.Overflows,listenerCount=listenerCount,
                sampleFrames=tap==null?0:tap.SampleFrames,clippedSamples=tap==null?0:tap.ClippedSamples,peak=tap==null?0:tap.Peak,rms=tap==null?0:tap.Rms,
                paladinSpendManaCount=paladinSpendManaCount,paladinPlayHeroPowerCount=paladinPlayHeroPowerCount,
                paladinSummonCount=paladinSummonCount,paladinRecruitSummonCount=paladinRecruitSummonCount,
                paladinSourceCueCount=paladinSourceCueCount,paladinAreaCloneCreations=paladinAreaCloneCreations,
                paladinAreaFinishedNotifications=paladinAreaFinishedNotifications,paladinAreaPending=paladinAreaPending,
                paladinAudioActionCount=paladinAudioActionCount,paladinAudioPlayCount=paladinAudioPlayCount,
                paladinUnhandledActions=paladinUnhandledActions,paladinUnboundAudioActions=paladinUnboundAudioActions,
                paladinInputUnlockSampleFrame=paladinInputUnlockSampleFrame,paladinInputUnlockSampleTime=paladinInputUnlockSampleTime,
                paladinEventOrderValid=paladinEventOrderValid,paladinSourceCueSemanticsValid=paladinSourceCueSemanticsValid,
                paladinFriendlyAreaOnlyReconstruction=paladinFriendlyAreaOnlyReconstruction,
                paladinParentFlowConstructed=paladinParentFlowConstructed,paladinAreaCompleted=paladinAreaCompleted,
                paladinAreaReleased=paladinAreaReleased,paladinInputUnlockedAfterArea=paladinInputUnlockedAfterArea,
                paladinTurnSubmitVersion=paladinTurnSubmitVersion,paladinTailSubmitFrame=paladinTailSubmitFrame,
                paladinNextTurnVersion=paladinNextTurnVersion,paladinNextTurnAppliedFrame=paladinNextTurnAppliedFrame,
                paladinNextTurnFrame=paladinNextTurnFrame,paladinTailReleaseFrame=paladinTailReleaseFrame,
                paladinNextTurnPublicHash=paladinNextTurnPublicHash,paladinNextTurnDisplayHash=paladinNextTurnDisplayHash,
                paladinTailEndTurnSubmitted=paladinTailEndTurnSubmitted,paladinNextTurnAppliedDuringTail=paladinNextTurnAppliedDuringTail,
                paladinInputOpenDuringNextTurnTail=paladinInputOpenDuringNextTurnTail,paladinTailReleasedAfterNextTurn=paladinTailReleasedAfterNextTurn,
                paladinBefore=paladinBefore,paladinAfter=paladinAfter,
                paladinSourceAudioRecords=paladinAudioRecordStartIndex<0?new Record[0]:records.Skip(paladinAudioRecordStartIndex).Where(record=>
                    record.audioSource=="native-effect"&&record.message=="source play").ToArray()};
            File.WriteAllText(Path.Combine(folder,"paladin-evidence.json"),JsonUtility.ToJson(paladinReport,true));
        }
        if(Shaman&&role=="driver")
        {
            var shamanReport=new ShamanEvidence{result=result,role=role,publicHash=publicHash,displayHash=shamanDisplayHash,heroPower=TestedPower,
                totemCardId=shamanTotemCardId,diagnostics=shamanDiagnostics,checks=checks,width=Screen.width,height=Screen.height,rate=tap==null?0:tap.Rate,
                channels=tap==null?0:tap.Channels,dspBuffer=tap==null?0:tap.DspBuffer,dspBuffers=tap==null?0:tap.DspBuffers,
                callbacks=tap==null?0:tap.Callbacks,overflows=tap==null?0:tap.Overflows,listenerCount=listenerCount,
                sampleFrames=tap==null?0:tap.SampleFrames,clippedSamples=tap==null?0:tap.ClippedSamples,totemEntity=shamanTotemEntity,
                spendManaCount=shamanSpendManaCount,playHeroPowerCount=shamanPlayHeroPowerCount,summonCount=shamanSummonCount,
                nativeActiveCount=shamanNativeActiveCount,runtimeFailures=shamanRuntimeFailures,sourceAudioPlayCount=shamanSourceAudioPlayCount,
                inputUnlockSampleFrame=shamanInputUnlockSampleFrame,inputUnlockSampleTime=shamanInputUnlockSampleTime,
                inputUnlockBatchTime=shamanInputUnlockBatchTime,eventOrderValid=shamanEventOrderValid,eventLinkValid=shamanEventLinkValid,
                nativeActorVisible=shamanNativeActorVisible,actorVisualHiddenDuringSource=shamanActorVisualHiddenDuringSource,
                actorVisualRestoredAfterSource=shamanActorVisualRestoredAfterSource,
                inputUnlockedDuringSource=shamanInputUnlockedDuringSource,displayHashMatched=shamanDisplayHashMatched,
                sourceAudioRecords=records.Skip(shamanAudioRecordStartIndex<0?records.Count:shamanAudioRecordStartIndex)
                    .Where(record=>record.audioSource=="native-effect"&&record.message=="source play").ToArray()};
            File.WriteAllText(Path.Combine(folder,"shaman-evidence.json"),JsonUtility.ToJson(shamanReport,true));
        }
        if(!(Hunter&&role=="driver"))PublishReportResult(result);
        if(role=="observer")
        {
            string completed=Path.Combine(Directory.GetParent(folder).FullName,"observer-complete.txt");
            File.WriteAllText(completed+".tmp","complete");File.Move(completed+".tmp",completed);
        }
        if(IsPairedPriestRole&&exit==0)
        {finishResult=result;finishExit=exit;peerFinishDeadline=Time.realtimeSinceStartup+15f;Debug.Log("FIREBLAST FORMAL WAIT peer="+PeerRole+" checks="+checks);return;}
        if(Hunter&&role=="driver")
        {hunterDiagnosticFinalizing=true;hunterDiagnosticDeadline=Time.realtimeSinceStartup+10f;return;}
        Debug.Log("FIREBLAST FORMAL "+result+" checks="+checks);Application.Quit(exit);
    }
    void PublishReportResult(string result)
    {
        if(finishedReport==null)return;
        finishedReport.result=result;
        string reportPath=Path.Combine(folder,"report.json"),reportTemp=reportPath+".tmp";
        File.WriteAllText(reportTemp,JsonUtility.ToJson(finishedReport,true));
        if(File.Exists(reportPath))File.Delete(reportPath);
        File.Move(reportTemp,reportPath);
        if(role=="driver")PublishDriverCompletion(result);
    }
    void PublishDriverCompletion(string result)
    {
        string marker=Path.Combine(Directory.GetParent(folder).FullName,"driver-complete.txt"),temp=marker+".tmp";
        File.WriteAllText(temp,result);
        if(File.Exists(marker))File.Delete(marker);
        File.Move(temp,marker);
    }
    void OnDestroy(){tap?.StopCapture();}
}
