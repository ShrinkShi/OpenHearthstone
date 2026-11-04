using System;
using System.IO;
using System.Linq;
using UnityEngine;

// H11 is routed only when a real PlayHeroPower event owns one real ghoul Summon.
public static class NativeDeathKnightHeroPowerPlayback
{
    public const string HeroPowerId = "HERO_11bp";
    public const string GhoulId = "HERO_11bpt";
    public const float AuthoredTimelineDuration = 5f;
    public const float TimelineStartAge = 1f/6f;
    public const int TokenAudioFrames = 192634;
    public const int TokenAudioSampleRate = 48000;
    public const float TokenAudioDuration = TokenAudioFrames/(float)TokenAudioSampleRate;
    public const float AudioCompleteAge = TimelineStartAge+TokenAudioDuration;
    static float? cachedAudioGraceSeconds;

    public static float AudioGraceSeconds
    {
        get
        {
            if(cachedAudioGraceSeconds.HasValue)return cachedAudioGraceSeconds.Value;
            int bufferLength,buffers;AudioSettings.GetDSPBufferSize(out bufferLength,out buffers);
            int rate=AudioSettings.outputSampleRate;
            float grace=bufferLength>0&&buffers>0&&rate>0?bufferLength*(float)buffers/rate:0;
            cachedAudioGraceSeconds=grace;return grace;
        }
    }
    public static float IdentityThroughAge{get{return AudioCompleteAge+AudioGraceSeconds;}}

    // Keep the source clock unchanged through the full token WAV and Unity's queued DSP buffers.
    public static float PlaybackAge(float age)
    {
        if(age<0||float.IsNaN(age)||float.IsInfinity(age))throw new ArgumentOutOfRangeException("age");
        return age<IdentityThroughAge?age:AuthoredTimelineDuration;
    }

    public static float PlaybackAge(float age,bool tokenAudioPlaying)
    {
        float mappedAge=PlaybackAge(age);
        if(tokenAudioPlaying&&age>=IdentityThroughAge)
            return Mathf.Min(age,AuthoredTimelineDuration-.001f);
        return mappedAge;
    }

    public static NativeAudioCue TokenAudioCue(NativeEffectInstance effect)
    {
        if(effect==null||effect.TimelineSoundStarts!=1||effect.AudioCues==null)return null;
        var cues=effect.AudioCues.Where(cue=>cue!=null&&cue.sourceName!=null&&
            cue.sourceName.StartsWith("Native timeline sound - ",StringComparison.Ordinal)).ToArray();
        return cues.Length==1?cues[0]:null;
    }

    public static bool TokenAudioPlaying(NativeEffectInstance effect)
    {
        var cue=TokenAudioCue(effect);
        return cue!=null&&cue.playbackActive!=null&&cue.playbackActive();
    }

    public static Rect ButtonEffectRect(Rect button)
    {
        float width=button.width>0?button.width:120,height=button.height>0?button.height:120;
        float halfWidth=width*.85f,halfHeight=height*.85f;
        return new Rect(button.center.x-halfWidth,button.center.y-halfHeight,halfWidth*2,halfHeight*2);
    }

    public sealed class Pair
    {
        public SemanticEvent power;
        public SemanticEvent summon;
        public EntityView powerEntity;
        public EntityView ghoul;
        public int ownerIndex;
        public int ownerSeat;
    }

    static EntityView FindPower(StateView state,int entity,out int ownerIndex,out int ownerSeat)
    {
        ownerIndex=-1;ownerSeat=-1;
        if(state==null||state.players==null)return null;
        for(int i=0;i<state.players.Length;i++)
        {
            var player=state.players[i];
            if(player==null||player.power==null||player.power.entity!=entity)continue;
            if(ownerIndex>=0)return null;
            ownerIndex=i;ownerSeat=player.seat;return player.power;
        }
        return null;
    }

    static EntityView FindGhoul(StateView state,int entity,out int ownerIndex)
    {
        ownerIndex=-1;EntityView found=null;
        if(state==null||state.players==null)return null;
        for(int i=0;i<state.players.Length;i++)
            foreach(var card in state.players[i]==null?new EntityView[0]:state.players[i].field??new EntityView[0])
                if(card!=null&&card.entity==entity)
                {if(found!=null)return null;found=card;ownerIndex=i;}
        return found;
    }

    static bool SameEvent(SemanticEvent left,SemanticEvent right)
    {return left!=null&&right!=null&&left.type==right.type&&left.card_id==right.card_id&&left.source==right.source&&left.target==right.target&&left.owner==right.owner;}

