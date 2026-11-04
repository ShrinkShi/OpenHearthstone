using System;
using System.Collections.Generic;
using System.Linq;

public sealed class NativeMinionSummonVoices
{
    sealed class Binding
    {
        public string cardId,eventKey;
    }

    readonly Dictionary<SemanticEvent,Binding> bindings=new Dictionary<SemanticEvent,Binding>();
    readonly HashSet<string> observedKeys=new HashSet<string>();
    readonly Queue<string> observedOrder=new Queue<string>();
    string match="";
    int version=-1;
    public int Registered{get;private set;}
    public int Handled{get;private set;}
    public int Rejected{get;private set;}
    public int Duplicate{get;private set;}
    public int PendingCount{get{return bindings.Count;}}

    public void ObserveSummonVoice(StateView before,StateView after)
    {
        if(after==null||String.IsNullOrEmpty(after.match_id))return;
        if(before==null||before.match_id!=after.match_id)
        {
            Clear();match=after.match_id;version=after.version;return;
        }
        if(match!=after.match_id)
        {
            // A valid same-match pair is enough to identify the events introduced by after.
            Clear();match=after.match_id;version=before.version;
        }
        if(after.version<=version)return;
        version=after.version;
        var events=after.events??new SemanticEvent[0];
        for(int i=0;i<events.Length;i++)
        {
            var data=events[i];
            if(data==null||data.type!="Summon")continue;
            if(data.card_type!=4||data.target<=0){Rejected++;continue;}
            if(IsNonMinionEntity(before,data.target)||IsNonMinionEntity(after,data.target))
            {Rejected++;continue;}
            EntityView minion;
            if(data.visual!=null)
            {
                // Summon.visual is the entity as it entered. Keep that identity if the
                // same rules batch then morphs, kills, or returns it to hand.
                minion=data.visual;
                if(minion.entity!=data.target||String.IsNullOrEmpty(minion.card_id))
                {Rejected++;continue;}
            }
            else
            {
                minion=FindMinion(after,data.target);
                if(minion==null||String.IsNullOrEmpty(minion.card_id)||ContainsMinion(before,data.target))
                {Rejected++;continue;}
            }
            string eventIdentity=String.IsNullOrEmpty(data.event_id)?after.version+":"+i:"event:"+data.event_id;
            string key=after.match_id+":"+eventIdentity+":"+data.target;
            if(!Remember(key)||bindings.ContainsKey(data)){Duplicate++;continue;}
            bindings[data]=new Binding{cardId=minion.card_id,eventKey=key};Registered++;
        }
    }

    static EntityView FindMinion(StateView state,int entity)
    {
        if(state==null||state.players==null)return null;
        foreach(var player in state.players)
        {
            if(player==null||player.field==null)continue;
            var found=player.field.FirstOrDefault(card=>card!=null&&card.entity==entity);
            if(found!=null)return found;
        }
        return null;
    }

    static bool IsNonMinionEntity(StateView state,int entity)
    {
        return state!=null&&state.players!=null&&state.players.Any(player=>player!=null&&
            (player.hero!=null&&player.hero.entity==entity||player.power!=null&&player.power.entity==entity||
             player.weapon!=null&&player.weapon.entity==entity));
    }

    static bool ContainsMinion(StateView state,int entity)
    {return FindMinion(state,entity)!=null;}

    bool Remember(string key)
    {
        if(!observedKeys.Add(key))return false;
        observedOrder.Enqueue(key);
        while(observedOrder.Count>4096)observedKeys.Remove(observedOrder.Dequeue());
        return true;
    }

    public bool TryHandleSummonVoice(SemanticEvent data,out string minionCardId,out string eventKey)
    {
        minionCardId="";eventKey="";
        Binding binding;
        if(data==null||!bindings.TryGetValue(data,out binding))return false;
        bindings.Remove(data);
        minionCardId=binding.cardId;eventKey=binding.eventKey;Handled++;
        return true;
    }

    public void Complete(StateView after)
    {
        if(after==null)return;
        foreach(var data in after.events??new SemanticEvent[0])if(data!=null)bindings.Remove(data);
    }

    public void Clear()
    {
        bindings.Clear();observedKeys.Clear();observedOrder.Clear();match="";version=-1;
        Registered=Handled=Rejected=Duplicate=0;
    }
}
