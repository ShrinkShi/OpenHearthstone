using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable] public sealed class UITextStyle
{
    public bool fillOverride,outline=true,shadow=true;
    public string fill="#FFFFFFFF",stroke="#000000FF",shade="#000000B0";
    public float outlineWidth=1,shadowX=2,shadowY=2;
    public int size,fontMode;
    public UITextStyle Copy(){return (UITextStyle)MemberwiseClone();}
    public void Validate()
    {
        Color color;
        if(!ColorUtility.TryParseHtmlString(fill,out color)||!ColorUtility.TryParseHtmlString(stroke,out color)||!ColorUtility.TryParseHtmlString(shade,out color)
            ||float.IsNaN(outlineWidth)||float.IsNaN(shadowX)||float.IsNaN(shadowY)||float.IsInfinity(outlineWidth)||float.IsInfinity(shadowX)||float.IsInfinity(shadowY)
            ||outlineWidth<0||outlineWidth>6||Mathf.Abs(shadowX)>16||Mathf.Abs(shadowY)>16||size<0||size>120||fontMode<0||fontMode>4)
            throw new InvalidDataException("字体样式无效：颜色使用 #RRGGBB 或 #RRGGBBAA，字号 0~120，描边 0~6。");
    }
}
public static class UIText
{
    public static bool HubPreviewRuleText;
    static readonly Dictionary<string,GUIStyle> styles=new Dictionary<string,GUIStyle>();
    static readonly Vector2[] offsets={Vector2.left,Vector2.right,Vector2.up,Vector2.down,new Vector2(-.707f,-.707f),new Vector2(.707f,-.707f),new Vector2(-.707f,.707f),new Vector2(.707f,.707f)};
    public static string Rich(string text)
    {
        return Regex.Replace((text??"").Replace("[x]",""),"<[^>]*>",m=>
            Regex.IsMatch(m.Value,"^</?[bi]>$",RegexOptions.IgnoreCase)?m.Value.ToLowerInvariant():Regex.IsMatch(m.Value,"^<br\\s*/?>$",RegexOptions.IgnoreCase)?"\n":"");
    }
    public static Color StatColor(int value,int maximum,bool buffed)
    {return value<maximum?new Color(1,.18f,.12f):buffed?new Color(.18f,1,.18f):Color.white;}
    public static Color AttackColor(int value,int printed,bool buffed=false)
    {return buffed||value>printed?new Color(.18f,1,.18f):Color.white;}
    static string Description(string text,bool stripUnknownPlaceholders)
    {
        // CardDefs 已标出关键词。去掉正文的装饰标签，仅保留关键词加粗，不把整句变粗。
        string clean=Regex.Replace(Rich(text),"</?[bi]>","",RegexOptions.IgnoreCase);
        if(stripUnknownPlaceholders)clean=Regex.Replace(clean,@"[$#](\d+)","$1");
        const string words="战吼|亡语|嘲讽|冲锋|突袭|圣盾|风怒|潜行|冻结|沉默|法术伤害|奥秘|过载|抉择|连击|激怒|发现|吸血|剧毒|复生|休眠|流放|英雄技能|Battlecry|Deathrattle|Taunt|Charge|Rush|Divine Shield|Windfury|Stealth|Spell Damage|Secret|Overload|Choose One|Combo|Enrage|Discover|Lifesteal|Poisonous|Reborn|Dormant|Outcast";
        return Regex.Replace(clean,"("+words+")",m=>"<b>"+m.Value+"</b>");
    }
    public static string CardDescription(string text){return Description(text,true);}
    public static string PowerName(CardDef card){return card==null?"":card.Name??"";}
    public static int PowerCost(EntityView entity){return entity==null?0:entity.cost;}
    public static string PowerDescription(CardDef card,EntityView entity)
    {
        if(card==null)return "";
        string text=HeroPowerText.Resolve(card.CardId,card.Text,
            entity==null?0:entity.display_damage,entity!=null&&entity.display_damage_known,
            entity==null?0:entity.display_heal,entity!=null&&entity.display_heal_known);
        return Description(text,false);
    }
    public static void Draw(Rect rect,string text,GUIStyle source,Color fill,string id=null,bool numbers=false)
    {
        DrawCore(rect,text,source,fill,id,true,numbers);
    }
    public static void DrawLabel(Rect rect,string text,GUIStyle source,Color fill,string id=null)
    {
        DrawCore(rect,text,source,fill,id,false,false);
    }
    static void DrawCore(Rect rect,string text,GUIStyle source,Color fill,string id,bool defaultShadow,bool defaultOutline)
    {
        var calibration=UICalibration.Current;var edit=calibration==null?null:calibration.TextStyle(id??calibration.DrawingId)??calibration.DrawingTextStyle;
        var units=calibration==null?Vector2.one:calibration.DrawingTextUnits;
        int size=edit!=null&&edit.size>0?Mathf.RoundToInt(edit.size*Mathf.Sqrt(units.x*units.y)):source.fontSize;
        var fontStyle=edit==null||edit.fontMode==0?source.fontStyle:(FontStyle)(edit.fontMode-1);
        string key=source.GetHashCode()+":"+size+":"+(int)fontStyle+":"+source.wordWrap+":"+(int)source.alignment;
        GUIStyle style;
        if(!styles.TryGetValue(key,out style))
        {
            if(styles.Count>256)styles.Clear();style=new GUIStyle(source){fontSize=size,fontStyle=fontStyle,richText=true};style.normal.textColor=Color.white;styles.Add(key,style);
        }
        var old=GUI.color;Color color;
        string styleId=id??(calibration==null?null:calibration.DrawingId);
        bool previewRuleText=HubPreviewRuleText&&styleId!=null&&styleId.StartsWith("card.",StringComparison.Ordinal)&&styleId.EndsWith("/text",StringComparison.Ordinal);
        if(!previewRuleText&&edit!=null&&edit.fillOverride&&ColorUtility.TryParseHtmlString(edit.fill,out color))fill=color;
        bool shadow=edit==null?defaultShadow:edit.shadow,outline=edit==null?defaultOutline:edit.outline;
        if(previewRuleText)
        {shadow=false;outline=false;}
        if(shadow)
        {
            color=new Color(0,0,0,.7f);if(edit!=null)ColorUtility.TryParseHtmlString(edit.shade,out color);
            GUI.color=old*color;GUI.Label(new Rect(rect.x+(edit==null?2:edit.shadowX),rect.y+(edit==null?2:edit.shadowY),rect.width,rect.height),text,style);
        }
        if(outline)
        {
            color=Color.black;if(edit!=null)ColorUtility.TryParseHtmlString(edit.stroke,out color);GUI.color=old*color;
            float width=edit==null?1.6f:edit.outlineWidth;
            foreach(var offset in offsets)GUI.Label(new Rect(rect.position+offset*width,rect.size),text,style);
        }
        GUI.color=old*fill;GUI.Label(rect,text,style);GUI.color=old;
    }
}
