using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class HeroSpeechListenerAudioRecord
{
    public string path,format,error;
    public int rate,channels,callbacks,overflows;
    public long sample_frames,clipped_samples;
    public float peak,rms;
}

[Serializable] public sealed class HeroSpeechReplayRecord
{
    public string match_id, hero_card_id, concede_card_id, error,last_own_emote_id,emote_bubble_id,emote_bubble_text;
    public int seat, voice_play_count, menu_selection_count, concede_owner=-1;
    public int opening_play_count;
    public int mulligan_input_repaint_count, replay_mulligan_keep_accepted_version=-1;
    public int replay_harness_version=2;
    public bool server_supported, opening_complete, menu_opened, concede_sent, concede_received, concede_bubble_rendered, result_seen,emote_bubble_captured;
    public bool opening_evidence_enabled, versus_drawn, mulligan_visible, mulligan_interactable;
    public bool start_bubble_visible_during_mulligan, mulligan_interactable_before_start_complete;
    public bool versus_announcer_playing,menu_capture_after_start,menu_capture_after_start_bubble_expired;
    public bool attack_replay_enabled, attack_replay_complete;
    public bool replay_mulligan_keep_submitted, replay_mulligan_keep_accepted;
    public int attack_mulligan_count, attack_move_count, duplicate_attack_callbacks, attack_voice_start_count;
    public float battle_opening_started_at=-1,versus_complete_at=-1,battle_opening_completed_at=-1;
    public float hero_start_started_at=-1,hero_start_completed_at=-1,versus_drawn_at=-1,versus_drawn_age=-1;
    public float mulligan_visible_at=-1,mulligan_interactable_at=-1;
    public float replay_mulligan_keep_submitted_at=-1,replay_mulligan_keep_accepted_at=-1;
    public string menu_capture_phase="";
    public string replay_mulligan_keep_accepted_phase="";
    public string[] requested_options=new string[0], sent_options=new string[0], received_emotes=new string[0], rendered_bubbles=new string[0];
    public string[] opening_frame_tags=new string[0];
    public HeroSpeechReplayOpeningPhase[] opening_phases=new HeroSpeechReplayOpeningPhase[0];
    public HeroSpeechReplayOpeningVoice[] opening_voices=new HeroSpeechReplayOpeningVoice[0];
    public HeroSpeechReplayAttack[] hero_attacks=new HeroSpeechReplayAttack[0];
    public HeroSpeechReplayWeaponCue[] weapon_audio=new HeroSpeechReplayWeaponCue[0];
    public HeroSpeechListenerAudioRecord listener_audio;
}

[Serializable] public sealed class HeroSpeechReplayOpeningPhase
{public string phase;public float time;}

[Serializable] public sealed class HeroSpeechReplayOpeningVoice
{public string source,card_id,event_name,asset_id;public float started_at,duration;}

[Serializable] public sealed class HeroSpeechReplayAttack
{public string event_key,card_id,weapon_card_id,weapon_card_id_source,voice_id;public int seat,source,target,accepted_count,voice_start_count;public float accepted_at=-1,voice_started_at=-1,duration;}

[Serializable] public sealed class HeroSpeechReplayWeaponCue
{public string file,source_name;public bool playback_started;public float started_at;}

