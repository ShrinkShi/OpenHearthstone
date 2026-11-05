using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Page prefabs have no serialized page camera. This uses an explicit bounds-fit camera for the exported meshes.
public sealed class NativeHubScene : IDisposable
{
    [Serializable] sealed class PackVisualCatalog { public int schema_version; public PackVisualEntry[] entries; }
    [Serializable] sealed class PackVisualEntry
    {
        public int set_id, source_set_id;
        public string model_id, ui_id, source_type, presentation_kind, source_asset_id;
        public string primary_node_name, camera_suggestion, front_axis, up_axis, front_axis_note, scene_file;
        public string[] primary_node_path;
        public bool include_primary_mesh_only, exported;
        public string[] excluded_node_names;
    }

    sealed class Page
    {
        public GameObject root;
        public Material[] materials;
        public Transform[] frameRoots;
        public RenderTexture texture;
        public RenderTexture croppedCaptureTexture;
        public float aspect;
        public bool rendered;
        public Bounds frameBounds;
        public int frameRendererCount;
        public bool frameBoundsFrozen;
        public float framePaddingScale = 1.08f;
        public Vector3 cameraFocusOffset;
        public Rect displayRect;
        public bool hasDisplayRect;
        public Transform collectionBookBack;
        public Transform collectionDeckTray;
        public Vector3 collectionBookBackSourcePosition, collectionBookBackSourceScale;
        public Vector3 collectionDeckTraySourcePosition, collectionDeckTraySourceScale;
        public float collectionCompositionAspect = -1f;
        public readonly List<Mesh> generatedMeshes = new List<Mesh>();
        public Transform[] shopFrameRoots;
        public Vector3[] shopFrameSourcePositions, shopFrameSourceScales;
        public float shopCompositionAspect = -1f;
        public Transform collectionPageManager;
        public Transform collectionDeckContentsBackground;
        public Transform collectionFrameBackground;
        public Vector3 collectionPageManagerSourcePosition, collectionPageManagerSourceScale;
        public Vector3 collectionDeckContentsSourcePosition, collectionDeckContentsSourceScale;
        public Rect collectionCompositionRect;
        public bool collectionDesignCamera;
        public bool collectionSourceCamera;
        public Vector3 collectionSourceCameraPosition;
        public Quaternion collectionSourceCameraRotation;
        public float collectionSourceCameraFov;
        public float collectionSourceCameraNear, collectionSourceCameraFar;
        public bool cropToVisibleBounds;
        public Rect visibleUv = new Rect(0, 0, 1, 1);
        public readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        public int packTrayAtlasRepeatMaterialCount;
        public int packSideNeutralMaterialCount, packTrayNeutralMaterialCount, packDiskNeutralMaterialCount;
    }

    sealed class PackPreview
    {
        public GameObject root;
        public Material[] materials;
        public Renderer[] renderers;
        public RenderTexture texture;
        public float aspect;
        public float presentationAspect;
        public Bounds frameBounds;
        public int frameRendererCount;
    }

    readonly AssetRegistry assets;
    readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
    readonly Dictionary<int, PackPreview> packPreviews = new Dictionary<int, PackPreview>();
    readonly Dictionary<string, PackPreview> questTilePreviews = new Dictionary<string, PackPreview>();
    readonly Dictionary<bool, PackPreview> buttonPreviews = new Dictionary<bool, PackPreview>();
    readonly Camera camera;
    readonly GameObject cameraObject;
    readonly int layer = 31;
    readonly int packLayer = 29;
    string activeScene = "";
    PackVisualCatalog packVisualCatalog;
    bool disposed;

    public NativeHubScene(AssetRegistry assets)
    {
        this.assets = assets;
        cameraObject = new GameObject("NativeHubBoundsCamera");
        camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.cullingMask = 1 << layer;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 10000;
    }

    public void Update(string page)
    {
        ActivateScene(SceneIdForPage(page));
    }

    void ActivateScene(string sceneId)
    {
        bool changed = sceneId != activeScene;
        activeScene = sceneId;
        foreach (var entry in pages)
        {
            bool shouldBeActive = entry.Key == activeScene;
            if (entry.Value.root != null && entry.Value.root.activeSelf != shouldBeActive)
                entry.Value.root.SetActive(shouldBeActive);
            if (changed && shouldBeActive) entry.Value.rendered = false;
        }
    }

    public string SceneIdForPage(string page)
    {
        if (page == "collection" && !String.IsNullOrEmpty(assets.PathFor("SCENE_COLLECTION_PAGE")) &&
            File.Exists(assets.PathFor("SCENE_COLLECTION_PAGE"))) return "SCENE_COLLECTION_PAGE";
        return SceneForPage(page);
    }

    public bool Draw(Rect rect, string sceneId)
    {
        if (disposed || String.IsNullOrEmpty(sceneId)) return false;
        // GUI events can change pages after Update; honor the explicit scene on this same repaint.
        ActivateScene(sceneId);
        var page = GetPage(sceneId);
        if (page == null) return false;
        page.displayRect = rect;
        page.hasDisplayRect = true;
        float aspect = rect.height <= 0 ? 1 : rect.width / rect.height;
        if (sceneId == "SCENE_COLLECTION_PAGE") ConfigureCollectionComposition(page, aspect);
        else if (sceneId == "SCENE_MAIN_SHOP_PC") ConfigureShopComposition(page, aspect);
        if (Event.current.type == EventType.Repaint)
        {
            var texture = RenderPage(page, aspect, 900);
            if (sceneId == "SCENE_PACK_OPENING")
                GUI.DrawTextureWithTexCoords(rect, texture, page.visibleUv, true);
            else
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }
        return true;
    }

#if UNITY_EDITOR
    public RenderTexture CapturePageForDiagnostics(string sceneId, int width, int height, out string report)
    {
        var page = GetPage(sceneId);
        if (page == null || width <= 0 || height <= 0)
        {
            report = sceneId + ": missing source scene or invalid capture size";
            return null;
        }

        bool wasActive = page.root.activeSelf;
        page.root.SetActive(true);
        page.rendered = false;
        try
        {
            page.displayRect = new Rect(0, 0, width, height);
            page.hasDisplayRect = true;
            if (sceneId == "SCENE_COLLECTION_PAGE") ConfigureCollectionComposition(page, width / (float)height);
            else if (sceneId == "SCENE_MAIN_SHOP_PC") ConfigureShopComposition(page, width / (float)height);
            RenderPage(page, width / (float)height, height);
            var renderers = page.root.GetComponentsInChildren<Renderer>(true)
                .Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
            report = String.Format(
                "{0}\nframeRoots={1}\nframeRenderers={2}\nactiveRenderers={3}\nbounds center={4} extents={5}\ncamera position={6} orthographicSize={7:F4} aspect={8:F4}\n{9}",
                sceneId,
                String.Join(", ", (page.frameRoots ?? new Transform[0]).Where(item => item != null).Select(item => item.name).ToArray()),
                page.frameRendererCount,
                renderers.Length,
                page.frameBounds.center,
                page.frameBounds.extents,
                camera.transform.position,
                camera.orthographicSize,
                camera.aspect,
                DescribeRenderers(renderers));
            if (sceneId == "SCENE_PACK_OPENING")
                report += "\nvisible_uv=" + page.visibleUv + " (camera-projected source frame bounds, bottom-left texture coordinates; Draw and layout share this crop).";
            if (sceneId == "SCENE_PACK_OPENING")
            {
                report += "\nframe_exclusion=PackOpening/Socket is omitted from static frame bounds to avoid the oversized deck-socket FX; scene renderers remain active.";
                report += String.Format(
                    "\nstatic_appearance_reconstruction=neutral _Tint on exact source groups (side={0}, tray={1}, disk={2}); these are static values, not recovered lighting/SH.",
                    page.packSideNeutralMaterialCount, page.packTrayNeutralMaterialCount, page.packDiskNeutralMaterialCount)
                    + String.Format("\ntexture_wrap=local Repeat clone for 036aea0e60b4dc3c881e7932.png on {0} material slots; restored from source Texture2D c344939515b83d62d4b9f349 (initial_base_global-8411aad9-texture-13.unity3d, CAB-51315388aaeaca768a550d02d88ba4c3, pathID -6581863386966491286), m_TextureSettings U/V/W=0 (Unity Repeat). Static-neutral lighting remains an approximation.", page.packTrayAtlasRepeatMaterialCount);
            }
            return page.cropToVisibleBounds ? RenderVisibleCapture(page) : page.texture;
        }
        finally { page.root.SetActive(wasActive); }
    }

