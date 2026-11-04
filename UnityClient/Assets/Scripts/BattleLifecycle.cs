using UnityEngine;

// 仅追踪表现时间；最终胜负仍来自服务器。新比赛和返回菜单必须清空时钟。
public sealed class BattleLifecycle
{
    public System.Action<string> AudioCue;
    public const float EntranceDuration=1.25f,DeathDuration=1.65f;
    string match;
    float entrance=EntranceDuration,ending=-1;
    public bool Entering{get{return entrance<EntranceDuration;}}
    public bool Ending{get{return ending>=0;}}
    public bool ResultReady{get{return ending>=DeathDuration;}}
    public float DeathProgress{get{return Mathf.Clamp01(ending/DeathDuration);}}
    public float ResultProgress{get{return Tween.Curve((ending-DeathDuration)/.9f,Ease.Overshoot);}}
    public float ResultAlpha{get{return Tween.Curve((ending-DeathDuration)/.5f);}}
    public void Reset(){match=null;entrance=EntranceDuration;ending=-1;}
    public void Observe(StateView state)
    {
        if(state==null||match==state.match_id)return;
        match=state.match_id;entrance=state.phase=="mulligan"?0:EntranceDuration;ending=-1;
    }
    public void Tick(StateView shown,bool queueBusy,float delta)
    {
        float dt=Mathf.Max(0,delta);entrance=Mathf.Min(EntranceDuration,entrance+dt);
        if(shown!=null&&shown.phase=="finished"&&!queueBusy)
        {
            if(ending<0){ending=0;if(AudioCue!=null)AudioCue("SFX_HERO_SHATTER");}
            else
            {bool ready=ResultReady;ending+=dt;if(!ready&&ResultReady&&AudioCue!=null)AudioCue("SFX_RESULT_START");}
        }
    }
    public void Skip(){entrance=EntranceDuration;if(ending>=0)ending=DeathDuration+1;}
    public Rect HeroPose(Rect rect,bool own)
    {
        float t=Tween.Curve((entrance-(own?.18f:0))/.9f,Ease.Overshoot);
        var center=Vector2.LerpUnclamped(rect.center+new Vector2(own?-200:200,own?280:-280),rect.center,t);
        rect.size*=Mathf.LerpUnclamped(.7f,1,t);rect.center=center;return rect;
    }
    public bool DeadHero(PlayerView player)
    {return Ending&&(player.result=="LOST"||player.hero.health<=0);}
    public Rect ResultHero(Rect rect)
    {var center=rect.center+Vector2.up*(90*(1-ResultProgress));rect.size*=Mathf.LerpUnclamped(.7f,1,ResultProgress);rect.center=center;return rect;}
}
