using System;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class NativeKey
{public float time,value,inSlope,outSlope,inWeight=1f/3,outWeight=1f/3;public int weightedMode;public bool step,in_step,out_step;}
[Serializable] public sealed class NativeCurve
{
    public int mode;public float scalar,minimum;public NativeKey[] maximum,minimum_keys;
    public static AnimationCurve Keys(NativeKey[] source)
    {
        var keys=new Keyframe[source==null?0:source.Length];
        for(int i=0;i<keys.Length;i++){var k=source[i];keys[i]=new Keyframe(k.time,k.value,k.step||k.in_step?float.PositiveInfinity:k.inSlope,k.step||k.out_step?float.PositiveInfinity:k.outSlope,k.inWeight,k.outWeight){weightedMode=(WeightedMode)k.weightedMode};}
        return new AnimationCurve(keys);
    }
    public ParticleSystem.MinMaxCurve UnityCurve()
    {
        if(mode==1)return new ParticleSystem.MinMaxCurve(scalar,Keys(maximum));
        if(mode==2)return new ParticleSystem.MinMaxCurve(scalar,Keys(minimum_keys),Keys(maximum));
        if(mode==3)return new ParticleSystem.MinMaxCurve(minimum,scalar);
        return new ParticleSystem.MinMaxCurve(scalar);
    }
}
[Serializable] public sealed class NativeGradientKeys
{
    public int mode;public OriginalColorKey[] colors;public OriginalAlphaKey[] alpha;
    public Gradient UnityGradient()
    {
        var g=new Gradient();var c=new GradientColorKey[colors==null?0:colors.Length];var a=new GradientAlphaKey[alpha==null?0:alpha.Length];
        for(int i=0;i<c.Length;i++)c[i]=new GradientColorKey(colors[i].value,colors[i].time);
        for(int i=0;i<a.Length;i++)a[i]=new GradientAlphaKey(alpha[i].value,alpha[i].time);
        if(c.Length>0&&a.Length>0)g.SetKeys(c,a);g.mode=mode==1?GradientMode.Fixed:GradientMode.Blend;return g;
    }
}
[Serializable] public sealed class NativeGradient
{
    public bool enabled;public int mode;public Color maximum=Color.white,minimum=Color.white;public NativeGradientKeys high,low;
    public ParticleSystem.MinMaxGradient UnityGradient()
    {
        if(mode==1)return new ParticleSystem.MinMaxGradient(high.UnityGradient());
        if(mode==2)return new ParticleSystem.MinMaxGradient(minimum,maximum);
        if(mode==3)return new ParticleSystem.MinMaxGradient(low.UnityGradient(),high.UnityGradient());
        if(mode==4){var g=new ParticleSystem.MinMaxGradient(high.UnityGradient());g.mode=ParticleSystemGradientMode.RandomColor;return g;}
        return new ParticleSystem.MinMaxGradient(maximum);
    }
}
[Serializable] public sealed class NativeBurst{public float time,interval,probability;public int cycles;public NativeCurve count;}
[Serializable] public sealed class NativeEmission{public bool enabled;public NativeCurve rate,distance;public NativeBurst[] bursts;}
[Serializable] public sealed class NativeShape{public bool enabled;public int kind;public float radius,angle,length,radius_thickness;public Vector3 position,rotation,scale;public string mesh;}
[Serializable] public sealed class NativeModuleCurve{public string name;public NativeCurve value;}
[Serializable] public sealed class NativeModule{public string name;public bool separate,world;public float dampen,frequency;public int octaves,mode;public Vector2 range;public NativeModuleCurve[] curves;
 public ParticleSystem.MinMaxCurve Curve(string key){foreach(var c in curves)if(c.name==key)return c.value.UnityCurve();return new ParticleSystem.MinMaxCurve(0);}}
