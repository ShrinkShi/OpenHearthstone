using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeDruidHeroPowerRouteChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Druid hero power route: "+reason);passed++;}
    static bool SamePosition(Vector3 left,Vector3 right)
    {return Vector3.Distance(left,right)<.001f;}
    static int CountRgbPixels(RenderTexture image,Rect screenRect)
    {
        var previous=RenderTexture.active;var readback=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=image;readback.ReadPixels(new Rect(0,0,image.width,image.height),0,0,false);readback.Apply(false,false);
            var pixels=readback.GetPixels32();int count=0;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {
                int screenY=image.height-1-y;
                if(!screenRect.Contains(new Vector2(x+.5f,screenY+.5f)))continue;
                var pixel=pixels[y*image.width+x];if(pixel.r!=0||pixel.g!=0||pixel.b!=0)count++;
            }
            return count;
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(readback);}
    }

    static StateView State(int version)
    {
        return new StateView{match_id="druid-route-check",version=version,seat=0,active=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],
            players=new[]{
                new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="hero"},power=new EntityView{entity=2,card_id="HERO_06bp"},
                    weapon=null,hand=new EntityView[0],field=new EntityView[0],secrets=new EntityView[0]},
                new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="hero"},power=new EntityView{entity=4,card_id="power"},
                    weapon=null,hand=new EntityView[0],field=new EntityView[0],secrets=new EntityView[0]}}};
    }

    public static void Run()
    {
        passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_06bp");
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_06bp"&&row.file==entry.file);
        Check(!entry.enabled&&entry.duration>0,"disabled source entry is enabled only by the reviewed SuperSpell route");
        Check(config.target_behavior==6&&!config.start_enabled&&config.start_config_null&&config.missile!=null&&
            config.missile.m_Enabled==0&&String.IsNullOrEmpty(config.missile_reference),
            "H06 source remains untargeted and has no Start or Missile branch");
        Check(config.impact!=null&&Mathf.Approximately(config.impact.game_delay_min,.5f)&&
            Mathf.Approximately(config.impact.game_delay_max,.5f),
            "the imported 0.5 second game-delay values remain source data, separate from the presentation cue clock");
        Check(NativeSuperSpell.SupportsNoMissileUntargeted(config),"the H06 source passes the constrained no-missile branch gate");

        var before=State(1);var after=State(2);
        after.players[0].hero.attack=1;after.players[0].hero.armor=1;
        after.events=new[]{
            new SemanticEvent{type="PlayHeroPower",target=2,card_id="HERO_06bp",owner=0},
            new SemanticEvent{type="Buff",source=1,target=1,amount=1,owner=0,
                visual=new EntityView{entity=1,card_id="hero",attack=1,armor=0,health=30}},
            new SemanticEvent{type="GainArmor",source=1,target=1,amount=1,owner=0,
                visual=new EntityView{entity=1,card_id="hero",attack=1,armor=1,health=30}}};
        var locations=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)}};
        using(var unbound=new NativeEffectPlayer(assets))
        {
            var noTarget=State(1);noTarget.moves=new[]{new Move{kind="power",entity=2,target=0}};
            Check(unbound.Duration("HERO_06bp")==0,"the disabled index stays closed without a source scene");
            Check(!unbound.SelectPower(noTarget,2),"untargeted H06 never enters targeted selection");
        }
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id=="hero"?3:id=="HERO_06bp"?10:0;
            player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                id=>id==1?BattleLayout.Hero(true).center:id==2?BattleLayout.Power(true).center:
                    id==3?BattleLayout.Hero(false).center:id==4?BattleLayout.Power(false).center:(Vector2?)null,
                viewport,origin,player.CardType);
            float sourceDuration=player.Duration("HERO_06bp");
            Check(sourceDuration>0,"a bound source scene enables the reviewed disabled H06 runtime-index entry");
            var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};
            presentation.NativePrepare=(key,data,oldState,newState,area,age)=>
            {if(!player.PrepareSource(key,data,oldState,newState,area,age))throw new Exception("Druid hero power route: source cue did not prepare");};
            presentation.Accept(null,before,locations);presentation.Accept(before,after,locations);
            var cue=presentation.Cues.Single(row=>row.data.type=="SpellEffect"&&row.data.card_id=="HERO_06bp");
            string key=after.match_id+":"+after.version+":"+cue.data.source+":"+cue.start;
            Check(cue.data.source==2&&cue.data.target==0&&cue.data.owner==0,"PlayHeroPower becomes a source-only cue with no target selection");
            Check(Mathf.Approximately(cue.start,0)&&Mathf.Approximately(cue.apply,cue.start)&&
                Mathf.Approximately(cue.duration,sourceDuration),"H06 cue starts at PlayHeroPower and uses the native player's source duration");
            var attackCue=presentation.Cues.Single(row=>row.data.type=="Buff"&&row.data.target==1);
            var armorCue=presentation.Cues.Single(row=>row.data.type=="GainArmor"&&row.data.target==1);
            Check(Mathf.Approximately(attackCue.start,cue.start)&&Mathf.Approximately(attackCue.apply,cue.start)&&
                Mathf.Approximately(armorCue.start,cue.start)&&Mathf.Approximately(armorCue.apply,cue.start),
                "H06 attack and armor update in the same presentation beat as the power-button effect");
            Check(Mathf.Approximately(attackCue.duration,0)&&presentation.IsInitialArmorGain(armorCue)&&
                presentation.ArmorBefore(armorCue)==0&&presentation.ArmorAfter(armorCue)==1,
                "H06 attack has no extra animation and its first armor gain is distinguished from later armor");
            presentation.Seek(0);
            Check(presentation.Display.players[0].hero.attack==1&&presentation.Display.players[0].hero.armor==1,
                "H06 attack and armor values are visible on the same first frame");
            var powerBounds=Presentation.HeroPowerButtonBounds(2,locations,new Vector2(-1,-1));
            Check(powerBounds==BattleLayout.Power(true),"the H06 native effect rect uses the live hero-power button bounds");
            Check(presentation.NativeKeys().SequenceEqual(new[]{key}),"Presentation exposes the H06 cue to the native player under its standard key");
            presentation.PrepareNative();
            var spell=player.SourceSpell(key);
            Check(spell!=null&&!spell.Flow.Faulted&&spell.SourceEntity==2,"production PrepareNative creates the H06 source root");
            var sourceLocation=spell.Context.Placement.Location(config.location);
            Check(sourceLocation!=null&&SamePosition(spell.Effect.Nodes[0].transform.position,sourceLocation.transform.position),
                "the source root keeps its authored placement anchor after the initial timeline seek");
            var projectedRoot=NativeSuperSpellBoard.Project(BattleLayout.Hero(true).center,new Rect(0,0,1920,1080),new Vector3(20000,10000,20000));
            Check(config.location==3&&sourceLocation!=null&&SamePosition(sourceLocation.transform.position,projectedRoot),
                "the H06 thin source root retains authored SourceHero; the Fang Impact has its separate power-button location");
            var visualTargets=spell.Context.Placement.Targets.GetTargets().ToArray();
            Check(visualTargets.Length==1&&visualTargets[0]==spell.Context.Placement.source.Object,
                "target behavior 6 keeps the source as the sole visual target");
            Check(!spell.Flow.Flights.Any()&&!spell.Events.Any(value=>value.Contains("StartBirth")||value.Contains("MissileBirth")||value.Contains(" Arrival ")),
                "H06 remains on the no-Start/no-Missile path and never waits for Fireball Arrival");
            Check(!player.SourceImpactTime(key).HasValue,"the no-missile root does not invent an Arrival-based impact time");
            presentation.Seek(.49f);presentation.PrepareNative();
            Check(Mathf.Abs(spell.Time-.49f)<.001f&&!spell.Events.Any(value=>value.Contains(" Arrival ")),
                "the source root follows the presentation clock without requiring an arrival signal");
            presentation.Seek(.51f);presentation.PrepareNative();
            Check(Mathf.Abs(spell.Time-.51f)<.001f&&!spell.Events.Any(value=>value.Contains(" Arrival ")),
                "the source root continues past the configured game-delay point without inventing a missile arrival");
            var claw=spell.DruidHeroPowerButtonClone;
            Check(claw!=null&&NativeSuperSpellPlayback.SupportsDruidFangButtonProgram(claw.Effect.Program),
                "H06 button rendering selects the active source-bound Fang Impact clone and its Fangs mesh subtree");
            var impactLocation=spell.Context.Placement.Location(config.impact.location);
            Check(claw!=null&&impactLocation!=null&&ReferenceEquals(claw.Source,spell.Context.Placement.source.Object)&&
                SamePosition(claw.Effect.Nodes[0].transform.position,impactLocation.transform.position),
                "the H06 Fang Impact clone remains bound to authored impact location 4 on the live power entity");
            var buttonFrame=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32);buttonFrame.Create();
            try
            {
                var previous=RenderTexture.active;RenderTexture.active=buttonFrame;
                try
                {
                    GL.Clear(true,true,Color.clear);
                    Check(player.DrawHeroPowerButton(key,cue.data,before,after,powerBounds,.51f,Matrix4x4.identity,
                        buttonFrame.width,buttonFrame.height,Color.white),
                        "the actual H06 hero-power button draw path composites its native Fang subtree");
                    Check(CountRgbPixels(buttonFrame,powerBounds)>0,
                        "the H06 button render produces non-empty RGB inside the projected button bounds");
                }
                finally{RenderTexture.active=previous;}
            }
            finally{buttonFrame.Release();AssetRegistry.ReleaseTransient(buttonFrame);}
            Check(SamePosition(spell.Effect.Nodes[0].transform.position,sourceLocation.transform.position),
                "repeated source-clock samples keep the root at its authored hero anchor");
            presentation.Seek(Mathf.Max(.66f,sourceDuration-.01f));
            Check(presentation.InputLocked,"Druid input remains locked while the visible source cue is still within its duration");
            presentation.Seek(sourceDuration+.01f);
            Check(!presentation.InputLocked,"Druid input unlocks when its visible effect ends even if source cleanup remains active");
            presentation.NativeActive=active=>true;
            var followup=State(3);presentation.Accept(after,followup,locations);
            presentation.Tick(.06f,locations);
            Check(presentation.QueueCount==1&&presentation.Cues.All(row=>row.data.card_id!="HERO_06bp"),
                "a source cleanup tail cannot keep the completed H06 batch ahead of the next snapshot");
            presentation.Seek(.13f);
            Check(!presentation.InputLocked,"the prior H06 source tail cannot hold input after the next batch starts");
        }
        Debug.Log("DRUID HERO POWER ROUTE: "+passed+" checks passed; source-only cue routed, no Arrival dependency");
    }
}