    public static bool TryGetPair(SemanticEvent powerEvent,StateView before,StateView after,out Pair pair)
    {
        pair=null;
        if(powerEvent==null||powerEvent.type!="PlayHeroPower"||before==null||after==null||
           before.match_id!=after.match_id||before.players==null||after.players==null)return false;
        var events=after.events??new SemanticEvent[0];
        var plays=events.Where(e=>e!=null&&e.type=="PlayHeroPower"&&e.target==powerEvent.target).ToArray();
        if(plays.Length!=1||!SameEvent(plays[0],powerEvent))return false;
        int beforeOwner,beforeSeat,afterOwner,afterSeat;
        var oldPower=FindPower(before,powerEvent.target,out beforeOwner,out beforeSeat);
        var currentPower=FindPower(after,powerEvent.target,out afterOwner,out afterSeat);
        if(oldPower==null||currentPower==null||oldPower.card_id!=HeroPowerId||currentPower.card_id!=HeroPowerId||
           beforeOwner!=afterOwner||beforeSeat!=afterSeat||powerEvent.owner>=0&&powerEvent.owner!=afterOwner||
           !String.IsNullOrEmpty(powerEvent.card_id)&&powerEvent.card_id!=HeroPowerId)return false;
        var summons=events.Where(e=>e!=null&&e.type=="Summon"&&e.source==powerEvent.target).ToArray();
        if(summons.Length!=1)return false;
        var summon=summons[0];
        int targetOwner;var ghoul=FindGhoul(after,summon.target,out targetOwner);
        if(ghoul==null||ghoul.card_id!=GhoulId||targetOwner!=afterOwner||
           summon.owner>=0&&summon.owner!=afterOwner)return false;
        pair=new Pair{power=powerEvent,summon=summon,powerEntity=currentPower,ghoul=ghoul,
            ownerIndex=afterOwner,ownerSeat=afterSeat};
        return true;
    }

    public static bool TryGetPairForSummon(SemanticEvent summon,StateView before,StateView after,out Pair pair)
    {
        pair=null;
        if(summon==null||summon.type!="Summon"||after==null)return false;
        var powers=(after.events??new SemanticEvent[0]).Where(e=>e!=null&&e.type=="PlayHeroPower"&&e.target==summon.source).ToArray();
        if(powers.Length!=1||!TryGetPair(powers[0],before,after,out pair)||!SameEvent(pair.summon,summon))
        {pair=null;return false;}
        return true;
    }

    public static bool TryGetPairForPowerCue(SemanticEvent cue,StateView before,StateView after,out Pair pair)
    {
        pair=null;
        if(cue==null||cue.type!="SpellEffect"||cue.card_id!=HeroPowerId||cue.source<=0||cue.target<=0||after==null)return false;
        var powers=(after.events??new SemanticEvent[0]).Where(e=>e!=null&&e.type=="PlayHeroPower"&&e.target==cue.source).ToArray();
        if(powers.Length!=1||!TryGetPair(powers[0],before,after,out pair)||pair.ghoul.entity!=cue.target||
           cue.owner>=0&&cue.owner!=pair.ownerIndex){pair=null;return false;}
        return true;
    }

    public static bool SamePair(Pair left,Pair right)
    {return left!=null&&right!=null&&left.ownerIndex==right.ownerIndex&&left.ownerSeat==right.ownerSeat&&
        SameEvent(left.power,right.power)&&SameEvent(left.summon,right.summon)&&left.ghoul.entity==right.ghoul.entity;}

