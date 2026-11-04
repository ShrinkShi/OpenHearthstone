using System;
using System.Linq;
using UnityEngine;

public sealed class HeroEmoteMenu
{
    public static readonly string[] Options={"THANKS","WELL_PLAYED","GREETINGS","WOW","OOPS","THREATEN","SORRY"};
    const float BubbleWidth=228,BubbleHeight=114;
    readonly Rect[] imageRects=new Rect[7];
    readonly Rect[] hitRects=new Rect[7];
    readonly string[] regions={"upper_right_tail","upper_right_tail","upper_right_tail",
        "lower_left_tail","lower_left_tail","lower_left_tail","lower_left_tail"};
    string matchId="";
    bool open;

    public bool IsOpen{get{return open;}}
    public void Close(){open=false;}

    public Rect OptionRect(string id)
    {int index=Array.IndexOf(Options,id);return index<0?new Rect():CalibratedRect(index,imageRects[index]);}
    public Rect OptionHitRect(string id)
    {int index=Array.IndexOf(Options,id);return index<0?new Rect():CalibratedRect(index,hitRects[index]);}
    public Rect OptionArtworkRect(string id)
    {
        int index=Array.IndexOf(Options,id);if(index<0)return new Rect();
        var image=imageRects[index];bool left=index<3;
        int x0=left?68:69,y0=left?22:12,x1=left?471:470,y1=left?210:200;
        var art=new Rect(image.x+x0/512f*BubbleWidth,image.y+y0/256f*BubbleHeight,
            (x1-x0)/512f*BubbleWidth,(y1-y0)/256f*BubbleHeight);
        return CalibratedRect(index,art);
    }

    public bool ReplayRightClick(Vector2 mouse,Rect ownHero,string currentMatch)
    {
        string diagnostic;
        return ReplayRightClick(mouse,ownHero,currentMatch,out diagnostic);
    }

    public bool ReplayRightClick(Vector2 mouse,Rect ownHero,string currentMatch,out string diagnostic)
    {
        EventType type=EventType.MouseDown;int button=1;bool consume;
        bool wasOpen=open,insideHero=ownHero.Contains(mouse);
        bool handled=HandleInput(type,button,KeyCode.None,mouse,ownHero,currentMatch,out consume);
        diagnostic="type="+type+", button="+button+", mouse="+mouse+", heroRect="+ownHero+", currentMatch="+(currentMatch??"")+", storedMatch="+matchId+
            ", ownHeroContains="+insideHero+", wasOpen="+wasOpen+", handled="+handled+", consumed="+consume+", open="+open;
        return handled&&open;
    }

    public bool ReplayInput(EventType type,int button,KeyCode keyCode,Vector2 mouse,Rect ownHero,string currentMatch,out bool consumeEvent)
    {return HandleInput(type,button,keyCode,mouse,ownHero,currentMatch,out consumeEvent);}

    public bool ReplaySelect(string id,Action<string> send)
    {
        if(!open||Array.IndexOf(Options,id)<0)return false;
        open=false;send(id);return true;
    }

    public bool HandleInput(Event current,Vector2 mouse,Rect ownHero,string currentMatch,out bool consumeEvent)
    {
        if(current==null){consumeEvent=false;return false;}
        return HandleInput(current.type,current.button,current.keyCode,mouse,ownHero,currentMatch,out consumeEvent);
    }

    public bool HandleInput(EventType type,int button,KeyCode keyCode,Vector2 mouse,Rect ownHero,string currentMatch,out bool consumeEvent)
    {
        consumeEvent=false;
        if(currentMatch!=matchId&&open)open=false;
        matchId=currentMatch??"";
        if(open)Layout(ownHero);
        if(type==EventType.KeyDown&&keyCode==KeyCode.Escape&&open)
        {open=false;consumeEvent=true;return true;}
        if(type==EventType.MouseDown&&button==1)
        {
            if(open&&OverMenu(mouse)){consumeEvent=true;return true;}
            bool wasOpen=open;
            if(wasOpen)open=false;
            if(wasOpen&&!ownHero.Contains(mouse)){consumeEvent=true;return true;}
            if(ownHero.Contains(mouse)&&!String.IsNullOrEmpty(matchId))
            {open=true;Layout(ownHero);consumeEvent=true;return true;}
            return false;
        }
        if(!open)return false;
        if(type==EventType.MouseDown||type==EventType.MouseUp)
        {
            if(OverMenu(mouse))return true;
            open=false;consumeEvent=true;return true;
        }
        return true;
    }

