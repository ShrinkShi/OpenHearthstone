using System;
using System.IO;
using System.Linq;
using UnityEngine;

// 离屏组件截图，不伪称桌面人工验收；使用正式模型、布局和破碎 Shader。
public static class LifecycleRenderChecks
{
    static Material surface;
    static int passed;
    static string folder;
    static void Quad(RenderTexture canvas,Texture texture,Rect rect,Matrix4x4 matrix,Color tint)
    {
        surface.mainTexture=texture;surface.SetColor("_Tint",tint);RenderTexture.active=canvas;
        GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,canvas.width,canvas.height,0);GL.MultMatrix(matrix);surface.SetPass(0);GL.Begin(GL.QUADS);
            GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
            GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
        }
        finally{GL.PopMatrix();}
    }
    static void Save(RenderTexture canvas,string name,bool visible=true)
    {
        RenderTexture.active=canvas;var image=new Texture2D(canvas.width,canvas.height,TextureFormat.RGBA32,false);
        try
        {
            image.ReadPixels(new Rect(0,0,canvas.width,canvas.height),0,0);image.Apply();
            int content=image.GetPixels32().Count(c=>Mathf.Abs(c.r-31)>3||Mathf.Abs(c.g-26)>3||Mathf.Abs(c.b-18)>3);
            if(visible&&content<64)throw new Exception("Empty lifecycle frame: "+name);passed++;
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        }
        finally{UnityEngine.Object.DestroyImmediate(image);}
    }
    public static void Run()
    {
        InteractionLifecycleChecks.Run();folder=Path.GetFullPath("../tests/visual/current/lifecycle");Directory.CreateDirectory(folder);passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var calibration=new UICalibration(Path.GetFullPath("../local/ui-calibration.json"));string error;
        if(!calibration.Load(out error))throw new Exception(error);UICalibration.Current=calibration;
        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));CardEffectRegistry.Load(assets.Root);
        surface=new Material(Resources.Load<Material>("AssetSurface"));surface.SetFloat("_UseAlpha",1);surface.SetInt("_ZWrite",0);surface.SetInt("_SrcBlend",5);surface.SetInt("_DstBlend",10);
        var fragments=new HeroFragments();var particles=new SpellParticles();var canvas=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);canvas.Create();var previous=RenderTexture.active;
        try
        {
            var hero=visuals.Render(db.cards.First(c=>c.CardId=="HERO_03"),"hero");
            var cards=new[]{"EX1_131","CS2_179","EX1_134","EX1_133"}.Select(id=>visuals.Render(db.cards.First(c=>c.CardId==id))).ToArray();
            var menu=visuals.Prop("MODEL_GAME_MENU_BUTTON");var banner=visuals.Prop("MODEL_RESULT_BANNER");var crown=visuals.Prop("MODEL_RESULT_CROWN");
            foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1440,1024),new Vector2(2335,515)})
            {
                RenderTexture.active=null;canvas.Release();canvas.width=(int)size.x;canvas.height=(int)size.y;canvas.Create();
                foreach(int count in new[]{1,2,3,4,5,7,10})
                {
                    RenderTexture.active=canvas;GL.Clear(true,true,new Color(.12f,.1f,.07f));
                    var view=new DesignViewport(size.x,size.y,1920,1080,1520);
                    Quad(canvas,hero,BattleLayout.Hero(true),view.Matrix,Color.white);
                    for(int i=0;i<count;i++)
                    {var p=HandLayout.OwnPose(i,count,calibration);Quad(canvas,cards[i%cards.Length],p.Rect(182,273),view.Matrix*BattleGeometry.Rotation(p.angle,p.center),Color.white);}
                    Quad(canvas,menu,BattleGeometry.EdgeRect(view,true,1014,77,53),view.Matrix,Color.white);
                    Vector2 from=new Vector2(960,790),to=new Vector2(1050,215);
                    for(int i=1;i<24;i++)
                    {float t=i/24f;var at=BattleGeometry.ArrowPoint(from,to,t);var tangent=BattleGeometry.ArrowTangent(from,to,t);Quad(canvas,Texture2D.whiteTexture,new Rect(at.x-9,at.y-5,18,10),view.Matrix*BattleGeometry.Rotation(Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg,at),Color.red);}
                    var end=BattleGeometry.ArrowTangent(from,to,1);
                    Quad(canvas,BattlePainter.ArrowTexture,new Rect(to.x-20,to.y,40,38),view.Matrix*BattleGeometry.Rotation(Mathf.Atan2(end.y,end.x)*Mathf.Rad2Deg+90,to),Color.red);
                    Save(canvas,"hand"+count+"-arrow-"+(int)size.x+"x"+(int)size.y);
                }
            }
            RenderTexture.active=null;canvas.Release();canvas.width=1920;canvas.height=1080;canvas.Create();
            var life=new BattleLifecycle();var opening=new StateView{match_id="gpu",phase="mulligan"};life.Observe(opening);
            foreach(float time in new[]{.15f,.7f,1.25f})
            {
                life.Reset();life.Observe(opening);life.Tick(opening,false,time);RenderTexture.active=canvas;GL.Clear(true,true,new Color(.12f,.1f,.07f));
                Quad(canvas,hero,life.HeroPose(BattleLayout.Hero(true),true),Matrix4x4.identity,Color.white);
                Quad(canvas,hero,life.HeroPose(BattleLayout.Hero(false),false),Matrix4x4.identity,Color.white);Save(canvas,"entrance-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            }
            foreach(float t in new[]{0,.18f,.4f,.7f,1})
            {
                RenderTexture.active=canvas;GL.Clear(true,true,new Color(.12f,.1f,.07f));
                fragments.Render(new Rect(810,300,300,375),hero,t,Matrix4x4.identity,1920,1080);
                Save(canvas,"hero-death-"+t.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),t<1);
            }
            foreach(string result in new[]{"victory","defeat"})
            {
                RenderTexture.active=canvas;GL.Clear(true,true,new Color(.12f,.1f,.07f));
                Quad(canvas,hero,new Rect(830,290,260,322),Matrix4x4.identity,Color.white);
                Quad(canvas,banner,new Rect(645,585,630,146),Matrix4x4.identity,Color.white);
                if(result=="victory")Quad(canvas,crown,new Rect(840,188,240,116),Matrix4x4.identity,Color.white);
                Save(canvas,result+"-components");
            }
            foreach(var profile in JsonUtility.FromJson<CardEffectIndex>(File.ReadAllText("../local/runtime/effect-index.json")).entries.Where(e=>(e.visuals??new CardEffectSprite[0]).Any(s=>s.HasNative)))
            {
                foreach(float t in new[]{.25f,.55f,.8f})
                {
                    RenderTexture.active=canvas;GL.Clear(true,true,new Color(.12f,.1f,.07f));
                    foreach(var sprite in profile.visuals)
                    {
                        float scale=!sprite.HasNative?1:sprite.native.Size(t);var rect=new Rect(760,340,400*scale,400*scale);
                        var tint=sprite.tint*(!sprite.HasNative?Color.white:sprite.native.Color(t));
                        particles.Render(rect,assets.Texture(sprite.texture),tint,BattleGeometry.Rotation(!sprite.HasNative?0:sprite.native.Angle(t),rect.center),1920,1080,false,sprite);
                    }
                    Save(canvas,profile.card_id+"-native-"+t.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),t==.55f);
                }
            }
            File.WriteAllText(Path.Combine(folder,"render-report.json"),"{\"passed\":"+passed+",\"kind\":\"offscreen-component-render\"}");
            Debug.Log("LIFECYCLE GPU CHECKS PASSED: "+passed);
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(surface);surface=null;fragments.Dispose();particles.Dispose();visuals.Clear();canvas.Release();UnityEngine.Object.DestroyImmediate(canvas);}
    }
}
