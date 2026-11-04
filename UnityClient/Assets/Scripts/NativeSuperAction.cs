using System;

public sealed class NativeSuperActionServices
{
    public Func<NativeAreaInfo> DetermineArea;
    public Action<NativeAreaInfo> SpawnArea;
    public Func<bool> HasMissile,HasImpact,HasChain,MissilesInSequence;
    public Func<int> VisualTargetCount,CurrentTargetIndex;
    public Action SpawnChainMissile,SpawnMissileInSequence,SpawnAllMissiles,SpawnAllImpacts,SpawnChain,DoStartSpellAction;
    public Action<int> SpawnImpact;
    public Func<int> PendingEffects;
    public Func<int,bool> HasStateContent;
    public Action BaseSpellFinished,BaseStateFinished,CleanUp;
}

// Original SuperSpell.DoActionNow and completion gate. Services are original
// operations, not normalized game events or invented timing substitutes.
public sealed class NativeSuperAction
{
    readonly NativeSuperActionServices services;
    public bool SettingUpAction;
    public bool PendingSpellFinish{get;private set;}
    public bool PendingNoneStateChange{get;private set;}
    public NativeSuperAction(NativeSuperActionServices services){this.services=services;}
    public bool AreEffectsActive(){return services.PendingEffects()>0;}
    public void UpdatePendingStateChangeFlags(int type)
    {
        bool pending=!services.HasStateContent(type);
        PendingNoneStateChange=pending;PendingSpellFinish=pending;
    }
    public void OnSpellFinished()
    {
        if(AreEffectsActive()){PendingSpellFinish=true;return;}
        services.BaseSpellFinished();
    }
    public void OnStateFinished(int guessedNextState)
    {
        if(guessedNextState==0&&AreEffectsActive()){PendingNoneStateChange=true;return;}
        services.BaseStateFinished();
    }
    public void OnFsmStateStarted(int activeStateChange,int type,Action baseStateStarted)
    {
        if(activeStateChange==type)return;
        if(type==0&&AreEffectsActive())
        {PendingSpellFinish=true;PendingNoneStateChange=true;return;}
        baseStateStarted();
    }
    public bool CanPurge(int activeCloneCount,Func<bool> baseCanPurge)
    {return activeCloneCount>0?false:baseCanPurge();}
    public void FinishIfPossible()
    {
        if(SettingUpAction||AreEffectsActive())return;
        if(PendingSpellFinish){services.BaseSpellFinished();PendingSpellFinish=false;}
        if(PendingNoneStateChange){services.BaseStateFinished();PendingNoneStateChange=false;}
        services.CleanUp();
    }
    public void DoActionNow()
    {
        var area=services.DetermineArea();
        if(area!=null)services.SpawnArea(area);
        bool missile=services.HasMissile(),impact=services.HasImpact(),chain=services.HasChain();
        if(services.VisualTargetCount()>0&&(missile||impact||chain))
        {
            if(missile)
            {
                if(chain)services.SpawnChainMissile();
                else if(services.MissilesInSequence())services.SpawnMissileInSequence();
                else services.SpawnAllMissiles();
            }
            else
            {
                if(impact)
                {
                    if(chain)services.SpawnImpact(services.CurrentTargetIndex());
                    else services.SpawnAllImpacts();
                }
                if(chain)services.SpawnChain();
                services.DoStartSpellAction();
            }
        }
        else services.DoStartSpellAction();
        FinishIfPossible();
    }
}