public sealed class HeroSpeechReplay
{
    readonly string[] options;
    readonly string reportPath;
    readonly bool concede,autoExit,attackReplay;
    readonly string configurationError;
    readonly List<string> sent=new List<string>(),received=new List<string>(),visible=new List<string>();
    readonly List<string> openingFrameTags=new List<string>();
    readonly List<HeroSpeechReplayOpeningPhase> openingPhases=new List<HeroSpeechReplayOpeningPhase>();
    readonly List<HeroSpeechReplayOpeningVoice> openingVoices=new List<HeroSpeechReplayOpeningVoice>();
    readonly List<HeroSpeechReplayAttack> heroAttacks=new List<HeroSpeechReplayAttack>();
    readonly List<HeroSpeechReplayWeaponCue> weaponAudio=new List<HeroSpeechReplayWeaponCue>();
    readonly HashSet<string> receivedIds=new HashSet<string>();
    readonly HashSet<string> acceptedAttackKeys=new HashSet<string>();
    readonly Dictionary<string,string> attackWeaponBefore=new Dictionary<string,string>();
    StateView lastObservedState;
    HeroSpeechReplayRecord record=new HeroSpeechReplayRecord();
    FireblastPcmTap listenerTap;
    BattleOpening attachedOpening;
    HeroSpeech attachedSpeech;
    string currentMatch="";
    int optionIndex;
    float nextActionTime,exitAt;
    bool armed,menuCaptured,bubbleCaptured,resultCaptured,listenerCaptureStopped;
    bool attackMulliganSubmitted,attackSubmitted,attackEndTurnSubmitted,attackReplayComplete;
    bool replayMulliganSubmitted;
    int mulliganInputRepaintFrame=-1,lastMulliganInputRepaintFrame=-1;
    int attackSubmitVersion=-1;

