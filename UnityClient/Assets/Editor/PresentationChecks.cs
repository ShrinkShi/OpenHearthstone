using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class PresentationChecks
{
    static int passed;
    static void Check(bool condition,string name){if(!condition)throw new Exception("Presentation check: "+name);passed++;}
    static StateView State(int version,int health)
    {
        return new StateView{match_id="presentation-test",version=version,seat=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],
            players=new[]{new PlayerView{hero=new EntityView{entity=1,health=health},power=new EntityView{entity=2},field=new EntityView[0],hand=new EntityView[0]},
                new PlayerView{hero=new EntityView{entity=3,health=30},power=new EntityView{entity=4},field=new EntityView[0],hand=new EntityView[0]}}};
    }
    static NativeAudioCurve SourceAudioCurve(float first,float last)
    {return new NativeAudioCurve{m_Curve=new[]{new NativeKey{time=0,value=first},new NativeKey{time=1,value=last}}};}
    static NativeHeroDamageAudioProfile HeroDamageAudioProfile()
    {
        var source=new NativeAudioSourceValues{m_Enabled=1,m_audioClip=new NativeAudioPointer(),OutputAudioMixerGroup=new NativeAudioPointer(),
            m_Volume=1,m_Pitch=1,MinDistance=1,MaxDistance=500,rolloffMode=1,
            rolloffCustomCurve=SourceAudioCurve(1,0),panLevelCustomCurve=SourceAudioCurve(0,0),
            spreadCustomCurve=SourceAudioCurve(0,0),reverbZoneMixCustomCurve=SourceAudioCurve(1,1)};
        const string cab="CAB-69c88b234dfa427e02d1fcee9337ae34",bundle="essential_base_global-prefab-0.unity3d";
        return new NativeHeroDamageAudioProfile{profile="hero-health-damagesplat-v1",
            source=new NativeAssetSource{cab=cab,pathid="-6173526402183834210",type="GameObject",bundle=bundle},
            action_source=new NativeAssetSource{cab=cab,pathid="188442603838002590",type="MonoBehaviour",bundle=bundle},
            sound_source=new NativeAssetSource{cab=cab,pathid="8997213827551090078",type="MonoBehaviour",bundle=bundle},
            audio_source=new NativeAudioSourceData{source=new NativeAssetSource{cab=cab,pathid="-4717049972804004450",type="AudioSource",bundle=bundle},values=source},
            sound=new NativeSoundAsset{values=new NativeSoundValues{m_AudioClip=NativeHeroDamageAudio.HealthClipReference,
                m_RandomClips=new NativeSoundChoice[0],m_RandomVolumeMin=.75f,m_RandomVolumeMax=.75f,
                m_RandomPitchMin=1,m_RandomPitchMax=1,m_Category=1},
                clips=new[]{new NativeAudioClipRef{reference=NativeHeroDamageAudio.HealthClipReference,file="source-blood.wav"}}},
            action=new NativeHeroDamageAudioAction{state="Health",name="AudioPlayAction",game_object_node=0,m_VolumeScale=1,m_Delay=0}};
    }
    static void HeroDamageAudioChecks()
    {
        var profile=HeroDamageAudioProfile();var resolved=NativeHeroDamageAudio.ResolveCue(profile,()=>.5f);
        Check(resolved!=null&&resolved.file=="source-blood.wav"&&Mathf.Approximately(resolved.volume,.75f)&&
            Mathf.Approximately(resolved.pitch,1)&&resolved.category==1&&resolved.template==profile.audio_source.values,
            "Health AudioPlayAction keeps the source AudioSource, SoundDef .75 gain, category and pitch");
        var state=State(70,30);
        var hit=new SemanticEvent{type="Hit",source=2,target=3,owner=1,amount=4,card_type=3,
            visual=new EntityView{entity=3,health=26,armor=0}};
        Check(NativeHeroDamageAudio.ShouldPlay(hit,"HERO_05bp",0,state),"unarmored positive hero health damage plays the source splat");
        Check(NativeHeroDamageAudio.ShouldPlay(hit,"HERO_05bp",1,state),"partial armor still plays the health splat when damage penetrates");
        Check(!NativeHeroDamageAudio.ShouldPlay(hit,"HERO_05bp",4,state),"armor-only hit stays on its existing armor source route");
        var minion=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(hit));minion.card_type=4;
        Check(!NativeHeroDamageAudio.ShouldPlay(minion,"HERO_05bp",0,state),"minion hits do not receive the hero Health splat");
        var selfHit=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(hit));
        selfHit.source=2;selfHit.target=1;selfHit.owner=0;selfHit.visual=new EntityView{entity=1,health=28};
        Check(!NativeHeroDamageAudio.ShouldPlay(selfHit,"HERO_07bp",0,state),"Life Tap self-damage keeps its dedicated sound without a generic hero splat");
        var emitted=new List<NativeAudioCue>();var adapter=new NativeHeroDamageAudio(profile,emitted.Add,()=>.5f);
        Check(adapter.TryPlay(hit,"HERO_05bp",0,state)&&!adapter.TryPlay(hit,"HERO_05bp",0,state)&&emitted.Count==1&&adapter.Started==1,
            "one authority Hit cue emits the source sound at most once");
    }
    public static void Run()
    {
        passed=0;
        HeroDamageAudioChecks();
        for(int count=1;count<=10;count++)
        {
            float previous=-10000;
            for(int i=0;i<count;i++)
            {
                var pose=HandLayout.Pose(i,count);
                Check(pose.center.x>previous&&pose.center.x>400&&pose.center.x<1520,"hand "+count+" order/bounds");previous=pose.center.x;
                Check(HandLayout.Contains(pose,pose.center),"rotated hit testing");
                Check(!HandLayout.Contains(pose,pose.center+Vector2.up*400),"outside hand rejected");
                float top=pose.center.y-(Mathf.Abs(Mathf.Cos(pose.angle*Mathf.Deg2Rad))*HandLayout.CardHeight+Mathf.Abs(Mathf.Sin(pose.angle*Mathf.Deg2Rad))*HandLayout.CardWidth)/2;
                Check(top>BattleLayout.Hero(true).yMax,"hand leaves hero health visible");
            }
        }
        for(int count=1;count<=7;count++)
        {
            Check(Mathf.Abs((BattleLayout.Minion(true,0,count).center.x+BattleLayout.Minion(true,count-1,count).center.x)/2-960)<.01f,"minion symmetry");
            Check(BattleLayout.Minion(true,0,count).xMin>=440&&BattleLayout.Minion(true,count-1,count).xMax<=1480,"minion bounds");
        }
        var heroLayout=BattleLayout.Hero(true);var heroPose=new Rect(heroLayout.x+35,heroLayout.y-22,heroLayout.width*1.15f,heroLayout.height*1.1f);
        var heroArmor=BattleLayout.TransformRect(heroLayout,heroPose,new Rect(heroLayout.xMax-16,heroLayout.y+104,52,48));
        var heroAttack=BattleLayout.TransformRect(heroLayout,heroPose,new Rect(heroLayout.x-16,heroLayout.yMax-57,52,48));
        Check(Mathf.Abs(heroArmor.center.x-(heroPose.x+(heroLayout.xMax-16-heroLayout.x+26)*1.15f))<.01f&&
            Mathf.Abs(heroArmor.center.y-(heroPose.y+(heroLayout.y+104+24-heroLayout.y)*1.1f))<.01f&&
            Mathf.Abs(heroAttack.center.x-(heroPose.x+(-16+26)*1.15f))<.01f&&
            Mathf.Abs(heroAttack.center.y-(heroPose.y+(heroLayout.height-57+24)*1.1f))<.01f,
            "hero attack and armor badges follow the animated portrait pose");
        Vector2 sourcePosition=new Vector2(1100,780);
        Func<int,Vector2?> locate=id=>id==2?(Vector2?)sourcePosition:null;
        var heroPowers=new[]{"HERO_01bp","HERO_02bp","HERO_03bp","HERO_04bp","HERO_05bp","HERO_06bp",
            "HERO_07bp","HERO_08bp","HERO_09bp","HERO_10bp","HERO_11bp"};
        Check(heroPowers.All(card=>NativeSuperSpellBoard.ResolvePresentationPosition(card,2,2,locate)==sourcePosition)&&
            NativeSuperSpellBoard.ResolvePresentationPosition("HERO_05bp",2,9,locate)==null,
            "all eleven hero-power source effects use their live source position and missing entities stay unbound");
        var demonHunterRect=NativeSuperSpellBoard.ScreenRect("HERO_10bp",sourcePosition);
        Check(demonHunterRect==new Rect(0,0,1920,1080)&&
            NativeSuperSpellBoard.TargetScreenRect("HERO_10bp",new Vector2(930,700))==new Rect(0,0,1920,1080)&&
            NativeSuperSpellBoard.TargetScreenRect("HERO_10bp",null)==new Rect(0,0,1920,1080)&&
            NativeSuperSpellBoard.ScreenRect("HERO_09bp",sourcePosition)==new Rect(0,0,1920,1080),
            "world-projected hero-power source and target layers composite without a second screen translation");
        Check(AudioEventRegistry.SuppressDefaultAudio("HERO_07bp","Hit",true)&&
            AudioEventRegistry.SuppressDefaultAudio("HERO_07bp","PlayHeroPower",true)&&
            !AudioEventRegistry.SuppressDefaultAudio("HERO_07bp","Hit",false)&&
            !AudioEventRegistry.SuppressDefaultAudio("HERO_05bp","Hit",true),
            "Life Tap source audio replaces only its generic power/hit fallback when that route is available");
        var a=State(0,30);var b=State(1,27);b.events=new[]{new SemanticEvent{type="Attack",source=3,target=1},new SemanticEvent{type="Hit",source=3,target=1,amount=3}};
        var handPresentation=new HandPresentation();var fallback=HandLayout.Pose(0,1);
        Check(handPresentation.Pose(18,fallback).center==fallback.center,"new entity can render before next layout tick");
        var c=State(2,25);c.events=new[]{new SemanticEvent{type="Hit",source=3,target=1,amount=2}};
        var locations=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{3,BattleLayout.Hero(false)}};
        var healedBefore=State(40,28);var healedAfter=State(41,30);
        healedAfter.events=new[]{new SemanticEvent{type="Heal",source=2,target=1,amount=2}};
        var healed=new Presentation();healed.Accept(null,healedBefore,locations);healed.Accept(healedBefore,healedAfter,locations);
        healed.Tick(.07f,locations);
        Check(healed.Animate(1,locations[1])==locations[1]&&healed.Tint(1)==0,
            "healing updates health without a damage shake or red hit tint");
        var priestBefore=State(70,30);priestBefore.players[0].power=new EntityView{entity=2,card_id="HERO_09bp"};
        priestBefore.moves=new[]{new Move{kind="power",entity=2,target=1}};
        var priestAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestBefore));priestAfter.version=71;
        priestAfter.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_09bp"},
            new SemanticEvent{type="HeroPowerTarget",source=2,target=1,owner=0,card_id="HERO_09bp"}};
        var priest=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
        priest.Accept(null,priestBefore,locations);priest.Accept(priestBefore,priestAfter,locations);
        var priestCue=priest.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp");
        Check(priestCue.data.source==2&&priestCue.data.target==1&&priestCue.data.owner==0&&
            priestAfter.events.All(item=>item.type!="Heal")&&priestAfter.players[0].hero.health==30,
            "full-health Priest uses the unique authority target without inventing a Heal or health mutation");
        var observerPriestBefore=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestBefore));
        observerPriestBefore.seat=1;observerPriestBefore.moves=new Move[0];observerPriestBefore.version=75;
        foreach(int observerPriestTarget in new[]{1,3})
        {
            var observerPriestAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(observerPriestBefore));
            observerPriestAfter.version++;
            observerPriestAfter.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_09bp"},
                new SemanticEvent{type="HeroPowerTarget",source=2,target=observerPriestTarget,owner=0,card_id="HERO_09bp"}};
            var observerPriest=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
            observerPriest.Accept(null,observerPriestBefore,locations);observerPriest.Accept(observerPriestBefore,observerPriestAfter,locations);
            Check(observerPriest.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp").data.target==observerPriestTarget,
                "observer seat routes the owner's full-health Priest target from authority without local moves");
        }
        var oldPriestAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestAfter));oldPriestAfter.version=72;
        oldPriestAfter.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_09bp"},
            new SemanticEvent{type="Heal",source=2,target=1,owner=0,amount=2}};
        var oldPriest=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
        oldPriest.Accept(null,priestBefore,locations);oldPriest.Accept(priestBefore,oldPriestAfter,locations);
        Check(oldPriest.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp").data.target==1,
            "legacy Priest Heal-only snapshots still route the native target from the authoritative Heal event");
        var priestMismatch=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestAfter));priestMismatch.version=73;
        priestMismatch.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_09bp"},
            new SemanticEvent{type="HeroPowerTarget",source=2,target=1,owner=0,card_id="HERO_09bp"},
            new SemanticEvent{type="Heal",source=2,target=3,owner=0,amount=2}};
        var mismatchedPriest=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
        mismatchedPriest.Accept(null,priestBefore,locations);mismatchedPriest.Accept(priestBefore,priestMismatch,locations);
        Check(!mismatchedPriest.Cues.Any(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp"),
            "Priest target routing rejects an authority target that disagrees with the real Heal result");
        var duplicatePriest=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestAfter));duplicatePriest.version=74;
        duplicatePriest.events=priestAfter.events.Concat(new[]{priestAfter.events[1]}).ToArray();
        var duplicatedPriest=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
        duplicatedPriest.Accept(null,priestBefore,locations);duplicatedPriest.Accept(priestBefore,duplicatePriest,locations);
        Check(!duplicatedPriest.Cues.Any(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp"),
            "Priest target routing rejects duplicate authority target events even when their targets match");
        var wrongOwnerPriest=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(priestAfter));wrongOwnerPriest.version=76;
        wrongOwnerPriest.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_09bp"},
            new SemanticEvent{type="HeroPowerTarget",source=2,target=1,owner=1,card_id="HERO_09bp"}};
        var wrongOwnerPresentation=new Presentation{NativeDuration=id=>id=="HERO_09bp"?1.6f:0};
        wrongOwnerPresentation.Accept(null,priestBefore,locations);wrongOwnerPresentation.Accept(priestBefore,wrongOwnerPriest,locations);
        Check(!wrongOwnerPresentation.Cues.Any(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_09bp"),
            "Priest target routing rejects an authority target attributed to the wrong owner");
        var armoredBefore=State(10,30);armoredBefore.players[0].hero.armor=2;
        var armoredAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(armoredBefore));armoredAfter.version=11;
        armoredAfter.players[0].hero.armor=0;armoredAfter.players[0].hero.health=27;
        armoredAfter.events=new[]{new SemanticEvent{type="Hit",source=3,target=1,amount=3,
            visual=new EntityView{entity=1,card_id="HERO_01",health=27,armor=0}}};
        var armorHit=new Presentation();armorHit.Accept(null,armoredBefore,locations);armorHit.Accept(armoredBefore,armoredAfter,locations);
        var armorHitCue=armorHit.Cues.Single(cue=>cue.data.type=="Hit");
        Check(armorHit.ArmorBefore(armorHitCue)==2&&armorHit.ArmorAfter(armorHitCue)==0&&armorHit.ArmorAbsorbed(armorHitCue)==2&&
            AudioEventRegistry.ArmorHit=="SFX_ARMOR_HIT",
            "armor loss computes the absorbed hit amount and selects its dedicated source sound");
        armorHit.Tick(.01f,locations);
        Check(armorHit.DamageDecorationCount==1,"a non-Fireblast hero hit creates the original DamageSplat number");
        armorHit.Tick(.61f,locations);
        Check(!armorHit.InputLocked&&armorHit.DamageDecorationCount==1,
            "the original damage decoration remains visible after the hit reaction releases input");
        armorHit.Tick(3.5f,locations);Check(armorHit.DamageDecorationCount==0,"DamageSplat expires on its independent lifetime");
        var warriorBefore=State(60,30);warriorBefore.public_hash="warrior-before";warriorBefore.players[0].mana=10;
        warriorBefore.players[0].power=new EntityView{entity=2,card_id="HERO_01bp"};
        var warriorAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(warriorBefore));warriorAfter.version=61;
        warriorAfter.public_hash="warrior-cast";warriorAfter.players[0].mana=8;warriorAfter.players[0].hero.armor=2;warriorAfter.players[0].power.used=true;
        warriorAfter.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,owner=0,card_id="HERO_01bp"},
            new SemanticEvent{type="GainArmor",source=2,target=1,owner=0,amount=2}};
        bool warriorSourceActive=true;float warriorTailAge=-1;var warrior=new Presentation{NativeDuration=id=>id=="HERO_01bp"?1.55f:0,
            NativeActive=key=>warriorSourceActive};
        warrior.NativePrepare=(key,data,old,next,area,age)=>{warriorTailAge=age;};
        warrior.Accept(null,warriorBefore,locations);warrior.Accept(warriorBefore,warriorAfter,locations);
        Check(warrior.InputLocked,"Warrior keeps a short input gate at the start of its accepted batch");
        warrior.Tick(.19f,locations);Check(warrior.InputLocked,"Warrior input remains gated before 0.2s");
        warrior.Tick(.02f,locations);Check(!warrior.InputLocked&&warrior.Busy,
            "Warrior unlocks at 0.2s while its batch and native source are still active");
        warrior.Tick(.6f,locations);Check(warrior.NativeVisualTailCount==1&&warrior.Busy&&!warrior.InputLocked&&
            warrior.Display.public_hash=="warrior-cast","Warrior source survives its completed gameplay batch as a visual tail");
        warrior.PrepareNative();Check(warriorTailAge>.75f&&warrior.NativeKeys().Any(),
            "Warrior native tail continues to receive source age samples after the batch completes");
        var nextTurn=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(warriorAfter));nextTurn.version=62;nextTurn.turn=2;nextTurn.active=1;
        nextTurn.public_hash="warrior-next-state";nextTurn.events=new[]{new SemanticEvent{type="TURN_START",owner=1}};
        warrior.Accept(warriorAfter,nextTurn,locations);warrior.Tick(1.2f,locations);
        Check(warrior.Display.public_hash=="warrior-next-state"&&warrior.NativeVisualTailCount==1,
            "the queued authority batch advances while the old Warrior source tail remains active");
        warriorSourceActive=false;warrior.Tick(.01f,locations);
        Check(!warrior.Busy&&warrior.Display.public_hash=="warrior-next-state",
            "old Warrior tail cleanup cannot restore the prior authority state");
        var minionBefore=State(20,30);minionBefore.players[1].field=new[]{new EntityView{entity=5,card_id="minion",health=3}};
        var minionAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(minionBefore));minionAfter.version=21;
        minionAfter.players[1].field[0].health=2;minionAfter.events=new[]{new SemanticEvent{type="Hit",source=3,target=5,amount=1}};
        var minionLocations=new Dictionary<int,Rect>(locations){{5,BattleLayout.Minion(false,0,1)}};
        var minionHit=new Presentation();minionHit.Accept(null,minionBefore,minionLocations);minionHit.Accept(minionBefore,minionAfter,minionLocations);
        minionHit.Tick(.01f,minionLocations);Check(minionHit.DamageDecorationCount==1,"a non-Fireblast minion hit creates the original DamageSplat");
        var weaponBefore=State(30,30);weaponBefore.players[0].weapon=new EntityView{entity=20,card_id="CS2_082",durability=2};
        var weaponAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(weaponBefore));weaponAfter.version=31;
        weaponAfter.players[0].weapon.durability=1;weaponAfter.events=new[]{new SemanticEvent{type="Hit",source=20,target=20,amount=1}};
        var weaponHit=new Presentation();weaponHit.Accept(null,weaponBefore,locations);weaponHit.Accept(weaponBefore,weaponAfter,locations);
        weaponHit.Tick(.01f,locations);Check(weaponHit.DamageDecorationCount==0,"weapon durability hits do not create hero/minion DamageSplats");
        var firstArmorBefore=State(40,30);var firstArmorAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(firstArmorBefore));
        firstArmorAfter.version=41;firstArmorAfter.players[0].hero.armor=1;
        firstArmorAfter.events=new[]{new SemanticEvent{type="GainArmor",source=2,target=1,amount=1,
            visual=new EntityView{entity=1,card_id="HERO_01",health=30,armor=1}}};
        var firstArmor=new Presentation();firstArmor.Accept(null,firstArmorBefore,locations);firstArmor.Accept(firstArmorBefore,firstArmorAfter,locations);
        Check(firstArmor.IsInitialArmorGain(firstArmor.Cues.Single(cue=>cue.data.type=="GainArmor")),"the 0-to-1 armor transition plays the first-gain badge flash");
        var stackedArmorBefore=State(50,30);stackedArmorBefore.players[0].hero.armor=2;
        var stackedArmorAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(stackedArmorBefore));stackedArmorAfter.version=51;
        stackedArmorAfter.players[0].hero.armor=3;
        stackedArmorAfter.events=new[]{new SemanticEvent{type="GainArmor",source=2,target=1,amount=1,
            visual=new EntityView{entity=1,card_id="HERO_01",health=30,armor=3}}};
        var stackedArmor=new Presentation();stackedArmor.Accept(null,stackedArmorBefore,locations);stackedArmor.Accept(stackedArmorBefore,stackedArmorAfter,locations);
        Check(!stackedArmor.IsInitialArmorGain(stackedArmor.Cues.Single(cue=>cue.data.type=="GainArmor")),
            "gaining armor while already armored updates the number without replaying the first-gain flash");
        var presentation=new Presentation();var sounds=new List<string>();presentation.AudioCue=(e,id)=>sounds.Add(e.type);
        presentation.Accept(null,a,locations);presentation.Accept(a,b,locations);presentation.Accept(b,c,locations);
        presentation.Tick(.1f,locations);Check(presentation.Display.players[0].hero.health==30,"health waits for impact");
        Check(presentation.QueueCount==2,"later packet cannot replace attack");
        presentation.Tick(.2f,locations);Check(presentation.Display.players[0].hero.health==27,"health resolves at impact");
        Check(sounds.FindAll(s=>s=="Hit").Count==1,"one sound per impact");
        presentation.Skip();Check(!presentation.Busy&&presentation.Display.version==2,"skip reaches newest snapshot");
        presentation.Accept(c,c,locations);Check(!presentation.Busy,"duplicate snapshot ignored");
        presentation.Accept(c,b,locations);presentation.Reset();Check(!presentation.Busy&&presentation.Display==null,"menu resets visual state");
        var opening=State(0,30);opening.phase="mulligan";opening.moves=new[]{new Move{kind="mulligan"}};
        opening.players[0].hand=new[]{new EntityView{entity=10},new EntityView{entity=11},new EntityView{entity=12}};
        var replacement=State(1,30);replacement.phase="mulligan";replacement.players[0].hand=new[]{new EntityView{entity=10},new EntityView{entity=13},new EntityView{entity=12}};
        var mulligan=new MulliganPresentation();mulligan.Observe(null,opening);mulligan.Tick(1);
        Check(mulligan.Interactable,"opening enables input after deal");mulligan.Observe(opening,replacement);
        Check(mulligan.Confirmed&&mulligan.OldCards.Length==3&&mulligan.Cards[1].entity==13,"replacement retains outgoing visuals");
        replacement.phase="play";mulligan.Observe(opening,replacement);mulligan.Tick(1.4f);
        Check(!mulligan.Active,"mulligan finishes without input lock");
        CardEffectRegistry.Load(Path.GetFullPath("../local/runtime"));
        var fanBefore=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/fan_of_knives.json"));
        var fanAfter=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/fan_of_knives-after.json"));
        var fan=new Presentation();int committed=0;
        var log=new BattleHistory();fan.BatchCompleted=(old,next)=>{committed++;log.Observe(old,next,id=>new CardDef{CardId=id});};
        fan.Accept(null,fanBefore,locations);fan.Accept(fanBefore,fanAfter,locations);
        var play=fan.Cues.First(cue=>cue.data.type=="CardPlay");var spell=fan.Cues.First(cue=>cue.data.type=="SpellEffect");
        var hits=fan.Cues.Where(cue=>cue.data.type=="Hit").ToArray();var triggers=fan.Cues.Where(cue=>cue.data.type=="Trigger").ToArray();
        var gains=fan.Cues.Where(cue=>cue.data.type=="GainArmor").ToArray();
        Check(spell.start>=play.start+play.duration,"spell waits for departing card");
        int prepares=0;string preparedKey=null;float preparedAge=-1;bool preparedArea=false;
        fan.NativePrepare=(key,data,old,next,area,age)=>{prepares++;preparedKey=key;preparedAge=age;preparedArea=area;Check(old==fanBefore&&next==fanAfter&&data.source==spell.data.source,"native preparation preserves authoritative event snapshots");};
        fan.Seek(Mathf.Max(0,spell.start-.01f));fan.PrepareNative();Check(prepares==0,"native Actor is not sampled before its spell cue");
        fan.Seek(spell.start+.1f);fan.PrepareNative();
        Check(prepares==1&&preparedArea&&Mathf.Abs(preparedAge-.1f)<.0001f,"native Actor prepass samples current area spell before entity paint");
        Check(preparedKey==fanAfter.match_id+":"+fanAfter.version+":"+spell.data.source+":"+spell.start,"native preparation and draw share effect instance key");
        Check(fan.NativeKeys().SequenceEqual(new[]{preparedKey}),"expired effect pruning uses the same active cue identities");
        fan.NativePrepare=null;fan.Seek(0);
        Check(hits.All(cue=>cue.start>=spell.start+spell.duration),"damage waits for spell flight");
        Check(triggers.Length==3&&gains.Length==3,"real fan fixture has each armorsmith reaction");
        Check(triggers[0].start>=hits.Max(cue=>cue.start+cue.duration),"trigger waits for damage numbers");
        for(int i=1;i<triggers.Length;i++)Check(triggers[i].start>=triggers[i-1].start+triggers[i-1].duration,"trigger pulses never collapse together");
        fan.Seek(hits[0].start-.01f);
        Check(fan.Display.players[1].hero.armor==0,"no early final armor");
        var smith=fanBefore.players[1].field.First(e=>e.card_id=="EX1_402");
        Check(BattleInteraction.Entity(fan.Display,smith.entity).health==4,"health remains before impact");
        fan.Seek(hits[0].start+.01f);
        Check(BattleInteraction.Entity(fan.Display,smith.entity).health==3&&fan.Display.players[1].hero.armor==0,"hit resolves before armor reaction");
        for(int i=0;i<gains.Length;i++)
        {fan.Seek(gains[i].start+.01f);Check(fan.Display.players[1].hero.armor==i+1,"armor advances one actual event at a time");}
        var death=fan.Cues.First(cue=>cue.data.type=="Death");fan.Seek(death.start+.2f);
        Check(fan.Dying(death.data.target)&&BattleInteraction.Entity(fan.Display,death.data.target)!=null,"dying entity retained during collapse");
        fan.Seek(death.start+death.duration+.01f);Check(BattleInteraction.Entity(fan.Display,death.data.target)==null,"dead entity removed after animation");
        Check(committed==0&&log.Entries.Count==0,"history withheld until full sequence completes");
        Check(fanBefore.players[1].hero.armor==0&&smith.health==4,"presentation never mutates authoritative before snapshot");
        fan.Tick(10,locations);Check(!fan.Busy&&committed==1&&log.Entries.Count==1,"sequence completion commits history once");
        Check(fan.Display.public_hash==fanAfter.public_hash&&fan.Display.version==fanAfter.version,"final display catches authoritative snapshot");
        fan.Accept(fanBefore,fanAfter,locations);fan.Speed=2;fan.Tick(.4f,locations);
        Check(Mathf.Abs(fan.TimeInBatch-.8f)<.001f,"developer speed scales complete sequence");fan.Skip();
        Check(!fan.Busy&&fan.Display.version==fanAfter.version,"skip recovers without waiting on server");
        var summonBefore=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/weapon_summon.json"));
        var summonAfter=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/weapon_summon-after.json"));
        var weaponPresentation=new Presentation();weaponPresentation.Accept(null,summonBefore,locations);weaponPresentation.Accept(summonBefore,summonAfter,locations);
        var powerCue=weaponPresentation.Cues.First(cue=>cue.data.type=="PlayHeroPower");
        weaponPresentation.Seek(powerCue.apply-.01f);Check(!weaponPresentation.Display.players[0].power.used,"power remains face up before effect");
        weaponPresentation.Seek(powerCue.apply+.01f);
        Check(weaponPresentation.Display.players[0].power.used,"spent power turns to original back");
        Check(weaponPresentation.Display.players[0].weapon!=null&&weaponPresentation.Display.players[0].weapon.card_id=="CS2_082","dagger appears at summon cue");
        Check(weaponPresentation.Display.players[0].field.Length==0,"weapon summon never enters minion zone");
        Check(weaponPresentation.Display.players[0].hero.attack==1,"equipped dagger grants displayed hero attack");
        weaponPresentation.Skip();Check(weaponPresentation.Display.public_hash==summonAfter.public_hash,"weapon sequence catches authority");
        var rogueBefore=State(1,30);rogueBefore.players[0].hero.card_id="HERO_03";rogueBefore.players[0].hero.attack=1;
        rogueBefore.players[0].weapon=new EntityView{entity=20,card_id="CS2_082",attack=1,durability=2};
        var rogueAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(rogueBefore));rogueAfter.version=2;
        rogueAfter.players[0].weapon.durability=1;rogueAfter.events=new[]{
            new SemanticEvent{type="Attack",source=1,target=3,owner=0},
            new SemanticEvent{type="Hit",source=1,target=3,amount=1,owner=0},
            new SemanticEvent{type="Hit",source=3,target=1,amount=1,owner=1},
            new SemanticEvent{type="Hit",source=20,target=20,amount=1,owner=0}};
        var rogueTimeline=new Presentation();rogueTimeline.Accept(null,rogueBefore,locations);rogueTimeline.Accept(rogueBefore,rogueAfter,locations);
        float combatImpact=rogueTimeline.Cues.Single(cue=>cue.data.type=="Attack").start+.28f;
        var exchangeHits=rogueTimeline.Cues.Where(cue=>cue.data.type=="Hit"&&cue.data.source!=20).ToArray();
        var durabilityHit=rogueTimeline.Cues.Single(cue=>cue.data.type=="Hit"&&cue.data.source==20);
        Check(exchangeHits.Length==2&&exchangeHits.All(cue=>Mathf.Approximately(cue.start,combatImpact))&&
            Mathf.Approximately(durabilityHit.start,combatImpact+.55f),
            "weapon combat and both health hits resolve together before the delayed durability/audio event");
        NativeDruidHeroPowerRouteChecks.Run();
        NativeWarriorHeroPowerChecks.RunRoute();
        Directory.CreateDirectory("../work");File.WriteAllText("../work/presentation-checks.json","{\"passed\":"+passed+"}");
        Debug.Log("PRESENTATION CHECKS PASSED: "+passed);
    }
}
