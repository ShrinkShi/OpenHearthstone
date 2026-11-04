using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public static class NativeDemonHunterHeroPowerChecks
{
    static int passed;
    static int depthWarnings;
    static void Check(bool value,string reason)
    {if(!value)throw new Exception("Demon Hunter hero power: "+reason);passed++;}
    static void ObserveDepthWarning(string condition,string stack,LogType type)
    {if(type==LogType.Warning&&condition.IndexOf("depth",StringComparison.OrdinalIgnoreCase)>=0)depthWarnings++;}
    static int MaxRgbNear(Texture2D image,Vector2 point,int radius)
    {
        int centerX=Mathf.RoundToInt(point.x/1920f*(image.width-1));
        int centerY=image.height-1-Mathf.RoundToInt(point.y/1080f*(image.height-1));
        int maximum=0;
        for(int y=Mathf.Max(0,centerY-radius);y<=Mathf.Min(image.height-1,centerY+radius);y++)
            for(int x=Mathf.Max(0,centerX-radius);x<=Mathf.Min(image.width-1,centerX+radius);x++)
            {
                var color=image.GetPixel(x,y);
                maximum=Mathf.Max(maximum,Mathf.RoundToInt(255*Mathf.Max(color.r,Mathf.Max(color.g,color.b))));
            }
        return maximum;
    }
    static void CheckDemonHunterFramebuffer(NativeEffectPlayer player,NativeSuperSpellPlayback spell,
        Vector2 sourcePoint,Vector2 targetPoint,float age)
    {
        var previous=RenderTexture.active;
        var frame=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        Texture2D pixels=null;frame.Create();var full=new Rect(0,0,1920,1080);
        float playbackAge=NativeDemonHunterHeroPowerPlayback.PlaybackAge(age)+spell.CastOffset;
        try
        {
            RenderTexture.active=frame;GL.Clear(true,true,Color.clear);
            var source=spell.RenderDemonHunterSourceLayer(playbackAge);
            Check(player.RenderAffine(full,source,spell.DemonHunterSourceTransmission,Matrix4x4.identity,1920,1080,Color.white),
                "source layer reaches the full-screen GPU composite");
            var target=spell.RenderDemonHunterTargetLayer(playbackAge);
            Check(player.RenderAffine(spell.DemonHunterTargetBodyRect,target,spell.DemonHunterTargetTransmission,Matrix4x4.identity,1920,1080,Color.white),
                "target layer fits the actual hero portrait in the GPU composite");
            foreach(var clone in spell.Flow.Instances)
            {
                Check(Vector3.Distance(clone.Effect.Root.transform.position,spell.Effect.Root.transform.position)<.001f,
                    "impact clone shares the projected board camera origin");
                Check(Mathf.Abs(clone.Effect.PreviewCamera.transform.position.x-clone.Effect.Root.transform.position.x)<.01f,
                    "the independently captured Impact camera moves with its staging origin");
            }
            pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGBA32,false,true);
            pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);pixels.Apply();
            string frameFolder=Path.GetFullPath("../work/f021-h10-body-gpu");Directory.CreateDirectory(frameFolder);
            File.WriteAllBytes(Path.Combine(frameFolder,"hero-body.png"),pixels.EncodeToPNG());
            Check(spell.Events.Any(value=>value.Contains(" StartClone "))&&spell.SourceGraphActions().ContainsKey("ParticlePlayAction"),
                "the actual Start instance owns the source particle lifecycle, including its early Death transition");
            Check(MaxRgbNear(pixels,targetPoint,48)>8,
                "the final GPU framebuffer contains the target glow around the real hero pixel");
            Check(MaxRgbNear(pixels,targetPoint+new Vector2(0,-80),18)>8&&MaxRgbNear(pixels,targetPoint+new Vector2(0,80),18)>8,
                "the hero-shaped target glow spans the portrait height instead of a compressed center strip");
        }
        finally
        {
            RenderTexture.active=previous;if(pixels!=null)AssetRegistry.ReleaseTransient(pixels);AssetRegistry.ReleaseTransient(frame);
        }
    }

    static StateView State(int version)
    {
        return new StateView{match_id="h10-source-route",version=version,seat=0,active=0,turn=1,phase="play",
            players=new[]{
                new PlayerView{seat=0,mana=version==1?10:9,hero=new EntityView{entity=4,card_id="HERO_10",attack=version==1?0:1,
                    health=30,max_health=30,attack_buffed=version!=1},power=new EntityView{entity=5,card_id=NativeDemonHunterHeroPowerPlayback.HeroPowerId,
                    used=version!=1},hand=new EntityView[0],field=new EntityView[0]},
                new PlayerView{seat=1,mana=10,hero=new EntityView{entity=6,card_id="HERO_08",health=30,max_health=30},
                    power=new EntityView{entity=7,card_id="HERO_08bp"},hand=new EntityView[0],field=new EntityView[0]}}
        };
    }
    static StateView After(StateView before)
    {
        var after=State(2);
        after.events=new[]{
            new SemanticEvent{type="SpendMana",source=5,target=2,owner=0,amount=1},
            new SemanticEvent{type="PlayHeroPower",source=2,target=5,owner=0},
            new SemanticEvent{type="Buff",source=5,target=4,owner=0,amount=0,
                visual=new EntityView{entity=4,card_id="HERO_10",attack=1,health=30,max_health=30,attack_buffed=true}}
        };
        return after;
    }
    static string Hash(string path)
    {using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    static void CopyTree(string source,string target)
    {
        Directory.CreateDirectory(target);
        foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)));
        foreach(string directory in Directory.GetDirectories(source))CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)));
    }

    static void PairContracts()
    {
        var before=State(1);var after=After(before);NativeDemonHunterHeroPowerPlayback.Pair pair;
        Check(NativeDemonHunterHeroPowerPlayback.TryGetPair(after.events[1],before,after,out pair),
            "accepts the actual SpendMana -> PlayHeroPower -> self-hero Buff batch");
        Check(pair.power.entity==5&&pair.hero.entity==4&&pair.ownerIndex==0&&pair.hero.attack==1,
            "returns the authoritative caster power and the same owner's buffed hero");
        Check(!NativeDemonHunterHeroPowerPlayback.TryGetPair(new SemanticEvent{type="PlayHeroPower",source=2,target=7,owner=0},before,after,out pair),
            "rejects a hero power whose source is not the real paired caster power");
        var wrongTarget=After(before);wrongTarget.events[2].target=6;
        Check(!NativeDemonHunterHeroPowerPlayback.TryGetPair(wrongTarget.events[1],before,wrongTarget,out pair),
            "rejects a Buff targeting the opponent hero");
        var wrongAttack=After(before);wrongAttack.players[0].hero.attack=2;wrongAttack.events[2].visual.attack=2;
        Check(!NativeDemonHunterHeroPowerPlayback.TryGetPair(wrongAttack.events[1],before,wrongAttack,out pair),
            "rejects any hero attack result other than 0 -> 1");
        var ambiguous=After(before);ambiguous.events=ambiguous.events.Concat(new[]{new SemanticEvent{type="Hit",source=5,target=6,owner=0}}).ToArray();
        Check(!NativeDemonHunterHeroPowerPlayback.TryGetPair(ambiguous.events[1],before,ambiguous,out pair),
            "rejects a batch with effects outside the single reviewed Buff result");
    }

    public static void RunSourceRoute()
    {
        passed=0;depthWarnings=0;PairContracts();
        float previousAge=-1;
        foreach(float age in new[]{0f,.1f,.5f,1f,2f})
        {
            float mapped=NativeDemonHunterHeroPowerPlayback.PlaybackAge(age);
            Check(mapped>previousAge&&Mathf.Abs(mapped-age*1.15f)<.00001f,
                "H10 source and target playback age is monotonic and 1.15x at "+age.ToString("F2"));
            previousAge=mapped;
        }
        bool rejectedInvalidAge=false;
        try{NativeDemonHunterHeroPowerPlayback.PlaybackAge(float.NaN);}
        catch(ArgumentOutOfRangeException){rejectedInvalidAge=true;}
        Check(rejectedInvalidAge,"H10 playback age rejects a non-finite frame sample");
        string sourceRoot=Path.GetFullPath("../local"),sourceRuntime=Path.Combine(sourceRoot,"runtime");
        string productionIndex=Path.Combine(sourceRuntime,"native-effect-index.json"),productionHash=Hash(productionIndex);
        string testRoot=Path.Combine(Path.GetTempPath(),"HearthstoneH10Route-"+Guid.NewGuid().ToString("N"));
        string testRuntime=Path.Combine(testRoot,"runtime");Directory.CreateDirectory(testRoot);
        Application.logMessageReceived+=ObserveDepthWarning;
        try
        {
            CopyTree(sourceRuntime,testRuntime);
            string rootSource=Path.Combine(testRuntime,"77a947e5d6ef3ec34b9f2f93.hero-power-source.json");
            Check(File.ReadAllText(rootSource).IndexOf("\"property_data\"",StringComparison.Ordinal)<0,
                "the original H10 source artifact has no recursive property_data field to discard");
            var parsedRoot=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            Debug.Log("H10 particle DTO readback: "+NativeDemonHunterHeroPowerPlayback.ParticleProfileSummary(parsedRoot));
            string profileFailure=NativeDemonHunterHeroPowerPlayback.RootProgramFailure(parsedRoot);
            Check(NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(parsedRoot)&&String.IsNullOrEmpty(profileFailure),
                "the H10-only reader failed strict profile at: "+profileFailure);
            var missingParticle=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            missingParticle.nodes[7].particle=null;
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(missingParticle),
                "the H10 source gate rejects a missing authored particle node");
            var changedLayer=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            changedLayer.nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].parent=0;
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(changedLayer),
                "the H10 source gate rejects a merged source/target layer hierarchy");
            var unexpectedParticle=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            unexpectedParticle.nodes[0].particle=unexpectedParticle.nodes[7].particle;
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(unexpectedParticle),
                "the H10 source gate rejects a particle object at an unreviewed node");
            var unrestoredParticle=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            unrestoredParticle.nodes[7].particle.restored=false;
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(unrestoredParticle),
                "the H10 source gate rejects an authored particle that is not restored");
            var changedTexture=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            var textureAction=changedTexture.machines.Single(machine=>machine.owner==12).states
                .Single(state=>state.name=="Hero").actions.First(action=>action.name=="SetMaterialTexture");
            textureAction.Param("texture").asset_path="tampered.png";
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(changedTexture),
                "the H10 source gate rejects changed direct texture parameters");
            var changedScale=NativeDemonHunterHeroPowerPlayback.ReadProgram(rootSource);
            var scaleAction=changedScale.machines.Single(machine=>machine.owner==1).states
                .Single(state=>state.name=="Minion").actions.First(action=>action.name=="SetScale");
            scaleAction.Param("x").number=.71f;
            Check(!NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(changedScale),
                "the H10 source gate rejects changed direct scale parameters");
            string testIndex=Path.Combine(testRuntime,"native-effect-index.json");
            var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(testIndex));
            var entry=index.entries.Single(row=>row.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId);
            Check(entry.enabled&&Mathf.Approximately(entry.duration,.1f)&&!entry.replace,
                "the reviewed H10 importer profile enables only the bounded source/target route");
            var assets=new AssetRegistry(testRoot);var before=State(1);var after=After(before);
            var heroPoint=new Vector2(960,930);var powerPoint=new Vector2(1150,930);
            var opponentHeroPoint=new Vector2(960,140);var opponentPowerPoint=new Vector2(1150,140);
            var locations=new System.Collections.Generic.Dictionary<int,Rect>{{4,new Rect(heroPoint.x-60,heroPoint.y-60,120,120)},
                {5,new Rect(powerPoint.x-60,powerPoint.y-60,120,120)},
                {6,new Rect(opponentHeroPoint.x-60,opponentHeroPoint.y-60,120,120)},
                {7,new Rect(opponentPowerPoint.x-60,opponentPowerPoint.y-60,120,120)}};
            using(var unbound=new NativeEffectPlayer(assets))
                Check(unbound.Duration(NativeDemonHunterHeroPowerPlayback.HeroPowerId)==0,
                    "H10 remains disabled until the real source scene is bound");
            using(var player=new NativeEffectPlayer(assets))
            {
                player.CardType=id=>id=="HERO_10"?3:id==NativeDemonHunterHeroPowerPlayback.HeroPowerId?10:0;
                NativeSuperSpellScene routedScene=null;
                player.SuperSpellScene=(data,oldState,newState,viewport,origin)=>
                    routedScene=NativeSuperSpellBoard.FromPresentation(data,oldState,newState,
                        id=>id==4?heroPoint:id==5?powerPoint:id==6?opponentHeroPoint:id==7?opponentPowerPoint:(Vector2?)null,
                        viewport,origin,player.CardType);
                Check(Mathf.Approximately(player.Duration(NativeDemonHunterHeroPowerPlayback.HeroPowerId),.1f),
                    "exact source profile and imported assets open only the 0.1 second presentation cue");
                var presentation=new Presentation{NativeDuration=player.Duration,NativeActive=player.SourceBusy};
                presentation.NativePrepare=(key,data,oldState,newState,area,age)=>
                {if(!player.PrepareSource(key,data,oldState,newState,area,age))throw new Exception("H10 route did not prepare");};
                presentation.Accept(null,before,locations);presentation.Accept(before,after,locations);
                var cue=presentation.Cues.Single(row=>row.data.type=="SpellEffect"&&row.data.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId);
                Check(cue.data.source==5&&cue.data.target==4&&cue.data.owner==0,
                    "Presentation cue carries the actual power entity and same-owner hero target");
                presentation.PrepareNative();
                string key=after.match_id+":"+after.version+":"+cue.data.source+":"+cue.start;
                var spell=player.SourceSpell(key);
                Check(spell!=null&&!spell.Flow.Faulted&&spell.SourceEntity==5&&!spell.HasOriginalHistory,
                    "production PrepareNative creates the source root with no original History/metadata host");
                int rootSameAgeSkips=spell.Effect.H10SameAgeSeekSkips;
                int rootActions=spell.Effect.ActionExecutions.Values.Sum();
                spell.Effect.Seek(spell.Time);
                Check(spell.Effect.H10SameAgeSeekSkips==rootSameAgeSkips+1&&spell.Effect.ActionExecutions.Values.Sum()==rootActions,
                    "repeated H10 root sampling at the same authored time skips redundant playback work");
                foreach(var clone in spell.Flow.Instances.ToArray())
                {
                    int cloneSameAgeSkips=clone.Effect.H10SameAgeSeekSkips;
                    int cloneActions=clone.Effect.ActionExecutions.Values.Sum();
                    clone.Effect.Seek(clone.PlaybackTime);
                    Check(clone.Effect.H10SameAgeSeekSkips==cloneSameAgeSkips+1&&clone.Effect.ActionExecutions.Values.Sum()==cloneActions,
                        "repeated H10 source/impact clone sampling at the same authored time skips redundant playback work: "+clone.Effect.Program.reference);
                }
                Check(spell.Effect.Program.machines.Length==0,"prefab definitions in the exported closure are not played as duplicate instances");
                var fullScreen=new Rect(0,0,1920,1080);
                Check(spell.DrawRect==fullScreen&&spell.TargetDrawRect==fullScreen,
                    "source and target world-projected layers use one full-screen draw rectangle");
                Vector3 sourceAuto=routedScene.Placement.SourceAuto().transform.position;
                Vector3 heroWorld=routedScene.Placement.Targets.GetTarget().transform.position;
                Check(Vector3.Distance(spell.Effect.Nodes[0].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(spell.Effect.Nodes[1].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(spell.Effect.Nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].transform.position,heroWorld)<.001f,
                    "source and Idle roots use the real power button while the target root uses the same-batch hero");
                Check(!spell.Flow.FirstArrivalTime.HasValue&&!spell.Flow.Flights.Any(),
                    "H10 starts and impacts without a fabricated missile arrival");
                presentation.Seek(.19f);presentation.PrepareNative();
                Check(presentation.InputLocked&&player.SourceBusy(key),
                    "H10 keeps the short input gate before 0.2s while its visual source is active");
                presentation.Seek(.21f);presentation.PrepareNative();
                Check(!presentation.InputLocked&&presentation.Busy&&player.SourceBusy(key),
                    "H10 input unlocks by 0.2s while its source continues as a visual tail");
                for(int frameIndex=1;frameIndex<=29;frameIndex++)
                    Check(player.PrepareSource(key,cue.data,before,after,false,frameIndex/60f),"the H10 frame coroutine advances normally before capture");
                presentation.Seek(.49f);presentation.PrepareNative();
                spell=player.SourceSpell(key);
                Check(spell!=null,"the monotonic H10 source is rebound after the deliberate age-regression restart");
                Check(Mathf.Abs(spell.Time-NativeDemonHunterHeroPowerPlayback.PlaybackAge(.49f)-spell.CastOffset)<.001f&&
                    !spell.Flow.FirstArrivalTime.HasValue,
                    "the 1.15x source reaches the mapped 0.49s sample without waiting for the authored 0.5 GameDelay");
                Check(spell.DrawRect==fullScreen&&spell.TargetDrawRect==fullScreen&&
                    Vector3.Distance(spell.Effect.Nodes[1].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(spell.Effect.Nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].transform.position,heroWorld)<.001f,
                    "both H10 world anchors remain fixed at 0.49s");
                CheckDemonHunterFramebuffer(player,spell,powerPoint,heroPoint,.49f);
                presentation.Seek(.51f);presentation.PrepareNative();
                Check(Mathf.Abs(spell.Time-NativeDemonHunterHeroPowerPlayback.PlaybackAge(.51f)-spell.CastOffset)<.001f&&
                    !spell.Flow.FirstArrivalTime.HasValue,
                    "the mapped source clock passes 0.5s without a metadata wait or Arrival dependency");
                Check(spell.DrawRect==fullScreen&&spell.TargetDrawRect==fullScreen&&
                    Vector3.Distance(spell.Effect.Nodes[1].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(spell.Effect.Nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].transform.position,heroWorld)<.001f,
                    "both H10 world anchors remain fixed at 0.51s");
                for(int sample=11;sample<=52;sample++)
                {
                    float sampleAge=sample*.05f;
                    Check(player.PrepareSource(key,cue.data,before,after,false,sampleAge),
                        "H10 source advances during repeated impact rendering at "+sampleAge.ToString("F2"));
                    float mappedAge=NativeDemonHunterHeroPowerPlayback.PlaybackAge(sampleAge)+spell.CastOffset;
                    spell.RenderDemonHunterSourceLayer(mappedAge);
                    spell.RenderDemonHunterTargetLayer(mappedAge);
                }
                presentation.Seek(2.6f);presentation.PrepareNative();
                Check(spell.Flow.Completed&&spell.Flow.ActiveClones==0&&!player.SourceBusy(key),
                    "repeated rendered frames naturally release the H10 impact clone and source tail by 2.6s");
                Check(!spell.Flow.Faulted&&spell.UnboundControllerActions==0&&spell.UnboundControllerDetails.Count==0,
                    "CardType-selected H10 child states do not create false unbound Spell lifecycle callbacks");
                Check(spell.DrawRect==fullScreen&&spell.TargetDrawRect==fullScreen&&
                    Vector3.Distance(spell.Effect.Nodes[1].transform.position,sourceAuto)<.001f&&
                    Vector3.Distance(spell.Effect.Nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].transform.position,heroWorld)<.001f,
                    "both H10 world anchors remain fixed after the root animation tail");
                Check(spell.Effect.UnboundTextureActions==0&&spell.Effect.UnboundScaleActions==0&&spell.SourceGraphActions().ContainsKey("SetMaterialTexture"),
                    "the actual child instances bind authored textures without replaying the prefab definitions");
            }
            Check(depthWarnings==0,"the H10-only reader emits no Unity JSON depth-limit warning");
            Check(Hash(productionIndex)==productionHash,"the isolated route contract left the production native-effect index byte-for-byte unchanged");
            Debug.Log("NATIVE DEMON HUNTER HERO POWER: "+passed+" strict source-route checks passed");
        }
        finally
        {
            Application.logMessageReceived-=ObserveDepthWarning;
            if(Directory.Exists(testRoot))Directory.Delete(testRoot,true);
        }
    }
}
