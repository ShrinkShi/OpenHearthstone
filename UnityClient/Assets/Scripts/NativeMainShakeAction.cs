using System;
using UnityEngine;

public sealed class NativeMainShakeAction
{
    readonly NativeAction action;
    readonly Func<NativeParam,float> number;
    readonly Func<NativeParam,Vector3> vector;
    readonly Action<Vector3,float> shake;
    float timer;
    bool fired;
    public bool Finished{get;private set;}
    public NativeMainShakeAction(NativeAction action,Func<NativeParam,float> number,Func<NativeParam,Vector3> vector,Action<Vector3,float> shake)
    {
        this.action=action;this.number=number;this.vector=vector;this.shake=shake;
        foreach(var name in new[]{"m_Amount","m_Time","m_Delay","m_FinishedEvent"})
        {var p=action.Param(name);if(p.name==null||p.unresolved)throw new ArgumentException("Original main camera shake controls are unrestored");}
    }
    public bool Step(float delta)
    {
        if(Finished)return false;
        timer+=delta;
        if(timer>=number(action.Param("m_Delay"))&&!fired){shake(vector(action.Param("m_Amount")),number(action.Param("m_Time")));fired=true;}
        if(timer>=number(action.Param("m_Delay"))+number(action.Param("m_Time"))){Finished=true;return true;}
        return false;
    }
}
