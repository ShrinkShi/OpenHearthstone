using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class HeroSpeech
{
    sealed class Bubble
    {
        public string text;
        public string emoteId;
        public float until;
    }

    readonly AssetRegistry assets;
    readonly Font font;
    readonly AudioSource[] outputs;
    readonly Dictionary<string,HeroSpeechEntry> entries=new Dictionary<string,HeroSpeechEntry>();
    readonly Dictionary<string,string> labels=new Dictionary<string,string>();
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly Dictionary<int,Bubble> bubbles=new Dictionary<int,Bubble>();
    readonly Dictionary<int,string> renderedBubbleTexts=new Dictionary<int,string>();
    readonly Dictionary<int,string> renderedBubbleEmoteIds=new Dictionary<int,string>();
    readonly HashSet<string> received=new HashSet<string>();
    readonly Queue<string> receivedOrder=new Queue<string>();
    GUIStyle bubbleTextStyle;
    string openingMatch="";
    string activeMatch="";
    int generation,openingGeneration;
    bool openingSequenceStarted;
    HeroSpeechBubbleSource bubbleArt;
    Texture2D bubbleTexture;
    public bool OpeningComplete{get;private set;}
    public bool OpeningStarted{get{return openingSequenceStarted;}}
    public int AudioPlayCount{get;private set;}
    public int OpeningPlayCount{get;private set;}
    public event Action<string,string,string,float,float> OpeningVoiceStarted;

    static readonly string[] EmoteIds={"THANKS","WELL_PLAYED","GREETINGS","WOW","OOPS","THREATEN","SORRY"};

    public HeroSpeech(AssetRegistry registry,AudioSource[] audioOutputs,Font subtitleFont=null)
    {
        assets=registry;font=subtitleFont;outputs=audioOutputs??new AudioSource[0];OpeningComplete=true;
        string path=Path.Combine(assets.Root,"hero-speech-index.json");
        if(!File.Exists(path))return;
        try
        {
            var index=JsonUtility.FromJson<HeroSpeechIndex>(File.ReadAllText(path));
            bubbleArt=index.bubble_art;
            foreach(var entry in index.entries??new HeroSpeechEntry[0])
                if(entry!=null&&!String.IsNullOrEmpty(entry.card_id)&&!String.IsNullOrEmpty(entry.emote_id))
                    entries[Key(entry.card_id,entry.emote_id)]=entry;
            foreach(var label in index.labels??new HeroSpeechLabel[0])
                if(label!=null&&!String.IsNullOrEmpty(label.id)&&!String.IsNullOrEmpty(label.text))labels[label.id]=label.text;
        }
        catch(Exception e){Debug.LogWarning("英雄语音索引不可用："+e.Message);}
    }

    static string Key(string cardId,string emoteId){return (cardId??"")+":"+(emoteId??"");}
    HeroSpeechEntry Find(string cardId,string emoteId)
    {HeroSpeechEntry entry;return entries.TryGetValue(Key(cardId,emoteId),out entry)?entry:null;}

    public string Label(string emoteId)
    {
        string text; if(labels.TryGetValue(emoteId??"",out text))return text;
        switch(emoteId)
        {
            case "THANKS":return "感谢";
            case "WELL_PLAYED":return "称赞";
            case "GREETINGS":return "问候";
            case "WOW":return "惊叹";
            case "OOPS":return "失误";
            case "THREATEN":return "威胁";
            case "SORRY":return "抱歉";
            default:return emoteId??"";
        }
    }

    public void BeginMatch(StateView state,MonoBehaviour runner)
    {BeginMatch(state,runner,false);}

    public void BeginMatch(StateView state,MonoBehaviour runner,bool deferOpening)
    {
        if(state==null||String.IsNullOrEmpty(state.match_id)||openingMatch==state.match_id)return;
        openingMatch=activeMatch=state.match_id;generation++;openingGeneration++;
        OpeningComplete=false;openingSequenceStarted=false;AudioPlayCount=0;OpeningPlayCount=0;received.Clear();receivedOrder.Clear();renderedBubbleTexts.Clear();renderedBubbleEmoteIds.Clear();
        StopVoices();
        bubbles.Clear();
        if(!deferOpening)runner.StartCoroutine(PlayOpeningSequence(state));
    }

    public IEnumerator PlayOpeningSequence(StateView state,bool persistBubbles=false)
    {
        if(state==null||state.phase=="finished"||state.match_id!=activeMatch||openingSequenceStarted)yield break;
        openingSequenceStarted=true;
        yield return PlayOpening(state,activeMatch,generation,openingGeneration,persistBubbles);
    }

    public void ClearBubbles(){bubbles.Clear();renderedBubbleTexts.Clear();renderedBubbleEmoteIds.Clear();}
    public void ClearOpeningBubbles()
    {
        foreach(var pair in bubbles.Where(pair=>pair.Value.emoteId=="START").ToArray())
        {bubbles.Remove(pair.Key);renderedBubbleTexts.Remove(pair.Key);renderedBubbleEmoteIds.Remove(pair.Key);}
    }
    public void Clear()
    {
        generation++;openingGeneration++;openingMatch="";activeMatch="";openingSequenceStarted=false;OpeningComplete=true;bubbles.Clear();renderedBubbleTexts.Clear();renderedBubbleEmoteIds.Clear();StopVoices();
        foreach(var clip in clips.Values)if(clip!=null)UnityEngine.Object.Destroy(clip);
        clips.Clear();
    }

    void StopVoices()
    {foreach(var source in outputs)if(source!=null)source.Stop();}

    bool IsCurrent(string matchId,int expectedGeneration)
    {return expectedGeneration==generation&&matchId==activeMatch;}

    IEnumerator PlayOpening(StateView state,string matchId,int expectedGeneration,int expectedOpening,bool persistBubbles)
    {
        for(int seat=0;seat<2;seat++)
        {
            if(!IsCurrent(matchId,expectedGeneration)||expectedOpening!=openingGeneration){OpeningComplete=true;yield break;}
            var player=(state.players??new PlayerView[0]).FirstOrDefault(p=>p!=null&&p.seat==seat);
            if(player==null&&state.players!=null&&seat<state.players.Length)player=state.players[seat];
            if(player==null||player.hero==null)continue;
            var entry=Find(player.hero.card_id,"START");if(entry==null)continue;
            yield return Speak(entry,seat,0,true,persistBubbles,matchId,expectedGeneration,expectedOpening);
        }
        OpeningComplete=true;
    }

    public void ReceiveConcede(SemanticEvent e,StateView state,MonoBehaviour runner)
    {
        if(e==null||e.type!="Concede"||e.owner<0||e.owner>1||state==null||state.match_id!=activeMatch)return;
        var player=(state.players??new PlayerView[0]).FirstOrDefault(p=>p!=null&&p.seat==e.owner);
        if(player==null||player.hero==null||String.IsNullOrEmpty(e.card_id)||player.hero.card_id!=e.card_id)return;
        string id=state.match_id+":concede:"+(e.event_id??(e.owner+":"+e.source));
        if(!Remember(id))return;
        generation++;openingGeneration++;OpeningComplete=true;StopVoices();
        var entry=Find(e.card_id,"CONCEDE");if(entry!=null)runner.StartCoroutine(Speak(entry,e.owner,0,false,true,state.match_id,generation,openingGeneration));
    }

    public bool ReceiveEmote(StateView message,StateView current,MonoBehaviour runner)
    {
        if(message==null||message.type!="hero_emote_event"||current==null||
            message.match_id!=current.match_id||current.match_id!=activeMatch||message.seat<0||message.seat>1||
            String.IsNullOrEmpty(message.event_id)||String.IsNullOrEmpty(message.hero_card_id)||
            !EmoteIds.Contains(message.emote_id))return false;
        var player=(current.players??new PlayerView[0]).FirstOrDefault(p=>p!=null&&p.seat==message.seat);
        if(player==null||player.hero==null||player.hero.card_id!=message.hero_card_id)return false;
        if(!Remember(current.match_id+":emote:"+message.event_id))return true;
        var entry=Find(message.hero_card_id,message.emote_id);
        if(entry!=null)runner.StartCoroutine(Speak(entry,message.seat,0,false,false,current.match_id,generation,openingGeneration));
        return true;
    }

    bool Remember(string id)
    {
        if(String.IsNullOrEmpty(id)||!received.Add(id))return false;
        receivedOrder.Enqueue(id);
        while(receivedOrder.Count>4096)received.Remove(receivedOrder.Dequeue());
        return true;
    }

    public bool HasBubble(int seat)
    {Bubble bubble;return bubbles.TryGetValue(seat,out bubble)&&Time.unscaledTime<bubble.until;}
    public bool HasRenderedBubble(int seat)
    {
        Bubble bubble;string rendered,renderedEmoteId;
        return bubbles.TryGetValue(seat,out bubble)&&Time.unscaledTime<bubble.until&&
            renderedBubbleTexts.TryGetValue(seat,out rendered)&&rendered==bubble.text&&
            renderedBubbleEmoteIds.TryGetValue(seat,out renderedEmoteId)&&renderedEmoteId==bubble.emoteId;
    }
    public string BubbleText(int seat)
    {Bubble bubble;return bubbles.TryGetValue(seat,out bubble)&&Time.unscaledTime<bubble.until?bubble.text:"";}
    public string BubbleEmoteId(int seat)
    {Bubble bubble;return bubbles.TryGetValue(seat,out bubble)&&Time.unscaledTime<bubble.until?bubble.emoteId:"";}

    IEnumerator Speak(HeroSpeechEntry entry,int seat,float fixedBubbleDuration,bool waitForVoice,bool persistBubble,
        string matchId,int expectedGeneration,int expectedOpening)
    {
        bool openingVoice=waitForVoice;
        if(entry==null||!IsCurrent(matchId,expectedGeneration)||(openingVoice&&expectedOpening!=openingGeneration))yield break;
        float initialBubbleDuration=fixedBubbleDuration>0?fixedBubbleDuration:Mathf.Max(3f,entry.duration+1.1f);
        float expiry=persistBubble?Single.PositiveInfinity:Time.unscaledTime+initialBubbleDuration;
        if(!String.IsNullOrEmpty(entry.text))bubbles[seat]=new Bubble{text=entry.text,emoteId=entry.emote_id,until=expiry};
        string audioId=entry.audio_id;
        if((entry.candidates??new HeroSpeechCandidate[0]).Length>0&&
            entry.candidates[0]!=null&&!String.IsNullOrEmpty(entry.candidates[0].id))audioId=entry.candidates[0].id;
        AudioClip clip;
        clips.TryGetValue(audioId??"",out clip);
        if(clip==null&&!String.IsNullOrEmpty(audioId))
        {
            yield return assets.Audio(audioId,c=>
            {
                clip=c;
                if(c!=null&&IsCurrent(matchId,expectedGeneration)&&(!openingVoice||expectedOpening==openingGeneration))clips[audioId]=c;
            });
        }
        if(!IsCurrent(matchId,expectedGeneration)||(openingVoice&&expectedOpening!=openingGeneration))
        {if(clip!=null&&!clips.Values.Contains(clip))UnityEngine.Object.Destroy(clip);yield break;}
        float duration=clip!=null?clip.length:Mathf.Max(0,entry.duration);
        float bubbleDuration=fixedBubbleDuration>0?fixedBubbleDuration:Mathf.Max(3f,duration+1.1f);
        expiry=persistBubble?Single.PositiveInfinity:Time.unscaledTime+bubbleDuration;
        if(!String.IsNullOrEmpty(entry.text))bubbles[seat]=new Bubble{text=entry.text,emoteId=entry.emote_id,until=expiry};
        if(clip!=null&&seat>=0&&seat<outputs.Length&&outputs[seat]!=null)
        {
            outputs[seat].Stop();outputs[seat].PlayOneShot(clip);AudioPlayCount++;
            if(openingVoice)
            {
                OpeningPlayCount++;
                var started=OpeningVoiceStarted;if(started!=null)started(entry.card_id,entry.emote_id,audioId,duration,Time.realtimeSinceStartup);
            }
            Debug.Log("HERO SPEECH "+entry.card_id+"/"+entry.emote_id+" seat="+seat+" clip="+audioId+" duration="+duration.ToString("F3"));
        }
        if(waitForVoice)yield return new WaitForSecondsRealtime(Mathf.Max(2.2f,duration)+.45f);
    }

    public void Draw(StateView state,int viewSeat,IDictionary<int,Rect> locations)
    {
        if(state==null||state.players==null||locations==null)return;
        if(bubbleTextStyle==null)
        {
            bubbleTextStyle=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=26};
            bubbleTextStyle.normal.textColor=new Color(.20f,.14f,.09f);
        }
        foreach(var pair in bubbles.ToArray())if(Time.unscaledTime>=pair.Value.until)bubbles.Remove(pair.Key);
        foreach(var pair in bubbles)
        {
            if(pair.Key<0||pair.Key>1)continue;
            var player=state.players.FirstOrDefault(p=>p!=null&&p.seat==pair.Key);
            Rect hero;if(player==null||player.hero==null||!locations.TryGetValue(player.hero.entity,out hero))continue;
            bool own=pair.Key==viewSeat;
            string id="battle.speech/"+(own?"own":"enemy");
            if(DrawBubble(id,own?"己方英雄对话泡":"敌方英雄对话泡",hero,pair.Value.text,own)&&Event.current.type==EventType.Repaint)
            {renderedBubbleTexts[pair.Key]=pair.Value.text;renderedBubbleEmoteIds[pair.Key]=pair.Value.emoteId;}
        }
    }

    public bool DrawPreviewBubble(string targetId,string label,string cardId,string emoteId,Rect hero,bool own)
    {
        var entry=Find(cardId,emoteId);
        if(entry==null||String.IsNullOrEmpty(entry.text))return false;
        return DrawBubble(targetId,label,hero,entry.text,own);
    }

    bool DrawBubble(string targetId,string label,Rect hero,string text,bool own)
    {
        if(String.IsNullOrEmpty(text)||hero.width<=0||hero.height<=0)return false;
        if(bubbleTextStyle==null)
        {
            bubbleTextStyle=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=26};
            bubbleTextStyle.normal.textColor=new Color(.20f,.14f,.09f);
        }
        if(bubbleTexture==null)bubbleTexture=assets.Texture("UI_EMOTE_BUBBLES");
        if(bubbleTexture==null)return false;
        string regionId=own?"upper_right_tail":"lower_left_tail";
        var region=(bubbleArt==null?new HeroSpeechRegion[0]:bubbleArt.regions??new HeroSpeechRegion[0]).FirstOrDefault(item=>item!=null&&item.id==regionId);
        if(!ValidRegion(region)||region.tail_tip_px==null||region.tail_tip_px.Length<2)return false;
        float width=560,height=280,sourceWidth=region.source_rect_px[2],sourceHeight=region.source_rect_px[3];
        float tipX=region.tail_tip_px[0]-region.source_rect_px[0];
        float tipY=region.tail_tip_px[1]-region.source_rect_px[1];
        if(region.flip_y)tipY=sourceHeight-tipY;
        var anchor=own?new Vector2(hero.center.x,hero.yMin):new Vector2(hero.center.x,hero.yMax);
        var rect=new Rect(anchor.x-tipX/sourceWidth*width,anchor.y-tipY/sourceHeight*height,width,height);
        Action draw=()=>DrawBubbleArt(rect,region,text,region.flip_y?22:26,bubbleTextStyle,targetId+"/text");
        var calibration=UICalibration.Current;
        if(calibration!=null)calibration.Draw(targetId,label,rect,Vector2.one,draw);
        else draw();
        return true;
    }

    static bool ValidRegion(HeroSpeechRegion region)
    {
        return region!=null&&region.source_rect_px!=null&&region.source_rect_px.Length>=4&&region.source_rect_px[2]>0&&region.source_rect_px[3]>0&&
            region.text_rect_px!=null&&region.text_rect_px.Length>=4&&region.uv!=null&&region.uv.Length>=4;
    }

    void DrawBubbleArt(Rect rect,HeroSpeechRegion region,string text,int fontSize,GUIStyle style,string textId)
    {
        float sourceWidth=region.source_rect_px[2],sourceHeight=region.source_rect_px[3];
        if(sourceWidth<=0||sourceHeight<=0)return;
        var uv=new Rect(region.uv[0],region.uv[1],region.uv[2],region.uv[3]);
        var oldColor=GUI.color;GUI.color=Color.white;
        var oldMatrix=GUI.matrix;
        if(region.flip_y)GUIUtility.ScaleAroundPivot(new Vector2(1,-1),rect.center);
        GUI.DrawTextureWithTexCoords(rect,bubbleTexture,uv,true);
        GUI.matrix=oldMatrix;
        float bodyX=region.text_rect_px[0]-region.source_rect_px[0];
        float bodyY=region.text_rect_px[1]-region.source_rect_px[1];
        float bodyWidth=region.text_rect_px[2],bodyHeight=region.text_rect_px[3];
        if(region.flip_y)bodyY=sourceHeight-bodyY-bodyHeight;
        var textRect=new Rect(rect.x+bodyX/sourceWidth*rect.width,rect.y+bodyY/sourceHeight*rect.height,
            bodyWidth/sourceWidth*rect.width,bodyHeight/sourceHeight*rect.height);
        style.fontSize=fontSize;
        var calibration=UICalibration.Current;
        Action drawText=()=>UIText.DrawLabel(textRect,text,style,style.normal.textColor,textId);
        if(calibration!=null)calibration.Draw(textId,"气泡文字",textRect,Vector2.one,drawText);else GUI.Label(textRect,text,style);
        GUI.color=oldColor;
    }
}
