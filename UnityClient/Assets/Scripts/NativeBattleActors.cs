using System;
using System.Collections.Generic;
using UnityEngine;

// Borrowed by native effects; effects must release their mutations before Clear.
public sealed class NativeBattleActors : IDisposable
{
    readonly CardVisuals visuals;
    readonly Func<string,CardDef> cards;
    readonly Dictionary<int,CardVisuals.LiveActor> actors=new Dictionary<int,CardVisuals.LiveActor>();
    readonly Dictionary<int,NativeHighlightState> highlights=new Dictionary<int,NativeHighlightState>();
    readonly HashSet<int> highlightFailures=new HashSet<int>();
    readonly HashSet<int> dirty=new HashSet<int>();
    public int Count{get{return actors.Count;}}
    public NativeBattleActors(CardVisuals visuals,Func<string,CardDef> cards){this.visuals=visuals;this.cards=cards;}
    public GameObject Resolve(EntityView entity)
    {
        if(entity==null)return null;
        var card=cards(entity.card_id);if(card==null||(card.Type!=3&&card.Type!=4&&card.Type!=5&&card.Type!=7&&card.Type!=10))return null;
        CardVisuals.LiveActor actor;
        if(actors.TryGetValue(entity.entity,out actor))return actor.CardId==entity.card_id?actor.ActorObject:null;
        string mode=card.Type==3?"hero":card.Type==4?"minion":card.Type==7?"weapon":card.Type==10?"power":"card";
        actor=visuals.CreateLiveActor(card,mode,card.Type==4);actors.Add(entity.entity,actor);return actor.ActorObject;
    }
    public CardVisuals.LiveActor Find(EntityView entity)
    {CardVisuals.LiveActor actor;return entity!=null&&actors.TryGetValue(entity.entity,out actor)&&actor.CardId==entity.card_id?actor:null;}
    public bool Highlight(EntityView entity,AssetRegistry assets,int state)
    {
        var actor=Find(entity);if(actor==null||highlightFailures.Contains(entity.entity))return false;
        var identity=actor.ActorObject.GetComponent<NativeActorIdentity>();if(identity==null)return false;
        NativeHighlightState highlight;
        try
        {
            if(!highlights.TryGetValue(entity.entity,out highlight))
            {highlight=new NativeHighlightState(identity,assets);highlights.Add(entity.entity,highlight);}
            return highlight.ChangeState(state);
        }
        catch(Exception error)
        {
            if(highlights.TryGetValue(entity.entity,out highlight)){highlight.Dispose();highlights.Remove(entity.entity);}
            highlightFailures.Add(entity.entity);Debug.LogWarning("Original battle highlight fallback: "+error.Message);return false;
        }
    }
    public static int HighlightState(bool own,bool ready,bool over,bool selected)
    {return selected?5:ready?(over?7:6):over?(own?2:3):1;}
    public void Invalidate(){foreach(var id in actors.Keys)dirty.Add(id);}
    public void RefreshUnborrowed(IEnumerable<GameObject> borrowed)
    {
        var keep=new HashSet<GameObject>(borrowed);
        foreach(var id in new List<int>(dirty))
        {
            CardVisuals.LiveActor actor;
            if(actors.TryGetValue(id,out actor)&&!keep.Contains(actor.ActorObject)){Release(id,actor);actors.Remove(id);}
        }
    }
    void Release(int entity,CardVisuals.LiveActor actor)
    {
        NativeHighlightState highlight;if(highlights.TryGetValue(entity,out highlight)){highlight.Dispose();highlights.Remove(entity);}
        dirty.Remove(entity);highlightFailures.Remove(entity);actor.Dispose();
    }
    public void Retain(IEnumerable<GameObject> borrowed)
    {
        var keep=new HashSet<GameObject>(borrowed);var remove=new List<int>();
        foreach(var pair in actors)if(!keep.Contains(pair.Value.ActorObject)){Release(pair.Key,pair.Value);remove.Add(pair.Key);}
        foreach(var id in remove)actors.Remove(id);
    }
    public void Clear(){foreach(var pair in actors)Release(pair.Key,pair.Value);actors.Clear();}
    public void Dispose(){Clear();}
}
