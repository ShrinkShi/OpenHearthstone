using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSuperSpellChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("SuperSpell orchestration: "+reason);passed++;}
    static StateView State(string card,int targets)
    {
        var state=new StateView{seat=0,phase="play",match_id="super-spell-check",players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="hero"},power=new EntityView{entity=2,card_id="power"},
                hand=new[]{new EntityView{entity=10,card_id=card}},field=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="hero"},power=new EntityView{entity=4,card_id="power"},
                hand=new EntityView[0],field=Enumerable.Range(0,targets).Select(i=>new EntityView{entity=20+i,card_id="minion"}).ToArray()}}};
        state.events=Enumerable.Range(0,targets).Select(i=>new SemanticEvent{type="Hit",source=10,target=20+i}).ToArray();return state;
    }
    static NativeEffectPlayer Player(AssetRegistry assets,Action<NativeSuperSpellScene> configure=null)
    {
        var player=new NativeEffectPlayer(assets);player.CardType=id=>id=="hero"?3:id=="power"?10:id=="minion"?4:5;
        player.SuperSpellScene=(data,before,after,viewport,origin)=>
        {
            var scene=NativeSuperSpellBoard.FromPresentation(data,before,after,
                id=>id==10?new Vector2(960,480):id==1?new Vector2(960,930):id==3?new Vector2(960,140):new Vector2(800+(id-20)*220,350),
                viewport,origin,player.CardType);
            scene.Range=(a,b)=>a;scene.RandomBool=()=>true;configure?.Invoke(scene);return scene;
        };
        return player;
    }
    public static void RunCleanup(){NativePresentationCoroutineChecks.Run();NativeSuperSpellHistoryChecks.Run();}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        string folder=Path.GetFullPath("../tests/visual/current/native-super-spell");Directory.CreateDirectory(folder);
        foreach(string card in new[]{"EX1_129","EX1_134"})
        using(var player=Player(assets))
        {
            var state=State(card,1);var data=new SemanticEvent{type="SpellEffect",source=10,target=20,card_id=card,owner=0};
            Check(player.PrepareSource(card,data,state,state,false,0),"formal player creates root "+card);
            var spell=player.SourceSpell(card);Check(spell!=null&&!spell.Flow.Completed,"root waits for child work "+card);
            var phases=new HashSet<string>();int frames=0,max=0;
            for(int frame=1;frame<=600;frame++)
            {
                float time=frame/120f;
                if(!player.PrepareSource(card,data,state,state,false,time))throw new Exception("Forward sample failed "+card+" "+frame);
                if(frame==2||frame==6||frame==18||frame==36||frame==72||frame==120||frame==240||frame==600)
                {
                    int visible=Capture(spell,folder,card+"-"+frame,time);max=Math.Max(max,visible);frames++;
                }
                foreach(var line in spell.Events)phases.Add(line.Split(' ')[1]);
            }
            File.WriteAllLines(Path.Combine(folder,card+"-trace.txt"),spell.Events);
            Check(phases.Contains("Birth")&&phases.Contains("Action")&&phases.Contains("MissileBirth")&&phases.Contains("Arrival")&&phases.Contains("ImpactScheduled")&&phases.Contains("MissileDeath"),"real source lifecycle trace "+card);
            Check(max>0,"actual independent prefabs render on shared GPU camera "+card);
            Check(spell.Flow.Pending==0&&spell.Flow.Completed&&spell.Flow.FinishedNotifications==1,"root aggregates actual child completion "+card);
            Check(spell.Flow.ActiveClones==0&&!spell.Busy,"all source None callbacks physically release clones "+card);
            Debug.Log("SUPER SPELL SAMPLE "+card+" pending="+spell.Flow.Pending+" active="+spell.Flow.ActiveClones+" max_pixels="+max+" samples="+frames);
        }
        Multiple(assets);MissingAndCancel(assets);History(assets);HistoryCallbacks(assets);Scheduler();PublicSource(assets);Phases(assets,folder);
        NativeSuperSpellStopChecks.Run();
        NativeSuperSpellHistoryChecks.Run();
        NativePresentationCoroutineChecks.Run();
        Debug.Log("NATIVE SUPER SPELL: "+passed+" checks passed; autonomous project scene, official geometry/fidelity unverified");
    }
    public static void RunRegression(){Run();NativeDruidHeroPowerChecks.RunSourceRoot();NativeWarriorHeroPowerChecks.RunSourceRoot();NativeWarlockHeroPowerChecks.RunSourceRoot();NativePriestHeroPowerChecks.RunSourceRoute();NativePaladinHeroPowerChecks.RunSourceRoute();NativeRogueHeroPowerChecks.RunSourceRoute();NativeDemonHunterHeroPowerChecks.RunSourceRoute();NativeDeathKnightHeroPowerChecks.RunDisabledRoute();NativeDeathKnightHeroPowerChecks.RunEnabledRoute();NativeShamanHeroPowerChecks.RunRuntimeRoute();NativeShamanHeroPowerChecks.RunDisabledRoute();HunterHeroPowerChecks.Run();NativeSuperImpactChecks.RunRegression();NativeSuperActionChecks.Run();PresentationChecks.Run();}
    public static void RunFinal(){RunRegression();RunRules();}
    public static void RunHeroPowerFeedback(){RunRegression();NativeWeaponAudioChecks.Run();NativeSourceAudioChecks.RunSourceOnly();}
    public static void RunHeroPowerFeedbackBuild(){RunHeroPowerFeedback();HeroSpeechChecks.Run();BuildClient.Build();}
    public static void RunRules()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(assets.Root);
        var types=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json")).cards.ToDictionary(c=>c.CardId,c=>c.Type);
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(int seat in new[]{0,1})using(var player=new NativeEffectPlayer(assets))
        {
            var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/"+card+"-"+seat+"-before.json"));
            var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/"+card+"-"+seat+"-after.json"));
            var play=after.events.First(e=>e.type=="CardPlay"&&e.card_id==card);
            var data=new SemanticEvent{type="SpellEffect",source=play.source,target=play.target,card_id=card,owner=play.owner};
            var locations=new Dictionary<int,Vector2>{{data.source,new Vector2(960,480)}};
            foreach(var p in before.players)
            {
                bool own=p.seat==seat;locations[p.hero.entity]=BattleLayout.Hero(own).center;locations[p.power.entity]=BattleLayout.Power(own).center;
                for(int i=0;i<p.field.Length;i++)locations[p.field[i].entity]=BattleLayout.Minion(own,i,p.field.Length).center;
            }
            player.CardType=id=>types.ContainsKey(id)?types[id]:0;
            player.SuperSpellScene=(evt,b,a,viewport,origin)=>
            {var scene=NativeSuperSpellBoard.FromPresentation(evt,b,a,id=>locations.ContainsKey(id)?locations[id]:(Vector2?)null,viewport,origin,player.CardType);scene.Range=(min,max)=>min;scene.RandomBool=()=>true;return scene;};
            Check(player.Duration(card)>0,"formal native route has existing presentation interval "+card+"/"+seat);
            Check(player.PrepareSource("rules",data,before,after,false,0),"actual public Match.apply inputs bind "+card+"/"+seat);
            int trailPoints=0,trailWrites=0;
            for(int frame=1;frame<=720;frame++)
            {
                if(!player.PrepareSource("rules",data,before,after,false,frame/120f))throw new Exception("Actual rule source failed");
                foreach(var clone in player.SourceSpell("rules").Flow.Instances)
                {foreach(var trail in clone.Effect.Nodes.SelectMany(n=>n.GetComponents<TrailRenderer>()))trailPoints=Math.Max(trailPoints,trail.positionCount);trailWrites=Math.Max(trailWrites,clone.Effect.PropertyWrites);}
            }
            var spell=player.SourceSpell("rules");int expected=card=="EX1_129"?after.events.Where(e=>e.type=="Hit"&&e.source==data.source).Select(e=>e.target).Distinct().Count():1;
            Check(spell.Flow.Flights.Count()==expected&&spell.Flow.Completed&&spell.Flow.ActiveClones==0,"actual public targets complete true root path "+card+"/"+seat);
            Check(spell.Events.Count(e=>e.Contains(" Arrival "))==expected,"actual source arrival count "+card+"/"+seat);
            Check(spell.UnboundControllerActions==0,"actual source controller/placement/property bindings are complete "+card+"/"+seat+": "+spell.UnboundControllerActions);
            if(card=="EX1_134")Check(trailPoints>1&&trailWrites>=6,"source TrailRenderer.time writes and sampled path positions execute "+seat+": "+trailPoints+"/"+trailWrites);
            if(card=="EX1_129")Check(spell.Flow.AreaReference.Contains(seat==0?"FanOfKnives.prefab":"FanOfKnives_Opponent.prefab"),"source controller selects correct real area side "+seat);
            else Check(spell.Flow.AreaReference==null,"eviscerate does not invent a missing area");
            var expectedSource=NativeSuperSpellBoard.Project(card=="EX1_129"?BattleLayout.Hero(seat==0).center:new Vector2(960,480),new Rect(0,0,1920,1080),spell.Effect.Root.transform.position);
            Check(Vector3.Distance(spell.Effect.Nodes[0].transform.position,expectedSource)<.003f,"actual source Location/SourceAuto resolves project entity role "+card+"/"+seat);
            Debug.Log("SUPER SPELL REAL RULE "+card+" seat="+seat+" targets="+expected+" unbound_controller="+spell.UnboundControllerActions+" before="+before.public_hash+" after="+after.public_hash);
        }
        Debug.Log("NATIVE SUPER SPELL REAL RULES: "+passed+" checks passed; public semantic host, no original history fabricated");
    }
    static void Advance(NativeEffectPlayer player,string key,SemanticEvent data,StateView state,float from,float until)
    {for(int frame=Mathf.RoundToInt(from*120)+1;frame<=Mathf.RoundToInt(until*120);frame++)if(!player.PrepareSource(key,data,state,state,false,frame/120f))throw new Exception("Source sample failed: "+key+"/"+frame);}
    static float PhaseTime(NativeSuperSpellPlayback spell,string phase,int index)
    {return float.Parse(spell.Events.First(line=>line.EndsWith(" "+phase+" "+index)).Split(' ')[0],System.Globalization.CultureInfo.InvariantCulture);}
    static void Multiple(AssetRegistry assets)
    {
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(bool reversed in new[]{false,true})
        using(var player=Player(assets))
        {
            var state=State(card,3);if(reversed)state.events=state.events.Reverse().ToArray();
            var data=new SemanticEvent{type="SpellEffect",source=10,card_id=card,owner=reversed?1:0};string key=card+reversed;
            Check(player.PrepareSource(key,data,state,state,false,0),"multi root prepares "+key);var spell=player.SourceSpell(key);
            Advance(player,key,data,state,0,7);
            Check(spell.Flow.Flights.Count()==3&&spell.Events.Count(l=>l.Contains(" Arrival "))==3,"one real arrival per visual index "+key);
            Check(spell.Flow.Completed&&spell.Flow.Pending==0&&spell.Flow.ActiveClones==0,"multi root completes and releases "+key);
            var births=Enumerable.Range(0,3).Select(i=>PhaseTime(spell,"MissileBirth",i)).ToArray();
            if(card=="EX1_129")Check(births.Distinct().Count()==1,"knife starts all missiles concurrently "+key);
            else Check(births[1]==PhaseTime(spell,"Arrival",0)&&births[2]==PhaseTime(spell,"Arrival",1),"eviscerate starts next on prior arrival without an extra initial delay "+key);
            var ends=spell.Flow.Flights.Select(f=>f.Path.Points[2].x).ToArray();
            Check(reversed?ends[0]>ends[2]:ends[0]<ends[2],"input target order controls actual source paths "+key);
            for(int i=0;i<3;i++)
            {
                int arrival=spell.Events.FindIndex(l=>l.EndsWith(" Arrival "+i)),impact=spell.Events.FindIndex(l=>l.EndsWith(" ImpactScheduled "+i)),death=spell.Events.FindIndex(l=>l.EndsWith(" MissileDeath "+i));
                Check(arrival<impact&&impact<death,"arrival dispatch precedes original Death "+key+"/"+i);
            }
            int count=spell.Events.Count;Advance(player,key,data,state,7,8);Check(spell.Events.Count==count,"terminal samples cannot duplicate callbacks "+key);
        }
    }
    static void MissingAndCancel(AssetRegistry assets)
    {
        foreach(string card in new[]{"EX1_129","EX1_134"})
        {
            NativeSuperSpellScene scene=null;
            using(var player=Player(assets,s=>scene=s))
            {
                var state=State(card,1);var data=new SemanticEvent{type="SpellEffect",source=10,target=20,card_id=card};
                Check(player.PrepareSource(card,data,state,state,false,0),"missing-target root prepares "+card);
                AssetRegistry.ReleaseTransient(scene.Placement.Targets.GetTarget());
                Advance(player,card,data,state,0,1);var spell=player.SourceSpell(card);
                Check(!spell.Events.Any(l=>l.Contains(" MissileBirth "))&&spell.Flow.Pending==0,"destroyed target is skipped after original waits "+card);
                if(card=="EX1_134")Check(!spell.Flow.Completed,"source skip branch does not invent FinishIfPossible for eviscerate");
                spell.Flow.Cancel();spell.Flow.Cancel();Check(spell.Flow.ActiveClones==0&&spell.Flow.Pending==0&&!spell.Flow.Faulted,"cancel skipped root is idempotent "+card);
            }
            using(var player=Player(assets,s=>scene=s))
            {
                var state=State(card,2);var data=new SemanticEvent{type="SpellEffect",source=10,card_id=card};
                player.PrepareSource(card,data,state,state,false,0);Advance(player,card,data,state,0,.04f);var spell=player.SourceSpell(card);
                var before=spell.Flow.Instances.Select(c=>new[]{c.Effect.Root,c.Endpoint.Object}).SelectMany(v=>v).ToArray();
                var textures=spell.Flow.Instances.SelectMany(c=>c.Effect.Root.GetComponentsInChildren<Camera>(true)).Select(c=>c.targetTexture).Where(t=>t!=null).ToArray();
                spell.Flow.Cancel();int arrivals=spell.Events.Count(l=>l.Contains(" Arrival "));
                spell.Flow.Tick(3);spell.Flow.Cancel();
                Check(spell.Flow.Pending==0&&spell.Flow.ActiveClones==0&&before.All(o=>o==null),"cancel physically destroys root-owned clone graphs "+card);
                Check(textures.Length>0&&textures.All(t=>t==null),"cancel destroys every clone output RenderTexture "+card);
                Check(spell.Events.Count(l=>l.Contains(" Arrival "))==arrivals&&!spell.Flow.Completed,"cancel prevents late arrivals and false success "+card);
            }
            using(var player=Player(assets,s=>scene=s))
            {
                var state=State(card,1);var data=new SemanticEvent{type="SpellEffect",source=10,target=20,card_id=card};
                player.PrepareSource(card,data,state,state,false,0);Advance(player,card,data,state,0,.04f);
                var spell=player.SourceSpell(card);AssetRegistry.ReleaseTransient(scene.Placement.Targets.GetTarget());
                // EX1_134 Birth 尚未结束时原 GetTarget 不能生成路径；明确故障并清理。
                if(card=="EX1_134")
                {
                    bool failed=false;for(int frame=6;frame<=180&&!failed;frame++)failed=!player.PrepareSource(card,data,state,state,false,frame/120f);
                    Check(failed&&spell.Flow.Faulted&&spell.Flow.ActiveClones==0,"target lost during Birth faults without substitute and cleans owned graphs");
                }
                else
                {Advance(player,card,data,state,.04f,4);Check(spell.Flow.Completed&&spell.Flow.ActiveClones==0,"target lost after path capture keeps original fixed path and skips impact");}
            }
        }
    }
    static IEnumerator CallbackOnly(Action callback){callback();yield break;}
    static void History(AssetRegistry assets)
    {
        bool ready=false,zoneReady=false,loading=true;int applied=0;
        var source=new NativePowerTaskList{tasks=new[]{
            new NativePowerHistoryTask{type=4,entity=20,tag=49,tagChange=true,payloadType="HistTagChange"},
            new NativePowerHistoryTask{type=8,payloadType="HistMetaData",metaType=0,metaData=0},
            new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
        var services=new NativePowerServices{GameplayPresent=true,CanDoTask=t=>ready,MulliganBusy=()=>false,
            BeginZoneChanges=(list,start,end,callback)=>null,EntityType=id=>4,TimelineEnd=(id,time,end)=>{},ExecutionError=e=>{throw e;}};
        services.handlers[4]=p=>{applied++;return true;};services.handlers[8]=p=>true;
        var host=new NativeSuperSpellTaskHost(source,services,new Dictionary<int,int>{{0,5}},new Dictionary<int,int>{{5,1}},
            new NativeSuperSpellZoneWait{Entity=t=>t.GetPower().entity,VisualEntities=new HashSet<int>{20},InDestinationZone=t=>zoneReady,ActorLoading=t=>loading});
        NativeSuperSpellScene scene=null;
        using(var player=Player(assets,s=>{scene=s;s.History=host.Bind();}))
        {
            var state=State("EX1_134",1);var data=new SemanticEvent{type="SpellEffect",source=10,target=20,card_id="EX1_134"};
            Check(player.PrepareSource("history",data,state,state,false,0),"explicit history root prepares");var spell=player.SourceSpell("history");
            Advance(player,"history",data,state,0,.1f);Check(!spell.Flow.Flights.Any()&&applied==0,"earlier original PowerExecution task gate blocks clone birth");
            ready=true;Advance(player,"history",data,state,.1f,.2f);Check(applied==1&&!spell.Flow.Flights.Any(),"completed task still waits actual destination Zone");
            zoneReady=true;Advance(player,"history",data,state,.2f,.3f);Check(!spell.Flow.Flights.Any(),"destination reached still waits Actor loading");
            loading=false;Advance(player,"history",data,state,.3f,1.2f);Check(spell.Flow.Flights.Any()&&!spell.Flow.Completed&&applied==1,"flight and visual finish cannot settle delayed metadata From");
            ready=false;Advance(player,"history",data,state,1.2f,2);Check(!spell.Flow.Completed&&spell.Flow.Pending>0,"root counts metadata task callback beyond its coroutine return");
            ready=true;Advance(player,"history",data,state,2,5);Check(spell.Flow.Completed&&spell.Flow.Pending==0&&applied==2&&host.Execution.IsComplete(),
                "real original task callback releases root exactly once; completed="+spell.Flow.Completed+" pending="+spell.Flow.Pending+" applied="+applied+" execution="+host.Execution.IsComplete());
        }
        using(var player=Player(assets,s=>{s.History=new NativeSuperSpellHistory{Tasks=source};}))
        {
            var state=State("EX1_134",1);Check(!player.PrepareSource("bad-history",new SemanticEvent{source=10,target=20,card_id="EX1_134"},state,state,false,0),"incomplete original history never silently enters no-history path");
        }
    }
    static IEnumerator Zero(List<string> events){events.Add("start");yield return new WaitForSeconds(0);events.Add("end");}
    static void Scheduler()
    {
        var events=new List<string>();using(var clock=new NativePresentationCoroutines(e=>{throw e;}))
        {clock.Start(Zero(events));Check(events.SequenceEqual(new[]{"start"}),"zero wait suspends after synchronous Start");clock.Tick(0);Check(events.SequenceEqual(new[]{"start","end"}),"zero wait resumes on next scaled presentation frame");}
    }
    static void HistoryCallbacks(AssetRegistry assets)
    {
        foreach(bool cancel in new[]{false,true})
        {
            Action complete=null;var tasks=new NativePowerTaskList();
            using(var player=Player(assets,s=>s.History=new NativeSuperSpellHistory{Tasks=tasks,MetaDataIndex=i=>0,ShouldCompleteUntil=i=>false,
                CompleteUntil=i=>CallbackOnly(()=>{}),CompleteFrom=(i,callback)=>CallbackOnly(()=>complete=callback),Pending=()=>0,Failed=()=>false,Cancel=()=>{}}))
            {
                var state=State("EX1_134",1);var data=new SemanticEvent{source=10,target=20,card_id="EX1_134"};
                player.PrepareSource("callback",data,state,state,false,0);Advance(player,"callback",data,state,0,2);
                var spell=player.SourceSpell("callback");Check(complete!=null&&spell.Flow.Pending==1&&!spell.Flow.Completed,"metadata completion is a callback beyond coroutine return");
                if(cancel)spell.Flow.Cancel();complete();complete();
                Check(spell.Flow.Pending==0&&spell.Flow.FinishedNotifications==(cancel?0:1),"duplicate/late metadata callbacks cannot settle root twice, cancel="+cancel);
            }
        }
        var source=new NativePowerTaskList{tasks=new[]{new NativePowerHistoryTask{type=8,payloadType="HistMetaData",metaType=0,metaData=0},
            new NativePowerHistoryTask{type=4,payloadType="HistTagChange",tagChange=true,tag=44},new NativePowerHistoryTask{type=8,payloadType="HistMetaData",metaType=0,metaData=0}}};
        int applied=0;var services=new NativePowerServices{TimelineEnd=(id,time,end)=>{}};services.handlers[4]=p=>{applied++;return true;};services.handlers[8]=p=>true;
        var host=new NativeSuperSpellTaskHost(source,services,new Dictionary<int,int>{{0,4}},new Dictionary<int,int>{{4,0}});int callbacks=0;
        var history=host.Bind();var from=history.CompleteFrom(0,()=>callbacks++);while(from.MoveNext()){}
        Check(applied==1&&callbacks==1&&host.Execution.GetTaskList().Take(2).All(t=>t.IsCompleted())&&!host.Execution.GetTaskList()[2].IsCompleted(),"metadata span stops before next target metadata");
        Check(history.MetaDataIndex(0)==0&&history.MetaDataIndex(9)==-1&&!history.ShouldCompleteUntil(-1),"explicit two-level maps and real unmapped branch");
    }
    static void PublicSource(AssetRegistry assets)
    {
        foreach(string card in new[]{"EX1_129","EX1_134"})using(var player=Player(assets))
        {
            var state=State(card,1);state.players[0].hand=new EntityView[0];
            var data=new SemanticEvent{type="SpellEffect",source=10,target=20,card_id=card,owner=0};
            Check(player.PrepareSource("public",data,state,state,false,0),"public CardPlay identity binds source when opponent hand identity is hidden "+card);
            Advance(player,"public",data,state,0,5);Check(player.SourceSpell("public").Flow.Completed,"public source root completes "+card);
        }
    }
    [Serializable] sealed class PhaseRecord{public string card,phase;public float time;public int pixels;public bool official_reference=false;}
    [Serializable] sealed class PhaseReport{public string clock="scaled presentation frames / 240 Hz fixture",camera="1024x576 orthographic size16; project GUI projection";public bool whole_effect_verified=false;public PhaseRecord[] phases;}
    static void Phases(AssetRegistry assets,string folder)
    {
        var report=new List<PhaseRecord>();
        foreach(string card in new[]{"EX1_129","EX1_134"})using(var player=Player(assets))
        {
            var state=State(card,1);var data=new SemanticEvent{source=10,target=20,card_id=card};
            player.PrepareSource("phases",data,state,state,false,0);var spell=player.SourceSpell("phases");var captured=new HashSet<string>();
            void Frame(string phase,float time,bool visible)
            {
                if(!captured.Add(phase))return;int pixels=Capture(spell,folder,card+"-phase-"+phase,time);
                report.Add(new PhaseRecord{card=card,phase=phase,time=time,pixels=pixels});
                if(visible)Check(pixels>0,"GPU phase contains actual prefabs: "+card+"/"+phase);
            }
            for(int i=1;i<=1440;i++)
            {
                float time=i/240f;if(!player.PrepareSource("phases",data,state,state,false,time))throw new Exception("Phase sample failed");
                if(spell.Flow.Flights.Any())
                {
                    Frame("Birth",time,card=="EX1_129");var flight=spell.Flow.Flights.First();
                    if(flight.Path!=null)
                    {
                        float born=PhaseTime(spell,"MissileBirth",0),fraction=(time-born-flight.FiredAt)/flight.Path.Duration;
                        if(fraction>=.1f)Frame("Early",time,true);if(fraction>=.5f)Frame("Mid",time,true);
                    }
                }
                if(spell.Events.Any(e=>e.Contains(" Arrival ")))Frame("Arrival",time,false);
                if(spell.Events.Any(e=>e.Contains(" ImpactState5 ")))Frame("Hit",time,true);
                if(spell.Events.Any(e=>e.Contains(" MissileNone ")))Frame("Death",time,false);
                if(spell.Flow.Completed&&spell.Flow.ActiveClones==0){Frame("Cleanup",time,false);break;}
            }
            Check(captured.Count==7,"all lifecycle GPU phases captured from source events "+card);
        }
        File.WriteAllText(Path.Combine(folder,"phases.json"),JsonUtility.ToJson(new PhaseReport{phases=report.ToArray()},true));
    }
    static int Capture(NativeSuperSpellPlayback spell,string folder,string name,float time)
    {
        var texture=spell.RenderAffine(time);var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
        try
        {
            RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
            var pixels=image.GetPixels32();int visible=pixels.Count(c=>c.r>3||c.g>3||c.b>3);File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());return visible;
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
}
