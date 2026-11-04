using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Diagnostic source lifecycle survey, not a card-fidelity approval gate.
public static class NativeITweenSurvey
{
    [Serializable] sealed class Sample
    {
        public string card,file,lifecycle,error;public int source_type,declared_actions,unbound_actions,unbound_actor_actions,actor_object_bindings,completed_actions,stopped_clocks,transition_limits,visible_samples,max_pixels;
        public int unbound_actor_visibility_actions,actor_root_visibility_bindings,unrestored_actor_visibility_side_effects,unbound_actor_initialization_actions;
        public string[] states;
    }
    [Serializable] sealed class Report
    {public string kind="original-itween-lifecycle-diagnostic";public bool full_fidelity=false;public int programs,actions,frames;public int[] source_eases;public Sample[] samples;}
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var entries=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json"))).entries;
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        string folder=Path.GetFullPath("../tests/visual/current/native-itween");Directory.CreateDirectory(folder);
        var samples=new List<Sample>();var eases=new HashSet<int>();int programs=0,actions=0,frames=0;
        var previous=RenderTexture.active;
        try
        {
            foreach(var entry in entries.GroupBy(e=>e.file).Select(g=>g.First()))
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
                var declared=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="iTweenScaleTo"||a.name=="iTweenScaleFrom"||a.name=="iTweenFadeToAction").ToArray();
                if(declared.Length==0)continue;programs++;actions+=declared.Length;
                foreach(var action in declared)
                {int ease=(int)action.Param("easeType").number;eases.Add(ease);if(!NativeITween.SupportedEase(ease))throw new Exception("Unrestored source easing "+ease);}
                var card=database.cards.FirstOrDefault(c=>c.CardId==entry.card_id.Split('@')[0]);int source=card==null?5:card.Type;
                var events=program.machines.SelectMany(m=>m.events).Select(e=>e.@event).Where(e=>e=="Birth"||e=="Death"||e=="Action"||e=="Cancel").Distinct().ToArray();
                if(events.Length==0)events=new[]{"default"};
                foreach(string lifecycle in events)
                {
                    var sample=new Sample{card=entry.card_id,file=entry.file,lifecycle=lifecycle,source_type=source,declared_actions=declared.Length};samples.Add(sample);
                    try
                    {
                        using(var instance=new NativeEffectInstance(program,assets,0,128,128,()=>Color.white,new NativeSpellContext(source,4,3,lifecycle:lifecycle=="default"?null:lifecycle)))
                        {
                            foreach(float time in new[]{.12f,.3f,.6f,1f,2f,3f})
                            {
                                var texture=instance.Render(time);RenderTexture.active=texture;
                                var image=new Texture2D(128,128,TextureFormat.RGBA32,false);
                                try
                                {
                                    image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                                    int pixels=image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3);
                                    sample.max_pixels=Math.Max(sample.max_pixels,pixels);if(pixels>0)sample.visible_samples++;frames++;
                                    if(time==.3f||time==1)
                                        File.WriteAllBytes(Path.Combine(folder,Path.GetFileNameWithoutExtension(entry.file)+"-"+lifecycle+"-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                                }
                                finally{AssetRegistry.ReleaseTransient(image);RenderTexture.active=previous;}
                            }
                            sample.unbound_actions=instance.UnboundITweenActions;sample.completed_actions=instance.CompletedITweenActions;
                            sample.unbound_actor_actions=instance.UnboundActorActions;sample.actor_object_bindings=instance.ActorObjectBindings;
                            sample.unbound_actor_visibility_actions=instance.UnboundActorVisibilityActions;sample.actor_root_visibility_bindings=instance.ActorRootVisibilityBindings;sample.unrestored_actor_visibility_side_effects=instance.UnrestoredActorVisibilitySideEffects;
                            sample.unbound_actor_initialization_actions=instance.UnboundActorInitializationActions;
                            sample.stopped_clocks=instance.StoppedITweens;sample.transition_limits=instance.TransitionLimitHits;sample.states=instance.CurrentStates;
                        }
                    }
                    catch(Exception error){sample.error=error.GetType().Name+": "+error.Message;}
                }
            }
        }
        finally{RenderTexture.active=previous;}
        var report=new Report{programs=programs,actions=actions,frames=frames,source_eases=eases.OrderBy(e=>e).ToArray(),samples=samples.ToArray()};
        File.WriteAllText("../work/native-itween-survey.json",JsonUtility.ToJson(report,true));
        Debug.Log("NATIVE ITWEEN SURVEY: "+programs+" programs / "+actions+" actions / "+samples.Count+" lifecycle scenarios / "+frames+" frames / "+samples.Count(s=>s.error!=null)+" exceptions");
    }
}
