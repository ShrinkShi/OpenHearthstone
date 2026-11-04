using System;
using UnityEngine;

// Original ColorInterpolate: equal intervals, live endpoint reads, strict > finish.
public sealed class NativeColorInterpolation
{
    readonly NativeParam[] colors;readonly NativeParam time;
    readonly Func<NativeParam,float> number;readonly Func<NativeParam,Color> color;
    float current;
    public bool Finished{get;private set;}
    public bool InitialWrite{get;private set;}
    public Color Value{get;private set;}
    public NativeColorInterpolation(NativeAction action,Func<NativeParam,float> number,Func<NativeParam,Color> color)
    {
        this.number=number;this.color=color;time=action.Param("time");
        var header=action.Param("colors");int index=Array.IndexOf(action.@params,header);
        if(index<0||!header.array_known||header.unresolved||header.array_type!="HutongGames.PlayMaker.FsmColor"||header.array_count<0||index+header.array_count>=action.@params.Length||time.name!="time"||time.unresolved||action.Param("realTime").name!="realTime"||action.Param("realTime").unresolved||action.Param("storeColor").name!="storeColor"||action.Param("storeColor").unresolved||action.Param("storeColor").object_null)
            throw new ArgumentException("Original ColorInterpolate array/control is unrestored");
        if(action.Param("realTime").number!=0)throw new ArgumentException("Original realtime ColorInterpolate clock is not bound");
        colors=new NativeParam[header.array_count];Array.Copy(action.@params,index+1,colors,0,colors.Length);
        foreach(var p in colors)if(p.kind!=25||p.unresolved)throw new ArgumentException("Original FsmColor array element is unrestored");
        InitialWrite=colors.Length>0;if(InitialWrite)Value=color(colors[0]);Finished=colors.Length<2;
    }
    public bool Step(float delta)
    {
        if(Finished)return false;
        current+=delta;float duration=number(time);
        if(current>duration){Finished=true;Value=color(colors[colors.Length-1]);return true;}
        float position=(colors.Length-1)*current/duration;
        if(position==0)Value=color(colors[0]);
        else if(position==colors.Length-1)Value=color(colors[colors.Length-1]);
        else Value=Color.Lerp(color(colors[Mathf.FloorToInt(position)]),color(colors[Mathf.CeilToInt(position)]),position-Mathf.Floor(position));
        return true;
    }
}
