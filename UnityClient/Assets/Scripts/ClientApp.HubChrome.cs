using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class ClientApp
{
    ClientOptions f026ClientOptions;
    bool f026OptionsDirty, f026ResolutionListOpen, f026QualityListOpen, f026QuestPreviewActive;
    bool f026ShopBuyOpen, f026FriendsOpen, f026FriendAddOpen, f026RequestsOpen;
    int f026ShopBuySetId = -1, f026ShopBuyCount = 1;
    string f026SelectedFriendAccountId = "";
    Rect[] f026SocialFlyoutActionRects;
    bool f026SocialFlyoutLayoutResolved;
    float f026OptionsSaveAt;
    string f026OptionsError = "", f026FriendName = "";
    Vector2 f026ResolutionScroll, f026FriendScroll, f026RequestScroll;
    GUIStyle f026MenuTextStyle, f026CurrencyTextStyle, f026FriendCountStyle;
    GUIStyle f026ShopTitleStyle, f026ShopDescriptionStyle, f026ShopValueStyle;

    static readonly int[] F026FrameRates = { 30, 60, 120, 144 };
    const float F026BaseMusicVolume = .25f;

    bool F026CameraShakeEnabled
    {
        get { return f026ClientOptions == null || f026ClientOptions.screenShake; }
    }

    bool F026ShowsHubChrome
    {
        get { return page != "battle"; }
    }

    void F026InitializeClientOptions()
    {
        f026ClientOptions = ClientOptions.Load(profilePath);
        QualitySettings.SetQualityLevel(f026ClientOptions.qualityLevel);
        Application.targetFrameRate = f026ClientOptions.targetFrameRate;
        Application.runInBackground = f026ClientOptions.runInBackground;
        AudioListener.volume = f026ClientOptions.masterVolume;
        if (music != null) music.volume = F026BaseMusicVolume * f026ClientOptions.musicVolume;
    }

    void F026ApplyClientOptions()
    {
        if (f026ClientOptions == null) return;
        QualitySettings.SetQualityLevel(f026ClientOptions.qualityLevel);
        Application.targetFrameRate = f026ClientOptions.targetFrameRate;
        Application.runInBackground = f026ClientOptions.runInBackground;
        AudioListener.volume = f026ClientOptions.masterVolume;
        if (music != null) music.volume = F026BaseMusicVolume * f026ClientOptions.musicVolume;
        if (!f026ClientOptions.screenShake) NativeCameraShakeDriver.Stop(cameraView);
    }

    void F026MarkOptionsChanged(string target, string handler)
    {
        if (f026ClientOptions == null) return;
        F026ApplyClientOptions();
        f026OptionsDirty = true;
        f026OptionsSaveAt = Time.unscaledTime + .4f;
        if (!String.IsNullOrEmpty(target) && !String.IsNullOrEmpty(handler))
            F024ReplayGuiMarkHandler(target, handler);
    }

    void F026PumpClientOptions()
    {
        if (!f026OptionsDirty || f026ClientOptions == null || Time.unscaledTime < f026OptionsSaveAt) return;
        f026OptionsDirty = false;
        if (!f026ClientOptions.Save(profilePath, out f026OptionsError))
            Debug.LogWarning("客户端选项无法保存：" + f026OptionsError);
    }

    bool F026DrawWidget(Rect rect, string widgetId)
    {
        return Event.current.type == EventType.Repaint && nativeHubWidgets != null &&
            nativeHubWidgets.DrawWidget(rect, widgetId);
    }

    void F026DrawOptionsFrame(Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        var texture = assets == null ? null : assets.Texture("UI_OPTIONS_FRAME");
        if (texture != null) GUI.DrawTextureWithTexCoords(rect, texture, new Rect(0, .315f, .698f, .685f), true);
        else Panel(rect);
    }

    bool F026NativeButton(Rect rect, string label, string target, string widgetId = "ui.button")
    {
        return F026NativeButton(rect, label, target, widgetId, null, Vector2.zero);
    }

    bool F026NativeScrollButton(Rect rect, string label, string target, string widgetId, Rect viewport, Vector2 scroll)
    {
        return F026NativeButton(rect, label, target, widgetId, viewport, scroll);
    }

    bool F026NativeButton(Rect rect, string label, string target, string widgetId, Rect? viewport, Vector2 scroll)
    {
        bool enabled = GUI.enabled;
        bool over = enabled && rect.Contains(Event.current.mousePosition);
        var old = GUI.color;
        if (!enabled) GUI.color = old * new Color(.6f, .6f, .6f, .82f);
        bool drawn = F026DrawWidget(rect, widgetId);
        if (!drawn)
        {
            Texture2D actionAtlas = Event.current.type == EventType.Repaint && assets != null
                ? assets.Texture("UI_MENU_ACTION") : null;
            if (actionAtlas != null)
                menuButtons.Draw(rect, menuButtons.Image(assets, false));
            else
            {
                Fill(rect, new Color(.2f, .13f, .08f, .92f));
                Frame(rect, over ? new Color(.95f, .71f, .28f) : new Color(.58f, .39f, .19f), 2);
            }
        }
        GUI.color = old;
        if (F024ReplayGuiEnabled)
        {
            if (viewport.HasValue) F024ReplayGuiRegisterTarget(target, rect, viewport.Value, scroll);
            else F024ReplayGuiRegisterTarget(target, rect);
        }
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        F024ReplayGuiObserveControlAfter(target);
        if (f026MenuTextStyle == null) f026MenuTextStyle = new GUIStyle(menuButtonStyle);
        f026MenuTextStyle.fontSize = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(rect.width * .09f, rect.height * .42f)), 14, 26);
        f026MenuTextStyle.alignment = TextAnchor.MiddleCenter;
        f026MenuTextStyle.wordWrap = false;
        f026MenuTextStyle.clipping = TextClipping.Clip;
        f026MenuTextStyle.normal.textColor = enabled ? new Color(.2f, .12f, .055f) : new Color(.35f, .29f, .22f);
        f026MenuTextStyle.hover.textColor = new Color(.46f, .24f, .06f);
        GUI.Label(rect, label, f026MenuTextStyle);
        if (clicked) PlaySound("SFX_UI_CLICK");
        return clicked;
    }

    bool F026PlainButton(Rect rect, string label, string target)
    {
        return F026NativeButton(rect, label, target);
    }

    void DrawF026HubChrome()
    {
        if (!F026ShowsHubChrome || F025HubOverlayOpen) return;

        Matrix4x4 inverse = GUI.matrix.inverse;
        Vector3 topLeft = inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 bottomRight = inverse.MultiplyPoint3x4(new Vector3(Screen.width, Screen.height, 0));
        Rect window = Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
        var friendRect = new Rect(window.xMin + 8, window.yMax - 70, 80, 40);
        if (!F026DrawWidget(friendRect, "chrome.friends"))
        {
            Fill(friendRect, new Color(.12f, .09f, .08f, .9f));
            Frame(friendRect, new Color(.62f, .46f, .26f), 3);
            GUI.Label(friendRect, "好友", smallStyle);
        }
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("social.open", friendRect);
        F024ReplayGuiObserveControlBefore("social.open", friendRect);
        if (GUI.Button(friendRect, GUIContent.none, GUIStyle.none))
        {
            f026FriendsOpen = true;
            f026FriendScroll = Vector2.zero;
            if (F024HasAccount) F024RequestAccountState();
            F024ReplayGuiMarkHandler("social.open", "OpenFriendsAndRefresh");
            PlaySound("SFX_UI_CLICK");
        }
        F024ReplayGuiObserveControlAfter("social.open");
        int onlineCount = F024HasAccount && f024Account.friends != null
            ? f024Account.friends.Count(friend => friend != null && friend.online) : 0;
        if (f026FriendCountStyle == null)
        {
            f026FriendCountStyle = new GUIStyle(smallStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(smallStyle.fontSize, 12, 20)
            };
        }
        GUI.Label(new Rect(friendRect.x + 52, friendRect.y, 24, friendRect.height), onlineCount.ToString(), f026FriendCountStyle);
        GUI.Label(new Rect(friendRect.x + 88, friendRect.y, 78, 40), DateTime.Now.ToString("HH:mm"), smallStyle);

        var currency = new Rect(window.xMax - 332, window.yMax - 75, 224, 62);
        if (F024HasAccount)
        {
            if (f026CurrencyTextStyle == null)
            {
                f026CurrencyTextStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight };
                f026CurrencyTextStyle.fontSize = Mathf.Max(14, smallStyle.fontSize);
            }
            if (page == "menu" || page == "collection")
            {
                var goldBar = new Rect(currency.x + 108, currency.y, 116, currency.height);
                F026DrawWidget(goldBar, "chrome.coinbar");
                var goldIcon = new Rect(goldBar.x + 80, goldBar.y + 17, 28, 28);
                if (!F026DrawWidget(goldIcon, "chrome.gold")) F024DrawNativeImage(goldIcon, "UI_GOLD", ScaleMode.ScaleToFit);
                GUI.Label(new Rect(goldBar.x + 4, goldBar.y, 72, goldBar.height), f024Account.gold.ToString(), f026CurrencyTextStyle);
                if (page == "collection")
                {
                    var dustBar = new Rect(currency.x - 16, currency.y, 116, currency.height);
                    F026DrawWidget(dustBar, "chrome.coinbar");
                    var dustIcon = new Rect(dustBar.x + 82, dustBar.y + 17, 26, 28);
                    if (!F026DrawWidget(dustIcon, "chrome.dust")) F024DrawNativeImage(dustIcon, "UI_DUST", ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(dustBar.x + 4, dustBar.y, 72, dustBar.height), f024Account.dust.ToString(), f026CurrencyTextStyle);
                }
            }
            else
            {
                F026DrawWidget(currency, "chrome.coinbar");
                var dustIcon = new Rect(currency.x + 68, currency.y + 17, 26, 28);
                var goldIcon = new Rect(currency.x + 176, currency.y + 17, 28, 28);
                if (!F026DrawWidget(goldIcon, "chrome.gold")) F024DrawNativeImage(goldIcon, "UI_GOLD", ScaleMode.ScaleToFit);
                if (!F026DrawWidget(dustIcon, "chrome.dust")) F024DrawNativeImage(dustIcon, "UI_DUST", ScaleMode.ScaleToFit);
                GUI.Label(new Rect(currency.x + 4, currency.y, 58, currency.height), f024Account.dust.ToString(), f026CurrencyTextStyle);
                GUI.Label(new Rect(currency.x + 100, currency.y, 66, currency.height), f024Account.gold.ToString(), f026CurrencyTextStyle);
            }
        }
        else GUI.Label(currency, "登录后查看钱包", smallStyle);

        var gearRect = new Rect(window.xMax - 100, window.yMax - 64, 84, 52);
        if (!F026DrawWidget(gearRect, "chrome.gear"))
        {
            var gear = cardVisuals == null ? null : cardVisuals.Prop("MODEL_GAME_MENU_BUTTON");
            if (gear != null) GUI.DrawTexture(gearRect, gear, ScaleMode.ScaleToFit, true);
            else
            {
                Fill(gearRect, new Color(.14f, .1f, .07f, .9f));
                Frame(gearRect, new Color(.62f, .46f, .26f), 3);
                GUI.Label(gearRect, "设置", smallStyle);
            }
        }
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.open", gearRect);
        F024ReplayGuiObserveControlBefore("settings.open", gearRect);
        if (GUI.Button(gearRect, GUIContent.none, GUIStyle.none))
        {
            F025OpenSettings(false);
            F024ReplayGuiMarkHandler("settings.open", "OpenSettings");
            PlaySound("SFX_UI_CLICK");
        }
        F024ReplayGuiObserveControlAfter("settings.open");
    }

    void DrawF026SettingsOverlay()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            F025CloseSettings();
            Event.current.Use();
            return;
        }

        Fill(new Rect(0, 0, 1600, 1000), new Color(.015f, .01f, .008f, .78f));
        if (f025SettingsTab == 0)
        {
            DrawF026GameMenu();
            return;
        }

        Rect panel = f025SettingsTab == 2 ? new Rect(420, 190, 760, 620) : new Rect(170, 105, 1260, 790);
        if (!F026DrawWidget(panel, "options.panel")) F026DrawOptionsFrame(panel);
        GUI.Label(new Rect(panel.x + 34, panel.y + 14, panel.width - 210, 58), F026SettingsTitle(), titleStyle);
        DrawF026SettingsTabs(panel);

        if (f025SettingsTab == 1) DrawF026OptionsPage(panel);
        else if (f025SettingsTab == 2) DrawF025AccountSettings(panel);
        else if (f025SettingsTab == 3) DrawF026OtherSettings(panel);
        else if (f025SettingsTab == 4) DrawF026PrivacySettings(panel);

        var closeRect = new Rect(panel.xMax - 132, panel.y + 18, 98, 42);
        if (F026PlainButton(closeRect, "关闭", "settings.close"))
        {
            F025CloseSettings();
            F024ReplayGuiMarkHandler("settings.close", "CloseSettings");
            return;
        }
        if (!String.IsNullOrEmpty(f024AccountMessage))
            GUI.Label(new Rect(panel.x + 34, panel.yMax - 40, panel.width - 68, 28), f024AccountMessage, smallStyle);
        if (!String.IsNullOrEmpty(f026OptionsError))
            GUI.Label(new Rect(panel.x + 34, panel.yMax - 40, panel.width - 68, 28), "选项保存失败：" + f026OptionsError, smallStyle);
    }

    void DrawF026GameMenu()
    {
        var panel = new Rect(520, 300, 560, 400);
        if (!F026DrawWidget(panel, "menu.panel")) F026DrawOptionsFrame(panel);
        var titleRect = new Rect(panel.x + 104, panel.y + 12, panel.width - 208, 58);
        F026DrawWidget(titleRect, "menu.header");
        GUI.Label(titleRect, "游戏菜单", titleStyle);
        var close = new Rect(panel.xMax - 104, panel.y + 18, 76, 38);
        if (F026PlainButton(close, "关闭", "settings.close"))
        {
            F025CloseSettings();
            F024ReplayGuiMarkHandler("settings.close", "CloseSettings");
            return;
        }
        if (F026NativeButton(new Rect(panel.x + 86, panel.y + 112, 388, 72), "选项", "settings.options", "menu.button"))
        {
            f025SettingsTab = 1;
            F024ReplayGuiMarkHandler("settings.options", "OpenOptions");
        }
        if (F026NativeButton(new Rect(panel.x + 86, panel.y + 220, 388, 72), "退出", "settings.quit", "menu.button"))
        {
            F024ReplayGuiMarkHandler("settings.quit", "QuitApplication");
            Application.Quit();
        }
    }

    string F026SettingsTitle()
    {
        if (f025SettingsTab == 1) return "选项";
        if (f025SettingsTab == 2) return "账号与会话";
        if (f025SettingsTab == 3) return "其他";
        if (f025SettingsTab == 4) return "隐私";
        return "设置";
    }

    void DrawF026SettingsTabs(Rect panel)
    {
        string[] labels = { "选项", "其他", "隐私", "账号与会话" };
        string[] targets = { "settings.options", "settings.other", "settings.privacy", "settings.account" };
        int[] tabs = { 1, 3, 4, 2 };
        float x = panel.x + 36, y = panel.y + 76, width = panel.width - 72;
        float gap = 10, tabWidth = (width - gap * (labels.Length - 1)) / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = new Rect(x + i * (tabWidth + gap), y, tabWidth, 44);
            bool selected = f025SettingsTab == tabs[i];
            if (selected) Frame(rect, new Color(.92f, .68f, .27f), 2);
            if (F026PlainButton(rect, labels[i], targets[i]))
            {
                f025SettingsTab = tabs[i];
                f026ResolutionListOpen = f026QualityListOpen = false;
                F024ReplayGuiMarkHandler(targets[i], i == 3 ? "OpenAccountSettings" : "OpenSettingsPage");
            }
        }
    }

    void DrawF026OptionsPage(Rect panel)
    {
        if (f026ClientOptions == null) return;
        float contentY = panel.y + 150;
        float leftX = panel.x + 42, columnWidth = 558;
        float rightX = panel.x + 650;
        GUI.Label(new Rect(leftX, contentY, columnWidth, 46), "图像", labelStyle);
        GUI.Label(new Rect(rightX, contentY, columnWidth, 46), "声音", labelStyle);

        var resolutionLabel = new Rect(leftX + 10, contentY + 58, 130, 38);
        GUI.Label(resolutionLabel, "分辨率", smallStyle);
        var resolutionField = new Rect(leftX + 148, contentY + 56, 390, 42);
        string selectedResolution = f026ClientOptions.resolutionWidth + " × " + f026ClientOptions.resolutionHeight;
        if (F026PlainButton(resolutionField, selectedResolution + "  ▼", "settings.display.resolution"))
            f026ResolutionListOpen = !f026ResolutionListOpen;
        if (f026ResolutionListOpen) DrawF026ResolutionList(resolutionField);

        GUI.Label(new Rect(leftX + 10, contentY + 122, 130, 38), "画质", smallStyle);
        var qualityField = new Rect(leftX + 148, contentY + 120, 390, 42);
        string[] qualityNames = QualitySettings.names;
        int quality = Mathf.Clamp(f026ClientOptions.qualityLevel, 0, Mathf.Max(0, qualityNames.Length - 1));
        string qualityName = qualityNames.Length == 0 ? "默认" : qualityNames[quality];
        if (F026PlainButton(qualityField, qualityName + "  ▼", "settings.display.quality"))
            f026QualityListOpen = !f026QualityListOpen;
        if (f026QualityListOpen) DrawF026QualityList(qualityField, qualityNames);

        GUI.Label(new Rect(leftX + 10, contentY + 186, 130, 38), "帧率", smallStyle);
        for (int i = 0; i < F026FrameRates.Length; i++)
        {
            int rate = F026FrameRates[i];
            var rect = new Rect(leftX + 148 + i * 98, contentY + 182, 90, 44);
            string target = "settings.display.fps:" + rate;
            if (rate == f026ClientOptions.targetFrameRate) Frame(rect, new Color(.92f, .68f, .27f), 2);
            if (F026PlainButton(rect, rate.ToString(), target) && rate != f026ClientOptions.targetFrameRate)
            {
                f026ClientOptions.targetFrameRate = rate;
                F026MarkOptionsChanged(target, "SetFrameRate");
            }
        }

        var fullscreenRect = new Rect(leftX + 10, contentY + 246, columnWidth - 20, 46);
        F024ReplayGuiObserveControlBefore("settings.display.fullscreen", fullscreenRect);
        bool fullscreen = GUI.Toggle(fullscreenRect, f026ClientOptions.fullscreen, "全屏模式偏好（应用后生效）", GUI.skin.toggle);
        F024ReplayGuiObserveControlAfter("settings.display.fullscreen");
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.display.fullscreen", fullscreenRect);
        if (fullscreen != f026ClientOptions.fullscreen)
        {
            f026ClientOptions.fullscreen = fullscreen;
            F026MarkOptionsChanged("settings.display.fullscreen", "SetFullscreenPreference");
        }
        GUI.Label(new Rect(leftX + 10, contentY + 292, columnWidth - 20, 40),
            "当前 " + Screen.width + " × " + Screen.height + (Screen.fullScreen ? " · 全屏" : " · 窗口"), smallStyle);
        if (F026NativeButton(new Rect(leftX + 148, contentY + 348, 300, 52), "应用显示设置", "settings.display.apply"))
        {
            Screen.SetResolution(f026ClientOptions.resolutionWidth, f026ClientOptions.resolutionHeight, f026ClientOptions.fullscreen);
            f026ClientOptions.qualityLevel = Mathf.Clamp(f026ClientOptions.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            F026MarkOptionsChanged("settings.display.apply", "ApplyDisplaySettings");
        }

        F026DrawVolumeSlider(new Rect(rightX + 12, contentY + 66, columnWidth - 24, 76),
            "settings.audio.master", "主音量", true);
        F026DrawVolumeSlider(new Rect(rightX + 12, contentY + 164, columnWidth - 24, 76),
            "settings.audio.music", "音乐音量", false);
        var linksY = contentY + 280;
        if (F026NativeButton(new Rect(rightX + 8, linksY, 230, 50), "其他", "settings.other"))
        {
            f025SettingsTab = 3;
            F024ReplayGuiMarkHandler("settings.other", "OpenOtherSettings");
        }
        if (F026NativeButton(new Rect(rightX + 252, linksY, 230, 50), "隐私", "settings.privacy"))
        {
            f025SettingsTab = 4;
            F024ReplayGuiMarkHandler("settings.privacy", "OpenPrivacySettings");
        }
        if (F026NativeButton(new Rect(rightX + 8, linksY + 70, 474, 50), "账号与会话", "settings.account"))
        {
            f025SettingsTab = 2;
            F024ReplayGuiMarkHandler("settings.account", "OpenAccountSettings");
        }
    }

    void F026DrawVolumeSlider(Rect rect, string target, string label, bool master)
    {
        float current = master ? f026ClientOptions.masterVolume : f026ClientOptions.musicVolume;
        GUI.Label(new Rect(rect.x, rect.y, rect.width - 80, 34), label, smallStyle);
        GUI.Label(new Rect(rect.xMax - 76, rect.y, 76, 34), Mathf.RoundToInt(current * 100) + "%", smallStyle);
        var slider = new Rect(rect.x + 4, rect.y + 40, rect.width - 8, 26);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, slider);
        F024ReplayGuiObserveControlBefore(target, slider);
        float next = GUI.HorizontalSlider(slider, current, 0f, 1f);
        F024ReplayGuiObserveControlAfter(target);
        if (Mathf.Abs(next - current) < .001f) return;
        if (master) f026ClientOptions.masterVolume = next;
        else f026ClientOptions.musicVolume = next;
        F026MarkOptionsChanged(target, master ? "SetMasterVolume" : "SetMusicVolume");
    }

    Resolution[] F026Resolutions()
    {
        var resolutions = Screen.resolutions ?? new Resolution[0];
        var unique = resolutions.GroupBy(item => item.width + "x" + item.height)
            .Select(group => group.OrderByDescending(item => item.refreshRate).First())
            .OrderBy(item => item.width).ThenBy(item => item.height).ToArray();
        if (unique.Length > 0) return unique;
        return new[] { Screen.currentResolution };
    }

    void DrawF026ResolutionList(Rect field)
    {
        var values = F026Resolutions();
        var popup = new Rect(field.x, field.yMax + 4, field.width, Mathf.Min(236, values.Length * 34 + 8));
        Fill(popup, new Color(.08f, .05f, .035f, .98f));
        Frame(popup, new Color(.68f, .48f, .23f), 2);
        var view = new Rect(popup.x + 4, popup.y + 4, popup.width - 8, popup.height - 8);
        f026ResolutionScroll = GUI.BeginScrollView(view, f026ResolutionScroll,
            new Rect(0, 0, view.width - 18, values.Length * 34));
        for (int i = 0; i < values.Length; i++)
        {
            var item = values[i];
            var rect = new Rect(0, i * 34, view.width - 18, 32);
            string label = item.width + " × " + item.height;
            string target = "settings.display.resolution:" + item.width + "x" + item.height;
            if (item.width == f026ClientOptions.resolutionWidth && item.height == f026ClientOptions.resolutionHeight)
                Frame(rect, new Color(.92f, .68f, .27f), 1);
            if (F026ScrollButton(rect, label, target, view, f026ResolutionScroll))
            {
                f026ClientOptions.resolutionWidth = item.width;
                f026ClientOptions.resolutionHeight = item.height;
                f026ResolutionListOpen = false;
                F026MarkOptionsChanged(target, "SetResolution");
            }
        }
        GUI.EndScrollView();
    }

    void DrawF026QualityList(Rect field, string[] qualityNames)
    {
        if (qualityNames == null || qualityNames.Length == 0) return;
        var popup = new Rect(field.x, field.yMax + 4, field.width, qualityNames.Length * 38 + 8);
        Fill(popup, new Color(.08f, .05f, .035f, .98f));
        Frame(popup, new Color(.68f, .48f, .23f), 2);
        for (int i = 0; i < qualityNames.Length; i++)
        {
            int selected = i;
            var rect = new Rect(popup.x + 6, popup.y + 4 + i * 38, popup.width - 12, 36);
            if (selected == f026ClientOptions.qualityLevel) Frame(rect, new Color(.92f, .68f, .27f), 1);
            string target = "settings.display.quality:" + selected;
            if (F026PlainButton(rect, qualityNames[i], target) && selected != f026ClientOptions.qualityLevel)
            {
                f026ClientOptions.qualityLevel = selected;
                f026QualityListOpen = false;
                F026MarkOptionsChanged(target, "SetQualityLevel");
            }
        }
    }

    bool F026ScrollButton(Rect rect, string label, string target, Rect view, Vector2 scroll)
    {
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect, view, scroll);
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, label, F024ButtonStyle(rect));
        F024ReplayGuiObserveControlAfter(target);
        if (clicked) PlaySound("SFX_UI_CLICK");
        return clicked;
    }

    void DrawF026OtherSettings(Rect panel)
    {
        float x = panel.x + 86, y = panel.y + 180;
        var shakeRect = new Rect(x, y, 520, 48);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.other.screen-shake", shakeRect);
        F024ReplayGuiObserveControlBefore("settings.other.screen-shake", shakeRect);
        bool shake = GUI.Toggle(shakeRect, f026ClientOptions.screenShake, "开启屏幕抖动", GUI.skin.toggle);
        F024ReplayGuiObserveControlAfter("settings.other.screen-shake");
        if (shake != f026ClientOptions.screenShake)
        {
            f026ClientOptions.screenShake = shake;
            F026MarkOptionsChanged("settings.other.screen-shake", "SetScreenShake");
        }
        var backgroundRect = new Rect(x, y + 72, 520, 48);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.other.background", backgroundRect);
        F024ReplayGuiObserveControlBefore("settings.other.background", backgroundRect);
        bool background = GUI.Toggle(backgroundRect, f026ClientOptions.runInBackground, "窗口失焦时继续运行", GUI.skin.toggle);
        F024ReplayGuiObserveControlAfter("settings.other.background");
        if (background != f026ClientOptions.runInBackground)
        {
            f026ClientOptions.runInBackground = background;
            F026MarkOptionsChanged("settings.other.background", "SetRunInBackground");
        }
        GUI.Label(new Rect(x, y + 150, 1050, 52), "选项保存在当前 profile；声音主音量会影响游戏内所有音源。", smallStyle);
    }

    void DrawF026PrivacySettings(Rect panel)
    {
        float x = panel.x + 86, y = panel.y + 180;
        if (!F024HasAccount)
        {
            GUI.Label(new Rect(x, y, panel.width - 172, 66), "登录账号后可管理在线状态与好友申请。", labelStyle);
            if (F026NativeButton(new Rect(x, y + 92, 330, 56), "登录或注册", "settings.account"))
            {
                f025SettingsTab = 2;
                F024ReplayGuiMarkHandler("settings.account", "OpenAccountSettings");
            }
            return;
        }
        if (f024Account.privacy == null)
        {
            GUI.Label(new Rect(x, y, panel.width - 172, 66), "正在同步服务器隐私设置……", labelStyle);
            return;
        }
        bool previous = GUI.enabled;
        GUI.enabled = previous && !f024AccountBusy;
        var hideRect = new Rect(x, y, 700, 48);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.privacy.hide-online", hideRect);
        F024ReplayGuiObserveControlBefore("settings.privacy.hide-online", hideRect);
        bool hideOnline = GUI.Toggle(hideRect, f024Account.privacy.hide_online, "隐藏在线状态", GUI.skin.toggle);
        F024ReplayGuiObserveControlAfter("settings.privacy.hide-online");
        var requestsRect = new Rect(x, y + 72, 700, 48);
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("settings.privacy.friend-requests", requestsRect);
        F024ReplayGuiObserveControlBefore("settings.privacy.friend-requests", requestsRect);
        bool allowRequests = GUI.Toggle(requestsRect, f024Account.privacy.allow_friend_requests, "允许好友申请", GUI.skin.toggle);
        F024ReplayGuiObserveControlAfter("settings.privacy.friend-requests");
        GUI.enabled = previous;
        bool hideChanged = hideOnline != f024Account.privacy.hide_online;
        bool requestsChanged = allowRequests != f024Account.privacy.allow_friend_requests;
        if (hideChanged || requestsChanged)
        {
            F026RequestPrivacyUpdate(hideOnline, allowRequests);
            if (hideChanged) F024ReplayGuiMarkHandler("settings.privacy.hide-online", "UpdateAccountPrivacy");
            if (requestsChanged) F024ReplayGuiMarkHandler("settings.privacy.friend-requests", "UpdateAccountPrivacy");
        }
        GUI.Label(new Rect(x, y + 150, 1040, 50), "隐私设置由账号服务保存。", smallStyle);
    }

    void F026OpenShopBuy(int setId)
    {
        if (!F024HasAccount || f024AccountBusy || setId < 0) return;
        f026ShopBuySetId = setId;
        f026ShopBuyCount = 1;
        f026ShopBuyOpen = true;
    }

    void F026CloseShopBuy()
    {
        f026ShopBuyOpen = false;
        f026ShopBuySetId = -1;
        f026ShopBuyCount = 1;
    }

    bool F026ShopBuyControl(Rect rect, string label, string target)
    {
        return F026NativeButton(rect, label, target, "menu.button");
    }

    GUIStyle F026ShopDescriptionTextStyle()
    {
        if (f026ShopDescriptionStyle == null)
        {
            f026ShopDescriptionStyle = new GUIStyle(smallStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 20,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            f026ShopDescriptionStyle.normal.textColor = new Color(.22f, .15f, .08f);
        }
        return f026ShopDescriptionStyle;
    }

    void DrawF026ShopBuyOverlay()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            F026CloseShopBuy();
            Event.current.Use();
            return;
        }
        Fill(new Rect(0, 0, 1600, 1000), new Color(.01f, .008f, .006f, .84f));
        var panel = new Rect(280, 120, 1040, 760);
        bool nativeFrameDrawn = F026DrawWidget(panel, "modal.bulkBuy.frame");
        if (!nativeFrameDrawn) F026DrawOptionsFrame(panel);

        var item = ((f024Account == null ? null : f024Account.shop) ?? new F024AccountShopItem[0])
            .FirstOrDefault(row => row != null && row.set_id == f026ShopBuySetId);
        Rect previewArea = Rect.zero, descriptionArea = Rect.zero, quantityArea = Rect.zero;
        bool nativeLayout = nativeHubWidgets != null &&
            nativeHubWidgets.TryGetBulkBuyLayout(panel, out previewArea, out descriptionArea, out quantityArea);
        if (!nativeLayout)
        {
            previewArea = new Rect(panel.x + panel.width * .052f, panel.y + panel.height * .071f,
                panel.width * .897f, panel.height * .624f);
            descriptionArea = new Rect(panel.x + panel.width * .040f, panel.y + panel.height * .706f,
                panel.width * .676f, panel.height * .269f);
            quantityArea = new Rect(panel.x + panel.width * .718f, panel.y + panel.height * .718f,
                panel.width * .245f, panel.height * .259f);
        }

        string setName = F024SetName(f026ShopBuySetId);
        var titleRect = new Rect(previewArea.x + previewArea.width * .045f,
            previewArea.y + previewArea.height * .045f, previewArea.width * .60f, previewArea.height * .27f);
        if (f026ShopTitleStyle == null)
        {
            f026ShopTitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }
        GUI.Label(titleRect, setName, f026ShopTitleStyle);

        var preview = new Rect(previewArea.x + previewArea.width * .68f,
            previewArea.y + previewArea.height * .19f, previewArea.width * .27f, previewArea.height * .75f);
        if (Event.current.type == EventType.Repaint && f026ShopBuySetId >= 0)
            F024DrawPackModel(preview, f026ShopBuySetId);

        GUIStyle descriptionStyle = F026ShopDescriptionTextStyle();
        GUI.Label(new Rect(descriptionArea.x + 28, descriptionArea.y + 30,
            descriptionArea.width - 56, 46), "•  每包包含 5 张卡牌", descriptionStyle);
        GUI.Label(new Rect(descriptionArea.x + 28, descriptionArea.y + 94,
            descriptionArea.width - 56, 46), "•  至少 1 张为稀有或更高品质", descriptionStyle);

        var minus = new Rect(quantityArea.x + quantityArea.width * .08f,
            quantityArea.y + quantityArea.height * .08f, quantityArea.width * .23f, quantityArea.height * .35f);
        var count = new Rect(quantityArea.x + quantityArea.width * .34f,
            quantityArea.y + quantityArea.height * .10f, quantityArea.width * .32f, quantityArea.height * .31f);
        var plus = new Rect(quantityArea.x + quantityArea.width * .70f,
            quantityArea.y + quantityArea.height * .08f, quantityArea.width * .23f, quantityArea.height * .35f);
        if (!nativeFrameDrawn)
        {
            Fill(count, new Color(.08f, .05f, .04f, .94f));
            Frame(count, new Color(.62f, .43f, .2f), 2);
        }
        if (f026ShopValueStyle == null)
        {
            f026ShopValueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            f026ShopValueStyle.normal.textColor = new Color(1f, .91f, .68f);
        }

        bool previous = GUI.enabled;
        GUI.enabled = previous && f026ShopBuyCount > 1;
        bool minusClicked = F026ShopBuyControl(minus, "−", "shop.confirm.count.minus");
        if (minusClicked)
        {
            f026ShopBuyCount = Mathf.Max(1, f026ShopBuyCount - 1);
            F024ReplayGuiMarkHandler("shop.confirm.count.minus", "ShopBuyCountDown");
        }
        GUI.enabled = previous;
        GUI.Label(count, f026ShopBuyCount.ToString(), f026ShopValueStyle);
        GUI.enabled = previous && f026ShopBuyCount < 1000;
        bool plusClicked = F026ShopBuyControl(plus, "+", "shop.confirm.count.plus");
        if (plusClicked)
        {
            f026ShopBuyCount = Mathf.Min(1000, f026ShopBuyCount + 1);
            F024ReplayGuiMarkHandler("shop.confirm.count.plus", "ShopBuyCountUp");
        }
        GUI.enabled = previous;

        long totalCost = (long)(item == null ? 100 : item.price) * f026ShopBuyCount;
        int balance = f024Account == null ? 0 : f024Account.gold;
        bool validItem = item != null && item.price == 100 && f026ShopBuyCount >= 1 && f026ShopBuyCount <= 1000;
        bool enough = totalCost <= balance;
        var message = new Rect(quantityArea.x + 6, quantityArea.y + quantityArea.height * .43f,
            quantityArea.width - 12, quantityArea.height * .12f);
        if (!validItem) GUI.Label(message, "商品信息无效", descriptionStyle);
        else if (!enough) GUI.Label(message, "金币不足", descriptionStyle);
        else if (f024AccountBusy) GUI.Label(message, "处理中……", descriptionStyle);

        var confirm = new Rect(quantityArea.x + quantityArea.width * .04f,
            quantityArea.y + quantityArea.height * .56f, quantityArea.width * .92f, quantityArea.height * .34f);
        GUI.enabled = previous && validItem && enough && !f024AccountBusy;
        string confirmTarget = "shop.confirm:" + f026ShopBuySetId;
        bool confirmClicked = F026ShopBuyControl(confirm,
            "确认购买 · " + totalCost + " 金币", confirmTarget);
        if (confirmClicked)
        {
            int setId = f026ShopBuySetId, quantity = f026ShopBuyCount;
            F024RequestShopBuy(setId, quantity);
            F024ReplayGuiMarkHandler(confirmTarget, "ConfirmShopBuy");
            F026CloseShopBuy();
        }
        GUI.enabled = previous;
        var cancel = new Rect(panel.xMax - 126, panel.y + 14, 108, 42);
        if (F026PlainButton(cancel, "取消", "shop.confirm.cancel"))
        {
            F026CloseShopBuy();
            F024ReplayGuiMarkHandler("shop.confirm.cancel", "CloseShopBuyModal");
        }
    }

    void DrawF026FriendsPanel()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            if (f026RequestsOpen)
            {
                f026RequestsOpen = false;
                F024ReplayGuiMarkHandler("social.requests.close", "CloseFriendRequests");
            }
            else
            {
                F026CloseFriendsPanel();
                F024ReplayGuiMarkHandler("social.close", "CloseFriendsPanel");
            }
            Event.current.Use();
            return;
        }
        Fill(new Rect(0, 0, 1600, 1000), new Color(.01f, .008f, .006f, .34f));
        bool enabled = GUI.enabled;
        if (f026RequestsOpen) GUI.enabled = false;
        var panel = new Rect(12, 32, 416, 936);
        if (!F026DrawWidget(panel, "social.panel.frame")) Panel(panel);
        var ownIcon = cardVisuals == null ? null : cardVisuals.Prop("MODEL_CARD_BACK");
        if (ownIcon != null) GUI.DrawTexture(new Rect(panel.x + 18, panel.y + 14, 66, 78), ownIcon, ScaleMode.ScaleToFit, true);
        string ownLabel = F024HasAccount ? f024Account.display_name + " #" + f024Account.account_id.Substring(0, 8) : "尚未登录";
        GUI.Label(new Rect(panel.x + 92, panel.y + 18, panel.width - 156, 48), ownLabel, smallStyle);
        var close = new Rect(panel.xMax - 58, panel.y + 18, 44, 40);
        if (F026PlainButton(close, "×", "social.close"))
        {
            F026CloseFriendsPanel();
            F024ReplayGuiMarkHandler("social.close", "CloseFriendsPanel");
            return;
        }

        var friendArray = (f024Account == null ? null : f024Account.friends) ?? new F026Friend[0];
        var friends = friendArray.Where(friend => friend != null)
            .OrderByDescending(friend => friend.online)
            .ThenBy(friend => friend.display_name ?? friend.username, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!String.IsNullOrEmpty(f026SelectedFriendAccountId) &&
            !friends.Any(friend => friend.account_id == f026SelectedFriendAccountId))
            f026SelectedFriendAccountId = "";
        F026FriendRequest[] incomingSource = f024Account == null ? null : f024Account.incoming_friend_requests;
        F026FriendRequest[] outgoingSource = f024Account == null ? null : f024Account.outgoing_friend_requests;
        var incoming = (incomingSource ?? new F026FriendRequest[0]).Where(request => request != null).ToArray();
        var outgoing = (outgoingSource ?? new F026FriendRequest[0]).Where(request => request != null).ToArray();

        GUI.Label(new Rect(panel.x + 24, panel.y + 68, panel.width - 48, 36),
            F024HasAccount ? "好友 · " + friends.Count(friend => friend.online) + "/" + friends.Length + " 在线" : "未登录", smallStyle);
        if (!F024HasAccount)
        {
            GUI.Label(new Rect(panel.x + 26, panel.y + 150, panel.width - 52, 86), "登录后可查看和管理真实好友。", labelStyle);
            if (F026NativeButton(new Rect(panel.x + 35, panel.y + 270, panel.width - 70, 60), "登录或注册", "settings.account"))
            {
                f026FriendsOpen = false;
                F025OpenSettings(true);
                F024ReplayGuiMarkHandler("settings.account", "OpenAccountSettings");
            }
            return;
        }

        var friendViewport = new Rect(panel.x + 12, panel.y + 112, panel.width - 24, panel.height - 132);
        f026FriendScroll = GUI.BeginScrollView(friendViewport, f026FriendScroll,
            new Rect(0, 0, friendViewport.width - 18, Mathf.Max(1, friends.Length) * 76));
        for (int i = 0; i < friends.Length; i++)
        {
            var friend = friends[i];
            var row = new Rect(0, i * 76, friendViewport.width - 18, 72);
            bool selected = friend.account_id == f026SelectedFriendAccountId;
            if (selected) Fill(row, new Color(.52f, .37f, .18f, .55f));
            Frame(row, new Color(.32f, .28f, .24f), 1);
            var statusDot = new Rect(row.x + 12, row.y + 25, 16, 16);
            Fill(statusDot, friend.online ? new Color(.24f, .78f, .25f) : new Color(.42f, .42f, .42f));
            GUI.Label(new Rect(row.x + 38, row.y + 5, row.width - 54, 32),
                (String.IsNullOrEmpty(friend.display_name) ? friend.username : friend.display_name) +
                "  #" + (friend.short_id ?? ""), smallStyle);
            GUI.Label(new Rect(row.x + 38, row.y + 36, row.width - 54, 28),
                friend.online ? "在线" : F026LastSeen(friend.last_seen), smallStyle);
            string selectTarget = "social.select:" + friend.account_id;
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(selectTarget, row, friendViewport, f026FriendScroll);
            F024ReplayGuiObserveControlBefore(selectTarget, row);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                f026SelectedFriendAccountId = friend.account_id;
                F024ReplayGuiMarkHandler(selectTarget, "SelectFriend");
                PlaySound("SFX_UI_CLICK");
            }
            F024ReplayGuiObserveControlAfter(selectTarget);
        }
        if (friends.Length == 0) GUI.Label(new Rect(18, 18, friendViewport.width - 48, 46), "好友列表为空。", smallStyle);
        GUI.EndScrollView();

        var selectedFriend = friends.FirstOrDefault(friend => friend.account_id == f026SelectedFriendAccountId);
        var flyout = new Rect(panel.xMax + 14, panel.y + 2, 320, 274);
        if (!f026SocialFlyoutLayoutResolved)
        {
            f026SocialFlyoutLayoutResolved = true;
            Rect[] measured;
            if (nativeHubWidgets != null && nativeHubWidgets.TryGetSocialFlyoutLayout(flyout, out measured) &&
                measured != null && measured.Length == 4)
                f026SocialFlyoutActionRects = measured;
        }
        bool nativeFlyoutAvailable = f026SocialFlyoutActionRects != null;
        F026DrawWidget(flyout, "social.flyout");
        Rect[] actionRects = nativeFlyoutAvailable ? f026SocialFlyoutActionRects : new[]
        {
            new Rect(flyout.x + 12, flyout.y + 6, flyout.width - 24, 52),
            new Rect(flyout.x + 12, flyout.y + 72, flyout.width - 24, 52),
            new Rect(flyout.x + 12, flyout.y + 138, flyout.width - 24, 52),
            new Rect(flyout.x + 12, flyout.y + 204, flyout.width - 24, 52)
        };
        DrawF026SocialFlyoutActions(actionRects, selectedFriend, nativeFlyoutAvailable);

        float contentY = flyout.yMax + 14;
        if (f026FriendAddOpen)
        {
            var username = new Rect(flyout.x + 6, contentY, 198, 44);
            if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget("social.add.username", username);
            F024ReplayGuiObserveControlBefore("social.add.username", username);
            f026FriendName = GUI.TextField(username, f026FriendName ?? "", 32);
            F024ReplayGuiObserveControlAfter("social.add.username");
            GUI.enabled = enabled && !f024AccountBusy && !String.IsNullOrWhiteSpace(f026FriendName);
            var add = new Rect(username.xMax + 8, contentY, 108, 44);
            if (F026NativeButton(add, "发送申请", "social.add"))
            {
                F026RequestFriendAdd(f026FriendName.Trim());
                f026FriendName = "";
                F024ReplayGuiMarkHandler("social.add", "AddFriend");
            }
            GUI.enabled = enabled;
            contentY += 56;
        }

        if (selectedFriend != null)
        {
            string displayName = String.IsNullOrEmpty(selectedFriend.display_name) ? selectedFriend.username : selectedFriend.display_name;
            GUI.Label(new Rect(flyout.x, flyout.yMax + 126, 620, 50), displayName + "  #" + (selectedFriend.short_id ?? ""), labelStyle);
            GUI.Label(new Rect(flyout.x, flyout.yMax + 180, 620, 42), selectedFriend.online ? "在线" : F026LastSeen(selectedFriend.last_seen), smallStyle);
        }
        else GUI.Label(new Rect(flyout.x, flyout.yMax + 126, 620, 50), "选择左侧好友查看在线状态。", labelStyle);
        if (!String.IsNullOrEmpty(f024AccountMessage))
            GUI.Label(new Rect(flyout.x, flyout.yMax + 234, 740, 46), f024AccountMessage, smallStyle);
        GUI.enabled = enabled;
        if (f026RequestsOpen) DrawF026FriendRequestsDialog(incoming, outgoing);
    }

    void DrawF026SocialFlyoutActions(Rect[] actionRects, F026Friend selectedFriend, bool nativePanelDrawn)
    {
        if (actionRects == null || actionRects.Length != 4) return;
        string[] labels = { f026FriendAddOpen ? "收起添加" : "添加好友", "好友申请", "移除好友", "隐私设置" };
        string removeTarget = selectedFriend == null ? "social.remove" : "social.remove:" + selectedFriend.account_id;
        string[] targets = { "social.add.open", "social.requests.open", removeTarget, "settings.privacy" };
        bool enabled = GUI.enabled;
        for (int i = 0; i < actionRects.Length; i++)
        {
            bool actionEnabled = i != 2 || selectedFriend != null && !f024AccountBusy;
            GUI.enabled = enabled && actionEnabled;
            bool clicked = nativePanelDrawn
                ? F026OverlayButton(actionRects[i], labels[i], targets[i])
                : F026NativeButton(actionRects[i], labels[i], targets[i]);
            if (!clicked) continue;
            if (i == 0)
            {
                f026FriendAddOpen = !f026FriendAddOpen;
                F024ReplayGuiMarkHandler(targets[i], "ToggleFriendAdd");
            }
            else if (i == 1)
            {
                f026RequestsOpen = true;
                f026FriendAddOpen = false;
                F024ReplayGuiMarkHandler(targets[i], "OpenFriendRequests");
            }
            else if (i == 2)
            {
                F026RequestFriendRemove(selectedFriend.account_id);
                F024ReplayGuiMarkHandler(targets[i], "RemoveFriend");
            }
            else
            {
                f026FriendsOpen = false;
                F025OpenSettings(false);
                f025SettingsTab = 4;
                F024ReplayGuiMarkHandler(targets[i], "OpenPrivacySettings");
            }
        }
        GUI.enabled = enabled;
    }

    bool F026OverlayButton(Rect rect, string label, string target)
    {
        if (F024ReplayGuiEnabled) F024ReplayGuiRegisterTarget(target, rect);
        F024ReplayGuiObserveControlBefore(target, rect);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        F024ReplayGuiObserveControlAfter(target);
        var style = f026MenuTextStyle ?? (f026MenuTextStyle = new GUIStyle(menuButtonStyle));
        style.fontSize = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(rect.width * .09f, rect.height * .42f)), 14, 26);
        style.alignment = TextAnchor.MiddleCenter;
        style.wordWrap = false;
        style.clipping = TextClipping.Clip;
        style.normal.textColor = GUI.enabled ? new Color(.2f, .12f, .055f) : new Color(.35f, .29f, .22f);
        GUI.Label(rect, label, style);
        if (clicked) PlaySound("SFX_UI_CLICK");
        return clicked;
    }

    void DrawF026FriendRequestsDialog(F026FriendRequest[] incoming, F026FriendRequest[] outgoing)
    {
        Fill(new Rect(0, 0, 1600, 1000), new Color(.01f, .008f, .006f, .56f));
        var panel = new Rect(520, 180, 560, 640);
        F026DrawOptionsFrame(panel);
        GUI.Label(new Rect(panel.x + 30, panel.y + 14, 390, 50), "好友申请", titleStyle);
        var close = new Rect(panel.xMax - 104, panel.y + 18, 78, 40);
        if (F026NativeButton(close, "关闭", "social.requests.close"))
        {
            f026RequestsOpen = false;
            F024ReplayGuiMarkHandler("social.requests.close", "CloseFriendRequests");
            return;
        }

        var viewport = new Rect(panel.x + 22, panel.y + 82, panel.width - 44, panel.height - 110);
        float contentHeight = 70 + incoming.Length * 68 + 58 + outgoing.Length * 64;
        f026RequestScroll = GUI.BeginScrollView(viewport, f026RequestScroll,
            new Rect(0, 0, viewport.width - 18, Mathf.Max(viewport.height, contentHeight)));
        float y = 4;
        GUI.Label(new Rect(6, y, viewport.width - 34, 34), "收到的申请 · " + incoming.Length, labelStyle);
        y += 40;
        for (int i = 0; i < incoming.Length; i++)
        {
            var request = incoming[i];
            var row = new Rect(0, y, viewport.width - 18, 64);
            GUI.Label(new Rect(row.x + 4, row.y + 5, row.width - 148, 52),
                (String.IsNullOrEmpty(request.display_name) ? request.username : request.display_name) + " 请求添加你", smallStyle);
            string acceptTarget = "social.accept:" + request.request_id;
            var accept = new Rect(row.xMax - 136, row.y + 9, 62, 44);
            var reject = new Rect(row.xMax - 68, row.y + 9, 62, 44);
            bool previous = GUI.enabled;
            GUI.enabled = previous && !f024AccountBusy;
            if (F026NativeScrollButton(accept, "接受", acceptTarget, "ui.button", viewport, f026RequestScroll))
            {
                F026RequestFriendAccept(request.request_id);
                F024ReplayGuiMarkHandler(acceptTarget, "AcceptFriendRequest");
            }
            string rejectTarget = "social.reject:" + request.request_id;
            if (F026NativeScrollButton(reject, "拒绝", rejectTarget, "ui.button", viewport, f026RequestScroll))
            {
                F026RequestFriendReject(request.request_id);
                F024ReplayGuiMarkHandler(rejectTarget, "RejectFriendRequest");
            }
            GUI.enabled = previous;
            y += 68;
        }
        if (incoming.Length == 0) GUI.Label(new Rect(8, y, viewport.width - 34, 36), "暂无收到的申请。", smallStyle);
        y += 46;
        GUI.Label(new Rect(6, y, viewport.width - 34, 34), "已发送的申请 · " + outgoing.Length, labelStyle);
        y += 40;
        for (int i = 0; i < outgoing.Length; i++)
        {
            var request = outgoing[i];
            var row = new Rect(0, y, viewport.width - 18, 60);
            GUI.Label(new Rect(row.x + 4, row.y + 4, row.width - 120, 50),
                (String.IsNullOrEmpty(request.display_name) ? request.username : request.display_name) + " · 等待回应", smallStyle);
            string target = "social.cancel:" + request.request_id;
            bool previous = GUI.enabled;
            GUI.enabled = previous && !f024AccountBusy;
            if (F026NativeScrollButton(new Rect(row.xMax - 112, row.y + 8, 104, 42), "取消", target, "ui.button", viewport, f026RequestScroll))
            {
                F026RequestFriendCancel(request.request_id);
                F024ReplayGuiMarkHandler(target, "CancelFriendRequest");
            }
            GUI.enabled = previous;
            y += 64;
        }
        if (outgoing.Length == 0) GUI.Label(new Rect(8, y, viewport.width - 34, 36), "暂无待处理申请。", smallStyle);
        GUI.EndScrollView();
    }

    void F026CloseFriendsPanel()
    {
        f026FriendsOpen = false;
        f026FriendAddOpen = false;
        f026RequestsOpen = false;
    }

    string F026LastSeen(long unixSeconds)
    {
        if (unixSeconds <= 0) return "最近未在线";
        DateTimeOffset seen;
        try { seen = DateTimeOffset.FromUnixTimeSeconds(unixSeconds); }
        catch (ArgumentOutOfRangeException) { return "最近未在线"; }
        double minutes = Math.Max(0, (DateTimeOffset.UtcNow - seen).TotalMinutes);
        if (minutes < 1) return "刚刚在线";
        if (minutes < 60) return ((int)minutes) + " 分钟前在线";
        if (minutes < 1440) return ((int)(minutes / 60)) + " 小时前在线";
        return ((int)(minutes / 1440)) + " 天前在线";
    }
}
