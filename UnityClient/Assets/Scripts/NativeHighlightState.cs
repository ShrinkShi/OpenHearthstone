using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// HighlightState.ChangeState's no-FSM branch, used by the source ordinary minion.
// This does not implement premium/static silhouettes or the original clock service.
public sealed class NativeHighlightState : IDisposable
{
    [Serializable] sealed class State{public int state;public Vector3 offset;public NativeMaterial material;}
    [Serializable] sealed class Driver{public bool has_fsm;public int resolved_type;public Vector3 history_translation;}
    [Serializable] sealed class Definition{public State[] states;public int render_queue,queue_offset;public Driver state_driver;}
    readonly Definition definition;
    readonly MeshRenderer renderer;
    readonly Transform owner;
    readonly NativeHighlightSilhouette silhouette;
    readonly Material material,originalMaterial;
    readonly Dictionary<int,Material> states=new Dictionary<int,Material>();
    readonly Vector3 originalPosition;
    readonly bool originalEnabled,originalActive;
    readonly float seed;
    bool hidden,visible,disposed;
    public int CurrentState{get;private set;}=1;
    public int PreviousState{get;private set;}
    public bool VisibilityState{get{return visible;}}
    public Material Material{get{return material;}}
    public NativeHighlightState(NativeActorIdentity actor,AssetRegistry assets,float? seed=null)
    {
        definition=JsonUtility.FromJson<Definition>(File.ReadAllText(Path.Combine(assets.Root,"original-minion-highlight.json")));
        if(definition.state_driver==null||definition.state_driver.has_fsm||definition.state_driver.resolved_type!=1)
            throw new InvalidDataException("Only the proven source ordinary minion no-FSM branch is restored");
        renderer=NativeActorIdentity.FindChildBySubstring(actor.gameObject,"Highlight_Card_Plane").GetComponent<MeshRenderer>();
        owner=renderer.transform.parent;originalPosition=owner.localPosition;
        originalMaterial=renderer.sharedMaterial;originalEnabled=renderer.enabled;originalActive=renderer.gameObject.activeSelf;
        this.seed=seed??UnityEngine.Random.value;
        try
        {
            material=new Material(Resources.Load<Shader>("OriginalActorHighlight"));
            foreach(var state in definition.states)if(state.material!=null&&!String.IsNullOrEmpty(state.material.shader))states.Add(state.state,NativeActorMaterial.Create(state.material,assets));
            silhouette=new NativeHighlightSilhouette(actor);
            renderer.sharedMaterial=material;renderer.enabled=false;renderer.gameObject.SetActive(true);
        }
        catch
        {
            foreach(var state in states.Values)AssetRegistry.ReleaseTransient(state);
            AssetRegistry.ReleaseTransient(material);throw;
        }
    }
    public bool ChangeState(int state)
    {
        if(disposed)throw new ObjectDisposedException("NativeHighlightState");
        if(CurrentState==state)return true;
        PreviousState=CurrentState;CurrentState=state;
        if(state==0||state==1||state==29){renderer.enabled=false;visible=false;return true;}
        foreach(var source in definition.states)
        {
            if(source.state!=state)continue;
            if(!states.ContainsKey(state)){renderer.enabled=false;visible=false;return true;}
            material.CopyPropertiesFromMaterial(states[state]);renderer.sharedMaterial=material;material.SetFloat("_Seed",seed);
            UpdateSilhouette();
            owner.localPosition=state==28?definition.state_driver.history_translation:source.offset;
            if(!hidden)renderer.enabled=true;
            visible=true;return true;
        }
        CurrentState=1;renderer.enabled=false;visible=false;return false;
    }
    public void UpdateSilhouette()
    {
        if(disposed)throw new ObjectDisposedException("NativeHighlightState");
        silhouette.Generate();material.mainTexture=silhouette.Texture;
        material.renderQueue=definition.render_queue+definition.queue_offset;
    }
    public void Hide(){hidden=true;renderer.enabled=false;}
    public void Show(){hidden=false;if(visible&&!renderer.enabled)renderer.enabled=true;}
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        if(renderer!=null){renderer.sharedMaterial=originalMaterial;renderer.enabled=originalEnabled;renderer.gameObject.SetActive(originalActive);owner.localPosition=originalPosition;}
        silhouette.Dispose();foreach(var state in states.Values)AssetRegistry.ReleaseTransient(state);AssetRegistry.ReleaseTransient(material);
    }
}
