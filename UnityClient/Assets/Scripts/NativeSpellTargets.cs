using System.Collections.Generic;
using UnityEngine;

// Spell's visual accessors alias the same mutable List<GameObject>. It retains
// duplicates and null entries; controller metadata collection deduplicates later.
public sealed class NativeSpellTargets
{
    readonly List<GameObject> targets=new List<GameObject>();
    public List<GameObject> GetTargets(){return targets;}
    public GameObject GetTarget(){return targets.Count==0?null:targets[0];}
    public void AddTarget(GameObject value){targets.Add(value);}
    public void AddTargets(IEnumerable<GameObject> values){targets.AddRange(values);}
    public bool RemoveTarget(GameObject value){return targets.Remove(value);}
    public void RemoveAllTargets(){targets.Clear();}
    public bool IsTarget(GameObject value){return targets.Contains(value);}
    public List<GameObject> GetVisualTargets(){return GetTargets();}
    public GameObject GetVisualTarget(){return GetTarget();}
    public void AddVisualTarget(GameObject value){AddTarget(value);}
    public void AddVisualTargets(IEnumerable<GameObject> values){AddTargets(values);}
    public bool RemoveVisualTarget(GameObject value){return RemoveTarget(value);}
    public void RemoveAllVisualTargets(){RemoveAllTargets();}
    public bool IsVisualTarget(GameObject value){return IsTarget(value);}
}
