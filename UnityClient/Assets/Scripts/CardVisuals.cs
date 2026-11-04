using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 复用官方模型的 UV 与层级，卡面只替换运行时原画和职业图集。
public sealed class CardVisuals
{
    readonly AssetRegistry assets;
    readonly Dictionary<string,RenderTexture> cache=new Dictionary<string,RenderTexture>();
    readonly Dictionary<string,UIEditTarget[]> parts=new Dictionary<string,UIEditTarget[]>();
    readonly Dictionary<string,HistoryPortrait> historyPortraits=new Dictionary<string,HistoryPortrait>();
    [Serializable] sealed class PortraitCrop {public float scale=1,x,y;}
    [Serializable] sealed class HistoryPortrait {public string card_id;public PortraitCrop full,half;}
    [Serializable] sealed class HistoryPortraits {public HistoryPortrait[] entries;}
    readonly Camera camera;
    public int RenderCount{get{return cache.Count;}}
    readonly string[] classes={"","DEATHKNIGHT","DRUID","HUNTER","MAGE","PALADIN","PRIEST","ROGUE","SHAMAN","WARLOCK","WARRIOR","","GENERIC","","DEMONHUNTER"};
    public void Clear(){foreach(var rt in cache.Values)AssetRegistry.ReleaseTransient(rt);cache.Clear();parts.Clear();}
    public void Invalidate(string id)
    {
        if(id=="*"){Clear();return;}
        if(!id.Contains("/mesh/")&&!id.EndsWith("/art",StringComparison.Ordinal))return;
        string template=id.Substring(0,id.IndexOf('/'));
        var invalid=new List<string>();foreach(var pair in parts)if(Array.Exists(pair.Value,p=>p.id.StartsWith(template+"/",StringComparison.Ordinal)))invalid.Add(pair.Key);
        foreach(var key in invalid){RenderTexture rt;if(cache.TryGetValue(key,out rt))AssetRegistry.ReleaseTransient(rt);cache.Remove(key);parts.Remove(key);}
    }
    public CardVisuals(AssetRegistry registry)
    {
        assets=registry;
        var cropPath=Path.Combine(registry.Root,"history-portraits.json");
        if(File.Exists(cropPath))foreach(var entry in JsonUtility.FromJson<HistoryPortraits>(File.ReadAllText(cropPath)).entries)historyPortraits[entry.card_id]=entry;
        camera=new GameObject("CardPortraitCamera").AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=30;
        camera.transform.rotation=Quaternion.Euler(90,0,0);
    }
    public Texture Render(CardDef card,string mode="card")
    {
        string key=mode+":"+card.CardId;
        if(cache.ContainsKey(key)) return cache[key];
        return Capture(CreateActor(card,mode),key,mode=="card"||mode=="power_detail"?320:256,mode=="card"||mode=="power_detail"?480:mode.StartsWith("power")||mode=="weapon"||mode.StartsWith("history",StringComparison.Ordinal)?256:320,Template(card,mode));
    }
    // Caller owns the returned model and its independently instantiated materials.
    public GameObject CreateActor(CardDef card,string mode="minion")
    {
        if(mode=="minion")
        {var original=CreateOriginalMinion(card);original.transform.position=new Vector3(0,-1000,0);return original;}
        string model=mode.StartsWith("history",StringComparison.Ordinal)?(mode.Contains("attack")?"MODEL_HISTORY_ATTACK":"MODEL_HISTORY_CARD"):mode=="weapon"?"MODEL_WEAPON":mode=="power_detail"?"MODEL_POWER_DETAIL":mode=="hero"?"MODEL_HERO":mode.StartsWith("power")?"MODEL_HERO_POWER":mode=="minion"?"MODEL_MINION":card.Type==5?"MODEL_CARD_SPELL":card.Type==7?"MODEL_CARD_WEAPON":"MODEL_CARD_MINION";
        string template=Template(card,mode);
        var root=assets.Scene(model);
        root.transform.position=new Vector3(0,-1000,0);
        root.transform.localRotation=Quaternion.Euler(0,180,0);
        if(mode=="power_used")root.transform.localRotation=Quaternion.Euler(180,180,0);
        var portrait=assets.Texture("CARD_"+card.CardId);
        string category=card.Type==5?"ABILITY":card.Type==7?"WEAPON":"MINION";
        string className=card.Class>=0&&card.Class<classes.Length?classes[card.Class]:"GENERIC";
        var frame=assets.Texture("FRAME_"+category+"_"+className)??assets.Texture("UI_CARD_INHAND_GENERIC");
        var banner=assets.Texture("UI_GENFX_CARD_INHAND_BANNERATLAS");
        foreach(var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer=30;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string name=renderer.gameObject.name.ToLowerInvariant();
            if(mode=="power_detail"&&name!="lettuce_ability_frame_mesh"&&name!="lettuce_ability_description_mesh")
            {renderer.gameObject.SetActive(false);continue;}
            if((mode=="power_detail"||mode.StartsWith("history",StringComparison.Ordinal))&&(name.Contains("highlight")||name.Contains("shadow")||name=="gem_mana"))
            {renderer.gameObject.SetActive(false);continue;}
            if(name.Contains("dragon")||name.Contains("raritygem")||name.Contains("raceplate")||name.Contains("description_school"))
            {
                bool shown=name.Contains("dragon")?card.Rarity==5:name.Contains("raritygem")?card.Rarity>1:name.Contains("raceplate")?card.Race!=0:false;
                renderer.gameObject.SetActive(shown);
            }
            // Scene 已创建独立材质，避免 materials 再克隆一份并泄漏原实例。
            var materials=renderer.sharedMaterials;
            for(int j=0;j<materials.Length;j++)
            {
                var mat=materials[j];string material=mat.name.ToLowerInvariant();Texture image;
                if(mode=="power_detail")image=assets.Texture("UI_POWER_DETAIL_FRAME");
                else if(mode.StartsWith("history",StringComparison.Ordinal))image=material.Contains("portrait")?portrait:mat.mainTexture;
                else if(mode=="hero") image=j==0?assets.Texture("UI_GENFX_HEROFRAME"):portrait;
                else if(mode=="weapon")image=j==1?portrait:frame;
                else if(mode.StartsWith("power")) image=material.Contains("portrait")?portrait:mat.mainTexture;
                else if(mode=="minion") image=(name.Contains("portrait")||j==0)?portrait:assets.Texture("UI_GENFX_CARD_INPLAY_ALLYFRAME");
                else if(material.Contains("back")) image=assets.Texture("UI_CARD_BACK");
                else if(material.Contains("raritygems")) image=assets.Texture("UI_GENFX_RARITYGEMS");
                else if(name.Contains("portrait") && j==(card.Type==4?0:1)) image=portrait;
                else if(material.Contains("banner")||name.Contains("discrption")) image=banner;
                else image=frame;
                mat.mainTexture=image;
                // 原画 alpha 经常保存特殊材质参数；头像轮廓由原网格裁切。
                mat.SetColor("_Tint",Color.white);mat.SetFloat("_UseAlpha",mode.StartsWith("power")||mode=="weapon"||image==portrait?0:1);mat.SetFloat("_VertexTint",0);mat.SetFloat("_Effect",0);mat.SetInt("_ZWrite",1);
                // 清除导入材质的 Multiply/Additive 状态，否则普通头像仍会被二次压暗。
                mat.SetFloat("_Multiply",0);mat.SetInt("_SrcBlend",5);mat.SetInt("_DstBlend",10);
                mat.mainTextureScale=Vector2.one;mat.mainTextureOffset=Vector2.zero;mat.renderQueue=2000;
                if(mode.StartsWith("history",StringComparison.Ordinal)&&!material.Contains("portrait"))mat.SetColor("_Tint",mode.EndsWith("enemy")?new Color(1,.45f,.35f):new Color(.5f,.7f,1));
                if(material.Contains("raritygems")){mat.mainTextureOffset=new Vector2(card.Rarity==3||card.Rarity==5?.5f:0,card.Rarity>=4?-.5f:0);mat.SetFloat("_UseAlpha",0);}
                HistoryPortrait history;
                if(image==portrait&&mode.StartsWith("history",StringComparison.Ordinal)&&historyPortraits.TryGetValue(card.CardId,out history))
                {
                    var crop=mode.Contains("attack")?history.half:history.full;
                    if(crop!=null){mat.mainTextureScale=Vector2.one*crop.scale;mat.mainTextureOffset=Vector2.one*(1-crop.scale)*.5f-new Vector2(crop.x,crop.y);}
                }
                if(image==portrait&&portrait!=null&&UICalibration.Current!=null)
                {
                    var uv=UICalibration.Current.Get(template+"/art");
                    var scale=new Vector2((uv.flipX?-1:1)/uv.scaleX,(uv.flipY?-1:1)/uv.scaleY);var sourceScale=mat.mainTextureScale;
                    mat.mainTextureOffset+=Vector2.Scale(sourceScale,(Vector2.one-scale)*.5f-new Vector2(uv.x/320,uv.y/480));
                    mat.mainTextureScale=Vector2.Scale(sourceScale,scale);
                    mat.SetFloat("_UVAngle",uv.angle);
                }
            }
            renderer.sharedMaterials=materials;
        }
        if(mode.StartsWith("history",StringComparison.Ordinal))root.transform.localRotation=Quaternion.identity;
        return root;
    }
    static string Template(CardDef card,string mode)
    {return mode=="card"?UICalibration.CardTemplate(card):mode=="power_detail"?"power.detail":mode=="power_used"?"power":mode;}
    GameObject CreateOriginalMinion(CardDef card)
    {
        var model=new GameObject("Original minion board slot");
        try
        {
            var source=assets.Scene("MODEL_ACTOR_MINION");source.transform.SetParent(model.transform,false);
            var identity=source.GetComponentInChildren<NativeActorIdentity>(true);
            if(identity==null||identity.MeshRenderer==null||identity.AnimationProgram==null||identity.SpellDefinition==null)
                throw new InvalidOperationException("Original minion Actor references are incomplete");
            identity.transform.localPosition=Vector3.zero;
            identity.ConfigureDefinitionTags(card.Type,card.EliteTag,card.MercenaryTag);
            foreach(var t in source.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            foreach(var r in source.GetComponentsInChildren<Renderer>(true))if(r!=identity.MeshRenderer)r.gameObject.SetActive(false);
            var portrait=identity.MeshRenderer.sharedMaterials[identity.PortraitMaterialIndex];
            portrait.mainTexture=assets.Texture("CARD_"+card.CardId);identity.ConfigurePortrait(portrait.mainTexture);portrait.SetFloat("_UseAlpha",0);portrait.SetColor("_Tint",Color.white);
            if(UICalibration.Current!=null)
            {
                var uv=UICalibration.Current.Get("minion/art");
                var scale=new Vector2((uv.flipX?-1:1)/uv.scaleX,(uv.flipY?-1:1)/uv.scaleY);
                portrait.mainTextureScale=scale;portrait.mainTextureOffset=(Vector2.one-scale)*.5f-new Vector2(uv.x/320,uv.y/480);portrait.SetFloat("_UVAngle",uv.angle);
            }
            return model;
        }
        catch
        {
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))foreach(var material in r.sharedMaterials.Distinct())AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(model);throw;
        }
    }
    public sealed class LiveActor : IDisposable
    {
        public readonly GameObject Model;
        public readonly GameObject ActorObject;
        public readonly string CardId;
        readonly Camera camera;
        readonly RenderTexture output;
        readonly Vector3 originalScale,originalPosition;
        readonly Renderer[] renderers;
        readonly AssetRegistry assets;
        bool disposed;
        const float Margin=2;
        static int nextSlot;
        internal LiveActor(CardVisuals owner,CardDef card,string mode,bool original=false)
        {
            assets=owner.assets;
            CardId=card.CardId;
            if(original)
            {
                if(mode!="minion")throw new ArgumentException("Only the recovered original minion Actor is routed here");
                Model=owner.CreateOriginalMinion(card);
                ActorObject=Model.GetComponentInChildren<NativeActorIdentity>(true).gameObject;
            }
            else{Model=owner.CreateActor(card,mode);ActorObject=Model;}
            Model.transform.position=new Vector3(40000+(nextSlot++%1000)*100,-1000,0);
            originalPosition=ActorObject.transform.position;originalScale=ActorObject.transform.localScale;
            renderers=Model.GetComponentsInChildren<Renderer>(true);
            camera=new GameObject("Native battle Actor camera").AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.transform.rotation=Quaternion.Euler(90,0,0);
            var bounds=new Bounds(Model.transform.position,Vector3.zero);bool first=true;
            foreach(var renderer in Model.GetComponentsInChildren<Renderer>())
            {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            camera.aspect=256f/320;float size=Mathf.Max(bounds.size.z*.5f,bounds.size.x*.5f/camera.aspect)*1.035f;
            camera.orthographicSize=size*Margin;camera.transform.position=new Vector3(bounds.center.x,bounds.max.y+8,bounds.center.z);
            owner.CalibrateActor(Model,Template(card,mode),bounds,size*2,size*2*camera.aspect,null);
            if(!original)Model.AddComponent<NativeActorPresentation>().Configure(Model.transform.GetChild(0).gameObject);
            output=new RenderTexture(512,640,24,RenderTextureFormat.ARGB32){name="Actor:"+CardId,antiAliasing=4};output.Create();
        }
        public bool Visible{get{return Model!=null&&Model.activeInHierarchy&&Model.GetComponentsInChildren<Renderer>(true).Any(r=>r!=null&&r.enabled&&r.gameObject.activeInHierarchy)&&ActorObject.transform.localScale.sqrMagnitude>.00000001f;}}
        public bool PresentationVisible{get{return Visible&&NativeActorPresentation.IsActorShown(Model);}}
        public Rect ImageRect(Rect rect){return new Rect(rect.center.x-rect.width*Margin/2,rect.center.y-rect.height*Margin/2,rect.width*Margin,rect.height*Margin);}
        public Rect StatusRect(Rect rect)
        {
            var scale=ActorObject.transform.localScale;
            var shift=ActorObject.transform.position-originalPosition;
            float span=camera.orthographicSize*2/Margin;
            var center=rect.center+new Vector2(shift.x/(span*camera.aspect)*rect.width,-shift.z/span*rect.height);
            float width=rect.width*Mathf.Abs(scale.x/originalScale.x),height=rect.height*Mathf.Abs(scale.z/originalScale.z);
            return new Rect(center.x-width/2,center.y-height/2,width,height);
        }
        public Texture Render()
        {
            if(Event.current!=null&&Event.current.type!=EventType.Repaint)return output;
            foreach(var renderer in renderers)NativeActorMaterial.BindAmbient(renderer,assets);
            camera.targetTexture=output;
            try{camera.Render();return output;}finally{camera.targetTexture=null;}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(Model!=null)Model.SetActive(false);if(camera!=null)camera.targetTexture=null;
            foreach(var material in renderers.Where(r=>r!=null).SelectMany(r=>r.sharedMaterials).Distinct())AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(output);if(camera!=null)AssetRegistry.ReleaseTransient(camera.gameObject);AssetRegistry.ReleaseTransient(Model);
        }
    }
    public LiveActor CreateLiveActor(CardDef card,string mode="minion",bool original=false){return new LiveActor(this,card,mode,original);}
    void CalibrateActor(GameObject root,string template,Bounds bounds,float spanZ,float spanX,string key)
    {
        var list=new List<UIEditTarget>();
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var b=renderer.bounds;string id=template+"/mesh/"+UICalibration.NodePath(renderer.transform,root.transform);
            list.Add(new UIEditTarget{id=id,label="模型 · "+renderer.name,rect=new Rect((b.min.x-bounds.center.x+spanX/2)/spanX*320,(bounds.center.z+spanZ/2-b.max.z)/spanZ*480,b.size.x/spanX*320,b.size.z/spanZ*480)});
            if(UICalibration.Current==null)continue;
            var edit=UICalibration.Current.Get(id);var node=renderer.transform;
            node.position+=new Vector3(edit.x/320*spanX,0,-edit.y/480*spanZ);
            node.rotation=Quaternion.Euler(0,edit.angle,0)*node.rotation;
            node.localScale=Vector3.Scale(node.localScale,new Vector3(edit.scaleX*(edit.flipX?-1:1),1,edit.scaleY*(edit.flipY?-1:1)));
        }
        if(key!=null)parts[key]=list.ToArray();
    }
    public Texture Prop(string id)
    {
        string key="prop:"+id;
        if(cache.ContainsKey(key)) return cache[key];
        var root=assets.Scene(id=="MODEL_MANA_COUNTER"?"MODEL_MANA_ENEMY":id);root.transform.position=new Vector3(0,-1000,0);root.transform.localRotation=Quaternion.Euler(0,180,0);
        if(id=="MODEL_GEM_ARMOR"||id=="MODEL_MINION_POISONOUS"||id=="MODEL_MINION_DEATHRATTLE")root.transform.localRotation=Quaternion.identity;
        if(id=="MODEL_GAME_MENU_BUTTON")root.transform.localRotation=Quaternion.Euler(90,0,0);
        if(id.StartsWith("MODEL_DECK_",StringComparison.Ordinal))root.transform.localRotation=Quaternion.Euler(70,180,-12);
        foreach(var transform in root.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=30;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if((id=="MODEL_MANA_COUNTER"&&renderer.name!="polySurface298")||(id=="MODEL_GAME_MENU_BUTTON"&&renderer.name.Contains("Highlight")))
            {renderer.gameObject.SetActive(false);continue;}
            if((id=="MODEL_MULLIGAN_BANNER"||id=="MODEL_MULLIGAN_BUTTON")&&(renderer.name.Contains("Glow")||renderer.name.Contains("Desaturate")))
            {renderer.gameObject.SetActive(false);continue;}
            foreach(var mat in renderer.sharedMaterials)
            {
                if(id=="MODEL_MINION_POISONOUS")
                {
                    // 原网格默认是占位材质，药瓶内容由原客户端脚本换入。
                    mat.mainTexture=assets.Texture("UI_MINION_POISONOUS");mat.mainTextureScale=Vector2.one;mat.mainTextureOffset=Vector2.zero;
                }
                if(id=="MODEL_MANA_COUNTER")
                {
                    // 裸网格材质没有贴图；原蓝色计数框位于棋盘 STR_FRAME 图集左上槽。
                    var uv=renderer.GetComponent<MeshFilter>().sharedMesh.uv;
                    float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                    foreach(var point in uv){minX=Mathf.Min(minX,point.x);minY=Mathf.Min(minY,point.y);maxX=Mathf.Max(maxX,point.x);maxY=Mathf.Max(maxY,point.y);}
                    var scale=new Vector2(.112f/(maxX-minX),.058f/(maxY-minY));
                    mat.mainTexture=assets.Texture("UI_BOARD_FRAME");mat.mainTextureScale=scale;mat.mainTextureOffset=new Vector2(.499f-minX*scale.x,.937f-minY*scale.y);
                }
                // 宝石 alpha 储存材质参数；轮廓由原始网格决定。
                mat.SetFloat("_UseAlpha",0);mat.SetFloat("_VertexTint",0);mat.SetFloat("_Effect",0);
                mat.SetFloat("_Multiply",0);mat.SetInt("_SrcBlend",5);mat.SetInt("_DstBlend",10);
                mat.SetColor("_Tint",Color.white);mat.SetInt("_ZWrite",1);mat.renderQueue=2000;
            }
        }
        return Capture(root,key,id=="MODEL_MULLIGAN_BANNER"||id=="MODEL_RESULT_BANNER"?640:id=="MODEL_MULLIGAN_BUTTON"?360:id=="MODEL_MANA_COUNTER"||id=="MODEL_RESULT_CROWN"?320:192,id=="MODEL_MULLIGAN_BANNER"||id=="MODEL_RESULT_BANNER"?148:id=="MODEL_MULLIGAN_BUTTON"||id=="MODEL_MANA_COUNTER"?128:id=="MODEL_RESULT_CROWN"?160:id=="MODEL_CARD_BACK"||id=="MODEL_MINION_TAUNT"||id.StartsWith("MODEL_DECK_",StringComparison.Ordinal)?280:192);
    }
    public void RegisterParts(CardDef card,string mode,Rect rect)
    {
        if(UICalibration.Current==null||!UICalibration.Current.Collecting)return;
        UIEditTarget[] items;if(!parts.TryGetValue(mode+":"+card.CardId,out items))return;
        var units=new Vector2(rect.width/320,rect.height/480);
        foreach(var part in items)
        {
            var r=new Rect(rect.x+part.rect.x*units.x,rect.y+part.rect.y*units.y,part.rect.width*units.x,part.rect.height*units.y);
            UICalibration.Current.Register(part.id,part.label,r,units);
        }
    }
    Texture Capture(GameObject root,string key,int width,int height,string template=null)
    {
        var bounds=new Bounds(root.transform.position,Vector3.zero);bool first=true;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>()) {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name=key,antiAliasing=4};rt.Create();
        camera.aspect=(float)width/height;
        camera.orthographicSize=Mathf.Max(bounds.size.z*.5f,bounds.size.x*.5f/camera.aspect)*1.035f;
        camera.transform.position=new Vector3(bounds.center.x,bounds.max.y+8,bounds.center.z);
        if(template!=null)
        {
            // 固定原始相机取景后再校对部件，否则自动包围盒会抵消移动与缩放。
            float spanZ=camera.orthographicSize*2,spanX=spanZ*camera.aspect;
            CalibrateActor(root,template,bounds,spanZ,spanX,key);
        }
        camera.targetTexture=rt;camera.Render();camera.targetTexture=null;
        root.SetActive(false);
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) foreach(var material in renderer.sharedMaterials) AssetRegistry.ReleaseTransient(material);
        AssetRegistry.ReleaseTransient(root);cache[key]=rt;
        return rt;
    }
}