    public RenderTexture CapturePackOpeningVariantForDiagnostics(string variant, int width, int height, out string report)
    {
        report = "SCENE_PACK_OPENING: unsupported diagnostic variant " + variant;
        if (variant != "shadow-off" && variant != "disk-off" && variant != "tray-off" && variant != "self-illumin-2x" &&
            variant != "self-illumin-neutral") return null;

        var page = GetPage("SCENE_PACK_OPENING");
        if (page == null || width <= 0 || height <= 0)
        {
            report = "SCENE_PACK_OPENING: missing source scene or invalid capture size";
            return null;
        }

        if (!page.hasDisplayRect || !page.rendered)
        {
            string baselineReport;
            var baselineTexture = CapturePageForDiagnostics("SCENE_PACK_OPENING", width, height, out baselineReport);
            if (baselineTexture == null)
            {
                report = baselineReport;
                return null;
            }
            if (baselineTexture != page.texture) ReleaseTexture(baselineTexture);
        }

        bool wasActive = page.root.activeSelf;
        bool oldBoundsFrozen = page.frameBoundsFrozen;
        string diagnosticNode = variant == "disk-off" ? "PackOpeningTray_Disk" :
            variant == "tray-off" ? "PackOpeningTray" : "PackOpeningTray_fakeShadow";
        Transform shadow = FindNodePath(page.root.transform, new[]
        {
            "PackOpening", "SocketTray", diagnosticNode
        });
        bool shadowWasActive = shadow != null && shadow.gameObject.activeSelf;
        var tintRestore = new Dictionary<Material, Color>();
        string sourceMatchSummary = "n/a";
        page.root.SetActive(true);
        try
        {
            // Keep the baseline composition fixed so each capture changes only the named layer/materials.
            page.frameBoundsFrozen = true;
            if (variant == "shadow-off" || variant == "disk-off" || variant == "tray-off")
            {
                if (shadow == null)
                {
                    report = "SCENE_PACK_OPENING: exact PackOpeningTray_fakeShadow node not found";
                    return null;
                }
                shadow.gameObject.SetActive(false);
            }
            else
            {
                int sideCount, trayCount, diskCount;
                if (variant == "self-illumin-neutral")
                {
                    sideCount = NeutralizeSourceTint(page.root.transform,
                        new[] { "PackOpening", "PackTray", "SidePackOpeningTray" },
                        "SidePackOpeningTray", tintRestore);
                    trayCount = NeutralizeSourceTint(page.root.transform,
                        new[] { "PackOpening", "SocketTray", "PackOpeningTray" },
                        "PackOpeningTray", tintRestore);
                    diskCount = NeutralizeSourceTint(page.root.transform,
                        new[] { "PackOpening", "SocketTray", "PackOpeningTray_Disk" },
                        "PackOpeningTray_Disk", tintRestore);
                }
                else
                {
                    sideCount = ScaleSourceTint(page.root.transform,
                        new[] { "PackOpening", "PackTray", "SidePackOpeningTray" },
                        "SidePackOpeningTray", 2f, tintRestore);
                    trayCount = ScaleSourceTint(page.root.transform,
                        new[] { "PackOpening", "SocketTray", "PackOpeningTray" },
                        "PackOpeningTray", 2f, tintRestore);
                    diskCount = ScaleSourceTint(page.root.transform,
                        new[] { "PackOpening", "SocketTray", "PackOpeningTray_Disk" },
                        "PackOpeningTray_Disk", 2f, tintRestore);
                }
                sourceMatchSummary = String.Format("SidePackOpeningTray={0},PackOpeningTray={1},PackOpeningTray_Disk={2}",
                    sideCount, trayCount, diskCount);
                if (sideCount == 0 || trayCount == 0 || diskCount == 0)
                {
                    report = String.Format(
                        "SCENE_PACK_OPENING: exact source material missing ({0})", sourceMatchSummary);
                    return null;
                }
            }

            string captureReport;
            var texture = CapturePageForDiagnostics("SCENE_PACK_OPENING", width, height, out captureReport);
            report = captureReport + "\nvariant=" + variant + "\nchanged_materials=" + tintRestore.Count +
                "\nmatched_source_material_slots=" + sourceMatchSummary +
                "\nshadow_active=" + (shadow != null && shadow.gameObject.activeInHierarchy);
            return texture;
        }
        finally
        {
            if (shadow != null) shadow.gameObject.SetActive(shadowWasActive);
            foreach (var pair in tintRestore) pair.Key.SetColor("_Tint", pair.Value);
            page.frameBoundsFrozen = oldBoundsFrozen;
            page.root.SetActive(wasActive);
            page.rendered = false;
        }
    }

    static int ScaleSourceTint(Transform sceneRoot, string[] nodePath, string materialName,
        float multiplier, Dictionary<Material, Color> restore)
    {
        var node = FindNodePath(sceneRoot, nodePath);
        int matched = 0;
        foreach (var renderer in ActiveRenderers(node))
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.name != materialName || !material.HasProperty("_Tint")) continue;
                if (!restore.ContainsKey(material)) restore.Add(material, material.GetColor("_Tint"));
                Color tint = material.GetColor("_Tint");
                material.SetColor("_Tint", new Color(tint.r * multiplier, tint.g * multiplier,
                    tint.b * multiplier, tint.a));
                matched++;
            }
        }
        return matched;
    }

    static int NeutralizeSourceTint(Transform sceneRoot, string[] nodePath, string materialName,
        Dictionary<Material, Color> restore)
    {
        var node = FindNodePath(sceneRoot, nodePath);
        int matched = 0;
        foreach (var renderer in ActiveRenderers(node))
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.name != materialName || !material.HasProperty("_Tint")) continue;
                if (!restore.ContainsKey(material)) restore.Add(material, material.GetColor("_Tint"));
                Color tint = material.GetColor("_Tint");
                material.SetColor("_Tint", new Color(1, 1, 1, tint.a));
                matched++;
            }
        }
        return matched;
    }

    static string DescribeRenderers(Renderer[] renderers)
    {
        var report = new StringBuilder();
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;
            var materials = renderer.sharedMaterials.Select(material =>
            {
                if (material == null) return "null";
                var texture = material.mainTexture;
                string shader = material.shader == null ? "no shader" : material.shader.name;
                return texture == null ? material.name + "[" + shader + "](no texture)" :
                    material.name + "[" + shader + "](" + texture.name + " " + texture.width + "x" + texture.height + ")";
            }).ToArray();
            string uvBounds = "none";
            if (mesh != null && mesh.uv != null && mesh.uv.Length > 0)
            {
                var uv = mesh.uv;
                Vector2 min = uv[0], max = uv[0];
                for (int i = 1; i < uv.Length; i++)
                {
                    min = Vector2.Min(min, uv[i]);
                    max = Vector2.Max(max, uv[i]);
                }
                uvBounds = String.Format("{0:F3},{1:F3}..{2:F3},{3:F3}", min.x, min.y, max.x, max.y);
            }
            report.AppendFormat(
                "renderer={0} vertices={1} uv={2} submeshes={3} materialSlots={4} uvBounds={5} materials={6}\n",
                TransformPath(renderer.transform),
                mesh == null ? 0 : mesh.vertexCount,
                mesh == null ? 0 : mesh.uv.Length,
                mesh == null ? 0 : mesh.subMeshCount,
                materials.Length,
                uvBounds,
                String.Join(", ", materials));
        }
        return report.ToString();
    }

    static string TransformPath(Transform transform)
    {
        var parts = new List<string>();
        for (var current = transform; current != null; current = current.parent) parts.Add(current.name);
        parts.Reverse();
        return String.Join("/", parts.ToArray());
    }

    public RenderTexture CaptureQuestTileForDiagnostics(bool weekly, int width, int height, out string report)
    {
        if (width <= 0 || height <= 0)
        {
            report = "invalid quest-tile capture size";
            return null;
        }
        var preview = GetQuestTilePreview(weekly);
        if (preview == null)
        {
            report = weekly ? "SCENE_QUEST_TILE_WEEKLY: unavailable" : "SCENE_QUEST_TILE_DAILY: unavailable";
            return null;
        }
        string sceneId = weekly ? "SCENE_QUEST_TILE_WEEKLY" : "SCENE_QUEST_TILE_DAILY";
        RenderPreview(preview, width / (float)height, "", "unknown", height);
        report = String.Format(
            "{0}\nactiveRenderers={1}\nbounds center={2} extents={3}\ncamera position={4} orthographicSize={5:F4} aspect={6:F4}\n{7}",
            sceneId,
            preview.frameRendererCount,
            preview.frameBounds.center,
            preview.frameBounds.extents,
            camera.transform.position,
            camera.orthographicSize,
            camera.aspect,
            DescribeRenderers(preview.renderers.Where(item => item.enabled).ToArray()));
        return preview.texture;
    }
