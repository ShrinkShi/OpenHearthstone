using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scene object access stays lazy so the native writer registry owns every borrow.
public sealed class NativeSpellEntityObject
{
    public Func<GameObject> resolve,hero,abilityOwner,power,attached;
    public Func<int,GameObject> indexedPower;
    public int type,powerIndex,entity;public string card_id;public bool hasCard=true,hasPowerIndex;
    public int? controllerSide;
    public GameObject Object{get{return resolve==null?null:resolve();}}
}

// Original SpellUtils.FindSource/TargetAutoObjectForSpell and FindAutoObjectForSpell.
// PowerTaskList can override the source type; the target never uses that override.
public sealed class NativeSpellObjectSelection
{
    public NativeSpellEntityObject source;
    readonly Dictionary<GameObject,NativeSpellEntityObject> targetRecords=new Dictionary<GameObject,NativeSpellEntityObject>();
    NativeSpellTargets targets;
    NativeSpellEntityObject[] pendingTargets=new NativeSpellEntityObject[0];
    public NativeSpellEntityObject visualTarget
    {
        get{return Record(Targets.GetVisualTarget());}
        set{SetTargets(value==null?new NativeSpellEntityObject[0]:new[]{value});}
    }
    public void SetTargets(IEnumerable<NativeSpellEntityObject> values)
    {pendingTargets=values.ToArray();targets=null;targetRecords.Clear();}
    public NativeSpellTargets Targets
    {
        get
        {
            if(targets!=null)return targets;
            targets=new NativeSpellTargets();
            foreach(var record in pendingTargets)
            {
                var obj=record==null?null:record.Object;targets.AddTarget(obj);
                if(obj!=null)targetRecords[obj]=record;
            }
            pendingTargets=new NativeSpellEntityObject[0];return targets;
        }
    }
    NativeSpellEntityObject Record(GameObject obj)
    {
        if(obj==null)return null;
        NativeSpellEntityObject record;
        return targetRecords.TryGetValue(obj,out record)?record:new NativeSpellEntityObject{resolve=()=>obj,hasCard=false};
    }
    public NativeSpellEntityObject TargetRecord(GameObject obj){return Record(obj);}
    public void AddTarget(NativeSpellEntityObject record)
    {var set=Targets;var obj=record==null?null:record.Object;set.AddTarget(obj);if(obj!=null)targetRecords[obj]=record;}
    public NativeSpellEntityObject BestTarget()
    {
        var first=visualTarget;
        if(source==null||source.Object==null||!source.hasCard||!source.controllerSide.HasValue)return first!=null&&first.hasCard?first:null;
        foreach(var obj in Targets.GetVisualTargets())
        {
            // The original calls GetComponent directly, rather than skipping nulls.
            if(obj==null)obj.GetComponent<Transform>();
            var card=Record(obj);if(!card.hasCard)continue;
            if(!card.controllerSide.HasValue)throw new InvalidOperationException("Original target controller is unresolved");
            if(card.controllerSide.Value!=source.controllerSide.Value)return card;
        }
        return first!=null&&first.hasCard?first:null;
    }
    public int? effectType;
    public GameObject SourceAuto(){return Auto(source,effectType);}
    public GameObject TargetAuto(){return Auto(visualTarget,null);}
    static GameObject Related(Func<GameObject> value){return value==null?null:value();}
    static GameObject Auto(NativeSpellEntityObject entity,int? overrideType)
    {
        if(entity==null)return null;
        var original=entity.Object;if(original==null||!entity.hasCard)return original;
        int type=overrideType??entity.type;
        if(type==5)
        {
            var hero=Related(entity.hero);if(hero!=null)return hero;
            var ability=Related(entity.abilityOwner);return ability!=null?ability:original;
        }
        if(type==10)
        {
            var indexed=entity.hasPowerIndex&&entity.indexedPower!=null?entity.indexedPower(entity.powerIndex):null;
            if(indexed!=null)return indexed;
            var power=Related(entity.power);return power!=null?power:original;
        }
        if(type==6){var attached=Related(entity.attached);return attached!=null?attached:original;}
        return original;
    }
    public GameObject Location(int value)
    {
        switch(value)
        {
            case 0:return null;
            case 1:return source==null?null:source.Object;
            case 2:return SourceAuto();
            case 3:return source==null||!source.hasCard?null:Related(source.hero);
            case 4:return source==null||!source.hasCard?null:Related(source.power);
            case 6:return visualTarget==null?null:visualTarget.Object;
            case 7:return TargetAuto();
            case 8:return visualTarget==null||!visualTarget.hasCard?null:Related(visualTarget.hero);
            case 9:return visualTarget==null||!visualTarget.hasCard?null:Related(visualTarget.power);
            default:return null; // Board/zones still need their original scene objects.
        }
    }
    public GameObject Facing(int value)
    {
        if(value==5||value==7||value==10)return Location(1);
        if(value==6||value==8||value==11)return SourceAuto();
        if(value==1||value==4||value==12)return Location(3);
        if(value==2)return Location(6);
        if(value==3){var best=BestTarget();return best==null?null:Related(best.hero);}
        return null; // BestTarget, PowerTarget and opposing hero require more context.
    }
    public static NativeSpellObjectSelection ForActor(GameObject actor,int type)
    {return new NativeSpellObjectSelection{source=new NativeSpellEntityObject{resolve=()=>actor,type=type}};}
}
