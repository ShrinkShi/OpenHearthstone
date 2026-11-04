using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class NativeAreaInfo
{
    public bool enabled,prefab_null,use_super_location,parent_to_location,rotate_x,rotate_y;
    public string prefab_reference;public NativeAssetSource prefab_source;public int prefab_node,location,facing;public float delay_min,delay_max;
}
[Serializable] public sealed class NativeSuperAreaConfig
{public string kind;public int node;public NativeAreaInfo friendly,opponent;}
[Serializable] public sealed class NativeSuperAreaRecord{public string file,reference;public NativeSuperAreaConfig config;}
[Serializable] public sealed class NativeSuperAreaCatalog{public NativeSuperAreaRecord[] programs;}

public sealed class NativeAreaClone
{
    public GameObject Object;
    public Action<Action> AddFinished,AddStateStarted;
    public Func<int> ActiveState;
    public Action<GameObject> SetSource;
    public Func<NativePowerTaskList,bool> AttachTasks;
    public Action<Vector3> SetPosition;
    public Action<int,bool> SetLocation;
    public Action<int,bool,bool> SetFacing;
    public Action UpdatePosition,UpdateOrientation,Activate;
}
public sealed class NativeSuperAreaServices
{
    public GameObject Root;
    public Func<GameObject> Source;
    public NativePowerTaskList Tasks;
    public Func<NativeAreaInfo,NativeAreaClone> GetSpell;
    public Func<NativeAreaClone,bool,bool> ReleaseSpell;
    public Action<NativeAreaClone> BeforeActivate;
    public Action FinishIfPossible;
    public Func<float,float,float> RandomRange=UnityEngine.Random.Range;
    // Original WaitForSeconds is scaled and frame scheduled. Host must supply it.
    public Action<float,Action> WaitScaled;
}

// Original DetermineAreaEffectInfo / SpawnAreaEffect / CloneSpell / d__100 order.
// Source IsMakingClones returns true, regardless of the serialized m_MakeClones.
public sealed class NativeSuperArea
{
    public static void RegisterClone(NativeAreaClone clone,GameObject root,List<NativeAreaClone> clones,
        Func<NativeAreaClone,bool,bool> release,Action finished)
    {
        clone.AddStateStarted(()=>{if(clone.ActiveState()!=0)return;clones.Remove(clone);release(clone,false);});
        clone.Object.transform.SetParent(root.transform,true);clones.Add(clone);
        clone.AddFinished(finished);
    }
    readonly NativeSuperAreaServices services;
    readonly List<NativeAreaClone> clones=new List<NativeAreaClone>();
    public int Pending{get;private set;}
    public NativeAreaClone ActiveArea{get;private set;}
    public IReadOnlyList<NativeAreaClone> ActiveClones{get{return clones;}}
    public NativeSuperArea(NativeSuperAreaServices services){this.services=services;}
    static bool Available(NativeAreaInfo info){return info!=null&&info.enabled&&!info.prefab_null;}
    public static NativeAreaInfo Determine(NativeSuperAreaConfig config,bool? sourceFriendly)
    {
        if(sourceFriendly==true&&Available(config.friendly))return config.friendly;
        if(sourceFriendly==false&&Available(config.opponent))return config.opponent;
        return Available(config.friendly)?config.friendly:Available(config.opponent)?config.opponent:null;
    }
    public void Spawn(NativeAreaInfo info)
    {
        Pending++;
        float delay=services.RandomRange(info.delay_min,info.delay_max);
        if(delay>0)
        {
            if(services.WaitScaled==null)throw new NotSupportedException("Original scaled area coroutine wait is unrestored");
            services.WaitScaled(delay,()=>SpawnNow(info));
        }
        else SpawnNow(info);
    }
    void SpawnNow(NativeAreaInfo info)
    {
        var clone=services.GetSpell(info);
        RegisterClone(clone,services.Root,clones,services.ReleaseSpell,()=>{Pending--;services.FinishIfPossible();});
        clone.SetSource(services.Source());
        if(services.Tasks!=null&&clone.Object!=null)clone.AttachTasks(services.Tasks);
        if(info.use_super_location)clone.SetPosition(services.Root.transform.position);
        else if(info.location!=0){clone.SetLocation(info.location,info.parent_to_location);clone.UpdatePosition();}
        if(info.facing!=0){clone.SetFacing(info.facing,info.rotate_x,info.rotate_y);clone.UpdateOrientation();}
        if(services.BeforeActivate!=null)services.BeforeActivate(clone);
        clone.Activate();ActiveArea=clone;
    }
}
