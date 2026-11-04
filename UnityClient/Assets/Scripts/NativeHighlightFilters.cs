using System;
using UnityEngine;
using UnityEngine.Rendering;

// Source postprocess inputs use UV1. An explicit quad preserves that channel.
public sealed class NativeHighlightFilters : IDisposable
{
    readonly Material material;
    readonly Mesh quad;
    public NativeHighlightFilters()
    {
        var shader=Resources.Load<Shader>("OriginalHighlightFilter");
        if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original highlight filter unavailable");
        material=new Material(shader);
        quad=new Mesh{name="Original highlight UV1 filter quad"};
        quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};
        quad.triangles=new[]{0,2,1,2,3,1};quad.uv2=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
    }
    public void Apply(Texture source,Texture blend,RenderTexture destination,int pass,float blur)
    {
        if(source==null||destination==null||pass<0||pass>2||((pass==1||pass==2)&&blend==null))throw new ArgumentException("Invalid original filter inputs");
        if(source==destination||blend==destination)throw new ArgumentException("Original filter cannot read its render target");
        var commands=new CommandBuffer{name="Original highlight postprocess"};
        var properties=new MaterialPropertyBlock();
        properties.SetTexture("_MainTex",source);if(blend!=null)properties.SetTexture("_BlendTex",blend);
        properties.SetVector("_MainTex_TexelSize",new Vector4(1f/source.width,1f/source.height,source.width,source.height));
        properties.SetVector("_BlurOffsets",Vector4.one*-blur);
        try
        {
            commands.SetRenderTarget(destination);commands.SetViewport(new Rect(0,0,destination.width,destination.height));
            commands.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
            commands.DrawMesh(quad,Matrix4x4.identity,material,0,pass,properties);Graphics.ExecuteCommandBuffer(commands);
        }
        finally{commands.Release();}
    }
    public void Dispose(){AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(quad);}
}
