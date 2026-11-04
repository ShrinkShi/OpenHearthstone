using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class NativePowerHistoryTask
{public int type,entity,tag,value;public bool tagChange;public string payloadType;public NativePowerTag[] tags;public int? metaType,metaData;}
[Serializable] public sealed class NativePowerTag{public int name,value;}

// Explicit original-history inputs, not inferred from SemanticEvent damage.
public sealed class NativePowerTaskList
{
    public int id;public int[] sourceEntities=new int[0];public NativePowerHistoryTask[] tasks=new NativePowerHistoryTask[0];
    public bool hasBlockStart,blockHasEffectTiming,hasEffectTiming,endOfBlock;
    public bool historyBlockEnd;
    public int? effectType;
    public NativePowerExecution CreateExecution(NativePowerServices services)
    {var execution=new NativePowerExecution(this,services);foreach(var task in tasks)execution.CreateTask(task);return execution;}
    readonly List<NativeSpellEntityObject> sourceCache=new List<NativeSpellEntityObject>();bool dirty=true;
    public void SetSourceEntitiesDirty(){dirty=true;}
    public List<NativeSpellEntityObject> GetSourceEntities(bool required,Func<int,NativeSpellEntityObject> resolve,Func<int,bool> removed,Action<string> warning)
    {
        if(!hasBlockStart)return null;
        if(!dirty)return sourceCache;
        sourceCache.Clear();sourceCache.Capacity=sourceEntities.Length;
        foreach(int id in sourceEntities)
        {
            var entity=resolve(id);
            if(entity==null&&required&&(removed==null||!removed(id)))
            {if(warning!=null)warning("Original power source entity is missing: "+id);return null;}
            sourceCache.Add(entity);
        }
        dirty=false;return sourceCache;
    }
    public bool CanAddPowerTargets()
    {return !(blockHasEffectTiming&&!hasEffectTiming)&&(tasks.Length>0||endOfBlock);}
}

// SpellController collection uses Card identity. Here the identity is its actual
// reconstructed scene GameObject; the original Card component is not executed.
public sealed class NativePowerTaskTargets
{
    readonly Func<int,NativeSpellEntityObject> entities;
    readonly Func<int,bool> removed;readonly Action<string> warning;
    readonly List<GameObject> sources=new List<GameObject>(),targets=new List<GameObject>();
    readonly Dictionary<GameObject,NativeSpellEntityObject> records=new Dictionary<GameObject,NativeSpellEntityObject>();
    public NativePowerTaskList TaskList{get;private set;}
    public int TaskListId{get;private set;}
    public NativePowerTaskTargets(Func<int,NativeSpellEntityObject> entities,Func<int,bool> removed=null,Action<string> warning=null)
    {this.entities=entities;this.removed=removed;this.warning=warning;}
    GameObject Card(NativeSpellEntityObject entity)
    {
        var obj=entity==null||!entity.hasCard?null:entity.Object;
        if(obj!=null)records[obj]=entity;return obj;
    }
    public List<GameObject> GetSources(){return sources;}
    public IEnumerable<NativeSpellEntityObject> GetSourceRecords()
    {foreach(var obj in sources){NativeSpellEntityObject record;yield return obj!=null&&records.TryGetValue(obj,out record)?record:null;}}
    public GameObject GetSource(){return sources.Count==0?null:sources[0];}
    public List<GameObject> GetTargets(){return targets;}
    public GameObject GetTarget(){return targets.Count==0?null:targets[0];}
    public void SetSource(GameObject value){sources.Clear();sources.Add(value);}
    public void SetSource(IEnumerable<GameObject> values){sources.Clear();sources.AddRange(values);}
    public void RemoveSource(){sources.Clear();}
    public bool IsSource(GameObject value){return sources.Contains(value);}
    public void AddTarget(GameObject value){targets.Add(value);}
    public void RemoveTarget(GameObject value){targets.Remove(value);}
    public void RemoveAllTargets(){targets.Clear();}
    public bool IsTarget(GameObject value){return targets.Contains(value);}
    public bool HasSourceCard(NativePowerTaskList input)
    {
        var values=input.GetSourceEntities(true,entities,removed,warning);
        if(values==null||values.Count==0)return false;
        var cards=values.Where(v=>v!=null).Select(Card).ToList();
        return cards.Count>0&&!cards.Any(c=>c==null);
    }
    public GameObject GetTargetCardFromPowerTask(NativePowerHistoryTask task)
    {
        if(task.type!=4)return null;
        if(!task.tagChange)throw new NullReferenceException("Original type-4 history is not HistTagChange");
        var entity=entities(task.entity);
        if(entity==null){if(warning!=null)warning("Original power target entity is missing: "+task.entity);return null;}
        return Card(entity);
    }
    public bool AddPowerSourceAndTargets(NativePowerTaskList input)
    {
        if(!HasSourceCard(input)||!input.CanAddPowerTargets())return false;
        var cards=input.GetSourceEntities(true,entities,removed,warning).Where(v=>v!=null).Select(Card).ToList();
        SetSource(cards);
        // Original method reads targets from the attached m_taskList, not input.
        foreach(var task in TaskList.tasks)
        {
            var card=GetTargetCardFromPowerTask(task);
            if(card!=null&&!cards.Contains(card)&&!IsTarget(card))AddTarget(card);
        }
        return cards.Count==0||cards.Any(c=>c==null)?targets.Count>0:true;
    }
    public bool AttachPowerTaskList(NativePowerTaskList value)
    {
        if(!ReferenceEquals(TaskList,value)){DetachPowerTaskList();TaskList=value;}
        TaskListId=TaskList.id;return AddPowerSourceAndTargets(value);
    }
    public void SetPowerTaskList(NativePowerTaskList value)
    {if(ReferenceEquals(TaskList,value))return;DetachPowerTaskList();TaskList=value;}
    public NativePowerTaskList DetachPowerTaskList()
    {var previous=TaskList;RemoveSource();RemoveAllTargets();records.Clear();TaskList=null;return previous;}
    public void ApplyTo(NativeSpellObjectSelection selection)
    {
        NativeSpellEntityObject record;var first=GetSource();
        selection.source=first!=null&&records.TryGetValue(first,out record)?record:null;
        selection.SetTargets(targets.Select(t=>t!=null&&records.TryGetValue(t,out record)?record:null));
        selection.effectType=TaskList==null?null:TaskList.effectType;
    }
}
