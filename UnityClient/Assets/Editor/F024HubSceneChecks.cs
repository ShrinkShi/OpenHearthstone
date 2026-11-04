using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class F024HubSceneChecks
{
    static void Save(RenderTexture source, string file)
    {
        if (source == null) throw new InvalidOperationException("Native hub scene capture returned no texture: " + file);
        var previous = RenderTexture.active;
        var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = source;
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply(false, false);
            File.WriteAllBytes(file, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            AssetRegistry.ReleaseTransient(image);
        }
    }

    static void CapturePage(NativeHubScene hub, string page, int width, int height, string output, StringBuilder report)
    {
        hub.Update(page);
        string sceneId = hub.SceneIdForPage(page);
        string detail;
        var texture = hub.CapturePageForDiagnostics(sceneId, width, height, out detail);
        Save(texture, Path.Combine(output, page + ".png"));
        report.AppendLine("## " + page);
        report.AppendLine(detail);
        report.AppendLine();
    }

    static void CaptureQuestTile(NativeHubScene hub, bool weekly, string output, StringBuilder report)
    {
        string name = weekly ? "quest-weekly" : "quest-daily";
        string detail;
        var texture = hub.CaptureQuestTileForDiagnostics(weekly, 440, 280, out detail);
        Save(texture, Path.Combine(output, name + ".png"));
        report.AppendLine("## " + name);
        report.AppendLine(detail);
        report.AppendLine();
    }

    public static void Run()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
        string output = Path.Combine(projectRoot, "work", "f024-hub-native-scenes");
        Directory.CreateDirectory(output);
        var report = new StringBuilder();
        report.AppendLine("F024 native hub scene GPU captures (no GUI text)");
        report.AppendLine();

        var hub = new NativeHubScene(new AssetRegistry(Path.Combine(projectRoot, "local")));
        try
        {
            CapturePage(hub, "collection", 1530, 835, output, report);
            CapturePage(hub, "packs", 1510, 835, output, report);
            CapturePage(hub, "shop", 1500, 835, output, report);
            CapturePage(hub, "quests", 1490, 835, output, report);
            CaptureQuestTile(hub, false, output, report);
            CaptureQuestTile(hub, true, output, report);
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log("F024 hub scene captures exported: " + output);
        }
        finally { hub.Dispose(); }
    }
}
