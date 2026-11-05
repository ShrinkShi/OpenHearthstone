using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed partial class ClientApp : MonoBehaviour
{
    public event Action<string> ServerActionRejected;
    AssetRegistry assets;
    RuntimeAssetLoader assetLoader;
    CardVisuals cardVisuals;
    InteractionVisuals interactionVisuals;
    SpellParticles spellParticles;
    NativeEffectPlayer nativeEffects;
    NativeAudioPlayback nativeAudio;
    NativeHeroDamageAudio heroDamageAudio;
    NativeBattleWeapons nativeWeapons;
    NativeBattleSummons nativeSummons;
    NativeMinionSummonVoices nativeMinionSummonVoices;
    NativeShamanHeroPowerPlayback nativeShaman;
    NativeBattleActors nativeActors;
    readonly NativeShaderTime nativeShaderTime=new NativeShaderTime();
    TurnButtonVisuals turnButton;
    readonly MenuButtonVisuals menuButtons=new MenuButtonVisuals();
    readonly BattleHistory battleHistory=new BattleHistory();
    int historyOffset;
    bool showingDetail;
    readonly CardPickup cardPickup=new CardPickup();
    readonly HeroSelection heroSelection=new HeroSelection();
    readonly BattleLifecycle lifecycle=new BattleLifecycle();
    HeroSpeech heroSpeech;
    BattleOpening battleOpening;
    bool OpeningActive { get { return battleOpening!=null&&battleOpening.Active; } }
    AudioSource openingVoiceSource;
    readonly HeroEmoteMenu heroEmoteMenu=new HeroEmoteMenu();
    HeroSpeechReplay heroSpeechReplay;
    HeroFragments heroFragments;
    int placementIndex=-1;
    Vector2 placementPoint;
    UICalibration calibration;
    UIEditor uiEditor;
    StateView editorState;
    MenuPresentation menuPresentation;
    readonly Presentation presentation=new Presentation();
    readonly HandPresentation handPresentation=new HandPresentation();
    readonly MinionBoardLayout minionLayout=new MinionBoardLayout();
    readonly MulliganPresentation mulliganPresentation=new MulliganPresentation();
    readonly PerformanceProbe performance=new PerformanceProbe();
    readonly PresentationFrameClock frameClock=new PresentationFrameClock();float visualFrameDelta;
    BoardPresentation boardPresentation;
    AssetBundle fontBundle;
    Font numberFont,bodyFont;
    NativeTargetArrow targetArrow;
    NativePowerVisual powerVisual;
    internal Vector2? EvidencePointer;
    internal bool EvidencePressed;
    bool debugUI,battleMenu,battleOptions,resultAudioPlayed;
    bool ownDeckHover,enemyDeckHover,enemyHandHover;
    Rect ownDeckBounds,enemyDeckBounds;
    string visualTest="";
    int lastHandHover;
    CardDB database;
    AudioSource[] heroVoiceSources;
    readonly Dictionary<string,CardDef> cards = new Dictionary<string,CardDef>();
    Connection connection;
    StateView state, previous;
    Move[] playChoices;
    GameObject menuScene, boardScene;
    Camera cameraView;
    NativeHubScene nativeHubScene;
    NativeHubWidgets nativeHubWidgets;
    AudioSource music, sfx;
    AudioSource[] f031PageTurnSources;
    int f031PageTurnSourceIndex;
    readonly Dictionary<string,AudioClip> sounds = new Dictionary<string,AudioClip>();
    readonly Dictionary<string,VoiceEntry> voices=new Dictionary<string,VoiceEntry>();
    readonly HashSet<SemanticEvent> heroAttackVoiceEvents=new HashSet<SemanticEvent>();
    readonly Queue<SemanticEvent> heroAttackVoiceOrder=new Queue<SemanticEvent>();
    readonly Dictionary<int,List<string>> customDecks=new Dictionary<int,List<string>>();
    readonly Dictionary<int,Rect> locations = new Dictionary<int,Rect>();
    readonly Dictionary<int,Rect> armorLocations = new Dictionary<int,Rect>();
    readonly HashSet<int> mulligan = new HashSet<int>();
    readonly List<string> history = new List<string>();
    string page="menu", playerName="Shrink1", host="127.0.0.1", profile="player1", profilePath, dataRoot;
    string status="", search="", screenshotDir="", lastScreenshotPhase="", menuHovered="";
    int classIndex, port=37240, selected, dragged, actionCounter, hover;
    List<string> deck=new List<string>();
    GUIStyle titleStyle, labelStyle, smallStyle, numberStyle, damageSplatStyle, buttonStyle, cardNameStyle, cardTextStyle, menuButtonStyle;
    static readonly Vector2[] damageSplatOutlineOffsets={new Vector2(-3,0),new Vector2(3,0),new Vector2(0,-3),new Vector2(0,3)};
    Vector2 poolScroll, deckScroll;
    Font font;
    bool autoMatch, autoplay, pending, joining, initialized, connectionLost;
    bool heroEmotesSupported;
    bool heroReplayExitStarted;
    float nextAuto, animationStart;

    string Argument(string name, string fallback="")
    {
        var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,name);
        return index>=0 && index+1<args.Length ? args[index+1] : fallback;
    }
    bool Flag(string name) {return Environment.GetCommandLineArgs().Contains(name);}
    void Start()
    {
        string healthAudioProbe=Argument("--hero-health-audio-probe");
        if(!String.IsNullOrEmpty(healthAudioProbe))
        {
            enabled=false;
            string probeDataRoot=Argument("--data",Path.GetFullPath(Path.Combine(Application.dataPath,Application.isEditor?"../../local":"../../../local")));
            gameObject.AddComponent<HeroHealthAudioProbe>().Begin(healthAudioProbe,probeDataRoot);
            return;
        }
        Application.runInBackground=true; Application.targetFrameRate=60;
        profile=Regex.Replace(Argument("--profile","player1"),"[^A-Za-z0-9_-]","_");
        profilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HearthstoneClone","profiles",profile);
        Directory.CreateDirectory(profilePath);
        dataRoot=Argument("--data",Path.GetFullPath(Path.Combine(Application.dataPath,Application.isEditor?"../../local":"../../../local")));
        autoMatch=Flag("--auto-match"); autoplay=Flag("--autoplay");
        f024GuestDiagnosticReplay=F024HasGuestDiagnosticReplayFlag()||(autoMatch&&autoplay);
        autoMatch=autoMatch||f024GuestDiagnosticReplay;
        screenshotDir=Argument("--screenshot-dir");
        debugUI=Flag("--debug-ui")||Flag("--dev");visualTest=Argument("--visual-test");
        heroSpeechReplay=new HeroSpeechReplay(Argument("--hero-speech-replay"),
            Argument("--hero-attack-report",Argument("--hero-speech-report")),
            Flag("--hero-speech-replay-concede"),Flag("--hero-speech-replay-exit"),Flag("--hero-attack-replay"));
        int.TryParse(Argument("--port","37240"),out port);
        try
        {
            assets=new AssetRegistry(dataRoot);
            lifecycle.AudioCue=PlaySound;
            calibration=new UICalibration(Argument("--ui-layout",Path.Combine(dataRoot,"ui-calibration.json")));UICalibration.Current=calibration;
            string layoutError;if(!calibration.Load(out layoutError))Debug.LogWarning("UI calibration: "+layoutError);
            assetLoader=new RuntimeAssetLoader(dataRoot);assets.MissingPortrait=assetLoader.Request;
            cardVisuals=new CardVisuals(assets);
            interactionVisuals=new InteractionVisuals();
            spellParticles=new SpellParticles();
            nativeEffects=new NativeEffectPlayer(assets);presentation.NativeDraw=nativeEffects.Draw;presentation.NativeButtonDraw=nativeEffects.DrawHeroPowerButton;presentation.NativeDuration=nativeEffects.Duration;presentation.NativeImpactDelay=nativeEffects.ImpactDelay;presentation.DamageSplatDraw=DrawDamageSplat;
            nativeEffects.EntityPresentationPresent=entity=>presentation.Display!=null&&!presentation.Dying(entity)&&
                presentation.Display.players!=null&&presentation.Display.players.Any(p=>p!=null&&p.field!=null&&p.field.Any(e=>e!=null&&e.entity==entity));
            presentation.ArmorBadgeBounds=id=>{Rect bounds;return armorLocations.TryGetValue(id,out bounds)?(Rect?)bounds:null;};
            assets.BattleAmbient=()=>page=="battle"&&state!=null&&state.phase!="mulligan"?(Color?)nativeEffects.BoardAmbientColor:null;
            if(File.Exists(Path.Combine(assets.Root,"fireblast-ui.json")))powerVisual=new NativePowerVisual(assets);
            turnButton=new TurnButtonVisuals(assets);
            database=JsonUtility.FromJson<CardDB>(File.ReadAllText(Path.Combine(dataRoot,"card-database.json")));
            foreach(var c in database.cards) cards[c.CardId]=c;
            nativeEffects.CardType=id=>!String.IsNullOrEmpty(id)&&cards.ContainsKey(id)?cards[id].Type:0;
            nativeActors=new NativeBattleActors(cardVisuals,id=>cards.ContainsKey(id)?cards[id]:null);
            nativeEffects.ActorObject=nativeActors.Resolve;presentation.NativePrepare=nativeEffects.Prepare;
            var fireblastCamera=JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json"))).cameras.Single(c=>c.name=="Main Camera");
            nativeEffects.SuperSpellScene=(data,before,after,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(data,before,after,
                id=>NativeSuperSpellBoard.ResolvePresentationPosition(data.card_id,data.source,id,
                    entity=>presentation.NativePosition(entity,locations)),viewport,origin,nativeEffects.CardType,
                data.card_id=="HERO_08bp"||data.card_id=="HERO_04bp"||data.card_id=="HERO_07bp"?fireblastCamera:null,
                entity=>{Rect bounds;return locations.TryGetValue(entity,out bounds)?(Rect?)bounds:null;});
            presentation.NativeActive=nativeEffects.SourceBusy;
            presentation.NativeTailActive=nativeEffects.SourceVisualTailActive;
            presentation.NativeImpactTime=nativeEffects.SourceImpactTime;
            nativeAudio=new NativeAudioPlayback(assets.Root,transform){DeltaTime=()=>visualFrameDelta*Time.timeScale};
            heroDamageAudio=new NativeHeroDamageAudio(assets,cue=>nativeEffects.AudioCue?.Invoke(cue));
            nativeWeapons=new NativeBattleWeapons(assets);nativeWeapons.AudioCue=cue=>{nativeAudio.Play(cue);heroSpeechReplay?.ObserveWeaponCue(cue);};
            nativeMinionSummonVoices=new NativeMinionSummonVoices();
            NativeCameraShakeDriver.UseFrameDelta(cameraView,()=>visualFrameDelta*Time.timeScale);
            if(powerVisual!=null)powerVisual.AudioCue=nativeAudio.Play;
            nativeEffects.PowerSelectionCancelled=()=>powerVisual?.PlayButtonSound("cancel");
            nativeEffects.AudioCue=cue=>{try{nativeAudio.Play(cue);}catch(Exception error){Debug.LogWarning("Native audio: "+error.Message);}};
            nativeSummons=new NativeBattleSummons(assets,nativeActors);nativeSummons.AudioCue=nativeEffects.AudioCue;
            nativeShaman=new NativeShamanHeroPowerPlayback(assets,nativeActors);nativeShaman.AudioCue=nativeEffects.AudioCue;
            presentation.NativeSummonPrepare=nativeSummons.Prepare;presentation.NativeSummonActive=nativeSummons.Has;presentation.NativeSummonDuration=nativeSummons.Duration;
            presentation.NativeShamanSummonPrepare=nativeShaman.Prepare;presentation.NativeShamanDeathPrepare=nativeShaman.PrepareDeath;
            presentation.NativeShamanSummonDraw=(key,data,before,after,rect,age)=>nativeShaman.Draw(key,rect,age,nativeEffects.RenderAffine);
            presentation.NativeShamanSummonActive=nativeShaman.Has;
            presentation.NativeSummonSuppressed=(data,before,after)=>
            {
                NativeDeathKnightHeroPowerPlayback.Pair pair;
                return NativeDeathKnightHeroPowerPlayback.TryGetPairForSummon(data,before,after,out pair)&&nativeEffects.HasDeathKnightRouteFor(pair);
            };
            nativeSummons.SkipSpell=(data,before,after)=>
            {
                if(nativeShaman.Enabled&&NativeShamanHeroPowerPlayback.IsShamanSummon(data,null,after))return true;
                NativeDeathKnightHeroPowerPlayback.Pair pair;
                return NativeDeathKnightHeroPowerPlayback.TryGetPairForSummon(data,before,after,out pair)&&nativeEffects.HasDeathKnightRouteFor(pair);
            };
            calibration.Changed=id=>{cardVisuals.Invalidate(id);interactionVisuals.Clear();nativeActors.Invalidate();};
            uiEditor=new UIEditor(calibration,database.cards,EditorPreview,EditorViewport,DrawBattleCalibrationPreview);
            if(layoutError!="")uiEditor.Message="读取校对文件失败，当前沿用默认布局："+layoutError;
            classIndex=Array.FindIndex(database.classes,c=>c.id==4);
            var saved=Path.Combine(profilePath,"profile.json");
            if(File.Exists(saved))
            {
                var p=JsonUtility.FromJson<Profile>(File.ReadAllText(saved)); playerName=p.name; host=p.host;
                int ix=Array.FindIndex(database.classes,c=>c.id==p.class_id); if(ix>=0) classIndex=ix;
                if(p.deck!=null) deck=p.deck.ToList();
                if(p.decks!=null)foreach(var savedDeck in p.decks)
                    if(savedDeck.cards!=null&&HoldsClass(savedDeck.class_id))customDecks[savedDeck.class_id]=savedDeck.cards.ToList();
                if(p.deck!=null&&!customDecks.ContainsKey(p.class_id))customDecks[p.class_id]=p.deck.ToList();
            }
            playerName=Argument("--name",playerName); host=Argument("--host",host);
            var classArg=Argument("--class"); if(classArg!="") classIndex=Array.FindIndex(database.classes,c=>c.id==int.Parse(classArg));
            if(classIndex<0) classIndex=0;
            int selectedClass=database.classes[classIndex].id;
            if(deck.Count!=30 || classArg!="")deck=customDecks.ContainsKey(selectedClass)?customDecks[selectedClass].ToList():database.classes[classIndex].deck.ToList();
            font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",22);
            string fonts=Path.Combine(dataRoot,"runtime/local-fonts.assetbundle");
            if(File.Exists(fonts))
            {
                fontBundle=AssetBundle.LoadFromFile(fonts);
                font=fontBundle.LoadAsset<Font>("UGCSH00M");numberFont=fontBundle.LoadAsset<Font>("BelweNumbers");bodyFont=fontBundle.LoadAsset<Font>("gkai00m");
                if(powerVisual!=null){powerVisual.NameFont=fontBundle.LoadAsset<Font>("GLEI00M_t_Outline");powerVisual.BodyFont=fontBundle.LoadAsset<Font>("BlizzardGlobal");}
            }
            cameraView=new GameObject("PresentationCamera").AddComponent<Camera>();
            cameraView.cullingMask&=~((1<<30)|(1<<31)); // 离屏素材与Hub模型只由各自相机采样。
            nativeHubScene=new NativeHubScene(assets);
            nativeHubWidgets=new NativeHubWidgets(assets);
            nativeEffects.TimelineCamera=cameraView;nativeSummons.TimelineCamera=cameraView;nativeShaman.TimelineCamera=cameraView;
            var clear=new GameObject("FiniteBackgroundClear").AddComponent<Camera>();
            clear.depth=-100;clear.cullingMask=0;clear.clearFlags=CameraClearFlags.SolidColor;clear.backgroundColor=Color.black;
            cameraView.gameObject.AddComponent<AudioListener>();
            cameraView.clearFlags=CameraClearFlags.SolidColor; cameraView.backgroundColor=new Color(.035f,.022f,.014f);
            cameraView.transform.position=new Vector3(0,22,-4); cameraView.transform.LookAt(Vector3.zero);
            cameraView.fieldOfView=49; cameraView.nearClipPlane=.1f; cameraView.farClipPlane=100;
            menuScene=assets.Scene("SCENE_MAIN_MENU");
            menuPresentation=new MenuPresentation(menuScene,cameraView,JsonUtility.FromJson<SceneData>(File.ReadAllText(assets.PathFor("SCENE_MAIN_MENU"))));
            menuPresentation.Show();
            boardScene=assets.Scene("SCENE_BOARD"); AssetRegistry.Fit(boardScene,28); boardScene.SetActive(false);
            var table=assets.Scene("MODEL_BOARD_TABLE");AssetRegistry.Fit(table,48);
            float boardFloor=boardScene.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.y);
            float tableTop=table.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.max.y);
            table.transform.position+=Vector3.down*(tableTop-boardFloor+.2f);table.transform.SetParent(boardScene.transform,true);
            boardPresentation=new BoardPresentation(boardScene,cameraView);
            presentation.AudioCue=PlayPresentationAudio;
            presentation.BatchCompleted=(before,after)=>{battleHistory.Observe(before,after,id=>cards.ContainsKey(id)?cards[id]:null);nativeWeapons?.Complete(after);nativeMinionSummonVoices?.Complete(after);historyOffset=0;};
            music=gameObject.AddComponent<AudioSource>(); music.loop=true; music.volume=.25f;
            sfx=gameObject.AddComponent<AudioSource>(); sfx.volume=.5f;
            f031PageTurnSources=new AudioSource[4];
            for(int i=0;i<f031PageTurnSources.Length;i++)
            {f031PageTurnSources[i]=gameObject.AddComponent<AudioSource>();f031PageTurnSources[i].playOnAwake=false;f031PageTurnSources[i].spatialBlend=0;f031PageTurnSources[i].volume=sfx.volume;}
            heroVoiceSources=new AudioSource[2];
            for(int seat=0;seat<heroVoiceSources.Length;seat++)
            {heroVoiceSources[seat]=gameObject.AddComponent<AudioSource>();heroVoiceSources[seat].playOnAwake=false;heroVoiceSources[seat].spatialBlend=0;heroVoiceSources[seat].volume=.62f;}
            heroSpeech=new HeroSpeech(assets,heroVoiceSources,font);
            var voicePath=Path.Combine(dataRoot,"runtime/voice-index.json");
            if(File.Exists(voicePath))foreach(var voice in JsonUtility.FromJson<VoiceIndex>(File.ReadAllText(voicePath)).entries)
            {
                string key=voice.card_id+":"+voice.@event;
                if(!voices.ContainsKey(key))voices[key]=voice;
            }
            openingVoiceSource=gameObject.AddComponent<AudioSource>();openingVoiceSource.playOnAwake=false;
            openingVoiceSource.spatialBlend=0;openingVoiceSource.volume=.68f;
            F026InitializeClientOptions();
            battleOpening=new BattleOpening(assets,cardVisuals,openingVoiceSource,cards,database.classes,voices);
            heroSpeechReplay?.AttachOpening(battleOpening,heroSpeech);
            CardEffectRegistry.Load(assets.Root);
            foreach(var id in AudioEventRegistry.Assets.Values.Concat(CardEffectRegistry.AudioIds).Concat(new[]{"BGM_MAIN_TITLE","BGM_DUEL","SFX_FIREBALL","SFX_SPELL_IMPACT",AudioEventRegistry.ArmorHit,"SFX_COLLECTION_CARD","SFX_COLLECTION_PAGE_BACK","SFX_COLLECTION_PAGE_BACK_2","SFX_COLLECTION_PAGE_BACK_3","SFX_COLLECTION_PAGE_BACK_4","SFX_COLLECTION_PAGE_FORWARD","SFX_COLLECTION_PAGE_FORWARD_2","SFX_COLLECTION_PAGE_FORWARD_3","SFX_COLLECTION_PAGE_FORWARD_4","SFX_COLLECTION_FILTER_OPEN","SFX_COLLECTION_FILTER_CLOSE"}).Distinct())
                StartCoroutine(assets.Audio(id,c=>{sounds[id]=c;if(id=="BGM_MAIN_TITLE") SetMusic(id);}));
            initialized=true; SaveProfile();InitializeF024AccountSession();if(Flag("--account-replay"))StartCoroutine(RunF024AccountReplay());
            NativeWindow.SetTitle("HearthstoneClone · "+playerName);
            if(autoMatch) {page="decks";nextAuto=Time.time+3;}
            if(Flag("--visual-gallery")) {page="gallery";StartCoroutine(Capture("gallery",1.2f));}
            if(Flag("--visual-menu"))StartCoroutine(Capture("main_menu",1.2f));
            if(Flag("--ui-editor"))
            {
                uiEditor.Active=true;uiEditor.SelectView(Argument("--ui-view","card.minion"));
                uiEditor.SetCounts(int.Parse(Argument("--ui-count","4")),int.Parse(Argument("--ui-field-count","3")));
                if(Argument("--ui-select")!="")uiEditor.Select(Argument("--ui-select"));
                if(Flag("--ui-capture"))StartCoroutine(CaptureUIEditor());
            }
            var visualState=Argument("--visual-state");
            if(visualTest!="")visualState=Path.Combine(dataRoot,"visual/states/"+visualTest+".json");
            if(visualState!="")
            {
                state=JsonUtility.FromJson<StateView>(File.ReadAllText(visualState));
                presentation.Reset(state);mulliganPresentation.Observe(null,state);lifecycle.Observe(state);
                heroSpeech?.BeginMatch(state,this);
                battleHistory.Observe(null,state,id=>cards.ContainsKey(id)?cards[id]:null);
                if(visualTest=="mulligan_hover"&&mulliganPresentation.Cards.Length>1)mulligan.Add(mulliganPresentation.Cards.Last().entity);
                page="battle";menuScene.SetActive(false);boardScene.SetActive(true);
                if(visualTest=="game_menu"||visualTest=="game_options"){battleMenu=true;battleOptions=visualTest=="game_options";}
                SetMusic("BGM_DUEL");status="固定视觉测试";
                if(visualTest=="")StartCoroutine(Capture("visual",2));else StartCoroutine(VisualScenario());
            }
        }
        catch(Exception e) {status=e.ToString();Debug.LogException(e);}
    }
    void SaveProfile()
    {
        int cls=database.classes[classIndex].id;customDecks[cls]=deck.ToList();
        var p=new Profile{name=playerName,host=host,class_id=cls,deck=deck.ToArray(),
            decks=customDecks.OrderBy(pair=>pair.Key).Select(pair=>new SavedDeck{class_id=pair.Key,cards=pair.Value.ToArray()}).ToArray()};
        File.WriteAllText(Path.Combine(profilePath,"profile.json"),JsonUtility.ToJson(p,true));
    }
    bool HoldsClass(int cls){return database.classes.Any(c=>c.id==cls);}
    void SelectClass(int index)
    {
        customDecks[database.classes[classIndex].id]=deck.ToList();classIndex=index;
        int cls=database.classes[index].id;deck=customDecks.ContainsKey(cls)?customDecks[cls].ToList():database.classes[index].deck.ToList();
        status="";
    }
    void SetMusic(string id) {if(sounds.ContainsKey(id) && music.clip!=sounds[id]) {music.clip=sounds[id];music.Play();}}
    void PlaySound(string id) {if(sounds.ContainsKey(id)) sfx.PlayOneShot(sounds[id]);}
    void Connect()
    {
        if(!F024CanStartMatch())return;
        var error=ValidateDeck(); if(error!="") {status=error;return;}
        NativeWindow.SetTitle("HearthstoneClone · "+playerName);
        if(!F024HasAccount)SaveProfile(); if(connection!=null) connection.Dispose();battleOpening?.Clear();heroSpeech?.Clear();heroEmotesSupported=false;heroEmoteMenu.Close();
        connection=new Connection(); connectionLost=false;joining=true;status="正在连接服务器……";page="queue";connection.Connect(host,port);
    }
    string ValidateDeck()
    {
        if(!database.classes[classIndex].available) return "此职业卡牌尚未实现，暂不能匹配";
        if(deck.Count!=30) return "卡组必须恰好 30 张";
        foreach(var group in deck.GroupBy(x=>x))
        {
            if(!cards.ContainsKey(group.Key) || !cards[group.Key].Playable) return "存在未实现卡牌";
            var c=cards[group.Key]; if(c.Class!=12 && c.Class!=database.classes[classIndex].id) return "卡牌职业不合法";
            if(group.Count()>(c.Rarity==5?1:2)) return "卡牌数量超过限制";
        }
        if(database.classes[classIndex].id==1 && deck.Max(x=>cards[x].Blood)+deck.Max(x=>cards[x].Frost)+deck.Max(x=>cards[x].Unholy)>3) return "卡组需要超过三个符文";
        return "";
    }
    [NonSerialized] public Vector3 EvidenceFrameDeltas;
    void Update()
    {
        float earlyFrameDelta=Time.unscaledDeltaTime;
        visualFrameDelta=frameClock.Sample(Time.realtimeSinceStartupAsDouble);
        nativeShaderTime.Update(visualFrameDelta*Time.timeScale,Input.gyro.gravity);
        if(nativeAudio!=null)nativeAudio.Update();
        F026PumpClientOptions();
        if(!initialized) return;
        PumpF024AccountGateway();
        nativeHubScene?.Update(page);
        if(uiEditor.Active)
        {
            NativeCameraShakeDriver.Stop(cameraView);
            menuScene.SetActive(false);boardPresentation.Show(Vector2.zero);
            EditorViewport(uiEditor.SceneView,uiEditor.Viewport);
        }
        else if(page=="battle")
        {
            menuScene.SetActive(false);boardScene.SetActive(true);
            boardPresentation.Show(F026CameraShakeEnabled?presentation.Shake:Vector3.zero);
        }
        else
        {
            NativeCameraShakeDriver.Stop(cameraView);
            boardScene.SetActive(false);
            bool showMenuScene=page=="menu"||page=="account";
            if(menuScene.activeSelf!=showMenuScene)menuScene.SetActive(showMenuScene);
            cameraView.ResetProjectionMatrix();
            if(showMenuScene)
            {
                menuPresentation.Show();
                if(page=="menu")menuPresentation.LayoutHubFooter();
            }
        }
        if(assetLoader.Pump()){assets.Refresh();cardVisuals.Clear();interactionVisuals.Clear();nativeActors.Invalidate();}
        if(connection!=null)
        {
            string message;
            while(connection.Incoming.TryDequeue(out message))
            {
                var view=JsonUtility.FromJson<StateView>(message);
                if(view.type=="hello" && joining)
                {
                    heroEmotesSupported=(view.capabilities??new string[0]).Contains("hero_emotes_v1");
                    if(!HandleF024GameHello(view))
                    {joining=false;connection.Send(JsonUtility.ToJson(new JoinMessage{name=playerName,class_id=database.classes[classIndex].id,deck=deck.ToArray()}));}
                }
                else if(view.type=="account_result"&&joining)
                    HandleF024GameResume(JsonUtility.FromJson<F024AccountResponse>(message));
                else if(view.type=="queued") status="等待另一位玩家加入……";
                else if(view.type=="error") {if(joining){joining=false;connectionLost=true;}status=view.message;pending=false;nativeEffects?.RejectPowerSelection();ServerActionRejected?.Invoke(view.message);Debug.LogWarning(message);}
                else if(view.type=="hero_emote_ack") { }
                else if(view.type=="hero_emote_error") status="表情发送失败："+view.message;
                else if(view.type=="hero_emote_event")
                {heroSpeechReplay?.ObserveEmote(view);if(heroSpeech!=null&&!heroSpeech.ReceiveEmote(view,state,this))Debug.LogWarning("忽略无效的英雄表情广播："+message);}
                else if(view.type=="disconnected") {battleOpening?.EndEarly();battleOpening?.Clear();mulliganPresentation.ReleaseOpening();heroSpeech?.Clear();heroEmoteMenu.Close();status=view.message;connectionLost=true;pending=false;joining=false;selected=dragged=0;nativeEffects?.CancelPowerSelection(false);}
                else if(view.type=="menu") ResetMenu();
                else if(view.type=="state")
                {
                    nativeEffects?.ObservePowerState(view);
                    previous=state;state=view;pending=false;selected=0;dragged=0;cardPickup.Cancel();playChoices=null;
                    bool newMatch=previous==null||previous.match_id!=view.match_id;
                    bool deferOpening=newMatch&&view.phase=="mulligan"&&view.players!=null&&view.players.Length>=2&&battleOpening!=null;
                    heroSpeech?.BeginMatch(view,this,deferOpening);
                    heroSpeechReplay?.ObserveState(view,heroEmotesSupported);
                    nativeWeapons?.Observe(previous,view);nativeMinionSummonVoices?.ObserveSummonVoice(previous,view);presentation.Accept(previous,view,locations);
                    mulliganPresentation.Observe(previous,view);lifecycle.Observe(view);
                    if(deferOpening&&battleOpening!=null)
                    {
                        mulliganPresentation.HoldOpening();
                        battleOpening.Begin(view,this,heroSpeech==null?null:heroSpeech.PlayOpeningSequence(view),
                            ()=>mulliganPresentation.ReleaseOpening());
                    }
                    else if(OpeningActive&&view.phase!="mulligan")battleOpening.EndEarly();
                    page="battle";menuScene.SetActive(false);boardScene.SetActive(true);SetMusic("BGM_DUEL"); animationStart=Time.time;
                    File.WriteAllText(Path.Combine(profilePath,"state.json"),message);
                    if(previous!=null && previous.version!=view.version)
                    {
                        foreach(var e in view.events??new SemanticEvent[0])
                        {
                            string label=EventLabel(e);if(label!="")history.Add(label);
                            if(e.type=="Concede"){heroSpeech?.ReceiveConcede(e,view,this);heroSpeechReplay?.ObserveConcede(e);}
                        }
                        if(history.Count>50) history.RemoveRange(0,history.Count-50);
                    }
                    status=view.phase=="mulligan"?"选择需要替换的卡牌，然后确认":view.active==view.seat?"你的回合":"对手回合";
                    if(screenshotDir!="" && lastScreenshotPhase!=view.phase) {lastScreenshotPhase=view.phase;StartCoroutine(view.phase=="finished"?CaptureAfterPresentation(view.phase):Capture(view.phase,1));}
                    nextAuto=Time.time+.55f;
                }
            }
        }
        if(autoMatch && page=="decks" && Time.time>=nextAuto) {autoMatch=false;Connect();}
        float presentationDelta=visualFrameDelta;presentation.Tick(presentationDelta,locations);battleOpening?.Tick(visualFrameDelta);
        if(nativeShaman!=null&&page=="battle"&&!presentation.Paused)
            nativeShaman.Advance(presentationDelta*Mathf.Clamp(presentation.Speed,.1f,8),presentation.Display,presentation.NativeDeathEntities());
        EvidenceFrameDeltas=new Vector3(earlyFrameDelta,presentationDelta,Time.unscaledDeltaTime);mulliganPresentation.Tick(visualFrameDelta*presentation.Speed);
        if(nativeEffects!=null&&!presentation.Paused)nativeEffects.TickPowerSelection(visualFrameDelta*Mathf.Clamp(presentation.Speed,.1f,8));
        if(nativeWeapons!=null)nativeWeapons.Advance(presentation.Paused?0:visualFrameDelta*Mathf.Clamp(presentation.Speed,.1f,8));
        if(powerVisual!=null&&!presentation.Paused)powerVisual.Tick(visualFrameDelta*Mathf.Clamp(presentation.Speed,.1f,8));
        if(targetArrow!=null){if(selected==0||pending||presentation.InputLocked||battleMenu)targetArrow.Hide();else if(!presentation.Paused)targetArrow.Tick(visualFrameDelta*Mathf.Clamp(presentation.Speed,.1f,8));}
        if(nativeSummons!=null)nativeSummons.Advance(presentation.Paused?0:visualFrameDelta*Mathf.Clamp(presentation.Speed,.1f,8),presentation.Display);
        turnButton.Tick(visualFrameDelta*presentation.Speed);
        if(page=="battle")performance.Tick(visualFrameDelta,presentation.QueueCount,Path.Combine(profilePath,"performance.json"),assets.TextureCount,cardVisuals.RenderCount);
        var displayed=presentation.Display;
        lifecycle.Tick(displayed,presentation.Busy,presentation.Paused?0:visualFrameDelta*presentation.Speed);
        heroSelection.Tick(displayed!=null&&selected==displayed.players[displayed.seat].hero.entity&&dragged==0&&!presentation.InputLocked&&!battleMenu,visualFrameDelta);
        if(displayed!=null&&displayed.phase=="finished"&&lifecycle.ResultReady&&!resultAudioPlayed)
        {resultAudioPlayed=true;PlaySound(displayed.players[displayed.seat].result=="WON"?"SFX_VICTORY":"SFX_DEFEAT");}
        if(page=="battle"&&displayed!=null)
        {
            minionLayout.Tick(displayed,visualFrameDelta*presentation.Speed);
            var mouse=BattleMouse();
            if(visualTest=="card_drag"){dragged=displayed.players[displayed.seat].hand[0].entity;mouse=new Vector2(890,530);}
            if(visualTest=="hero_attack_select"||visualTest=="attack_arrow")selected=displayed.players[displayed.seat].hero.entity;
            if(visualTest=="combo_target_select"||visualTest=="weapon_target_select")
            {selected=dragged=displayed.players[displayed.seat].hand.First(e=>e.card_id== (visualTest=="combo_target_select"?"EX1_134":"EX1_133")).entity;placementIndex=0;placementPoint=new Vector2(800,595);}
            handPresentation.Tick(displayed.players[displayed.seat].hand,mouse,placementIndex>=0||TargetedSpell(dragged)?0:dragged,presentation,dragged==0&&!battleMenu&&!uiEditor.Active&&!mulliganPresentation.Active&&!presentation.InputLocked&&(visualTest==""||visualTest=="card_hover"),visualFrameDelta,visualTest=="card_hover"?1:-1);
            if(lastHandHover!=handPresentation.Hovered&&handPresentation.Hovered!=0)PlaySound("SFX_CARD_HOVER");lastHandHover=handPresentation.Hovered;
        }
        if(autoplay && !OpeningActive && !uiEditor.Active && state!=null && !pending && !connectionLost && !presentation.InputLocked && (!mulliganPresentation.Active || mulliganPresentation.Interactable) && state.moves.Length>0 && Time.time>=nextAuto)
        {
            string[] order={"mulligan","choose","attack","play","power","end_turn"};Move move=null;
            foreach(var kind in order) {move=state.moves.FirstOrDefault(m=>m.kind==kind);if(move!=null)break;}if(move!=null) SendMove(move);
        }
        heroSpeechReplay?.TickMulliganReplay(state,OpeningActive,presentation.InputLocked,pending,mulliganPresentation,TrySendReplayMove);
        heroSpeechReplay?.TickAttackReplay(state,OpeningActive,presentation.InputLocked,pending,mulliganPresentation,TrySendReplayMove);
        if(!OpeningActive&&Input.GetKeyDown(KeyCode.F12)) StartCoroutine(Capture("manual"));
        if(!OpeningActive&&Input.GetKeyDown(KeyCode.F3))debugUI=!debugUI;
        if(!OpeningActive&&Input.GetKeyDown(KeyCode.F6)){uiEditor.Active=!uiEditor.Active;selected=dragged=0;nativeEffects?.CancelPowerSelection(false);}
        if(!OpeningActive&&Input.GetKeyDown(KeyCode.F7))uiEditor.Reload();
        if(!OpeningActive&&debugUI&&Input.GetKeyDown(KeyCode.F4))presentation.Speed=presentation.Speed==1?2:1;
        if(!OpeningActive&&debugUI&&Input.GetKeyDown(KeyCode.F5)){presentation.Skip();lifecycle.Tick(presentation.Display,false,0);lifecycle.Skip();}
        if(Input.GetKeyDown(KeyCode.Escape)&&!OpeningActive)HandleBattleEscape();
    }
    void HandleBattleEscape(){if(heroEmoteMenu.IsOpen){heroEmoteMenu.Close();return;}if(uiEditor.Active)uiEditor.Active=false;else if(selected==0&&dragged==0)battleMenu=!battleMenu;selected=0;dragged=0;cardPickup.Cancel();playChoices=null;nativeEffects?.CancelPowerSelection(true);}
    string VisibleCardId(int entity)
    {
        foreach(var snapshot in new[]{state,previous})
        {
            if(snapshot==null)continue;
            foreach(var player in snapshot.players)
            {
                if(player.hero.entity==entity)return player.hero.card_id;
                var card=player.field.Concat(player.hand).FirstOrDefault(c=>c.entity==entity);
                if(card!=null)return card.card_id;
            }
        }
        return "";
    }
    IEnumerator PlayVoice(string cardId,string eventName,float presentationDelay=0,SemanticEvent semantic=null)
    {
        VoiceEntry voice;if(!voices.TryGetValue(cardId+":"+eventName,out voice))yield break;
        float delay=voice.delay+presentationDelay;if(delay>0)yield return new WaitForSeconds(delay);
        if(page!="battle")yield break;
        if(sounds.ContainsKey(voice.id))
        {
            PlaySound(voice.id);Debug.Log("VOICE "+voice.id+" t="+Time.time.ToString("F3"));
            if(eventName=="ATTACK"&&semantic!=null)heroSpeechReplay?.ObserveHeroAttackVoiceStarted(semantic,cardId,voice.id,sounds[voice.id].length);
            yield break;
        }
        yield return assets.Audio(voice.id,c=>
        {
            sounds[voice.id]=c;sfx.PlayOneShot(c);Debug.Log("VOICE "+voice.id+" samples="+c.samples+" t="+Time.time.ToString("F3"));
            if(eventName=="ATTACK"&&semantic!=null)heroSpeechReplay?.ObserveHeroAttackVoiceStarted(semantic,cardId,voice.id,c.length);
        });
    }
    void PlayPresentationAudio(SemanticEvent e,string cardId)
    {
        bool nativeWeaponHandled=nativeWeapons!=null&&nativeWeapons.TryHandle(e);
        bool armorHit=e.type=="Hit"&&presentation.ArmorAbsorbed(e)>0;
        if(armorHit)PlaySound(AudioEventRegistry.ArmorHit);
        if(e.type=="PlayHeroPower")cardId=presentation.CardId(e.target);
        if(!String.IsNullOrEmpty(e.card_id))cardId=e.card_id;
        bool heroAttack=e.type=="Attack"&&IsBaseHero(cardId);
        if(heroAttack&&heroAttackVoiceEvents.Add(e))
        {
            heroAttackVoiceOrder.Enqueue(e);
            while(heroAttackVoiceOrder.Count>512)heroAttackVoiceEvents.Remove(heroAttackVoiceOrder.Dequeue());
            VoiceEntry attackVoice;string voiceId=voices.TryGetValue(cardId+":ATTACK",out attackVoice)?attackVoice.id:"";
            heroSpeechReplay?.ObserveHeroAttackAccepted(e,cardId,voiceId,state);
            StartCoroutine(PlayVoice(cardId,"ATTACK",0,e));
        }
        if(nativeWeaponHandled)return;
        string summonCardId="";
        bool matchedMinionSummon=nativeMinionSummonVoices!=null&&nativeMinionSummonVoices.TryHandleSummonVoice(e,out summonCardId,out _);
        VoiceEntry sourcePlayVoice=null;
        bool sourceVoiceAvailable=matchedMinionSummon&&voices.TryGetValue(summonCardId+":PLAY",out sourcePlayVoice);
        bool suppressPlayOnSummon=sourcePlayVoice!=null&&sourcePlayVoice.suppress_play_sound_on_summon;
        bool playMinionSourceVoice=AudioEventRegistry.ShouldPlayMinionSourceVoice(
            String.IsNullOrEmpty(e.type)?"":e.type.ToUpperInvariant(),e.card_type,matchedMinionSummon,sourceVoiceAvailable,suppressPlayOnSummon);
        if(playMinionSourceVoice)StartCoroutine(PlayVoice(summonCardId,"PLAY"));
        bool nativeHeroHit=heroDamageAudio!=null&&heroDamageAudio.TryPlay(e,cardId,presentation.ArmorAbsorbed(e),state);
        if(cardId=="HERO_08bp"&&nativeEffects!=null&&nativeEffects.Duration(cardId)>0&&
            (e.type=="PlayHeroPower"||e.type=="EffectLaunch"||e.type=="Hit"||e.type=="SpellEffect"))return;
        var effect=CardEffectRegistry.Find(cardId);
        bool sourceOwnsWarlockAudio=nativeEffects!=null&&nativeEffects.Duration(cardId)>0&&
            AudioEventRegistry.SuppressDefaultAudio(cardId,e.type,true);
        string effectAudio=effect==null?null:e.type=="Hit"?effect.hit:e.type=="EffectLaunch"?effect.cast:e.type=="CardPlay"||e.type=="PlayHeroPower"?effect.start:null;
        string id;if(AudioEventRegistry.Assets.TryGetValue(e.type,out id))
        {
            if((!armorHit||e.type!="Hit")&&!nativeHeroHit&&(String.IsNullOrEmpty(effectAudio)||e.type=="CardPlay")&&!sourceOwnsWarlockAudio&&
                !(e.type=="Summon"&&nativeSummons!=null&&nativeSummons.Handles(e,state)))PlaySound(id);
        }
        if(!String.IsNullOrEmpty(effectAudio))PlaySound(effectAudio);
        string voice=AudioEventRegistry.VoiceEvent(e.type);
        bool minionVoiceEvent=e.card_type==4&&(e.type=="CardPlay"||e.type=="Summon");
        if(voice!=""&&!heroAttack&&!minionVoiceEvent)StartCoroutine(PlayVoice(e.card_id??cardId,voice));
    }
    bool IsBaseHero(string cardId)
    {return database!=null&&(database.classes??new ClassDef[0]).Any(hero=>hero!=null&&hero.hero==cardId);}
    IEnumerator Capture(string name,float delay=0)
    {
        if(delay>0) yield return new WaitForSeconds(delay);
        // Batch Player 不支持 WaitForEndOfFrame；截图请求仍在本帧渲染后执行。
        if(Application.isBatchMode)yield return null;else yield return new WaitForEndOfFrame();
        string dir=screenshotDir==""?Path.Combine(profilePath,"screenshots"):screenshotDir;
        Directory.CreateDirectory(dir);ScreenCapture.CaptureScreenshot(Path.Combine(dir,profile+"-"+name+".png"));
    }
    IEnumerator CaptureAfterPresentation(string name)
    {
        while(presentation.Busy)yield return null;
        yield return Capture(name);
    }
    string EventLabel(SemanticEvent e)
    {
        var labels=new Dictionary<string,string>{{"Hit","伤害"},{"Heal","治疗"},{"Summon","召唤"},{"Death","阵亡"},{"Draw","抽牌"},{"Buff","增益"},{"TURN_START","回合开始"},{"Morph","变形"},{"Reveal","奥秘触发"},{"PlayHeroPower","英雄技能"},{"Attack","攻击"},{"EquipWeapon","装备武器"},{"Silence","沉默"},{"Discard","弃牌"}};
        if(e.type=="CardPlay")return "打出 "+(cards.ContainsKey(e.card_id??"")?cards[e.card_id].Name:e.card_id);
        return labels.ContainsKey(e.type)?labels[e.type]+(e.amount!=0?" "+e.amount:""):"";
    }
    void SendMove(Move move)
    {
        if(state==null || pending || connection==null || connectionLost || OpeningActive || (presentation.InputLocked&&move.kind!="concede")) return;
        if(move.kind=="concede")presentation.Skip();
        if(move.kind=="end_turn")PlaySound("SFX_END_TURN");
        var msg=new ActionMessage{kind=move.kind,entity=move.entity,target=move.target,index=move.index,choice=move.choice,cards=move.cards??new int[0],
            match_id=state.match_id,version=state.version,action_id=profile+"-"+(++actionCounter)+"-"+Guid.NewGuid().ToString("N")};
        try {connection.Send(JsonUtility.ToJson(msg));pending=true;if(move.kind=="power"){nativeEffects?.SubmitPowerSelection(move.entity);selected=dragged=0;}}
        catch(Exception e) {status=e.Message;nativeEffects?.CancelPowerSelection(true);}
    }
    bool TrySendReplayMove(Move move)
    {bool wasPending=pending;SendMove(move);return !wasPending&&pending;}
    void SendHeroEmote(string emoteId)
    {
        if(OpeningActive)return;
        if(!heroEmotesSupported){status="当前服务器不支持英雄表情";return;}
        if(state==null||connection==null||connectionLost||state.phase=="finished")
        {status="当前对局不可发送英雄表情";return;}
        var message=HeroEmoteMenu.Command(state.match_id,emoteId,Guid.NewGuid().ToString("N"));
        try{connection.Send(JsonUtility.ToJson(message));}
        catch(Exception e){status="表情发送失败："+e.Message;}
    }
    void ResetMenu()
    {
        frameClock.Reset();
        page="menu";state=null;previous=null;status="";connectionLost=false;pending=false;joining=false;
        heroEmotesSupported=false;heroEmoteMenu.Close();
        battleOpening?.Clear();heroSpeech?.Clear();
        selected=dragged=hover=0;cardPickup.Cancel();playChoices=null;mulligan.Clear();history.Clear();lastScreenshotPhase="";
        battleHistory.Reset();historyOffset=0;interactionVisuals.Clear();
        sfx.Stop();
        presentation.Reset();handPresentation.Reset();minionLayout.Reset();mulliganPresentation.Reset();heroSelection.Reset();lifecycle.Reset();battleMenu=battleOptions=resultAudioPlayed=false;
        nativeEffects.Dispose();
        powerVisual?.ClearViews();targetArrow?.Dispose();targetArrow=null;
        if(nativeSummons!=null)nativeSummons.Dispose();
        if(nativeShaman!=null)nativeShaman.Dispose();
        if(nativeAudio!=null)nativeAudio.StopAll();
        nativeWeapons?.Clear();nativeMinionSummonVoices?.Clear();
        nativeActors.Clear();
        menuScene.SetActive(true);boardScene.SetActive(false);SetMusic("BGM_MAIN_TITLE");OnF024MenuReset();
    }
    void ReturnMenu()
    {
        if(connectionLost || connection==null){if(connection!=null)connection.Dispose();ResetMenu();return;}
        try{connection.Send("{\"type\":\"cancel\"}");}catch(IOException){connection.Dispose();ResetMenu();}
    }
    void Styles()
    {
        if(titleStyle!=null) return;
        titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=42,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};titleStyle.normal.textColor=new Color(1,.86f,.52f);
        labelStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=23,alignment=TextAnchor.MiddleCenter};smallStyle=new GUIStyle(labelStyle){fontSize=16};
        numberStyle=new GUIStyle(labelStyle){font=numberFont??font,fontSize=34,fontStyle=FontStyle.Normal};
        damageSplatStyle=new GUIStyle(numberStyle){font=numberFont??font,fontSize=70,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        damageSplatStyle.normal.textColor=Color.white;
        cardNameStyle=new GUIStyle(labelStyle){fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true};cardTextStyle=new GUIStyle(smallStyle){font=bodyFont??font,fontSize=13,wordWrap=true};
        buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=23,alignment=TextAnchor.MiddleCenter};
        menuButtonStyle=new GUIStyle(labelStyle){fontSize=27,fontStyle=FontStyle.Bold};
        menuButtonStyle.normal.textColor=new Color(.18f,.10f,.055f);menuButtonStyle.hover.textColor=new Color(.46f,.24f,.06f);
    }
    void Fill(Rect r, Color c) {var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void DrawDamageSplat(Rect rect,string value,float alpha)
    {
        var old=GUI.color;GUI.color=new Color(0,0,0,alpha);
        foreach(var offset in damageSplatOutlineOffsets)
            GUI.Label(new Rect(rect.x+offset.x,rect.y+offset.y,rect.width,rect.height),value,damageSplatStyle);
        GUI.color=new Color(1,1,1,alpha);GUI.Label(rect,value,damageSplatStyle);GUI.color=old;
    }
    void Frame(Rect r,Color c,float t=3) {Fill(new Rect(r.x,r.y,r.width,t),c);Fill(new Rect(r.x,r.yMax-t,r.width,t),c);Fill(new Rect(r.x,r.y,t,r.height),c);Fill(new Rect(r.xMax-t,r.y,t,r.height),c);}
    bool Button(Rect r,string text) {bool clicked=GUI.Button(r,text,F024ButtonStyle(r));if(clicked)PlaySound("SFX_UI_CLICK");return clicked;}
    bool Button(Rect r,string text,string replayTarget)
    {
        if(!F024ReplayGuiEnabled) {bool originalClick=GUI.Button(r,text,F024ButtonStyle(r));if(originalClick)PlaySound("SFX_UI_CLICK");return originalClick;}
        F024ReplayGuiRegisterTarget(replayTarget,r);
        F024ReplayGuiObserveControlBefore(replayTarget,r);
        bool clicked=GUI.Button(r,text,F024ButtonStyle(r));
        F024ReplayGuiObserveControlAfter(replayTarget);
        if(clicked)PlaySound("SFX_UI_CLICK");
        return clicked;
    }
    bool Button(Rect r,string text,string replayTarget,Rect scrollView,Vector2 scroll)
    {
        if(!F024ReplayGuiEnabled) {bool originalClick=GUI.Button(r,text,F024ButtonStyle(r));if(originalClick)PlaySound("SFX_UI_CLICK");return originalClick;}
        F024ReplayGuiRegisterTarget(replayTarget,r,scrollView,scroll);
        F024ReplayGuiObserveControlBefore(replayTarget,r);
        bool clicked=GUI.Button(r,text,F024ButtonStyle(r));
        F024ReplayGuiObserveControlAfter(replayTarget);
        if(clicked)PlaySound("SFX_UI_CLICK");
        return clicked;
    }
    void Panel(Rect r) {Fill(r,new Color(.08f,.045f,.023f,.92f));Frame(r,new Color(.56f,.37f,.17f));}
    void OnGUI()
    {
        if(Event.current.type==EventType.Repaint)powerVisual?.BeginGUI();
        GUI.matrix=page=="battle"?BattleLayout.Matrix:page=="menu"?DesignViewport.Menu.Matrix:DesignViewport.Fit(1600,1000);Styles();
        if(!initialized) {GUI.Label(new Rect(40,40,1500,800),status,labelStyle);return;}
        if(uiEditor.Active){uiEditor.Draw(font);return;}
        bool replayInjected=false;
        try
        {
            if(f024AccountReplayActive&&f024AccountGuiReplayMode)
            {
                replayInjected=F024ReplayGuiBeginOnGUI();
            }
            bool openingBlocked=page=="battle"&&OpeningActive;
            bool hubOverlayOpen=F025HubOverlayOpen;
            bool previousEnabled=GUI.enabled;if(openingBlocked||hubOverlayOpen)GUI.enabled=false;
            calibration.Collecting=false;calibration.BeginFrame();
            if(debugUI){GUI.Label(new Rect(15,12,450,34),"炉石传说 · 本地对战",labelStyle);GUI.Label(new Rect(1240,12,350,34),playerName+" / "+profile,smallStyle);}
            if(page=="menu") DrawF024Hub();
            else if(page=="account") DrawF024AccountPage();
            else if(page=="collection") DrawF024CollectionPage();
            else if(page=="packs") DrawF024PacksPage();
            else if(page=="shop") DrawF024ShopPage();
            else if(page=="quests") DrawF026QuestPage();
            else if(page=="decks"&&F024HasAccount) DrawF024DeckPicker();
            else if(page=="decks"||page=="builder") Decks();
            else if(page=="gallery") Gallery();
            else if(page=="queue") {Panel(new Rect(470,320,660,300));GUI.Label(new Rect(480,350,640,100),"局域网匹配",titleStyle);GUI.Label(new Rect(480,460,640,80),status,labelStyle);if(Button(new Rect(680,555,240,55),connectionLost?"返回卡组选择":"取消")) {connection.Dispose();connectionLost=false;joining=false;status="";page="decks";}}
            else if(page=="battle" && state!=null) Battle();
            GUI.enabled=previousEnabled;
            if(openingBlocked&&OpeningActive)
            {
                battleOpening.Draw(state,font);
                heroSpeech?.Draw(state,state.seat,battleOpening.BubbleLocations);
                ConsumeOpeningInput();
            }
            else if(page=="battle"&&state!=null)heroSpeech?.Draw(state,state.seat,locations);
            heroSpeechReplay?.ObserveOpeningFrame(state,battleOpening,mulliganPresentation,heroSpeech,
                name=>StartCoroutine(Capture(name)));
            if(f025SettingsOpen) DrawF025SettingsOverlay();
            else if(f025CollectionDetailOpen) DrawF025CollectionDetailOverlay();
            else if(f026ShopBuyOpen) DrawF026ShopBuyOverlay();
            else if(f026FriendsOpen) DrawF026FriendsPanel();
            else if(f024PackRevealActive) DrawF024PackReveal();
            else if(page!="battle") DrawF026HubChrome();
            if(page!="battle"&&page!="menu"&&!hubOverlayOpen &&
                !(page=="collection" && status=="已同步账号数据")) GUI.Label(new Rect(280,930,960,35),status,smallStyle);
            if(!F026CameraShakeEnabled)NativeCameraShakeDriver.Stop(cameraView);
        }
        finally
        {
            if(replayInjected)
            {
                F024ReplayGuiEndOnGUI();
            }
        }
    }
    void ConsumeOpeningInput()
    {
        var current=Event.current;
        if(current.type==EventType.MouseDown||current.type==EventType.MouseUp||current.type==EventType.MouseMove||
            current.type==EventType.ContextClick||current.type==EventType.ScrollWheel||current.type==EventType.KeyDown||current.type==EventType.KeyUp)
            current.Use();
    }
    void Menu()
    {
        GUI.Label(new Rect(520,264,560,74),"炉石传说",menuButtonStyle);
        if(MenuButton("FirstButton","对战模式")) {page="decks";status="选择职业与卡组";}
        if(MenuButton("SecondButton","我的卡组")) page="builder";
        if(MenuButton("ThirdButton","卡组选择")) page="decks";
        if(MenuButton("FourthButton","退出游戏")) Application.Quit();
        if(Event.current.type==EventType.Repaint&&!new[]{"FirstButton","SecondButton","ThirdButton","FourthButton"}.Any(n=>menuPresentation.ButtonRect(n).Contains(Event.current.mousePosition)))menuHovered="";
        GUI.Label(new Rect(610,716,380,42),"玩家名称",smallStyle);playerName=GUI.TextField(new Rect(630,764,340,42),playerName,32);
        var safe=DesignViewport.Menu.Matrix.inverse.MultiplyPoint3x4(new Vector2(Screen.width,Screen.height));
        if(Button(new Rect(Mathf.Min(1260,safe.x-275),840,255,52),"UI 校对工具 · F6"))uiEditor.Active=true;
    }
    bool MenuButton(string name,string label)
    {
        var rect=menuPresentation.ButtonRect(name);bool over=rect.Contains(Event.current.mousePosition);
        if(Event.current.type==EventType.Repaint)
        {
            menuPresentation.Highlight(name,over,over&&Input.GetMouseButton(0));
            if(over&&menuHovered!=name){PlaySound("SFX_UI_HOVER");menuHovered=name;}
        }
        if(!F024ReplayGuiEnabled)
        {
            bool originalClick=GUI.Button(rect,label,menuButtonStyle);if(originalClick)PlaySound("SFX_UI_CLICK");return originalClick;
        }
        string replayTarget=F024ReplayMenuTarget(name);
        F024ReplayGuiRegisterTarget(replayTarget,rect);
        bool clicked=GUI.Button(rect,label,menuButtonStyle);
        if(clicked)PlaySound("SFX_UI_CLICK");return clicked;
    }
    void Gallery()
    {
        Panel(new Rect(120,110,1360,770));
        string[] ids={"CS2_029","CS2_182","EX1_567","EX1_284","RLK_085"};
        for(int i=0;i<ids.Length;i++)
        {
            var c=cards[ids[i]];Card(new Rect(160+i*260,300,225,340),new EntityView{card_id=c.CardId,cost=c.Cost,attack=c.Attack,health=c.Health,durability=c.Durability},true);
        }
        GUI.Label(new Rect(300,150,1000,65),"固定视觉回归 · 法术 / 随从 / 武器 / 传说 / 死亡骑士",labelStyle);
    }
    void Decks()
    {
        Panel(new Rect(70,80,1460,805));GUI.Label(new Rect(500,92,600,62),page=="builder"?"我的卡组":"选择卡组",titleStyle);
        for(int i=0;i<database.classes.Length;i++)
        {
            var c=database.classes[i];float x=95+(i%6)*234,y=160+(i/6)*176;var r=new Rect(x,y,212,166);
            if(i==classIndex)Frame(r,new Color(.95f,.73f,.25f),3);
            var art=cardVisuals.Render(cards[c.hero],"hero");GUI.DrawTexture(new Rect(x+51,y+1,110,137),art,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(x,y+132,212,33),c.name+(c.available?"":" · 开发中"),labelStyle);
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)) {SelectClass(i);PlaySound("SFX_UI_CLICK");}
        }
        if(page=="decks")
        {
            GUI.Label(new Rect(220,520,1160,52),database.classes[classIndex].name+" · "+deck.Count+" / 30 张",labelStyle);
            GUI.Label(new Rect(280,590,280,45),"服务器 IP",labelStyle);host=GUI.TextField(new Rect(580,590,460,44),host,64);
            if(Button(new Rect(380,695,360,72),"编辑卡组")) page="builder";if(Button(new Rect(840,695,360,72),"进入 LAN 匹配")) Connect();
        }
        else Builder();if(Button(new Rect(110,810,210,54),"返回")) {SaveProfile();status="";page="menu";}
    }
    void Builder()
    {
        GUI.Label(new Rect(95,496,500,40),"卡池 · 点击加入",labelStyle);search=GUI.TextField(new Rect(130,543,480,36),search);
        var pool=database.cards.Where(c=>c.Playable && (c.Class==12||c.Class==database.classes[classIndex].id) && (search==""||c.Name.Contains(search)||c.CardId.Contains(search))).OrderBy(c=>c.Cost).ThenBy(c=>c.Name).ToArray();
        poolScroll=GUI.BeginScrollView(new Rect(100,585,600,210),poolScroll,new Rect(0,0,560,pool.Length*34));
        for(int i=0;i<pool.Length;i++) {var c=pool[i];if(GUI.Button(new Rect(0,i*34,554,32),c.Cost+" "+c.Name+" "+c.Attack+" / "+c.Health,smallStyle)) {if(deck.Count<30 && deck.Count(x=>x==c.CardId)<(c.Rarity==5?1:2)) deck.Add(c.CardId);}}
        GUI.EndScrollView();GUI.Label(new Rect(765,495,650,42),"当前卡组 · "+deck.Count+" / 30 · 点击移除",labelStyle);
        var groups=deck.GroupBy(x=>x).OrderBy(g=>cards[g.Key].Cost).ToArray();deckScroll=GUI.BeginScrollView(new Rect(765,545,640,195),deckScroll,new Rect(0,0,600,groups.Length*34));
        for(int i=0;i<groups.Length;i++) {var g=groups[i];var c=cards[g.Key];if(GUI.Button(new Rect(0,i*34,594,32),c.Cost+" "+c.Name+" × "+g.Count(),smallStyle)) deck.Remove(g.Key);}
        GUI.EndScrollView();if(Button(new Rect(770,757,200,50),"恢复预设")) deck=database.classes[classIndex].deck.ToList();
        if(Button(new Rect(985,757,200,50),"清空")) deck.Clear();if(Button(new Rect(1200,757,200,50),"保存")) {status=ValidateDeck();if(status==""){SaveProfile();status="卡组已保存";page="decks";}}
    }
    Rect EntityRect(int player,int index,int count,bool hero=false)
    {
        return hero?BattleLayout.Hero(player==state.seat):BattleLayout.Minion(player==state.seat,index,count);
    }
    void Battle()
    {
        bool guiEnabledAtStart=GUI.enabled;
        if(heroEmoteMenu.IsOpen)GUI.enabled=false;
        var shown=presentation.Display??state;
        if(Event.current.type==EventType.Repaint)
        {
            nativeActors.RefreshUnborrowed(nativeEffects.BorrowedActors.Concat(nativeSummons.BorrowedActors).Concat(nativeShaman.BorrowedActors));
            var boardActors=shown.players.SelectMany(p=>p.field).Select(e=>nativeActors.Resolve(e)).Where(a=>a!=null).ToArray();
            nativeEffects.BeginFrame(presentation.NativeKeys());nativeSummons.BeginFrame(presentation.NativeSummonKeys());presentation.PrepareNative();nativeEffects.EndFrame();
            nativeActors.Retain(nativeEffects.BorrowedActors.Concat(nativeSummons.BorrowedActors).Concat(nativeShaman.BorrowedActors).Concat(boardActors));
        }
        locations.Clear();armorLocations.Clear();
        ownDeckHover=enemyDeckHover=enemyHandHover=false;
        for(int i=0;i<2;i++)
        {
            var p=shown.players[i];bool own=i==shown.seat;
            HeroArea(p,own);PowerArea(p,own);WeaponArea(p,own);
            var zone=new Rect(440,own?BattleLayout.OwnMinionY:BattleLayout.EnemyMinionY,1040,172);
            calibration.Draw("battle.minions."+(own?"own":"enemy"),own?"己方随从区域":"敌方随从区域",zone,()=>
            {
            for(int j=0;j<p.field.Length;j++)
            {
                var entity=p.field[j];var r=minionLayout.Pose(entity.entity,BattleLayout.Minion(own,j,p.field.Length));locations[entity.entity]=r;
                if(!presentation.Dying(entity.entity))Portrait(AnimatedRect(entity.entity,r,"minion"),entity,false);locations[entity.entity]=calibration.VisualBounds(entity.entity,r);
            }
            });
            Deck(p,own);Mana(p,own);
        }
        var opponent=shown.players[1-shown.seat];
        for(int i=0;i<opponent.hand_count;i++)
        {
            var pose=HandLayout.Pose(i,opponent.hand_count,true);
            pose=calibration.Pose("battle.hand.enemy",pose,new Vector2(960,-10));
            pose.center=Vector2.Lerp(BattleLayout.Deck(false).center,pose.center,presentation.EnemyHandProgress(i,1-shown.seat));
            var r=pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight);var matrix=GUI.matrix;
            enemyHandHover|=HandLayout.Contains(pose,Event.current.mousePosition);
            BattleGeometry.Rotate(pose.angle,pose.center);DrawAsset(r,"UI_CARD_BACK");GUI.matrix=matrix;
        }
        var hand=shown.players[shown.seat].hand;
        if(!mulliganPresentation.Active)
        {
            foreach(var e in hand)if(e.entity!=handPresentation.Hovered&&e.entity!=dragged&&!presentation.Playing(e.entity))HandCard(e);
            var hovered=hand.FirstOrDefault(e=>e.entity==handPresentation.Hovered);
            if(hovered!=null&&hovered.entity!=dragged&&!presentation.Playing(hovered.entity))HandCard(hovered);
        }
        bool controlsEnabled=GUI.enabled;
        if(heroEmoteMenu.IsOpen)GUI.enabled=false;
        EndTurn(shown);
        GUI.enabled=controlsEnabled;
        DrawBattleHistory(shown);
        if(debugUI)DebugOverlay();
        if(heroEmoteMenu.IsOpen)GUI.enabled=false;
        GameMenuButton();
        GUI.enabled=controlsEnabled;
        if(battleMenu||connectionLost||state==null||state.phase=="finished")heroEmoteMenu.Close();
        bool emoteInputHandled=false;
        Rect replayHeroRect=new Rect();bool hasReplayHero=false;
        if(!OpeningActive&&!battleMenu&&state!=null&&state.players!=null&&state.seat>=0&&state.seat<state.players.Length)
        {
            var ownHero=state.players[state.seat].hero;Rect heroRect;
            if(ownHero!=null&&locations.TryGetValue(ownHero.entity,out heroRect))
            {
                replayHeroRect=heroRect;hasReplayHero=true;
                if(state.phase!="finished")
                {
                    bool consumeEvent;
                    emoteInputHandled=heroEmoteMenu.HandleInput(Event.current,BattleMouse(),heroRect,state.match_id,out consumeEvent);
                    if(consumeEvent)Event.current.Use();
                }
            }
        }
        if(hasReplayHero&&!OpeningActive)
            heroSpeechReplay?.Tick(state,heroEmotesSupported,state.phase=="finished"&&!presentation.Busy&&lifecycle.ResultReady,
                replayHeroRect,heroSpeech,heroEmoteMenu,SendHeroEmote,()=>SendMove(new Move{kind="concede"}),name=>StartCoroutine(Capture(name)));
        if(heroSpeechReplay!=null&&heroSpeechReplay.ShouldExit&&!heroReplayExitStarted){heroReplayExitStarted=true;Application.Quit();}
        if(shown.phase=="finished"&&!presentation.Busy){if(lifecycle.ResultReady)Result();}
        else if(connectionLost)
        {
            Panel(new Rect(650,350,620,360));GUI.Label(new Rect(660,365,600,75),"连接已中断",titleStyle);
            GUI.Label(new Rect(665,450,590,110),status+"\n返回主界面后可重新进入匹配。",labelStyle);
            if(Button(new Rect(795,600,330,70),"返回主界面"))ReturnMenu();
        }
        else if(mulliganPresentation.Active&&!OpeningActive)Mulligan();
        else if(!battleMenu&&state.choices!=null&&state.choices.Length>0&&!presentation.InputLocked)Choices();
        else if(!presentation.InputLocked&&!pending&&!battleMenu&&!heroEmoteMenu.IsOpen&&!emoteInputHandled)Inputs();
        if(!mulliganPresentation.Active)
        {
            if(placementIndex>=0&&dragged!=0)
            {
                var held=state.players[state.seat].hand.FirstOrDefault(c=>c.entity==dragged);
                if(held!=null)
                {
                    bool weapon=cards.ContainsKey(held.card_id)&&cards[held.card_id].Type==7;
                    var preview=weapon?new Rect(placementPoint.x-60,placementPoint.y-60,120,120):new Rect(placementPoint.x-66,placementPoint.y-82,132,164);
                    if(weapon)WeaponVisual(preview,held);else Portrait(preview,held,false);
                    locations[held.entity]=calibration.VisualBounds(held.entity,preview);
                }
            }
            var drag=hand.FirstOrDefault(e=>e.entity==dragged);if(drag!=null&&placementIndex<0)HandCard(drag);
            Effects();
            nativeEffects?.DrawPowerSelection();
            if(selected!=0&&(dragged==0||TargetedHeld()))Arrow(selected);
            DrawBattleDetails(shown);
            DrawCountHints(shown);
        }
        GUI.enabled=guiEnabledAtStart;
        if(!battleMenu)heroSpeech?.Draw(shown,shown.seat,locations);
        if(!battleMenu&&heroEmoteMenu.IsOpen)heroEmoteMenu.Draw(id=>heroSpeech==null?id:heroSpeech.Label(id),SendHeroEmote,heroSpeech,buttonStyle);
        if(battleMenu)
            GameMenu();
        GUI.enabled=guiEnabledAtStart;
    }
    Rect AnimatedRect(int entity,Rect r,string template)
    {
        var parent=BattleLayout.Matrix.inverse*GUI.matrix;
        var matrix=parent*calibration.Get(template).Matrix(r,new Vector2(r.width/320,r.height/480));
        var world=r;world.center=matrix.MultiplyPoint3x4(r.center);
        var animated=presentation.Animate(entity,world);r.center+=(Vector2)parent.inverse.MultiplyVector(animated.center-world.center);
        var center=r.center;r.size*=animated.width/Mathf.Max(.01f,world.width);r.center=center;return r;
    }
    void HeroArea(PlayerView p,bool own,bool animate=true)
    {
        var hr=BattleLayout.Hero(own);string key="battle.hero."+(own?"own":"enemy");
        if(animate){hr=lifecycle.HeroPose(hr,own);if(own)hr=heroSelection.Pose(hr);}
        calibration.Draw(key,own?"己方英雄区域":"敌方英雄区域",hr,()=>
        {
            var portrait=animate?AnimatedRect(p.hero.entity,hr,"hero"):hr;
            if(!animate||!lifecycle.DeadHero(p))Portrait(portrait,p.hero,true);
            else if(!lifecycle.ResultReady)
            {
                if(heroFragments==null)heroFragments=new HeroFragments();
                heroFragments.Draw(portrait,cardVisuals.Render(cards[p.hero.card_id],"hero"),lifecycle.DeathProgress);
                var cracks=assets.Texture("VFX_HERO_CRACKS");
                spellParticles.Draw(portrait,cracks,new Color(1,.72f,.35f,Mathf.Sin(lifecycle.DeathProgress*Mathf.PI)*.85f));
                BattlePainter.Glow(new Rect(portrait.center.x-170,portrait.center.y-170,340,340),new Color(1,.55f,.12f,Mathf.Sin(lifecycle.DeathProgress*Mathf.PI)*.65f));
            }
            locations[p.hero.entity]=calibration.VisualBounds(p.hero.entity,portrait);
            if(animate&&lifecycle.DeadHero(p))return;
            var armor=BattleLayout.TransformRect(hr,portrait,new Rect(hr.xMax-16,hr.y+104,52,48));string armorId=key+"/armor";
            armorLocations[p.hero.entity]=calibration.VisualBounds(armorId,armor,Vector2.one);
            if(p.hero.armor>0||calibration.Collecting)calibration.Draw(armorId,"护甲",armor,()=>Badge(armor,p.hero.armor,Color.white,"MODEL_GEM_ARMOR"));
            var attack=BattleLayout.TransformRect(hr,portrait,new Rect(hr.x-16,hr.yMax-57,52,48));if(p.hero.attack>0||calibration.Collecting)calibration.Draw(key+"/attack","英雄攻击",attack,()=>Badge(attack,p.hero.attack,new Color(.87f,.66f,.16f),null,UIText.AttackColor(p.hero.attack,0,p.hero.attack_buffed)));
            if(p.secret_count>0)Ink(new Rect(hr.x-38,hr.y-15,60,46),"? "+p.secret_count,numberStyle,new Color(1,.5f,1));
            if(p.hero.card_id=="HERO_11")Ink(new Rect(hr.x-175,hr.y+110,150,30),"尸体 "+p.corpses,smallStyle,new Color(.8f,.9f,.8f));
        });
        var viewport=uiEditor!=null&&uiEditor.Active?new DesignViewport(1920,1080,1920,1080,1520):DesignViewport.Battle;
        var name=BattleGeometry.EdgeRect(viewport,false,own?920:195,168,40);
        calibration.Draw(key+"/name","窗口左侧玩家名",name,()=>Ink(name,p.name,smallStyle,Color.white));
    }
    void PowerVisual(Rect pr,EntityView entity,Color color,bool ready=false,bool over=false)
    {
        if(NativePowerVisual.SupportsCard(entity.card_id)&&powerVisual!=null)
        {
            var shown=presentation.Display??state;bool enemy=shown!=null&&shown.players[1-shown.seat].power.entity==entity.entity;
            int actorState=selected==entity.entity?5:ready?(over?7:6):over?(enemy?3:2):1;
            calibration.Draw("power","英雄技能整体",pr,new Vector2(pr.width/320,pr.height/480),()=>
                powerVisual.Draw(pr,entity,cards[entity.card_id],false,enemy,color,powerVisual.Time,!presentation.Paused,actorState),entity.entity);
            return;
        }
        var c=cards.ContainsKey(entity.card_id)?cards[entity.card_id]:new CardDef{CardId=entity.card_id};var units=new Vector2(pr.width/320,pr.height/480);
        calibration.Draw("power","英雄技能整体",pr,units,()=>
        {
            over=(over||PointerOver(entity.entity))&&!mulliganPresentation.Active;
            calibration.Draw("power/model","技能图像 / 边框",pr,units,()=>
            {
                var image=cardVisuals.Render(c,entity.used?"power_used":"power");
                if(ready)interactionVisuals.Draw(pr,image,new Color(.15f,1,.04f,.95f));
                if(over)interactionVisuals.Draw(pr,image,Color.white,true);
                BattlePainter.Draw(pr,image,color);cardVisuals.RegisterParts(c,"power",pr);
                if(calibration.Collecting)calibration.Register("power/art","原画纹理 · UV 翻转 / 旋转",pr,units);
            });
            var cost=new Rect(pr.x+8,pr.yMax-32,37*pr.width/122,39*pr.height/126);
            if(!entity.used)calibration.Draw("power/cost","技能费用",cost,units,()=>Badge(cost,UIText.PowerCost(entity),new Color(.08f,.38f,.72f)));
        },entity.entity);
    }
    void PowerArea(PlayerView p,bool own,bool interactive=true)
    {
        var pr=BattleLayout.Power(own);
        calibration.Draw("battle.power."+(own?"own":"enemy"),own?"己方英雄技能区域":"敌方英雄技能区域",pr,()=>
        {
            bool ready=own&&!pending&&!presentation.InputLocked&&BattleInteraction.Ready(state,p.power.entity,"power");
            bool over=PointerOver(p.power.entity)&&!mulliganPresentation.Active;
            var color=NativePowerVisual.SupportsCard(p.power.card_id)?Color.white:over?(EvidencePressed||Input.GetMouseButton(0)?new Color(.85f,.85f,.85f):new Color(1.12f,1.12f,1.12f)):Color.white;
            var visual=p.power;
            if(NativePowerVisual.SupportsCard(p.power.card_id)&&!presentation.Paused&&state!=null&&state.match_id==(presentation.Display??state).match_id)
            {
                var authority=BattleInteraction.Entity(state,p.power.entity);
                if(authority!=null){visual=BattleInteraction.Copy(p.power);visual.entity=p.power.entity;visual.used=authority.used;}
            }
            PowerVisual(pr,visual,color,ready,over);locations[p.power.entity]=calibration.VisualBounds(p.power.entity,pr);
        });
    }
    void WeaponArea(PlayerView p,bool own)
    {
        if(p==null||(p.weapon==null||string.IsNullOrEmpty(p.weapon.card_id))&&
            (nativeWeapons==null||!nativeWeapons.HasSummonGlow(p.seat)))return;
        var r=BattleLayout.Weapon(own);
        calibration.Draw("battle.weapon."+(own?"own":"enemy"),own?"己方武器区域":"敌方武器区域",r,()=>
        {
            NativeBattleWeapons.GlowSample glow=null;
            bool showingGlow=nativeWeapons!=null&&nativeWeapons.TrySummonGlow(p.seat,out glow);
            if(p.weapon!=null&&!string.IsNullOrEmpty(p.weapon.card_id))
            {
                // The source FSM enables its summon quad before the new weapon is presented.
                if(!presentation.Dying(p.weapon.entity)&&(!showingGlow||glow.entity!=p.weapon.entity))
                    WeaponVisual(AnimatedRect(p.weapon.entity,r,"weapon"),p.weapon);
                locations[p.weapon.entity]=calibration.VisualBounds(p.weapon.entity,r);
            }
            if(showingGlow)nativeWeapons.DrawSummonGlow(p.seat,r);
        });
    }
    void WeaponVisual(Rect rect,EntityView entity)
    {
        CardDef card;if(!cards.TryGetValue(entity.card_id,out card))return;
        var units=new Vector2(rect.width/320,rect.height/480);
        calibration.Draw("weapon","装备武器整体",rect,units,()=>
        {
            calibration.Draw("weapon/model","圆形武器框",rect,units,()=>
            {BattlePainter.Draw(rect,cardVisuals.Render(card,"weapon"),Color.white);cardVisuals.RegisterParts(card,"weapon",rect);if(calibration.Collecting)calibration.Register("weapon/art","武器原画 UV",rect,units);});
            float scale=rect.width/120;int oldSize=numberStyle.fontSize;numberStyle.fontSize=Mathf.RoundToInt(30*scale);
            var attack=new Rect(rect.x-5*scale,rect.yMax-37*scale,40*scale,37*scale);
            calibration.Draw("weapon/attack","武器攻击",attack,units,()=>UIText.Draw(attack,entity.attack.ToString(),numberStyle,UIText.AttackColor(entity.attack,card.Attack,entity.attack_buffed),null,true));numberStyle.fontSize=oldSize;
            var durability=new Rect(rect.xMax-33*scale,rect.yMax-37*scale,40*scale,37*scale);
            calibration.Draw("weapon/durability","武器耐久",durability,units,()=>Badge(durability,entity.durability,Color.white,"MODEL_GEM_DURABILITY",UIText.StatColor(entity.durability,entity.max_durability,entity.durability_buffed)));
        },entity.entity);
    }
    void HandCard(EntityView e)
    {
        var shown=presentation.Display??state;var hand=shown.players[shown.seat].hand;
        var fallback=HandLayout.OwnPose(Array.FindIndex(hand,c=>c.entity==e.entity),hand.Length,calibration);
        fallback.center=Vector2.Lerp(BattleLayout.Deck(true).center,fallback.center,presentation.HandProgress(e.entity));
        var pose=handPresentation.Pose(e.entity,fallback);var r=pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight);
        locations[e.entity]=r;var matrix=GUI.matrix;BattleGeometry.Rotate(pose.angle,pose.center);
        BattlePainter.Shadow(new Rect(r.x-8,r.y+12,r.width+24,r.height+12),.85f);Card(r,e,true);GUI.matrix=matrix;
        locations[e.entity]=calibration.VisualBounds(e.entity,r);
        if(e.entity==dragged&&BattleLayout.PlayZone.Contains(visualTest=="card_drag"?new Vector2(890,530):Event.current.mousePosition))
            BattlePainter.Ring(new Rect(470,555,980,184),new Color(.4f,.9f,.7f,.2f));
    }
    void Deck(PlayerView p,bool own)
    {
        var r=BattleLayout.Deck(own);
        calibration.Draw("battle.deck."+(own?"own":"enemy"),own?"己方牌库":"敌方牌库",r,()=>
        {
        BattlePainter.Shadow(new Rect(r.x-14,r.y+14,r.width+35,r.height+22));
        if(p.deck_count>0)BattlePainter.Draw(r,cardVisuals.Prop(DeckModel(p.deck_count)),Color.white);
        bool over=r.Contains(Event.current.mousePosition);var m=BattleLayout.Matrix.inverse*GUI.matrix;
        var a=(Vector2)m.MultiplyPoint3x4(r.min);var b=(Vector2)m.MultiplyPoint3x4(r.max);
        var bounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        if(own){ownDeckHover=over;ownDeckBounds=bounds;}else{enemyDeckHover=over;enemyDeckBounds=bounds;}
        if(debugUI)Ink(new Rect(r.x+10,r.yMax+3,r.width,36),p.deck_count.ToString(),numberStyle,Color.white);
        });
    }
    public static string DeckModel(int count){return "MODEL_DECK_"+(count<=1?"1":count<=8?"25":count<=16?"50":count<=24?"75":"Full");}
    void Mana(PlayerView p,bool own)
    {
        var area=BattleLayout.Mana(own);float x=area.x,y=area.y;
        calibration.Draw("battle.mana."+(own?"own":"enemy"),own?"己方法力区域":"敌方法力区域",area,()=>
        {
        var number=BattleLayout.ManaNumber(own);calibration.Draw("battle.mana."+(own?"own":"enemy")+"/number","法力计数",number,()=>
        {
            BattlePainter.Draw(number,cardVisuals.Prop("MODEL_MANA_COUNTER"),Color.white);
            Ink(number,p.mana+"/"+p.max_mana,numberStyle,Color.white);
        });
        if(own)for(int i=0;i<p.max_mana;i++)
        {
            var old=GUI.color;GUI.color=i<p.mana?Color.white:new Color(.26f,.34f,.43f);
            DrawAsset(new Rect(x+i*32,y,32,37),"UI_MANA");GUI.color=old;
        }
        });
    }
    void EndTurn(StateView shown,bool interactive=true)
    {
        var r=BattleLayout.EndTurn;bool own=shown.active==shown.seat&&shown.phase=="play";
        calibration.Draw("battle.end_turn","结束回合按钮",r,()=>EndTurnContent(shown,r,own,interactive));
    }
    void EndTurnContent(StateView shown,Rect r,bool own,bool interactive)
    {
        bool ready=own&&!battleMenu&&!pending&&!presentation.InputLocked&&!mulliganPresentation.Active;
        bool over=visualTest==""&&r.Contains(Event.current.mousePosition);var old=GUI.color;
        bool exhausted=ready&&BattleInteraction.NoActions(state);
        GUI.color=ready&&over?(Input.GetMouseButton(0)?new Color(.8f,.8f,.8f):new Color(1.15f,1.15f,1.15f)):own&&!ready?new Color(.7f,.7f,.7f):Color.white;
        turnButton.Observe(own);
        var texture=turnButton.Render(exhausted?new Color(.55f,1,.4f):Color.white);
        if(texture!=null)GUI.DrawTexture(r,texture,ScaleMode.StretchToFill,true);GUI.color=old;
        if(exhausted)BattlePainter.ButtonGlow(r,new Color(.18f,1,.04f,.95f));
        var labelColor=new Color(.16f,.10f,.04f,turnButton.LabelVisibility);
        if(turnButton.LabelVisibility>.12f)Ink(r,own?"结束回合":"对手回合",labelStyle,labelColor);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&ready&&interactive&&!turnButton.Flipping;
        if(GUI.Button(r,GUIContent.none,GUIStyle.none)){var move=state.moves.FirstOrDefault(m=>m.kind=="end_turn");if(move!=null)SendMove(move);}GUI.enabled=enabled;
    }
    void DebugOverlay()
    {
        Panel(new Rect(10,80,300,430));GUI.Label(new Rect(16,90,288,62),"开发模式 · F3 隐藏\nF4 动画倍速 / F5 跳过",smallStyle);
        GUI.Label(new Rect(16,155,288,55),"v"+state.version+" · turn "+state.turn+"\n队列 "+presentation.QueueCount+" · "+presentation.Speed+"x",smallStyle);
        for(int i=Mathf.Max(0,history.Count-8);i<history.Count;i++)GUI.Label(new Rect(16,230+(i-Mathf.Max(0,history.Count-8))*30,288,29),history[i],smallStyle);
    }
    void CountHint(Rect rect,string heading,string body)
    {
        var texture=assets.Texture("UI_TOOLTIP_FRAME");
        BattlePainter.Shadow(new Rect(rect.x-9,rect.y+8,rect.width+18,rect.height+12),.7f);
        if(texture!=null)GUI.DrawTextureWithTexCoords(rect,texture,new Rect(.18f,.75f,.65f,.245f),true);
        Ink(new Rect(rect.x+18,rect.y+9,rect.width-36,29),heading,new GUIStyle(smallStyle){alignment=TextAnchor.MiddleLeft},Color.white);
        Ink(new Rect(rect.x+18,rect.y+37,rect.width-36,42),body,new GUIStyle(labelStyle){fontSize=21,alignment=TextAnchor.MiddleLeft},Color.white);
    }
    void DrawCountHints(StateView shown)
    {
        if(battleMenu||dragged!=0||selected!=0||mulliganPresentation.Active||shown.phase!="play")return;
        var own=shown.players[shown.seat];var enemy=shown.players[1-shown.seat];
        if(visualTest=="deck_hover_own")ownDeckHover=true;
        if(visualTest=="deck_hover_enemy")enemyDeckHover=true;
        if(visualTest=="hand_hover_enemy")enemyHandHover=true;
        if(ownDeckHover||enemyDeckHover)
        {
            var anchor=ownDeckHover?ownDeckBounds:enemyDeckBounds;var player=ownDeckHover?own:enemy;
            var rect=new Rect(Mathf.Clamp(anchor.x-345,100,1530),Mathf.Clamp(anchor.center.y-49,80,870),330,93);
            CountHint(rect,"牌库",(ownDeckHover?"你的":"对手的")+"牌库中有"+player.deck_count+"张卡牌。");
            if(ownDeckHover){rect.y+=102;CountHint(rect,"手牌","你的手牌中有"+own.hand_count+"张卡牌。");}
        }
        else if(enemyHandHover)
            CountHint(new Rect(1160,85,330,93),"手牌","对手的手牌中有"+enemy.hand_count+"张卡牌。");
    }
    void GameMenuButton()
    {
        var rect=BattleGeometry.EdgeRect(DesignViewport.Battle,true,1014,77,53);
        calibration.Draw("battle.menu","菜单齿轮",rect,()=>
        {
            var image=cardVisuals.Prop("MODEL_GAME_MENU_BUTTON");
            if(rect.Contains(Event.current.mousePosition))interactionVisuals.Draw(rect,image,Color.white,true);
            BattlePainter.Draw(rect,image,Color.white);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))
            {battleMenu=!battleMenu;battleOptions=false;selected=dragged=0;cardPickup.Cancel();PlaySound("SFX_UI_CLICK");}
        });
    }
    bool GameMenuAction(Rect rect,string text,bool red=false)
    {
        var old=GUI.color;bool over=rect.Contains(Event.current.mousePosition);
        GUI.color=over?new Color(1.1f,1.1f,1.1f):Color.white;
        menuButtons.Draw(rect,menuButtons.Image(assets,red));GUI.color=old;
        Ink(rect,text,menuButtonStyle,red?Color.white:new Color(.2f,.12f,.055f));
        bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);if(clicked)PlaySound("SFX_UI_CLICK");return clicked;
    }
    void GameMenu()
    {
        Fill(new Rect(0,0,1920,1080),new Color(0,0,0,.45f));
        var frame=new Rect(710,285,500,510);var texture=assets.Texture("UI_OPTIONS_FRAME");
        if(texture!=null)GUI.DrawTextureWithTexCoords(frame,texture,new Rect(0,.315f,.698f,.685f),true);
        Ink(new Rect(745,313,430,65),battleOptions?"选项":"游戏菜单",titleStyle,new Color(1,.88f,.65f));
        if(battleOptions)
        {
            Ink(new Rect(780,415,360,45),"音乐音量",labelStyle,Color.white);
            music.volume=GUI.HorizontalSlider(new Rect(790,477,340,26),music.volume,0,1);
            Ink(new Rect(780,515,360,45),"音效音量",labelStyle,Color.white);
            sfx.volume=GUI.HorizontalSlider(new Rect(790,577,340,26),sfx.volume,0,1);
            if(GameMenuAction(new Rect(770,657,380,64),"返回"))battleOptions=false;
        }
        else
        {
            if(GameMenuAction(new Rect(770,405,380,70),"选项"))battleOptions=true;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&state.phase=="play"&&!pending&&!connectionLost;
            if(GameMenuAction(new Rect(770,512,380,70),"认输",true)){battleMenu=false;SendMove(new Move{kind="concede"});}GUI.enabled=enabled;
            if(GameMenuAction(new Rect(770,657,380,70),"继续游戏")){battleMenu=false;battleOptions=false;}
        }
        // 吞掉面板内剩余输入；菜单不暂停服务器比赛。
        if(Event.current.isMouse)Event.current.Use();
    }
    bool PointerOver(int entity)
    {
        var shown=presentation.Display??state;
        if(visualTest!="")
        {
            if(shown==null)return false;
            var own=shown.players[shown.seat];
            if(visualTest=="power_hover"||visualTest=="power_used_hover")return own.power.entity==entity;
            if(visualTest=="mulligan_hover")return mulliganPresentation.Cards.Length>0&&mulliganPresentation.Cards[0].entity==entity;
            if(visualTest.StartsWith("minion_hover",StringComparison.Ordinal))return own.field.Length>0&&own.field[visualTest.EndsWith("right")?own.field.Length-1:0].entity==entity;
            return visualTest=="card_hover"&&entity==handPresentation.Hovered;
        }
        var mouse=BattleMouse();
        return calibration.Hit(entity,mouse);
    }
    void PowerDetail(Rect rect,EntityView entity)
    {
        CardDef card;if(!cards.TryGetValue(entity.card_id,out card))return;
        if(NativePowerVisual.SupportsCard(entity.card_id)&&powerVisual!=null)
        {
            var shown=presentation.Display??state;bool enemy=shown!=null&&shown.players[1-shown.seat].power.entity==entity.entity;
            calibration.Draw("power.detail","技能详情整体",rect,new Vector2(rect.width/320,rect.height/480),()=>powerVisual.Draw(rect,entity,card,true,enemy,Color.white,powerVisual.Time,false));
            return;
        }
        var units=new Vector2(rect.width/320,rect.height/480);
        calibration.Draw("power.detail","技能详情整体",rect,units,()=>
        {
            calibration.Draw("power.detail/model","技能详情卡框",rect,units,()=>
            {GUI.DrawTexture(rect,cardVisuals.Render(card,"power_detail"),ScaleMode.StretchToFill,true);cardVisuals.RegisterParts(card,"power_detail",rect);});
            var portrait=BattleInteraction.PowerPortraitRect(rect);
            calibration.Draw("power.detail/portrait","技能详情头像",portrait,units,()=>GUI.DrawTexture(portrait,cardVisuals.Render(card,"power"),ScaleMode.StretchToFill,true));
            var name=new Rect(rect.x+18,rect.y+rect.height*.45f,rect.width-36,rect.height*.15f);
            int size=cardNameStyle.fontSize;cardNameStyle.fontSize=Mathf.RoundToInt(rect.width*.075f);
            calibration.Draw("power.detail/name","技能详情名称",name,units,()=>Ink(name,UIText.PowerName(card),cardNameStyle,Color.white));cardNameStyle.fontSize=size;
            var text=new Rect(rect.x+30,rect.y+rect.height*.68f,rect.width-60,rect.height*.25f);
            size=cardTextStyle.fontSize;cardTextStyle.fontSize=Mathf.RoundToInt(rect.width*.075f);
            string description=UIText.PowerDescription(card,entity).Trim();
            if(!description.StartsWith("<b>英雄技能",StringComparison.Ordinal))description="<b>英雄技能</b>\n"+description;
            calibration.Draw("power.detail/text","技能详情说明",text,units,()=>Ink(text,description,cardTextStyle,new Color(.12f,.075f,.04f)));cardTextStyle.fontSize=size;
            float gem=rect.width*.212f;var cost=new Rect(rect.center.x-gem/2,rect.y-rect.height*.034f,gem,gem*1.05f);
            calibration.Draw("power.detail/cost","技能详情费用",cost,units,()=>Badge(cost,UIText.PowerCost(entity),new Color(.08f,.38f,.72f)));
        });
    }
    void DetailCard(Rect rect,EntityView entity)
    {
        if(entity==null||string.IsNullOrEmpty(entity.card_id))return;
        CardDef card;if(!cards.TryGetValue(entity.card_id,out card))return;
        showingDetail=true;
        try
        {
            var copy=BattleInteraction.Copy(entity);
            if(entity.card_id=="HERO_08bp")copy.entity=entity.entity;
            BattlePainter.Shadow(new Rect(rect.x-12,rect.y+18,rect.width+24,rect.height+8),.9f);
            if(card.Type==10)PowerDetail(rect,copy);
            else if(card.Type==3)Portrait(rect,copy,true);
            else Card(rect,copy,true);
        }
        finally{showingDetail=false;}
    }
    void DrawBattleDetails(StateView shown)
    {
        if(dragged!=0||selected!=0||battleMenu||shown.phase!="play"||presentation.InputLocked)return;
        foreach(var p in shown.players)
        {
            if(PointerOver(p.power.entity)&&locations.ContainsKey(p.power.entity))
            {DetailCard(BattleInteraction.DetailRect(locations[p.power.entity],true),p.power);return;}
            foreach(var e in p.field)
                if(PointerOver(e.entity)&&locations.ContainsKey(e.entity))
                {DetailCard(BattleInteraction.DetailRect(locations[e.entity]),e);return;}
        }
    }
    void DrawBattleHistory(StateView shown,bool preview=false)
    {
        var rect=new Rect(195,166,74,548);
        calibration.Draw("battle.history","操作历史区域",rect,()=>
        {
            var rows=preview?new[]{new BattleHistory.Entry{kind="CardPlay",owner=0,card=EditorEntity("CS2_182",0)},
                new BattleHistory.Entry{kind="PlayHeroPower",owner=1,card=EditorEntity("HERO_01bp",0)},
                new BattleHistory.Entry{kind="Attack",owner=0,card=EditorEntity("CS2_172",0),target=EditorEntity("CS2_120",0)}}:
                battleHistory.Entries.Where(e=>e.version<=shown.version).ToArray();
            if(rows.Length==0)return;
            historyOffset=Mathf.Clamp(historyOffset,0,Mathf.Max(0,rows.Length-8));
            int start=Mathf.Max(0,rows.Length-8-historyOffset),end=Mathf.Min(rows.Length,start+8);
            for(int i=start;i<end;i++)
            {
                var entry=rows[i];var r=new Rect(rect.x,rect.y+(i-start)*67,66,61);
                bool over=preview?false:visualTest=="history_hover"?i==end-1:visualTest==""&&r.Contains(Event.current.mousePosition);
                Texture image;
                CardDef card;if(entry.card!=null&&!string.IsNullOrEmpty(entry.card.card_id)&&cards.TryGetValue(entry.card.card_id,out card))
                    image=cardVisuals.Render(card,(entry.kind=="Attack"?"history_attack_":"history_")+(entry.owner==shown.seat?"own":"enemy"));
                else image=cardVisuals.Prop("MODEL_CARD_BACK");
                BattlePainter.Shadow(new Rect(r.x-5,r.y+5,r.width+10,r.height+6));
                if(over)interactionVisuals.Draw(r,image,Color.white,true);
                GUI.DrawTexture(r,image,ScaleMode.StretchToFill,true);
                if(over&&!battleMenu&&dragged==0&&!mulliganPresentation.Active)
                {
                    var m=BattleLayout.Matrix.inverse*GUI.matrix;var anchor=r;anchor.center=m.MultiplyPoint3x4(r.center);
                    var matrix=GUI.matrix;GUI.matrix=BattleLayout.Matrix;
                    var detail=new Rect(Mathf.Clamp(anchor.xMax+28,110,1450),Mathf.Clamp(anchor.y-40,35,600),292,438);
                    if(entry.card!=null&&!string.IsNullOrEmpty(entry.card.card_id))DetailCard(detail,entry.card);
                    else Ink(new Rect(detail.x,detail.y+100,292,70),"未揭示的奥秘",labelStyle,Color.white);
                    if(entry.kind=="Attack"&&entry.target!=null)DetailCard(new Rect(detail.xMax+22,detail.y+40,200,300),entry.target);
                    GUI.matrix=matrix;
                }
            }
            if(rows.Length>8)Ink(new Rect(rect.x-5,rect.yMax,90,30),"滚轮查看",smallStyle,new Color(.8f,.8f,.75f));
            if(!OpeningActive&&!preview&&Event.current.type==EventType.ScrollWheel&&rect.Contains(Event.current.mousePosition))
            {historyOffset=Mathf.Clamp(historyOffset+Mathf.RoundToInt(Event.current.delta.y),0,Mathf.Max(0,rows.Length-8));Event.current.Use();}
        });
    }
    void Ink(Rect r,string value,GUIStyle style,Color color)
    {
        UIText.Draw(r,value,style,color);
    }
    void DrawAsset(Rect r,string id)
    {
        Texture t=id=="UI_CARD_BACK"?cardVisuals.Prop("MODEL_CARD_BACK"):id=="UI_MANA"?cardVisuals.Prop("MODEL_GEM_MANA"):assets.Texture(id);if(t==null)return;
        string key=id=="UI_CARD_BACK"?"card_back":"mana/model";var units=id=="UI_CARD_BACK"?new Vector2(r.width/320,r.height/480):new Vector2(r.width/64,r.height/64);
        if(id=="UI_MANA")calibration.Draw("mana","水晶整体",r,units,()=>calibration.Draw(key,"法力水晶",r,units,()=>GUI.DrawTexture(r,t,ScaleMode.StretchToFill,true)));
        else calibration.Draw(key,"卡背",r,units,()=>GUI.DrawTexture(r,t,ScaleMode.StretchToFill,true));
    }
    void Badge(Rect r,int n,Color c,string assetId=null,Color? numberColor=null)
    {
        string id=assetId??(c.b>.4f?"MODEL_GEM_MANA":c.g>.5f?"MODEL_GEM_ATTACK":"MODEL_GEM_HEALTH");
        string key=id=="MODEL_GEM_MANA"?"mana":id=="MODEL_GEM_ATTACK"?"attack":id=="MODEL_GEM_ARMOR"?"armor":id=="MODEL_GEM_DURABILITY"?"durability":"health";
        var units=new Vector2(r.width/64,r.height/64);
        calibration.Draw(key,"宝石整体",r,units,()=>
        {
            calibration.Draw(key+"/model","宝石模型",r,units,()=>GUI.DrawTexture(r,cardVisuals.Prop(id),ScaleMode.StretchToFill,true));
            calibration.Draw(key+"/number","宝石数字",r,units,()=>
            {
                int size=numberStyle.fontSize;numberStyle.fontSize=Mathf.RoundToInt(r.height*.8f);
                UIText.Draw(new Rect(r.x,r.y-1,r.width,r.height),n.ToString(),numberStyle,numberColor??Color.white,key+"/number",true);numberStyle.fontSize=size;
            });
        });
    }
    void Card(Rect r,EntityView e,bool full)
    {
        if(e==null || String.IsNullOrEmpty(e.card_id)) return;
        var c=cards.ContainsKey(e.card_id)?cards[e.card_id]:new CardDef{CardId=e.card_id,Name=e.card_id,Text="",Type=4};
        string template=UICalibration.CardTemplate(c);var units=new Vector2(r.width/320,r.height/480);
        calibration.Draw(template,"卡牌整体",r,units,()=>CardContent(r,e,c,template,units),e.entity);
    }
    void CardContent(Rect r,EntityView e,CardDef c,string template,Vector2 units)
    {
        bool opening=mulliganPresentation.Interactable&&!mulligan.Contains(e.entity)&&!pending;
        bool ready=!showingDetail&&!uiEditor.Active&&(opening||(!mulliganPresentation.Active&&!pending&&!presentation.InputLocked&&BattleInteraction.Ready(state,e.entity,"play")));
        bool over=!showingDetail&&!uiEditor.Active&&PointerOver(e.entity)&&(!mulliganPresentation.Active||opening);
        calibration.Draw(template+"/model","卡框 / 模型整体",r,units,()=>
        {
            var image=cardVisuals.Render(c);
            if(ready)interactionVisuals.Draw(r,image,opening?new Color(.16f,1,.03f,.95f):BattleInteraction.PlayGlow(e));
            if(over)interactionVisuals.Draw(r,image,Color.white,true);
            GUI.DrawTexture(r,image,ScaleMode.StretchToFill,true);cardVisuals.RegisterParts(c,"card",r);
            if(calibration.Collecting)calibration.Register(template+"/art","原画纹理 · UV 翻转 / 旋转",new Rect(r.x+r.width*.15f,r.y+r.height*.14f,r.width*.7f,r.height*.38f),units);
        });
        var old=GUI.color;var nameRect=new Rect(r.x+12,r.y+r.height*.47f,r.width-24,r.height*.15f);
        cardNameStyle.fontSize=Mathf.RoundToInt(r.width*.078f);cardNameStyle.wordWrap=false;
        calibration.Draw(template+"/name","卡牌名称",nameRect,units,()=>
        {
            Ink(nameRect,c.Name,cardNameStyle,Color.white);
        });
        var textRect=new Rect(r.x+16,r.y+r.height*.67f,r.width-32,r.height*.27f);
        calibration.Draw(template+"/text","规则文字",textRect,units,()=>
        {
            cardTextStyle.fontSize=Mathf.RoundToInt(r.width*.062f);
            Ink(textRect,UIText.CardDescription(c.Text),cardTextStyle,new Color(.13f,.08f,.04f));
        });
        float gem=r.width*.23f;
        var cost=new Rect(r.x-4,r.y-5,gem,gem);calibration.Draw(template+"/cost","法力消耗",cost,units,()=>Badge(cost,e.cost,new Color(.08f,.38f,.72f)));
        if(c.Type==4||c.Type==7)
        {
            var attack=new Rect(r.x-3,r.yMax-gem,gem,gem);var health=new Rect(r.xMax-gem,r.yMax-gem,gem,gem);
            calibration.Draw(template+"/attack","攻击数值",attack,units,()=>Badge(attack,e.attack,new Color(.86f,.62f,.14f),null,UIText.AttackColor(e.attack,c.Attack,e.attack_buffed)));
            calibration.Draw(template+"/health",c.Type==7?"耐久数值":"生命数值",health,units,()=>Badge(health,c.Type==7?e.durability:e.health,new Color(.74f,.13f,.08f),c.Type==7?"MODEL_GEM_DURABILITY":null,StatTint(e,c,c.Type==7)));
        }
    }
    void Portrait(Rect r,EntityView e,bool hero)
    {
        var c=cards.ContainsKey(e.card_id)?cards[e.card_id]:new CardDef{CardId=e.card_id,Type=4};
        string template=hero?"hero":"minion";var units=new Vector2(r.width/320,r.height/480);
        calibration.Draw(template,hero?"英雄整体":"场上随从整体",r,units,()=>PortraitContent(r,e,hero,c,template,units),e.entity);
    }
    void PortraitContent(Rect r,EntityView e,bool hero,CardDef c,string template,Vector2 units)
    {
        var live=showingDetail||calibration.Collecting?null:nativeActors.Find(e);
        var modelRect=r;
        if(live!=null){if(!live.PresentationVisible)return;r=live.StatusRect(r);}
        calibration.Draw(template+"/shadow","阴影",r,units,()=>BattlePainter.Shadow(new Rect(r.x-10,r.y+15,r.width+20,r.height+9),.85f));
        if(!hero&&(calibration.Collecting||e.keywords!=null&&e.keywords.Contains("taunt")))
        {
            var shield=new Rect(r.x-r.width*.11f,r.y-r.height*.06f,r.width*1.22f,r.height*1.13f);
            calibration.Draw("minion/taunt","原版嘲讽盾牌模型",shield,units,()=>BattlePainter.Draw(shield,cardVisuals.Prop("MODEL_MINION_TAUNT"),Color.white));
        }
        bool ready=!showingDetail&&!pending&&!presentation.InputLocked&&BattleInteraction.Ready(state,e.entity,"attack");
        bool over=!showingDetail&&!uiEditor.Active&&PointerOver(e.entity)&&!mulliganPresentation.Active;
        bool originalHighlight=!hero&&live!=null&&!showingDetail&&!calibration.Collecting;
        if(originalHighlight&&Event.current.type==EventType.Repaint)
        {
            bool own=(presentation.Display??state).players[(presentation.Display??state).seat].field.Any(a=>a.entity==e.entity);
            bool validPowerTarget=BattleInteraction.IsLegalTarget(state,selected,e.entity);
            originalHighlight=nativeActors.Highlight(e,assets,validPowerTarget?(over?27:26):NativeBattleActors.HighlightState(own,ready,over,selected==e.entity));
        }
        if(!originalHighlight&&selected==e.entity)BattlePainter.Glow(new Rect(r.x-25,r.y-25,r.width+50,r.height+50),new Color(1,.75f,.1f,.7f));
        var old=GUI.color;float hit=presentation.Tint(e.entity);GUI.color*=Color.Lerp(Color.white,new Color(1,.25f,.15f),hit);
        if(e.keywords!=null&&e.keywords.Contains("stealthed"))GUI.color*=new Color(.65f,.75f,.85f,.6f);
        calibration.Draw(template+"/model","头像 / 边框整体",modelRect,units,()=>
        {
            var image=live==null?cardVisuals.Render(c,hero?"hero":"minion"):live.Render();
            var imageRect=live==null?modelRect:live.ImageRect(modelRect);
            if(!originalHighlight&&ready)interactionVisuals.Draw(imageRect,image,new Color(.16f,1,.03f,.95f));
            if(!originalHighlight&&over)interactionVisuals.Draw(imageRect,image,Color.white,true);
            GUI.DrawTexture(imageRect,image,ScaleMode.StretchToFill,true);cardVisuals.RegisterParts(c,hero?"hero":"minion",modelRect);
            if(calibration.Collecting)calibration.Register(template+"/art","原画纹理 · UV 翻转 / 旋转",r,units);
        });
        GUI.color=old;float gem=r.width*.29f;
        var health=new Rect(r.xMax-gem*.86f,r.yMax-gem*.85f,gem,gem);calibration.Draw(template+"/health","生命数值",health,units,()=>Badge(health,Mathf.Max(0,e.health),new Color(.72f,.08f,.04f),null,StatTint(e,c)));
        if(!hero){var attack=new Rect(r.x-3,r.yMax-gem*.85f,gem,gem);calibration.Draw(template+"/attack","攻击数值",attack,units,()=>Badge(attack,e.attack,new Color(.85f,.61f,.11f),null,UIText.AttackColor(e.attack,c.Attack,e.attack_buffed)));}
        if(e.keywords!=null)
        {
            if(e.keywords.Contains("divine_shield"))BattlePainter.Glow(new Rect(r.x-15,r.y-15,r.width+30,r.height+30),new Color(1,.88f,.22f,.48f));
            if(e.keywords.Contains("frozen"))BattlePainter.Ring(new Rect(r.x-9,r.y-9,r.width+18,r.height+18),new Color(.3f,.8f,1,.9f));
            if(e.keywords.Contains("silenced"))Ink(new Rect(r.x+5,r.y+4,25,30),"×",smallStyle,new Color(.85f,.85f,.85f));
            var badges=MinionStatusVisuals.Badges(e.keywords,calibration.Collecting);
            for(int i=0;!hero&&i<badges.Length;i++)
            {
                string status=badges[i];var trigger=MinionStatusVisuals.Badge(r,i,badges.Length);
                calibration.Draw(template+"/"+status,status=="trigger"?"扳机闪电 · 实际触发高亮":status=="deathrattle"?"亡语标志":"剧毒标志",trigger,units,()=>
                {
                    Texture image=cardVisuals.Prop(status=="trigger"?"MODEL_MINION_TRIGGER":status=="deathrattle"?"MODEL_MINION_DEATHRATTLE":"MODEL_MINION_POISONOUS");
                    float pulse=status=="trigger"?presentation.TriggerPulse(e.entity):0;
                    if(pulse>0)
                    {
                        var flash=MinionStatusVisuals.Flash(trigger,pulse);
                        interactionVisuals.Draw(flash,image,new Color(1,1,.2f,pulse));
                        BattlePainter.Glow(new Rect(trigger.center.x-42,trigger.center.y-42,84,84),new Color(1,.87f,.12f,pulse*.85f));
                        BattlePainter.Draw(flash,image,new Color(2,1.85f,.8f,pulse));
                    }
                    BattlePainter.Draw(trigger,image,Color.white);
                });
            }
        }
        if(!hero&&e.asleep&&!showingDetail)
        {
            var sleep=new Rect(r.center.x,r.y-35,r.width*.45f,85);
            calibration.Draw(template+"/sleep","睡觉粒子 · 召唤疲劳",sleep,units,()=>
            {
                var texture=assets.Texture("VFX_SLEEP_Z");
                for(int i=0;i<3;i++)
                {
                    float t=Mathf.Repeat(Time.unscaledTime*.6f+i/3f,1),size=Mathf.Lerp(15,29,t);
                    var position=new Vector2(sleep.x+Mathf.Sin(t*3)*10,sleep.yMax-t*sleep.height);
                    BattlePainter.Draw(new Rect(position.x,position.y,size,size),texture,new Color(1,1,1,Mathf.Sin(t*Mathf.PI)));
                }
            });
        }
    }
    static Color StatTint(EntityView entity,CardDef card,bool weapon=false)
    {
        int maximum=weapon?entity.max_durability:entity.max_health,printed=weapon?card.Durability:card.Health;
        if(maximum<=0)maximum=printed>0?printed:30;
        return UIText.StatColor(weapon?entity.durability:entity.health,maximum,(weapon?entity.durability_buffed:entity.health_buffed)||maximum>printed&&printed>0);
    }
    void Select(int entity,string kind)
    {
        var options=state.moves.Where(m=>m.entity==entity&&m.kind==kind).ToArray();if(options.Length==0)return;
        if(options.Length==1&&options[0].target==0&&String.IsNullOrEmpty(options[0].choice))SendMove(options[0]);
        else{selected=entity;if(kind=="power")nativeEffects?.SelectPower(state,entity);}
    }
    void Inputs()
    {
        if(playChoices!=null)
        {
            Panel(new Rect(430,350,740,220));GUI.Label(new Rect(440,370,720,50),"抉择",titleStyle);
            for(int i=0;i<playChoices.Length;i++)
            {
                var move=playChoices[i];var name=cards.ContainsKey(move.choice)?cards[move.choice].Name:move.choice;
                if(Button(new Rect(460+i*350,450,330,65),name)){playChoices=null;SendMove(move);return;}
            }
            return;
        }
        var e=Event.current;if(HandleBattleInput(e.type,e.button,BattleMouse()))e.Use();
    }
    Vector2 BattleMouse()
    {return EvidencePointer??(Vector2)BattleLayout.Matrix.inverse.MultiplyPoint3x4(new Vector3(Input.mousePosition.x,Screen.height-Input.mousePosition.y,0));}
    bool HandleBattleInput(EventType type,int button,Vector2 mouse)
    {
        if(OpeningActive||battleMenu||state==null||pending||presentation.InputLocked||mulliganPresentation.Active)return false;
        if(dragged==0)placementIndex=-1;
        if(type==EventType.MouseDrag&&button==0)cardPickup.Track(mouse);
        if(type==EventType.MouseDown&&button==1){selected=dragged=0;placementIndex=-1;cardPickup.Cancel();nativeEffects?.CancelPowerSelection(true);return true;}
        if(type==EventType.MouseDown&&button==0)
        {
            if(dragged!=0)
            {
                if(TryDropCard(mouse)){cardPickup.Cancel();dragged=0;placementIndex=-1;}else if(!TargetedHeld()){selected=dragged=0;cardPickup.Cancel();}
                return true;
            }
            int hit=locations.Where(kv=>calibration.Hit(kv.Key,mouse)).Select(kv=>kv.Key).LastOrDefault();
            if(selected!=0&&hit!=0){var options=state.moves.Where(m=>m.entity==selected&&m.target==hit).ToArray();if(options.Length>0){ChooseMove(options);return true;}}
            if(hit==state.players[state.seat].power.entity&&state.moves.Any(m=>m.entity==hit&&m.kind=="power"))
            {
                if(selected==hit){selected=0;nativeEffects?.CancelPowerSelection(true);}
                else Select(hit,"power");
                return true;
            }
            if(handPresentation.Hovered!=0)hit=handPresentation.Hovered;
            if(hit!=0){if(state.players[state.seat].hand.Any(c=>c.entity==hit)){nativeEffects?.CancelPowerSelection(true);dragged=hit;selected=hit;cardPickup.Begin(hit,mouse);PlaySound("SFX_CARD_PICKUP");return true;}else if(state.moves.Any(m=>m.kind=="attack"&&m.entity==hit)){nativeEffects?.CancelPowerSelection(true);selected=hit;}}
        }
        if(type==EventType.MouseUp&&button==0&&dragged!=0)
        {
            if(cardPickup.IsDrag(mouse))
            {
                bool played=TryDropCard(mouse);
                if(played||placementIndex<0){if(!played)selected=0;dragged=0;placementIndex=-1;cardPickup.Cancel();}
            }
            return true;
        }
        if(type==EventType.MouseUp&&button==0&&selected!=0&&dragged==0)
        {
            int hit=locations.Where(kv=>calibration.Hit(kv.Key,mouse)).Select(kv=>kv.Key).LastOrDefault();
            var move=state.moves.FirstOrDefault(m=>m.entity==selected&&m.target==hit&&(m.kind=="attack"||m.kind=="power"));
            if(move!=null) {SendMove(move);return true;}
        }
        return false;
    }
    bool TargetedSpell(int entity)
    {
        if(entity==0||state==null)return false;var held=state.players[state.seat].hand.FirstOrDefault(c=>c.entity==entity);CardDef card;
        return held!=null&&cards.TryGetValue(held.card_id,out card)&&card.Type==5&&BattleInteraction.PlayRequiresTarget(state,entity);
    }
    bool TargetedHeld(){return placementIndex>=0||BattleInteraction.PlayOffersTarget(state,dragged);}
    bool TryDropCard(Vector2 mouse)
    {
        int hit=locations.Where(kv=>kv.Key!=dragged&&calibration.Hit(kv.Key,mouse)).Select(kv=>kv.Key).LastOrDefault();
        int position=state.players[state.seat].field.Count(c=>locations.ContainsKey(c.entity)&&locations[c.entity].center.x<mouse.x);
        var entity=state.players[state.seat].hand.FirstOrDefault(c=>c.entity==dragged);if(entity==null)return false;
        int cardType=cards.ContainsKey(entity.card_id)?cards[entity.card_id].Type:0;
        bool minion=cardType==4;
        var legal=state.moves.Where(m=>m.kind=="play"&&m.entity==dragged).ToArray();
        if((minion||cardType==7)&&placementIndex<0&&BattleInteraction.PlayOffersTarget(state,dragged))
        {
            if(BattleLayout.PlayZone.Contains(mouse)&&legal.Any(m=>!minion||m.index==position))
            {placementIndex=minion?position:0;placementPoint=mouse;cardPickup.Cancel();}
            return false;
        }
        if(placementIndex>=0)position=placementIndex;
        var options=state.moves.Where(m=>m.kind=="play"&&m.entity==dragged&&m.target==hit&&(!minion||m.index==position)).ToArray();
        bool targeted=TargetedHeld();
        if(!CardPickup.DropAllowed(mouse,targeted,options.Length>0))return false;
        if(options.Length==0&&!targeted)options=state.moves.Where(m=>m.kind=="play"&&m.entity==dragged&&m.target==0&&(!minion||m.index==position)).ToArray();
        if(options.Length==0)return false;ChooseMove(options);return true;
    }
    void ChooseMove(Move[] options)
    {
        var branches=options.GroupBy(m=>m.choice??"").Select(g=>g.First()).ToArray();
        if(branches.Length>1)playChoices=branches;else SendMove(options[0]);
    }
    void Arrow(int entity)
    {
        if(!locations.ContainsKey(entity))return;var from=locations[entity].center;var to=visualTest=="hero_attack_select"||visualTest=="attack_arrow"||visualTest.EndsWith("target_select")?new Vector2(960,260):BattleMouse();
        if(dragged!=0&&placementIndex<0)from=HandLayout.OwnPose(Array.FindIndex(state.players[state.seat].hand,c=>c.entity==entity),state.players[state.seat].hand.Length,calibration).center;
        if(targetArrow==null)targetArrow=new NativeTargetArrow(assets);
        int hit=locations.Where(pair=>pair.Key!=dragged&&calibration.Hit(pair.Key,to)).Select(pair=>pair.Key).LastOrDefault();
        bool legal=BattleInteraction.IsLegalTarget(state,entity,hit);
        targetArrow.Show(from,to,legal);targetArrow.Draw();
    }
    void Mulligan()
    {
        Fill(new Rect(0,0,BattleLayout.Width,BattleLayout.Height),new Color(0,0,0,mulliganPresentation.Darkness));
        calibration.Draw("mulligan","换牌整体",new Rect(420,150,1080,810),MulliganContent);
    }
    void MulliganContent()
    {
        Color old=GUI.color;GUI.color=new Color(1,1,1,mulliganPresentation.ChromeAlpha);
        var banner=cardVisuals.Prop("MODEL_MULLIGAN_BANNER");
        var bannerRect=new Rect(632,180,656,157);if(banner!=null)calibration.Draw("mulligan/banner","换牌标题卷轴",bannerRect,()=>GUI.DrawTexture(bannerRect,banner,ScaleMode.StretchToFill,true));
        var title=new Rect(680,195,560,78);calibration.Draw("mulligan/title","换牌标题文字",title,()=>Ink(title,"选择起始手牌",titleStyle,new Color(.3f,.15f,.045f)));
        var opening=mulliganPresentation.Cards;
        calibration.Draw("mulligan/cards","起手卡牌区域",new Rect(396,332,1128,378),()=>
        {
        for(int i=0;i<opening.Length;i++)
        {
            var baseRect=BattleLayout.Mulligan(i,opening.Length);
            int slot=i;calibration.Draw("mulligan/slot."+i,"起手卡牌位置 "+(i+1),baseRect,()=>
            {
            var template=UICalibration.CardTemplate(cards[opening[slot].card_id]);
            var hit=calibration.Register(template,"起手卡牌交互",baseRect,new Vector2(baseRect.width/320,baseRect.height/480));
            bool over=visualTest==""?hit.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)):PointerOver(opening[slot].entity);
            var r=mulliganPresentation.Rect(slot,over);
            BattlePainter.Shadow(new Rect(r.x-15,r.y+22,r.width+30,r.height+20),.95f);Card(r,opening[slot],true);
            if(mulligan.Contains(opening[i].entity)&&!mulliganPresentation.Confirmed)
            {
                var mark=assets.Texture("UI_MULLIGAN_MARK");BattlePainter.Draw(new Rect(r.x+38,r.y+105,r.width-76,r.width-76),mark,Color.white);
                Ink(new Rect(r.x-5,r.yMax+2,r.width+10,49),"替换",labelStyle,new Color(1,.3f,.22f));
            }
            if(!battleMenu&&mulliganPresentation.Interactable&&!pending&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&PointerOver(opening[slot].entity))
            {if(!mulligan.Add(opening[slot].entity))mulligan.Remove(opening[slot].entity);PlaySound("SFX_MULLIGAN_SELECT");Event.current.Use();}
            });
        }
        var outgoing=mulliganPresentation.OldCards;
        for(int i=0;i<outgoing.Length;i++)
            if(!opening.Any(e=>e.entity==outgoing[i].entity)&&mulliganPresentation.Exchange<.42f)
                Card(mulliganPresentation.OldRect(i),outgoing[i],true);
        });
        bool active=!battleMenu&&mulliganPresentation.Interactable&&!pending&&state.moves.Any(m=>m.kind=="mulligan");
        var confirm=new Rect(816,827,288,87);BattlePainter.Glow(new Rect(804,815,312,110),new Color(1,.65f,.12f,active?.28f:.1f));
        calibration.Draw("mulligan/confirm","换牌确认按钮",confirm,()=>
        {
        var button=cardVisuals.Prop("MODEL_MULLIGAN_BUTTON");BattlePainter.Draw(confirm,button,active?Color.white:new Color(.5f,.5f,.5f));
        Ink(confirm,mulliganPresentation.Confirmed?"等待对手":"确认",labelStyle,new Color(1,.9f,.7f));
        if(active&&GUI.Button(confirm,GUIContent.none,GUIStyle.none))
        {PlaySound("SFX_MULLIGAN_CONFIRM");SendMove(new Move{kind="mulligan",cards=mulligan.ToArray()});mulligan.Clear();}
        });
        GUI.color=old;
    }
    void Choices()
    {
        Panel(new Rect(300,280,1000,410));for(int i=0;i<state.choices.Length;i++){var c=state.choices[i];var r=new Rect(800-state.choices.Length*105+i*210,320,180,270);Card(r,c,true);if(GUI.Button(r,GUIContent.none,GUIStyle.none))SendMove(new Move{kind="choose",entity=c.entity});}
    }
    void Result()
    {
        Fill(new Rect(0,0,1920,1080),new Color(0,0,0,.62f*lifecycle.ResultAlpha));var result=state.players[state.seat].result;
        var old=GUI.color;GUI.color*=new Color(1,1,1,lifecycle.ResultAlpha);
        BattlePainter.Glow(new Rect(660,200,600,600),result=="WON"?new Color(1,.7f,.08f,.8f):new Color(.35f,.45f,.8f,.6f));
        var hero=BattleInteraction.Copy(state.players[state.seat].hero);hero.entity=-999;hero.health=Mathf.Max(1,hero.health);var r=lifecycle.ResultHero(new Rect(830,290,260,322));
        BattlePainter.Draw(r,cardVisuals.Render(cards[hero.card_id],"hero"),Color.white);
        if(result=="WON")BattlePainter.Draw(new Rect(840,188,240,116),cardVisuals.Prop("MODEL_RESULT_CROWN"),Color.white);
        var banner=cardVisuals.Prop("MODEL_RESULT_BANNER");
        if(banner!=null)GUI.DrawTexture(new Rect(645,585,630,146),banner,ScaleMode.StretchToFill,true);
        Ink(new Rect(660,596,600,110),result=="WON"?"胜利！":result=="TIED"?"平局":"失败",titleStyle,new Color(1,.91f,.64f));
        GUI.color=old;
        if(lifecycle.ResultAlpha>.99f&&Button(new Rect(790,798,340,72),"返回主界面"))ReturnMenu();
    }
    void Effects()
    {
        try{
        presentation.Draw(locations,(r,e)=>{CardDef card;if(cards.TryGetValue(e.card_id,out card)&&card.Type==7)WeaponVisual(r,e);else Portrait(r,e,card!=null&&card.Type==3);},(r,e)=>Card(r,e,true),
            (r,t)=>{int amount;if((t.StartsWith("+")||t.StartsWith("-"))&&Int32.TryParse(t,out amount))UIText.Draw(r,t,numberStyle,GUI.color,null,true);else Ink(r,t,t=="你的回合"?titleStyle:numberStyle,GUI.color);},
            (r,id,c)=>{if(id=="UI_CARD_GLOW")BattlePainter.Glow(r,c);else if(id.StartsWith("VFX_",StringComparison.Ordinal))spellParticles.Draw(r,assets.Texture(id),c,id=="VFX_COLD_BLOOD_FURY",CardEffectRegistry.Sprite(id));else BattlePainter.Draw(r,assets.Texture(id),c);});
        }finally{nativeEffects.EndFrame();}
    }
    IEnumerator VisualScenario()
    {
        yield return new WaitForSeconds(2);
        var path=Path.Combine(dataRoot,"visual/states/"+visualTest+"-after.json");
        if(File.Exists(path))
        {
            previous=state;state=JsonUtility.FromJson<StateView>(File.ReadAllText(path));
            presentation.Accept(previous,state,locations);mulliganPresentation.Observe(previous,state);
            float captureAt=presentation.SampleTime(visualTest);
            presentation.Paused=true;presentation.Seek(captureAt);
            yield return null;yield return Capture(visualTest+"-action");presentation.Paused=false;
            while(presentation.Busy||mulliganPresentation.Active)yield return null;
        }
        yield return Capture(visualTest);
        if(Flag("--visual-exit")){yield return new WaitForSeconds(.15f);Application.Quit();}
    }
    void EditorViewport(bool scene,Rect viewport)
    {
        boardScene.SetActive(scene);menuScene.SetActive(false);
        if(!scene){cameraView.rect=new Rect(0,0,1,1);return;}
        var matrix=DesignViewport.Fit(1920,1080);var min=matrix.MultiplyPoint3x4(new Vector2(viewport.x,viewport.y));var max=matrix.MultiplyPoint3x4(new Vector2(viewport.xMax,viewport.yMax));
        boardPresentation.Show(Vector2.zero,new DesignViewport(Screen.width,Screen.height,1920,1080,1920));
        var projection=cameraView.projectionMatrix;
        cameraView.rect=new Rect(min.x/Screen.width,1-max.y/Screen.height,(max.x-min.x)/Screen.width,(max.y-min.y)/Screen.height);
        // 编辑器画布本来就等比；改变相机 viewport 后保留设计投影，避免重复适配。
        cameraView.projectionMatrix=projection;
    }
    EntityView EditorEntity(string id,int entity)
    {
        var c=cards.ContainsKey(id)?cards[id]:cards["CS2_182"];
        return new EntityView{card_id=c.CardId,entity=entity,cost=c.Cost,attack=c.Attack,health=c.Health==0?30:c.Health,durability=c.Durability,keywords=new string[0]};
    }
    void EditorPreview(string view,string cardId,int count,int fieldCount)
    {
        bool enabled=GUI.enabled;
        try
        {
            if(view.StartsWith("card.",StringComparison.Ordinal))Card(new Rect(730,160,460,690),EditorEntity(cardId,-1),true);
            else if(view=="minion"||view=="hero")
            {var entity=EditorEntity(cardId,-1);entity.asleep=view=="minion";Portrait(new Rect(725,230,470,585),entity,view=="hero");}
            else if(view=="power")PowerVisual(new Rect(745,310,430,444),EditorEntity(cardId,-1),Color.white);
            else if(view=="weapon")WeaponVisual(new Rect(745,310,430,430),EditorEntity(cardId,-1));
            else if(view=="power.detail")PowerDetail(new Rect(730,160,460,690),EditorEntity(cardId,-1));
            else if(view=="card_back")DrawAsset(new Rect(750,210,420,630),"UI_CARD_BACK");
            else if(view=="quests")DrawF026QuestPage(true);
            else if(view=="mana"||view=="attack"||view=="health")Badge(new Rect(780,360,360,360),view=="mana"?5:view=="attack"?4:7,view=="mana"?new Color(.08f,.38f,.72f):view=="attack"?new Color(.86f,.62f,.14f):new Color(.74f,.13f,.08f));
            else if(view=="armor"||view=="durability")Badge(new Rect(780,360,360,360),view=="armor"?8:3,Color.white,view=="armor"?"MODEL_GEM_ARMOR":"MODEL_GEM_DURABILITY");
            else EditorBattle(view,count,fieldCount);
        }
        finally{GUI.enabled=enabled;}
    }
    void EditorBattle(string view,int count,int fieldCount)
    {
        if(editorState==null)
        {
            editorState=new StateView{seat=0,active=0,phase="play",moves=new Move[0],players=new PlayerView[2]};
            for(int i=0;i<2;i++)editorState.players[i]=new PlayerView{name=i==0?"Shrink1":"Shrink2",seat=i,mana=5,max_mana=10,deck_count=26,
                hero=EditorEntity(i==0?"HERO_08":"HERO_01",-100-i*100),power=EditorEntity(i==0?"HERO_08bp":"HERO_01bp",-101-i*100),
                weapon=EditorEntity("EX1_567",-102-i*100),hand=new EntityView[0],field=new EntityView[0]};
        }
        boardPresentation.RegisterEditorTargets();
        for(int i=0;i<2;i++)
        {
            var p=editorState.players[i];bool own=i==0;p.hero.armor=5;p.hero.attack=3;p.hand_count=count;
            HeroArea(p,own,false);PowerArea(p,own,false);WeaponArea(p,own);Deck(p,own);Mana(p,own);
            var zone=new Rect(440,own?BattleLayout.OwnMinionY:BattleLayout.EnemyMinionY,1040,172);
            calibration.Draw("battle.minions."+(own?"own":"enemy"),own?"己方随从区域":"敌方随从区域",zone,()=>
            {
                for(int j=0;j<fieldCount;j++)Portrait(BattleLayout.Minion(own,j,fieldCount),EditorEntity(j%2==0?"CS2_182":"CS2_120",-110-i*100-j),false);
            });
        }
        calibration.Register("battle.hand.own","己方手牌区域",new Rect(520,910,880,276),Vector2.one);
        calibration.Register("battle.hand.enemy","敌方手牌区域",new Rect(650,-125,620,230),Vector2.one);
        for(int i=0;i<count;i++)
        {
            var matrix=GUI.matrix;var pose=calibration.Pose("battle.hand.enemy",HandLayout.Pose(i,count,true),new Vector2(960,-10));
            BattleGeometry.Rotate(pose.angle,pose.center);DrawAsset(pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight),"UI_CARD_BACK");GUI.matrix=matrix;
            if(view!="mulligan")
            {
                pose=HandLayout.OwnPose(i,count,calibration);BattleGeometry.Rotate(pose.angle,pose.center);
                Card(pose.Rect(HandLayout.CardWidth,HandLayout.CardHeight),EditorEntity(i%3==0?"CS2_029":i%3==1?"CS2_182":"EX1_567",-310-i),true);GUI.matrix=matrix;
            }
        }
        EndTurn(editorState,false);
        DrawBattleHistory(editorState,true);
        if(view=="mulligan")
        {
            Fill(new Rect(0,0,1920,1080),new Color(0,0,0,.32f));
            calibration.Draw("mulligan","换牌整体",new Rect(420,150,1080,810),()=>
            {
                var banner=new Rect(632,180,656,157);calibration.Draw("mulligan/banner","换牌标题卷轴",banner,()=>GUI.DrawTexture(banner,cardVisuals.Prop("MODEL_MULLIGAN_BANNER"),ScaleMode.StretchToFill,true));
                var title=new Rect(680,195,560,78);calibration.Draw("mulligan/title","换牌标题文字",title,()=>Ink(title,"选择起始手牌",titleStyle,new Color(.3f,.15f,.045f)));
                calibration.Draw("mulligan/cards","起手卡牌区域",new Rect(396,332,1128,378),()=>
                {
                    for(int i=0;i<4;i++){var r=BattleLayout.Mulligan(i,4);int slot=i;calibration.Draw("mulligan/slot."+i,"起手卡牌位置 "+(i+1),r,()=>Card(r,EditorEntity(slot==0?"CS2_029":slot==3?"EX1_567":"CS2_182",-410-slot),true));}
                });
                var confirm=new Rect(816,827,288,87);calibration.Draw("mulligan/confirm","换牌确认按钮",confirm,()=>{GUI.DrawTexture(confirm,cardVisuals.Prop("MODEL_MULLIGAN_BUTTON"),ScaleMode.StretchToFill,true);Ink(confirm,"确认",labelStyle,new Color(1,.9f,.7f));});
            });
        }
    }
    bool resourcesReleased;
    void OnApplicationQuit(){ReleaseResources();}
    void OnApplicationPause(bool paused){frameClock.Reset();}
    IEnumerator CaptureUIEditor()
    {
        yield return new WaitForSeconds(2);
        yield return Capture("ui-"+uiEditor.View);
        if(Flag("--ui-exit")){yield return new WaitForSeconds(.2f);Application.Quit();}
    }
    void OnDestroy(){ReleaseResources();}
    void ReleaseResources(){if(resourcesReleased)return;resourcesReleased=true;battleOpening?.Clear();heroSpeech?.Clear();connection?.Dispose();f024ReplayPeerConnection?.Dispose();f024AccountGateway?.Dispose();nativeHubWidgets?.Dispose();nativeHubScene?.Dispose();f032PageTurn?.Dispose();if(f032PageSnapshot!=null){RenderTexture.ReleaseTemporary(f032PageSnapshot);f032PageSnapshot=null;}powerVisual?.Dispose();nativeWeapons?.Clear();nativeMinionSummonVoices?.Clear();nativeShaderTime.Shutdown();if(uiEditor!=null)uiEditor.Dispose();if(assetLoader!=null)assetLoader.Dispose();if(cardVisuals!=null)cardVisuals.Clear();if(interactionVisuals!=null)interactionVisuals.Dispose();if(spellParticles!=null)spellParticles.Dispose();if(targetArrow!=null)targetArrow.Dispose();if(nativeEffects!=null)nativeEffects.Dispose();if(nativeSummons!=null)nativeSummons.Dispose();if(nativeShaman!=null)nativeShaman.Dispose();if(nativeAudio!=null)nativeAudio.Dispose();if(nativeActors!=null)nativeActors.Dispose();if(heroFragments!=null)heroFragments.Dispose();if(turnButton!=null)turnButton.Dispose();menuButtons.Dispose();if(fontBundle!=null)fontBundle.Unload(false);}
}
