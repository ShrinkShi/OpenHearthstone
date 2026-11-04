using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;

public sealed class NativeSuperSpellScene
{
    public NativeSpellObjectSelection Placement;
    public NativeSpellContext Context;
    public Vector2? SourceScreenPoint;
    public Vector2? TargetScreenPoint;
    public Rect? TargetScreenBounds;
    public NativeSuperSpellHistory History;
    public Func<float,float,float> Range;
    public Func<bool> RandomBool;
    public Func<GameObject,bool> TargetInHand;
    public Func<GameObject,Vector3> HandSlot;
    public Action Dispose;
    public Action<int[]> SetTargets;
    public NativeSourceCamera SourceCamera;public Vector3 SourceCameraOrigin;
}

// 与原场景精度分开：当前 GUI 棋盘的明确投影，绝不使用离屏拍卡 Actor 的缓存坐标。
public static class NativeSuperSpellBoard
{
    public static int ResolvePriestTarget(SemanticEvent data,StateView before,StateView after)
    {
        if(data==null||data.card_id!="HERO_09bp"||after==null)
            throw new NotSupportedException("HERO_09bp target authority is unavailable");
        var events=after.events??new SemanticEvent[0];
        var targetEvents=events.Where(e=>e!=null&&e.type=="HeroPowerTarget"&&
            (e.source==data.source||e.card_id=="HERO_09bp")).ToArray();
        int target;
        if(targetEvents.Length>0)
        {
            if(targetEvents.Length!=1)throw new NotSupportedException("HERO_09bp requires one same-batch HeroPowerTarget event");
            var authority=targetEvents[0];
            int owner=data.owner>=0?data.owner:Controller(data,before,after);
            if(authority.source!=data.source||authority.card_id!="HERO_09bp"||authority.owner!=owner||authority.target==0||authority.target==data.source)
                throw new NotSupportedException("HERO_09bp HeroPowerTarget does not match the accepted power source");
            target=authority.target;
            var heals=events.Where(e=>e!=null&&e.type=="Heal"&&e.source==data.source).ToArray();
            if(heals.Length>0&&(heals.Length!=1||heals[0].target==0||heals[0].target!=target))
                throw new NotSupportedException("HERO_09bp HeroPowerTarget conflicts with its same-batch Heal");
            return target;
        }
        var legacy=events.Where(e=>e!=null&&e.type=="Heal"&&e.source==data.source).ToArray();
        if(legacy.Length!=1||legacy[0].target==0||legacy[0].target==data.source)
            throw new NotSupportedException("HERO_09bp requires one actual same-batch Heal target or HeroPowerTarget");
        return legacy[0].target;
    }
    public static Rect ScreenRect(string cardId,Vector2? sourcePoint)
    {
        return new Rect(0,0,1920,1080);
    }
    public static Rect TargetScreenRect(string cardId,Vector2? targetPoint)
    {
        return new Rect(0,0,1920,1080);
    }
    public static Vector2? ResolvePresentationPosition(string cardId,int source,int entity,Func<int,Vector2?> position)
    {
        bool heroPower=cardId!=null&&cardId.StartsWith("HERO_")&&cardId.EndsWith("bp");
        if(entity==source&&heroPower)
            return position==null?null:position(entity);
        if(entity==source&&cardId!="HERO_08bp"&&cardId!="HERO_07bp"&&cardId!="HERO_06bp"&&cardId!="HERO_09bp"&&cardId!="HERO_10bp"&&cardId!="HERO_05bp")
            return new Vector2(960,480);
        return position==null?null:position(entity);
    }
    public static int Controller(SemanticEvent data,StateView before,StateView after)
    {
        foreach(var state in new[]{before,after})if(state!=null)foreach(var player in state.players??new PlayerView[0])
            if(new[]{player.hero,player.power,player.weapon}.Concat(player.hand??new EntityView[0]).Concat(player.field??new EntityView[0]).Any(e=>e!=null&&e.entity==data.source))return player.seat;
        if(data.owner>=0)return data.owner;
        var owners=(after.events??new SemanticEvent[0]).Where(e=>e.type=="SpendMana"&&e.source==data.source&&e.owner>=0).Select(e=>e.owner).Distinct().ToArray();
        if(owners.Length!=1)throw new NotSupportedException("SuperSpell public source controller is unavailable");return owners[0];
    }
    public static Vector3 Project(Vector2 point,Rect viewport,Vector3 origin)
    {float span=32/viewport.height;return origin+new Vector3((point.x-viewport.center.x)*span,0,(viewport.center.y-point.y)*span);}
    public static NativeSuperSpellScene FromPresentation(SemanticEvent data,StateView before,StateView after,
        Func<int,Vector2?> position,Rect viewport,Vector3 origin,Func<string,int> cardType,NativeSourceCamera sourceCamera=null,Func<int,Rect?> bounds=null)
    {
        if(position==null||cardType==null)throw new NotSupportedException("SuperSpell board projection is unbound");
        int sourceOwner=Controller(data,before,after);
        var entities=(before.players??new PlayerView[0]).Concat(after.players??new PlayerView[0]);
        Func<PlayerView,IEnumerable<EntityView>> all=p=>new[]{p.hero,p.power,p.weapon}.Concat(p.hand??new EntityView[0]).Concat(p.field??new EntityView[0]).Where(e=>e!=null);
        var players=entities.ToArray();var records=new Dictionary<int,NativeSpellEntityObject>();var objects=new List<GameObject>();Vector2? sourceScreenPoint=null;
        Camera projection=null;Vector3 cameraOrigin=Vector3.zero;
        Vector3 ProjectPoint(Vector2 point)
        {
            if(projection==null)return Project(point,viewport,origin);
            var ray=projection.ViewportPointToRay(new Vector3((point.x-viewport.x)/viewport.width,1-(point.y-viewport.y)/viewport.height,0));float distance;
            if(!new Plane(Vector3.up,origin).Raycast(ray,out distance))throw new NotSupportedException("Fireblast GUI point misses explicit board plane");return ray.GetPoint(distance);
        }
        NativeSpellEntityObject Record(int id)
        {
            if(id==0)return null;
            NativeSpellEntityObject cached;if(records.TryGetValue(id,out cached))return cached;
            var entity=players.SelectMany(all).FirstOrDefault(e=>e.entity==id);
            // 对手手牌未公开时，CardPlay 给实体/卡牌，SpendMana 给来源控制方。
            if(entity==null&&id==data.source&&!String.IsNullOrEmpty(data.card_id)&&players.Any(p=>p.seat==sourceOwner))
                entity=new EntityView{entity=id,card_id=data.card_id};
            if(entity==null)throw new InvalidOperationException("SuperSpell board entity is unavailable: "+id);
            var owner=players.FirstOrDefault(p=>all(p).Any(e=>e.entity==id))??players.First(p=>id==data.source&&p.seat==sourceOwner);
            var point=id==data.source&&data.card_id=="HERO_04bp"&&owner.hero!=null?position(owner.hero.entity):position(id);
            if(!point.HasValue)throw new NotSupportedException("SuperSpell board position is unavailable: "+id);
            if(id==data.source)sourceScreenPoint=point;
            var node=new GameObject("SuperSpell scene entity "+id);objects.Add(node);node.transform.position=ProjectPoint(point.Value);
            cached=new NativeSpellEntityObject{entity=id,type=cardType(entity.card_id),card_id=entity.card_id,controllerSide=owner.seat,resolve=()=>node,
                hero=()=>Record(owner.hero.entity).Object,power=()=>owner.power==null?null:Record(owner.power.entity).Object};records[id]=cached;return cached;
        }
        try
        {
            if(sourceCamera!=null)
            {
                var cameraObject=new GameObject("Fireblast source camera board adapter");objects.Add(cameraObject);projection=cameraObject.AddComponent<Camera>();projection.enabled=false;
                cameraOrigin=new Vector3(sourceCamera.Position.x,0,0);sourceCamera.Apply(projection,cameraOrigin,origin,viewport.width/viewport.height);
            }
            var placement=new NativeSpellObjectSelection{source=Record(data.source)};
            // 这里只收集本项目已结算批次的公开视觉目标，不生成原历史或 metadata。
            int[] targets;
            if(data.card_id=="HERO_09bp")
            {
                bool selecting=data.type=="HeroPowerSelection"&&before!=null&&after!=null&&
                    before.match_id==after.match_id&&before.version==after.version;
                targets=selecting?new int[0]:new[]{ResolvePriestTarget(data,before,after)};
            }
            else targets=data.target!=0?new[]{data.target}:(after.events??new SemanticEvent[0])
                .Where(e=>e.type=="Hit"&&e.source==data.source).Select(e=>e.target).Distinct().ToArray();
            placement.SetTargets(targets.Select(Record));
            if(placement.source==null)throw new NotSupportedException("SuperSpell source entity is unavailable");
            var target=targets.Length==0?null:Record(targets[0]);
            var ownerHero=placement.source.hero();
            var context=new NativeSpellContext(placement.source.type,target==null?0:target.type,heroType:3,placement:placement,
                actorObjects:role=>role==0?placement.source.Object:role==1?target?.Object:role==2?ownerHero:null,
                sourceCardId:placement.source.card_id,targetCardId:target?.card_id);
            NativeSuperSpellScene scene=null;
            scene=new NativeSuperSpellScene{Placement=placement,Context=context,SourceScreenPoint=sourceScreenPoint,
                TargetScreenPoint=target==null?null:position(target.entity),SourceCamera=sourceCamera,SourceCameraOrigin=cameraOrigin,
                TargetScreenBounds=target==null||bounds==null?(Rect?)null:bounds(target.entity),
                SetTargets=ids=>
                {
                    placement.SetTargets(ids.Select(Record));
                    target=placement.TargetRecord(placement.Targets.GetTarget());
                    context.SetTargetCardId(target?.card_id);
                    scene.TargetScreenPoint=target==null?null:position(target.entity);
                    scene.TargetScreenBounds=target==null||bounds==null?(Rect?)null:bounds(target.entity);
                },
                TargetInHand=obj=>records.Values.Any(r=>r.Object==obj&&players.Any(p=>(p.hand??new EntityView[0]).Any(e=>e.entity==r.entity))),
                Dispose=()=>{foreach(var obj in objects)AssetRegistry.ReleaseTransient(obj);}};
            return scene;
        }
        catch{foreach(var obj in objects)AssetRegistry.ReleaseTransient(obj);throw;}
    }
}

