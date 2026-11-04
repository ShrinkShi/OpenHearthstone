using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗布局统一使用 1920×1080；窗口只做一次等比映射。
public static class BattleLayout
{
    public const float Width=1920, Height=1080;
    public const float OwnMinionY=512,EnemyMinionY=310;
    public static readonly Rect PlayZone=new Rect(460,300,1030,390);
    public static readonly Rect EndTurn=new Rect(1496,477,205,82);
    public static Matrix4x4 Matrix
    {
        get
        {
            return DesignViewport.Battle.Matrix;
        }
    }
    public static Rect Hero(bool own){return new Rect(865,own?700:88,190,200);}
    public static Rect TransformRect(Rect from,Rect to,Rect element)
    {
        if(from.width==0||from.height==0)return element;
        float x=to.width/from.width,y=to.height/from.height;
        return new Rect(to.x+(element.x-from.x)*x,to.y+(element.y-from.y)*y,element.width*x,element.height*y);
    }
    public static Rect Power(bool own){return new Rect(1090,own?750:146,122,126);}
    public static Rect Weapon(bool own){return new Rect(713,own?749:145,120,120);}
    public static Rect Deck(bool own){return new Rect(1618,own?690:210,96,140);}
    public static Rect Mana(bool own){return new Rect(1270,own?1014:48,435,41);}
    public static Rect ManaNumber(bool own){var area=Mana(own);return new Rect(area.x+335,area.y-1,100,41);}
    public static Rect OwnManaBounds(UICalibration calibration)
    {
        var number=ManaNumber(true);var matrix=Matrix4x4.identity;
        if(calibration!=null)matrix=calibration.Get("battle.mana.own").Matrix(Mana(true),Vector2.one)*calibration.Get("battle.mana.own/number").Matrix(number,Vector2.one);
        var a=(Vector2)matrix.MultiplyPoint3x4(number.min);var b=(Vector2)matrix.MultiplyPoint3x4(new Vector2(number.xMax,number.yMin));
        var c=(Vector2)matrix.MultiplyPoint3x4(number.max);var d=(Vector2)matrix.MultiplyPoint3x4(new Vector2(number.xMin,number.yMax));
        return Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.y,b.y,c.y,d.y),Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.y,b.y,c.y,d.y));
    }
    public static Rect Minion(bool own,int index,int count)
    {
        float step=Mathf.Min(155,990f/Mathf.Max(1,count));
        return new Rect(960+(index-(count-1)*.5f)*step-67,own?OwnMinionY:EnemyMinionY,134,172);
    }
    public static Rect Mulligan(int index,int count)
    {
        return new Rect(960+(index-(count-1)*.5f)*286-126,332,252,378);
    }
}

public struct CardPose
{
    public Vector2 center;
    public float angle,scale;
    public Rect Rect(float width,float height){return new Rect(center.x-width*scale/2,center.y-height*scale/2,width*scale,height*scale);}
    public static CardPose Lerp(CardPose a,CardPose b,float t)
    {
        return new CardPose{center=Vector2.Lerp(a.center,b.center,t),angle=Mathf.Lerp(a.angle,b.angle,t),scale=Mathf.Lerp(a.scale,b.scale,t)};
    }
}

