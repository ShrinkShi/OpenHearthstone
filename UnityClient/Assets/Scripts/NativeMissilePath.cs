using System;
using UnityEngine;

[Serializable] public sealed class NativeMissilePrefabReference
{
    public int m_FileID;
    public long m_PathID;
}

[Serializable] public sealed class NativeMissilePathInfo
{
    public int m_Enabled,m_SpawnInSequence,m_OrientToPath,m_DebugForceMax,m_UseTargetCardPositionInsteadOfHandSlot,m_PathEaseType;
    public int m_UseSuperSpellLocation,m_TimesToHitSameTarget;
    public float m_SpawnDelaySecMin,m_SpawnDelaySecMax,m_SpawnOffset;
    public float m_PathDurationMin,m_PathDurationMax,m_CenterOffsetPercent,m_CenterPointHeightMin,m_CenterPointHeightMax;
    public float m_RightMin,m_RightMax,m_LeftMin,m_LeftMax,m_DistanceScaleFactor,m_TargetHeightOffset;
    public NativeMissilePrefabReference m_Prefab,m_ReversePrefab;
    public string m_TargetJoint;
    public Vector3 m_JointUpVector;
}

// SuperSpell.GenerateMissilePath/CenterPoint and original iTween CR spline.
// Joint selection, LookUpdate rotation, cloning, task gates and impact spawning
// belong to the controller, not this position sampler.
public sealed class NativeMissilePath
{
    public readonly Vector3[] Points;
    readonly Vector3[] controls;
    readonly int ease;
    public readonly float Duration;
    public NativeMissilePath(Vector3[] points,float duration,int ease)
    {
        if(points==null||points.Length<2)throw new ArgumentException("Original missile path needs at least two points");
        if(!NativeITween.SupportedEase(ease))throw new NotSupportedException("Original missile ease is unrestored");
        Points=(Vector3[])points.Clone();controls=ControlPoints(Points);Duration=duration;this.ease=ease;
    }
    public static Vector3[] Generate(NativeMissilePathInfo info,Vector3 start,Vector3 target,
        Func<float,float,float> range,Func<bool> randomBool,bool targetInHand=false,Func<Vector3> handSlot=null)
    {
        if(targetInHand&&info.m_UseTargetCardPositionInsteadOfHandSlot==0)
        {if(handSlot==null)throw new NotSupportedException("Original hand slot position is unbound");target=handSlot();}
        var difference=target-start;float distance=difference.magnitude;bool same=distance<=Mathf.Epsilon;
        var center=same?start:start+difference*(info.m_CenterOffsetPercent*.01f);
        float factor=distance/info.m_DistanceScaleFactor;
        if(same)
            center.y+=info.m_CenterPointHeightMin<=Mathf.Epsilon&&info.m_CenterPointHeightMax<=Mathf.Epsilon?2:range(info.m_CenterPointHeightMin,info.m_CenterPointHeightMax);
        else center.y+=factor*range(info.m_CenterPointHeightMin,info.m_CenterPointHeightMax);
        float direction=start.z>target.z?-1:1;
        bool right=randomBool();
        if(info.m_RightMin==0&&info.m_RightMax==0)right=false;
        if(info.m_LeftMin==0&&info.m_LeftMax==0)right=true;
        float min=right?info.m_RightMin:info.m_LeftMin,max=right?info.m_RightMax:info.m_LeftMax;
        float horizontal=min==max||info.m_DebugForceMax!=0?max*factor:range(min*factor,max*factor);
        center.x+=(right?1:-1)*horizontal*direction;
        return new[]{start,center,target};
    }
    public static Vector3[] ControlPoints(Vector3[] path)
    {
        var result=new Vector3[path.Length+2];Array.Copy(path,0,result,1,path.Length);
        result[0]=result[1]+result[1]-result[2];int last=result.Length-1;
        result[last]=result[last-1]+result[last-1]-result[last-2];
        if(result[1]==result[last-1]){result[0]=result[last-2];result[last]=result[2];}
        return result;
    }
    public static Vector3 Interpolate(Vector3[] controls,float t)
    {
        int sections=controls.Length-3,index=Mathf.Min(Mathf.FloorToInt(t*sections),sections-1);
        float u=t*sections-index;var a=controls[index];var b=controls[index+1];var c=controls[index+2];var d=controls[index+3];
        return .5f*((-a+3*b-3*c+d)*u*u*u+(2*a-5*b+4*c-d)*u*u+(-a+c)*u+2*b);
    }
    public Vector3 SamplePercentage(float percent)
    {return Interpolate(controls,Mathf.Clamp01(NativeITween.Sample(0,1,percent,ease)));}
    public Vector3 Sample(float elapsed){return SamplePercentage(Duration<=0?1:Mathf.Clamp01(elapsed/Duration));}
    public Vector3 LookPoint(float elapsed,float lookAhead)
    {return SamplePercentage(Mathf.Min(1,(Duration<=0?1:Mathf.Clamp01(elapsed/Duration))+lookAhead));}
}
