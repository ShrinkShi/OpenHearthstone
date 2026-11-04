using System;
using System.Collections;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class NativeSuperActionInfo
{public int m_ShowSpellVisuals;public float m_StartDelayMin,m_StartDelayMax;}
[Serializable] public sealed class NativeSuperRawConfig
{public NativeSuperActionInfo m_ActionInfo;public NativeSuperEnabledInfo m_StartInfo,m_MissileInfo,m_ImpactInfo,m_ChainInfo;}
[Serializable] public sealed class NativeSuperEnabledInfo{public int m_Enabled;}

public sealed class NativeStartSpell
{
    public bool IsConcreteSpell;
    public Func<int> ActiveState;
    public Func<int,bool> HasUsableState;
    public Action<int> SafeActivateState,ActivateState;
    public Action UpdateTransform;
    public Action<Action<int>> AddStateFinished,RemoveStateFinished;
    public Action<Action> AddFinished;
}
public sealed class NativeSuperStartServices
{
    public Func<bool> CheckGameEventWait,HasStart;
    public Action SpawnStart,DoActionNow;
    public Func<float> DelayMin,DelayMax;
    public Action<int> AddPending;
    public Action<IEnumerator> StartCoroutine;
    public Func<float,float,float> RandomRange=UnityEngine.Random.Range;
    public void BindSourceDelays(NativeProgram program)
    {
        var source=(program.spell_transforms??new NativeSourceSpellTransform[0]).SingleOrDefault(t=>t.node==0&&t.enabled);
        if(source==null||source.raw==null||source.raw.m_ActionInfo==null)
            throw new NotSupportedException("Original SuperSpell action delay source is unrestored");
        // Keep each controller's settings independent of its shared program.
        float min=source.raw.m_ActionInfo.m_StartDelayMin,max=source.raw.m_ActionInfo.m_StartDelayMax;
        DelayMin=()=>min;DelayMax=()=>max;
    }
}

// Original DoAction gate order, d__65 scaled wait and start lifecycle handling.
// Source spawning and game-event services must be provided by the controller.
public sealed class NativeSuperStart
{
    readonly NativeSuperStartServices services;
    public NativeStartSpell Start;
    public NativeSuperStart(NativeSuperStartServices services){this.services=services;}
    public void DoAction()
    {
        if(services.CheckGameEventWait())return;
        if(CheckAndWaitForStartDelay())return;
        if(CheckAndWaitForStartPrefab())return;
        services.DoActionNow();
    }
    public bool CheckAndWaitForStartDelay()
    {
        // IL bgt.un also enters the coroutine for an unordered comparison.
        if(Mathf.Min(services.DelayMax(),services.DelayMin())<=Mathf.Epsilon)return false;
        services.AddPending(1);
        services.StartCoroutine(WaitForStartDelay());
        return true;
    }
    IEnumerator WaitForStartDelay()
    {
        float delay=services.RandomRange(services.DelayMin(),services.DelayMax());
        yield return new WaitForSeconds(delay);
        services.AddPending(-1);
        if(CheckAndWaitForStartPrefab())yield break;
        services.DoActionNow();
    }
    public bool CheckAndWaitForStartPrefab()
    {
        if(!services.HasStart())return false;
        if(Start!=null&&Start.ActiveState()==2)return false;
        if(Start==null)services.SpawnStart();
        if(Start.IsConcreteSpell)
        {
            var spell=Start;Action<int> callback=null;
            callback=type=>
            {
                if(type!=1)return;
                spell.RemoveStateFinished(callback);
                services.DoActionNow();
            };
            spell.AddStateFinished(callback);
        }
        if(Start.ActiveState()!=1)
        {
            Start.SafeActivateState(1);
            if(Start.ActiveState()==0){Start=null;return false;}
        }
        return true;
    }
    public void DoStartSpellAction()
    {
        if(Start==null)return;
        if(!Start.HasUsableState(3))
        {Start.UpdateTransform();Start.SafeActivateState(5);}
        else
        {
            if(Start.IsConcreteSpell)
            {
                var spell=Start;
                spell.AddFinished(()=>{if(spell.ActiveState()==3)spell.SafeActivateState(5);});
            }
            Start.ActivateState(3);
        }
        Start=null;
    }
}
