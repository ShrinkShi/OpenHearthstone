using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativePriestHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Priest hero power: "+reason);passed++;}
    static bool SamePosition(Vector3 left,Vector3 right)
    {return Vector3.Distance(left,right)<.001f;}
    static int ActionCount(NativeEffectInstance effect,string action)
    {int count;return effect.ActionExecutions.TryGetValue(action,out count)?count:0;}
    static int SchedulerActive(NativeSuperSpell flow)
    {
        var field=typeof(NativeSuperSpell).GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic);
        var scheduler=field==null?null:field.GetValue(flow) as NativePresentationCoroutines;
        return scheduler==null?-1:scheduler.Active;
    }
    static string SourceSnapshot(string point,NativeSuperSpellPlayback spell,NativeAreaInstance startClone)
    {
        var flow=spell.Flow;
        string clones=String.Join("; ",flow.Instances.Select(instance=>instance.Effect.Program.reference+
            " sourceState="+instance.Effect.ActiveSourceSpellState+" fsm="+String.Join(",",instance.Effect.CurrentStates)+
            " wait="+ActionCount(instance.Effect,"Wait")+" cardAudio="+ActionCount(instance.Effect,"SpellCardIdAudioPlayAction")));
        return "H09 source clock "+point+": time="+flow.Time.ToString("F3",CultureInfo.InvariantCulture)+
            " pending="+flow.Pending+" activeClones="+flow.ActiveClones+" schedulerActive="+SchedulerActive(flow)+
            " faulted="+flow.Faulted+" startSourceState="+startClone.Effect.ActiveSourceSpellState+
            " startFsm="+String.Join(",",startClone.Effect.CurrentStates)+" startWait="+ActionCount(startClone.Effect,"Wait")+
            " startClipActions="+ActionCount(startClone.Effect,"AudioPlayClipAction")+" clones=["+clones+"] events=["+
            String.Join(" | ",spell.Events)+"]";
    }
    static NativeProgram Copy(NativeProgram value)
    {return JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(value));}
    static NativeSuperSpellConfig Copy(NativeSuperSpellConfig value)
    {return JsonUtility.FromJson<NativeSuperSpellConfig>(JsonUtility.ToJson(value));}
    static StateView State(int version)
    {
        return new StateView{match_id="priest-targeted-check",version=version,seat=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_09"},power=new EntityView{entity=2,card_id="HERO_09bp"},
                field=new EntityView[0],hand=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_02"},power=new EntityView{entity=4,card_id="HERO_02bp"},
                field=new[]{new EntityView{entity=20,card_id="CS2_119"}},hand=new EntityView[0]}}};
    }
    static NativeSuperSpellScene Scene(SemanticEvent cue,StateView before,StateView after)
    {
        return NativeSuperSpellBoard.FromPresentation(cue,before,after,
            id=>id==1?BattleLayout.Hero(true).center:id==2?BattleLayout.Power(true).center:
                id==3?BattleLayout.Hero(false).center:id==4?BattleLayout.Power(false).center:
                id==20?new Vector2(900,350):(Vector2?)null,
            new Rect(0,0,1920,1080),new Vector3(0,0,0),id=>id=="HERO_09bp"||id=="HERO_02bp"?10:
                id=="HERO_09"||id=="HERO_02"?3:4);
    }
    public static void RunSourceRoute()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_09bp");
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_09bp"&&row.file==entry.file);
        var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        var startAsset=catalog.prefabs.Single(asset=>asset.reference==config.start.prefab_reference);
        var impactAsset=catalog.prefabs.Single(asset=>asset.reference==config.impact.prefab.prefab_reference);
        var start=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,startAsset.file)));
        var impact=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,impactAsset.file)));
        Check(!entry.enabled&&config.start_enabled&&!config.start_config_null&&config.start.prefab_reference==start.reference&&
            config.missile.m_Enabled==0&&String.IsNullOrEmpty(config.missile_reference)&&config.impact.enabled&&
            Mathf.Approximately(config.impact.game_delay_min,.5f)&&Mathf.Approximately(config.impact.game_delay_max,.5f),
            "source import retains the authored Start, null disabled Missile and 0.5 second Impact delay");
        Check(NativeSuperSpell.SupportsPriestNoMissileTargeted(config),
            "reviewed targeted no-Missile CardDef profile passes its strict gate");
        Check(NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,start,impact,assets.Root),
            "reviewed root, Start, Impact and audio sources pass their strict gate");

        var missingStart=Copy(config);missingStart.start=null;
        Check(!NativeSuperSpell.SupportsPriestNoMissileTargeted(missingStart),"missing Start fails closed");
        var enabledMissile=Copy(config);enabledMissile.missile.m_Enabled=1;
        Check(!NativeSuperSpell.SupportsPriestNoMissileTargeted(enabledMissile),"enabled Missile fails closed");
        var unsupported=Copy(start);unsupported.unknown_actions=unsupported.unknown_actions.Concat(new[]{"UnsupportedAction"}).ToArray();
        Check(!NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,unsupported,impact,assets.Root),
            "unsupported Start actions fail closed");
        var wrongColor=Copy(start);wrongColor.machines.Single(machine=>machine.owner_known&&machine.owner==0)
            .states.Single(state=>state.name=="BirthState").actions.Single(action=>action.name=="SetColorValue")
            .Param("color").color=Color.white;
        Check(!NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,wrongColor,impact,assets.Root),
            "the SetColorValue output must retain the authored transparent color");
        var wrongVariable=Copy(start);wrongVariable.machines.Single(machine=>machine.owner_known&&machine.owner==0)
            .states.Single(state=>state.name=="BirthState").actions.Single(action=>action.name=="SetColorValue")
            .Param("colorVariable").color=Color.clear;
        Check(!NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,wrongVariable,impact,assets.Root),
            "the RaysGlow FSM variable must retain its source fallback color");
        var unresolved=Copy(impact);unresolved.machines[0].states.Single(state=>state.name=="DeathState").actions[0].@params[0].unresolved=true;
        Check(!NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,start,unresolved,assets.Root),
            "unresolved Impact action parameters fail closed");
        var wrongAudio=Copy(impact);wrongAudio.machines[0].states.Single(state=>state.name=="DeathState")
            .actions.Single(action=>action.name=="SpellCardIdAudioPlayAction").Param("m_DefaultSound").asset_path="missing.native-sound.json";
        Check(!NativeSuperSpellPlayback.SupportsPriestHeroPowerRoot(root,config,start,wrongAudio,assets.Root),
            "missing Impact default audio fails closed");

        var before=State(1);var after=State(2);after.events=new[]{
            new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},
            new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0},
            new SemanticEvent{type="Heal",source=2,target=20,amount=2,owner=0}};
        var visualCue=new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_09bp",owner=0};
        var scene=Scene(visualCue,before,after);
        try
        {
            Check(scene.Placement.source.card_id=="HERO_09bp"&&scene.Placement.Targets.GetTargets().Count==1&&
                scene.Placement.Targets.GetTarget()!=null&&scene.Placement.Targets.GetTarget().name.Contains("20"),
                "the selected visual target comes from matching same-batch HeroPowerTarget and Heal, not PlayHeroPower.target");
            Check(scene.Context.CardId(0)=="HERO_09bp"&&scene.Context.CardId(1)=="CS2_119",
                "audio selector context keeps the power as source and healed entity as target");
        }
        finally{scene.Dispose?.Invoke();}
        var legacy=State(2);legacy.events=new[]{
            new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},
            new SemanticEvent{type="Heal",source=2,target=20,amount=2,owner=0}};
        var legacyScene=Scene(visualCue,before,legacy);
        try{Check(legacyScene.Placement.Targets.GetTargets().Count==1&&legacyScene.Placement.Targets.GetTarget().name.Contains("20"),
            "legacy batches without HeroPowerTarget continue to resolve their unique actual Heal target");}
        finally{legacyScene.Dispose?.Invoke();}
        var fullHealth=State(2);fullHealth.events=new[]{
            new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},
            new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0}};
        var fullHealthScene=Scene(visualCue,before,fullHealth);
        try{Check(fullHealthScene.Placement.Targets.GetTargets().Count==1&&fullHealthScene.Placement.Targets.GetTarget().name.Contains("20")&&
                !fullHealth.events.Any(evt=>evt.type=="Heal"),
                "a full-health cast retains its authority-selected target without inventing a Heal event");}
        finally{fullHealthScene.Dispose?.Invoke();}
        var selectionCue=new SemanticEvent{type="HeroPowerSelection",source=2,card_id="HERO_09bp",owner=0};
        var selectionScene=Scene(selectionCue,before,before);NativeSuperSpellPlayback fullHealthPlayback=null;
        try
        {
            fullHealthPlayback=new NativeSuperSpellPlayback(root,entry,assets,24,selectionScene,true,null,null,null,selection:true);
            fullHealthPlayback.IdentifySelection(fullHealth.match_id);fullHealthPlayback.Cast(selectionCue,fullHealth);
            fullHealthPlayback.Seek(1.01f);
            fullHealthPlayback.Seek(1.02f);
            var fullHealthImpacts=fullHealthPlayback.Flow.Instances.Where(instance=>instance.Effect.Program.reference==impact.reference).ToArray();
            Check(fullHealthImpacts.Length==1&&fullHealthImpacts[0].Targets.GetTargets().Count==1&&
                fullHealthImpacts[0].Targets.GetTarget()==selectionScene.Placement.Targets.GetTarget()&&
                !fullHealth.events.Any(evt=>evt.type=="Heal"),
                "the production Start-to-Impact route emits the authored target Impact for a full-health cast with no Heal event");
        }
        finally
        {
            if(fullHealthPlayback!=null)fullHealthPlayback.Dispose();else selectionScene.Dispose?.Invoke();
        }
        foreach(var bad in new[]{
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp"},new SemanticEvent{type="Hit",source=2,target=20}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp"},new SemanticEvent{type="Heal",source=2,target=20},new SemanticEvent{type="Heal",source=2,target=1}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp"},new SemanticEvent{type="Heal",source=2,target=20},new SemanticEvent{type="Heal",source=2,target=20}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=1}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0},new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0},new SemanticEvent{type="Heal",source=2,target=1,amount=2,owner=0}},
            new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_09bp",owner=0},new SemanticEvent{type="HeroPowerTarget",source=2,target=20,card_id="HERO_09bp",owner=0},new SemanticEvent{type="Heal",source=2,target=20,amount=2,owner=0},new SemanticEvent{type="Heal",source=2,target=20,amount=2,owner=0}},
        })
        {
            var rejected=State(2);rejected.events=bad;
            bool failed=false;try{Scene(visualCue,before,rejected).Dispose?.Invoke();}catch(NotSupportedException){failed=true;}
            Check(failed,"missing, ambiguous, mismatched-owner, duplicate authority/Heal, or Heal-conflicting target evidence fails closed");
        }

        var presentation=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1:0,NativeActive=key=>true};
        presentation.Accept(before,after,new Dictionary<int,Rect>());
        var heal=presentation.Cues.Single(cue=>cue.data.type=="Heal");
        var native=presentation.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp");
        Check(presentation.Cues.Count(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp")==1&&
            native.apply==float.MaxValue&&heal.nativeImpact==null,
            "one decorative PlayHeroPower cue does not attach or replay the authoritative Heal");
        presentation.Tick(2,new Dictionary<int,Rect>());
        Check(!presentation.InputLocked,"the source FSM tail does not lock global input after the Heal timeline");

        var routeBefore=State(1);var routeAfter=State(2);routeAfter.events=after.events;
        routeBefore.moves=new[]{new Move{kind="power",entity=2,target=1}};
        var locations=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)},{20,new Rect(830,300,140,100)}};
        var routedAudio=new List<NativeAudioCue>();var preparedAges=new List<float>();NativeSuperSpellScene routedScene=null;
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id=="HERO_09bp"||id=="HERO_02bp"?10:id=="HERO_09"||id=="HERO_02"?3:4;
            player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>
            {
                routedScene=NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                    id=>id==1?BattleLayout.Hero(true).center:id==2?BattleLayout.Power(true).center:
                        id==3?BattleLayout.Hero(false).center:id==4?BattleLayout.Power(false).center:
                        id==20?new Vector2(900,350):(Vector2?)null,viewport,origin,player.CardType);
                return routedScene;
            };
            player.AudioCue=cue=>routedAudio.Add(cue);
            var indexField=typeof(NativeEffectPlayer).GetField("index",BindingFlags.Instance|BindingFlags.NonPublic);
            var indexed=(Dictionary<string,NativeEffectEntry>)indexField.GetValue(player);var priestEntry=indexed["HERO_09bp"];
            string priestFile=priestEntry.file;priestEntry.file="unsupported-priest-profile.json";
            Check(!player.SelectPower(routeBefore,2)&&!player.HasPowerSelection,
                "a missing H09 source profile fails closed before opening target selection");
            priestEntry.file=priestFile;
            Check(player.SelectPower(routeBefore,2)&&player.HasPowerSelection,
                "a legal own-hero H09 power move enters selection through the reviewed source profile");
            var selected=player.PowerSelection;
            Check(selected.CardId=="HERO_09bp"&&selected.SourceEntity==2&&selected.Flow!=null&&!selected.Flow.Faulted&&
                selected.Context.Placement.Targets.GetTargets().Count==0,
                "H09 source selection begins without inventing a Heal target");
            var selectedStart=selected.Flow.Instances.Single(instance=>instance.Effect.Program.reference==start.reference);
            Check(selectedStart.Targets.GetTargets().Count==0,
                "the selection-time Start clone is created before a Heal target exists");
            Check(selectedStart.Effect.StagingOrigin==selected.Effect.Root.transform.position,
                "the H09 Start clone registers the shared staging origin after its slot-root reanchor");
            Check(routedAudio.Count(audio=>audio.file=="0b172af97e533a48568c40c7.wav")==1,
                "the selected Start source clip is emitted once before the accepted authority cue");
            player.SubmitPowerSelection(2);player.ObservePowerState(routeAfter);
            var route=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};
            route.NativePrepare=(key,data,oldState,newState,area,age)=>
            {
                if(!player.PrepareSource(key,data,oldState,newState,area,age))
                    throw new Exception("Priest hero power route: production source cue did not prepare");
                preparedAges.Add(age);
            };
            route.Accept(null,routeBefore,locations);route.Accept(routeBefore,routeAfter,locations);
            var cue=route.Cues.Single(row=>row.data.type=="SpellEffect"&&row.data.card_id=="HERO_09bp");
            var healCue=route.Cues.Single(row=>row.data.type=="Heal"&&row.data.source==2&&row.data.target==20);
            string key=routeAfter.match_id+":"+routeAfter.version+":"+cue.data.source+":"+cue.start;
            var profile=CardEffectRegistry.Find("HERO_09bp");
            float expectedDuration=profile!=null&&profile.cast_duration>0?profile.cast_duration:Mathf.Clamp(entry.duration,.1f,8);
            Check(Mathf.Approximately(player.Duration("HERO_09bp"),expectedDuration)&&Mathf.Approximately(cue.duration,expectedDuration),
                "the SuperSpell classification keeps Priest's existing source duration rule instead of inheriting another hero's timing");
            Check(NativeSuperSpellBoard.Controller(cue.data,routeBefore,routeAfter)==0&&cue.data.owner==0,
                "the SuperSpell classification resolves Priest Friendly from the power entity's controller");
            Check(cue.data.source==2&&cue.data.target==20&&cue.apply==float.MaxValue&&route.Cues.Count(row=>row.data.type=="Heal")==1&&healCue.nativeImpact==null,
                "the production route carries the authority-selected target while the single Heal stays server-owned");
            Check(route.NativeKeys().SequenceEqual(new[]{key}),"Presentation exposes the H09 source under its normal native key");
            route.PrepareNative();
            var spell=player.SourceSpell(key);
            Check(spell!=null&&ReferenceEquals(spell,selected)&&spell.CardId=="HERO_09bp"&&spell.Flow!=null&&!spell.Flow.Faulted&&spell.SourceEntity==2&&
                spell.Flow.FirstArrivalTime==null&&spell.Flow.Flights.Count()==0,
                "the accepted source-only cue adopts the selected H09 graph instead of starting a duplicate playback");
            var sourceLocation=routedScene.Placement.Location(config.location);
            Check(sourceLocation!=null&&SamePosition(spell.Effect.Nodes[0].transform.position,sourceLocation.transform.position),
                "the source root keeps its authored power anchor after the initial timeline seek");
            Check(routedAudio.Count(audio=>audio.file=="0b172af97e533a48568c40c7.wav")==1,
                "the accepted source-only cue does not replay the already-started H09 source clip");
            Check(spell.Context.CardId(0)=="HERO_09bp"&&spell.Context.CardId(1)=="CS2_119"&&spell.Context.Placement.source.entity==2,
                "the production SuperSpell context binds the Priest power source and the authority-selected target");
            Check(routedScene!=null&&routedScene.History==null,
                "the Presentation route has no original metadata History, so source game-delay is not simulated locally");
            var target=spell.Context.Placement.Targets.GetTarget();
            var targetRecord=spell.Context.Placement.TargetRecord(target);
            Check(spell.TargetScreenPoint.HasValue&&Vector2.Distance(spell.TargetScreenPoint.Value,new Vector2(900,350))<.5f,
                "adopting the accepted authority target updates the selection scene screen anchor");
            Check(spell.Context.Placement.Targets.GetTargets().Count==1&&targetRecord!=null&&targetRecord.entity==20&&targetRecord.card_id=="CS2_119",
                "the Flow receives the one same-batch authority target (matching Heal), not the PlayHeroPower skill entity");
            var startClone=spell.Flow.Instances.Single(instance=>instance.Effect.Program.reference==start.reference);
            var startCues=routedAudio.Where(audio=>audio.file=="0b172af97e533a48568c40c7.wav").ToArray();
            Check(ActionCount(startClone.Effect,"AudioPlayClipAction")==1&&startClone.Effect.ClipAudioStarts==1&&
                startClone.Effect.AudioCues.Count(audio=>audio.file=="0b172af97e533a48568c40c7.wav")==1&&
                startCues.Length==1&&Mathf.Abs(startCues[0].time)<.001f,
                "the production Start FSM enters its authored clip action exactly once at the source clock origin");
            Check(startCues[0].stagingFrame==startClone.Effect.StagingFrame&&startCues[0].stagingOrigin==startClone.Effect.StagingOrigin,
                "the Start voice keeps its clone's registered common staging frame after reanchor");

            void CheckTargets(string point)
            {
                var visualTargets=spell.Context.Placement.Targets.GetTargets();
                Check(visualTargets.Count==1&&visualTargets[0]==target&&
                    spell.Context.Placement.TargetRecord(target).entity==20&&spell.Context.Placement.TargetRecord(target).card_id=="CS2_119",
                    point+" preserves the unique authority-selected entity as the Flow target");
                Check(ReferenceEquals(startClone,selectedStart)&&startClone.Targets.GetTargets().Count==0,
                    point+" leaves the pre-acceptance Start clone targetless instead of backfilling it");
                foreach(var clone in spell.Flow.Instances.Where(instance=>instance.Effect.Program.reference==impact.reference))
                    Check(clone.Targets.GetTargets().Count==1&&clone.Targets.GetTarget()==target,
                    point+" binds each post-acceptance Impact clone to the authority-selected target");
                Check(spell.Flow.Flights.Count()==0&&spell.Flow.FirstArrivalTime==null&&
                    !spell.Events.Any(value=>value.IndexOf("Missile",StringComparison.Ordinal)>=0||value.Contains(" Arrival ")),
                    point+" emits no Missile or Arrival route");
            }
            void Advance(float age)
            {
                route.Seek(age);int samples=preparedAges.Count;route.PrepareNative();
                if(preparedAges.Count==samples)
                {
                    if(!player.PrepareSource(key,cue.data,routeBefore,routeAfter,false,age))
                        throw new Exception("Priest hero power route: direct production sample failed at "+age);
                    preparedAges.Add(age);
                }
                Check(SamePosition(spell.Effect.Nodes[0].transform.position,sourceLocation.transform.position),
                    "source-clock sample at "+age.ToString("F2",CultureInfo.InvariantCulture)+" keeps the authored power anchor");
                Check(Mathf.Abs(player.SourceSpell(key).Time-age)<.001f,"the production Flow follows presentation age "+age);
                CheckTargets(age.ToString("F2",CultureInfo.InvariantCulture));
            }
            var startMachine=start.machines.Single(machine=>machine.owner_known&&machine.owner==0);
            float startBirthWait=startMachine.states.Single(state=>state.name=="BirthAnimation").actions
                .Single(action=>action.name=="Wait").Param("time").number;
            Check(Mathf.Approximately(startBirthWait,1f),"the imported Start BirthAnimation retains its authored one second FSM wait");
            void CheckNoImpact(string point)
            {
                bool hasImpact=spell.Flow.Instances.Any(instance=>instance.Effect.Program.reference==impact.reference)||
                    spell.Events.Any(value=>value.Contains("ImpactClone"));
                Check(!hasImpact,point+" remains behind the authored Start BirthAnimation gate; "+SourceSnapshot(point,spell,startClone));
            }
            void CheckStartBirthWait(string point)
            {
                Check(startClone.Effect.ActiveSourceSpellState==1&&startClone.Effect.CurrentStates.Contains("BirthAnimation")&&
                    ActionCount(startClone.Effect,"Wait")==1,
                    point+" shows the active Start BirthAnimation Wait; "+SourceSnapshot(point,spell,startClone));
            }
            Debug.Log(SourceSnapshot("0.00",spell,startClone));
            CheckNoImpact("0.00");
            Check(spell.Flow.Pending==1&&spell.Flow.ActiveClones==1&&SchedulerActive(spell.Flow)==0,
                "age 0 owns only the synchronous Start clone and has no scheduled coroutine; "+SourceSnapshot("0.00",spell,startClone));
            Advance(.49f);
            Debug.Log(SourceSnapshot("0.49",spell,startClone));
            CheckNoImpact("0.49");CheckStartBirthWait("0.49");
            Check(spell.Flow.Pending==1&&spell.Flow.ActiveClones==1&&SchedulerActive(spell.Flow)==0,
                "age .49 is still waiting in the Start FSM, not in the .5 source game-delay path; "+SourceSnapshot("0.49",spell,startClone));
            Advance(.51f);
            Debug.Log(SourceSnapshot("0.51",spell,startClone));
            CheckNoImpact("0.51");CheckStartBirthWait("0.51");
            Advance(1.01f);
            Debug.Log(SourceSnapshot("1.01",spell,startClone));
            CheckNoImpact("1.01");
            Check(spell.Events.Any(value=>value.Contains("StartAction"))&&spell.Events.Any(value=>value.Contains("StartFinished"))&&
                SchedulerActive(spell.Flow)==1&&spell.Flow.Pending==1&&spell.Flow.ActiveClones==1,
                "Start pending has settled; the remaining one pending request is Impact's cross-frame spawn while its Start clone is in DeathState; "+
                SourceSnapshot("1.01",spell,startClone));
            Advance(1.02f);
            Debug.Log(SourceSnapshot("1.02",spell,startClone));
            var impactClones=spell.Flow.Instances.Where(instance=>instance.Effect.Program.reference==impact.reference).ToArray();
            Check(impactClones.Length==1&&spell.Events.Count(value=>value.Contains("ImpactClone"))==1,
                "the next presentation frame creates one Impact clone; "+SourceSnapshot("1.02",spell,startClone));
            var impactClone=impactClones.Single();
            Check(impactClone.Effect.StagingOrigin==spell.Effect.Root.transform.position,
                "the delayed H09 Impact clone reanchors its staging origin to the shared spell root");
            Check(impactClone.Targets.GetTargets().Count==1&&impactClone.Targets.GetTarget()==target,
                "the accepted source route binds its delayed Impact clone to the unique Heal entity");
            string impactBirth=spell.Events.Single(value=>value.Contains("ImpactClone"));
            float impactBirthTime=float.Parse(impactBirth.Split(' ')[0],CultureInfo.InvariantCulture);
            Check(impactBirthTime>=startBirthWait&&impactBirthTime<startBirthWait+.05f&&routedScene.History==null&&
                Mathf.Approximately(config.impact.game_delay_min,.5f)&&Mathf.Approximately(config.impact.game_delay_max,.5f),
                "Impact starts after the local Start FSM wait while its authored .5 game_delay remains source metadata without a History host");
            var impactCues=routedAudio.Where(audio=>audio.file=="af37262f5987b240bfd815c2.wav").ToArray();
            Check(impactCues.Length==1&&impactCues[0].stagingFrame==impactClone.Effect.StagingFrame&&
                impactCues[0].stagingOrigin==impactClone.Effect.StagingOrigin,
                "the Impact voice keeps its own registered common staging frame after reanchor");
            Check(impactClone.Effect.ActiveSourceSpellState==5&&impactClone.Effect.CurrentStates.Contains("DeathState")&&
                ActionCount(impactClone.Effect,"Wait")==1&&ActionCount(impactClone.Effect,"SpellCardIdAudioPlayAction")==1&&
                impactClone.Effect.CardIdAudioStarts==1&&impactCues.Length==1&&Mathf.Abs(impactCues[0].time)<.001f,
                "Impact DeathState starts its authored 2.5 second FSM Wait and zero-delay default audio action in parallel at clone age zero");
            Advance(impactBirthTime+2.49f);
            Check(spell.Flow.Instances.Contains(impactClone)&&ActionCount(impactClone.Effect,"Wait")==1&&
                ActionCount(impactClone.Effect,"SpellCardIdAudioPlayAction")==1&&
                routedAudio.Count(audio=>audio.file=="af37262f5987b240bfd815c2.wav")==1&&
                !spell.Events.Any(value=>value.Contains("ImpactState0")),
                "Impact remains in DeathState just before its authored 2.5 second Wait completes");
            Advance(impactBirthTime+2.51f);
            Check(!spell.Flow.Instances.Contains(impactClone)&&spell.Events.Count(value=>value.Contains("ImpactState0"))==1&&
                ActionCount(impactClone.Effect,"Wait")==1&&ActionCount(impactClone.Effect,"SpellCardIdAudioPlayAction")==1&&
                routedAudio.Count(audio=>audio.file=="af37262f5987b240bfd815c2.wav")==1,
                "after the FSM Wait, Impact enters None and is released without replaying audio or healing again");
            Check(new[]{0f,.49f,.51f,1.01f,1.02f,impactBirthTime+2.49f,impactBirthTime+2.51f}.All(age=>preparedAges.Any(sample=>Mathf.Abs(sample-age)<.001f)),
                "Presentation or its direct production sample covers every requested source clock point");
            Check(!route.InputLocked,"the source FSM tail and pending Impact audio do not hold global input");
        }
        Debug.Log("NATIVE PRIEST HERO POWER: "+passed+" checks passed; source-gated Start/Impact visuals and one server Heal");
    }
}
