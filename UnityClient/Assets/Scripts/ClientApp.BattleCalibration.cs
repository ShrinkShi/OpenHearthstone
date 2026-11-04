using UnityEngine;

public sealed partial class ClientApp
{
    Rect BattleCalibrationHero(bool own)
    {
        if(editorState!=null&&editorState.players!=null)
        {
            int index=own?0:1;
            if(index<editorState.players.Length&&editorState.players[index]!=null&&editorState.players[index].hero!=null)
            {
                Rect rect;
                if(locations.TryGetValue(editorState.players[index].hero.entity,out rect))
                {
                    var battleToPreview=GUI.matrix.inverse*BattleLayout.Matrix;
                    var a=(Vector2)battleToPreview.MultiplyPoint3x4(new Vector2(rect.xMin,rect.yMin));
                    var b=(Vector2)battleToPreview.MultiplyPoint3x4(new Vector2(rect.xMax,rect.yMin));
                    var c=(Vector2)battleToPreview.MultiplyPoint3x4(new Vector2(rect.xMax,rect.yMax));
                    var d=(Vector2)battleToPreview.MultiplyPoint3x4(new Vector2(rect.xMin,rect.yMax));
                    return Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.y,b.y,c.y,d.y),
                        Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.y,b.y,c.y,d.y));
                }
            }
        }
        return BattleLayout.Hero(own);
    }

    void DrawBattleCalibrationPreview(string view,string cardId,string emoteId)
    {
        if(heroSpeech==null)return;
        if(view=="battle.speech")
        {
            heroSpeech.DrawPreviewBubble("battle.speech/own","己方英雄对话泡",cardId,emoteId,
                BattleCalibrationHero(true),true);
            heroSpeech.DrawPreviewBubble("battle.speech/enemy","敌方英雄对话泡",cardId,emoteId,
                BattleCalibrationHero(false),false);
        }
        else if(view=="battle.emotes")
            heroEmoteMenu.DrawPreview(BattleCalibrationHero(true),heroSpeech,id=>heroSpeech.Label(id));
    }
}