[Serializable] public sealed class NativeUV{public bool enabled;public int x,y,mode,row;public float cycles;public NativeCurve frame,start;}
[Serializable] public sealed class NativeParticleRender{public int mode,alignment;public float length,velocity,maximum;public string mesh;}
[Serializable] public sealed class NativeColorBySpeed
{
    public NativeGradient color;public Vector2 range;
    public void Apply(ParticleSystem particle)
    {var module=particle.colorBySpeed;module.enabled=color!=null&&color.enabled;if(module.enabled){module.color=color.UnityGradient();module.range=range;}}
}
[Serializable] public sealed class NativeParticle
{
    public NativeParticleCollision collision;public bool empty_sub_emitters;
    public bool restored,loop,awake,size3d,rotation3d;public int space,scaling,max_particles;public float duration,speed;
    public NativeCurve delay,life,velocity,size,size_y,size_z,rotation,rotation_x,rotation_y,gravity;
    public NativeGradient start_color,color;public NativeColorBySpeed color_speed;public NativeEmission emission;public NativeShape shape;public NativeModule[] modules;public NativeUV uv;public NativeParticleRender render;public string[] unsupported;
    public ParticleSystem Create(GameObject go,AssetRegistry assets,uint seed,bool sourceSimulationSpace=false)
    {
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=seed;
        var main=ps.main;main.playOnAwake=false;main.duration=Mathf.Max(.01f,duration);main.loop=loop;main.simulationSpeed=speed;
        if(sourceSimulationSpace)
        {
            if(space<0||space>2)throw new NotSupportedException("Unknown source particle simulation space "+space);
            main.simulationSpace=(ParticleSystemSimulationSpace)space;
        }
        else main.simulationSpace=space==0?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
        main.scalingMode=(ParticleSystemScalingMode)Mathf.Clamp(scaling,0,2);main.maxParticles=max_particles;main.startDelay=delay.UnityCurve();
        main.startLifetime=life.UnityCurve();main.startSpeed=velocity.UnityCurve();main.startSize3D=size3d;main.startSizeX=size.UnityCurve();main.startSizeY=size_y.UnityCurve();main.startSizeZ=size_z.UnityCurve();
        main.startRotation3D=rotation3d;main.startRotationX=rotation_x.UnityCurve();main.startRotationY=rotation_y.UnityCurve();main.startRotationZ=rotation.UnityCurve();main.gravityModifier=gravity.UnityCurve();main.startColor=start_color.UnityGradient();
        var em=ps.emission;em.enabled=emission.enabled;em.rateOverTime=emission.rate.UnityCurve();em.rateOverDistance=emission.distance.UnityCurve();
        var bursts=new ParticleSystem.Burst[emission.bursts.Length];for(int i=0;i<bursts.Length;i++){var b=emission.bursts[i];bursts[i]=new ParticleSystem.Burst(b.time,b.count.UnityCurve(),b.cycles,b.interval){probability=b.probability};}em.SetBursts(bursts);
        var s=ps.shape;s.enabled=shape.enabled;s.shapeType=(ParticleSystemShapeType)shape.kind;s.radius=shape.radius;s.angle=shape.angle;s.length=shape.length;s.position=shape.position;s.rotation=shape.rotation;s.scale=shape.scale;s.radiusThickness=shape.radius_thickness;
        if(!String.IsNullOrEmpty(shape.mesh))s.mesh=assets.MeshFile(shape.mesh);
        var col=ps.colorOverLifetime;col.enabled=color!=null&&color.enabled;if(col.enabled)col.color=color.UnityGradient();
        if(color_speed!=null)color_speed.Apply(ps);
        foreach(var m in modules)
        {
            if(m.name=="SizeModule"){var v=ps.sizeOverLifetime;v.enabled=true;v.separateAxes=m.separate;v.x=m.Curve("curve");v.y=m.Curve("y");v.z=m.Curve("z");}
            else if(m.name=="RotationModule"){var v=ps.rotationOverLifetime;v.enabled=true;v.separateAxes=m.separate;v.x=m.Curve("x");v.y=m.Curve("y");v.z=m.Curve("curve");}
            else if(m.name=="VelocityModule"){var v=ps.velocityOverLifetime;v.enabled=true;v.space=m.world?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;v.x=m.Curve("x");v.y=m.Curve("y");v.z=m.Curve("z");v.orbitalX=m.Curve("orbitalX");v.orbitalY=m.Curve("orbitalY");v.orbitalZ=m.Curve("orbitalZ");v.radial=m.Curve("radial");}
            else if(m.name=="ForceModule"){var v=ps.forceOverLifetime;v.enabled=true;v.space=m.world?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;v.x=m.Curve("x");v.y=m.Curve("y");v.z=m.Curve("z");}
            else if(m.name=="ClampVelocityModule"){var v=ps.limitVelocityOverLifetime;v.enabled=true;v.separateAxes=m.separate;v.space=m.world?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;v.limit=m.Curve("magnitude");v.limitX=m.Curve("x");v.limitY=m.Curve("y");v.limitZ=m.Curve("z");v.dampen=m.dampen;v.drag=m.Curve("drag");}
            else if(m.name=="NoiseModule"){var v=ps.noise;v.enabled=true;v.separateAxes=m.separate;v.strengthX=m.Curve("strength");v.strengthY=m.Curve("strengthY");v.strengthZ=m.Curve("strengthZ");v.frequency=m.frequency;v.octaveCount=m.octaves;v.scrollSpeed=m.Curve("scrollSpeed");}
            else if(m.name=="InheritVelocityModule"){var v=ps.inheritVelocity;v.enabled=true;v.mode=(ParticleSystemInheritVelocityMode)m.mode;v.curve=m.Curve("m_Curve");}
            else if(m.name=="SizeBySpeedModule"){var v=ps.sizeBySpeed;v.enabled=true;v.separateAxes=m.separate;v.x=m.Curve("curve");v.y=m.Curve("y");v.z=m.Curve("z");v.range=m.range;}
            else if(m.name=="RotationBySpeedModule"){var v=ps.rotationBySpeed;v.enabled=true;v.separateAxes=m.separate;v.x=m.Curve("x");v.y=m.Curve("y");v.z=m.Curve("curve");v.range=m.range;}
        }
        var sheet=ps.textureSheetAnimation;sheet.enabled=uv.enabled;if(uv.enabled){sheet.numTilesX=uv.x;sheet.numTilesY=uv.y;sheet.animation=(ParticleSystemAnimationType)uv.mode;sheet.rowIndex=uv.row;sheet.frameOverTime=uv.frame.UnityCurve();sheet.startFrame=uv.start.UnityCurve();sheet.cycleCount=Mathf.RoundToInt(uv.cycles);}
        var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=(ParticleSystemRenderMode)render.mode;r.alignment=(ParticleSystemRenderSpace)render.alignment;r.lengthScale=render.length;r.velocityScale=render.velocity;r.maxParticleSize=render.maximum;
        if(!String.IsNullOrEmpty(render.mesh))r.mesh=assets.MeshFile(render.mesh);return ps;
    }
}
[Serializable] public sealed class NativeParticleCollision
{
    public bool restored;public int[] planes;public NativeCurve dampen,bounce,lifetime_loss;public float minimum,maximum,radius;
    public void Apply(ParticleSystem particle,GameObject[] nodes)
    {
        if(!restored)throw new NotSupportedException("Unreviewed particle collision");
        var module=particle.collision;module.enabled=true;module.type=ParticleSystemCollisionType.Planes;module.mode=ParticleSystemCollisionMode.Collision3D;
        module.dampen=dampen.UnityCurve();module.bounce=bounce.UnityCurve();module.lifetimeLoss=lifetime_loss.UnityCurve();
        module.minKillSpeed=minimum;module.maxKillSpeed=maximum;module.radiusScale=radius;
        for(int i=0;i<planes.Length;i++)module.SetPlane(i,planes[i]<0?null:nodes[planes[i]].transform);
    }
}
[Serializable] public sealed class NativeLight
{
    public bool restored,enabled;public int kind;public Color color;public float intensity,range,spot;
    public void Create(GameObject node)
    {if(!restored||kind!=2)throw new NotSupportedException("Unreviewed source light");var light=node.GetComponent<Light>();if(light==null)light=node.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.spotAngle=spot;light.enabled=enabled;light.cullingMask=1<<30;}
}
[Serializable] public sealed class NativeFloat{public string name;public float value;}
[Serializable] public sealed class NativeColor{public string name;public Color value;}
[Serializable] public sealed class NativeTexture{public string name,path;public Vector2 scale,offset;}
[Serializable] public sealed class NativeShaderPass{public int index;public string light_mode;}
[Serializable] public sealed class NativeMaterial{public string name,shader,src_property,dst_property,src_alpha_property,dst_alpha_property;public string[] properties,keywords;public int src,dst,src_alpha,dst_alpha,render_queue;public NativeShaderPass[] passes;public NativeFloat[] floats;public NativeColor[] colors;public NativeTexture[] textures;}
[Serializable] public sealed class NativeAnimatorState{public string name,full_name,layer,clip;public int layer_index;public float speed=1,offset;public bool loop;}
[Serializable] public sealed class NativeLegacyAnimation{public int wrap_mode,culling_type;public bool wrap_known,play_automatically,animate_physics;public string default_clip;public NativeAssetSource source;}
[Serializable] public sealed class NativeSkinRenderer{public int[] bones;public int root_bone,quality;public bool update_when_offscreen;public NativeAssetSource source;}
[Serializable] public sealed class NativeCameraShakerData{public bool restored,hold,is_override;public Vector3 amount;public NativeKey[] curve;public float hold_at_sec;public NativeAssetSource source;}
[Serializable] public sealed class NativeNode{public string name,path,mesh;public string[] animation_refs;public NativeAnimatorState[] animator_states;public NativeLegacyAnimation legacy_animation;public int parent,sorting_order,source_layer;public bool active,visible,skinned_renderer;public Vector3 position,scale;public Quaternion rotation;public NativeParticle particle;public NativeTrailData trail;public NativeSkinRenderer skin;public NativeCameraShakerData camera_shaker;public NativeMaterial[] materials;public NativeLight light;}
[Serializable] public sealed class NativeAnimationTrack{public string path,property;public NativeKey[] keys;AnimationCurve curve;
 public float Sample(float time){if(curve==null)curve=NativeCurve.Keys(keys);return curve.Evaluate(time);}}
