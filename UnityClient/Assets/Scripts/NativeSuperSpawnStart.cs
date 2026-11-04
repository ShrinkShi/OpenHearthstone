using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class NativeStartInfo
{
    public bool enabled,prefab_null,use_super_location,adjust_rotation,death_after_missiles;
    public string prefab_reference;public Vector3 rotation;
}
public sealed class NativeSuperSpawnStart
{
    public GameObject Root;
    public Func<GameObject> Source;
    public Func<IEnumerable<GameObject>> Targets;
    public Func<NativeStartInfo,NativeStartInstance> GetSpell;
    public Func<NativeAreaClone,bool,bool> Release;
    public Action FinishIfPossible;
    readonly List<NativeAreaClone> clones=new List<NativeAreaClone>();
    public int Pending{get;private set;}
    public NativeStartInstance Start{get;private set;}
    public IReadOnlyList<NativeAreaClone> ActiveClones{get{return clones;}}
    public static bool HasStart(NativeStartInfo info){return info!=null&&info.enabled&&!info.prefab_null;}
    public void Spawn(NativeStartInfo info)
    {
        Pending++;
        Start=GetSpell(info);
        NativeSuperArea.RegisterClone(Start.Clone.Endpoint,Root,clones,Release,()=>{Pending--;FinishIfPossible();});
        Start.Clone.Endpoint.SetSource(Source());Start.Clone.Targets.AddTargets(Targets());
        if(info.use_super_location)Start.Clone.Endpoint.SetPosition(Root.transform.position);
        if(info.adjust_rotation&&info.rotation!=Vector3.zero)
        {Start.Clone.Endpoint.Object.transform.Rotate(info.rotation);Start.Clone.Endpoint.UpdateOrientation();}
    }
}
