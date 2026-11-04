using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;

public static class NativeFireblastCache
{
    [Serializable] sealed class Header{public string mesh_color_schema;}
    public static string Read(AssetRegistry assets)
    {
        string json=File.ReadAllText(Path.Combine(assets.Root,"fireblast-ui.json"));
        if(JsonUtility.FromJson<Header>(json).mesh_color_schema!="source-channel-v2")
            throw new InvalidDataException("Stale Fireblast vertex-color cache; run tools.fireblast_ui_assets to recover source-channel-v2 (Fireblast only)");
        return json;
    }
}

// 标准技能Actor的独立实例；保留原网格、材质、文本节点与翻面曲线。
public sealed class NativePowerVisual:IDisposable
{
    static readonly HashSet<string> StandardPowers=new HashSet<string>{"HERO_01bp","HERO_02bp","HERO_03bp","HERO_04bp","HERO_05bp","HERO_06bp","HERO_07bp","HERO_08bp","HERO_09bp","HERO_10bp","HERO_11bp"};
    public static bool SupportsCard(string cardId){return cardId!=null&&StandardPowers.Contains(cardId);}
    [Serializable] sealed class TextNode{public int node,font_size;public string name;public float width,height,character_size,outline_size;public bool outline,word_wrap;public Color color;public NativePowerTextSettings raw;}
    [Serializable] sealed class HighlightState{public int state;public Vector3 offset;public NativeMaterial material;}
    [Serializable] sealed class Highlight{public int node,queue;public string silhouette;public HighlightState[] states;}
    [Serializable] sealed class Entry{public string id,file;public TextNode[] texts;public Highlight highlight;}
    [Serializable] sealed class Catalog{public Entry[] roots;public NativeAnimation[] animations;public NativePowerFontDefinition[] fonts;}
    sealed class View:IDisposable
    {
        public NativeEffectInstance effect;public Entry entry;public bool initialized,used;public float changed;
        public NativeAnimation animation;public string portraitCard="HERO_08bp";
        public Renderer highlight;public Dictionary<int,Material> highlights=new Dictionary<int,Material>();
        public Dictionary<string,NativePowerText> texts=new Dictionary<string,NativePowerText>();
        public void Dispose(){foreach(var text in texts.Values)text.Dispose();effect.Dispose();}
    }
    readonly AssetRegistry assets;readonly Catalog catalog;readonly NativeEffectPlayer compositor;
    readonly NativePowerDirectDraw direct;
    readonly Dictionary<string,View> views=new Dictionary<string,View>();
    public Font NameFont,BodyFont;
    public Action<NativeAudioCue> AudioCue;
    readonly Dictionary<string,NativeAudioDefinition> buttonSounds=new Dictionary<string,NativeAudioDefinition>();
    public float Time{get;private set;}
    public void Tick(float delta){Time+=Mathf.Max(0,delta);}
    public int InstanceCount{get{return views.Count;}}
    public void BeginGUI(){direct.BeginGUI();}
    public NativePowerVisual(AssetRegistry assets)
    {
        this.assets=assets;catalog=JsonUtility.FromJson<Catalog>(NativeFireblastCache.Read(assets));
        foreach(string name in new[]{"used","restore","cancel"})buttonSounds.Add(name,JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,"fireblast-"+name+"-audio.json"))));
        compositor=new NativeEffectPlayer(assets);direct=new NativePowerDirectDraw();
    }
    public void PlayButtonSound(string name)
    {
        var cue=NativeAudio.Resolve(buttonSounds[name],"",0,1,()=>UnityEngine.Random.value);
        if(cue==null)throw new InvalidDataException("Original hero power UI sound unresolved: "+name);
        AudioCue?.Invoke(cue);
    }
    View Get(int entity,bool detail,bool enemy)
    {
        string key=entity+":"+detail+":"+enemy;View view;if(views.TryGetValue(key,out view))return view;
        string id=detail?(enemy?"MODEL_MAGE_POWER_DETAIL_ENEMY":"MODEL_MAGE_POWER_DETAIL"):"MODEL_MAGE_POWER_BUTTON";
        var entry=catalog.roots.Single(e=>e.id==id);
        var program=NativeVisualProgram.Parse(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        var effect=new NativeEffectInstance(program,assets,80+views.Count,detail?384:256,detail?576:256);
        try
        {
            effect.Seek(0);
            foreach(var node in effect.Nodes)
            {
                // Actor控制的高亮/升级/幽灵分支不由Prefab初始可见性决定。
                if(node.name=="Highlight"||node.name=="CardBackGlow"||node.name=="Orange_Fill_EvolutionCard"||node.name=="FX_Ghost_Quad")node.SetActive(false);
                var renderer=node.GetComponent<Renderer>();if(renderer!=null)NativeActorMaterial.BindAmbient(renderer,assets);
            }
            var portrait=effect.Nodes.Single(n=>n.name=="Mesh").GetComponent<Renderer>();
            portrait.sharedMaterials[1].SetTexture("_MainTex",assets.FireblastTextureFile(Path.GetFileName(assets.PathFor("CARD_HERO_08bp"))));
            // 固定原始正面取景；每帧重算包围盒会抵消原动画。
            var renderers=effect.Root.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            effect.FitPreviewCamera(bounds);
            view=new View{effect=effect,entry=entry};
            if(entry.highlight!=null)
            {
                view.highlight=effect.Nodes.Single(n=>n.name=="Highlight_Card_Plane").GetComponent<Renderer>();
                foreach(var state in entry.highlight.states.Where(s=>s.material!=null&&!String.IsNullOrEmpty(s.material.shader)))
                {
                    var material=effect.ReplaceVisualMaterial(view.highlight,state.material);material.mainTexture=assets.FireblastTextureFile(entry.highlight.silhouette);material.renderQueue=entry.highlight.queue;
                    material.SetFloat("_Seed",UnityEngine.Random.value);view.highlights.Add(state.state,material);
                }
                view.highlight.enabled=false;
            }
            views.Add(key,view);return view;
        }
        catch{effect.Dispose();throw;}
    }
    void Pose(View view,bool used,float now,bool animate)
    {
        if(!view.initialized||view.used!=used)
        {
            bool changed=view.initialized;view.initialized=true;view.used=used;
            view.animation=catalog.animations.Single(a=>a.name==(used?"HeroPower_Used":"HeroPower_Restore"));
            view.changed=changed&&animate?now:now-view.animation.duration;
            if(changed&&animate)PlayButtonSound(used?"used":"restore");
        }
        if(view.animation!=null)view.effect.SampleVisualAnimation(view.animation,now-view.changed);
    }
    public NativeEffectInstance Prepare(int entity,bool used,bool detail=false,bool enemy=false,float now=0,bool animate=false)
    {
        var view=Get(entity,detail,enemy);if(!detail)Pose(view,used,now,animate);return view.effect;
    }
    public RenderTexture Render(int entity,bool used,bool detail=false,bool enemy=false,float now=0,bool animate=false)
    {var effect=Prepare(entity,used,detail,enemy,now,animate);return effect.RenderTogether(new[]{effect});}
    public void ApplyActorState(int entity,int actorState,bool detail=false,bool enemy=false)
    {
        var view=Get(entity,detail,enemy);if(view.highlight==null)return;
        Material highlight;bool visible=!detail&&view.highlights.TryGetValue(actorState,out highlight);
        view.highlight.transform.parent.gameObject.SetActive(visible);view.highlight.enabled=visible;
        if(visible){view.highlight.sharedMaterial=view.highlights[actorState];view.highlight.transform.parent.localPosition=view.entry.highlight.states.Single(s=>s.state==actorState).offset;}
    }
    public Rect TextRect(Rect area,int entity,string name,bool detail=false,bool enemy=false)
    {
        var view=Get(entity,detail,enemy);var text=view.entry.texts.Single(t=>t.name==name);var transform=view.effect.Nodes[text.node].transform;
        Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        foreach(float x in new[]{-.5f,.5f})foreach(float y in new[]{-.5f,.5f})
        {
            var point=view.effect.PreviewPoint(transform.TransformPoint(new Vector3(x*text.width,y*text.height,0)));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        return new Rect(area.x+area.width*min.x,area.y+area.height*min.y,area.width*(max.x-min.x),area.height*(max.y-min.y));
    }
    public void PrepareText(EntityView entity,CardDef card,bool detail=false,bool enemy=false)
    {
        var view=Get(entity.entity,detail,enemy);
        // Card_Play_HeroPower 是共用Actor；原画由当前CardDef绑定，翻面曲线与字体保持原来源。
        if(view.portraitCard!=card.CardId)
        {
            if(!SupportsCard(card.CardId))throw new NotSupportedException("Unsupported standard hero power: "+card.CardId);
            string portraitPath=assets.PathFor("CARD_"+card.CardId);
            if(String.IsNullOrEmpty(portraitPath))throw new FileNotFoundException("Hero power portrait missing: "+card.CardId);
            view.effect.Nodes.Single(n=>n.name=="Mesh").GetComponent<Renderer>().sharedMaterials[1]
                .SetTexture("_MainTex",assets.FireblastTextureFile(Path.GetFileName(portraitPath)));
            view.portraitCard=card.CardId;
        }
        foreach(var source in view.entry.texts)
        {
            NativePowerText text;
            if(!view.texts.TryGetValue(source.name,out text))
            {
                var font=source.name=="PowersUberText"?BodyFont:NameFont;
                if(font==null)throw new InvalidOperationException("Original zhCN hero power font bundle missing");
                var definition=catalog.fonts.Single(f=>f.file==(source.name=="PowersUberText"?"BlizzardGlobal.ttf":"GLEI00M_t_Outline.ttf"));
                text=new NativePowerText(view.effect,view.effect.Nodes[source.node],source.raw,font,definition,source.name=="PowersUberText");view.texts.Add(source.name,text);
            }
            int cost=UIText.PowerCost(entity);
            text.Set(source.name=="CostUberText"?cost.ToString(CultureInfo.InvariantCulture):source.name=="NameUberText"?UIText.PowerName(card):UIText.PowerDescription(card,entity));
            if(source.name=="CostUberText")text.SetColor(cost<card.Cost?Color.green:cost>card.Cost?Color.red:Color.white);
        }
        foreach(var text in view.texts.Values)text.RefreshTexture();
    }
    public void Draw(Rect area,EntityView entity,CardDef card,bool detail,bool enemy,Color tint,float now,bool animate,int actorState=1)
    {
        if(Event.current.type!=EventType.Repaint)return;
        direct.Trace("before-prepare");
        var effect=Prepare(entity.entity,entity.used,detail,enemy,now,animate);
        PrepareText(entity,card,detail,enemy);effect.SetVisualClock(now);
        foreach(var renderer in effect.Root.GetComponentsInChildren<Renderer>(true))NativeActorMaterial.BindAmbient(renderer,assets);
        direct.Trace("after-text");
        var view=Get(entity.entity,detail,enemy);
        ApplyActorState(entity.entity,actorState,detail,enemy);
        direct.Draw(effect,area,GUI.matrix,Screen.width,Screen.height);
    }
    public void ClearViews(){foreach(var view in views.Values)view.Dispose();views.Clear();direct.Clear();}
    public void Dispose(){ClearViews();direct.Dispose();compositor.Dispose();}
}
