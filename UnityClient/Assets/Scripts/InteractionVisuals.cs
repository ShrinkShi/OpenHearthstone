using System;
using System.Collections.Generic;
using UnityEngine;

// 从实际卡框/头像 alpha 生成轮廓并缓存；不在每帧读取 GPU 像素。
public sealed class InteractionVisuals : IDisposable
{
    readonly Material material=new Material(Resources.Load<Shader>("InteractionGlow"));
    readonly Dictionary<string,RenderTexture> masks=new Dictionary<string,RenderTexture>();
    public void Clear(){foreach(var rt in masks.Values)AssetRegistry.ReleaseTransient(rt);masks.Clear();}
    public void Draw(Rect rect,Texture source,Color color,bool narrow=false)
    {
        if(source==null||Event.current.type!=EventType.Repaint)return;
        string key=source.GetInstanceID()+":"+narrow;RenderTexture rt;
        if(!masks.TryGetValue(key,out rt))
        {
            if(masks.Count>=64)Clear();
            rt=new RenderTexture(source.width+64,source.height+64,0,RenderTextureFormat.ARGB32);rt.Create();
            material.SetFloat("_Radius",narrow?10:26);material.SetFloat("_Padding",32);Graphics.Blit(source,rt,material);masks.Add(key,rt);
        }
        float x=rect.width*32/source.width,y=rect.height*32/source.height;
        BattlePainter.Draw(new Rect(rect.x-x,rect.y-y,rect.width+2*x,rect.height+2*y),rt,color);
    }
    public void Dispose(){Clear();AssetRegistry.ReleaseTransient(material);}
}
