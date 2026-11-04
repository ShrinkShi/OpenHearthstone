using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

// 为 IMGUI 页面提供可选的原生 Prefab 与图标绘制。
public sealed class NativeHubWidgets : IDisposable
{
    sealed class SceneWidget
    {
        public GameObject root;
        public Renderer[] renderers;
        public Material[] materials;
        public Texture2D[] clonedTextures;
        public RenderTexture texture;
        public Rect contentUv = new Rect(0, 0, 1, 1);
        public string materialAdapter;
        public bool trimContent;
        public float aspect;
        public int height;
    }

    readonly AssetRegistry assets;
    readonly Dictionary<string, SceneWidget> scenes = new Dictionary<string, SceneWidget>();
    readonly Camera camera;
    readonly GameObject cameraObject;
    readonly int layer = 28;
    bool disposed;

    public NativeHubWidgets(AssetRegistry assets)
    {
        this.assets = assets;
        cameraObject = new GameObject("NativeHubWidgetCamera");
        camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.cullingMask = 1 << layer;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 10000;
    }

    public bool DrawWidget(Rect rect, string widgetId)
    {
        if (disposed || rect.width <= 0 || rect.height <= 0 || String.IsNullOrEmpty(widgetId)) return false;
        if (widgetId == "modal.bulkBuy.frame")
        {
            if (Event.current.type == EventType.Repaint) return DrawBulkBuyFrame(rect);
            return HasBulkBuyTextures();
        }

        string textureId;
        if (TryGetTextureId(widgetId, out textureId))
        {
            var texture = assets.Texture(textureId);
            if (texture == null) return false;
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            return true;
        }

        string sceneId;
        string[] nodePath;
        if (!TryGetSceneSource(widgetId, out sceneId, out nodePath)) return false;
        var scene = GetScene(sceneId, nodePath, widgetId);
        if (scene == null) return false;
        if (Event.current.type == EventType.Repaint)
        {
            float aspect = rect.width / rect.height;
            int height = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(256, rect.height)), 128, 1024);
            if (!Render(scene, aspect, height)) return false;
            Rect uv = scene.trimContent ? scene.contentUv : new Rect(0, 0, 1, 1);
            if (widgetId == "chrome.friends")
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            if (widgetId == "collection.card.count")
                uv = new Rect(uv.x, uv.yMax, uv.width, -uv.height);
            if (widgetId == "collection.detail.related")
                uv = new Rect(uv.xMax, uv.yMax, -uv.width, -uv.height);
            GUI.DrawTextureWithTexCoords(rect, scene.texture, uv, true);
            if (widgetId == "social.flyout")
            {
                Rect[] actions;
                if (!TryGetSocialFlyoutLayout(rect, out actions)) return false;
                foreach (var action in actions)
                    if (!DrawWidget(action, "menu.button")) return false;
            }
        }
        return true;
    }

    // Source bounds are normalized against the complete native modal frame rect.
    public bool TryGetBulkBuyLayout(Rect widgetRect, out Rect previewRect,
        out Rect descriptionRect, out Rect quantityRect)
    {
        previewRect = descriptionRect = quantityRect = default(Rect);
        if (disposed || widgetRect.width <= 0 || widgetRect.height <= 0) return false;
        previewRect = RelativeRect(widgetRect, .052f, .071f, .897f, .624f);
        descriptionRect = RelativeRect(widgetRect, .040f, .706f, .676f, .269f);
        quantityRect = RelativeRect(widgetRect, .718f, .718f, .245f, .259f);
        return true;
    }

    bool HasBulkBuyTextures()
    {
        return assets.Texture("HUB_TEXTURE_SHOP_DETAIL_FRAME") != null &&
            assets.Texture("HUB_TEXTURE_SHOP_DETAIL_BUNDLE_FRAME") != null &&
            assets.Texture("HUB_TEXTURE_SHOP_BACKING_PLATE") != null;
    }

    bool DrawBulkBuyFrame(Rect rect)
    {
        var frame = assets.Texture("HUB_TEXTURE_SHOP_DETAIL_FRAME");
        var bundleFrame = assets.Texture("HUB_TEXTURE_SHOP_DETAIL_BUNDLE_FRAME");
        var backing = assets.Texture("HUB_TEXTURE_SHOP_BACKING_PLATE");
        if (frame == null || bundleFrame == null || backing == null) return false;

        DrawBulkBuyComposition(rect, frame, bundleFrame, backing, DrawGuiTexture);
        return true;
    }

    static Rect RelativeRect(Rect outer, float x, float y, float width, float height)
    {
        return new Rect(outer.x + outer.width * x, outer.y + outer.height * y,
            outer.width * width, outer.height * height);
    }

    static Rect AtlasUv(Texture2D texture, Rect sourcePixels)
    {
        return new Rect(sourcePixels.x / texture.width,
            1f - (sourcePixels.y + sourcePixels.height) / texture.height,
            sourcePixels.width / texture.width, sourcePixels.height / texture.height);
    }

    static void DrawBulkBuyComposition(Rect rect, Texture2D frame, Texture2D bundleFrame,
        Texture2D backing, Action<Rect, Texture2D, Rect> draw)
    {
        Rect preview = RelativeRect(rect, .052f, .071f, .897f, .624f);
        Rect description = RelativeRect(rect, .040f, .706f, .676f, .269f);
        Rect quantity = RelativeRect(rect, .718f, .718f, .245f, .259f);
        draw(rect, backing, new Rect(0, 0, 1, 1));
        DrawAtlasNineSlice(rect, frame, new Rect(30, 10, 980, 486), 42, 40, 36, 22, false, draw);
        draw(preview, backing, new Rect(0, 0, 1, 1));
        DrawAtlasNineSlice(description, frame, new Rect(72, 344, 650, 132), 24, 24, 14, 12, true, draw);
        DrawAtlasNineSlice(quantity, bundleFrame, new Rect(690, 152, 245, 130), 20, 20, 14, 12, true, draw);
    }

    static void DrawAtlasNineSlice(Rect destination, Texture2D texture, Rect sourcePixels,
        float left, float right, float top, float bottom, bool drawCenter,
        Action<Rect, Texture2D, Rect> draw)
    {
        float leftDst = destination.width * left / sourcePixels.width;
        float rightDst = destination.width * right / sourcePixels.width;
        float topDst = destination.height * top / sourcePixels.height;
        float bottomDst = destination.height * bottom / sourcePixels.height;
        if (leftDst + rightDst > destination.width)
        {
            float scale = destination.width / (leftDst + rightDst);
            leftDst *= scale;
            rightDst *= scale;
        }
        if (topDst + bottomDst > destination.height)
        {
            float scale = destination.height / (topDst + bottomDst);
            topDst *= scale;
            bottomDst *= scale;
        }

        float[] sx = { sourcePixels.x, sourcePixels.x + left,
            sourcePixels.xMax - right, sourcePixels.xMax };
        float[] sy = { sourcePixels.y, sourcePixels.y + top,
            sourcePixels.yMax - bottom, sourcePixels.yMax };
        float[] dx = { destination.x, destination.x + leftDst,
            destination.xMax - rightDst, destination.xMax };
        float[] dy = { destination.y, destination.y + topDst,
            destination.yMax - bottomDst, destination.yMax };

        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
        {
            if (!drawCenter && row == 1 && column == 1) continue;
            var target = Rect.MinMaxRect(dx[column], dy[row], dx[column + 1], dy[row + 1]);
            var source = Rect.MinMaxRect(sx[column], sy[row], sx[column + 1], sy[row + 1]);
            if (target.width <= 0 || target.height <= 0 || source.width <= 0 || source.height <= 0) continue;
            draw(target, texture, AtlasUv(texture, source));
        }
    }

    static void DrawGuiTexture(Rect destination, Texture2D texture, Rect sourceUv)
    {
        GUI.DrawTextureWithTexCoords(destination, texture, sourceUv, true);
    }

    // Output order is Add, RAF, Remove, Privacy; source transforms overflow the frame in this export.
    public bool TryGetSocialFlyoutLayout(Rect widgetRect, out Rect[] actionRects)
    {
        actionRects = new Rect[0];
        if (disposed || widgetRect.width <= 0 || widgetRect.height <= 0) return false;
        const float x = .11f, width = .78f, top = .09f, height = .17f, gap = .035f;
        actionRects = new Rect[4];
        for (int i = 0; i < actionRects.Length; i++)
            actionRects[i] = RelativeRect(widgetRect, x, top + i * (height + gap), width, height);
        return true;
    }

