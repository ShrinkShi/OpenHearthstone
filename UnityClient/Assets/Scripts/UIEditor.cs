using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// 运行时校对工具：直接调用游戏的渲染函数，不维护另一套卡面。
public sealed class UIEditor
{
    readonly UICalibration calibration;
    readonly Action<string,string,int,int> preview;
    readonly Action<bool,Rect> setViewport;
    readonly Action<string,string,string> battlePreview;
    readonly CardDef[] cards;
    readonly string[] views={"card.minion","card.spell","card.weapon","weapon","minion","hero","power","power.detail","scene","hand","mulligan","quests","card_back","mana","attack","health","armor","durability","battle.speech","battle.emotes"};
    readonly string[] names={"随从牌","法术牌","武器牌","装备武器","战场随从","英雄","英雄技能","技能详情卡","战场布局 / 棋盘","手牌布局","起手换牌","任务页","卡背","法力水晶","攻击宝石","生命宝石","护甲图标","武器耐久图标","英雄对话泡","右键七种表情"};
    static readonly string[] BaseHeroIds={"HERO_01","HERO_02","HERO_03","HERO_04","HERO_05","HERO_06","HERO_07","HERO_08","HERO_09","HERO_10","HERO_11"};
    static readonly string[] SpeechCues={"START","THANKS","WELL_PLAYED","GREETINGS","WOW","OOPS","THREATEN","SORRY","CONCEDE"};
    static readonly string[] SpeechCueLabels={"开场","感谢","称赞","问候","惊叹","失误","威胁","抱歉","投降"};
    public bool Active;
    public string View{get;private set;}="card.minion";
    public string Selected{get;private set;}="card.minion";
    public string Message="拖动移动 · E 旋转 · R 缩放 · 方向键微调 · Ctrl+S 保存";
    public Rect Canvas{get{return new Rect(308,116,1220,814);}}
    public bool SceneView{get{return View=="scene"||View=="hand"||View=="mulligan"||View=="battle.speech"||View=="battle.emotes";}}
    public Rect Viewport
    {
        get
        {
            float scale=Mathf.Min(Canvas.width/1920,Canvas.height/1080)*zoom;
            var origin=Canvas.center+pan-new Vector2(960,540)*scale;return new Rect(origin.x,origin.y,1920*scale,1080*scale);
        }
    }
    string cardId="CS2_182",search="",mode="move",transferPath,referencePath;
    string[] fields=new string[5];
    int count=4,fieldCount=3;
    float zoom=1,referenceAlpha=.4f;
    bool snap,showBounds=true,referenceVisible,advanced;
    bool textInspector;
    UITextStyle textEdit=new UITextStyle();
    string textSize="0",strokeWidth="1",shadowX="2",shadowY="2";
    string speechCue="START";
    string colorPicker="";
    Vector2 viewScroll,listScroll,cardScroll,pan,dragStart,panStart;
    public static Rect CategoriesRect{get{return new Rect(12,122,278,418);}}
    public static Rect ElementsHeading{get{return new Rect(18,551,264,34);}}
    public static Rect ElementsRect{get{return new Rect(12,593,278,415);}}
    UIEditTarget dragTarget;
    UIAdjustment dragValue;
    string gesture="";
    Texture2D reference;
    GUIStyle label,small,button,heading,input,toggle;
    public UIEditor(UICalibration calibration,CardDef[] cards,Action<string,string,int,int> preview,Action<bool,Rect> setViewport,Action<string,string,string> battlePreview=null)
    {
        this.calibration=calibration;this.cards=cards;this.preview=preview;this.setViewport=setViewport;this.battlePreview=battlePreview;
        transferPath=Path.Combine(Path.GetDirectoryName(calibration.PathName),"ui-calibration-export.json");
        referencePath=Path.Combine(Path.GetDirectoryName(calibration.PathName),"ui-reference.png");SyncFields();
    }
    public void SelectView(string view)
    {
        if(!views.Contains(view))return;View=view;Selected=view=="scene"?"scene.board":view=="hand"?"battle.hand.own":view;zoom=1;pan=Vector2.zero;listScroll=Vector2.zero;
        cardId=view=="card.spell"?"CS2_029":view=="card.weapon"||view=="weapon"?"EX1_567":view=="hero"||view=="battle.speech"||view=="battle.emotes"?"HERO_08":view=="power"||view=="power.detail"?"HERO_08bp":"CS2_182";SyncFields();
    }
    public void Select(string id){Selected=id;SyncFields();}
    public void SetCounts(int hand,int field){count=Mathf.Clamp(hand,1,10);fieldCount=Mathf.Clamp(field,1,7);}
    void SyncFields()
    {
        var e=calibration.Get(Selected);fields=new[]{e.x,e.y,e.angle,e.scaleX,e.scaleY}.Select(v=>v.ToString("0.###",CultureInfo.InvariantCulture)).ToArray();
        textEdit=e.text==null?new UITextStyle():e.text.Copy();textSize=textEdit.size.ToString(CultureInfo.InvariantCulture);colorPicker="";
        strokeWidth=textEdit.outlineWidth.ToString("0.##",CultureInfo.InvariantCulture);shadowX=textEdit.shadowX.ToString("0.##",CultureInfo.InvariantCulture);shadowY=textEdit.shadowY.ToString("0.##",CultureInfo.InvariantCulture);
    }
    void Edit(Action<UIAdjustment> change,bool record=true)
    {
        try{var e=calibration.Get(Selected);change(e);calibration.Set(e,record);SyncFields();Message="预览已更新；保存后新启动的客户端会使用此配置。";}
        catch(Exception e){Message=e.Message;}
    }
    bool ApplyFields()
    {
        var values=new float[5];for(int i=0;i<5;i++)if(!float.TryParse(fields[i],NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])){Message="请输入有效数字（小数点使用 .）。";return false;}
        var value=calibration.Get(Selected);if(value.x==values[0]&&value.y==values[1]&&value.angle==values[2]&&value.scaleX==values[3]&&value.scaleY==values[4])return true;
        value.x=values[0];value.y=values[1];value.angle=values[2];value.scaleX=values[3];value.scaleY=values[4];
        try{calibration.Set(value);SyncFields();Message="预览已更新。";return true;}catch(Exception e){Message=e.Message;return false;}
    }
    public void Save()
    {
        if(textInspector&&!ApplyText())return;
        if(!ApplyFields())return;
        string error;Message=calibration.Save(out error)?"已保存 · "+calibration.PathName:"保存失败 · "+error;
    }
    public void Reload()
    {
        string error;calibration.Checkpoint();Message=calibration.Load(out error)?"已重新读取本地校对文件。":"读取失败 · "+error;SyncFields();
    }
    static void Box(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    static void Line(Vector2 a,Vector2 b,Color color,float width=1)
    {
        var m=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);Box(new Rect(a.x,a.y-width/2,Vector2.Distance(a,b),width),color);GUI.matrix=m;
    }
    static void Outline(UIEditTarget t,Color c,float width=1)
    {
        var old=GUI.matrix;GUI.matrix=t.matrix;var r=t.rect;
        Box(new Rect(r.xMin,r.yMin,r.width,width),c);Box(new Rect(r.xMin,r.yMax-width,r.width,width),c);
        Box(new Rect(r.xMin,r.yMin,width,r.height),c);Box(new Rect(r.xMax-width,r.yMin,width,r.height),c);GUI.matrix=old;
    }
    bool Button(Rect r,string text){return GUI.Button(r,text,button);}
    public void Draw(Font font)
    {
        // 先处理编辑器快捷键，避免 TextField 吞掉保存或应用按键。
        var key=Event.current;
        if(key.type==EventType.KeyDown)
        {
            if(key.control&&key.keyCode==KeyCode.S){Save();key.Use();}
            else if(key.control&&key.keyCode==KeyCode.Z){calibration.Undo();SyncFields();key.Use();}
            else if(key.control&&key.keyCode==KeyCode.Y){calibration.Redo();SyncFields();key.Use();}
            else if(key.keyCode==KeyCode.Return&&(GUI.GetNameOfFocusedControl()??"").StartsWith("ui-edit-",StringComparison.Ordinal)){if(textInspector)ApplyText();else ApplyFields();key.Use();}
        }
        if(label==null)
        {
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=19,alignment=TextAnchor.MiddleLeft,wordWrap=true};
            small=new GUIStyle(label){fontSize=16};heading=new GUIStyle(label){fontSize=26,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=18};
            input=new GUIStyle(GUI.skin.textField){font=font,fontSize=18};toggle=new GUIStyle(GUI.skin.toggle){font=font,fontSize=17};
        }
        var design=DesignViewport.Fit(1920,1080);GUI.matrix=design;
        bool scene=SceneView;
        if(!scene)Box(Canvas,new Color(.105f,.12f,.145f));
        if(showBounds&&!scene)
        {
            for(float x=Canvas.x;x<Canvas.xMax;x+=40)Box(new Rect(x,Canvas.y,1,Canvas.height),new Color(.7f,.8f,1,.045f));
            for(float y=Canvas.y;y<Canvas.yMax;y+=40)Box(new Rect(Canvas.x,y,Canvas.width,1),new Color(.7f,.8f,1,.045f));
        }
        float scale=Mathf.Min(Canvas.width/1920,Canvas.height/1080)*zoom;
        var origin=Canvas.center+pan-new Vector2(960,540)*scale;
        setViewport(scene,new Rect(origin.x,origin.y,1920*scale,1080*scale));
        GUI.matrix=design*Matrix4x4.TRS(origin,Quaternion.identity,new Vector3(scale,scale,1));
        calibration.Collecting=true;calibration.BeginFrame();
        preview(View,cardId,count,fieldCount);
        battlePreview?.Invoke(View,cardId,speechCue);
        calibration.Collecting=false;GUI.matrix=design;
        if(referenceVisible&&reference!=null)
        {var old=GUI.color;GUI.color=new Color(1,1,1,referenceAlpha);GUI.DrawTexture(Canvas,reference,ScaleMode.ScaleToFit,true);GUI.color=old;}
        var selected=calibration.Targets.FirstOrDefault(t=>t.id==Selected);
        GUI.matrix=Matrix4x4.identity;
        if(showBounds)foreach(var t in calibration.Targets.Where(t=>!t.id.Contains("/mesh/")&&(advanced||!t.id.StartsWith("scene.node/",StringComparison.Ordinal))))Outline(t,new Color(.35f,.65f,1,.2f));
        if(selected!=null)
        {
            Outline(selected,new Color(1,.77f,.2f),2);
            Vector2 center=selected.Center,corner=selected.matrix.MultiplyPoint3x4(new Vector2(selected.rect.xMax,selected.rect.yMax));
            Vector2 rotate=selected.matrix.MultiplyPoint3x4(new Vector2(selected.rect.center.x,selected.rect.yMin));rotate+=(rotate-center).normalized*30;
            Line(center,rotate,new Color(1,.77f,.2f));
            Box(new Rect(center.x-6,center.y-6,12,12),new Color(1,.77f,.2f));Box(new Rect(corner.x-6,corner.y-6,12,12),new Color(.2f,.85f,1));
            Box(new Rect(rotate.x-7,rotate.y-7,14,14),new Color(1,.4f,.3f));
        }
        GUI.matrix=design;
        // 面板在预览之后绘制，遮住缩放溢出的卡牌。
        Box(new Rect(0,0,1920,106),new Color(.06f,.07f,.09f));Box(new Rect(0,106,298,974),new Color(.075f,.085f,.105f));
        Box(new Rect(1540,106,380,974),new Color(.075f,.085f,.105f));Box(new Rect(298,940,1242,140),new Color(.06f,.07f,.09f));
        GUI.Label(new Rect(22,18,690,48),"UI 校对编辑器  ·  "+(calibration.Dirty?"有未保存修改":"已保存"),heading);
        if(Button(new Rect(725,25,100,43),"移动 V"))mode="move";
        if(Button(new Rect(831,25,100,43),"旋转 E"))mode="rotate";
        if(Button(new Rect(937,25,100,43),"缩放 R"))mode="scale";
        GUI.enabled=calibration.CanUndo;if(Button(new Rect(1052,25,90,43),"撤销")){calibration.Undo();SyncFields();}
        GUI.enabled=calibration.CanRedo;if(Button(new Rect(1148,25,90,43),"重做")){calibration.Redo();SyncFields();}GUI.enabled=true;
        if(Button(new Rect(1260,25,105,43),"保存"))Save();if(Button(new Rect(1373,25,115,43),"重新读取"))Reload();
        if(Button(new Rect(1720,25,176,43),"返回游戏 F6"))Active=false;
        GUI.Label(new Rect(319,74,1180,29),"模式："+mode+"   橙色中心：移动   红柄：旋转   蓝柄：缩放   Shift：精细移动 / 等比缩放",small);
        viewScroll=GUI.BeginScrollView(CategoriesRect,viewScroll,new Rect(0,0,254,views.Length*36));
        for(int i=0;i<views.Length;i++)
        {
            var old=GUI.color;if(View==views[i])GUI.color=new Color(1,.8f,.35f);
            if(Button(new Rect(0,i*36,252,33),names[i]))SelectView(views[i]);GUI.color=old;
        }
        GUI.EndScrollView();
        GUI.Label(ElementsHeading,"元素 / 模型部件",heading);
        var targets=calibration.Targets.Where(t=>View=="scene"?true:View=="hand"?t.id.StartsWith("battle.hand.",StringComparison.Ordinal):View=="mulligan"?t.id=="mulligan"||t.id.StartsWith("mulligan/",StringComparison.Ordinal):t.id==View||t.id.StartsWith(View+"/",StringComparison.Ordinal))
            .Where(t=>advanced||(!t.id.Contains("/mesh/")&&!t.id.StartsWith("scene.node/",StringComparison.Ordinal)))
            .GroupBy(t=>t.id).Select(g=>g.First()).OrderBy(t=>t.id.StartsWith("scene.node/",StringComparison.Ordinal)?1:0).ToArray();
        listScroll=GUI.BeginScrollView(ElementsRect,listScroll,new Rect(0,0,254,targets.Length*32));
        for(int i=0;i<targets.Length;i++)
        {
            var old=GUI.color;if(targets[i].id==Selected)GUI.color=new Color(1,.8f,.35f);
            if(Button(new Rect(0,i*32,252,30),targets[i].label))Select(targets[i].id);GUI.color=old;
        }
        GUI.EndScrollView();
        advanced=GUI.Toggle(new Rect(18,1022,264,35),advanced,"显示模型内部部件",toggle);
        Inspector();
        GUI.Label(new Rect(326,949,1200,45),Message,small);
        GUI.Label(new Rect(326,997,1200,62),"模板修改作用于同类所有卡牌；战场位置作用于双方指定区域。\n当前保存文件："+calibration.PathName,small);
        HandleInput(selected,design);
    }
    void Inspector()
    {
        bool before=textInspector;textInspector=GUI.Toolbar(new Rect(1558,78,338,34),textInspector?1:0,new[]{"布局 / 背景","字体 / 颜色"},button)==1;
        if(before!=textInspector)SyncFields();
        if(textInspector){InspectorText();return;}
        GUI.Label(new Rect(1558,119,344,30),"属性 · "+Selected.Split('/').Last(),heading);
        GUI.Label(new Rect(1558,151,344,54),Selected,small);
        string[] names={"X 偏移","Y 偏移","旋转 °","X 缩放","Y 缩放"};
        for(int i=0;i<5;i++)
        {
            GUI.Label(new Rect(1558,213+i*40,120,34),names[i],label);GUI.SetNextControlName("ui-edit-"+i);
            fields[i]=GUI.TextField(new Rect(1680,213+i*40,218,34),fields[i],24,input);
        }
        if(Button(new Rect(1558,420,164,38),"应用数值 Enter"))ApplyFields();
        if(Button(new Rect(1730,420,166,38),"重置所选")){calibration.Reset(Selected);SyncFields();}
        if(Button(new Rect(1558,467,164,38),"水平翻转"+(calibration.Get(Selected).flipX?" ✓":"")))Edit(e=>e.flipX=!e.flipX);
        if(Button(new Rect(1730,467,166,38),"垂直翻转"+(calibration.Get(Selected).flipY?" ✓":"")))Edit(e=>e.flipY=!e.flipY);
        if(Button(new Rect(1558,514,164,38),"旋转 +90°"))Edit(e=>e.angle+=90);
        if(Button(new Rect(1730,514,166,38),"旋转 180°"))Edit(e=>e.angle+=180);
        if(Button(new Rect(1558,562,338,38),"重置当前页及子元素")){calibration.ResetPage(View);SyncFields();}
        snap=GUI.Toggle(new Rect(1558,611,164,32),snap,"5 单位吸附",toggle);showBounds=GUI.Toggle(new Rect(1730,611,166,32),showBounds,"显示辅助线",toggle);
        GUI.Label(new Rect(1558,651,88,30),"缩放预览",small);zoom=GUI.HorizontalSlider(new Rect(1655,661,170,24),zoom,.4f,2);
        if(Button(new Rect(1834,651,62,30),"复位")){zoom=1;pan=Vector2.zero;}
        if(View=="hand"||View=="scene")
        {
            if(View=="scene"&&Button(new Rect(1558,753,338,38),"调整整个战斗背景缩放"))Select("scene.background");
            GUI.Label(new Rect(1558,687,310,27),"手牌 "+count+" 张 · 随从 "+fieldCount+" 个",small);
            count=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(1558,720,154,24),count,1,10));
            fieldCount=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(1732,720,164,24),fieldCount,1,7));
        }
        else if(View=="quests")
        {
            GUI.Label(new Rect(1558,687,338,70),"任务页预览使用本地固定的 4 个日常与 4 个周常任务。\n预览中的重抽按钮不会请求账号服务。",small);
        }
        else
        {
            GUI.Label(new Rect(1558,687,300,27),"预览卡牌 · 名称或 CardId 搜索",small);GUI.SetNextControlName("ui-edit-search");search=GUI.TextField(new Rect(1558,720,338,29),search,50,input);
            int type=View=="card.spell"?5:View=="card.weapon"||View=="weapon"?7:View=="hero"||View=="battle.speech"||View=="battle.emotes"?3:View=="power"||View=="power.detail"?10:4;
            bool baseHeroPreview=View=="battle.speech"||View=="battle.emotes";
            var pool=cards.Where(c=>c.Type==type&&(!baseHeroPreview||BaseHeroIds.Contains(c.CardId))&&(search==""?c.Collectible||c.CardId==cardId:c.Name.Contains(search)||c.CardId.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)).Take(180).ToArray();
            float cardListHeight=View=="battle.speech"?52:84;
            cardScroll=GUI.BeginScrollView(new Rect(1558,754,340,cardListHeight),cardScroll,new Rect(0,0,317,pool.Length*27));
            for(int i=0;i<pool.Length;i++)if(Button(new Rect(0,i*27,315,25),pool[i].Name+" · "+pool[i].CardId))cardId=pool[i].CardId;GUI.EndScrollView();
            if(View=="battle.speech")
            {
                GUI.Label(new Rect(1558,811,338,25),"预览语义 · 使用所选英雄的原文",small);
                int selected=Array.IndexOf(SpeechCues,speechCue);selected=Mathf.Max(0,selected);
                selected=GUI.SelectionGrid(new Rect(1558,836,338,72),selected,SpeechCueLabels,3,button);speechCue=SpeechCues[selected];
                GUI.Label(new Rect(1558,916,338,25),"导入 / 导出布局 JSON",small);GUI.SetNextControlName("ui-edit-path");transferPath=GUI.TextField(new Rect(1558,942,338,28),transferPath,input);
                if(Button(new Rect(1558,974,164,31),"导入")){string error;Message=calibration.Import(transferPath,out error)?"已导入；尚未保存。":error;SyncFields();}
                if(Button(new Rect(1730,974,166,31),"导出")){string error;Message=calibration.Export(transferPath,out error)?"已导出 "+transferPath:error;}
                GUI.SetNextControlName("ui-edit-reference");referencePath=GUI.TextField(new Rect(1558,1011,338,27),referencePath,input);
                if(Button(new Rect(1558,1042,164,29),"参考图叠加"))LoadReference();
                referenceVisible=GUI.Toggle(new Rect(1730,1042,164,28),referenceVisible,"显示参考图",toggle);return;
            }
        }
        GUI.Label(new Rect(1558,849,338,30),"导入 / 导出布局 JSON",small);GUI.SetNextControlName("ui-edit-path");transferPath=GUI.TextField(new Rect(1558,882,338,33),transferPath,input);
        if(Button(new Rect(1558,924,164,35),"导入")){string error;Message=calibration.Import(transferPath,out error)?"已导入；尚未保存。":error;SyncFields();}
        if(Button(new Rect(1730,924,166,35),"导出")){string error;Message=calibration.Export(transferPath,out error)?"已导出 "+transferPath:error;}
        GUI.SetNextControlName("ui-edit-reference");referencePath=GUI.TextField(new Rect(1558,971,338,29),referencePath,input);
        if(Button(new Rect(1558,1009,164,33),"参考图叠加"))LoadReference();
        referenceVisible=GUI.Toggle(new Rect(1730,1009,164,28),referenceVisible,"显示参考图",toggle);referenceAlpha=GUI.HorizontalSlider(new Rect(1558,1052,338,20),referenceAlpha,0,1);
    }
    void InspectorText()
    {
        GUI.Label(new Rect(1558,119,338,72),"字体 · "+Selected,small);
        textEdit.fillOverride=GUI.Toggle(new Rect(1558,213,338,32),textEdit.fillOverride,"自定义填充颜色（否则跟随游戏状态）",toggle);
        textEdit.fill=ColorField(252,"填充",textEdit.fill);
        textEdit.outline=GUI.Toggle(new Rect(1558,300,180,32),textEdit.outline,"颜色描边",toggle);
        GUI.SetNextControlName("ui-edit-stroke-width");strokeWidth=GUI.TextField(new Rect(1730,300,166,32),strokeWidth,input);
        textEdit.stroke=ColorField(346,"描边",textEdit.stroke);
        GUI.Label(new Rect(1558,395,174,32),"字号 · 0 为自动",label);GUI.SetNextControlName("ui-edit-font-size");textSize=GUI.TextField(new Rect(1735,395,161,32),textSize,input);
        textEdit.fontMode=GUI.Toolbar(new Rect(1558,443,338,34),textEdit.fontMode,new[]{"继承","普通","粗体","斜体","粗斜"},button);
        textEdit.shadow=GUI.Toggle(new Rect(1558,496,338,32),textEdit.shadow,"文字阴影",toggle);
        textEdit.shade=ColorField(538,"阴影",textEdit.shade);
        GUI.Label(new Rect(1558,586,96,32),"阴影 X/Y",label);GUI.SetNextControlName("ui-edit-shadow-x");shadowX=GUI.TextField(new Rect(1660,586,112,32),shadowX,input);
        GUI.SetNextControlName("ui-edit-shadow-y");shadowY=GUI.TextField(new Rect(1784,586,112,32),shadowY,input);
        if(Button(new Rect(1558,643,164,38),"应用字体样式"))ApplyText();
        if(Button(new Rect(1730,643,166,38),"清除字体样式"))Edit(e=>e.text=null);
        GUI.Label(new Rect(1558,700,338,95),"颜色格式 #RRGGBB / #RRGGBBAA。字号以模板单位保存，随卡牌大小缩放。\n选择整体可设置子元素的继承样式。",small);
        GUI.Label(new Rect(1558,820,338,110),"卡牌描述保留原始粗体与斜体。生命/耐久默认白、红、绿动态变化；自定义填充可覆盖它。\nCtrl+S 保存，另一实例 F7 读取。",small);
        if(colorPicker!="")DrawColorPicker();
    }
    string ColorField(float y,string labelText,string value)
    {
        GUI.Label(new Rect(1558,y,76,32),labelText,label);GUI.SetNextControlName("ui-edit-color-"+labelText);
        value=GUI.TextField(new Rect(1638,y,215,32),value,9,input);Color color;
        if(ColorUtility.TryParseHtmlString(value,out color))Box(new Rect(1866,y+2,28,28),color);
        if(GUI.Button(new Rect(1866,y+2,28,28),GUIContent.none,GUIStyle.none))colorPicker=colorPicker==labelText?"":labelText;
        return value;
    }
    void DrawColorPicker()
    {
        Box(new Rect(1554,750,346,283),new Color(.09f,.08f,.07f));
        string value=colorPicker=="填充"?textEdit.fill:colorPicker=="描边"?textEdit.stroke:textEdit.shade;Color color;
        if(!ColorUtility.TryParseHtmlString(value,out color))color=Color.white;
        GUI.Label(new Rect(1564,759,250,30),colorPicker+"颜色 · RGBA",label);
        var channels=new[]{color.r,color.g,color.b,color.a};string[] labels={"红","绿","蓝","透明"};
        for(int i=0;i<4;i++){GUI.Label(new Rect(1564,800+i*33,65,28),labels[i],small);channels[i]=GUI.HorizontalSlider(new Rect(1634,809+i*33,248,22),channels[i],0,1);}
        color=new Color(channels[0],channels[1],channels[2],channels[3]);
        var palette=new[]{Color.white,Color.black,Color.red,Color.green,Color.blue,Color.yellow,new Color(1,.62f,.12f),new Color(.12f,.07f,.035f)};
        for(int i=0;i<palette.Length;i++){var r=new Rect(1564+i*40,939,32,32);Box(r,palette[i]);if(GUI.Button(r,GUIContent.none,GUIStyle.none))color=palette[i];}
        value="#"+ColorUtility.ToHtmlStringRGBA(color);
        if(colorPicker=="填充"){textEdit.fill=value;textEdit.fillOverride=true;}else if(colorPicker=="描边")textEdit.stroke=value;else textEdit.shade=value;
        if(Button(new Rect(1564,986,318,32),"收起调色板"))colorPicker="";
    }
    bool ApplyText()
    {
        int size;float width,x,y;
        if(!int.TryParse(textSize,out size)||!float.TryParse(strokeWidth,NumberStyles.Float,CultureInfo.InvariantCulture,out width)
            ||!float.TryParse(shadowX,NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(shadowY,NumberStyles.Float,CultureInfo.InvariantCulture,out y))
        {Message="字号和描边/阴影偏移必须是有效数字。";return false;}
        var value=textEdit.Copy();value.size=size;value.outlineWidth=width;value.shadowX=x;value.shadowY=y;
        try{value.Validate();Edit(e=>e.text=value);return true;}catch(Exception e){Message=e.Message;return false;}
    }
    void LoadReference()
    {
        try
        {
            var bytes=File.ReadAllBytes(referencePath);if(bytes.Length>16000000)throw new IOException("参考图最大 16 MB。");
            var image=new Texture2D(2,2);if(!image.LoadImage(bytes)){UnityEngine.Object.Destroy(image);throw new IOException("图片格式无效。");}
            if(reference!=null)UnityEngine.Object.Destroy(reference);reference=image;referenceVisible=true;Message="参考图仅显示在校对编辑器，不会进入对战。";
        }
        catch(Exception e){Message=e.Message;}
    }
    void HandleInput(UIEditTarget selected,Matrix4x4 design)
    {
        var e=Event.current;var mouse=e.mousePosition;var screen=(Vector2)design.MultiplyPoint3x4(mouse);
        bool onCanvas=Canvas.Contains(mouse);
        if(e.type==EventType.MouseDown&&onCanvas&&(e.button==0||e.button==2))
        {
            GUI.FocusControl("");dragStart=screen;panStart=pan;
            if(e.button==2){gesture="pan";e.Use();return;}
            gesture=mode;
            if(selected!=null)
            {
                var corner=(Vector2)selected.matrix.MultiplyPoint3x4(new Vector2(selected.rect.xMax,selected.rect.yMax));
                var rotate=(Vector2)selected.matrix.MultiplyPoint3x4(new Vector2(selected.rect.center.x,selected.rect.yMin));rotate+=(rotate-selected.Center).normalized*30;
                if(Vector2.Distance(screen,corner)<16)gesture="scale";else if(Vector2.Distance(screen,rotate)<17)gesture="rotate";
                else if(!selected.Contains(screen)&&Vector2.Distance(screen,selected.Center)>18)selected=null;
            }
            if(selected==null){selected=calibration.Targets.LastOrDefault(t=>t.Contains(screen)&&!t.id.EndsWith("/art"));if(selected!=null)Select(selected.id);}
            if(selected!=null){dragTarget=selected;dragValue=calibration.Get(selected.id);calibration.Checkpoint();}else gesture="";
            e.Use();
        }
        if(e.type==EventType.MouseDrag&&gesture!="")
        {
            if(gesture=="pan"){pan=panStart+(Vector2)design.inverse.MultiplyVector(screen-dragStart);e.Use();return;}
            if(dragTarget==null)return;var value=dragValue.Copy();var localDelta=(Vector2)dragTarget.parent.inverse.MultiplyVector(screen-dragStart);
            if(gesture=="move")
            {
                value.x+=localDelta.x/Mathf.Max(.0001f,dragTarget.units.x);value.y+=localDelta.y/Mathf.Max(.0001f,dragTarget.units.y);
                if(snap&&!e.shift){value.x=Mathf.Round(value.x/5)*5;value.y=Mathf.Round(value.y/5)*5;}
            }
            else if(gesture=="rotate")
            {
                var center=(Vector2)dragTarget.parent.inverse.MultiplyPoint3x4(dragTarget.Center);
                var a=(Vector2)dragTarget.parent.inverse.MultiplyPoint3x4(dragStart)-center;var b=(Vector2)dragTarget.parent.inverse.MultiplyPoint3x4(screen)-center;
                value.angle+=Vector2.SignedAngle(a,b);if(snap)value.angle=Mathf.Round(value.angle/5)*5;
            }
            else if(gesture=="scale")
            {
                var a=(Vector2)dragTarget.matrix.inverse.MultiplyPoint3x4(dragStart)-dragTarget.rect.center;
                var b=(Vector2)dragTarget.matrix.inverse.MultiplyPoint3x4(screen)-dragTarget.rect.center;
                float sx=Mathf.Abs(a.x)>8?Mathf.Abs(b.x/a.x):1+localDelta.x/150,sy=Mathf.Abs(a.y)>8?Mathf.Abs(b.y/a.y):1-localDelta.y/150;
                if(e.shift)sx=sy=Mathf.Max(sx,sy);value.scaleX=Mathf.Clamp(value.scaleX*sx,.05f,10);value.scaleY=Mathf.Clamp(value.scaleY*sy,.05f,10);
            }
            calibration.Set(value,false);SyncFields();e.Use();
        }
        if(e.type==EventType.MouseUp&&gesture!=""){gesture="";dragTarget=null;e.Use();}
        if(e.type==EventType.ScrollWheel&&onCanvas){zoom=Mathf.Clamp(zoom-e.delta.y*.04f,.4f,2);e.Use();}
        if(e.type!=EventType.KeyDown)return;
        bool text=(GUI.GetNameOfFocusedControl()??"").StartsWith("ui-edit-",StringComparison.Ordinal);
        if(!text)
        {
            if(e.keyCode==KeyCode.V)mode="move";if(e.keyCode==KeyCode.E)mode="rotate";if(e.keyCode==KeyCode.R)mode="scale";
            float step=e.shift?10:1;
            if(e.keyCode==KeyCode.LeftArrow){Edit(v=>v.x-=step);e.Use();}
            if(e.keyCode==KeyCode.RightArrow){Edit(v=>v.x+=step);e.Use();}
            if(e.keyCode==KeyCode.UpArrow){Edit(v=>v.y-=step);e.Use();}
            if(e.keyCode==KeyCode.DownArrow){Edit(v=>v.y+=step);e.Use();}
        }
    }
    public void Dispose(){if(reference!=null)UnityEngine.Object.Destroy(reference);}
}
