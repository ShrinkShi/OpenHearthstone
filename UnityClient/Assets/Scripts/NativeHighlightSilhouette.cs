using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Restored ordinary Actor branch of HighlightRender.CreateSilhouetteTexture.
// Premium/override/include/exclude handling remains separate.
public sealed class NativeHighlightSilhouette : IDisposable
{
    readonly NativeActorIdentity actor;
    readonly MeshRenderer plane;
    readonly Material grey;
    readonly NativeHighlightFilters filters;
    RenderTexture output;
    public RenderTexture Texture{get{return output;}}
    public NativeHighlightSilhouette(NativeActorIdentity actor)
    {
        this.actor=actor;
        var source=NativeActorIdentity.FindChildBySubstring(actor.gameObject,"Highlight_Card_Plane");
        plane=source==null?null:source.GetComponent<MeshRenderer>();
        if(plane==null)throw new InvalidOperationException("Original Actor highlight plane missing");
        grey=new Material(Resources.Load<Shader>("OriginalSilhouetteColor"));filters=new NativeHighlightFilters();
    }
    static RenderTexture Temporary(int width,int height,int depth=0)
    {
        var texture=RenderTexture.GetTemporary(width,height,depth,RenderTextureFormat.R8,RenderTextureReadWrite.Default);
        texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;return texture;
    }
    public void Generate()
    {
        var root=actor.transform;var parent=root.parent;var position=root.localPosition;var rotation=root.localRotation;var scale=root.localScale;
        var textures=new List<RenderTexture>();var previous=RenderTexture.active;
        try
        {
            // Source SetWorldScale normalizes before sizing, and restores afterward.
            root.SetParent(null,true);root.localScale=Vector3.one;root.rotation=Quaternion.identity;
            var bounds=plane.bounds;float width=bounds.size.x,height=Mathf.Max(bounds.size.z,bounds.size.y);
            if(width<=0||height<=0)throw new InvalidOperationException("Invalid original silhouette bounds");
            int w=width>height?256:(int)(256*width/height),h=width>height?(int)(256*height/width):256;
            if(w<1||h<1)throw new InvalidOperationException("Invalid original silhouette dimensions");
            if(output==null||output.width!=w||output.height!=h)
            {AssetRegistry.ReleaseTransient(output);output=new RenderTexture(w,h,16,RenderTextureFormat.R8){name="Original Actor dynamic silhouette",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};output.Create();}
            var commands=new List<Tuple<Renderer,int>>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer==plane||!renderer.enabled)continue;
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<Mathf.Min(materials.Length,mesh.sharedMesh.subMeshCount);i++)
                    if(materials[i]!=null&&!String.IsNullOrEmpty(materials[i].GetTag("Highlight",false)))commands.Add(Tuple.Create((Renderer)renderer,i));
            }
            if(commands.Count==0)throw new InvalidOperationException("No original highlight-eligible submeshes");
            var view=Matrix4x4.TRS(plane.transform.position,plane.transform.rotation*Quaternion.Euler(90,180,0),new Vector3(1,1,-1)).inverse;
            float ortho=height*.5f,renderScale=plane.transform.lossyScale.x;
            Func<int,int,int,RenderTexture> temp=(x,y,d)=>{var t=Temporary(x,y,d);textures.Add(t);return t;};
            var a=temp((int)(w*.3f),(int)(h*.3f),16);var b=temp(a.width,a.height,16);var c=temp(a.width,a.height,0);
            Draw(a,.25f,ortho-.2f,view,renderScale,commands);Draw(b,.5f,ortho-.25f,view,renderScale,commands);filters.Apply(a,b,c,1,1.25f);
            a=temp((int)(w*.5f),(int)(h*.5f),16);b=temp(a.width,a.height,0);
            Draw(a,.75f,ortho-.01f,view,renderScale,commands);filters.Apply(c,a,b,1,1.25f);
            a=temp(w,h,16);c=temp(w,h,0);
            Draw(a,.75f,ortho+.05f,view,renderScale,commands);filters.Apply(b,a,c,1,1f);
            a=temp(w,h,0);filters.Apply(c,null,a,0,1.5f);
            b=temp((int)(w*.92f),(int)(h*.92f),16);Draw(b,1,ortho+.1f,view,renderScale,commands);
            filters.Apply(a,b,output,2,.8f);
        }
        finally
        {
            RenderTexture.active=previous;
            foreach(var texture in textures)RenderTexture.ReleaseTemporary(texture);
            root.SetParent(parent,false);root.localPosition=position;root.localRotation=rotation;root.localScale=scale;
        }
    }
    void Draw(RenderTexture target,float value,float ortho,Matrix4x4 view,float renderScale,List<Tuple<Renderer,int>> renderers)
    {
        if(ortho<=0)throw new InvalidOperationException("Original silhouette projection became invalid");
        grey.SetFloat("_OriginalGrey",value);
        var commands=new CommandBuffer{name="Original silhouette replacement draw"};
        try
        {
            commands.SetRenderTarget(target);commands.SetViewport(new Rect(0,0,target.width,target.height));commands.ClearRenderTarget(true,true,Color.clear);
            commands.SetViewProjectionMatrices(view,Matrix4x4.Ortho(-ortho,ortho,-ortho,ortho,1-renderScale,1+renderScale));
            foreach(var item in renderers)commands.DrawRenderer(item.Item1,grey,item.Item2,0);
            Graphics.ExecuteCommandBuffer(commands);
        }
        finally{commands.Release();}
    }
    public void Dispose(){AssetRegistry.ReleaseTransient(output);AssetRegistry.ReleaseTransient(grey);filters.Dispose();}
}
