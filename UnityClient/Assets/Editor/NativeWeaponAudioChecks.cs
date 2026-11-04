using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativeWeaponAudioChecks
{
    [Serializable] sealed class Batch{public string kind;public int owner;public StateView before,after;}
    [Serializable] sealed class Evidence{public Batch[] batches;}
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception("Weapon audio: "+message);checks++;}
    public static void Run()
    {
        checks=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));string folder=assets.Root;
        var evidence=JsonUtility.FromJson<Evidence>(File.ReadAllText(Path.GetFullPath("../work/weapon-audio-authority.json")));
        foreach(var batch in evidence.batches)
        {
            var adapter=new NativeBattleWeapons(assets);var cues=new List<NativeAudioCue>();adapter.AudioCue=cues.Add;
            Check(adapter.Enabled,"exact source profile loads");
            adapter.Observe(batch.before,batch.after);adapter.Observe(batch.before,batch.after);
            var slots=batch.after.events.Select(e=>NativeBattleWeapons.Slot(e,batch.before,batch.after)).Where(s=>s>=0).ToArray();
            int equip=batch.owner==batch.after.seat?41:42;
            int[] expected=batch.kind=="equip"?new[]{equip}:batch.kind=="wear"?new[]{7}:batch.kind=="break"?new[]{7,5}:new[]{5,equip};
            Check(slots.SequenceEqual(expected),"real authority "+batch.kind+" slots for seat "+batch.after.seat);
            Check(adapter.ActiveGlowCount==0,"authority arrival does not play a queued equip effect early");
            var presentation=new Presentation();presentation.AudioCue=(data,id)=>adapter.TryHandle(data);
            presentation.Accept(batch.before,batch.after,new Dictionary<int,Rect>());
            if(expected.Contains(41)||expected.Contains(42))
            {
                for(int frame=0;frame<600&&adapter.ActiveGlowCount==0;frame++)presentation.Tick(.01f,new Dictionary<int,Rect>());
                int owner=batch.after.players.Single(p=>p.weapon!=null&&p.weapon.card_id=="CS2_082").seat;
                NativeBattleWeapons.GlowSample glow;
                Check(adapter.TrySummonGlow(owner,out glow)&&adapter.ActiveGlowCount==1&&glow.texture.width==512&&glow.texture.height==512,
                    "equip binds the side-specific original 512px glow texture to its weapon slot");
                Check(Mathf.Approximately(glow.age,0)&&Mathf.Approximately(glow.tint.r,0)&&
                    Mathf.Approximately(glow.tint.g,0)&&Mathf.Approximately(glow.tint.b,0)&&
                    Mathf.Abs(glow.tint.a-.5019608f)<1e-5f,"the source animation starts with its authored tint and material alpha");
                adapter.Advance(.21666667f);
                Check(adapter.TrySummonGlow(owner,out glow)&&Mathf.Abs(glow.age-.21666667f)<1e-5f&&
                    Mathf.Abs(glow.tint.r-.2214533f)<1e-4f&&Mathf.Abs(glow.tint.g-.26206335f)<1e-4f&&
                    Mathf.Abs(glow.tint.b-.3137255f)<1e-4f,"the source Hermite tint curves sample their authored first key");
                var target=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32);target.Create();
                var pixels=new Texture2D(128,128,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=target;GL.Clear(true,true,new Color(.2f,.3f,.4f,1));
                    pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                    var baseline=pixels.GetPixel(64,64);var corner=pixels.GetPixel(4,4);
                    Check(adapter.DrawSummonGlow(owner,new Rect(0,0,128,128),Matrix4x4.identity,128,128,Color.white),
                        "the production weapon-slot emission compositor draws the original glow");
                    pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                    var center=pixels.GetPixel(64,64);var border=pixels.GetPixel(4,4);
                    Check((center.r>baseline.r+.02f||center.g>baseline.g+.02f||center.b>baseline.b+.02f)&&Mathf.Abs(border.r-corner.r)<.01f&&
                        Mathf.Abs(border.g-corner.g)<.01f&&Mathf.Abs(border.b-corner.b)<.01f,
                        "the source glow center adds light while opaque black texture borders leave the board unchanged");
                }
                finally{RenderTexture.active=previous;target.Release();AssetRegistry.ReleaseTransient(target);AssetRegistry.ReleaseTransient(pixels);}
                adapter.Advance(.5f);
                Check(!adapter.HasSummonGlow(owner)&&adapter.ActiveGlowCount==0,"the summoned weapon glow expires at its source clip boundary");
            }
            for(int frame=0;frame<600;frame++)presentation.Tick(.01f,new Dictionary<int,Rect>());
            Check(adapter.Started==expected.Length&&adapter.Failures==0&&adapter.BindingCount==0,"production Presentation callback plays each source once");
            Check(cues.All(c=>File.Exists(Path.Combine(folder,c.file))),"every source resolves a local clip");
            Check(cues.Count(c=>Mathf.Approximately(c.volume,.8f))==expected.Count(s=>s==41||s==42),"equip volume replaces template volume");
            Check(cues.Count(c=>Mathf.Approximately(c.volume,.6f))==expected.Count(s=>s==7),"durability volume follows source action");
            Check(cues.Count(c=>Mathf.Approximately(c.volume,1))==expected.Count(s=>s==5),"death volume follows source action");
            using(var playback=new NativeAudioPlayback(folder,null,true))
            {
                foreach(var cue in cues)
                {
                    var pcm=playback.Load(cue.file);
                    Check(pcm.channels==2&&pcm.frequency==48000&&pcm.samples>0,"source PCM decodes at original format");
                    playback.Play(cue);
                }
                Check(playback.StartedVoices==expected.Length,"production NativeAudioPlayback starts every resolved voice");
            }
            adapter.Clear();Check(adapter.BindingCount==0,"reset releases bindings");
        }
        var equipBatch=evidence.batches.First(b=>b.kind=="equip");
        var delayAdapter=new NativeBattleWeapons(assets);var delayed=new List<NativeAudioCue>();delayAdapter.AudioCue=delayed.Add;
        delayAdapter.Observe(equipBatch.before,equipBatch.after);
        var summon=equipBatch.after.events.Single(e=>e.type=="Summon");
        Check(delayAdapter.TryHandle(summon)&&delayed.Count==1,"equip audio follows immediately finished AnimationPlaythroughAction");
        Check(!delayAdapter.TryHandle(summon),"event binding consumed once");
        delayAdapter.Clear();delayAdapter.Observe(equipBatch.before,equipBatch.after);delayAdapter.Clear();
        Check(!delayAdapter.TryHandle(summon)&&delayed.Count==1,"reset cancels an unfired event binding");
        var wear=evidence.batches.First(b=>b.kind=="wear");
        var hit=wear.after.events.Single(e=>e.type=="Hit"&&e.source==e.target);
        var falseHit=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(hit));falseHit.source=wear.before.players[wear.owner].hero.entity;
        Check(NativeBattleWeapons.Slot(falseHit,wear.before,wear.after)<0,"hero hit cannot masquerade as durability");
        falseHit=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(hit));falseHit.visual.durability=wear.before.players[wear.owner].weapon.durability;
        Check(NativeBattleWeapons.Slot(falseHit,wear.before,wear.after)<0,"unchanged durability rejected");
        VerifyGenericWeaponTransitions(wear,evidence.batches.First(b=>b.kind=="break"));
        VerifyGenericWeaponSummons(assets,folder);
        Debug.Log("NATIVE WEAPON CHECKS PASS "+checks+" checks; source texture/tint and verified additive base restored; complete Actor lifecycle remains unverified");
    }

    static void VerifyGenericWeaponTransitions(Batch wear,Batch broken)
    {
        foreach(string cardId in new[]{"CS2_106","EX1_567"})
        {
            var before=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(wear.before));
            var after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(wear.after));
            before.players[wear.owner].weapon.card_id=cardId;
            after.players[wear.owner].weapon.card_id=cardId;
            var hit=after.events.Single(e=>e.type=="Hit"&&e.source==e.target);
            if(hit.visual!=null)hit.visual.card_id=cardId;
            Check(NativeBattleWeapons.Slot(hit,before,after)==7,
                cardId+" keeps the original weapon durability-hit cue");

            before=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(broken.before));
            after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(broken.after));
            before.players[broken.owner].weapon.card_id=cardId;
            var terminalHit=after.events.Single(e=>e.type=="Hit"&&e.source==e.target);
            if(terminalHit.visual!=null)terminalHit.visual.card_id=cardId;
            var death=after.events.Single(e=>e.type=="Death"&&e.target==before.players[broken.owner].weapon.entity);
            Check(NativeBattleWeapons.Slot(terminalHit,before,after)==7&&
                NativeBattleWeapons.Slot(death,before,after)==5,
                cardId+" keeps the original durability and break cues at terminal wear");
            var attack=after.events.Single(e=>e.type=="Attack"&&e.source==before.players[broken.owner].hero.entity);
            var beforeOwner=before.players[broken.owner];
            bool weaponStillEquipped=beforeOwner.weapon!=null&&after.players.Any(player=>player!=null&&player.weapon!=null&&
                player.weapon.entity==beforeOwner.weapon.entity);
            Check(beforeOwner.hero!=null&&beforeOwner.weapon!=null&&before.match_id==after.match_id&&
                before.version<after.version&&beforeOwner.seat==broken.owner&&
                attack.source==beforeOwner.hero.entity&&attack.target!=0&&death.target==beforeOwner.weapon.entity&&
                !weaponStillEquipped,
                cardId+" break fixture preserves authority match/version/seat/source and removes that weapon entity");

            var replay=new HeroSpeechReplay("","",false,false,true);
            replay.ObserveState(before,true);
            replay.ObserveState(after,true);
            replay.ObserveHeroAttackAccepted(attack,beforeOwner.hero.card_id,"",after);
            var recordedAttacks=typeof(HeroSpeechReplay).GetField("heroAttacks",BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(replay) as List<HeroSpeechReplayAttack>;
            Check(recordedAttacks!=null&&recordedAttacks.Count==1&&
                recordedAttacks[0].weapon_card_id==cardId&&recordedAttacks[0].weapon_card_id_source=="before-snapshot"&&
                recordedAttacks[0].seat==beforeOwner.seat,
                cardId+" replay records the equipped card from the observed before snapshot");
        }
    }

    static void VerifyGenericWeaponSummons(AssetRegistry assets,string folder)
    {
        string[] cards={"CS2_106","EX1_567"};
        var before=new StateView{match_id="f024-generic-weapons",version=0,seat=0,phase="play",players=new PlayerView[2]};
        var after=new StateView{match_id=before.match_id,version=1,seat=0,phase="play",players=new PlayerView[2]};
        for(int seat=0;seat<2;seat++)
        {
            before.players[seat]=new PlayerView{seat=seat,hero=new EntityView{entity=100+seat,card_id=seat==0?"HERO_01":"HERO_02"},field=new EntityView[0]};
            after.players[seat]=new PlayerView{seat=seat,hero=before.players[seat].hero,field=new EntityView[0]};
        }
        var events=new SemanticEvent[2];
        for(int seat=0;seat<2;seat++)
        {
            int entity=200+seat;var weapon=new EntityView{entity=entity,card_id=cards[seat],attack=3,durability=2,max_durability=2};
            after.players[seat].weapon=weapon;
            events[seat]=new SemanticEvent{type="Summon",source=before.players[seat].hero.entity,target=entity,
                card_type=7,owner=seat,visual=weapon};
        }
        after.events=events;
        Check(NativeBattleWeapons.Slot(events[0],before,after)==41&&NativeBattleWeapons.Slot(events[1],before,after)==42,
            "Fiery War Axe and Doomhammer use the shared friendly/opponent source summon slots");
        var adapter=new NativeBattleWeapons(assets);var cues=new List<NativeAudioCue>();adapter.AudioCue=cues.Add;
        adapter.Observe(before,after);
        Check(adapter.BindingCount==2,"both generic weapon summon events bind once");
        foreach(var data in events)
        {
            Check(adapter.TryHandle(data),"generic weapon cue starts on its true Summon event");
            Check(!adapter.TryHandle(data),"generic weapon source event cannot start twice");
        }
        Check(adapter.Started==2&&adapter.Failures==0&&adapter.BindingCount==0&&adapter.ActiveGlowCount==2,
            "generic weapon source cues replace only their corresponding source bindings");
        Check(cues.All(c=>File.Exists(Path.Combine(folder,c.file)))&&cues.Count(c=>Mathf.Approximately(c.volume,.8f))==2,
            "generic weapon slots resolve original clips at source volume");
        using(var playback=new NativeAudioPlayback(folder,null,true))
        {
            foreach(var cue in cues)playback.Play(cue);
            Check(playback.StartedVoices==2,"both generic weapon source clips reach NativeAudioPlayback");
        }
        var falseHero=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(events[0]));falseHero.card_type=3;
        Check(NativeBattleWeapons.Slot(falseHero,before,after)<0,"hero Summon cannot enter the weapon source route");
        var falseMinion=JsonUtility.FromJson<SemanticEvent>(JsonUtility.ToJson(events[0]));falseMinion.card_type=4;
        Check(NativeBattleWeapons.Slot(falseMinion,before,after)<0,"minion Summon cannot enter the weapon source route");
        var alreadyEquipped=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));
        alreadyEquipped.players[0].weapon=JsonUtility.FromJson<EntityView>(JsonUtility.ToJson(after.players[0].weapon));
        Check(NativeBattleWeapons.Slot(events[0],alreadyEquipped,after)<0,"existing weapon entity cannot replay equip audio");
        adapter.Clear();Check(adapter.ActiveGlowCount==0&&adapter.BindingCount==0,"generic weapon reset releases source state");
    }
}
