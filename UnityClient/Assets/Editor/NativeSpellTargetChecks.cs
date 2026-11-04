using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSpellTargetChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original Spell targets: "+message);passed++;}
    public static void Run()
    {
        passed=0;var sourceObject=new GameObject("Source Card");var friendly=new GameObject("Friendly target");
        var enemy=new GameObject("Enemy target");var decoration=new GameObject("Non-Card target");var enemyHero=new GameObject("Enemy hero");
        try
        {
            var list=new NativeSpellTargets();Check(list.GetTarget()==null,"empty target is null");
            Check(ReferenceEquals(list.GetTargets(),list.GetVisualTargets()),"visual and logical accessors share mutable list");
            list.AddTarget(friendly);list.AddVisualTargets(new[]{enemy,friendly,null});
            Check(list.GetTargets().Count==4&&list.GetVisualTarget()==friendly,"range retains order, duplicate and null");
            Check(list.IsVisualTarget(null)&&list.IsTarget(enemy),"contains uses actual list semantics");
            Check(list.RemoveVisualTarget(friendly)&&list.GetTarget()==enemy&&list.GetTargets()[1]==friendly,"remove deletes only first duplicate");
            Check(!list.RemoveTarget(decoration),"missing remove returns false");
            list.GetVisualTargets().Insert(0,null);Check(list.GetTarget()==null,"null first is not skipped");
            list.RemoveAllVisualTargets();Check(list.GetTargets().Count==0,"visual clear clears logical list");
            bool nullRange=false;try{list.AddTargets(null);}catch(ArgumentNullException){nullRange=true;}Check(nullRange,"null range preserves List exception");
            var dead=new GameObject("Destroyed target");list.AddTarget(dead);list.AddTarget(enemy);AssetRegistry.ReleaseTransient(dead);
            Check(ReferenceEquals(list.GetTarget(),dead)&&list.GetTarget()==null,"destroyed first slot retained without selecting next");
            var source=new NativeSpellEntityObject{type=4,controllerSide=0,resolve=()=>sourceObject};
            var ally=new NativeSpellEntityObject{type=4,controllerSide=0,resolve=()=>friendly};
            var foe=new NativeSpellEntityObject{type=4,controllerSide=1,resolve=()=>enemy,hero=()=>enemyHero};
            var props=new NativeSpellEntityObject{hasCard=false,resolve=()=>decoration};
            var selection=new NativeSpellObjectSelection{source=source};int borrowed=0;
            var lazy=new NativeSpellEntityObject{type=4,controllerSide=0,resolve=()=>{borrowed++;return friendly;}};
            selection.SetTargets(new[]{lazy,props,foe,ally});Check(borrowed==0,"metadata construction does not borrow scene objects");
            Check(selection.Targets.GetVisualTargets().Count==4&&borrowed==1,"list materialization borrows each supplied object once");
            Check(selection.visualTarget.Object==friendly&&selection.BestTarget().Object==enemy,"BestTarget prefers first opposing Card over friendly or non-Card");
            Check(selection.Facing(3)==enemyHero,"BestTargetHero uses selected opposing target's hero");
            selection.Targets.RemoveVisualTarget(enemy);Check(selection.BestTarget().Object==friendly,"no opposing Card falls back to first Card");
            selection.SetTargets(new[]{props,ally});Check(selection.BestTarget()==null,"fallback examines first target only, not first Card");
            selection.AddTarget(foe);Check(selection.BestTarget()==foe,"non-Card head does not prevent later opposing selection");
            source.controllerSide=null;Check(selection.BestTarget()==null,"missing source controller falls back to head Card only");
            source.controllerSide=0;selection.SetTargets(new NativeSpellEntityObject[]{null,foe});
            Check(selection.Targets.GetTarget()==null,"null metadata slot remains null at head");
            bool nullBest=false;try{selection.BestTarget();}catch(NullReferenceException){nullBest=true;}Check(nullBest,"original BestTarget does not silently skip null slot");
            selection.SetTargets(new[]{new NativeSpellEntityObject{resolve=()=>dead},foe});
            Type directError=null,selectionError=null;
            try{dead.GetComponent<Transform>();}catch(Exception error){directError=error.GetType();}
            try{selection.BestTarget();}catch(Exception error){selectionError=error.GetType();}
            Check(directError!=null&&selectionError==directError,"destroyed object follows actual Unity component access exception");
            selection.SetTargets(new[]{ally,foe,ally});selection.Targets.GetTargets().Reverse();
            Check(selection.Targets.GetTargets().Count==3&&selection.BestTarget()==foe,"external list mutation retains duplicate metadata identities");
            selection.Targets.RemoveAllTargets();Check(selection.visualTarget==null&&selection.BestTarget()==null,"cleared list has no stale singleton target");
        }
        finally{foreach(var go in new[]{sourceObject,friendly,enemy,decoration,enemyHero})AssetRegistry.ReleaseTransient(go);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        using(var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id)))
        {
            var caster=new EntityView{entity=10,card_id="CS2_025"};var ally=new EntityView{entity=20,card_id="CS2_179"};var foe=new EntityView{entity=30,card_id="CS2_179"};
            var ownHero=new EntityView{entity=40,card_id="HERO_08"};var foeHero=new EntityView{entity=50,card_id="HERO_01"};
            var before=new StateView{players=new[]{new PlayerView{seat=0,hero=ownHero,hand=new[]{caster},field=new[]{ally}},new PlayerView{seat=1,hero=foeHero,field=new[]{foe}}}};
            var after=new StateView{players=new[]{new PlayerView{seat=0,hero=ownHero,field=new[]{ally}},new PlayerView{seat=1,hero=foeHero,field=new EntityView[0]}}};
            var context=NativeSpellContext.FromEvent(new SemanticEvent{source=10,target=20,owner=0,card_id=caster.card_id},before,after,
                id=>database.cards.First(c=>c.CardId==id).Type,actors.Resolve,targetEntities:new[]{20,30,30});
            Check(context.Placement.Targets.GetTargets().SequenceEqual(new[]{actors.Resolve(ally),actors.Resolve(foe),actors.Resolve(foe)}),"explicit metadata IDs preserve order and duplicate after enemy leaves field");
            Check(context.Placement.BestTarget().Object==actors.Resolve(foe)&&context.Placement.Facing(3)==actors.Resolve(foeHero),"real event context selects opposing target and its hero from original snapshot");
            context.Placement.Targets.RemoveTarget(actors.Resolve(foe));Check(context.Placement.Targets.GetTargets().Count==2&&context.Placement.BestTarget().Object==actors.Resolve(foe),"one duplicate removal keeps remaining actual target");
            context.Placement.Targets.RemoveAllVisualTargets();Check(context.Placement.visualTarget==null,"actual event context clear releases active target choice");
        }
        visuals.Clear();Debug.Log("NATIVE SPELL TARGET COLLECTION: "+passed+" checks passed; full controller/task metadata import and battle composition remain incomplete");
        NativeSpellObjectChecks.Run();
    }
}