public sealed class NativeSuperSpellPlayback : IDisposable
{
    const string DruidFangImpactReference="ReuseFX_Fang_Impact_01_Gold.prefab:8e4c202f8ab4b3c46a913adf107abcff";
    [Serializable] sealed class Asset{public string reference,file;}
    [Serializable] sealed class Catalog{public NativeSuperSpellConfig[] programs;public Asset[] prefabs;}
    readonly NativeSuperSpellScene scene;
    readonly List<NativeAreaInstance> owned=new List<NativeAreaInstance>();
    public readonly NativeEffectInstance Effect;
    public readonly NativeSuperSpell Flow;
    public Action<string,float> DemonHunterTimingDiagnostic;
    public bool HasOriginalHistory{get{return scene!=null&&scene.History!=null;}}
    public readonly List<string> Events=new List<string>();
    public readonly NativeSpellContext Context;
    public int SourceEntity{get;private set;}
    public string CardId{get;private set;}
    public Rect DrawRect{get{return NativeSuperSpellBoard.ScreenRect(CardId,scene==null?null:scene.SourceScreenPoint);}}
    public Rect TargetDrawRect{get{return NativeSuperSpellBoard.TargetScreenRect(CardId,scene==null?null:scene.TargetScreenPoint);}}
    public Vector2? TargetScreenPoint{get{return scene==null?null:scene.TargetScreenPoint;}}
    public Rect DemonHunterTargetBodyRect
    {
        get
        {
            var point=scene?.TargetScreenPoint??Vector2.zero;
            var bounds=scene?.TargetScreenBounds??new Rect(point.x-95,point.y-100,190,200);
            return bounds;
        }
    }
    public string MatchId{get;private set;}
    public float CastOffset{get;private set;}
    public int UnboundControllerActions{get;private set;}
    public readonly List<string> UnboundControllerDetails=new List<string>();
    public int ParentUnhandledActions{get{return Effect==null?0:Effect.UnhandledActions;}}
    public int ImpactCloneUnhandledActions{get;private set;}
    public int UnhandledActions{get{return ParentUnhandledActions+ImpactCloneUnhandledActions;}}
    public int UnboundAudioActions{get;private set;}
    public int UnboundComponentActions{get;private set;}
    public int MissingAudioPlaybackServices{get;private set;}
    public int ParentCameraShakerStarts{get{return Effect==null?0:Effect.CameraShakerStarts;}}
    public int ImpactCloneCameraShakerStarts{get;private set;}
    public int CameraShakerStarts{get{return ParentCameraShakerStarts+ImpactCloneCameraShakerStarts;}}
    public readonly Dictionary<string,int> ActionExecutions=new Dictionary<string,int>();
    public readonly List<string> ImpactCloneUnhandledActionDetails=new List<string>();
    public static string[] SourceSimulationSpaceOverrides(string cardId,NativeSuperSpellConfig config,string reference,NativeProgram program)
    {
        if(config==null||program==null)return null;
        if(reference==config.missile_reference)
            return new[]{"FireParticleLeft","FireParticleFill","FireParticleRight","Sparks01","Smoke01"};
        if(cardId=="HERO_05bp"&&config.impact!=null&&config.impact.prefab!=null&&
            reference==config.impact.prefab.prefab_reference)
            return program.nodes.Where(node=>node.particle!=null&&node.particle.restored).Select(node=>node.name).Distinct().ToArray();
        return null;
    }
    public IEnumerable<string> ParentUnhandledActionDetails{get{return Effect==null?Enumerable.Empty<string>():Effect.UnhandledActionDetails.Select(detail=>Effect.Program.reference+" | "+detail);}}
    public IEnumerable<string> UnhandledActionDetails{get{return ParentUnhandledActionDetails.Concat(ImpactCloneUnhandledActionDetails);}}
    public string PositionDiagnostics()
    {
        string Position(GameObject value){return value==null?"null":value.transform.position.ToString("F2");}
        string Describe(NativeAreaInstance instance)
        {
            var effect=instance.Effect;
            var particles=effect.Particles.Select((particle,index)=>new{particle,index}).Where(item=>item.particle!=null).Select(item=>
            {
                var node=effect.Nodes[item.index];var renderer=node.GetComponent<ParticleSystemRenderer>();
                var bounds=renderer==null?new Bounds():renderer.bounds;
                var viewport=effect.PreviewCamera==null?Vector3.zero:effect.PreviewCamera.WorldToViewportPoint(bounds.center);
                var main=item.particle.main;var samples=new ParticleSystem.Particle[Mathf.Min(3,item.particle.particleCount)];
                int sampleCount=samples.Length==0?0:item.particle.GetParticles(samples);
                string sample=String.Join(";",Enumerable.Range(0,sampleCount).Select(i=>
                {
                    var position=samples[i].position;var world=main.simulationSpace==ParticleSystemSimulationSpace.Local?node.transform.TransformPoint(position):position;
                    return position.ToString("F2")+"@"+world.ToString("F2");
                }).ToArray());
                return effect.Nodes[item.index].name+":"+item.particle.particleCount+"/"+item.particle.isPlaying+
                    "/renderer="+(renderer!=null&&renderer.enabled)+"/visible="+(renderer!=null&&renderer.isVisible)+
                    "/space="+main.simulationSpace+"/node="+node.transform.position.ToString("F2")+"/local="+node.transform.localPosition.ToString("F2")+
                    "/parent="+(node.transform.parent==null?"null":node.transform.parent.position.ToString("F2"))+
                    "/bounds="+bounds.size.ToString("F2")+"@"+bounds.center.ToString("F2")+"/view="+viewport.ToString("F2")+"/sample="+sample;
            });
            var actions=effect.ActionExecutions.OrderBy(pair=>pair.Key).Select(pair=>pair.Key+"="+pair.Value);
            var root=effect.Nodes==null||effect.Nodes.Length==0?null:effect.Nodes[0];
            return effect.Program.reference+"="+Position(root)+" states=["+String.Join(",",effect.CurrentStates)+"] actions=["+
                String.Join(",",actions.ToArray())+"] particles=["+String.Join(",",particles.ToArray())+"]";
        }
        var source=scene?.Placement?.source?.Object;
        var targets=scene==null||scene.Placement==null?new GameObject[0]:scene.Placement.Targets.GetTargets().ToArray();
        var clones=Flow==null?Enumerable.Empty<string>():Flow.Instances.Select(Describe);
        return "source="+Position(source)+" root="+Position(Effect==null||Effect.Nodes==null||Effect.Nodes.Length==0?null:Effect.Nodes[0])+" targets=["+
            String.Join(",",targets.Select(Position).ToArray())+"] clones=["+String.Join(",",clones.ToArray())+"]";
    }
    float sourceOnlyTime;
    Vector3 sourcePresentationPosition;
    bool hasSourcePresentationPosition;
    Vector3 targetPresentationPosition;
    bool hasTargetPresentationPosition;
    NativeEffectInstance.SubtreeFrame? demonHunterBodyFrame;
    NativeEffectInstance demonHunterRenderedTarget;
    NativeEffectInstance demonHunterRenderedSource;
    public RenderTexture DemonHunterTargetTransmission{get{return (demonHunterRenderedTarget??Effect).Transmission;}}
    public RenderTexture DemonHunterSourceTransmission{get{return (demonHunterRenderedSource??Effect).Transmission;}}
    public Dictionary<string,int> SourceGraphActions()
    {
        var result=new Dictionary<string,int>(ActionExecutions);
        foreach(var instance in new[]{Effect}.Concat(Flow==null?Enumerable.Empty<NativeEffectInstance>():Flow.Instances.Select(clone=>clone.Effect)))
            foreach(var action in instance.ActionExecutions){int count;result.TryGetValue(action.Key,out count);result[action.Key]=count+action.Value;}
        return result;
    }
    public float Time{get{return Flow==null?sourceOnlyTime:Flow.Time;}}
    public bool Busy{get{return Flow!=null&&!Flow.Cancelled&&!Flow.Faulted&&(!Flow.Completed||Flow.ActiveClones>0);}}
    public NativeAreaInstance DruidHeroPowerButtonClone
    {
        get
        {
            if(CardId!="HERO_06bp"||Flow==null||Context==null||Context.Placement==null||Context.Placement.source==null)return null;
            var matches=Flow.Instances.Where(clone=>clone.Effect!=null&&clone.Effect.Program!=null&&
                clone.Effect.Program.reference==DruidFangImpactReference).ToArray();
            if(matches.Length!=1||!SupportsDruidFangButtonProgram(matches[0].Effect.Program)||
               !ReferenceEquals(matches[0].Source,Context.Placement.source.Object))return null;
            return matches[0];
        }
    }
    public bool SourceRootPending
    {
        get
        {
            if(CardId!="HERO_07bp"||Flow!=null||Effect==null)return false;
            int waits,states;Effect.ActionExecutions.TryGetValue("Wait",out waits);Effect.ActionExecutions.TryGetValue("SpellStateAction",out states);
            // Keep sampling the authored four-second FSM through its NoneState transition, with a bounded fallback.
            return (waits!=1||states!=2)&&Time<4.5f;
        }
    }
    public static void BindVisualTargets(NativeSuperSpellConfig config,NativeSuperSpellScene scene)
    {
        if(config==null||scene==null)throw new ArgumentNullException(config==null?nameof(config):nameof(scene));
        if(config.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId)
        {
            NativeDemonHunterHeroPowerPlayback.Pair pair;
            if(!NativeDemonHunterHeroPowerPlayback.SupportsConfig(config)||scene.Placement==null||scene.Placement.source==null||
                scene.Placement.Targets==null||scene.Placement.Targets.GetTargets().Count()!=1||
                scene.Placement.Targets.GetTarget()==null||scene.Placement.Targets.GetTarget()==scene.Placement.source.Object)
                throw new NotSupportedException("HERO_10bp requires its reviewed source and same-batch distinct self-hero visual target");
            return;
        }
        if(config.target_behavior==0)return;
        if(!NativeSuperSpell.SupportsNoMissileUntargeted(config)||scene.Placement==null||scene.Placement.source==null||
            scene.Placement.source.entity==0||scene.SetTargets==null)
            throw new NotSupportedException("Unreviewed SuperSpell visual-target behavior");
        // Official target behavior 6 adds the source as the only visual target.
        scene.SetTargets(new[]{scene.Placement.source.entity});
    }
    public static bool SupportsDruidFangButtonProgram(NativeProgram program)
    {
        if(program==null||program.reference!=DruidFangImpactReference||program.nodes==null||program.nodes.Length!=5)return false;
        var nodes=program.nodes;
        if(nodes.Any(node=>node==null))return false;
        return nodes[0].name=="ReuseFX_Fang_Impact_01_Gold"&&nodes[0].parent==-1&&
            nodes[1].name=="Fangs"&&nodes[1].parent==0&&
            nodes[2].name=="FangsBottom_mesh"&&nodes[2].parent==1&&!String.IsNullOrEmpty(nodes[2].mesh)&&nodes[2].materials!=null&&nodes[2].materials.Length==1&&
            nodes[3].name=="FangsTop_mesh"&&nodes[3].parent==1&&!String.IsNullOrEmpty(nodes[3].mesh)&&nodes[3].materials!=null&&nodes[3].materials.Length==1&&
            nodes[4].name=="FX_RedPhase"&&nodes[4].parent==0&&nodes[4].particle!=null;
    }
    public static bool SupportsWarriorHeroPowerRoot(NativeProgram program)
    {
        try{ValidateWarriorHeroPowerRoot(program);return true;}catch{return false;}
    }
    public static bool SupportsPriestHeroPowerRoot(NativeProgram root,NativeSuperSpellConfig config,
        NativeProgram start,NativeProgram impact,string assetsRoot)
    {
        try{ValidatePriestHeroPowerRoot(root,config,start,impact,assetsRoot);return true;}
        catch(Exception exception)
        {
#if UNITY_EDITOR
            Debug.LogWarning("HERO_09bp source profile rejected: "+exception.Message);
#endif
            return false;
        }
    }
    static void ValidatePriestHeroPowerRoot(NativeProgram root,NativeSuperSpellConfig config,
        NativeProgram start,NativeProgram impact,string assetsRoot)
    {
        const string rootRef="Holy_Targeted_PriestHeroPower_FX.prefab:9a1243a1e217073439fc02475eba1605";
        const string startRef="ReuseFX_Holy_Idle_HeroPower.prefab:81346ada619d6044eaa929c6ac9f17c0";
        const string impactRef="ReuseFX_Holy_Impact_01.prefab:72e94a2cb20f5f243acd6d7e4dab17e1";
        string[] rootActions={"AnimateColor","AudioPlayAction","AudioPlayClipAction","SetColorValue","SetProperty","SpellCardIdAudioPlayAction"};
        string[] startActions={"AnimateColor","AudioPlayAction","AudioPlayClipAction","SetColorValue","SetProperty"};
        string[] impactActions={"SpellCardIdAudioPlayAction"};
        if(root==null||config==null||start==null||impact==null||String.IsNullOrEmpty(assetsRoot)||
            !NativeSuperSpell.SupportsPriestNoMissileTargeted(config)||root.reference!=rootRef||root.nodes==null||root.nodes.Length!=22||
            start.reference!=startRef||start.nodes==null||start.nodes.Length!=12||impact.reference!=impactRef||impact.nodes==null||impact.nodes.Length!=9)
            throw new NotSupportedException("HERO_09bp source identities or targeted root config changed");
        string[] Sorted(string[] values){return (values??new string[0]).OrderBy(value=>value,StringComparer.Ordinal).ToArray();}
        if(!Sorted(root.unknown_actions).SequenceEqual(Sorted(rootActions))||
            !Sorted(start.unknown_actions).SequenceEqual(Sorted(startActions))||
            !Sorted(impact.unknown_actions).SequenceEqual(Sorted(impactActions)))
            throw new NotSupportedException("HERO_09bp source action inventory changed");
        void Parameters(NativeProgram program)
        {
            if(program.machines==null)throw new NotSupportedException("HERO_09bp source FSMs are missing");
            foreach(var machine in program.machines)
            {
                if(machine==null||machine.states==null||machine.events==null)throw new NotSupportedException("HERO_09bp source FSM contains null data");
                foreach(var state in machine.states)
                {
                    if(state==null||state.actions==null||state.transitions==null||state.actions.Any(action=>action==null||action.@params==null||
                        action.@params.Any(parameter=>parameter==null||parameter.unresolved))||state.transitions.Any(transition=>transition==null))
                        throw new NotSupportedException("HERO_09bp has a null or unresolved source action/transition");
                }
            }
        }
        Parameters(root);Parameters(start);Parameters(impact);
        if(root.machines.Length!=2||!root.machines.All(machine=>machine.owner_known)||
            !root.machines.Select(machine=>machine.owner).OrderBy(owner=>owner).SequenceEqual(new[]{1,13})||
            !root.machines.Single(machine=>machine.owner==1).states.Select(state=>state.name).OrderBy(name=>name,StringComparer.Ordinal)
                .SequenceEqual(new[]{"ActionFinish","ActionState","BirthAnimation","BirthState","CancelState","CancelState2","DeathFadeOut","DeathState","Idle","NoneState","State 1"}.OrderBy(name=>name,StringComparer.Ordinal))||
            !root.machines.Single(machine=>machine.owner==13).states.Select(state=>state.name).OrderBy(name=>name,StringComparer.Ordinal)
                .SequenceEqual(new[]{"DeathState","NoneState","State 1"}.OrderBy(name=>name,StringComparer.Ordinal)))
            throw new NotSupportedException("HERO_09bp root FSM owners or state graph changed");
        if(start.machines.Length!=1||impact.machines.Length!=1)
            throw new NotSupportedException("HERO_09bp Start/Impact must retain one source FSM each");
        var startMachine=start.machines.SingleOrDefault(machine=>machine.owner_known&&machine.owner==0);
        var startStates=new Dictionary<string,string[]>
        {
            {"State 1",new string[0]},
            {"BirthState",new[]{"SpellStateAction","SetVisibility","SetColorValue","SetMaterialColor","ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","SetVisibility","SetVisibility","AudioPlayClipAction"}},
            {"DeathState",new[]{"SpellStateAction","ParticleStopAction","ParticleStopAction","ParticleStopAction"}},
            {"NoneState",new[]{"SpellStateAction"}},
            {"CancelState",new[]{"SpellStateAction","ParticleStopAction","ParticleStopAction","ParticleStopAction"}},
            {"BirthAnimation",new[]{"AnimateFloat","SetColorRGBA","SetMaterialColor","AnimateFloat","SetProperty","SetProperty","SetProperty","AnimateColor","SetMaterialColor","AnimateFloat","SetMaterialFloat","AnimateFloat","AnimateFloat","SetMaterialFloat","AnimateFloat","SetProperty","AudioPlayAction","AnimateFloat","AudioSetVolumeAction","Wait"}},
            {"DeathFadeOut",new[]{"SpellFinishAction","AnimateFloat","SetColorRGBA","SetMaterialColor","AnimateFloat","SetProperty","SetProperty","AnimateFloat","SetProperty","AnimateColor","SetMaterialColor","AnimateFloat","SetMaterialFloat","AnimateFloat","SetMaterialFloat","Wait","AnimateFloat","AudioSetVolumeAction"}},
            {"CancelState2",new[]{"SpellFinishAction","AnimateFloat","SetColorRGBA","SetMaterialColor","AnimateFloat","SetProperty","SetProperty","AnimateFloat","SetProperty","AnimateColor","SetMaterialColor","AnimateFloat","SetMaterialFloat","AnimateFloat","SetMaterialFloat","Wait","AnimateFloat","AudioSetVolumeAction"}},
            {"ActionState",new[]{"SpellStateAction","Wait"}},
            {"ActionFinish",new[]{"SpellFinishAction"}},
            {"Idle",new[]{"SpellStateAction"}},
        };
        var startTransitions=new Dictionary<string,string[]>
        {
            {"State 1",new string[0]},{"BirthState",new[]{"FINISHED>BirthAnimation"}},
            {"DeathState",new[]{"FINISHED>DeathFadeOut"}},{"NoneState",new string[0]},
            {"CancelState",new[]{"FINISHED>CancelState2"}},{"BirthAnimation",new[]{"FINISHED>Idle"}},
            {"DeathFadeOut",new[]{"FINISHED>NoneState"}},{"CancelState2",new[]{"FINISHED>NoneState"}},
            {"ActionState",new[]{"FINISHED>ActionFinish"}},{"ActionFinish",new string[0]},{"Idle",new string[0]},
        };
        void Machine(NativeMachine machine,Dictionary<string,string[]> expected,string entry,string[] events)
        {
            if(machine==null||machine.start!=entry||machine.states.Length!=expected.Count||
                !machine.states.Select(state=>state.name).OrderBy(name=>name,StringComparer.Ordinal).SequenceEqual(expected.Keys.OrderBy(name=>name,StringComparer.Ordinal))||
                !machine.events.Select(item=>item.@event+">"+item.state).OrderBy(value=>value,StringComparer.Ordinal)
                    .SequenceEqual(events.OrderBy(value=>value,StringComparer.Ordinal)))
                throw new NotSupportedException("HERO_09bp source FSM entry or lifecycle changed");
            foreach(var state in machine.states)
            {
                if(!state.actions.Select(action=>action.name).SequenceEqual(expected[state.name]))
                    throw new NotSupportedException("HERO_09bp source action order changed: "+state.name);
                var transitions=state.transitions.Select(item=>item.@event+">"+item.state).ToArray();
                if(!transitions.SequenceEqual(startTransitions[state.name]))
                    throw new NotSupportedException("HERO_09bp source transition changed: "+state.name);
            }
        }
        Machine(startMachine,startStates,"State 1",new[]{"Idle>Idle","Action>ActionState","Birth>BirthState","Death>DeathState","None>NoneState","Cancel>CancelState"});
        var impactMachine=impact.machines.SingleOrDefault(machine=>machine.owner_known&&machine.owner==0);
        var impactStates=new Dictionary<string,string[]>
        {
            {"State 1",new string[0]},
            {"DeathState",new[]{"SpellStateAction","SpellFinishAction","ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","AnimateFloat","PlayAnimation","SetVisibility","Wait","SpellCardIdAudioPlayAction"}},
            {"NoneState",new[]{"SpellStateAction"}},
        };
        if(impactMachine==null||impactMachine.start!="State 1"||impactMachine.states.Length!=impactStates.Count||
            !impactMachine.states.Select(state=>state.name).OrderBy(name=>name,StringComparer.Ordinal).SequenceEqual(impactStates.Keys.OrderBy(name=>name,StringComparer.Ordinal))||
            !impactMachine.events.Select(item=>item.@event+">"+item.state).OrderBy(value=>value,StringComparer.Ordinal)
                .SequenceEqual(new[]{"Death>DeathState","None>NoneState"}.OrderBy(value=>value,StringComparer.Ordinal)))
            throw new NotSupportedException("HERO_09bp impact FSM entry or lifecycle changed");
        foreach(var state in impactMachine.states)
        {
            if(!state.actions.Select(action=>action.name).SequenceEqual(impactStates[state.name]))
                throw new NotSupportedException("HERO_09bp impact action order changed: "+state.name);
            var transitions=state.transitions.Select(item=>item.@event+">"+item.state).ToArray();
            var expected=state.name=="DeathState"?new[]{"FINISHED>NoneState"}:new string[0];
            if(!transitions.SequenceEqual(expected))throw new NotSupportedException("HERO_09bp impact transition changed: "+state.name);
        }
        var setColor=startMachine.states.Single(state=>state.name=="BirthState").actions.Single(action=>action.name=="SetColorValue");
        if(setColor.@params.Length!=3||!setColor.@params.Select(parameter=>parameter.name).SequenceEqual(new[]{"colorVariable","color","everyFrame"})||
            setColor.Param("colorVariable").variable!="RaysGlow"||setColor.Param("colorVariable").is_none||
            setColor.Param("colorVariable").color!=new Color(0f,1f,0f,120f/255f)||
            setColor.Param("color").kind!=25||!setColor.Param("color").none_known||setColor.Param("color").is_none||
            setColor.Param("color").color!=Color.clear||
            setColor.Param("everyFrame").number!=0)
            throw new NotSupportedException("HERO_09bp SetColorValue is outside its reviewed constant profile");
        var clip=startMachine.states.Single(state=>state.name=="BirthState").actions.Single(action=>action.name=="AudioPlayClipAction");
        var clipSound=clip.Param("m_Sound");
        if(!clip.@params.Select(parameter=>parameter.name).SequenceEqual(new[]{"m_ParentObject","m_Sound","m_Volume","m_Pitch","m_SpatialBlend","m_Category","m_Delay","m_TemplateSource",
                "m_IsMinionSummonVO","m_InstanceLimited","m_InstanceLimitedDuration","m_InstanceLimitMaximum","m_LimitMaxOutOption"})||
            clipSound.kind!=24||clipSound.object_null||clipSound.asset_type!="MonoBehaviour"||
            clipSound.asset_path!="f279bf35ff1330067ee528e6.native-sound.json"||clip.Param("m_ParentObject").node!=0||
            !clip.Param("m_TemplateSource").object_null||clip.Param("m_Category").number!=0||clip.Param("m_Delay").number!=0||
            clip.Param("m_Volume").number!=1||clip.Param("m_Pitch").number!=1||clip.Param("m_SpatialBlend").number!=1||
            clip.Param("m_IsMinionSummonVO").number!=0||clip.Param("m_InstanceLimited").number!=0||
            clip.Param("m_InstanceLimitedDuration").number!=0||clip.Param("m_InstanceLimitMaximum").number!=1||
            clip.Param("m_LimitMaxOutOption").number!=0)
            throw new NotSupportedException("HERO_09bp Start clip action parameters changed");
        var cardAudio=impactMachine.states.Single(state=>state.name=="DeathState").actions.Single(action=>action.name=="SpellCardIdAudioPlayAction");
        var idsHeader=cardAudio.Param("m_CardIds");var soundsHeader=cardAudio.Param("m_Sounds");
        int idsIndex=Array.IndexOf(cardAudio.@params,idsHeader),soundsIndex=Array.IndexOf(cardAudio.@params,soundsHeader);
        var ids=cardAudio.@params.Skip(idsIndex+1).Take(soundsIndex-idsIndex-1).Select(parameter=>parameter.text).ToArray();
        if(cardAudio.Param("m_WhichCard").number!=0||idsHeader.array_count!=7||soundsHeader.array_count!=7||
            !ids.SequenceEqual(new[]{"OG_234","CFM_604","CFM_120","LOOT_088","GIL_118","CFM_662","DAL_544"})||
            ids.Contains("HERO_09bp")||cardAudio.Param("m_DefaultSound").asset_path!="996fde0a41fbfca79f31f84b.native-sound.json"||
            cardAudio.Param("m_VolumeScale").number!=1||cardAudio.Param("m_Delay").number!=0||cardAudio.Param("m_WaitForFinish").number!=0)
            throw new NotSupportedException("HERO_09bp Impact source-card audio selector changed");
        void Sound(string file,string reference,string output)
        {
            string path=Path.Combine(assetsRoot,file);
            if(!File.Exists(path))throw new NotSupportedException("HERO_09bp sound definition is missing: "+file);
            var sound=JsonUtility.FromJson<NativeSoundAsset>(File.ReadAllText(path));
            if(sound.values==null||sound.values.m_AudioClip!=reference||sound.clips==null||sound.clips.Length!=1||
                sound.clips[0].reference!=reference||!File.Exists(Path.Combine(assetsRoot,sound.clips[0].file))||
                sound.clips[0].file!=output)
                throw new NotSupportedException("HERO_09bp sound clip export is incomplete: "+file);
        }
        Sound(clipSound.asset_path,"Holy_Targeted_Start_01.wav:a1ed1dc4df5e8574d9e3109c074342af","0b172af97e533a48568c40c7.wav");
        Sound(cardAudio.Param("m_DefaultSound").asset_path,"spell_HolyLight_cast_1.wav:3009e1f6dd930044f8f886e79e031b50","af37262f5987b240bfd815c2.wav");
        int selected;bool useDefault;
        if(!NativeAudio.TrySelectCardIdSound(0,"HERO_09bp",null,new[]{"OG_234","CFM_604","CFM_120","LOOT_088","GIL_118","CFM_662","DAL_544"},7,out selected,out useDefault)||!useDefault)
            throw new NotSupportedException("HERO_09bp must use the authored default Impact sound");
    }
    static void ValidateWarriorHeroPowerRoot(NativeProgram program)
    {
        if(program==null||program.nodes==null||program.nodes.Length<2||program.machines==null||program.machines.Length!=1||
            program.nodes[1].name!="ReuseFX_Generic_Untargeted_Cast"||String.IsNullOrEmpty(program.audio_file))
            throw new NotSupportedException("HERO_01bp root identity, audio import or node graph is incomplete");
        if((program.unknown_actions??new string[0]).Distinct().OrderBy(name=>name)
            .SequenceEqual(new[]{"AudioPlayClipAction","InvokeMethod"}.OrderBy(name=>name))==false)
            throw new NotSupportedException("HERO_01bp root contains an unsupported source action");
        var machine=program.machines.SingleOrDefault(row=>row.owner_known&&row.owner==1);
        var expectedStateNames=new[]{"State 1","DeathState","DeathState2","DeathState3","NoneState"};
        if(machine==null||machine.start!="State 1"||machine.states==null||machine.states.Length!=expectedStateNames.Length||
            !machine.states.Select(row=>row.name).OrderBy(name=>name).SequenceEqual(expectedStateNames.OrderBy(name=>name))||
            machine.events==null||machine.events.Length!=2||
            !machine.events.Any(row=>row.@event=="Death"&&row.state=="DeathState")||
            !machine.events.Any(row=>row.@event=="None"&&row.state=="NoneState"))
            throw new NotSupportedException("HERO_01bp owner-1 Death/None entry is unresolved");
        var expectedActions=new Dictionary<string,string[]>
        {
            {"State 1",new string[0]},
            {"DeathState",new[]{"SpellStateAction","ParticlePlayAction","ParticlePlayAction","Wait","AudioPlayClipAction"}},
            {"DeathState2",new[]{"ParticlePlayAction","ParticlePlayAction","ParticlePlayAction","InvokeMethod"}},
            {"DeathState3",new[]{"SpellFinishAction","Wait"}},
            {"NoneState",new[]{"SpellStateAction"}},
        };
        var expectedTransitions=new Dictionary<string,string[]>
        {
            {"State 1",new string[0]},
            {"DeathState",new[]{"FINISHED>DeathState2"}},
            {"DeathState2",new[]{"FINISHED>DeathState3"}},
            {"DeathState3",new[]{"FINISHED>NoneState"}},
            {"NoneState",new string[0]},
        };
        foreach(var state in machine.states)
        {
            var stateActionNames=(state.actions??new NativeAction[0]).Select(action=>action.name).OrderBy(name=>name).ToArray();
            var transitions=(state.transitions??new NativeTransition[0]).Select(row=>row.@event+">"+row.state).ToArray();
            if(!stateActionNames.SequenceEqual(expectedActions[state.name].OrderBy(name=>name))||
                !transitions.SequenceEqual(expectedTransitions[state.name]))
                throw new NotSupportedException("HERO_01bp state/action lifecycle is outside the reviewed root profile: "+state.name);
        }
        var actions=machine.states.SelectMany(state=>state.actions??new NativeAction[0]).ToArray();
        var names=actions.GroupBy(action=>action.name).ToDictionary(group=>group.Key,group=>group.Count());
        var expected=new Dictionary<string,int>{{"SpellStateAction",2},{"ParticlePlayAction",5},{"Wait",2},
            {"AudioPlayClipAction",1},{"SpellFinishAction",1},{"InvokeMethod",1}};
        if(names.Count!=expected.Count||expected.Any(pair=>!names.ContainsKey(pair.Key)||names[pair.Key]!=pair.Value)||
            actions.Any(action=>(action.@params??new NativeParam[0]).Any(parameter=>parameter==null||parameter.unresolved)))
            throw new NotSupportedException("HERO_01bp root contains an unsupported or unresolved action");
        var invoke=actions.Single(action=>action.name=="InvokeMethod");
        if(!NativeEffectInstance.SupportsCameraShakerInvoke(program,invoke)||
            invoke.Param("gameObject").node!=1||
            invoke.Param("delay").number<0.099f||invoke.Param("delay").number>0.101f||
            invoke.Param("repeatDelay").number!=1||invoke.Param("repeating").number!=0||invoke.Param("cancelOnExit").number!=0)
            throw new NotSupportedException("HERO_01bp CameraShaker invocation is outside the reviewed one-shot StartShake profile");
        var shaker=program.nodes[invoke.Param("gameObject").node].camera_shaker;
        if(shaker.hold||!Mathf.Approximately(shaker.amount.x,.1f)||!Mathf.Approximately(shaker.amount.y,.1f)||
            !Mathf.Approximately(shaker.amount.z,.1f)||shaker.curve.Length!=2||
            !Mathf.Approximately(shaker.curve[0].time,0)||!Mathf.Approximately(shaker.curve[0].value,1)||
            !Mathf.Approximately(shaker.curve[shaker.curve.Length-1].time,.6f)||
            !Mathf.Approximately(shaker.curve[shaker.curve.Length-1].value,0)||
            !Mathf.Approximately(shaker.hold_at_sec,0)||shaker.is_override)
            throw new NotSupportedException("HERO_01bp CameraShaker component values are not the reviewed source profile");
        foreach(var action in actions.Where(row=>row.name=="ParticlePlayAction"))
        {
            int node=action.Param("m_GameObject").node;
            if(node<0||node>=program.nodes.Length||program.nodes[node].particle==null||!program.nodes[node].particle.restored||
                action.Param("m_IncludeChildren").number!=0)
                throw new NotSupportedException("HERO_01bp root particle action is outside the reviewed node-only branch");
        }
    }
    public static bool SupportsWarlockHeroPowerRoot(NativeProgram program,NativeSuperSpellConfig config)
    {
        try{ValidateWarlockHeroPowerRoot(program,config);return true;}catch{return false;}
    }
    static void ValidateWarlockHeroPowerRoot(NativeProgram program,NativeSuperSpellConfig config)
    {
        const string reference="ReuseFX_BuffImpact_Void_SwirlIntake_Small_HeroPower_Super.prefab:5c3f93a27c9cb434ea6e0c37005a2c0d";
        if(program==null||config==null||program.reference!=reference||program.nodes==null||program.nodes.Length!=21||
            config.card_id!="HERO_07bp"||config.reference!=reference||config.location!=2||config.target_behavior!=0||
            !config.start_enabled||!config.start_config_null||config.missile==null||config.missile.m_Enabled!=0||
            !String.IsNullOrEmpty(config.missile_reference)||config.chain_available||config.reverse_available||
            config.impact==null||!config.impact.enabled||config.impact.prefab==null||config.impact.prefab.prefab_null)
            throw new NotSupportedException("HERO_07bp CardDef does not match the enabled-null Start source profile");
        var spellTransforms=program.spell_transforms??new NativeSourceSpellTransform[0];
        var superRoot=spellTransforms.SingleOrDefault(row=>row.node==0&&row.enabled);
        var visualRoot=spellTransforms.SingleOrDefault(row=>row.node==1&&row.enabled);
        if(program.nodes[0].parent!=-1||program.nodes[1].parent!=-1||superRoot==null||superRoot.kind!="SuperSpell"||superRoot.location!=2||
            visualRoot==null||visualRoot.kind!="Spell"||visualRoot.location!=2||!String.IsNullOrEmpty(visualRoot.location_transform)||visualRoot.parent_to_location)
            throw new NotSupportedException("HERO_07bp SourceAuto visual Spell root placement changed");
        var capture=(program.captures??new NativeCapture[0]).SingleOrDefault();
        if(capture==null||!NativeGeneratedCapture.Supports(capture)||capture.owner!=8||capture.source!=3||capture.target!=-1||!capture.hide_source||
            capture.original_parameters==null||capture.original_parameters.m_RenderMaterial!=0||
            capture.original_parameters.m_CreateRenderPlane!=1||capture.material==null||
            capture.material.shader!="Custom/FX/FX_AreaEffect_Render"||capture.material.name!="GenFX_RtTRamp_Void_PinkPurpleBlack02"||
            !(capture.material.textures??new NativeTexture[0]).Any(texture=>texture.name=="_RampTex"&&!String.IsNullOrEmpty(texture.path)))
            throw new NotSupportedException("HERO_07bp explicit source render plane is unrestored");
        if((program.unknown_actions??new string[0]).Distinct().OrderBy(name=>name)
            .SequenceEqual(new[]{"AudioPlayClipAction"})==false)
            throw new NotSupportedException("HERO_07bp root contains an unsupported source action");
        if(program.machines==null||program.machines.Length!=1)throw new NotSupportedException("HERO_07bp requires one root FSM");
        var machine=program.machines.SingleOrDefault(row=>row.owner_known&&row.owner==1);
        var expectedActions=new Dictionary<string,string[]>
        {
            {"Play FX",new[]{"SpellStateAction","SpellFinishAction","ActivateGameObject","ActivateGameObject","ParticlePlayAction","AudioPlayClipAction","ParticlePlayAction","Wait"}},
            {"StartState",new string[0]},
            {"NoneState",new[]{"SpellStateAction"}},
        };
        if(machine==null||machine.start!="StartState"||machine.states==null||machine.states.Length!=expectedActions.Count||
            !machine.states.Select(row=>row.name).OrderBy(name=>name).SequenceEqual(expectedActions.Keys.OrderBy(name=>name))||
            machine.events==null||machine.events.Length!=2||
            !machine.events.Any(row=>row.@event=="Death"&&row.state=="Play FX")||
            !machine.events.Any(row=>row.@event=="None"&&row.state=="NoneState"))
            throw new NotSupportedException("HERO_07bp Death/None root entry changed");
        foreach(var state in machine.states)
        {
            var actions=state.actions??new NativeAction[0];
            if(!actions.Select(row=>row.name).SequenceEqual(expectedActions[state.name]))
                throw new NotSupportedException("HERO_07bp root action order changed: "+state.name);
            var transitions=(state.transitions??new NativeTransition[0]).Select(row=>row.@event+">"+row.state).ToArray();
            var expected=state.name=="Play FX"?new[]{"FINISHED>NoneState"}:new string[0];
            if(!transitions.SequenceEqual(expected)||actions.Any(action=>action.@params==null||action.@params.Any(parameter=>parameter==null||parameter.unresolved)))
                throw new NotSupportedException("HERO_07bp root action parameters or transitions changed: "+state.name);
        }
        var play=machine.states.Single(state=>state.name=="Play FX").actions;
        void Params(NativeAction action,params string[] names)
        {if(action.@params.Length!=names.Length||!action.@params.Select(parameter=>parameter.name).SequenceEqual(names))throw new NotSupportedException("HERO_07bp action signature changed: "+action.name);}
        void Node(NativeAction action,string name,int node)
        {var value=action.Param(name);if(value.kind!=20||value.node!=node)throw new NotSupportedException("HERO_07bp action target changed: "+action.name);}
        void Number(NativeAction action,string name,float value)
        {if(!Mathf.Approximately(action.Param(name).number,value))throw new NotSupportedException("HERO_07bp action value changed: "+action.name+"."+name);}
        Params(play[0],"m_SpellObject");Node(play[0],"m_SpellObject",1);
        Params(play[1],"m_SpellObject","m_Delay");Node(play[1],"m_SpellObject",1);Number(play[1],"m_Delay",0);
        for(int i=0;i<2;i++)
        {
            var action=play[2+i];Params(action,"gameObject","activate","recursive","resetOnExit","everyFrame");
            Node(action,"gameObject",i==0?3:8);Number(action,"activate",1);Number(action,"recursive",0);
            Number(action,"resetOnExit",0);Number(action,"everyFrame",0);
        }
        foreach(var pair in new[]{Tuple.Create(play[4],4),Tuple.Create(play[6],9)})
        {Params(pair.Item1,"m_GameObject","m_IncludeChildren");Node(pair.Item1,"m_GameObject",pair.Item2);Number(pair.Item1,"m_IncludeChildren",1);
            var particle=program.nodes[pair.Item2].particle;if(particle==null||!particle.restored)throw new NotSupportedException("HERO_07bp source particle was not restored");}
        var audio=play[5];Params(audio,"m_ParentObject","m_Sound","m_Volume","m_Pitch","m_SpatialBlend","m_Category","m_Delay","m_TemplateSource",
            "m_IsMinionSummonVO","m_InstanceLimited","m_InstanceLimitedDuration","m_InstanceLimitMaximum","m_LimitMaxOutOption");
        Node(audio,"m_ParentObject",1);var sound=audio.Param("m_Sound");
        if(sound.kind!=24||sound.object_null||sound.asset_type!="MonoBehaviour"||sound.asset_path!="ce406cfd04976a99ebc44463.native-sound.json"||
            !audio.Param("m_TemplateSource").object_null)
            throw new NotSupportedException("HERO_07bp source clip identity or template changed");
        foreach(var pair in new[]{Tuple.Create("m_Volume",.8f),Tuple.Create("m_Pitch",1f),Tuple.Create("m_SpatialBlend",0f),
            Tuple.Create("m_Category",1f),Tuple.Create("m_Delay",0f),Tuple.Create("m_IsMinionSummonVO",0f),Tuple.Create("m_InstanceLimited",1f),
            Tuple.Create("m_InstanceLimitedDuration",.5f),Tuple.Create("m_InstanceLimitMaximum",1f),Tuple.Create("m_LimitMaxOutOption",0f)})Number(audio,pair.Item1,pair.Item2);
        Params(play[7],"time","finishEvent","realTime");Number(play[7],"time",4);Number(play[7],"realTime",0);
        if(play[7].Param("finishEvent").text!="FINISHED")throw new NotSupportedException("HERO_07bp source wait event changed");
    }
    public NativeSuperSpellPlayback(NativeProgram parent,NativeEffectEntry entry,AssetRegistry assets,int slot,
        NativeSuperSpellScene scene,bool? friendly,Action<NativeAudioCue> audio,Func<Color?> ambient,Camera timelineCamera,
        bool selection=false,bool demonHunterProfileAlreadyValidated=false)
    {
        this.scene=scene??throw new NotSupportedException("SuperSpell scene host is unbound");CardId=entry.card_id;Context=scene.Context;
        if(Context!=null&&Context.PowerTargets!=null&&(scene.History==null||scene.History.Tasks!=Context.PowerTargets.TaskList))
            throw new NotSupportedException("Explicit original history requires its original metadata host");
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,entry.card_id=="HERO_08bp"?"native-fireblast.json":"native-super-spell.json")));
        var config=catalog.programs.Single(p=>p.card_id==entry.card_id&&p.file==entry.file&&p.reference==parent.reference);
        bool warlock=entry.card_id=="HERO_07bp",priest=entry.card_id=="HERO_09bp",rogue=entry.card_id=="HERO_03bp";
        if(entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&
            (!NativeDemonHunterHeroPowerPlayback.SupportsConfig(config)||!scene.SourceScreenPoint.HasValue||!scene.TargetScreenPoint.HasValue))
            throw new NotSupportedException("HERO_10bp source configuration or live source/target screen anchors failed its strict profile gate");
        if(rogue)NativeRogueHeroPowerProfile.Validate(parent,config,assets.Root);
        else if(warlock)ValidateWarlockHeroPowerRoot(parent,config);else BindVisualTargets(config,scene);
        if(priest)
        {
            var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            var startAsset=catalog.prefabs.Single(asset=>asset.reference==config.start.prefab_reference);
            var impactAsset=catalog.prefabs.Single(asset=>asset.reference==config.impact.prefab.prefab_reference);
            var startProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,startAsset.file)));
            var impactProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,impactAsset.file)));
            if(!SupportsPriestHeroPowerRoot(source,config,startProgram,impactProgram,assets.Root))
                throw new NotSupportedException("HERO_09bp source route failed its strict profile gate");
        }
        var origin=new Vector3(20000+slot*1000,10000,20000);
        bool warrior=entry.card_id=="HERO_01bp",demonHunter=entry.card_id==NativeDemonHunterHeroPowerPlayback.HeroPowerId;
        if(warrior)ValidateWarriorHeroPowerRoot(parent);
        if(demonHunter)
        {
            var root=NativeDemonHunterHeroPowerPlayback.ReadProgram(Path.Combine(assets.Root,entry.file));
            if(root==null||root.reference!=config.reference||
               !(demonHunterProfileAlreadyValidated?NativeDemonHunterHeroPowerPlayback.SupportsRootProgram(root):
                   NativeDemonHunterHeroPowerPlayback.Supports(assets,entry)))
                throw new NotSupportedException("HERO_10bp imported source graph failed its strict runtime profile");
            // The exported closure also contains the Impact prefab definition. Flow owns its actual instance.
            root.machines=new NativeMachine[0];
            for(int node=1;node<root.nodes.Length;node++)
            {root.nodes[node].active=false;root.nodes[node].visible=false;}
            Effect=new NativeEffectInstance(root,assets,slot,1024,576,ambient,context:scene.Context,audioCallback:audio,deferEntryEvents:true);
        }
        else if(warlock||warrior||rogue)Effect=new NativeEffectInstance(parent,assets,slot,1024,576,ambient,
            context:new NativeSpellContext(0,lifecycle:"Death",placement:warlock||rogue?scene.Placement:null,sourceCardId:entry.card_id),audioCallback:audio);
        else
        {
            var node=new NativeNode{name="SuperSpell autonomous root",parent=-1,active=true,scale=Vector3.one,rotation=Quaternion.identity,materials=new NativeMaterial[0]};
            var thin=new NativeProgram{nodes=new[]{node},machines=new NativeMachine[0],animations=new NativeAnimation[0],captures=new NativeCapture[0],unknown_actions=new string[0]};
            Effect=new NativeEffectInstance(thin,assets,slot,1024,576,ambient,context:new NativeSpellContext(0,lifecycle:"None"));
        }
        try
        {
            if(warrior)Effect.BindTimelineCamera(timelineCamera);
            if(scene.SourceCamera!=null)Effect.ApplySourceCamera(scene.SourceCamera,scene.SourceCameraOrigin);
            var location=scene.Placement.Location(config.location);
            if(location==null)throw new NotSupportedException("SuperSpell Source location is unbound: "+config.location);
            if(warlock||warrior||rogue)
            {
                if(Effect.Nodes.Length<2)throw new NotSupportedException(entry.card_id+" visual root node is missing");
            }
            Effect.Seek(0);
            if(warrior)
            {
                sourcePresentationPosition=location.transform.position;
                hasSourcePresentationPosition=true;
                ApplySourcePresentationPosition();
            }
            else if(demonHunter||rogue)
            {
                var sourceAuto=scene.Placement.SourceAuto();
                if(sourceAuto==null)throw new NotSupportedException(entry.card_id+" SourceAuto anchor is unbound");
                sourcePresentationPosition=sourceAuto.transform.position;
                hasSourcePresentationPosition=true;
                ApplySourcePresentationPosition();
                if(demonHunter)
                {
                    var target=scene.Placement.Targets.GetTarget();
                    if(target==null)throw new NotSupportedException("HERO_10bp same-batch hero target is unbound");
                    targetPresentationPosition=target.transform.position;
                    hasTargetPresentationPosition=true;
                    ApplyDemonHunterPresentationPosition();
                }
            }
            else if(!warlock)Effect.Nodes[0].transform.position=location.transform.position;
            if(warlock)
            {
                var sourceTransform=parent.spell_transforms.Single(row=>row.node==1&&row.enabled).Copy();
                new NativeSpellTransform(sourceTransform,Effect.Nodes[1].transform,scene.Placement.Location,scene.Placement.Facing).UpdatePosition();
            }
            if(warlock||rogue)
            {
                Effect.UpdateOriginalPosition();
                if(rogue)ApplySourcePresentationPosition();
                SourceEntity=scene.Placement.source.entity;
                return;
            }
            NativeAreaInstance Create(string reference)
            {
                var asset=catalog.prefabs.Single(p=>p.reference==reference);
                var program=demonHunter?NativeDemonHunterHeroPowerPlayback.ReadProgram(Path.Combine(assets.Root,asset.file)):
                    JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,asset.file)));
                if(program.reference!=reference)throw new InvalidDataException("SuperSpell independent prefab identity mismatch");
                NativeAreaInstance clone=null;
                var selection=new NativeSpellObjectSelection{source=scene.Placement.source};
                GameObject Location(int kind)
                {
                    selection.SetTargets(clone.Targets.GetTargets().Select(t=>new NativeSpellEntityObject{resolve=()=>t}));
                    return selection.Location(kind)??throw new NotSupportedException("SuperSpell clone location is unbound: "+kind);
                }
                GameObject Facing(int kind)
                {return selection.Facing(kind)??throw new NotSupportedException("SuperSpell clone facing is unbound: "+kind);}
                clone=new NativeAreaInstance(program,assets,slot+10+owned.Count,locations:Location,facings:Facing,
                    attachTasks:tasks=>scene.History!=null&&scene.History.Tasks==tasks,audio:audio,
                    width:demonHunter?1024:512,height:demonHunter?576:512,
                    context:new NativeSpellContext(scene.Placement.source.type,heroType:3,lifecycle:"None",
                        actorObjects:role=>role==1?clone?.Targets.GetTarget():role==2?scene.Placement.source.hero():scene.Placement.source.Object,
                        runtimeCardTypes:which=>which==1?(scene.Placement.TargetRecord(clone?.Targets.GetTarget())?.type??0):which==2||which==4?3:scene.Placement.source.type,
                        sourceCardId:entry.card_id,targetCardId:scene.Placement.TargetRecord(clone?.Targets.GetTarget())?.card_id),
                    ambient:ambient,timelineCamera:timelineCamera,startAtFsmEntry:true,
                    sourceSimulationSpaceNodes:SourceSimulationSpaceOverrides(entry.card_id,config,reference,program));
                var cameraOffset=clone.Effect.PreviewCamera.transform.position-clone.Effect.Root.transform.position;
                clone.Effect.ReanchorStagingRoot(origin);
                if(demonHunter)clone.Effect.PreviewCamera.transform.position=origin+cameraOffset;
                if(entry.card_id=="HERO_05bp"&&config.impact!=null&&config.impact.prefab!=null&&reference==config.impact.prefab.prefab_reference)
                    clone.Effect.StartHunterImpactExplosionAtAgeZero();
                owned.Add(clone);return clone;
            }
        Flow=new NativeSuperSpell(config,new NativeSuperSpellServices{Root=Effect.Nodes[0],Source=()=>scene.Placement.source.Object,
                Targets=new List<GameObject>(scene.Placement.Targets.GetTargets()),Friendly=friendly,History=scene.History,Create=Create,
                Range=scene.Range??UnityEngine.Random.Range,RandomBool=scene.RandomBool??(()=>UnityEngine.Random.Range(0,2)==0),
                TargetInHand=scene.TargetInHand,HandSlot=scene.HandSlot,
                Release=clone=>
                {
                    var effect=clone.Effect;
                    void Detail(string name,int count){if(count!=0)UnboundControllerDetails.Add(effect.Program.reference+" | "+name+"="+count);}
                    Detail("UnboundSourcePlacements",effect.UnboundSourcePlacements);
                    Detail("UnboundSourceStateObjects",effect.UnboundSourceStateObjects);
                    Detail("UnboundPropertyActions",effect.UnboundPropertyActions);
                    Detail("UnboundCardTypeActions",effect.UnboundCardTypeActions);
                    Detail("UnboundFinishActions",effect.UnboundFinishActions);
                    Detail("UnboundLifecycleEvents",effect.UnboundLifecycleEvents);
                    Detail("UnboundMachineOwnerActions",effect.UnboundMachineOwnerActions);
                    Detail("UnrestoredSourceActionSideEffects",effect.UnrestoredSourceActionSideEffects);
                    UnboundControllerActions+=effect.UnboundSourcePlacements+effect.UnboundSourceStateObjects+effect.UnboundPropertyActions+
                        effect.UnboundCardTypeActions+effect.UnboundFinishActions+effect.UnboundLifecycleEvents+effect.UnboundMachineOwnerActions+effect.UnrestoredSourceActionSideEffects;
                    ImpactCloneUnhandledActions+=effect.UnhandledActions;ImpactCloneUnhandledActionDetails.AddRange(effect.UnhandledActionDetails.Select(detail=>effect.Program.reference+" | "+detail));
                    UnboundAudioActions+=effect.UnboundAudioActions+effect.UnboundAudioStopActions+effect.UnboundAudioVolumeActions+effect.UnboundAudioFadeActions;
                    UnboundComponentActions+=effect.UnboundComponentActions;MissingAudioPlaybackServices+=effect.MissingAudioPlaybackServices;
                    ImpactCloneCameraShakerStarts+=effect.CameraShakerStarts;
                    foreach(var pair in effect.ActionExecutions){int count;ActionExecutions.TryGetValue(pair.Key,out count);ActionExecutions[pair.Key]=count+pair.Value;}
                    owned.Remove(clone);clone.Dispose();
                },Trace=(phase,index,time)=>Events.Add(time.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" "+phase+" "+index)},selection);
        if(demonHunter)Flow.DemonHunterTimingDiagnostic=(stage,milliseconds)=>DemonHunterTimingDiagnostic?.Invoke(stage,milliseconds);
            SourceEntity=scene.Placement.source.entity;
            if(selection)Flow.BeginSelection();else Flow.Begin();
            if(demonHunter)ApplyDemonHunterPresentationPosition();
        }
        catch{foreach(var clone in owned)clone.Dispose();Effect.Dispose();scene.Dispose?.Invoke();throw;}
    }
    void ApplySourcePresentationPosition()
    {
        if(!hasSourcePresentationPosition||Effect==null||Effect.Nodes==null||Effect.Nodes.Length<2)return;
        Effect.Nodes[0].transform.position=sourcePresentationPosition;
        Effect.Nodes[1].transform.position=sourcePresentationPosition;
    }
    void ApplyDemonHunterPresentationPosition()
    {
        if(CardId!=NativeDemonHunterHeroPowerPlayback.HeroPowerId||!hasTargetPresentationPosition||
            Effect==null||Effect.Nodes==null||Effect.Nodes.Length<=NativeDemonHunterHeroPowerPlayback.TargetRootNode)return;
        ApplySourcePresentationPosition();
        Effect.Nodes[NativeDemonHunterHeroPowerPlayback.TargetRootNode].transform.position=targetPresentationPosition;
    }
    public void IdentifySelection(string match){MatchId=match;}
    public void Cast(SemanticEvent data,StateView after)
    {
        if(data.source!=SourceEntity||after.match_id!=MatchId||scene.SetTargets==null)throw new InvalidOperationException("Fireblast selection identity mismatch");
        var targets=CardId=="HERO_09bp"?
            new[]{NativeSuperSpellBoard.ResolvePriestTarget(data,null,after)}:
            data.target!=0?new[]{data.target}:(after.events??new SemanticEvent[0]).Where(e=>e.type=="Hit"&&e.source==data.source).Select(e=>e.target).Distinct().ToArray();
        if(CardId==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&(targets.Length!=1||targets[0]==0))
            throw new InvalidOperationException("HERO_10bp requires one actual same-batch self-hero target");
        scene.SetTargets(targets);CastOffset=Flow.Time;Flow.Cast(new List<GameObject>(scene.Placement.Targets.GetTargets()));
    }
    public void Seek(float age)
    {
        if(Flow==null)
        {
            sourceOnlyTime=age;Effect.Seek(age);
            if(CardId=="HERO_03bp")ApplySourcePresentationPosition();
            else if(CardId==NativeDemonHunterHeroPowerPlayback.HeroPowerId)ApplyDemonHunterPresentationPosition();
            return;
        }
        if(CardId=="HERO_01bp"){Effect.Seek(age);ApplySourcePresentationPosition();}
        else if(CardId==NativeDemonHunterHeroPowerPlayback.HeroPowerId)
        {
            long started=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
            Effect.Seek(age);ReportDemonHunterTiming("demonhunter-root-seek",started);
            started=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
            Flow.Tick(age);ReportDemonHunterTiming("demonhunter-flow-tick",started);
            started=DemonHunterTimingDiagnostic==null?0:Stopwatch.GetTimestamp();
            ApplyDemonHunterPresentationPosition();ReportDemonHunterTiming("demonhunter-position",started);return;
        }
        Flow.Tick(age);
    }
    void ReportDemonHunterTiming(string stage,long started)
    {if(started!=0)DemonHunterTimingDiagnostic?.Invoke(stage,(float)(Stopwatch.GetTimestamp()-started)*1000f/Stopwatch.Frequency);}
    public RenderTexture RenderDemonHunterSourceLayer(float age)
    {
        if(CardId!=NativeDemonHunterHeroPowerPlayback.HeroPowerId)throw new InvalidOperationException("HERO_10bp source layer requested for another card");
        Seek(age);
        var source=Flow.Instances.SingleOrDefault(clone=>clone.Effect.Program.reference==NativeDemonHunterHeroPowerPlayback.StartReference);
        demonHunterRenderedSource=source?.Effect;
        if(source!=null)return source.RenderAffine(source.PlaybackTime);
        return Effect.RenderAffineExcludingSubtree(age,NativeDemonHunterHeroPowerPlayback.TargetRootNode,
            ApplyDemonHunterPresentationPosition);
    }
    public RenderTexture RenderDemonHunterTargetLayer(float age)
    {
        if(CardId!=NativeDemonHunterHeroPowerPlayback.HeroPowerId)throw new InvalidOperationException("HERO_10bp target layer requested for another card");
        Seek(age);
        // The GUI hero Actor has its own calibrated proportions. Fit the source hero meshes to that portrait,
        // rather than inheriting the square capture's board-space aspect and compressing the arch vertically.
        var impact=Flow.Instances.SingleOrDefault(clone=>clone.Effect.Program.reference==NativeDemonHunterHeroPowerPlayback.ImpactReference);
        demonHunterRenderedTarget=impact?.Effect;
        if(impact==null)return Effect.RenderAffineSubtree(age,NativeDemonHunterHeroPowerPlayback.TargetRootNode,0,ApplyDemonHunterPresentationPosition);
        int heroRoot=Array.FindIndex(impact.Effect.Program.nodes,node=>node.name=="HeroRoot");
        if(heroRoot<0)throw new NotSupportedException("HERO_10bp Impact hero meshes are missing");
        if(!demonHunterBodyFrame.HasValue)
        {
            var framing=impact.Effect.CaptureSubtreeFrame(impact.PlaybackTime,heroRoot,null,true,true);
            int overlay=Array.FindIndex(impact.Effect.Program.nodes,node=>node.name=="mesh_AddOverlay_DH");
            var mesh=overlay<0?null:impact.Effect.Nodes[overlay].GetComponent<MeshFilter>()?.sharedMesh;
            if(mesh==null)throw new NotSupportedException("HERO_10bp authored hero overlay mesh is missing");
            var scale=Vector3.one;
            for(int node=overlay;node>=0;node=impact.Effect.Program.nodes[node].parent)
                scale=Vector3.Scale(scale,impact.Effect.Program.nodes[node].scale);
            // The first rendered frame can catch the animated backer at a very wide scale.
            // Its transient width must not become the permanent aspect of the hero portrait.
            framing.aspect=Mathf.Abs(mesh.bounds.size.x*scale.x)/Mathf.Max(.01f,Mathf.Abs(mesh.bounds.size.z*scale.z));
            demonHunterBodyFrame=framing;
        }
        return impact.Effect.RenderAffineSubtree(impact.PlaybackTime,0,demonHunterBodyFrame.Value);
    }
    public RenderTexture RenderAffine(float age)
    {
        // 无 Flow 的来源根自身持有捕获和材质；空克隆集合不会更新它们。
        if(Flow==null)
        {
            sourceOnlyTime=age;Effect.Seek(age);
            if(CardId=="HERO_03bp")ApplySourcePresentationPosition();
            else if(CardId==NativeDemonHunterHeroPowerPlayback.HeroPowerId)ApplyDemonHunterPresentationPosition();
            var emission=Effect.RenderAffine(age);
            if(CardId=="HERO_03bp")ApplySourcePresentationPosition();
            else if(CardId==NativeDemonHunterHeroPowerPlayback.HeroPowerId)ApplyDemonHunterPresentationPosition();
            return emission;
        }
        Seek(age);return Effect.RenderTogether(Flow.Instances.Select(i=>i.Effect));
    }
    public void Dispose(){Flow?.Dispose();foreach(var clone in owned.ToArray())clone.Dispose();owned.Clear();Effect.Dispose();scene.Dispose?.Invoke();}
}
