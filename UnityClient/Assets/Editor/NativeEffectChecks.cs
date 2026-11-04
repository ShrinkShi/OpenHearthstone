using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeEffectChecks
{
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception("Native effect: "+message);passed++;}
    public static void Run()
    {
        passed=0;
        var two=new NativeCurve{mode=3,minimum=2,scalar=5}.UnityCurve();Check(two.mode==ParticleSystemCurveMode.TwoConstants&&two.constantMin==2&&two.constantMax==5,"random min/max remain a Unity distribution");
        var step=NativeCurve.Keys(new[]{new NativeKey{time=0,value=0,step=true},new NativeKey{time=1,value=1}});Check(step.Evaluate(.5f)==0,"step keys preserve discontinuities");
        var independent=NativeCurve.Keys(new[]{new NativeKey{time=0,value=1,out_step=true},new NativeKey{time=1,value=4,in_step=true,outSlope=2},new NativeKey{time=2,value=6,inSlope=2}});
        Check(independent.Evaluate(.5f)==1&&Mathf.Abs(independent.Evaluate(1.5f)-5)<.0001f,"streamed incoming step retains smooth outgoing tangent");
        string folder=Path.GetFullPath("../local/runtime");var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var entries=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(folder,"native-effect-index.json"))).entries;
        foreach(var entry in entries)
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,entry.file)));
            Check(program.nodes.Length>0,"hierarchy "+entry.card_id);
            foreach(var clip in program.animations)Check(clip.wrap_known&&NativeLegacyWrap.Supported(clip.wrap_mode),"exact source Clip wrap mode is present "+entry.card_id+"/"+clip.name);
            foreach(var n in program.nodes)
            {
                Check(n.parent<program.nodes.Length,"bounded parent");
                if(n.animation_refs!=null&&n.animation_refs.Length>0)Check(n.legacy_animation!=null&&n.legacy_animation.wrap_known,"source Animation component mode mapped to exact node "+entry.card_id+"/"+n.path);
                if(n.particle==null||!n.particle.restored)continue;
                Check(n.particle.life.scalar>=0&&n.particle.max_particles>0,"original main module");
                foreach(var b in n.particle.emission.bursts)Check(b.count.scalar>=0&&b.time>=0,"original burst");
            }
            foreach(var m in program.machines)foreach(var s in m.states)foreach(var a in s.actions)foreach(var p in a.@params)
                Check(p.node<program.nodes.Length,"typed FSM node reference");
        }
        var arcane=entries.First(e=>e.card_id=="CS2_025");var data=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,arcane.file)));
        using(var first=new NativeEffectInstance(data,assets,0,128,128))
        using(var second=new NativeEffectInstance(data,assets,1,128,128))
        {
            first.Seek(.25f);second.Seek(.25f);Check(first.ParticleCount>0,"FSM actually starts original emission");Check(first.ParticleCount==second.ParticleCount,"deterministic seeded simulation");
            int count=first.ParticleCount;first.Seek(.8f);first.Seek(.25f);Check(first.ParticleCount==count,"backward seek rebuilds particle state");
            Check(first.TransitionLimitHits==0,"original finite FSM is not truncated");
            var original=data.nodes.First(n=>n.particle!=null&&n.particle.restored).particle;
            var ps=first.Particles.First(p=>p!=null);Check(ps.main.duration==original.duration&&ps.main.startLifetime.constantMax==original.life.scalar,"main module values reach Shuriken");
            var reveal=data.animations.First(a=>a.name=="FX_ArcaneExplosion1");Check(reveal.tracks.Any(t=>t.property=="_Intensity"),"original material animation restored");
        }
        var ambientProgram=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(data));
        int ambientNode=Array.FindIndex(ambientProgram.nodes,n=>!String.IsNullOrEmpty(n.mesh)&&n.materials.Any(m=>m.properties!=null&&m.properties.Contains("_Color")));
        var readAmbient=new NativeAction{name="GetBoardAmbientColorAction",@params=new[]{new NativeParam{name="m_Color",variable="ambient"},new NativeParam{name="m_EveryFrame",number=0}}};
        var writeAmbient=new NativeAction{name="SetMaterialColor",@params=new[]{new NativeParam{name="gameObject",node=ambientNode},new NativeParam{name="namedColor",text="_Color"},new NativeParam{name="color",variable="ambient"},new NativeParam{name="everyFrame",number=1}}};
        ambientProgram.machines=new[]{new NativeMachine{start="Read",floats=new NativeFloat[0],colors=new[]{new NativeColor{name="ambient",value=Color.white}},events=new NativeTransition[0],states=new[]{new NativeState{name="Read",actions=new[]{readAmbient,writeAmbient},transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}}},new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
        var previousAmbient=RenderSettings.ambientLight;var boardAmbient=new Color(.7f,.4f,.2f,1);
        using(var player=new NativeEffectPlayer(assets))Check(player.BoardAmbientColor==Color.white,"normal board ambient default follows original Board constructor");
        try
        {
            RenderSettings.ambientLight=new Color(.1f,.2f,.3f,1);
            using(var instance=new NativeEffectInstance(ambientProgram,assets,0,32,32,()=>boardAmbient))
            {instance.Seek(0);Check(instance.Nodes[ambientNode].GetComponent<Renderer>().sharedMaterial.GetColor("_Color")==boardAmbient,"GetBoardAmbientColorAction prefers original board context");}
            using(var instance=new NativeEffectInstance(ambientProgram,assets,0,32,32))
            {instance.Seek(0);Check(instance.Nodes[ambientNode].GetComponent<Renderer>().sharedMaterial.GetColor("_Color")==RenderSettings.ambientLight,"GetBoardAmbientColorAction falls back to RenderSettings");}
            readAmbient.name="GetAmbientColorAction";readAmbient.@params[1].number=1;
            using(var instance=new NativeEffectInstance(ambientProgram,assets,0,32,32,()=>boardAmbient))
            {
                instance.Seek(0);var material=instance.Nodes[ambientNode].GetComponent<Renderer>().sharedMaterial;
                Check(material.GetColor("_Color")==boardAmbient,"GetAmbientColorAction OnEnter reads board");
                RenderSettings.ambientLight=new Color(.3f,.6f,.9f,1);instance.Seek(.1f);
                Check(material.GetColor("_Color")==RenderSettings.ambientLight,"GetAmbientColorAction every-frame reads RenderSettings without finishing state");
                instance.Seek(0);Check(material.GetColor("_Color")==boardAmbient,"backward seek repeats ambient OnEnter");
            }
        }
        finally{RenderSettings.ambientLight=previousAmbient;}
        CardTypeChecks(ambientProgram,assets);
        AnimatorChecks(entries,folder,assets);
        ScaleChecks(folder,assets);
        ITweenChecks(folder,assets);
        ActorObjectChecks(folder,assets);
        ActorVisibilityChecks(folder,assets);
        ScaleFromChecks(folder,assets);
        MachineOwnerChecks(folder,assets);
        ActorTemplateChecks(folder,assets);
        NativeActorSpellTableChecks.Run(assets);
        NativeActorAnimationChecks.Run(assets);
        NativeAudioChecks.Run(assets);
        NativeBattleSummonChecks.Run();
        LifecycleChecks(folder,assets);
        ColorChecks(folder,assets);
        EaseChecks(folder,assets);
        LegacyClockChecks(data,assets);
        LegacyWrapChecks(data,assets);
        AnimationCompletionChecks(data,assets);
        foreach(var sample in new[]{new{file="4fc0fe4fe4824ecbe3db289c.native-effect.json",shader="Hero/AddMultiply/AddMultiplyFXRotation",family=13},new{file="5e8791da25f86e52a2d27696.native-effect.json",shader="Hero/AddMultiply/AddMultiplyFXScrollingManual",family=12},new{file="00029afcc27a43680cb13656.native-effect.json",shader="Hero/Additive/Additive",family=16},new{file="0194accb9992f97829c87db2.native-effect.json",shader="Hero/Particle/Particles_Transparent",family=17},new{file="13e4ba2cda5d567ad3d3e0be.native-effect.json",shader="Hero/Particle/Particles_Transparent_ColorAsAlpha",family=18},new{file="03f4011c21d2abaf92986b43.native-effect.json",shader="FX/CommonFX",family=19}})
        using(var instance=new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,sample.file))),assets,0,32,32))
        {
            int count=0;
            for(int n=0;n<instance.Nodes.Length;n++)
            {
                var renderer=instance.Nodes[n].GetComponent<Renderer>();if(renderer==null)continue;
                for(int m=0;m<instance.Program.nodes[n].materials.Length;m++)
                    if(instance.Program.nodes[n].materials[m].shader==sample.shader && (sample.family!=19 || new System.Collections.Generic.HashSet<string>(instance.Program.nodes[n].materials[m].keywords).SetEquals(new[]{"_COLORING_NONE","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_NONE","_TEXTURETYPE_RGBR"})))
                    {count++;Check(renderer.sharedMaterials[m].GetFloat("_OriginalFamily")==sample.family,"actual original cached FX material binds verified family");}
            }
            Check(count>0,"actual original rotation/manual material fixture is present");
            instance.Seek(.3f);Check(instance.TransitionLimitHits==0,"actual original FX hierarchy remains traversable");
            if(sample.family==17)
            {
                var material=instance.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).First(m=>m.GetFloat("_OriginalFamily")==17);
                material.SetFloat("_RgbSrcBlend",1);material.SetFloat("_RgbDstBlend",1);
                material.SetFloat("_AlphaSrcBlend",1);material.SetFloat("_AlphaDstBlend",1);
                instance.Seek(.31f);
                Check(material.GetInt("_SrcBlend")==1&&material.GetInt("_DstBlend")==1,"live property-backed blend change reaches rendering");
                Check(material.GetInt("_OriginalSrcAlpha")==1&&material.GetInt("_OriginalDstAlpha")==1,"live alpha blend reaches original internal/affine capture");
                instance.Seek(0);
                Check(material.GetInt("_SrcBlend")==material.GetInt("_RgbSrcBlend")&&material.GetInt("_OriginalSrcAlpha")==material.GetInt("_AlphaSrcBlend"),"rewind restores source blend properties");
            }
            if(sample.family==19)
            {
                var material=instance.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).First(m=>m.GetFloat("_OriginalFamily")==19);
                material.SetFloat("_SrcBlendOutline",5);material.SetFloat("_DstBlendOutline",1);instance.Seek(.31f);
                Check(material.GetInt("_SrcBlend")==5&&material.GetInt("_DstBlend")==1,"CommonFX live original blend properties reach base rendering");
                Check(material.GetInt("_OriginalSrcAlpha")==5&&material.GetInt("_OriginalDstAlpha")==1,"CommonFX live alpha blend reaches original capture");
                instance.Seek(0);
                Check(material.GetInt("_SrcBlend")==material.GetInt("_SrcBlendOutline")&&material.GetInt("_DstBlend")==material.GetInt("_DstBlendOutline"),"CommonFX rewind restores source blends");
            }
        }
        var frost=entries.First(e=>e.card_id=="CS2_026");
        using(var instance=new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,frost.file))),assets,0,128,128))
        {
            instance.Seek(0);var masks=instance.Nodes.Where(n=>n.name=="Offscreen").SelectMany(n=>n.GetComponentsInChildren<Renderer>(true)).ToArray();
            Check(masks.Length>0&&masks.All(r=>!r.enabled),"internal masks stay hidden on the initial frame");
            Check(instance.Program.captures.Length==2,"both original frost capture components restored");
            var multiply=instance.Nodes[12].GetComponent<Renderer>().sharedMaterial;
            Check(multiply.GetFloat("_OriginalFamily")==7&&multiply.GetFloat("_SrcBlend")==0&&multiply.GetFloat("_DstBlend")==3,"actual Frost Nova Multiply binds original program and destination blend");
            Check(multiply.GetFloat("_CaptureSrcAlpha")==0&&multiply.GetFloat("_CaptureDstAlpha")==3,"actual Frost Nova Multiply preserves original alpha multiplication");
            foreach(var capture in instance.Program.captures)
            {
                Check(capture.near<0&&capture.far>0,"original signed clipping range retained");
                var renderer=instance.Nodes[capture.target].GetComponent<Renderer>();
                var texture=renderer.sharedMaterial.mainTexture as RenderTexture;
                Check(texture!=null&&texture.width==350&&texture.height==184,"capture bound to exact target with original aspect");
                var children=instance.Nodes[capture.target].GetComponentsInChildren<ParticleSystemRenderer>(true);
                Check(children.All(r=>r.sharedMaterial.mainTexture!=texture),"target binding preserves child particle atlas");
            }
            instance.Render(.3f);Check(masks.All(r=>!r.enabled),"capture sources remain isolated after animated visibility");
            instance.Render(.12f);Check(masks.All(r=>!r.enabled),"backward seek keeps capture sources isolated");
            var ring=instance.Nodes.First(n=>n.name=="Ring_mesh").GetComponent<Renderer>().sharedMaterial;
            Check(ring.GetFloat("_Intensity")==1,"undeclared legacy Intensity does not overexpose frost ring");
            Check(ring.GetFloat("_OriginalFamily")==1&&ring.GetFloat("_ColorMul")==2,"original particle program family and declared color multiplier");
            instance.Seek(.6f);Check(ring.GetFloat("_Intensity")==1,"animation cannot revive undeclared material property");
        }
        var hellfire=entries.First(e=>e.card_id=="CS2_062");
        using(var instance=new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,hellfire.file))),assets,0,32,32))
        {
            foreach(var node in instance.Nodes)
            {
                var renderer=node.GetComponent<Renderer>();if(renderer==null)continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material.name.StartsWith("GenFX_ShockwaveHellfire"))
                        Check(material.GetFloat("_OriginalFamily")==8,"actual Hellfire shockwave binds clip-coordinate FXWorld");
                    if(material.name.StartsWith("GenFX_Hell_Ground"))
                        Check(material.GetFloat("_OriginalFamily")==9,"actual Hellfire internal ground binds FXScrolling");
                    if(material.name.StartsWith("GenFX_HellfireMask2"))
                    {
                        Check(material.GetFloat("_OriginalFamily")==10,"actual Hellfire wipe mask binds original dissolve program");
                        Check(Mathf.Abs(material.GetFloat("_Alpha")-3.29f)<.001f,"actual Hellfire declared Alpha reaches RGB-only dissolve multiplier");
                        Check(Mathf.Abs(material.GetFloat("_DissolveIntensity")-10.28f)<.001f,"actual Hellfire declared dissolve edge gain retained");
                    }
                    if(instance.Program.nodes[Array.IndexOf(instance.Nodes,node)].materials.Any(m=>m.shader=="Utility/Black"))
                    {
                        Check(material.GetFloat("_OriginalFamily")==11&&material.GetFloat("_HasSourceTexture")==1,"actual textureless Hellfire black wipe is renderable");
                        Check(material.GetFloat("_Cull")==2&&material.GetFloat("_ZWrite")==1,"actual black wipe preserves back-face culling and depth write");
                    }
                }
            }
            instance.Seek(.34f);Check(instance.FinishNotifications.Count==2&&instance.FinishNotifications.All(n=>!n.delivered),"actual Hellfire delayed completion does not fire before .35 seconds");
            instance.Seek(.36f);Check(instance.FinishNotifications.All(n=>n.delivered&&n.time>.35f&&n.time<.351f),"actual Hellfire delayed callbacks are independent of six-second clip");
            Check(instance.Nodes[18].GetComponent<Renderer>().enabled&&instance.Nodes[36].GetComponent<Renderer>().enabled,"actual Hellfire visual tail remains after completion callbacks");
            instance.Seek(2.02f);Check(instance.Nodes[18].GetComponent<Renderer>().enabled&&instance.Nodes[36].GetComponent<Renderer>().enabled,"actual Hellfire one-second camera action precedes two-second visual tail wait");
            instance.Seek(3.03f);Check(!instance.Nodes[18].GetComponent<Renderer>().enabled&&!instance.Nodes[36].GetComponent<Renderer>().enabled,"actual Hellfire original camera action plus Wait hides both captured planes after three seconds");
            instance.Seek(.1f);Check(instance.FinishNotifications.Count==2&&instance.FinishNotifications.All(n=>!n.delivered),"backward replay rebuilds finish callbacks without duplicates");
        }
        CommonFXSecondAlphaChecks(folder,assets);
        var before=new StateView{match_id="native-queue",version=1,seat=0,phase="play",players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,health=30},hand=new[]{new EntityView{entity=10,card_id="CS2_025"}},field=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=2,health=30},hand=new EntityView[0],field=new EntityView[0]}}};
        var after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));after.version=2;after.players[1].hero.health=29;
        after.events=new[]{new SemanticEvent{type="CardPlay",card_id="CS2_025",card_type=5,source=10},new SemanticEvent{type="Hit",source=10,target=2,amount=1}};
        var presentation=new Presentation{NativeDuration=id=>1.2f};int history=0;presentation.BatchCompleted=(a,b)=>history++;
        presentation.Accept(before,after,new System.Collections.Generic.Dictionary<int,Rect>());
        var effect=presentation.Cues.First(c=>c.data.type=="SpellEffect");var hit=presentation.Cues.First(c=>c.data.type=="Hit");
        Check(effect.duration==1.2f&&hit.start>=effect.start+effect.duration,"native effect interval precedes damage");
        presentation.Seek(effect.start+.5f);Check(presentation.Display.players[1].hero.health==30&&after.players[1].hero.health==29,"authoritative state can lead visible state");
        presentation.Skip();Check(presentation.Display.version==2&&history==1,"native queue skip converges once");
        presentation.Speed=2;presentation.Accept(after,after,new System.Collections.Generic.Dictionary<int,Rect>());presentation.Skip();Check(!presentation.Busy,"developer speed/skip drains native queue");
        presentation.NativeDuration=id=>2.1f;presentation.NativeImpactDelay=id=>.35f;
        presentation.Accept(before,after,new System.Collections.Generic.Dictionary<int,Rect>());
        effect=presentation.Cues.First(c=>c.data.type=="SpellEffect");hit=presentation.Cues.First(c=>c.data.type=="Hit");
        Check(Mathf.Abs(hit.start-effect.start-.35f)<.0001f&&effect.duration==2.1f,"reviewed original finish time is separate from visual tail");
        presentation.Seek(effect.start+.34f);Check(presentation.Display.players[1].hero.health==30,"visible damage waits for original finish callback");
        presentation.Seek(effect.start+.36f);Check(presentation.Display.players[1].hero.health==29&&presentation.Busy,"visible damage resolves while original tail remains active");
        presentation.Seek(effect.start+2);Check(presentation.Busy,"presentation preserves the reviewed two-second tail");
        presentation.Skip();Check(!presentation.Busy&&history==2,"skip drains original finish/tail once without server wait");
        File.WriteAllText("../work/native-effect-checks.json","{\"passed\":"+passed+"}");Debug.Log("NATIVE EFFECT CHECKS PASSED: "+passed);
    }
    static void CommonFXSecondAlphaChecks(string folder,AssetRegistry assets)
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"40ac8d77701fdbccc4033483.native-effect.json")));
        var rgbKeywords=new System.Collections.Generic.HashSet<string>{"_COLORING_NONE","_DISSOLVE_SECONDALPHA","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_MASK","_TEXTURETYPE_RGBA"};
        var rgbaKeywords=new System.Collections.Generic.HashSet<string>{"_COLORING_NONE","_DISSOLVE_SECONDALPHA","_SECONDTEXTURETYPE_RGBA","_SECONDTEXTURE_MASK","_TEXTURETYPE_RGBA"};
        var addKeywords=new System.Collections.Generic.HashSet<string>{"_COLORING_NONE","_DISSOLVE_NONE","_SECONDTEXTURETYPE_RGBR","_SECONDTEXTURE_ADD","_TEXTURETYPE_RGBA"};
        int rgbCount=0,rgbaCount=0,addCount=0,otherCount=0;
        using(var instance=new NativeEffectInstance(program,assets,0,64,64))
        {
            for(int i=0;i<program.nodes.Length;i++)
            {
                var renderer=instance.Nodes[i].GetComponent<Renderer>();if(renderer==null)continue;
                for(int j=0;j<program.nodes[i].materials.Length;j++)
                {
                    var source=program.nodes[i].materials[j];if(source.shader!="FX/CommonFX")continue;
                    var keywords=new System.Collections.Generic.HashSet<string>(source.keywords??new string[0]);var material=renderer.sharedMaterials[j];
                    if(keywords.SetEquals(rgbKeywords))
                    {
                        rgbCount++;Check(material.GetFloat("_OriginalFamily")==44,"exact CommonFX RGBR second-alpha mask maps to dedicated family 44");
                        Check(Mathf.Abs(material.GetFloat("_DissolveSmoothness")-source.floats.First(v=>v.name=="_DissolveSmoothness").value)<.0001f,"CommonFX RGBR source dissolve property survives material creation");
                        Check(material.HasProperty("_DissolveFactor")&&material.HasProperty("_DissolveSmoothness")&&material.HasProperty("_MinAlpha")&&material.HasProperty("_IgnoreVertexAlpha")&&material.HasProperty("_ColorMul2"),"CommonFX RGBR dissolve shader exposes every original animated/property input");
                    }
                    else if(keywords.SetEquals(rgbaKeywords))
                    {
                        rgbaCount++;Check(material.GetFloat("_OriginalFamily")==45,"exact CommonFX RGBA second-alpha mask maps to dedicated family 45");
                        Check(Mathf.Abs(material.GetFloat("_DissolveSmoothness")-source.floats.First(v=>v.name=="_DissolveSmoothness").value)<.0001f,"CommonFX RGBA source dissolve property survives material creation");
                        Check(material.HasProperty("_DissolveFactor")&&material.HasProperty("_DissolveSmoothness")&&material.HasProperty("_MinAlpha")&&material.HasProperty("_IgnoreVertexAlpha")&&material.HasProperty("_ColorMul2"),"CommonFX RGBA dissolve shader exposes every original animated/property input");
                    }
                    else if(keywords.SetEquals(addKeywords))
                    {
                        addCount++;Check(material.GetFloat("_OriginalFamily")==46,"exact CommonFX RGBR additive variant maps to premultiplied family 46");
                        Check(material.HasProperty("_ColorAdd")&&material.HasProperty("_ColorMul")&&material.HasProperty("_ColorMul2")&&material.HasProperty("_PremultipliedRGBTexture"),"CommonFX additive shader exposes its original premultiply and color inputs");
                    }
                    else
                    {
                        otherCount++;Check(material.GetFloat("_OriginalFamily")!=44&&material.GetFloat("_OriginalFamily")!=45&&material.GetFloat("_OriginalFamily")!=46,"CommonFX variants require their exact keyword set");
                    }
                }
            }
            Check(rgbCount==2&&rgbaCount==2&&addCount==1&&otherCount>0,"H11 fixture exercises both dissolve channels, the additive crack, and unrelated CommonFX variants");
            Check(instance.Nodes.Any(n=>n.name=="Mesh_Initial_Crack")&&instance.Nodes.Any(n=>n.name=="Mesh_Cracked_Hole")&&instance.Nodes.Any(n=>n.name=="FX_Dust"),"H11 CommonFX source includes the initial crack, burrow hole, and dust renderers");
        }
    }
    static void ActorObjectChecks(string folder,AssetRegistry assets)
    {
        var actor=assets.Scene("MODEL_MINION");actor.name="Borrowed original minion Actor";actor.transform.localScale=Vector3.one;
        try
        {
            int role=-1;var context=new NativeSpellContext(5,4,3,4,7,lifecycle:"Death",actorObjects:which=>{role=which;return actor;});
            foreach(var pair in new[]{new{which=0,role=0},new{which=1,role=1},new{which=2,role=2},new{which=4,role=2},new{which=3,role=3},new{which=5,role=5}})
            {Check(context.ActorObject(pair.which)==actor&&role==pair.role,"original GetActor follows GetCard selector "+pair.which);}
            var fallback=new NativeSpellContext(5,4,3,actorObjects:which=>{role=which;return actor;});
            Check(fallback.ActorObject(3)==actor&&role==0&&fallback.ActorObject(5)==actor&&role==0,"missing optional power/spell actor falls back to source card");
            Check(new NativeSpellContext(5).ActorObject(1)==null,"missing target is not replaced by a source/root proxy");
            var source=new EntityView{entity=101,card_id="source"};var target=new EntityView{entity=202,card_id="target"};var hero=new EntityView{entity=303,card_id="hero"};
            var before=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,hand=new[]{source}},new PlayerView{seat=1,field=new[]{target}}}};
            var after=new StateView{players=new[]{new PlayerView{seat=0,hero=hero,hand=new EntityView[0]},new PlayerView{seat=1,field=new EntityView[0]}}};
            EntityView selected=null;
            var eventContext=NativeSpellContext.FromEvent(new SemanticEvent{source=101,target=202,card_id="source",owner=0},before,after,
                id=>id=="source"?5:id=="target"?4:3,entity=>{selected=entity;return actor;});
            Check(eventContext.ActorObject(0)==actor&&selected==source,"event bridge resolves source actor from before snapshot after card leaves hand");
            Check(eventContext.ActorObject(1)==actor&&selected==target,"event bridge preserves target identity after target leaves field");
            Check(eventContext.ActorObject(2)==actor&&selected==hero,"event bridge maps source hero without inventing an actor proxy");
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"81adc1ee5e4a815a745824b5.native-effect.json")));
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,context))
            {
                instance.Seek(0);Check(actor.transform.localScale==Vector3.zero,"original RLK Death GetActor -> named Actor -> SetScale hides actual supplied model");
                instance.Seek(.5f);Vector3 halfway=actor.transform.localScale;
                Check(Vector3.Distance(halfway,Vector3.one*NativeITween.Sample(0,1,.5f,30))<.00001f,"original RLK named Actor variable drives supplied model elastic scale");
                Check(instance.ActorObjectBindings>0&&instance.UnboundITweenActions==0,"source dynamic owner resolves before ScaleTo instead of node=-1 rejection");
                instance.Seek(1.01f);Check(actor.transform.localScale==Vector3.one,"original target scale reaches final pose");
                instance.Seek(.5f);Check(Vector3.Distance(actor.transform.localScale,halfway)<.00001f,"borrowed Actor source scale and variables rewind deterministically");
            }
            Check(actor!=null&&actor.transform.localScale==Vector3.one,"native effect disposal restores and does not destroy supplied actor");
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Death")))
            {instance.Seek(.5f);Check(instance.UnboundActorActions>0&&instance.UnboundITweenActions==1,"missing real Actor provider remains explicitly unbound");}
        }
        finally
        {
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(actor);
        }
    }
    static void ActorTemplateChecks(string folder,AssetRegistry assets)
    {
        foreach(string model in new[]{"MODEL_ACTOR_MINION","MODEL_ACTOR_HERO"})
        {
            var scene=assets.Scene(model);var actor=scene.GetComponentInChildren<NativeActorIdentity>(true);
            try
            {
                Check(actor!=null&&actor.RootObject!=null&&actor.RootObject.name=="RootObject","catalog Actor source root resolves "+model);
                Check(actor.MeshRenderer!=null&&actor.PortraitRenderer!=null,"catalog Actor main/portrait references resolve "+model);
                Check(actor.GetMeshRenderer(true)==actor.MeshRenderer,"normal Actor returns main renderer "+model);
                actor.PremiumType=2;Check(actor.GetMeshRenderer(true)==actor.PortraitRenderer&&actor.GetMeshRenderer(false)==actor.MeshRenderer,"premium Actor portrait branch follows original IL "+model);actor.PremiumType=0;
                Check(!String.IsNullOrEmpty(actor.SpellTable),"source Actor SpellTable GUID retained "+model);
                var presentation=actor.GetComponent<NativeActorPresentation>();Check(presentation!=null&&presentation.VisualRoot==actor.RootObject,"source Actor visibility boundary uses original RootObject "+model);
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"d089ebacb048f027de7a3178.native-effect.json")));
                var init=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellInitActorVariables");
                var scaleProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"81adc1ee5e4a815a745824b5.native-effect.json")));
                var scale=scaleProgram.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SetScale"&&a.Param("gameObject").variable=="Actor");
                var writes=new[]{"Actor","RootObject","RootObjectMesh"}.Select((name,i)=>
                {var copy=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(scale));copy.Param("gameObject").variable=name;foreach(string axis in new[]{"x","y","z"}){var p=copy.Param(axis);p.variable="";p.is_none=false;p.number=.4f+i*.1f;}return copy;}).ToArray();
                program.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Init",events=new NativeTransition[0],states=new[]{new NativeState{name="Init",actions=new[]{init}.Concat(writes).ToArray(),transitions=new NativeTransition[0]}}}};
                var original=actor.transform.localScale;var rootScale=actor.RootObject.transform.localScale;var meshScale=actor.MeshRenderer.transform.localScale;
                using(var instance=new NativeEffectInstance(program,assets,0,32,32))
                {
                    // Explicit hierarchy binding fixture; production Spell parenting is not restored.
                    instance.Root.transform.SetParent(actor.transform,true);instance.Seek(0);
                    Check(Vector3.Distance(actor.transform.localScale,Vector3.one*.4f)<.0001f,"source initializer writes Actor variable "+model);
                    Check(Vector3.Distance(actor.RootObject.transform.localScale,Vector3.one*.5f)<.0001f,"source initializer writes RootObject variable "+model);
                    Check(Vector3.Distance(actor.MeshRenderer.transform.localScale,Vector3.one*.6f)<.0001f,"source initializer writes RootObjectMesh variable "+model);
                    Check(instance.UnboundActorInitializationActions==0&&instance.ActorObjectBindings==3,"real parent Actor initializes all source outputs "+model);
                    instance.Seek(.2f);instance.Seek(0);Check(instance.ActorObjectBindings==3,"parent Actor initialization replays "+model);
                }
                Check(actor!=null&&actor.transform.localScale==original&&actor.RootObject.transform.localScale==rootScale&&actor.MeshRenderer.transform.localScale==meshScale,"effects restore all borrowed original Actor objects "+model);
                var hideProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"076d511f7b25ad78f327937e.native-effect.json")));
                var hide=hideProgram.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="ActorSetVisibilityRecursiveAction"&&a.Param("m_Visible").number==0);
                program.machines[0].states[0].actions=new[]{init,hide};bool originallyShown=presentation.Shown;
                using(var instance=new NativeEffectInstance(program,assets,0,32,32))
                {instance.Root.transform.SetParent(actor.transform,true);instance.Seek(0);Check(!presentation.Shown&&!actor.RootObject.activeSelf&&instance.UnrestoredActorVisibilitySideEffects>0,"source Actor visibility hides original root and reports remaining side effects "+model);}
                Check(presentation.Shown==originallyShown,"source Actor visibility restores original root on disposal "+model);
            }
            finally{foreach(var renderer in scene.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(scene);}
        }
        var root=new GameObject("RootObjectSelf");var child=new GameObject("rootobject-child");child.transform.SetParent(root.transform,false);
        try{Check(NativeActorIdentity.FindChildBySubstring(root,"RootObject")==root,"original substring search includes self before children");Check(NativeActorIdentity.FindChildBySubstring(child,"ROOTOBJECT")==child,"original substring search is ordinal ignore case");Check(NativeActorIdentity.FindInThisOrParents(child)==null,"display geometry alone is not original Actor identity");}
        finally{AssetRegistry.ReleaseTransient(root);}
    }
    static void MachineOwnerChecks(string folder,AssetRegistry assets)
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"076d511f7b25ad78f327937e.native-effect.json")));
        Check(program.machines[0].owner_known&&program.machines[0].owner==1,"source Paw FSM belongs to exact exported node one");
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(4,lifecycle:"Action")))
        {
            Check(instance.MachineOwner(0)==instance.Nodes[1],"runtime FSM owner is exact node, not effect root");
            Check(instance.MachineOwner(-1)==null&&instance.MachineOwner(1)==null,"out-of-range machine has no fallback owner");
            instance.Seek(.2f);Check(instance.UnboundMachineOwnerActions==0&&instance.UnboundActorInitializationActions>0,"known FSM owner does not imply parent Actor is restored");
        }
        foreach(int invalid in new[]{-1,program.nodes.Length})
        {
            program.machines[0].owner=invalid;
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(4,lifecycle:"Action")))
            {Check(instance.MachineOwner(0)==null,"invalid source owner rejects without root substitution");instance.Seek(0);Check(instance.UnboundMachineOwnerActions>0,"missing owner explicitly reported by initializer");}
        }
        program.machines[0].owner=0;program.machines[0].owner_known=false;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))Check(instance.MachineOwner(0)==null,"old cache default zero is not evidence of root ownership");
    }
    static void ScaleFromChecks(string folder,AssetRegistry assets)
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"076d511f7b25ad78f327937e.native-effect.json")));
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="iTweenScaleFrom");
        var originalProgram=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        using(var instance=new NativeEffectInstance(originalProgram,assets,0,32,32,context:new NativeSpellContext(4,lifecycle:"Action")))
        {instance.Seek(.5f);Check(instance.UnboundActorInitializationActions>0,"original spawn owner initializer remains explicit gap instead of guessing source Actor");instance.Seek(0);Check(instance.UnboundActorInitializationActions>0,"initialization diagnostics rebuild on rewind");}
        Check(action.Param("speed").none_known&&action.Param("speed").is_none,"source ScaleFrom speed is None, not literal zero");
        var getter=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"81adc1ee5e4a815a745824b5.native-effect.json"))).machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellGetActorAction"&&a.Param("m_GameObject").variable=="Actor");
        program.machines=new[]{new NativeMachine{start="From",events=new NativeTransition[0],states=new[]{new NativeState{name="From",actions=new[]{getter,action},transitions=new NativeTransition[0]}}}};
        var actor=assets.Scene("MODEL_MINION");var context=new NativeSpellContext(5,4,actorObjects:which=>actor);
        try
        {
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {
                foreach(float time in new[]{0f,.1f,.2f,.4f,.1f})
                {instance.Seek(time);float expected=NativeITween.Sample(.1f,1,Mathf.Clamp01(time/.4f),27);Check(Vector3.Distance(actor.transform.localScale,Vector3.one*expected)<.0001f,"source spawn ScaleFrom elastic curve and replay "+time);}
                Check(instance.UnboundITweenActions==0,"source ScaleFrom binds named actual Actor");
            }
            Check(actor.transform.localScale==Vector3.one,"ScaleFrom disposal restores pre-snap Actor scale");
            action.Param("delay").number=.3f;
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(.2f);Check(Vector3.Distance(actor.transform.localScale,Vector3.one*.1f)<.00001f,"ScaleFrom snaps before original delay");instance.Seek(.4f);Check(Vector3.Distance(actor.transform.localScale,Vector3.one*NativeITween.Sample(.1f,1,.25f,27))<.0001f,"ScaleFrom delay precedes elastic playback");}
            action.Param("delay").number=0;action.Param("speed").is_none=false;action.Param("speed").number=1.8f;
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(.3f);float duration=Vector3.Distance(Vector3.one*.1f,Vector3.one)/1.8f;Check(Vector3.Distance(actor.transform.localScale,Vector3.one*NativeITween.Sample(.1f,1,.3f/duration,27))<.0001f,"ScaleFrom speed duration uses snapped start and captured destination");}
            action.Param("loopType").number=1;
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(0);Check(instance.UnboundITweenActions==1&&actor.transform.localScale==Vector3.one,"unrestored loop rejects without mutating Actor");}
        }
        finally{foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(actor);}
    }
    static void ActorVisibilityChecks(string folder,AssetRegistry assets)
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"13e4ba2cda5d567ad3d3e0be.native-effect.json")));
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="ActorSetVisibilityRecursiveAction");
        program.machines=new[]{new NativeMachine{start="Hide",events=new NativeTransition[0],states=new[]{new NativeState{name="Hide",actions=new[]{action},transitions=new NativeTransition[0]}}}};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {instance.Seek(0);var renderers=instance.Nodes[3].GetComponentsInChildren<Renderer>(true);Check(renderers.Length>0&&renderers.All(r=>!r.enabled),"original Patchwerk literal hierarchy hides all renderers");Check(instance.UnboundActorVisibilityActions==0,"literal visibility action binds without Actor stand-in");}
        var actor=assets.Scene("MODEL_MINION");var view=actor.AddComponent<NativeActorPresentation>();view.Configure(actor.transform.GetChild(0).gameObject);
        var originalScales=actor.transform.localScale;
        try
        {
            var getProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"81adc1ee5e4a815a745824b5.native-effect.json")));
            var get=getProgram.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SpellGetActorAction"&&a.Param("m_GameObject").variable=="Actor");
            action.Param("m_GameObject").node=-1;action.Param("m_GameObject").variable="Actor";
            program.machines[0].states[0].actions=new[]{get,action};
            var context=new NativeSpellContext(5,4,actorObjects:which=>actor);
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(0);Check(!view.Shown&&!view.VisualRoot.activeSelf,"dynamic original Actor variable hides client visual root");Check(!NativeActorPresentation.IsActorShown(actor),"hidden source Actor also suppresses its GUI portrait and status layers");Check(instance.ActorRootVisibilityBindings==1&&instance.UnrestoredActorVisibilitySideEffects==1,"root-only binding reports missing original Actor side effects");instance.Seek(.3f);instance.Seek(0);Check(!view.Shown&&!NativeActorPresentation.IsActorShown(actor),"rewind to the source-hidden point restores the same state");}
            Check(view.Shown&&view.VisualRoot.activeSelf&&actor.transform.localScale==originalScales,"dispose restores borrowed root without destroying or rescaling Actor");
            action.Param("m_ResetOnExit").number=1;
            program.machines[0].states[0].transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}};
            program.machines[0].states=new[]{program.machines[0].states[0],new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}};
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(.1f);Check(view.Shown,"source resetOnExit restores Actor visibility at state exit");}
            action.Param("m_GameObject").variable="MissingActor";
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:context))
            {instance.Seek(0);Check(instance.UnboundActorVisibilityActions==1&&view.Shown,"missing dynamic owner is diagnosed without hiding another model");}
        }
        finally{foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(actor);}
    }
    static void LifecycleChecks(string folder,AssetRegistry assets)
    {
        var plan=JsonUtility.FromJson<NativeEffectEntry>("{\"card_id\":\"fixture\",\"initial_lifecycle\":\"None\",\"lifecycle\":[{\"machine\":1,\"state\":5,\"time\":0.4}]}");
        Check(plan.initial_lifecycle=="None"&&plan.lifecycle.Length==1&&plan.lifecycle[0].machine==1&&plan.lifecycle[0].state==5&&plan.lifecycle[0].time==.4f,"reviewed runtime index preserves explicit original lifecycle machine/state/time");
        var context=NativeSpellContext.FromEvent(new SemanticEvent{card_id="fixture"},null,null,id=>5,lifecycle:plan.initial_lifecycle);
        Check(context.Lifecycle=="None","event context forwards reviewed initial lifecycle without altering entity identity");
        foreach(var row in new[]{
            new{current=0,available=new[]{1,2,3,4,5,6},next=1},new{current=0,available=new[]{2,3,4,5},next=2},
            new{current=0,available=new[]{3,4,5},next=3},new{current=0,available=new[]{4,5},next=5},
            new{current=0,available=new[]{4,6},next=4},new{current=0,available=new[]{6},next=0},
            new{current=1,available=new[]{2,3,5},next=2},new{current=1,available=new[]{3,5},next=0},
            new{current=2,available=new[]{3,5},next=3},new{current=2,available=new[]{5},next=0},
            new{current=3,available=new[]{5},next=5},new{current=3,available=new[]{4},next=0},
            new{current=4,available=new[]{1,2,3,5},next=0},new{current=5,available=new[]{1,2,3,4},next=0},new{current=6,available=new[]{1,2,3,4,5},next=0}})
            Check((int)NativeSpellLifecycle.Next((NativeSpellStateType)row.current,t=>row.available.Contains((int)t))==row.next,"source Spell.GuessNextStateType branch "+row.current+" -> "+row.next);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"81adc1ee5e4a815a745824b5.native-effect.json")));
        Check(NativeSpellLifecycle.Destination(program.machines[1],NativeSpellStateType.Death).state=="State1","real root-independent Death map follows original global target");
        Check(NativeSpellLifecycle.Entry(program.machines[0]).state=="Birth State","original RLK first usable state is Birth");
        Check(NativeSpellLifecycle.Destination(program.machines[0],NativeSpellStateType.Reset)==null,"missing original lifecycle state remains missing");
        var generic=new NativeMachine{events=new[]{new NativeTransition{@event="Birth",state="Visual"}},states=new[]{new NativeState{name="Visual",actions=new NativeAction[0]}}};
        Check(NativeSpellLifecycle.Entry(generic)==null,"generic FSM without SpellStateAction is not an original Spell lifecycle");
        var actor=assets.Scene("MODEL_MINION");actor.transform.localScale=Vector3.one;
        try
        {
            using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"None",actorObjects:which=>which==1?actor:null)))
            {
                instance.SetLifecycleTimeline(new[]{new NativeLifecycleEvent(1,.4f,NativeSpellStateType.Death)});
                instance.Seek(.39f);Check(actor.transform.localScale==Vector3.one&&instance.LifecycleChanges==0,"explicit source lifecycle waits until requested timestamp");
                instance.Seek(.4f);Check(actor.transform.localScale==Vector3.zero&&instance.CurrentStates[1]=="State1","Death event reaches original target Actor at exact boundary");
                instance.Seek(.5f);var pose=actor.transform.localScale;
                Check(pose.x>1&&Vector3.Distance(pose,Vector3.one*NativeITween.Sample(0,1,.1f,30))<.001f,"late frame samples original elastic tween from lifecycle entry time");
                Check(instance.LifecycleChanges==1&&instance.UnboundLifecycleEvents==0,"one delivered lifecycle event is not confused with FINISHED");
                instance.Seek(.39f);Check(actor.transform.localScale==Vector3.one,"rewind restores actor and pending lifecycle cursor");
                instance.Seek(.5f);Check(Vector3.Distance(actor.transform.localScale,pose)<.0001f&&instance.LifecycleChanges==1,"lifecycle replay reconstructs source pose deterministically");
                instance.SetLifecycleTimeline(new[]{new NativeLifecycleEvent(1,.4f,NativeSpellStateType.None),new NativeLifecycleEvent(1,.4f,NativeSpellStateType.Death),new NativeLifecycleEvent(1,.6f,NativeSpellStateType.Reset)});
                instance.Seek(.4f);Check(actor.transform.localScale==Vector3.zero&&instance.LifecycleChanges==2,"same-time source events preserve supplied order");
                instance.Seek(.61f);Check(instance.UnboundLifecycleEvents==1,"unsupported RESET is diagnosed rather than substituted as Death");
                bool rejected=false;try{instance.SetLifecycleTimeline(new[]{new NativeLifecycleEvent(99,0,NativeSpellStateType.Death)});}catch(ArgumentException){rejected=true;}
                Check(rejected,"out-of-scope FSM lifecycle cannot target another instance");
                rejected=false;try{instance.SetLifecycleTimeline(new[]{new NativeLifecycleEvent(1,float.NaN,NativeSpellStateType.Death)});}catch(ArgumentException){rejected=true;}
                Check(rejected,"NaN source lifecycle timestamp rejected");
            }
            Check(actor!=null&&actor.transform.localScale==Vector3.one,"timeline disposal preserves borrowed model ownership");
        }
        finally
        {
            foreach(var material in actor.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(actor);
        }
    }
    static void ITweenChecks(string folder,AssetRegistry assets)
    {
        Check(NativeITween.Sample(2,6,.25f,21)==3,"original iTween linear enum 21");
        Check(Mathf.Abs(NativeITween.Sample(0,1,.1f,30)-1.25f)<.0001f,"original elastic overshoot is not clamped");
        Check(NativeITween.Sample(2,6,0,30)==2&&NativeITween.Sample(2,6,1,30)==6,"original elastic exact endpoints");
        Check(!NativeITween.SupportedEase(99),"unverified iTween enum remains unsupported");
        foreach(var sample in new[]{
            new{mode=0,quarter=.0625f,half=.25f,three=.5625f},
            new{mode=1,quarter=.4375f,half=.75f,three=.9375f},
            new{mode=2,quarter=.125f,half=.5f,three=.875f},
            new{mode=7,quarter=.68359375f,half=.9375f,three=.99609375f},
            new{mode=12,quarter=.076120466f,half=.2928932f,three=.6173166f},
            new{mode=13,quarter=.38268343f,half=.70710677f,three=.9238795f},
            new{mode=24,quarter=.47265625f,half=.765625f,three=.97265625f},
            new{mode=27,quarter=.8174097f,half=1.0876975f,three=1.0641366f}})
        {
            Check(Mathf.Abs(NativeITween.Sample(0,1,.25f,sample.mode)-sample.quarter)<.000001f,"source iTween quarter "+sample.mode);
            Check(Mathf.Abs(NativeITween.Sample(0,1,.5f,sample.mode)-sample.half)<.000001f,"source iTween midpoint "+sample.mode);
            Check(Mathf.Abs(NativeITween.Sample(0,1,.75f,sample.mode)-sample.three)<.000001f,"source iTween third quarter "+sample.mode);
            Check(Mathf.Abs(NativeITween.Sample(3,-2,.5f,sample.mode)-(3-5*sample.half))<.000001f,"source signed iTween delta "+sample.mode);
        }
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"1438c953f1f357de128e06d3.native-effect.json")));
        using(var instance=new NativeEffectInstance(program,assets,0,64,64,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(.25f);var material=instance.Nodes[3].GetComponent<Renderer>().sharedMaterial;
            Check(Mathf.Abs(material.GetColor("_Color").a-.5f)<.0001f,"original zero-alpha reticle fades at source duration");
            Check(Mathf.Abs(instance.Nodes[2].transform.localScale.x-NativeITween.Sample(.1f,1.4f,.25f/.8f,30))<.0001f,"source reticle elastic scale executes");
            Check(instance.UnboundITweenActions==0,"source reticle tween parameters are bound");
            instance.Seek(.81f);Check(instance.CompletedITweenActions==2&&Mathf.Abs(material.GetColor("_Color").a-1)<.0001f,"source fade and scale complete independently");
            instance.Seek(1.01f);Check(instance.CurrentStates[0]=="Idle","Wait finish event transitions despite looping original clip");
            instance.Seek(.25f);Check(Mathf.Abs(material.GetColor("_Color").a-.5f)<.0001f&&instance.CompletedITweenActions==0,"backward seek restores source alpha and tween callbacks");
        }
        var birth=program.machines[0].states.First(s=>s.name=="BirthState");
        var scale=birth.actions.First(a=>a.name=="iTweenScaleTo");var fade=birth.actions.First(a=>a.name=="iTweenFadeToAction");
        var wait=birth.actions.First(a=>a.name=="Wait");
        birth.actions=birth.actions.Where(a=>a.name!="PlayAnimation").ToArray();
        wait.Param("time").number=.2f;fade.Param("time").number=.6f;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(.21f);float stopped=instance.Nodes[2].transform.localScale.x;
            Check(instance.CurrentStates[0]=="Idle"&&instance.StoppedITweens==1,"scale stopOnExit stops owner/type at Wait boundary");
            instance.Seek(.5f);Check(instance.Nodes[2].transform.localScale.x==stopped,"stopped scale holds boundary pose");
            Check(Mathf.Abs(instance.Nodes[3].GetComponent<Renderer>().sharedMaterial.GetColor("_Color").a-.5f/.6f)<.0001f,"fade stopOnExit false survives callback unregistration");
            instance.Seek(.7f);Check(instance.CompletedITweenActions==0,"detached tween completion does not finish prior FSM action");
        }
        wait.Param("time").number=2;scale.Param("easeType").number=99;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {instance.Seek(.1f);Check(instance.UnboundITweenActions==1,"unknown source easing is audited instead of linear fallback");}
        birth.actions=new[]{fade,wait};fade.Param("delay").number=.1f;fade.Param("finishEvent").text="FINISHED";
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(.05f);var material=instance.Nodes[3].GetComponent<Renderer>().sharedMaterial;
            Check(material.GetColor("_Color").a==0,"source fade delay holds initial transparent material");
            instance.Seek(.4f);Check(Mathf.Abs(material.GetColor("_Color").a-.5f)<.0001f,"source fade clock begins after delay");
            instance.Seek(.71f);Check(instance.CurrentStates[0]=="Idle"&&instance.CompletedITweenActions==1,"source tween completion dispatches finishEvent before Wait");
        }
        birth.actions=new[]{scale,wait};scale.Param("easeType").number=21;scale.Param("vectorScale").vector=new Vector3(2.4f,1.4f,1.4f);
        scale.Param("speed").is_none=false;scale.Param("speed").number=2;scale.Param("time").number=.01f;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(.25f);Check(Mathf.Abs(instance.Nodes[2].transform.localScale.x-1.9f)<.00001f,"original speed takes precedence over time using scale distance");
            instance.Seek(.51f);Check(instance.CompletedITweenActions==1&&Mathf.Abs(instance.Nodes[2].transform.localScale.x-2.4f)<.00001f,"original speed-derived duration completes at distance/speed");
        }
        scale.Param("speed").is_none=true;scale.Param("time").is_none=true;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {instance.Seek(.5f);Check(Mathf.Abs(instance.Nodes[2].transform.localScale.x-1.9f)<.00001f,"source ScaleTo time None defaults to one second");}
        scale.Param("transformScale").is_none=false;scale.Param("transformScale").object_null=false;scale.Param("transformScale").node=3;
        scale.Param("vectorScale").vector=new Vector3(1,2,3);
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(0);Vector3 target=program.nodes[3].scale+new Vector3(1,2,3);instance.Nodes[3].transform.localScale=Vector3.zero;
            instance.Seek(.5f);Check(Vector3.Distance(instance.Nodes[2].transform.localScale,(program.nodes[2].scale+target)/2)<.00001f,"source transformScale adds vector and snapshots target at OnEnter");
        }
        birth.actions=new[]{fade,wait};wait.Param("time").number=.2f;
        fade.Param("gameObject").node=3;fade.Param("delay").number=0;fade.Param("finishEvent").text="";fade.Param("stopOnExit").number=1;
        program.nodes[4].parent=3;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
        {
            instance.Seek(.5f);float parent=instance.Nodes[3].GetComponent<Renderer>().sharedMaterial.GetColor("_Color").a;
            float child=instance.Nodes[4].GetComponent<Renderer>().sharedMaterial.GetColor("_Color").a;
            Check(instance.UnboundITweenActions==0&&instance.StoppedITweens==1,"source child fade supports parent-only stopOnExit");
            Check(Mathf.Abs(parent-.2f/.6f)<.00001f&&Mathf.Abs(child-.5f/.6f)<.00001f,"source parent stop does not stop descendant color clocks");
            instance.Seek(.7f);Check(instance.CompletedITweenActions==0,"child fade completion has no parent FSM callback");
            instance.Seek(.1f);Check(Mathf.Abs(instance.Nodes[4].GetComponent<Renderer>().sharedMaterial.GetColor("_Color").a-.1f/.6f)<.00001f,"independent child clocks reset on rewind");
        }
    }
    static void AnimationCompletionChecks(NativeProgram original,AssetRegistry assets)
    {
        var data=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));
        data.nodes[0].animation_refs=new[]{"watched","other"};
        data.animations=new[]{new NativeAnimation{id="watched",name="Watched",wrap_known=true,wrap_mode=8,duration=1,tracks=new[]{new NativeAnimationTrack{path="",property="position.x",keys=new[]{new NativeKey{time=0,value=0,outSlope=1},new NativeKey{time=1,value=1,inSlope=1}}}}},
            new NativeAnimation{id="other",name="Other",duration=1,tracks=new NativeAnimationTrack[0]}};
        var finish=new NativeParam{name="finishEvent",text="DONE"};
        var loop=new NativeParam{name="loopEvent",text=""};
        var play=new NativeAction{name="PlayAnimation",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="animName",text="Watched"},finish,loop}};
        Func<float,NativeAction> wait=t=>new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=t}}};
        Func<float,NativeAction> speed=s=>new NativeAction{name="SetAnimationSpeed",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="animName",text="Watched"},new NativeParam{name="speed",number=s}}};
        var watcher=new NativeMachine{start="Watch",events=new NativeTransition[0],states=new[]{
            new NativeState{name="Watch",actions=new[]{play},transitions=new[]{new NativeTransition{@event="DONE",state="Done"}}},
            new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}};
        var control=new NativeMachine{start="Before",events=new NativeTransition[0],states=new[]{
            new NativeState{name="Before",actions=new[]{wait(.25f)},transitions=new[]{new NativeTransition{@event="FINISHED",state="Pause"}}},
            new NativeState{name="Pause",actions=new[]{speed(0),wait(.5f)},transitions=new[]{new NativeTransition{@event="FINISHED",state="Resume"}}},
            new NativeState{name="Resume",actions=new[]{speed(2)},transitions=new NativeTransition[0]}}};
        data.machines=new[]{watcher,control};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.6f);Check(instance.CurrentStates[0]=="Watch"&&instance.CompletedAnimationActions==0,"paused animation action does not finish");
            instance.Seek(1.05f);Check(instance.CurrentStates[0]=="Watch","completion follows changed speed rather than old one-second deadline");
            instance.Seek(1.2f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==1,"custom finishEvent dispatched after actual clip time passes length");
            instance.Seek(.6f);Check(instance.CurrentStates[0]=="Watch"&&instance.CompletedAnimationActions==0,"rewind clears completion state and rebuilds pause");
        }
        control.states[1].actions=new[]{new NativeAction{name="StopAnimation",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="animName",text="Watched"}}}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.3f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==1,"explicit stop completes original PlayAnimation action before duration");}
        control.states[1].actions=new[]{new NativeAction{name="AnimationPlaythroughAction",@params=new[]{new NativeParam{name="m_GameObject",node=0},new NativeParam{name="m_AnimName",text="Other"}}}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.3f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==1,"replacement playback completes disabled watched name");}
        data.machines=new[]{watcher};finish.text="";loop.text="CYCLE";data.animations[0].wrap_mode=2;
        watcher.states[0].transitions=new[]{new NativeTransition{@event="CYCLE",state="Done"}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(1);Check(instance.CurrentStates[0]=="Watch","original loop event uses strict greater-than length");instance.Seek(1.05f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==0,"source loop event changes FSM without marking playback complete");}
        loop.text="";data.animations[0].wrap_mode=8;watcher.states[0].transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(1);Check(instance.CurrentStates[0]=="Watch","ClampForever action remains active at exact length");instance.Seek(1.05f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==1,"finished actions still dispatch aggregate FINISHED when no custom event supplied");}
        data.animations[0].wrap_mode=2;finish.text="DONE";loop.text="IGNORED_CYCLE";
        watcher.states[0].transitions=new[]{new NativeTransition{@event="DONE",state="Done"}};
        control.states[0].actions=new[]{wait(1.25f)};control.states[1].actions=new[]{new NativeAction{name="StopAnimation",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="animName",text="Watched"}}}};
        data.machines=new[]{watcher,control};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(1.35f);Check(instance.CurrentStates[0]=="Done"&&instance.CompletedAnimationActions==1,"disabled looping state cannot overwrite stop completion with stale loop time");}
    }
    static void LegacyWrapChecks(NativeProgram original,AssetRegistry assets)
    {
        foreach(int clip in new[]{0,1,2,4,8})foreach(int component in new[]{0,1,2})
            Check(NativeLegacyWrap.Resolve(clip,component)==(clip!=0?clip:component!=0?component:1),"native oracle wrap inheritance "+clip+"/"+component);
        Check(NativeLegacyWrap.Resolve(2,2,1)==1&&NativeLegacyWrap.Resolve(1,1,2)==2,"explicit state wrap wins over Clip and component");
        Check(Mathf.Abs(NativeLegacyWrap.Sample(.3f,.2f,4)-.1f)<.0001f&&Mathf.Abs(NativeLegacyWrap.Sample(-.1f,.2f,4)-.1f)<.0001f,"PingPong reverses pose without reversing clock");
        Check(Mathf.Abs(NativeLegacyWrap.Sample(-.1f,.2f,2)-.1f)<.0001f,"Loop preserves negative clock wrapping");
        bool rejected=false;try{NativeLegacyWrap.Resolve(99,0);}catch(ArgumentException){rejected=true;}
        Check(rejected,"unknown source wrap is not silently coerced");
        var data=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));
        data.nodes[0].animation_refs=new[]{"wrap"};data.nodes[0].legacy_animation=new NativeLegacyAnimation{wrap_known=true,wrap_mode=0};
        var animation=new NativeAnimation{id="wrap",name="Wrap",duration=.2f,wrap_known=true,wrap_mode=4,
            tracks=new[]{new NativeAnimationTrack{path="",property="position.x",keys=new[]{new NativeKey{time=0,value=0,outSlope=50},new NativeKey{time=.2f,value=10,inSlope=50}}}}};
        data.animations=new[]{animation};
        data.machines=new[]{new NativeMachine{start="Start",events=new NativeTransition[0],states=new[]{new NativeState{name="Start",transitions=new NativeTransition[0],actions=new[]{
            new NativeAction{name="PlayAnimation",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="animName",text="Wrap"}}}}}}}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            foreach(float time in new[]{.15f,.3f,.55f,.3f})
            {instance.Seek(time);Check(Mathf.Abs(instance.Nodes[0].transform.localPosition.x-50*Mathf.PingPong(time,.2f))<.002f,"production PingPong sampling and backward replay "+time);}
            Check(instance.UnboundAnimationActions==0,"known PingPong binds successfully");
        }
        animation.wrap_mode=0;data.nodes[0].legacy_animation.wrap_mode=2;
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.3f);Check(Mathf.Abs(instance.Nodes[0].transform.localPosition.x-5)<.002f,"production Default inherits component Loop");}
        data.nodes[0].legacy_animation=null;
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.3f);Check(instance.UnboundAnimationActions==1,"known Default with missing component is unresolved");}
        animation.wrap_mode=1;animation.tracks[0].property="scale.x";
        var play=data.machines[0].states[0].actions[0];play.name="AnimationPlaythroughAction";
        play.@params=new[]{new NativeParam{name="m_GameObject",node=0},new NativeParam{name="m_AnimName",text="Wrap"}};
        data.machines[0].states[0].actions=new[]{play,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.3f}}}};
        data.machines[0].states[0].transitions=new[]{new NativeTransition{@event="FINISHED",state="After"}};
        data.machines[0].states=data.machines[0].states.Concat(new[]{new NativeState{name="After",transitions=new NativeTransition[0],actions=new[]{
            new NativeAction{name="SetScale",@params=new[]{new NativeParam{name="gameObject",node=0},new NativeParam{name="vector",none_known=true,is_none=true},new NativeParam{name="x",none_known=true,number=5},new NativeParam{name="y",none_known=true,is_none=true},new NativeParam{name="z",none_known=true,is_none=true}}}}}}).ToArray();
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.5f);Check(instance.CurrentStates[0]=="After"&&Mathf.Abs(instance.Nodes[0].transform.localScale.x-5)<.001f,"Once auto-disables even for immediately finished Playthrough action");}
    }
    static void LegacyClockChecks(NativeProgram original,AssetRegistry assets)
    {
        var data=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));int node=0;
        data.nodes[node].animation_refs=new[]{"clock-a","clock-b"};
        data.animations=new[]{new NativeAnimation{id="clock-a",name="A",duration=10,tracks=new[]{new NativeAnimationTrack{path="",property="position.x",keys=new[]{new NativeKey{time=0,value=0,outSlope=1},new NativeKey{time=10,value=10,inSlope=1}}}}},
            new NativeAnimation{id="clock-b",name="B",duration=10,tracks=new[]{new NativeAnimationTrack{path="",property="position.x",keys=new[]{new NativeKey{time=0,value=0,outSlope=1},new NativeKey{time=10,value=10,inSlope=1}}}}}};
        Func<string,float,NativeAction> speed=(name,value)=>new NativeAction{name="SetAnimationSpeed",@params=new[]{new NativeParam{name="gameObject",node=node},new NativeParam{name="animName",text=name},new NativeParam{name="speed",number=value}}};
        Func<string,NativeAction> play=name=>new NativeAction{name="AnimationPlaythroughAction",@params=new[]{new NativeParam{name="m_GameObject",node=node},new NativeParam{name="m_AnimName",text=name}}};
        Func<float,NativeAction> wait=time=>new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=time}}};
        data.machines=new[]{new NativeMachine{start="Start",events=new NativeTransition[0],floats=new NativeFloat[0],colors=new NativeColor[0],states=new[]{
            new NativeState{name="Start",actions=new[]{speed("A",2),play("B"),wait(.5f)},transitions=new[]{new NativeTransition{@event="FINISHED",state="Pause"}}},
            new NativeState{name="Pause",actions=new[]{speed("B",0),wait(.5f)},transitions=new[]{new NativeTransition{@event="FINISHED",state="Reverse"}}},
            new NativeState{name="Reverse",actions=new[]{speed("B",-1)},transitions=new NativeTransition[0]}}}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.25f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.25f)<.002f,"named speed does not leak to another clip on the same node");
            instance.Seek(.75f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.5f)<.002f,"zero animation speed pauses at the exact change time");
            instance.Seek(1.25f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.25f)<.002f,"negative animation speed reverses without timeline jump");
            instance.Seek(.75f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.5f)<.002f,"backward seek deterministically rebuilds named clock changes");
        }
        data.machines[0].states[0].actions=new[]{speed("B",.5f),play("B"),wait(.5f)};
        var dynamic=speed("B",0);dynamic.@params[2].variable="clock-speed";
        dynamic.@params=dynamic.@params.Concat(new[]{new NativeParam{name="everyFrame",number=1}}).ToArray();
        data.machines[0].floats=new[]{new NativeFloat{name="clock-speed",value=.5f}};
        data.machines[0].states[1].actions=new[]{dynamic};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.8f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.4f)<.002f,"every-frame named speed resolves FSM variable without restarting clip");
            Check(instance.CurrentStates[0]=="Pause","every-frame speed action stays active and does not Finish");
        }
        Func<string,NativeAction> stop=name=>new NativeAction{name="StopAnimation",@params=new[]{new NativeParam{name="gameObject",node=node},new NativeParam{name="animName",text=name}}};
        data.machines[0].states[0].actions=new[]{play("B"),wait(.513f)};
        data.machines[0].states[1].actions=new[]{stop("A"),wait(.5f)};
        data.machines[0].states[2].actions=new[]{stop("B")};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.75f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.75f)<.002f,"stopping another clip does not interrupt the active name");
            instance.Seek(1.3f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-1.013f)<.0001f,"named stop samples exact transition time and holds pose");
            instance.Seek(.75f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.75f)<.002f,"backward replay restores stopped animation");
            Check(instance.UnboundAnimationActions==0,"bound named stop has no unsupported target");
        }
        data.machines[0].states[1].actions=new[]{stop(""),wait(.5f)};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.8f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.513f)<.0001f,"empty stop name stops all legacy clips on this node");}
        data.nodes[node].position=new Vector3(7,0,0);
        data.animations[0].tracks[0].keys=new[]{new NativeKey{time=0,value=0},new NativeKey{time=10,value=0}};
        data.animations[1].tracks[0].keys=new[]{new NativeKey{time=0,value=10},new NativeKey{time=10,value=10}};
        int materialNode=Array.FindIndex(data.nodes,n=>n.materials!=null&&n.materials.Length>0&&n.materials[0].properties!=null&&n.materials[0].properties.Contains("_Intensity")&&n.materials[0].properties.Contains("_Color"));
        Check(materialNode>=0,"real source material exposes test float and RGBA channels");
        node=materialNode;data.nodes[node].position=new Vector3(7,0,0);data.nodes[node].animation_refs=new[]{"clock-a","clock-b"};
        for(int index=0;index<2;index++)data.animations[index].tracks=data.animations[index].tracks.Concat(new[]{
            new NativeAnimationTrack{path="",property="_Intensity",keys=new[]{new NativeKey{time=0,value=index==0?2:6},new NativeKey{time=10,value=index==0?2:6}}},
            new NativeAnimationTrack{path="",property="_Color.r",keys=new[]{new NativeKey{time=0,value=index==0?.1f:.9f},new NativeKey{time=10,value=index==0?.1f:.9f}}}}).ToArray();
        var blend=play("B");blend.@params=blend.@params.Concat(new[]{new NativeParam{name="m_CrossFadeSec",number=.3f}}).ToArray();
        data.machines[0].states[0].actions=new[]{play("A"),wait(.51f)};
        data.machines[0].states[1].actions=new[]{blend,wait(1)};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.66f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-5)<.01f,"production native mixer blends retained old and new clocks at half fade");
            var material=instance.Nodes[materialNode].GetComponent<Renderer>().sharedMaterial;
            Check(Mathf.Abs(material.GetFloat("_Intensity")-4)<.001f&&Mathf.Abs(material.GetColor("_Color").r-.5f)<.001f,"production material float and RGBA share normalized original weights: "+material.GetFloat("_Intensity")+" / "+material.GetColor("_Color").r+" path="+data.nodes[materialNode].path);
            instance.Seek(.9f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-10)<.01f,"production mixer retires fully faded old clock");
            instance.Seek(.66f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-5)<.01f,"backward replay rebuilds original fade weights");
        }
        data.machines[0].states[0].actions=new[]{wait(.51f)};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.6f);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-10)<.01f,"production single clip normalizes positive partial fade weight rather than lerping rest");
            Check(Mathf.Abs(instance.Nodes[materialNode].GetComponent<Renderer>().sharedMaterial.GetFloat("_Intensity")-6)<.001f,"single material clip normalizes partial positive weight");}
        data.animations[1].tracks[0].keys=new[]{new NativeKey{time=0,value=0,outSlope=1},new NativeKey{time=10,value=10,inSlope=1}};
        data.machines[0].states[0].actions=new[]{play("A"),wait(.51f)};
        data.machines[0].states[1].actions=new[]{blend,wait(.3f)};
        data.machines[0].states[2].actions=new[]{blend};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(1);Check(Mathf.Abs(instance.Nodes[node].transform.localPosition.x-.49f)<.001f,"repeated CrossFade preserves already-playing named clip time");}
        var nativeObject=new GameObject("OriginalStopOracle");var native=nativeObject.AddComponent<Animation>();
        var nativeClip=new AnimationClip{legacy=true};nativeClip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,10,10));native.AddClip(nativeClip,"B");
        try
        {
            native.Play("B");native["B"].time=.513f;native.Sample();native.Stop("A");
            Check(native.IsPlaying("B"),"Unity Stop of unrelated name preserves B");
            native.Stop("B");native.Sample();
            Check(!native["B"].enabled&&native["B"].time==0,"Unity named Stop disables and resets legacy state time");
            Check(!native.IsPlaying("B")&&Mathf.Abs(nativeObject.transform.localPosition.x-.513f)<.0001f,"Unity legacy Stop preserves sampled pose without another animation");
        }
        finally{AssetRegistry.ReleaseTransient(nativeClip);AssetRegistry.ReleaseTransient(nativeObject);}
        var moon=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"46d81302ae2bb97b610d8d97.native-effect.json")));
        var stops=moon.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="StopAnimation").ToArray();
        Check(stops.Length==3&&stops.All(a=>a.Param("gameObject").node==1),"actual Moonfire owner-default stops bind to original animation node");
        using(var instance=new NativeEffectInstance(moon,assets,0,32,32,null,new NativeSpellContext(5,4,lifecycle:"Death")))
        {instance.Seek(.6f);Check(instance.UnboundAnimationActions==0&&instance.TransitionLimitHits==0,"actual Moonfire Death executes named stop without unknown owner or runaway FSM");}
        ExitAnimationChecks(original,assets);
    }
    static void ExitAnimationChecks(NativeProgram original,AssetRegistry assets)
    {
        var data=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));int node=0;
        data.nodes[node].animation_refs=new[]{"exit-clip"};
        data.animations=new[]{new NativeAnimation{id="exit-clip",name="Exit",duration=.5f,tracks=new[]{
            new NativeAnimationTrack{path="",property="scale.x",keys=new[]{new NativeKey{time=0,value=1,outSlope=2},new NativeKey{time=.5f,value=2,inSlope=2}}}}}};
        var stop=new NativeParam{name="stopOnExit",number=1};
        var play=new NativeAction{name="PlayAnimation",@params=new[]{new NativeParam{name="gameObject",node=node},new NativeParam{name="animName",text="Exit"},stop}};
        var scale=new NativeAction{name="SetScale",@params=new[]{new NativeParam{name="gameObject",node=node},new NativeParam{name="vector",none_known=true,is_none=true},
            new NativeParam{name="x",none_known=true,number=5},new NativeParam{name="y",none_known=true,is_none=true},new NativeParam{name="z",none_known=true,is_none=true}}};
        data.machines=new[]{new NativeMachine{start="Play",events=new NativeTransition[0],states=new[]{
            new NativeState{name="Play",actions=new[]{play},transitions=new[]{new NativeTransition{@event="FINISHED",state="After"}}},
            new NativeState{name="After",actions=new[]{scale},transitions=new NativeTransition[0]}}}};
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {
            instance.Seek(.4f);Check(instance.CurrentStates[0]=="Play"&&instance.StoppedOnExitClips==0,"StopOnExit does not stop active state prematurely");
            instance.Seek(.7f);Check(instance.CurrentStates[0]=="After"&&instance.StoppedOnExitClips==1&&Mathf.Abs(instance.Nodes[node].transform.localScale.x-5)<.001f,"original OnExit stop protects new state from old clip overwrite");
            instance.Seek(.3f);Check(instance.StoppedOnExitClips==0&&Mathf.Abs(instance.Nodes[node].transform.localScale.x-1.6f)<.001f,"backwards replay restores exited clip and clears exit counter");
        }
        stop.number=0;
        using(var instance=new NativeEffectInstance(data,assets,0,32,32))
        {instance.Seek(.7f);Check(instance.StoppedOnExitClips==0&&Mathf.Abs(instance.Nodes[node].transform.localScale.x-2)<.001f,"false StopOnExit retains original clip after state exit");}
        foreach(string file in new[]{"1ca86742da8eb534a697ead5","2dc0f32c42c2aec252d77e39","eb0bf826aae72bb9b8a0a26f"})
        {
            var arrow=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
            using(var instance=new NativeEffectInstance(arrow,assets,0,32,32,null,new NativeSpellContext(5,4,lifecycle:"Death")))
            {instance.Seek(.8f);Check(instance.CompletedAnimationActions==1&&instance.StoppedOnExitClips==0&&instance.UnboundAnimationActions==0&&instance.TransitionLimitHits==0,"actual hunter arrow Once completes before OnExit stop, matching source mode "+file);}
        }
    }
    static void EaseChecks(string folder,AssetRegistry assets)
    {
        int count=0;
        foreach(string path in Directory.GetFiles(folder,"*.native-effect.json"))
        foreach(var machine in JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path)).machines)
        foreach(var state in machine.states)foreach(var action in state.actions.Where(a=>a.name=="EaseColor"||a.name=="EaseFloat"))
        {var animation=new NativeEaseAnimation(action,action.Param("fromValue").color,action.Param("toValue").color,p=>p.number);Check(!animation.Finished,"original easing parameters bind "+Path.GetFileName(path));count++;}
        Check(count==122,"all current 122 original easing actions examined");
        int[] types={0,1,2,3,4,19,21};float[] samples={.0625f,.4375f,.125f,.015625f,.578125f,.6614378f,.25f};
        for(int i=0;i<types.Length;i++)Check(Mathf.Abs(NativeEaseAnimation.Evaluate(types[i],0,1,.25f)-samples[i])<.000001f,"original quarter-time nonlinear easing "+types[i]);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"22291b74bdc6da4185f8a0d8.native-effect.json")));
        var machineColor=program.machines[1];var colorState=machineColor.states.First(s=>s.name=="Action 3");var colorAction=colorState.actions.First(a=>a.name=="EaseColor");
        var materialAction=colorState.actions.First(a=>a.name=="SetMaterialColor");int node=materialAction.Param("gameObject").node;
        machineColor.start=colorState.name;machineColor.events=new NativeTransition[0];colorState.transitions=new NativeTransition[0];program.machines=new[]{machineColor};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {
            instance.Seek(1);Color initial=instance.Nodes[node].GetComponent<Renderer>().sharedMaterial.GetColor("_Color");
            Check(initial==colorAction.Param("fromValue").color,"actual Frost Nova holds initial color throughout two-second delay");
            instance.Seek(2.5f);Color middle=instance.Nodes[node].GetComponent<Renderer>().sharedMaterial.GetColor("_Color");
            Color expected=Color.LerpUnclamped(colorAction.Param("fromValue").color,colorAction.Param("toValue").color,.875f);
            Check(Mathf.Abs(middle.r-expected.r)<.02f&&Mathf.Abs(middle.a-expected.a)<.02f,"actual Frost Nova cubic fade reaches original material");
            instance.Seek(3.2f);Check(instance.Nodes[node].GetComponent<Renderer>().sharedMaterial.GetColor("_Color")==colorAction.Param("toValue").color,"actual Frost Nova writes final endpoint");
            instance.Seek(1);Check(instance.Nodes[node].GetComponent<Renderer>().sharedMaterial.GetColor("_Color")==initial&&instance.UnboundEaseActions==0,"actual Frost Nova easing replays after backward seek");
        }
        colorState.transitions=new[]{new NativeTransition{@event="FINISHED",state="EaseDone"}};
        machineColor.states=new[]{colorState,new NativeState{name="EaseDone",actions=new NativeAction[0],transitions=new NativeTransition[0]}};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {instance.Seek(2.8f);Check(instance.CurrentStates[0]==colorState.name,"source finishEvent cannot skip original easing delay");instance.Seek(3.3f);Check(instance.CurrentStates[0]=="EaseDone"&&instance.TransitionLimitHits==0,"source finishEvent transitions once after final write");instance.Seek(1);Check(instance.CurrentStates[0]==colorState.name,"source finishEvent timeline rebuilds on backward seek");}
        var shadow=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"a78b10af18608c6a921c39e6.native-effect.json")));
        var machineFloat=shadow.machines[0];var floatState=machineFloat.states.First(s=>s.name=="ActionState4");machineFloat.start=floatState.name;machineFloat.events=new NativeTransition[0];floatState.transitions=new NativeTransition[0];shadow.machines=new[]{machineFloat};
        using(var instance=new NativeEffectInstance(shadow,assets,0,32,32))
        {instance.Seek(.5f);Check(Mathf.Abs(instance.Nodes[6].GetComponent<Renderer>().sharedMaterial.GetFloat("_AlphaIntensity")-(2-2*Mathf.Sqrt(.75f)))<.03f,"actual shadow circular EaseFloat reaches particle material");Check(instance.UnboundEaseActions==0,"actual shadow EaseFloat and EaseColor bound");}
        var fixture=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(colorAction));fixture.Param("delay").number=-1;fixture.Param("time").number=1;fixture.Param("reverse").number=1;
        var reverse=new NativeEaseAnimation(fixture,Color.black,Color.white,p=>p.number);Check(reverse.Value==Color.white,"reverse OnEnter writes destination");reverse.Step(.25f);
        Check(Mathf.Abs(reverse.Value.r-.984375f)<.000001f,"reverse uses original percentage before cubic easing");reverse.Step(.75f);Check(!reverse.Finished&&reverse.Value==Color.black,"final write precedes Finish");reverse.Step(.01f);Check(reverse.Finished,"Finish delivered on next update");
        fixture.Param("easeType").number=22;bool rejected=false;try{new NativeEaseAnimation(fixture,Color.black,Color.white,p=>p.number);}catch(ArgumentException){rejected=true;}Check(rejected,"unrestored easing rejected without linear substitution");
    }
    static void ColorChecks(string folder,AssetRegistry assets)
    {
        NativeCurveEncodingChecks.Run();
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"05928182ba88d64927d6d9d6.native-effect.json")));
        var machine=program.machines.First(m=>m.states.Any(s=>s.actions.Any(a=>a.name=="AnimateColor")));
        var state=machine.states.First(s=>s.name=="BirthAnimation");var action=state.actions.First(a=>a.name=="AnimateColor");
        var set=state.actions.First(a=>a.name=="SetMaterialColor"&&a.Param("color").variable=="RaysGlow");int node=set.Param("gameObject").node;
        machine.start=state.name;machine.events=new NativeTransition[0];program.machines=new[]{machine};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {
            foreach(float time in new[]{.3f,.6f,.3f})
            {instance.Seek(time);var color=instance.Nodes[node].GetComponent<Renderer>().sharedMaterial.GetColor("_Color");Check(Mathf.Abs(color.g-action.Param("curveG").SampleCurve(time))<.001f&&Mathf.Abs(color.a-action.Param("curveA").SampleCurve(time))<.001f,"actual original blessing RGBA curve reaches material and replays "+time);}
            Check(instance.UnboundColorActions==0,"original blessing color action bound");
        }
        var clone=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(action));var initial=new Color(.8f,.6f,.4f,.2f);
        clone.Param("calculationR").number=0;clone.Param("calculationG").number=3;
        var animation=new NativeColorAnimation(clone,initial,p=>p.number);
        animation.Step(.25f);Check(Mathf.Abs(animation.Value.r-initial.r)<.0001f&&Mathf.Abs(animation.Value.g-(initial.g-clone.Param("curveG").SampleCurve(.25f)))<.0001f,"original None and subtract calculation preserve captured start color");
        for(int frame=0;frame<200;frame++)animation.Step(1f/120);
        Check(animation.Finished,"clamped original curve finishes after final write");
        clone.Param("curveG").post_wrap=1;var looping=new NativeColorAnimation(clone,initial,p=>p.number);
        for(int frame=0;frame<300;frame++)looping.Step(1f/120);
        Check(!looping.Finished,"original looping infinity keeps action alive");
        clone.Param("curveG").post_wrap=2;clone.Param("time").is_none=false;clone.Param("time").number=2;
        var stretched=new NativeColorAnimation(clone,initial,p=>p.number);stretched.Step(.5f);
        Check(Mathf.Abs(stretched.Value.g-(initial.g-clone.Param("curveG").SampleCurve(.5f/2*clone.Param("curveG").CurveDuration)))<.0001f,"explicit time stretches each original channel independently");
    }
    static void ScaleChecks(string folder,AssetRegistry assets)
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"07ebfc013884b374cb15adea.native-effect.json")));
        Check(program.reference.StartsWith("Nature_Targeted_Damage_FX.prefab:"),"original Nature targeted damage identity");
        var machine=program.machines.First(m=>m.states.Any(s=>s.actions.Any(a=>a.name=="SetScale")));
        var state=machine.states.First(s=>s.name=="DeathState");var scale=state.actions.First(a=>a.name=="SetScale");
        var curve=state.actions.First(a=>a.name=="AnimateFloat"&&a.Param("floatVariable").variable=="ThornsScale").Param("animCurve");
        int node=scale.Param("gameObject").node;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,4,lifecycle:"Death")))
        {
            foreach(float time in new[]{0f,.3f,.75f,1.2f,.3f})
            {instance.Seek(time);Check(Vector3.Distance(instance.Nodes[node].transform.localScale,Vector3.one*curve.SampleCurve(time))<.001f,"original thorn scale follows source curve and backward replay "+time);}
            Check(instance.UnboundScaleActions==0,"source thorn SetScale is bound");
        }
        var test=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        var action=JsonUtility.FromJson<NativeAction>(JsonUtility.ToJson(scale));
        foreach(string name in new[]{"x","y","z"}){var p=action.Param(name);p.variable="";p.is_none=true;}
        action.Param("x").is_none=false;action.Param("x").number=0;
        action.Param("everyFrame").number=0;test.nodes[node].scale=new Vector3(2,3,4);
        test.machines=new[]{new NativeMachine{start="Set",floats=new NativeFloat[0],colors=new NativeColor[0],states=new[]{new NativeState{name="Set",actions=new[]{action},transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}}},new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}},events=new NativeTransition[0]}};
        using(var instance=new NativeEffectInstance(test,assets,0,32,32))
        {instance.Seek(.1f);Check(instance.Nodes[node].transform.localScale==new Vector3(0,3,4),"literal zero axis writes while None axes retain existing scale");Check(instance.CurrentStates.Single()=="Done","one-shot scale finishes without delaying server");}
        action.Param("everyFrame").number=1;
        using(var instance=new NativeEffectInstance(test,assets,0,32,32))
        {instance.Seek(.1f);Check(instance.CurrentStates.Single()=="Set","continuous scale keeps FINISHED pending");}
        action.Param("x").variable="Missing";
        using(var instance=new NativeEffectInstance(test,assets,0,32,32))
        {instance.Seek(0);Check(instance.UnboundScaleActions==1&&instance.Nodes[node].transform.localScale==new Vector3(2,3,4),"missing variable never zeros original scale");}
    }
    static void AnimatorChecks(NativeEffectEntry[] entries,string folder,AssetRegistry assets)
    {
        var entry=entries.First(e=>e.card_id=="BT_035");
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,entry.file)));
        Check(program.animations.Length==3&&program.animations.All(c=>c.duration>0&&c.tracks.Length==12),"original Mecanim clips have real duration and transform/material channels");
        int owner=Array.FindIndex(program.nodes,n=>n.name=="MinionRoot");
        var binding=program.nodes[owner].animator_states.First(s=>s.name=="MinionBoof");
        var clip=program.animations.First(c=>c.id==binding.clip);
        Check(Mathf.Abs(clip.duration-1.5f)<.0001f&&program.nodes[owner].animator_states.Length==3,"original controller resolves Minion/Weapon/Bacon clips");
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(4,3,3,lifecycle:"Death")))
        {
            instance.Seek(.01f);
            Check(instance.TextureBindings==3&&instance.UnboundTextureActions==0,"original Hero branch binds three literal texture references");
            foreach(var action in program.machines.SelectMany(m=>m.states).Where(s=>s.name=="Hero").SelectMany(s=>s.actions).Where(a=>a.name=="SetMaterialTexture"))
            {
                int node=action.Param("gameObject").node;var parameter=action.Param("texture");
                var material=instance.Nodes[node].GetComponent<Renderer>().sharedMaterial;
                Check(material.GetTexture("_MainTex")==assets.TextureFile(parameter.asset_path),"Hero texture uses original CAB/PathID export");
                var scale=material.GetTextureScale("_MainTex");var offset=material.GetTextureOffset("_MainTex");
                material.SetTexture("_MainTex",null);instance.Seek(0);
                Check(material.GetTexture("_MainTex")==assets.TextureFile(parameter.asset_path)&&material.GetTextureScale("_MainTex")==scale&&material.GetTextureOffset("_MainTex")==offset,"backward seek rebinds source texture without resetting UV ST");
                instance.Seek(.01f);
            }
        }
        var track=clip.tracks.First(t=>t.path=="mesh_DarkBacker"&&t.property=="scale.x");
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(4,4,3,lifecycle:"Death")))
        {
            var target=instance.Nodes[owner].transform.Find(track.path);
            instance.Seek(.3f);Check(Mathf.Abs(target.localScale.x-track.Sample(.3f))<.001f,"actual source FSM starts original Animator track");
            var alpha=clip.tracks.First(t=>t.path==track.path&&t.property=="material._AlphaMul");
            var material=target.GetComponent<Renderer>().sharedMaterial;
            Check(Mathf.Abs(material.GetFloat("_AlphaMul")-alpha.Sample(.3f))<.001f,"original animated AlphaMul reaches restored CommonFX program");
            Check(material.GetFloat("_OriginalFamily")==5&&material.GetInt("_SrcBlend")==1&&material.GetInt("_DstBlend")==10,"exact CommonFX keyword variant and named blend state");
            instance.Seek(.8f);Check(Mathf.Abs(target.localScale.x-track.Sample(.8f))<.001f,"original scale animation advances with client timeline");
            instance.Seek(.3f);Check(Mathf.Abs(target.localScale.x-track.Sample(.3f))<.001f,"backward seek restarts exact controller state");
        }
    }
    static void CardTypeChecks(NativeProgram original,AssetRegistry assets)
    {
        var context=new NativeSpellContext(5,4,3,7,10);
        foreach(var pair in new[]{new[]{0,5},new[]{1,4},new[]{2,3},new[]{3,7},new[]{4,3},new[]{5,10}})
            Check(context.CardType(pair[0])==pair[1],"original SpellAction Which "+pair[0]);
        var fallback=new NativeSpellContext(5);
        Check(fallback.CardType(1)==0&&fallback.CardType(2)==0&&fallback.CardType(4)==0&&fallback.CardType(3)==5&&fallback.CardType(5)==5&&new NativeSpellContext(5,actorType:0).CardType(5)==0,"missing target/hero stay missing; absent actor falls back, present actor with no card stays missing");
        var before=new StateView{players=new[]{new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="hero"},hand=new[]{new EntityView{entity=8,card_id="spell"}},field=new EntityView[0]},new PlayerView{seat=1,hero=new EntityView{entity=2,card_id="hero"},field=new[]{new EntityView{entity=9,card_id="minion"}}}}};
        var after=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(before));after.players[0].hand=new EntityView[0];
        var resolved=NativeSpellContext.FromEvent(new SemanticEvent{source=8,target=9,card_id="spell",owner=0},before,after,id=>id=="hero"?3:id=="minion"?4:id=="spell"?5:0);
        Check(resolved.CardType(0)==5&&resolved.CardType(1)==4&&resolved.CardType(2)==3,"cue resolves removed source and original target from public batch");
        Check(resolved.CardId(0)=="spell"&&resolved.CardId(1)=="minion"&&resolved.WithLifecycle("Death").CardId(0)=="spell",
            "source and target card ids survive event resolution and lifecycle cloning");
        var types=new[]{4,3,10,7,5,39,40};
        var names=new[]{"m_MinionEvent","m_HeroEvent","m_HeroPowerEvent","m_WeaponEvent","m_SpellEvent","m_LocationEvent","m_BaconQuestRewardEvent"};
        var choose=new NativeAction{name="SpellCardTypeAction",@params=new[]{new NativeParam{name="m_WhichCard",number=1}}.Concat(types.Select((type,i)=>new NativeParam{name=names[i],text="branch-"+type,kind=23})).ToArray()};
        var states=types.Select(type=>new NativeState{name="selected-"+type,actions=new[]{new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=1}}}},transitions=new NativeTransition[0]}).ToList();
        states.Add(new NativeState{name="Read",actions=new[]{choose},transitions=types.Select(type=>new NativeTransition{@event="branch-"+type,state="selected-"+type}).ToArray()});
        var program=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));
        program.machines=new[]{new NativeMachine{start="Read",floats=new NativeFloat[0],colors=new NativeColor[0],events=new NativeTransition[0],states=states.ToArray()}};
        foreach(int type in types)
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,type)))
        {
            instance.Seek(0);Check(instance.CurrentStates.Single()=="selected-"+type,"original target type dispatches serialized event on initial frame "+type);
            Check(instance.UnboundCardTypeActions==0&&instance.TransitionLimitHits==0,"bound dispatch stays finite "+type);
            instance.Seek(.2f);instance.Seek(0);Check(instance.CurrentStates.Single()=="selected-"+type,"backward seek repeats target dispatch "+type);
        }
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {instance.Seek(0);Check(instance.CurrentStates.Single()=="Read"&&instance.UnboundCardTypeActions==1,"missing entity context never guesses a minion branch");}
        choose.Param("m_MinionEvent").text="";
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,4)))
        {instance.Seek(0);Check(instance.CurrentStates.Single()=="Read"&&instance.UnboundCardTypeActions==0,"original null event does not invent transition");}
        choose.Param("m_MinionEvent").text="branch-4";choose.Param("m_MinionEvent").unresolved=true;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32,context:new NativeSpellContext(5,4)))
        {instance.Seek(0);Check(instance.CurrentStates.Single()=="Read"&&instance.UnboundCardTypeActions==1,"unresolved serialized event is retained as unbound");}
        int node=Array.FindIndex(program.nodes,n=>!String.IsNullOrEmpty(n.mesh)&&n.materials.Any(m=>m.textures.Length>0));
        program.nodes[node].visible=false;
        var show=new NativeAction{name="SetVisibilityRecursiveAction",@params=new[]{new NativeParam{name="gameObject",node=node},new NativeParam{name="visible",number=1},new NativeParam{name="includeChildren",number=0},new NativeParam{name="resetOnExit",number=1}}};
        program.machines[0].start="Show";program.machines[0].states=new[]{new NativeState{name="Show",actions=new[]{show,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f}}}},transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}}},new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}};
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {
            instance.Seek(0);var renderer=instance.Nodes[node].GetComponent<Renderer>();
            Check(renderer.enabled,"original recursive visibility can select only owner renderer");
            instance.Seek(.2f);Check(!renderer.enabled,"resetOnExit restores exact initial visibility");
            instance.Seek(0);Check(renderer.enabled,"backward seek repeats visibility OnEnter");
        }
        show.Param("resetOnExit").number=0;
        using(var instance=new NativeEffectInstance(program,assets,0,32,32))
        {instance.Seek(.2f);Check(instance.Nodes[node].GetComponent<Renderer>().enabled,"visibility survives state exit when original resetOnExit is false");}
    }
}
