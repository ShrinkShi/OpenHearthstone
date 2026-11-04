using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class F024BattleChecks
{
    static int checks;
    static void Check(bool value,string message)
    {
        if(!value)throw new Exception("F024 battle check: "+message);
        checks++;
    }

    static StateView State(int version)
    {
        return new StateView
        {
            match_id="f024-battle-check",
            version=version,
            seat=0,
            phase="play",
            players=new[]
            {
                new PlayerView{seat=0,hero=new EntityView{entity=11,card_id="HERO_04"},
                    power=new EntityView{entity=12,card_id="HERO_04bp"},field=new EntityView[0]},
                new PlayerView{seat=1,hero=new EntityView{entity=21,card_id="HERO_01"},field=new EntityView[0]}
            }
        };
    }

    static StateView Copy(StateView state)
    {return JsonUtility.FromJson<StateView>(JsonUtility.ToJson(state));}

    public static void Run()
    {
        checks=0;
        Check(AudioEventRegistry.VoiceEvent("CardPlay")=="PLAY"&&AudioEventRegistry.VoiceEvent("Summon")=="PLAY",
            "CardDef PLAY source remains the cue for minion play and summon semantics");
        Check(!AudioEventRegistry.ShouldPlayMinionSourceVoice("CardPlay",4,false,true,false),
            "hand CardPlay does not start the minion PLAY source voice");
        Check(!AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",4,false,true,false),
            "an unbound or old entity cannot start the minion PLAY voice");
        Check(!AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",4,true,false,false),
            "missing source voice does not invent a substitute cue");
        Check(!AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",4,true,true,true),
            "CardDef summon suppression flag is honored");
        Check(AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",4,true,true,false),
            "a resolved new minion entity starts its source PLAY once at Summon");
        Check(AudioEventRegistry.ShouldPlayMinionSourceVoice("SUMMON",4,true,true,false)&&
            !AudioEventRegistry.ShouldPlayMinionSourceVoice("CARDPLAY",4,false,true,false),
            "production callback casing preserves the minion voice policy");
        Check(!AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",3,true,true,false)&&
            !AudioEventRegistry.ShouldPlayMinionSourceVoice("Summon",7,true,true,false),
            "hero and weapon summons cannot use minion voices");

        var router=new NativeMinionSummonVoices();
        var before=State(1);
        var handAfter=State(2);
        var handMinion=new EntityView{entity=701,card_id="CS2_182"};
        var handPlay=new SemanticEvent{type="CardPlay",card_id="CS2_182",source=501,card_type=4,owner=0};
        var handSummon=new SemanticEvent{type="Summon",event_id="hand-summon-1",source=501,target=701,
            card_type=4,owner=0,visual=handMinion};
        var duplicatedSummon=new SemanticEvent{type="Summon",event_id="hand-summon-1",source=501,target=701,
            card_type=4,owner=0,visual=handMinion};
        handAfter.players[0].field=new[]{handMinion};
        handAfter.events=new[]{handPlay,handSummon,duplicatedSummon};
        router.ObserveSummonVoice(before,handAfter);
        Check(router.Registered==1&&router.Duplicate==1&&router.PendingCount==1,
            "hand play batch binds one new minion summon and deduplicates its event id");
        var seededRouter=new NativeMinionSummonVoices();
        seededRouter.ObserveSummonVoice(null,before);
        Check(seededRouter.Registered==0&&seededRouter.PendingCount==0,
            "the first state without a prior snapshot seeds match and version only");
        seededRouter.ObserveSummonVoice(before,handAfter);
        Check(seededRouter.Registered==1&&seededRouter.Duplicate==1&&seededRouter.PendingCount==1,
            "the first post-seed hand play batch registers its new minion summon");
        string cardId,eventKey;
        Check(!router.TryHandleSummonVoice(handPlay,out cardId,out eventKey),
            "CardPlay event itself cannot consume a summon voice binding");
        Check(router.TryHandleSummonVoice(handSummon,out cardId,out eventKey)&&cardId=="CS2_182"&&
            eventKey.Contains("hand-summon-1"),"true hand-play summon resolves the summoned entity's CardId");
        Check(!router.TryHandleSummonVoice(handSummon,out cardId,out eventKey)&&
            !router.TryHandleSummonVoice(duplicatedSummon,out cardId,out eventKey),
            "one event and its duplicate can start at most one source voice");

        var tokenAfter=Copy(handAfter);tokenAfter.version=3;
        var token=new EntityView{entity=702,card_id="CS2_101t"};
        tokenAfter.players[0].field=handAfter.players[0].field.Concat(new[]{token}).ToArray();
        var powerPlay=new SemanticEvent{type="PlayHeroPower",card_id="HERO_04bp",source=12,target=12,card_type=10,owner=0};
        var tokenSummon=new SemanticEvent{type="Summon",event_id="power-token-1",source=12,target=702,card_type=4,owner=0,visual=token};
        tokenAfter.events=new[]{powerPlay,tokenSummon};
        router.ObserveSummonVoice(handAfter,tokenAfter);
        Check(router.Registered==2&&router.TryHandleSummonVoice(tokenSummon,out cardId,out eventKey)&&
            cardId=="CS2_101t"&&cardId!=powerPlay.card_id,
            "Paladin hero-power report token uses the new summoned entity, not its caster");

        var deathAfter=Copy(tokenAfter);deathAfter.version=4;
        var shortLived=new EntityView{entity=804,card_id="CS2_101t"};
        var deathSummon=new SemanticEvent{type="Summon",event_id="summon-then-death",source=12,target=804,
            card_type=4,visual=shortLived};
        deathAfter.events=new[]{deathSummon,new SemanticEvent{type="Death",source=804,target=804}};
        router.ObserveSummonVoice(tokenAfter,deathAfter);
        Check(router.TryHandleSummonVoice(deathSummon,out cardId,out eventKey)&&cardId=="CS2_101t"&&
            !deathAfter.players[0].field.Any(entity=>entity.entity==804),
            "source Summon.visual survives same-batch Death and an absent final field entity");

        var morphAfter=Copy(tokenAfter);morphAfter.version=5;
        var morphed=new EntityView{entity=805,card_id="CS2_183"};
        morphAfter.players[0].field=morphAfter.players[0].field.Concat(new[]{morphed}).ToArray();
        var beforeMorph=new EntityView{entity=805,card_id="CS2_182"};
        var morphSummon=new SemanticEvent{type="Summon",event_id="summon-then-morph",target=805,card_type=4,visual=beforeMorph};
        morphAfter.events=new[]{morphSummon,new SemanticEvent{type="Morph",target=805,card_type=4,visual=morphed}};
        router.ObserveSummonVoice(deathAfter,morphAfter);
        Check(router.TryHandleSummonVoice(morphSummon,out cardId,out eventKey)&&cardId=="CS2_182"&&
            morphAfter.players[0].field.Any(entity=>entity.entity==805&&entity.card_id=="CS2_183"),
            "Summon source CardId is kept when the same batch morphs the final entity");

        var bouncedAfter=Copy(tokenAfter);bouncedAfter.version=6;
        var bouncedVisual=new EntityView{entity=806,card_id="CS2_101t"};
        var bouncedSummon=new SemanticEvent{type="Summon",event_id="summon-then-leaves-field",target=806,
            card_type=4,visual=bouncedVisual};
        bouncedAfter.events=new[]{bouncedSummon};
        router.ObserveSummonVoice(morphAfter,bouncedAfter);
        Check(router.TryHandleSummonVoice(bouncedSummon,out cardId,out eventKey)&&cardId=="CS2_101t"&&
            !bouncedAfter.players[0].field.Any(entity=>entity.entity==806),
            "source Summon.visual survives a same-batch return or other removal from the final field");

        var fallbackAfter=Copy(tokenAfter);fallbackAfter.version=7;
        var fallbackMinion=new EntityView{entity=807,card_id="CS2_182"};
        fallbackAfter.players[0].field=fallbackAfter.players[0].field.Concat(new[]{fallbackMinion}).ToArray();
        var fallbackSummon=new SemanticEvent{type="Summon",target=807,card_type=4};
        fallbackAfter.events=new[]{fallbackSummon};
        router.ObserveSummonVoice(bouncedAfter,fallbackAfter);
        Check(router.TryHandleSummonVoice(fallbackSummon,out cardId,out eventKey)&&cardId=="CS2_182",
            "a source event without visual can use its new field entity as a bounded fallback");

        var rejectedAfter=Copy(fallbackAfter);rejectedAfter.version=8;
        var weapon=new EntityView{entity=801,card_id="CS2_106"};
        var newMinion=new EntityView{entity=802,card_id="CS2_182"};
        rejectedAfter.players[0].weapon=weapon;
        rejectedAfter.players[0].field=rejectedAfter.players[0].field.Concat(new[]{newMinion}).ToArray();
        var heroSummon=new SemanticEvent{type="Summon",target=11,card_type=3};
        var weaponSummon=new SemanticEvent{type="Summon",target=801,card_type=7};
        var staleSummon=new SemanticEvent{type="Summon",target=702,card_type=4};
        var mismatchedSummon=new SemanticEvent{type="Summon",target=802,card_type=4,
            visual=new EntityView{entity=899,card_id="CS2_182"}};
        rejectedAfter.events=new[]{heroSummon,weaponSummon,staleSummon,mismatchedSummon};
        router.ObserveSummonVoice(fallbackAfter,rejectedAfter);
        Check(router.Rejected>=4&&router.PendingCount==0,
            "hero, weapon, previously existing entity, and mismatched visual entity are rejected");
        Check(!router.TryHandleSummonVoice(heroSummon,out cardId,out eventKey)&&
            !router.TryHandleSummonVoice(weaponSummon,out cardId,out eventKey),
            "hero and weapon events cannot be consumed as minion source voices");

        var cleanupAfter=Copy(rejectedAfter);cleanupAfter.version=9;
        var cleanupMinion=new EntityView{entity=803,card_id="CS2_182"};
        cleanupAfter.players[0].field=cleanupAfter.players[0].field.Concat(new[]{cleanupMinion}).ToArray();
        var unhandled=new SemanticEvent{type="Summon",event_id="cleanup-1",target=803,card_type=4,visual=cleanupMinion};
        cleanupAfter.events=new[]{unhandled};
        router.ObserveSummonVoice(rejectedAfter,cleanupAfter);
        Check(router.PendingCount==1,"unplayed source cue stays bounded until presentation completion");
        router.Complete(cleanupAfter);
        Check(router.PendingCount==0&&!router.TryHandleSummonVoice(unhandled,out cardId,out eventKey),
            "presentation completion removes an unconsumed summon binding");

        VerifyCalibrationPersistence();
        VerifyRotatedEmoteHitArea();
        Debug.Log("F024 BATTLE CHECKS PASSED: "+checks);
    }

    static void VerifyRotatedEmoteHitArea()
    {
        var previous=UICalibration.Current;
        var calibration=new UICalibration("../work/f024-emote-hit-check.json");
        try
        {
            UICalibration.Current=calibration;
            calibration.Set(new UIAdjustment{id="battle.emotes/THANKS",angle=45,scaleX=1.4f,scaleY=.8f});
            var menu=new HeroEmoteMenu();var hero=new Rect(865,700,190,200);
            Check(menu.ReplayRightClick(hero.center,hero,"rotation-check"),
                "hero right-click opens the emote menu for rotated hit testing");
            var imageRects=(Rect[])typeof(HeroEmoteMenu).GetField("imageRects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            var hitRects=(Rect[])typeof(HeroEmoteMenu).GetField("hitRects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            var matrices=new Matrix4x4[HeroEmoteMenu.Options.Length];var bounds=new Rect[HeroEmoteMenu.Options.Length];
            for(int i=0;i<matrices.Length;i++)
            {
                matrices[i]=calibration.Get("battle.emotes/"+HeroEmoteMenu.Options[i]).Matrix(imageRects[i],Vector2.one);
                bounds[i]=menu.OptionHitRect(HeroEmoteMenu.Options[i]);
            }
            Vector2 emptyCorner=Vector2.zero;bool found=false;
            for(int i=0;i<bounds.Length&&!found;i++)
                for(float x=bounds[i].xMin+2;x<bounds[i].xMax-1&&!found;x+=3)
                    for(float y=bounds[i].yMin+2;y<bounds[i].yMax-1;y+=3)
                    {
                        var point=new Vector2(x,y);
                        if(hero.Contains(point)||!bounds.Any(rect=>rect.Contains(point)))continue;
                        bool actualHit=false;
                        for(int target=0;target<hitRects.Length;target++)
                            if(hitRects[target].Contains(matrices[target].inverse.MultiplyPoint3x4(point)))
                            {actualHit=true;break;}
                        if(actualHit)continue;
                        emptyCorner=point;found=true;break;
                    }
            Check(found,"rotated AABB exposes an empty corner outside every real emote hit rect");
            bool consumed;
            menu.ReplayInput(EventType.MouseDown,1,KeyCode.None,emptyCorner,hero,"rotation-check",out consumed);
            Check(consumed&&!menu.IsOpen,
                "right-click in a rotated AABB empty corner closes the menu instead of hitting an emote");
        }
        finally{UICalibration.Current=previous;}
    }

    static void VerifyCalibrationPersistence()
    {
        string folder=Path.GetFullPath("../work/f024-battle-checks");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,Guid.NewGuid()+".json");
        var calibration=new UICalibration(path);
        calibration.Set(new UIAdjustment{id="battle.speech/own",x=24,y=-13,angle=9,scaleX=1.12f,scaleY=.92f});
        calibration.Set(new UIAdjustment{id="battle.speech/own/text",x=2,y=3,scaleX=1.03f,scaleY=.97f,
            text=new UITextStyle{fillOverride=true,fill="#EEDDCCFF",outline=false,shadow=false,size=31,fontMode=2}});
        calibration.Set(new UIAdjustment{id="battle.speech/enemy",x=-18,y=12,angle=-7,scaleX=1.08f,scaleY=.94f});
        calibration.Set(new UIAdjustment{id="battle.speech/enemy/text",x=-3,y=4,
            text=new UITextStyle{fillOverride=true,fill="#DDEEFFCC",outline=true,shadow=false,size=27,fontMode=1}});
        string[] emoteFills={"#FFB000FF","#22AAFFFF","#66CC66FF","#CC66CCFF","#FF6666FF","#66CCCCFF","#CCCC66FF"};
        for(int i=0;i<HeroEmoteMenu.Options.Length;i++)
        {
            string id=HeroEmoteMenu.Options[i];
            calibration.Set(new UIAdjustment{id="battle.emotes/"+id,x=1,angle=2,scaleX=1.01f,scaleY=.99f});
            calibration.Set(new UIAdjustment{id="battle.emotes/"+id+"/text",
                text=new UITextStyle{fillOverride=true,fill=emoteFills[i],outline=i%2==0,shadow=i%2!=0,size=18+i,fontMode=i%5}});
        }
        string error;Check(calibration.Save(out error),"both speech bubbles and seven emote text styles save without touching the user's config");
        var restored=new UICalibration(path);
        Check(restored.Load(out error),"battle-specific calibration reload succeeds");
        var speech=restored.Get("battle.speech/own");
        Check(speech.x==24&&speech.y==-13&&speech.angle==9&&Mathf.Approximately(speech.scaleX,1.12f),
            "speech position, scale, and rotation round-trip");
        var style=restored.TextStyle("battle.speech/own/text");
        Check(style!=null&&style.fillOverride&&style.fill=="#EEDDCCFF"&&!style.outline&&!style.shadow&&style.size==31,
            "speech text style round-trips");
        var enemy=restored.Get("battle.speech/enemy");
        var enemyStyle=restored.TextStyle("battle.speech/enemy/text");
        Check(enemy.x==-18&&enemy.y==12&&enemy.angle==-7&&Mathf.Approximately(enemy.scaleY,.94f)&&
            enemyStyle!=null&&enemyStyle.fill=="#DDEEFFCC"&&enemyStyle.size==27,
            "enemy speech bubble geometry and text style round-trip independently");
        bool allEmoteStyles=HeroEmoteMenu.Options.Length==7;
        for(int i=0;i<HeroEmoteMenu.Options.Length;i++)
        {
            string id=HeroEmoteMenu.Options[i];var emote=restored.Get("battle.emotes/"+id);
            var emoteStyle=restored.TextStyle("battle.emotes/"+id+"/text");
            allEmoteStyles=allEmoteStyles&&Mathf.Approximately(emote.scaleX,1.01f)&&emoteStyle!=null&&
                emoteStyle.fillOverride&&emoteStyle.fill==emoteFills[i]&&emoteStyle.size==18+i;
        }
        Check(allEmoteStyles,
            "all seven menu option layouts and text styles round-trip independently");
    }
}
