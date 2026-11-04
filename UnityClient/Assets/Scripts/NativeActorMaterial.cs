using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public static class NativeActorMaterial
{
    [Serializable] sealed class Lighting{public float[] sh;}
    public static void BindAmbient(Renderer renderer,AssetRegistry assets)
    {
        var probe=new SphericalHarmonicsL2();
        var ambient=assets.BattleAmbient?.Invoke();
        if(ambient.HasValue)probe.AddAmbientLight(ambient.Value);
        else
        {
            var source=JsonUtility.FromJson<Lighting>(File.ReadAllText(Path.Combine(assets.Root,"original-gameplay-lighting.json")));
            if(source.sh==null||source.sh.Length!=27)throw new InvalidDataException("Original Gameplay ambient probe missing");
            for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)probe[channel,coefficient]=source.sh[channel*9+coefficient];
        }
        var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
        properties.CopySHCoefficientArraysFrom(new[]{probe});renderer.lightProbeUsage=LightProbeUsage.CustomProvided;
        renderer.SetPropertyBlock(properties);
    }
    public static Material Create(NativeMaterial source,AssetRegistry assets)
    {
        string resource;
        switch(source.shader)
        {
            case "Custom/Card/Unlit_Desaturate_2Texture2uv":resource="OriginalActorPortrait";break;
            case "Custom/Card/UnlitTexture":resource="OriginalActorFrame";break;
            case "Custom/Selection/Highlight":resource="OriginalActorHighlight";break;
            default:throw new ArgumentException("Unrestored Actor shader");
        }
        var shader=Resources.Load<Shader>(resource);
        if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original Actor shader unavailable: "+resource);
        var material=new Material(shader){name=source.name,renderQueue=source.render_queue>0?source.render_queue:2000};
        foreach(var value in source.floats??new NativeFloat[0])if(material.HasProperty(value.name))material.SetFloat(value.name,value.value);
        foreach(var value in source.colors??new NativeColor[0])if(material.HasProperty(value.name))material.SetColor(value.name,value.value);
        foreach(var value in source.textures??new NativeTexture[0])if(material.HasProperty(value.name))
        {material.SetTexture(value.name,source.shader=="Custom/Selection/Highlight"&&value.name=="_FlareTex"?assets.HighlightTextureFile(value.path):assets.TextureFile(value.path));material.SetTextureScale(value.name,value.scale);material.SetTextureOffset(value.name,value.offset);}
        return material;
    }
}
