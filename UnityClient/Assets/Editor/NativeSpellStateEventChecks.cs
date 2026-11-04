using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSpellStateEventChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Source animation event: "+message);passed++;}
    public static void Run()
    {
        passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        try
        {
            foreach(string file in new[]{"23d38186cee691b6a6171c7a","2a965e276d24b2db14e2483b","bf21da970c07c866f15b2302","c7109e87ad46c02a4a7e74ed"})
            using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId=="CS2_179"),"minion",true))
            {
                var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".actor-spell.json")));
                using(var effect=new NativeEffectInstance(source,assets,0,32,32))
                {
                    effect.AttachToOriginalParent(live.ActorObject);
                    effect.SelectSourceSpellState(1,0);
                    Check(effect.ActiveSourceSpellState==1,"Birth selected");
                    effect.Seek(.52f);Check(effect.ActiveSourceSpellState==1,"event-only clip survives until source callback");
                    effect.Seek(.54f);Check(effect.ActiveSourceSpellState==2,"source OnStateFinished selects Idle");
                    var birth=effect.AnimationEvents.First(e=>e.function=="OnStateFinished");
                    Check(birth.handled&&Mathf.Abs(birth.time-.53333336f)<.00001f,"exact original birth event timestamp");
                    effect.SelectSourceSpellState(5,.6f);
                    effect.Seek(1.10f);Check(!effect.SourceSpellFinished,"Death does not finish too early");
                    effect.Seek(1.12f);Check(effect.SourceSpellFinished&&effect.ActiveSourceSpellState==5,"OnSpellFinished unblocks before visual tail");
                    effect.Seek(1.15f);Check(effect.ActiveSourceSpellState==0,"death OnStateFinished selects None");
                    var notification=effect.FinishNotifications.First(n=>n.machine==-1);
                    Check(Mathf.Abs(notification.time-(.6f+.51666665f))<.00001f,"source finish callback time is retained");
                    Check(effect.AnimationEvents.Count(e=>e.function=="OnSpellFinished")==1,"finish event delivered once");
                    effect.SelectSourceSpellState(1,1.2f);effect.Seek(1.4f);effect.SelectSourceSpellState(1,1.4f);
                    effect.Seek(1.8f);Check(effect.ActiveSourceSpellState==1,"same state selection restarts its event clock");
                    effect.HideSourceSpellStates();int count=effect.AnimationEvents.Count;
                    effect.Seek(2.1f);Check(effect.AnimationEvents.Count==count,"hidden clips cannot emit callbacks");
                    effect.ShowSourceSpellStates(2.1f);effect.Seek(2.64f);
                    Check(effect.ActiveSourceSpellState==2,"shown state replays callback at new source time");
                    var cancel=source.spell_states.FirstOrDefault(s=>s.type==4);
                    if(cancel!=null&&cancel.animations.Any(a=>a.enabled&&source.animations.Any(c=>c.id==a.clip&&(c.events??new NativeAnimationEvent[0]).Any(e=>e.function=="OnSpellFinished"))))
                    {
                        effect.SelectSourceSpellState(4,2.7f);effect.Seek(2.71f);
                        Check(!effect.SourceSpellFinished,"Cancel short source delay retained");
                        effect.Seek(2.72f);Check(effect.SourceSpellFinished&&effect.ActiveSourceSpellState==4,"Cancel finish precedes state exit");
                        effect.Seek(3.01f);Check(effect.ActiveSourceSpellState==0,"Cancel source state exits after tail");
                    }
                    else if(cancel!=null)
                    {
                        effect.SelectSourceSpellState(4,2.7f);effect.Seek(3.01f);
                        Check(!effect.SourceSpellFinished&&effect.ActiveSourceSpellState==4,"disabled Cancel bindings do not synthesize finish events");
                    }
                    if(file=="23d38186cee691b6a6171c7a")
                    {
                        effect.SelectSourceSpellState(3,3.2f);effect.Seek(3.3f);
                        Check(effect.AnimationEvents.Any(e=>e.function=="PlayDefaultSound"&&e.handled)&&effect.UnboundAnimationEvents==0&&effect.SourceEventAudioStarts==1,"original default sound callback resolved from existing source");
                        Check(effect.SourceParticleEvents>0,"actual original particle callbacks executed");
                    }
                    Debug.Log("SOURCE STATE EVENT PREFAB "+file+": changes="+effect.SourceStateChanges+" events="+effect.AnimationEvents.Count+" particle_events="+effect.SourceParticleEvents+" unbound="+effect.UnboundAnimationEvents);
                }
            }
        }
        finally{visuals.Clear();}
        Debug.Log("NATIVE SPELL STATE EVENTS: "+passed+" checks passed; original controller and all callbacks remain incomplete");
        NativeSpellStatePlaybackChecks.Run();
    }
}
