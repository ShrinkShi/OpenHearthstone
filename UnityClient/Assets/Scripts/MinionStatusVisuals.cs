using System.Linq;
using UnityEngine;

// 只根据权威有效机制显示标志；多个标志并排，保留独立校对节点。
public static class MinionStatusVisuals
{
    static readonly string[] Order={"deathrattle","trigger","poisonous"};
    public static string[] Badges(string[] keywords,bool editor=false)
    {
        return Order.Where(k=>editor||(keywords!=null&&keywords.Contains(k))).ToArray();
    }
    public static Rect Badge(Rect portrait,int index,int count)
    {
        return new Rect(portrait.center.x-13+(index-(count-1)*.5f)*27,portrait.yMax-4,26,36);
    }
    public static Rect Flash(Rect badge,float pulse)
    {
        var result=badge;result.size*=1+Mathf.Clamp01(pulse)*.6f;result.center=badge.center;return result;
    }
}
