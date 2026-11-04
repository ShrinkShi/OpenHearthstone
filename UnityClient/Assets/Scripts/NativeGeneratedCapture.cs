using System;
using UnityEngine;

// 本技能实际依赖的RenderToTexture AlphaClipBloom自动平面；已有显式target捕获不变。
public sealed class NativeGeneratedCapture:IDisposable
{
    readonly NativeCapture data;readonly GameObject[] nodes;readonly Func<Renderer,bool> visible;readonly Func<Material,NativeMaterial> source;
    readonly Func<NativeMaterial,Material> create;readonly Action<Material> bound,release;
    GameObject plane,bloomPlane;Mesh mesh;Material material,bloomMaterial,processing;NativeEffectCapture capture;
    RenderTexture bloom,first,second;bool disposed;
    public bool Active{get{return plane!=null;}}
    public RenderTexture Texture{get{return capture?.Texture;}}
    public GameObject Plane{get{return plane;}}
    public static bool Supports(NativeCapture data)
    {
        var p=data?.original_parameters;
        return p!=null&&p.m_CreateRenderPlane==1&&p.m_AlphaClipRenderStyle==0&&p.m_RenderTextureFormat==7&&
            (p.m_RenderMaterial==5||p.m_RenderMaterial==0&&data.material!=null&&
                data.material.shader=="Custom/FX/FX_AreaEffect_Render"&&data.hide_source);
    }
    public NativeGeneratedCapture(NativeCapture data,GameObject[] nodes,Func<Renderer,bool> visible,Func<Material,NativeMaterial> source,
        Func<NativeMaterial,Material> create,Action<Material> bound,Action<Material> release)
    {
        if(!Supports(data))
            throw new NotSupportedException("Unreviewed generated RenderToTexture branch");
        this.data=data;this.nodes=nodes;this.visible=visible;this.source=source;this.create=create;this.bound=bound;this.release=release;
    }
    NativeMaterial Definition(bool glow)
    {
        var p=data.original_parameters;
        return new NativeMaterial{name=glow?"Original R2T bloom plane":"Original R2T alpha clip plane",shader=glow?"Hidden/R2TAdditive":"Hidden/R2TAlphaClip",
            src=5,dst=glow?1:10,src_alpha=5,dst_alpha=glow?1:10,render_queue=p.m_RenderQueueOffset+p.m_RenderQueue+(glow?1:0),
            properties=new[]{"_MainTex","_Color","_Intensity","_AlphaIntensity","_Cutoff","_LightingBlend"},textures=new NativeTexture[0],
            floats=new[]{new NativeFloat{name="_Intensity",value=glow?2:p.m_AlphaClipIntensity},new NativeFloat{name="_Cutoff",value=p.m_AlphaClip},
                new NativeFloat{name="_AlphaIntensity",value=p.m_AlphaClipAlphaIntensity},new NativeFloat{name="_LightingBlend",value=0}},
            colors=new[]{new NativeColor{name="_Color",value=glow?p.m_BloomColor:Color.white}}};
    }
    GameObject MakePlane(string name,Transform parent,Vector3 position,Material material,int order)
    {
        var go=new GameObject(name);go.layer=30;go.transform.SetParent(parent,false);go.transform.localPosition=position;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;return go;
    }
    void Initialize()
    {
        var p=data.original_parameters;float x=data.width/2,z=data.height/2;
        try
        {
            mesh=new Mesh{name="Original RenderToTexture.CreateMeshPlane"};mesh.vertices=new[]{new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(-x,0,z),new Vector3(x,0,z)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};mesh.triangles=new[]{3,1,2,2,1,0};mesh.RecalculateBounds();
            material=create(p.m_RenderMaterial==0?data.material:Definition(false));
            if(p.m_RenderMaterial==0)material.renderQueue=p.m_RenderQueueOffset+p.m_RenderQueue;
            plane=MakePlane(nodes[data.owner].name+"_RenderPlane",nodes[data.owner].transform,data.position,material,p.m_RenderQueue);
            capture=new NativeEffectCapture(data,nodes,visible,source,plane.GetComponent<Renderer>());bound(material);
            if(p.m_BloomIntensity>0)
            {
                bloomMaterial=create(Definition(true));bloomPlane=MakePlane("Original BloomPlane",plane.transform,new Vector3(0,.15f,0),bloomMaterial,p.m_RenderQueue+1);
                int width=(int)(capture.Texture.width*Mathf.Clamp01(p.m_BloomResolutionRatio)),height=(int)(capture.Texture.height*Mathf.Clamp01(p.m_BloomResolutionRatio));
                bloom=Target(width,height);first=Target(width,height);second=Target(width,height);
                bloomMaterial.mainTexture=bloom;bloomMaterial.SetFloat("_HasSourceTexture",1);bound(bloomMaterial);
                processing=new Material(Resources.Load<Shader>("NativeR2TBloom"));
            }
        }
        catch{Clear();throw;}
    }
    static RenderTexture Target(int width,int height)
    {var target=new RenderTexture(Mathf.Max(1,width),Mathf.Max(1,height),0,RenderTextureFormat.ARGB32);target.Create();return target;}
    public void Render()
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeGeneratedCapture));
        if(!nodes[data.owner].activeInHierarchy){Clear();return;}
        if(plane==null)Initialize();capture.Render();
        if(bloom==null)return;
        var p=data.original_parameters;var previous=RenderTexture.active;
        try
        {
            // 同源重画到Bloom目标，不用主RT降采样冒充第二次原捕获。
            capture.RenderInto(bloom);
            processing.SetFloat("_Cutoff",p.m_AlphaClip);processing.SetFloat("_AlphaIntensity",p.m_AlphaClipAlphaIntensity);
            processing.SetFloat("_Intensity",p.m_BloomIntensity/(1-p.m_BloomThreshold));processing.SetFloat("_Threshold",p.m_BloomThreshold);
            processing.SetVector("_Offset",new Vector4(1.5f/bloom.width,1.5f/bloom.height,0,0));Graphics.Blit(bloom,second,processing,0);
            processing.SetVector("_Offset",new Vector4(4*p.m_BloomBlur/bloom.width,0,0,0));Graphics.Blit(second,first,processing,1);
            processing.SetVector("_Offset",new Vector4(0,4*p.m_BloomBlur/bloom.height,0,0));Graphics.Blit(first,bloom,processing,1);
        }
        finally{RenderTexture.active=previous;}
    }
    public void Clear()
    {
        capture?.Dispose();capture=null;
        AssetRegistry.ReleaseTransient(plane);plane=null;bloomPlane=null;AssetRegistry.ReleaseTransient(mesh);mesh=null;
        if(material!=null)release(material);material=null;if(bloomMaterial!=null)release(bloomMaterial);bloomMaterial=null;
        AssetRegistry.ReleaseTransient(processing);processing=null;AssetRegistry.ReleaseTransient(bloom);bloom=null;
        AssetRegistry.ReleaseTransient(first);first=null;AssetRegistry.ReleaseTransient(second);second=null;
    }
    public void Dispose(){if(disposed)return;disposed=true;Clear();}
}
