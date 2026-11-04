using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeShamanHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Shaman hero power: "+reason);passed++;}
    static Rect VisiblePixelBounds(Color32[] pixels,int width,int height)
    {
        int minX=width,minY=height,maxX=-1,maxY=-1;
        for(int i=0;i<pixels.Length;i++)
        {
            var color=pixels[i];if(Mathf.Max(color.r,Mathf.Max(color.g,color.b))<=8)continue;
            int x=i%width,y=i/width;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
        }
        return maxX<minX?Rect.zero:Rect.MinMaxRect(minX,minY,maxX+1,maxY+1);
    }

    public static void RunDisabledRoute()
    {
        passed=0;
        string root=Path.Combine(Path.GetTempPath(),"HearthstoneH02Route-"+Guid.NewGuid().ToString("N"));
        string runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);
        try
        {
            File.WriteAllText(Path.Combine(runtime,"registry.json"),"{\"entries\":[]}");
            File.WriteAllText(Path.Combine(runtime,"native-effect-index.json"),
                "{\"entries\":[{\"card_id\":\"HERO_02bp\",\"file\":\"source-only.json\",\"enabled\":true,\"duration\":4,\"replace\":true}]}");
            using(var player=new NativeEffectPlayer(new AssetRegistry(root)))
            {
                player.SuperSpellScene=(data,before,after,viewport,origin)=>null;
                Check(player.Duration("HERO_02bp")==0,
                    "an index row without source dependencies cannot open CustomSpawn playback");
                Check(!player.PrepareSource("h02-source",new SemanticEvent{type="SpellEffect",card_id="HERO_02bp"},null,null,false,0),
                    "source preparation fails closed before resolving a Totem Actor");
                Check(!player.Draw("h02-event",new SemanticEvent{type="SpellEffect",card_id="HERO_02bp"},null,null,new Rect(0,0,100,100),0),
                    "semantic-event drawing cannot enter the disabled source route");
                Check(!player.Draw("h02-direct","HERO_02bp",new Rect(0,0,100,100),0),
                    "direct card drawing cannot bypass the disabled source route");
                Check(player.ActiveCount==0,"the disabled route created no native effect instance");
            }
            Debug.Log("NATIVE SHAMAN HERO POWER: "+passed+" disabled-route checks passed");
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    public static void RunRuntimeRoute()
    {
        passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=Array.Find(index.entries,row=>row.card_id=="HERO_02bp");
        Check(entry!=null&&entry.enabled&&entry.duration>=2.5f,"imported index opens the bounded H02 source route");
        Check(NativeShamanHeroPowerPlayback.Supports(assets,entry),"source identity and both event-driven FSMs pass the runtime gate");
        var buttonProgram=NativeShamanHeroPowerPlayback.ForOwner(assets,entry,0);
        Check(buttonProgram.spell_transforms.Any(transform=>transform.enabled&&transform.node==0&&transform.location==4),
            "owner-0 retains its authored location-4 button placement after CustomSpawn subtree filtering");
        var buttonBounds=new Rect(200,300,122,126);
        var buttonFx=NativeShamanHeroPowerPlayback.ButtonEffectRect(buttonBounds);
        Check(Vector2.Distance(buttonFx.center,buttonBounds.center)<.001f&&
            Mathf.Abs(buttonFx.width/Mathf.Max(buttonBounds.width,buttonBounds.height)-1.55f)<.001f,
            "the rune halo stays on the real power button and scales from its calibrated bounds");
        var tokenBounds=new Rect(820,500,134,172);
        var summonFx=NativeShamanHeroPowerPlayback.SummonEffectRect(tokenBounds);
        Check(Vector2.Distance(summonFx.center,tokenBounds.center)<.001f&&
            Mathf.Abs(summonFx.width/Mathf.Max(tokenBounds.width,tokenBounds.height)-2f)<.001f,
            "the blue spawn overlay scales from the authoritative token bounds rather than a fixed center rect");
        var fullProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        Check(Mathf.Abs(fullProgram.nodes[1].particle.shape.radius-.17f)<.002f,
            "the selected H02 cache stores the scalar shape.radius expected by NativeShape");
        Check(NativeShamanHeroPowerPlayback.SupportsButtonRoot(NativeShamanHeroPowerPlayback.ForOwner(assets,entry,0)),
            "owner 0 binds MoteBurst and the TotemQuad glyph enabled by its FX_TotemFadeIn source animation");
        var fadeIn=fullProgram.animations.Single(animation=>animation.name=="FX_TotemFadeIn");
        var glyphMesh=assets.MeshFile(fullProgram.nodes[2].mesh);
        float peakScale=Mathf.Max(fadeIn.tracks.Single(track=>track.property=="scale.x").keys.Max(key=>key.value),
            fadeIn.tracks.Single(track=>track.property=="scale.z").keys.Max(key=>key.value));
        float peakHalfExtent=Mathf.Max(glyphMesh.bounds.extents.x,glyphMesh.bounds.extents.z)*peakScale;
        float requiredOrtho=peakHalfExtent*1.035f;
        Check(Mathf.Abs(requiredOrtho-NativeShamanHeroPowerPlayback.ButtonOrthographicSize)<.01f,
            "fixed H02 button framing covers the source mesh at its authored scale peak plus 3.5% margin (required="+
            requiredOrtho.ToString("F3")+")");
        float spawnFinishAge;
        Check(NativeShamanHeroPowerPlayback.TryGetSpawnFinishAge(fullProgram,out spawnFinishAge)&&Mathf.Abs(spawnFinishAge-1.9f)<.002f,
            "source Action wait and TotemSpawn clip bound the per-Actor finish event");

        int rootAudio=0;
        using(var player=new NativeEffectPlayer(assets))
        {
            player.AudioCue=cue=>rootAudio++;
            Check(Mathf.Abs(player.Duration("HERO_02bp")-entry.duration)<.001f,"root duration follows its enabled source entry");
            var power=new EntityView{entity=2,card_id="HERO_02bp"};
            var state=new StateView{match_id="h02-editor",version=1,players=new[]{new PlayerView{seat=0,power=power},new PlayerView{seat=1}}};
            var data=new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_02bp",owner=0};
            Check(player.PrepareSource("h02-root",data,state,state,false,0),"the real root Action event binds and starts the source cast");
            Check(rootAudio==1,"the authored cast SoundDef action is dispatched once");
            const int screenWidth=1920,screenHeight=1080;var renderedButtonBounds=new Rect(910,490,100,100);
            const float buttonRectScale=1.55f;
            var effectBounds=new Rect(renderedButtonBounds.center.x-renderedButtonBounds.width*buttonRectScale*.5f,
                renderedButtonBounds.center.y-renderedButtonBounds.height*buttonRectScale*.5f,
                renderedButtonBounds.width*buttonRectScale,renderedButtonBounds.height*buttonRectScale);
            var canvas=new RenderTexture(screenWidth,screenHeight,0,RenderTextureFormat.ARGB32);canvas.Create();
            var readback=new Texture2D(screenWidth,screenHeight,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
            try
            {
                foreach(float sampleAge in new[]{.75f,1.1667f})
                {
                    Check(player.PrepareSource("h02-root",data,state,state,false,sampleAge),
                        "the root continues sampling the source FSM at age "+sampleAge.ToString("F2"));
                    RenderTexture.active=canvas;GL.Clear(true,true,Color.clear);
                    player.Draw("h02-root","HERO_02bp",effectBounds,sampleAge,null,false,Matrix4x4.identity,
                        screenWidth,screenHeight,Color.white);
                    RenderTexture.active=canvas;readback.ReadPixels(new Rect(0,0,screenWidth,screenHeight),0,0,false);readback.Apply(false,false);
                    string sampleFolder=Path.GetFullPath("../work/f021-shaman-button-gpu");Directory.CreateDirectory(sampleFolder);
                    File.WriteAllBytes(Path.Combine(sampleFolder,"button-"+sampleAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png"),readback.EncodeToPNG());
                    var visible=VisiblePixelBounds(readback.GetPixels32(),screenWidth,screenHeight);
                    float widthInButtons=visible.width/renderedButtonBounds.width,heightInButtons=visible.height/renderedButtonBounds.height;
                    Check(visible.width>0&&visible.height>0,"the H02 source button produced visible GPU pixels at age "+sampleAge.ToString("F2"));
                    Check(widthInButtons>=.9f&&widthInButtons<=1.8f&&heightInButtons>=.9f&&heightInButtons<=1.8f,
                        "visible source bounds measure "+widthInButtons.ToString("F2")+"x"+heightInButtons.ToString("F2")+
                        " hero buttons at age "+sampleAge.ToString("F2")+" after the 1.55x output rect");
                    Debug.Log("NATIVE SHAMAN H02 BUTTON age="+sampleAge.ToString("F2")+" visible="+visible+
                        " size/button="+widthInButtons.ToString("F2")+"x"+heightInButtons.ToString("F2"));
                }
            }
            finally
            {
                RenderTexture.active=previous;AssetRegistry.ReleaseTransient(canvas);AssetRegistry.ReleaseTransient(readback);
            }
            Check(player.SourceBusy("h02-root"),"the H02 root keeps its button overlay drawable from its own active FSM clock");
        }

        var actor=new GameObject("H02 test Actor");
        var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.transform.SetParent(actor.transform,false);
        var presentation=actor.AddComponent<NativeActorPresentation>();presentation.Configure(visual);
        var spawnFx=GameObject.CreatePrimitive(PrimitiveType.Cube);spawnFx.name="H02 detached spawn FX";
        spawnFx.transform.SetParent(actor.transform,false);
        actor.AddComponent<NativeActorIdentity>();
        var cameraObject=new GameObject("H02 test camera");var camera=cameraObject.AddComponent<Camera>();
        try
        {
            var program=NativeShamanHeroPowerPlayback.ForOwner(assets,entry,3);
            Check(program.spell_transforms.Any(transform=>transform.enabled&&transform.node==3&&transform.location==2),
                "the CustomSpawn Spell root uses its source-object location for the real Summon Actor");
            Check(NativeEffectInstance.NodeBelongsToSubtree(program,9,3)&&!NativeEffectInstance.NodeBelongsToSubtree(program,0,3),
                "the independent overlay includes the source Totem mesh and excludes the outer hero-power root");
            int spawnAudio=0;
            using(var effect=new NativeEffectInstance(program,assets,0,512,512,
                context:new NativeSpellContext(4,placement:NativeSpellObjectSelection.ForActor(actor,4)),
                audioCallback:cue=>spawnAudio++,deferEntryEvents:true))
            {
                effect.BindSourcePlacementRoot(3);effect.BindTimelineCamera(camera);effect.AttachToOriginalParent(actor);
                effect.Seek(0);effect.UpdateOriginalPosition();
                Check(effect.Root.transform.parent==actor.transform&&effect.PreviewCamera.targetTexture.width==512&&effect.PreviewCamera.targetTexture.height==512,
                    "the source FX keeps its own 512-square render target parented to the actual Actor");
                Check(effect.DispatchMachineEvent(0,"Action"),"actual Summon dispatch enters the per-Actor Action state");
                Check(effect.SourcePositionUpdates>0&&effect.UnboundSourcePlacements==0,
                    "the H02 Spell root resolves its authored node-3 location against the actual Actor");
                Check(Vector3.Distance(effect.Nodes[3].transform.position,actor.transform.position)<.001f,
                    "the independent source-effect root is positioned at the live Summon Actor");
                Check(effect.UnboundActorInitializationActions==0&&effect.ActorObjectBindings>0,
                    "CustomSpawn actions bind the live Actor identity while its visual root is hidden");
                Check(!presentation.Shown&&effect.ActorRootVisibilityBindings==1,"source spawn hides only the real Actor visual root");
                Check(spawnFx.activeInHierarchy&&spawnFx.GetComponent<Renderer>().enabled&&!NativeActorPresentation.IsActorShown(actor),
                    "visible spawn FX cannot reveal the hidden Actor portrait, frame, stats, taunt or sleep UI");
                effect.Seek(.5f);
                Check(!presentation.Shown&&effect.ActorRootVisibilityBindings==1&&spawnAudio==1,
                    "catch-up to the authored PlayFX time completes the Action wait, hides the Actor, and starts TotemSpawn audio");
                Check(!NativeActorPresentation.IsActorShown(actor),"the authored PlayFX boundary keeps all Actor GUI layers hidden");
                Check(effect.DispatchMachineEvent(0,"PlayFX"),"the delayed source PlayFX event enters the crack effect");
                effect.Seek(.85f);
                Check(effect.SetParentActions==1&&effect.MainShakeStarts==1,"PlayFX detaches and plays its source particle/camera actions");
                Check(effect.UnboundSetParentActions==0&&effect.UnboundMainShakeActions==0,"source PlayFX actions bind to their source nodes and camera");
                Check(spawnAudio==1,"the source TotemSpawn animation state plays its authored audio once");
                effect.Seek(spawnFinishAge);
                Check(effect.CurrentStates.Single()=="State 6"&&!presentation.Shown,"PlayFX advances to its source follow-up state while the Actor remains hidden through TotemSpawn");
                Check(!NativeActorPresentation.IsActorShown(actor),"TotemSpawn completion does not expose GUI before source visibility restoration");
                Check(effect.DispatchMachineEvent(0,"Die"),"source spawn completion enters the authored CustomSpawn cleanup state");
                Check(presentation.Shown&&effect.DestroyObjectActions==1,"CustomSpawn completion restores the Actor and destroys the detached crack node (shown="+presentation.Shown+" destroy="+effect.DestroyObjectActions+" unbound="+effect.UnboundDestroyObjectActions+" visibility="+effect.ActorRootVisibilityBindings+" visUnbound="+effect.UnboundActorVisibilityActions+" state="+effect.CurrentStates.Single()+")");
                Check(NativeActorPresentation.IsActorShown(actor),"source restoration reveals the portrait and every GUI layer through the same gate");
                effect.Seek(spawnFinishAge+2.1f);
                Check(effect.CurrentStates.Single()=="NoneState"&&effect.UnhandledActions==0,"the source cleanup tail reaches NoneState without unknown actions");
            }
            Check(NativeActorPresentation.IsActorShown(actor),"disposing CustomSpawn leaves the borrowed Actor GUI visibility restored");
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(cameraObject);}
        var before=new StateView{match_id="h02-input",version=0,phase="play",seat=0,
            players=new[]{new PlayerView{seat=0,power=new EntityView{entity=2,card_id="HERO_02bp"}},new PlayerView{seat=1}},events=new SemanticEvent[0]};
        var after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));after.version=1;
        after.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,card_id="HERO_02bp",owner=0}};
        var timeline=new Presentation{NativeDuration=card=>card=="HERO_02bp"?2.5f:0,NativeActive=key=>true};
        timeline.Accept(before,after,new System.Collections.Generic.Dictionary<int,Rect>());timeline.Tick(.7f,new System.Collections.Generic.Dictionary<int,Rect>());
        Check(!timeline.InputLocked,"the full visual source duration does not extend the gameplay input lock");
        var summonAfter=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));summonAfter.version=1;
        summonAfter.players[0].field=new[]{new EntityView{entity=301,card_id="CS2_050"}};
        summonAfter.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,card_id="HERO_02bp",owner=0},
            new SemanticEvent{type="Summon",source=2,target=301,card_id="CS2_050",card_type=4,owner=0}};
        var pairedTimeline=new Presentation{NativeDuration=card=>card=="HERO_02bp"?2.5f:0,
            NativeSummonDuration=(evt,state)=>evt.type=="Summon"?spawnFinishAge:0};
        pairedTimeline.Accept(null,before,new System.Collections.Generic.Dictionary<int,Rect>());
        pairedTimeline.Accept(before,summonAfter,new System.Collections.Generic.Dictionary<int,Rect>());
        var buttonCue=pairedTimeline.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_02bp");
        var summonCue=pairedTimeline.Cues.Single(cue=>cue.data.type=="Summon"&&cue.data.target==301);
        Check(Mathf.Approximately(buttonCue.start,summonCue.start)&&Mathf.Approximately(summonCue.start,0)&&
            Mathf.Approximately(summonCue.duration,spawnFinishAge),
            "the Shaman button and totem spawn effects start together, then the token is revealed at the authored spawn-finish age");
        Debug.Log("NATIVE SHAMAN HERO POWER: "+passed+" runtime-route checks passed");
    }
}
