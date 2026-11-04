using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeWarlockHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Warlock hero power: "+reason);passed++;}
    static NativeSourceCamera OriginalMainCamera(AssetRegistry assets)
    {
        return JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json")))
            .cameras.Single(camera=>camera.name=="Main Camera");
    }
    static StateView State(int version)
    {
        return new StateView{match_id="warlock-life-tap-check",version=version,seat=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="hero",health=30},power=new EntityView{entity=2,card_id="HERO_07bp"},
                field=new EntityView[0],hand=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="hero",health=30},power=new EntityView{entity=4,card_id="power"},
                field=new EntityView[0],hand=new EntityView[0]}}};
    }
    static NativeEffectPlayer Player(AssetRegistry assets)
    {
        var mainCamera=OriginalMainCamera(assets);
        var player=new NativeEffectPlayer(assets);player.CardType=id=>id=="hero"?3:id=="HERO_07bp"||id=="power"?10:5;
        player.SuperSpellScene=(data,before,after,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(data,before,after,
            id=>id==1?BattleLayout.Hero(true).center:id==3?BattleLayout.Hero(false).center:
                id==2?BattleLayout.Power(true).center:id==4?BattleLayout.Power(false).center:(Vector2?)null,
            viewport,origin,player.CardType,mainCamera);
        return player;
    }
    static void CheckCameraProjection(AssetRegistry assets,int seat)
    {
        var before=State(0);var after=State(1);before.seat=seat;after.seat=seat;
        int source=seat==0?2:4;var point=BattleLayout.Power(seat==0).center;
        var data=new SemanticEvent{type="SpellEffect",source=source,card_id="HERO_07bp",owner=seat};
        string key="warlock-camera-"+seat;
        using(var player=Player(assets))
        {
            Check(player.PrepareSource(key,data,before,after,false,0),
                "H07 source prepares with the original Main Camera for seat "+seat);
            var spell=player.SourceSpell(key);var camera=spell==null?null:spell.Effect.PreviewCamera;
            var actual=camera==null?Vector2.zero:spell.Effect.PreviewPoint(spell.Effect.Nodes[0].transform.position);
            var expected=new Vector2(point.x/1920f,point.y/1080f);
            var error=Vector2.Scale(actual-expected,new Vector2(1920,1080));
            var original=OriginalMainCamera(assets);
            Check(camera!=null&&!camera.orthographic&&Mathf.Approximately(camera.fieldOfView,original.fov)&&error.magnitude<=2f,
                "H07 source and effect camera project seat "+seat+" to the GUI power point; errorPixels="+error.ToString("F3"));
        }
    }
    public static void RunSourceRoot()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_07bp");
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_07bp");
        Check(entry.enabled&&Mathf.Approximately(entry.duration,4),"imported root cue is enabled for its authored four-second FSM wait");
        Check(config.start_enabled&&config.start_config_null&&config.missile.m_Enabled==0,
            "enabled-null Start source marker and disabled Missile are preserved");
        Check(NativeSuperSpellPlayback.SupportsWarlockHeroPowerRoot(program,config),"strict H07 root support profile accepts source exports");
        var unsupported=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        unsupported.unknown_actions=unsupported.unknown_actions.Concat(new[]{"InvokeMethod"}).ToArray();
        Check(!NativeSuperSpellPlayback.SupportsWarlockHeroPowerRoot(unsupported,config),"unrecognized source actions fail closed");
        var missingPlane=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        missingPlane.captures[0].material=null;
        Check(!NativeSuperSpellPlayback.SupportsWarlockHeroPowerRoot(missingPlane,config),"missing explicit source plane material fails closed");
        var unsupportedPlane=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        unsupportedPlane.captures[0].original_parameters.m_RenderTextureFormat=0;
        Check(!NativeSuperSpellPlayback.SupportsWarlockHeroPowerRoot(unsupportedPlane,config),"an unsupported H07 capture format fails the route gate");

        var before=State(0);var after=State(1);
        after.events=new[]{new SemanticEvent{type="PlayHeroPower",target=2,card_id="HERO_07bp",owner=0},
            new SemanticEvent{type="Hit",source=2,target=1,amount=2,owner=0},
            new SemanticEvent{type="Draw",source=2,target=11,owner=0}};
        var positions=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)}};
        using(var player=Player(assets))
        {
            var audio=new List<NativeAudioCue>();player.AudioCue=audio.Add;
            var data=new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_07bp",owner=0};
            Check(player.PrepareSource("warlock-root",data,before,after,false,0),"source-only root prepares from the power entity");
            var root=player.SourceSpell("warlock-root");
            Check(root!=null&&root.Flow==null&&!root.Busy,"H07 bypasses the target-arrival Flow and never holds input Busy");
            Check(root.Effect.Program.nodes.Length==21&&root.Effect.Program.machines.Length==1&&
                root.Effect.Program.machines[0].states.Any(state=>state.name=="Play FX"),
                "production PrepareSource retains the complete imported root and FSM");
            Check(root.Effect.Program.reference==config.reference&&root.Effect.ActionExecutions["ActivateGameObject"]==2&&
                root.Effect.ActionExecutions["ParticlePlayAction"]==2&&root.Effect.ActionExecutions["AudioPlayClipAction"]==1,
                "one Death entry runs the two activations, particles and clip action");
            Check(root.Effect.UnhandledActions==0&&root.Effect.UnboundAudioActions==0&&root.Effect.ClipAudioStarts==1&&audio.Count==1,
                "reviewed source clip is bound once without unknown or unbound actions");
            Check(root.Effect.Nodes[3].activeSelf&&root.Effect.Nodes[8].activeSelf,"authored visual groups are activated");
            Check(player.SourceBusy("warlock-root"),"the visual queue keeps sampling the source root until its authored Wait finishes");
            Check(root.Effect.SourcePositionUpdates>0,"original SuperSpell SourceAuto mapping updates the source root position");
            var sourceCamera=OriginalMainCamera(assets);var sourceCameraPreview=root.Effect.PreviewCamera;
            var actualSourceScreen=sourceCameraPreview==null?Vector2.zero:root.Effect.PreviewPoint(root.Effect.Nodes[0].transform.position);
            var expectedSourceScreen=new Vector2(positions[2].center.x/1920f,positions[2].center.y/1080f);
            var sourceErrorPixels=Vector2.Scale(actualSourceScreen-expectedSourceScreen,new Vector2(1920,1080));
            Check(sourceCameraPreview!=null&&!sourceCameraPreview.orthographic&&
                Mathf.Approximately(sourceCameraPreview.fieldOfView,sourceCamera.fov)&&
                Mathf.Approximately(sourceCameraPreview.nearClipPlane,sourceCamera.near)&&
                Mathf.Approximately(sourceCameraPreview.farClipPlane,sourceCamera.far)&&sourceErrorPixels.magnitude<=2f,
                "H07 source placement and effect rendering share the original Main Camera projection; errorPixels="+sourceErrorPixels.ToString("F3"));
            Check(Mathf.Approximately(audio[0].volume,.8f)&&Mathf.Approximately(audio[0].pitch,1)&&
                audio[0].options!=null&&audio[0].options.limited&&Mathf.Approximately(audio[0].options.duration,.5f)&&audio[0].options.maximum==1,
                "source clip preserves authored gain and instance limit");
            for(int frame=1;frame<=250;frame++)
                Check(player.PrepareSource("warlock-root",data,before,after,false,frame/120f),"root remains sampleable through the authored Wait");
            Check(player.SourceBusy("warlock-root"),"the decorative root remains active during its authored four-second Wait");
            for(int frame=251;frame<=500;frame++)
                Check(player.PrepareSource("warlock-root",data,before,after,false,frame/120f),"root remains sampleable through the authored Wait");
            Check(root.Effect.ActionExecutions["Wait"]==1&&root.Effect.ActionExecutions["SpellStateAction"]==2&&
                !root.Busy&&!player.SourceBusy("warlock-root"),"four-second wait exits to None and releases the visual tail");
        }
        CheckCameraProjection(assets,1);

        using(var player=Player(assets))
        {
            var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};string preparedKey=null;
            presentation.NativePrepare=(key,data,b,a,area,age)=>{preparedKey=key;player.PrepareSource(key,data,b,a,area,age);};
            presentation.Accept(before,after,positions);
            var cast=presentation.Cues.Single(cue=>cue.data.type=="PlayHeroPower");
            var source=presentation.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_07bp");
            var hit=presentation.Cues.Single(cue=>cue.data.type=="Hit");
            var draw=presentation.Cues.Single(cue=>cue.data.type=="Draw");
            Check(source.start==cast.start&&source.apply==float.MaxValue&&hit.nativeImpact==null&&
                Mathf.Approximately(cast.apply,source.start)&&Mathf.Approximately(hit.start,source.start),
                "the source root and authoritative self-Hit resolve together without replaying an arrival");
            Check(hit.start<draw.start&&Mathf.Approximately(draw.start,.55f),"self-damage starts with the power effect before card draw");
            presentation.PrepareNative();
            Check(preparedKey!=null&&player.SourceSpell(preparedKey)!=null,"presentation samples one source root cue");
            presentation.Seek(.05f);
            var heroRect=BattleLayout.Hero(true);
            Check(presentation.Shake==Vector2.zero&&presentation.Tint(1)==0&&
                Mathf.Abs(presentation.Animate(1,heroRect).x-heroRect.x)<.001f,
                "Life Tap health loss keeps the damage cue but suppresses ordinary hero hit shake and tint");
            presentation.Tick(1.6f,positions);presentation.PrepareNative();
            Check(presentation.Busy&&!presentation.InputLocked,"the four-second decorative tail does not hold input after Hit and Draw");
        }
        CheckSourceCapture(assets,before,after);
        CheckUnreviewedCapture(assets);
        Debug.Log("NATIVE WARLOCK LIFE TAP: "+passed+" checks passed; source-only root and public event order");
    }
    static int ChangedPixels(RenderTexture target,Color background)
    {
        var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            return image.GetPixels().Count(pixel=>Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>.03f);
        }
        finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
    }
    static void CheckSourceCapture(AssetRegistry assets,StateView before,StateView after)
    {
        using(var player=Player(assets))
        {
            var sounds=new List<NativeAudioCue>();player.AudioCue=sounds.Add;
            var cue=new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_07bp",owner=0};
            Check(player.PrepareSource("warlock-capture",cue,before,after,false,0),"real H07 capture source prepares");
            var spell=player.SourceSpell("warlock-capture");
            Check(spell.Effect.GeneratedCaptures.Count()==1,"real H07 exports one explicit material automatic plane");
            int maxSource=0,maxScreen=0;
            foreach(float age in new[]{.1f,.25f,.5f,.9f})
            {
                var emission=spell.RenderAffine(age);var capture=spell.Effect.GeneratedCaptures.Single();
                Check(capture.Active&&capture.Texture!=null,"production root render updates its original automatic capture");
                Check(capture.Plane.GetComponent<Renderer>().sharedMaterial.GetFloat("_OriginalFamily")==43,
                    "automatic plane uses the source area ramp shader");
                maxSource=Math.Max(maxSource,ChangedPixels(capture.Texture,spell.Effect.Program.captures[0].clear));
                maxScreen=Math.Max(maxScreen,ChangedPixels(emission,Color.black));
            }
            Check(maxSource>8,"real H07 source capture contains particle content, pixels="+maxSource);
            Check(maxScreen>8,"real H07 source capture reaches composed output, pixels="+maxScreen);
            Check(sounds.Count==1,"capture renders never repeat the source sound");
        }
    }
    static void CheckUnreviewedCapture(AssetRegistry assets)
    {
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="BT_035");
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        Check(program.reference=="ReuseFX_BuffImpact_DemonHunter_Small_Super_NoIdle.prefab:4694fc2ee619cd54c9352938dbf84940",
            "capture regression reads the actual AnimatorChecks BT_035 source");
        Check(program.captures.Length==2&&program.captures.All(capture=>!NativeGeneratedCapture.Supports(capture)),
            "unreviewed BT_035 mode-zero format-zero captures remain unsupported");
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
            Check(!instance.GeneratedCaptures.Any()&&instance.UnrestoredGeneratedCaptures==2,
                "the factory records both unsupported captures without constructing reviewed generators");
        bool rejected=false;
        try{new NativeGeneratedCapture(program.captures[0],null,null,null,null,null,null);}
        catch(NotSupportedException){rejected=true;}
        Check(rejected,"direct construction still rejects an unreviewed capture branch");
    }
}
