using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeRogueHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Rogue hero power: "+reason);passed++;}
    static StateView State(int version,bool weapon,bool friendly)
    {
        int rogueSeat=friendly?0:1,mageSeat=1-rogueSeat;
        var rogue=new PlayerView{seat=rogueSeat,hero=new EntityView{entity=rogueSeat==0?1:3,card_id="HERO_03",health=30,attack=weapon?1:0},
            power=new EntityView{entity=rogueSeat==0?2:4,card_id="HERO_03bp"},
            weapon=weapon?new EntityView{entity=20,card_id="CS2_082",attack=1,durability=2}:null,
            field=new EntityView[0],hand=new EntityView[0]};
        var mage=new PlayerView{seat=mageSeat,hero=new EntityView{entity=mageSeat==0?1:3,card_id="HERO_08",health=30},
            power=new EntityView{entity=mageSeat==0?2:4,card_id="HERO_08bp"},field=new EntityView[0],hand=new EntityView[0]};
        return new StateView{match_id="rogue-dagger-check",version=version,seat=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],
            players=rogueSeat==0?new[]{rogue,mage}:new[]{mage,rogue}};
    }
    static StateView OccupiedReplacementState(int version,bool oldWeapon)
    {
        return new StateView{match_id="rogue-shaman-v18-source-regression",version=version,turn=7,active=0,seat=0,phase="play",
            public_hash=oldWeapon?"rogue-shaman-v17":"99c95a929878135ba461a450c1538df7bb41da8677419e43107bae1bb054e027",
            moves=oldWeapon?new Move[0]:new[]{new Move{kind="end_turn"}},events=new SemanticEvent[0],players=new[]{
                new PlayerView{seat=0,mana=oldWeapon?4:2,max_mana=4,
                    hero=new EntityView{entity=4,card_id="HERO_03",health=30,attack=1},
                    power=new EntityView{entity=5,card_id="HERO_03bp"},
                    weapon=new EntityView{entity=oldWeapon?74:76,card_id="CS2_082",cost=1,attack=1,
                        durability=oldWeapon?1:2,max_durability=2},field=new EntityView[0],hand=new EntityView[0]},
                new PlayerView{seat=1,mana=1,max_mana=3,
                    hero=new EntityView{entity=37,card_id="HERO_02",health=30},
                    power=new EntityView{entity=38,card_id="HERO_02bp"},
                    field=new[]{new EntityView{entity=73,card_id="NEW1_009",attack=0,health=1},
                        new EntityView{entity=75,card_id="CS2_051",attack=0,health=1}},hand=new EntityView[0]}}};
    }
    static void OccupiedReplacement(AssetRegistry assets)
    {
        var before=OccupiedReplacementState(17,true);
        var after=OccupiedReplacementState(18,false);
        after.events=new[]{
            new SemanticEvent{type="SpendMana",source=5,target=2,owner=0,amount=2},
            new SemanticEvent{type="PlayHeroPower",source=2,target=5,owner=0,amount=0},
            new SemanticEvent{type="Destroy",source=74,target=74,owner=0,amount=0},
            new SemanticEvent{type="Death",source=74,target=74,amount=0},
            new SemanticEvent{type="Summon",source=5,target=76,owner=0,card_type=7,amount=0,
                visual=new EntityView{entity=76,card_id="CS2_082",cost=1,attack=1,durability=2,max_durability=2}}};
        Check(before.version==17&&before.turn==7&&before.players[0].weapon.entity==74&&before.players[0].weapon.durability==1&&
            before.players[1].field.Select(x=>x.entity).SequenceEqual(new[]{73,75}),
            "v17 state keeps one-durability dagger 74 and the two surviving Shaman totems");
        Check(after.version==18&&after.players[0].weapon.entity==76&&after.players[0].weapon.durability==2&&
            after.moves.Length==1&&after.moves[0].kind=="end_turn"&&
            after.events.Select(e=>e.type).SequenceEqual(new[]{"SpendMana","PlayHeroPower","Destroy","Death","Summon"}),
            "v18 legal end-turn follows the exact occupied-slot replacement event order");

        var positions=new Dictionary<int,Rect>{{4,BattleLayout.Hero(true)},{5,BattleLayout.Power(true)},
            {37,BattleLayout.Hero(false)},{38,BattleLayout.Power(false)},
            {73,BattleLayout.Minion(false,0,2)},{75,BattleLayout.Minion(false,1,2)}};
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id.EndsWith("bp")?10:id=="CS2_082"?7:3;
            player.SuperSpellScene=(data,b,a,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(data,b,a,
                id=>positions.ContainsKey(id)?positions[id].center:(Vector2?)null,viewport,origin,player.CardType);
            string key=null;bool prepared=false;
            var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy,
                NativeImpactTime=player.SourceImpactTime};
            presentation.NativePrepare=(identity,data,b,a,area,age)=>
            {key=identity;prepared|=player.PrepareSource(identity,data,b,a,area,age);};
            presentation.Accept(null,before,positions);presentation.Accept(before,after,positions);
            presentation.PrepareNative();
            var source=player.SourceSpell(key);
            Check(prepared&&source!=null&&source.Flow==null&&source.Effect.Program.nodes.Length==3,
                "v18 instantiates the supported source-only Rogue root without a parent Flow");
            Check(!player.SourceImpactTime(key).HasValue,
                "the actual NativeEffectPlayer source route reports no impact arrival for this weapon effect");

            var death=presentation.Cues.FirstOrDefault(c=>c.data.type=="Death"&&c.data.target==74);
            for(int frame=0;frame<180&&presentation.Busy;frame++)
            {
                presentation.PrepareNative();
                presentation.Tick(.05f,positions);
            }
            Check(death!=null&&death.nativeImpact==null&&death.fired&&death.applied&&
                !presentation.Busy&&!presentation.InputLocked&&presentation.QueueCount==0&&
                presentation.Display.public_hash==after.public_hash&&presentation.Display.players[0].weapon.entity==76,
                "v18 replacement settles from its source clock; busy="+presentation.Busy+" inputLocked="+presentation.InputLocked+
                " queue="+presentation.QueueCount+" elapsed="+presentation.TimeInBatch+" sourceImpact="+player.SourceImpactTime(key)+
                " deathImpact="+(death!=null&&death.nativeImpact!=null)+" deathFired="+(death!=null&&death.fired)+
                " deathApplied="+(death!=null&&death.applied)+" hash="+presentation.Display.public_hash);
        }
    }
    public static void RunOccupiedReplacementRegression()
    {
        passed=0;OccupiedReplacement(new AssetRegistry(Path.GetFullPath("../local")));
        Debug.Log("NATIVE ROGUE OCCUPIED REPLACEMENT: "+passed+" checks; real v18 source-only batch");
    }
    public static void RunSourceRoute()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_03bp");
        var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_03bp");
        Check(!entry.enabled&&Mathf.Approximately(entry.duration,2.552f),"generic route stays disabled; source gate owns the audio-tail cue");
        Check(NativeRogueHeroPowerProfile.Supports(root,config,assets.Root),"exact source profile accepted");
        var changedHierarchy=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(root));
        changedHierarchy.nodes[1].parent=0;
        Check(!NativeRogueHeroPowerProfile.Supports(changedHierarchy,config,assets.Root),"changed embedded-root parent is rejected");
        foreach(var field in new[]{"chain_enabled","chain_prefab_null","chain_available"})
        {
            var changed=JsonUtility.FromJson<NativeSuperSpellConfig>(JsonUtility.ToJson(config));
            var info=typeof(NativeSuperSpellConfig).GetField(field);info.SetValue(changed,!(bool)info.GetValue(changed));
            Check(!NativeRogueHeroPowerProfile.Supports(root,changed,assets.Root),"changed "+field+" is rejected");
        }
        var bad=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(root));
        bad.machines[0].events[0].@event="Birth";
        Check(!NativeRogueHeroPowerProfile.Supports(bad,config,assets.Root),"Death-to-Birth-State must not be reinterpreted by state name");
        bad=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(root));
        bad.machines[0].states.Single(s=>s.name=="Birth State").actions[2].Param("m_Sounds").array_count=2;
        Check(!NativeRogueHeroPowerProfile.Supports(bad,config,assets.Root),"additional random sound choices fail closed");
        bool rejected=false;
        try{using(var flow=new NativeSuperSpell(config,new NativeSuperSpellServices())){}}
        catch(NotSupportedException){rejected=true;}
        Check(rejected,"full parent Flow is rejected while null-Chain semantics remain unresolved");

        foreach(bool friendly in new[]{true,false})foreach(bool replace in new[]{false,true})
        {
            int rogueSeat=friendly?0:1,hero=friendly?1:3,power=friendly?2:4;
            var before=State(1,replace,friendly);if(replace)before.players[rogueSeat].weapon.entity=19;
            var after=State(2,true,friendly);after.events=new[]{
                new SemanticEvent{type="PlayHeroPower",source=hero,target=power,owner=rogueSeat},
                new SemanticEvent{type="Summon",source=power,target=20,card_id="CS2_082",card_type=7,owner=rogueSeat}};
            var positions=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
                {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)}};
            using(var player=new NativeEffectPlayer(assets))
            {
                player.CardType=id=>id.EndsWith("bp")?10:id=="CS2_082"?7:3;
                NativeSuperSpellScene routedScene=null;
                player.SuperSpellScene=(data,b,a,viewport,origin)=>
                    routedScene=NativeSuperSpellBoard.FromPresentation(data,b,a,
                        id=>positions.ContainsKey(id)?positions[id].center:(Vector2?)null,viewport,origin,player.CardType);
                var audio=new List<NativeAudioCue>();player.AudioCue=audio.Add;string key=null;
                var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};
                presentation.NativePrepare=(identity,data,b,a,area,age)=>{
                    key=identity;Check(player.PrepareSource(identity,data,b,a,area,age),"source reconstruction prepares");};
                presentation.Accept(null,before,positions);presentation.Accept(before,after,positions);
                var visual=presentation.Cues.Single(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_03bp");
                Check(visual.data.source==power&&visual.data.owner==rogueSeat&&visual.data.target==0&&visual.apply==float.MaxValue,
                    "one decorative source cue without a fabricated target");
                Check(presentation.Cues.Count(c=>c.data.type=="Summon"&&c.data.card_type==7)==1&&
                    !presentation.Cues.Any(c=>c.data.type=="Hit"||c.data.type=="EquipWeapon"),"only the authoritative weapon Summon remains");
                presentation.PrepareNative();var source=player.SourceSpell(key);
                Check(source!=null&&source.Flow==null&&!source.Busy&&source.Effect.Program.nodes.Length==3,
                    "embedded source root plays without a parent dispatcher, target clones or Chain");
                var sourceAuto=routedScene.Placement.SourceAuto().transform.position;
                var expectedButton=positions[power].center;
                Check(routedScene.SourceScreenPoint.HasValue&&Vector2.Distance(routedScene.SourceScreenPoint.Value,expectedButton)<.001f&&
                    source.DrawRect==new Rect(0,0,1920,1080)&&
                    Vector3.Distance(source.Effect.Nodes[0].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(source.Effect.Nodes[1].transform.position,sourceAuto)<.001f,
                    "friendly/opponent root and Idle nodes project to their actual SourceAuto power button");
                Check(source.Effect.ActionExecutions["ParticlePlayAction"]==1&&source.Effect.ClipAudioStarts==1&&audio.Count==1&&
                    source.Effect.UnboundAudioActions==0&&source.Effect.UnhandledActions==0,"one source particle and one bound original sound");
                Check(audio[0].file=="ba19cf1de88bdaa283ae8177.wav"&&audio[0].volume==1&&audio[0].pitch==1&&audio[0].spatialBlend==1,
                    "single-candidate clip and authored playback parameters");
                presentation.Tick(.95f,positions);presentation.PrepareNative();
                Check(presentation.Display.players[rogueSeat].weapon.entity==20&&presentation.Display.players[rogueSeat].hero.attack==1&&
                    !presentation.InputLocked,"equip/replacement applies once and decorative tail does not lock input");
                Check(Vector3.Distance(source.Effect.Nodes[0].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(source.Effect.Nodes[1].transform.position,sourceAuto)<.001f,
                    "both source nodes remain at the correct button after the first visual sample");
                presentation.Tick(.25f,positions);presentation.PrepareNative();
                Check(source.Effect.ActionExecutions["SpellStateAction"]==2&&source.Effect.ClipAudioStarts==1&&
                    after.players[rogueSeat].weapon.entity==20&&
                    Vector3.Distance(source.Effect.Nodes[0].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(source.Effect.Nodes[1].transform.position,sourceAuto)<.001f,
                    "one-second Wait reaches None without replaying sound/equipment or moving the source nodes");
            }
        }
        OccupiedReplacement(assets);
        Debug.Log("NATIVE ROGUE HERO POWER: "+passed+" checks; source-root-only reconstruction; original enabled-null Chain unresolved");
    }
}
