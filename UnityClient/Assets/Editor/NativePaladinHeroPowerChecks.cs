using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativePaladinHeroPowerChecks
{
    const string RootReference="Holy_Untargeted_FX.prefab:9abe2b276b167a9459cc37d1d50b1660";
    const string AreaReference="ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10";
    static int passed;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Paladin hero power: "+reason);passed++;}
    static NativeSourceCamera OriginalMainCamera(AssetRegistry assets)
    {
        return JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json")))
            .cameras.Single(camera=>camera.name=="Main Camera");
    }
    static bool ByteColor(Color color)
    {
        return new[]{color.r,color.g,color.b,color.a}.All(value=>(Mathf.Approximately(value,0)||Mathf.Approximately(value,255)));
    }
    static bool UnitColor(Color color)
    {
        return new[]{color.r,color.g,color.b,color.a}.All(value=>!float.IsNaN(value)&&value>=0&&value<=1);
    }
    static T Copy<T>(T value)
    {return JsonUtility.FromJson<T>(JsonUtility.ToJson(value));}
    static string GateMismatches(NativeProgram root,NativeSuperSpellConfig config,NativeProgram areaPrefab)
    {
        var mismatches=new List<string>();
        Action<string,bool,string> require=(field,valid,actual)=>{if(!valid)mismatches.Add(field+"="+actual);};
        require("root",root!=null,root==null?"null":"present");
        require("config",config!=null,config==null?"null":"present");
        require("areaPrefab",areaPrefab!=null,areaPrefab==null?"null":"present");
        if(root!=null)
        {
            require("root.reference",root.reference==RootReference,root.reference??"<null>");
            require("root.nodes.Length",root.nodes!=null&&root.nodes.Length==13,root.nodes==null?"<null>":root.nodes.Length.ToString());
        }
        if(config!=null)
        {
            require("config.card_id",config.card_id=="HERO_04bp",config.card_id??"<null>");
            require("config.reference",root!=null&&config.reference==root.reference,config.reference??"<null>");
            require("config.location",config.location==3,config.location.ToString());
            require("config.target_behavior",config.target_behavior==6,config.target_behavior.ToString());
            require("config.start_enabled",!config.start_enabled,config.start_enabled.ToString());
            require("config.start_config_null",config.start_config_null,config.start_config_null.ToString());
            require("config.missile",config.missile!=null,config.missile==null?"null":"present");
            if(config.missile!=null)require("config.missile.m_Enabled",config.missile.m_Enabled==0,config.missile.m_Enabled.ToString());
            require("config.missile_reference",String.IsNullOrEmpty(config.missile_reference),config.missile_reference??"<null>");
            require("config.impact",config.impact!=null,config.impact==null?"null":"present");
            if(config.impact!=null)
            {
                require("config.impact.enabled",!config.impact.enabled,config.impact.enabled.ToString());
                require("config.impact.prefab",config.impact.prefab!=null,config.impact.prefab==null?"null":"present");
                if(config.impact.prefab!=null)
                {
                    require("config.impact.prefab.prefab_null",config.impact.prefab.prefab_null,config.impact.prefab.prefab_null.ToString());
                    require("config.impact.prefab.prefab_reference",String.IsNullOrEmpty(config.impact.prefab.prefab_reference),config.impact.prefab.prefab_reference??"<null>");
                }
                require("config.impact.ranges",config.impact.ranges!=null&&config.impact.ranges.Length==0,
                    config.impact.ranges==null?"<null>":config.impact.ranges.Length.ToString());
            }
            require("config.area",config.area!=null,config.area==null?"null":"present");
            if(config.area!=null)
            {
                require("config.area.node",config.area.node==0,config.area.node.ToString());
                require("config.area.kind",config.area.kind=="SuperSpell",config.area.kind??"<null>");
                var friendly=config.area.friendly;
                require("config.area.friendly",friendly!=null,friendly==null?"null":"present");
                if(friendly!=null)
                {
                    require("friendly.enabled",friendly.enabled,friendly.enabled.ToString());
                    require("friendly.prefab_null",!friendly.prefab_null,friendly.prefab_null.ToString());
                    require("friendly.prefab_reference",friendly.prefab_reference==AreaReference,friendly.prefab_reference??"<null>");
                    require("friendly.prefab_source",friendly.prefab_source!=null,friendly.prefab_source==null?"null":"present");
                    if(friendly.prefab_source!=null)
                        require("friendly.prefab_source.pathid",friendly.prefab_source.pathid=="8695582295512710279",friendly.prefab_source.pathid??"<null>");
                    require("friendly.use_super_location",friendly.use_super_location,friendly.use_super_location.ToString());
                    require("friendly.location",friendly.location==0,friendly.location.ToString());
                    require("friendly.parent_to_location",!friendly.parent_to_location,friendly.parent_to_location.ToString());
                    require("friendly.facing",friendly.facing==0,friendly.facing.ToString());
                    require("friendly.delay_min",Mathf.Approximately(friendly.delay_min,0),friendly.delay_min.ToString("R"));
                    require("friendly.delay_max",Mathf.Approximately(friendly.delay_max,0),friendly.delay_max.ToString("R"));
                }
                var opponent=config.area.opponent;
                require("config.area.opponent",opponent!=null,opponent==null?"null":"present");
                if(opponent!=null)
                {
                    require("opponent.enabled",opponent.enabled,opponent.enabled.ToString());
                    require("opponent.prefab_null",opponent.prefab_null,opponent.prefab_null.ToString());
                    require("opponent.prefab_reference",String.IsNullOrEmpty(opponent.prefab_reference),opponent.prefab_reference??"<null>");
                }
            }
            require("config.chain_enabled",config.chain_enabled,config.chain_enabled.ToString());
            require("config.chain_prefab_null",config.chain_prefab_null,config.chain_prefab_null.ToString());
            require("config.chain_reference",String.IsNullOrEmpty(config.chain_reference),config.chain_reference??"<null>");
            require("config.chain_available",!config.chain_available,config.chain_available.ToString());
            require("config.reverse_available",!config.reverse_available,config.reverse_available.ToString());
            require("config.friendly_area_profile",config.friendly_area_profile=="friendly-area-only-reconstruction; enabled-null-chain-unresolved",
                config.friendly_area_profile??"<null>");
        }
        if(areaPrefab!=null)
        {
            require("areaPrefab.reference",config!=null&&config.area!=null&&config.area.friendly!=null&&
                areaPrefab.reference==config.area.friendly.prefab_reference,areaPrefab.reference??"<null>");
            require("areaPrefab.nodes.Length",areaPrefab.nodes!=null&&areaPrefab.nodes.Length==12,
                areaPrefab.nodes==null?"<null>":areaPrefab.nodes.Length.ToString());
            require("areaPrefab.machines.Length",areaPrefab.machines!=null&&areaPrefab.machines.Length==1,
                areaPrefab.machines==null?"<null>":areaPrefab.machines.Length.ToString());
            if(areaPrefab.machines!=null&&areaPrefab.machines.Length>0)
            {
                require("areaPrefab.machines[0].owner_known",areaPrefab.machines[0].owner_known,areaPrefab.machines[0].owner_known.ToString());
                require("areaPrefab.machines[0].owner",areaPrefab.machines[0].owner==0,areaPrefab.machines[0].owner.ToString());
            }
        }
        return mismatches.Count==0?"<all typed gate fields match>":String.Join("; ",mismatches.ToArray());
    }
    static StateView State(int version)
    {
        return new StateView{match_id="paladin-friendly-area-check",version=version,seat=0,phase="play",moves=new Move[0],events=new SemanticEvent[0],players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_04"},power=new EntityView{entity=2,card_id="HERO_04bp"},
                field=new EntityView[0],hand=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_02"},power=new EntityView{entity=4,card_id="HERO_02bp"},
                field=new EntityView[0],hand=new EntityView[0]}}};
    }
    public static void RunSourceRoute()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_04bp");
        var catalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(row=>row.card_id=="HERO_04bp"&&row.file==entry.file);
        var root=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        var prefab=catalog.prefabs.Single(row=>row.reference==AreaReference);
        var area=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        Check(!entry.enabled&&root.reference==RootReference&&config.reference==RootReference,
            "the authored disabled/null root is retained and enters only the dedicated reconstruction route");
        Check(config.chain_enabled&&config.chain_prefab_null&&String.IsNullOrEmpty(config.chain_reference)&&!config.chain_available,
            "enabled-null Chain remains distinct from disabled and is unavailable to the playback route");
        var rootTransform=(root.spell_transforms??new NativeSourceSpellTransform[0]).FirstOrDefault(row=>row.node==0&&row.enabled);
        int rawChainEnabled=rootTransform==null||rootTransform.raw==null||rootTransform.raw.m_ChainInfo==null?-1:rootTransform.raw.m_ChainInfo.m_Enabled;
        bool strictGate=NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,config,area);
        string gateMismatches=GateMismatches(root,config,area);
        Debug.Log("PALADIN H04 GATE DIAGNOSTIC: raw root m_ChainInfo.m_Enabled="+rawChainEnabled+
            "; strict gate="+strictGate+"; gate field mismatches="+gateMismatches);
        Check(rawChainEnabled==1,"raw root Chain marker must remain enabled (actual="+rawChainEnabled+")");
        Check(strictGate,"strict FriendlyArea reconstruction gate must accept its imported source profile ("+gateMismatches+")");
        var missingChain=Copy(config);missingChain.chain_enabled=false;
        var fabricatedChain=Copy(config);fabricatedChain.chain_prefab_null=false;
        var referencedChain=Copy(config);referencedChain.chain_reference="Unverified.prefab:11111111111111111111111111111111";
        var wrongArea=Copy(config);wrongArea.area.friendly.prefab_source.pathid="0";
        var emptyStartProjection=Copy(config);emptyStartProjection.start=new NativeSuperStartConfig();
        var enabledStart=Copy(config);enabledStart.start_enabled=true;
        var nonNullStartSource=Copy(config);nonNullStartSource.start_config_null=false;
        Check(NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,emptyStartProjection,area)&&
            !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,enabledStart,area)&&
            !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,nonNullStartSource,area),
            "JsonUtility's empty-object projection for a null Start is accepted only with the authored disabled/null flags");
        Check(!NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,missingChain,area)&&
            !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,fabricatedChain,area)&&
            !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,referencedChain,area)&&
            !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(root,wrongArea,area),
            "changed Chain source facts or an unverified FriendlyArea source fail closed");

        var areaCatalog=JsonUtility.FromJson<NativeSuperAreaCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-config.json")));
        var areaRecord=areaCatalog.programs.Single(row=>row.file==entry.file&&row.reference==root.reference);
        var prefabCatalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-prefabs.json")));
        var registered=prefabCatalog.prefabs.Single(row=>row.reference==AreaReference);
        Check(areaRecord.config.friendly.enabled&&areaRecord.config.friendly.prefab_reference==AreaReference&&
            registered.file==prefab.file&&File.Exists(Path.Combine(assets.Root,area.audio_file)),
            "the exact FriendlyArea prefab and its source audio export are registered independently");

        var mainCamera=OriginalMainCamera(assets);
        var before=State(1);var after=State(2);after.events=new[]{
            new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_04bp",owner=0},
            new SemanticEvent{type="Summon",source=2,target=20,card_id="CS2_101",card_type=4,owner=0,index=0}};
        after.players[0].field=new[]{new EntityView{entity=20,card_id="CS2_101"}};
        var positions=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)},{20,new Rect(830,300,140,100)}};
        var maskCopies=new Mesh[2];var cachedMasks=new Mesh[2];var cachedColors=new Color[2][];
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id=="HERO_04bp"||id=="HERO_02bp"?10:id=="HERO_04"||id=="HERO_02"?3:4;
            NativeSuperSpellScene routedScene=null;
            player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>routedScene=NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                id=>positions.ContainsKey(id)?(Vector2?)positions[id].center:null,viewport,origin,player.CardType,mainCamera,
                entity=>positions.ContainsKey(entity)?(Rect?)positions[entity]:null);
            var route=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy,NativeTailActive=player.SourceVisualTailActive};
            int summonPreparationCalls=0;
            route.NativePrepare=(key,data,oldState,newState,isArea,age)=>
            {
                if(!player.PrepareSource(key,data,oldState,newState,isArea,age))
                    throw new Exception("Paladin hero power: FriendlyArea source was rejected");
            };
            route.NativeSummonPrepare=(key,data,oldState,newState,age)=>{summonPreparationCalls++;return false;};
            route.Accept(null,before,positions);route.Accept(before,after,positions);
            var visual=route.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_04bp");
            string key=after.match_id+":"+after.version+":"+visual.data.source+":"+visual.start;
            Check(route.Cues.Count(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_04bp")==1&&
                visual.data.source==2&&visual.data.target==0&&visual.apply==float.MaxValue&&
                route.Cues.Count(cue=>cue.data.type=="Summon"&&cue.data.target==20)==1,
                "PlayHeroPower emits one source-only cue while the same-batch server Summon remains one separate event");
            route.PrepareNative();
            var sourceArea=player.SourceArea(key);
            Check(routedScene!=null&&routedScene.SourceScreenPoint.HasValue&&
                Vector2.Distance(routedScene.SourceScreenPoint.Value,positions[1].center)<.001f,
                "Paladin's source effect is projected at the owning hero portrait rather than the circular power button");
            Check(routedScene.SourceCamera==mainCamera&&sourceArea!=null&&sourceArea.Effect.PreviewCamera!=null&&
                sourceArea.Effect.PreviewCamera.orthographic==mainCamera.orthographic&&
                Mathf.Approximately(sourceArea.Effect.PreviewCamera.fieldOfView,mainCamera.fov),
                "the H04 placement adapter and production effect camera use the same original Main Camera profile");
            var sourceNodes=sourceArea==null||sourceArea.Effect==null?null:sourceArea.Effect.Nodes;
            var cardHeroNode=sourceNodes==null||sourceNodes.Length<=11?null:sourceNodes[11];
            var cardHeroRenderer=cardHeroNode==null?null:cardHeroNode.GetComponent<Renderer>();
            var previewCamera=sourceArea==null||sourceArea.Effect==null?null:sourceArea.Effect.PreviewCamera;
            Check(sourceArea!=null&&sourceNodes!=null&&sourceNodes.Length>11&&area.nodes!=null&&area.nodes.Length>11&&
                area.nodes[11]!=null&&area.nodes[11].name=="Card_Hero"&&area.nodes[11].source_layer==21&&cardHeroNode!=null&&cardHeroNode.layer==21&&
                cardHeroRenderer!=null&&cardHeroRenderer.enabled&&
                previewCamera!=null&&(previewCamera.cullingMask&(1<<21))==0&&
                (previewCamera.cullingMask&(1<<30))!=0,
                "the source layer-21 Card_Hero producer stays visible to capture code but is isolated from the final effect camera");
            foreach(int pair in new[]{0,1})
            {
                int node=pair==0?6:7;var source=assets.MeshFile(area.nodes[node].mesh);
                var filter=sourceArea.Effect.Nodes[node].GetComponent<MeshFilter>();
                var instanceMesh=filter==null?null:filter.sharedMesh;
                cachedMasks[pair]=source;cachedColors[pair]=source==null?null:source.colors.ToArray();maskCopies[pair]=instanceMesh;
                var instanceColors=instanceMesh==null?null:instanceMesh.colors;
                Check(source!=null&&instanceMesh!=null&&source!=instanceMesh,
                    "H04 node "+node+" normalizes a per-instance mesh clone while retaining the shared cache mesh");
                Check(cachedColors[pair]!=null&&cachedColors[pair].Length>0&&cachedColors[pair].All(ByteColor)&&
                    instanceColors!=null&&instanceColors.Length==cachedColors[pair].Length&&instanceColors.All(UnitColor),
                    "H04 node "+node+" runtime readback maps source byte colors into [0,1]");
                Check(source.colors.SequenceEqual(cachedColors[pair]),
                    "H04 node "+node+" shared AssetRegistry mesh remains unchanged before playback disposal");
            }
            Check(sourceArea!=null&&sourceArea.FriendlyAreaOnlyReconstruction&&sourceArea.Reference==AreaReference&&
                sourceArea.AreaCloneCreations==1&&player.SourceSpell(key)==null&&player.SourceAreaCount==1&&player.ActiveCount==1,
                "the production cue creates one FriendlyArea visual clone and never constructs the parent Chain dispatcher");
            var expectedHero=new Vector2(positions[1].center.x/1920,positions[1].center.y/1080);
            var initialHeroProjectionError=Vector2.Scale(
                sourceArea.Effect.PreviewPoint(sourceArea.Effect.Nodes[0].transform.position)-expectedHero,new Vector2(1920,1080));
            Debug.Log("PALADIN SOURCE CAMERA initial hero projection errorPixels="+initialHeroProjectionError.ToString("F3"));
            Check(initialHeroProjectionError.magnitude<=2f,
                "the production FriendlyArea inherits parent SourceHero placement within two logical pixels; errorPixels="+initialHeroProjectionError.ToString("F3"));
            route.Tick(.35f,positions);route.PrepareNative();
            Check(summonPreparationCalls==1&&after.players[0].field.Length==1&&after.players[0].field[0].entity==20&&
                route.Cues.Count(cue=>cue.data.type=="Summon"&&cue.data.target==20)==1&&sourceArea.AreaCloneCreations==1,
                "visual preparation leaves the single server-owned token untouched and never emits another Summon");
            var seekHeroProjectionError=Vector2.Scale(
                sourceArea.Effect.PreviewPoint(sourceArea.Effect.Nodes[0].transform.position)-expectedHero,new Vector2(1920,1080));
            Debug.Log("PALADIN SOURCE CAMERA first forward Seek hero projection errorPixels="+seekHeroProjectionError.ToString("F3"));
            Check(seekHeroProjectionError.magnitude<=2f,
                "the first forward Seek retains parent SourceHero placement within two logical pixels; errorPixels="+seekHeroProjectionError.ToString("F3"));
            route.Tick(.5f,positions);route.PrepareNative();
            Check(sourceArea.Pending==0&&sourceArea.VisualTailActive&&!player.SourceBusy(key)&&route.Busy&&!route.InputLocked,
                "source finish releases gameplay at 0.7s while its authored fade remains visible");
            route.Tick(.2f,positions);route.PrepareNative();
            Check(sourceArea.VisualTailActive&&route.NativeVisualTailCount==1&&route.NativeKeys().Contains(key)&&
                !player.SourceBusy(key)&&route.Busy&&!route.InputLocked&&sourceArea.Time>1f,
                "after the normal batch window, the live source fade is retained and sampled as a detached visual tail");
            var next=Copy(after);next.version=3;next.seat=1;
            next.events=new[]{new SemanticEvent{type="TURN_START",owner=1}};
            route.Accept(after,next,positions);route.Tick(1.2f,positions);route.PrepareNative();
            Check(route.Display.version==3&&
                route.NativeVisualTailCount==1&&route.NativeKeys().Contains(key)&&sourceArea.VisualTailActive&&
                route.Busy&&!route.InputLocked,
                "a later authority batch completes and unlocks input before the prior source tail finishes");
            route.Tick(.6f,positions);route.PrepareNative();route.Tick(0,positions);
            Check(!sourceArea.VisualTailActive&&!player.SourceBusy(key)&&sourceArea.Effect.ActiveSourceSpellState==0&&
                route.NativeVisualTailCount==0&&!route.Busy&&!route.InputLocked,
                "the detached source continues through its authored fade Wait and releases after reaching None");
        }
        for(int pair=0;pair<2;pair++)
        {
            int node=pair==0?6:7;
            Check(maskCopies[pair]==null,"H04 node "+node+" instance mesh clone is released with its source area");
            Check(cachedMasks[pair]!=null&&cachedMasks[pair].colors.SequenceEqual(cachedColors[pair])&&cachedColors[pair].All(ByteColor),
                "H04 node "+node+" shared cache color readback remains byte-valued after clone disposal");
        }
        SourceProjection(assets,root,entry);
        Debug.Log("NATIVE PALADIN HERO POWER: "+passed+" checks; FriendlyArea-only reconstruction with enabled-null Chain retained");
    }
    static string SaveDiagnosticTexture(string folder,string name,Texture source)
    {
        if(source==null)return name+"=<null>";
        int width=Mathf.Max(1,source.width),height=Mathf.Max(1,source.height);
        var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        Texture2D snapshot=null;
        try
        {
            Graphics.Blit(source,target);RenderTexture.active=target;
            snapshot=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
            snapshot.ReadPixels(new Rect(0,0,width,height),0,0);snapshot.Apply(false,false);
            var pixels=snapshot.GetPixels32();int left=width,top=height,right=-1,bottom=-1,nonzero=0;
            long red=0,green=0,blue=0,alpha=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var pixel=pixels[y*width+x];red+=pixel.r;green+=pixel.g;blue+=pixel.b;alpha+=pixel.a;
                if(Mathf.Max(Mathf.Max(pixel.r,pixel.g),Mathf.Max(pixel.b,pixel.a))<=8)continue;
                nonzero++;left=Mathf.Min(left,x);right=Mathf.Max(right,x);top=Mathf.Min(top,y);bottom=Mathf.Max(bottom,y);
            }
            string path=Path.Combine(folder,name+".png");File.WriteAllBytes(path,snapshot.EncodeToPNG());
            string bounds=nonzero==0?"empty":left+","+top+"-"+right+","+bottom;
            return name+"="+source.name+" "+width+"x"+height+" content="+nonzero+" bounds="+bounds+
                " meanRGBA="+(red/(double)pixels.Length).ToString("F1")+","+(green/(double)pixels.Length).ToString("F1")+","+
                (blue/(double)pixels.Length).ToString("F1")+","+(alpha/(double)pixels.Length).ToString("F1");
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
            if(snapshot!=null)UnityEngine.Object.DestroyImmediate(snapshot);
        }
    }
    static RenderTexture RenderDiagnosticLayerCamera(NativeEffectInstance effect,GameObject focus,int mask,bool boardView)
    {
        var cameraObject=new GameObject("Paladin source diagnostic camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=mask;
        camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.aspect=1024f/576f;camera.orthographic=true;
        if(boardView)
        {
            camera.orthographicSize=16;camera.transform.position=new Vector3(0,22,-4);camera.transform.LookAt(Vector3.zero);
        }
        else
        {
            var renderer=focus==null?null:focus.GetComponent<Renderer>();var bounds=renderer==null?new Bounds(focus==null?Vector3.zero:focus.transform.position,Vector3.one):renderer.bounds;
            camera.orthographicSize=Mathf.Max(.15f,Mathf.Max(bounds.extents.x,bounds.extents.z)*1.2f);
            camera.transform.rotation=effect.PreviewCamera.transform.rotation;
            camera.transform.position=bounds.center-camera.transform.forward*5f;
        }
        var target=new RenderTexture(1024,576,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        camera.Render();camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);return target;
    }
    public static void RunVisualDiagnosticBuild(){RunVisualDiagnostic();NativeSuperSpellChecks.RunHeroPowerFeedbackBuild();}
    public static void RunVisualDiagnostic()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.Single(row=>row.card_id=="HERO_04bp");
        var before=State(1);var after=State(2);after.events=new[]{
            new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id="HERO_04bp",owner=0},
            new SemanticEvent{type="Summon",source=2,target=20,card_id="CS2_101",card_type=4,owner=0,index=0}};
        after.players[0].field=new[]{new EntityView{entity=20,card_id="CS2_101"}};
        var positions=new Dictionary<int,Rect>{{1,BattleLayout.Hero(true)},{2,BattleLayout.Power(true)},
            {3,BattleLayout.Hero(false)},{4,BattleLayout.Power(false)},{20,new Rect(830,300,140,100)}};
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","work","native-paladin-source-diagnostic"));
        Directory.CreateDirectory(folder);var notes=new List<string>();
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id=="HERO_04bp"||id=="HERO_02bp"?10:id=="HERO_04"||id=="HERO_02"?3:4;
            NativeSuperSpellScene scene=null;
            var sourceCamera=OriginalMainCamera(assets);
            player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>scene=NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                id=>positions.ContainsKey(id)?(Vector2?)positions[id].center:null,viewport,origin,player.CardType,sourceCamera);
            var route=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy,NativeTailActive=player.SourceVisualTailActive};
            route.NativeSummonPrepare=(key,data,oldState,newState,age)=>false;
            route.NativePrepare=(key,data,oldState,newState,isArea,age)=>
            {if(!player.PrepareSource(key,data,oldState,newState,isArea,age))throw new InvalidOperationException("Paladin diagnostic source was rejected");};
            route.Accept(null,before,positions);route.Accept(before,after,positions);route.PrepareNative();
            var visual=route.Cues.Single(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id=="HERO_04bp");
            string key=after.match_id+":"+after.version+":"+visual.data.source+":"+visual.start;
            var area=player.SourceArea(key);if(area==null||scene==null||!area.FriendlyAreaOnlyReconstruction)
                throw new InvalidOperationException("Paladin diagnostic failed to construct the strict FriendlyArea source route");
            const float age=.44f;area.Seek(age);var effect=area.Effect;var nodes=effect.Nodes;
            if(nodes==null||nodes.Length<=11)throw new InvalidOperationException("Paladin source node 11 is missing");
            var mainCamera=effect.PreviewCamera;var plane=nodes[9].GetComponent<Renderer>();var hero=nodes[11];var heroRenderer=hero.GetComponent<Renderer>();
            var heroMask=nodes[6].GetComponent<Renderer>();var heroGlow=nodes[7].GetComponent<Renderer>();
            if(mainCamera==null||plane==null||heroRenderer==null)throw new InvalidOperationException("Paladin diagnostic source renderers are missing");
            if(heroMask==null||heroGlow==null)throw new InvalidOperationException("Paladin diagnostic mask/glow renderers are missing");
            var heroMaterial=heroRenderer.sharedMaterial;var heroTexture=heroMaterial!=null&&heroMaterial.HasProperty("_MainTex")?heroMaterial.GetTexture("_MainTex"):null;
            var planeMaterial=plane.sharedMaterial;var planeTexture=planeMaterial!=null&&planeMaterial.HasProperty("_MainTex")?planeMaterial.GetTexture("_MainTex"):null;
            var sourceCapture=effect.OffscreenFX.SingleOrDefault();
            var heroBounds=heroRenderer.bounds;var heroBoardCamera=RenderDiagnosticLayerCamera(effect,hero,1<<21,true);
            var heroCenteredCamera=RenderDiagnosticLayerCamera(effect,hero,1<<21,false);
            var frame=effect.CaptureSubtreeFrame(age,0);
            notes.Add("reference="+effect.Program.reference+" sourceAge="+area.Time.ToString("F3")+" state="+String.Join("/",effect.CurrentStates)+
                " friendlyArea="+area.FriendlyAreaOnlyReconstruction+" route="+scene.SourceScreenPoint+" effectRoot="+effect.Root.transform.position.ToString("F3")+
                " node0="+nodes[0].transform.position.ToString("F3")+" cameraMask="+mainCamera.cullingMask+" camera="+mainCamera.transform.position.ToString("F3")+
                " ortho="+mainCamera.orthographicSize.ToString("F3")+" frame="+frame.center.ToString("F3")+"/"+frame.orthographicSize.ToString("F3"));
            notes.Add("node9="+nodes[9].name+" layer="+nodes[9].layer+" enabled="+plane.enabled+" active="+plane.gameObject.activeInHierarchy+
                " pos="+nodes[9].transform.position.ToString("F3")+" bounds="+plane.bounds.center.ToString("F3")+"/"+plane.bounds.size.ToString("F3")+
                " texture="+(planeTexture==null?"<null>":planeTexture.name+" "+planeTexture.width+"x"+planeTexture.height)+
                " floats="+String.Join(",",effect.Program.nodes[9].materials[0].floats.Select(value=>value.name+"="+value.value.ToString("F3")).ToArray()));
            notes.Add("node11="+hero.name+" sourceLayer="+effect.Program.nodes[11].source_layer+" layer="+hero.layer+" enabled="+heroRenderer.enabled+
                " active="+hero.activeInHierarchy+" isVisible="+heroRenderer.isVisible+" world="+hero.transform.position.ToString("F3")+
                " bounds="+heroBounds.center.ToString("F3")+"/"+heroBounds.size.ToString("F3")+
                " previewViewport="+mainCamera.WorldToViewportPoint(heroBounds.center).ToString("F3")+" previewMask21="+((mainCamera.cullingMask&(1<<21))!=0)+
                " material="+(heroMaterial==null?"<null>":heroMaterial.name+"/"+effect.Program.nodes[11].materials[0].shader)+
                " texture="+(heroTexture==null?"<null>":heroTexture.name+" "+heroTexture.width+"x"+heroTexture.height)+
                " tint="+(heroMaterial!=null&&heroMaterial.HasProperty("_TintColor")?heroMaterial.GetColor("_TintColor").ToString("F3"):"<none>"));
            notes.Add("node6="+nodes[6].name+" sourceVisible="+effect.Program.nodes[6].visible+" layer="+nodes[6].layer+
                " enabled="+heroMask.enabled+" active="+heroMask.gameObject.activeInHierarchy+" bounds="+
                heroMask.bounds.center.ToString("F3")+"/"+heroMask.bounds.size.ToString("F3")+" shader="+effect.Program.nodes[6].materials[0].shader+
                " family="+heroMask.sharedMaterial.GetFloat("_OriginalFamily"));
            notes.Add("node7="+nodes[7].name+" sourceVisible="+effect.Program.nodes[7].visible+" layer="+nodes[7].layer+
                " enabled="+heroGlow.enabled+" active="+heroGlow.gameObject.activeInHierarchy+" bounds="+
                heroGlow.bounds.center.ToString("F3")+"/"+heroGlow.bounds.size.ToString("F3")+" shader="+effect.Program.nodes[7].materials[0].shader+
                " family="+heroGlow.sharedMaterial.GetFloat("_OriginalFamily"));
            if(sourceCapture!=null)notes.Add("offscreenFx="+sourceCapture.Size.ToString("F3")+" captureCameraMask="+sourceCapture.CaptureCamera.cullingMask+
                " texture="+sourceCapture.Texture.width+"x"+sourceCapture.Texture.height);
            else notes.Add("offscreenFx=<missing>");
            var fullSourceAffine=area.RenderAffine(age);var fullSourceTransmission=effect.Transmission;
            notes.Add(SaveDiagnosticTexture(folder,"full-source-affine",fullSourceAffine));
            notes.Add(SaveDiagnosticTexture(folder,"full-source-transmission",fullSourceTransmission));
            notes.Add(SaveDiagnosticTexture(folder,"offscreen-source-rt",sourceCapture==null?null:sourceCapture.Texture));
            notes.Add(SaveDiagnosticTexture(folder,"node9-renderplane-rt",planeTexture));
            notes.Add(SaveDiagnosticTexture(folder,"node11-cardhero-input",heroTexture));
            var heroSourceMaterial=effect.Program.nodes[11].materials[0];
            notes.Add("node11 source passes="+String.Join(",",(heroSourceMaterial.passes??new NativeShaderPass[0]).Select(pass=>pass.index+":"+pass.light_mode).ToArray())+
                " runtime _GlowIntensity="+heroMaterial.GetFloat("_GlowIntensity").ToString("F3")+
                " _GlowAddMul="+heroMaterial.GetFloat("_GlowAddMul").ToString("F3")+
                " _Intensity="+heroMaterial.GetFloat("_Intensity").ToString("F3")+
                " _TintColor="+heroMaterial.GetColor("_TintColor").ToString("F3")+
                " _Color="+heroMaterial.GetColor("_Color").ToString("F3")+
                " mainCameraHDR="+mainCamera.allowHDR+" offscreenCameraHDR="+(sourceCapture!=null&&sourceCapture.CaptureCamera.allowHDR));
            notes.Add(SaveDiagnosticTexture(folder,"node11-glowtransparent-pass",effect.RenderGlow(age)));
            var maskFrame=effect.CaptureSubtreeFrame(age,6,fitAspect:true,meshOnly:true);
            var glowFrame=effect.CaptureSubtreeFrame(age,7,fitAspect:true,meshOnly:true);
            notes.Add(SaveDiagnosticTexture(folder,"node6-isolated-mask",effect.RenderAffineSubtree(age,6,maskFrame)));
            notes.Add(SaveDiagnosticTexture(folder,"node7-isolated-vertex-glow",effect.RenderAffineSubtree(age,7,glowFrame)));
            if(sourceCapture!=null)
            {
                bool maskEnabled=heroMask.enabled,glowEnabled=heroGlow.enabled;
                try
                {
                    heroMask.enabled=false;heroGlow.enabled=false;
                    area.RenderAffine(age);
                    notes.Add(SaveDiagnosticTexture(folder,"offscreen-without-hero-mask-glow",sourceCapture.Texture));
                    heroMask.enabled=maskEnabled;heroGlow.enabled=false;
                    area.RenderAffine(age);
                    notes.Add(SaveDiagnosticTexture(folder,"offscreen-mask-only",sourceCapture.Texture));
                    heroMask.enabled=false;heroGlow.enabled=glowEnabled;
                    area.RenderAffine(age);
                    notes.Add(SaveDiagnosticTexture(folder,"offscreen-vertex-glow-only",sourceCapture.Texture));
                }
                finally{heroMask.enabled=maskEnabled;heroGlow.enabled=glowEnabled;}
            }
            notes.Add(SaveDiagnosticTexture(folder,"node11-preview-camera-isolated",effect.RenderAffineSubtree(age,10,frame)));
            notes.Add(SaveDiagnosticTexture(folder,"node11-board-camera-layer21",heroBoardCamera));
            notes.Add(SaveDiagnosticTexture(folder,"node11-centered-camera-layer21",heroCenteredCamera));
            heroBoardCamera.Release();heroCenteredCamera.Release();
        }
        File.WriteAllLines(Path.Combine(folder,"diagnostic.txt"),notes.ToArray());
        Debug.Log("PALADIN SOURCE GPU DIAGNOSTIC: "+String.Join(" | ",notes.ToArray()));
    }
    static void SourceProjection(AssetRegistry assets,NativeProgram root,NativeEffectEntry entry)
    {
        var sourceCamera=JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json")))
            .cameras.Single(camera=>camera.name=="Main Camera");
        foreach(int seat in new[]{0,1})foreach(bool authoredCamera in new[]{false,true})
        {
            var state=State(1);int source=seat==0?2:4,hero=seat==0?1:3;
            state.players[seat].power.card_id="HERO_04bp";
            var data=new SemanticEvent{type="SpellEffect",source=source,card_id="HERO_04bp",owner=seat};
            var points=new Dictionary<int,Vector2>{{1,new Vector2(817,832)},{2,new Vector2(1120,852)},
                {3,new Vector2(1064,207)},{4,new Vector2(1260,225)}};
            var origin=new Vector3(23000,10000,20000);var viewport=new Rect(0,0,1920,1080);
            var scene=NativeSuperSpellBoard.FromPresentation(data,state,state,id=>points.ContainsKey(id)?(Vector2?)points[id]:null,
                viewport,origin,id=>id.EndsWith("bp",StringComparison.Ordinal)?10:3,authoredCamera?sourceCamera:null);
            NativeAreaPlayback playback=null;bool sceneReleased=false;var release=scene.Dispose;
            scene.Dispose=()=>{sceneReleased=true;release();};
            GameObject referenceObject=null;
            try
            {
                playback=new NativeAreaPlayback(root,entry,assets,3,seat==0,true,scene.Context,null,scene:scene);
                var position=playback.Effect.Nodes[0].transform.position;
                Check(Vector3.Distance(position,scene.Placement.Location(3).transform.position)<.001f,
                    "both controller sides resolve the actual source hero rather than the power or board center");
                var expected=new Vector2(points[hero].x/viewport.width,points[hero].y/viewport.height);
                // 独立重建scene投影相机，分别测量适配器往返误差和clone复制误差。
                var sourceProjection=expected;
                if(authoredCamera)
                {
                    referenceObject=new GameObject("Paladin source scene projection reference");var referenceCamera=referenceObject.AddComponent<Camera>();referenceCamera.enabled=false;
                    scene.SourceCamera.Apply(referenceCamera,scene.SourceCameraOrigin,origin,viewport.width/viewport.height);
                    var referencePoint=referenceCamera.WorldToViewportPoint(scene.Placement.Location(3).transform.position);
                    sourceProjection=new Vector2(referencePoint.x,1-referencePoint.y);
                }
                var sourceErrorPixels=Vector2.Scale(sourceProjection-expected,new Vector2(viewport.width,viewport.height));
                Check(sourceErrorPixels.magnitude<=2f,
                    "the source scene's perspective ray/board-plane/float staging round trip stays within two logical pixels: "+sourceErrorPixels.ToString("F7"));
                string ProjectionState(float age)
                {
                    var camera=playback.Effect.PreviewCamera;
                    var actual=playback.Effect.PreviewPoint(position);
                    return "seat="+seat+" authoredCamera="+authoredCamera+" age="+age+" position="+position.ToString("F7")+
                        " current="+playback.Effect.Nodes[0].transform.position.ToString("F7")+" projected="+actual.ToString("F7")+
                        " expected="+expected.ToString("F7")+" error="+Vector2.Distance(actual,expected).ToString("G9")+
                        " sourceProjected="+sourceProjection.ToString("F7")+" sourceErrorPixels="+sourceErrorPixels.ToString("F7")+
                        " camera="+camera.transform.position.ToString("F7")+" rotation="+camera.transform.rotation.ToString("F7")+
                        " sourceOrigin="+scene.SourceCameraOrigin.ToString("F7")+" captureOrigin="+playback.Effect.Root.transform.position.ToString("F7")+
                        " aspect="+camera.aspect+" ortho="+camera.orthographic+" fov="+camera.fieldOfView+
                        " view="+camera.worldToCameraMatrix.ToString("F7")+" projection="+camera.projectionMatrix.ToString("F7");
                }
                var initializedProjection=playback.Effect.PreviewPoint(position);
                var initializedErrorPixels=Vector2.Scale(initializedProjection-expected,new Vector2(viewport.width,viewport.height));
                Debug.Log("PALADIN SOURCE PROJECTION: "+ProjectionState(-1));
                Check(Vector2.Distance(initializedProjection,sourceProjection)<.0001f&&initializedErrorPixels.magnitude<=2f,
                    "the initialized source hero uses the scene's projection before any forward Seek: "+ProjectionState(-1));
                foreach(float age in new[]{0f,.1f,.35f,.85f,1.5f})
                {
                    playback.Seek(age);
                    var projected=playback.Effect.PreviewPoint(position);
                    var pixelError=Vector2.Scale(projected-expected,new Vector2(viewport.width,viewport.height));
                    Check(Vector3.Distance(playback.Effect.Nodes[0].transform.position,position)<.001f&&
                        Vector2.Distance(projected,sourceProjection)<.0001f&&
                        Vector2.Distance(projected,initializedProjection)<.0001f&&pixelError.magnitude<=2f,
                        "source hero position and camera projection survive initialization and forward Seek: "+ProjectionState(age));
                }
                Check(!sceneReleased,"the projected source scene remains owned throughout visual playback");
            }
            finally{if(playback!=null)playback.Dispose();else scene.Dispose();if(referenceObject!=null)AssetRegistry.ReleaseTransient(referenceObject);}
            Check(sceneReleased,"disposing the FriendlyArea releases its owned source scene");
        }
    }
}
