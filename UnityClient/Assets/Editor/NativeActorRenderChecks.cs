using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorRenderChecks
{
    public static void RunAll(){Run();RunTimeline();RunVisibility();RunScaleFrom();RunOriginalTemplates();NativeActorMaterialChecks.RunGpu();NativeHighlightMaterialChecks.RunGpu();NativeHighlightSilhouetteChecks.RunGpu();NativeShaderTimeChecks.RunGpu();NativeBattleHighlightChecks.RunGpu();NativePortraitTextureChecks.RunGpu();NativeCustomSpawnChecks.RunGpu();NativeTimelineChecks.RunGpu();NativeStaticActorChecks.RunGpu();NativeActorSpellRenderChecks.Run();NativeBattleSummonChecks.RunGpu();NativeEffectRenderChecks.Run();NativeITweenSurvey.Run();}
    public static void RunOriginalTemplates()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));int passed=0,frames=0;var previous=RenderTexture.active;
        string folder=Path.GetFullPath("../tests/visual/current/native-templates");Directory.CreateDirectory(folder);
        foreach(var pair in new[]{new{model="MODEL_ACTOR_MINION",card="CS2_179"},new{model="MODEL_ACTOR_HERO",card="HERO_08"}})
        {
            var scene=assets.Scene(pair.model);scene.transform.position=new Vector3(45000,-1000,0);
            var actor=scene.GetComponentInChildren<NativeActorIdentity>(true);var renderer=actor.MeshRenderer;
            foreach(var r in scene.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=30;r.enabled=r==renderer;}
            renderer.sharedMaterials[actor.PortraitMaterialIndex].mainTexture=assets.Texture("CARD_"+pair.card);
            var camera=new GameObject("Original Actor template test camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.transform.rotation=Quaternion.Euler(90,0,0);var bounds=renderer.bounds;camera.aspect=.8f;
            camera.orthographicSize=Mathf.Max(bounds.size.z/2,bounds.size.x/2/camera.aspect)*1.5f;
            camera.transform.position=new Vector3(bounds.center.x,bounds.max.y+20,bounds.center.z);
            var output=new RenderTexture(256,320,24,RenderTextureFormat.ARGB32);output.Create();camera.targetTexture=output;
            var image=new Texture2D(256,320,TextureFormat.RGBA32,false);
            try
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/076d511f7b25ad78f327937e.native-effect.json"));
                var scale=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="iTweenScaleFrom");
                var init=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/d089ebacb048f027de7a3178.native-effect.json")).machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellInitActorVariables");
                program.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Init",events=new NativeTransition[0],states=new[]{new NativeState{name="Init",actions=new[]{init,scale},transitions=new NativeTransition[0]}}}};
                using(var instance=new NativeEffectInstance(program,assets,0,32,32))
                {
                    instance.Root.transform.SetParent(actor.transform,true);
                    foreach(float time in new[]{0f,.2f,.4f})
                    {
                        instance.Seek(time);camera.Render();RenderTexture.active=output;image.ReadPixels(new Rect(0,0,256,320),0,0);image.Apply();
                        if(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)<4)throw new Exception("Original Actor template render is blank: "+pair.model);passed++;
                        if(instance.UnboundActorInitializationActions!=0||instance.UnboundITweenActions!=0||instance.ActorObjectBindings!=3)throw new Exception("Original Actor template initializer failed");passed++;
                        File.WriteAllBytes(Path.Combine(folder,pair.model+"-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());frames++;
                    }
                }
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;AssetRegistry.ReleaseTransient(output);AssetRegistry.ReleaseTransient(image);AssetRegistry.ReleaseTransient(camera.gameObject);foreach(var r in scene.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)AssetRegistry.ReleaseTransient(m);AssetRegistry.ReleaseTransient(scene);}
        }
        File.WriteAllText("../work/native-template-render-checks.json","{\"passed\":"+passed+",\"frames\":"+frames+",\"actor_complete\":false,\"production_parenting\":false}");
        Debug.Log("NATIVE TEMPLATE GPU CHECKS PASSED: "+passed);
    }
    public static void RunScaleFrom()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId=="CS2_179"));var actor=live.Model;
        var image=new Texture2D(512,640,TextureFormat.RGBA32,false);var previous=RenderTexture.active;int passed=0;
        string folder=Path.GetFullPath("../tests/visual/current/native-scalefrom");Directory.CreateDirectory(folder);
        try
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/076d511f7b25ad78f327937e.native-effect.json"));
            var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="iTweenScaleFrom");
            var getter=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/81adc1ee5e4a815a745824b5.native-effect.json")).machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellGetActorAction"&&a.Param("m_GameObject").variable=="Actor");
            // Original actions with an explicit test Actor binding, not the complete spawn controller.
            program.machines=new[]{new NativeMachine{start="From",events=new NativeTransition[0],states=new[]{new NativeState{name="From",actions=new[]{getter,action},transitions=new NativeTransition[0]}}}};
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,4,actorObjects:which=>actor)))
            {
                foreach(float time in new[]{0f,.1f,.2f,.4f})
                {
                    instance.Seek(time);float expected=NativeITween.Sample(.1f,1,time/.4f,27);
                    if(Vector3.Distance(actor.transform.localScale,Vector3.one*expected)>.0001f)throw new Exception("Original ScaleFrom GPU pose diverged");passed++;
                    RenderTexture.active=(RenderTexture)live.Render();image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    if(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)<8)throw new Exception("ScaleFrom model GPU capture blank");passed++;
                    File.WriteAllBytes(Path.Combine(folder,"CS2_179-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                }
            }
        }
        finally{RenderTexture.active=previous;live.Dispose();visuals.Clear();AssetRegistry.ReleaseTransient(image);}
        File.WriteAllText("../work/native-scalefrom-render-checks.json","{\"passed\":"+passed+",\"frames\":4,\"effect_complete\":false,\"source_controller_complete\":false}");
        Debug.Log("NATIVE SCALEFROM GPU CHECKS PASSED: "+passed);
    }
    public static void RunVisibility()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var card=database.cards.First(c=>c.CardId=="CS2_179");var live=visuals.CreateLiveActor(card);var actor=live.Model;
        var image=new Texture2D(512,640,TextureFormat.RGBA32,false);var previous=RenderTexture.active;int passed=0;
        string folder=Path.GetFullPath("../tests/visual/current/native-visibility");Directory.CreateDirectory(folder);
        try
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/13e4ba2cda5d567ad3d3e0be.native-effect.json"));
            var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="ActorSetVisibilityRecursiveAction");
            var getProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/81adc1ee5e4a815a745824b5.native-effect.json"));
            var get=getProgram.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellGetActorAction"&&a.Param("m_GameObject").variable=="Actor");
            action.Param("m_GameObject").node=-1;action.Param("m_GameObject").variable="Actor";
            // A binding fixture using source actions, not an original card sequence.
            program.machines=new[]{new NativeMachine{start="Hide",events=new NativeTransition[0],states=new[]{new NativeState{name="Hide",actions=new[]{get,action},transitions=new NativeTransition[0]}}}};
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,4,actorObjects:which=>actor)))
            {
                instance.Seek(0);Capture("hidden",false);if(live.Visible)throw new Exception("Hidden Actor retained HUD visibility");passed++;
            }
            Capture("restored",true);if(!live.Visible)throw new Exception("Disposed effect did not restore Actor HUD visibility");passed++;
        }
        finally{RenderTexture.active=previous;live.Dispose();visuals.Clear();AssetRegistry.ReleaseTransient(image);}
        void Capture(string label,bool visible)
        {
            RenderTexture.active=(RenderTexture)live.Render();image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
            int pixels=image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3);
            if(visible?pixels<32:pixels!=0)throw new Exception("Actor visibility did not reach GPU: "+label);passed++;
            File.WriteAllBytes(Path.Combine(folder,"CS2_179-"+label+".png"),image.EncodeToPNG());
        }
        File.WriteAllText("../work/native-visibility-render-checks.json","{\"passed\":"+passed+",\"frames\":2,\"actor_complete\":false,\"original_sequence\":false}");
        Debug.Log("NATIVE VISIBILITY GPU CHECKS PASSED: "+passed);
    }
    public static void RunTimeline()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id));
        var entity=new EntityView{entity=202,card_id="CS2_179"};var actor=actors.Resolve(entity);var live=actors.Find(entity);
        var image=new Texture2D(512,640,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        string folder=Path.GetFullPath("../tests/visual/current/native-lifecycle");Directory.CreateDirectory(folder);int passed=0;
        try
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/81adc1ee5e4a815a745824b5.native-effect.json"));
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"None",actorObjects:which=>which==1?actor:null)))
            {
                instance.SetLifecycleTimeline(new[]{new NativeLifecycleEvent(1,.4f,NativeSpellStateType.Death)});
                foreach(float time in new[]{0f,.39f,.4f,.5f,1.41f})
                {
                    instance.Seek(time);RenderTexture.active=(RenderTexture)live.Render();
                    image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    int pixels=image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3);
                    if(time==.4f?pixels!=0:pixels<32)throw new Exception("Original timed lifecycle did not reach real Actor render");passed++;
                    File.WriteAllBytes(Path.Combine(folder,"RLK_086-Death-at-0.40-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                }
                instance.Seek(.5f);if(actor.transform.localScale.x<=1||instance.LifecycleChanges!=1||instance.UnboundLifecycleEvents!=0)throw new Exception("Lifecycle boundary/tween binding failed");passed++;
                instance.Seek(.39f);if(actor.transform.localScale!=Vector3.one||instance.LifecycleChanges!=0)throw new Exception("Lifecycle rewind was not deterministic");passed++;
            }
        }
        finally{RenderTexture.active=previous;actors.Dispose();visuals.Clear();AssetRegistry.ReleaseTransient(image);}
        File.WriteAllText("../work/native-lifecycle-render-checks.json","{\"passed\":"+passed+",\"frames\":5,\"effect_complete\":false,\"source_controller_complete\":false}");
        Debug.Log("NATIVE LIFECYCLE GPU CHECKS PASSED: "+passed);
    }
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id));
        var entity=new EntityView{entity=202,card_id="CS2_179"};var actor=actors.Resolve(entity);var live=actors.Find(entity);
        var image=new Texture2D(512,640,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        string folder=Path.GetFullPath("../tests/visual/current/native-actor");Directory.CreateDirectory(folder);int passed=0;
        try
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/81adc1ee5e4a815a745824b5.native-effect.json"));
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Death",actorObjects:which=>which==1?actor:null)))
            {
                var rect=new Rect(600,500,134,172);int normalPixels=0;
                foreach(float time in new[]{0f,.1f,.3f,.5f,1.01f})
                {
                    instance.Seek(time);RenderTexture.active=(RenderTexture)live.Render();
                    image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();
                    int pixels=image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3);
                    if(time==0&&pixels!=0)throw new Exception("Original GetActor/SetScale failed to hide real minion geometry");
                    if(time>0&&pixels<32)throw new Exception("Original ScaleTo failed to reveal real minion geometry");passed++;
                    if(time==0&&live.Visible)throw new Exception("Battle UI did not suppress hidden Actor badges");
                    if(time>0&&!live.Visible)throw new Exception("Battle UI did not restore Actor badges");passed++;
                    if(time==.1f&&live.StatusRect(rect).width<=rect.width)throw new Exception("Battle status geometry lost actual Actor overshoot");
                    if(time==1.01f){normalPixels=pixels;if(live.StatusRect(rect)!=rect)throw new Exception("Battle status geometry did not return to original layout");}
                    File.WriteAllBytes(Path.Combine(folder,"RLK_086-target-CS2_179-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                }
                if(instance.UnboundITweenActions!=0||instance.ActorObjectBindings==0)throw new Exception("Original actor variable not bound");passed++;
                instance.Seek(.1f);if(actor.transform.localScale.x<=1)throw new Exception("Original elastic overshoot lost");passed++;
                if(normalPixels==0||actors.Resolve(entity)!=actor||actors.Count!=1)throw new Exception("Actor entity identity/cache broken");passed++;
                if(actors.Resolve(new EntityView{entity=202,card_id="CS2_182"})!=null)throw new Exception("Morph must not silently reuse wrong borrowed model");passed++;
                if(actors.Resolve(new EntityView{entity=303,card_id="CS2_025"})!=null)throw new Exception("Unsupported card Actor must remain unbound");passed++;
            }
            if(actor==null||actor.transform.localScale!=Vector3.one)throw new Exception("Borrowed actor ownership/reset failed");passed++;
            actors.Retain(new[]{actor});if(actors.Count!=1)throw new Exception("Active native effect lost its borrowed Actor");passed++;
            actors.Retain(new GameObject[0]);if(actors.Count!=0||actors.Find(entity)!=null||actor!=null)throw new Exception("Finished native effect retained Actor camera/model");passed++;
        }
        finally
        {
            RenderTexture.active=previous;actors.Dispose();visuals.Clear();AssetRegistry.ReleaseTransient(image);
        }
        File.WriteAllText("../work/native-actor-render-checks.json","{\"passed\":"+passed+",\"frames\":5,\"real_game_ui_bound\":true,\"full_effect_restored\":false,\"capture\":\"offscreen_using_battle_adapter\"}");
        Debug.Log("NATIVE ACTOR GPU CHECKS PASSED: "+passed);
    }
}
