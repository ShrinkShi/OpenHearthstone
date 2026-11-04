using System;
using System.Linq;
using UnityEngine;

public sealed partial class HeroSpeech
{
    GUIStyle menuTextStyle;

    public void DrawEmoteMenuBubble(Rect rect,Rect hitRect,string regionId,string text,Action selected)
    {DrawEmoteMenuBubble(rect,hitRect,regionId,text,selected,"",true);}

    public void DrawEmoteMenuBubble(Rect rect,Rect hitRect,string regionId,string text,Action selected,string targetId,bool interactive)
    {
        bool clicked=false;
        Action draw=()=>
        {
            if(interactive)clicked=GUI.Button(hitRect,GUIContent.none,GUIStyle.none);
            if(bubbleTexture==null)bubbleTexture=assets.Texture("UI_EMOTE_BUBBLES");
            var region=(bubbleArt==null?new HeroSpeechRegion[0]:bubbleArt.regions??new HeroSpeechRegion[0]).FirstOrDefault(item=>item!=null&&item.id==regionId);
            EnsureMenuTextStyle();
            if(ValidRegion(region)&&bubbleTexture!=null)DrawMenuBubbleArt(rect,region,text,30,menuTextStyle,String.IsNullOrEmpty(targetId)?"":targetId+"/text");
            else
            {
                GUI.Box(rect,"",GUI.skin.box);
                DrawMenuText(hitRect,text,menuTextStyle,String.IsNullOrEmpty(targetId)?"":targetId+"/text");
            }
        };
        var calibration=UICalibration.Current;
        if(calibration!=null&&!String.IsNullOrEmpty(targetId))calibration.Draw(targetId,"右键表情 · "+Label(text),rect,Vector2.one,draw);
        else draw();
        if(clicked&&selected!=null)selected();
    }

    void EnsureMenuTextStyle()
    {
        if(menuTextStyle!=null)return;
        menuTextStyle=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,wordWrap=false,fontSize=30};
        menuTextStyle.normal.textColor=new Color(.20f,.14f,.09f);
    }

    void DrawMenuBubbleArt(Rect rect,HeroSpeechRegion region,string text,int fontSize,GUIStyle style,string textId)
    {
        float sourceWidth=region.source_rect_px[2],sourceHeight=region.source_rect_px[3];
        if(sourceWidth<=0||sourceHeight<=0)return;
        var uv=new Rect(region.uv[0],region.uv[1],region.uv[2],region.uv[3]);
        var oldColor=GUI.color;GUI.color=Color.white;
        GUI.DrawTextureWithTexCoords(rect,bubbleTexture,uv,true);
        float bodyX=region.text_rect_px[0]-region.source_rect_px[0];
        float bodyY=region.text_rect_px[1]-region.source_rect_px[1];
        var textRect=new Rect(rect.x+bodyX/sourceWidth*rect.width,rect.y+bodyY/sourceHeight*rect.height,
            region.text_rect_px[2]/sourceWidth*rect.width,region.text_rect_px[3]/sourceHeight*rect.height);
        style.fontSize=fontSize;
        DrawMenuText(textRect,text,style,textId);GUI.color=oldColor;
    }

    void DrawMenuText(Rect rect,string text,GUIStyle style,string textId)
    {
        Action draw=()=>UIText.DrawLabel(rect,text,style,style.normal.textColor,textId);
        var calibration=UICalibration.Current;
        if(calibration!=null&&!String.IsNullOrEmpty(textId))calibration.Draw(textId,"表情文字",rect,Vector2.one,draw);else GUI.Label(rect,text,style);
    }
}
