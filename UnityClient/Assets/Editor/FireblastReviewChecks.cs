using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

public static class FireblastReviewChecks
{
    static int checks;
    [Serializable] sealed class Metric{public string phase;public float sourceTime;public Rect visible,body,plane,hero;public Vector3 rootScale,ownerScale;}
    [Serializable] sealed class ParticleMetric{public string phase,node;public float sourceTime,remainingMin,remainingMax,sizeMin,sizeMax,alphaMin,alphaMax,luminanceMin,luminanceMax;public int count;public Vector3 root,particleMin,particleMax;public Color colorMin,colorMax;}
    [Serializable] sealed class Metrics{public Metric[] samples;public ParticleMetric[] particles;public float visibleThreshold=.01f;public string scope="Actual isolated source graph mapped to current 1920x1080 GUI; not official frame comparison";}
    static readonly List<Metric> metrics=new List<Metric>();
    static readonly List<ParticleMetric> particleMetrics=new List<ParticleMetric>();
    static void Check(bool value,string message){if(!value)throw new Exception("Fireblast review: "+message);checks++;}
    static StateView State(int seat,int version,int health)
    {
        return new StateView{match_id="fireblast-review",seat=seat,version=version,phase="play",events=new SemanticEvent[0],moves=new Move[0],players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_08",health=30},power=new EntityView{entity=2,card_id="HERO_08bp",cost=2},field=new EntityView[0],hand=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_07",health=health},power=new EntityView{entity=4,card_id="HERO_07bp",cost=2},field=new EntityView[0],hand=new EntityView[0]}}};
    }
    public static void Run()
    {
        checks=0;
        foreach(int seat in new[]{0,1})
        {
            var before=State(seat,1,30);var after=State(seat,2,29);
            after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2},new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
            float? arrival=null;int hitAudio=0;
            var presentation=new Presentation{NativeDuration=id=>id=="HERO_08bp"?.65f:0,NativeImpactTime=key=>arrival,NativeActive=key=>!arrival.HasValue,
                AudioCue=(e,id)=>{if(e.type=="Hit")hitAudio++;}};
            var rect=new Rect(500,300,190,200);presentation.Reset(before);presentation.Accept(before,after,new Dictionary<int,Rect>());
            presentation.Tick(.35f,new Dictionary<int,Rect>());
            Check(presentation.Display.players[1].hero.health==30,"authority result stays hidden before source Arrival seat"+seat);
            Check(presentation.Tint(3)==0,"no premature damage tint before native Arrival seat"+seat);
            Check(presentation.Shake==Vector2.zero&&presentation.Animate(3,rect)==rect,"no premature motion/shake before native Arrival seat"+seat);
            Check(hitAudio==0,"no premature Hit audio seat"+seat);
            presentation.Tick(.45f,new Dictionary<int,Rect>());arrival=.8f;presentation.Tick(.02f,new Dictionary<int,Rect>());
            Check(presentation.Display.players[1].hero.health==29&&hitAudio==1,"one visible damage/Hit audio at actual Arrival seat"+seat);
            var hit=presentation.Cues.Single(c=>c.data.type=="Hit");Check(Mathf.Abs(hit.start-presentation.TimeInBatch)<.00001f,"full reaction starts when Arrival is observed seat"+seat);
            presentation.Tick(.04f,new Dictionary<int,Rect>());Check(presentation.Tint(3)>0&&presentation.Animate(3,rect)!=rect,"full visible damage reaction after Arrival seat"+seat);
            presentation.Tick(.1f,new Dictionary<int,Rect>());Check(hitAudio==1,"no repeated Hit callback after Arrival seat"+seat);
        }
        RunInputLockChecks();
        foreach(bool lethal in new[]{false,true})
        {
            var before=State(1,1,30);var after=State(1,2,30);
            before.players[1].field=new[]{new EntityView{entity=5,card_id="CS2_119",health=lethal?1:7}};
            after.players[1].field=lethal?new EntityView[0]:new[]{new EntityView{entity=5,card_id="CS2_119",health=6}};
            var events=new List<SemanticEvent>{new SemanticEvent{type="PlayHeroPower",source=1,target=2},new SemanticEvent{type="Hit",source=2,target=5,amount=1}};
            if(lethal)events.Add(new SemanticEvent{type="Death",target=5});after.events=events.ToArray();
            float? arrival=null;int hits=0;var p=new Presentation{NativeDuration=id=>.65f,NativeImpactTime=key=>arrival,NativeActive=key=>!arrival.HasValue,AudioCue=(e,id)=>{if(e.type=="Hit")hits++;}};
            var locations=new Dictionary<int,Rect>();p.Reset(before);p.Accept(before,after,locations);p.Tick(2,locations);
            Check(hits==0&&p.Tint(5)==0&&!p.Dying(5)&&BattleInteraction.Entity(p.Display,5)!=null,"late observation preserves target without pre-impact feedback lethal="+lethal);
            arrival=.8f;p.Tick(.02f,locations);var hit=p.Cues.Single(c=>c.data.type=="Hit");
            Check(hits==1&&Mathf.Abs(hit.start-p.TimeInBatch)<.00001f,"late native timestamp does not expire Hit window lethal="+lethal);
            p.Tick(.04f,locations);Check(p.Tint(5)>0&&!p.Dying(5),"full Hit remains visible before death lethal="+lethal);
            if(lethal){var death=p.Cues.Single(c=>c.data.type=="Death");Check(death.start>=hit.start+hit.duration,"death retains post-hit ordering");p.Tick(death.start-p.TimeInBatch+.03f,locations);Check(p.Dying(5)&&BattleInteraction.Entity(p.Display,5)!=null,"death animation retains disappeared authority target");Check(p.InputLocked,"death feedback keeps gameplay input locked lethal target");}
            p.Tick(2,locations);Check(hits==1&&(!lethal||BattleInteraction.Entity(p.Display,5)==null),"one completed reaction and final target cleanup lethal="+lethal);
            Check(!p.InputLocked&&p.DamageDecorationCount==1,"decorative DamageSplat outlives reaction and death queue lethal="+lethal);
            p.Tick(4.2f,locations);Check(p.DamageDecorationCount==0,"DamageSplat expires on its own lifetime lethal="+lethal);
        }
        Debug.Log("FIREBLAST REVIEW: "+checks+" timing checks passed");
    }
    static void RunInputLockChecks()
    {
        var before=State(0,1,30);var after=State(0,2,29);
        after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2},new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
        float? arrival=null;bool nativeActive=true;
        var presentation=new Presentation{NativeDuration=id=>id=="HERO_08bp"?.65f:0,NativeImpactTime=key=>arrival,NativeActive=key=>nativeActive};
        var locations=new Dictionary<int,Rect>{{3,BattleLayout.Hero(false)}};
        presentation.Reset(before);presentation.Accept(before,after,locations);presentation.Tick(.8f,locations);
        Check(presentation.InputLocked,"unresolved native Arrival keeps input closed");
        arrival=.8f;presentation.Tick(.02f,locations);presentation.Tick(.54f,locations);
        Check(presentation.InputLocked,"Hit feedback window keeps input closed");
        presentation.Tick(.02f,locations);
        Check(presentation.Busy&&!presentation.InputLocked&&presentation.DamageDecorationCount==1,"input unlocks while native cleanup and DamageSplat continue");
        var next=State(0,3,29);next.events=new[]{new SemanticEvent{type="TURN_START",source=1}};
        presentation.Accept(after,next,locations);
        Check(presentation.InputLocked&&presentation.QueueCount==2,"a newly accepted action waits behind the current visual batch");
        nativeActive=false;presentation.Tick(.06f,locations);
        Check(presentation.Busy&&presentation.InputLocked&&presentation.Cues.Any(c=>c.data.type=="TURN_START"),"queued action begins in order after source cleanup");
        presentation.Tick(4.2f,locations);
        Check(!presentation.Busy&&!presentation.InputLocked&&presentation.DamageDecorationCount==0,"queue drains while the independent DamageSplat lifetime expires");
    }
    static Color[] Read(RenderTexture target,string file=null)
    {
        var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
        try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();if(file!=null)File.WriteAllBytes(file,image.EncodeToPNG());return image.GetPixels();}
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
    static Rect PixelBounds(Color[] pixels,int width,int height)
    {
        int minX=width,minY=height,maxX=-1,maxY=-1;
        for(int i=0;i<pixels.Length;i++)if(Mathf.Max(pixels[i].r,Mathf.Max(pixels[i].g,pixels[i].b))>.01f){int x=i%width,y=i/width;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        return maxX<0?Rect.zero:new Rect(minX*1920f/width,(height-1-maxY)*1080f/height,(maxX-minX+1)*1920f/width,(maxY-minY+1)*1080f/height);
    }
    static Metric Measure(string phase,NativeSuperSpellPlayback spell,NativeEffectInstance effect,NativeGeneratedCapture capture,RenderTexture frame,Color[] pixels)
    {
        var points=capture.Plane.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>spell.Effect.PreviewPoint(capture.Plane.transform.TransformPoint(v))).ToArray();
        var metric=new Metric{phase=phase,sourceTime=spell.Time,visible=PixelBounds(pixels,frame.width,frame.height),plane=Rect.MinMaxRect(points.Min(v=>v.x)*1920,points.Min(v=>v.y)*1080,points.Max(v=>v.x)*1920,points.Max(v=>v.y)*1080),hero=BattleLayout.Hero(true),rootScale=effect.Nodes[0].transform.lossyScale,ownerScale=capture.Plane.transform.parent.lossyScale};
        metrics.Add(metric);Debug.Log("FIREBLAST REVIEW SCREEN "+phase+" time="+spell.Time+" visible="+metric.visible+" plane="+metric.plane+" hero="+metric.hero+" visibleHeight/hero="+metric.visible.height/metric.hero.height);
        return metric;
    }
    static IEnumerable<Renderer> Renderers(NativeEffectInstance effect)
    {return effect.Nodes.Where(n=>n!=null).SelectMany(n=>n.GetComponents<Renderer>()).Concat(effect.GeneratedCaptures.Where(c=>c.Active).SelectMany(c=>c.Plane.GetComponentsInChildren<Renderer>())).Distinct();}
    public static void RunVisual()
    {
        Run();checks=0;metrics.Clear();particleMetrics.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(assets.Root);
        string folder=Path.GetFullPath("../work/f005c-r2-gpu");Directory.CreateDirectory(folder);
        var owned=new List<UnityEngine.Object>();
        using(var f=new FireblastSelectionChecks.Fixture(assets,true))
        {
            Check(f.Player.SelectPower(f.State,2),"real source selection starts");var root=f.Player.PowerSelection;
            foreach(float age in new[]{.05f,.15f,.3f,.6f,1.5f,3f})
            {
                f.Selection(age);var frame=root.RenderAffine(root.Time);var pixels=Read(frame,Path.Combine(folder,"selected-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                var effect=root.Flow.Instances.Single().Effect;var capture=effect.GeneratedCaptures.Single();owned.Add(effect.Root);
                Check(capture.Active&&capture.Texture.width==170&&capture.Texture.height==170,"source Start automatic capture active at "+age);
                var colors=Read(capture.Texture,Path.Combine(folder,"start-source-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                Check(colors.Any(c=>c.a>.01f)&&colors.Max(c=>c.r)-colors.Min(c=>c.r)>.02f,"hidden source body contributes actual capture pixels at "+age);
                Check(capture.Plane.transform.parent==effect.Nodes[effect.Program.captures.Single().owner].transform&&capture.Plane.transform.localScale==Vector3.one,"original owner-child scale at "+age);
                Measure("selection",root,effect,capture,frame,pixels);
                Debug.Log("FIREBLAST REVIEW CAPTURE age="+age+" rootScale="+effect.Nodes[0].transform.lossyScale+" ownerScale="+capture.Plane.transform.parent.lossyScale+" planeBounds="+capture.Plane.GetComponent<Renderer>().bounds+" alphaPixels="+colors.Count(c=>c.a>.01f));
            }
            var current=root.Flow.Instances.Single().Effect;var output=current.GeneratedCaptures.Single();var old=output.Texture;var owner=output.Plane.transform.parent.gameObject;
            owner.SetActive(false);output.Render();Check(!output.Active&&old==null,"owner disable releases auto plane and capture");owner.SetActive(true);output.Render();
            Check(output.Active&&output.Texture!=null,"owner reenable rebuilds original output");owned.Add(output.Texture);owned.Add(output.Plane);
            f.Player.CancelPowerSelection(false);Check(owned.All(o=>o==null),"hard cancellation physically releases generated objects and RTs");
        }
        using(var f=new FireblastSelectionChecks.Fixture(assets,true))
        {
            f.Player.SelectPower(f.State,2);f.Selection(3);float last=-1;
            f.CastToEnd("review-stages",3,(spell,time)=>
            {
                if(time-last<.05f)return;
                var missile=spell.Flow.Instances.Select(i=>i.Effect).FirstOrDefault(e=>e.Program.captures.Any(c=>c.width==2.5f));if(missile==null)return;
                var frame=spell.RenderAffine(spell.Time);var capture=missile.GeneratedCaptures.Single();if(!capture.Active)return;
                last=time;var pixels=Read(frame,Path.Combine(folder,"flight-"+time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                var metric=Measure(spell.Flow.FirstArrivalTime.HasValue?"arrival-tail":"flight",spell,missile,capture,frame,pixels);
                foreach(string particleName in new[]{"FireParticleLeft","FireParticleFill","FireParticleRight","Sparks01","Smoke01"})
                {
                    int nodeIndex=Array.FindIndex(missile.Program.nodes,n=>n.name==particleName);var system=missile.Particles[nodeIndex];
                    var particles=new ParticleSystem.Particle[Mathf.Max(1,system.particleCount)];int count=system.GetParticles(particles);
                    if(count==0)continue;
                    var first=particles[0].position;float minLife=particles[0].remainingLifetime,maxLife=minLife;var min=first;var max=first;
                    Color firstColor=particles[0].startColor;float minAlpha=firstColor.a,maxAlpha=minAlpha,minLuminance=firstColor.grayscale,maxLuminance=minLuminance;
                    float firstSize=particles[0].startSize,minSize=firstSize,maxSize=firstSize;var minColor=firstColor;var maxColor=firstColor;
                    for(int i=1;i<count;i++){var p=particles[i];Color color=p.startColor;float size=p.startSize;min=Vector3.Min(min,p.position);max=Vector3.Max(max,p.position);minLife=Mathf.Min(minLife,p.remainingLifetime);maxLife=Mathf.Max(maxLife,p.remainingLifetime);minAlpha=Mathf.Min(minAlpha,color.a);maxAlpha=Mathf.Max(maxAlpha,color.a);minLuminance=Mathf.Min(minLuminance,color.grayscale);maxLuminance=Mathf.Max(maxLuminance,color.grayscale);minSize=Mathf.Min(minSize,size);maxSize=Mathf.Max(maxSize,size);minColor.r=Mathf.Min(minColor.r,color.r);minColor.g=Mathf.Min(minColor.g,color.g);minColor.b=Mathf.Min(minColor.b,color.b);minColor.a=Mathf.Min(minColor.a,color.a);maxColor.r=Mathf.Max(maxColor.r,color.r);maxColor.g=Mathf.Max(maxColor.g,color.g);maxColor.b=Mathf.Max(maxColor.b,color.b);maxColor.a=Mathf.Max(maxColor.a,color.a);}
                    particleMetrics.Add(new ParticleMetric{phase=spell.Flow.FirstArrivalTime.HasValue?"arrival-tail":"flight",node=particleName,sourceTime=spell.Time,remainingMin=minLife,remainingMax=maxLife,count=count,root=missile.Nodes[nodeIndex].transform.position,particleMin=min,particleMax=max,sizeMin=minSize,sizeMax=maxSize,alphaMin=minAlpha,alphaMax=maxAlpha,luminanceMin=minLuminance,luminanceMax=maxLuminance,colorMin=minColor,colorMax=maxColor});
                }
                var original=spell.Flow.Instances.SelectMany(i=>Renderers(i.Effect)).Distinct().ToDictionary(r=>r,r=>r.enabled);var keep=new HashSet<Renderer>(Renderers(missile));
                try
                {
                    foreach(var renderer in original.Keys)if(!keep.Contains(renderer))renderer.enabled=false;
                    var body=spell.Effect.RenderTogether(spell.Flow.Instances.Select(i=>i.Effect));var bodyPixels=Read(body,Path.Combine(folder,"missile-body-"+time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                    metric.body=PixelBounds(bodyPixels,body.width,body.height);Debug.Log("FIREBLAST REVIEW MISSILE BODY time="+spell.Time+" state="+missile.ActiveSourceSpellState+" visible="+metric.body+" height/hero="+metric.body.height/metric.hero.height);
                }
                finally{foreach(var pair in original)if(pair.Key!=null)pair.Key.enabled=pair.Value;}
            });
            Check(metrics.Any(m=>m.phase=="flight")&&metrics.Any(m=>m.phase=="arrival-tail"),"source phase samples include flight and actual Arrival tail");
        }
        using(var f=new FireblastSelectionChecks.Fixture(assets,true))
        {
            f.Player.SelectPower(f.State,2);f.Selection(.6f);var root=f.Player.PowerSelection;
            bool seen=false;owned.Clear();
            f.CastToEnd("review-flight",2,(spell,time)=>
            {
                if(seen)return;var missile=spell.Flow.Instances.Select(i=>i.Effect).FirstOrDefault(e=>e.Program.captures.Any(c=>c.width==2.5f));if(missile==null)return;
                var frame=spell.RenderAffine(spell.Time);var capture=missile.GeneratedCaptures.Single();if(!capture.Active||!Read(capture.Texture).Any(c=>c.a>.01f))return;
                seen=true;Read(frame,Path.Combine(folder,"missile-early.png"));Read(capture.Texture,Path.Combine(folder,"missile-source.png"));
                Check(capture.Texture.width==111&&capture.Texture.height==380,"source missile long-edge capture truncation 111x380");
                Check(true,"actual hidden missile body is captured at source time "+spell.Time);owned.Add(capture.Texture);owned.Add(capture.Plane);owned.Add(missile.Root);
            });
            Check(seen,"actual source state reaches visible missile body");f.Player.Dispose();Check(owned.All(o=>o==null),"active missile hard disposal releases capture and output");
        }
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);var cameraObject=new GameObject("Review ambient GPU");var camera=cameraObject.AddComponent<Camera>();
        quad.layer=29;quad.transform.position=new Vector3(5000,0,0);camera.transform.position=new Vector3(5000,0,-2);camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<29;camera.enabled=false;
        var target=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();camera.targetTexture=target;
        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false,true);texture.SetPixel(0,0,new Color(.4f,.2f,.1f,1));texture.Apply();
        var material=new Material(Resources.Load<Shader>("OriginalActorPortrait"));material.mainTexture=texture;material.SetColor("_SecondTint",Color.clear);quad.GetComponent<Renderer>().sharedMaterial=material;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        try
        {
            var renderer=quad.GetComponent<Renderer>();NativeActorMaterial.BindAmbient(renderer,assets);camera.Render();var saved=Read(target)[16*32+16];
            assets.BattleAmbient=()=>Color.white;NativeActorMaterial.BindAmbient(renderer,assets);camera.Render();var flat=Read(target)[16*32+16];var sample=texture.GetPixel(0,0);
            Check(Mathf.Abs(flat.r-sample.r)<.01f&&Mathf.Abs(flat.g-sample.g)<.01f&&Mathf.Abs(flat.b-sample.b)<.01f,"original portrait shader with real packed flat-white SH preserves input RGB");
            Check(saved.r<flat.r*.9f,"saved scene probe reproduces dim input before board ambient restore");
            var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);Debug.Log("FIREBLAST REVIEW AMBIENT saved="+saved+" boardWhite="+flat+" texture="+sample+" SHAr="+properties.GetVector("unity_SHAr"));
        }
        finally{camera.targetTexture=null;AssetRegistry.ReleaseTransient(texture);AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(mesh);AssetRegistry.ReleaseTransient(target);AssetRegistry.ReleaseTransient(quad);AssetRegistry.ReleaseTransient(cameraObject);}
        File.WriteAllText(Path.Combine(folder,"screen-metrics.json"),JsonUtility.ToJson(new Metrics{samples=metrics.ToArray(),particles=particleMetrics.ToArray()},true));
        Debug.Log("FIREBLAST REVIEW: "+checks+" actual capture/ambient GPU checks passed");
    }
    public static void RunClock()
    {
        checks=0;var clock=new PresentationFrameClock();
        Check(clock.Sample(1000000)==0,"first clock sample has no startup catchup");Check(clock.Sample(1000000.125)==.125f,"double precision frame interval");Check(clock.Sample(1000000.125)==0,"duplicate timestamp has zero interval");
        Check(clock.Sample(999999)==0&&clock.Sample(999999.25)==.25f,"negative discontinuity rebases without negative frame");
        bool invalid=false;try{clock.Sample(double.NaN);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"nonfinite monotonic input is diagnosed");
        clock.Reset();Check(clock.Sample(500)==0,"scene/pause reset does not carry old time");
        var before=State(1,1,28);var after=State(1,2,26);after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=3,target=4},new SemanticEvent{type="Hit",source=4,target=3,amount=2}};
        var locations=new Dictionary<int,Rect>();var p=new Presentation();p.Reset(before);p.Accept(before,after,locations);double now=500;
        p.Tick(clock.Sample(now+=.1),locations);Check(Mathf.Abs(p.TimeInBatch-.1f)<.00001f&&p.Display.players[1].hero.health==28,"ordinary Warlock power uses one frame input before impact");
        float elapsed=p.TimeInBatch;p.Paused=true;for(int i=0;i<30;i++)p.Tick(clock.Sample(now+=.1),locations);
        Check(p.TimeInBatch==elapsed,"pause freezes scene while clock continues sampling");p.Paused=false;p.Tick(clock.Sample(now+=.1),locations);
        Check(Mathf.Abs(p.TimeInBatch-.2f)<.00001f,"resume advances only next frame without paused catchup");
        p.Speed=2;p.Tick(clock.Sample(now+=.05),locations);Check(Mathf.Abs(p.TimeInBatch-.3f)<.00001f,"ordinary scene 2x applies speed once");
        p.Speed=.5f;p.Tick(clock.Sample(now+=.1),locations);Check(Mathf.Abs(p.TimeInBatch-.35f)<.00001f&&p.Display.players[1].hero.health==26,"ordinary scene half speed and real power damage");
        p.Tick(clock.Sample(now+=2),locations);Check(!p.Busy&&p.Display.players[1].hero.health==26,"ordinary power completes with same authoritative result");
        Debug.Log("FIREBLAST FRAME CLOCK: "+checks+" checks passed; explicit frame input, existing scene clocks unchanged");
    }
    public static void RunBuild(){RunClock();RunVisual();FireblastShaderChecks.Run();BuildClient.Build();}
    public static void RunRegressionBuild(){RunVisual();FireblastShaderChecks.Run();FireblastSelectionChecks.Run();FireblastRegressionChecks.Run();BuildClient.Build();}
}
