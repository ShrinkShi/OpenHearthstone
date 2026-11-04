using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class CombatFeedbackChecks
{
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception("Combat feedback check: "+message);passed++;}
    public static void Run()
    {
        passed=0;
        var calibration=new UICalibration(Path.GetFullPath("../local/ui-calibration.json"));string error;
        Check(calibration.Load(out error),"load existing user calibration");string original=calibration.Serialize();
        foreach(var settings in new[]{(UICalibration)null,calibration})
            for(int count=1;count<=10;count++)
                for(int hover=-1;hover<count;hover++)
                {
                    float previous=-10000;var mana=BattleLayout.OwnManaBounds(settings);
                    for(int i=0;i<count;i++)
                    {
                        var pose=HandLayout.OwnPose(i,count,settings,hover);
                        Check(pose.center.x>previous,"compact hand retains ordered cards");previous=pose.center.x;
                        Check(!HandLayout.Bounds(pose).Overlaps(mana),"hand and hover avoid calibrated mana counter");
                    }
                }
        Check(calibration.Serialize()==original&&!calibration.Dirty,"layout never overwrites saved calibration");
        var temporary=new UICalibration(Path.GetFullPath("../work/layout-cache-test.json"));
        var cached=HandLayout.OwnPose(0,1,temporary);temporary.Set(new UIAdjustment{id="battle.hand.own",x=-40});
        Check(HandLayout.OwnPose(0,1,temporary).center.x==cached.center.x-40,"layout cache invalidates on calibration revision");
        Check(HandLayout.Pose(9,10).center.x-HandLayout.Pose(0,10).center.x<=590,"ten cards have bounded span");
        Check(UIText.AttackColor(4,4)==Color.white,"printed attack white");
        Check(UIText.AttackColor(6,4).g==1&&UIText.AttackColor(6,4).r<.2f,"increased attack green");
        Check(UIText.AttackColor(4,4,true).g==1,"authoritative buff color preserved");
        var combo=new EntityView{combo_ready=true,attack_buffed=true};
        Check(BattleInteraction.PlayGlow(combo).r==1&&BattleInteraction.PlayGlow(combo).b<.1f,"ready combo yellow");
        Check(BattleInteraction.PlayGlow(new EntityView()).g==1,"ordinary playable card green");
        Check(BattleInteraction.Copy(combo).combo_ready&&BattleInteraction.Copy(combo).attack_buffed,"presentation copy preserves authoritative flags");
        var state=new StateView{moves=new[]{new Move{entity=3,kind="play",target=40,index=0},new Move{entity=3,kind="play",target=41,index=0}}};
        Check(BattleInteraction.PlayRequiresTarget(state,3),"battlecry/active combo uses legal targets");
        state.moves=new[]{new Move{entity=3,kind="play",target=0}};
        Check(!BattleInteraction.PlayRequiresTarget(state,3),"inactive combo does not invent a target");
        Check(!BattleInteraction.PlayRequiresTarget(state,99),"unplayable entity has no target indicator");
        var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/ordinary_combat.json"));
        var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/ordinary_combat-after.json"));
        var presentation=new Presentation();var positions=new Dictionary<int,Rect>();presentation.Accept(before,after,positions);
        var hits=presentation.Cues.Where(c=>c.data.type=="Hit").ToArray();var deaths=presentation.Cues.Where(c=>c.data.type=="Death").ToArray();
        Check(hits.Length==2&&hits[0].start==hits[1].start,"attack and retaliation damage simultaneously");
        Check(deaths.Length==2&&deaths[0].start==deaths[1].start&&deaths[0].apply==deaths[1].apply,"mutual deaths simultaneous");
        presentation.Seek(hits[0].start+.01f);
        Check(presentation.Display.players.SelectMany(p=>p.field).All(e=>e.health<=0),"both health values settle at same impact");
        presentation.Tick(1,positions);Check(!presentation.Busy&&presentation.Display.players.All(p=>p.field.Length==0),"ordinary combat drains without serial trigger delay");
        Check(before.players.All(p=>p.field[0].health==1),"presentation does not alter server snapshots");
        before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/combat_trigger.json"));
        after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/combat_trigger-after.json"));
        presentation.Reset();presentation.Accept(before,after,positions);hits=presentation.Cues.Where(c=>c.data.type=="Hit").ToArray();
        Check(hits.Length==2&&hits[0].start==hits[1].start,"retaliation does not wait for intervening armorsmith trigger");
        Check(presentation.Cues.First(c=>c.data.type=="Trigger").start>=hits.Max(c=>c.start+c.duration),"actual trigger still has its own subsequent animation");
        CardEffectRegistry.Load(Path.GetFullPath("../local/runtime"));
        foreach(string id in new[]{"CS2_072","EX1_124","CS2_073","EX1_129","CS2_029"})
            Check(CardEffectRegistry.Find(id)!=null,"incremental effects retain "+id);
        foreach(string name in new[]{"backstab","eviscerate","cold_blood"})
        {
            before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+".json"));
            after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+"-after.json"));
            presentation.Reset();presentation.Accept(before,after,positions);
            var effect=presentation.Cues.First(c=>c.data.type=="SpellEffect");var play=presentation.Cues.First(c=>c.data.type=="CardPlay");
            Check(effect.start>=play.duration,"effect follows departing card: "+name);
            Check(presentation.Cues.Where(c=>c.data.type=="Hit"||c.data.type=="Buff").All(c=>c.start+.0001f>=effect.start+effect.duration),"rules display waits for actual effect: "+name);
            presentation.Skip();Check(presentation.Display.public_hash==after.public_hash,"effect skip catches authority: "+name);
        }
        File.WriteAllText("../work/combat-feedback-checks.json","{\"passed\":"+passed+"}");Debug.Log("COMBAT FEEDBACK CHECKS PASSED: "+passed);
    }
}
