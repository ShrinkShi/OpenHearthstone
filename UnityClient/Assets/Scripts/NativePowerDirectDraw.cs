using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 仅标准技能Actor使用当前GUI颜色目标；独立深度防止跨widget写入。
public sealed class NativePowerDirectDraw:IDisposable
{
    readonly CommandBuffer commands=new CommandBuffer{name="Original standard power direct GUI"};
    RenderTexture depth;bool disposed;
    RenderTexture guiTarget;RenderBuffer guiColor,guiDepth;bool guiFrame;string entry;int traceCount;
    string TargetState(){var rt=RenderTexture.active;return "active="+(rt==null?"screen":rt.width+"x"+rt.height)+" color="+Graphics.activeColorBuffer.GetNativeRenderBufferPtr()+" depth="+Graphics.activeDepthBuffer.GetNativeRenderBufferPtr();}
    public void BeginGUI(){guiTarget=RenderTexture.active;guiColor=Graphics.activeColorBuffer;guiDepth=Graphics.activeDepthBuffer;guiFrame=true;entry=TargetState();}
    public void Trace(string phase){if(guiFrame&&traceCount<8)Debug.Log("POWER GUI TARGET "+phase+" entry["+entry+"] now["+TargetState()+"]");}
    sealed class Item{public Renderer renderer;public Material material;public int submesh;public float distance;}
    public void Draw(NativeEffectInstance effect,Rect area,Matrix4x4 guiMatrix,int width,int height)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativePowerDirectDraw));
        var target=guiFrame?guiTarget:RenderTexture.active;int samples=target==null?1:target.antiAliasing;
        if(depth==null||depth.width!=width||depth.height!=height||depth.antiAliasing!=samples)
        {AssetRegistry.ReleaseTransient(depth);depth=new RenderTexture(width,height,24,RenderTextureFormat.Depth){antiAliasing=samples};depth.Create();}
        var camera=effect.Root.GetComponentInChildren<Camera>();
        var items=new List<Item>();
        foreach(var renderer in effect.Root.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.enabled)continue;
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)items.Add(new Item{renderer=renderer,material=materials[i],submesh=i,distance=Vector3.Dot(renderer.bounds.center-camera.transform.position,camera.transform.forward)});
        }
        var state=new Dictionary<Material,int[]>();
        var savedColor=guiFrame?guiColor:Graphics.activeColorBuffer;var savedDepth=guiFrame?guiDepth:Graphics.activeDepthBuffer;
        var savedView=Shader.GetGlobalMatrix("unity_MatrixV");var savedProjection=Shader.GetGlobalMatrix("unity_MatrixP");
        var toPixel=Matrix4x4.identity;toPixel.m00=area.width*.5f;toPixel.m03=area.center.x;toPixel.m11=-area.height*.5f;toPixel.m13=area.center.y;
        var toClip=Matrix4x4.identity;toClip.m00=2f/width;toClip.m03=-1;toClip.m11=-2f/height;toClip.m13=1;
        var corners=new[]{new Vector3(area.xMin,area.yMin),new Vector3(area.xMax,area.yMin),new Vector3(area.xMin,area.yMax),new Vector3(area.xMax,area.yMax)}.Select(p=>guiMatrix.MultiplyPoint3x4(p)).ToArray();
        var clip=Rect.MinMaxRect(Mathf.Max(0,corners.Min(p=>p.x)),Mathf.Max(0,height-corners.Max(p=>p.y)),Mathf.Min(width,corners.Max(p=>p.x)),Mathf.Min(height,height-corners.Min(p=>p.y)));
        if(clip.width<=0||clip.height<=0)return;
        GL.PushMatrix();
        try
        {
            Trace("before-restore");Graphics.SetRenderTarget(savedColor,savedDepth);Trace("restored-entry");traceCount++;
            foreach(var material in items.Select(i=>i.material).Distinct())
            {
                state.Add(material,new[]{material.GetInt("_SrcBlend"),material.GetInt("_DstBlend"),material.GetInt("_CaptureSrcAlpha"),material.GetInt("_CaptureDstAlpha")});
                material.SetInt("_SrcBlend",material.GetInt("_OriginalSrcBlend"));material.SetInt("_DstBlend",material.GetInt("_OriginalDstBlend"));
                material.SetInt("_CaptureSrcAlpha",material.GetInt("_OriginalSrcAlpha"));material.SetInt("_CaptureDstAlpha",material.GetInt("_OriginalDstAlpha"));
            }
            commands.Clear();
            commands.SetRenderTarget(target==null?new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive):new RenderTargetIdentifier(target),new RenderTargetIdentifier(depth));
            commands.SetViewport(new Rect(0,0,width,height));commands.ClearRenderTarget(true,false,Color.clear);
            commands.EnableScissorRect(clip);commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,toClip*guiMatrix*toPixel*camera.projectionMatrix);
            foreach(var item in items.OrderBy(i=>i.material.renderQueue).ThenBy(i=>i.renderer.sortingLayerID).ThenBy(i=>i.renderer.sortingOrder).ThenBy(i=>i.material.renderQueue<=2500?i.distance:-i.distance))
                commands.DrawRenderer(item.renderer,item.material,item.submesh,0);
            commands.DisableScissorRect();commands.SetViewProjectionMatrices(savedView,savedProjection);
            if(target!=null)commands.SetRenderTarget(new RenderTargetIdentifier(target));
            commands.SetViewport(new Rect(0,0,width,height));Graphics.ExecuteCommandBuffer(commands);
        }
        finally
        {
            foreach(var pair in state){var m=pair.Key;var s=pair.Value;m.SetInt("_SrcBlend",s[0]);m.SetInt("_DstBlend",s[1]);m.SetInt("_CaptureSrcAlpha",s[2]);m.SetInt("_CaptureDstAlpha",s[3]);}
            Graphics.SetRenderTarget(savedColor,savedDepth);
            GL.Viewport(new Rect(0,0,width,height));GL.PopMatrix();
        }
    }
    public void Clear(){commands.Clear();AssetRegistry.ReleaseTransient(depth);depth=null;}
    public void Dispose(){if(disposed)return;disposed=true;Clear();commands.Dispose();}
}