[Serializable] public sealed class NativeAnimationEvent{public float time,number;public string function,text;public int integer,options;public bool object_null;public NativeAssetSource object_source;}
[Serializable] public sealed class NativeAnimationEventReceiver{public int node;public string kind;public NativeAnimationEventTarget[] targets;}
[Serializable] public sealed class NativeAnimationEventTarget{public string name;public int node;public bool object_null;}
[Serializable] public sealed class NativeAnimation{public string id,name;public bool loop,wrap_known;public int wrap_mode;public float duration,event_horizon;public NativeAnimationTrack[] tracks;public NativeAnimationEvent[] events;}
public static class NativeLegacyWrap
{
    public static bool Supported(int mode){return mode==0||mode==1||mode==2||mode==4||mode==8;}
    public static int Resolve(int clip,int component,int state=0)
    {
        if(!Supported(clip)||!Supported(component)||!Supported(state))throw new ArgumentException("Unknown original legacy WrapMode");
        return state!=0?state:clip!=0?clip:component!=0?component:1;
    }
    public static float Sample(float time,float length,int mode)
    {
        if(length<=0)return 0;
        if(mode==2)return Mathf.Repeat(time,length);
        if(mode==4)return Mathf.PingPong(time,length);
        return Mathf.Clamp(time,0,length);
    }
}
[Serializable] public sealed class NativeAssetSource{public string cab,pathid,type,bundle;}
[Serializable] public sealed class NativeParam{public string name,text,variable,asset_type,asset_path,array_type;public NativeAssetSource asset_source;public NativePropertyDescriptor property_data;public bool object_null,none_known,is_none,wrap_known,integer_known,array_known;public int integer,kind,node,pre_wrap,post_wrap,array_count;public float number;public Vector3 vector;public Color color;public NativeKey[] curve;public bool unresolved;
 public float CurveDuration{get{return curve==null||curve.Length==0?0:curve[curve.Length-1].time;}}
 public static WrapMode InfinityMode(int serialized){switch(serialized){case 0:return WrapMode.PingPong;case 1:return WrapMode.Loop;case 2:return WrapMode.ClampForever;case 3:return WrapMode.Default;default:throw new ArgumentException("Unknown serialized curve infinity "+serialized);}}
 AnimationCurve cached;public float SampleCurve(float time){if(cached==null){cached=NativeCurve.Keys(curve);if(wrap_known){cached.preWrapMode=InfinityMode(pre_wrap);cached.postWrapMode=InfinityMode(post_wrap);}}return cached.Evaluate(time);}}
