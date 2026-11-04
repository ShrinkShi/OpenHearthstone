using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class HunterHeroPowerChecks
{
    [Serializable] sealed class PrefabRow{public string reference,file;}
    [Serializable] sealed class PrefabCatalog{public PrefabRow[] prefabs;}
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Hunter hero power: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(assets.Root);
        AutomaticNoStartCast(assets);NativeAudioSelection();SetPositionSemantics();HunterRootFlight(assets);HunterImpactParticleSpace(assets);HunterImpactExplosionAgeZero(assets);HunterImpactCameraShake(assets);RuntimeMaterialSnapshot(assets);
        Debug.Log("HUNTER HERO POWER: "+passed+" checks passed; two-player replay remains the runtime acceptance");
    }
    static void HunterImpactCameraShake(AssetRegistry assets)
    {
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_05bp");
        var prefab=JsonUtility.FromJson<PrefabCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")))
            .prefabs.Single(row=>row.reference==config.impact.prefab.prefab_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        var machine=program.machines.Single(row=>row.owner_known&&row.owner==0);
        var invoke=machine.states.Single(row=>row.name=="DeathState 2").actions.Single(row=>row.name=="InvokeMethod");
        Check(program.reference=="ReuseFX_Arrow_Impact_01.prefab:1c032452d358567449b65ff342bb87ce"&&
              NativeEffectInstance.SupportsCameraShakerInvoke(program,invoke)&&invoke.Param("gameObject").node==machine.owner,
            "HERO_05bp UseOwner InvokeMethod binds to the imported impact root component");
        var shaker=program.nodes[0].camera_shaker;
        Check(shaker!=null&&shaker.restored&&Mathf.Approximately(shaker.amount.x,.2f)&&Mathf.Approximately(shaker.amount.y,.2f)&&
              Mathf.Approximately(shaker.amount.z,.2f)&&shaker.curve.Length==2&&Mathf.Approximately(shaker.curve[1].time,.25f)&&
              Mathf.Approximately(shaker.curve[0].outSlope,-4)&&Mathf.Approximately(shaker.curve[1].inSlope,-4)&&
              !shaker.hold&&!shaker.is_override,
            "HERO_05bp CameraShaker keeps its authored 0.2 amount and quarter-second intensity curve");
    }
    static void HunterImpactParticleSpace(AssetRegistry assets)
    {
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_05bp");
        var prefab=catalog.prefabs.Single(row=>row.reference==config.impact.prefab.prefab_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        var overrides=NativeSuperSpellPlayback.SourceSimulationSpaceOverrides(config.card_id,config,prefab.reference,program);
        var sourceParticles=program.nodes.Where(node=>node.particle!=null&&node.particle.restored).ToArray();
        Check(overrides!=null&&overrides.Length==sourceParticles.Length&&sourceParticles.All(node=>overrides.Contains(node.name)),
            "HERO_05bp impact preserves serialized simulation space for every restored particle system");
        using(var effect=new NativeEffectInstance(program,assets,sourceSimulationSpaceNodes:overrides))
            foreach(var node in sourceParticles)
            {
                int index=Array.IndexOf(program.nodes,node);
                Check(effect.Particles[index]!=null&&effect.Particles[index].main.simulationSpace==(ParticleSystemSimulationSpace)node.particle.space,
                    "HERO_05bp impact particle keeps authored space: "+node.name);
            }
    }
    static void HunterImpactExplosionAgeZero(AssetRegistry assets)
    {
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_05bp");
        var prefab=catalog.prefabs.Single(row=>row.reference==config.impact.prefab.prefab_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        var machine=program.machines.Single(row=>row.owner_known&&row.owner==0);
        var state=machine.states.Single(row=>row.name=="DeathState 2");
        var action=state.actions[10];
        var expectedSoundPaths=new[]{"38dc39eff4ccb3616e7b89ea.native-sound.json","d4e7853ed281083a4e54bf76.native-sound.json","866ce32403a1e84d9b7cced8.native-sound.json"};
        var soundParams=action.@params.Where(param=>param.asset_type=="SoundDef").ToArray();
        var weights=action.@params.Where(param=>param.kind==15&&String.IsNullOrEmpty(param.name)).ToArray();
        Check(action.name=="AudioPlayRandomClipAction"&&action.Param("m_ParentObject").node==0&&soundParams.Select(param=>param.asset_path).SequenceEqual(expectedSoundPaths)&&
              weights.Select(param=>param.number).SequenceEqual(new[]{1f,1f,1f})&&Mathf.Approximately(action.Param("m_Delay").number,0),
            "HERO_05bp early impact cue is the original three-way equally weighted DeathState 2 explosion action");
        var explosionFiles=new HashSet<string>(soundParams.SelectMany(param=>
            JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(Path.Combine(assets.Root,param.asset_path))).clips.Select(clip=>clip.file)));
        var received=new List<NativeAudioCue>();
        using(var clone=new NativeAreaInstance(program,assets,audio:received.Add,
            context:new NativeSpellContext(10,heroType:3,lifecycle:"None",sourceCardId:"HERO_05bp"),startAtFsmEntry:true))
        {
            var effect=clone.Effect;effect.Root.transform.position=Vector3.zero;
            var statesAtImpact=String.Join("|",effect.CurrentStates);
            int bang=Array.FindIndex(program.nodes,node=>node.name=="Bang");
            float bangAge=bang<0||effect.Particles[bang]==null?float.NaN:effect.Particles[bang].time;
            Check(effect.StartHunterImpactExplosionAtAgeZero(),"HERO_05bp impact binds its source explosion action at clone age zero");
            Check(String.Join("|",effect.CurrentStates)==statesAtImpact&&
                  (float.IsNaN(bangAge)||Mathf.Approximately(effect.Particles[bang].time,bangAge)),
                "early explosion dispatch does not advance the impact FSM or Bang particle visual clock");
            Check(received.Count(cue=>explosionFiles.Contains(cue.file))==1&&
                  received.Single(cue=>explosionFiles.Contains(cue.file)).time==0,
                "original weighted explosion cue reaches the audio callback at impact-clone age zero");
            clone.Endpoint.Activate();clone.Seek(2f);
            Check(effect.SuppressedClipAudioActionEntries>0&&received.Count(cue=>explosionFiles.Contains(cue.file))==1,
                "later DeathState 2 entry suppresses the duplicate explosion cue");
        }
    }
    static void AutomaticNoStartCast(AssetRegistry assets)
    {
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id=="hero"?3:id=="HERO_05bp"?10:4;
            var state=new StateView{seat=0,active=0,phase="play",match_id="hunter-auto-cast-check",moves=new[]{new Move{kind="power",entity=2,target=0}},players=new[]{
                new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="hero"},power=new EntityView{entity=2,card_id="HERO_05bp"},field=new EntityView[0],hand=new EntityView[0]},
                new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="hero"},power=new EntityView{entity=4,card_id="HERO_05bp"},field=new EntityView[0],hand=new EntityView[0]}}};
            Check(BattleInteraction.Ready(state,2,"power"),"authority grants Hunter power availability");
            Check(state.moves.Single().target==0,"authority represents Hunter's automatic hero target as target=0");
            Check(!BattleInteraction.IsLegalTarget(state,2,3)&&!BattleInteraction.IsLegalTarget(state,2,1),"automatic target does not expose a manual selection target");
            Check(!player.SelectPower(state,2)&&!player.HasPowerSelection,"no-Start Hunter power cannot enter the targeted selection path");
        }
    }
    static void NativeAudioSelection()
    {
        Check(NativeAudio.SelectWeightedIndex(new[]{1f,1f,1f},0)==0,"weighted sound selection preserves the first interval");
        Check(NativeAudio.SelectWeightedIndex(new[]{1f,1f,1f},.5f)==1,"weighted sound selection preserves equal-weight middle interval");
        Check(NativeAudio.SelectWeightedIndex(new[]{1f,1f,1f},1)==2,"weighted sound selection clamps the final interval");
        Check(NativeAudio.SelectWeightedIndex(new[]{1f,-1f},.5f)==-1,"invalid negative weight remains unbound");
    }
    static void SetPositionSemantics()
    {
        var target=new GameObject("SetPosition semantics");
        try
        {
            target.transform.localPosition=new Vector3(1,2,3);
            Check(NativeEffectInstance.ApplySetPosition(target.transform,1,true,Vector3.zero,new[]{true,false,true},new Vector3(0,9,0),false,false)&&
                  target.transform.localPosition==new Vector3(1,9,3),"source Self/None uses current local axes and overrides only literal components");
            Check(NativeEffectInstance.ApplySetPosition(target.transform,0,false,new Vector3(8,7,6),new[]{false,true,false},new Vector3(4,0,0),false,false)&&
                  target.transform.position==new Vector3(4,7,0),"source World/vector preserves None axes while applying explicit components");
            var before=target.transform.position;
            Check(!NativeEffectInstance.ApplySetPosition(target.transform,1,true,Vector3.zero,new[]{true,true,true},Vector3.zero,true,false)&&
                  target.transform.position==before,"continuous SetPosition is not mistaken for the one-shot source path");
        }
        finally{AssetRegistry.ReleaseTransient(target);}
    }
    static void HunterRootFlight(AssetRegistry assets)
    {
        var index=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=index.programs.Single(program=>program.card_id=="HERO_05bp");
        Check(!config.start_enabled&&config.missile!=null&&config.missile.m_TargetJoint=="TargetJoint","tested flight uses the imported no-Start source config");
        var catalog=JsonUtility.FromJson<PrefabCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var prefab=catalog.prefabs.Single(item=>item.reference==config.missile_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        var trailIndex=Array.FindIndex(program.nodes,node=>node.name=="Arrow Trail");
        Check(trailIndex>=0,"Hunter missile source contains its authored TrailRenderer node");
        var sourceMaterial=program.nodes[trailIndex].materials.Single(material=>material.shader=="Hero/AddMultiply/AddMultiply");
        var sourceTexture=sourceMaterial.textures.Single(texture=>texture.name=="_MainTex");
        Check(sourceMaterial.properties.Contains("_MainTex")&&File.Exists(Path.Combine(assets.Root,sourceTexture.path)),"Hunter source trail material keeps its MainTex property and extracted PNG");
        var loadedTexture=assets.TextureFile(sourceTexture.path);
        Check(loadedTexture!=null&&loadedTexture.width>0&&loadedTexture.height>0,"AssetRegistry loads the source trail PNG as a nonempty Texture2D");
        var compatibilityShader=Resources.Load<Shader>("NativeEffect");
        var shaderProbe=compatibilityShader==null?null:new Material(compatibilityShader);
        Check(shaderProbe!=null&&shaderProbe.HasProperty("_MainTex"),"NativeEffect compatibility shader exposes the authored MainTex property");
        AssetRegistry.ReleaseTransient(shaderProbe);
        Check(!program.nodes.Any(node=>node.name.Contains(config.missile.m_TargetJoint)),"current Hunter missile contains no matching TargetJoint and follows source root fallback");
        var arrowIndex=Array.FindIndex(program.nodes,node=>node.path=="root/ArrowRoot/Arrow/Arrow");
        var glowIndex=Array.FindIndex(program.nodes,node=>node.path=="root/ArrowRoot/Arrow/ArrowGlowPlane");
        var arrowRootIndex=Array.FindIndex(program.nodes,node=>node.path=="root/ArrowRoot");
        Check(arrowIndex>=0&&glowIndex>=0&&program.nodes[arrowIndex].skinned_renderer&&program.nodes[glowIndex].skinned_renderer&&
              program.nodes[arrowIndex].skin!=null&&program.nodes[glowIndex].skin!=null,
            "Hunter source export retains both authored SkinnedMeshRenderer nodes");
        var arrowMesh=assets.MeshFile(program.nodes[arrowIndex].mesh);
        Check(arrowMesh.vertexCount==70&&arrowMesh.boneWeights.Length==70&&arrowMesh.bindposes.Length==4,
            "Hunter Arrow mesh restores its 70 weighted vertices and four source bindposes");
        Check(program.nodes[arrowIndex].skin.bones.Length==4&&program.nodes[arrowIndex].skin.root_bone==program.nodes[arrowIndex].skin.bones[0]&&
              !program.nodes[arrowIndex].visible&&!program.nodes[glowIndex].visible,
            "Hunter Arrow keeps its four source bone links and authored disabled renderer state");
        var target=new GameObject("Hunter missile target");target.transform.position=new Vector3(0,0,8);
        try
        {
            using(var clone=new NativeAreaInstance(program,assets,locations:kind=>target,facings:kind=>target))
            {
                var runtimeArrow=clone.Effect.Nodes[arrowIndex].GetComponent<SkinnedMeshRenderer>();
                Check(runtimeArrow!=null&&runtimeArrow.sharedMesh.vertexCount==70&&runtimeArrow.bones.Length==4&&runtimeArrow.rootBone==runtimeArrow.bones[0],
                    "constructed Hunter Arrow renderer binds the exported mesh and source skeleton");
                Check(!runtimeArrow.enabled,"constructed Hunter Arrow preserves the source-disabled renderer flag");
                var trailRenderer=clone.Effect.Nodes[trailIndex].GetComponent<TrailRenderer>();
                var runtimeTexture=trailRenderer==null||trailRenderer.sharedMaterial==null?null:trailRenderer.sharedMaterial.GetTexture("_MainTex");
                Check(trailRenderer!=null&&trailRenderer.emitting==program.nodes[trailIndex].trail.emitting,
                    "constructed Hunter TrailRenderer preserves the authored emitting state");
                Check(trailRenderer!=null&&runtimeTexture!=null&&runtimeTexture.width==loadedTexture.width&&runtimeTexture.height==loadedTexture.height,
                    "constructed Hunter TrailRenderer retains its source MainTex texture object");
                Check(trailRenderer.sharedMaterial.shader==compatibilityShader&&trailRenderer.sharedMaterial.GetFloat("_OriginalFamily")==3,
                    "compatibility shader maps Hero/AddMultiply/AddMultiply to its reviewed texture family");
                clone.Effect.SetOriginalPosition(Vector3.zero);int arrivals=0,looks=0;
                using(var flight=new NativeMissileFlight(clone,config.missile,()=>target.transform.position,(a,b)=>a,()=>true,
                    ()=>arrivals++,look=>looks++,targetTransform:()=>target.transform))
                {
                    var root=clone.Effect.Nodes[0].transform;
                    Check(flight.MovementTransform==root,"missing TargetJoint selects the original projectile root mover");
                    flight.Birth();
                    Check(clone.Effect.ActionExecutions.ContainsKey("SetPosition")&&clone.Effect.ActionExecutions["SetPosition"]>0&&
                          clone.Effect.Nodes[arrowRootIndex].transform.localPosition==Vector3.zero&&
                          !clone.Effect.UnhandledActionDetails.Any(detail=>detail.Contains("action=SetPosition")),
                        "source Birth executes Self-space zero SetPosition on ArrowRoot without altering its authored zero transform");
                    float sample=.01f;flight.Tick(sample);Check(flight.Path==null,"Hunter source Birth honors its authored RandomWaitAction");
                    while(flight.Path==null&&sample<.46f){sample+=1f/120;flight.Tick(sample);}
                    Check(flight.Path!=null,"Hunter source Birth completion generates the configured path within its 0.15-0.45s source wait");
                    Check(Mathf.Abs(flight.Path.Points[2].y-target.transform.position.y)<.0001f,"TargetHeightOffset is conditional on a matching joint");
                    var pathTarget=flight.Path.Points[2];var targetPosition=target.transform.position;
                    target.transform.position+=Vector3.one;
                    Check(Vector3.Distance(flight.TargetPosition,pathTarget)<.0001f,"flight target diagnostic remains the generated endpoint after target transforms change");
                    target.transform.position=targetPosition;
                    flight.Tick(flight.FiredAt+flight.Path.Duration*.5f);
                    Check(Vector3.Distance(root.position,flight.Path.Sample(flight.Path.Duration*.5f))<.002f&&looks>0,"root fallback advances along source path and requests LookUpdate");
                    flight.Tick(Mathf.Max(flight.FiredAt+flight.Path.Duration,sample)+.002f);
                    Check(arrivals==1&&flight.Reached,"source missile arrival callback runs once before death (time="+flight.FiredAt+" duration="+flight.Path.Duration+" reached="+flight.Reached+" callbacks="+arrivals+")");
                }
            }
            using(var clone=new NativeAreaInstance(program,assets,slot:1,locations:kind=>target,facings:kind=>target))
            {
                clone.Effect.SetOriginalPosition(Vector3.zero);var child=clone.Effect.Nodes.Skip(1).First(node=>node.transform.parent!=null);
                child.name="SyntheticTargetJoint";int looks=0;
                using(var flight=new NativeMissileFlight(clone,config.missile,()=>target.transform.position,(a,b)=>a,()=>true,
                    ()=>{},look=>looks++,targetTransform:()=>target.transform))
                {
                    Check(flight.MovementTransform==child.transform,"a matching authored joint selects the joint mover");
                    Check(Vector3.Distance(clone.Effect.Nodes[0].transform.forward,(target.transform.position-clone.Effect.Nodes[0].transform.position).normalized)<.01f,
                        "joint branch initially points the projectile root at the target");
                    flight.Birth();float sample=.01f;flight.Tick(sample);
                    while(flight.Path==null&&sample<.46f){sample+=1f/120;flight.Tick(sample);}
                    Check(flight.Path!=null,"joint branch also waits for the source Birth FSM");
                    flight.Tick(flight.FiredAt+flight.Path.Duration*.5f);
                    Check(looks>0&&clone.Effect.Nodes[0].transform.position!=flight.MovementTransform.position,"joint mover branch preserves root and moves the matching child");
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(target);}
    }
    static void RuntimeMaterialSnapshot(AssetRegistry assets)
    {
        var index=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=index.programs.Single(program=>program.card_id=="HERO_05bp");
        var catalog=JsonUtility.FromJson<PrefabCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var prefab=catalog.prefabs.Single(item=>item.reference==config.missile_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        int trailIndex=Array.FindIndex(program.nodes,node=>node.name=="Arrow Trail");
        var target=new GameObject("Hunter material snapshot target");
        try
        {
            using(var clone=new NativeAreaInstance(program,assets,locations:kind=>target,facings:kind=>target))
            {
                var renderer=clone.Effect.Nodes[trailIndex].GetComponent<TrailRenderer>();
                var material=renderer.sharedMaterial;var source=program.nodes[trailIndex].materials.Single(item=>item.name==material.name);
                var original=material.GetColor("_Color");var transient=original;transient.r*=.75f;transient.a*=.5f;
                material.SetColor("_Color",transient);
                var snapshot=HunterRuntimeMaterialSnapshot.Capture(material,source);
                material.SetColor("_Color",original);string error;
                Check(snapshot.Apply(material,assets,out error)&&material.GetColor("_Color")==transient,
                    "captured runtime material color replays exactly at the sampled flight stage");
                Check(snapshot.textures.Length==source.textures.Length&&snapshot.textures.All(value=>value.isNull||!String.IsNullOrEmpty(value.path)),
                    "runtime material texture state retains a source path or explicit null");
                if(snapshot.textures.Length>0)
                {
                    var path=snapshot.textures[0].path;snapshot.textures[0].path="";
                    Check(!snapshot.Apply(material,assets,out error)&&error.Contains("no resolvable source path"),
                        "final-draw replay fails closed when a runtime texture cannot be resolved");
                    snapshot.textures[0].path=path;
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(target);}
    }
}
