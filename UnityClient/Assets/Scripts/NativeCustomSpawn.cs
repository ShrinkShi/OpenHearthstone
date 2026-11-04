using System.Collections.Generic;
using UnityEngine;

// Source spawn actions move the Spell owner out of the Actor before parenting the Actor to its token.
public sealed class NativeCustomSpawn
{
    sealed class Pose{public Transform target,parent;public Vector3 position,scale;public Quaternion rotation;public int sibling;}
    static readonly Dictionary<GameObject,NativeCustomSpawn> owners=new Dictionary<GameObject,NativeCustomSpawn>();
    readonly List<Pose> poses=new List<Pose>();
    readonly HashSet<GameObject> actors=new HashSet<GameObject>();
    void Remember(Transform target)
    {
        if(poses.Exists(p=>p.target==target))return;
        poses.Add(new Pose{target=target,parent=target.parent,position=target.localPosition,rotation=target.localRotation,scale=target.localScale,sibling=target.GetSiblingIndex()});
    }
    static void ResetPose(Transform actor,bool position,bool rotation,bool scale)
    {if(position)actor.localPosition=Vector3.zero;if(rotation)actor.localEulerAngles=Vector3.zero;if(scale)actor.localScale=Vector3.one;}
    public bool Initialize(GameObject owner,GameObject actor,GameObject token,bool position,bool rotation,bool scale)
    {
        if(owner==null||actor==null||token==null||actor.transform.parent==null||owner==actor||token==actor||
           !owner.transform.IsChildOf(actor.transform)||!token.transform.IsChildOf(owner.transform))return false;
        NativeCustomSpawn previous;if(owners.TryGetValue(actor,out previous)&&previous!=this)return false;
        owners[actor]=this;actors.Add(actor);Remember(owner.transform);Remember(actor.transform);
        var parent=actor.transform.parent;
        owner.transform.SetParent(parent,true);actor.transform.SetParent(token.transform,true);
        ResetPose(actor.transform,position,rotation,scale);return true;
    }
    public bool Finalize(GameObject owner,GameObject actor,bool position,bool rotation,bool scale)
    {
        if(owner==null||actor==null||owner.transform.parent==null||owner==actor)return false;
        NativeCustomSpawn previous;if(owners.TryGetValue(actor,out previous)&&previous!=this)return false;
        if(owner.transform.parent.IsChildOf(actor.transform))return false;
        owners[actor]=this;actors.Add(actor);Remember(actor.transform);
        actor.transform.SetParent(owner.transform.parent,true);ResetPose(actor.transform,position,rotation,scale);return true;
    }
    public void Restore()
    {
        for(int i=poses.Count-1;i>=0;i--)
        {
            var pose=poses[i];if(pose.target==null)continue;
            pose.target.SetParent(pose.parent,false);pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;pose.target.localScale=pose.scale;pose.target.SetSiblingIndex(pose.sibling);
        }
        foreach(var actor in actors){NativeCustomSpawn current;if(owners.TryGetValue(actor,out current)&&current==this)owners.Remove(actor);}
        poses.Clear();actors.Clear();
    }
}
