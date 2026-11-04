using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativeDeathKnightHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Death Knight hero power: "+reason);passed++;}

    static StateView State(int owner,int localSeat,bool includeGhoul=true)
    {
        var players=new[]{
            new PlayerView{seat=0,power=owner==0?new EntityView{entity=101,card_id=NativeDeathKnightHeroPowerPlayback.HeroPowerId}:new EntityView{entity=201,card_id="OTHER_POWER"},field=new EntityView[0]},
            new PlayerView{seat=1,power=owner==1?new EntityView{entity=101,card_id=NativeDeathKnightHeroPowerPlayback.HeroPowerId}:new EntityView{entity=201,card_id="OTHER_POWER"},field=new EntityView[0]}
        };
        if(includeGhoul)players[owner].field=new[]{new EntityView{entity=301,card_id=NativeDeathKnightHeroPowerPlayback.GhoulId}};
        return new StateView{match_id="h11-check",version=1,seat=localSeat,players=players};
    }

    static SemanticEvent[] Events(int owner)
    {
        return new[]{
            new SemanticEvent{type="PlayHeroPower",target=101,card_id=NativeDeathKnightHeroPowerPlayback.HeroPowerId,owner=owner},
            new SemanticEvent{type="Summon",source=101,target=301,owner=owner}
        };
    }

    static void RunPairChecks()
    {
        var button=new Rect(100,200,40,50);
        var buttonEffect=NativeDeathKnightHeroPowerPlayback.ButtonEffectRect(button);
        Check(buttonEffect.center==button.center&&Mathf.Approximately(buttonEffect.width,button.width*1.7f)&&
            Mathf.Approximately(buttonEffect.height,button.height*1.7f),
            "button FX keeps the real hero-power button center while giving the halo room around its rim");
        foreach(int owner in new[]{0,1})
        {
            var before=State(owner,0,false);var after=State(owner,0);after.events=Events(owner);
            NativeDeathKnightHeroPowerPlayback.Pair pair;
            Check(NativeDeathKnightHeroPowerPlayback.TryGetPair(after.events[0],before,after,out pair),
                "accepts one PlayHeroPower paired with its authoritative ghoul Summon for owner "+owner);
            Check(pair.ownerIndex==owner&&pair.ghoul.entity==301,
                "returns the real owner and ghoul entity");
            Check(NativeDeathKnightHeroPowerPlayback.ShouldSkipGenericSummon(true,after.events[1],before,after),
                "suppresses duplicate generic SUMMON_IN only for the enabled exact pair");
            Check(!NativeDeathKnightHeroPowerPlayback.ShouldSkipGenericSummon(false,after.events[1],before,after),
                "leaves generic SUMMON_IN active while the H11 route is disabled");
            var own=State(owner,owner);string side;
            Check(NativeDeathKnightHeroPowerPlayback.TryGetSideEvent(pair,own,out side)&&side=="Friendly",
                "dispatches Friendly for the local owning seat");
            var opponent=State(owner,1-owner);
            Check(NativeDeathKnightHeroPowerPlayback.TryGetSideEvent(pair,opponent,out side)&&side=="Opponent",
                "dispatches Opponent for the other local seat");
        }

        var oldState=State(0,0,false);var newState=State(0,0,false);newState.events=Events(0);
        NativeDeathKnightHeroPowerPlayback.Pair ignored;
        Check(!NativeDeathKnightHeroPowerPlayback.TryGetPair(newState.events[0],oldState,newState,out ignored),
            "rejects a missing authoritative ghoul entity");
        newState=State(0,0);newState.events=Events(0);newState.events[1].source=999;
        Check(!NativeDeathKnightHeroPowerPlayback.TryGetPair(newState.events[0],oldState,newState,out ignored),
            "rejects a Summon from any source other than the power entity");
        newState=State(0,0);newState.events=Events(0);newState.players[0].field[0].card_id="OTHER_TOKEN";
        Check(!NativeDeathKnightHeroPowerPlayback.TryGetPair(newState.events[0],oldState,newState,out ignored),
            "rejects a different card occupying the target entity");
        newState=State(0,0);newState.events=Events(0);newState.events=newState.events.Concat(new[]{
            new SemanticEvent{type="Summon",source=101,target=301,owner=0}}).ToArray();
        Check(!NativeDeathKnightHeroPowerPlayback.TryGetPair(newState.events[0],oldState,newState,out ignored),
            "rejects ambiguous duplicate summons");
    }

    static void RunActorRestoreChecks()
    {
        var parent=new GameObject("H11 original parent");var actor=new GameObject("H11 actor");
        var sibling=new GameObject("H11 sibling");var owner=new GameObject("H11 Spell owner");var token=new GameObject("H11 token root");
        try
        {
            actor.transform.SetParent(parent.transform,false);sibling.transform.SetParent(parent.transform,false);
            actor.transform.localPosition=new Vector3(2,3,4);actor.transform.localRotation=Quaternion.Euler(5,6,7);actor.transform.localScale=new Vector3(1.2f,.8f,1.1f);
            owner.transform.SetParent(actor.transform,false);owner.transform.localPosition=new Vector3(.2f,.3f,.4f);
            token.transform.SetParent(owner.transform,false);
            var originalPosition=actor.transform.localPosition;var originalRotation=actor.transform.localRotation;var originalScale=actor.transform.localScale;
            int originalSibling=actor.transform.GetSiblingIndex();var spawn=new NativeCustomSpawn();
            Check(spawn.Initialize(owner,actor,token,true,true,false)&&actor.transform.parent==token.transform&&owner.transform.parent==parent.transform,
                "CustomSpawn borrows the authoritative Actor only under its source token after moving Spell owner");
            spawn.Restore();
            Check(actor.transform.parent==parent.transform&&owner.transform.parent==actor.transform&&actor.transform.GetSiblingIndex()==originalSibling,
                "CustomSpawn restores original Actor and Spell hierarchy on route disposal");
            Check(actor.transform.localPosition==originalPosition&&actor.transform.localRotation==originalRotation&&actor.transform.localScale==originalScale,
                "CustomSpawn restores original Actor pose and scale");
        }
        finally
        {
            if(token!=null)UnityEngine.Object.DestroyImmediate(token);
            if(owner!=null)UnityEngine.Object.DestroyImmediate(owner);
            if(actor!=null)UnityEngine.Object.DestroyImmediate(actor);
            if(sibling!=null)UnityEngine.Object.DestroyImmediate(sibling);
            if(parent!=null)UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    static void RunTimelineSoundChecks()
    {
        string root=Path.Combine(Path.GetTempPath(),"HearthstoneH11Sound-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var owner=new GameObject("H11 Timeline sound node");
        try
        {
            File.WriteAllText(Path.Combine(root,"GhoulCharge_Token_Sound.wav"),"");
            var program=new NativeProgram{nodes=new NativeNode[21]};program.nodes[20]=new NativeNode{name=owner.name};
            var timeline=new NativeTimeline{node=1,wrap=2,duration=5};
            var track=new NativeTimelineTrack{kind="SoundTimelineTrack",node=20,clips=new[]{new NativeTimelineClip{start=0,duration=5,speed=1,clip_in=0,pre=0,post=0}}};
            var definition=new NativeAudioDefinition{
                nodes=new[]{new NativeAudioNode{node=20,node_known=true,path="source/20",audio_sources=new[]{new NativeAudioSourceData{values=new NativeAudioSourceValues{m_audioClip=new NativeAudioPointer{m_PathID=0},m_Volume=1,m_Pitch=1}}},
                    sound_defs=new[]{new NativeSoundData{values=new NativeSoundValues{m_AudioClip="token-ref",m_RandomVolumeMin=1,m_RandomVolumeMax=1,m_RandomPitchMin=1,m_RandomPitchMax=1}}}}},
                clips=new[]{new NativeAudioClipRef{reference="token-ref",file="GhoulCharge_Token_Sound.wav"}}};
            var cue=NativeDeathKnightHeroPowerPlayback.ResolveTimelineSoundCue(program,definition,root,timeline,track,owner,0,()=>0);
            Check(cue!=null&&cue.file=="GhoulCharge_Token_Sound.wav"&&cue.sourceParent==owner.transform,
                "Timeline node 20 resolves its own SoundDef and binds the cue to its source parent");
            definition.nodes[0].audio_sources[0].values.Loop=true;
            Check(NativeDeathKnightHeroPowerPlayback.ResolveTimelineSoundCue(program,definition,root,timeline,track,owner,0,()=>0)==null,
                "H11 rejects looping token source audio so the source lease can end with playback");
            definition.nodes[0].audio_sources[0].values.Loop=false;
            var button=new NativeSoundAsset{values=new NativeSoundValues{m_AudioClip="button-ref",m_RandomVolumeMin=1,m_RandomVolumeMax=1,m_RandomPitchMin=1,m_RandomPitchMax=1},
                clips=new[]{new NativeAudioClipRef{reference="button-ref",file="GhoulCharge_Button_Sound.wav"}}};
            var buttonCue=NativeAudio.ResolveClip(button,0,1,1,0,()=>0);
            Check(buttonCue!=null&&buttonCue.file=="GhoulCharge_Button_Sound.wav"&&cue.file!=buttonCue.file,
                "Timeline token sound remains distinct from the FSM button sound");
            track.clips[0].start=.1f;
            Check(NativeDeathKnightHeroPowerPlayback.ResolveTimelineSoundCue(program,definition,root,timeline,track,owner,0,()=>0)==null,
                "rejects a Timeline sound whose clip start no longer matches the source cache");
        }
        finally{UnityEngine.Object.DestroyImmediate(owner);if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    static void RunDeferredEntryChecks(string root)
    {
        var runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime,"registry.json"),"{\"entries\":[]}");
        var nodes=new[]{new NativeNode{name="H11 check root",parent=-1,active=true,materials=new NativeMaterial[0]},
            new NativeNode{name="H11 check owner",parent=-1,active=true,materials=new NativeMaterial[0]}};
        var side=new NativeAction{name="SpellPlayerSideEventAction",@params=new[]{
            new NativeParam{name="m_SpellObject",kind=20,node=1},new NativeParam{name="m_FriendlyEvent",kind=23,text="Friendly"},
            new NativeParam{name="m_OpponentEvent",kind=23,text="Opponent"}}};
        var machine=new NativeMachine{owner_known=true,owner=1,start="State 1",floats=new NativeFloat[0],colors=new NativeColor[0],
            vectors=new NativeVector[0],textures=new NativeTextureVariable[0],events=new[]{new NativeTransition{@event="Action",state="ActionState"}},
            states=new[]{new NativeState{name="State 1",actions=new[]{side},transitions=new[]{
                new NativeTransition{@event="Friendly",state="Friendly Position"},new NativeTransition{@event="Opponent",state="Opponent Position"}}},
                new NativeState{name="Friendly Position",actions=new NativeAction[0],transitions=new NativeTransition[0]},
                new NativeState{name="Opponent Position",actions=new NativeAction[0],transitions=new NativeTransition[0]},
                new NativeState{name="ActionState",actions=new[]{new NativeAction{name="SpellStateAction",@params=new NativeParam[0]}},transitions=new NativeTransition[0]}}};
        var program=new NativeProgram{nodes=nodes,machines=new[]{machine},animations=new NativeAnimation[0],timelines=new NativeTimeline[0]};
        var actor=new GameObject("H11 deferred Actor");
        try
        {
            int sideResolverCalls=0;
            using(var effect=new NativeEffectInstance(program,new AssetRegistry(root),0,16,16,deferEntryEvents:true))
            {
                effect.SpellPlayerSideEventResolver=(machineIndex,action)=>
                {sideResolverCalls++;return machineIndex==0&&action==side?"Friendly":null;};
                Check(effect.ActionExecutions.Count==0&&sideResolverCalls==0,
                    "deferred NativeEffectInstance construction executes no FSM actions before attachment");
                effect.AttachToOriginalParent(actor);
                Check(effect.Root.transform.parent==actor.transform,"H11 Actor is the NativeEffect root parent before initial entry");
                effect.Seek(0);
                Check(sideResolverCalls==1&&effect.SpellPlayerSideEventDispatches==1&&effect.UnboundSpellPlayerSideEventActions==0&&
                    effect.ActionExecutions["SpellPlayerSideEventAction"]==1,
                    "first post-attachment seek dispatches exactly one source Friendly/Opponent side event");
                Check(effect.DispatchMachineEvent(0,"Action"),"H11's explicit Action event follows the side-local transition");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }

    static NativeAction PositionAction(string vectorName,Vector3 value)
    {
        return new NativeAction{name="SetPosition",@params=new[]{
            new NativeParam{name="gameObject",kind=20,node=1,variable="HeroPowerFX"},
            new NativeParam{name="vector",kind=28,variable=vectorName,vector=value,none_known=true},
            new NativeParam{name="x",kind=15,none_known=true,is_none=true},
            new NativeParam{name="y",kind=15,none_known=true,is_none=true},
            new NativeParam{name="z",kind=15,none_known=true,is_none=true},
            new NativeParam{name="space",kind=7,number=0},
            new NativeParam{name="everyFrame",kind=1,number=0},
            new NativeParam{name="lateUpdate",kind=1,number=0},
        }};
    }

    static NativeProgram PositionRouteProgram(bool unknownOpponent)
    {
        var side=new NativeAction{name="SpellPlayerSideEventAction",@params=new[]{
            new NativeParam{name="m_SpellObject",kind=20,node=1},
            new NativeParam{name="m_FriendlyEvent",kind=23,text="Friendly"},
            new NativeParam{name="m_OpponentEvent",kind=23,text="Opponent"}}};
        var friendly=new Vector3(-5.78662014f,.300000012f,-8.09019184f);
        var opponent=new Vector3(-5.78112984f,.300000012f,-1.41999996f);
        var vectors=new List<NativeVector>{new NativeVector{name="FriendlyHP",value=friendly},new NativeVector{name="OpponentHP",value=opponent}};
        var machine=new NativeMachine{owner_known=true,owner=1,start="State 1",floats=new NativeFloat[0],colors=new NativeColor[0],
            vectors=vectors.ToArray(),textures=new NativeTextureVariable[0],events=new NativeTransition[0],states=new[]{
                new NativeState{name="State 1",actions=new[]{side},transitions=new[]{
                    new NativeTransition{@event="Friendly",state="Friendly Position"},
                    new NativeTransition{@event="Opponent",state="Opponent Position"}}},
                new NativeState{name="Friendly Position",actions=new[]{PositionAction("FriendlyHP",friendly)},transitions=new NativeTransition[0]},
                new NativeState{name="Opponent Position",actions=new[]{PositionAction(unknownOpponent?"MissingHP":"OpponentHP",opponent)},transitions=new NativeTransition[0]},
            }};
        return new NativeProgram{nodes=new[]{new NativeNode{name="H11 route root",parent=-1,active=true,materials=new NativeMaterial[0]},
            new NativeNode{name="H11 position target",parent=-1,active=true,materials=new NativeMaterial[0]}},
            machines=new[]{machine},animations=new NativeAnimation[0],timelines=new NativeTimeline[0]};
    }

    static void RunPositionVectorChecks(string root)
    {
        var actor=new GameObject("H11 position Actor");
        try
        {
            foreach(var side in new[]{Tuple.Create("Friendly",new Vector3(-5.78662014f,.300000012f,-8.09019184f)),
                                      Tuple.Create("Opponent",new Vector3(-5.78112984f,.300000012f,-1.41999996f))})
            using(var effect=new NativeEffectInstance(PositionRouteProgram(false),new AssetRegistry(root),0,16,16,deferEntryEvents:true))
            {
                effect.SpellPlayerSideEventResolver=(machine,action)=>machine==0&&action.name=="SpellPlayerSideEventAction"?side.Item1:null;
                effect.AttachToOriginalParent(actor);effect.Seek(0);
                Check(effect.SpellPlayerSideEventDispatches==1&&effect.UnhandledActions==0&&
                    effect.Nodes[1].transform.position==side.Item2,
                    "source " + side.Item1 + " side state resolves its defined position vector");
            }
            using(var effect=new NativeEffectInstance(PositionRouteProgram(true),new AssetRegistry(root),0,16,16,deferEntryEvents:true))
            {
                effect.SpellPlayerSideEventResolver=(machine,action)=>machine==0&&action.name=="SpellPlayerSideEventAction"?"Opponent":null;
                effect.AttachToOriginalParent(actor);effect.Seek(0);
                Check(effect.UnhandledActions==1&&effect.UnhandledActionDetails.Single().Contains("state=Opponent Position action=SetPosition")&&
                    effect.Nodes[1].transform.position==Vector3.zero,
                    "unknown SetPosition vector names fail closed without moving the target");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }

    static void AddDependency(HashSet<string> files,string path)
    {if(!String.IsNullOrEmpty(path))files.Add(path);}

    static void CopyDependency(string sourceRoot,string targetRoot,string path)
    {
        if(String.IsNullOrEmpty(path)||Path.IsPathRooted(path))throw new InvalidDataException("H11 cache dependency is not relative: "+path);
        var sourceBase=Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var source=Path.GetFullPath(Path.Combine(sourceBase,path));
        if(!source.StartsWith(sourceBase,StringComparison.OrdinalIgnoreCase)||!File.Exists(source))
            throw new FileNotFoundException("Missing H11 exported cache dependency: "+path,source);
        var target=Path.Combine(targetRoot,path);
        Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,true);
    }

    static void CopyPositiveRouteCache(string sourceRoot,string targetRoot,string programFile,NativeProgram program)
    {
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase){programFile,"registry.json"};
        AddDependency(files,program.audio_file);AddDependency(files,program.event_audio_file);
        foreach(var node in program.nodes??new NativeNode[0])
        {
            AddDependency(files,node.mesh);
            if(node.particle!=null){AddDependency(files,node.particle.shape==null?null:node.particle.shape.mesh);AddDependency(files,node.particle.render==null?null:node.particle.render.mesh);}
            foreach(var material in node.materials??new NativeMaterial[0])
                foreach(var texture in material.textures??new NativeTexture[0])AddDependency(files,texture.path);
        }
        foreach(var machine in program.machines??new NativeMachine[0])
        {
            foreach(var texture in machine.textures??new NativeTextureVariable[0])AddDependency(files,texture.path);
            foreach(var action in (machine.states??new NativeState[0]).SelectMany(state=>state.actions??new NativeAction[0]))
                foreach(var parameter in action.@params??new NativeParam[0])
                    if(!String.IsNullOrEmpty(parameter.asset_path)&&File.Exists(Path.Combine(sourceRoot,parameter.asset_path)))
                        AddDependency(files,parameter.asset_path);
        }
        foreach(var file in files.Where(file=>file.EndsWith(".native-sound.json",StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var sound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(sourceRoot,file)));
            foreach(var clip in sound.clips??new NativeAudioClipRef[0])AddDependency(files,clip.file);
        }
        foreach(var file in new[]{program.audio_file,program.event_audio_file}.Where(file=>!String.IsNullOrEmpty(file)))
        {
            var audio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(sourceRoot,file)));
            foreach(var clip in audio.clips??new NativeAudioClipRef[0])AddDependency(files,clip.file);
        }
        foreach(var file in files)CopyDependency(sourceRoot,targetRoot,file);
    }

    static NativeEffectInstance ActiveInstance(NativeEffectPlayer player,string key)
    {
        var field=typeof(NativeEffectPlayer).GetField("active",BindingFlags.Instance|BindingFlags.NonPublic);
        var active=field==null?null:field.GetValue(player) as Dictionary<string,NativeEffectInstance>;
        NativeEffectInstance effect;return active!=null&&active.TryGetValue(key,out effect)?effect:null;
    }

    public static void RunEnabledRoute()
    {
        passed=0;
        var repository=Path.GetFullPath(Path.Combine(Application.dataPath,"..",".."));
        var sourceRoot=Path.Combine(repository,"local","runtime");
        const string programFile="40ac8d77701fdbccc4033483.hero-power-source.json";
        var productionIndex=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(sourceRoot,"native-effect-index.json")));
        var productionEntry=(productionIndex.entries??new NativeEffectEntry[0]).Single(entry=>entry.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId);
        Check(productionEntry.enabled&&Mathf.Abs(productionEntry.duration-5f)<.001f&&!productionEntry.replace&&
            productionEntry.file==programFile&&productionEntry.impact_delay==-1f,
            "production H11 index matches the locally verified five-second CustomSpawn contract");
        Check(NativeDeathKnightHeroPowerPlayback.Supports(new AssetRegistry(Path.Combine(repository,"local")),productionEntry),
            "production H11 configuration passes the unchanged source identity and dependency gate");

        var root=Path.Combine(Path.GetTempPath(),"HearthstoneH11Enabled-"+Guid.NewGuid().ToString("N"));
        var runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);
        var parent=new GameObject("H11 positive original parent");var actor=new GameObject("H11 positive authoritative Actor");
        var cameraObject=new GameObject("H11 positive timeline camera");cameraObject.AddComponent<Camera>();
        actor.transform.SetParent(parent.transform,false);actor.transform.localPosition=new Vector3(2,3,4);
        actor.transform.localRotation=Quaternion.Euler(5,6,7);actor.transform.localScale=new Vector3(1.2f,.8f,1.1f);
        actor.AddComponent<NativeActorIdentity>();actor.AddComponent<NativeActorPresentation>().Configure(actor);
        var originalPosition=actor.transform.localPosition;var originalRotation=actor.transform.localRotation;var originalScale=actor.transform.localScale;
        var originalParent=actor.transform.parent;
        var audioCues=new List<NativeAudioCue>();int actorReads=0;string fallbackWarning=null;
        Application.LogCallback warning=(condition,stack,type)=>
        {if(type==LogType.Warning&&condition.Contains("H11 CustomSpawn fallback"))fallbackWarning=condition;};
        try
        {
            var sourceProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(sourceRoot,programFile)));
            Check(sourceProgram!=null&&sourceProgram.nodes!=null&&sourceProgram.nodes.Length==21,
                "positive route fixture reads the real exported H11 source cache");
            Check(NativeEffectInstance.NodeBelongsToSubtree(sourceProgram,17,17)&&
                NativeEffectInstance.NodeBelongsToSubtree(sourceProgram,18,17)&&
                NativeEffectInstance.NodeBelongsToSubtree(sourceProgram,19,17)&&
                !NativeEffectInstance.NodeBelongsToSubtree(sourceProgram,5,17)&&
                !NativeEffectInstance.NodeBelongsToSubtree(sourceProgram,20,17),
                "real source hierarchy separates the button smoke/motes from ghoul ground burst and token audio");
            CopyPositiveRouteCache(sourceRoot,runtime,programFile,sourceProgram);
            File.WriteAllText(Path.Combine(runtime,"native-effect-index.json"),
                "{\"entries\":[{\"card_id\":\"HERO_11bp\",\"file\":\""+programFile+"\",\"enabled\":true,\"duration\":5,\"replace\":false,\"impact_delay\":-1}]}");

            var before=State(0,1,false);var after=State(0,1);after.events=Events(0);
            var cue=new SemanticEvent{type="SpellEffect",card_id=NativeDeathKnightHeroPowerPlayback.HeroPowerId,source=101,target=301,owner=0};
            var missing=State(0,1,false);missing.events=Events(0);
            var duplicate=State(0,1);duplicate.events=Events(0).Concat(new[]{new SemanticEvent{type="Summon",source=101,target=301,owner=0}}).ToArray();
            var pairSummon=after.events.Single(item=>item.type=="Summon");
            GameObject resolvedActor=null;
            using(var player=new NativeEffectPlayer(new AssetRegistry(root)))
            {
                player.TimelineCamera=cameraObject.GetComponent<Camera>();
                player.AudioCue=audioCues.Add;
                player.ActorObject=entity=>{actorReads++;return entity!=null&&entity.entity==301?resolvedActor:null;};
                resolvedActor=actor;
                Check(player.Duration(NativeDeathKnightHeroPowerPlayback.HeroPowerId)==5,
                    "temporary enabled index passes the source-bound H11 profile gate");
                Check(!player.PrepareSource("h11-missing-pair",cue,before,missing,false,0),
                    "actual NativeEffectPlayer rejects a missing authoritative ghoul entity");
                Check(!player.PrepareSource("h11-duplicate-pair",cue,before,duplicate,false,0),
                    "actual NativeEffectPlayer rejects duplicate source Summons");
                Check(actorReads==0&&player.ActiveCount==0&&audioCues.Count==0,
                    "missing and duplicate pairs fail before Actor borrowing or audio dispatch");
                NativeDeathKnightHeroPowerPlayback.Pair pair;
                Check(NativeDeathKnightHeroPowerPlayback.TryGetPairForPowerCue(cue,before,after,out pair)&&
                    NativeDeathKnightHeroPowerPlayback.ShouldSkipGenericSummon(true,pairSummon,before,after)&&
                    !NativeDeathKnightHeroPowerPlayback.ShouldSkipGenericSummon(false,pairSummon,before,after),
                    "the accepted real event pair alone controls H11 summon suppression");
                Application.logMessageReceived+=warning;
                bool prepared=player.PrepareSource("h11-positive",cue,before,after,false,0);
                Check(prepared,"actual NativeEffectPlayer prepares the source pair: "+(fallbackWarning??"no source failure reported"));
                Check(actorReads==1&&player.ActiveCount==1&&player.HasDeathKnightRouteFor(pair),
                    "positive route borrows the exact authoritative HERO_11bpt Actor");
                var effect=ActiveInstance(player,"h11-positive");
                Check(effect!=null&&effect.SpellPlayerSideEventDispatches==1&&effect.UnboundSpellPlayerSideEventActions==0&&
                    effect.ActionExecutions.ContainsKey("SpellPlayerSideEventAction")&&effect.ActionExecutions["SpellPlayerSideEventAction"]==1,
                    "real source FSM dispatches the Opponent side event once before Action");
                Check(effect.CustomSpawnInitializations==1&&actor.transform.parent==effect.Nodes[3].transform&&
                    actor.transform.Find("NativeEffect")==effect.Root.transform,
                    "real CustomSpawn attaches the NativeEffect root and authoritative Actor to source node 3 before the source wait");
                var actionState=sourceProgram.machines.Single().states.Single(state=>state.name=="ActionState");
                var sourceWait=actionState.actions.Single(action=>action.name=="Wait").Param("time").number;
                Check(sourceWait>0&&!float.IsNaN(sourceWait)&&!float.IsInfinity(sourceWait),
                    "positive route advances using the imported ActionState Wait value");
                float spawnWait=sourceProgram.machines.Single().states.Single(state=>state.name=="Minion Spawn")
                    .actions.Single(action=>action.name=="Wait").Param("time").number;
                float revealAge=sourceWait+spawnWait;
                float audioGrace=NativeDeathKnightHeroPowerPlayback.AudioGraceSeconds;
                Check(Mathf.Abs(revealAge-.5f)<.001f&&Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(revealAge)-revealAge)<.00001f&&
                      Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(1f)-1f)<.00001f&&
                      Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(NativeDeathKnightHeroPowerPlayback.AudioCompleteAge)-
                          NativeDeathKnightHeroPowerPlayback.AudioCompleteAge)<.00001f&&audioGrace>=0&&
                      Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(
                          NativeDeathKnightHeroPowerPlayback.IdentityThroughAge)-5f)<.00001f,
                    "H11 clock preserves birth, reveal, finalize, token audio and measured DSP queue drain before skipping its empty tail");
                float heldAge=NativeDeathKnightHeroPowerPlayback.IdentityThroughAge+.25f;
                Check(Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(heldAge,true)-heldAge)<.00001f&&
                      Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(heldAge,false)-5f)<.00001f&&
                      Mathf.Abs(NativeDeathKnightHeroPowerPlayback.PlaybackAge(20,true)-4.999f)<.00001f,
                    "H11 preserves source age while the real token cue plays, then completes only after that cue ends");
                float sampleAge=sourceWait+1f/120;
                Check(player.PrepareSource("h11-positive",cue,before,after,false,sampleAge),
                    "source timeline advances only after the imported FSM wait");
                var groundPass=effect.RenderAffineExcludingSubtree(sampleAge,17);
                var buttonPass=effect.RenderAffineSubtree(sampleAge,17,1.25f);
                Check(groundPass!=null&&buttonPass!=null&&groundPass.width>0&&buttonPass.width>0,
                    "actual source render produces separate ground-emergence and button-smoke layers at the same timeline age");
                var audio=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(runtime,sourceProgram.audio_file)));
                var tokenNode=NativeAudio.Node(audio,sourceProgram.nodes[20].path,20);
                var tokenReference=tokenNode.sound_defs.Single().values.m_AudioClip;
                var tokenFile=audio.clips.Single(clip=>clip.reference==tokenReference).file;
                var tokenCues=audioCues.Where(item=>item.file==tokenFile).ToArray();
                var buttonAction=actionState.actions.Single(action=>action.name=="AudioPlayClipAction");
                var buttonSound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(runtime,buttonAction.Param("m_Sound").asset_path)));
                var buttonFile=buttonSound.clips.Single(clip=>clip.reference==buttonSound.values.m_AudioClip).file;
                var buttonCues=audioCues.Where(item=>item.file==buttonFile).ToArray();
                Check(effect.TimelineStarts==1&&effect.TimelineSoundStarts==1&&effect.UnboundTimelineSounds==0&&
                    tokenCues.Length==1&&tokenCues[0].sourceParent==null&&tokenCues[0].trackedSource==effect.Nodes[20]&&
                    tokenCues[0].stagingBindingKnown&&tokenCues[0].stagedCloneSource&&tokenCues[0].stopWhenSourceInactive&&
                    tokenCues[0].stagingOrigin==effect.StagingOrigin,
                    "real node 20 TimelineTrack emits one token cue tracked to its exact staged source node and frozen origin");
                Check(buttonFile!=tokenFile&&buttonCues.Length==1&&buttonCues[0].file==buttonFile&&
                    buttonCues[0].time==0&&tokenCues[0].time==sourceWait,
                    "real FSM button cue and node 20 Timeline token cue are distinct source clips");
                Check(NativeDeathKnightHeroPowerPlayback.TokenAudioCue(effect)==tokenCues[0],
                    "actual H11 playback identifies its unique source-bound token Timeline cue");
                var actorPresentation=actor.GetComponent<NativeActorPresentation>();
                Check(player.PrepareSource("h11-positive",cue,before,after,false,revealAge-1f/120)&&
                      actorPresentation!=null&&!actorPresentation.Shown,
                    "the real Actor stays hidden until the imported 0.5-second reveal boundary");
                Check(player.PrepareSource("h11-positive",cue,before,after,false,revealAge)&&actorPresentation.Shown,
                    "the real Actor is revealed at the authored 0.5-second boundary");
                var displayedEntities=new HashSet<int>{301,302};
                player.EntityPresentationPresent=entity=>displayedEntities.Contains(entity);
                Check(NativeEffectPlayer.ShouldIncludeDeathKnightBorrowedActor(pair.ghoul.entity,player.EntityPresentationPresent)&&
                      NativeEffectPlayer.ShouldIncludeDeathKnightBorrowedActor(pair.ghoul.entity,null),
                    "H11 keeps the borrowed Actor in the source capture during birth and when Editor has no display callback");
                displayedEntities.Remove(pair.ghoul.entity);
                Check(!NativeEffectPlayer.ShouldIncludeDeathKnightBorrowedActor(pair.ghoul.entity,player.EntityPresentationPresent)&&
                      NativeEffectPlayer.ShouldIncludeDeathKnightBorrowedActor(302,player.EntityPresentationPresent)&&actorPresentation.Shown,
                    "H11 excludes only the removed paired Actor from source capture after death without hiding it globally or excluding other entities");
                displayedEntities.Add(pair.ghoul.entity);
                float finalizeAge=revealAge+.5f;
                Check(player.PrepareSource("h11-positive",cue,before,after,false,finalizeAge-1f/120)&&effect.CustomSpawnFinalizations==0,
                    "CustomSpawn remains active through the authored half-second reveal hold");
                Check(player.PrepareSource("h11-positive",cue,before,after,false,finalizeAge)&&effect.CustomSpawnFinalizations==1,
                    "CustomSpawn finalizes at the source-derived 1.0-second boundary");
                bool tokenPlaying=true;tokenCues[0].playbackActive=()=>tokenPlaying;
                Check(player.PrepareSource("h11-positive",cue,before,after,false,heldAge)&&
                      Mathf.Abs(player.GetDeathKnightPlaybackAge("h11-positive",heldAge)-heldAge)<.00001f&&
                      player.SourceBusy("h11-positive"),
                    "NativeEffectPlayer retains the H11 route at the short input-independent audio tail");
                tokenPlaying=false;
                Check(player.PrepareSource("h11-positive",cue,before,after,false,heldAge+.01f)&&
                      Mathf.Abs(player.GetDeathKnightPlaybackAge("h11-positive",heldAge+.01f)-5f)<.00001f&&
                      !player.SourceBusy("h11-positive"),
                    "NativeEffectPlayer releases the H11 route after the actual token playback ends");
                player.Dispose();
                Check(actor.transform.parent==originalParent&&actor.transform.localPosition==originalPosition&&
                    actor.transform.localRotation==originalRotation&&actor.transform.localScale==originalScale,
                    "positive route disposal restores the borrowed Actor parent and pose");
            }
            Application.logMessageReceived-=warning;
            Debug.Log("NATIVE DEATH KNIGHT HERO POWER POSITIVE: "+passed+" source-route checks passed");
        }
        finally
        {
            Application.logMessageReceived-=warning;
            UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(parent);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    public static void RunDisabledRoute()
    {
        passed=0;
        RunPairChecks();
        RunActorRestoreChecks();RunTimelineSoundChecks();
        string root=Path.Combine(Path.GetTempPath(),"HearthstoneH11Route-"+Guid.NewGuid().ToString("N"));
        string runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);
        int actorReads=0,powerTaskReads=0,fallbackWarnings=0;
        Application.LogCallback warning=(condition,stack,type)=>
        {if(type==LogType.Warning&&condition.Contains("Native effect prepare fallback HERO_11bp"))fallbackWarnings++;};
        try
        {
            File.WriteAllText(Path.Combine(runtime,"registry.json"),"{\"entries\":[]}");
            RunDeferredEntryChecks(root);
            RunPositionVectorChecks(root);
            File.WriteAllText(Path.Combine(runtime,"native-effect-index.json"),
                "{\"entries\":[{\"card_id\":\"HERO_11bp\",\"file\":\"disabled-fixture.json\",\"enabled\":false,\"duration\":5,\"replace\":false}]}");
            using(var disabled=new NativeEffectPlayer(new AssetRegistry(root)))
            {
                disabled.ActorObject=entity=>{actorReads++;return null;};
                Check(disabled.Duration("HERO_11bp")==0&&
                    !disabled.PrepareSource("h11-disabled",new SemanticEvent{type="SpellEffect",card_id="HERO_11bp"},null,null,false,0)&&
                    disabled.ActiveCount==0&&actorReads==0,
                    "an isolated disabled fixture is rejected before source or Actor construction independently of production policy");
            }
            File.WriteAllText(Path.Combine(runtime,"native-effect-index.json"),
                "{\"entries\":[{\"card_id\":\"HERO_11bp\",\"file\":\"source-only.json\",\"enabled\":true,\"duration\":4,\"replace\":true}]}");
            using(var player=new NativeEffectPlayer(new AssetRegistry(root)))
            {
                player.CardType=id=>1;
                player.ActorObject=entity=>{actorReads++;return null;};
                player.OriginalPowerTasks=(data,before,after)=>{powerTaskReads++;return null;};
                player.SuperSpellScene=(data,before,after,viewport,origin)=>null;
                Application.logMessageReceived+=warning;
                Check(player.Duration("HERO_11bp")==0,
                    "an enabled index row cannot open the unreviewed CustomSpawn route");
                Check(!player.PrepareSource("h11-source",new SemanticEvent{type="SpellEffect",card_id="HERO_11bp"},null,null,false,0),
                    "source preparation fails closed before constructing the native effect instance");
                Check(Event.current==null,"disabled H11 draw contract runs without a GUI Event");
                Check(!player.Draw("h11-source",new SemanticEvent{type="SpellEffect",card_id="HERO_11bp"},null,null,new Rect(0,0,200,200),0),
                    "direct draw cannot bypass the disabled H11 factory gate");
                Check(player.ActiveCount==0&&actorReads==0&&powerTaskReads==0&&fallbackWarnings==0,
                    "a stale enabled index must not construct a cue, resolve an Actor, read task history, or fall through to factory errors");
                Application.logMessageReceived-=warning;
            }
            Debug.Log("NATIVE DEATH KNIGHT HERO POWER: "+passed+" disabled-route checks passed");
        }
        finally
        {
            Application.logMessageReceived-=warning;
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
