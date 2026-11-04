using System;
using UnityEngine;
using UnityEngine.Rendering;

// 原名称文字的CommandBuffer→RT→抗锯齿平面，当前明确采用非低质量尺寸。
public sealed class NativePowerTextCapture:IDisposable
{
    readonly RenderTexture target;readonly Mesh mesh;readonly MeshRenderer textRenderer;readonly Material material;
    readonly CommandBuffer commands=new CommandBuffer();readonly NativePowerTextSettings source;
    bool disposed;
    public RenderTexture Target{get{return target;}}
    public NativePowerTextCapture(NativeEffectInstance effect,GameObject owner,MeshRenderer textRenderer,NativePowerTextSettings source)
    {
        this.textRenderer=textRenderer;this.source=source;
        float aspect=source.m_Width/source.m_Height;
        int width=(int)(aspect>1?source.m_Resolution:source.m_Resolution*aspect),height=(int)(aspect>1?source.m_Resolution/aspect:source.m_Resolution);
        target=new RenderTexture(width,height,0,RenderTextureFormat.Default,RenderTextureReadWrite.Default);target.Create();
        var plane=new GameObject("Original title render plane");plane.layer=30;plane.transform.SetParent(owner.transform,false);plane.transform.localRotation=Quaternion.Euler(-90,0,0);
        float x=source.m_Width*.5f,z=source.m_Height*.5f;
        mesh=new Mesh{name="Original UberText generated plane",vertices=new[]{new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(-x,0,z),new Vector3(x,0,z)},
            uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up},
            colors=new[]{source.m_GradientUpperColor,source.m_GradientUpperColor,source.m_GradientLowerColor,source.m_GradientLowerColor},triangles=new[]{3,1,2,2,1,0}};mesh.RecalculateBounds();
        plane.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=plane.AddComponent<MeshRenderer>();renderer.sortingOrder=source.m_RenderQueue;
        var definition=new NativeMaterial{name="Original title antialias plane",shader="Hidden/TextAntialiasing",src=5,dst=10,src_alpha=5,dst_alpha=10,render_queue=3000+source.m_RenderQueue,
            properties=new[]{"_MainTex","_Color","_OffsetX","_OffsetY","_Edge"},floats=new NativeFloat[0],colors=new NativeColor[0],textures=new NativeTexture[0]};
        material=effect.ReplaceVisualMaterial(renderer,definition);material.mainTexture=target;material.SetColor("_Color",Color.white);
        material.SetFloat("_OffsetX",source.m_AntiAliasAmount/width);material.SetFloat("_OffsetY",source.m_AntiAliasAmount/height);material.SetFloat("_Edge",source.m_AntiAliasEdge);
        textRenderer.enabled=false;
    }
    public void Render()
    {
        var transform=textRenderer.transform;var parent=transform.parent;var position=transform.localPosition;var rotation=transform.localRotation;var scale=transform.localScale;
        var center=new Vector3(-3000,3000,0);var prior=RenderTexture.active;
        try
        {
            transform.SetParent(null,false);transform.position=center;transform.rotation=Quaternion.identity;transform.localScale=Vector3.one;
            var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.LookAt(center,center+Vector3.forward,Vector3.up).inverse;
            var projection=Matrix4x4.Ortho(-source.m_Width*.5f,source.m_Width*.5f,-source.m_Height*.5f,source.m_Height*.5f,-.1f,.1f);
            commands.Clear();commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,new Color(source.m_OutlineColor.r,source.m_OutlineColor.g,source.m_OutlineColor.b,0));
            commands.SetViewProjectionMatrices(view,projection);commands.DrawRenderer(textRenderer,textRenderer.sharedMaterials[0],0,-1);Graphics.ExecuteCommandBuffer(commands);
        }
        finally{RenderTexture.active=prior;transform.SetParent(parent,false);transform.localPosition=position;transform.localRotation=rotation;transform.localScale=scale;}
    }
    public void Dispose(){if(disposed)return;disposed=true;commands.Dispose();AssetRegistry.ReleaseTransient(target);AssetRegistry.ReleaseTransient(mesh);}
}
