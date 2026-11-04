using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Restore the original RenderToTexture source -> material binding, separately
// from the final board camera. Original postprocess/stencil variants are audited.
public sealed class NativeEffectCapture : IDisposable
{
    readonly NativeCapture data;
    readonly Transform owner;
    readonly Transform sourceRoot;
    readonly bool generated;
    readonly Renderer[] sources,targets;
    readonly Func<Renderer,bool> visible;
    readonly Func<Material,NativeMaterial> sourceMaterial;
    public sealed class DrawCommand{public Renderer renderer;public Material material;public int meshIndex,sourcePass;}
    readonly List<DrawCommand> draws=new List<DrawCommand>();
    static readonly string[] LightModes={"SRPDefaultUnlit","UniversalForward","LightweightForward"};
    public static int[] SourcePasses(NativeShaderPass[] passes)
    {
        if(passes==null)return new[]{0}; // Legacy cache lacks the pass audit.
        var found=new List<int>();
        foreach(string mode in LightModes)
            foreach(var pass in passes)
                if((String.IsNullOrEmpty(pass.light_mode)?"SRPDefaultUnlit":pass.light_mode)==mode){found.Add(pass.index);break;}
        return found.ToArray();
    }
    public static int CompareCommands(DrawCommand a,DrawCommand b)
    {
        int group=(a.material.renderQueue>=3000?1:0).CompareTo(b.material.renderQueue>=3000?1:0);
        if(group!=0)return group;
        int order=a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder);
        return order!=0?order:a.material.renderQueue.CompareTo(b.material.renderQueue);
    }
    sealed class BlendState{public float source,destination,luminance,sourceAlpha,destinationAlpha;}
    readonly Dictionary<Material,BlendState> blending=new Dictionary<Material,BlendState>();
    readonly RenderTexture texture;
    readonly CommandBuffer commands=new CommandBuffer{name="Original RenderToTexture"};
    public RenderTexture Texture{get{return texture;}}
    public NativeEffectCapture(NativeCapture source,GameObject[] nodes,Func<Renderer,bool> visibility,Func<Material,NativeMaterial> materialSource=null,Renderer generatedTarget=null)
    {
        data=source;owner=nodes[data.owner].transform;sourceRoot=nodes[data.source].transform;visible=visibility;sourceMaterial=materialSource;generated=generatedTarget!=null;
        sources=nodes[data.source].GetComponentsInChildren<Renderer>(true);
        // Original IL binds the target renderer, falling back to one child.
        // Binding every descendant overwrites the IceSpikes particle atlas.
        var target=generatedTarget??nodes[data.target].GetComponent<Renderer>()??nodes[data.target].GetComponentInChildren<Renderer>(true);
        targets=target==null?new Renderer[0]:new[]{target};
        foreach(var renderer in sources)
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
                foreach(int pass in SourcePasses(sourceMaterial==null?null:sourceMaterial(materials[i]).passes))
                    // Only source base pass 0 is restored by NativeEffect.shader.
                    // Do not paint an unsupported Glow/forward program as base.
                    if(pass==0)draws.Add(new DrawCommand{renderer=renderer,material=materials[i],meshIndex=i,sourcePass=pass});
        }
        int width=Mathf.Clamp(data.resolution,16,2048),height=Mathf.Max(16,Mathf.RoundToInt(width*data.height/data.width));
        if(generatedTarget!=null)
        {
            float longest=Mathf.Max(data.width,data.height);width=(int)(data.resolution*data.width/longest);height=(int)(data.resolution*data.height/longest);
        }
        texture=new RenderTexture(width,height,generatedTarget==null?24:16,generatedTarget==null?RenderTextureFormat.ARGB32:RenderTextureFormat.Default);texture.Create();
        foreach(var renderer in targets)foreach(var material in renderer.sharedMaterials)
        {
            material.SetTexture(String.IsNullOrEmpty(data.texture_name)?"_MainTex":data.texture_name,texture);
            material.SetFloat("_HasSourceTexture",1);
        }
    }
    public void Render()
    {RenderInto(texture);}
    public void RenderInto(RenderTexture destination)
    {
        var scale=owner.lossyScale;if(data.uniform_scale)scale=Vector3.one*Mathf.Max(scale.x,scale.y,scale.z);
        float width=data.width*scale.x,height=data.height*scale.y;
        // Original IL: height/2 unless an override is present. The clip range
        // may straddle zero; a projection matrix preserves that signed range.
        var position=owner.position+owner.rotation*(data.position+data.camera_offset);
        // 原隐藏源分支的捕获相机跟随离屏源，而展示平面保留在技能位置。
        if(generated&&data.original_parameters.m_RenderMaterial==0&&data.hide_source)
            position=sourceRoot.position+data.camera_offset;
        // CreateCamera IL rotates 90 degrees around local X after owner rotation.
        var rotation=owner.rotation*Quaternion.Euler(90,0,0);
        var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(position,rotation,Vector3.one).inverse;
        float clipScale=generated?scale.y:scale.z;
        var projection=Matrix4x4.Ortho(-width/2,width/2,-height/2,height/2,data.near*clipScale,data.far*clipScale);
        commands.Clear();commands.SetRenderTarget(destination);commands.ClearRenderTarget(true,true,data.clear);
        commands.SetViewProjectionMatrices(view,projection);
        blending.Clear();
        draws.Sort(CompareCommands);
        foreach(var draw in draws)
        {
            var renderer=draw.renderer;
            if(!renderer.gameObject.activeInHierarchy||!visible(renderer))continue;
                var material=draw.material;
                if(!blending.ContainsKey(material))blending[material]=new BlendState{source=material.GetFloat("_SrcBlend"),destination=material.GetFloat("_DstBlend"),luminance=material.GetFloat("_LuminanceAlpha"),sourceAlpha=material.GetFloat("_CaptureSrcAlpha"),destinationAlpha=material.GetFloat("_CaptureDstAlpha")};
                // Internal captures have an opaque clear. They use the actual
                // source blend; alpha compatibility is only for the board RT.
                material.SetFloat("_SrcBlend",material.GetFloat("_OriginalSrcBlend"));
                material.SetFloat("_DstBlend",material.GetFloat("_OriginalDstBlend"));
                material.SetFloat("_LuminanceAlpha",0);
                material.SetFloat("_CaptureSrcAlpha",material.GetFloat("_OriginalSrcAlpha"));
                material.SetFloat("_CaptureDstAlpha",material.GetFloat("_OriginalDstAlpha"));
                commands.DrawRenderer(renderer,material,draw.meshIndex,0);
        }
        var previous=RenderTexture.active;
        try{Graphics.ExecuteCommandBuffer(commands);}finally
        {
            foreach(var entry in blending){entry.Key.SetFloat("_SrcBlend",entry.Value.source);entry.Key.SetFloat("_DstBlend",entry.Value.destination);entry.Key.SetFloat("_LuminanceAlpha",entry.Value.luminance);entry.Key.SetFloat("_CaptureSrcAlpha",entry.Value.sourceAlpha);entry.Key.SetFloat("_CaptureDstAlpha",entry.Value.destinationAlpha);}
            RenderTexture.active=previous;
        }
    }
    public void Dispose(){commands.Dispose();if(RenderTexture.active==texture)RenderTexture.active=null;AssetRegistry.ReleaseTransient(texture);}
}