public static class HandLayout
{
    public const float CardWidth=182, CardHeight=273;
    public static readonly Vector2 OwnAnchor=new Vector2(960,1048);
    static UICalibration cachedCalibration;
    static int cachedCount=-1,cachedHover=-2,cachedRevision=-1;
    static CardPose[] cachedPoses=new CardPose[0];
    public static CardPose Pose(int index,int count,bool enemy=false,int hovered=-1)
    {
        float offset=index-(count-1)*.5f;
        float step=Mathf.Min(enemy?57:92,(enemy?540:590)/Mathf.Max(1,count-1));
        float angle=offset*Mathf.Min(enemy?5:4.5f,31f/Mathf.Max(1,count-1));
        float x=960+offset*step;
        float y=(enemy?-10:OwnAnchor.y)+(enemy?-1:1)*Mathf.Pow(offset,2)*(enemy?3:4.2f);
        if(!enemy&&hovered>=0)
        {
            if(index==hovered)return new CardPose{center=new Vector2(x,863),scale=1.37f,angle=0};
            x+=Mathf.Sign(index-hovered)*46*Mathf.Exp(-Mathf.Abs(index-hovered)*.4f);
        }
        return new CardPose{center=new Vector2(x,y),angle=enemy?-angle:angle,scale=enemy?.62f:1};
    }
    static CardPose Calibrated(int index,int count,int hovered,UICalibration calibration)
    {
        var pose=Pose(index,count,false,hovered);
        return calibration==null?pose:calibration.Pose("battle.hand.own",pose,OwnAnchor);
    }
    public static Rect Bounds(CardPose pose)
    {
        float angle=pose.angle*Mathf.Deg2Rad;
        var half=new Vector2((Mathf.Abs(Mathf.Cos(angle))*CardWidth+Mathf.Abs(Mathf.Sin(angle))*CardHeight)*pose.scale/2,
            (Mathf.Abs(Mathf.Sin(angle))*CardWidth+Mathf.Abs(Mathf.Cos(angle))*CardHeight)*pose.scale/2);
        return new Rect(pose.center-half,half*2);
    }
    public static CardPose OwnPose(int index,int count,UICalibration calibration,int hovered=-1)
    {
        int revision=calibration==null?0:calibration.Revision;
        if(cachedCount!=count||cachedHover!=hovered||cachedCalibration!=calibration||cachedRevision!=revision)
        {
            cachedCount=count;cachedHover=hovered;cachedCalibration=calibration;cachedRevision=revision;
            cachedPoses=new CardPose[count];var mana=BattleLayout.OwnManaBounds(calibration);float overflow=0;
            // 整扇手牌一起避让；数量、Hover 或校对变化时才重新计算矩阵。
            for(int i=0;i<count;i++)
            {
                cachedPoses[i]=Calibrated(i,count,hovered,calibration);var bounds=Bounds(cachedPoses[i]);
                if(bounds.yMin<mana.yMax&&bounds.yMax>mana.yMin&&bounds.xMin<mana.xMax)
                    overflow=Mathf.Max(overflow,bounds.xMax-(mana.xMin-18));
            }
            for(int i=0;i<count;i++)cachedPoses[i].center.x-=Mathf.Max(0,overflow);
        }
        return cachedPoses[index];
    }
    public static bool Contains(CardPose pose,Vector2 point,float width=CardWidth,float height=CardHeight)
    {
        Vector2 d=point-pose.center;float a=-pose.angle*Mathf.Deg2Rad;
        var local=new Vector2(d.x*Mathf.Cos(a)-d.y*Mathf.Sin(a),d.x*Mathf.Sin(a)+d.y*Mathf.Cos(a));
        return Mathf.Abs(local.x)<=width*pose.scale/2&&Mathf.Abs(local.y)<=height*pose.scale/2;
    }
}

public sealed class MinionBoardLayout
{
    readonly Dictionary<int,Rect> poses=new Dictionary<int,Rect>();
    public void Reset(){poses.Clear();}
    public void Tick(StateView state,float delta)
    {
        float blend=1-Mathf.Exp(-delta*13);var alive=new HashSet<int>();
        for(int seat=0;seat<state.players.Length;seat++)
        {
            var field=state.players[seat].field;
            for(int i=0;i<field.Length;i++)
            {
                int id=field[i].entity;alive.Add(id);var target=BattleLayout.Minion(seat==state.seat,i,field.Length);
                Rect old;poses[id]=poses.TryGetValue(id,out old)?Tween.Rect(old,target,blend):target;
            }
        }
        foreach(int id in poses.Keys.ToArray())if(!alive.Contains(id))poses.Remove(id);
    }
    public Rect Pose(int id,Rect fallback){Rect result;return poses.TryGetValue(id,out result)?result:fallback;}
}
