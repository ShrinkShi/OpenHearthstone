using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// H02's outer Action spell and cloned per-Totem CustomSpawn spell are separate source FSM hosts.
public sealed class NativeShamanHeroPowerPlayback : IDisposable
{
    const string Reference="Shaman_HeroPower_FX.prefab:e4a069df7e3900b4a878dfb437de270c";
    const float PlayFxDelay=.5f;
    const float ButtonEffectScale=1.55f, SummonEffectScale=2f;
    public const float ButtonOrthographicSize=1.33f;
    sealed class Entry
    {
        public string key,card,playFxTrace;public int entity;public GameObject actor;public NativeEffectInstance effect;
        public float age,spawnFinishAge,maxAgeRegression;public int ageRegressionCount;public bool playFx,finishing;
    }
    readonly AssetRegistry assets;readonly NativeBattleActors actors;readonly NativeEffectEntry definition;
    readonly Dictionary<string,Entry> active=new Dictionary<string,Entry>();
    readonly HashSet<string> failed=new HashSet<string>();
    public Action<NativeAudioCue> AudioCue;
    public Camera TimelineCamera;
    public int ActiveCount{get{return active.Count;}}
    public int Failures{get;private set;}
    public string LastFailure{get;private set;}
    public string Diagnostics{get{return String.Join(";",active.Values.Select(entry=>"entity="+entry.entity+" age="+entry.age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
        " states="+String.Join(",",entry.effect.CurrentStates)+" actorBindings="+entry.effect.ActorObjectBindings+" visibilityBindings="+entry.effect.ActorRootVisibilityBindings+
        " unboundVisibility="+entry.effect.UnboundActorVisibilityActions+" audioStarts="+entry.effect.ClipAudioStarts+" unboundAudio="+entry.effect.UnboundAudioActions+
        " actions="+String.Join(",",entry.effect.ActionExecutions.OrderBy(pair=>pair.Key).Select(pair=>pair.Key+":"+pair.Value).ToArray())+
        " playFx="+entry.playFx+" finishing="+entry.finishing+" spawnFinishAge="+entry.spawnFinishAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
        " ageRegressions="+entry.ageRegressionCount+" maxAgeRegression="+
        entry.maxAgeRegression.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" trace="+entry.playFxTrace));}}
    public bool Enabled{get{return definition!=null&&definition.enabled&&Supports(assets,definition);}}
    public IEnumerable<GameObject> BorrowedActors{get{return active.Values.Select(e=>e.actor).Where(a=>a!=null);}}
    public bool Has(string key){return active.ContainsKey(key);}

