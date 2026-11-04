using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Concrete source dispatch for reviewed area-only roots. Missile/start/chain
// roots must use their own controllers; they cannot silently enter this path.
public sealed class NativeAreaPlayback : IDisposable
{
    [Serializable] sealed class Prefab{public string reference,file;}
    [Serializable] sealed class Prefabs{public Prefab[] prefabs;}
    readonly GameObject root;
    readonly NativeSuperSpellScene scene;
    NativeAreaInstance clone;
    readonly NativeSuperArea area;
    readonly NativeSuperAction action;
    readonly List<Mesh> paladinMaskMeshes=new List<Mesh>();
    public string Reference{get;private set;}
    public bool FriendlyAreaOnlyReconstruction{get;private set;}
    public int AreaCloneCreations{get;private set;}
    public readonly NativeSpellContext Context;
    public float Time{get{return clone.PlaybackTime;}}
    public int Pending{get{return area.Pending;}}
    public bool VisualTailActive{get{return FriendlyAreaOnlyReconstruction&&area.Pending==0&&clone!=null&&clone.Effect.ActiveSourceSpellState!=0;}}
    public int FinishedNotifications{get;private set;}
    public int StateFinishedNotifications{get;private set;}
    public NativeEffectInstance Effect{get{return clone.Effect;}}
    public NativeAreaPlayback(NativeProgram parent,NativeEffectEntry entry,AssetRegistry assets,int slot,bool? friendly,bool wide,NativeSpellContext context,Action<NativeAudioCue> audio,Func<Color?> ambient=null,Camera timelineCamera=null,NativeSuperSpellScene scene=null)
    {
        this.scene=scene;Context=scene==null?context:scene.Context;context=Context;
        NativeSuperSpellConfig sourceConfig=null;
        var raw=(parent.spell_transforms??new NativeSourceSpellTransform[0]).Single(t=>t.node==0&&t.enabled).raw;
        if(entry.card_id=="HERO_04bp")
        {
            var sourceCatalog=JsonUtility.FromJson<NativeSuperSpellCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
            sourceConfig=sourceCatalog.programs.Single(p=>p.card_id==entry.card_id&&p.file==entry.file);
            var sourcePrefab=sourceCatalog.prefabs.Single(p=>p.reference==sourceConfig.area.friendly.prefab_reference);
            var sourceArea=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,sourcePrefab.file)));
            if(raw==null||raw.m_ChainInfo==null||raw.m_ChainInfo.m_Enabled!=1||
                !NativeSuperSpell.SupportsPaladinFriendlyAreaReconstruction(parent,sourceConfig,sourceArea))
                throw new NotSupportedException("HERO_04bp FriendlyArea-only reconstruction failed its source gate; enabled-null Chain remains unsupported");
            FriendlyAreaOnlyReconstruction=true;
            if(scene==null||scene.Placement==null||context==null)
                throw new NotSupportedException("HERO_04bp FriendlyArea source scene is unbound");
        }
        else if(raw==null||raw.m_StartInfo==null||raw.m_MissileInfo==null||raw.m_ImpactInfo==null||raw.m_ChainInfo==null||
            raw.m_StartInfo.m_Enabled!=0||raw.m_MissileInfo.m_Enabled!=0||raw.m_ImpactInfo.m_Enabled!=0||raw.m_ChainInfo.m_Enabled!=0||
            raw.m_ActionInfo==null||raw.m_ActionInfo.m_StartDelayMin!=0||raw.m_ActionInfo.m_StartDelayMax!=0||
            (parent.spell_states??new NativeSourceSpellState[0]).Any(s=>s.node==0&&s.enabled))
            throw new NotSupportedException("Original root requires additional start/delay/missile/state services");
        var catalog=JsonUtility.FromJson<NativeSuperAreaCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-config.json")));
        var config=catalog.programs.Single(p=>p.file==entry.file).config;
        var info=NativeSuperArea.Determine(config,friendly);
        if(info==null||info.delay_min!=0||info.delay_max!=0||info.facing!=0||!info.use_super_location&&info.location!=0)
            throw new NotSupportedException("Original area requires additional delay/placement services");
        var prefabs=JsonUtility.FromJson<Prefabs>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-prefabs.json")));
        var prefab=prefabs.prefabs.Single(p=>p.reference==info.prefab_reference);Reference=prefab.reference;
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        root=new GameObject("Original area SuperSpell host");root.transform.position=new Vector3(20000+slot*1000,10000,20000);
        try
        {
            if(FriendlyAreaOnlyReconstruction)
            {
                var location=scene.Placement.Location(sourceConfig.location);
                if(location==null)throw new NotSupportedException("HERO_04bp source hero location is unbound");
                root.transform.position=location.transform.position;
            }
            area=new NativeSuperArea(new NativeSuperAreaServices{Root=root,Source=()=>context==null?null:context.ActorObject(0),Tasks=context==null||context.PowerTargets==null?null:context.PowerTargets.TaskList,
                GetSpell=i=>
                {
                    AreaCloneCreations++;
                    clone=new NativeAreaInstance(program,assets,slot,locations:scene==null?null:(Func<int,GameObject>)scene.Placement.Location,
                        facings:scene==null?null:(Func<int,GameObject>)scene.Placement.Facing,
                        attachTasks:tasks=>context!=null&&context.PowerTargets!=null&&context.PowerTargets.TaskList==tasks,audio:audio,context:context,
                        width:FriendlyAreaOnlyReconstruction?1024:512,height:FriendlyAreaOnlyReconstruction?576:wide?256:512,ambient:ambient,timelineCamera:timelineCamera);
                    if(FriendlyAreaOnlyReconstruction)paladinMaskMeshes.AddRange(NativePaladinHeroMaskChecks.NormalizeVertexColors(clone.Effect));
                    if(scene!=null&&scene.SourceCamera!=null)clone.Effect.ApplySourceCamera(scene.SourceCamera,scene.SourceCameraOrigin);
                    return clone.Endpoint;
                },
                ReleaseSpell=(c,reset)=>true,FinishIfPossible=()=>action.FinishIfPossible()});
            action=new NativeSuperAction(new NativeSuperActionServices{DetermineArea=()=>info,SpawnArea=area.Spawn,
                HasMissile=()=>false,HasImpact=()=>false,HasChain=()=>false,VisualTargetCount=()=>context==null||context.Placement==null?0:context.Placement.Targets.GetTargets().Count,
                DoStartSpellAction=()=>{},PendingEffects=()=>area.Pending,HasStateContent=t=>false,
                BaseSpellFinished=()=>FinishedNotifications++,BaseStateFinished=()=>StateFinishedNotifications++,CleanUp=()=>{}});
            action.SettingUpAction=true;action.UpdatePendingStateChangeFlags((int)NativeSpellStateType.Action);action.DoActionNow();
            action.SettingUpAction=false;action.FinishIfPossible();
        }
        catch{if(clone!=null)clone.Dispose();ReleasePaladinMaskMeshes();AssetRegistry.ReleaseTransient(root);throw;}
    }
    public void Seek(float age){clone.Seek(age);}
    public RenderTexture RenderAffine(float age){return clone.RenderAffine(age);}
    void ReleasePaladinMaskMeshes(){foreach(var mesh in paladinMaskMeshes)AssetRegistry.ReleaseTransient(mesh);paladinMaskMeshes.Clear();}
    public void Dispose(){if(clone!=null){clone.Dispose();clone=null;}ReleasePaladinMaskMeshes();AssetRegistry.ReleaseTransient(root);scene?.Dispose?.Invoke();}
}
