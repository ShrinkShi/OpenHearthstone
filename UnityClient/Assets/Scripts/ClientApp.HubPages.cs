using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class ClientApp
{
    const int F024CollectionPageSize = 8;
    const float F024CollectionSetDropdownRowHeight = 28f;
    const float F024CollectionSetDropdownHeaderHeight = 24f;
    float f031SetTrayProgress, f031SetTrayStartProgress, f031SetTrayStartedAt;
    bool f031SetTrayAnimating;
    NativeCollectionPageTurn f032PageTurn;
    RenderTexture f032PageSnapshot;
    bool f032PageTurnPending, f032PageTurnActive, f032PageTurnRight;
    int f032PreviousPage;
    float f032PageTurnStart;
    static readonly Dictionary<int, string> F024CollectionSetNames = new Dictionary<int, string>
    {
        { 2, "基础系列" }, { 3, "经典卡包" }, { 4, "荣誉室" },
        { 12, "纳克萨玛斯的诅咒" }, { 13, "地精大战侏儒" }, { 14, "黑石山的火焰" },
        { 15, "冠军的试炼" }, { 20, "探险者协会" }, { 21, "上古之神的低语" },
        { 23, "卡拉赞之夜" }, { 25, "龙争虎斗加基森" }, { 27, "勇闯安戈洛" },
        { 1001, "冰封王座的骑士" }, { 1004, "狗头人与地下世界" }, { 1125, "女巫森林" },
        { 1127, "砰砰计划" }, { 1129, "拉斯塔哈的大乱斗" }, { 1130, "暗影崛起" },
        { 1158, "奥丹姆奇兵" }, { 1347, "巨龙降临" }, { 1403, "迦拉克隆的觉醒" },
        { 1414, "外域的灰烬" }, { 1443, "通灵学园" }, { 1463, "恶魔猎手新兵" },
        { 1635, "经典系列" }, { 1637, "核心系列" }, { 1869, "阿尔萨斯之路" }
    };
    int f024CollectionTab;
    int f024CollectionClass = -1, f024CollectionMana = -1, f024CollectionSet = -1, f024CollectionOwnedMode, f024CollectionPage;
    int f024DraftClassId;
    string f024CollectionSearch = "", f024DeckBuilderSearch = "", f024CollectionCacheKey = "", f024SelectedCardId = "";
    string f028DraftFormat = "wild", f028ClipboardCode = "", f028PendingCodeRequestId = "", f028ReplayClipboard = "";
    string f028PendingSaveRequestId = "";
    bool f028OfferClipboardImport, f028ConfirmDiscard;
    string f028PendingDeckSwitch = "";
    string f024DraftId = "", f024DraftName = "";
    string f024LastOpenedId = "";
    int f024SelectedPackSet = -1, f024DraggingPackSet = -1;
    string f024RevealOpenId = "";
    CardDef[] f024FilteredCards = new CardDef[0];
    readonly List<string> f024DraftCards = new List<string>();
    F024OpenedCard[] f024RevealCards = new F024OpenedCard[0];
    Vector2 f024DeckListScroll, f024PackListScroll, f024ShopScroll, f024DeckBuilderPoolScroll, f024DeckBuilderListScroll;
    Vector2 f025CollectionDeckScroll, f024CollectionSetDropdownScroll;
    bool f024DraftDirty, f024PackRevealActive, f024GoldenCraft, f024CollectionSetDropdownOpen, f029CraftingMode;
    int f028CardControl;
    string f028DraggedCard = "", f028HoveredCard = "";
    bool f028DragFromDeck, f028CardMoved;
    Vector2 f028CardStart;
    int f024RevealIndex = -1;
    int f024PackDragControlId;
    GUIStyle f024NativeButtonTextStyle;
    GUIStyle f024PaperLabelStyle, f024PaperSelectorStyle, f024PaperWrappedStyle, f024CollectionSearchStyle;
    GUIStyle f024CollectionSetDropdownLabelStyle;
    GUIStyle f024QuestTitleStyle, f024QuestBodyStyle;
    GUIStyle f025DeckTrayTextStyle, f025DeckTrayTitleStyle, f025DeckBannerTextStyle, f025DeckBannerMetaStyle, f025ManaNumberStyle, f027CountStyle;
    bool f025SettingsOpen, f025CollectionDetailOpen;
    int f025SettingsTab;
    string f025SettingsReturnPage = "menu";

    Rect F025CollectionPaperRect
    {
        get
        {
            Rect paper, tray;
            if (nativeHubScene != null && nativeHubScene.TryGetCollectionLayout(out paper, out tray) &&
                paper.width > 100 && paper.height > 100) return paper;
            return new Rect(120, 145, 980, 730);
        }
    }

    Rect F025CollectionDeckTrayRect
    {
        get
        {
            Rect paper, tray;
            if (nativeHubScene != null && nativeHubScene.TryGetCollectionLayout(out paper, out tray) &&
                tray.width > 100 && tray.height > 100) return tray;
            return new Rect(1120, 145, 400, 730);
        }
    }

    bool F025HubOverlayOpen { get { return f025SettingsOpen || f025CollectionDetailOpen || f026ShopBuyOpen || f026FriendsOpen || f024PackRevealActive; } }
    bool F028DeckModalOpen { get { return f028OfferClipboardImport || f028ConfirmDiscard; } }
    bool F028DraftRequestPending { get { return !String.IsNullOrEmpty(f028PendingSaveRequestId) || !String.IsNullOrEmpty(f028PendingCodeRequestId); } }

    GUIStyle F024ButtonStyle(Rect rect)
    {
        if (nativeHubScene == null || !(page == "account" || page == "collection" || page == "packs" ||
            page == "shop" || page == "quests" || page == "decks")) return buttonStyle;
        bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
        var previousColor = GUI.color;
        if (!GUI.enabled) GUI.color = previousColor * new Color(.65f, .65f, .65f, .8f);
        bool drawn;
        try { drawn = nativeHubScene.DrawButton(rect, hovered, hovered && Input.GetMouseButton(0)); }
        finally { GUI.color = previousColor; }
        if (!drawn) return buttonStyle;
        if (f024NativeButtonTextStyle == null)
        {
            f024NativeButtonTextStyle = new GUIStyle(menuButtonStyle);
        }
        f024NativeButtonTextStyle.fontSize = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Min(rect.width * .105f, rect.height * .34f)), 12, 23);
        f024NativeButtonTextStyle.alignment = TextAnchor.MiddleCenter;
        f024NativeButtonTextStyle.wordWrap = false;
        f024NativeButtonTextStyle.clipping = TextClipping.Clip;
        f024NativeButtonTextStyle.contentOffset = Vector2.zero;
        int horizontalInset = Mathf.RoundToInt(rect.width * .20f);
        int verticalInset = Mathf.RoundToInt(rect.height * .24f);
        f024NativeButtonTextStyle.padding = new RectOffset(horizontalInset, horizontalInset, verticalInset, verticalInset);
        var ink = new Color(.18f, .10f, .055f);
        var hoverInk = new Color(.34f, .20f, .085f);
        f024NativeButtonTextStyle.normal.textColor = ink;
        f024NativeButtonTextStyle.hover.textColor = hoverInk;
        f024NativeButtonTextStyle.active.textColor = ink;
        f024NativeButtonTextStyle.focused.textColor = ink;
        f024NativeButtonTextStyle.onNormal.textColor = ink;
        f024NativeButtonTextStyle.onHover.textColor = hoverInk;
        f024NativeButtonTextStyle.onActive.textColor = ink;
        f024NativeButtonTextStyle.onFocused.textColor = ink;
        return f024NativeButtonTextStyle;
    }

    GUIStyle F024PaperLabelStyle
    {
        get
        {
            if (f024PaperLabelStyle == null)
            {
                f024PaperLabelStyle = new GUIStyle(smallStyle) { wordWrap = false };
                F024SetPaperInk(f024PaperLabelStyle);
            }
            return f024PaperLabelStyle;
        }
    }

    GUIStyle F024PaperSelectorStyle
    {
        get
        {
            if (f024PaperSelectorStyle == null)
            {
                f024PaperSelectorStyle = new GUIStyle(F024PaperLabelStyle)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
            }
            return f024PaperSelectorStyle;
        }
    }

    GUIStyle F024PaperWrappedStyle
    {
        get
        {
            if (f024PaperWrappedStyle == null)
            {
                f024PaperWrappedStyle = new GUIStyle(F024PaperLabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            }
            return f024PaperWrappedStyle;
        }
    }

    GUIStyle F024CollectionSearchStyle
    {
        get
        {
            if (f024CollectionSearchStyle == null)
            {
                f024CollectionSearchStyle = new GUIStyle(GUIStyle.none)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    font = smallStyle.font,
                    fontSize = smallStyle.fontSize,
                    padding = new RectOffset(10, 8, 2, 2)
                };
                f024CollectionSearchStyle.normal.background = null;
                f024CollectionSearchStyle.hover.background = null;
                f024CollectionSearchStyle.active.background = null;
                f024CollectionSearchStyle.focused.background = null;
                f024CollectionSearchStyle.onNormal.background = null;
                f024CollectionSearchStyle.onHover.background = null;
                f024CollectionSearchStyle.onActive.background = null;
                f024CollectionSearchStyle.onFocused.background = null;
                F024SetPaperInk(f024CollectionSearchStyle);
                f024CollectionSearchStyle.normal.textColor = new Color(.96f, .87f, .7f);
                f024CollectionSearchStyle.focused.textColor = f024CollectionSearchStyle.normal.textColor;
            }
            return f024CollectionSearchStyle;
        }
    }

    GUIStyle F025DeckTrayTextStyle
    {
        get
        {
            if (f025DeckTrayTextStyle == null)
            {
                f025DeckTrayTextStyle = new GUIStyle(smallStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    fontSize = 16
                };
                var ink = new Color(.96f, .91f, .79f);
                f025DeckTrayTextStyle.normal.textColor = ink;
                f025DeckTrayTextStyle.hover.textColor = Color.white;
                f025DeckTrayTextStyle.active.textColor = ink;
                f025DeckTrayTextStyle.focused.textColor = ink;
            }
            return f025DeckTrayTextStyle;
        }
    }

    GUIStyle F025DeckBannerTextStyle
    {
        get
        {
            if (f025DeckBannerTextStyle == null)
            {
                f025DeckBannerTextStyle = new GUIStyle(F025DeckTrayTextStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                    padding = new RectOffset(8, 4, 0, 0)
                };
                f025DeckBannerTextStyle.normal.textColor = Color.white;
                f025DeckBannerTextStyle.hover.textColor = Color.white;
                f025DeckBannerTextStyle.active.textColor = Color.white;
                f025DeckBannerTextStyle.focused.textColor = Color.white;
            }
            return f025DeckBannerTextStyle;
        }
    }

    GUIStyle F025DeckTrayTitleStyle
    {
        get
        {
            if (f025DeckTrayTitleStyle == null)
            {
                f025DeckTrayTitleStyle = new GUIStyle(F025DeckTrayTextStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(4, 4, 0, 0)
                };
            }
            return f025DeckTrayTitleStyle;
        }
    }

    GUIStyle F025DeckBannerMetaStyle
    {
        get
        {
            if (f025DeckBannerMetaStyle == null)
            {
                f025DeckBannerMetaStyle = new GUIStyle(F025DeckBannerTextStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Normal,
                    fontSize = 12,
                    padding = new RectOffset(2, 8, 0, 0)
                };
            }
            return f025DeckBannerMetaStyle;
        }
    }

    GUIStyle F025ManaNumberStyle
    {
        get
        {
            if (f025ManaNumberStyle == null)
            {
                f025ManaNumberStyle = new GUIStyle(F024PaperSelectorStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 16,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                f025ManaNumberStyle.normal.textColor = Color.white;
                f025ManaNumberStyle.hover.textColor = Color.white;
                f025ManaNumberStyle.active.textColor = Color.white;
                f025ManaNumberStyle.focused.textColor = Color.white;
            }
            return f025ManaNumberStyle;
        }
    }

    static void F024SetPaperInk(GUIStyle style)
    {
        var ink = new Color(.20f, .12f, .065f);
        var hoverInk = new Color(.34f, .20f, .09f);
        style.normal.textColor = ink;
        style.hover.textColor = hoverInk;
        style.active.textColor = ink;
        style.focused.textColor = ink;
        style.onNormal.textColor = ink;
        style.onHover.textColor = hoverInk;
        style.onActive.textColor = ink;
        style.onFocused.textColor = ink;
    }

    void F024HighlightPaperCell(Rect rect, bool selected)
    {
        if (!selected || Event.current.type != EventType.Repaint) return;
        Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), new Color(.58f, .37f, .15f, .18f));
        Frame(rect, new Color(.45f, .27f, .09f, .65f), 1);
    }

    void DrawF024Hub()
    {
        menuPresentation?.LayoutHubFooter();
        GUI.Label(new Rect(520, 264, 560, 74), "炉石开源复刻", menuButtonStyle);
        if (F024NativeMenuButton("FirstButton", "冒险模式", new Rect(640, 265, 320, 60), false)) { }
        if (F024NativeMenuButton("SecondButton", "传统对战", new Rect(640, 345, 320, 60), true))
        {
            if (F024HasAccount)
            {
                F024OpenPage("decks");
                F024ReplayGuiMarkHandler("menu.traditional", "F024OpenPage(decks)");
            }
            else
            {
                F025OpenSettings(true);
                f024AccountMessage = "登录后可使用服务器卡组开始传统对战";
                F024ReplayGuiMarkHandler("menu.traditional", "OpenAccountSettings");
            }
        }
        if (F024NativeMenuButton("ThirdButton", "竞技模式", new Rect(640, 425, 320, 60), false)) { }
        if (F024NativeMenuButton("FourthButton", "其他模式", new Rect(640, 505, 320, 60), false)) { }

        if (F024NativeMenuButton("Button_Store", "商店", new Rect(190, 805, 170, 120), true))
        {
            F024OpenPage("shop");
            F024ReplayGuiMarkHandler("menu.shop", "F024OpenPage(shop)");
        }
        if (F024NativeMenuButton("Button_OpenPacks_mesh", "开包", new Rect(360, 805, 200, 120), true))
        {
            F024OpenPage("packs");
            F024ReplayGuiMarkHandler("menu.packs", "F024OpenPage(packs)");
        }
        if (F024NativeMenuButton("Button_Collection_mesh", "我的收藏", new Rect(650, 805, 430, 120), true))
        {
            f024CollectionTab = 0;
            F024OpenPage("collection");
            F024ReplayGuiMarkHandler("menu.collection", "F024OpenPage(collection)");
        }
        if (F024NativeMenuButton("QuestLogButton", "任务", new Rect(45, 820, 100, 100), true))
        {
            F024OpenPage("quests");
            F024ReplayGuiMarkHandler("menu.quests", "F024OpenPage(quests)");
        }

        if (debugUI && Button(new Rect(1320, 20, 240, 44), "UI 校对工具 · F6")) uiEditor.Active = true;
    }

    bool F024NativeMenuButton(string node, string label, Rect fallback, bool enabled)
    {
        Rect rect = menuPresentation == null ? Rect.zero : menuPresentation.ButtonRect(node);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && enabled;
        bool clicked;
        if (rect.width > 8 && rect.height > 8) clicked = MenuButton(node, label);
        else clicked = F024ReplayGuiEnabled ? Button(fallback, label, F024ReplayMenuTarget(node)) : Button(fallback, label);
        GUI.enabled = wasEnabled;
        return clicked;
    }

    void DrawF025GlobalSettingsButton()
    {
        DrawF026HubChrome();
    }

    void F025OpenSettings(bool openAccount)
    {
        if (!f025SettingsOpen) f025SettingsReturnPage = page;
        f025SettingsOpen = true;
        f025SettingsTab = openAccount ? 2 : 0;
        F026CloseFriendsPanel();
        f026ShopBuyOpen = false;
        if (f024PackDragControlId != 0 && GUIUtility.hotControl == f024PackDragControlId)
            GUIUtility.hotControl = 0;
        f024PackDragControlId = 0;
        f024DraggingPackSet = -1;
    }

    void F025CloseSettings()
    {
        f025SettingsOpen = false;
        f025SettingsTab = 0;
        if (!String.IsNullOrEmpty(f025SettingsReturnPage)) page = f025SettingsReturnPage;
    }

    void DrawF025SettingsOverlay()
    {
        DrawF026SettingsOverlay();
    }

    void DrawF025AccountSettings(Rect panel)
    {
        GUI.Label(new Rect(panel.x + 44, panel.y + 144, 660, 44), "账号与会话", labelStyle);
        GUI.Label(new Rect(panel.x + 44, panel.y + 194, 86, 34), "服务器", smallStyle);
        bool previous = GUI.enabled;
        GUI.enabled = previous && !F024HasAccount && !f024AccountBusy;
        host = GUI.TextField(new Rect(panel.x + 132, panel.y + 190, 340, 38), host, 128);
        if (String.IsNullOrEmpty(f024GatewayPort)) f024GatewayPort = port.ToString();
        GUI.Label(new Rect(panel.x + 485, panel.y + 194, 48, 34), "端口", smallStyle);
        f024GatewayPort = GUI.TextField(new Rect(panel.x + 530, panel.y + 190, 112, 38), f024GatewayPort, 5);
        GUI.enabled = previous;

        if (F024HasAccount)
        {
            GUI.Label(new Rect(panel.x + 70, panel.y + 258, 620, 42), playerName + " · " + f024Account.username, labelStyle);
            GUI.Label(new Rect(panel.x + 70, panel.y + 308, 620, 42), "金币 " + f024Account.gold + "    奥尘 " + f024Account.dust, labelStyle);
            GUI.Label(new Rect(panel.x + 70, panel.y + 360, 620, 36),
                "服务器卡组 " + ((f024Account.decks ?? new F024AccountDeck[0]).Length), smallStyle);
            GUI.enabled = previous && !f024AccountBusy;
            if (Button(new Rect(panel.x + 70, panel.y + 430, 220, 54), "退出账号", "account.logout"))
            {
                F024Logout();
                F024ReplayGuiMarkHandler("account.logout", "F024Logout");
            }
            if (Button(new Rect(panel.x + 320, panel.y + 430, 220, 54), "返回设置", "account.back"))
            {
                F025CloseSettings();
                F024ReplayGuiMarkHandler("account.back", "ReturnFromAccountPage");
            }
            GUI.enabled = previous;
            return;
        }

        string[] tabs = { "登录", "注册" };
        int selectedTab = GUI.SelectionGrid(new Rect(panel.x + 188, panel.y + 244, 350, 42), f024RegisterMode ? 1 : 0, tabs, 2, buttonStyle);
        f024RegisterMode = selectedTab == 1;
        GUI.Label(new Rect(panel.x + 92, panel.y + 302, 100, 34), "账号名", smallStyle);
        f024Username = GUI.TextField(new Rect(panel.x + 198, panel.y + 298, 430, 38), f024Username, 32);
        float passwordY = panel.y + 356;
        if (f024RegisterMode)
        {
            GUI.Label(new Rect(panel.x + 92, panel.y + 350, 100, 34), "显示名称", smallStyle);
            f024DisplayName = GUI.TextField(new Rect(panel.x + 198, panel.y + 346, 430, 38), f024DisplayName, 32);
            passwordY += 48;
        }
        GUI.Label(new Rect(panel.x + 92, passwordY + 4, 100, 34), "密码", smallStyle);
        f024Password = GUI.PasswordField(new Rect(panel.x + 198, passwordY, 430, 38), f024Password, '*', 128);
        GUI.Label(new Rect(panel.x + 92, panel.y + 440, 570, 34), "旧本地卡组仍保存在原profile，不会自动写入账号收藏。", smallStyle);
        GUI.enabled = previous && !f024AccountBusy;
        string target = f024RegisterMode ? "account.register" : "account.login";
        string label = f024RegisterMode ? "创建账号" : "登录";
        if (Button(new Rect(panel.x + 198, panel.y + 492, 200, 52), label, target))
        {
            if (f024RegisterMode)
            {
                F024Register();
                F024ReplayGuiMarkHandler("account.register", "F024Register");
            }
            else
            {
                F024Login();
                F024ReplayGuiMarkHandler("account.login", "F024Login");
            }
        }
        if (Button(new Rect(panel.x + 428, panel.y + 492, 200, 52), "返回设置", "account.back"))
        {
            F025CloseSettings();
            F024ReplayGuiMarkHandler("account.back", "ReturnFromAccountPage");
        }
        GUI.enabled = previous;
    }

    void DrawF025CollectionDetailOverlay()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            CloseCollectionDetail();
            Event.current.Use();
            return;
        }

        Fill(new Rect(0, 0, 1600, 1000), new Color(.008f, .006f, .01f, .78f));
        var closeRect = new Rect(1460, 28, 106, 42);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("collection.detail.close", closeRect);
        if (Button(closeRect, "关闭详情", "collection.detail.close"))
        {
            CloseCollectionDetail();
            F024ReplayGuiMarkHandler("collection.detail.close", "CloseCollectionDetail");
            return;
        }

        CardDef card = null;
        if (cards == null || !cards.TryGetValue(f024SelectedCardId ?? "", out card) || !F024IsBookCard(card))
        {
            var emptyPanel = new Rect(520, 420, 560, 120);
            Panel(emptyPanel);
            GUI.Label(new Rect(emptyPanel.x + 18, emptyPanel.y + 18, emptyPanel.width - 36, 84),
                "选择一本卡牌查看详情", labelStyle);
            return;
        }

        var relatedPanel = new Rect(82, 230, 420, 444);
        var relatedIds = (card.RelatedCardIds ?? new string[0])
            .Where(id => !String.IsNullOrEmpty(id) && id != card.CardId && cards.ContainsKey(id))
            .Distinct().ToArray();
        if (relatedIds.Length > 0)
        {
            F025DrawCollectionDetailPanel(relatedPanel, "相关卡牌", false, "collection.detail.related");
            float relatedY = relatedPanel.y + 90;
            int visibleRelated = Mathf.Min(6, relatedIds.Length);
            for (int i = 0; i < visibleRelated; i++)
            {
                CardDef relatedCard;
                if (!cards.TryGetValue(relatedIds[i], out relatedCard) || relatedCard == null) continue;
                var row = new Rect(relatedPanel.x + 56, relatedY, relatedPanel.width - 110, 48);
                Fill(row, new Color(.19f, .13f, .22f, .86f));
                Frame(row, new Color(.50f, .48f, .46f, .72f), 1);
                var costRect = new Rect(row.x + 7, row.y + 7, 34, 34);
                Fill(costRect, new Color(.12f, .34f, .63f, .96f));
                Frame(costRect, new Color(.78f, .72f, .54f, .85f), 1);
                GUI.Label(costRect, relatedCard.Cost.ToString(), smallStyle);
                GUI.Label(new Rect(costRect.xMax + 9, row.y + 4, row.width - 58, 40),
                    relatedCard.Name, F025DeckTrayTextStyle);
                relatedY += 54;
            }
            if (relatedIds.Length > visibleRelated)
                GUI.Label(new Rect(relatedPanel.x + 16, relatedPanel.yMax - 36, relatedPanel.width - 32, 24),
                    "还有 " + (relatedIds.Length - visibleRelated) + " 张直接关联卡牌", F025DeckTrayTextStyle);
        }

        var infoPanel = new Rect(1098, 230, 420, 444);
        F025DrawCollectionDetailPanel(infoPanel, "卡牌信息", true, "collection.detail.info");
        string typeName = card.Type == 4 ? "随从" : card.Type == 5 ? "法术" : "武器";
        string rarityName = card.Rarity == 1 ? "普通" : card.Rarity == 2 ? "免费" :
            card.Rarity == 3 ? "稀有" : card.Rarity == 4 ? "史诗" : card.Rarity == 5 ? "传说" : "未标注";
        var inventory = F024FindAccountCard(card.CardId);
        float infoX = infoPanel.x + 18;
        float infoWidth = infoPanel.width - 36;
        GUI.Label(new Rect(infoX, infoPanel.y + 48, infoWidth, 28),
            F024ClassNameOrNeutral(card.Class) + " · " + typeName + " · " + card.Cost + " 费 · " + rarityName, F024PaperLabelStyle);
        GUI.Label(new Rect(infoX, infoPanel.y + 77, infoWidth, 28),
            F024SetName(card.Set) + " · 普通 " + (inventory == null ? 0 : inventory.normal) +
                " · 金色 " + (inventory == null ? 0 : inventory.golden) + " · 基础 " + (inventory == null ? 0 : inventory.free),
            F024PaperLabelStyle);
        GUI.Label(new Rect(infoX, infoPanel.y + 106, infoWidth, 28),
            card.Playable ? "可用于对战卡组" : "当前不能加入对战卡组", F024PaperLabelStyle);
        Fill(new Rect(infoX + 12, infoPanel.y + 136, infoWidth - 24, 1), new Color(.35f, .25f, .15f, .7f));
        string flavorText = String.IsNullOrWhiteSpace(card.FlavorText) ? "暂无风味文本" : card.FlavorText;
        GUI.Label(new Rect(infoX + 8, infoPanel.y + 144, infoWidth - 16, 110), flavorText, F024PaperWrappedStyle);
        if (!String.IsNullOrWhiteSpace(card.ArtistName))
            GUI.Label(new Rect(infoX, infoPanel.y + 354, infoWidth, 28), "画师 · " + card.ArtistName, F024PaperLabelStyle);
        bool previous = GUI.enabled;
        bool canAdd = card.Playable && f024DraftCards.Count < 30 && F024CanAddDraftCard(card) && !f024AccountBusy;
        GUI.enabled = previous && canAdd;
        if (Button(new Rect(infoX + 28, infoPanel.y + 394, infoWidth - 56, 42), "加入当前卡组"))
            F024TryAddDraftCard(card);
        GUI.enabled = previous;

        F024DrawCardFace(new Rect(620, 104, 360, 540), card);
        F025DrawCollectionDetailCrafting(new Rect(590, 748, 420, 242), card, inventory);
    }

    void F025DrawCollectionDetailPanel(Rect rect, string heading, bool parchment, string widgetId)
    {
        bool nativeDrawn = F026DrawWidget(rect, widgetId);
        var body = new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16);
        if (!nativeDrawn && Event.current.type == EventType.Repaint)
        {
            Panel(rect);
            Fill(body, parchment ? new Color(.76f, .68f, .53f, .97f) : new Color(.105f, .07f, .15f, .97f));
            Frame(body, parchment ? new Color(.34f, .25f, .16f, .92f) : new Color(.57f, .56f, .52f, .82f), 2);
        }
        var headingRect = widgetId == "collection.detail.related"
            ? new Rect(body.x + 56, body.y + 22, body.width - 110, 34)
            : new Rect(body.x + 12, body.y + 7, body.width - 24, 34);
        GUI.Label(headingRect, heading, parchment || widgetId == "collection.detail.related"
            ? F024PaperLabelStyle : F025DeckTrayTextStyle);
        if (Event.current.type == EventType.Repaint)
            Fill(new Rect(body.x + 14, headingRect.yMax + 2, body.width - 28, 1),
                parchment ? new Color(.34f, .25f, .16f, .58f) : new Color(.72f, .61f, .42f, .65f));
    }

    void F025DrawCollectionDetailCrafting(Rect rect, CardDef card, F024AccountCard inventory)
    {
        bool nativeDrawn = nativeHubWidgets != null && nativeHubWidgets.DrawWidget(rect, "collection.detail.crafting");
        if (nativeDrawn)
        {
            F027DrawNativeCollectionCraftingControls(rect, card, inventory);
            return;
        }
        var body = new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16);
        if (!nativeDrawn && Event.current.type == EventType.Repaint)
        {
            Panel(rect);
            Fill(body, new Color(.095f, .065f, .13f, .98f));
            Frame(body, new Color(.66f, .55f, .37f, .82f), 2);
        }
        GUI.Label(new Rect(body.x + 12, body.y + 3, body.width - 24, 28), "制作与分解", F025DeckTrayTextStyle);
        f024GoldenCraft = GUI.Toggle(new Rect(body.x + 12, body.y + 33, 178, 28),
            f024GoldenCraft, "金色卡牌", F025DeckTrayTextStyle);

        int craftCost = F024CraftDust(card, f024GoldenCraft);
        int disenchantValue = F024DisenchantDust(card, f024GoldenCraft);
        int dustBalance = f024Account == null ? 0 : f024Account.dust;
        bool craftable = F024CanCraftOrDisenchant(card);
        bool ownsSelected = inventory != null && (f024GoldenCraft ? inventory.golden > 0 : inventory.normal > 0);
        bool previous = GUI.enabled;
        GUI.enabled = previous && craftable && dustBalance >= craftCost && !f024AccountBusy;
        var craftRect = new Rect(body.x + 12, body.y + 64, (body.width - 32) * .5f, 46);
        if (Button(craftRect, "制作 1 张", "collection.craft"))
        {
            F024RequestCraft(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.craft", "F024RequestCraft");
        }
        GUI.enabled = previous && craftable && ownsSelected && !f024AccountBusy;
        var disenchantRect = new Rect(craftRect.xMax + 8, craftRect.y, craftRect.width, craftRect.height);
        if (Button(disenchantRect, "分解 1 张", "collection.disenchant"))
        {
            F024RequestDisenchant(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.disenchant", "F024RequestDisenchant");
        }
        GUI.enabled = previous;

        GUI.Label(new Rect(craftRect.x, body.y + 112, craftRect.width, 23),
            craftable ? "-" + craftCost + " 奥尘" : "不可制作", F025DeckTrayTextStyle);
        GUI.Label(new Rect(disenchantRect.x, body.y + 112, disenchantRect.width, 23),
            craftable ? "+" + disenchantValue + " 奥尘" : "不可分解", F025DeckTrayTextStyle);
        var dustIcon = new Rect(body.center.x - 52, body.yMax - 30, 24, 24);
        if (!F024DrawNativeImage(dustIcon, "UI_DUST", ScaleMode.ScaleToFit))
            GUI.Label(dustIcon, "尘", F025DeckTrayTextStyle);
        GUI.Label(new Rect(dustIcon.xMax + 4, dustIcon.y, 112, 24), dustBalance + " 奥尘", F025DeckTrayTextStyle);
    }

    void F027DrawNativeCollectionCraftingControls(Rect rect, CardDef card, F024AccountCard inventory)
    {
        f024GoldenCraft = GUI.Toggle(new Rect(rect.x, rect.y - 26, 150, 24),
            f024GoldenCraft, "金色卡牌", F025DeckTrayTextStyle);
        int craftCost = F024CraftDust(card, f024GoldenCraft);
        int disenchantValue = F024DisenchantDust(card, f024GoldenCraft);
        int dustBalance = f024Account == null ? 0 : f024Account.dust;
        bool craftable = F024CanCraftOrDisenchant(card);
        bool ownsSelected = inventory != null && (f024GoldenCraft ? inventory.golden > 0 : inventory.normal > 0);
        bool previous = GUI.enabled;
        var disenchantRect = new Rect(rect.x + rect.width * .09f, rect.y + rect.height * .22f,
            rect.width * .36f, rect.height * .34f);
        var craftRect = new Rect(rect.x + rect.width * .55f, disenchantRect.y,
            disenchantRect.width, disenchantRect.height);
        GUI.enabled = previous && craftable && ownsSelected && !f024AccountBusy;
        if (F027NativeCraftButton(disenchantRect, "分解", "collection.disenchant"))
        {
            F024RequestDisenchant(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.disenchant", "F024RequestDisenchant");
        }
        GUI.enabled = previous && craftable && dustBalance >= craftCost && !f024AccountBusy;
        if (F027NativeCraftButton(craftRect, "制作", "collection.craft"))
        {
            F024RequestCraft(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.craft", "F024RequestCraft");
        }
        GUI.enabled = previous;
        float costY = rect.y + rect.height * .58f;
        GUI.Label(new Rect(disenchantRect.x, costY, disenchantRect.width, 26),
            craftable ? "+" + disenchantValue : "—", F025ManaNumberStyle);
        GUI.Label(new Rect(craftRect.x, costY, craftRect.width, 26),
            craftable ? "−" + craftCost : "—", F025ManaNumberStyle);
        GUI.Label(new Rect(rect.x + rect.width * .35f, rect.y + rect.height * .84f,
            rect.width * .3f, 25), dustBalance.ToString(), F025ManaNumberStyle);
    }

    bool F027NativeCraftButton(Rect rect, string text, string target)
    {
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect);
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        F024ReplayGuiObserveControlAfter(target);
        GUI.Label(rect, text, F025ManaNumberStyle);
        if (clicked) PlaySound("SFX_UI_CLICK");
        return clicked;
    }

    void CloseCollectionDetail()
    {
        f025CollectionDetailOpen = false;
    }

    void DrawF024AccountPage()
    {
        F024DrawPageFrame("账号", "CollectionManagerFrame_Texture", new Rect(285, 115, 1030, 765));
        GUI.Label(new Rect(360, 140, 880, 70), "账号与会话", titleStyle);
        GUI.Label(new Rect(360, 220, 880, 42), "服务器", labelStyle);
        GUI.Label(new Rect(405, 274, 140, 40), "主机", smallStyle);
        bool previousEndpointEnabled = GUI.enabled;
        GUI.enabled = previousEndpointEnabled && !F024HasAccount && !f024AccountBusy;
        host = GUI.TextField(new Rect(555, 274, 440, 42), host, 128);
        GUI.Label(new Rect(1005, 274, 88, 40), "端口", smallStyle);
        if (String.IsNullOrEmpty(f024GatewayPort)) f024GatewayPort = port.ToString();
        f024GatewayPort = GUI.TextField(new Rect(1090, 274, 130, 42), f024GatewayPort, 5);
        GUI.enabled = previousEndpointEnabled;

        if (F024HasAccount)
        {
            GUI.Label(new Rect(380, 355, 840, 50), playerName + " · " + f024Account.username, labelStyle);
            GUI.Label(new Rect(380, 412, 840, 44), "金币 " + f024Account.gold + "    奥尘 " + f024Account.dust, labelStyle);
            GUI.Label(new Rect(380, 470, 840, 52), "服务器卡组 " + ((f024Account.decks ?? new F024AccountDeck[0]).Length), smallStyle);
            if (!String.IsNullOrEmpty(f024AccountMessage)) GUI.Label(new Rect(380, 525, 840, 58), f024AccountMessage, smallStyle);
            bool canLogout = !f024AccountBusy;
            bool previous = GUI.enabled;
            GUI.enabled = previous && canLogout;
            if (Button(new Rect(535, 640, 240, 58), "退出账号", "account.logout"))
            {
                F024Logout();
                F024ReplayGuiMarkHandler("account.logout", "F024Logout");
            }
            GUI.enabled = previous;
            if (Button(new Rect(825, 640, 240, 58), "返回主界面", "account.back"))
            {
                page = "menu";
                F024ReplayGuiMarkHandler("account.back", "ReturnFromAccountPage");
            }
        }
        else
        {
            string[] tabs = { "登录", "注册" };
            int tab = f024RegisterMode ? 1 : 0;
            int chosen = GUI.SelectionGrid(new Rect(500, 345, 600, 54), tab, tabs, 2, buttonStyle);
            f024RegisterMode = chosen == 1;
            GUI.Label(new Rect(405, 430, 170, 42), "账号名", smallStyle);
            f024Username = GUI.TextField(new Rect(585, 430, 560, 42), f024Username, 32);
            if (f024RegisterMode)
            {
                GUI.Label(new Rect(405, 488, 170, 42), "显示名称", smallStyle);
                f024DisplayName = GUI.TextField(new Rect(585, 488, 560, 42), f024DisplayName, 32);
            }
            GUI.Label(new Rect(405, f024RegisterMode ? 546 : 488, 170, 42), "密码", smallStyle);
            f024Password = GUI.PasswordField(new Rect(585, f024RegisterMode ? 546 : 488, 560, 42), f024Password, '*', 128);
            GUI.Label(new Rect(385, 610, 830, 42), "注册账户将获得基础与核心卡牌、1000金币和0奥尘。", smallStyle);
            GUI.Label(new Rect(385, 650, 830, 42), "旧本地卡组文件保留在原profile，不会自动写入账号收藏。", smallStyle);
            bool previous = GUI.enabled;
            GUI.enabled = previous && !f024AccountBusy;
            string submitTarget = f024RegisterMode ? "account.register" : "account.login";
            if (Button(new Rect(535, 710, 240, 58), f024RegisterMode ? "创建账号" : "登录", submitTarget))
            {
                if (f024RegisterMode)
                {
                    F024Register();
                    F024ReplayGuiMarkHandler("account.register", "F024Register");
                }
                else
                {
                    F024Login();
                    F024ReplayGuiMarkHandler("account.login", "F024Login");
                }
            }
            GUI.enabled = previous;
            if (Button(new Rect(825, 710, 240, 58), "返回主界面", "account.back"))
            {
                page = "menu";
                F024ReplayGuiMarkHandler("account.back", "ReturnFromAccountPage");
            }
        }
        if (!String.IsNullOrEmpty(f024AccountMessage)) GUI.Label(new Rect(340, 810, 920, 44), f024AccountMessage, smallStyle);
    }

    void F024DrawPageFrame(string title, string nativeFrame, Rect rect)
    {
        string sceneId = page == "collection" && nativeHubScene != null
            ? nativeHubScene.SceneIdForPage(page)
            : F024SceneForFrame(nativeFrame);
        bool nativeOnlyFrame = sceneId == "SCENE_PACK_OPENING" || sceneId == "SCENE_QUEST_LOG" ||
            sceneId == "SCENE_MAIN_SHOP_PC" || sceneId == "SCENE_COLLECTION_MANAGER" ||
            sceneId == "SCENE_COLLECTION_PAGE";
        if (!nativeOnlyFrame) Fill(new Rect(0, 0, 1600, 1000), new Color(.015f, .01f, .008f, .58f));
        Rect sceneRect = rect;
        if (nativeOnlyFrame && !f026QuestPreviewActive)
        {
            Matrix4x4 inverse = GUI.matrix.inverse;
            Vector3 screenTopLeft = inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 screenBottomRight = inverse.MultiplyPoint3x4(new Vector3(Screen.width, Screen.height, 0));
            sceneRect = Rect.MinMaxRect(screenTopLeft.x, screenTopLeft.y,
                screenBottomRight.x, screenBottomRight.y);
        }
        if (sceneId == "SCENE_PACK_OPENING")
        {
            Fill(sceneRect, new Color(.035f, .022f, .014f));
            sceneRect.height -= 65;
        }
        bool nativeSceneDrawn = nativeHubScene != null && sceneId != "" && nativeHubScene.Draw(sceneRect, sceneId);
        if (!nativeSceneDrawn)
        {
            if (nativeOnlyFrame || (!F024DrawNativeImage(rect, F024FallbackFrame(nativeFrame)) && !F024DrawNativeImage(rect, "UI_MENU_FRAME")))
                Panel(rect);
        }
        if (!String.IsNullOrEmpty(title))
            GUI.Label(new Rect(rect.x + 35, rect.y + 16, rect.width - 70, 58), title, titleStyle);
        if (f026QuestPreviewActive) return;
        Rect backRect = page == "collection" ? new Rect(1380, 878, 160, 42) : new Rect(1332, 888, 160, 52);
        if (page == "quests") backRect = new Rect(1070, 936, 160, 52);
        bool nativePackBack = page == "packs" && nativeHubScene != null &&
            nativeHubScene.TryGetPackOpeningLayout(out _, out _, out backRect);
        bool backClicked;
        if (nativePackBack)
        {
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("page.back", backRect);
            F024ReplayGuiObserveControlBefore("page.back", backRect);
            backClicked = GUI.Button(backRect, GUIContent.none, GUIStyle.none);
            Ink(backRect, "返回", new GUIStyle(labelStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter }, new Color(.2f, .13f, .07f));
            F024ReplayGuiObserveControlAfter("page.back");
            if (backClicked) PlaySound("SFX_UI_CLICK");
        }
        else if (page == "collection" && f024CollectionTab == 1)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !f024AccountBusy && !F028DraftRequestPending &&
                !String.IsNullOrWhiteSpace(f024DraftName) &&
                F028ValidateDeckDraft(f024DraftCards.ToArray(), f024DraftClassId) == "";
            backClicked = Button(backRect, "完成", "collection.deck-complete");
            GUI.enabled = enabled;
            if (backClicked)
            {
                F028CancelCardDrag();
                F024RequestDeckSave(f024DraftId, f024DraftName.Trim(), f024DraftClassId, f024DraftCards.ToArray());
                F024ReplayGuiMarkHandler("collection.deck-complete", "F024RequestDeckSave");
            }
            return;
        }
        else backClicked = Button(backRect, "返回", "page.back");
        if (backClicked)
        {
            F028CancelCardDrag();
            if (page == "collection" && f024DraftDirty) F028RequestDeckSwitch("__leave__");
            else { page = "menu"; F024RequestAccountState(); }
            F024ReplayGuiMarkHandler("page.back", "ReturnToMenuAndRefreshAccount");
        }
    }

    string F024SceneForFrame(string assetId)
    {
        if (assetId == "CollectionManagerFrame_Texture") return "SCENE_COLLECTION_MANAGER";
        if (assetId == "PackOpeningTray") return "SCENE_PACK_OPENING";
        if (assetId == "QuestBoard_Atlas") return "SCENE_QUEST_LOG";
        if (assetId == "SCENE_MAIN_SHOP") return "SCENE_MAIN_SHOP_PC";
        return "";
    }

    string F024FallbackFrame(string assetId)
    {
        return assetId == "SCENE_MAIN_SHOP" ? "CollectionManagerFrame_Texture" : assetId;
    }

    bool F024DrawNativeImage(Rect rect, string assetId, ScaleMode scale = ScaleMode.StretchToFill)
    {
        if (assets == null || String.IsNullOrEmpty(assetId)) return false;
        Texture texture = assets.Texture(assetId);
        if (texture == null) return false;
        GUI.DrawTexture(rect, texture, scale, true);
        return true;
    }

    void F024DrawWallet(Rect rect)
    {
        if (!F024HasAccount)
        {
            GUI.Label(rect, "未登录", smallStyle);
            return;
        }
        float gap = 18;
        float column = (rect.width - gap) * .5f;
        var goldIcon = new Rect(rect.x, rect.y + (rect.height - 28) * .5f, 28, 28);
        var dustIcon = new Rect(rect.x + column + gap, goldIcon.y, 28, 28);
        if (!F024DrawNativeImage(goldIcon, "UI_GOLD", ScaleMode.ScaleToFit))
            GUI.Label(goldIcon, "G", smallStyle);
        if (!F024DrawNativeImage(dustIcon, "UI_DUST", ScaleMode.ScaleToFit))
            GUI.Label(dustIcon, "D", smallStyle);
        GUI.Label(new Rect(goldIcon.xMax + 6, rect.y, column - goldIcon.width - 6, rect.height), f024Account.gold.ToString(), smallStyle);
        GUI.Label(new Rect(dustIcon.xMax + 6, rect.y, column - dustIcon.width - 6, rect.height), f024Account.dust.ToString(), smallStyle);
    }

    void F024DrawLoginGate(Rect rect, string message)
    {
        GUI.Label(rect, message, labelStyle);
        var loginRect = new Rect(rect.x + rect.width * .34f, rect.y + 88, rect.width * .32f, 58);
        if (Button(loginRect, "登录或注册", "settings.account"))
        {
            F025OpenSettings(true);
            F024ReplayGuiMarkHandler("settings.account", "OpenAccountSettings");
        }
    }

    void DrawF024DeckPicker()
    {
        F024DrawPageFrame("传统对战 · 我的卡组", "CollectionManagerFrame_Texture", new Rect(80, 85, 1440, 820));
        if (!F024HasAccount)
        {
            F024DrawLoginGate(new Rect(300, 380, 1000, 130), "请登录账号以读取服务器卡组");
            return;
        }
        var serverDecks = (f024Account.decks ?? new F024AccountDeck[0]).Where(item => item != null).ToArray();
        F024DrawWallet(new Rect(1150, 122, 310, 40));
        var deckViewport = new Rect(125, 185, 430, 630);
        f024DeckListScroll = GUI.BeginScrollView(deckViewport, f024DeckListScroll,
            new Rect(0, 0, 390, Mathf.Max(1, serverDecks.Length) * 66));
        for (int i = 0; i < serverDecks.Length; i++)
        {
            var item = serverDecks[i];
            var row = new Rect(4, i * 66, 380, 58);
            bool replayGui = F024ReplayGuiEnabled;
            string replayTarget = replayGui ? "decks.select:" + item.deck_id : "";
            if (replayGui) F024ReplayGuiRegisterTarget(replayTarget, row, deckViewport, f024DeckListScroll);
            if (item.deck_id == f024SelectedDeckId) Frame(row, new Color(.96f, .75f, .27f), 2);
            bool selected = GUI.Button(row, item.name + " · " + F024ClassName(item.class_id) + " · " + (item.cards ?? new string[0]).Length + "/30", smallStyle);
            if (selected)
            {
                F024SelectServerDeck(item.deck_id);
                if (replayGui) F024ReplayGuiMarkHandler(replayTarget, "F024SelectServerDeck");
            }
        }
        GUI.EndScrollView();

        var selectedDeck = serverDecks.FirstOrDefault(item => item.deck_id == f024SelectedDeckId);
        if (selectedDeck != null)
        {
            GUI.Label(new Rect(625, 190, 700, 54), selectedDeck.name, titleStyle);
            GUI.Label(new Rect(635, 250, 680, 42), F024ClassName(selectedDeck.class_id) + " · " + (selectedDeck.cards ?? new string[0]).Length + " 张", labelStyle);
            string validation = F024ValidateOwnedDeck(selectedDeck.cards, selectedDeck.class_id);
            if (validation == "")
            {
                GUI.Label(new Rect(635, 305, 680, 42), "服务器持有卡牌已验证，可以开始对战", smallStyle);
            }
            else GUI.Label(new Rect(635, 305, 680, 56), validation, smallStyle);
            int classAt = Array.FindIndex(database.classes, item => item.id == selectedDeck.class_id);
            if (classAt >= 0 && Event.current.type == EventType.Repaint)
            {
                Texture hero = cardVisuals.Render(cards[database.classes[classAt].hero], "hero");
                if (hero != null) GUI.DrawTexture(new Rect(805, 360, 260, 310), hero, ScaleMode.ScaleToFit, true);
            }
            var savedCards = (selectedDeck.cards ?? new string[0]).GroupBy(id => id)
                .Select(group => (cards.ContainsKey(group.Key) ? cards[group.Key].Name : group.Key) + " × " + group.Count())
                .Take(10).ToArray();
            GUI.Label(new Rect(1110, 365, 310, 330), String.Join("\n", savedCards), smallStyle);
            bool valid = validation == "" && !f024AccountBusy;
            bool priorEnabled = GUI.enabled;
            GUI.enabled = priorEnabled && valid;
            if (Button(new Rect(710, 730, 230, 62), "进入传统对战", "decks.start"))
            {
                Connect();
                F024ReplayGuiMarkHandler("decks.start", "Connect");
            }
            GUI.enabled = priorEnabled;
            if (Button(new Rect(970, 730, 230, 62), "编辑卡组"))
            {
                F024BeginDeckDraft(selectedDeck);
                f024CollectionTab = 1;
                F024OpenPage("collection");
            }
            if (Button(new Rect(1230, 730, 220, 62), "复制并组建"))
            {
                F024BeginDeckDraft(selectedDeck);
                f024DraftId = "";
                f024DraftName = selectedDeck.name + " 副本";
                f024DraftDirty = true;
                f024CollectionTab = 1;
                F024OpenPage("collection");
            }
        }
        else GUI.Label(new Rect(625, 270, 700, 60), "账号中还没有服务器卡组。", labelStyle);
        if (!String.IsNullOrEmpty(f024AccountMessage)) GUI.Label(new Rect(210, 835, 1160, 38), f024AccountMessage, smallStyle);
    }

    void F024SelectServerDeck(string deckId)
    {
        f024SelectedDeckId = deckId ?? "";
        f024DraftDirty = false;
        F024RefreshDeckDraftFromSnapshot();
        var selectedDeck = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null && item.deck_id == f024SelectedDeckId);
        if (selectedDeck != null)
        {
            deck = (selectedDeck.cards ?? new string[0]).ToList();
            int classAt = Array.FindIndex(database.classes, item => item.id == selectedDeck.class_id);
            if (classAt >= 0) classIndex = classAt;
        }
    }

    string F024ClassName(int classId)
    {
        var item = database.classes.FirstOrDefault(definition => definition.id == classId);
        return item == null ? "未知职业" : item.name;
    }

    void DrawF024CollectionPage()
    {
        bool previousEnabled = GUI.enabled;
        if (F028DeckModalOpen || F028DraftRequestPending) GUI.enabled = false;
        F024DrawPageFrame("", "CollectionManagerFrame_Texture", new Rect(35, 75, 1530, 835));
        if (!F024HasAccount)
        {
            GUI.enabled = previousEnabled;
            F024DrawLoginGate(new Rect(300, 390, 1000, 140), "登录账号后可查看服务器收藏和卡组");
            return;
        }
        Rect paper = F025CollectionPaperRect;
        float inset = Mathf.Clamp(paper.width * .035f, 18, 34);
        float innerWidth = paper.width - inset * 2;
        if (f024CollectionTab == 0)
            DrawF024ClassFilters(new Rect(paper.x + inset, paper.y + 10, innerWidth, 58));
        else
            GUI.Label(new Rect(paper.x + inset, paper.y + 10, innerWidth, 58),
                F024ClassName(f024DraftClassId) + " · 选择收藏卡牌加入套牌", F024PaperLabelStyle);
        f028HoveredCard = "";
        DrawF024CardBook();
        F028FinishCardDrag();
        F028DrawCardPreview();
        GUI.enabled = previousEnabled;
        if (f028OfferClipboardImport) F028DrawClipboardOffer();
        if (f028ConfirmDiscard) F028DrawDiscardConfirmation();
    }

    void DrawF024CardBook()
    {
        if ((f032PageTurnPending || f032PageTurnActive) &&
            (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp ||
             Event.current.type == EventType.MouseDrag || Event.current.type == EventType.ScrollWheel ||
             Event.current.type == EventType.KeyDown)) Event.current.Use();
        Rect paper = F025CollectionPaperRect;
        float inset = Mathf.Clamp(paper.width * .035f, 18, 34);
        float innerX = paper.x + inset;
        float innerWidth = paper.width - inset * 2;
        float filterY = paper.yMax - 44;
        float gap = 7;
        var setFilterRect = new Rect(innerX - 16, paper.yMax - 64, 108, 70);
        float manaWidth = Mathf.Clamp(innerWidth * .39f, 270, 340);
        var manaRect = new Rect(setFilterRect.xMax + gap, filterY, manaWidth, 38);
        int[] collectionSets = F024CollectionSetIds();
        Rect setPopupRect = F024CollectionSetDropdownRect(setFilterRect);
        if (F025HubOverlayOpen)
        {
            f024CollectionSetDropdownOpen = false;
            f031SetTrayProgress = 0;
            f031SetTrayAnimating = false;
        }
        F031UpdateSetTrayAnimation();
        F024HandleCollectionSetDropdownDismiss(setPopupRect, setFilterRect);
        if (f024CollectionSetDropdownOpen && !f031SetTrayAnimating)
            F024DrawCollectionSetDropdownControls(setPopupRect, collectionSets);
        Rect movingRegion = f031SetTrayAnimating ? setPopupRect : Rect.zero;
        if (f031SetTrayAnimating) movingRegion.x -= setPopupRect.width * (1f - f031SetTrayProgress);
        GUI.Button(movingRegion, GUIContent.none, GUIStyle.none);

        string[] ownershipLabels = { "全部", "已拥有", "未拥有" };
        float ownershipWidth = Mathf.Clamp(innerWidth * .32f, 240, 330);
        float ownershipCellWidth = ownershipWidth / ownershipLabels.Length;
        for (int i = 0; f029CraftingMode && i < ownershipLabels.Length; i++)
        {
            var rect = new Rect(innerX + i * ownershipCellWidth, paper.y + 72, ownershipCellWidth - 4, 34);
            bool selected = f024CollectionOwnedMode == i;
            string target = "collection.filter.ownership." + i;
            F024HighlightPaperCell(rect, selected);
            GUI.Label(rect, ownershipLabels[i], F024PaperSelectorStyle);
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect);
            F024ReplayGuiObserveControlBefore(target, rect);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            F024ReplayGuiObserveControlAfter(target);
            if (clicked && f024CollectionOwnedMode != i)
            {
                f024CollectionOwnedMode = i;
                f024CollectionPage = 0;
                F024ReplayGuiMarkHandler(target, "ChangeCollectionOwnershipFilter");
            }
        }
        DrawF024CollectionSetFilter(setFilterRect, collectionSets, setPopupRect);
        float pagerWidth = Mathf.Clamp(innerWidth * .15f, 120, 150);
        float pagerX = innerX + innerWidth - pagerWidth;
        float searchLabelWidth = 0;
        float searchX = manaRect.xMax + gap;
        float craftWidth = 100;
        float searchWidth = Mathf.Max(90, pagerX - gap - searchX - searchLabelWidth - craftWidth - gap);
        float textX = searchX + searchLabelWidth;
        var searchRect = new Rect(textX, filterY, searchWidth, 34);
        if (!F026DrawWidget(searchRect, "collection.search.background") && Event.current.type == EventType.Repaint)
        {
            Fill(searchRect, new Color(.24f, .055f, .06f, .98f));
            Frame(searchRect, new Color(.43f, .25f, .19f, .96f), 2);
        }
        var inputRect = searchRect;
        bool showClearButton = !String.IsNullOrEmpty(f024CollectionSearch);
        float clearSize = Mathf.Min(22, searchRect.height - 6);
        var clearRect = new Rect(searchRect.xMax - clearSize - 5,
            searchRect.center.y - clearSize * .5f, clearSize, clearSize);
        // Reserve the clear slot from the start so the named TextField keeps a stable control ID while typing.
        inputRect.width = clearRect.x - inputRect.x - 2;
        const string searchTarget = "collection.search.input";
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(searchTarget, inputRect);
        if (F024ReplayGuiEnabled && f024ReplayGuiCurrent != null && f024ReplayGuiCurrent.target == searchTarget)
        {
            f024ReplayCollectionSearchFocusedControlBefore = GUI.GetNameOfFocusedControl() ?? "";
            f024ReplayCollectionSearchKeyboardControlBefore = GUIUtility.keyboardControl;
        }
        F024ReplayGuiObserveControlBefore(searchTarget, inputRect);
        GUI.SetNextControlName(searchTarget);
        f024CollectionSearch = GUI.TextField(inputRect, f024CollectionSearch, 48, F024CollectionSearchStyle);
        F024ReplayGuiObserveControlAfter(searchTarget);
        if (F024ReplayGuiEnabled)
        {
            f024ReplayCollectionSearchFocusedControl = GUI.GetNameOfFocusedControl() ?? "";
            f024ReplayCollectionSearchKeyboardControl = GUIUtility.keyboardControl;
        }
        F024ReplayGuiMarkHandler(searchTarget, "FocusCollectionSearch");
        if (showClearButton)
        {
            const string clearTarget = "collection.search.clear";
            if (!F026DrawWidget(clearRect, clearTarget) && Event.current.type == EventType.Repaint)
                GUI.Label(clearRect, "x", F024PaperSelectorStyle);
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(clearTarget, clearRect);
            F024ReplayGuiObserveControlBefore(clearTarget, clearRect);
            bool clearClicked = GUI.Button(clearRect, GUIContent.none, GUIStyle.none);
            F024ReplayGuiObserveControlAfter(clearTarget);
            if (clearClicked)
            {
                f024CollectionSearch = "";
                f024CollectionPage = 0;
                F024ReplayGuiMarkHandler(clearTarget, "ClearCollectionSearch");
            }
        }

        {
            var craftRect = new Rect(searchRect.xMax + gap, filterY, craftWidth, 34);
            F024HighlightPaperCell(craftRect, f029CraftingMode || craftRect.Contains(Event.current.mousePosition));
            if (Button(craftRect, f029CraftingMode ? "完成" : "卡牌制作", "collection.crafting-mode"))
            {
                f029CraftingMode = !f029CraftingMode;
                f024CollectionOwnedMode = 0;
                f024CollectionPage = 0;
                F024ReplayGuiMarkHandler("collection.crafting-mode", "ToggleCollectionCraftingMode");
            }
        }

        F024BuildCollectionList();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(f024FilteredCards.Length / (float)F024CollectionPageSize));
        float pageTextWidth = pagerWidth * .46f;
        float pageButtonWidth = (pagerWidth - pageTextWidth - 8) * .5f;
        GUI.Label(new Rect(pagerX, filterY, pageTextWidth, 34),
            (f024CollectionPage + 1) + "/" + pageCount, F024PaperSelectorStyle);
        string previousPageTarget = "collection.page.previous";
        var previousPageRect = new Rect(pagerX + pageTextWidth + 4, filterY, pageButtonWidth, 34);
        int pageBeforeClick = f024CollectionPage;
        if (F029CollectionPageButton(previousPageRect, "<", previousPageTarget))
        {
            f024CollectionPage = Mathf.Max(0, f024CollectionPage - 1);
            if (f024CollectionPage != pageBeforeClick)
            {
                F030PlayCollectionPageSound(false);
                F032BeginPageTurn(false, pageBeforeClick, paper);
                F024ReplayGuiMarkHandler(previousPageTarget, "ChangeCollectionPage");
            }
        }
        string nextPageTarget = "collection.page.next";
        var nextPageRect = new Rect(pagerX + pageTextWidth + 4 + pageButtonWidth + 4, filterY, pageButtonWidth, 34);
        pageBeforeClick = f024CollectionPage;
        if (F029CollectionPageButton(nextPageRect, ">", nextPageTarget))
        {
            f024CollectionPage = Mathf.Min(pageCount - 1, f024CollectionPage + 1);
            if (f024CollectionPage != pageBeforeClick)
            {
                F030PlayCollectionPageSound(true);
                F032BeginPageTurn(true, pageBeforeClick, paper);
                F024ReplayGuiMarkHandler(nextPageTarget, "ChangeCollectionPage");
            }
        }

        DrawF024ManaFilters(manaRect);
        float gridY = paper.y + 120;
        float gridBottom = filterY - 8;
        float gridHeight = Mathf.Max(120, gridBottom - gridY);
        float rowHeight = gridHeight / 2f;
        float gapX = Mathf.Clamp(innerWidth * .012f, 4, 12);
        float cellWidth = (innerWidth - gapX * 3) / 4f;
        for (int i = 0; i < F024CollectionPageSize; i++)
        {
            int displayPage = f032PageTurnPending ? f032PreviousPage : f024CollectionPage;
            int index = displayPage * F024CollectionPageSize + i;
            if (index >= f024FilteredCards.Length) break;
            int row = i / 4, column = i % 4;
            var rect = new Rect(innerX + column * (cellWidth + gapX), gridY + row * rowHeight, cellWidth, rowHeight - 2);
            DrawF024CollectionCard(rect, f024FilteredCards[index]);
        }
        if (f032PageTurnActive && f032PageTurn != null && Event.current.type == EventType.Repaint)
        {
            Matrix4x4 inverse = GUI.matrix.inverse;
            Vector3 topLeft = inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 bottomRight = inverse.MultiplyPoint3x4(new Vector3(Screen.width, Screen.height, 0));
            Rect viewport = Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
            float speed = f032PageTurnRight ? 1.6f : 2f;
            Rect pageClip = new Rect(paper.x, gridY - 8, paper.width, gridBottom - gridY + 8);
            GUI.BeginGroup(pageClip);
            viewport.position -= pageClip.position;
            GUI.DrawTexture(viewport, f032PageTurn.Render(f032PageTurnRight, (Time.unscaledTime - f032PageTurnStart) * speed), ScaleMode.StretchToFill, true);
            GUI.EndGroup();
        }
        if (f024CollectionTab == 0) DrawF025CollectionDeckTray(F025CollectionDeckTrayRect);
        else F028DrawDeckEditor(F025CollectionDeckTrayRect);
        if (f031SetTrayProgress > 0f)
        {
            Rect animatedPopup = setPopupRect;
            animatedPopup.x -= setPopupRect.width * (1f - f031SetTrayProgress);
            F024DrawCollectionSetDropdownOverlay(animatedPopup, collectionSets);
        }
    }

    void F032BeginPageTurn(bool right, int previousPage, Rect paper)
    {
        if (f032PageTurnPending || f032PageTurnActive) return;
        if (!NativeCollectionPageTurn.IsAvailable(assets)) return;
        Matrix4x4 matrix = GUI.matrix;
        Vector3 topLeft = matrix.MultiplyPoint3x4(new Vector3(paper.xMin, paper.yMin, 0));
        Vector3 bottomRight = matrix.MultiplyPoint3x4(new Vector3(paper.xMax, paper.yMax, 0));
        f032PreviousPage = previousPage;
        f032PageTurnRight = right;
        f032PageTurnPending = true;
        StartCoroutine(F032CaptureAndTurn(topLeft, bottomRight));
    }

    IEnumerator F032CaptureAndTurn(Vector3 topLeft, Vector3 bottomRight)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            if (page != "collection" || !F032TryCapturePage(topLeft, bottomRight)) yield break;
            f032PageTurnPending = false;
            f032PageTurnActive = true;
            f032PageTurnStart = Time.unscaledTime;
            float duration = f032PageTurnRight ? f032PageTurn.RightDuration / 1.6f : f032PageTurn.LeftDuration / 2f;
            while (page == "collection" && Time.unscaledTime - f032PageTurnStart < duration) yield return null;
        }
        finally
        {
            f032PageTurnPending = false;
            f032PageTurnActive = false;
            if (!resourcesReleased && f032PageTurn != null) f032PageTurn.SetPageTexture(null);
            if (f032PageSnapshot != null) RenderTexture.ReleaseTemporary(f032PageSnapshot);
            f032PageSnapshot = null;
        }
    }

    bool F032TryCapturePage(Vector3 topLeft, Vector3 bottomRight)
    {
        RenderTexture screen = null;
        try
        {
            screen = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
            int x = Mathf.Clamp(Mathf.FloorToInt(topLeft.x), 0, screen.width - 1);
            int y = Mathf.Clamp(screen.height - Mathf.CeilToInt(bottomRight.y), 0, screen.height - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(bottomRight.x), x + 1, screen.width);
            int yMax = Mathf.Clamp(screen.height - Mathf.FloorToInt(topLeft.y), y + 1, screen.height);
            f032PageSnapshot = RenderTexture.GetTemporary(xMax - x, yMax - y, 0, RenderTextureFormat.ARGB32);
            bool flipY = SystemInfo.graphicsUVStartsAtTop;
            Graphics.Blit(screen, f032PageSnapshot,
                new Vector2((xMax - x) / (float)screen.width, (flipY ? -1f : 1f) * (yMax - y) / screen.height),
                new Vector2(x / (float)screen.width, (flipY ? screen.height - y : y) / (float)screen.height));
            if (f032PageTurn == null) f032PageTurn = new NativeCollectionPageTurn(assets);
            f032PageTurn.SetPageTexture(f032PageSnapshot);
            f032PageTurn.SetScreenProjection(Screen.width, Screen.height);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning("收藏页翻页动画不可用，已直接切页：" + error.Message);
            return false;
        }
        finally { if (screen != null) RenderTexture.ReleaseTemporary(screen); }
    }

    bool F029CollectionPageButton(Rect rect, string text, string target)
    {
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect);
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, text, F024ButtonStyle(rect));
        F024ReplayGuiObserveControlAfter(target);
        return clicked;
    }

    void F030PlayCollectionPageSound(bool forward)
    {
        string id = forward ? "SFX_COLLECTION_PAGE_FORWARD" : "SFX_COLLECTION_PAGE_BACK";
        int variant = UnityEngine.Random.Range(0, 4);
        if (variant != 0) id += "_" + (variant + 1);
        if (!sounds.TryGetValue(id, out var clip) || f031PageTurnSources == null) return;
        AudioSource source = f031PageTurnSources[f031PageTurnSourceIndex];
        f031PageTurnSourceIndex = (f031PageTurnSourceIndex + 1) % f031PageTurnSources.Length;
        source.Stop();
        source.pitch = UnityEngine.Random.Range(.7f, 1f);
        source.PlayOneShot(clip, .4f);
    }

    int[] F024CollectionSetIds()
    {
        return database.cards.Where(card => F024IsBookCard(card) && card.Collectible)
            .Select(card => card.Set).Distinct().OrderBy(setId => setId).ToArray();
    }

    void DrawF024CollectionSetFilter(Rect rect, int[] sets, Rect popupRect)
    {
        if (sets == null) sets = new int[0];
        if (f024CollectionSet >= 0 && !sets.Contains(f024CollectionSet)) f024CollectionSet = -1;
        const string dropdownTarget = "collection.set.dropdown";
        bool highlighted = f024CollectionSetDropdownOpen || rect.Contains(Event.current.mousePosition);
        if (!F026DrawWidget(rect, highlighted ? "collection.set.button.hover" : "collection.set.button") && Event.current.type == EventType.Repaint)
            F024HighlightPaperCell(rect, highlighted);
        if (F024ReplayGuiEnabled && !f031SetTrayAnimating) F024ReplayGuiRegisterTarget(dropdownTarget, rect);
        F024ReplayGuiObserveControlBefore(dropdownTarget, rect);
        bool previouslyEnabled = GUI.enabled;
        GUI.enabled = previouslyEnabled && !f031SetTrayAnimating;
        bool dropdownClicked = GUI.Button(rect, new GUIContent("", f024CollectionSet < 0 ? "全部扩展包" : F024SetName(f024CollectionSet)), GUIStyle.none);
        GUI.enabled = previouslyEnabled;
        F024ReplayGuiObserveControlAfter(dropdownTarget);
        if (dropdownClicked)
        {
            F031SetCollectionSetTrayOpen(!f024CollectionSetDropdownOpen);
            F030PlayCollectionFilterSound(f024CollectionSetDropdownOpen);
            if (f024CollectionSetDropdownOpen)
                F024EnsureCollectionSetDropdownSelectionVisible(popupRect, sets);
            F024ReplayGuiMarkHandler(dropdownTarget, "ToggleCollectionSetDropdown");
        }

    }

    void F031UpdateSetTrayAnimation()
    {
        if (!f031SetTrayAnimating) return;
        float duration = f024CollectionSetDropdownOpen ? .35f : .25f;
        float t = Mathf.Clamp01((Time.unscaledTime - f031SetTrayStartedAt) / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        f031SetTrayProgress = Mathf.Lerp(f031SetTrayStartProgress, f024CollectionSetDropdownOpen ? 1f : 0f, eased);
        if (t >= 1f) f031SetTrayAnimating = false;
    }

    void F031SetCollectionSetTrayOpen(bool open)
    {
        F031UpdateSetTrayAnimation();
        if (f024CollectionSetDropdownOpen == open) return;
        f024CollectionSetDropdownOpen = open;
        f031SetTrayStartProgress = f031SetTrayProgress;
        f031SetTrayStartedAt = Time.unscaledTime;
        f031SetTrayAnimating = true;
    }

    void F030PlayCollectionFilterSound(bool open)
    {
        string id = open ? "SFX_COLLECTION_FILTER_OPEN" : "SFX_COLLECTION_FILTER_CLOSE";
        if (sounds.TryGetValue(id, out var clip)) sfx.PlayOneShot(clip, .3f);
    }

    GUIStyle F024CollectionSetDropdownLabelStyle
    {
        get
        {
            if (f024CollectionSetDropdownLabelStyle == null)
            {
                f024CollectionSetDropdownLabelStyle = new GUIStyle(F024PaperSelectorStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    padding = new RectOffset(10, 8, 1, 1),
                    clipping = TextClipping.Clip
                };
                f024CollectionSetDropdownLabelStyle.normal.textColor = new Color(.93f, .86f, .69f);
                f024CollectionSetDropdownLabelStyle.hover.textColor = Color.white;
                f024CollectionSetDropdownLabelStyle.active.textColor = new Color(.93f, .86f, .69f);
                f024CollectionSetDropdownLabelStyle.focused.textColor = new Color(.93f, .86f, .69f);
            }
            return f024CollectionSetDropdownLabelStyle;
        }
    }

    Rect F024CollectionSetDropdownRect(Rect selectorRect)
    {
        Rect paper = F025CollectionPaperRect;
        float width = Mathf.Min(Mathf.Max(selectorRect.width, 200), paper.width - 12);
        float height = Mathf.Clamp(selectorRect.y - paper.y - 90, 160, 300);
        float x = Mathf.Clamp(selectorRect.x, paper.x + 6, paper.xMax - width - 6);
        return new Rect(x, selectorRect.y - height - 4, width, height);
    }

    Rect F024CollectionSetDropdownViewport(Rect popupRect)
    {
        return new Rect(popupRect.x + 4, popupRect.y + F024CollectionSetDropdownHeaderHeight,
            Mathf.Max(80, popupRect.width - 8), Mathf.Max(72, popupRect.height - F024CollectionSetDropdownHeaderHeight - 9));
    }

    void F024EnsureCollectionSetDropdownSelectionVisible(Rect popupRect, int[] sets)
    {
        if (sets == null) sets = new int[0];
        Rect viewport = F024CollectionSetDropdownViewport(popupRect);
        int selectedIndex = f024CollectionSet < 0 ? 0 : Array.IndexOf(sets, f024CollectionSet) + 1;
        float rowTop = Mathf.Max(0, selectedIndex) * F024CollectionSetDropdownRowHeight;
        if (rowTop < f024CollectionSetDropdownScroll.y)
            f024CollectionSetDropdownScroll.y = rowTop;
        else if (rowTop + F024CollectionSetDropdownRowHeight >
            f024CollectionSetDropdownScroll.y + viewport.height)
            f024CollectionSetDropdownScroll.y = rowTop + F024CollectionSetDropdownRowHeight - viewport.height;
        float maxScroll = Mathf.Max(0, (sets.Length + 1) * F024CollectionSetDropdownRowHeight - viewport.height);
        f024CollectionSetDropdownScroll.y = Mathf.Clamp(f024CollectionSetDropdownScroll.y, 0, maxScroll);
    }

    void F024HandleCollectionSetDropdownDismiss(Rect popupRect, Rect selectorRect)
    {
        if (!f024CollectionSetDropdownOpen) return;
        Event current = Event.current;
        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            F031SetCollectionSetTrayOpen(false);
            F030PlayCollectionFilterSound(false);
            current.Use();
            return;
        }
        if (current.type == EventType.MouseDown &&
            !popupRect.Contains(current.mousePosition) && !selectorRect.Contains(current.mousePosition))
        {
            F031SetCollectionSetTrayOpen(false);
            F030PlayCollectionFilterSound(false);
        }
    }

    void F024DrawCollectionSetDropdownControls(Rect popupRect, int[] sets)
    {
        Rect closeRect = new Rect(popupRect.xMax - 24, popupRect.y + 2, 20, 19);
        const string closeTarget = "collection.set.dropdown.close";
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(closeTarget, closeRect);
        F024ReplayGuiObserveControlBefore(closeTarget, closeRect);
        bool closeClicked = GUI.Button(closeRect, GUIContent.none, GUIStyle.none);
        F024ReplayGuiObserveControlAfter(closeTarget);
        if (closeClicked)
        {
            F031SetCollectionSetTrayOpen(false);
            F030PlayCollectionFilterSound(false);
            F024ReplayGuiMarkHandler(closeTarget, "CloseCollectionSetDropdown");
            return;
        }

        if (sets == null) sets = new int[0];
        Rect viewport = F024CollectionSetDropdownViewport(popupRect);
        float contentWidth = Mathf.Max(48, viewport.width - 16);
        float contentHeight = Mathf.Max(viewport.height, (sets.Length + 1) * F024CollectionSetDropdownRowHeight);
        var contentRect = new Rect(0, 0, contentWidth, contentHeight);
        f024CollectionSetDropdownScroll = GUI.BeginScrollView(viewport, f024CollectionSetDropdownScroll,
            contentRect, false, true);
        for (int i = 0; i <= sets.Length; i++)
        {
            int setId = i == 0 ? -1 : sets[i - 1];
            var row = new Rect(0, i * F024CollectionSetDropdownRowHeight, contentWidth,
                F024CollectionSetDropdownRowHeight);
            if (!row.Overlaps(new Rect(f024CollectionSetDropdownScroll.x, f024CollectionSetDropdownScroll.y,
                    viewport.width, viewport.height))) continue;
            string target = "collection.set.item." + (setId < 0 ? "all" : setId.ToString());
            if (F024ReplayGuiEnabled)
                F024ReplayGuiRegisterTarget(target, row, viewport, f024CollectionSetDropdownScroll);
            F024ReplayGuiObserveControlBefore(target, row);
            bool clicked = GUI.Button(row, GUIContent.none, GUIStyle.none);
            F024ReplayGuiObserveControlAfter(target);
            if (!clicked) continue;
            f024CollectionSet = setId;
            f024CollectionPage = 0;
            F031SetCollectionSetTrayOpen(false);
            F030PlayCollectionFilterSound(false);
            f024CollectionSetDropdownScroll = Vector2.zero;
            F024ReplayGuiMarkHandler(target, "ChangeCollectionSet");
            PlaySound("SFX_UI_CLICK");
            break;
        }
        GUI.EndScrollView();
    }

    void F024DrawCollectionSetDropdownOverlay(Rect popupRect, int[] sets)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (sets == null) sets = new int[0];
        F024DrawCollectionSetDropdownPanel(popupRect);
        var headerRect = new Rect(popupRect.x + 8, popupRect.y + 1, popupRect.width - 36,
            F024CollectionSetDropdownHeaderHeight - 2);
        GUI.Label(headerRect, "选择扩展包", F024CollectionSetDropdownLabelStyle);
        var closeRect = new Rect(popupRect.xMax - 24, popupRect.y + 2, 20, 19);
        GUI.Label(closeRect, "x", F024PaperSelectorStyle);

        Rect viewport = F024CollectionSetDropdownViewport(popupRect);
        float contentWidth = Mathf.Max(48, viewport.width - 16);
        GUI.BeginGroup(viewport);
        float scrollY = f024CollectionSetDropdownScroll.y;
        int first = Mathf.Max(0, Mathf.FloorToInt(scrollY / F024CollectionSetDropdownRowHeight));
        int last = Mathf.Min(sets.Length, Mathf.CeilToInt((scrollY + viewport.height) /
            F024CollectionSetDropdownRowHeight));
        Vector2 mouse = Event.current.mousePosition;
        for (int i = first; i <= last; i++)
        {
            int setId = i == 0 ? -1 : sets[i - 1];
            var row = new Rect(0, i * F024CollectionSetDropdownRowHeight - scrollY,
                contentWidth, F024CollectionSetDropdownRowHeight);
            if (row.yMax <= 0 || row.y >= viewport.height) continue;
            bool selected = f024CollectionSet == setId;
            bool hovered = row.Contains(new Vector2(mouse.x - viewport.x, mouse.y - viewport.y));
            string widget = selected ? "collection.set.dropdown.selected" :
                hovered ? "collection.set.dropdown.highlight" : "";
            if (!String.IsNullOrEmpty(widget) && !F026DrawWidget(row, widget))
            {
                Fill(row, selected ? new Color(.47f, .33f, .17f, .9f) : new Color(.64f, .5f, .31f, .46f));
                Frame(row, new Color(.78f, .65f, .43f, .55f), 1);
            }
            string label = setId < 0 ? "全部扩展包" : F024SetName(setId);
            GUI.Label(row, label, F024CollectionSetDropdownLabelStyle);
        }
        GUI.EndGroup();

        float contentHeight = (sets.Length + 1) * F024CollectionSetDropdownRowHeight;
        if (contentHeight > viewport.height)
        {
            var track = new Rect(viewport.xMax - 8, viewport.y + 4, 4, viewport.height - 8);
            float thumbHeight = Mathf.Max(20, track.height * viewport.height / contentHeight);
            float maxScroll = contentHeight - viewport.height;
            float thumbY = track.y + (track.height - thumbHeight) *
                (maxScroll <= 0 ? 0 : Mathf.Clamp01(f024CollectionSetDropdownScroll.y / maxScroll));
            Fill(track, new Color(.08f, .055f, .035f, .8f));
            var thumb = new Rect(track.x, thumbY, track.width, thumbHeight);
            Fill(thumb, new Color(.72f, .60f, .40f, .9f));
            Frame(thumb, new Color(.25f, .16f, .08f, .8f), 1);
        }
    }

    void F024DrawCollectionSetDropdownPanel(Rect rect)
    {
        Fill(rect, new Color(.12f, .09f, .055f, .98f));
        float edgeHeight = Mathf.Min(14, rect.height * .16f);
        var top = new Rect(rect.x, rect.y, rect.width, edgeHeight);
        var middle = new Rect(rect.x, top.yMax - 2, rect.width, Mathf.Max(1, rect.height - edgeHeight * 2 + 4));
        var bottom = new Rect(rect.x, rect.yMax - edgeHeight, rect.width, edgeHeight);
        F026DrawWidget(top, "collection.set.dropdown.panel.top");
        F026DrawWidget(middle, "collection.set.dropdown.panel.middle");
        F026DrawWidget(bottom, "collection.set.dropdown.panel.bottom");
        Frame(rect, new Color(.53f, .40f, .24f, .96f), 1);
    }

    void DrawF024ClassFilters(Rect rect)
    {
        var values = new List<int> { -1, 12 };
        var labels = new List<string> { "全部", "中立" };
        for (int i = 0; i < database.classes.Length; i++)
        {
            values.Add(database.classes[i].id);
            labels.Add(database.classes[i].name);
        }
        int selected = values.IndexOf(f024CollectionClass);
        if (selected < 0) selected = 0;
        float cellWidth = rect.width / values.Count;
        Texture tabFrame = Event.current.type == EventType.Repaint && assets != null
            ? assets.Texture("ClassTabs")
            : null;
        var classTabSourceUv = new Rect(.10131836f, .00655746f, .80102539f, .82059097f);
        for (int i = 0; i < values.Count; i++)
        {
            var cell = new Rect(rect.x + i * cellWidth, rect.y, cellWidth, rect.height);
            int value = values[i];
            string target = value < 0 ? "collection.filter.class.all" :
                value == 12 ? "collection.filter.class.neutral" : "collection.filter.class." + value;
            bool isHeroClass = value >= 0 && value != 12;
            var tabRect = new Rect(cell.center.x - cell.width * .39f, cell.y,
                cell.width * .78f, cell.height - 17);
            if (isHeroClass)
            {
                int classIndex = Array.FindIndex(database.classes, item => item.id == value);
                Texture icon = null;
                CardDef heroCard;
                if (classIndex >= 0 && cards.TryGetValue(database.classes[classIndex].hero ?? "", out heroCard) &&
                    Event.current.type == EventType.Repaint)
                    icon = assets == null ? null : assets.Texture("CARD_" + heroCard.CardId);
                if (tabFrame != null && Event.current.type == EventType.Repaint)
                    GUI.DrawTextureWithTexCoords(tabRect, tabFrame, classTabSourceUv, true);
                else if (Event.current.type == EventType.Repaint)
                {
                    F024HighlightPaperCell(tabRect, selected == i);
                    Frame(tabRect, selected == i ? new Color(.93f, .70f, .25f) : new Color(.43f, .30f, .15f), 1);
                }
                var iconRect = new Rect(cell.center.x - Mathf.Min(17, cell.width * .24f), cell.y + 4,
                    Mathf.Min(34, cell.width * .48f), cell.height - 25);
                if (icon != null && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleAndCrop, true);
                if (selected == i && Event.current.type == EventType.Repaint)
                    Frame(tabRect, new Color(1f, .80f, .30f, .95f), 2);
                GUI.Label(new Rect(cell.x + 1, cell.yMax - 18, cell.width - 2, 18), labels[i], F024PaperSelectorStyle);
            }
            else
            {
                F024HighlightPaperCell(cell, selected == i);
            }
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, cell);
            F024ReplayGuiObserveControlBefore(target, cell);
            bool clicked = GUI.Button(cell, isHeroClass ? GUIContent.none : new GUIContent(labels[i]),
                isHeroClass ? GUIStyle.none : F024PaperSelectorStyle);
            F024ReplayGuiObserveControlAfter(target);
            if (clicked && f024CollectionClass != value)
            {
                f024CollectionClass = value;
                f024CollectionPage = 0;
                if (F024ReplayGuiEnabled) F024ReplayGuiMarkHandler(target, "ChangeCollectionClassFilter");
            }
        }
    }

    void DrawF024ManaFilters(Rect rect)
    {
        string[] labels = { "0", "1", "2", "3", "4", "5", "6", "7+" };
        float cellWidth = Mathf.Min(48, (rect.width - 14) / labels.Length);
        var filterBackground = new Rect(rect.x, rect.y, cellWidth * labels.Length + 14, rect.height);
        if (!F026DrawWidget(filterBackground, "collection.mana.background") && Event.current.type == EventType.Repaint)
        {
            Fill(filterBackground, new Color(.90f, .82f, .64f, .86f));
            Frame(filterBackground, new Color(.35f, .25f, .13f, .92f), 1);
        }
        Texture manaGem = Event.current.type == EventType.Repaint && cardVisuals != null
            ? cardVisuals.Prop("MODEL_GEM_MANA")
            : null;
        for (int i = 0; i < labels.Length; i++)
        {
            var cell = new Rect(rect.x + 4 + i * cellWidth, rect.y, cellWidth, rect.height);
            bool highlighted = f024CollectionMana == i || cell.Contains(Event.current.mousePosition);
            if (!F026DrawWidget(cell, "collection.mana.cell") && Event.current.type == EventType.Repaint)
                F024HighlightPaperCell(cell, highlighted);
            string target = "collection.filter.mana." + i;
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, cell);
            F024ReplayGuiObserveControlBefore(target, cell);
            bool clicked = GUI.Button(cell, GUIContent.none, GUIStyle.none);
            F024ReplayGuiObserveControlAfter(target);
            float gemSize = Mathf.Min(rect.height - 2, cell.width - 4);
            var gemRect = new Rect(cell.center.x - gemSize * .5f, cell.y + (cell.height - gemSize) * .5f,
                gemSize, gemSize);
            if (Event.current.type == EventType.Repaint && manaGem != null)
            {
                GUI.DrawTexture(gemRect, manaGem, ScaleMode.ScaleToFit, true);
                if (highlighted)
                {
                    Color previous = GUI.color;
                    GUI.color = new Color(.65f, .9f, 1f, .65f);
                    GUI.DrawTexture(gemRect, manaGem, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }
            }
            GUI.Label(gemRect, labels[i], F025ManaNumberStyle);
            if (clicked)
            {
                f024CollectionMana = f024CollectionMana == i ? -1 : i;
                f024CollectionPage = 0;
                F024ReplayGuiMarkHandler(target, "ChangeCollectionManaFilter");
            }
        }
    }

    void F024BuildCollectionList()
    {
        string accountId = f024Account == null ? "" : (f024Account.account_id ?? "");
        string key = accountId + ":" + (f024Account == null ? 0 : f024Account.revision) + ":" + f024CollectionTab + ":" + f029CraftingMode + ":" + f024DraftClassId + ":" + f024CollectionClass + ":" +
            f024CollectionMana + ":" + f024CollectionSet + ":" + f024CollectionOwnedMode + ":" + f024CollectionPage + ":" + f024CollectionSearch;
        if (key == f024CollectionCacheKey) return;
        f024CollectionCacheKey = key;
        if (cardVisuals != null) cardVisuals.Clear();
        string query = (f024CollectionSearch ?? "").Trim();
        var pool = database.cards.Where(F024IsBookCard)
            .Where(card => card.Collectible)
            .Where(card => f024CollectionTab != 1 || f029CraftingMode || (card.Playable && F024OwnedCardCount(card.CardId) > 0))
            .Where(card => f024CollectionTab == 1 ? (card.Class == 12 || card.Class == f024DraftClassId) :
                (f024CollectionClass < 0 || card.Class == f024CollectionClass))
            .Where(card => f024CollectionMana < 0 || (f024CollectionMana == 7 ? card.Cost >= 7 : card.Cost == f024CollectionMana))
            .Where(card => f024CollectionSet < 0 || card.Set == f024CollectionSet)
            .Where(card => f024CollectionOwnedMode == 0 || (F024OwnedCardCount(card.CardId) > 0) == (f024CollectionOwnedMode == 1))
            .Where(card => query.Length == 0 || card.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                card.CardId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(card => card.Cost).ThenBy(card => card.Set).ThenBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        f024FilteredCards = pool;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(pool.Length / (float)F024CollectionPageSize));
        f024CollectionPage = Mathf.Clamp(f024CollectionPage, 0, pageCount - 1);
        if (!pool.Any(card => card.CardId == f024SelectedCardId)) f024SelectedCardId = pool.FirstOrDefault()?.CardId ?? "";
    }

    void DrawF024CollectionCard(Rect rect, CardDef card)
    {
        if (!F024IsBookCard(card)) return;
        float cardHeight = Mathf.Max(88, rect.height - 22);
        float cardWidth = Mathf.Min(rect.width * .88f, cardHeight * (2f / 3f));
        cardHeight = cardWidth * 1.5f;
        var cardRect = new Rect(rect.x + (rect.width - cardWidth) * .5f, rect.y, cardWidth, cardHeight);
        F024DrawCardFace(cardRect, card);
        int ownedCount = F024OwnedCardCount(card.CardId);
        float countHeight = Mathf.Max(26, rect.yMax - cardRect.yMax - 1);
        float countWidth = Mathf.Clamp(rect.width * .24f, 48, 60);
        var countRect = new Rect(rect.center.x - countWidth * .5f, cardRect.yMax + 1, countWidth, countHeight);
        if (!F026DrawWidget(countRect, "collection.card.count") && Event.current.type == EventType.Repaint)
        {
            Fill(countRect, new Color(.75f, .64f, .42f, .94f));
            Frame(countRect, new Color(.28f, .19f, .09f, .88f), 2);
        }
        if (f027CountStyle == null)
            f027CountStyle = new GUIStyle(F024PaperSelectorStyle) { fontSize = 19, fontStyle = FontStyle.Bold };
        GUI.Label(countRect, "x" + ownedCount, f027CountStyle);
        bool replayGui = F024ReplayGuiEnabled;
        string replayTarget = replayGui ? "collection.card:" + card.CardId : "";
        if (replayGui) F024ReplayGuiRegisterTarget(replayTarget, rect);
        if (!F025HubOverlayOpen && !F028DeckModalOpen && GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
        {
            f024SelectedCardId = card.CardId;
            f025CollectionDetailOpen = true;
            PlaySound("SFX_COLLECTION_CARD");
            Event.current.Use();
            return;
        }
        if (f024CollectionTab == 1 && !f029CraftingMode)
        {
            F028CardInput(rect, card.CardId, false, replayTarget);
            return;
        }
        bool selected = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (selected)
        {
            f024SelectedCardId = card.CardId;
            f025CollectionDetailOpen = true;
            PlaySound("SFX_COLLECTION_CARD");
            if (replayGui) F024ReplayGuiMarkHandler(replayTarget, "SelectCollectionCard");
        }
    }

    void DrawF025CollectionDeckTray(Rect rect)
    {
        if (rect.width < 160 || rect.height < 180) return;
        var decks = ((f024Account == null ? null : f024Account.decks) ?? new F024AccountDeck[0])
            .Where(item => item != null).ToArray();
        if (!decks.Any(item => item.deck_id == f024SelectedDeckId))
            f024SelectedDeckId = decks.FirstOrDefault()?.deck_id ?? "";

        float insetX = Mathf.Clamp(rect.width * .065f, 14, 28);
        float insetY = Mathf.Clamp(rect.height * .055f, 14, 28);
        var titleRect = new Rect(rect.x + insetX, rect.y + 6, rect.width - insetX * 2, 42);
        GUI.Label(titleRect, "我的套牌", F025DeckTrayTitleStyle);
        float actionHeight = 115;
        var viewport = new Rect(rect.x + insetX, titleRect.yMax + 8, rect.width - insetX * 2,
            rect.height - titleRect.height - actionHeight - insetY * 2 - 12);
        float rowHeight = Mathf.Clamp(viewport.height * .13f, 70, 88);
        float contentHeight = Mathf.Max(viewport.height, decks.Length * rowHeight);
        f025CollectionDeckScroll = GUI.BeginScrollView(viewport, f025CollectionDeckScroll,
            new Rect(0, 0, viewport.width - 18, contentHeight));
        float rowWidth = Mathf.Min(viewport.width - 18, viewport.width * .84f);
        float rowX = (viewport.width - 18 - rowWidth) * .5f;
        for (int i = 0; i < decks.Length; i++)
        {
            var deck = decks[i];
            var row = new Rect(rowX, i * rowHeight, rowWidth, rowHeight - 5);
            string target = F024ReplayGuiEnabled ? "collection.deck:" + deck.deck_id : "";
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, row, viewport, f025CollectionDeckScroll);
            Fill(row, new Color(.11f, .07f, .035f, .94f));
            var bannerRect = new Rect(row.x + 5, row.y + 5, row.width - 10, row.height - 10);
            int classIndex = Array.FindIndex(database.classes, item => item.id == deck.class_id);
            Texture heroArt = null;
            if (classIndex >= 0)
            {
                CardDef heroCard;
                if (cards.TryGetValue(database.classes[classIndex].hero ?? "", out heroCard) &&
                    assets != null && Event.current.type == EventType.Repaint)
                    heroArt = assets.Texture("CARD_" + heroCard.CardId);
            }
            if (heroArt != null && Event.current.type == EventType.Repaint)
            {
                float visibleHeight = Mathf.Clamp01(bannerRect.height * heroArt.width / (bannerRect.width * heroArt.height));
                GUI.DrawTextureWithTexCoords(bannerRect, heroArt,
                    new Rect(0, Mathf.Clamp01(.68f - visibleHeight * .5f), 1, visibleHeight), true);
            }
            else Fill(bannerRect, new Color(.16f, .22f, .25f, 1));
            float captionHeight = Mathf.Clamp(bannerRect.height * .43f, 24, 30);
            float captionWidth = Mathf.Min(bannerRect.width * .68f, 196);
            var captionRect = new Rect(bannerRect.x + 1, bannerRect.yMax - captionHeight - 1,
                captionWidth, captionHeight);
            Fill(captionRect, new Color(.015f, .009f, .004f, .84f));
            GUI.Label(captionRect, deck.name, F025DeckBannerTextStyle);
            Frame(row, deck.deck_id == f024SelectedDeckId
                ? new Color(.96f, .75f, .30f, .98f)
                : new Color(.49f, .34f, .16f, .96f), deck.deck_id == f024SelectedDeckId ? 3 : 2);
            bool hovered = row.Contains(Event.current.mousePosition) && !F025HubOverlayOpen;
            if (hovered) Frame(row, new Color(1f, 1f, 1f, 1f), 3);
            var deleteRect = new Rect(row.x + 2, row.y + 2, 32, 30);
            string deleteTarget = "collection.deck-delete:" + deck.deck_id;
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(deleteTarget, deleteRect, viewport, f025CollectionDeckScroll);
            bool deleteEnabled = GUI.enabled;
            GUI.enabled = deleteEnabled && !f024AccountBusy && !F025HubOverlayOpen;
            bool deleteClicked = GUI.Button(deleteRect, GUIContent.none, GUIStyle.none);
            if (hovered && !F026DrawWidget(deleteRect, "collection.search.clear"))
                GUI.Label(deleteRect, "×", F025DeckTrayTextStyle);
            if (deleteClicked)
            {
                if (f024DraftDirty && deck.deck_id == f024DraftId)
                    F028RequestDeckSwitch("__delete__" + deck.deck_id);
                else F024RequestDeckDelete(deck.deck_id);
                F024ReplayGuiMarkHandler(deleteTarget, "F024RequestDeckDelete");
            }
            GUI.enabled = deleteEnabled && !f024AccountBusy && !F025HubOverlayOpen;
            bool clicked = GUI.Button(row, GUIContent.none, GUIStyle.none);
            GUI.enabled = deleteEnabled;
            if (clicked)
            {
                F028RequestDeckSwitch(deck.deck_id);
                if (F024ReplayGuiEnabled) F024ReplayGuiMarkHandler(target, "EditSelectedCollectionDeck");
            }
        }
        GUI.EndScrollView();

        bool previous = GUI.enabled;
        GUI.enabled = previous && !f024AccountBusy;
        string editTarget = "collection.deck-new";
        float editWidth = Mathf.Min(rect.width - insetX * 2, 250);
        var editRect = new Rect(rect.center.x - editWidth * .5f, rect.yMax - insetY - 108, editWidth, 42);
        if (Button(editRect, "新建卡组", editTarget))
        {
            F028RequestDeckSwitch("__new__");
            F024ReplayGuiMarkHandler(editTarget, "CreateCollectionDeck");
        }
        GUI.enabled = previous;
        if (decks.Length == 0)
            GUI.Label(new Rect(rect.x + insetX, viewport.y + 12, viewport.width, 84), "尚无服务器卡组。", F024PaperWrappedStyle);
    }

    void F028RequestDeckSwitch(string target)
    {
        if (f024DraftDirty && target != f024DraftId)
        {
            f028PendingDeckSwitch = target;
            f028ConfirmDiscard = true;
            return;
        }
        F028ApplyDeckSwitch(target);
    }

    void F028ApplyDeckSwitch(string target)
    {
        f028ConfirmDiscard = false;
        f028PendingDeckSwitch = "";
        F028CancelCardDrag();
        if (target == "__leave__")
        {
            f024DraftDirty = false;
            page = "menu";
            F024RequestAccountState();
            return;
        }
        if (target.StartsWith("__delete__", StringComparison.Ordinal))
        {
            string deckId = target.Substring("__delete__".Length);
            f024DraftDirty = false;
            f024DraftId = "";
            f024DraftCards.Clear();
            F024RequestDeckDelete(deckId);
            return;
        }
        if (target == "__new__")
        {
            f028PendingCodeRequestId = "";
            f024DraftId = "";
            f024DraftName = "新卡组";
            f024DraftCards.Clear();
            f024DraftClassId = database.classes.Length > 0 ? database.classes[0].id : 0;
            f028DraftFormat = "wild";
            f024DraftDirty = true;
            F028OfferImportFromClipboard();
        }
        else if (target != f024DraftId || !f024DraftDirty)
        {
            var deck = ((f024Account == null ? null : f024Account.decks) ?? new F024AccountDeck[0])
                .FirstOrDefault(item => item != null && item.deck_id == target);
            if (deck == null) { status = "卡组不存在"; return; }
            f024SelectedDeckId = deck.deck_id;
            F024BeginDeckDraft(deck);
        }
        f024CollectionTab = 1;
        f024CollectionPage = 0;
        f024CollectionSearch = "";
        f029CraftingMode = false;
        f024CollectionOwnedMode = 0;
    }

    void F028DrawDiscardConfirmation()
    {
        Fill(new Rect(0, 0, 1600, 1000), new Color(0, 0, 0, .72f));
        var rect = new Rect(500, 350, 600, 250);
        Panel(rect);
        GUI.Label(new Rect(rect.x + 28, rect.y + 30, rect.width - 56, 80),
            "卡组还有未保存的修改，是否放弃？", labelStyle);
        if (Button(new Rect(rect.x + 52, rect.y + 150, 220, 52), "放弃修改", "collection.deck-discard"))
        {
            string target = f028PendingDeckSwitch;
            F028ApplyDeckSwitch(target);
            F024ReplayGuiMarkHandler("collection.deck-discard", "DiscardDeckDraft");
        }
        if (Button(new Rect(rect.x + 328, rect.y + 150, 220, 52), "继续编辑", "collection.deck-discard-cancel"))
        {
            f028ConfirmDiscard = false;
            f028PendingDeckSwitch = "";
            f024CollectionTab = 1;
            F024ReplayGuiMarkHandler("collection.deck-discard-cancel", "KeepDeckDraft");
        }
    }

    void DrawF024CardDetail(Rect rect)
    {
        Panel(rect);
        CardDef card;
        if (!cards.TryGetValue(f024SelectedCardId ?? "", out card))
        {
            GUI.Label(new Rect(rect.x + 18, rect.y + 28, rect.width - 36, 60), "选择一本卡牌查看详情", labelStyle);
            return;
        }
        if (!F024IsBookCard(card))
        {
            GUI.Label(new Rect(rect.x + 18, rect.y + 28, rect.width - 36, 60), "此项目不是普通卡牌", labelStyle);
            return;
        }
        var imageRect = new Rect(rect.x + 18, rect.y + 22, 205, 308);
        F024DrawCardFace(imageRect, card);
        GUI.Label(new Rect(rect.x + 238, rect.y + 28, rect.width - 250, 70), card.Name, labelStyle);
        GUI.Label(new Rect(rect.x + 238, rect.y + 102, rect.width - 252, 72), F024ClassNameOrNeutral(card.Class) + " · " + card.Cost + " 费", smallStyle);
        var inventory = F024FindAccountCard(card.CardId);
        GUI.Label(new Rect(rect.x + 238, rect.y + 174, rect.width - 252, 85),
            "普通 " + (inventory == null ? 0 : inventory.normal) + "   金色 " + (inventory == null ? 0 : inventory.golden) +
            "\n基础 " + (inventory == null ? 0 : inventory.free), smallStyle);
        bool craftable = F024CanCraftOrDisenchant(card);
        int craftCost = F024CraftDust(card, f024GoldenCraft);
        int disenchantValue = F024DisenchantDust(card, f024GoldenCraft);
        f024GoldenCraft = GUI.Toggle(new Rect(rect.x + 238, rect.y + 270, 210, 32), f024GoldenCraft, "金色卡牌", smallStyle);
        GUI.Label(new Rect(rect.x + 238, rect.y + 306, rect.width - 252, 40),
            craftable ? "制作 " + craftCost + " 奥尘 · 分解 " + disenchantValue + " 奥尘" : "基础卡牌不可制作或分解", smallStyle);
        bool ownsSelected = inventory != null && (f024GoldenCraft ? inventory.golden > 0 : inventory.normal > 0);
        bool canCraft = craftable && f024Account.dust >= craftCost && !f024AccountBusy;
        bool canDisenchant = craftable && ownsSelected && !f024AccountBusy;
        bool previous = GUI.enabled;
        GUI.enabled = previous && canCraft;
        if (Button(new Rect(rect.x + 238, rect.y + 358, 145, 48), "制作 1 张", "collection.craft"))
        {
            F024RequestCraft(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.craft", "F024RequestCraft");
        }
        GUI.enabled = previous && canDisenchant;
        if (Button(new Rect(rect.x + 398, rect.y + 358, 145, 48), "分解 1 张", "collection.disenchant"))
        {
            F024RequestDisenchant(card.CardId, f024GoldenCraft);
            F024ReplayGuiMarkHandler("collection.disenchant", "F024RequestDisenchant");
        }
        GUI.enabled = previous;
        string deckNote = card.Playable ? "可用于已实现对战卡组" : "可收藏；当前不能加入对战卡组";
        GUI.Label(new Rect(rect.x + 238, rect.y + 416, rect.width - 252, 38), deckNote, smallStyle);
        bool canAdd = card.Playable && f024DraftCards.Count < 30 && F024CanAddDraftCard(card) && !f024AccountBusy;
        GUI.enabled = previous && canAdd;
        if (Button(new Rect(rect.x + 238, rect.y + 458, 260, 42), "加入当前卡组")) F024TryAddDraftCard(card);
        GUI.enabled = previous;
    }

    static bool F024IsBookCard(CardDef card)
    {
        return card != null && (card.Type == 4 || card.Type == 5 || card.Type == 7);
    }

    void F024DrawCardFace(Rect rect, CardDef card)
    {
        if (!F024IsBookCard(card)) return;
        var entity = new EntityView
        {
            entity = -1,
            card_id = card.CardId,
            cost = card.Cost,
            attack = card.Attack,
            health = card.Health,
            durability = card.Durability
        };
        bool previousDetail = showingDetail;
        bool previousPreviewRuleText = UIText.HubPreviewRuleText;
        showingDetail = true;
        UIText.HubPreviewRuleText = true;
        try { Card(rect, entity, true); }
        finally
        {
            UIText.HubPreviewRuleText = previousPreviewRuleText;
            showingDetail = previousDetail;
        }
    }

    void F028CardInput(Rect rect, string cardId, bool fromDeck, string target, Vector2 offset = default(Vector2))
    {
        Event current = Event.current;
        int control = GUIUtility.GetControlID((fromDeck ? "deck:" : "book:").GetHashCode() ^ cardId.GetHashCode(), FocusType.Passive, rect);
        if (!F025HubOverlayOpen && !F028DeckModalOpen && GUI.enabled && rect.Contains(current.mousePosition))
        {
            if (fromDeck && f028CardControl == 0) f028HoveredCard = cardId;
            if (!f024AccountBusy && current.type == EventType.MouseDown && current.button == 0)
            {
                GUIUtility.hotControl = control;
                f028CardControl = control;
                f028DraggedCard = cardId;
                f028DragFromDeck = fromDeck;
                f028CardMoved = false;
                f028CardStart = current.mousePosition + offset;
                current.Use();
            }
        }
    }

    void F028FinishCardDrag()
    {
        if (f028CardControl == 0) return;
        Event current = Event.current;
        if (F025HubOverlayOpen || F028DeckModalOpen || F028DraftRequestPending || page != "collection" || f024CollectionTab != 1 || GUIUtility.hotControl != f028CardControl)
        {
            F028CancelCardDrag();
            return;
        }
        if (current.type == EventType.MouseDrag)
        {
            if ((current.mousePosition - f028CardStart).sqrMagnitude > 64f) f028CardMoved = true;
            current.Use();
        }
        if (current.type == EventType.MouseUp && current.button == 0)
        {
            string id = f028DraggedCard;
            bool fromDeck = f028DragFromDeck;
            bool accept = !f028CardMoved || (fromDeck ? F025CollectionPaperRect : F025CollectionDeckTrayRect).Contains(current.mousePosition);
            GUIUtility.hotControl = 0;
            f028CardControl = 0;
            f028DraggedCard = "";
            if (accept)
            {
                int countBefore = f024DraftCards.Count;
                if (fromDeck)
                {
                    if (f024DraftCards.Remove(id)) f024DraftDirty = true;
                    F024ReplayGuiMarkHandler("collection.deck-card:" + id, "RemoveDraftCard");
                }
                else
                {
                    CardDef card;
                    if (cards.TryGetValue(id, out card)) F024TryAddDraftCard(card);
                    F024ReplayGuiMarkHandler("collection.card:" + id, "AddDraftCard");
                }
                if (f024DraftCards.Count != countBefore) PlaySound("SFX_COLLECTION_CARD");
            }
            current.Use();
        }
    }

    void F028CancelCardDrag()
    {
        if (f028CardControl != 0 && GUIUtility.hotControl == f028CardControl) GUIUtility.hotControl = 0;
        f028CardControl = 0;
        f028DraggedCard = "";
    }

    void F028DrawCardPreview()
    {
        string id = f028CardControl != 0 && f028CardMoved ? f028DraggedCard : f028HoveredCard;
        CardDef card;
        if (F025HubOverlayOpen || F028DeckModalOpen || String.IsNullOrEmpty(id) || !cards.TryGetValue(id, out card) || Event.current.type != EventType.Repaint) return;
        var pointer = Event.current.mousePosition;
        float width = f028CardControl != 0 ? 130 : 230;
        float height = width * 1.5f;
        var rect = new Rect(Mathf.Clamp(pointer.x - width - 12, 0, 1600 - width),
            Mathf.Clamp(pointer.y - height * .5f, 0, 1000 - height), width, height);
        F024DrawCardFace(rect, card);
    }

    void F028DrawDeckEditor(Rect rect)
    {
        float inset = Mathf.Clamp(rect.width * .065f, 14, 28);
        float width = rect.width - inset * 2;
        float x = rect.x + inset;
        var banner = new Rect(x, rect.y + 8, width, 72);
        int classIndex = Array.FindIndex(database.classes, item => item.id == f024DraftClassId);
        CardDef hero;
        Texture heroArt = null;
        if (classIndex >= 0 && cards.TryGetValue(database.classes[classIndex].hero ?? "", out hero) &&
            assets != null && Event.current.type == EventType.Repaint)
            heroArt = assets.Texture("CARD_" + hero.CardId);
        if (heroArt != null && Event.current.type == EventType.Repaint)
        {
            float visibleHeight = Mathf.Clamp01(banner.height * heroArt.width / (banner.width * heroArt.height));
            GUI.DrawTextureWithTexCoords(banner, heroArt,
                new Rect(0, Mathf.Clamp01(.68f - visibleHeight * .5f), 1, visibleHeight), true);
        }
        else Fill(banner, new Color(.13f, .10f, .08f, .95f));
        Frame(banner, new Color(.74f, .57f, .29f, .98f), 2);
        var nameRect = new Rect(banner.x + 5, banner.yMax - 32, banner.width - 10, 27);
        Fill(nameRect, new Color(.015f, .009f, .004f, .84f));
        string name = GUI.TextField(nameRect, f024DraftName, 32, F024CollectionSearchStyle);
        if (name != f024DraftName) { f024DraftName = name; f024DraftDirty = true; }
        float optionY = rect.y + 85;
        if (String.IsNullOrEmpty(f024DraftId) && database.classes.Length > 0)
        {
            int chosen = GUI.SelectionGrid(new Rect(x, optionY, width, 88), Mathf.Max(0, classIndex),
                database.classes.Select(item => item.name).ToArray(), 3, F024PaperSelectorStyle);
            if (chosen >= 0 && chosen < database.classes.Length && database.classes[chosen].id != f024DraftClassId)
            {
                f024DraftClassId = database.classes[chosen].id;
                f024DraftDirty = true;
                f024CollectionPage = 0;
            }
            optionY += 92;
        }
        float optionWidth = (width - 8) * .5f;
        bool showOptions = new Rect(banner.x, banner.y, banner.width, optionY + 34 - banner.y)
            .Contains(Event.current.mousePosition);
        if (F028DeckOptionButton(new Rect(x, optionY, optionWidth, 34),
            f028DraftFormat == "wild" ? "转为标准" : "转为狂野", "collection.deck-format", showOptions))
        {
            f028DraftFormat = f028DraftFormat == "wild" ? "standard" : "wild";
            f024DraftDirty = true;
            F024ReplayGuiMarkHandler("collection.deck-format", "ChangeDraftDeckFormat");
        }
        if (F028DeckOptionButton(new Rect(x + optionWidth + 8, optionY, optionWidth, 34),
            "复制套牌", "collection.deck-code", showOptions))
        {
            F028RequestDeckCode();
            F024ReplayGuiMarkHandler("collection.deck-code", "CopyDraftDeckCode");
        }
        GUI.Label(new Rect(x, optionY + 37, width, 30),
            (f028DraftFormat == "standard" ? "标准" : "狂野") + " · " + f024DraftCards.Count + " / 30", F025DeckTrayTextStyle);
        var groups = f024DraftCards.GroupBy(id => id).OrderBy(group => cards.ContainsKey(group.Key) ? cards[group.Key].Cost : 99).ToArray();
        float listY = optionY + 69;
        var viewport = new Rect(x, listY, width, Mathf.Max(100, rect.yMax - 70 - listY));
        if (F024ReplayGuiEnabled)
        {
            F024ReplayGuiRegisterTarget("collection.deck-drop", viewport);
            F024ReplayGuiRegisterTarget("collection.book-drop", F025CollectionPaperRect);
        }
        f024DeckBuilderListScroll = GUI.BeginScrollView(viewport, f024DeckBuilderListScroll,
            new Rect(0, 0, width - 24, Mathf.Max(viewport.height, groups.Length * 38)));
        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            CardDef def;
            cards.TryGetValue(group.Key, out def);
            var row = new Rect(0, i * 38, width - 24, 36);
            string target = "collection.deck-card:" + group.Key;
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, row, viewport, f024DeckBuilderListScroll);
            Fill(row, new Color(.075f, .07f, .12f, .98f));
            Texture art = def == null || assets == null || Event.current.type != EventType.Repaint
                ? null : assets.Texture(def.ArtAsset);
            var artRect = new Rect(row.x + row.width * .51f, row.y + 2, row.width * .47f, row.height - 4);
            if (art != null && Event.current.type == EventType.Repaint)
            {
                float visibleHeight = Mathf.Clamp01(.70f * artRect.height * art.width / (artRect.width * art.height));
                GUI.DrawTextureWithTexCoords(artRect, art,
                    new Rect(.15f, Mathf.Clamp01(.68f - visibleHeight * .5f), .70f, visibleHeight), true);
            }
            Fill(new Rect(row.x + 28, row.y + 2, row.width * .70f, row.height - 4), new Color(.02f, .015f, .02f, .72f));
            Frame(row, new Color(.38f, .42f, .48f, .9f), 1);
            if (row.Contains(Event.current.mousePosition) && !F025HubOverlayOpen) Frame(row, new Color(1f, .9f, .55f), 2);
            var mana = new Rect(row.x + 1, row.y + 3, 28, 28);
            Texture manaGem = Event.current.type == EventType.Repaint && cardVisuals != null ? cardVisuals.Prop("MODEL_GEM_MANA") : null;
            if (manaGem != null) GUI.DrawTexture(mana, manaGem, ScaleMode.ScaleToFit, true);
            GUI.Label(mana, def == null ? "?" : def.Cost.ToString(), F025ManaNumberStyle);
            GUI.Label(new Rect(row.x + 31, row.y, row.width - 65, row.height), def == null ? group.Key : def.Name, F025DeckTrayTextStyle);
            GUI.Label(new Rect(row.xMax - 34, row.y, 31, row.height), "×" + group.Count(), F025DeckTrayTextStyle);
            F028CardInput(row, group.Key, true, target, viewport.position - f024DeckBuilderListScroll);
        }
        GUI.EndScrollView();
        string validation = F028ValidateDeckDraft(f024DraftCards.ToArray(), f024DraftClassId);
        string deckStatus = validation != "" ? validation : f024DraftCards.Count < 30
            ? "卡组未满 30 张，保存后暂不能对战" : f028DraftFormat == "standard"
            ? "标准卡池暂未配置，保存后暂不能对战" : "卡组合法";
        GUI.Label(new Rect(x, rect.yMax - 64, width, 45), deckStatus, F024PaperWrappedStyle);
    }

    bool F028DeckOptionButton(Rect rect, string text, string target, bool visible)
    {
        if (visible) return Button(rect, text, target);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect);
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        F024ReplayGuiObserveControlAfter(target);
        return clicked;
    }

    void F028OfferImportFromClipboard()
    {
        f028ClipboardCode = "";
        f028OfferClipboardImport = false;
        try
        {
            string candidate = (f024AccountReplayActive ? f028ReplayClipboard : GUIUtility.systemCopyBuffer ?? "").Trim();
            if (candidate.Length < 8 || candidate.Length > 2048) return;
            byte[] bytes = Convert.FromBase64String(candidate);
            if (bytes.Length < 6 || bytes[0] != 0 || bytes[1] != 1 || (bytes[2] != 1 && bytes[2] != 2)) return;
            f028ClipboardCode = candidate;
            f028OfferClipboardImport = true;
        }
        catch (FormatException) { }
        catch (Exception error) { Debug.LogWarning("读取剪贴板失败：" + error.Message); }
    }

    void F028DrawClipboardOffer()
    {
        Fill(new Rect(0, 0, 1600, 1000), new Color(0, 0, 0, .72f));
        var rect = new Rect(500, 350, 600, 250);
        Panel(rect);
        GUI.Label(new Rect(rect.x + 28, rect.y + 30, rect.width - 56, 80),
            "剪贴板中发现卡组代码，是否导入？", labelStyle);
        if (Button(new Rect(rect.x + 52, rect.y + 150, 220, 52), "导入", "collection.deck-import"))
        {
            f028OfferClipboardImport = false;
            F028RequestDeckImport(f028ClipboardCode);
            f028ClipboardCode = "";
            F024ReplayGuiMarkHandler("collection.deck-import", "ImportDeckCode");
        }
        if (Button(new Rect(rect.x + 328, rect.y + 150, 220, 52), "不导入", "collection.deck-import-cancel"))
        {
            f028OfferClipboardImport = false;
            f028ClipboardCode = "";
            F024ReplayGuiMarkHandler("collection.deck-import-cancel", "CancelDeckCodeImport");
        }
    }

    F024AccountCard F024FindAccountCard(string cardId)
    {
        return ((f024Account == null ? null : f024Account.cards) ?? new F024AccountCard[0])
            .FirstOrDefault(item => item != null && item.card_id == cardId);
    }

    string F024ClassNameOrNeutral(int classId)
    {
        return classId == 12 ? "中立" : F024ClassName(classId);
    }

    bool F024CanCraftOrDisenchant(CardDef card)
    {
        return card != null && card.Set != 1637 && (card.Rarity == 1 || card.Rarity == 3 || card.Rarity == 4 || card.Rarity == 5);
    }

    int F024CraftDust(CardDef card, bool golden)
    {
        if (card == null) return 0;
        if (golden) return card.Rarity == 1 ? 400 : card.Rarity == 3 ? 800 : card.Rarity == 4 ? 1600 : card.Rarity == 5 ? 3200 : 0;
        return card.Rarity == 1 ? 40 : card.Rarity == 3 ? 100 : card.Rarity == 4 ? 400 : card.Rarity == 5 ? 1600 : 0;
    }

    int F024DisenchantDust(CardDef card, bool golden)
    {
        if (card == null) return 0;
        if (golden) return card.Rarity == 1 ? 50 : card.Rarity == 3 ? 100 : card.Rarity == 4 ? 400 : card.Rarity == 5 ? 1600 : 0;
        return card.Rarity == 1 ? 5 : card.Rarity == 3 ? 20 : card.Rarity == 4 ? 100 : card.Rarity == 5 ? 400 : 0;
    }

    void DrawF024DeckBuilder()
    {
        Rect paperRect = F025CollectionPaperRect;
        Rect trayRect = F025CollectionDeckTrayRect;
        float builderTop = paperRect.y + 58;
        if (f024DraftCards.Count == 0 && !f024DraftDirty) F024RefreshDeckDraftFromSnapshot();
        if (f024Account.decks == null || f024Account.decks.Length == 0)
        {
            Rect paper = F025CollectionPaperRect;
            GUI.Label(new Rect(paper.x + 24, paper.y + 148, paper.width - 48, 90), "账号没有服务器卡组。", F024PaperLabelStyle);
            return;
        }
        if (String.IsNullOrEmpty(f024DraftId) && f024DraftCards.Count == 0 && !f024DraftDirty) F024RefreshDeckDraftFromSnapshot();

        float paperInset = Mathf.Clamp(paperRect.width * .035f, 18, 34);
        GUI.Label(new Rect(paperRect.x + paperInset, builderTop + 16, 90, 36), "卡组名称", F024PaperLabelStyle);
        f024DraftName = GUI.TextField(new Rect(paperRect.x + paperInset + 94, builderTop + 14, paperRect.width * .40f, 40), f024DraftName, 32);
        GUI.Label(new Rect(paperRect.x + paperRect.width * .58f, builderTop + 16, paperRect.width * .38f, 36),
            F024ClassName(f024DraftClassId) + " · " + f024DraftCards.Count + " / 30 张", F024PaperLabelStyle);

        int classIndexChoice = Array.FindIndex(database.classes, item => item.id == f024DraftClassId);
        if (classIndexChoice < 0) classIndexChoice = 0;
        string[] classNames = database.classes.Select(item => item.name).ToArray();
        if (classNames.Length > 0)
        {
            var classGrid = new Rect(paperRect.x + paperInset, builderTop + 60, paperRect.width - paperInset * 2, 58);
            int classColumns = Mathf.Min(6, classNames.Length);
            float classCellWidth = classGrid.width / classColumns;
            float classCellHeight = classGrid.height / Mathf.CeilToInt(classNames.Length / (float)classColumns);
            F024HighlightPaperCell(new Rect(classGrid.x + classIndexChoice % classColumns * classCellWidth,
                classGrid.y + classIndexChoice / classColumns * classCellHeight, classCellWidth, classCellHeight), true);
            int chosen = GUI.SelectionGrid(classGrid, classIndexChoice, classNames, classColumns, F024PaperSelectorStyle);
            if (chosen >= 0 && chosen < database.classes.Length && database.classes[chosen].id != f024DraftClassId)
            {
                f024DraftClassId = database.classes[chosen].id;
                f024DraftDirty = true;
            }
        }

        float poolTop = builderTop + 128;
        float poolBottom = paperRect.yMax - 72;
        float poolWidth = paperRect.width - paperInset * 2;
        GUI.Label(new Rect(paperRect.x + paperInset, poolTop, 150, 32), "可加入卡牌", F024PaperLabelStyle);
        GUI.Label(new Rect(paperRect.x + paperInset + 152, poolTop, 136, 32), "已拥有 / 可对战", F024PaperLabelStyle);
        f024DeckBuilderSearch = GUI.TextField(new Rect(paperRect.x + paperInset + 290, poolTop, Mathf.Max(90, poolWidth - 290), 32), f024DeckBuilderSearch, 48);
        var ownedPool = database.cards.Where(F024IsBookCard).Where(card => card.Collectible && card.Playable &&
                (card.Class == 12 || card.Class == f024DraftClassId) && F024OwnedCardCount(card.CardId) > 0 &&
                (String.IsNullOrEmpty(f024DeckBuilderSearch) || card.Name.IndexOf(f024DeckBuilderSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    card.CardId.IndexOf(f024DeckBuilderSearch, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(card => card.Cost).ThenBy(card => card.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var poolViewport = new Rect(paperRect.x + paperInset, poolTop + 36, poolWidth, Mathf.Max(100, poolBottom - poolTop - 36));
        f024DeckBuilderPoolScroll = GUI.BeginScrollView(poolViewport, f024DeckBuilderPoolScroll,
            new Rect(0, 0, poolWidth - 24, Mathf.Max(1, ownedPool.Length) * 42));
        for (int i = 0; i < ownedPool.Length; i++)
        {
            var card = ownedPool[i];
            int inDeck = f024DraftCards.Count(id => id == card.CardId);
            var row = new Rect(0, i * 42, poolWidth - 24, 38);
            string label = card.Cost + " · " + card.Name + "  " + inDeck + "/" + F024OwnedCardCount(card.CardId);
            if (GUI.Button(row, label, F024PaperLabelStyle)) F024TryAddDraftCard(card);
        }
        GUI.EndScrollView();

        float trayInset = Mathf.Clamp(trayRect.width * .065f, 14, 28);
        var builderTrayTitle = new Rect(trayRect.x + trayInset, trayRect.y + 18, trayRect.width - trayInset * 2, 36);
        GUI.Label(new Rect(builderTrayTitle.x, builderTrayTitle.y, builderTrayTitle.width * .52f, builderTrayTitle.height),
            "当前卡组 · 点击条目移除", F025DeckTrayTextStyle);
        F024DrawWallet(new Rect(builderTrayTitle.x + builderTrayTitle.width * .53f, builderTrayTitle.y,
            builderTrayTitle.width * .47f, builderTrayTitle.height));
        var groups = f024DraftCards.GroupBy(id => id).OrderBy(group => cards.ContainsKey(group.Key) ? cards[group.Key].Cost : 99).ToArray();
        var deckViewport = new Rect(trayRect.x + trayInset, trayRect.y + 58, trayRect.width - trayInset * 2,
            Mathf.Max(100, trayRect.height - 176));
        f024DeckBuilderListScroll = GUI.BeginScrollView(deckViewport, f024DeckBuilderListScroll,
            new Rect(0, 0, deckViewport.width - 24, Mathf.Max(1, groups.Length) * 42));
        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var def = cards.ContainsKey(group.Key) ? cards[group.Key] : null;
            string label = (def == null ? group.Key : def.Cost + " · " + def.Name) + " × " + group.Count();
            if (GUI.Button(new Rect(0, i * 42, deckViewport.width - 24, 38), label, smallStyle))
            {
                f024DraftCards.Remove(group.Key);
                f024DraftDirty = true;
            }
        }
        GUI.EndScrollView();

        string validation = F024ValidateOwnedDeck(f024DraftCards.ToArray(), f024DraftClassId);
        var footer = new Rect(paperRect.x + paperInset, paperRect.yMax - 56, paperRect.width - paperInset * 2, 44);
        GUI.Label(new Rect(footer.x, footer.y, footer.width * .47f, footer.height),
            validation == "" ? "卡组合法 · 保存后由服务器再次验证" : validation, smallStyle);
        bool previous = GUI.enabled;
        GUI.enabled = previous && validation == "" && !f024AccountBusy && !String.IsNullOrWhiteSpace(f024DraftName);
        float actionWidth = Mathf.Clamp((trayRect.width - trayInset * 2 - 12) * .5f, 150, 220);
        float actionY = trayRect.yMax - trayInset - 48;
        if (Button(new Rect(trayRect.x + trayInset, actionY, actionWidth, 48), "保存到服务器", "collection.deck-save"))
        {
            F024RequestDeckSave(f024DraftId, f024DraftName.Trim(), f024DraftClassId, f024DraftCards.ToArray());
            F024ReplayGuiMarkHandler("collection.deck-save", "F024RequestDeckSave");
        }
        GUI.enabled = previous;
        if (Button(new Rect(trayRect.x + trayInset + actionWidth + 12, actionY, actionWidth, 48), "复制当前卡组", "collection.deck-copy"))
        {
            F024CreateDeckCopy();
            F024ReplayGuiMarkHandler("collection.deck-copy", "F024CreateDeckCopy");
        }
    }

    void F024RefreshDeckDraftFromSnapshot()
    {
        if (f024DraftDirty || f024Account == null) return;
        var decks = f024Account.decks ?? new F024AccountDeck[0];
        var current = decks.FirstOrDefault(item => item != null && item.deck_id == f024DraftId);
        if (current == null) current = decks.FirstOrDefault(item => item != null && item.deck_id == f024SelectedDeckId);
        if (current == null) current = decks.FirstOrDefault(item => item != null);
        if (current == null)
        {
            f024DraftId = "";
            f024DraftName = "新卡组";
            f024DraftCards.Clear();
            return;
        }
        f024DraftId = current.deck_id;
        f024DraftName = current.name;
        f024DraftClassId = current.class_id;
        f028DraftFormat = current.format == "standard" ? "standard" : "wild";
        f024DraftCards.Clear();
        f024DraftCards.AddRange(current.cards ?? new string[0]);
        f024SelectedDeckId = current.deck_id;
    }

    void F024BeginDeckDraft(F024AccountDeck source)
    {
        if (source == null) return;
        f028PendingCodeRequestId = "";
        f024DraftId = source.deck_id;
        f024DraftName = source.name;
        f024DraftClassId = source.class_id;
        f028DraftFormat = source.format == "standard" ? "standard" : "wild";
        f024DraftCards.Clear();
        f024DraftCards.AddRange(source.cards ?? new string[0]);
        f024DraftDirty = false;
    }

    void F024CreateDeckCopy()
    {
        var source = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null && item.deck_id == f024DraftId);
        if (source == null) source = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null && item.deck_id == f024SelectedDeckId);
        if (source == null) return;
        F024BeginDeckDraft(source);
        f024DraftId = "";
        f024DraftName = source.name + " 副本";
        f024DraftDirty = true;
    }

    bool F024CanAddDraftCard(CardDef card)
    {
        if (card == null || !card.Playable || (card.Class != 12 && card.Class != f024DraftClassId)) return false;
        int limit = card.Rarity == 5 ? 1 : 2;
        int currently = f024DraftCards.Count(id => id == card.CardId);
        return f024DraftCards.Count < 30 && currently < limit && currently < F024OwnedCardCount(card.CardId);
    }

    void F024TryAddDraftCard(CardDef card)
    {
        if (!F024CanAddDraftCard(card)) return;
        f024DraftCards.Add(card.CardId);
        f024DraftDirty = true;
    }

    void DrawF024PacksPage()
    {
        F024DrawPageFrame("", "PackOpeningTray", new Rect(0, 0, 1600, 1000));
        Rect headerRect;
        if (nativeHubScene != null && nativeHubScene.TryGetPackOpeningHeader(out headerRect))
            Ink(headerRect, "开包", new GUIStyle(labelStyle) { fontSize = 25, alignment = TextAnchor.MiddleCenter }, Color.white);
        if (!F024HasAccount)
        {
            F024DrawLoginGate(new Rect(300, 390, 1000, 140), "登录账号后可查看已拥有卡包");
            return;
        }
        var packs = (f024Account.packs ?? new F024AccountPack[0]).Where(item => item != null && item.count > 0).ToArray();
        if (!packs.Any(item => item.set_id == f024SelectedPackSet)) f024SelectedPackSet = packs.FirstOrDefault()?.set_id ?? -1;
        Rect trayRect = Rect.zero, openSlot = Rect.zero, backRect = Rect.zero;
        bool hasNativeLayout = nativeHubScene != null && nativeHubScene.TryGetPackOpeningLayout(out trayRect, out openSlot, out backRect);
        if (!hasNativeLayout)
        {
            trayRect = new Rect(105, 205, 330, 600);
            openSlot = new Rect(720, 290, 550, 430);
        }
        var packViewport = new Rect(trayRect.x + 10, trayRect.y + 16,
            Mathf.Max(200, trayRect.width - 20), Mathf.Max(300, trayRect.height - 32));
        int visibleSlots = Mathf.Min(3, Mathf.Max(1, packs.Length));
        float packRowHeight = Mathf.Clamp(packViewport.height / visibleSlots, 210, 330);
        f024PackListScroll = GUI.BeginScrollView(packViewport, f024PackListScroll,
            new Rect(0, 0, packViewport.width - 18, Mathf.Max(packViewport.height, packs.Length * packRowHeight)));
        var visiblePacks = new Rect(f024PackListScroll.x, f024PackListScroll.y, packViewport.width, packViewport.height);
        bool guiReplay = F024ReplayGuiEnabled;
        if (guiReplay)
        {
            F024ReplayGuiRegisterTarget("packs.slot", openSlot);
            F024ReplayGuiRegisterTarget("packs.outside", new Rect(packViewport.x + 12, packViewport.yMax - 56, 40, 40));
        }
        for (int i = 0; i < packs.Length; i++)
        {
            var item = packs[i];
            var row = new Rect(0, i * packRowHeight, packViewport.width - 18, packRowHeight - 8);
            bool replayGui = F024ReplayGuiEnabled;
            string sourceTarget = replayGui ? "packs.source:" + item.set_id : "";
            string dragTarget = replayGui ? "packs.drag:" + item.set_id : "";
            if (replayGui) F024ReplayGuiRegisterTarget(sourceTarget, row, packViewport, f024PackListScroll);
            if (item.set_id == f024SelectedPackSet) Fill(row, new Color(.5f, .35f, .15f, .16f));
            if (Event.current.type == EventType.Repaint && row.Overlaps(visiblePacks))
            {
                float modelHeight = Mathf.Min(row.height - 8, row.width * 1.38f);
                float modelWidth = Mathf.Min(row.width - 24, modelHeight * .72f);
                var packRect = new Rect(row.center.x - modelWidth * .5f, row.center.y - modelHeight * .5f,
                    modelWidth, modelHeight);
                F024DrawPackModel(packRect, item.set_id);
            }
            var countRect = new Rect(row.xMax - 56, row.y + 14, 44, 44);
            Fill(countRect, new Color(.16f, .24f, .38f, .94f));
            Frame(countRect, new Color(.69f, .77f, .88f, .92f), 2);
            GUI.Label(countRect, item.count.ToString(), labelStyle);
            int control = GUIUtility.GetControlID(item.set_id.GetHashCode(), FocusType.Passive, row);
            if (!F025HubOverlayOpen && !f024AccountBusy && Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 && row.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = control;
                f024PackDragControlId = control;
                f024DraggingPackSet = item.set_id;
                f024SelectedPackSet = item.set_id;
                Event.current.Use();
            }
        }
        GUI.EndScrollView();
        if (!F025HubOverlayOpen && f024PackDragControlId != 0 && GUIUtility.hotControl == f024PackDragControlId)
        {
            string dragTarget = F024ReplayGuiEnabled ? "packs.drag:" + f024DraggingPackSet : "";
            if (Event.current.type == EventType.MouseDrag) Event.current.Use();
            if (Event.current.type == EventType.MouseUp)
            {
                int draggedSet = f024DraggingPackSet;
                bool releaseInside = openSlot.Contains(Event.current.mousePosition);
                GUIUtility.hotControl = 0;
                f024PackDragControlId = 0;
                f024DraggingPackSet = -1;
                if (releaseInside)
                {
                    F024OpenPackFromDrag(draggedSet);
                    F024ReplayGuiMarkHandler(dragTarget, "F024OpenPackFromDrag");
                }
                Event.current.Use();
            }
        }

        if (f024DraggingPackSet >= 0 && Event.current.type == EventType.Repaint)
        {
            var pointer = Event.current.mousePosition;
            F024DrawPackModel(new Rect(pointer.x - 40, pointer.y - 60, 80, 120), f024DraggingPackSet);
        }
    }

    string F024SetName(int setId)
    {
        var item = ((f024Account == null ? null : f024Account.shop) ?? new F024AccountShopItem[0])
            .FirstOrDefault(shopItem => shopItem != null && shopItem.set_id == setId);
        if (item != null && !String.IsNullOrEmpty(item.name)) return item.name;
        string knownName;
        return F024CollectionSetNames.TryGetValue(setId, out knownName) ? knownName : "扩展包 " + setId;
    }

    bool F024DrawPackModel(Rect rect, int setId)
    {
        return nativeHubScene != null && nativeHubScene.DrawPackPreview(rect, setId);
    }

    void F024OpenPackFromDrag(int setId)
    {
        if (setId < 0 || f024AccountBusy) return;
        var pack = ((f024Account == null ? null : f024Account.packs) ?? new F024AccountPack[0])
            .FirstOrDefault(item => item != null && item.set_id == setId);
        if (pack == null || pack.count < 1) return;
        f024SelectedPackSet = setId;
        f024DraggingPackSet = -1;
        f024PackDragControlId = 0;
        F024RequestPackOpen(setId);
    }

    void BeginF024PackReveal(F024OpenedCard[] openedCards, string openId)
    {
        if (String.IsNullOrEmpty(openId) || openId == f024LastOpenedId) return;
        f024LastOpenedId = openId;
        f024RevealOpenId = openId;
        f024RevealCards = openedCards ?? new F024OpenedCard[0];
        if (f024RevealCards.Length != 5)
        {
            f024PackRevealActive = false;
            f024AccountMessage = "服务器已保存开包结果，但返回卡牌数量异常；收藏仍以服务器数据为准";
            status = f024AccountMessage;
            return;
        }
        f024RevealIndex = -1;
        f024PackRevealActive = true;
        page = "packs";
        f024AccountMessage = "服务器已保存5张卡牌；逐张翻开后可在收藏中查看";
    }

    void DrawF024PackReveal()
    {
        Fill(new Rect(0, 0, 1600, 1000), new Color(0, 0, 0, .76f));
        Panel(new Rect(390, 110, 820, 790));
        GUI.Label(new Rect(450, 130, 700, 64), "卡包揭晓 · " + (f024RevealIndex + 1) + " / 5", titleStyle);
        if (f024RevealIndex < 0)
        {
            if (Event.current.type == EventType.Repaint) DrawAsset(new Rect(680, 235, 240, 360), "UI_CARD_BACK");
            GUI.Label(new Rect(470, 620, 660, 54), "点击下方按钮翻开第一张卡牌", labelStyle);
        }
        else
        {
            var opened = f024RevealCards[f024RevealIndex];
            CardDef card;
            if (opened != null && cards.TryGetValue(opened.card_id ?? "", out card) && F024IsBookCard(card))
            {
                F024DrawCardFace(new Rect(660, 218, 280, 420), card);
                GUI.Label(new Rect(460, 648, 680, 48), (opened.golden ? "金色卡牌" : "普通卡牌") + (opened.new_card ? " · 新卡牌" : ""), labelStyle);
            }
            else GUI.Label(new Rect(460, 330, 680, 60), "收到非普通卡牌项目 " + (opened == null ? "" : opened.card_id), labelStyle);
        }
        string action = f024RevealIndex >= f024RevealCards.Length - 1 ? "完成" : "翻开下一张";
        if (Button(new Rect(670, 742, 260, 64), action, "packs.reveal"))
        {
            F024AdvancePackReveal();
            F024ReplayGuiMarkHandler("packs.reveal", "F024AdvancePackReveal");
        }
        GUI.Label(new Rect(470, 825, 660, 40), "卡牌已由服务器写入收藏；关闭窗口不会撤销开包。", smallStyle);
    }

    void F024AdvancePackReveal()
    {
        if (f024RevealIndex + 1 < f024RevealCards.Length)
        {
            f024RevealIndex++;
            return;
        }
        f024PackRevealActive = false;
        f024RevealIndex = -1;
        F024RequestAccountState();
    }

    void DrawF024ShopPage()
    {
        F024DrawPageFrame("商店 · 扩展卡包", "SCENE_MAIN_SHOP", new Rect(50, 75, 1500, 835));
        if (!F024HasAccount)
        {
            F024DrawLoginGate(new Rect(300, 390, 1000, 140), "登录账号后可使用金币购买扩展卡包");
            return;
        }
        GUI.Label(new Rect(220, 150, 1160, 40), "商店仅提供扩展卡包 · 每包100金币", labelStyle);
        var items = (f024Account.shop ?? new F024AccountShopItem[0]).Where(item => item != null && item.price == 100)
            .OrderBy(item => item.name, StringComparer.OrdinalIgnoreCase).Take(16).ToArray();
        const float shopRowHeight = 330;
        var shopViewport = new Rect(250, 198, 1100, 655);
        var shopContent = new Rect(0, 0, 1080, Mathf.Max(1, Mathf.CeilToInt(items.Length / 3f)) * shopRowHeight);
        string replayBuyTarget = f024ReplayGuiCurrent == null ? "" : f024ReplayGuiCurrent.target;
        F024ReplayGuiObserveScroll(replayBuyTarget, "before", shopViewport, f024ShopScroll);
        f024ShopScroll = GUI.BeginScrollView(shopViewport, f024ShopScroll, shopContent);
        F024ReplayGuiObserveScroll(replayBuyTarget, "after", shopViewport, f024ShopScroll);
        var visibleShop = new Rect(f024ShopScroll.x, f024ShopScroll.y, 1080, 655);
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int row = i / 3, column = i % 3;
            var rect = new Rect(column * 360 + 6, row * shopRowHeight + 2, 348, 324);
            Frame(rect, new Color(.52f, .37f, .61f, .62f), 1);
            if (Event.current.type == EventType.Repaint && rect.Overlaps(visibleShop))
                F024DrawPackModel(new Rect(rect.center.x - 80, rect.y + 6, 160, 210), item.set_id);
            GUI.Label(new Rect(rect.x + 10, rect.y + 218, rect.width - 20, 30), item.name, labelStyle);
            bool validPrice = item.price == 100;
            GUI.Label(new Rect(rect.x + 10, rect.y + 248, rect.width - 20, 27),
                validPrice ? "5张卡牌 · 100金币" : "商品价格与本服规则不符", smallStyle);
            bool previous = GUI.enabled;
            GUI.enabled = previous && validPrice && !f024AccountBusy;
            bool replayGui = F024ReplayGuiEnabled;
            string buyTarget = replayGui ? "shop.buy:" + item.set_id : "";
            var buyRect = new Rect(rect.center.x - 105, rect.y + 278, 210, 42);
            bool buyClicked = replayGui
                ? Button(buyRect, "购买", buyTarget, shopViewport, f024ShopScroll)
                : Button(buyRect, "购买");
            if (buyClicked)
            {
                F026OpenShopBuy(item.set_id);
                F024ReplayGuiMarkHandler(buyTarget, "OpenShopBuyModal");
            }
            GUI.enabled = previous;
        }
        GUI.EndScrollView();
        if (items.Length == 0) GUI.Label(new Rect(400, 400, 800, 60), "当前没有可售扩展卡包。", labelStyle);
    }

    internal void DrawF026QuestPage(bool preview = false)
    {
        bool previousPreview = f026QuestPreviewActive;
        f026QuestPreviewActive = preview;
        try
        {
            Rect pageRect = F026QuestCalibration.PageRect;
            calibration.Draw(F026QuestCalibration.PageId, "任务页", pageRect, () =>
            {
                calibration.Draw(F026QuestCalibration.ChildId(F026QuestCalibration.PageId, "background"), "任务页背景", pageRect, () =>
                    F024DrawPageFrame("任务", "QuestBoard_Atlas", pageRect));

                if (!preview && !F024HasAccount)
                {
                    F024DrawLoginGate(new Rect(300, 390, 1000, 140), "登录账号后可查看服务器每日与每周任务");
                    return;
                }

                F024AccountQuest[] quests = preview
                    ? F026QuestCalibration.FixtureQuests
                    : ((f024Account == null ? null : f024Account.quests) ?? new F024AccountQuest[0])
                        .Where(item => item != null).ToArray();
                var daily = quests.Where(item => String.Equals(item.period, "daily", StringComparison.OrdinalIgnoreCase)).Take(4).ToArray();
                var weekly = quests.Where(item => String.Equals(item.period, "weekly", StringComparison.OrdinalIgnoreCase)).Take(4).ToArray();

                DrawF026QuestTitle(false);
                DrawF026QuestTitle(true);
                for (int i = 0; i < 4; i++)
                {
                    DrawF026QuestTile(false, i, i < daily.Length ? daily[i] : null, preview);
                    DrawF026QuestTile(true, i, i < weekly.Length ? weekly[i] : null, preview);
                }
                if (!preview && (daily.Length == 0 || weekly.Length == 0))
                    GUI.Label(new Rect(475, 452, 650, 60), "等待服务器同步任务列表。", smallStyle);
            });
        }
        finally
        {
            f026QuestPreviewActive = previousPreview;
        }
    }

    void DrawF026QuestTitle(bool weekly)
    {
        Rect rect = F026QuestCalibration.TitleRect(weekly);
        calibration.Draw(F026QuestCalibration.TitleId(weekly), weekly ? "每周任务标题" : "每日任务标题", rect,
            () => Ink(rect, weekly ? "每周任务" : "每日任务", labelStyle, labelStyle.normal.textColor));
    }

    void DrawF026QuestTile(bool weekly, int index, F024AccountQuest quest, bool preview)
    {
        Rect rect = F026QuestCalibration.TileRect(weekly, index);
        string tileId = F026QuestCalibration.TileId(weekly, index);
        calibration.Draw(tileId, (weekly ? "每周" : "每日") + "任务卡 " + (index + 1), rect, () =>
        {
            if (f024QuestBodyStyle == null)
            {
                f024QuestBodyStyle = new GUIStyle(smallStyle) { fontSize = 18, wordWrap = true };
                f024QuestBodyStyle.normal.textColor = new Color(.2f, .12f, .06f);
                f024QuestTitleStyle = new GUIStyle(f024QuestBodyStyle) { fontSize = 19, fontStyle = FontStyle.Bold };
                f024QuestTitleStyle.normal.textColor = new Color(1f, .91f, .72f);
            }
            if (!(nativeHubScene != null && nativeHubScene.DrawQuestTile(rect, weekly))) Panel(rect);

            Rect icon = F026QuestCalibration.IconRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "icon"), "任务奖励图标", icon, () =>
            {
                if (!F024DrawNativeImage(icon, "UI_GOLD", ScaleMode.ScaleToFit))
                    GUI.Label(icon, "金", smallStyle);
            });

            string difficulty = quest == null ? "等待同步" :
                quest.difficulty == "easy" ? "简单" : quest.difficulty == "normal" ? "普通" :
                quest.difficulty == "hard" ? "困难" : quest.difficulty;
            Rect title = F026QuestCalibration.TileTitleRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "title"), "任务标题", title, () =>
                Ink(title, quest == null || String.IsNullOrWhiteSpace(quest.title) ? difficulty + "任务" : quest.title, f024QuestTitleStyle, f024QuestTitleStyle.normal.textColor));

            Rect description = F026QuestCalibration.TileTextRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "text"), "任务说明", description, () =>
                Ink(description, quest == null ? "等待服务器同步任务列表。" : quest.description ?? "", f024QuestBodyStyle, f024QuestBodyStyle.normal.textColor));

            Rect reward = F026QuestCalibration.RewardRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "reward"), "任务奖励", reward, () =>
                Ink(reward, quest == null ? "奖励 -- 金币" : "奖励 " + quest.reward_gold + " 金币", f024QuestBodyStyle, f024QuestBodyStyle.normal.textColor));

            Rect progress = F026QuestCalibration.ProgressRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "progress"), "任务进度", progress, () =>
                Ink(progress, quest == null ? "-- / --" : quest.progress + " / " + quest.target, f024QuestTitleStyle, f024QuestTitleStyle.normal.textColor));

            Rect statusRect = F026QuestCalibration.StatusRect(rect);
            calibration.Draw(F026QuestCalibration.ChildId(tileId, "status"), "任务状态", statusRect, () =>
                Ink(statusRect, quest == null ? "" : quest.completed ? "已完成 · 奖励已入账" : "进行中", f024QuestBodyStyle, f024QuestBodyStyle.normal.textColor));

            Rect buttonRect = F026QuestCalibration.ButtonRect(rect);
            string buttonId = F026QuestCalibration.ChildId(tileId, "button");
            calibration.Draw(buttonId, "任务重抽按钮区域", buttonRect, () =>
            {
                if (quest == null || !quest.reroll_available || quest.completed)
                {
                    if (quest == null) GUI.Label(buttonRect, "", smallStyle);
                    return;
                }
                string target = F026QuestCalibration.RerollId(quest.quest_id);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !preview && !f024AccountBusy;
                calibration.Draw(target, "重抽任务箭头", buttonRect, () =>
                {
                    if (!F026DrawWidget(buttonRect, "quests.reroll"))
                        GUI.Label(buttonRect, "重抽", smallStyle);
                    if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, buttonRect);
                    F024ReplayGuiObserveControlBefore(target, buttonRect);
                    bool clicked = GUI.Button(buttonRect, GUIContent.none, GUIStyle.none);
                    F024ReplayGuiObserveControlAfter(target);
                    if (clicked && F026QuestCalibration.TryReroll(preview, () => F024RequestQuestReroll(quest.quest_id)))
                    {
                        F024ReplayGuiMarkHandler(target, "RerollQuest");
                        PlaySound("SFX_UI_CLICK");
                    }
                });
                GUI.enabled = previousEnabled;
            });
        });
    }
}
