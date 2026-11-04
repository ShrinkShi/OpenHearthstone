using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 直接渲染到 GPU 纹理，可在用户游戏时检查原模型而不操作桌面。
public static class InteractionRenderChecks
{
    static string folder;
    static int passed;
    static void Save(Texture source,string name)
    {
        var previous=RenderTexture.active;
        var rt=source as RenderTexture;
        bool temporary=rt==null;
        if(temporary){rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);Graphics.Blit(source,rt);}
        var image=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,source.width,source.height),0,0);image.Apply();
            if(image.GetPixels32().Count(p=>p.a>100&&p.r+p.g+p.b>120)<500)throw new Exception("Empty asset render: "+name);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());passed++;
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);if(temporary)RenderTexture.ReleaseTemporary(rt);}
    }
    public static void Run()
    {
        folder=Path.GetFullPath("../tests/visual/current/interaction-models");Directory.CreateDirectory(folder);passed=0;
        var root=Path.GetFullPath("../local");var registry=new AssetRegistry(root);
        var calibration=new UICalibration(Path.Combine(root,"ui-calibration.json"));string error;
        if(!calibration.Load(out error))throw new Exception(error);UICalibration.Current=calibration;
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText(Path.Combine(root,"card-database.json")));
        var visuals=new CardVisuals(registry);
        try
        {
            var power=database.cards.First(c=>c.CardId=="HERO_01bp");
            var frame=visuals.Render(power,"power_detail");var portrait=visuals.Render(power,"power");
            Save(frame,"power-frame");Save(portrait,"power-portrait");
            var combined=new RenderTexture(320,480,0,RenderTextureFormat.ARGB32);combined.Create();var active=RenderTexture.active;
            try
            {
                RenderTexture.active=combined;GL.Clear(true,true,Color.clear);GL.PushMatrix();GL.LoadPixelMatrix(0,320,480,0);
                Graphics.DrawTexture(new Rect(0,0,320,480),frame);Graphics.DrawTexture(BattleInteraction.PowerPortraitRect(new Rect(0,0,320,480)),portrait);
                GL.PopMatrix();Save(combined,"power-composite");
            }
            finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(combined);}
            Save(visuals.Prop("MODEL_GEM_ARMOR"),"armor");
            Save(visuals.Prop("MODEL_GEM_DURABILITY"),"durability");
            Save(visuals.Prop("MODEL_GEM_HEALTH"),"health");
            Save(visuals.Prop("MODEL_MINION_TRIGGER"),"trigger-lightning");
            Save(visuals.Prop("MODEL_MINION_TAUNT"),"taunt-shield");
            var tauntCard=database.cards.First(c=>c.CardId=="CS2_179");
            var tauntCanvas=new RenderTexture(256,340,0,RenderTextureFormat.ARGB32);tauntCanvas.Create();var oldTaunt=RenderTexture.active;
            try
            {
                RenderTexture.active=tauntCanvas;GL.Clear(true,true,Color.clear);GL.PushMatrix();GL.LoadPixelMatrix(0,256,340,0);
                Graphics.DrawTexture(new Rect(2,2,252,336),visuals.Prop("MODEL_MINION_TAUNT"));
                Graphics.DrawTexture(new Rect(25,20,206,296),visuals.Render(tauntCard,"minion"));GL.PopMatrix();
                Save(tauntCanvas,"taunt-composite");
            }
            finally{RenderTexture.active=oldTaunt;UnityEngine.Object.DestroyImmediate(tauntCanvas);}
            Save(visuals.Prop("MODEL_GAME_MENU_BUTTON"),"menu-gear");
            foreach(string id in new[]{"MODEL_MINION_DEATHRATTLE","MODEL_MINION_POISONOUS"})
            {
                var icon=visuals.Prop(id);Save(icon,id.ToLowerInvariant());
                var previousIcon=RenderTexture.active;var pixels=new Texture2D(icon.width,icon.height,TextureFormat.RGBA32,false);
                try
                {
                    RenderTexture.active=icon as RenderTexture;pixels.ReadPixels(new Rect(0,0,icon.width,icon.height),0,0);pixels.Apply();
                    if(pixels.GetPixel(0,0).a>.05f)throw new Exception("Mechanic icon has rectangular background: "+id);passed++;
                    if(id=="MODEL_MINION_POISONOUS"&&pixels.GetPixels32().Count(p=>p.a>100&&p.g>p.r*1.35f&&p.g>p.b*1.15f)<300)throw new Exception("Poison bottle is missing green liquid");passed++;
                }
                finally{RenderTexture.active=previousIcon;UnityEngine.Object.DestroyImmediate(pixels);}
            }
            Save(visuals.Render(database.cards.First(c=>c.CardId=="CS2_082"),"weapon"),"weapon-equipped");
            var weaponImage=visuals.Render(database.cards.First(c=>c.CardId=="CS2_082"),"weapon");var activeWeapon=RenderTexture.active;
            var weaponPixels=new Texture2D(64,64,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=weaponImage as RenderTexture;weaponPixels.ReadPixels(new Rect(96,96,64,64),0,0);weaponPixels.Apply();
                if(weaponPixels.GetPixels32().Count(p=>p.r>245&&p.g>245&&p.b>245)>500)throw new Exception("Weapon portrait is white placeholder");passed++;
            }
            finally{RenderTexture.active=activeWeapon;UnityEngine.Object.DestroyImmediate(weaponPixels);}
            Save(visuals.Render(power,"power_used"),"power-used");
            foreach(var count in new[]{1,8,16,24,30})Save(visuals.Prop(ClientApp.DeckModel(count)),"deck-"+count);
            var buttons=new MenuButtonVisuals();
            try
            {
                var button=buttons.Image(registry,false);Save(button,"menu-button-filled");
                if(button.GetPixel(button.width/2,button.height/2).a<.99f)throw new Exception("Menu button middle missing");passed++;
            }
            finally{buttons.Dispose();}
            Save(visuals.Prop("MODEL_MANA_COUNTER"),"mana-counter");
            var mana=visuals.Prop("MODEL_MANA_COUNTER");var oldManaActive=RenderTexture.active;
            var manaPixels=new Texture2D(mana.width,mana.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=mana as RenderTexture;manaPixels.ReadPixels(new Rect(0,0,mana.width,mana.height),0,0);manaPixels.Apply();
                if(manaPixels.GetPixels32().Count(p=>p.a>100&&p.b>p.r*1.5f)<1000)throw new Exception("Mana counter lost blue surface");passed++;
            }
            finally{RenderTexture.active=oldManaActive;UnityEngine.Object.DestroyImmediate(manaPixels);}
            using(var turn=new TurnButtonVisuals(registry))
            {
                Save(turn.RenderSample("ENDTURN_WAITING",0,Color.white),"end-turn-front");
                Save(turn.RenderSample("ENDTURN_WAITING",.19f,Color.white),"end-turn-flip");
                Save(turn.RenderSample("ENDTURN_WAITING",.6f,Color.white),"end-turn-back");
            }
            Save(visuals.Render(database.cards.First(c=>c.CardId=="CS2_182"),"history_own"),"history-card");
            Save(visuals.Render(database.cards.First(c=>c.CardId=="CS2_172"),"history_attack_enemy"),"history-attack");
            Save(visuals.Render(database.cards.First(c=>c.CardId=="EX1_012")),"minion-card");
            var shader=new Material(Resources.Load<Shader>("InteractionGlow"));
            var source=visuals.Render(database.cards.First(c=>c.CardId=="EX1_012"));
            var outline=new RenderTexture(source.width+64,source.height+64,0,RenderTextureFormat.ARGB32);outline.Create();var beforeOutline=RenderTexture.active;
            try
            {
                shader.SetFloat("_Radius",26);shader.SetFloat("_Padding",32);Graphics.Blit(source,outline,shader);Save(outline,"card-outline");
            }
            finally{RenderTexture.active=beforeOutline;UnityEngine.Object.DestroyImmediate(outline);UnityEngine.Object.DestroyImmediate(shader);}
            SaveCombo(visuals,database);
            var particles=new SpellParticles();var canvas=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32);canvas.Create();var oldActive=RenderTexture.active;
            try
            {
                foreach(var id in new[]{"VFX_FIRE_CRESCENT","VFX_FIRE_SPARK","VFX_FROST_MISSILE","VFX_FROST_SHARDS","VFX_INTELLECT_SYMBOL","VFX_INTELLECT_RUNES","VFX_SUMMON_RING","VFX_SUMMON_BURST","VFX_SUMMON_MOTE","VFX_FAN_DAGGER","VFX_FAN_TRAIL","VFX_EVISCERATE_TRAIL","VFX_ROGUE_DAGGER","VFX_COLD_BLOOD_FACE","VFX_COLD_BLOOD_FURY"})
                {
                    var background=new Color(.12f,.10f,.08f,1);RenderTexture.active=canvas;GL.Clear(true,true,background);
                    particles.Render(new Rect(0,0,256,256),registry.Texture(id),Color.white,Matrix4x4.identity,256,256,id=="VFX_COLD_BLOOD_FURY");
                    Save(canvas,id.ToLowerInvariant());
                    var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                    var corner=pixels.GetPixel(0,0);UnityEngine.Object.DestroyImmediate(pixels);
                    if(Mathf.Abs(corner.r-background.r)>.03f||Mathf.Abs(corner.g-background.g)>.03f||Mathf.Abs(corner.b-background.b)>.03f)throw new Exception("Particle background not transparent: "+id);
                    passed++;
                }
            }
            finally{RenderTexture.active=oldActive;particles.Dispose();UnityEngine.Object.DestroyImmediate(canvas);}
            var board=registry.Scene("SCENE_BOARD");AssetRegistry.Fit(board,28);
            var table=registry.Scene("MODEL_BOARD_TABLE");AssetRegistry.Fit(table,48);
            float floor=board.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.y),top=table.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.max.y);
            table.transform.position+=Vector3.down*(top-floor+.2f);table.transform.SetParent(board.transform,true);
            var boardCamera=new GameObject("BoardRenderCheck").AddComponent<Camera>();boardCamera.enabled=false;boardCamera.farClipPlane=100;
            boardCamera.clearFlags=CameraClearFlags.SolidColor;boardCamera.backgroundColor=new Color(.035f,.022f,.014f);
            var boardFrame=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);boardFrame.Create();
            try
            {
                new BoardPresentation(board,boardCamera).Show(Vector2.zero);
                if(Vector3.Dot(boardCamera.transform.forward,Vector3.down)<.999f)throw new Exception("Board camera is tilted");passed++;
                boardCamera.targetTexture=boardFrame;boardCamera.Render();Save(boardFrame,"board-top-down");
                SaveHand(visuals,registry,database,calibration,boardFrame,10);
            }
            finally
            {
                boardCamera.targetTexture=null;boardFrame.Release();UnityEngine.Object.DestroyImmediate(boardFrame);UnityEngine.Object.DestroyImmediate(boardCamera.gameObject);
                foreach(var renderer in board.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(board);
            }
            File.WriteAllText(Path.Combine(folder,"render-report.json"),"{\"passed\":"+passed+",\"type\":\"offscreen-model-render\"}");
            Debug.Log("INTERACTION MODEL RENDERS PASSED: "+passed);
        }
        finally{visuals.Clear();UICalibration.Current=null;}
    }
    static void SaveCombo(CardVisuals visuals,CardDB database)
    {
        var source=visuals.Render(database.cards.First(c=>c.CardId=="EX1_131"));
        var mask=new RenderTexture(384,544,0,RenderTextureFormat.ARGB32);mask.Create();
        var canvas=new RenderTexture(384,544,0,RenderTextureFormat.ARGB32);canvas.Create();
        var shader=new Material(Resources.Load<Shader>("InteractionGlow"));var previous=RenderTexture.active;
        try
        {
            shader.SetFloat("_Radius",26);shader.SetFloat("_Padding",32);Graphics.Blit(source,mask,shader);
            RenderTexture.active=canvas;GL.Clear(true,true,new Color(.10f,.08f,.05f));GL.PushMatrix();GL.LoadPixelMatrix(0,384,544,0);
            Graphics.DrawTexture(new Rect(0,0,384,544),mask,new Rect(0,0,1,1),0,0,0,0,BattleInteraction.PlayGlow(new EntityView{combo_ready=true}));
            Graphics.DrawTexture(new Rect(32,32,320,480),source);GL.PopMatrix();Save(canvas,"combo-yellow-components");
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(shader);UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(canvas);}
    }
    static void SaveHand(CardVisuals visuals,AssetRegistry registry,CardDB database,UICalibration calibration,RenderTexture board,int count)
    {
        var canvas=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32);canvas.Create();var previous=RenderTexture.active;
        try
        {
            Graphics.Blit(board,canvas);RenderTexture.active=canvas;GL.PushMatrix();GL.LoadPixelMatrix(0,1920,1080,0);
            for(int i=0;i<count;i++)
            {
                var pose=HandLayout.OwnPose(i,count,calibration);var rect=pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight);
                var card=database.cards.First(c=>c.CardId==(i%3==0?"CS2_029":i%3==1?"EX1_131":"CS2_179"));
                var rotation=Matrix4x4.TRS(pose.center,Quaternion.Euler(0,0,pose.angle),Vector3.one)*Matrix4x4.Translate(-pose.center);
                GL.PushMatrix();GL.MultMatrix(rotation*calibration.Get(UICalibration.CardTemplate(card)).Matrix(rect,new Vector2(rect.width/320,rect.height/480)));
                Graphics.DrawTexture(rect,visuals.Render(card));GL.PopMatrix();
            }
            var number=BattleLayout.ManaNumber(true);
            GL.PushMatrix();GL.MultMatrix(calibration.Get("battle.mana.own").Matrix(BattleLayout.Mana(true),Vector2.one)*calibration.Get("battle.mana.own/number").Matrix(number,Vector2.one));
            Graphics.DrawTexture(number,visuals.Prop("MODEL_MANA_COUNTER"));GL.PopMatrix();GL.PopMatrix();Save(canvas,"hand10-mana-components");
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(canvas);}
    }
}
