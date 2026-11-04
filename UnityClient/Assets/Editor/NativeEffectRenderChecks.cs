using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public static class NativeEffectRenderChecks
{
    [Serializable] sealed class Capture{public string card,program_sha256,lifecycle;public int target_card_type,unbound_card_type_actions,unbound_texture_actions,unbound_color_actions,unbound_ease_actions,unbound_itween_actions,completed_itween_actions,unbound_actor_actions,actor_object_bindings,texture_bindings,visible_samples,particles,max_pixels,black_alpha_pixels,glow_samples,glow_pixels;public float duration,mean_ms,mean_frame_ms,p95_frame_ms,mean_affine_ms,p95_affine_ms,glow_max_rgb;}
    [Serializable] sealed class Report{public string kind="offscreen-native-prefab";public int passed,captured_frames;public Capture[] cards;}
    public static void Run()
    {
        NativeEffectChecks.Run();
        var assets=new AssetRegistry(Path.GetFullPath("../local"));string folder=Path.GetFullPath("../tests/visual/current/native-effects");Directory.CreateDirectory(folder);
        var entries=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json"))).entries;
        var captures=new List<Capture>();int passed=0,captured=0;
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        foreach(string scenario in new[]{"CS2_025","CS2_026","CS2_028","CS2_032","CS2_062","CS1_112","EX1_129","EX1_134","BT_035","BT_175","BT_801","BT_035-Hero","Nature-Thorns","Holy-Wisdom","EX1_180","EX1_044@m_TriggerEffectDefs:0","EX1_173","EX1_173-Death","DS1_185","EX1_537","DS1_183","CS2_084","EX1_539"})
        {
            string captureName=String.Concat(scenario.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
            string card=scenario=="BT_035-Hero"?"BT_035":scenario=="EX1_173-Death"?"EX1_173":scenario;
            var entry=scenario=="Nature-Thorns"?entries.First(e=>e.file=="07ebfc013884b374cb15adea.native-effect.json"):scenario=="Holy-Wisdom"?entries.First(e=>e.file=="05928182ba88d64927d6d9d6.native-effect.json"):entries.First(e=>e.card_id==card);var data=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
            string path=Path.Combine(assets.Root,entry.file);
            string fingerprint;using(var hash=SHA256.Create())fingerprint=String.Concat(hash.ComputeHash(File.ReadAllBytes(path)).Select(b=>b.ToString("x2")));
            var capture=new Capture{card=scenario,program_sha256=fingerprint};var clock=new Stopwatch();
            NativeSpellContext context=null;
            bool reticle=card=="CS2_084"||card=="EX1_539";
            if(reticle){capture.target_card_type=4;capture.lifecycle="Birth";context=new NativeSpellContext(5,4,lifecycle:"Birth");}
            if(card=="DS1_185"||card=="EX1_537"||card=="DS1_183"){capture.target_card_type=4;capture.lifecycle="Death";context=new NativeSpellContext(5,4,lifecycle:"Death");}
            if(card=="EX1_173"){capture.target_card_type=4;capture.lifecycle=scenario.EndsWith("-Death")?"Death":"Birth";context=new NativeSpellContext(5,4,lifecycle:capture.lifecycle);}
            if(scenario=="Nature-Thorns"){capture.target_card_type=4;capture.lifecycle="Death";context=new NativeSpellContext(5,4,lifecycle:"Death");}
            if(scenario=="Holy-Wisdom"){capture.target_card_type=4;capture.lifecycle="Birth";context=new NativeSpellContext(5,4,lifecycle:"Birth");}
            if(card=="BT_035"||card=="BT_175"||card=="BT_801")
            {capture.target_card_type=scenario=="BT_035-Hero"?3:4;capture.lifecycle=card=="BT_801"?"Birth":"Death";context=new NativeSpellContext(database.cards.First(c=>c.CardId==card).Type,capture.target_card_type,3,lifecycle:capture.lifecycle);}
            using(var instance=new NativeEffectInstance(data,assets,0,512,512,()=>Color.white,context))
            {
                foreach(float time in reticle?new[]{.12f,.3f,.6f,1f,2f,3f,4.5f,5f,6f,8.5f}:new[]{.12f,.3f,.6f,1f,2f,3f})
                {
                    clock.Start();var texture=instance.Render(time);clock.Stop();RenderTexture.active=texture;
                    var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
                    image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();
                    // Premultiplied additive captures can have RGB energy with
                    // zero opacity. The real composite uses One/OneMinusAlpha.
                    int pixels=image.GetPixels32().Count(c=>Math.Max(c.r,Math.Max(c.g,c.b))>3);
                    if(pixels>32)capture.visible_samples++;
                    capture.max_pixels=Math.Max(capture.max_pixels,pixels);
                    capture.black_alpha_pixels=Math.Max(capture.black_alpha_pixels,image.GetPixels32().Count(c=>c.a>32&&Math.Max(c.r,Math.Max(c.g,c.b))<4));
                    capture.particles=Math.Max(capture.particles,instance.ParticleCount);
                    if(pixels>32)capture.duration=time;
                    File.WriteAllBytes(Path.Combine(folder,captureName+"-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());AssetRegistry.ReleaseTransient(image);captured++;
                    if(card=="CS2_062"&&time==.3f)
                        File.WriteAllLines("../work/native-hellfire-renderers.txt",instance.Nodes.Where(n=>n.GetComponent<MeshRenderer>()!=null).Select(n=>{
                            var r=n.GetComponent<Renderer>();return n.name+" enabled="+r.enabled+" active="+n.activeInHierarchy+" size="+r.bounds.size+" center="+(r.bounds.center-instance.Root.transform.position)+" floats="+String.Join(",",r.sharedMaterials.Select(m=>m.GetFloat("_Intensity")+"/"+m.GetFloat("_FxIntensity")));}));
                    var glow=instance.RenderGlow(time);RenderTexture.active=glow;
                    var glowImage=new Texture2D(glow.width,glow.height,TextureFormat.RGBAFloat,false,true);
                    try
                    {
                        glowImage.ReadPixels(new Rect(0,0,glow.width,glow.height),0,0);glowImage.Apply();var values=glowImage.GetPixels();
                        int glowing=values.Count(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))>.00001f);
                        if(glowing>0)capture.glow_samples++;capture.glow_pixels=Math.Max(capture.glow_pixels,glowing);
                        capture.glow_max_rgb=Mathf.Max(capture.glow_max_rgb,values.Max(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))));
                    }
                    finally{AssetRegistry.ReleaseTransient(glowImage);}
                }
                if(instance.TransitionLimitHits!=0)throw new Exception(card+" original FSM exceeded limit");
                capture.unbound_card_type_actions=instance.UnboundCardTypeActions;
                capture.unbound_texture_actions=instance.UnboundTextureActions;capture.texture_bindings=instance.TextureBindings;
                capture.unbound_color_actions=instance.UnboundColorActions;
                capture.unbound_ease_actions=instance.UnboundEaseActions;
                capture.unbound_itween_actions=instance.UnboundITweenActions;capture.completed_itween_actions=instance.CompletedITweenActions;
                capture.unbound_actor_actions=instance.UnboundActorActions;capture.actor_object_bindings=instance.ActorObjectBindings;
                passed++;
                var times=new List<float>();instance.Render(0);
                for(int frame=1;frame<=30;frame++){var sample=Stopwatch.StartNew();instance.Render(frame/60f);sample.Stop();times.Add((float)sample.Elapsed.TotalMilliseconds);}
                capture.mean_frame_ms=times.Average();times.Sort();capture.p95_frame_ms=times[28];
                times.Clear();instance.RenderAffine(0);
                for(int frame=1;frame<=30;frame++){var sample=Stopwatch.StartNew();instance.RenderAffine(frame/60f);sample.Stop();times.Add((float)sample.Elapsed.TotalMilliseconds);}
                capture.mean_affine_ms=times.Average();times.Sort();capture.p95_affine_ms=times[28];
            }
            capture.mean_ms=(float)clock.Elapsed.TotalMilliseconds/(reticle?10:6);captures.Add(capture);
        }
        RenderTexture.active=null;
        var hero=captures.First(c=>c.card=="BT_035-Hero");
        if(hero.texture_bindings!=3||hero.unbound_texture_actions!=0)throw new Exception("Original Hero texture bindings failed");passed++;
        var diagnostic=new Report{passed=passed,captured_frames=captured,cards=captures.ToArray()};File.WriteAllText(Path.Combine(folder,"render-report.json"),JsonUtility.ToJson(diagnostic,true));
        foreach(string card in new[]{"CS2_025","CS2_026","CS2_062"})
        {
            var capture=captures.First(c=>c.card==card);
            if(capture.visible_samples<2)throw new Exception(card+" has no verified visible interval");passed++;
            if(capture.black_alpha_pixels>capture.max_pixels*.1f+32)throw new Exception(card+" paints an opaque black mask");passed++;
        }
        foreach(string card in new[]{"BT_035","BT_175","BT_801"})
        {
            var capture=captures.First(c=>c.card==card);
            if(capture.unbound_card_type_actions!=0||capture.particles==0||capture.visible_samples<2)
                throw new Exception(card+" original target-type branch did not emit visible particles");
            passed++;
        }
        foreach(string card in new[]{"CS2_084","EX1_539"})
        {
            var capture=captures.First(c=>c.card==card);
            if(capture.visible_samples!=10||capture.unbound_itween_actions!=0||capture.completed_itween_actions!=2)
                throw new Exception(card+" source reticle fade/scale did not produce a visible interval and callbacks");
            passed++;
        }
        passed+=CompositeChecks(assets,entries,folder);
        var report=new Report{passed=passed,captured_frames=captured,cards=captures.ToArray()};File.WriteAllText(Path.Combine(folder,"render-report.json"),JsonUtility.ToJson(report,true));
        UnityEngine.Debug.Log("NATIVE GPU CHECKS PASSED: "+passed);
    }
    static int CompositeChecks(AssetRegistry assets,NativeEffectEntry[] entries,string folder)
    {
        var board=assets.Scene("SCENE_BOARD");AssetRegistry.Fit(board,28);
        var table=assets.Scene("MODEL_BOARD_TABLE");AssetRegistry.Fit(table,48);
        float floor=board.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.y);
        table.transform.position+=Vector3.down*(table.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.max.y)-floor+.2f);
        table.transform.SetParent(board.transform,true);foreach(var t in board.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
        var camera=new GameObject("NativeCompositeBoard").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.farClipPlane=200;
        var scene=new BoardPresentation(board,camera);scene.Show(Vector2.zero,new DesignViewport(1920,1080,1920,1080,1520));
        var canvas=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);canvas.Create();camera.targetTexture=canvas;
        var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);var visuals=new CardVisuals(assets);
        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var player=new NativeEffectPlayer(assets);var old=RenderTexture.active;int passed=0;
        try
        {
            foreach(string id in new[]{"CS2_025","CS2_026","CS2_062"})
            {
                camera.Render();var entry=entries.First(e=>e.card_id==id);
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
                using(var native=new NativeEffectInstance(program,assets,0,512,256))
                {
                    var capture=native.Render(.3f);RenderTexture.active=capture;
                    var probe=new Texture2D(capture.width,capture.height,TextureFormat.RGBA32,false);
                    try
                    {
                        probe.ReadPixels(new Rect(0,0,probe.width,probe.height),0,0);probe.Apply();
                        if(probe.GetPixels32().Count(c=>Math.Max(c.r,Math.Max(c.g,c.b))>3)<32)throw new Exception(id+" composite frame is empty");passed++;
                    }
                    finally{AssetRegistry.ReleaseTransient(probe);}
                    RenderTexture.active=canvas;
                    foreach(bool own in new[]{true,false})
                        player.Render(BattleLayout.Hero(own),visuals.Render(db.cards.First(c=>c.CardId=="HERO_08"),"hero"),Matrix4x4.identity,1920,1080,Color.white);
                    var emission=native.RenderAffine(.3f);
                    player.RenderAffine(new Rect(385,285,1150,520),emission,native.Transmission,Matrix4x4.identity,1920,1080,Color.white);
                    image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(folder,id+"-board-components.png"),image.EncodeToPNG());
                    var background=image.GetPixel(0,0);
                    player.RenderAffine(new Rect(600,200,600,500),emission,native.Transmission,Matrix4x4.identity,1920,1080,Color.white*.5f);
                    image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                    var difference=image.GetPixel(0,0)-background;
                    if(Mathf.Max(Mathf.Abs(difference.r),Mathf.Abs(difference.g),Mathf.Abs(difference.b))>.01f)throw new Exception("Native composite leaked outside rectangle");passed++;
                }
            }
        }
        finally{RenderTexture.active=old;player.Dispose();visuals.Clear();camera.targetTexture=null;AssetRegistry.ReleaseTransient(image);AssetRegistry.ReleaseTransient(canvas);AssetRegistry.ReleaseTransient(camera.gameObject);AssetRegistry.ReleaseTransient(board);}
        return passed;
    }
}
