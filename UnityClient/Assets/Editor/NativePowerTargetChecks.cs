using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativePowerTargetChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original controller targets: "+message);passed++;}
    static NativePowerHistoryTask Tag(int id){return new NativePowerHistoryTask{type=4,entity=id,tagChange=true};}
    public static void Run()
    {
        passed=0;var source=new GameObject("Power source Card");var a=new GameObject("Target A");var b=new GameObject("Target B");
        try
        {
            var records=new Dictionary<int,NativeSpellEntityObject>{[1]=new NativeSpellEntityObject{entity=1,type=4,resolve=()=>source},[2]=new NativeSpellEntityObject{entity=2,type=4,resolve=()=>a},[3]=new NativeSpellEntityObject{entity=3,type=4,resolve=()=>b},[4]=new NativeSpellEntityObject{entity=4,hasCard=false}};
            int warnings=0,resolutions=0;Func<int,NativeSpellEntityObject> entity=id=>{resolutions++;NativeSpellEntityObject value;return records.TryGetValue(id,out value)?value:null;};
            var collector=new NativePowerTaskTargets(entity,id=>id==99,message=>warnings++);
            var task=new NativePowerTaskList{id=7,hasBlockStart=true,sourceEntities=new[]{1,1},tasks=new[]{Tag(1),Tag(2),Tag(2),new NativePowerHistoryTask{type=8,entity=3},Tag(3),Tag(98),Tag(4)}};
            Check(collector.AttachPowerTaskList(task),"valid block sources accepted");
            Check(collector.GetSources().SequenceEqual(new[]{source,source}),"source duplicates preserved");
            Check(collector.GetTargets().SequenceEqual(new[]{a,b})&&warnings==1,"tag-change targets ordered, unique, source excluded, unknown/non-Card skipped");
            Check(collector.GetSource()==source&&collector.GetTarget()==a&&collector.TaskListId==7,"controller first source/target and task ID");
            Check(collector.AttachPowerTaskList(task)&&collector.GetTargets().Count==2,"same attachment recollects without duplicate targets");
            collector.AddTarget(a);Check(collector.GetTargets().Count==3,"direct controller AddTarget does not deduplicate");
            collector.RemoveTarget(a);Check(collector.GetTargets().SequenceEqual(new[]{b,a}),"controller RemoveTarget removes first matching entry");
            var selection=new NativeSpellObjectSelection();collector.ApplyTo(selection);Check(selection.source.Object==source&&selection.Targets.GetTargets().SequenceEqual(new[]{b,a}),"actual collected identities reach Spell placement");
            var old=collector.DetachPowerTaskList();Check(old==task&&collector.TaskList==null&&collector.GetSources().Count==0&&collector.GetTargets().Count==0&&collector.TaskListId==7,"detach clears objects and retains original ID field");
            collector.SetPowerTaskList(task);Check(collector.TaskList==task&&collector.GetSources().Count==0&&collector.TaskListId==7,"SetTaskList does not collect");
            var other=new NativePowerTaskList{id=8,hasBlockStart=true,sourceEntities=new[]{2},tasks=new[]{Tag(1)}};
            collector.SetPowerTaskList(other);Check(collector.TaskListId==7,"SetTaskList does not update original ID field");
            Check(collector.AddPowerSourceAndTargets(task)&&collector.GetSources()[0]==source&&collector.GetTargets().Count==0,"direct collector uses input sources but attached-list targets");
            var empty=new NativePowerTaskList{id=9,hasBlockStart=true,sourceEntities=new[]{1}};
            Check(!collector.AttachPowerTaskList(empty)&&collector.GetSources().Count==0,"empty non-end task rejected before source mutation");
            empty.endOfBlock=true;Check(collector.AttachPowerTaskList(empty)&&collector.GetSource()==source,"empty end-of-block list accepted");
            empty.blockHasEffectTiming=true;Check(!collector.AddPowerSourceAndTargets(empty),"non-meta list inside meta block rejected");
            empty.hasEffectTiming=true;Check(collector.AddPowerSourceAndTargets(empty),"matching timing metadata accepted");
            var removed=new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{99,1},tasks=new[]{Tag(2)}};
            Check(collector.HasSourceCard(removed),"removed missing source Entity is skipped");
            var missing=new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{98,1},tasks=new[]{Tag(2)}};
            int beforeWarnings=warnings;Check(!collector.HasSourceCard(missing)&&warnings==beforeWarnings+1,"required missing live source rejects with warning");
            Check(!collector.HasSourceCard(new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{4}}),"existing Entity without Card rejects");
            Check(!collector.HasSourceCard(new NativePowerTaskList{sourceEntities=new[]{1}}),"missing block start rejects");
            Check(!collector.HasSourceCard(new NativePowerTaskList{hasBlockStart=true,sourceEntities=new int[0]}),"empty source block rejects");
            Check(!collector.HasSourceCard(new NativePowerTaskList{hasBlockStart=true}),"default repeated source IDs represent an empty block");
            var cached=new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{98,1}};
            var first=cached.GetSourceEntities(false,entity,null,null);int previousResolutions=resolutions;
            Check(first.Count==2&&first[0]==null&&ReferenceEquals(first,cached.GetSourceEntities(true,entity,null,null))&&resolutions==previousResolutions,"non-required cache retains null without revalidating required flag");
            cached.SetSourceEntitiesDirty();Check(cached.GetSourceEntities(true,entity,null,null)==null,"dirty flag revalidates missing required source");
            bool malformed=false;try{collector.GetTargetCardFromPowerTask(new NativePowerHistoryTask{type=4});}catch(NullReferenceException){malformed=true;}
            Check(malformed,"type-4 non-TagChange preserves original failed cast semantics");
            task.effectType=5;collector.AttachPowerTaskList(task);collector.ApplyTo(selection);Check(selection.effectType==5,"explicit EffectEntityDef type passes into SourceAuto");
        }
        finally{foreach(var go in new[]{source,a,b})AssetRegistry.ReleaseTransient(go);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        using(var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id)))
        {
            var caster=new EntityView{entity=10,card_id="CS2_025"};var minion=new EntityView{entity=20,card_id="CS2_179"};var foe=new EntityView{entity=30,card_id="CS2_179"};var hero=new EntityView{entity=40,card_id="HERO_08"};
            var before=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,hand=new[]{caster},field=new[]{minion}},new PlayerView{seat=1,field=new[]{foe}}}};
            var after=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,field=new[]{minion}},new PlayerView{seat=1,field=new EntityView[0]}}};
            var tasks=new NativePowerTaskList{id=15,hasBlockStart=true,sourceEntities=new[]{20},tasks=new[]{Tag(30),Tag(30)}};
            var context=NativeSpellContext.FromEvent(new SemanticEvent{source=10,target=20,owner=0,card_id=caster.card_id},before,after,id=>database.cards.First(c=>c.CardId==id).Type,actors.Resolve,powerTasks:tasks);
            Check(context.ActorObject(0)==actors.Resolve(minion)&&context.CardType(0)==4,"controller history source overrides semantic source identity and type");
            Check(context.ActorObject(1)==actors.Resolve(foe)&&context.CardType(1)==4&&context.Placement.Targets.GetTargets().Count==1,"original target dedup reaches actor lookup after death snapshot");
            Check(context.PowerTargets.TaskListId==15&&context.Placement.SourceAuto()==actors.Resolve(minion),"controller remains attached to resulting context");
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"23d38186cee691b6a6171c7a.actor-spell.json")));
            using(var effect=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {effect.AttachToOriginalParent(context.ActorObject(0));effect.SelectSourceSpellState(1,0);effect.Seek(.6f);Check(effect.SourcePositionUpdates==1&&effect.UnboundSourcePlacements==0,"actual source attack uses controller-imported primary source");}
            using(var player=new NativeEffectPlayer(assets))
            {
                player.CardType=id=>database.cards.First(c=>c.CardId==id).Type;player.ActorObject=actors.Resolve;int providers=0;
                player.OriginalPowerTasks=(data,oldState,newState)=>{providers++;return tasks;};
                    bool prepared=player.PrepareSource("Original history integration",new SemanticEvent{source=10,target=20,owner=0,card_id=caster.card_id},before,after,true,0);
                    Check(prepared&&providers==1&&player.ActiveCount==1,"real native player invokes original-history provider and creates source instance");
                    Check(player.BorrowedActors.Contains(actors.Resolve(minion))&&player.BorrowedActors.Contains(actors.Resolve(foe)),"cached history sources and targets register with current effect writer");
            }
            bool rejected=false;try{NativeSpellContext.FromEvent(new SemanticEvent{source=10},before,after,id=>4,actors.Resolve,powerTasks:new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{999}});}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"failed original collection does not silently fall back to semantic source");
        }
        visuals.Clear();Debug.Log("NATIVE POWER SOURCE/TARGET COLLECTION: "+passed+" checks passed; raw history transport, task execution and full controller remain incomplete");
        NativeSpellTargetChecks.Run();
    }
}
