using System;
using UnityEngine;

// 已读3/15与5/17无主阴影分支的显式输入；不猜原全局灯数组或range→attenuation映射。
public sealed class NativeDiffuseInputs
{
    public const string LocalStatus="Explicit local no-main-shadow pixel branch; main/additional lights zero. Original runtime keyword/light arrays/attenuation mapping unavailable.";
    public struct Light{public Vector4 position,color,attenuation,spotDirection;}
    public bool vertexLighting;
    public Vector4 mainPosition,mainColor;
    public float mainVisibility;
    public int globalCount;
    public Light[] lights=new Light[0];
    public int[] rendererIndices=new int[0];
    public void Apply(Material material)
    {
        if(globalCount<0||rendererIndices.Length>8)throw new NotSupportedException("Unreviewed diffuse light input count");
        int count=Math.Min(globalCount,rendererIndices.Length);
        var positions=new Vector4[8];var colors=new Vector4[8];var attenuation=new Vector4[8];var spots=new Vector4[8];
        for(int i=0;i<count;i++)
        {
            int index=rendererIndices[i];if(index<0||index>=lights.Length)throw new ArgumentOutOfRangeException(nameof(rendererIndices));
            var light=lights[index];positions[i]=light.position;colors[i]=light.color;attenuation[i]=light.attenuation;spots[i]=light.spotDirection;
        }
        material.SetFloat("_DiffuseVertexLighting",vertexLighting?1:0);material.SetVector("_DiffuseMainPosition",mainPosition);material.SetVector("_DiffuseMainColor",mainColor);
        material.SetFloat("_DiffuseMainVisibility",mainVisibility);material.SetFloat("_DiffuseAdditionalCount",count);
        material.SetVectorArray("_DiffuseLightPosition",positions);material.SetVectorArray("_DiffuseLightColor",colors);
        material.SetVectorArray("_DiffuseLightAttenuation",attenuation);material.SetVectorArray("_DiffuseLightSpotDirection",spots);
    }
}
