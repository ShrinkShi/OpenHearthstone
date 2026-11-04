using System;
using UnityEngine;

[Serializable] public sealed class OriginalCurveKey
{public float time,value,inSlope,outSlope,inWeight=1f/3,outWeight=1f/3;public int weightedMode;}
[Serializable] public sealed class OriginalScalarTrack
{
    public bool enabled;public float scalar=1;public int mode;public OriginalCurveKey[] keys;
    AnimationCurve curve;
    public float Sample(float t)
    {
        if((mode!=1&&mode!=2)||keys==null||keys.Length==0)return scalar;
        if(curve==null)
        {
            var values=new Keyframe[keys.Length];for(int i=0;i<keys.Length;i++)
            {var k=keys[i];values[i]=new Keyframe(k.time,k.value,k.inSlope,k.outSlope,k.inWeight,k.outWeight){weightedMode=(WeightedMode)k.weightedMode};}
            curve=new AnimationCurve(values);
        }
        return curve.Evaluate(Mathf.Clamp01(t))*scalar;
    }
}
[Serializable] public sealed class OriginalColorKey {public float time;public Color value;}
[Serializable] public sealed class OriginalAlphaKey {public float time,value;}
[Serializable] public sealed class OriginalColorTrack
{
    public bool enabled;public int mode;public Color constant=Color.white;public OriginalColorKey[] colors;public OriginalAlphaKey[] alpha;
    public Color Sample(float t)
    {
        if(mode!=1&&mode!=3&&mode!=4)return constant;
        Color color=Color.white;
        if(colors!=null&&colors.Length>0)
        {
            color=colors[colors.Length-1].value;
            if(t<=colors[0].time)color=colors[0].value;
            else for(int i=1;i<colors.Length;i++)if(t<=colors[i].time)
            {color=Color.Lerp(colors[i-1].value,colors[i].value,Mathf.InverseLerp(colors[i-1].time,colors[i].time,t));break;}
        }
        color.a=1;
        if(alpha!=null&&alpha.Length>0)
        {
            color.a=alpha[alpha.Length-1].value;
            if(t<=alpha[0].time)color.a=alpha[0].value;
            else for(int i=1;i<alpha.Length;i++)if(t<=alpha[i].time)
            {color.a=Mathf.Lerp(alpha[i-1].value,alpha[i].value,Mathf.InverseLerp(alpha[i-1].time,alpha[i].time,t));break;}
        }
        return color;
    }
}
[Serializable] public sealed class OriginalParticleTracks
{
    public bool restored;public float original_duration;public OriginalColorTrack color,start_color;
    public OriginalScalarTrack size,rotation,initial_rotation;
    public Color Color(float t){return (start_color==null||!start_color.enabled?UnityEngine.Color.white:start_color.Sample(t))*(color==null||!color.enabled?UnityEngine.Color.white:color.Sample(t));}
    public float Size(float t){return size==null||!size.enabled?1:Mathf.Clamp(size.Sample(t),0,4);}
    public float Angle(float t)
    {return ((initial_rotation==null||!initial_rotation.enabled?0:initial_rotation.Sample(t))+(rotation==null||!rotation.enabled?0:rotation.Sample(t)*t))*Mathf.Rad2Deg;}
}