[Serializable] public sealed class NativeAction{public string name;public NativeParam[] @params;
 public NativeParam Param(params string[] names){foreach(string n in names)foreach(var p in @params)if(p.name==n)return p;return new NativeParam{node=-1};}}
[Serializable] public sealed class NativeTransition{public string @event,state;}
[Serializable] public sealed class NativeState{public string name;public NativeAction[] actions;public NativeTransition[] transitions;}
[Serializable] public sealed class NativeVector{public string name;public Vector3 value;}
[Serializable] public sealed class NativeTextureVariable{public string name,path;public bool object_null,unresolved;}
[Serializable] public sealed class NativeMachine{public int owner;public bool owner_known;public string start;public NativeState[] states;public NativeTransition[] events;public NativeFloat[] floats;public NativeColor[] colors;public NativeVector[] vectors;public NativeTextureVariable[] textures;}
[Serializable] public sealed class NativeCaptureOriginal
{
    public int m_CreateRenderPlane,m_RenderMaterial,m_AlphaClipRenderStyle,m_RenderQueue,m_RenderQueueOffset,m_RenderTextureFormat;
    public float m_AlphaClip,m_AlphaClipIntensity,m_AlphaClipAlphaIntensity,m_BloomIntensity,m_BloomThreshold,m_BloomBlur,m_BloomResolutionRatio;
    public Color m_BloomColor;
}
[Serializable] public sealed class NativeCapture{public int owner,source,target,resolution;public float width,height,near,far;public Vector3 position,camera_offset;public Color clear;public string texture_name;public bool uniform_scale,hide_source;public NativeCaptureOriginal original_parameters;public NativeMaterial material;}
[Serializable] public sealed class NativeTimelineControl{public int node,post;public int[] particle_roots;public bool active,search,update_director,update_time_control,prefab_null,compatible;}
[Serializable] public sealed class NativeTimelineShakeData{public Vector3 amount;public float interval;public NativeKey[] curve;public int pre_wrap,post_wrap;public bool compatible;}
[Serializable] public sealed class NativeTimelineClip{public string animation;public float start,duration,clip_in,speed,pre_time,post_time;public int pre,post;public bool compatible,post_unlimited;public NativeTimelineControl control;public NativeTimelineShakeData shaker;}
[Serializable] public sealed class NativeTimelineTrack{public string kind;public int node;public bool muted;public NativeTimelineClip[] clips;}
[Serializable] public sealed class NativeTimeline{public int node,wrap;public float initial_time,duration;public NativeTimelineTrack[] tracks;}
[Serializable] public sealed class NativeSpellStateAnimation{public int node,target,layer;public string clip;public float speed,crossfade;public bool enabled,particles,clip_null,unresolved;}
[Serializable] public sealed class NativeSourceSpellState{public int node,type,audio_count;public float delay;public bool enabled;public NativeSpellStateAnimation[] animations;public NativeAssetSource source;}
[Serializable] public sealed class NativeProgram{public string reference,audio_file,event_audio_file;public NativeNode[] nodes;public NativeAnimation[] animations;public NativeMachine[] machines;public NativeCapture[] captures;public NativeOffscreenFXData[] offscreen_fx;public NativeTimeline[] timelines;public NativeSourceSpellState[] spell_states;public NativeAnimationEventReceiver[] animation_event_receivers;public NativeSourceSpellTransform[] spell_transforms;public string[] unknown_actions;}