    public NativeShamanHeroPowerPlayback(AssetRegistry assets,NativeBattleActors actors)
    {
        this.assets=assets;this.actors=actors;
        string path=Path.Combine(assets.Root,"native-effect-index.json");
        if(File.Exists(path))definition=(JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(path)).entries??new NativeEffectEntry[0])
            .FirstOrDefault(row=>row.card_id=="HERO_02bp");
    }

    static NativeProgram Read(AssetRegistry assets,NativeEffectEntry entry)
    {
        return JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
    }
    public static bool SupportsButtonRoot(NativeProgram program)
    {
        if(program==null||program.reference!=Reference||program.nodes==null||program.nodes.Length!=13||program.machines==null||program.animations==null)return false;
        var root=program.nodes[0];var motes=program.nodes[1];var quad=program.nodes[2];
        var particle=motes.particle;var bursts=particle==null||particle.emission==null?null:particle.emission.bursts;
        var cast=program.machines.FirstOrDefault(machine=>machine.owner_known&&machine.owner==0);
        var fade=program.animations.FirstOrDefault(animation=>animation.name=="FX_TotemFadeIn");
        var actionState=cast==null||cast.states==null?null:cast.states.FirstOrDefault(state=>state.name=="State0");
        var fadeAction=actionState==null?null:actionState.actions
            .FirstOrDefault(action=>action.name=="AnimationPlaythroughAction"&&action.Param("m_GameObject").node==2&&action.Param("m_AnimName").text=="FX_TotemFadeIn");
        var enabledTrack=fade==null||fade.tracks==null?null:fade.tracks.FirstOrDefault(track=>track.property=="m_Enabled");
        bool animatedVisible=fadeAction!=null&&enabledTrack!=null&&enabledTrack.keys!=null&&enabledTrack.keys.Length>=2&&
            quad.animation_refs!=null&&quad.animation_refs.Contains(fade.id)&&enabledTrack.keys[0].time==0&&enabledTrack.keys[0].value==1&&
            enabledTrack.keys.Length==3&&enabledTrack.keys[1].time>1.2f&&enabledTrack.keys[1].value==1&&
            enabledTrack.keys[enabledTrack.keys.Length-1].value==0;
        return root.name=="ReuseFX_Totem_OverrideSpawn_Blue_Super_SourceHeroPower"&&root.parent==-1&&root.active&&
            motes.name=="MoteBurst"&&motes.parent==0&&motes.active&&motes.visible&&particle!=null&&particle.restored&&
            particle.shape!=null&&particle.shape.enabled&&Mathf.Abs(particle.shape.radius-.17f)<.002f&&
            particle.life!=null&&Mathf.Abs(particle.life.minimum-.25f)<.002f&&Mathf.Abs(particle.life.scalar-1f)<.002f&&
            particle.velocity!=null&&Mathf.Abs(particle.velocity.minimum-5f)<.002f&&Mathf.Abs(particle.velocity.scalar-10f)<.002f&&
            particle.size!=null&&Mathf.Abs(particle.size.minimum-.2f)<.002f&&Mathf.Abs(particle.size.scalar-.4f)<.002f&&
            bursts!=null&&bursts.Length==1&&Mathf.Abs(bursts[0].count.scalar-50f)<.002f&&
            quad.name=="TotemQuad_mesh"&&quad.parent==0&&!quad.visible&&quad.mesh=="f3bd8ad06a388d90eea7996e.mesh.json"&&
            quad.materials!=null&&quad.materials.Any(material=>material.name=="GenFX_TotemIcon1"&&material.textures!=null&&
                material.textures.Any(texture=>texture.path=="51fb201b4dd9498be04d2c5b.png"))&&animatedVisible;
    }
    public static bool TryGetSpawnFinishAge(NativeProgram program,out float age)
    {
        age=0;
        try
        {
            if(program==null||program.nodes==null||program.nodes.Length<=3||program.animations==null||program.machines==null)return false;
            var spawn=program.machines.Single(machine=>machine.owner_known&&machine.owner==3);
            var actionState=spawn.states.Single(state=>state.name=="State 3");
            if(!actionState.transitions.Any(item=>item.@event=="FINISHED"&&item.state=="State 4"))return false;
            var wait=actionState.actions.Single(action=>action.name=="Wait").Param("time");
            var animationAction=spawn.states.Single(state=>state.name=="State 4").actions.Single(action=>action.name=="AnimationPlaythroughAction");
            var animationName=animationAction.Param("m_AnimName").text;
            var node=program.nodes[3];
            if(node.animation_refs==null||!node.animation_refs.Contains(program.animations.Single(clip=>clip.name==animationName).id))return false;
            var clip=program.animations.Single(item=>item.name==animationName);
            if(wait.unresolved||!String.IsNullOrEmpty(wait.variable)||float.IsNaN(wait.number)||float.IsInfinity(wait.number)||wait.number<0||
               String.IsNullOrEmpty(animationName)||clip.loop||clip.duration<=0||float.IsNaN(clip.duration)||float.IsInfinity(clip.duration))return false;
            age=wait.number+clip.duration;
            return age>PlayFxDelay&&age<5f;
        }
        catch{return false;}
    }
    public static bool Supports(AssetRegistry assets,NativeEffectEntry entry)
    {
        try
        {
            if(entry==null||entry.card_id!="HERO_02bp"||!entry.enabled||entry.duration<2.5f)return false;
            var program=Read(assets,entry);
            if(!SupportsButtonRoot(program)||program.machines==null||
               !File.Exists(Path.Combine(assets.Root,program.audio_file)))return false;
            var cast=program.machines.Single(machine=>machine.owner_known&&machine.owner==0);
            var spawn=program.machines.Single(machine=>machine.owner_known&&machine.owner==3);
            float spawnFinishAge;
            if(NativeSpellLifecycle.Entry(cast)==null||NativeSpellLifecycle.Entry(cast).state!="State0"||
               NativeSpellLifecycle.Entry(spawn)==null||NativeSpellLifecycle.Entry(spawn).state!="State 3"||
               !cast.events.Any(item=>item.@event=="Action"&&item.state=="State0")||
               !spawn.events.Any(item=>item.@event=="Action"&&item.state=="State 3")||
               !spawn.events.Any(item=>item.@event=="PlayFX"&&item.state=="State 2")||
               !spawn.events.Any(item=>item.@event=="Die"&&item.state=="State 5")||
               !TryGetSpawnFinishAge(program,out spawnFinishAge))return false;
            foreach(var action in program.machines.SelectMany(machine=>machine.states).SelectMany(state=>state.actions)
                .Where(action=>action.name=="AudioPlayClipAction"))
            {
                var sound=action.Param("m_Sound");
                if(sound.unresolved||String.IsNullOrEmpty(sound.asset_path)||!File.Exists(Path.Combine(assets.Root,sound.asset_path)))return false;
            }
            return true;
        }
        catch{return false;}
    }

    public static NativeProgram ForOwner(AssetRegistry assets,NativeEffectEntry entry,int owner)
    {
        if(owner!=0&&owner!=3||!Supports(assets,entry))throw new NotSupportedException("H02 source owner is not reviewed");
        var program=Read(assets,entry);var selected=program.machines.Single(machine=>machine.owner_known&&machine.owner==owner);
        program.machines=new[]{selected};
        var keep=new bool[program.nodes.Length];
        for(int i=0;i<program.nodes.Length;i++)
        {
            int node=i;
            while(node>=0){if(node==owner){keep[i]=true;break;}node=program.nodes[node].parent;}
            if(!keep[i]){program.nodes[i].active=false;program.nodes[i].visible=false;}
        }
        program.spell_transforms=(program.spell_transforms??new NativeSourceSpellTransform[0])
            .Where(transform=>transform.node>=0&&transform.node<keep.Length&&keep[transform.node]).ToArray();
        program.animation_event_receivers=(program.animation_event_receivers??new NativeAnimationEventReceiver[0])
            .Where(receiver=>receiver.node>=0&&receiver.node<keep.Length&&keep[receiver.node]).ToArray();
        if(program.spell_states!=null)program.spell_states=program.spell_states.Where(state=>state.node>=0&&state.node<keep.Length&&keep[state.node]).ToArray();
        return program;
    }

    public static bool IsShamanSummon(SemanticEvent data,StateView before,StateView after)
    {
        if(data==null||data.type!="Summon")return false;
        var source=BattleInteraction.Entity(before,data.source)??BattleInteraction.Entity(after,data.source);
        return source!=null&&source.card_id=="HERO_02bp";
    }

    public static Rect ButtonEffectRect(Rect buttonBounds)
    {
        // The supplied reference shows the rune halo at about 1.5x the actual power-button diameter.
        float size=Mathf.Max(buttonBounds.width,buttonBounds.height)*ButtonEffectScale;
        return new Rect(buttonBounds.center.x-size*.5f,buttonBounds.center.y-size*.5f,size,size);
    }

    public static Rect SummonEffectRect(Rect tokenBounds)
    {
        // The reference's lightning and totem span roughly twice the summoned token's board bounds.
        float size=Mathf.Max(tokenBounds.width,tokenBounds.height)*SummonEffectScale;
        return new Rect(tokenBounds.center.x-size*.5f,tokenBounds.center.y-size*.5f,size,size);
    }

    public bool Prepare(string key,SemanticEvent data,StateView before,StateView after,float age)
    {
        if(!Enabled||age<0||!IsShamanSummon(data,before,after)||failed.Contains(key))return false;
        Entry entry;
        try
        {
            if(!active.TryGetValue(key,out entry))
            {
                var entity=BattleInteraction.Entity(after,data.target)??BattleInteraction.Entity(before,data.target);
                if(entity==null||entity.entity!=data.target)return false;
                var actor=actors.Resolve(entity);var identity=actor==null?null:NativeActorIdentity.FindInThisOrParents(actor);
                if(identity==null||identity.GetComponent<NativeActorPresentation>()==null)return false;
                if(active.Values.Any(current=>current.actor==actor))return false;
                var source=Read(assets,definition);float spawnFinishAge;
                if(!TryGetSpawnFinishAge(source,out spawnFinishAge))throw new InvalidOperationException("H02 CustomSpawn completion timing is not source-bound");
                var program=ForOwner(assets,definition,3);
                var placement=NativeSpellObjectSelection.ForActor(actor,4);
                var effect=new NativeEffectInstance(program,assets,active.Count,512,512,
                    context:new NativeSpellContext(4,placement:placement),audioCallback:AudioCue,deferEntryEvents:true);
                effect.BindSourcePlacementRoot(3);
                effect.BindTimelineCamera(TimelineCamera);effect.AttachToOriginalParent(actor);
                entry=new Entry{key=key,entity=entity.entity,card=entity.card_id,actor=actor,effect=effect,spawnFinishAge=spawnFinishAge};
                active.Add(key,entry);
                effect.Seek(0);
                effect.UpdateOriginalPosition();
                bool dispatched=effect.DispatchMachineEvent(0,"Action");
                entry.playFxTrace="actionDispatched="+dispatched+" state="+String.Join(",",effect.CurrentStates);
                if(!dispatched)throw new InvalidOperationException("H02 CustomSpawn Action source event did not transition");
            }
            if(entry.finishing)return false;
            Sample(entry,age);
            return Valid(entry.effect);
        }
        catch(Exception error)
        {
            Failures++;failed.Add(key);
            LastFailure=error.GetBaseException().Message+(active.TryGetValue(key,out entry)?"; "+Describe(entry):"");
            if(active.TryGetValue(key,out entry)){entry.effect.Dispose();active.Remove(key);}
            Debug.LogWarning("Shaman CustomSpawn fallback: "+error.GetBaseException().Message);return false;
        }
    }

    void Sample(Entry entry,float age)
    {
        if(age<entry.age)
        {
            entry.ageRegressionCount++;entry.maxAgeRegression=Mathf.Max(entry.maxAgeRegression,entry.age-age);age=entry.age;
        }
        if(!entry.playFx&&age>=PlayFxDelay)
        {
            float eventAge=Mathf.Max(entry.age,PlayFxDelay);entry.effect.Seek(eventAge);
            string before=String.Join(",",entry.effect.CurrentStates);
            int actorBindings=entry.effect.ActorObjectBindings,visibilityBindings=entry.effect.ActorRootVisibilityBindings,audioStarts=entry.effect.ClipAudioStarts;
            bool dispatched=entry.effect.DispatchMachineEvent(0,"PlayFX");
            entry.playFxTrace+="; previous="+entry.age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " requested="+age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" event="+eventAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " before="+before+" actor="+actorBindings+" visibility="+visibilityBindings+" audio="+audioStarts+
                " dispatched="+dispatched+" after="+String.Join(",",entry.effect.CurrentStates)+
                " ageRegressions="+entry.ageRegressionCount+" maxAgeRegression="+entry.maxAgeRegression.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
            if(!dispatched)throw new InvalidOperationException("H02 PlayFX source event did not transition");
            entry.playFx=true;
        }
        if(!entry.finishing&&age>=entry.spawnFinishAge)
        {
            float finishAge=Mathf.Max(entry.age,entry.spawnFinishAge);entry.effect.Seek(finishAge);
            bool dispatched=entry.effect.DispatchMachineEvent(0,"Die");
            entry.playFxTrace+="; sourceSpawnFinished="+finishAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" dispatched="+dispatched;
            if(!dispatched)throw new InvalidOperationException("H02 source spawn-completion event did not transition");
            entry.finishing=true;
        }
        entry.effect.Seek(age);entry.age=age;
        if(!Valid(entry.effect))throw new InvalidOperationException("H02 cloned Spell has unbound source actions: "+Describe(entry));
    }

    static string Describe(Entry entry)
    {
        return "entity="+entry.entity+" age="+entry.age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            " states="+String.Join(",",entry.effect.CurrentStates)+" actorBindings="+entry.effect.ActorObjectBindings+
            " visibilityBindings="+entry.effect.ActorRootVisibilityBindings+" unboundVisibility="+entry.effect.UnboundActorVisibilityActions+
            " audioStarts="+entry.effect.ClipAudioStarts+" unboundAudio="+entry.effect.UnboundAudioActions+
            " actions="+String.Join(",",entry.effect.ActionExecutions.OrderBy(pair=>pair.Key).Select(pair=>pair.Key+":"+pair.Value).ToArray());
    }

    bool Valid(NativeEffectInstance effect)
    {
        return effect.UnboundActorInitializationActions==0&&effect.UnboundActorVisibilityActions==0&&
            effect.UnboundAnimationActions==0&&effect.UnboundAudioActions==0&&effect.UnboundMainShakeActions==0&&
            effect.UnboundSetParentActions==0&&effect.UnboundDestroyObjectActions==0&&effect.UnhandledActions==0&&
            effect.UnboundMachineOwnerActions==0&&effect.SourcePositionUpdates>0&&effect.UnboundSourcePlacements==0;
    }

    public bool Draw(string key,Rect rect,float age,Func<Rect,Texture,Texture,Matrix4x4,int,int,Color,bool> composite)
    {
        Entry entry;
        if(composite==null||!active.TryGetValue(key,out entry)||entry.effect==null||entry.actor==null||
           Event.current==null||Event.current.type!=EventType.Repaint||age<0||age>=entry.spawnFinishAge||float.IsNaN(age)||float.IsInfinity(age))return false;
        var presentation=entry.actor.GetComponent<NativeActorPresentation>();
        bool wasShown=presentation!=null&&presentation.Shown;
        try
        {
            // The overlay is captured independently from the borrowed portrait, including at the visibility handoff.
            if(wasShown)presentation.SetShown(false);
            var emission=entry.effect.RenderAffineSubtree(age,3,2.5f);
            return composite(rect,emission,entry.effect.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);
        }
        finally{if(wasShown)presentation.SetShown(true);}
    }

    public bool PrepareDeath(string key,SemanticEvent data,float age)
    {
        if(data==null||data.type!="Death")return false;
        var entry=active.Values.FirstOrDefault(current=>current.entity==data.target);
        if(entry==null||entry.finishing)return false;
        Sample(entry,Mathf.Max(entry.age,age));
        if(entry.finishing)return true;
        if(!entry.playFx&&entry.age>=PlayFxDelay)
        {
            entry.effect.Seek(entry.age);
            if(!entry.effect.DispatchMachineEvent(0,"PlayFX"))return false;
            entry.playFx=true;entry.effect.Seek(entry.age);
        }
        if(!entry.effect.DispatchMachineEvent(0,"Die"))return false;
        entry.finishing=true;return true;
    }

    public IEnumerable<int> DeathEntities(IEnumerable<Presentation.Cue> cues)
    {
        if(cues==null)yield break;
        foreach(var cue in cues)if(cue.data.type=="Death")yield return cue.data.target;
    }

    public void Advance(float delta,StateView shown,IEnumerable<int> pendingDeaths)
    {
        if(delta<0||float.IsNaN(delta)||float.IsInfinity(delta))throw new ArgumentException("Invalid Shaman CustomSpawn delta");
        var deaths=new HashSet<int>(pendingDeaths??Enumerable.Empty<int>());
        foreach(var key in active.Keys.ToArray())
        {
            var entry=active[key];
            bool visible=shown!=null&&(shown.players??new PlayerView[0]).Any(player=>(player.field??new EntityView[0]).Any(entity=>entity.entity==entry.entity&&entity.card_id==entry.card));
            if(!visible&&!entry.finishing&&!deaths.Contains(entry.entity)){Release(key);continue;}
            if(delta>0)Sample(entry,entry.age+delta);
            if(entry.finishing&&entry.effect.CurrentStates.Length>0&&entry.effect.CurrentStates[0]=="NoneState")Release(key);
        }
    }

    void Release(string key)
    {Entry entry;if(!active.TryGetValue(key,out entry))return;entry.effect.Dispose();active.Remove(key);}
    public void Dispose(){foreach(var key in active.Keys.ToArray())Release(key);failed.Clear();}
}
