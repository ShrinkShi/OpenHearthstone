using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSpellObjectChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original Spell object selection: "+message);passed++;}
    public static void Run()
    {
        passed=0;
        {
        var card=new GameObject("Actual source card");var hero=new GameObject("Controller hero");
        var ability=new GameObject("Ability owner");var power=new GameObject("Controller power");var indexed=new GameObject("Indexed power");
        var attached=new GameObject("Attached entity card");var target=new GameObject("Visual target card");
        try
        {
            int selectedIndex=-1;var source=new NativeSpellEntityObject{resolve=()=>card,type=5,hero=()=>hero,abilityOwner=()=>ability,power=()=>power,attached=()=>attached,
                hasPowerIndex=true,powerIndex=3,indexedPower=i=>{selectedIndex=i;return indexed;}};
            var objects=new NativeSpellObjectSelection{source=source,visualTarget=new NativeSpellEntityObject{resolve=()=>target,type=4}};
            Check(objects.SourceAuto()==hero,"spell uses controller hero before ability owner");
            source.hero=()=>null;Check(objects.SourceAuto()==ability,"spell uses ability owner when hero missing");
            source.abilityOwner=()=>null;Check(objects.SourceAuto()==card,"spell falls back to own card when both missing");
            source.type=10;Check(objects.SourceAuto()==indexed&&selectedIndex==3,"hero-power tag selects exact indexed power");
            source.indexedPower=i=>null;Check(objects.SourceAuto()==power,"missing indexed power falls back to default power");
            source.power=()=>null;Check(objects.SourceAuto()==card,"missing power falls back to source card");
            source.type=6;Check(objects.SourceAuto()==attached,"enchantment uses attached card");
            source.attached=()=>null;Check(objects.SourceAuto()==card,"missing attachment falls back to source card");
            foreach(int type in new[]{0,1,2,3,4,7,8,9,11,23}){source.type=type;Check(objects.SourceAuto()==card,"default auto retains original type "+type);}
            source.hero=()=>hero;source.type=4;objects.effectType=5;Check(objects.SourceAuto()==hero,"task effect type overrides source entity type");
            Check(objects.TargetAuto()==target,"task override does not apply to visual target");
            source.hasCard=false;Check(objects.SourceAuto()==card,"non-Card source ignores type override");source.hasCard=true;
            source.resolve=()=>null;Check(objects.SourceAuto()==null,"missing actual source is not replaced by hero");source.resolve=()=>card;
            objects.effectType=null;source.type=7;Check(objects.Location(2)==card&&objects.Location(3)==hero,"weapon Auto stays weapon while SourceHero is separate");
            Check(objects.Location(1)==card&&objects.Location(6)==target&&objects.Location(7)==target,"source and visual-target enum identities");
            Check(objects.Location(11)==null&&objects.Location(21)==null,"missing board and zones are not fabricated");
            Check(objects.Facing(7)==card&&objects.Facing(11)==card&&objects.Facing(2)==target&&objects.Facing(3)==null,"facing object identities and missing BestTarget");
        }
        finally{foreach(var go in new[]{card,hero,ability,power,indexed,attached,target})AssetRegistry.ReleaseTransient(go);}
        }
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        using(var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id)))
        {
            var source=new EntityView{entity=101,card_id="CS2_025"};var hero=new EntityView{entity=102,card_id="HERO_08"};
            var power=new EntityView{entity=103,card_id="HERO_08bp"};var minion=new EntityView{entity=201,card_id="CS2_179"};
            var weapon=new EntityView{entity=104,card_id="CS2_082"};
            var before=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,power=power,weapon=weapon,hand=new[]{source},field=new[]{minion}}}};
            var after=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,power=power,weapon=weapon,hand=new EntityView[0],field=new[]{minion}}}};
            Func<string,int> type=id=>database.cards.First(c=>c.CardId==id).Type;
            var spell=NativeSpellContext.FromEvent(new SemanticEvent{source=101,target=201,owner=0,card_id=source.card_id},before,after,type,actors.Resolve);
            Check(spell.Placement.Location(1)==actors.Resolve(source)&&actors.Find(source).Model.GetComponentsInChildren<Renderer>().Length>0,"source leaving hand has actual card model, not empty proxy");
            Check(spell.Placement.SourceAuto()==actors.Resolve(hero)&&spell.Placement.TargetAuto()==actors.Resolve(minion),"event bridge resolves source hero and actual minion");
            var skill=NativeSpellContext.FromEvent(new SemanticEvent{source=103,owner=0,card_id=power.card_id},before,after,type,actors.Resolve);
            Check(skill.Placement.SourceAuto()==actors.Resolve(power)&&actors.Find(power).Model.GetComponentsInChildren<Renderer>().Length>0,"hero power has actual power model");
            var armed=NativeSpellContext.FromEvent(new SemanticEvent{source=104,owner=0,card_id=weapon.card_id},before,after,type,actors.Resolve);
            Check(armed.Placement.SourceAuto()==actors.Resolve(weapon)&&armed.Placement.Location(3)==actors.Resolve(hero),"actual weapon remains distinct from owning hero");
            var attack=NativeSpellContext.FromEvent(new SemanticEvent{source=201,target=102,owner=0,card_id=minion.card_id},before,after,type,actors.Resolve);
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"23d38186cee691b6a6171c7a.actor-spell.json")));
            using(var effect=new NativeEffectInstance(program,assets,0,32,32,context:attack))
            {
                effect.AttachToOriginalParent(actors.Resolve(minion));effect.SelectSourceSpellState(1,0);effect.Seek(.6f);
                Check(effect.SourcePositionUpdates==1&&effect.UnboundSourcePlacements==0,"real attack Birth resolves SourceAuto through event context without manual enum resolver");
                effect.SelectSourceSpellState(3,.6f);effect.Seek(.61f);effect.Seek(.62f);
                Check(effect.SourceAttackActionsStarted==1&&effect.UnrestoredSourceActionSideEffects==0,"real attack Action resumes through automatic source placement");
            }
            Check(actors.Count==5,"source, hero, power, weapon and minion models cached once");
            var missing=NativeSpellContext.FromEvent(new SemanticEvent{source=999,owner=0,card_id=source.card_id},before,after,type,actors.Resolve);
            Check(missing.Placement.SourceAuto()==null,"unknown entity is not replaced by a fabricated source");
            actors.Clear();Check(actors.Count==0,"extended native object cache clears all types");
        }
        visuals.Clear();Debug.Log("NATIVE SPELL OBJECT SELECTION: "+passed+" checks passed; full task/target sets, zones, card identity and visual composition remain incomplete");
        NativeSpellTransformChecks.Run();
    }
}
