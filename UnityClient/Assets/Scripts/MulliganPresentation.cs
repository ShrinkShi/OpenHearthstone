using System;
using System.Linq;
using UnityEngine;

public sealed class MulliganPresentation
{
    EntityView[] cards=new EntityView[0],old=new EntityView[0];
    float age,exchange=-1,exit=-1;
    bool openingHeld;
    public bool Active{get;private set;}
    public bool Confirmed{get;private set;}
    public bool Interactable{get{return Active&&!Confirmed&&!openingHeld&&age>.65f&&exchange<0&&exit<0;}}
    public float Darkness{get{return .52f*(exit<0?Tween.Curve(age/.7f):1-Tween.Curve(exit/.9f));}}
    public float ChromeAlpha{get{return exit<0?Tween.Curve(age/.6f):1-Tween.Curve(exit/.45f);}}
    public EntityView[] Cards{get{return cards;}}
    public EntityView[] OldCards{get{return old;}}
    public float Exchange{get{return exchange;}}
    public void HoldOpening(){if(Active&&!Confirmed){openingHeld=true;age=0;}}
    public void ReleaseOpening(){if(Active){openingHeld=false;age=0;}}
    public void Reset(){Active=Confirmed=openingHeld=false;age=0;exchange=exit=-1;cards=old=new EntityView[0];}
    public void Observe(StateView before,StateView after)
    {
        if(after.phase=="mulligan"&&!Active&&(before==null||before.match_id!=after.match_id))
        {
            Reset();Active=true;cards=Opening(after);return;
        }
        if(!Active)return;
        bool confirmed=!(after.moves??new Move[0]).Any(m=>m.kind=="mulligan");
        if(confirmed&&!Confirmed)
        {
            Confirmed=true;var incoming=Opening(after).Take(cards.Length).ToArray();
            if(!cards.Select(e=>e.entity).SequenceEqual(incoming.Select(e=>e.entity))){old=cards;cards=incoming;exchange=0;}
        }
        if(after.phase!="mulligan"&&exit<0){exit=0;openingHeld=false;}
    }
    EntityView[] Opening(StateView s){return s.players[s.seat].hand.Where(e=>e.card_id!="GAME_005").ToArray();}
    public void Tick(float delta)
    {
        if(!Active||openingHeld)return;age+=delta;
        if(exchange>=0){exchange+=delta;if(exchange>1.05f){exchange=-1;old=new EntityView[0];}}
        if(exit>=0){exit+=delta;if(exit>1.3f){Active=false;old=new EntityView[0];}}
    }
    public Rect Rect(int index,bool hovered)
    {
        var target=BattleLayout.Mulligan(index,cards.Length);
        if(exchange>=0&&!old.Any(e=>e.entity==cards[index].entity))
            target=Tween.Rect(new Rect(1680,240,120,180),target,(exchange-.32f)/.58f,Ease.Overshoot);
        else if(age<.85f)target=Tween.Rect(new Rect(1640,220,125,187),target,(age-index*.1f)/.56f,Ease.Out);
        if(exit>=0)
        {
            var pose=HandLayout.Pose(index,cards.Length);target=Tween.Rect(target,pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight),(exit-.34f)/.8f,Ease.InOut);
        }
        else if(hovered&&Interactable){var center=target.center;target.size*=1.06f;target.center=center-Vector2.up*14;}
        return target;
    }
    public Rect OldRect(int index)
    {
        return Tween.Rect(BattleLayout.Mulligan(index,old.Length),new Rect(1690,-230,90,135),exchange/.42f,Ease.In);
    }
}
