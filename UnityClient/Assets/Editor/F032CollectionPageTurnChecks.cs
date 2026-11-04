using System;
using System.IO;
using UnityEngine;

public static class F032CollectionPageTurnChecks
{
    static void Capture(RenderTexture texture, string path)
    {
        var previous = RenderTexture.active;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            int visible = 0;
            foreach (var pixel in image.GetPixels32()) if (pixel.a > 8) visible++;
            if (visible < 100) throw new InvalidOperationException("翻页网格不可见: " + path);
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; AssetRegistry.ReleaseTransient(image); }
    }

    public static void Run()
    {
        string project = Directory.GetParent(Application.dataPath).Parent.FullName;
        string output = Path.Combine(project, "work", "f032-page-turn-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(output);
        var assets = new AssetRegistry(Path.Combine(project, "local"));
        string missingRoot = Path.Combine(output, "missing-assets");
        Directory.CreateDirectory(Path.Combine(missingRoot, "runtime"));
        File.WriteAllText(Path.Combine(missingRoot, "runtime", "registry.json"), "{\"entries\":[]}");
        if (NativeCollectionPageTurn.IsAvailable(new AssetRegistry(missingRoot)) || !NativeCollectionPageTurn.IsAvailable(assets))
            throw new InvalidOperationException("翻页资源存在性检查异常");
        using (var turn = new NativeCollectionPageTurn(assets))
        {
            foreach (bool right in new[] { false, true })
            {
                float duration = right ? turn.RightDuration : turn.LeftDuration;
                if (duration <= .01f) throw new InvalidOperationException("翻页动画时长无效");
                turn.Sample(right, 0f);
                var first = turn.BakeFrontVertices();
                Capture(turn.Render(right, 0f), Path.Combine(output, right ? "right-0.png" : "left-0.png"));
                turn.Sample(right, duration * .5f);
                var middle = turn.BakeFrontVertices();
                if (first.Length == 0 || middle.Length != first.Length) throw new InvalidOperationException("蒙皮顶点缺失");
                float movement = 0;
                for (int i = 0; i < first.Length; i++) movement = Mathf.Max(movement, Vector3.Distance(first[i], middle[i]));
                if (movement < .001f) throw new InvalidOperationException("翻页蒙皮没有变形");
                Capture(turn.Render(right, duration * .5f), Path.Combine(output, right ? "right-mid.png" : "left-mid.png"));
                Capture(turn.Render(right, duration), Path.Combine(output, right ? "right-end.png" : "left-end.png"));
                Debug.Log("F032 PAGE TURN " + (right ? "RIGHT" : "LEFT") + " duration=" + duration + " movement=" + movement);
            }
        }
        Debug.Log("F032 PAGE TURN GPU CHECKS PASSED: " + output);
    }
}
