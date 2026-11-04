using System.Globalization;

// 只处理已确认的基础英雄技能占位符；其余文本保持原样。
public static class HeroPowerText
{
    public static string Resolve(string cardId,string text,int displayDamage,bool displayDamageKnown)
    {return Resolve(cardId,text,displayDamage,displayDamageKnown,0,false);}
    public static string Resolve(string cardId,string text,int displayDamage,bool displayDamageKnown,int displayHeal,bool displayHealKnown)
    {
        string token=null;int value=0;bool dynamicDamage=false,dynamicHeal=false;
        switch(cardId)
        {
            case "HERO_05bp":token="$2";value=2;dynamicDamage=true;break;
            case "HERO_08bp":token="$1";value=1;dynamicDamage=true;break;
            case "HERO_09bp":token="#2";value=2;dynamicHeal=true;break;
            case "HERO_07bp":token="$2";value=2;dynamicDamage=true;break;
        }
        text=text??"";
        if(token==null)return text;
        if(dynamicDamage&&displayDamageKnown)value=displayDamage;
        if(dynamicHeal&&displayHealKnown)value=displayHeal;
        return text.Replace(token,value.ToString(CultureInfo.InvariantCulture));
    }
}
