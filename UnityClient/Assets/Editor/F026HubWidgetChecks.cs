using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class F026HubWidgetChecks
{
    struct WidgetCapture
    {
        public string id;
        public int width, height;

        public WidgetCapture(string id, int width, int height)
        {
            this.id = id;
            this.width = width;
            this.height = height;
        }
    }

    static void Save(RenderTexture source, string path)
    {
        if (source == null) return;
        var previous = RenderTexture.active;
        var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = source;
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            AssetRegistry.ReleaseTransient(image);
        }
    }

    public static void Run()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
        string work = Path.Combine(projectRoot, "work");
        string outputBase = Path.Combine(work, "f026-hub-native-scenes");
        string output = outputBase;
        if (Directory.Exists(outputBase))
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            output = Path.Combine(work, "f026-hub-native-scenes-" + stamp);
            int suffix = 1;
            while (Directory.Exists(output))
                output = Path.Combine(work, "f026-hub-native-scenes-" + stamp + "-" + suffix++);
        }
        Directory.CreateDirectory(output);
        var report = new StringBuilder();
        report.AppendLine("F026 native hub widget GPU captures; labels remain GUI-owned.");
        report.AppendLine("Each run writes a separate folder so prior captures remain intact.");
        report.AppendLine("run=" + Path.GetFileName(output));
        report.AppendLine("rejected_source=ShopQuantityPrompt: captured frame is a friend-add input, not the pack purchase page.");
        report.AppendLine("rejected_source=questReroll: blue yellow-arrow texture does not match the red parchment corner reference.");

        var assets = new AssetRegistry(Path.Combine(projectRoot, "local"));
        var widgets = new NativeHubWidgets(assets);
        var hub = new NativeHubScene(assets);
        try
        {
            var captures = new[]
            {
                new WidgetCapture("chrome.gear", 256, 256),
                new WidgetCapture("chrome.gold", 256, 256),
                new WidgetCapture("chrome.dust", 256, 256),
                new WidgetCapture("chrome.coinbar", 1000, 160),
                new WidgetCapture("chrome.coinbar.bnet", 1000, 160),
                new WidgetCapture("chrome.coinbar.legacy", 1000, 160),
                new WidgetCapture("chrome.friends", 512, 512),
                new WidgetCapture("menu.panel", 1000, 700),
                new WidgetCapture("menu.option", 800, 180),
                new WidgetCapture("menu.header", 800, 180),
                new WidgetCapture("menu.button", 800, 180),
                new WidgetCapture("options.panel", 1280, 800),
                new WidgetCapture("social.action", 512, 512),
                new WidgetCapture("social.flyout", 512, 512),
                new WidgetCapture("social.panel", 900, 1000),
                new WidgetCapture("social.panel.frame", 900, 1000),
                new WidgetCapture("modal.bulkBuy.candidate", 1280, 800),
                new WidgetCapture("modal.bulkBuy.frame", 1280, 800),
                new WidgetCapture("quests.reroll", 256, 256),
                new WidgetCapture("collection.card.count", 256, 256),
                new WidgetCapture("collection.set.button", 256, 256),
                new WidgetCapture("collection.set.button.hover", 256, 256),
                new WidgetCapture("collection.mana.background", 800, 100),
                new WidgetCapture("collection.mana.cell", 256, 256),
                new WidgetCapture("collection.search.background", 800, 150),
                new WidgetCapture("collection.search.clear", 150, 150),
                new WidgetCapture("collection.detail.related", 420, 450),
                new WidgetCapture("collection.detail.info", 420, 400),
                new WidgetCapture("collection.detail.crafting", 470, 184)
            };
            foreach (var item in captures)
            {
                string detail;
                var texture = widgets.CaptureWidgetForDiagnostics(item.id, item.width, item.height, out detail);
                bool captureOk = texture != null;
                if (captureOk)
                {
                    Save(texture, Path.Combine(output, item.id.Replace('.', '-') + ".png"));
                    AssetRegistry.ReleaseTransient(texture);
                }
                report.AppendLine("## " + item.id);
                report.AppendLine(captureOk ? "capture=ok" : "capture=<missing>");
                report.AppendLine(detail);
                report.AppendLine();
            }

            string sceneDetail;
            var packs = hub.CapturePageForDiagnostics("SCENE_PACK_OPENING", 1530, 835, out sceneDetail);
            if (packs != null)
            {
                string after = Path.Combine(output, "packs-after.png");
                Save(packs, after);
                string before = Path.Combine(output, "packs-before.png");
                string baseline = Path.Combine(work, "f026-hub-native-scenes-r1", "packs-before.png");
                if (File.Exists(baseline)) File.Copy(baseline, before, false);
                else if (!File.Exists(before)) File.Copy(after, before, false);
            }
            report.AppendLine("## packs");
            report.AppendLine(packs == null ? "capture=<missing>" : "capture=ok");
            report.AppendLine(sceneDetail);
            Rect trayRect, socketRect, backRect;
            bool hasLayout = hub.TryGetPackOpeningLayout(out trayRect, out socketRect, out backRect);
            Rect headerRect;
            report.AppendLine("pack_header=" + (hub.TryGetPackOpeningHeader(out headerRect) ? headerRect.ToString() : "unavailable"));
            report.AppendLine("pack_layout=" + (hasLayout
                ? String.Format("tray={0} socket={1} back={2} (capture Rect coordinates; socket is PackOpening/SocketTray/PackOpeningTray_Disk renderer bounds, all projections rather than source hitboxes)", trayRect, socketRect, backRect)
                : "unavailable"));

            Rect[] socialActions;
            bool hasSocialLayout = widgets.TryGetSocialFlyoutLayout(new Rect(0, 0, 512, 512), out socialActions);
            report.AppendLine("social_flyout_order=AddButton;RAFButton;RemoveButton;PrivacySettingsButton");
            report.AppendLine("social_flyout_buttons=" + (hasSocialLayout
                ? String.Join(";", Array.ConvertAll(socialActions, rect => rect.ToString()))
                : "unavailable"));

            Rect previewRect, descriptionRect, quantityRect;
            bool hasBulkBuyLayout = widgets.TryGetBulkBuyLayout(new Rect(0, 0, 1280, 800),
                out previewRect, out descriptionRect, out quantityRect);
            report.AppendLine("bulk_buy_layout=" + (hasBulkBuyLayout
                ? String.Format("preview={0} description={1} quantity={2} (derived from supplied reference proportions)",
                    previewRect, descriptionRect, quantityRect)
                : "unavailable"));

            foreach (var variant in new[] { "shadow-off", "disk-off", "tray-off", "self-illumin-2x", "self-illumin-neutral" })
            {
                string variantReport;
                var variantTexture = hub.CapturePackOpeningVariantForDiagnostics(variant, 1530, 835, out variantReport);
                if (variantTexture != null)
                    Save(variantTexture, Path.Combine(output, "packs-" + variant + ".png"));
                report.AppendLine("## packs diagnostic variant " + variant);
                report.AppendLine(variantTexture == null ? "capture=<missing>" : "capture=ok");
                report.AppendLine(variantReport);
                report.AppendLine();
            }

            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log("F026 hub widget captures exported: " + output);
        }
        finally
        {
            widgets.Dispose();
            hub.Dispose();
        }
    }
}