    public static bool Supports(AssetRegistry assets,NativeEffectEntry entry)
    {
        try
        {
            if(assets==null||entry==null||entry.card_id!=HeroPowerId||!entry.enabled||Mathf.Abs(entry.duration-5f)>.001f||
               String.IsNullOrEmpty(entry.file)||!File.Exists(Path.Combine(assets.Root,entry.file)))return false;
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            if(program==null||program.reference!="ReuseFX_Necrotic_DeathKnight_OverrideSpawn_HeroPower_Super.prefab:d71d2c073842bf24a9a24f9cabada171"||
               program.nodes==null||program.nodes.Length!=21||program.machines==null||program.machines.Length!=1||
               program.timelines==null||program.timelines.Length!=1||String.IsNullOrEmpty(program.audio_file)||
               !File.Exists(Path.Combine(assets.Root,program.audio_file))||program.nodes[0].parent!=-1||program.nodes[1].parent!=-1||
               program.nodes[2].parent!=1||program.nodes[3].parent!=2)return false;
            var machine=program.machines[0];
            if(!machine.owner_known||machine.owner!=1||machine.start!="State 1")return false;
            if(!SupportsPositionState(machine,"Friendly Position","FriendlyHP",new Vector3(-5.78662014f,.300000012f,-8.09019184f))||
               !SupportsPositionState(machine,"Opponent Position","OpponentHP",new Vector3(-5.78112984f,.300000012f,-1.41999996f)))return false;
            var state1=machine.states.Single(state=>state.name=="State 1");
            var side=state1.actions.Single(action=>action.name=="SpellPlayerSideEventAction");
            var receiver=side.Param("m_SpellObject");var friendly=side.Param("m_FriendlyEvent");var opponent=side.Param("m_OpponentEvent");
            if(state1.transitions==null||side.@params==null||side.@params.Any(parameter=>parameter.unresolved)||receiver.kind!=20||receiver.node!=machine.owner||
               friendly.kind!=23||friendly.text!="Friendly"||opponent.kind!=23||opponent.text!="Opponent"||
               !state1.transitions.Any(t=>t.@event==friendly.text)||!state1.transitions.Any(t=>t.@event==opponent.text))return false;
            var actionState=machine.states.Single(state=>state.name=="ActionState");
            var spawnState=machine.states.Single(state=>state.name=="Minion Spawn");
            var revealState=machine.states.Single(state=>state.name=="Visibility On");
            var finishState=machine.states.Single(state=>state.name=="Finish");
            if(!WaitMatches(actionState,TimelineStartAge)||!WaitMatches(spawnState,1f/3f)||
               !WaitMatches(revealState,.5f)||!WaitMatches(finishState,3f)||
               Mathf.Abs(PlaybackAge(.5f)-.5f)>.00001f||Mathf.Abs(PlaybackAge(1f)-1f)>.00001f)return false;
            var spawn=actionState.actions.Single(action=>action.name=="InitializeCustomSpawn");var token=spawn.Param("tokenRoot");
            var rootAction=actionState.actions.Single(action=>action.name=="AudioPlayClipAction");var buttonSound=rootAction.Param("m_Sound");
            if(token.kind!=19||token.node!=3||rootAction.@params.Any(parameter=>parameter.unresolved)||
               rootAction.Param("m_ParentObject").node!=1||buttonSound.unresolved||
               String.IsNullOrEmpty(buttonSound.asset_path)||!File.Exists(Path.Combine(assets.Root,buttonSound.asset_path)))return false;
            var timeline=program.timelines[0];
            if(timeline.node!=1||timeline.wrap!=2||Mathf.Abs(timeline.duration-5f)>.001f||timeline.initial_time!=0||
               timeline.tracks==null||timeline.tracks.Length!=5||timeline.tracks.Count(track=>track.kind=="AnimationTrack"&&!track.muted)!=1||
               timeline.tracks.Count(track=>track.kind=="ControlRandomizedTrack"&&!track.muted)!=2||
               timeline.tracks.Count(track=>track.kind=="CameraShaker2Track"&&!track.muted)!=1)return false;
            var soundTrack=timeline.tracks.Single(track=>track.kind=="SoundTimelineTrack");
            if(soundTrack.muted||soundTrack.node!=20||soundTrack.clips==null||soundTrack.clips.Length!=1||
               soundTrack.clips[0].start!=0||Mathf.Abs(soundTrack.clips[0].duration-5f)>.001f||soundTrack.clips[0].speed!=1||
               soundTrack.clips[0].clip_in!=0||soundTrack.clips[0].pre!=0||soundTrack.clips[0].post!=0)return false;
            var audio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,program.audio_file)));
            var tokenCue=ResolveTimelineSoundCue(program,audio,assets.Root,timeline,soundTrack,null,0,()=>0);
            var button=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,buttonSound.asset_path)));
            var buttonCue=NativeAudio.ResolveClip(button,0,rootAction.Param("m_Volume").number,rootAction.Param("m_Pitch").number,
                (int)rootAction.Param("m_Category").number,()=>0);
            return tokenCue!=null&&buttonCue!=null&&File.Exists(Path.Combine(assets.Root,buttonCue.file))&&tokenCue.file!=buttonCue.file&&
                Mathf.Abs(WavDurationSeconds(Path.Combine(assets.Root,tokenCue.file))-TokenAudioDuration)<1f/TokenAudioSampleRate;
        }
        catch{return false;}
    }

    static bool WaitMatches(NativeState state,float expected)
    {
        var wait=state==null||state.actions==null?null:state.actions.SingleOrDefault(action=>action.name=="Wait");
        var time=wait==null?null:wait.Param("time");
        return time!=null&&!time.unresolved&&Mathf.Abs(time.number-expected)<.001f;
    }

    static float WavDurationSeconds(string path)
    {
        using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
        {
            if(stream.Length<44||new string(reader.ReadChars(4))!="RIFF")return -1;
            reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")return -1;
            int bytesPerSecond=0;long dataBytes=-1;
            while(stream.Position+8<=stream.Length)
            {
                string chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=stream.Position+size;
                if(end>stream.Length)return -1;
                if(chunk=="fmt ")
                {
                    if(size<16)return -1;
                    reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();bytesPerSecond=reader.ReadInt32();
                }
                else if(chunk=="data")dataBytes=size;
                stream.Position=end+(size&1);
            }
            return bytesPerSecond>0&&dataBytes>0?dataBytes/(float)bytesPerSecond:-1;
        }
    }

    static bool SupportsPositionState(NativeMachine machine,string stateName,string vectorName,Vector3 expected)
    {
        var state=machine.states.SingleOrDefault(row=>row.name==stateName);
        if(state==null||state.actions==null||!state.actions.Select(action=>action.name).SequenceEqual(new[]{"SetParent","SetPosition"}))return false;
        var setParent=state.actions[0];var parentTarget=setParent.Param("gameObject");var parent=setParent.Param("parent");
        if(setParent.@params.Any(parameter=>parameter.unresolved)||parentTarget.kind!=20||parentTarget.node!=17||
           parentTarget.variable!="HeroPowerFX"||parent.unresolved||!parent.none_known||!parent.is_none)return false;
        var action=state.actions[1];var target=action.Param("gameObject");var vector=action.Param("vector");
        var space=action.Param("space");var every=action.Param("everyFrame");var late=action.Param("lateUpdate");
        if(action.@params.Any(parameter=>parameter.unresolved)||target.kind!=20||target.node!=17||target.variable!="HeroPowerFX"||
           vector.kind!=28||vector.variable!=vectorName||!vector.none_known||vector.is_none||
           space.kind!=7||space.number!=0||!String.IsNullOrEmpty(space.variable)||
           every.kind!=1||every.number!=0||!String.IsNullOrEmpty(every.variable)||
           late.kind!=1||late.number!=0||!String.IsNullOrEmpty(late.variable))return false;
        foreach(string axis in new[]{"x","y","z"})
        {var component=action.Param(axis);if(!component.none_known||!component.is_none||component.unresolved)return false;}
        var sourceVectors=machine.vectors??new NativeVector[0];
        if(sourceVectors.Length!=2)return false;
        var binding=sourceVectors.SingleOrDefault(row=>row.name==vectorName);
        return binding!=null&&CloseVector(binding.value,expected)&&CloseVector(vector.vector,expected);
    }
    static bool CloseVector(Vector3 left,Vector3 right)
    {return Mathf.Abs(left.x-right.x)<.00001f&&Mathf.Abs(left.y-right.y)<.00001f&&Mathf.Abs(left.z-right.z)<.00001f;}

    public static NativeAudioCue ResolveTimelineSoundCue(NativeProgram program,NativeAudioDefinition audio,string root,
        NativeTimeline timeline,NativeTimelineTrack track,GameObject owner,float time,Func<float> random)
    {
        if(program==null||program.nodes==null||audio==null||String.IsNullOrEmpty(root)||timeline==null||track==null||track.kind!="SoundTimelineTrack"||
           track.muted||track.node<0||track.node>=program.nodes.Length||track.clips==null||track.clips.Length!=1||
           owner!=null&&owner.name!=program.nodes[track.node].name)return null;
        var clip=track.clips[0];
        if(clip.start!=0||clip.duration!=timeline.duration||clip.speed!=1||clip.clip_in!=0||clip.pre!=0||clip.post!=0||
           float.IsNaN(time)||float.IsInfinity(time))return null;
        var source=NativeAudio.Node(audio,program.nodes[track.node].path,track.node);
        if(source==null||source.audio_sources==null||source.audio_sources.Length!=1||source.audio_sources[0].values==null||
           source.audio_sources[0].values.Loop||source.sound_defs==null||source.sound_defs.Length!=1)return null;
        var cue=NativeAudio.Resolve(audio,program.nodes[track.node].path,time,1,random??(()=>0),track.node);
        if(cue==null||String.IsNullOrEmpty(cue.file)||!File.Exists(Path.Combine(root,cue.file)))return null;
        if(owner!=null)cue.sourceParent=owner.transform;
        return cue;
    }

    public static bool ShouldSkipGenericSummon(bool routeEnabled,SemanticEvent summon,StateView before,StateView after)
    {Pair pair;return routeEnabled&&TryGetPairForSummon(summon,before,after,out pair);}

    public static bool TryGetSideEvent(Pair pair,StateView state,out string sideEvent)
    {
        sideEvent=null;
        if(pair==null||state==null||state.players==null||pair.ownerIndex<0||pair.ownerIndex>=state.players.Length||
           state.players[pair.ownerIndex]==null||state.players[pair.ownerIndex].seat!=pair.ownerSeat||
           !state.players.Any(player=>player!=null&&player.seat==state.seat))return false;
        sideEvent=pair.ownerSeat==state.seat?"Friendly":"Opponent";
        return true;
    }
}
