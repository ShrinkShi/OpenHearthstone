using System;
using System.IO;
using System.Linq;
using UnityEngine;

// 固定尺寸 GPU 离屏场景，不抢焦点、不操作桌面；图片明确不包含 IMGUI 操作面板。
public static class WindowRenderChecks
{
    static string folder;
    static int passed;
    static Material overlay;
    static void Layer(GameObject root){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=28;}
    static void Save(RenderTexture frame,string name)
    {
        var old=RenderTexture.active;var pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false);
        try
        {
            RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);pixels.Apply();
            if(pixels.GetPixels32().Count(p=>p.r+p.g+p.b>90)<frame.width*frame.height*.12f)throw new Exception("Empty scene: "+name);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());passed++;
        }
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static RenderTexture Render(Camera camera,DesignViewport viewport)
    {
        var frame=new RenderTexture(Mathf.RoundToInt(viewport.WindowWidth),Mathf.RoundToInt(viewport.WindowHeight),0,RenderTextureFormat.ARGB32);frame.Create();
        var scene=new RenderTexture(Mathf.RoundToInt(viewport.Pixels.width),Mathf.RoundToInt(viewport.Pixels.height),24,RenderTextureFormat.ARGB32);scene.Create();
        var rect=camera.rect;var old=RenderTexture.active;
        try
        {
            camera.targetTexture=scene;camera.rect=new Rect(0,0,1,1);camera.Render();camera.targetTexture=null;
            RenderTexture.active=frame;GL.Clear(true,true,Color.black);GL.PushMatrix();GL.LoadPixelMatrix(0,frame.width,frame.height,0);
            Graphics.DrawTexture(viewport.Pixels,scene);GL.PopMatrix();
        }
        finally{camera.targetTexture=null;camera.rect=rect;RenderTexture.active=old;scene.Release();UnityEngine.Object.DestroyImmediate(scene);}
        return frame;
    }
    static void Overlay(RenderTexture frame,Rect rect,Texture texture,Matrix4x4 matrix)
    {
        RenderTexture.active=frame;overlay.mainTexture=texture;overlay.SetColor("_Tint",Color.white);
        overlay.SetFloat("_UseAlpha",1);overlay.SetFloat("_ZWrite",0);
        GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,frame.width,frame.height,0);GL.MultMatrix(matrix);overlay.SetPass(0);GL.Begin(GL.QUADS);
            GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
            GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
        }
        finally{GL.PopMatrix();}
    }
    public static void Run()
    {
        folder=Path.GetFullPath("../tests/visual/current/window-feedback");Directory.CreateDirectory(folder);passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        overlay=new Material(Resources.Load<Material>("AssetSurface"));overlay.SetFloat("_UseAlpha",1);
        var calibration=new UICalibration(Path.GetFullPath("../local/ui-calibration.json"));string error;
        if(!calibration.Load(out error))throw new Exception(error);UICalibration.Current=calibration;
        var menu=assets.Scene("SCENE_MAIN_MENU");Layer(menu);
        var camera=new GameObject("WindowSceneCheck").AddComponent<Camera>();camera.enabled=false;
        camera.cullingMask=1<<28;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.farClipPlane=200;
        var presentation=new MenuPresentation(menu,camera,JsonUtility.FromJson<SceneData>(File.ReadAllText(assets.PathFor("SCENE_MAIN_MENU"))));
        try
        {
            foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1220,1110),new Vector2(2335,515)})
            {
                var viewport=new DesignViewport(size.x,size.y,1600,1000,1120);presentation.Show(viewport);
                var frame=Render(camera,viewport);
                try
                {
                    Save(frame,"menu-"+(int)size.x+"x"+(int)size.y);
                    var button=presentation.ButtonRect("FirstButton");
                    if(button.width<100||button.height<15)throw new Exception("Invalid projected menu button");passed++;
                }
                finally{frame.Release();UnityEngine.Object.DestroyImmediate(frame);}
            }
            menu.SetActive(false);
            var board=assets.Scene("SCENE_BOARD");AssetRegistry.Fit(board,28);
            var table=assets.Scene("MODEL_BOARD_TABLE");AssetRegistry.Fit(table,48);
            float floor=board.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.y),top=table.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.max.y);
            table.transform.position+=Vector3.down*(top-floor+.2f);table.transform.SetParent(board.transform,true);Layer(board);
            var boardPresentation=new BoardPresentation(board,camera);
            try
            {
                foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1220,1110),new Vector2(2335,515)})
                {
                    var viewport=new DesignViewport(size.x,size.y,1920,1080,1520);boardPresentation.Show(Vector2.zero,viewport);
                    var frame=Render(camera,viewport);
                    try
                    {
                        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));var visuals=new CardVisuals(assets);
                        try
                        {
                            // 子相机渲染会重设 GL 状态；先预热全部模型，再开始合成。
                            var hero=visuals.Render(db.cards.First(c=>c.CardId=="HERO_07"),"hero");
                            var power=visuals.Render(db.cards.First(c=>c.CardId=="HERO_03bp"),"power");
                            var card=visuals.Render(db.cards.First(c=>c.CardId=="EX1_131"));
                            var deathrattle=visuals.Prop("MODEL_MINION_DEATHRATTLE");var poisonous=visuals.Prop("MODEL_MINION_POISONOUS");
                            foreach(bool own in new[]{false,true})
                            {Overlay(frame,BattleLayout.Hero(own),hero,viewport.Matrix);Overlay(frame,BattleLayout.Power(own),power,viewport.Matrix);}
                            for(int i=0;i<10;i++)
                            {
                                var pose=HandLayout.OwnPose(i,10,calibration);var rect=pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight);
                                var rotation=Matrix4x4.TRS(pose.center,Quaternion.Euler(0,0,pose.angle),Vector3.one)*Matrix4x4.Translate(-pose.center);
                                Overlay(frame,rect,card,viewport.Matrix*rotation);
                            }
                            Overlay(frame,new Rect(775,570,90,90),deathrattle,viewport.Matrix);Overlay(frame,new Rect(985,570,90,90),poisonous,viewport.Matrix);
                        }
                        finally{visuals.Clear();}
                        Save(frame,"battle-components-"+(int)size.x+"x"+(int)size.y);
                    }
                    finally{RenderTexture.active=null;frame.Release();UnityEngine.Object.DestroyImmediate(frame);}
                }
            }
            finally{DestroyScene(board);}
            File.WriteAllText(Path.Combine(folder,"render-report.json"),"{\"passed\":"+passed+",\"type\":\"offscreen-scene-and-components\"}");Debug.Log("WINDOW GPU CHECKS PASSED: "+passed);
        }
        finally{DestroyScene(menu);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(overlay);UICalibration.Current=null;}
    }
    static void DestroyScene(GameObject root)
    {
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)UnityEngine.Object.DestroyImmediate(material);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
