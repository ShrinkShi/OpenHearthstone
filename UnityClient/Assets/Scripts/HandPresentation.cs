using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class HandPresentation
{
    readonly Dictionary<int,CardPose> poses=new Dictionary<int,CardPose>();
    public int Hovered{get;private set;}
    public int HoverIndex{get;private set;}=-1;
    public void Reset(){poses.Clear();Hovered=0;HoverIndex=-1;}
    public CardPose Pose(int entity,CardPose fallback)
    {
        CardPose pose;if(poses.TryGetValue(entity,out pose))return pose;
        // 截图采样或跳过动画可在 Update 与 OnGUI 之间切换快照。
        poses[entity]=fallback;return fallback;
    }
    public void Tick(EntityView[] hand,Vector2 mouse,int dragged,Presentation presentation,bool interactive,float delta,int forcedHover=-1)
    {
        int next=0,index=-1;
        if(interactive&&dragged==0)
        {
            for(int i=0;i<hand.Length;i++)
            {
                CardPose pose;if(!poses.TryGetValue(hand[i].entity,out pose))pose=HandLayout.OwnPose(i,hand.Length,UICalibration.Current);
                bool inside=UICalibration.Current!=null?UICalibration.Current.Hit(hand[i].entity,mouse):HandLayout.Contains(pose,mouse);
                if(inside){next=hand[i].entity;index=i;}
            }
            if(forcedHover>=0&&forcedHover<hand.Length){index=forcedHover;next=hand[index].entity;}
        }
        Hovered=next;HoverIndex=index;
        float blend=1-Mathf.Exp(-delta*17);
        for(int i=0;i<hand.Length;i++)
        {
            int id=hand[i].entity;var target=HandLayout.OwnPose(i,hand.Length,UICalibration.Current,index);
            float arrival=presentation.HandProgress(id);
            target.center=Vector2.Lerp(BattleLayout.Deck(true).center,target.center,arrival);
            if(id==dragged)target=new CardPose{center=mouse,angle=0,scale=1.16f};
            CardPose old;if(!poses.TryGetValue(id,out old))old=target;
            poses[id]=CardPose.Lerp(old,target,blend);
        }
        var alive=new HashSet<int>(hand.Select(e=>e.entity));foreach(int id in poses.Keys.ToArray())if(!alive.Contains(id))poses.Remove(id);
    }
}