// Original SpellAction.GetCard selects source, target, source hero, power target or spell actor.
public sealed class NativeSpellContext
{
    readonly int source,target,hero,powerTarget,actor;
    readonly string sourceCardId;
    string targetCardId;
    public readonly string Lifecycle;
    public readonly NativeSpellObjectSelection Placement;
    public readonly NativePowerTaskTargets PowerTargets;
    public NativePowerExecution CreatePowerExecution(NativePowerServices services)
    {
        if(PowerTargets==null||PowerTargets.TaskList==null)throw new InvalidOperationException("No explicit original history task list is attached");
        return PowerTargets.TaskList.CreateExecution(services);
    }
    readonly Func<int,GameObject> actorObjects;
    readonly Func<int,int> runtimeCardTypes;
    public NativeSpellContext WithLifecycle(string lifecycle)
    {return new NativeSpellContext(source,target,hero,powerTarget,actor,lifecycle,actorObjects,Placement,PowerTargets,runtimeCardTypes,sourceCardId,targetCardId);}
    public void SetTargetCardId(string cardId){targetCardId=cardId;}
    public NativeSpellContext(int sourceType,int targetType=0,int heroType=0,int powerTargetType=0,int actorType=-1,string lifecycle=null,Func<int,GameObject> actorObjects=null,NativeSpellObjectSelection placement=null,NativePowerTaskTargets powerTargets=null,Func<int,int> runtimeCardTypes=null,string sourceCardId=null,string targetCardId=null)
    {source=sourceType;target=targetType;hero=heroType;powerTarget=powerTargetType;actor=actorType;Lifecycle=lifecycle;this.actorObjects=actorObjects;Placement=placement;PowerTargets=powerTargets;this.runtimeCardTypes=runtimeCardTypes;this.sourceCardId=sourceCardId;this.targetCardId=targetCardId;}
    public string CardId(int which){return which==0?sourceCardId:which==1?targetCardId:null;}
    public GameObject ActorObject(int which)
    {
        if(actorObjects==null||CardType(which)==0)return null;
        int role=which==1?1:(which==2||which==4)&&source!=0?2:which==3&&powerTarget!=0?3:which==5&&actor>=0?5:0;
        return actorObjects(role);
    }
    public int CardType(int which)
    {
        if(runtimeCardTypes!=null)return runtimeCardTypes(which);
        if(which==1)return target;
        if((which==2||which==4)&&source!=0)return hero;
        if(which==3&&powerTarget!=0)return powerTarget;
        if(which==5&&actor>=0)return actor;
        return source;
    }
    public static NativeSpellContext FromEvent(SemanticEvent data,StateView before,StateView after,Func<string,int> cardType,Func<EntityView,GameObject> actorObject=null,string lifecycle=null,System.Collections.Generic.IEnumerable<int> targetEntities=null,NativePowerTaskList powerTasks=null,Func<int,bool> entityRemoved=null)
    {
        var players=(before==null?new PlayerView[0]:before.players??new PlayerView[0]).Concat(after==null?new PlayerView[0]:after.players??new PlayerView[0]).ToArray();
        Func<PlayerView,System.Collections.Generic.IEnumerable<EntityView>> entities=p=>new[]{p.hero,p.power,p.weapon}.Concat(p.hand??new EntityView[0]).Concat(p.field??new EntityView[0]).Where(e=>e!=null);
        var owner=players.FirstOrDefault(p=>entities(p).Any(e=>e.entity==data.source))??players.FirstOrDefault(p=>p.seat==data.owner);
        var target=players.SelectMany(entities).FirstOrDefault(e=>data.target!=0&&e.entity==data.target);
        var source=players.SelectMany(entities).FirstOrDefault(e=>e.entity==data.source);
        Func<EntityView,int> type=e=>e==null?0:cardType(e.card_id);
        Func<int,GameObject> objects=actorObject==null?null:new Func<int,GameObject>(role=>
            {var entity=role==1?target:role==2?(owner==null?null:owner.hero):role==0?source:null;return entity==null?null:actorObject(entity);});
        Func<EntityView,NativeSpellEntityObject> placementObject=entity=>
        {
            if(entity==null||actorObject==null)return null;
            var controller=players.FirstOrDefault(p=>entities(p).Any(e=>e.entity==entity.entity));
            return new NativeSpellEntityObject{entity=entity.entity,type=type(entity),controllerSide=controller==null?(int?)null:controller.seat,resolve=()=>actorObject(entity),
                hero=()=>controller==null||controller.hero==null?null:actorObject(controller.hero),
                power=()=>controller==null||controller.power==null?null:actorObject(controller.power)};
        };
        var placement=new NativeSpellObjectSelection{source=placementObject(source),visualTarget=placementObject(target)};
        if(targetEntities!=null)placement.SetTargets(targetEntities.Select(id=>placementObject(players.SelectMany(entities).FirstOrDefault(e=>e.entity==id))));
        NativePowerTaskTargets collected=null;
        if(powerTasks!=null)
        {
            collected=new NativePowerTaskTargets(id=>placementObject(players.SelectMany(entities).FirstOrDefault(e=>e.entity==id)),entityRemoved);
            if(!collected.AttachPowerTaskList(powerTasks))throw new InvalidOperationException("Original power source/target collection rejected the task list");
            collected.ApplyTo(placement);
            // Cached original Entity identities can outlive a presentation writer.
            // Register every cached source with this context's current borrow gate.
            foreach(var record in collected.GetSourceRecords())
            {
                var current=record==null?null:players.SelectMany(entities).FirstOrDefault(e=>e.entity==record.entity);
                if(current==null||actorObject==null||actorObject(current)!=record.Object)throw new InvalidOperationException("Cached original source cannot bind to the current scene writer");
            }
            source=placement.source==null?null:players.SelectMany(entities).FirstOrDefault(e=>e.entity==placement.source.entity);
            placement.source=placementObject(source);
            var selected=placement.visualTarget;target=selected==null?null:players.SelectMany(entities).FirstOrDefault(e=>e.entity==selected.entity);
            owner=source==null?null:players.FirstOrDefault(p=>entities(p).Any(e=>e.entity==source.entity));
        }
        return new NativeSpellContext(source==null?cardType(data.card_id):type(source),type(target),owner==null?0:type(owner.hero),actorObjects:objects,lifecycle:lifecycle,placement:placement,powerTargets:collected,
            sourceCardId:String.IsNullOrEmpty(data.card_id)?(source==null?null:source.card_id):data.card_id,targetCardId:target==null?null:target.card_id);
    }
}
