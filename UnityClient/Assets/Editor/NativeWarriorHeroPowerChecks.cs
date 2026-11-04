using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class NativeWarriorHeroPowerChecks
{
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Warrior hero power: "+reason);passed++;}

    static NativeProgram Copy(NativeProgram value)
    {return JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(value));}

    static NativeProgram Program(AssetRegistry assets,string file)
    {return JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file)));}

    public static void RunSourceRoot()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_01bp");
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_01bp"&&row.file==entry.file);
        var program=Program(assets,entry.file);
        Check(!entry.enabled&&Mathf.Abs(entry.duration-1.55f)<.001f,
            "root stays disabled in the shared index and carries the imported source visual lifetime");
        Check(config.target_behavior==6&&!config.start_enabled&&config.start_config_null&&config.missile!=null&&
            config.missile.m_Enabled==0&&String.IsNullOrEmpty(config.missile_reference)&&config.impact!=null&&config.impact.enabled,
            "HERO_01bp source config remains untargeted with no Start or Missile branch");
        Check(NativeSuperSpell.SupportsNoMissileUntargeted(config)&&
            NativeSuperSpellPlayback.SupportsWarriorHeroPowerRoot(program),
            "source root and constrained no-missile visual route pass their fail-closed gates");
        var invoke=program.machines.Single(row=>row.owner_known&&row.owner==1).states
            .SelectMany(state=>state.actions).Single(action=>action.name=="InvokeMethod");
        Check(NativeEffectInstance.SupportsCameraShakerInvoke(program,invoke),
            "only the imported CameraShaker.StartShake action matches the supported invocation profile");
        var shaker=program.nodes[invoke.Param("gameObject").node].camera_shaker;
        Check(shaker.restored&&Mathf.Abs(shaker.amount.x-.1f)<.0001f&&Mathf.Abs(shaker.amount.y-.1f)<.0001f&&
            Mathf.Abs(shaker.amount.z-.1f)<.0001f&&shaker.curve.Length==2&&Mathf.Approximately(shaker.curve[0].time,0)&&
            Mathf.Approximately(shaker.curve[0].value,1)&&Mathf.Abs(shaker.curve[1].time-.6f)<.0001f&&
            Mathf.Approximately(shaker.curve[1].value,0)&&!shaker.hold&&!shaker.is_override,
            "source shake preserves amount, 0.6 second curve, hold and override settings");

        var changedMethod=Copy(program);
        changedMethod.machines.Single(row=>row.owner_known&&row.owner==1).states.SelectMany(state=>state.actions)
            .Single(action=>action.name=="InvokeMethod").Param("methodName").text="Unsupported";
        var changedAction=changedMethod.machines.Single(row=>row.owner_known&&row.owner==1).states
            .SelectMany(state=>state.actions).Single(action=>action.name=="InvokeMethod");
        Check(!NativeEffectInstance.SupportsCameraShakerInvoke(changedMethod,changedAction)&&
            !NativeSuperSpellPlayback.SupportsWarriorHeroPowerRoot(changedMethod),
            "unknown InvokeMethod targets fail closed before playback");
        using(var unreviewed=new NativeEffectInstance(changedMethod,assets,width:32,height:32,
            context:new NativeSpellContext(0,lifecycle:"Death",sourceCardId:"HERO_01bp")))
        {
            unreviewed.Seek(0);unreviewed.Seek(.66f);
            Check(unreviewed.UnhandledActions>0&&unreviewed.CameraShakerStarts==0&&unreviewed.PendingCameraShakerInvocations==0,
                "an unreviewed InvokeMethod is diagnosed and skipped without aborting the effect instance");
        }
        var extraAction=Copy(program);var death2=extraAction.machines.Single(row=>row.owner_known&&row.owner==1).states
            .Single(state=>state.name=="DeathState2");
        death2.actions=death2.actions.Concat(new[]{new NativeAction{name="UnsupportedAction",@params=new NativeParam[0]}}).ToArray();
        Check(!NativeSuperSpellPlayback.SupportsWarriorHeroPowerRoot(extraAction),
            "an unsupported source FSM action closes the Warrior route");

        bool ownsCamera=Camera.main==null;GameObject cameraObject=null;
        if(ownsCamera){cameraObject=new GameObject("Warrior CameraShaker regression camera");cameraObject.tag="MainCamera";cameraObject.AddComponent<Camera>();}
        NativeEffectInstance effect=null;int calls=0;Camera calledCamera=null;NativeCameraShakerData calledData=null;
        try
        {
            effect=new NativeEffectInstance(program,assets,width:32,height:32,
                context:new NativeSpellContext(0,lifecycle:"Death",sourceCardId:"HERO_01bp"));
            effect.CameraShakerPlayback=(camera,data)=>{calls++;calledCamera=camera;calledData=data;};
            effect.Seek(0);
            effect.Seek(.64f);
            Check(effect.CurrentStates.Single()=="DeathState3"&&effect.PendingCameraShakerInvocations==1&&calls==0,
                "0.1 second invocation is queued from DeathState2 and survives its state exit");
            effect.Seek(.66f);
            Check(calls==1&&effect.CameraShakerStarts==1&&effect.PendingCameraShakerInvocations==0&&
                calledCamera==Camera.main&&calledData==program.nodes[invoke.Param("gameObject").node].camera_shaker,
                "the delayed one-shot runs after state exit against Camera.main with the imported curve payload");
            effect.Seek(.9f);
            Check(calls==1,"the source one-shot is not repeated on later samples");
            effect.Seek(.1f);effect.Seek(.66f);
            Check(calls==2&&effect.CameraShakerStarts==1,
                "rewinding resets the invocation clock and replays one call without stale duplicates");
            effect.Seek(.6f);
            Check(effect.PendingCameraShakerInvocations==1,"a pre-deadline invocation remains pending until cleanup");
            effect.Dispose();
            Check(effect.PendingCameraShakerInvocations==0,"disposing the root clears its pending delayed invocation");
        }
        finally
        {
            if(effect!=null)effect.Dispose();
            if(cameraObject!=null)AssetRegistry.ReleaseTransient(cameraObject);
        }
        Debug.Log("WARRIOR HERO POWER SOURCE ROOT: "+passed+" checks passed; source actions gated and delayed camera shake clocked");
    }

    static StateView State(int version)
    {
        return new StateView{match_id="warrior-route-check",version=version,seat=0,active=0,phase="play",
            moves=new Move[0],events=new SemanticEvent[0],players=new[]{
                new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_01"},
                    power=new EntityView{entity=2,card_id="HERO_01bp"},weapon=null,hand=new EntityView[0],field=new EntityView[0]},
                new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_02"},
                    power=new EntityView{entity=4,card_id="HERO_02bp"},weapon=null,hand=new EntityView[0],field=new EntityView[0]}}};
    }

    struct ButtonPixels
    {
        public int nonblack,bright,minX,minY,maxX,maxY;
    }

    static ButtonPixels ReadButtonPixels(RenderTexture image,Rect button,string capturePath)
    {
        var previous=RenderTexture.active;var readback=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=image;readback.ReadPixels(new Rect(0,0,image.width,image.height),0,0,false);readback.Apply(false,false);
            var pixels=readback.GetPixels32();var bounds=new ButtonPixels{minX=image.width,minY=image.height,maxX=-1,maxY=-1};
            var center=button.center;
            int left=Mathf.Max(0,Mathf.FloorToInt(center.x-button.width*1.5f));
            int right=Mathf.Min(image.width,Mathf.CeilToInt(center.x+button.width*1.5f));
            int top=Mathf.Max(0,Mathf.FloorToInt(center.y-button.height*1.5f));
            int bottom=Mathf.Min(image.height,Mathf.CeilToInt(center.y+button.height*1.5f));
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {
                var pixel=pixels[y*image.width+x];int value=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));
                if(value>0)bounds.nonblack++;
                int screenY=image.height-1-y;
                if(x<left||x>=right||screenY<top||screenY>=bottom||value<32)continue;
                bounds.bright++;bounds.minX=Mathf.Min(bounds.minX,x);bounds.maxX=Mathf.Max(bounds.maxX,x);
                bounds.minY=Mathf.Min(bounds.minY,screenY);bounds.maxY=Mathf.Max(bounds.maxY,screenY);
            }
            if(!String.IsNullOrEmpty(capturePath))File.WriteAllBytes(capturePath,readback.EncodeToPNG());
            return bounds;
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(readback);}
    }

    static string CurveKeys(AnimationCurve curve)
    {
        if(curve==null)return "null";
        return "["+String.Join(";",curve.keys.Select(key=>key.time.ToString("R",CultureInfo.InvariantCulture)+":"+
            key.value.ToString("R",CultureInfo.InvariantCulture)+"("+key.inTangent.ToString("R",CultureInfo.InvariantCulture)+","+
            key.outTangent.ToString("R",CultureInfo.InvariantCulture)+")").ToArray())+"]";
    }

    static string CurveProbe(ParticleSystem.MinMaxCurve curve)
    {
        if(curve.mode==ParticleSystemCurveMode.Constant)return "mode="+curve.mode+",value="+curve.constant.ToString("R",CultureInfo.InvariantCulture);
        if(curve.mode==ParticleSystemCurveMode.TwoConstants)return "mode="+curve.mode+",min="+
            curve.constantMin.ToString("R",CultureInfo.InvariantCulture)+",max="+curve.constantMax.ToString("R",CultureInfo.InvariantCulture);
        if(curve.mode==ParticleSystemCurveMode.Curve)return "mode="+curve.mode+",multiplier="+
            curve.curveMultiplier.ToString("R",CultureInfo.InvariantCulture)+",curve="+CurveKeys(curve.curve);
        if(curve.mode==ParticleSystemCurveMode.TwoCurves)return "mode="+curve.mode+",multiplier="+
            curve.curveMultiplier.ToString("R",CultureInfo.InvariantCulture)+",min="+CurveKeys(curve.curveMin)+
            ",max="+CurveKeys(curve.curveMax);
        return "mode="+curve.mode+",multiplier="+curve.curveMultiplier.ToString("R",CultureInfo.InvariantCulture)+
            ",curve="+CurveKeys(curve.curve)+",min="+CurveKeys(curve.curveMin)+",max="+CurveKeys(curve.curveMax);
    }

    static string ProbeParticles(NativeEffectInstance effect,string sampleName)
    {
        var camera=effect.PreviewCamera;
        var report=new StringBuilder();
        report.Append("H01 PARTICLE PROBE t=").Append(sampleName)
            .Append(" sourceCamera.position=").Append(camera.transform.position).Append("/rotation=")
            .Append(camera.transform.rotation).Append("/size=")
            .Append(camera.orthographicSize.ToString("R",CultureInfo.InvariantCulture)).Append("/aspect=")
            .Append(camera.aspect.ToString("R",CultureInfo.InvariantCulture)).Append("/orthographic=")
            .Append(camera.orthographic).Append("/fov=")
            .Append(camera.fieldOfView.ToString("R",CultureInfo.InvariantCulture)).Append("/pixelRect=")
            .Append(camera.pixelRect).Append(" target=")
            .Append(camera.targetTexture==null?"null":camera.targetTexture.width+"x"+camera.targetTexture.height);
        for(int i=0;i<effect.Particles.Length;i++)
        {
            var particleSystem=effect.Particles[i];
            if(particleSystem==null||!NativeEffectInstance.NodeBelongsToSubtree(effect.Program,i,1))continue;
            var renderer=particleSystem.GetComponent<ParticleSystemRenderer>();var main=particleSystem.main;
            var size=particleSystem.sizeOverLifetime;var bounds=renderer.bounds;
            var samples=new ParticleSystem.Particle[Mathf.Max(1,particleSystem.particleCount)];int count=particleSystem.GetParticles(samples);
            report.Append("\n node=").Append(i).Append(" name=").Append(effect.Program.nodes[i].name)
                .Append(" alive=").Append(particleSystem.particleCount).Append(" got=").Append(count)
                .Append(" source.render.maximum=").Append(effect.Program.nodes[i].particle.render.maximum.ToString("R",CultureInfo.InvariantCulture))
                .Append(" scaling=").Append(main.scalingMode).Append(" startSize3D=").Append(main.startSize3D)
                .Append(" main.startSize{").Append(CurveProbe(main.startSize)).Append("} X{").Append(CurveProbe(main.startSizeX))
                .Append("} Y{").Append(CurveProbe(main.startSizeY)).Append("} Z{").Append(CurveProbe(main.startSizeZ)).Append("}")
                .Append(" sizeOL(enabled=").Append(size.enabled).Append(",separate=").Append(size.separateAxes)
                .Append(",x{").Append(CurveProbe(size.x)).Append("},y{").Append(CurveProbe(size.y))
                .Append("},z{").Append(CurveProbe(size.z)).Append("}) render.max=")
                .Append(renderer.maxParticleSize.ToString("R",CultureInfo.InvariantCulture))
                .Append(" renderer.bounds=center:").Append(bounds.center).Append(" size:").Append(bounds.size);
            int take=Mathf.Min(count,3);
            for(int p=0;p<take;p++)
            {
                var value=samples[p];
                report.Append("\n  p").Append(p).Append(" pos=").Append(value.position)
                    .Append(" startSize=").Append(value.startSize.ToString("R",CultureInfo.InvariantCulture))
                    .Append(" startSize3D=").Append(value.startSize3D)
                    .Append(" currentSize=").Append(value.GetCurrentSize(particleSystem).ToString("R",CultureInfo.InvariantCulture))
                    .Append(" currentSize3D=").Append(value.GetCurrentSize3D(particleSystem));
            }
            var baked=new Mesh();
            try
            {
                renderer.BakeMesh(baked,camera,true);
                report.Append("\n  bakeMesh(useTransform=true) vertices=").Append(baked.vertexCount)
                    .Append(" meshBounds.center=").Append(baked.bounds.center)
                    .Append(" meshBounds.size=").Append(baked.bounds.size);
            }
            finally{AssetRegistry.ReleaseTransient(baked);}
        }
        return report.ToString();
    }

    public static void RunRoute()
    {
        passed=0;bool ownsCamera=Camera.main==null;GameObject cameraObject=null;Camera routeCamera=Camera.main;
        if(ownsCamera){cameraObject=new GameObject("Warrior route regression camera");routeCamera=cameraObject.AddComponent<Camera>();}
        try
        {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_01bp");
        var before=State(1);var after=State(2);
        after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_01bp",owner=0}};
        var locations=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)}};
        using(var unbound=new NativeEffectPlayer(assets))
        {
            Check(unbound.Duration("HERO_01bp")==0,"the disabled H01 source entry stays closed without the native scene route");
            var untargeted=State(1);untargeted.moves=new[]{new Move{kind="power",entity=2,target=0}};
            Check(!unbound.SelectPower(untargeted,2),"H01 never opens targeted power selection");
        }
        using(var player=new NativeEffectPlayer(assets))
        {
            player.TimelineCamera=routeCamera;
            player.CardType=id=>id=="HERO_01"||id=="HERO_02"?3:id=="HERO_01bp"||id=="HERO_02bp"?10:0;
            player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                id=>id==1?BattleLayout.Hero(true).center:id==2?BattleLayout.Power(true).center:
                    id==3?BattleLayout.Hero(false).center:id==4?BattleLayout.Power(false).center:(Vector2?)null,
                viewport,origin,player.CardType);
            float duration=player.Duration("HERO_01bp");
            Check(Mathf.Abs(duration-1.55f)<.001f,"a bound native scene enables the reviewed Warrior source entry");
            var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};
            presentation.NativePrepare=(key,data,oldState,newState,area,age)=>
            {if(!player.PrepareSource(key,data,oldState,newState,area,age))throw new Exception("Warrior hero power: source cue did not prepare");};
            presentation.Accept(null,before,locations);presentation.Accept(before,after,locations);
            var cue=presentation.Cues.Single(row=>row.data.type=="SpellEffect"&&row.data.card_id=="HERO_01bp");
            string key=after.match_id+":"+after.version+":"+cue.data.source+":"+cue.start;
            Check(cue.data.source==2&&cue.data.target==0&&cue.data.owner==0,
                "PlayHeroPower target skill entity becomes a source-only cue with no selected target");
            Check(Mathf.Approximately(cue.start,0)&&Mathf.Approximately(cue.apply,cue.start)&&
                Mathf.Abs(cue.duration-duration)<.001f&&presentation.NativeKeys().SequenceEqual(new[]{key}),
                "the H01 root cue starts at PlayHeroPower and uses the source clock duration");
            presentation.PrepareNative();var spell=player.SourceSpell(key);
            Check(spell!=null&&spell.CardId=="HERO_01bp"&&spell.SourceEntity==2&&!spell.Flow.Faulted,
                "production PrepareNative creates the H01 root FSM for the skill entity");
            var sourceLocation=spell.Context.Placement.Location(config.location);
            var projectedButton=NativeSuperSpellBoard.Project(BattleLayout.Power(true).center,new Rect(0,0,1920,1080),
                new Vector3(20000,10000,20000));
            Check(sourceLocation!=null&&Vector3.Distance(sourceLocation.transform.position,projectedButton)<.001f,
                "the H01 source root is projected from the live skill button rather than the hero portrait");
            var visualTargets=spell.Context.Placement.Targets.GetTargets().ToArray();
            Check(visualTargets.Length==1&&visualTargets[0]==spell.Context.Placement.source.Object&&
                !spell.Flow.Flights.Any()&&!player.SourceImpactTime(key).HasValue,
                "target behavior 6 binds only the source visual target and does not require an Arrival target");
            presentation.Seek(.66f);presentation.PrepareNative();
            Check(spell.Effect.CameraShakerStarts==1&&spell.Effect.PendingCameraShakerInvocations==0,
                "the routed H01 root advances its original delayed camera invocation with the cue clock");

            var button=locations[2];var image=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32);
            var previewCamera=spell.Effect.PreviewCamera;
            string captureDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","work","f021-warrior-h01-button"));
            var originalClamps=new Dictionary<int,float>();
            for(int i=0;i<spell.Effect.Particles.Length;i++)
                if(spell.Effect.Particles[i]!=null&&NativeEffectInstance.NodeBelongsToSubtree(spell.Effect.Program,i,1))
                    originalClamps[i]=spell.Effect.Particles[i].GetComponent<ParticleSystemRenderer>().maxParticleSize;
            var frameFailures=new List<string>();
            try
            {
                Directory.CreateDirectory(captureDirectory);
                Check(image.Create(),"H01 button readback target is created at 1920x1080");
                foreach(float sampleAge in new[]{.68f,1.02f})
                {
                    string sampleName=sampleAge.ToString("0.00",CultureInfo.InvariantCulture);
                    presentation.Seek(sampleAge);presentation.PrepareNative();
                    bool cameraOrthoBefore=previewCamera.orthographic;
                    var previous=RenderTexture.active;
                    bool drawn=false;string drawError=null;
                    try
                    {
                        RenderTexture.active=image;GL.Clear(true,true,Color.black);
                        try{drawn=player.DrawHeroPowerButton(key,cue.data,before,after,button,sampleAge,
                            Matrix4x4.identity,image.width,image.height,Color.white);}
                        catch(Exception error){drawError=error.GetType().Name+": "+error.Message;}
                    }
                    finally{RenderTexture.active=previous;}
                    bool cameraOrthoAfterDraw=previewCamera.orthographic;
                    string capturePath=Path.Combine(captureDirectory,"button-"+sampleName.Replace('.','_')+".png");
                    var pixels=ReadButtonPixels(image,button,capturePath);
                    int boundsWidth=pixels.maxX>=pixels.minX?pixels.maxX-pixels.minX+1:0;
                    int boundsHeight=pixels.maxY>=pixels.minY?pixels.maxY-pixels.minY+1:0;
                    string probe;
                    try{probe=ProbeParticles(spell.Effect,sampleName);}
                    catch(Exception error){probe="H01 PARTICLE PROBE ERROR t="+sampleName+" "+error;frameFailures.Add("probe t="+sampleName+": "+error.Message);}
                    bool cameraOrthoAfterProbe=previewCamera.orthographic;
                    Debug.Log("WARRIOR H01 BUTTON GPU t="+sampleName+" drawn="+drawn+" drawError="+(drawError??"none")+
                        " cameraOrtho="+cameraOrthoBefore+"->"+cameraOrthoAfterDraw+"->"+cameraOrthoAfterProbe+
                        " nonblack="+pixels.nonblack+" bright="+pixels.bright+" bounds="+boundsWidth+"x"+boundsHeight+
                        " button="+button.width+"x"+button.height+" roi=3x image="+capturePath+"\n"+probe);
                    if(cameraOrthoAfterDraw!=cameraOrthoBefore||cameraOrthoAfterProbe!=cameraOrthoBefore)
                        frameFailures.Add("PreviewCamera.orthographic changed at t="+sampleName+" through "+
                            cameraOrthoBefore+"/"+cameraOrthoAfterDraw+"/"+cameraOrthoAfterProbe);
                    if(!drawn)frameFailures.Add("production button draw returned false at t="+sampleName);
                    if(!String.IsNullOrEmpty(drawError))frameFailures.Add("draw t="+sampleName+": "+drawError);
                    if(pixels.nonblack==0)frameFailures.Add("black GPU readback at t="+sampleName);
                    float requiredBoundsRatio=sampleAge<1?.25f:.9f;
                    if(pixels.bright<Mathf.CeilToInt(button.width*button.height*.01f)||
                        boundsWidth<button.width*requiredBoundsRatio||boundsHeight<button.height*requiredBoundsRatio)
                        frameFailures.Add("small bright bounds at t="+sampleName+" pixels="+pixels.bright+
                            " bounds="+boundsWidth+"x"+boundsHeight+" requiredRatio="+requiredBoundsRatio);
                    foreach(var clamp in originalClamps)
                    {
                        float current=spell.Effect.Particles[clamp.Key].GetComponent<ParticleSystemRenderer>().maxParticleSize;
                        if(Mathf.Abs(current-clamp.Value)>.00001f)
                            frameFailures.Add("ParticleSystemRenderer.maxParticleSize changed at node="+clamp.Key+
                                " from "+clamp.Value.ToString("R",CultureInfo.InvariantCulture)+" to "+
                                current.ToString("R",CultureInfo.InvariantCulture)+" after t="+sampleName);
                    }
                }
                Check(frameFailures.Count==0,"H01 button frames pass visual bounds and preserve source renderer clamps: "+
                    String.Join("; ",frameFailures.ToArray()));
            }
            finally{AssetRegistry.ReleaseTransient(image);}
        }
        Debug.Log("WARRIOR HERO POWER ROUTE: "+passed+" checks passed; PlayHeroPower source-only route has no target selection");
        }
        finally
        {
            NativeCameraShakeDriver.Stop(routeCamera);
            if(cameraObject!=null)AssetRegistry.ReleaseTransient(cameraObject);
        }
    }
}
