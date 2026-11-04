using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 原关联贴图经正式 GPU 混合器渲染；不是原版粒子脚本的逐帧还原验收。
public static class AssociatedRenderChecks
{
    public static void Run()
    {
        AssociatedPresentationChecks.Run();
        string folder=Path.GetFullPath("../tests/visual/current/associated-effects");Directory.CreateDirectory(folder);
        var registry=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(Path.GetFullPath("../local/runtime"));
        var profiles=JsonUtility.FromJson<CardEffectIndex>(File.ReadAllText("../local/runtime/effect-index.json")).entries;
        var particles=new SpellParticles();var canvas=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32);canvas.Create();
        var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        int passed=0;var background=new Color(.12f,.10f,.08f,1);
        var failures=new List<string>();
        try
        {
            foreach(var sprite in profiles.SelectMany(p=>p.visuals??new CardEffectSprite[0]).GroupBy(s=>s.texture).Select(g=>g.First()))
            {
                RenderTexture.active=canvas;GL.Clear(true,true,background);
                particles.Render(new Rect(0,0,256,256),registry.Texture(sprite.texture),sprite.tint,Matrix4x4.identity,256,256,false,sprite);
                pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                foreach(var corner in new[]{pixels.GetPixel(0,0),pixels.GetPixel(255,0),pixels.GetPixel(0,255),pixels.GetPixel(255,255)})
                    if(Mathf.Max(Mathf.Abs(corner.r-background.r),Mathf.Abs(corner.g-background.g),Mathf.Abs(corner.b-background.b))>.035f)
                        failures.Add("Opaque particle border: "+sprite.texture);
                passed++;
                if(pixels.GetPixels().Count(c=>Mathf.Max(Mathf.Abs(c.r-background.r),Mathf.Abs(c.g-background.g),Mathf.Abs(c.b-background.b))>.008f)<16)
                    failures.Add("Invisible particle layer: "+sprite.texture);
                passed++;
            }
            foreach(string id in new[]{"CS2_025","CS2_028","CS2_032","CS2_022","CS1_112","EX1_134","EX1_593"})
            {
                RenderTexture.active=canvas;GL.Clear(true,true,background);
                foreach(var sprite in CardEffectRegistry.Find(id).visuals)
                    particles.Render(new Rect(0,0,256,256),registry.Texture(sprite.texture),sprite.tint,Matrix4x4.identity,256,256,false,sprite);
                pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder,id+"-material-composite.png"),pixels.EncodeToPNG());passed++;
            }
            File.WriteAllText(Path.Combine(folder,"render-report.json"),"{\"passed\":"+passed+",\"type\":\"associated-material-render\"}");
            File.WriteAllLines(Path.Combine(folder,"failures.txt"),failures);
            if(failures.Count>0)throw new Exception(String.Join("; ",failures));
            Debug.Log("ASSOCIATED GPU CHECKS PASSED: "+passed);
        }
        finally{RenderTexture.active=previous;particles.Dispose();canvas.Release();UnityEngine.Object.DestroyImmediate(canvas);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
