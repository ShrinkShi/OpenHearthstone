using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSpellStatePlaybackChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("SpellState: "+message);passed++;}
    public static void Run()
    {
        passed=0;int plays=0,stops=0;bool active=false;float started=-1;
        var state=new NativeSourceSpellState{delay=.4f};
        var clock=new NativeSpellStatePlayback(state,v=>active=v,t=>{plays++;started=t;},next=>stops++);
        clock.Play(0);Check(clock.Playing&&plays==0,"Play before Start records intent");
        clock.Initialize(0);clock.Tick(.39f);Check(active&&plays==0,"source delay is respected");
        clock.Tick(.4f);Check(plays==1&&started==.4f,"delayed PlayNow");
        clock.Hide();Check(!active&&clock.Playing&&!clock.Shown&&stops==1,"Hide preserves playing flag");
        clock.Show(1);clock.Tick(1.4f);Check(active&&plays==2,"Show replays shown state");
        clock.Stop();Check(!active&&!clock.Playing&&stops==2,"Stop resets playing");
        clock.Play(2);clock.Hide();clock.Tick(3);Check(plays==2,"deactivation cancels component coroutine");
        clock.Stop();clock.Play(3);Check(!clock.Playing,"hidden Play ignored");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        int programs=0,states=0,starts=0;
        try
        {
            foreach(var path in Directory.GetFiles(assets.Root,"*.actor-spell.json").OrderBy(p=>p))
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path));
                if(program.spell_states==null||program.spell_states.Length==0)continue;
                programs++;states+=program.spell_states.Length;
                using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId=="CS2_179"),"minion",true))
                {
                    var actor=live.ActorObject;var position=actor.transform.localPosition;var scale=actor.transform.localScale;var rotation=actor.transform.localRotation;
                    using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                    {
                        effect.AttachToOriginalParent(actor);float time=0;
                        foreach(var source in program.spell_states)
                        {
                            if(!source.enabled)
                            {
                                bool initial=effect.Nodes[source.node].activeSelf;
                                effect.SelectSourceSpellState(source.type,time);
                                Check(effect.Nodes[source.node].activeSelf==initial&&effect.SourceStateAnimationStarts==0,"disabled source component is not initialized");
                                continue;
                            }
                            effect.SelectSourceSpellState(source.type,time);
                            Check(effect.Nodes[source.node].activeSelf,"real source state active: "+Path.GetFileName(path));
                            effect.Seek(time+.1f);effect.HideSourceSpellStates();
                            Check(!effect.Nodes[source.node].activeSelf,"real source state hidden: "+Path.GetFileName(path)+" type "+source.type+" enabled "+source.enabled);
                            effect.ShowSourceSpellStates(time+.1f);effect.Seek(time+.2f);time+=.3f;
                        }
                        Check(effect.UnboundSourceStateObjects==0,"all source bindings resolve: "+Path.GetFileName(path)+" count "+effect.UnboundSourceStateObjects);
                        starts+=effect.SourceStateAnimationStarts;
                    }
                    Check(actor.transform.localPosition==position&&actor.transform.localScale==scale&&actor.transform.localRotation==rotation,"borrowed Actor pose restored");
                    Check(actor.GetComponent<Animation>()==null,"borrowed Animation released");
                }
            }
        }
        finally{visuals.Clear();}
        Check(programs==8&&states==23&&starts>0,"all exported source states played");
        Debug.Log("NATIVE SPELL STATE PLAYBACK: "+passed+" checks; "+programs+" programs; "+states+" states; "+starts+" animation starts; full controller restored=False");
        NativeEffectChecks.Run();NativeEntryGraphChecks.RunGpu();
    }
}
