using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class FireblastVisualChecks
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    static Color[] Read(RenderTexture target,string file)
    {
        var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
        try
        {
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            var pixels=image.GetPixels();
            if(file!=null){var png=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);try{png.SetPixels(pixels);png.Apply();File.WriteAllBytes(file,png.EncodeToPNG());}finally{AssetRegistry.ReleaseTransient(png);}}
            return pixels;
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));string folder=Path.GetFullPath("../work/f005-ui-gpu");Directory.CreateDirectory(folder);
        BuildClient.BuildLocalFonts();
        var fonts=AssetBundle.LoadFromFile(Path.Combine(assets.Root,"local-fonts.assetbundle"));
        try
        {
        var owned=new List<GameObject>();
        using(var arrow=new NativeTargetArrow(assets))
        {
            owned.AddRange(arrow.OwnedObjects);arrow.Show(new Vector2(1200,860),new Vector2(960,250),true);arrow.Tick(.5f);
            Check(arrow.VisibleLinks>0&&arrow.VisibleLinks<=15&&arrow.BullseyeVisible,"original fifteen-link pool and legal bullseye");
            var legal=Read(arrow.Render(),Path.Combine(folder,"arrow-legal.png"));
            Check(legal.Any(p=>p.r>.1f||p.g>.1f||p.b>.1f),"actual targeting mesh visible on GPU");
            arrow.Show(new Vector2(1200,860),new Vector2(960,250),false);
            var illegal=Read(arrow.Render(),Path.Combine(folder,"arrow-illegal.png"));
            Check(illegal.Count(p=>p.r>.05f||p.g>.05f||p.b>.05f)>300,"head and segments remain visible without legal bullseye");
            Check(!arrow.BullseyeVisible&&legal.Zip(illegal,(a,b)=>Vector4.Distance(a,b)).Any(d=>d>.01f),"invalid hover hides original bullseye only");
            float offset=arrow.Offset;arrow.Hide();arrow.Tick(.25f);Check(arrow.Offset==offset,"hidden arrow clock retains original phase");
            arrow.Show(new Vector2(1200,860),new Vector2(1200,860),true);Check(arrow.VisibleLinks==0,"zero-distance arrow excludes division by zero");
            Read(arrow.Render(),Path.Combine(folder,"arrow-short.png"));
            arrow.Show(new Vector2(40,1000),new Vector2(1880,60),true);Read(arrow.Render(),Path.Combine(folder,"arrow-edge.png"));
        }
        Check(owned.All(o=>o==null),"all original arrow clones and projection camera physically released");
        using(var powers=new NativePowerVisual(assets))
        {
            powers.NameFont=fonts.LoadAsset<Font>("GLEI00M_t_Outline");powers.BodyFont=fonts.LoadAsset<Font>("BlizzardGlobal");
            Check(powers.NameFont!=null&&powers.BodyFont!=null,"actual original zhCN font bundle");
            var own=powers.Prepare(2,false);var enemy=powers.Prepare(4,true,false,true);owned.Add(own.Root);owned.Add(enemy.Root);
            Check(!ReferenceEquals(own,enemy),"same CardID independent owners");
            var front=Read(powers.Render(2,false),Path.Combine(folder,"power-ready.png"));
            var back=Read(powers.Render(4,true,false,true),Path.Combine(folder,"power-used-enemy.png"));
            Check(front.Any(p=>p.r>.1f)&&back.Any(p=>p.r>.1f),"both original button orientations render");
            Check(front.Zip(back,(a,b)=>Vector4.Distance(a,b)).Any(d=>d>.1f),"used does not share ready RT or pose");
            var frontAgain=Read(powers.Render(2,false),null);Check(front.Zip(frontAgain,(a,b)=>Vector4.Distance(a,b)).Max()<.001f,"enemy used rendering preserves own ready material and pose");
            powers.Prepare(2,true,false,false,1,true);Read(powers.Render(2,true,false,false,1.2666665f,true),Path.Combine(folder,"power-flip-mid.png"));
            var detail=powers.Prepare(2,false,true);owned.Add(detail.Root);Read(powers.Render(2,false,true),Path.Combine(folder,"power-detail.png"));
            var enemyDetail=powers.Prepare(4,false,true,true);owned.Add(enemyDetail.Root);Read(powers.Render(4,false,true,true),Path.Combine(folder,"power-detail-enemy.png"));
            var card=new CardDef{CardId="HERO_08bp",Cost=2,Name="火焰冲击",Text="<b>英雄技能</b>\n造成$1点伤害。"};
            powers.PrepareText(new EntityView{entity=2,card_id="HERO_08bp",cost=2},card,true);
            Read(powers.Render(2,false,true),Path.Combine(folder,"power-detail-text.png"));
            var text=detail.Root.GetComponentsInChildren<TextMesh>();
            Debug.Log("FIREBLAST TEXT "+String.Join(";",text.Select(t=>t.text+" size="+t.fontSize+" chars="+t.characterSize+" line="+t.lineSpacing)));
            Check(text.Length==3&&text.Any(t=>t.text.Contains("火焰冲击")&&t.fontSize==45&&t.characterSize<=.05f),"source IL arithmetic on current Mono: name45, original running-game rounding unverified");
            Check(text.Any(t=>t.text.Contains("造成1点伤害。")&&t.fontSize==40&&Mathf.Abs(t.lineSpacing-.9375f)<.00001f),"source body TextMesh line spacing and dynamic marker");
            powers.PrepareText(new EntityView{entity=2,card_id="HERO_08bp",cost=1,display_damage=3,display_damage_known=true},card,true);
            Read(powers.Render(2,false,true),Path.Combine(folder,"power-detail-modified.png"));
            Check(text.Any(t=>t.text=="1"&&t.fontSize==74)&&text.Any(t=>t.text.Contains("造成3点伤害。")),"source cost and snapshot damage change glyph output");
            string commonFolder=Path.GetFullPath("../work/f021-common-power-ui");Directory.CreateDirectory(commonFolder);
            foreach(string id in Enumerable.Range(1,11).Select(i=>"HERO_"+i.ToString("00")+"bp"))
            {
                int entity=100+Int32.Parse(id.Substring(5,2));
                var common=powers.Prepare(entity,false);owned.Add(common.Root);
                var definition=new CardDef{CardId=id,Cost=id=="HERO_10bp"?1:2,Name=id,Text=""};
                powers.PrepareText(new EntityView{entity=entity,card_id=id,cost=definition.Cost},definition);
                var portrait=common.Nodes.Single(n=>n.name=="Mesh").GetComponent<Renderer>().sharedMaterials[1].mainTexture;
                Check(ReferenceEquals(portrait,assets.FireblastTextureFile(Path.GetFileName(assets.PathFor("CARD_"+id)))),"standard power uses its own CardDef portrait: "+id);
                var ready=Read(powers.Render(entity,false),Path.Combine(commonFolder,id+"-ready.png"));
                var used=Read(powers.Render(entity,true),Path.Combine(commonFolder,id+"-used.png"));
                Check(ready.Any(p=>p.r>.1f||p.g>.1f||p.b>.1f)&&ready.Zip(used,(a,b)=>Vector4.Distance(a,b)).Any(d=>d>.1f),"standard power renders distinct ready and used poses: "+id);
            }
            foreach(string name in new[]{"NameUberText","PowersUberText","CostUberText"})
            {var rect=powers.TextRect(new Rect(0,0,384,576),2,name,true);Check(rect.width>1&&rect.height>1&&rect.width<500&&rect.height<600,"original text node projection "+name);}
            Check(own.UnhandledActions==0&&detail.UnhandledActions==0,"thin UI graph has no silently ignored runtime actions");
            powers.ApplyActorState(2,5);
            var all=own.Root.GetComponentsInChildren<Renderer>(true);var enabled=all.ToDictionary(r=>r,r=>r.enabled);
            var shadow=all.Single(r=>r.sharedMaterials.Any(m=>m.GetFloat("_OriginalFamily")==34));
            foreach(var renderer in all)renderer.enabled=renderer==shadow;
            var shadowC=Read(powers.Render(2,false),Path.Combine(folder,"power-shadow-C.png"));
            var shadowK=Read(own.Transmission,Path.Combine(folder,"power-shadow-K.png"));
            Check(shadowC.All(p=>Mathf.Abs(p.r)+Mathf.Abs(p.g)+Mathf.Abs(p.b)<.0001f)&&shadowK.Any(p=>p.r>.99f&&p.g>.99f&&p.b>.99f),"actual original Multiply shadow has zero emission and preserves white-region destination");
            var highlight=all.Single(r=>r.name=="Highlight_Card_Plane");foreach(var renderer in all)renderer.enabled=renderer==highlight;
            var sourceColors=highlight.GetComponent<MeshFilter>().sharedMesh.colors;
            Check(sourceColors.Length==32&&sourceColors.All(c=>c.r>=0&&c.r<=1&&c.a==1)&&sourceColors.Any(c=>c.r==1),"original UNorm8 COLOR decoded to float0/1, not byte255");
            var native=highlight.sharedMaterial;native.SetFloat("_Seed",.25f);var nativeC=Read(powers.Render(2,false),Path.Combine(folder,"power-highlight-C.png"));var nativeK=Read(own.Transmission,Path.Combine(folder,"power-highlight-K.png"));
            var original=new Material(Resources.Load<Shader>("OriginalActorHighlight"));original.CopyPropertiesFromMaterial(native);
            try
            {
                highlight.sharedMaterial=original;var sourceC=Read(powers.Render(2,false),Path.Combine(folder,"power-highlight-source-C.png"));var sourceK=Read(own.Transmission,Path.Combine(folder,"power-highlight-source-K.png"));
                Check(nativeC.Zip(sourceC,(a,b)=>Vector4.Distance(a,b)).Max()<.001f&&nativeK.Zip(sourceK,(a,b)=>Vector4.Distance(a,b)).Max()<.001f,"actual source mesh/static RGB silhouette/flare uses original Highlight math and C/K");
                Debug.Log("FIREBLAST ACTUAL HIGHLIGHT peakC="+nativeC.Max(p=>Mathf.Max(p.r,Mathf.Max(p.g,p.b)))+" minK="+nativeK.Min(p=>Mathf.Min(p.r,Mathf.Min(p.g,p.b)))+" sourceMathCompared=True");
                var camera=own.Root.GetComponentInChildren<Camera>();var priorTarget=camera.targetTexture;var priorColor=camera.backgroundColor;bool priorHDR=camera.allowHDR;
                var ldr=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);ldr.Create();var background=new Color(.2f,.4f,.6f,.7f);
                try
                {
                    camera.targetTexture=ldr;camera.backgroundColor=background;camera.allowHDR=false;camera.Render();var direct=Read(ldr,Path.Combine(folder,"power-highlight-direct-LDR.png"));
                    float error=direct.Select((p,i)=>Mathf.Max(Mathf.Abs(p.r-Mathf.Clamp01(nativeC[i].r+background.r*nativeK[i].r)),Mathf.Max(Mathf.Abs(p.g-Mathf.Clamp01(nativeC[i].g+background.g*nativeK[i].g)),Mathf.Abs(p.b-Mathf.Clamp01(nativeC[i].b+background.b*nativeK[i].b))))).Max();
                    Debug.Log("FIREBLAST HIGHLIGHT FORMAT EXPERIMENT authoredHDRFalse directARGB32_vs_floatAffine maxRGB="+error+" runtimeTargetFormatUnknown=True");
                    File.WriteAllText(Path.Combine(folder,"highlight-format.json"),"{\"direct_ldr_vs_float_affine_max_rgb\":"+error.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"original_runtime_target_verified\":false}");
                    var actual=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);actual.Create();
                    try
                    {
                        highlight.sharedMaterial=native;RenderTexture.active=actual;GL.Clear(true,true,background);
                        using(var drawer=new NativePowerDirectDraw())drawer.Draw(own,new Rect(0,0,256,256),Matrix4x4.identity,256,256);
                        var result=Read(actual,Path.Combine(folder,"power-highlight-direct-command.png"));
                        Check(result.Zip(direct,(a,b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)))).Max()<.015f,"production direct framebuffer matches actual source LDR draw, not Float affine reconstruction");
                        Check(native.GetInt("_CaptureSrcAlpha")==1&&native.GetInt("_SrcBlend")==5,"direct GUI restores prior material blend state");
                    }
                    finally{RenderTexture.active=null;AssetRegistry.ReleaseTransient(actual);}
                }
                finally{camera.targetTexture=priorTarget;camera.backgroundColor=priorColor;camera.allowHDR=priorHDR;AssetRegistry.ReleaseTransient(ldr);}
            }
            finally{highlight.sharedMaterial=native;AssetRegistry.ReleaseTransient(original);foreach(var renderer in all)renderer.enabled=enabled[renderer];}
        }
        Check(owned.All(o=>o==null),"all original button/detail objects physically released");
        FireblastSelectionChecks.Run();
        Debug.Log("FIREBLAST VISUAL: "+checks+" checks passed; source camera plus current GUI plane, original global lighting/hitbox/official fidelity unverified");
        }
        finally{fonts.Unload(false);}
    }
}
