using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Original GlowTransparent output, kept separate from base color and final bloom.
public sealed class NativeEffectGlowCapture : IDisposable
{
    sealed class Binding{public Renderer renderer;public Material source,glow;public int mesh;}
    readonly List<Binding> bindings=new List<Binding>();
    readonly Func<Renderer,bool> visible;
    readonly CommandBuffer commands=new CommandBuffer{name="Original GlowTransparent"};
    readonly RenderTexture texture;
    public NativeEffectGlowCapture(NativeProgram program,GameObject[] nodes,int width,int height,Func<Renderer,bool> visibility)
    {
        visible=visibility;
        texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);texture.Create();
        for(int n=0;n<nodes.Length;n++)
        {
            var renderer=nodes[n].GetComponent<Renderer>();if(renderer==null)continue;
            var materials=renderer.sharedMaterials;
            for(int m=0;m<materials.Length;m++)
            {
                var source=program.nodes[n].materials[m];
                if(source.shader!="Hero/Particle/Particle Additive + 1"&&source.shader!="Hero/Particle/Particle Additive"&&source.shader!="Hero/Particle/Particle Multiply"&&source.shader!="Hero/AddMultiply/AddMultiplyDistortion"&&source.shader!="FX/CommonFX"&&source.shader!="Hero/AddMultiply/AddMultiplyFXScrolling"&&source.shader!="Hero/AddMultiply/AddMultiplyFXScrolling2Layer"&&source.shader!="Hero/Additive/Additive"&&source.shader!="Hero/Particle/Particles_Transparent"&&source.shader!="Hero/Particle/Particles_Transparent_ColorAsAlpha")continue;
                if(source.passes!=null&&!Array.Exists(source.passes,p=>p.light_mode=="GlowTransparent"))continue;
                bindings.Add(new Binding{renderer=renderer,source=materials[m],mesh=m,glow=new Material(Resources.Load<Shader>("NativeEffectGlow"))});
            }
        }
    }
    public RenderTexture Render(Camera camera)
    {
        commands.Clear();commands.SetRenderTarget(texture);commands.ClearRenderTarget(true,true,Color.clear);
        commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,camera.projectionMatrix);
        bindings.Sort((a,b)=>{int order=a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder);return order!=0?order:a.source.renderQueue.CompareTo(b.source.renderQueue);});
        foreach(var binding in bindings)
        {
            if(!binding.renderer.gameObject.activeInHierarchy||!binding.renderer.enabled||!visible(binding.renderer))continue;
            // FSM/Clip values can change every sample; never freeze glow at export defaults.
            var source=binding.source;var glow=binding.glow;
            glow.SetTexture("_MainTex",source.GetTexture("_MainTex"));
            glow.SetTextureScale("_MainTex",source.GetTextureScale("_MainTex"));glow.SetTextureOffset("_MainTex",source.GetTextureOffset("_MainTex"));
            glow.SetColor("_Color",source.GetColor("_Color"));glow.SetColor("_GlowColor",source.GetColor("_GlowColor"));
            glow.SetFloat("_GlowIntensity",source.GetFloat("_GlowIntensity"));glow.SetFloat("_GlowAddMul",source.GetFloat("_GlowAddMul"));
            commands.DrawRenderer(binding.renderer,glow,binding.mesh,0);
        }
        var previous=RenderTexture.active;
        try{Graphics.ExecuteCommandBuffer(commands);}finally{RenderTexture.active=previous;}
        return texture;
    }
    public void Dispose()
    {
        commands.Dispose();if(RenderTexture.active==texture)RenderTexture.active=null;AssetRegistry.ReleaseTransient(texture);
        foreach(var binding in bindings)AssetRegistry.ReleaseTransient(binding.glow);bindings.Clear();
    }
}
