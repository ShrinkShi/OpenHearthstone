using System;
using System.Collections.Generic;

public static class AudioEventRegistry
{
    public const string ArmorHit = "SFX_ARMOR_HIT";
    public static readonly Dictionary<string,string> Assets=new Dictionary<string,string>
    {
        {"Draw","SFX_CARD_DRAW"},{"Give","SFX_CARD_DRAW"},{"CardPlay","SFX_CARD_PLAY"},
        {"Summon","SFX_SUMMON"},{"Attack","SFX_ATTACK_LAUNCH"},
        {"Hit","SFX_ATTACK_IMPACT"},{"TURN_START","SFX_TURN_START"},{"Morph","SFX_TRANSFORM"},
        {"PlayHeroPower","SFX_SUMMON"},{"ConfirmMulligan","SFX_MULLIGAN_CONFIRM"},
        {"EndTurn","SFX_END_TURN"},{"Hover","SFX_CARD_HOVER"},{"Button","SFX_UI_CLICK"},
        {"Pickup","SFX_CARD_PICKUP"},{"MulliganSelect","SFX_MULLIGAN_SELECT"},
        {"Death","SFX_MINION_DEATH"},{"Victory","SFX_VICTORY"},{"Defeat","SFX_DEFEAT"},
        {"HeroShatter","SFX_HERO_SHATTER"},{"ResultStart","SFX_RESULT_START"}
    };
    public static string VoiceEvent(string semantic)
    {
        return semantic=="CardPlay"||semantic=="Summon"?"PLAY":semantic=="Attack"?"ATTACK":semantic=="Death"?"DEATH":"";
    }
    public static bool ShouldPlayMinionSourceVoice(string semantic,int cardType,bool matchedSummon,
        bool voiceAvailable,bool suppressPlayOnSummon)
    {
        if(cardType!=4)return false;
        if(String.Equals(semantic,"CardPlay",StringComparison.OrdinalIgnoreCase))return false;
        return String.Equals(semantic,"Summon",StringComparison.OrdinalIgnoreCase)&&
            matchedSummon&&voiceAvailable&&!suppressPlayOnSummon;
    }
    public static bool SuppressDefaultAudio(string cardId,string semantic,bool nativeRouteAvailable)
    {
        return nativeRouteAvailable&&cardId=="HERO_07bp"&&(semantic=="PlayHeroPower"||semantic=="Hit");
    }
}
