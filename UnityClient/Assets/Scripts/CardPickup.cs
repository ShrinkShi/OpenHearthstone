using UnityEngine;

// 点击拾起和按住拖动共用状态；短点击松开只持牌，不提交网络动作。
public sealed class CardPickup
{
    public int Entity{get;private set;}
    Vector2 start;
    bool moved;
    public void Begin(int entity,Vector2 pointer){Entity=entity;start=pointer;moved=false;}
    public void Track(Vector2 pointer){if(Entity!=0&&Vector2.Distance(start,pointer)>=8)moved=true;}
    public bool IsDrag(Vector2 pointer){Track(pointer);return Entity!=0&&moved;}
    public void Cancel(){Entity=0;moved=false;}
    public static bool DropAllowed(Vector2 pointer,bool targeted,bool legalTarget)
    {return targeted?legalTarget:BattleLayout.PlayZone.Contains(pointer);}
}
