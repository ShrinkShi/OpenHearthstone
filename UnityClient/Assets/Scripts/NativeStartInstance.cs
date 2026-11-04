using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NativeStartInstance : IDisposable
{
    public readonly NativeAreaInstance Clone;
    public readonly NativeStartSpell Lifecycle;
    readonly List<Action<int>> stateFinished=new List<Action<int>>();
    float time;
    public NativeStartInstance(NativeProgram program,AssetRegistry assets,Func<int,bool> originalUsable,
        Func<int,GameObject> locations=null,Func<int,GameObject> facings=null)
    {
        if(originalUsable==null)throw new ArgumentNullException(nameof(originalUsable));
        Clone=new NativeAreaInstance(program,assets,locations:locations,facings:facings);
        Clone.Effect.SourceStateFinished+=(type,at)=>
        {
            time=at;
            for(int i=stateFinished.Count-1;i>=0;i--)if(i<stateFinished.Count)stateFinished[i](type);
            if(Clone.Effect.ActiveSourceSpellState==0)stateFinished.Clear();
        };
        Lifecycle=new NativeStartSpell{IsConcreteSpell=true,ActiveState=()=>Clone.Effect.ActiveSourceSpellState,
            HasUsableState=originalUsable,SafeActivateState=type=>Change(originalUsable(type)?type:0),ActivateState=Change,
            UpdateTransform=()=>{Clone.Effect.UpdateOriginalPosition();Clone.Effect.UpdateOriginalOrientation();},
            AddStateFinished=c=>stateFinished.Add(c),RemoveStateFinished=c=>stateFinished.Remove(c),AddFinished=Clone.Endpoint.AddFinished};
    }
    void Change(int type){Clone.Effect.ChangeInitializedSourceState(type,Mathf.Max(time,Clone.PlaybackTime));}
    public void Seek(float value){time=value;Clone.Seek(value);time=value;}
    public RenderTexture Render(float value){time=value;var texture=Clone.Render(value);time=value;return texture;}
    public void Dispose(){Clone.Dispose();}
}
