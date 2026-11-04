using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 表现状态依据服务器合法动作；不根据客户端费用自行推断可操作性。
public static class BattleInteraction
{
    public static bool IsLegalTarget(StateView state,int source,int target)
    {return target!=0&&state!=null&&(state.moves??new Move[0]).Any(m=>m.entity==source&&m.target==target&&(m.kind=="power"||m.kind=="play"||m.kind=="attack"));}
    public static bool Ready(StateView state,int entity,string kind)
    {
        return state!=null&&state.phase=="play"&&state.active==state.seat
            &&(state.moves??new Move[0]).Any(m=>m.entity==entity&&m.kind==kind);
    }
    public static bool NoActions(StateView state)
    {
        return state!=null&&state.phase=="play"&&state.active==state.seat
            &&(state.moves??new Move[0]).Any(m=>m.kind=="end_turn")
            &&!(state.moves??new Move[0]).Any(m=>m.kind=="play"||m.kind=="power"||m.kind=="attack"||m.kind=="choose");
    }
    public static bool PlayRequiresTarget(StateView state,int entity)
    {
        var moves=(state==null?new Move[0]:state.moves??new Move[0]).Where(m=>m.kind=="play"&&m.entity==entity).ToArray();
        return moves.Length>0&&moves.All(m=>m.target!=0);
    }
    public static Color PlayGlow(EntityView entity)
    {return entity.combo_ready?new Color(1,.83f,.04f,.95f):new Color(.16f,1,.03f,.95f);}
    public static bool PlayOffersTarget(StateView state,int entity)
    {return entity!=0&&state!=null&&(state.moves??new Move[0]).Any(m=>m.kind=="play"&&m.entity==entity&&m.target!=0);}
    public static Rect DetailRect(Rect anchor,bool power=false)
    {
        const float w=292,h=438;
        float x=power||anchor.center.x<960?anchor.xMax+38:anchor.xMin-w-38;
        if(x+w>1885)x=anchor.xMin-w-38;
        return new Rect(Mathf.Clamp(x,120,1885-w),Mathf.Clamp(anchor.y-140,38,1040-h),w,h);
    }
    public static Rect PowerPortraitRect(Rect rect)
    {return new Rect(rect.x+rect.width*.10f,rect.y-rect.height*.045f,rect.width*.80f,rect.width*.80f);}
    public static EntityView Copy(EntityView e)
    {
        return e==null?null:new EntityView{card_id=e.card_id,cost=e.cost,attack=e.attack,health=e.health,
            armor=e.armor,durability=e.durability,max_health=e.max_health,max_durability=e.max_durability,
            health_buffed=e.health_buffed,durability_buffed=e.durability_buffed,attack_buffed=e.attack_buffed,combo_ready=e.combo_ready,asleep=e.asleep,used=e.used,display_damage=e.display_damage,display_damage_known=e.display_damage_known,display_heal=e.display_heal,display_heal_known=e.display_heal_known,keywords=e.keywords==null?new string[0]:(string[])e.keywords.Clone()};
    }
    public static EntityView Entity(StateView state,int id)
    {
        if(state==null||id==0)return null;
        foreach(var p in state.players??new PlayerView[0])
        {
            if(p.hero!=null&&p.hero.entity==id)return p.hero;
            if(p.power!=null&&p.power.entity==id)return p.power;
            if(p.weapon!=null&&p.weapon.entity==id)return p.weapon;
            var e=(p.field??new EntityView[0]).Concat(p.hand??new EntityView[0]).Concat(p.secrets??new EntityView[0]).FirstOrDefault(c=>c.entity==id);
            if(e!=null)return e;
        }
        return null;
    }
    public static int Owner(StateView state,int id)
    {
        if(state==null)return -1;
        for(int i=0;i<state.players.Length;i++)
        {
            var p=state.players[i];
            if((p.hero!=null&&p.hero.entity==id)||(p.power!=null&&p.power.entity==id)||(p.weapon!=null&&p.weapon.entity==id)
               ||(p.field??new EntityView[0]).Concat(p.hand??new EntityView[0]).Concat(p.secrets??new EntityView[0]).Any(c=>c.entity==id))return i;
        }
        return -1;
    }
}
public sealed class BattleHistory
{
    public sealed class Entry
    {
        public int version,turn,owner;
        public string kind;
        public EntityView card,target;
    }
    public readonly List<Entry> Entries=new List<Entry>();
    string match="";int version=-1;
    public void Reset(){Entries.Clear();match="";version=-1;}
    public void Observe(StateView before,StateView after,Func<string,CardDef> definition)
    {
        if(after==null)return;
        if(match!=after.match_id){Reset();match=after.match_id;}
        if(after.version<=version)return;version=after.version;
        if(before==null||before.match_id!=after.match_id)return;
        foreach(var e in after.events??new SemanticEvent[0])
        {
            if(e.type!="CardPlay"&&e.type!="Attack"&&e.type!="PlayHeroPower"&&e.type!="SecretTrigger"&&e.type!="Reveal")continue;
            var source=BattleInteraction.Entity(before,e.source)??BattleInteraction.Entity(after,e.source);
            int owner=BattleInteraction.Owner(before,e.source);if(owner<0)owner=BattleInteraction.Owner(after,e.source);
            if(e.type=="PlayHeroPower")
            {
                owner=BattleInteraction.Owner(before,e.target);
                if(owner<0)owner=before.active;
                if(owner>=0&&owner<before.players.Length)source=before.players[owner].power;
            }
            if(owner<0)owner=before.active;
            string id=!string.IsNullOrEmpty(e.card_id)?e.card_id:source==null?"":source.card_id;
            // 对方未揭示奥秘只记录背面；对方抽牌/生成牌根本不进入历史。
            bool secret=e.type=="CardPlay"&&owner!=after.seat&&owner>=0&&owner<after.players.Length
                &&after.players[owner].secret_count>before.players[owner].secret_count;
            EntityView card=secret?new EntityView():BattleInteraction.Copy(source);
            if(!secret&&card==null)
            {
                var c=definition(id);if(c==null)continue;
                card=new EntityView{card_id=c.CardId,cost=c.Cost,attack=c.Attack,health=c.Health,durability=c.Durability};
            }
            if(!secret&&string.IsNullOrEmpty(card.card_id))continue;
            Entries.Add(new Entry{version=after.version,turn=after.turn,owner=owner,kind=e.type,card=card,
                target=BattleInteraction.Copy(BattleInteraction.Entity(before,e.target)??BattleInteraction.Entity(after,e.target))});
        }
        if(Entries.Count>40)Entries.RemoveRange(0,Entries.Count-40);
    }
}
