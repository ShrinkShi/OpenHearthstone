using System;
using System.Collections.Generic;
using UnityEngine;

// Source blend operators are replayed over zero and one to retain C + D*K.
// Float targets avoid clipping away the colored transmission when C exceeds one.
public sealed class NativeAffineCapture : IDisposable
{
    readonly RenderTexture color,white,transmission;readonly Material difference;
    readonly Dictionary<Material,float[]> states=new Dictionary<Material,float[]>();
    readonly List<Material> touched=new List<Material>();
    public RenderTexture Color{get{return color;}}
    public RenderTexture Transmission{get{return transmission;}}
    public static bool DiagonalAffine(int source,int destination)
    {return (source>=0&&source<=6||source==10)&&(destination==0||destination==1||destination==3||destination==5||destination==6||destination==10);}
    public NativeAffineCapture(int width,int height)
    {
        if(!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))throw new NotSupportedException("Affine capture needs unclipped float render targets");
        try
        {color=Target(width,height);white=Target(width,height);transmission=Target(width,height);difference=new Material(Resources.Load<Shader>("NativeEffectTransmission"));}
        catch{Dispose();throw;}
    }
    static RenderTexture Target(int width,int height)
    {var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();return target;}
    public void Render(Camera camera,Dictionary<Material,NativeMaterial> sources)
    {
        var previous=RenderTexture.active;var target=camera.targetTexture;var background=camera.backgroundColor;var clear=camera.clearFlags;
        touched.Clear();
        try
        {
            foreach(var pair in sources)
            {
                var material=pair.Key;var source=pair.Value;
                bool dynamic=!String.IsNullOrEmpty(source.src_property);
                int src=dynamic?material.GetInt("_OriginalSrcBlend"):source.src,dst=dynamic?material.GetInt("_OriginalDstBlend"):source.dst;
                int srcAlpha=dynamic?material.GetInt("_OriginalSrcAlpha"):source.src_alpha,dstAlpha=dynamic?material.GetInt("_OriginalDstAlpha"):source.dst_alpha;
                if(!DiagonalAffine(src,dst)||!DiagonalAffine(srcAlpha,dstAlpha))throw new NotSupportedException("Original blend is not a diagonal affine operator: "+source.name);
                float[] state;if(!states.TryGetValue(material,out state)){state=new float[5];states[material]=state;}
                state[0]=material.GetFloat("_SrcBlend");state[1]=material.GetFloat("_DstBlend");state[2]=material.GetFloat("_CaptureSrcAlpha");state[3]=material.GetFloat("_CaptureDstAlpha");state[4]=material.GetFloat("_LuminanceAlpha");touched.Add(material);
                material.SetFloat("_SrcBlend",src);material.SetFloat("_DstBlend",dst);
                material.SetFloat("_CaptureSrcAlpha",srcAlpha);material.SetFloat("_CaptureDstAlpha",dstAlpha);material.SetFloat("_LuminanceAlpha",0);
            }
            camera.clearFlags=CameraClearFlags.SolidColor;camera.targetTexture=color;camera.backgroundColor=UnityEngine.Color.clear;camera.Render();
            camera.targetTexture=white;camera.backgroundColor=UnityEngine.Color.white;camera.Render();
            difference.SetTexture("_BlackTex",color);Graphics.Blit(white,transmission,difference);
        }
        finally
        {
            foreach(var m in touched)
            {var s=states[m];m.SetFloat("_SrcBlend",s[0]);m.SetFloat("_DstBlend",s[1]);m.SetFloat("_CaptureSrcAlpha",s[2]);m.SetFloat("_CaptureDstAlpha",s[3]);m.SetFloat("_LuminanceAlpha",s[4]);}
            camera.targetTexture=target;camera.backgroundColor=background;camera.clearFlags=clear;RenderTexture.active=previous;
        }
    }
    public void Dispose(){if(RenderTexture.active==color||RenderTexture.active==white||RenderTexture.active==transmission)RenderTexture.active=null;states.Clear();touched.Clear();AssetRegistry.ReleaseTransient(difference);AssetRegistry.ReleaseTransient(color);AssetRegistry.ReleaseTransient(white);AssetRegistry.ReleaseTransient(transmission);}
}
