using System;
using System.IO;
using UnityEngine;

public static class BattleInteractionChecks
{
    static int passed;
    static void Check(bool valid,string message){if(!valid)throw new Exception("Battle interaction check: "+message);passed++;}
    static StateView State(int version)
    {
        return new StateView{match_id="interaction",version=version,phase="play",active=0,seat=0,
            moves=new[]{new Move{kind="end_turn"}},events=new SemanticEvent[0],players=new[]{
            new PlayerView{hero=new EntityView{entity=1},power=new EntityView{entity=2,card_id="power",cost=2},hand=new[]{new EntityView{entity=5,card_id="card",health=4}},field=new EntityView[0]},
            new PlayerView{hero=new EntityView{entity=3},power=new EntityView{entity=4},hand=new EntityView[0],field=new EntityView[0]}}};
    }
    public static void Run()
    {
        passed=0;var state=State(0);
        Check(BattleInteraction.NoActions(state),"only end turn becomes green");
        foreach(var kind in new[]{"play","power","attack","choose"})
        {
            state.moves=new[]{new Move{kind="end_turn"},new Move{kind=kind,entity=2}};
            Check(!BattleInteraction.NoActions(state),"available "+kind+" keeps yellow");
        }
        Check(BattleInteraction.Ready(state,2,"choose")&&!BattleInteraction.Ready(state,2,"power"),"legality rather than cost decides readiness");
        state.active=1;Check(!BattleInteraction.NoActions(state)&&!BattleInteraction.Ready(state,2,"choose"),"opponent turn cannot become ready");
        state.active=0;state.phase="mulligan";Check(!BattleInteraction.Ready(state,2,"choose")&&!BattleInteraction.NoActions(state),"mulligan excludes action highlights");
        var left=new Rect(500,350,134,172);var right=new Rect(1200,350,134,172);
        Check(BattleInteraction.DetailRect(left).xMin>left.xMax,"left minion details on right");
        Check(BattleInteraction.DetailRect(right).xMax<right.xMin,"right minion details on left");
        Check(BattleInteraction.DetailRect(BattleLayout.Power(true),true).xMin>BattleLayout.Power(true).xMax,"power details prefer right");
        Check(BattleInteraction.DetailRect(new Rect(1820,1030,100,100),true).yMax<=1040,"detail stays inside screen");
        for(int n=1;n<=10;n++)
        {
            var hover=HandLayout.Pose(n/2,n,false,n/2).Rect(HandLayout.CardWidth,HandLayout.CardHeight);
            Check(hover.yMin>=0&&hover.yMax<=1080,"hover rules visible with "+n+" cards");
        }
        var history=new BattleHistory();var before=State(0);history.Observe(null,before,id=>new CardDef{CardId=id});
        var after=State(1);after.events=new[]{new SemanticEvent{type="CardPlay",source=5,card_id="card"},new SemanticEvent{type="Hit",source=5,target=3,amount=3}};
        history.Observe(before,after,id=>new CardDef{CardId=id});Check(history.Entries.Count==1,"damage does not duplicate action rows");
        before.players[0].hand[0].health=99;Check(history.Entries[0].card.health==4&&history.Entries[0].card.entity==0,"history preserves independent snapshot and cannot override input target");
        history.Observe(before,after,id=>new CardDef{CardId=id});Check(history.Entries.Count==1,"duplicate packet ignored");
        var power=State(2);power.events=new[]{new SemanticEvent{type="PlayHeroPower",source=77,target=2}};
        history.Observe(after,power,id=>new CardDef{CardId=id});Check(history.Entries.Count==2&&history.Entries[1].card.card_id=="power","player sourced hero power resolves public power identity");
        var hidden=State(3);hidden.active=1;hidden.events=new[]{new SemanticEvent{type="Draw",source=3,target=9,card_id="hidden"}};
        history.Observe(power,hidden,id=>new CardDef{CardId=id});Check(history.Entries.Count==2,"opponent draw identity not exposed");
        var secret=State(4);secret.players[1].secret_count=1;secret.events=new[]{new SemanticEvent{type="CardPlay",source=9,card_id="secret"}};
        history.Observe(hidden,secret,id=>new CardDef{CardId=id});Check(history.Entries.Count==3&&string.IsNullOrEmpty(history.Entries[2].card.card_id),"unrevealed enemy secret displays back");
        for(int i=5;i<55;i++){var next=State(i);next.events=new[]{new SemanticEvent{type="Attack",source=1,target=3}};history.Observe(secret,next,id=>new CardDef{CardId=id});}
        Check(history.Entries.Count<=40,"history bounded");
        history.Reset();Check(history.Entries.Count==0,"return to menu clears history");
        var attackBefore=State(0);attackBefore.players[1].field=new[]{new EntityView{entity=8,card_id="target",attack=3,health=2}};
        var attackAfter=State(1);attackAfter.events=new[]{new SemanticEvent{type="Attack",source=1,target=8}};
        history.Observe(null,attackBefore,id=>new CardDef{CardId=id});attackBefore.players[0].hero.card_id="hero";
        history.Observe(attackBefore,attackAfter,id=>new CardDef{CardId=id});
        Check(history.Entries.Count==1&&history.Entries[0].target.card_id=="target"&&history.Entries[0].target.health==2,"dead target remains inspectable from action snapshot");
        attackAfter.phase="finished";Check(!BattleInteraction.NoActions(attackAfter),"result screen never enables end turn");
        var nextMatch=State(0);nextMatch.match_id="next-match";history.Observe(null,nextMatch,id=>new CardDef{CardId=id});
        Check(history.Entries.Count==0,"new match clears old history");
        File.WriteAllText("../work/interaction-checks.json","{\"passed\":"+passed+"}");Debug.Log("BATTLE INTERACTION CHECKS PASSED: "+passed);
    }
}
