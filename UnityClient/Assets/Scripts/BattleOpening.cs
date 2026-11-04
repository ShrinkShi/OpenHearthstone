using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Reconstruct the client-only versus card layout around the verified original announcer clips.
public sealed class BattleOpening
{
    readonly AssetRegistry assets;
    readonly CardVisuals cardVisuals;
    readonly AudioSource output;
    readonly Dictionary<string,CardDef> cards;
    readonly ClassDef[] classes;
    readonly Dictionary<string,VoiceEntry> voices;
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly Dictionary<string,Texture> portraits=new Dictionary<string,Texture>();
    readonly Dictionary<int,Rect> bubbleLocations=new Dictionary<int,Rect>();
    Texture2D versusTexture;
    GUIStyle heroStyle,playerStyle,versusStyle;
    StateView openingState;
    Action onComplete;
    string matchId="";
    string phase="idle";
    int generation;
    float age,fadeStart=-1;

    public bool Active{get;private set;}
    public bool Complete{get;private set;}
    public bool VersusDrawn{get;private set;}
    public float Age{get{return age;}}
    public bool AnnouncerPlaying{get{return output!=null&&output.isPlaying;}}
    public bool StableVersusVisible{get{return Active&&VersusDrawn&&age>=.3f&&AnnouncerPlaying;}}
    public string Phase{get{return phase;}}
    public int AnnouncerPlayCount{get;private set;}
    public IDictionary<int,Rect> BubbleLocations{get{return bubbleLocations;}}
    public event Action<string,float> PhaseChanged;
    public event Action<string,string,string,float,float> VoiceStarted;

    public BattleOpening(AssetRegistry registry,CardVisuals visuals,AudioSource voiceOutput,
        Dictionary<string,CardDef> cardDatabase,ClassDef[] classDatabase,Dictionary<string,VoiceEntry> voiceIndex)
    {
        assets=registry;cardVisuals=visuals;output=voiceOutput;cards=cardDatabase;
        classes=classDatabase??new ClassDef[0];voices=voiceIndex??new Dictionary<string,VoiceEntry>();
        Complete=true;
    }

    public static Rect HeroPortraitRect(bool own)
    {return own?new Rect(350,500,450,500):new Rect(1225,30,450,500);}

    public static Rect HeroInfoRect(bool own)
    {return own?new Rect(350,1000,450,50):new Rect(1225,530,450,50);}

    public static Rect VersusRect{get{return new Rect(810,435,340,280);}}

    public static Rect[] LayoutRects
    {get{return new[]{HeroPortraitRect(true),HeroPortraitRect(false),HeroInfoRect(true),HeroInfoRect(false),VersusRect};}}

