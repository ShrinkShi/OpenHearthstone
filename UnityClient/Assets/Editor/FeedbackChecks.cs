using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class FeedbackChecks
{
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception("Feedback check: "+message);passed++;}
    public static void Run()
    {
        passed=0;
        Check(UIText.StatColor(5,5,false)==Color.white,"full health white");
        Check(UIText.StatColor(4,5,true).r==1&&UIText.StatColor(4,5,true).g<.2f,"damage takes priority over buff");
        Check(UIText.StatColor(7,7,true).g==1&&UIText.StatColor(7,7,true).r<.2f,"full buff health green");
        Check(UIText.StatColor(1,2,false).r==1,"damaged durability red");
        Check(UIText.StatColor(3,3,true).g==1,"full buff durability green");
        Check(UIText.Rich("[x]<b>战吼：</b>获得<i>护甲</i><br/>2点<color=red>生命</color>")=="<b>战吼：</b>获得<i>护甲</i>\n2点生命","preserve safe rich text");
        Check(UIText.CardDescription("<b>战吼：获得2点护甲。</b>")=="<b>战吼</b>：获得2点护甲。","only keywords bold");
        Check(UIText.CardDescription("嘲讽<br>圣盾")=="<b>嘲讽</b>\n<b>圣盾</b>","two keywords and line break");
        Check(UIText.CardDescription("Battlecry: Deal 2 damage.")=="<b>Battlecry</b>: Deal 2 damage.","regular English body retains casing");
        CheckHeroPowerText();
        Check(!UIEditor.CategoriesRect.Overlaps(UIEditor.ElementsHeading)&&!UIEditor.ElementsHeading.Overlaps(UIEditor.ElementsRect),"editor category/heading/list never overlap");
        var clips=JsonUtility.FromJson<TurnButtonVisuals.Clips>(File.ReadAllText("../local/runtime/turn-button-animation.json"));
        var waiting=Array.Find(clips.clips,c=>c.name=="ENDTURN_WAITING");
        Check(Quaternion.Angle(TurnButtonVisuals.Sample(waiting,0),Quaternion.identity)<.01f,"original waiting flip starts facing up");
        Check(Quaternion.Angle(TurnButtonVisuals.Sample(waiting,1),Quaternion.Euler(180,0,0))<.01f,"original waiting flip ends facing down");
        Check(Mathf.Abs((TurnButtonVisuals.Sample(waiting,.25f)*Vector3.up).y)<.9f,"original flip has spatial intermediate rotation");
        var pickup=new CardPickup();pickup.Begin(12,new Vector2(800,1000));
        Check(!pickup.IsDrag(new Vector2(802,999)),"tap release holds card");
        pickup.Track(new Vector2(800,700));Check(pickup.IsDrag(new Vector2(800,1000)),"drag back remains a drag");
        Check(!CardPickup.DropAllowed(new Vector2(800,1000),false,true),"non-targeted card cannot cast from hand");
        Check(CardPickup.DropAllowed(new Vector2(900,600),false,false),"board drop allowed");
        Check(CardPickup.DropAllowed(new Vector2(900,220),true,true),"target arrow reaches enemy hero");
        Check(!CardPickup.DropAllowed(new Vector2(900,220),true,false),"illegal target rejected");
        pickup.Cancel();Check(pickup.Entity==0,"cancel clears pickup");
        string folder=Path.GetFullPath("../work/feedback-checks");Directory.CreateDirectory(folder);
        var calibration=new UICalibration(Path.Combine(folder,Guid.NewGuid()+".json"));
        calibration.Set(new UIAdjustment{id="card.minion",x=37,text=new UITextStyle{fillOverride=true,fill="#10ACFFFF",fontMode=2,outlineWidth=3}});
        Check(calibration.TextStyle("card.minion/name").fontMode==2,"parent font inheritance");
        var copy=calibration.Get("card.minion");copy.text.fill="#FF0000";
        Check(calibration.Get("card.minion").text.fill=="#10ACFFFF","style copies isolated");
        string error;Check(calibration.Save(out error),"style save");
        var restored=new UICalibration(calibration.PathName);Check(restored.Load(out error)&&restored.Get("card.minion").x==37&&restored.Get("card.minion").text.outlineWidth==3,"old geometry and new style round trip");
        var before=calibration.Serialize();bool invalid=false;try{calibration.Set(new UIAdjustment{id="bad",text=new UITextStyle{fill="invalid"}});}catch(InvalidDataException){invalid=true;}
        Check(invalid&&before==calibration.Serialize(),"invalid style edit atomic");
        calibration.Set(new UIAdjustment{id="card.minion/name",text=new UITextStyle{fill="#F0A010",fontMode=3}});
        Check(calibration.TextStyle("card.minion/name").fontMode==3,"child style overrides");calibration.Undo();
        Check(calibration.TextStyle("card.minion/name").fontMode==2,"undo restores inheritance");
        var state=new StateView{version=0,match_id="feedback",phase="play",players=new[]{new PlayerView{hero=new EntityView(),power=new EntityView(),field=new[]{new EntityView{entity=9,card_id="EX1_402"}},hand=new EntityView[0]}},events=new SemanticEvent[0]};
        var after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(state));after.version=1;after.events=new[]{new SemanticEvent{type="Trigger",source=9,target=9}};
        var presentation=new Presentation();var locations=new Dictionary<int,Rect>();presentation.Accept(null,state,locations);presentation.Accept(state,after,locations);
        Check(presentation.TriggerPulse(9)==0,"no premature trigger flash");presentation.Tick(.25f,locations);
        Check(presentation.TriggerPulse(9)>.9f&&presentation.TriggerPulse(10)==0,"only actual listener flashes");presentation.Tick(.6f,locations);
        Check(!presentation.Busy&&presentation.TriggerPulse(9)==0,"trigger finishes and queue drains");
        CardEffectRegistry.Load(Path.GetFullPath("../local/runtime"));
        Check(CardEffectRegistry.Find("HERO_08bp").hit=="SFX_FIREBLAST_IMPACT","Fireblast small impact mapping");
        Check(CardEffectRegistry.Find("CS2_024").projectile=="VFX_FROST_MISSILE","Frostbolt own particle texture");
        Check(CardEffectRegistry.Find("CS2_023").kind=="draw","untargeted draw effect");
        File.WriteAllText("../work/feedback-checks.json","{\"passed\":"+passed+"}");Debug.Log("FEEDBACK CHECKS PASSED: "+passed);
    }
    static void CheckHeroPowerText()
    {
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var ids=new[]{"HERO_01bp","HERO_02bp","HERO_03bp","HERO_04bp","HERO_05bp","HERO_06bp","HERO_07bp","HERO_08bp","HERO_09bp","HERO_10bp","HERO_11bp"};
        var powers=new Dictionary<string,CardDef>();
        foreach(var id in ids)
        {
            var card=Array.Find(database.cards,c=>c.CardId==id);Check(card!=null,"basic power definition exists: "+id);powers.Add(id,card);
            var entity=new EntityView{card_id=id,cost=card.Cost};
            Check(UIText.PowerName(card)==card.Name,"power name comes from its CardDef: "+id);
            Check(UIText.PowerCost(entity)==card.Cost,"power cost uses current entity value: "+id);
            entity.cost=card.Cost+1;Check(UIText.PowerCost(entity)==entity.cost,"power cost follows entity updates: "+id);
        }
        var hunter=UIText.PowerDescription(powers["HERO_05bp"],new EntityView{card_id="HERO_05bp"});
        Check(hunter.Contains("对敌方英雄造成2点伤害。")&&hunter.Contains("造成2点伤害。")&&!hunter.Contains("$2"),"Hunter placeholder resolved in both authored variants");
        var hunterModified=UIText.PowerDescription(powers["HERO_05bp"],new EntityView{card_id="HERO_05bp",display_damage=4,display_damage_known=true});
        Check(hunterModified.Contains("对敌方英雄造成4点伤害。")&&hunterModified.Contains("造成4点伤害。")&&!hunterModified.Contains("$2"),"Hunter known dynamic damage overrides both authored variants");
        var mage=UIText.PowerDescription(powers["HERO_08bp"],new EntityView{card_id="HERO_08bp"});
        Check(mage.Contains("造成1点伤害。")&&!mage.Contains("$1"),"Mage uses authored base damage when dynamic value is unknown");
        var mageModified=UIText.PowerDescription(powers["HERO_08bp"],new EntityView{card_id="HERO_08bp",display_damage=3,display_damage_known=true});
        Check(mageModified.Contains("造成3点伤害。")&&!mageModified.Contains("$1"),"Mage known dynamic damage overrides its base value");
        var priest=UIText.PowerDescription(powers["HERO_09bp"],new EntityView{card_id="HERO_09bp"});
        Check(priest.Contains("恢复2点生命值。")&&!priest.Contains("#2"),"Priest uses its base healing value when dynamic data is absent");
        var priestEntity=BattleInteraction.Copy(new EntityView{card_id="HERO_09bp",display_heal=4,display_heal_known=true});
        var priestModified=UIText.PowerDescription(powers["HERO_09bp"],priestEntity);
        Check(priestEntity.display_heal==4&&priestEntity.display_heal_known&&priestModified.Contains("恢复4点生命值。")&&!priestModified.Contains("#2"),"Priest copied entity preserves known dynamic healing");
        var warlock=UIText.PowerDescription(powers["HERO_07bp"],new EntityView{card_id="HERO_07bp"});
        Check(warlock.Contains("受到2点伤害。")&&!warlock.Contains("$2"),"Warlock uses its base self-damage when dynamic data is absent");
        var warlockModified=UIText.PowerDescription(powers["HERO_07bp"],new EntityView{card_id="HERO_07bp",display_damage=4,display_damage_known=true});
        Check(warlockModified.Contains("受到4点伤害。")&&!warlockModified.Contains("$2"),"Warlock known dynamic damage overrides its base value");
        var unknown=UIText.PowerDescription(new CardDef{CardId="HERO_05bp",Text="未知数值$7。"},new EntityView{card_id="HERO_05bp"});
        Check(unknown.Contains("$7"),"unmapped hero power placeholder remains visible");
        foreach(var id in ids.Where(id=>id!="HERO_05bp"&&id!="HERO_07bp"&&id!="HERO_08bp"&&id!="HERO_09bp"))
        {
            var text=powers[id].Text??"";
            Check(!text.Contains("$")&&!text.Contains("#")&&UIText.PowerDescription(powers[id],new EntityView{card_id=id})==UIText.CardDescription(text),"non-placeholder power text remains unchanged: "+id);
        }
    }
}
