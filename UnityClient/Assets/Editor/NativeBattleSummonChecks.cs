using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeBattleSummonChecks
{
    public static void Run(){Verify(false);}
    public static void RunGpu(){Verify(true);}
    static void Verify(bool render)
    {
        int passed=0,frames=0;Action<bool,string> check=(value,message)=>{if(!value)throw new Exception("Battle summon: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id));
        var summons=new NativeBattleSummons(assets,actors);int sounds=0;summons.AudioCue=c=>sounds++;
        var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/minion_summon.json"));
        var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/minion_summon-after.json"));
        var data=after.events.First(e=>e.type=="Summon");var entity=after.players[after.seat].field.First(e=>e.entity==data.target);
        var presentation=new Presentation{NativeSummonPrepare=summons.Prepare,NativeSummonActive=summons.Has,NativeSummonDuration=summons.Duration};
        var locations=new Dictionary<int,Rect>();presentation.Reset(before);presentation.Accept(before,after,locations);
        var cue=presentation.Cues.First(c=>c.data.type=="Summon");
        check(Mathf.Abs(summons.SourceDuration-2.56f)<.0001f,"source FINISHED path retains both original Wait durations");
        check(Mathf.Abs(summons.BlockingDuration-.56f)<.0001f,"SpellFinishAction establishes source unblock boundary");
        check(cue.duration==summons.BlockingDuration,"real presentation reserves only source blocking duration");
        string folder="../tests/visual/current/native-battle-summon";if(render)Directory.CreateDirectory(folder);
        Texture2D image=render?new Texture2D(512,640,TextureFormat.RGBA32,false):null;var previous=RenderTexture.active;float last=0;
        try
        {
            foreach(float age in new[]{0f,.15f,.3f,.6f,1.2f,2.5f})
            {
                float time=cue.start+age;float delta=time-last;bool wasTail=summons.TailCount>0;presentation.Tick(delta,locations);last=time;
                if(wasTail)summons.Advance(delta,presentation.Display);
                summons.BeginFrame(presentation.NativeSummonKeys());presentation.PrepareNative();
                check(summons.ActiveCount==1&&summons.Failures==0,"actual Summon cue reaches native local table at "+age);
                var actor=actors.Resolve(entity);var live=actors.Find(entity);var identity=actor.GetComponent<NativeActorIdentity>();
                check(actors.Highlight(entity,assets,1)&&ReferenceEquals(actor,actors.Resolve(entity)),"source idle highlight and summon share actor ownership at "+age);
                check(identity!=null&&identity.SpellDefinition!=null&&identity.AnimationProgram!=null,"battle adapter supplies true original Actor at "+age);
                check(actor.GetComponent<Animation>()!=null,"source animation drives real battle Actor at "+age);
                var rect=new Rect(800,600,130,170);check(presentation.Animate(entity.entity,rect)==rect,"old 2D summon scaling is suppressed at "+age);
                check(sounds==(age>=.6f?1:0),"source sound callback is unique and timed at "+age);
                if(age>=.6f)check(summons.TailCount==1,"cleanup survives after blocking cue at "+age);
                if(age>=1.2f)check(!presentation.Busy,"source cleanup tail does not keep input queue busy at "+age);
                actors.Retain(summons.BorrowedActors);check(actors.Count==1,"live actor survives production retain pass at "+age);
                if(render)
                {
                    RenderTexture.active=(RenderTexture)live.Render();image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>32,"production Actor capture is visible at "+age);
                    if(age>=.6f){Color center=image.GetPixel(256,320);check(Mathf.Max(center.r,center.g,center.b)>.02f,"production portrait itself remains visible at "+age);}
                    File.WriteAllBytes(Path.Combine(folder,"CS2_182-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());frames++;
                }
            }
            var enemy=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(after));enemy.seat=1;
            check(summons.Handles(data,enemy)&&Mathf.Abs(summons.Duration(data,enemy)-.49f)<.0001f,"opponent slot has its own source .43 second landing boundary");
            check(!summons.Prepare("enemy",data,before,enemy,.2f),"same Actor cannot have two simultaneous animation writers");
            summons.BeginFrame(new string[0]);check(summons.ActiveCount==1,"cue expiry retains original cleanup tail");
            summons.Advance(.2f,after);check(summons.ActiveCount==0,"source cleanup expiry releases table instance");
            var restored=actors.Resolve(entity);check(restored.transform.localPosition==Vector3.zero,"expired animation restores original board slot pose");
            actors.Retain(new GameObject[0]);check(actors.Count==0,"expired production actor is released");
            check(summons.Prepare("replay",data,before,after,.6f)&&sounds==2,"fresh invocation can play sound on a fresh Actor");
            summons.BeginFrame(new string[0]);check(summons.TailCount==1,"fresh invocation can enter independent cleanup");
            summons.Advance(0,after);check(summons.TailCount==1&&sounds==2,"paused cleanup clock preserves particles and does not replay sound");
            var removed=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(after));removed.players[removed.seat].field=removed.players[removed.seat].field.Where(e=>e.entity!=data.target).ToArray();
            summons.Advance(.01f,removed);check(summons.ActiveCount==0,"removed visual entity cancels retained tail safely");
            var other=after.players[after.seat].field.First(e=>e.entity!=data.target);
            var second=new SemanticEvent{type="Summon",source=other.entity,target=other.entity,owner=after.seat};
            check(summons.Prepare("parallel-a",data,before,after,.6f),"first source tail can start after visual cancellation");
            check(summons.Prepare("parallel-b",second,before,after,.15f),"another Actor can summon while first tail remains alive");
            summons.BeginFrame(new string[0]);check(summons.TailCount==2&&sounds==4,"parallel tails keep independent Actor and audio clocks");
            actors.Retain(summons.BorrowedActors);check(actors.Count==2,"both cleanup actors survive shared retention");
            var firstActor=actors.Find(entity);var otherActor=actors.Find(other);
            check(firstActor.ActorObject!=otherActor.ActorObject,"parallel source effects do not share an Actor");
            summons.Advance(.25f,after);check(summons.TailCount==2&&sounds==4,"independent tail advancement cannot duplicate either audio cue");
            if(render)
            {
                foreach(var live in new[]{firstActor,otherActor})
                {
                    RenderTexture.active=(RenderTexture)live.Render();image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>32,"concurrent source tail retains visible independent capture");
                    File.WriteAllBytes(Path.Combine(folder,live.CardId+"-parallel.png"),image.EncodeToPNG());frames++;
                }
            }
            summons.Advance(3,after);check(summons.ActiveCount==0,"large developer step cleans up all parallel tails");
            actors.Retain(new GameObject[0]);int enemySounds=sounds;
            var opponentBefore=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));opponentBefore.seat=enemy.seat;
            var opponentPresentation=new Presentation{NativeSummonPrepare=summons.Prepare,NativeSummonActive=summons.Has,NativeSummonDuration=summons.Duration};
            opponentPresentation.Reset(opponentBefore);opponentPresentation.Accept(opponentBefore,enemy,locations);
            var opponentCue=opponentPresentation.Cues.First(c=>c.data.type=="Summon");last=0;
            check(Mathf.Abs(opponentCue.duration-.49f)<.0001f,"opponent production queue reserves its own source completion time");
            foreach(float age in new[]{0f,.15f,.3f,.44f,.5f,2.4f})
            {
                float time=opponentCue.start+age;float delta=time-last;bool wasTail=summons.TailCount>0;
                opponentPresentation.Tick(delta,locations);last=time;if(wasTail)summons.Advance(delta,opponentPresentation.Display);
                summons.BeginFrame(opponentPresentation.NativeSummonKeys());opponentPresentation.PrepareNative();
                var rect=new Rect(800,200,130,170);
                check(opponentPresentation.Animate(entity.entity,rect)==rect,"opponent native cue suppresses old scaling at "+age);
                check(summons.ActiveCount==1&&summons.Failures==0,"opponent source has no unbound actions at "+age);
                check(sounds==enemySounds+(age>=.44f?1:0),"opponent audio follows its source .43 second Wait at "+age);
                if(age>=.5f)check(summons.TailCount==1,"opponent cue completion keeps original cleanup independently at "+age);
                if(render)
                {
                    var live=actors.Find(entity);RenderTexture.active=(RenderTexture)live.Render();
                    image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>32,"opponent Actor capture is visible at "+age);
                    if(age>=.5f){Color center=image.GetPixel(256,320);check(Mathf.Max(center.r,center.g,center.b)>.02f,"opponent portrait itself remains visible at "+age);}
                    File.WriteAllBytes(Path.Combine(folder,"CS2_182-opponent-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());frames++;
                }
            }
            summons.BeginFrame(new string[0]);check(summons.TailCount==1,"opponent completion retains its own cleanup tail");
            summons.Advance(.11f,enemy);check(summons.ActiveCount==0,"opponent tail expires at its source duration rather than friendly duration");
            check(!summons.Handles(new SemanticEvent{type="Summon",target=-100},enemy),"missing entity has no original summon route");
            summons.Dispose();check(summons.ActiveCount==0,"battle reset releases all source effects");
        }
        finally{RenderTexture.active=previous;summons.Dispose();actors.Dispose();visuals.Clear();if(image!=null)AssetRegistry.ReleaseTransient(image);}
        File.WriteAllText("../work/native-battle-summon"+(render?"-gpu":"")+"-checks.json","{\"passed\":"+passed+",\"frames\":"+frames+",\"production_route\":true,\"enemy_restored\":true,\"complete\":false}");
        Debug.Log("NATIVE BATTLE SUMMON"+(render?" GPU":"")+" CHECKS PASSED: "+passed);
    }
}