    bool OverMenu(Vector2 point)
    {
        var calibration=UICalibration.Current;
        for(int i=0;i<hitRects.Length;i++)
        {
            var local=point;
            if(calibration!=null)
            {
                var matrix=calibration.Get("battle.emotes/"+Options[i]).Matrix(imageRects[i],Vector2.one);
                local=matrix.inverse.MultiplyPoint3x4(point);
            }
            if(hitRects[i].Contains(local))return true;
        }
        return false;
    }

    Rect CalibratedRect(int index,Rect rect)
    {
        var calibration=UICalibration.Current;
        if(calibration==null)return rect;
        var matrix=calibration.Get("battle.emotes/"+Options[index]).Matrix(imageRects[index],Vector2.one);
        var points=new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),
            new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)}.Select(point=>(Vector2)matrix.MultiplyPoint3x4(point));
        return Rect.MinMaxRect(points.Min(point=>point.x),points.Min(point=>point.y),points.Max(point=>point.x),points.Max(point=>point.y));
    }

    void Layout(Rect hero)
    {
        float leftAnchorX=hero.xMin-26,rightAnchorX=hero.xMax+26;
        float leftTailX=420f/512f,rightTailX=121f/512f;
        float leftTailY=209f/256f,rightTailY=199f/256f;
        float[] rowAnchors={hero.yMin+5,hero.center.y,hero.yMax-5};
        float[] rightAnchors={rowAnchors[0],rowAnchors[1],rowAnchors[2],rowAnchors[2]+BubbleHeight};
        float top=Single.PositiveInfinity,bottom=Single.NegativeInfinity;
        for(int i=0;i<Options.Length;i++)
        {
            bool left=i<3;
            float anchorX=left?leftAnchorX:rightAnchorX;
            float anchorY=left?rowAnchors[i]:rightAnchors[i-3];
            float tipX=left?leftTailX:rightTailX;
            float tipY=left?leftTailY:rightTailY;
            var image=new Rect(anchorX-tipX*BubbleWidth,anchorY-tipY*BubbleHeight,BubbleWidth,BubbleHeight);
            imageRects[i]=image;
            if(left)
            {
                var text=new Rect(image.x+90f/512f*BubbleWidth,image.y+40f/256f*BubbleHeight,
                    340f/512f*BubbleWidth,98f/256f*BubbleHeight);
                text.xMax=Mathf.Min(text.xMax,hero.xMin-10);
                hitRects[i]=Inset(text,5);
            }
            else
            {
                var text=new Rect(image.x+88f/512f*BubbleWidth,image.y+26f/256f*BubbleHeight,
                    336f/512f*BubbleWidth,80f/256f*BubbleHeight);
                text.xMin=Mathf.Max(text.xMin,hero.xMax+10);
                hitRects[i]=Inset(text,5);
            }
            top=Mathf.Min(top,image.y);bottom=Mathf.Max(bottom,image.yMax);
        }
        float shift=top<8?8-top:bottom>BattleLayout.Height-8?BattleLayout.Height-8-bottom:0;
        if(shift!=0)for(int i=0;i<Options.Length;i++){imageRects[i].y+=shift;hitRects[i].y+=shift;}
    }

    static Rect Inset(Rect rect,float amount)
    {rect.xMin+=amount;rect.xMax-=amount;rect.yMin+=amount;rect.yMax-=amount;return rect;}

    public void Draw(Func<string,string> label,Action<string> send,HeroSpeech speech,GUIStyle buttonStyle=null)
    {
        if(!open)return;
        var fallbackStyle=buttonStyle??GUI.skin.button;
        for(int i=0;i<Options.Length;i++)
        {
            string id=Options[i],text=label(id);
            if(speech!=null)
                speech.DrawEmoteMenuBubble(imageRects[i],hitRects[i],regions[i],text,()=>ReplaySelect(id,send),"battle.emotes/"+id,true);
            else
            {
                bool clicked=GUI.Button(hitRects[i],GUIContent.none,GUIStyle.none);
                GUI.Box(imageRects[i],"",GUI.skin.box);
                GUI.Label(hitRects[i],text,fallbackStyle);
                if(clicked)ReplaySelect(id,send);
            }
        }
    }

    public void DrawPreview(Rect hero,HeroSpeech speech,Func<string,string> label)
    {
        if(speech==null)return;
        Layout(hero);
        for(int i=0;i<Options.Length;i++)
        {
            string id=Options[i];
            speech.DrawEmoteMenuBubble(imageRects[i],hitRects[i],regions[i],label(id),null,"battle.emotes/"+id,false);
        }
    }

    public static HeroEmoteMessage Command(string currentMatch,string id,string requestId)
    {
        if(Array.IndexOf(Options,id)<0)throw new ArgumentException("Unapproved hero emote",nameof(id));
        return new HeroEmoteMessage{match_id=currentMatch,emote_id=id,request_id=requestId};
    }
}