#endif

    RenderTexture RenderPage(Page page, float aspect, int height)
    {
        if (page.texture == null || Mathf.Abs(page.aspect - aspect) > .01f || page.texture.height != height)
        {
            ReleaseTexture(page);
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 512, 2048);
            page.texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "NativeHub_" + page.root.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            page.texture.Create();
            page.aspect = aspect;
            page.rendered = false;
        }
        if (!page.rendered)
        {
            Frame(page, aspect);
            page.visibleUv = page.cropToVisibleBounds
                ? VisibleFrameUv(page)
                : new Rect(0, 0, 1, 1);
            camera.targetTexture = page.texture;
            camera.cullingMask = 1 << layer;
            try { camera.Render(); }
            finally { camera.targetTexture = null; }
            page.rendered = true;
        }
        return page.texture;
    }

    public bool DrawPackPreview(Rect rect, int setId)
    {
        if (disposed || setId < 0 || rect.width <= 0 || rect.height <= 0) return false;
        var preview = GetPackPreview(setId);
        if (preview == null) return false;
        return DrawPreview(rect, preview, 2f / 3f, GetPackSuggestion(setId), GetPackFrontAxis(setId), GetPackUpAxis(setId));
    }

    // 返回最近一次 Draw 设计坐标中的原生纸页与托盘投影边界。
    public bool TryGetCollectionLayout(out Rect paperRect, out Rect deckTrayRect)
    {
        paperRect = default(Rect);
        deckTrayRect = default(Rect);
        var page = GetPage("SCENE_COLLECTION_PAGE");
        if (page == null || !page.hasDisplayRect || page.displayRect.width <= 0 || page.displayRect.height <= 0)
            return false;

        // 预览按钮也会改写相机，因此投影前重新套用收藏页相机。
        Frame(page, page.displayRect.width / page.displayRect.height);
        var paper = FindNodePath(page.root.transform, new[]
        {
            "CollectionManager", "CollectionPageManager", "SCENE_COLLECTION_PAGE", "CollectionPage", "BasePage"
        });
        var tray = FindNodePath(page.root.transform, new[]
        {
            "CollectionManager", "CollectionDeckTray", "DeckTrayArea"
        });
        return TryProjectRect(paper, page.displayRect, out paperRect) &&
            TryProjectRect(tray, page.displayRect, out deckTrayRect);
    }

    // 返回托盘、圆形凹槽与返回按钮的裁后投影；它们不是源输入碰撞框。
    public bool TryGetPackOpeningLayout(out Rect trayRect, out Rect socketRect, out Rect backRect)
    {
        trayRect = default(Rect);
        socketRect = default(Rect);
        backRect = default(Rect);
        var page = GetPage("SCENE_PACK_OPENING");
        if (page == null || !page.hasDisplayRect || page.displayRect.width <= 0 || page.displayRect.height <= 0)
            return false;

        bool wasActive = page.root.activeSelf;
        page.root.SetActive(true);
        try
        {
            // 包预览会复用相机，先恢复当前开包页的 bounds 相机。
            Frame(page, page.displayRect.width / page.displayRect.height);
            page.visibleUv = VisibleFrameUv(page);
            var root = page.root.transform;
            var tray = FindNodePath(root, new[] { "PackOpening", "PackTray" });
            var socket = FindNodePath(root, new[] { "PackOpening", "SocketTray", "PackOpeningTray_Disk" });
            var back = FindNodePath(root, new[] { "PackOpening", "BackButton" });
            Rect projectedTray, projectedSocket, projectedBack;
            if (!TryProjectGroupRect(tray, page.displayRect, out projectedTray) ||
                !TryProjectGroupRect(socket, page.displayRect, out projectedSocket) ||
                !TryProjectGroupRect(back, page.displayRect, out projectedBack)) return false;
            trayRect = MapToVisibleRect(projectedTray, page.displayRect, page.visibleUv);
            trayRect = Rect.MinMaxRect(Mathf.Max(trayRect.xMin, page.displayRect.xMin),
                Mathf.Max(trayRect.yMin, page.displayRect.yMin),
                Mathf.Min(trayRect.xMax, page.displayRect.xMax),
                Mathf.Min(trayRect.yMax, page.displayRect.yMax));
            socketRect = MapToVisibleRect(projectedSocket, page.displayRect, page.visibleUv);
            backRect = MapToVisibleRect(projectedBack, page.displayRect, page.visibleUv);
            return true;
        }
        finally { page.root.SetActive(wasActive); }
    }

    bool TryProjectRect(Transform root, Rect displayRect, out Rect projectedRect)
    {
        projectedRect = default(Rect);
        if (root == null) return false;
        var renderer = root.GetComponent<Renderer>();
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;

        return TryProjectBounds(renderer.bounds, displayRect, out projectedRect);
    }

    public bool TryGetPackOpeningHeader(out Rect rect)
    {
        rect = default(Rect);
        Rect tray, socket, back;
        if (!TryGetPackOpeningLayout(out tray, out socket, out back)) return false;
        var page = GetPage("SCENE_PACK_OPENING");
        var header = FindNodePath(page.root.transform, new[] { "PackOpening", "SocketTray", "UberText_HeaderText" });
        if (header == null) return false;
        Vector3 viewport = camera.WorldToViewportPoint(header.position);
        if (viewport.z <= 0) return false;
        var projected = new Rect(page.displayRect.x + viewport.x * page.displayRect.width,
            page.displayRect.y + (1 - viewport.y) * page.displayRect.height, 0, 0);
        var anchor = MapToVisibleRect(projected, page.displayRect, page.visibleUv).position;
        // 使用源文字锚点；缺失的 UberText 排版由本地字体绘制。
        float width = Mathf.Min(360, socket.width * .65f);
        rect = new Rect(anchor.x - width * .5f, anchor.y - 24, width, 48);
        return true;
    }

    bool TryProjectGroupRect(Transform root, Rect displayRect, out Rect projectedRect)
    {
        projectedRect = default(Rect);
        var renderers = ActiveRenderers(root);
        return renderers.Length > 0 && TryProjectBounds(BoundsFor(renderers), displayRect, out projectedRect);
    }

    bool TryProjectBounds(Bounds bounds, Rect displayRect, out Rect projectedRect)
    {
        projectedRect = default(Rect);
        float minX = Single.PositiveInfinity, minY = Single.PositiveInfinity;
        float maxX = Single.NegativeInfinity, maxY = Single.NegativeInfinity;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1,
                (corner & 2) == 0 ? -1 : 1,
                (corner & 4) == 0 ? -1 : 1));
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0) continue;
            minX = Mathf.Min(minX, viewport.x);
            maxX = Mathf.Max(maxX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxY = Mathf.Max(maxY, viewport.y);
        }
        if (Single.IsInfinity(minX) || Single.IsInfinity(minY)) return false;
        projectedRect = new Rect(
            displayRect.x + minX * displayRect.width,
            displayRect.y + (1 - maxY) * displayRect.height,
            (maxX - minX) * displayRect.width,
            (maxY - minY) * displayRect.height);
        return projectedRect.width > 0 && projectedRect.height > 0;
    }

    public bool DrawQuestTile(Rect rect, bool weekly)
    {
        if (disposed || rect.width <= 0 || rect.height <= 0) return false;
        var preview = GetQuestTilePreview(weekly);
        if (preview == null) return false;
        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(preview, preview.presentationAspect, "", "unknown", 512);
            GUI.DrawTexture(rect, preview.texture, ScaleMode.StretchToFill, true);
        }
        return true;
    }

    public bool DrawButton(Rect rect, bool hovered, bool pressed)
    {
        if (disposed || rect.width <= 0 || rect.height <= 0) return false;
        var preview = GetButtonPreview(hovered || pressed);
        if (preview == null) return false;
        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(preview, preview.presentationAspect, "", "+Y", 512);
            GUI.DrawTexture(rect, preview.texture, ScaleMode.StretchToFill, true);
        }
        return true;
    }

    bool DrawPreview(Rect rect, PackPreview preview, float aspect, string cameraSuggestion, string frontAxis, string upAxis)
    {
        if (Event.current.type == EventType.Repaint)
        {
            int height = Mathf.Min(512, Mathf.Max(128, Mathf.FloorToInt(2048f / Mathf.Max(aspect, .01f))));
            RenderPreview(preview, aspect, cameraSuggestion, frontAxis, height, upAxis);
            GUI.DrawTexture(rect, preview.texture, ScaleMode.ScaleToFit, true);
        }
        return true;
    }

    void RenderPreview(PackPreview preview, float aspect, string cameraSuggestion, string frontAxis, int height, string upAxis = "")
    {
        if (preview.texture != null && Mathf.Abs(preview.aspect - aspect) <= .01f && preview.texture.height == height) return;
        ReleaseTexture(preview.texture);
        int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 128, 2048);
        preview.texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "NativePreview_" + preview.root.name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        preview.texture.Create();
        preview.aspect = aspect;
        bool wasActive = preview.root.activeSelf;
        preview.root.SetActive(true);
        try
        {
            FramePreview(preview, aspect, cameraSuggestion, frontAxis, upAxis);
            camera.targetTexture = preview.texture;
            camera.cullingMask = 1 << packLayer;
            camera.Render();
        }
        finally
        {
            camera.targetTexture = null;
            camera.cullingMask = 1 << layer;
            preview.root.SetActive(wasActive);
        }
    }

    PackPreview GetQuestTilePreview(bool weekly)
    {
        string sceneId = weekly ? "SCENE_QUEST_TILE_WEEKLY" : "SCENE_QUEST_TILE_DAILY";
        PackPreview preview;
        if (questTilePreviews.TryGetValue(sceneId, out preview)) return preview;
        string assetPath = assets.PathFor(sceneId);
        if (String.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;
        var root = assets.Scene(sceneId);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (!renderers.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy))
        {
            ReleaseScene(root);
            return null;
        }
        foreach (var effectPath in new[]
        {
            new[] { "QuestTile", "FSM", "RerollFX", "BurnCardQuad" },
            new[] { "QuestTile", "FSM", "GrantFX", "GlowCardQuad" }
        })
        {
            var effect = FindNodePath(root.transform, effectPath);
            if (effect != null) effect.gameObject.SetActive(false);
        }
        var frameRenderers = renderers.Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        if (frameRenderers.Length == 0)
        {
            ReleaseScene(root);
            return null;
        }
        Bounds frameBounds = frameRenderers[0].bounds;
        for (int i = 1; i < frameRenderers.Length; i++) frameBounds.Encapsulate(frameRenderers[i].bounds);
        // The export has no serialized quest camera; use its visible XZ face for a stable preview ratio.
        float presentationAspect = frameBounds.extents.x / Mathf.Max(frameBounds.extents.z, .001f);
        SetLayer(root.transform, packLayer);
        preview = new PackPreview
        {
            root = root,
            renderers = renderers,
            materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray(),
            presentationAspect = presentationAspect
        };
        preview.root.SetActive(false);
        questTilePreviews.Add(sceneId, preview);
        return preview;
    }

    PackPreview GetButtonPreview(bool highlighted)
    {
        PackPreview preview;
        if (buttonPreviews.TryGetValue(highlighted, out preview)) return preview;
        string assetPath = assets.PathFor("SCENE_COLLECTION_MANAGER");
        if (String.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;

        var source = assets.Scene("SCENE_COLLECTION_MANAGER");
        var sourceButton = FindNodePath(source.transform, new[]
        {
            "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "DoneButton"
        });
        if (sourceButton == null)
        {
            ReleaseScene(source);
            return null;
        }

        var root = new GameObject(highlighted ? "NativeButton_Hovered" : "NativeButton_Normal");
        var ownedMaterials = new List<Material>();
        try
        {
            var button = UnityEngine.Object.Instantiate(sourceButton.gameObject);
            button.name = "Button_Back_V3";
            button.transform.SetParent(root.transform, false);
            var text = FindNodePath(button.transform, new[] { "ButtonText" });
            if (text != null) text.gameObject.SetActive(false);
            var highlight = FindNodePath(button.transform, new[] { "Highlight_UI" });
            if (highlight != null) highlight.gameObject.SetActive(false);
            var glow = FindNodePath(button.transform, new[] { "Button", "ButtonBack_Glow_Mesh" });
            if (glow != null) glow.gameObject.SetActive(highlighted);

            var renderers = button.GetComponentsInChildren<Renderer>(true);
            var frameRenderers = renderers.Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
            if (frameRenderers.Length == 0) throw new InvalidOperationException("Native button clone has no active source renderers");
            Bounds bounds = frameRenderers[0].bounds;
            for (int i = 1; i < frameRenderers.Length; i++) bounds.Encapsulate(frameRenderers[i].bounds);
            float presentationAspect = bounds.extents.x / Mathf.Max(bounds.extents.z, .001f);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var material = new Material(materials[i]) { name = materials[i].name };
                    ownedMaterials.Add(material);
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
            SetLayer(root.transform, packLayer);
            preview = new PackPreview
            {
                root = root,
                renderers = renderers,
                materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray(),
                presentationAspect = presentationAspect
            };
            root.SetActive(false);
            buttonPreviews.Add(highlighted, preview);
            return preview;
        }
        catch
        {
            foreach (var material in ownedMaterials) AssetRegistry.ReleaseTransient(material);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
        finally { ReleaseScene(source); }
    }

    PackPreview GetPackPreview(int setId)
    {
        PackPreview cached;
        if (packPreviews.TryGetValue(setId, out cached)) return cached;
        var mapping = FindPackMapping(setId);
        if (mapping == null || !mapping.exported || String.IsNullOrEmpty(mapping.model_id) ||
            String.IsNullOrEmpty(assets.PathFor(mapping.model_id)) || !File.Exists(assets.PathFor(mapping.model_id))) return null;

        var root = assets.Scene(mapping.model_id);
        Transform primary = FindNodePath(root.transform, mapping.primary_node_path);
        if (primary == null && !String.IsNullOrEmpty(mapping.primary_node_name))
        {
            var candidates = root.GetComponentsInChildren<Transform>(true).Where(item => item.name == mapping.primary_node_name).ToArray();
            if (candidates.Length == 1) primary = candidates[0];
        }
        if (primary == null)
        {
            ReleaseScene(root);
            return null;
        }

        var excluded = new HashSet<string>(mapping.excluded_node_names ?? new string[0], StringComparer.Ordinal);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            bool withinPrimary = renderer.transform == primary || renderer.transform.IsChildOf(primary);
            bool explicitlyExcluded = false;
            for (Transform item = renderer.transform; withinPrimary && item != null && item != primary.parent; item = item.parent)
                if (excluded.Contains(item.name)) { explicitlyExcluded = true; break; }
            bool exactPrimary = renderer.transform == primary;
            renderer.enabled = withinPrimary && !explicitlyExcluded &&
                (!mapping.include_primary_mesh_only || exactPrimary);
        }
        if (!renderers.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy))
        {
            ReleaseScene(root);
            return null;
        }
        SetLayer(root.transform, packLayer);
        var preview = new PackPreview
        {
            root = root,
            renderers = renderers,
            materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray()
        };
        preview.root.SetActive(false);
        packPreviews.Add(setId, preview);
        return preview;
    }

    void FramePreview(PackPreview preview, float aspect, string suggestion, string frontAxis, string upAxis)
    {
        var renderers = preview.renderers.Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        preview.frameBounds = bounds;
        preview.frameRendererCount = renderers.Length;

        Vector3 extents = bounds.extents;
        Vector3 direction = Vector3.up, up = Vector3.forward;
        float faceX = extents.x, faceY = extents.z;
        if (frontAxis == "+Z") { direction = Vector3.forward; up = Vector3.up; faceX = extents.x; faceY = extents.y; }
        else if (frontAxis == "-Z") { direction = Vector3.back; up = Vector3.up; faceX = extents.x; faceY = extents.y; }
        else if (frontAxis == "+X") { direction = Vector3.right; up = Vector3.up; faceX = extents.z; faceY = extents.y; }
        else if (frontAxis == "-X") { direction = Vector3.left; up = Vector3.up; faceX = extents.z; faceY = extents.y; }
        else if (frontAxis == "-Y") { direction = Vector3.down; up = Vector3.forward; faceX = extents.x; faceY = extents.z; }
        else if (frontAxis == "+Y" || suggestion == "top_y") { direction = Vector3.up; up = Vector3.forward; faceX = extents.x; faceY = extents.z; }
        else if (suggestion == "front_z") { direction = Vector3.back; up = Vector3.up; faceX = extents.x; faceY = extents.y; }
        else if (suggestion == "side_x") { direction = Vector3.left; up = Vector3.up; faceX = extents.z; faceY = extents.y; }
        else
        {
            float xy = extents.x * extents.y;
            float xz = extents.x * extents.z;
            float yz = extents.y * extents.z;
            if (xy >= xz && xy >= yz) { direction = Vector3.back; up = Vector3.up; faceX = extents.x; faceY = extents.y; }
            else if (yz >= xy && yz >= xz) { direction = Vector3.left; up = Vector3.up; faceX = extents.z; faceY = extents.y; }
        }
        if (upAxis == "+X") up = Vector3.right;
        else if (upAxis == "-X") up = Vector3.left;
        else if (upAxis == "+Y") up = Vector3.up;
        else if (upAxis == "-Y") up = Vector3.down;
        else if (upAxis == "+Z") up = Vector3.forward;
        else if (upAxis == "-Z") up = Vector3.back;
        direction = Quaternion.AngleAxis(10f, up) * direction;
        float distance = Mathf.Max(4, extents.magnitude * 2 + 1);
        camera.aspect = aspect;
        camera.orthographicSize = Mathf.Max(.01f, Mathf.Max(faceY, faceX / Mathf.Max(aspect, .01f)) * 1.12f);
        camera.transform.SetPositionAndRotation(bounds.center + direction * distance, Quaternion.LookRotation(-direction, up));
        camera.nearClipPlane = .01f;
        camera.farClipPlane = distance + extents.magnitude * 2 + 10;
    }

    string GetPackSuggestion(int setId)
    {
        var item = FindPackMapping(setId);
        return item == null ? "" : item.camera_suggestion ?? "";
    }

    string GetPackFrontAxis(int setId)
    {
        var item = FindPackMapping(setId);
        if (item == null) return "unknown";
        if (!String.IsNullOrEmpty(item.front_axis) && item.front_axis != "unknown") return item.front_axis;
        return item.presentation_kind == "official_pack_model" ? "-Y" : "unknown";
    }

    string GetPackUpAxis(int setId)
    {
        var item = FindPackMapping(setId);
        if (item == null) return "";
        if (!String.IsNullOrEmpty(item.up_axis)) return item.up_axis;
        return item.presentation_kind == "official_pack_model" ? "-Z" : "";
    }

    public string PackPresentationNotice(int setId)
    {
        if (setId < 0) return "";
        var item = FindPackMapping(setId);
        if (item == null) return "原版展示资源尚未导出";
        if (item.presentation_kind == "adventure_book_model_derived") return "冒险主题书籍展示";
        if (item.presentation_kind == "themed_pack_model_derived") return "主题展示模型";
        if (item.presentation_kind == "class_pack_model_derived") return "职业主题展示模型";
        if (item.presentation_kind == "related_theme_book_derived") return "相关主题书籍展示";
        if (item.presentation_kind == "missing_original_asset") return "没有对应的原版展示模型";
        if (item.source_set_id > 0 && item.source_set_id != setId) return "原版经典包模型展示";
        return "";
    }

    PackVisualEntry FindPackMapping(int setId)
    {
        if (packVisualCatalog == null)
        {
            string path = Path.Combine(assets.Root, "pack-visuals.json");
            if (!File.Exists(path)) return null;
            packVisualCatalog = JsonUtility.FromJson<PackVisualCatalog>(File.ReadAllText(path));
        }
        return packVisualCatalog == null ? null : (packVisualCatalog.entries ?? new PackVisualEntry[0])
            .FirstOrDefault(item => item != null && item.set_id == setId);
    }

    static Transform FindNodePath(Transform root, string[] parts)
    {
        if (parts == null || parts.Length == 0) return null;
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true).Where(item => item.name == parts[0]))
        {
            Transform current = candidate;
            bool matched = true;
            for (int i = 1; i < parts.Length; i++)
            {
                current = Enumerable.Range(0, current.childCount).Select(current.GetChild).FirstOrDefault(item => item.name == parts[i]);
                if (current == null) { matched = false; break; }
            }
            if (matched) return current;
        }
        return null;
    }

    static void ReleaseScene(GameObject root)
    {
        if (root == null) return;
        foreach (var material in root.GetComponentsInChildren<Renderer>(true).SelectMany(item => item.sharedMaterials).Distinct())
            AssetRegistry.ReleaseTransient(material);
        AssetRegistry.ReleaseTransient(root);
    }

    Page GetPage(string id)
    {
        Page cached;
        if (pages.TryGetValue(id, out cached)) return cached;
        string assetPath = assets.PathFor(id);
        if (String.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;
        var page = new Page
        {
            root = new GameObject("NativeHub_" + id),
            cropToVisibleBounds = id == "SCENE_PACK_OPENING"
        };
        GameObject source = assets.Scene(id);
        source.transform.SetParent(page.root.transform, false);
        GameObject collectionManager = null;
        Transform collectionPageManager = null;
        Transform collectionBookBack = null;
        if (id == "SCENE_QUEST_LOG" &&
            !String.IsNullOrEmpty(assets.PathFor("SCENE_QUEST_LOG_CONTENT")) &&
            File.Exists(assets.PathFor("SCENE_QUEST_LOG_CONTENT")))
        {
            var content = assets.Scene("SCENE_QUEST_LOG_CONTENT");
            content.transform.SetParent(page.root.transform, false);
        }
        if (id == "SCENE_QUEST_LOG")
        {
            foreach (var entry in new[] { "DailySpecific", "WeeklySpecific" })
            {
                var specific = page.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == entry);
                if (specific != null) specific.gameObject.SetActive(true);
            }
            var eventSpecific = page.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "EventSpecific");
            if (eventSpecific != null) eventSpecific.gameObject.SetActive(false);
        }
        if (id == "SCENE_MAIN_SHOP_PC")
        {
            var shopRoot = new[] { "MainShopWidget", "BoxCalibrator", "ShopBox" };
            var phone = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_Phone" }).ToArray());
            if (phone != null) phone.gameObject.SetActive(false);
            var pc = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_PC" }).ToArray());
            if (pc != null) pc.gameObject.SetActive(true);
            var mask = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Contents", "PC_Mask" }).ToArray());
            if (mask != null) mask.gameObject.SetActive(true);
            // This source clipping plane has no image and renders as a white strip in the flat preview.
            var clippingTop = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_PC", "Clipping Top" }).ToArray());
            if (clippingTop != null) clippingTop.gameObject.SetActive(false);
            // 该Multiply贴图是旧3D落地框的整屏阴影，会把紫色平面右沿压成黑带。
            var landingBoxShadow = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_PC", "Shop_Box_3D", "Shop_Box_Shadow" }).ToArray());
            if (landingBoxShadow != null) landingBoxShadow.gameObject.SetActive(false);

            // 这些宽扩展在PC源状态启用，但越过此视口的真实边框。
            foreach (var path in new[]
            {
                new[] { "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Box_3D", "Shop_Texture_BoxExtension" },
                new[] { "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Box_3D", "Shop_Texture_BoxExtension (Flipped)" },
                new[] { "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Box_3D", "Shop_Texture_BoxExtensionEdge" },
                new[] { "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Box_3D", "BnetBarBg" }
            })
            {
                var outOfFrame = FindNodePath(page.root.transform, path);
                if (outOfFrame != null) outOfFrame.gameObject.SetActive(false);
            }
            var backing = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_PC", "Shop_Backing" }).ToArray());
            var border = FindNodePath(page.root.transform, shopRoot.Concat(new[] { "Framing", "Shop_Box_PC", "Shop_Box_Border" }).ToArray());
            RestoreShopSpriteUv(page, backing, "ShopBackingPlate", 858, 647);
            RestoreShopSpriteUv(page, border, "ShopFrame_PC", 1024, 731);
            foreach (var path in new[]
            {
                shopRoot.Concat(new[] { "Tabs" }).ToArray(),
                shopRoot.Concat(new[] { "Scroller" }).ToArray()
            })
            {
                var staticShopControl = FindNodePath(page.root.transform, path);
                if (staticShopControl != null) staticShopControl.gameObject.SetActive(false);
            }
            page.frameRoots = new[] { border, backing }.Where(item => item != null).ToArray();
            page.framePaddingScale = 1f;
            page.shopFrameRoots = page.frameRoots;
            page.shopFrameSourcePositions = page.shopFrameRoots.Select(item => item.localPosition).ToArray();
            page.shopFrameSourceScales = page.shopFrameRoots.Select(item => item.localScale).ToArray();
        }
        if (id == "SCENE_PACK_OPENING")
        {
            var hover = FindNodePath(page.root.transform, new[] { "PackOpening", "Hover" });
            if (hover != null) hover.gameObject.SetActive(false);
            // The Hub UI owns the shop CTA; the static scene has no pack list, pointer, or opening state.
            foreach (var path in new[]
            {
                new[] { "PackOpening", "PackTray", "UnopenedPackContainer", "StoreButton" },
                new[] { "PackOpening", "PackTray", "ScrollTrack", "ScrollThumb" },
                new[] { "PackOpening", "SocketAccent" },
                new[] { "PackOpening", "Socket" },
                new[] { "PackOpening", "SocketTray", "PackOpeningTray_fakeShadow" }
            })
            {
                var transient = FindNodePath(page.root.transform, path);
                if (transient != null) transient.gameObject.SetActive(false);
            }
            // 静态页排除动态发光与不支持的 Multiply 阴影，避免透明 RT 外出现灰色椭圆。
            page.frameRoots = new[]
            {
                FindNodePath(page.root.transform, new[] { "PackOpening", "PackTray" }),
                FindNodePath(page.root.transform, new[] { "PackOpening", "SocketTray", "PackOpeningTray" }),
                FindNodePath(page.root.transform, new[] { "PackOpening", "SocketAccent" }),
                FindNodePath(page.root.transform, new[] { "PackOpening", "BackButton" })
            }.Where(item => item != null).ToArray();
            ApplyPackOpeningStaticCompatibility(page);
        }
        if (id == "SCENE_COLLECTION_PAGE")
        {
            page.framePaddingScale = 1f;
            var stamp = page.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "TouristStamp");
            if (stamp != null) stamp.gameObject.SetActive(false);
            string managerPath = assets.PathFor("SCENE_COLLECTION_MANAGER");
            if (!String.IsNullOrEmpty(managerPath) && File.Exists(managerPath))
            {
                collectionManager = assets.Scene("SCENE_COLLECTION_MANAGER");
                collectionManager.transform.SetParent(page.root.transform, false);
                collectionPageManager = FindNodePath(page.root.transform, new[] { "CollectionManager", "CollectionPageManager" });
                collectionBookBack = FindNodePath(page.root.transform, new[] { "CollectionManager", "BookBack" });
                page.collectionBookBack = collectionBookBack;
                page.collectionPageManager = collectionPageManager;
                page.collectionDeckTray = FindNodePath(page.root.transform, new[] { "CollectionManager", "CollectionDeckTray" });
                page.collectionDeckContentsBackground = FindNodePath(page.root.transform, new[] { "CollectionManager", "CollectionDeckTray", "DeckContentsBackground" });
                // 原 Multiply 阴影的零色调在兼容着色器中会压成纯黑。
                var trayShadow = FindNodePath(page.root.transform, new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayInnerShadow" });
                if (trayShadow != null) trayShadow.gameObject.SetActive(false);
                if (collectionBookBack != null)
                {
                    page.collectionBookBackSourcePosition = collectionBookBack.localPosition;
                    page.collectionBookBackSourceScale = collectionBookBack.localScale;
                }
                if (collectionPageManager != null)
                {
                    page.collectionPageManagerSourcePosition = collectionPageManager.localPosition;
                    page.collectionPageManagerSourceScale = collectionPageManager.localScale;
                }
                if (page.collectionDeckTray != null)
                {
                    page.collectionDeckTraySourcePosition = page.collectionDeckTray.localPosition;
                    page.collectionDeckTraySourceScale = page.collectionDeckTray.localScale;
                }
                if (page.collectionDeckContentsBackground != null)
                {
                    page.collectionDeckContentsSourcePosition = page.collectionDeckContentsBackground.localPosition;
                    page.collectionDeckContentsSourceScale = page.collectionDeckContentsBackground.localScale;
                }
                page.collectionFrameBackground = CreateCollectionFrameBackground(page);
                foreach (var path in new[]
                {
                    new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "TrayContents", "CardContainer", "DeckCompleteButton", "DeckHelpButtonMesh", "Highlight_UI" },
                    new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "TrayContents", "CardContainer", "DeckTemplateHelpButton", "Highlight_UI" },
                    new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "TrayContents", "DeckContainer", "NewDeckButtonContainer", "Deck_Locator", "NewDeckButton", "Highlight_UI" },
                    new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "DoneButton", "Highlight_UI" }
                })
                {
                    var hover = FindNodePath(page.root.transform, path);
                    if (hover != null) hover.gameObject.SetActive(false);
                }
                var contentPage = source.transform;
                contentPage.SetParent(collectionPageManager != null ? collectionPageManager : page.root.transform, false);
                ConfigureCollectionStaticPageAppearance(page, contentPage);
                page.frameRoots = new[]
                {
                    collectionBookBack,
                    collectionPageManager,
                    FindNodePath(page.root.transform, new[] { "CollectionManager", "CollectionDeckTray" }),
                    FindNodePath(page.root.transform, new[] { "CollectionManager", "DesktopOnly" })
                }.Where(item => item != null).ToArray();
                HideStaticCollectionControls(page.root.transform, true);
                FreezeFrameBounds(page);
            }
            else
            {
                page.frameRoots = page.root.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "BasePage" || item.name == "ClassFlavorHeader").ToArray();
            }
        }
        if (id == "SCENE_COLLECTION_MANAGER")
        {
            page.frameRoots = new[] { page.root.transform };
            HideStaticCollectionControls(page.root.transform);
            FreezeFrameBounds(page);
        }
        if (id == "SCENE_QUEST_LOG")
        {
            Transform board = FindNodePath(page.root.transform, new[] { "QuestLog", "Root", "BoardMesh" });
            Transform content = page.root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "SCENE_QUEST_LOG_CONTENT");
            page.frameRoots = new[] { board, content }.Where(item => item != null).ToArray();
        }
        if (page.frameRoots == null || page.frameRoots.Length == 0) page.frameRoots = new[] { page.root.transform };
        SetLayer(page.root.transform, layer);
        page.materials = page.root.GetComponentsInChildren<Renderer>(true)
            .SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray();
        pages.Add(id, page);
        return page;
    }

    void ApplyPackOpeningStaticCompatibility(Page page)
    {
        var sourceTexture = assets.TextureFile("036aea0e60b4dc3c881e7932.png");
        if (sourceTexture != null)
        {
            var affected = page.root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null && material.mainTexture == sourceTexture)
                .Distinct().ToArray();
            page.packTrayAtlasRepeatMaterialCount = affected.Length;
            if (affected.Length > 0)
            {
                var repeatTexture = UnityEngine.Object.Instantiate(sourceTexture);
                repeatTexture.name = "NativePackOpening_TrayAtlas_Repeat";
                repeatTexture.wrapMode = TextureWrapMode.Repeat;
                page.ownedTextures.Add(repeatTexture);
                foreach (var material in affected) material.mainTexture = repeatTexture;
            }
        }

        page.packSideNeutralMaterialCount = SetPackOpeningNeutralTint(page.root.transform,
            new[] { "PackOpening", "PackTray", "SidePackOpeningTray" }, "SidePackOpeningTray");
        page.packTrayNeutralMaterialCount = SetPackOpeningNeutralTint(page.root.transform,
            new[] { "PackOpening", "SocketTray", "PackOpeningTray" }, "PackOpeningTray");
        page.packDiskNeutralMaterialCount = SetPackOpeningNeutralTint(page.root.transform,
            new[] { "PackOpening", "SocketTray", "PackOpeningTray_Disk" }, "PackOpeningTray_Disk");
    }

    static int SetPackOpeningNeutralTint(Transform sceneRoot, string[] nodePath, string materialName)
    {
        var node = FindNodePath(sceneRoot, nodePath);
        int matched = 0;
        foreach (var renderer in ActiveRenderers(node))
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.name != materialName || !material.HasProperty("_Tint")) continue;
                Color tint = material.GetColor("_Tint");
                material.SetColor("_Tint", new Color(1, 1, 1, tint.a));
                matched++;
            }
        }
        return matched;
    }

    static void RestoreShopSpriteUv(Page page, Transform sprite, string materialName, int textureWidth, int textureHeight)
    {
        if (sprite == null) return;
        var filter = sprite.GetComponent<MeshFilter>();
        var renderer = sprite.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null || renderer.sharedMaterials.Length != 1) return;

        var material = renderer.sharedMaterial;
        var texture = material == null ? null : material.mainTexture as Texture2D;
        var source = filter.sharedMesh;
        if (material == null || material.name != materialName || texture == null ||
            texture.width != textureWidth || texture.height != textureHeight || source == null) return;

        var sourceUv = source.uv;
        if (sourceUv.Length == source.vertexCount && sourceUv.Length > 0 &&
            sourceUv.Any(uv => (uv - sourceUv[0]).sqrMagnitude > .00000001f)) return;

        // 仅重建这两张完整裁切Sprite的UV；原导出mesh将所有顶点UV写成了(0,0)。
        Bounds bounds = source.bounds;
        if (bounds.size.x <= .001f || bounds.size.y <= .001f ||
            Mathf.Abs(bounds.size.x / bounds.size.y - textureWidth / (float)textureHeight) > .01f) return;

        var vertices = source.vertices;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            uv[i] = new Vector2(
                Mathf.InverseLerp(bounds.min.x, bounds.max.x, vertices[i].x),
                Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertices[i].y));

        var repaired = UnityEngine.Object.Instantiate(source);
        repaired.name = source.name + "_ShopSpriteUv";
        repaired.uv = uv;
        filter.sharedMesh = repaired;
        page.generatedMeshes.Add(repaired);
    }

    static void HideStaticCollectionControls(Transform root, bool hideObsoletePageControls = false)
    {
        if (hideObsoletePageControls)
        {
            foreach (var path in new[]
            {
                new[] { "CollectionManager", "CollectionPageManager", "ClassTabContainer" },
                new[] { "CollectionManager", "CollectionPageManager", "PageTurn" }
            })
            {
                var control = FindNodePath(root, path);
                if (control != null) control.gameObject.SetActive(false);
            }
            var bookBack = FindNodePath(root, new[] { "CollectionManager", "BookBack" });
            if (bookBack != null)
            {
                bookBack.gameObject.SetActive(true);
                foreach (Transform child in bookBack)
                    if (child.name != "BookBack_mesh") child.gameObject.SetActive(false);
            }
        }
        foreach (var path in new[]
        {
            new[] { "CollectionManager", "DesktopOnly", "CraftingModeButton" },
            new[] { "CollectionManager", "CollectionDeckTray", "DeckTrayArea", "DoneButton" }
        })
        {
            var control = FindNodePath(root, path);
            if (control != null) control.gameObject.SetActive(false);
        }
    }

    static void ConfigureCollectionStaticPageAppearance(Page page, Transform contentPage)
    {
        var classHeader = FindNodePath(contentPage, new[] { "CollectionPage", "ClassName", "ClassFlavorHeader" });
        if (classHeader != null) classHeader.gameObject.SetActive(false);

        var basePage = FindNodePath(contentPage, new[] { "CollectionPage", "BasePage" });
        var baseRenderer = basePage == null ? null : basePage.GetComponent<MeshRenderer>();
        var baseMaterial = baseRenderer == null ? null : baseRenderer.sharedMaterial;
        var pageTurnFront = FindNodePath(page.root.transform, new[]
        {
            "CollectionManager", "CollectionPageManager", "PageTurn", "PageTurnFront"
        });
        var frontRenderer = pageTurnFront == null ? null : pageTurnFront.GetComponent<MeshRenderer>();
        var frontMaterial = frontRenderer == null ? null : frontRenderer.sharedMaterial;
        if (baseMaterial == null || frontMaterial == null || baseMaterial.name != "PageA" ||
            frontMaterial.name != "PageA" || baseMaterial.mainTexture == null ||
            baseMaterial.mainTexture != frontMaterial.mainTexture ||
            !baseMaterial.HasProperty("_Tint") || !frontMaterial.HasProperty("_Tint"))
            return;

        Color sourceTint = baseMaterial.GetColor("_Tint");
        Color completedPageTint = frontMaterial.GetColor("_Tint");
        const float sourceBasePageTint = .5529412f;
        if (Mathf.Abs(sourceTint.r - sourceBasePageTint) > .001f ||
            Mathf.Abs(sourceTint.g - sourceBasePageTint) > .001f ||
            Mathf.Abs(sourceTint.b - sourceBasePageTint) > .001f ||
            completedPageTint.r < .99f || completedPageTint.g < .99f || completedPageTint.b < .99f)
            return;

        // 本地未移植Custom/BasePage着色器；静态纸面仅沿用PageTurnFront的PageA白色tint。
        baseMaterial.SetColor("_Tint", completedPageTint);
    }

    Transform CreateCollectionFrameBackground(Page page)
    {
        const string sceneId = "SCENE_MAIN_MENU";
        string scenePath = assets.PathFor(sceneId);
        if (String.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) return null;
        var source = assets.Scene(sceneId);
        {
            var box = FindNodePath(source.transform, new[] { "TheBox" });
            var sourceFrame = FindNodePath(source.transform, new[] { "TheBox", "RootObject", "TheBox_OuterFrame" });
            var sourceBar = FindNodePath(source.transform, new[] { "TheBox", "RootObject", "BnetBarBg" });
            var opened = FindNodePath(source.transform, new[] { "TheBox", "Bones", "CameraOpened" });
            var scene = JsonUtility.FromJson<SceneData>(File.ReadAllText(scenePath));
            var cameraNode = scene.nodes.FirstOrDefault(n => n.name == "TheBoxCamera");
            if (box != null && sourceFrame != null && opened != null && cameraNode != null && cameraNode.camera != null)
            {
                page.collectionSourceCameraPosition = opened.position;
                page.collectionSourceCameraRotation = opened.rotation;
                page.collectionSourceCameraFov = cameraNode.camera.field_of_view;
                page.collectionSourceCameraNear = cameraNode.camera.near;
                page.collectionSourceCameraFar = cameraNode.camera.far;
                page.collectionSourceCamera = true;
                var frameRenderer = sourceFrame.GetComponentsInChildren<Renderer>(true).FirstOrDefault(item => item.sharedMaterial != null && item.sharedMaterial.mainTexture is Texture2D);
                var frameTexture = frameRenderer == null ? null : frameRenderer.sharedMaterial.mainTexture as Texture2D;
                if (frameTexture != null)
                {
                    var sourceRepeatTexture = UnityEngine.Object.Instantiate(frameTexture);
                    sourceRepeatTexture.name = "CollectionSourceFrame_Repeat";
                    sourceRepeatTexture.wrapMode = TextureWrapMode.Repeat;
                    page.ownedTextures.Add(sourceRepeatTexture);
                    foreach (var material in sourceFrame.GetComponentsInChildren<Renderer>(true)
                        .SelectMany(item => item.sharedMaterials).Where(item => item != null && item.mainTexture == frameTexture))
                        material.mainTexture = sourceRepeatTexture;
                }
                var holder = new GameObject("CollectionSourceBox");
                holder.transform.SetParent(page.root.transform, false);
                holder.transform.localScale = box.localScale;
                sourceFrame.SetParent(holder.transform, false);
                if (sourceBar != null) sourceBar.SetParent(holder.transform, false);
                ReleaseScene(source);
                return holder.transform;
            }
        }
        var frame = FindNodePath(source.transform, new[] { "TheBox", "RootObject", "TheBox_OuterFrame" });
        var panels = frame == null ? null : frame.Find("TheBox_OuterFramePanels");
        var left = panels == null ? null : panels.Find("TheBox_FrameOuterPanelL_mesh");
        var right = panels == null ? null : panels.Find("TheBox_FrameOuterPanelR_mesh");
        var panelRenderer = left == null ? null : left.GetComponent<Renderer>();
        var texture = panelRenderer == null || panelRenderer.sharedMaterial == null
            ? null : panelRenderer.sharedMaterial.mainTexture as Texture2D;
        if (frame == null || left == null || right == null || texture == null ||
            frame.GetComponent<MeshFilter>() == null || left.GetComponent<MeshFilter>() == null ||
            right.GetComponent<MeshFilter>() == null)
        {
            ReleaseScene(source);
            return null;
        }

        var repeatTexture = UnityEngine.Object.Instantiate(texture);
        repeatTexture.name = "CollectionTheBoxFrame_Repeat";
        repeatTexture.wrapMode = TextureWrapMode.Repeat;
        page.ownedTextures.Add(repeatTexture);
        foreach (var material in frame.GetComponentsInChildren<Renderer>(true)
            .SelectMany(item => item.sharedMaterials).Where(item => item != null && item.mainTexture == texture))
            material.mainTexture = repeatTexture;

        // 源外框的局部层级和UV保留；收藏页的相机与位置仍是拟合重建。
        var wrapper = new GameObject("CollectionTheBoxFrame");
        wrapper.transform.SetParent(page.root.transform, false);
        frame.SetParent(wrapper.transform, false);
        ReleaseScene(source);
        return wrapper.transform;
    }

    static Renderer[] GetFrameRenderers(Page page)
    {
        return page.frameRoots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
    }

    static void FreezeFrameBounds(Page page)
    {
        var renderers = GetFrameRenderers(page);
        if (renderers.Length == 0) return;
        page.frameBounds = BoundsFor(renderers);
        page.frameRendererCount = renderers.Length;
        page.frameBoundsFrozen = true;
    }

    static Bounds BoundsFor(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Renderer[] ActiveRenderers(Transform root)
    {
        return root == null ? new Renderer[0] : root.GetComponentsInChildren<Renderer>(true)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
    }

    static void KeepBoundsCenter(Transform root, Renderer[] renderers, Vector3 center)
    {
        if (root == null || renderers.Length == 0) return;
        root.position += center - BoundsFor(renderers).center;
    }

    void ConfigureCollectionComposition(Page page, float aspect)
    {
        if (page.collectionBookBack == null || page.collectionPageManager == null || page.collectionDeckTray == null ||
            page.collectionDeckContentsBackground == null) return;
        if (page.collectionSourceCamera)
        {
            page.collectionDesignCamera = true;
            if (Mathf.Abs(page.collectionCompositionAspect - aspect) > .01f ||
                !RectApproximately(page.collectionCompositionRect, page.displayRect))
            {
                page.collectionCompositionAspect = aspect;
                page.collectionCompositionRect = page.displayRect;
                page.rendered = false;
            }
            return;
        }
        Rect displayRect = page.displayRect;
        if (page.collectionDesignCamera && Mathf.Abs(page.collectionCompositionAspect - aspect) <= .01f &&
            RectApproximately(page.collectionCompositionRect, displayRect)) return;

        page.collectionBookBack.localPosition = page.collectionBookBackSourcePosition;
        page.collectionBookBack.localScale = page.collectionBookBackSourceScale;
        page.collectionPageManager.localPosition = page.collectionPageManagerSourcePosition;
        page.collectionPageManager.localScale = page.collectionPageManagerSourceScale;
        page.collectionDeckTray.localPosition = page.collectionDeckTraySourcePosition;
        page.collectionDeckTray.localScale = page.collectionDeckTraySourceScale;
        page.collectionDeckContentsBackground.localPosition = page.collectionDeckContentsSourcePosition;
        page.collectionDeckContentsBackground.localScale = page.collectionDeckContentsSourceScale;

        float layoutScale = displayRect.height / 1000f;
        Rect paperTarget = new Rect(30f, 95f, 1060f, 790f);
        Rect trayTarget = new Rect(1120f, 80f, 450f, 810f);
        paperTarget = ScaleRect(paperTarget, layoutScale);
        trayTarget = ScaleRect(trayTarget, layoutScale);

        var paper = FindNodePath(page.root.transform, new[]
        {
            "CollectionManager", "CollectionPageManager", "SCENE_COLLECTION_PAGE", "CollectionPage", "BasePage"
        });
        var trayArea = FindNodePath(page.root.transform, new[]
        {
            "CollectionManager", "CollectionDeckTray", "DeckTrayArea"
        });
        var paperRenderer = paper == null ? null : paper.GetComponent<Renderer>();
        var trayRenderer = trayArea == null ? null : trayArea.GetComponent<Renderer>();
        if (paperRenderer == null || trayRenderer == null) return;

        FitCollectionGroup(page.collectionPageManager, paperRenderer, paperTarget, displayRect, false);
        FitCollectionGroup(page.collectionDeckTray, trayRenderer, trayTarget, displayRect, true);

        var bookBackMesh = FindNodePath(page.collectionBookBack, new[] { "BookBack", "BookBack_mesh" });
        var bookBackRenderer = bookBackMesh == null ? null : bookBackMesh.GetComponent<Renderer>();
        if (bookBackRenderer != null)
        {
            FitCollectionGroup(page.collectionBookBack, bookBackRenderer,
                ScaleRect(new Rect(30f, 40f, 1060f, 860f), layoutScale), displayRect, false);
            page.collectionBookBack.position += new Vector3(0,
                paperRenderer.bounds.min.y - 1f - bookBackRenderer.bounds.max.y, 0);
        }

        // Keep the purple card-list panel centered within the actual tray mesh after the tray is moved.
        var deckContentsRenderer = page.collectionDeckContentsBackground.GetComponent<Renderer>();
        if (deckContentsRenderer != null)
        {
            Bounds trayBounds = trayRenderer.bounds;
            Bounds contentsBounds = deckContentsRenderer.bounds;
            float inset = Mathf.Min(12f * layoutScale, Mathf.Min(trayBounds.size.x, trayBounds.size.z) * .04f);
            float targetWidth = Mathf.Max(.01f, trayBounds.size.x - inset * 2f);
            float targetHeight = Mathf.Max(.01f, trayBounds.size.z - inset * 2f);
            Vector3 scale = page.collectionDeckContentsSourceScale;
            page.collectionDeckContentsBackground.localScale = new Vector3(
                scale.x * targetWidth / Mathf.Max(.01f, contentsBounds.size.x),
                scale.y,
                scale.z * targetHeight / Mathf.Max(.01f, contentsBounds.size.z));
            contentsBounds = deckContentsRenderer.bounds;
            page.collectionDeckContentsBackground.position += new Vector3(
                trayBounds.center.x - contentsBounds.center.x,
                0,
                trayBounds.center.z - contentsBounds.center.z);
        }

        page.framePaddingScale = 1f;
        page.frameBoundsFrozen = false;
        FreezeFrameBounds(page);
        if (page.collectionFrameBackground != null)
        {
            var frame = page.collectionFrameBackground;
            frame.localPosition = Vector3.zero;
            frame.localScale = Vector3.one;
            var renderers = ActiveRenderers(frame);
            if (renderers.Length > 0)
            {
                Bounds sourceBounds = BoundsFor(renderers);
                frame.localScale = new Vector3(displayRect.width / Mathf.Max(.01f, sourceBounds.size.x),
                    1f, displayRect.height / Mathf.Max(.01f, sourceBounds.size.z));
                Bounds fittedBounds = BoundsFor(renderers);
                frame.position += new Vector3(displayRect.center.x - fittedBounds.center.x,
                    page.frameBounds.min.y - 1f - fittedBounds.max.y, -fittedBounds.center.z);
            }
        }
        page.collectionDesignCamera = true;
        page.collectionCompositionAspect = aspect;
        page.collectionCompositionRect = displayRect;
        page.rendered = false;
    }

    static Rect ScaleRect(Rect rect, float scale)
    {
        return new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
    }

    static bool RectApproximately(Rect left, Rect right)
    {
        return Mathf.Abs(left.x - right.x) <= .01f && Mathf.Abs(left.y - right.y) <= .01f &&
            Mathf.Abs(left.width - right.width) <= .01f && Mathf.Abs(left.height - right.height) <= .01f;
    }

    static void FitCollectionGroup(Transform group, Renderer renderer, Rect target, Rect displayRect, bool verticalOnLocalY)
    {
        Bounds bounds = renderer.bounds;
        float widthFactor = target.width / Mathf.Max(.01f, bounds.size.x);
        float heightFactor = target.height / Mathf.Max(.01f, bounds.size.z);
        Vector3 sourceScale = group.localScale;
        group.localScale = verticalOnLocalY
            ? new Vector3(sourceScale.x * widthFactor, sourceScale.y * heightFactor, sourceScale.z)
            : new Vector3(sourceScale.x * widthFactor, sourceScale.y, sourceScale.z * heightFactor);

        bounds = renderer.bounds;
        float targetCenterZ = displayRect.y + displayRect.height * .5f - target.center.y;
        group.position += new Vector3(target.center.x - bounds.center.x, 0, targetCenterZ - bounds.center.z);
    }

    void ConfigureShopComposition(Page page, float aspect)
    {
        if (page.shopFrameRoots == null || page.shopFrameRoots.Length == 0 ||
            Mathf.Abs(page.shopCompositionAspect - aspect) <= .01f) return;

        for (int i = 0; i < page.shopFrameRoots.Length; i++)
        {
            var frame = page.shopFrameRoots[i];
            if (frame == null) continue;
            frame.localPosition = page.shopFrameSourcePositions[i];
            frame.localScale = page.shopFrameSourceScales[i];
        }
        page.framePaddingScale = 1f;
        var frameRenderers = GetFrameRenderers(page);
        if (frameRenderers.Length > 0)
        {
            Bounds frameBounds = BoundsFor(frameRenderers);
            float targetWidth = frameBounds.size.z * aspect;
            float scaleFactor = targetWidth / Mathf.Max(frameBounds.size.x, .01f);
            if (scaleFactor > 1f)
            {
                for (int i = 0; i < page.shopFrameRoots.Length; i++)
                {
                    var frame = page.shopFrameRoots[i];
                    if (frame == null) continue;
                    var renderers = ActiveRenderers(frame);
                    if (renderers.Length == 0) continue;
                    Vector3 center = BoundsFor(renderers).center;
                    var scale = frame.localScale;
                    frame.localScale = new Vector3(scale.x * scaleFactor, scale.y, scale.z);
                    KeepBoundsCenter(frame, renderers, center);
                }
            }
        }
        FitShopBackingToBorder(page);
        page.shopCompositionAspect = aspect;
        page.rendered = false;
    }

    static void FitShopBackingToBorder(Page page)
    {
        var backing = FindNodePath(page.root.transform, new[]
        {
            "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Backing"
        });
        var border = FindNodePath(page.root.transform, new[]
        {
            "MainShopWidget", "BoxCalibrator", "ShopBox", "Framing", "Shop_Box_PC", "Shop_Box_Border"
        });
        var backingRenderers = ActiveRenderers(backing);
        var borderRenderers = ActiveRenderers(border);
        if (backing == null || border == null || backingRenderers.Length == 0 || borderRenderers.Length == 0)
            return;

        Bounds backingBounds = BoundsFor(backingRenderers);
        Bounds borderBounds = BoundsFor(borderRenderers);
        Vector3 sourceScale = backing.localScale;
        backing.localScale = new Vector3(
            sourceScale.x * borderBounds.size.x / Mathf.Max(.01f, backingBounds.size.x),
            sourceScale.y * borderBounds.size.z / Mathf.Max(.01f, backingBounds.size.z),
            sourceScale.z);
        Bounds fittedBounds = BoundsFor(backingRenderers);
        KeepBoundsCenter(backing, backingRenderers,
            new Vector3(borderBounds.center.x, fittedBounds.center.y, borderBounds.center.z));
    }

    void Frame(Page page, float aspect)
    {
        if (page.collectionSourceCamera)
        {
            camera.orthographic = false;
            camera.fieldOfView = page.collectionSourceCameraFov;
            camera.aspect = aspect;
            camera.transform.SetPositionAndRotation(page.collectionSourceCameraPosition, page.collectionSourceCameraRotation);
            camera.nearClipPlane = page.collectionSourceCameraNear;
            camera.farClipPlane = page.collectionSourceCameraFar;
            return;
        }
        camera.orthographic = true;
        if (!page.frameBoundsFrozen)
        {
            var renderers = GetFrameRenderers(page);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            page.frameBounds = bounds;
            page.frameRendererCount = renderers.Length;
        }
        Bounds frameBounds = page.frameBounds;
        if (page.collectionDesignCamera && page.hasDisplayRect)
        {
            float collectionDistance = Mathf.Max(100f, frameBounds.size.y + 100f);
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(.01f, page.displayRect.height * .5f);
            camera.transform.SetPositionAndRotation(
                new Vector3(page.displayRect.center.x, frameBounds.max.y + collectionDistance, 0),
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            camera.nearClipPlane = .01f;
            camera.farClipPlane = Mathf.Max(1000f, collectionDistance + frameBounds.size.y + 100f);
            return;
        }
        float distance = Mathf.Max(4, frameBounds.extents.magnitude * 2 + frameBounds.extents.y + 1);
        camera.aspect = aspect;
        camera.orthographicSize = Mathf.Max(.01f, Mathf.Max(frameBounds.extents.z, frameBounds.extents.x / Mathf.Max(aspect, .01f)) * page.framePaddingScale);
        camera.transform.SetPositionAndRotation(frameBounds.center + page.cameraFocusOffset + Vector3.up * distance, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        camera.nearClipPlane = .01f;
        camera.farClipPlane = distance + frameBounds.extents.y * 2 + 10;
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

    Rect VisibleFrameUv(Page page)
    {
        var crop = ProjectBoundsToViewport(page.frameBounds);
        var frame = FindNodePath(page.root.transform, new[] { "PackOpening", "SocketTray", "PackOpeningTray" });
        var renderer = frame == null ? null : frame.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 圆盘比外木框高；按木框上下边裁切，保留左侧卡包托盘的横向范围。
            var wood = ProjectBoundsToViewport(renderer.bounds);
            crop.y = wood.y;
            crop.height = wood.height;
        }
        return crop;
    }

    static Rect MapToVisibleRect(Rect projected, Rect displayRect, Rect visibleUv)
    {
        float width = Mathf.Max(.0001f, visibleUv.width);
        float height = Mathf.Max(.0001f, visibleUv.height);
        float top = 1 - visibleUv.yMax;
        return new Rect(
            displayRect.x + (projected.x - displayRect.x - visibleUv.x * displayRect.width) / width,
            displayRect.y + (projected.y - displayRect.y - top * displayRect.height) / height,
            projected.width / width,
            projected.height / height);
    }

    RenderTexture RenderVisibleCapture(Page page)
    {
        if (page.texture == null) return null;
        if (page.croppedCaptureTexture == null ||
            page.croppedCaptureTexture.width != page.texture.width ||
            page.croppedCaptureTexture.height != page.texture.height)
        {
            ReleaseTexture(page.croppedCaptureTexture);
            page.croppedCaptureTexture = new RenderTexture(page.texture.width, page.texture.height, 0,
                RenderTextureFormat.ARGB32)
            {
                name = "NativeHubVisibleCapture_" + page.root.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            if (!page.croppedCaptureTexture.Create())
            {
                ReleaseTexture(page.croppedCaptureTexture);
                page.croppedCaptureTexture = null;
                return page.texture;
            }
        }

        var previous = RenderTexture.active;
        try
        {
            Rect uv = page.visibleUv;
            Graphics.Blit(page.texture, page.croppedCaptureTexture,
                new Vector2(uv.width, uv.height), new Vector2(uv.xMin, uv.yMin));
        }
        finally { RenderTexture.active = previous; }
        return page.croppedCaptureTexture;
    }

    static string SceneForPage(string page)
    {
        if (page == "collection") return "SCENE_COLLECTION_PAGE";
        if (page == "account" || page == "decks") return "SCENE_COLLECTION_MANAGER";
        if (page == "packs") return "SCENE_PACK_OPENING";
        if (page == "shop") return "SCENE_MAIN_SHOP_PC";
        if (page == "quests") return "SCENE_QUEST_LOG";
        return "";
    }

    static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
    }

    static void ReleaseTexture(Page page)
    {
        ReleaseTexture(page.texture);
        page.texture = null;
        ReleaseTexture(page.croppedCaptureTexture);
        page.croppedCaptureTexture = null;
    }

    static void ReleaseTexture(RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
        else UnityEngine.Object.DestroyImmediate(texture);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var page in pages.Values)
        {
            ReleaseTexture(page);
            if (page.root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(page.root);
                else UnityEngine.Object.DestroyImmediate(page.root);
            }
            foreach (var material in page.materials ?? new Material[0])
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            foreach (var mesh in page.generatedMeshes)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            foreach (var texture in page.ownedTextures)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        pages.Clear();
        foreach (var preview in packPreviews.Values)
        {
            ReleaseTexture(preview.texture);
            ReleaseScene(preview.root);
        }
        packPreviews.Clear();
        foreach (var preview in questTilePreviews.Values)
        {
            ReleaseTexture(preview.texture);
            ReleaseScene(preview.root);
        }
        questTilePreviews.Clear();
        foreach (var preview in buttonPreviews.Values)
        {
            ReleaseTexture(preview.texture);
            ReleaseScene(preview.root);
        }
        buttonPreviews.Clear();
        if (cameraObject != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(cameraObject);
            else UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