    public static bool LayoutFits(float width,float height)
    {
        var viewport=new DesignViewport(width,height,1920,1080,1520);
        foreach(var rect in LayoutRects)
        {
            var matrix=viewport.Matrix;
            var a=matrix.MultiplyPoint3x4(new Vector3(rect.xMin,rect.yMin,0));
            var b=matrix.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMax,0));
            if(a.x<0||a.y<0||b.x>width||b.y>height)return false;
        }
        return true;
    }

    public void Begin(StateView state,MonoBehaviour runner,IEnumerator heroSpeechOpening,Action completed)
    {
        if(state==null||state.phase!="mulligan"||String.IsNullOrEmpty(state.match_id)||
            state.players==null||state.players.Length<2||matchId==state.match_id)return;
        generation++;matchId=state.match_id;openingState=state;age=0;fadeStart=-1;
        Active=true;Complete=false;VersusDrawn=false;AnnouncerPlayCount=0;onComplete=completed;bubbleLocations.Clear();
        SetPhase("versus_intro");
        foreach(var player in state.players)
            if(player!=null&&player.hero!=null)
                bubbleLocations[player.hero.entity]=HeroPortraitRect(player.seat==state.seat);
        runner.StartCoroutine(PlaySequence(runner,state,generation,heroSpeechOpening));
    }

    public void Tick(float delta)
    {if(Active)age+=Mathf.Max(0,delta);}

    public void EndEarly()
    {
        if(!Active)return;
        generation++;Finish(false);
    }

    public void Clear()
    {
        generation++;Active=false;Complete=false;VersusDrawn=false;matchId="";openingState=null;age=0;fadeStart=-1;
        SetPhase("cleared");
        bubbleLocations.Clear();onComplete=null;
        if(output!=null){output.Stop();output.clip=null;}
        foreach(var clip in clips.Values)if(clip!=null)UnityEngine.Object.Destroy(clip);
        clips.Clear();portraits.Clear();versusTexture=null;
    }

    bool IsCurrent(StateView state,int expected)
    {return Active&&expected==generation&&state!=null&&state.match_id==matchId;}

    IEnumerator PlaySequence(MonoBehaviour runner,StateView state,int expected,IEnumerator heroSpeechOpening)
    {
        yield return new WaitForSecondsRealtime(.22f);
        if(!IsCurrent(state,expected))yield break;
        var friendly=Player(state,state.seat);
        var opponent=Player(state,1-state.seat);
        if(friendly!=null&&friendly.hero!=null)
        {
            SetPhase("announce_before_versus");
            yield return PlayVoice(friendly.hero.card_id,"ANNOUNCER_BEFORE_VERSUS",state,expected);
        }
        yield return new WaitForSecondsRealtime(.12f);
        if(!IsCurrent(state,expected))yield break;
        SetPhase("announce_versus");
        yield return PlayVoice("GLOBAL","VERSUS",state,expected);
        yield return new WaitForSecondsRealtime(.12f);
        if(!IsCurrent(state,expected))yield break;
        if(opponent!=null&&opponent.hero!=null)
        {
            SetPhase("announce_after_versus");
            yield return PlayVoice(opponent.hero.card_id,"ANNOUNCER_AFTER_VERSUS",state,expected);
        }
        if(!IsCurrent(state,expected))yield break;
        SetPhase("versus_complete");
        SetPhase("hero_start");
        if(heroSpeechOpening!=null)runner.StartCoroutine(heroSpeechOpening);
        if(!IsCurrent(state,expected))yield break;
        fadeStart=age;SetPhase("fade_out");
        while(age-fadeStart<.45f&&IsCurrent(state,expected))yield return null;
        if(IsCurrent(state,expected))Finish(true);
    }

    IEnumerator PlayVoice(string cardId,string eventName,StateView state,int expected)
    {
        VoiceEntry entry;
        if(!voices.TryGetValue(cardId+":"+eventName,out entry)||entry==null)yield break;
        string audioId=SelectAudioId(entry);
        if(String.IsNullOrEmpty(audioId))yield break;
        AudioClip clip;
        clips.TryGetValue(audioId,out clip);
        if(clip==null)
        {
            yield return assets.Audio(audioId,loaded=>
            {
                if(loaded==null)return;
                if(IsCurrent(state,expected)){clip=loaded;clips[audioId]=loaded;}
                else UnityEngine.Object.Destroy(loaded);
            });
        }
        if(!IsCurrent(state,expected)||clip==null||output==null)yield break;
        output.Stop();output.clip=clip;output.Play();AnnouncerPlayCount++;
        var voiceStarted=VoiceStarted;if(voiceStarted!=null)voiceStarted(cardId,eventName,audioId,clip.length,Time.realtimeSinceStartup);
        Debug.Log("OPENING VOICE "+cardId+"/"+eventName+" clip="+audioId+" duration="+clip.length.ToString("F3"));
        while(output!=null&&output.isPlaying&&IsCurrent(state,expected))yield return null;
    }

    static string SelectAudioId(VoiceEntry entry)
    {
        var candidates=entry.candidates??new VoiceCandidate[0];
        if(candidates.Length==0)
        {
            if(entry.selection_strategy=="weighted_random_clips_then_m_AudioClip_fallback")return "";
            return String.IsNullOrEmpty(entry.fallback_id)?entry.id:entry.fallback_id;
        }
        if(entry.selection_strategy=="single_direct_m_AudioClip")return candidates[0]==null?"":candidates[0].id;
        if(entry.selection_strategy!="weighted_random_clips_then_m_AudioClip_fallback")return "";
        float total=0;
        foreach(var candidate in candidates)
        {
            if(candidate==null||String.IsNullOrEmpty(candidate.id)||candidate.weight<0||
                Single.IsNaN(candidate.weight)||Single.IsInfinity(candidate.weight))
                return "";
            total+=candidate.weight;
        }
        if(Single.IsNaN(total)||Single.IsInfinity(total))return "";
        float choice=UnityEngine.Random.Range(0f,total),cumulative=0;
        for(int i=0;i<candidates.Length-1;i++)
        {
            cumulative+=candidates[i].weight;
            if(choice<=cumulative)return candidates[i].id;
        }
        return candidates[candidates.Length-1].id;
    }

    static PlayerView Player(StateView state,int seat)
    {return (state.players??new PlayerView[0]).FirstOrDefault(player=>player!=null&&player.seat==seat);}

    void SetPhase(string value)
    {
        phase=value;
        var changed=PhaseChanged;if(changed!=null)changed(value,Time.realtimeSinceStartup);
    }

    void Finish(bool completed)
    {
        Active=false;Complete=completed;openingState=null;SetPhase(completed?"complete":"ended_early");
        if(output!=null){output.Stop();output.clip=null;}
        var callback=onComplete;onComplete=null;if(callback!=null)callback();
    }

    public void Draw(StateView state,Font font)
    {
        if(!Active||state==null||state.match_id!=matchId||openingState==null)return;
        float enter=Tween.Curve(age/.25f);
        float exit=fadeStart<0?1:1-Tween.Curve((age-fadeStart)/.45f);
        float alpha=enter*exit;
        var old=GUI.color;GUI.color=new Color(.005f,.008f,.018f,.72f*alpha);
        GUI.DrawTexture(new Rect(0,0,1920,1080),Texture2D.whiteTexture);GUI.color=old;
        DrawHero(state,state.seat,true,font,alpha);
        DrawHero(state,1-state.seat,false,font,alpha);
        if(versusTexture==null)versusTexture=assets.Texture("UI_VS_LETTERS");
        var versus=VersusRect;
        GUI.color=new Color(1,1,1,alpha);
        if(versusTexture!=null)GUI.DrawTexture(versus,versusTexture,ScaleMode.ScaleToFit,true);
        else
        {
            EnsureStyles(font);versusStyle.fontSize=104;
            GUI.Label(versus,"VS",versusStyle);
        }
        if(Event.current.type==EventType.Repaint)VersusDrawn=true;
        GUI.color=old;
    }

    void DrawHero(StateView state,int seat,bool own,Font font,float alpha)
    {
        var player=Player(state,seat);if(player==null||player.hero==null)return;
        Texture portrait;
        if(!portraits.TryGetValue(player.hero.card_id??"",out portrait))
        {
            CardDef card;
            if(cards.TryGetValue(player.hero.card_id??"",out card))
            {
                try{portrait=cardVisuals.Render(card,"hero");}catch(Exception e){Debug.LogWarning("Opening hero portrait: "+e.Message);}
            }
            if(portrait==null)portrait=assets.Texture("CARD_"+player.hero.card_id);
            portraits[player.hero.card_id??""]=portrait;
        }
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        if(portrait!=null)GUI.DrawTexture(HeroPortraitRect(own),portrait,ScaleMode.ScaleToFit,true);
        var info=HeroInfoRect(own);EnsureStyles(font);
        GUI.color=new Color(0,0,0,.78f*alpha);
        GUI.Label(new Rect(info.x+3,info.y+3,info.width,info.height),HeroName(player.hero.card_id),heroStyle);
        GUI.color=new Color(1,1,1,alpha);
        GUI.Label(info,HeroName(player.hero.card_id),heroStyle);
        GUI.color=new Color(.88f,.9f,.92f,alpha);
        GUI.Label(new Rect(info.x,info.y+50,info.width,30),ClassAndPlayer(player),playerStyle);
        GUI.color=old;
    }

    string HeroName(string cardId)
    {
        CardDef card;return cards.TryGetValue(cardId??"",out card)&&!String.IsNullOrEmpty(card.Name)?card.Name:cardId??"";
    }

    string ClassAndPlayer(PlayerView player)
    {
        var classDef=classes.FirstOrDefault(item=>item!=null&&item.hero==player.hero.card_id);
        return classDef==null?"":classDef.name;
    }

    void EnsureStyles(Font font)
    {
        if(heroStyle==null||heroStyle.font!=font)
        {
            heroStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=40,fontStyle=FontStyle.Bold,
                alignment=TextAnchor.MiddleCenter,wordWrap=false};
            heroStyle.normal.textColor=Color.white;
            playerStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=25,fontStyle=FontStyle.Bold,
                alignment=TextAnchor.MiddleCenter,wordWrap=false};
            playerStyle.normal.textColor=new Color(.88f,.9f,.92f);
            versusStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=104,fontStyle=FontStyle.Bold,
                alignment=TextAnchor.MiddleCenter,wordWrap=false};
            versusStyle.normal.textColor=new Color(1,.83f,.42f);
        }
    }
}
