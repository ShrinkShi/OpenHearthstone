using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable] public sealed class NativeTrailData
{
    public float time,min_distance,width,shadow_bias;public NativeKey[] width_curve;public NativeGradientKeys gradient;
    public bool restored,emitting,autodestruct,lighting,receive_shadows;public int corners,caps,alignment,texture_mode,cast_shadows;
    public Vector2 texture_scale;
}

// 原 TrailRenderer 参数/属性直接绑定 SDK；采样位置与回收使用表现时钟。
// 重建点上传不声明复刻原引擎内部顶点年龄/细分，精细视觉由后续比较判定。
public sealed class NativeTrailPlayback
{
    struct Point{public float time;public Vector3 position;}
    readonly NativeTrailData data;readonly List<Point> points=new List<Point>();
    public readonly TrailRenderer Renderer;
    public NativeTrailPlayback(NativeTrailData data,GameObject owner)
    {
        this.data=data;
        if(data.autodestruct||data.texture_scale!=Vector2.one||data.alignment!=0||data.texture_mode!=0)
            throw new NotSupportedException("Unreviewed source trail branch: auto="+data.autodestruct+" scale="+data.texture_scale+" alignment="+data.alignment+" texture="+data.texture_mode);
        Renderer=owner.AddComponent<TrailRenderer>();Renderer.autodestruct=false;Renderer.emitting=data.emitting;
        Renderer.time=data.time;Renderer.minVertexDistance=data.min_distance;Renderer.widthMultiplier=data.width;
        Renderer.widthCurve=NativeCurve.Keys(data.width_curve);Renderer.colorGradient=data.gradient.UnityGradient();
        Renderer.numCornerVertices=data.corners;Renderer.numCapVertices=data.caps;
        Renderer.alignment=(LineAlignment)data.alignment;Renderer.textureMode=(LineTextureMode)data.texture_mode;
        Renderer.generateLightingData=data.lighting;Renderer.shadowBias=data.shadow_bias;
        Renderer.shadowCastingMode=(ShadowCastingMode)data.cast_shadows;Renderer.receiveShadows=data.receive_shadows;
    }
    public void Reset(){points.Clear();Renderer.Clear();}
    public void Sample(float time)
    {
        float lifetime=Renderer.time;
        if(lifetime<=0){Reset();return;}
        points.RemoveAll(point=>time-point.time>lifetime);
        if(data.emitting&&Renderer.gameObject.activeInHierarchy&&
            (points.Count==0||Vector3.Distance(points[points.Count-1].position,Renderer.transform.position)>=Renderer.minVertexDistance))
            points.Add(new Point{time=time,position=Renderer.transform.position});
        Renderer.Clear();var positions=new Vector3[points.Count];for(int i=0;i<points.Count;i++)positions[i]=points[i].position;
        if(positions.Length>0)Renderer.AddPositions(positions);
    }
}
