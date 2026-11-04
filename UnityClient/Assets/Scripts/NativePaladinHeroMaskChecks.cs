using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Strict HERO_04bp-only adaptation for source Color UNorm8 data cached as byte-valued floats.
public static class NativePaladinHeroMaskChecks
{
    const int HeroMaskNode=6;
    const int HeroGlowNode=7;

    public static Mesh[] NormalizeVertexColors(NativeEffectInstance effect)
    {
        if(effect==null||effect.Program==null||effect.Program.nodes==null||effect.Program.nodes.Length<=HeroGlowNode||effect.Nodes.Length<=HeroGlowNode)
            throw new NotSupportedException("HERO_04bp mask source nodes are incomplete");
        ValidateNode(effect.Program.nodes[HeroMaskNode],"FX_Hero_Mask","02d2c3f3b47d121d4783931f.mesh.json",
            "Custom/FX/FX_HeroMask_VertColorOnly",0,3,0,3);
        ValidateNode(effect.Program.nodes[HeroGlowNode],"FX_Hero_VertexGlow","731994b0a080422087101f9a.mesh.json",
            "Effects/FX_AdditiveVertColor",5,1,5,1);
        var copies=new List<Mesh>();
        try
        {
            foreach(int index in new[]{HeroMaskNode,HeroGlowNode})
            {
                var filter=effect.Nodes[index].GetComponent<MeshFilter>();var source=filter==null?null:filter.sharedMesh;
                if(source==null)throw new NotSupportedException("HERO_04bp mask source mesh is unavailable");
                var colors=source.colors;
                if(colors==null||colors.Length!=source.vertexCount)throw new NotSupportedException("HERO_04bp mask source vertex colors are incomplete");
                float maximum=colors.Max(color=>Mathf.Max(color.r,Mathf.Max(color.g,Mathf.Max(color.b,color.a))));
                if(maximum<=1)continue;
                if(maximum>255||colors.Any(color=>!ByteColor(color)))
                    throw new NotSupportedException("HERO_04bp source mask colors are neither normalized nor UNorm8 bytes");
                var normalized=colors.Select(color=>color/255f).ToArray();
                var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name+" HERO_04 normalized UNorm8";copy.colors=normalized;
                filter.sharedMesh=copy;copies.Add(copy);
            }
            return copies.ToArray();
        }
        catch{foreach(var mesh in copies)AssetRegistry.ReleaseTransient(mesh);throw;}
    }

    static bool ByteColor(Color color)
    {
        foreach(float value in new[]{color.r,color.g,color.b,color.a})
            if(value<0||value>255||Mathf.Abs(value-Mathf.Round(value))>.001f)return false;
        return true;
    }

    static void ValidateNode(NativeNode node,string name,string mesh,string shader,int src,int dst,int srcAlpha,int dstAlpha)
    {
        var material=node.materials==null||node.materials.Length!=1?null:node.materials[0];
        if(node.name!=name||node.mesh!=mesh||material==null||material.shader!=shader||material.src!=src||material.dst!=dst||
            material.src_alpha!=srcAlpha||material.dst_alpha!=dstAlpha||material.render_queue!=3000||node.sorting_order!=0)
            throw new NotSupportedException("HERO_04bp native hero mask material/render-order profile changed: "+name);
    }
}