#if UNITY_EDITOR
    public RenderTexture CaptureWidgetForDiagnostics(string widgetId, int width, int height, out string report)
    {
        report = String.Empty;
        if (disposed || width <= 0 || height <= 0)
        {
            report = widgetId + ": invalid capture size or disposed renderer";
            return null;
        }

        if (widgetId == "modal.bulkBuy.frame")
            return CaptureBulkBuyFrameForDiagnostics(width, height, out report);
        if (widgetId == "social.flyout")
            return CaptureSocialFlyoutForDiagnostics(width, height, out report);

        string textureId;
        if (TryGetTextureId(widgetId, out textureId))
        {
            var source = assets.Texture(textureId);
            if (source == null)
            {
                report = widgetId + ": texture unavailable: " + textureId;
                return null;
            }
            report = DescribeFile("texture", assets.PathFor(textureId));
            return CopyToRenderTexture(source, width, height, widgetId);
        }

        string sceneId;
        string[] nodePath;
        if (!TryGetSceneSource(widgetId, out sceneId, out nodePath))
        {
            report = widgetId + ": no native source mapping";
            return null;
        }
        var scene = GetScene(sceneId, nodePath, widgetId);
        if (scene == null)
        {
            report = widgetId + ": scene source missing or has no active renderer: " + sceneId;
            return null;
        }
        if (!Render(scene, width / (float)height, height))
        {
            report = widgetId + ": source scene could not render: " + sceneId;
            return null;
        }

        var crop = scene.trimContent ? scene.contentUv : new Rect(0, 0, 1, 1);
        var output = CopyToRenderTexture(scene.texture, width, height, widgetId,
            widgetId == "chrome.friends", crop);
        var sourcePath = assets.PathFor(sceneId);
        var data = String.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)
            ? null
            : JsonUtility.FromJson<SceneData>(File.ReadAllText(sourcePath));
        var lines = new List<string>
        {
            "widget=" + widgetId,
            "scene=" + sceneId,
            "visible_uv=" + scene.contentUv,
            DescribeFile("scene", sourcePath),
            "active_renderers=" + scene.renderers.Count(renderer => renderer != null && renderer.enabled),
            "node_path=" + (nodePath == null ? "<scene root>" : String.Join("/", nodePath))
        };
        var textureFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!String.IsNullOrEmpty(scene.materialAdapter))
            lines.Add("material_adapter=" + scene.materialAdapter);
        foreach (var node in data == null ? new SceneNode[0] : data.nodes)
        {
            if (String.IsNullOrEmpty(node.mesh)) continue;
            foreach (var material in node.materials ?? new SceneMaterial[0])
            {
                lines.Add(String.Format("material={0} mesh={1} shader={2} tint={3:F3},{4:F3},{5:F3},{6:F3} texture={7}",
                    node.name, node.mesh, material.shader, material.tint.r, material.tint.g,
                    material.tint.b, material.tint.a, material.texture));
                if (String.IsNullOrEmpty(material.texture)) continue;
                string texturePath = Path.Combine(assets.Root, material.texture);
                if (textureFiles.Add(texturePath)) lines.Add(DescribeFile("material_texture", texturePath));
            }
        }
        report = String.Join("\n", lines.ToArray());
        return output;
    }

    RenderTexture CaptureBulkBuyFrameForDiagnostics(int width, int height, out string report)
    {
        if (!HasBulkBuyTextures())
        {
            report = "modal.bulkBuy.frame: source textures are unavailable";
            return null;
        }

        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "F026WidgetCapture_modal.bulkBuy.frame",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        if (!target.Create())
        {
            ReleaseTexture(target);
            report = "modal.bulkBuy.frame: render texture could not be created";
            return null;
        }

        var previous = RenderTexture.active;
        GL.PushMatrix();
        try
        {
            RenderTexture.active = target;
            GL.LoadPixelMatrix(0, width, height, 0);
            GL.Clear(false, true, new Color(0, 0, 0, 0));
            var rect = new Rect(0, 0, width, height);
            var frame = assets.Texture("HUB_TEXTURE_SHOP_DETAIL_FRAME");
            var bundleFrame = assets.Texture("HUB_TEXTURE_SHOP_DETAIL_BUNDLE_FRAME");
            var backing = assets.Texture("HUB_TEXTURE_SHOP_BACKING_PLATE");
            DrawBulkBuyComposition(rect, frame, bundleFrame, backing, DrawTextureToRenderTarget);
        }
        catch
        {
            ReleaseTexture(target);
            report = "modal.bulkBuy.frame: source composition could not render";
            return null;
        }
        finally
        {
            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        report = String.Join("\n", new[]
        {
            "widget=modal.bulkBuy.frame",
            "render=source texture composition; nine-slice borders retain source edges",
            "layout=preview .052,.071,.897,.624; description .040,.706,.676,.269; quantity .718,.718,.245,.259",
            DescribeFile("outer_frame", assets.PathFor("HUB_TEXTURE_SHOP_DETAIL_FRAME")),
            DescribeFile("quantity_frame", assets.PathFor("HUB_TEXTURE_SHOP_DETAIL_BUNDLE_FRAME")),
            DescribeFile("purple_backing", assets.PathFor("HUB_TEXTURE_SHOP_BACKING_PLATE")),
            "outer_frame_uv=" + AtlasUv(assets.Texture("HUB_TEXTURE_SHOP_DETAIL_FRAME"), new Rect(30, 10, 980, 486)),
            "paper_uv=" + AtlasUv(assets.Texture("HUB_TEXTURE_SHOP_DETAIL_FRAME"), new Rect(72, 344, 650, 132)),
            "quantity_uv=" + AtlasUv(assets.Texture("HUB_TEXTURE_SHOP_DETAIL_BUNDLE_FRAME"), new Rect(690, 152, 245, 130))
        });
        return target;
    }

    RenderTexture CaptureSocialFlyoutForDiagnostics(int width, int height, out string report)
    {
        string sceneId;
        string[] nodePath;
        if (!TryGetSceneSource("social.flyout", out sceneId, out nodePath))
        {
            report = "social.flyout: no native frame source mapping";
            return null;
        }
        var frame = GetScene(sceneId, nodePath, "social.flyout");
        if (frame == null || !Render(frame, width / (float)height, height))
        {
            report = "social.flyout: native frame could not render";
            return null;
        }

        Rect[] actions;
        if (!TryGetSocialFlyoutLayout(new Rect(0, 0, width, height), out actions))
        {
            report = "social.flyout: derived button layout unavailable";
            return null;
        }
        string buttonSceneId;
        string[] buttonPath;
        if (!TryGetSceneSource("menu.button", out buttonSceneId, out buttonPath))
        {
            report = "social.flyout: gold button source mapping unavailable";
            return null;
        }
        var button = GetScene(buttonSceneId, buttonPath, "menu.button");
        int buttonHeight = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(256, actions[0].height)), 128, 1024);
        if (button == null || !Render(button, actions[0].width / actions[0].height, buttonHeight))
        {
            report = "social.flyout: gold button source could not render";
            return null;
        }

        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "F026WidgetCapture_social.flyout",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        if (!target.Create())
        {
            ReleaseTexture(target);
            report = "social.flyout: render texture could not be created";
            return null;
        }

        var previous = RenderTexture.active;
        Rect buttonUv = button.trimContent ? button.contentUv : new Rect(0, 0, 1, 1);
        GL.PushMatrix();
        try
        {
            Graphics.Blit(frame.texture, target,
                new Vector2(frame.contentUv.width, frame.contentUv.height),
                new Vector2(frame.contentUv.xMin, frame.contentUv.yMin));
            RenderTexture.active = target;
            GL.LoadPixelMatrix(0, width, height, 0);
            foreach (var action in actions)
                Graphics.DrawTexture(action, button.texture, buttonUv, 0, 0, 0, 0);
        }
        catch
        {
            ReleaseTexture(target);
            report = "social.flyout: source composition could not render";
            return null;
        }
        finally
        {
            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        report = String.Join("\n", new[]
        {
            "widget=social.flyout",
            "frame_node=" + String.Join("/", nodePath),
            "button_source=" + buttonSceneId + ":" + String.Join("/", buttonPath),
            "button_layout=derived equal slots ordered Add, RAF, Remove, Privacy; source FlyoutMenu transforms overflow its panel in the exported initial state",
            "frame_visible_uv=" + frame.contentUv,
            "button_visible_uv=" + buttonUv,
            "buttons=" + String.Join(";", Array.ConvertAll(actions, rect => rect.ToString()))
        });
        return target;
    }

    static void DrawTextureToRenderTarget(Rect destination, Texture2D texture, Rect sourceUv)
    {
        Graphics.DrawTexture(destination, texture, sourceUv, 0, 0, 0, 0);
    }

    static RenderTexture CopyToRenderTexture(Texture source, int width, int height, string name,
        bool flipX = false, Rect? sourceUv = null)
    {
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "F026WidgetCapture_" + name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        if (!target.Create())
        {
            AssetRegistry.ReleaseTransient(target);
            return null;
        }
        var previous = RenderTexture.active;
        try
        {
            Rect uv = sourceUv ?? new Rect(0, 0, 1, 1);
            Vector2 scale = new Vector2((flipX ? -1 : 1) * uv.width, uv.height);
            Vector2 offset = new Vector2(flipX ? uv.xMax : uv.xMin, uv.yMin);
            Graphics.Blit(source, target, scale, offset);
        }
        finally { RenderTexture.active = previous; }
        return target;
    }

    static string DescribeFile(string label, string path)
    {
        if (String.IsNullOrEmpty(path) || !File.Exists(path)) return label + "=<missing>";
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return String.Format("{0}={1} sha256={2}", label, Path.GetFileName(path),
                BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", String.Empty));
    }
#endif

    static bool TryGetTextureId(string widgetId, out string textureId)
    {
        switch (widgetId)
        {
            case "chrome.gold": textureId = "UI_GOLD"; return true;
            case "chrome.dust": textureId = "UI_DUST"; return true;
            default: textureId = null; return false;
        }
    }

    static bool TryGetSceneSource(string widgetId, out string sceneId, out string[] nodePath)
    {
        nodePath = null;
        switch (widgetId)
        {
            case "chrome.coinbar":
            case "chrome.coinbar.currency": sceneId = "HUB_WIDGET_CURRENCY_BACKGROUND"; return true;
            case "chrome.coinbar.bnet": sceneId = "HUB_WIDGET_BNET_BAR"; return true;
            case "chrome.coinbar.legacy":
                sceneId = "SCENE_MAIN_MENU";
                nodePath = new[] { "TheBox", "RootObject", "BnetBarBg" };
                return true;
            case "chrome.gear": sceneId = "HUB_WIDGET_GAME_MENU_OPTION"; return true;
            case "menu.panel": sceneId = "HUB_WIDGET_GAME_MENU"; return true;
            case "menu.header":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "Header", "slimTitleHeader" };
                return true;
            case "menu.button":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "DropDownButton", "Button" };
                return true;
            case "options.panel":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "OptionFrame" };
                return true;
            case "chrome.friends": sceneId = "HUB_WIDGET_FRIENDS_BUTTON"; return true;
            case "social.panel": sceneId = "HUB_WIDGET_FRIENDS_FRAME"; return true;
            case "social.panel.frame": sceneId = "HUB_WIDGET_FRIENDS_FRAME"; return true;
            case "social.action":
                sceneId = "HUB_WIDGET_FRIENDS_FRAME";
                nodePath = new[] { "FriendListFrame", "FriendListFrame_Root", "FlyoutMenu" };
                return true;
            case "social.flyout":
                sceneId = "HUB_WIDGET_FRIENDS_FRAME";
                nodePath = new[] { "FriendListFrame", "FriendListFrame_Root", "FlyoutMenu", "Frame" };
                return true;
            case "modal.bulkBuy.candidate":
                sceneId = "HUB_WIDGET_SHOP_DETAILS_BOOSTER";
                return true;
            case "quests.reroll":
                sceneId = "SCENE_QUEST_TILE_DAILY";
                nodePath = new[] { "QuestTile", "Root", "RerollButton" };
                return true;
            case "collection.search.background":
                sceneId = "SCENE_COLLECTION_MANAGER";
                nodePath = new[] { "CollectionManager", "BookBack", "Search", "SearchBackground", "Background_Top_Normal" };
                return true;
            case "collection.mana.background":
                sceneId = "SCENE_COLLECTION_MANAGER";
                nodePath = new[] { "CollectionManager", "BookBack", "ManaTabManager", "GemBackground" };
                return true;
            case "collection.mana.cell":
                sceneId = "HUB_WIDGET_COLLECTION_MANA_TABS";
                nodePath = new[] { "ManaTabManager", "ManaCrystalContainer", "ManaSortSingle(Clone)" };
                return true;
            case "collection.set.button":
            case "collection.set.button.hover":
                sceneId = "HUB_WIDGET_COLLECTION_SET_FILTER";
                nodePath = new[] { "SetFilterTray", "FilterButton" };
                return true;
            case "collection.detail.related":
                sceneId = "HUB_WIDGET_COLLECTION_DETAIL_RELATED";
                nodePath = new[] { "RelatedCardsTray", "RelatedCardsContainer", "RelatedCardTray_Model" };
                return true;
            case "collection.detail.info":
                sceneId = "HUB_WIDGET_COLLECTION_DETAIL_INFO";
                nodePath = new[] { "CardInfoPane", "CardExchange_Info_mesh" };
                return true;
            case "collection.detail.crafting":
                sceneId = "HUB_WIDGET_COLLECTION_DETAIL_CRAFTING";
                nodePath = new[] { "CraftingUI" };
                return true;
            case "collection.card.count":
                sceneId = "HUB_WIDGET_COLLECTION_CARD_COUNT";
                nodePath = new[] { "CollectionCardCount", "Normal", "PageCountBorder" };
                return true;
            case "collection.search.clear":
                sceneId = "SCENE_COLLECTION_MANAGER";
                nodePath = new[] { "CollectionManager", "BookBack", "Search", "Mulligan_X_Mark", "Quad_mesh" };
                return true;
            case "collection.set.dropdown.arrow":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "DropDownButton", "Button", "DropDownArrow" };
                return true;
            case "collection.set.dropdown.panel.top":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "Menu", "DropdownTop" };
                return true;
            case "collection.set.dropdown.panel.middle":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "Menu", "DropdownMiddle" };
                return true;
            case "collection.set.dropdown.panel.bottom":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "Menu", "DropdownBottom" };
                return true;
            case "collection.set.dropdown.selected":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "Menu", "ItemContainer", "MenuItemTemplate", "DropdownSelected" };
                return true;
            case "collection.set.dropdown.highlight":
                sceneId = "HUB_WIDGET_OPTIONS_MENU";
                nodePath = new[] { "OptionsMenu", "WindowContents", "LeftPane", "GraphicsGroup",
                    "ResolutionDropDown", "Menu", "ItemContainer", "MenuItemTemplate", "DropdownHighlight" };
                return true;
            default: sceneId = null; return false;
        }
    }

    static bool IsWidgetRenderer(string widgetId, Renderer renderer, Transform selectedRoot)
    {
        if (widgetId == "collection.mana.cell") return renderer.transform == selectedRoot;
        if (widgetId == "collection.search.background")
            return renderer.gameObject.name == "Background_Top_Normal";
        if (widgetId == "collection.search.clear")
            return renderer.gameObject.name == "Quad_mesh";
        if (widgetId == "collection.detail.info")
            return renderer.gameObject.name == "CardExchange_Info_mesh";
        if (widgetId == "collection.detail.crafting")
        {
            string path = RendererPath(renderer.transform, selectedRoot);
            return path == "CraftingUI" ||
                path == "CraftingUI/Button_Create/CardXchange_ButtonMultiFront_mesh" ||
                path == "CraftingUI/Button_Create/CreateCostBar/CardXchange_RedBar_Mesh" ||
                path == "CraftingUI/Button_Create/CreateCostBar/DustJar_Icon_Create" ||
                path == "CraftingUI/Button_Disenchant/CardXchange_ButtonMultiFront_mesh" ||
                path == "CraftingUI/Button_Disenchant/DisenchantCostBar/CardXchange_GreenBar_Mesh" ||
                path == "CraftingUI/Button_Disenchant/DisenchantCostBar/DustJar_Icon_Disenchant" ||
                path == "CraftingUI/Lightning_Missile_01/CardXchange_Gem_mesh";
        }
        if (widgetId == "social.panel.frame") return IsSocialFrameRenderer(renderer);
        if (widgetId == "menu.button")
        {
            string name = renderer.gameObject.name;
            return name == "ButtonBack_Left_mesh" || name == "ButtonBack_Center_mesh" ||
                name == "ButtonBack_Right_mesh";
        }
        return true;
    }

    static string RendererPath(Transform renderer, Transform root)
    {
        var names = new List<string>();
        for (Transform current = renderer; current != null; current = current.parent)
        {
            names.Add(current.name);
            if (current == root)
            {
                names.Reverse();
                return String.Join("/", names.ToArray());
            }
        }
        return String.Empty;
    }

    SceneWidget GetScene(string sceneId, string[] nodePath, string widgetId)
    {
        string key = sceneId + (nodePath == null ? "" : ":" + String.Join("/", nodePath)) +
            (widgetId == "social.panel.frame" ? ":social_frame" : "") +
            (widgetId == "collection.set.button.hover" ? ":hover" : "");
        SceneWidget cached;
        if (scenes.TryGetValue(key, out cached)) return cached;

        string path = assets.PathFor(sceneId);
        if (String.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var root = assets.Scene(sceneId);
        if (root == null || root.name.EndsWith("_Missing", StringComparison.Ordinal))
        {
            AssetRegistry.ReleaseTransient(root);
            return null;
        }

        Transform selected = root.transform;
        if (nodePath != null)
        {
            for (int i = 0; i < nodePath.Length && selected != null; i++)
                selected = FindDirectChild(selected, nodePath[i]);
            if (selected == null)
            {
                ReleaseScene(root, null);
                return null;
            }
        }

        // Selected sources can live below a hidden popup or flyout root.
        for (var current = selected; current != null; current = current.parent)
            current.gameObject.SetActive(true);

        if (widgetId == "collection.set.button.hover")
        {
            var highlight = FindDirectChild(selected, "SortButtonHighlight");
            if (highlight != null) highlight.gameObject.SetActive(true);
        }

        var allRenderers = root.GetComponentsInChildren<Renderer>(true);
        var selectedRenderers = selected.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                IsWidgetRenderer(widgetId, renderer, selected)).ToArray();
        if (selectedRenderers.Length == 0)
        {
            ReleaseScene(root, CollectMaterials(allRenderers));
            return null;
        }

        foreach (var renderer in allRenderers)
            if (!IsWithin(renderer.transform, selected) ||
                !IsWidgetRenderer(widgetId, renderer, selected)) renderer.enabled = false;
        SetLayer(root.transform, layer);
        var materials = CollectMaterials(allRenderers);
        var clonedTextures = new List<Texture2D>();
        string materialAdapter = ConfigureCollectionMaterialAdapter(widgetId, selectedRenderers, clonedTextures);
        root.SetActive(false);
        var result = new SceneWidget
        {
            root = root,
            renderers = selectedRenderers,
            materials = materials,
            clonedTextures = clonedTextures.ToArray(),
            materialAdapter = materialAdapter,
            trimContent = ShouldTrimContent(widgetId)
        };
        scenes.Add(key, result);
        return result;
    }

    static bool ShouldTrimContent(string widgetId)
    {
        return widgetId == "chrome.friends" || widgetId == "options.panel" ||
            widgetId == "social.panel.frame" || widgetId == "social.action" ||
            widgetId == "social.flyout" || widgetId == "modal.bulkBuy.candidate" ||
            widgetId == "menu.header" || widgetId == "menu.button" || widgetId == "quests.reroll" ||
            widgetId == "collection.search.background" || widgetId == "collection.search.clear" ||
            widgetId == "collection.mana.background" || widgetId == "collection.mana.cell" || widgetId == "collection.card.count" ||
            widgetId == "collection.set.button" || widgetId == "collection.set.button.hover" ||
            widgetId.StartsWith("collection.set.dropdown.", StringComparison.Ordinal) ||
            widgetId == "collection.detail.related" || widgetId == "collection.detail.info";
    }

    string ConfigureCollectionMaterialAdapter(string widgetId, Renderer[] renderers,
        List<Texture2D> clonedTextures)
    {
        if (widgetId == "collection.set.button" || widgetId == "collection.set.button.hover")
        {
            int adapted = ConfigureTransparentBlend(renderers, "FilterMode_Icons_FilterWindow", null, true, false);
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.name == "FilterMode_Icons_FilterWindow")
                {
                    material.mainTextureScale = new Vector2(.25f, .25f);
                    material.mainTextureOffset = new Vector2(.75f, .5f);
                }
                if (widgetId == "collection.set.button.hover" && material.name == "UIElements_glow")
                {
                    if (material.HasProperty("_Effect")) material.SetFloat("_Effect", 0f);
                    if (material.HasProperty("_Tint")) material.SetColor("_Tint", new Color(.6691f, .4389f, .1476f, 1f));
                }
            }
            return adapted > 0 ? "Set filter book icon and local glow use the source atlas and tint; blend is approximated." :
                "Set filter icon alpha adapter could not find its local material.";
        }

        if (widgetId == "collection.search.background")
        {
            bool adapted = false;
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.name != "UIElements_Alt") continue;
                var source = material.mainTexture as Texture2D;
                if (source == null) continue;
                var repeatTexture = UnityEngine.Object.Instantiate(source);
                repeatTexture.name = source.name + "_NativeHubRepeat";
                repeatTexture.wrapMode = TextureWrapMode.Repeat;
                material.mainTexture = repeatTexture;
                clonedTextures.Add(repeatTexture);
                adapted = true;
            }
            return adapted
                ? "UIElements_Alt texture cloned locally with source Repeat wrapping; material offset is preserved."
                : "UIElements_Alt Repeat adapter could not find its local texture.";
        }

        if (widgetId == "collection.search.clear")
        {
            int adapted = ConfigureTransparentBlend(renderers, "UI_Search_X", null, true, false);
            return adapted > 0
                ? "UI_Search_X source alpha restored for Hero/Unlit_Transparent."
                : "UI_Search_X alpha adapter could not find its local material.";
        }

        if (widgetId == "collection.card.count")
        {
            bool filled = FillCollectionCountMask(renderers, clonedTextures);
            int adapted = ConfigureTransparentBlend(renderers, "CardCounts",
                Color.white, true, true);
            return adapted > 0
                ? (filled ? "Count border alpha encloses an opaque RGB fill; outside the border remains transparent. " :
                    "Count fill mask unavailable; retaining the source outline alpha. ") +
                    "Neutral-tint approximation; original shader is Particle Multiply."
                : "Normal/PageCountBorder CardCounts adapter could not find its local material.";
        }

        if (widgetId == "collection.detail.info")
        {
            int adjusted = 0;
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.name == "CardExchange_Info" && material.HasProperty("_Tint"))
                { material.SetColor("_Tint", Color.white); adjusted++; }
            return adjusted > 0 ? "Static neutral-tint compatibility for CardExchange_Info." : "CardExchange_Info material missing.";
        }

        if (widgetId == "collection.detail.crafting")
        {
            var atlas = assets.Texture("HUB_TEXTURE_COLLECTION_DETAIL_CRAFTING_ATLAS");
            var dustIcon = assets.Texture("HUB_TEXTURE_COLLECTION_DETAIL_DUST_JAR_ICON");
            if (atlas == null || dustIcon == null)
                return "CraftingUI atlas or dust icon texture is unavailable.";

            var repeatAtlas = UnityEngine.Object.Instantiate(atlas);
            repeatAtlas.name = atlas.name + "_CraftingRepeat";
            repeatAtlas.wrapModeU = TextureWrapMode.Repeat;
            repeatAtlas.wrapModeV = TextureWrapMode.Repeat;
            repeatAtlas.wrapModeW = TextureWrapMode.Repeat;
            clonedTextures.Add(repeatAtlas);

            var repeatDustIcon = UnityEngine.Object.Instantiate(dustIcon);
            repeatDustIcon.name = dustIcon.name + "_CraftingRepeat";
            repeatDustIcon.wrapModeU = TextureWrapMode.Repeat;
            repeatDustIcon.wrapModeV = TextureWrapMode.Repeat;
            repeatDustIcon.wrapModeW = TextureWrapMode.Repeat;
            clonedTextures.Add(repeatDustIcon);

            int atlasMaterials = 0;
            int iconMaterials = 0;
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.name == "CardExchange_base" ||
                    material.name == "CardExchange_ButtonCreate" ||
                    material.name == "CardExchange_ButtonDis" ||
                    material.name == "CardExchange_gem")
                {
                    material.mainTexture = repeatAtlas;
                    atlasMaterials++;
                }
                else if (material.name == "DustJarIcon")
                {
                    material.mainTexture = repeatDustIcon;
                    iconMaterials++;
                }
            }

            return "CraftingUI textures cloned with source Repeat wrapping; material UV scale and offset remain unchanged. " +
                "CardExchange materials=" + atlasMaterials + ", DustJarIcon materials=" + iconMaterials + ".";
        }

        return null;
    }

    static bool FillCollectionCountMask(Renderer[] renderers, List<Texture2D> clonedTextures)
    {
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var uv = filter.sharedMesh.uv;
            if (uv == null || uv.Length == 0) continue;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.name != "CardCounts") continue;
                var source = material.mainTexture as Texture2D;
                if (source == null || !source.isReadable) continue;
                var copy = UnityEngine.Object.Instantiate(source);
                var pixels = copy.GetPixels32();
                int x0 = Mathf.Clamp(Mathf.FloorToInt(uv.Min(p => p.x) * copy.width), 0, copy.width - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(uv.Max(p => p.x) * copy.width), 0, copy.width - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(uv.Min(p => p.y) * copy.height), 0, copy.height - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(uv.Max(p => p.y) * copy.height), 0, copy.height - 1);
                int filledRows = 0;
                for (int y = y0; y <= y1; y++)
                {
                    int left = x0, right = x1;
                    while (left <= right && pixels[y * copy.width + left].a == 0) left++;
                    while (right >= left && pixels[y * copy.width + right].a == 0) right--;
                    if (right - left < 2) continue;
                    for (int x = left + 1; x < right; x++)
                        pixels[y * copy.width + x].a = 255;
                    filledRows++;
                }
                if (filledRows == 0) { UnityEngine.Object.Destroy(copy); continue; }
                copy.name = source.name + "_CollectionCountFilled";
                copy.SetPixels32(pixels);
                copy.Apply(false, false);
                material.mainTexture = copy;
                clonedTextures.Add(copy);
                return true;
            }
        }
        return false;
    }

    static int ConfigureTransparentBlend(Renderer[] renderers, string materialName, Color? tint,
        bool useTextureAlpha, bool disableMultiply)
    {
        int adapted = 0;
        foreach (var renderer in renderers)
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || material.name != materialName || !material.HasProperty("_UseAlpha")) continue;
            material.SetFloat("_UseAlpha", useTextureAlpha ? 1f : 0f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", 5);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", 10);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Multiply")) material.SetFloat("_Multiply", disableMultiply ? 0f : material.GetFloat("_Multiply"));
            if (material.HasProperty("_Effect")) material.SetFloat("_Effect", 0f);
            if (material.HasProperty("_VertexTint")) material.SetFloat("_VertexTint", 0f);
            if (tint.HasValue && material.HasProperty("_Tint")) material.SetColor("_Tint", tint.Value);
            material.renderQueue = 3000;
            adapted++;
        }
        return adapted;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    static bool IsWithin(Transform child, Transform ancestor)
    {
        for (Transform current = child; current != null; current = current.parent)
            if (current == ancestor) return true;
        return false;
    }

    static bool IsSocialFrameRenderer(Renderer renderer)
    {
        string name = renderer.gameObject.name;
        return name == "FriendsListNew_Base_TopLeft_mesh" ||
            name == "FriendsListNew_Base_TopRight_mesh" ||
            name == "FriendsListNew_Base_Center_mesh" ||
            name == "FriendsListNew_Base_bottom_mesh" ||
            name == "FriendsListNew_DropShadow";
    }

    static Material[] CollectMaterials(Renderer[] renderers)
    {
        return renderers.Where(renderer => renderer != null)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null).Distinct().ToArray();
    }

    bool Render(SceneWidget scene, float aspect, int height)
    {
        if (scene.texture != null && Mathf.Abs(scene.aspect - aspect) <= .01f && scene.height == height)
            return true;
        ReleaseTexture(scene.texture);
        int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 128, 2048);
        scene.texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "NativeHubWidget_" + scene.root.name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        if (!scene.texture.Create())
        {
            ReleaseTexture(scene.texture);
            scene.texture = null;
            return false;
        }
        scene.aspect = aspect;
        scene.height = height;

        bool wasActive = scene.root.activeSelf;
        scene.root.SetActive(true);
        try
        {
            var renderers = scene.renderers.Where(renderer => renderer != null && renderer.enabled &&
                renderer.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) return false;
            Bounds bounds = BoundsFor(renderers);
            Frame(bounds, aspect);
            scene.contentUv = scene.trimContent ? ProjectBoundsToViewport(bounds) : new Rect(0, 0, 1, 1);
            camera.targetTexture = scene.texture;
            camera.cullingMask = 1 << layer;
            camera.Render();
            return true;
        }
        finally
        {
            camera.targetTexture = null;
            scene.root.SetActive(wasActive);
        }
    }

    void Frame(Bounds bounds, float aspect)
    {
        Vector3 size = bounds.size;
        Vector3 normal;
        Vector3 up;
        if (size.y <= size.x && size.y <= size.z)
        {
            normal = Vector3.up;
            up = Vector3.forward;
        }
        else if (size.x <= size.z)
        {
            normal = Vector3.right;
            up = Vector3.up;
        }
        else
        {
            normal = Vector3.forward;
            up = Vector3.up;
        }

        Quaternion rotation = Quaternion.LookRotation(-normal, up);
        Vector3 right = rotation * Vector3.right;
        Vector3 vertical = rotation * Vector3.up;
        float halfWidth = 0, halfHeight = 0, halfDepth = 0;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1,
                (corner & 2) == 0 ? -1 : 1,
                (corner & 4) == 0 ? -1 : 1));
            Vector3 delta = point - bounds.center;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(delta, right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(delta, vertical)));
            halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(delta, normal)));
        }

        float distance = Mathf.Max(2, bounds.extents.magnitude * 3 + 1);
        camera.aspect = aspect;
        camera.orthographicSize = Mathf.Max(.01f, Mathf.Max(halfHeight, halfWidth / Mathf.Max(aspect, .01f)) * 1.04f);
        camera.transform.SetPositionAndRotation(bounds.center + normal * distance, rotation);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = distance + halfDepth + 10;
    }

    static Bounds BoundsFor(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    Rect ProjectBoundsToViewport(Bounds bounds)
    {
        Vector2 min = new Vector2(Single.PositiveInfinity, Single.PositiveInfinity);
        Vector2 max = new Vector2(Single.NegativeInfinity, Single.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1,
                (corner & 2) == 0 ? -1 : 1,
                (corner & 4) == 0 ? -1 : 1));
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0) return new Rect(0, 0, 1, 1);
            min = Vector2.Min(min, new Vector2(viewport.x, viewport.y));
            max = Vector2.Max(max, new Vector2(viewport.x, viewport.y));
        }
        var projected = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        if (projected.width <= 0 || projected.height <= 0) return new Rect(0, 0, 1, 1);
        projected.xMin = Mathf.Clamp01(projected.xMin);
        projected.yMin = Mathf.Clamp01(projected.yMin);
        projected.xMax = Mathf.Clamp01(projected.xMax);
        projected.yMax = Mathf.Clamp01(projected.yMax);
        return projected.width > 0 && projected.height > 0 ? projected : new Rect(0, 0, 1, 1);
    }

    static void SetLayer(Transform root, int targetLayer)
    {
        root.gameObject.layer = targetLayer;
        for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), targetLayer);
    }

    static void ReleaseTexture(RenderTexture texture)
    {
        AssetRegistry.ReleaseTransient(texture);
    }

    static void ReleaseScene(GameObject root, Material[] materials)
    {
        AssetRegistry.ReleaseTransient(root);
        foreach (var material in materials ?? new Material[0]) AssetRegistry.ReleaseTransient(material);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var scene in scenes.Values)
        {
            ReleaseTexture(scene.texture);
            ReleaseScene(scene.root, scene.materials);
            foreach (var texture in scene.clonedTextures ?? new Texture2D[0])
                AssetRegistry.ReleaseTransient(texture);
        }
        scenes.Clear();
        AssetRegistry.ReleaseTransient(cameraObject);
    }
}