    public bool Enabled{get{return String.IsNullOrEmpty(configurationError)&&(options.Length>0||concede||attackReplay);}}
    public bool AttackReplayEnabled{get{return attackReplay;}}
    public bool ShouldExit
    {
        get
        {
            if(!autoExit||!(attackReplay?attackReplayComplete:resultCaptured)||Time.unscaledTime<exitAt)return false;
            StopListenerCapture();return true;
        }
    }
    public HeroSpeechReplay(string optionList,string outputPath,bool concedeAfterEmotes,bool exitAfterResult,bool attackReplayEnabled=false)
    {
        reportPath=outputPath;concede=concedeAfterEmotes;attackReplay=attackReplayEnabled;autoExit=exitAfterResult||attackReplayEnabled;
        options=(optionList??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(value=>value.Trim()).ToArray();
        if(options.Any(value=>Array.IndexOf(HeroEmoteMenu.Options,value)<0))
        {
            configurationError="Replay contains an unsupported emote ID";options=new string[0];
        }
        if(options.Distinct().Count()!=options.Length)
        {configurationError="Replay emote IDs must be unique";options=new string[0];}
        record.error=configurationError;
        record.requested_options=options;
    }

    public void AttachOpening(BattleOpening opening,HeroSpeech speech)
    {
        if(attachedOpening!=null)
        {
            attachedOpening.PhaseChanged-=ObserveOpeningPhase;
            attachedOpening.VoiceStarted-=ObserveAnnouncerVoice;
        }
        if(attachedSpeech!=null)attachedSpeech.OpeningVoiceStarted-=ObserveHeroStartVoice;
        attachedOpening=opening;attachedSpeech=speech;
        if(attachedOpening!=null)
        {
            attachedOpening.PhaseChanged+=ObserveOpeningPhase;
            attachedOpening.VoiceStarted+=ObserveAnnouncerVoice;
        }
        if(attachedSpeech!=null)attachedSpeech.OpeningVoiceStarted+=ObserveHeroStartVoice;
    }

    public void ObserveOpeningFrame(StateView state,BattleOpening opening,MulliganPresentation mulligan,
        HeroSpeech speech,Action<string> capture)
    {
        if(!Enabled||state==null||state.match_id!=currentMatch)return;
        bool captureEvidence=record.opening_evidence_enabled;
        float now=Time.realtimeSinceStartup;
        bool changed=false;
        if(captureEvidence&&opening!=null&&opening.StableVersusVisible&&!record.versus_drawn)
        {
            record.versus_drawn=true;record.versus_drawn_at=now;
            record.versus_drawn_age=opening.Age;record.versus_announcer_playing=opening.AnnouncerPlaying;
            CaptureOpeningFrame("battle-opening-vs",capture);changed=true;
        }
        bool mulliganVisible=state.phase=="mulligan"&&mulligan!=null&&mulligan.Active&&opening!=null&&!opening.Active;
        if(captureEvidence&&mulliganVisible&&!record.mulligan_visible)
        {
            record.mulligan_visible=true;record.mulligan_visible_at=now;
            record.start_bubble_visible_during_mulligan=speech!=null&&
                Enumerable.Range(0,2).Any(seat=>speech.BubbleEmoteId(seat)=="START"&&speech.HasRenderedBubble(seat));
            CaptureOpeningFrame("battle-opening-mulligan",capture);changed=true;
        }
        bool interactable=mulliganVisible&&mulligan.Interactable&&Event.current!=null&&Event.current.type==EventType.Repaint;
        if(interactable&&Time.frameCount!=lastMulliganInputRepaintFrame)
        {
            lastMulliganInputRepaintFrame=Time.frameCount;
            mulliganInputRepaintFrame=Time.frameCount;
            record.mulligan_input_repaint_count++;
            if(captureEvidence&&!record.mulligan_interactable)
            {
                record.mulligan_interactable=true;record.mulligan_interactable_at=now;
                record.mulligan_interactable_before_start_complete=speech==null||!speech.OpeningComplete;
                CaptureOpeningFrame("battle-opening-mulligan-input",capture);changed=true;
            }
        }
        if(captureEvidence&&speech!=null&&speech.OpeningStarted&&record.hero_start_started_at<0)
        {record.hero_start_started_at=now;changed=true;}
        if(captureEvidence&&speech!=null&&speech.OpeningComplete&&record.hero_start_completed_at<0)
        {record.hero_start_completed_at=now;changed=true;}
        if(changed)Save();
    }

    void ObserveOpeningPhase(string phase,float time)
    {
        if(!record.opening_evidence_enabled)return;
        openingPhases.Add(new HeroSpeechReplayOpeningPhase{phase=phase,time=time});
        record.opening_phases=openingPhases.ToArray();
        if(phase=="versus_intro")record.battle_opening_started_at=time;
        else if(phase=="versus_complete")record.versus_complete_at=time;
        else if(phase=="complete")record.battle_opening_completed_at=time;
        Save();
    }

    void ObserveAnnouncerVoice(string cardId,string eventName,string assetId,float duration,float time)
    {ObserveOpeningVoice("announcer",cardId,eventName,assetId,duration,time);}

    void ObserveHeroStartVoice(string cardId,string eventName,string assetId,float duration,float time)
    {ObserveOpeningVoice("hero_speech",cardId,eventName,assetId,duration,time);}

    void ObserveOpeningVoice(string source,string cardId,string eventName,string assetId,float duration,float time)
    {
        if(!record.opening_evidence_enabled)return;
        openingVoices.Add(new HeroSpeechReplayOpeningVoice{source=source,card_id=cardId,event_name=eventName,
            asset_id=assetId,started_at=time,duration=duration});
        record.opening_voices=openingVoices.ToArray();Save();
    }

    void CaptureOpeningFrame(string tag,Action<string> capture)
    {
        if(capture==null||openingFrameTags.Contains(tag))return;
        openingFrameTags.Add(tag);record.opening_frame_tags=openingFrameTags.ToArray();capture(tag);
    }

    public void ObserveState(StateView state,bool supported)
    {
        if(state==null||String.IsNullOrEmpty(state.match_id))return;
        if(currentMatch!=state.match_id)
        {
            currentMatch=state.match_id;optionIndex=0;armed=false;menuCaptured=bubbleCaptured=resultCaptured=false;
            nextActionTime=Time.unscaledTime+2f;sent.Clear();received.Clear();visible.Clear();receivedIds.Clear();
            openingFrameTags.Clear();openingPhases.Clear();openingVoices.Clear();heroAttacks.Clear();weaponAudio.Clear();acceptedAttackKeys.Clear();
            attackWeaponBefore.Clear();lastObservedState=null;
            attackMulliganSubmitted=attackSubmitted=attackEndTurnSubmitted=attackReplayComplete=false;attackSubmitVersion=-1;
            replayMulliganSubmitted=false;mulliganInputRepaintFrame=lastMulliganInputRepaintFrame=-1;
            var local=(state.players??new PlayerView[0]).FirstOrDefault(player=>player!=null&&player.seat==state.seat);
            record=new HeroSpeechReplayRecord{match_id=state.match_id,seat=state.seat,
                hero_card_id=local==null||local.hero==null?"":local.hero.card_id,
                server_supported=supported,requested_options=options,error=configurationError,
                opening_evidence_enabled=!String.IsNullOrEmpty(reportPath),attack_replay_enabled=attackReplay,replay_harness_version=2};
        }
        if(attackReplay&&lastObservedState!=null&&lastObservedState.match_id==state.match_id&&state.version>lastObservedState.version)
        {
            foreach(var attack in state.events??new SemanticEvent[0])
            {
                if(attack==null||attack.type!="Attack")continue;
                string key=attack.source+":"+attack.target;
                if(attackWeaponBefore.ContainsKey(key))continue;
                string weaponCardId;
                if(TryWeaponCardForAttack(lastObservedState,attack,out weaponCardId))attackWeaponBefore[key]=weaponCardId;
            }
        }
        lastObservedState=state;
        if(replayMulliganSubmitted&&!record.replay_mulligan_keep_accepted&&state.phase=="play")
        {
            record.replay_mulligan_keep_accepted=true;
            record.replay_mulligan_keep_accepted_at=Time.realtimeSinceStartup;
            record.replay_mulligan_keep_accepted_phase=state.phase;
            record.replay_mulligan_keep_accepted_version=state.version;
        }
        record.server_supported=supported;Save();
        EnsureListenerCapture();
    }

    public void TickMulliganReplay(StateView state,bool openingActive,bool inputLocked,bool pending,
        MulliganPresentation mulligan,Func<Move,bool> sendMove)
    {
        if(attackReplay||!Enabled||replayMulliganSubmitted||state==null||state.match_id!=currentMatch||
            state.phase!="mulligan"||openingActive||inputLocked||pending||mulligan==null||!mulligan.Interactable||
            mulliganInputRepaintFrame<0||Time.frameCount<=mulliganInputRepaintFrame||sendMove==null)return;
        var move=(state.moves??new Move[0]).FirstOrDefault(candidate=>candidate.kind=="mulligan");
        if(move!=null&&sendMove(move))
        {
            replayMulliganSubmitted=true;record.replay_mulligan_keep_submitted=true;
            record.replay_mulligan_keep_submitted_at=Time.realtimeSinceStartup;Save();
        }
    }

    public void TickAttackReplay(StateView state,bool openingActive,bool inputLocked,bool pending,
        MulliganPresentation mulligan,Func<Move,bool> sendMove)
    {
        if(!attackReplay||!Enabled||attackReplayComplete||state==null||state.match_id!=currentMatch||
            state.phase=="finished"||openingActive||inputLocked||pending||sendMove==null)return;
        if(state.phase=="mulligan")
        {
            if(!attackMulliganSubmitted&&mulligan!=null&&mulligan.Interactable&&
                mulliganInputRepaintFrame>=0&&Time.frameCount>mulliganInputRepaintFrame)
            {
                var keep=(state.moves??new Move[0]).FirstOrDefault(move=>move.kind=="mulligan");
                if(keep!=null&&sendMove(keep))
                {
                    attackMulliganSubmitted=true;replayMulliganSubmitted=true;
                    record.attack_mulligan_count++;record.replay_mulligan_keep_submitted=true;
                    record.replay_mulligan_keep_submitted_at=Time.realtimeSinceStartup;Save();
                }
            }
            return;
        }
        if(state.phase!="play"||state.seat<0||state.players==null||state.seat>=state.players.Length)return;
        var own=state.players[state.seat];var opponent=state.players.FirstOrDefault(player=>player!=null&&player.seat!=state.seat);
        if(own==null||own.hero==null||opponent==null||opponent.hero==null)return;
        if(!attackSubmitted)
        {
            var move=(state.moves??new Move[0]).FirstOrDefault(candidate=>candidate.kind=="attack"&&
                candidate.entity==own.hero.entity&&candidate.target==opponent.hero.entity);
            if(move!=null&&sendMove(move))
            {attackSubmitted=true;attackSubmitVersion=state.version;record.attack_move_count++;Save();}
            return;
        }
        if(!attackEndTurnSubmitted&&state.version>attackSubmitVersion&&state.active==state.seat&&
            (state.moves??new Move[0]).Any(move=>move.kind=="end_turn")&&sendMove(new Move{kind="end_turn"}))
        {attackEndTurnSubmitted=true;Save();}
    }

    public void ObserveHeroAttackAccepted(SemanticEvent semantic,string cardId,string voiceId,StateView state)
    {
        if(!attackReplay||semantic==null||semantic.type!="Attack"||String.IsNullOrEmpty(cardId)||state==null||state.players==null)return;
        var owner=state.players.FirstOrDefault(player=>player!=null&&player.hero!=null&&player.hero.entity==semantic.source);
        if(owner==null||owner.hero.card_id!=cardId)return;
        string key=semantic.source+":"+semantic.target;
        if(!acceptedAttackKeys.Add(key)){record.duplicate_attack_callbacks++;Save();return;}
        string weaponCardId,weaponCardIdSource;
        if(attackWeaponBefore.TryGetValue(key,out weaponCardId))
        {weaponCardIdSource="before-snapshot";attackWeaponBefore.Remove(key);}
        else
        {weaponCardId=owner.weapon==null?"":owner.weapon.card_id;weaponCardIdSource=owner.weapon==null?"unavailable":"current-snapshot";}
        heroAttacks.Add(new HeroSpeechReplayAttack{event_key=key,card_id=cardId,voice_id=voiceId,
            seat=owner.seat,source=semantic.source,target=semantic.target,weapon_card_id=weaponCardId,
            weapon_card_id_source=weaponCardIdSource,
            accepted_count=1,accepted_at=Time.realtimeSinceStartup});
        record.hero_attacks=heroAttacks.ToArray();Save();
    }

    public static bool TryWeaponCardForAttack(StateView before,SemanticEvent attack,out string cardId)
    {
        cardId="";
        if(before==null||attack==null||attack.type!="Attack"||before.players==null)return false;
        var owner=before.players.FirstOrDefault(player=>player!=null&&player.hero!=null&&player.hero.entity==attack.source);
        if(owner==null)return false;
        cardId=owner.weapon==null?"":owner.weapon.card_id??"";
        return true;
    }

    public void ObserveHeroAttackVoiceStarted(SemanticEvent semantic,string cardId,string voiceId,float duration)
    {
        if(!attackReplay||semantic==null)return;
        string key=semantic.source+":"+semantic.target;
        var attack=heroAttacks.FirstOrDefault(row=>row.event_key==key&&row.card_id==cardId);
        if(attack==null)return;
        attack.voice_start_count++;attack.voice_id=voiceId;attack.voice_started_at=Time.realtimeSinceStartup;attack.duration=duration;
        record.attack_voice_start_count++;record.hero_attacks=heroAttacks.ToArray();
        if(heroAttacks.Count==2&&heroAttacks.All(row=>row.accepted_count==1&&row.voice_start_count==1)&&
            heroAttacks.Select(row=>row.seat).Distinct().Count()==2)
        {
            attackReplayComplete=true;record.attack_replay_complete=true;
            exitAt=Time.unscaledTime+Mathf.Max(.75f,duration+.25f);
        }
        Save();
    }

    public void ObserveWeaponCue(NativeAudioCue cue)
    {
        if(!attackReplay||cue==null)return;
        weaponAudio.Add(new HeroSpeechReplayWeaponCue{file=cue.file,source_name=cue.sourceName,
            playback_started=cue.playbackActive!=null&&cue.playbackActive(),started_at=Time.realtimeSinceStartup});
        record.weapon_audio=weaponAudio.ToArray();Save();
    }

    public void ObserveEmote(StateView message)
    {
        if(message==null||message.match_id!=currentMatch||String.IsNullOrEmpty(message.event_id)||!receivedIds.Add(message.event_id))return;
        received.Add(message.seat+":"+message.hero_card_id+":"+message.emote_id+":"+message.event_id);
        if(message.seat==record.seat)record.last_own_emote_id=message.emote_id;
        record.received_emotes=received.ToArray();Save();
    }

    public void ObserveConcede(SemanticEvent semantic)
    {
        if(semantic==null||semantic.type!="Concede")return;
        record.concede_received=true;record.concede_owner=semantic.owner;record.concede_card_id=semantic.card_id;Save();
    }

    public void Tick(StateView state,bool supported,bool resultReady,Rect ownHero,HeroSpeech speech,HeroEmoteMenu menu,
        Action<string> sendEmote,Action concedeMove,Action<string> capture)
    {
        if(!Enabled||state==null||state.match_id!=currentMatch)return;
        record.server_supported=supported;record.opening_complete=speech==null||speech.OpeningComplete;
        record.voice_play_count=speech==null?0:speech.AudioPlayCount;
        record.opening_play_count=speech==null?0:speech.OpeningPlayCount;
        if(speech!=null)
            for(int seat=0;seat<2;seat++)if(speech.HasBubble(seat)&&speech.HasRenderedBubble(seat))
            {
                string item=seat+":"+speech.BubbleText(seat);if(!visible.Contains(item))visible.Add(item);
            }
        record.rendered_bubbles=visible.ToArray();
        record.concede_bubble_rendered=speech!=null&&record.concede_received&&speech.HasRenderedBubble(record.concede_owner);
        if(!armed){armed=true;nextActionTime=Mathf.Max(nextActionTime,Time.unscaledTime+0.5f);}

        bool startBubbleExpired=speech==null||Enumerable.Range(0,2).All(seat=>speech.BubbleEmoteId(seat)!="START");
        if(supported&&state.phase=="play"&&record.opening_complete&&startBubbleExpired&&
            optionIndex<options.Length&&Time.unscaledTime>=nextActionTime)
        {
            if(!menu.IsOpen)
            {
                if(menu.ReplayRightClick(ownHero.center,ownHero,state.match_id))
                {
                    record.menu_opened=true;
                    if(!menuCaptured)
                    {
                        menuCaptured=true;record.menu_capture_phase=state.phase;
                        record.menu_capture_after_start=speech==null||speech.OpeningComplete;
                        record.menu_capture_after_start_bubble_expired=startBubbleExpired;
                        capture?.Invoke("hero-speech-menu");
                    }
                    nextActionTime=Time.unscaledTime+.45f;
                }
            }
            else
            {
                string selected=options[optionIndex];
                if(menu.ReplaySelect(selected,sendEmote))
                {
                    sent.Add(selected);record.sent_options=sent.ToArray();record.menu_selection_count++;
                    optionIndex++;nextActionTime=Time.unscaledTime+1.15f;Save();
                }
            }
        }
        string bubbleEmote=speech==null?"":speech.BubbleEmoteId(record.seat);
        string bubbleText=speech==null?"":speech.BubbleText(record.seat);
        if(speech!=null&&!bubbleCaptured&&!String.IsNullOrEmpty(record.last_own_emote_id)&&
            speech.HasBubble(record.seat)&&speech.HasRenderedBubble(record.seat)&&bubbleEmote==record.last_own_emote_id&&
            !String.IsNullOrEmpty(bubbleText))
        {
            bubbleCaptured=true;record.emote_bubble_captured=true;record.emote_bubble_id=bubbleEmote;record.emote_bubble_text=bubbleText;
            capture?.Invoke("hero-speech-emote-bubble");
        }

        int expectedEvents=options.Length*2;
        if(supported&&concede&&!record.concede_sent&&optionIndex>=options.Length&&receivedIds.Count>=expectedEvents&&
            state.phase!="finished"&&Time.unscaledTime>=nextActionTime)
        {record.concede_sent=true;concedeMove();Save();}

        if(state.phase=="finished"&&resultReady)
        {
            record.result_seen=true;
            if(record.concede_received&&record.concede_bubble_rendered&&!resultCaptured)
            {resultCaptured=true;capture?.Invoke("hero-speech-result");exitAt=Time.unscaledTime+2.5f;}
        }
        Save();
    }

    void Save()
    {
        if(String.IsNullOrEmpty(reportPath))return;
        try
        {
            var folder=Path.GetDirectoryName(reportPath);if(!String.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
            File.WriteAllText(reportPath,JsonUtility.ToJson(record,true));
        }
        catch(Exception e){Debug.LogWarning("Hero speech replay report: "+e.Message);}
    }

    void EnsureListenerCapture()
    {
        if(!Enabled||String.IsNullOrEmpty(reportPath)||listenerTap!=null||listenerCaptureStopped||
            (record.listener_audio!=null&&!String.IsNullOrEmpty(record.listener_audio.error)))return;
        string path=Path.ChangeExtension(reportPath,".listener.wav");
        try
        {
            var listeners=UnityEngine.Object.FindObjectsOfType<AudioListener>().Where(listener=>listener!=null&&listener.enabled).ToArray();
            if(listeners.Length!=1)throw new InvalidOperationException("Expected one enabled Unity AudioListener, found "+listeners.Length);
            var folder=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
            listenerTap=listeners[0].gameObject.AddComponent<FireblastPcmTap>();listenerTap.Begin(path);
            record.listener_audio=new HeroSpeechListenerAudioRecord{path=Path.GetFullPath(path),format="WAVE_FORMAT_IEEE_FLOAT",rate=listenerTap.Rate,channels=listenerTap.Channels};
        }
        catch(Exception error)
        {
            if(listenerTap!=null){UnityEngine.Object.Destroy(listenerTap);listenerTap=null;}
            record.listener_audio=new HeroSpeechListenerAudioRecord{path=Path.GetFullPath(path),error=error.Message};
            Debug.LogWarning("Hero speech Listener capture unavailable: "+error.Message);
        }
        Save();
    }

    void StopListenerCapture()
    {
        if(listenerCaptureStopped)return;
        listenerCaptureStopped=true;
        if(listenerTap!=null)
        {
            try
            {
                listenerTap.StopCapture();
                record.listener_audio=new HeroSpeechListenerAudioRecord{path=Path.GetFullPath(Path.ChangeExtension(reportPath,".listener.wav")),
                    format="WAVE_FORMAT_IEEE_FLOAT",rate=listenerTap.Rate,channels=listenerTap.Channels,callbacks=listenerTap.Callbacks,
                    overflows=listenerTap.Overflows,sample_frames=listenerTap.SampleFrames,peak=listenerTap.Peak,rms=listenerTap.Rms,
                    clipped_samples=listenerTap.ClippedSamples};
            }
            catch(Exception error)
            {
                if(record.listener_audio==null)record.listener_audio=new HeroSpeechListenerAudioRecord();
                record.listener_audio.error=error.Message;
            }
            listenerTap=null;
        }
        Save();
    }
}
