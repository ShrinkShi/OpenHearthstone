using System;
using UnityEngine;

public static class NativeRGBAAction
{
    public static Color? Apply(Color? current,NativeAction action,Func<NativeParam,float> value)
    {
        // Original action checks only the FsmColor object for null, not IsNone.
        if(!current.HasValue)return null;
        var color=current.Value;string[] channels={"red","green","blue","alpha"};
        for(int i=0;i<channels.Length;i++)
        {
            var parameter=action.Param(channels[i]);
            if(parameter.unresolved||!parameter.none_known)throw new ArgumentException("Original RGBA channel None flag is unrestored");
            if(!parameter.is_none)color[i]=value(parameter);
        }
        return color;
    }
}
