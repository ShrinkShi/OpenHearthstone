using System;
using System.IO;
using System.Linq;
using UnityEngine;

// 原版翻页蒙皮与曲线；卡页画面由客户端捕获后传入。
public sealed class NativeCollectionPageTurn : IDisposable
{
    [Serializable] sealed class Source { public Animations animations; public Skins skins; }
    [Serializable] sealed class Animations { public Clip PageTurnLeft, PageTurnRight; }
    [Serializable] sealed class Skins { public Skin PageTurnFront, PageTurnBack; }
    [Serializable] sealed class Skin { public Bone[] bones; }
    [Serializable] sealed class Bone { public string name; }
    [Serializable] sealed class Clip { public Curves curves; }
    [Serializable] sealed class Curves { public Track[] m_PositionCurves, m_RotationCurves, m_ScaleCurves; }
    [Serializable] sealed class Track { public string path; public Curve curve; }
    [Serializable] sealed class Curve { public Key[] m_Curve; }
    [Serializable] sealed class Key
    {
        public float time; public Vector4 value, inSlope, outSlope, inWeight, outWeight;
        public int weightedMode;
    }
    sealed class Binding
    {
        public Transform transform;
        public string property;
        public AnimationCurve[] channels;
    }

    readonly GameObject root;
    readonly Camera camera;
    RenderTexture texture;
    readonly SkinnedMeshRenderer front, back;
    readonly Binding[] left, right;
    readonly Material[] materials;
    readonly Vector3 sourceCameraPosition;
    readonly Quaternion sourceCameraRotation;
    readonly float sourceCameraFov, sourceCameraNear, sourceCameraFar;
    public float LeftDuration { get; private set; }
    public float RightDuration { get; private set; }

    public static bool IsAvailable(AssetRegistry assets)
    {
        if (assets == null) return false;
        foreach (string id in new[] { "COLLECTION_PAGE_TURN_SOURCE", "SCENE_COLLECTION_MANAGER", "SCENE_MAIN_MENU" })
        {
            string path = assets.PathFor(id);
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        }
        return true;
    }

    public NativeCollectionPageTurn(AssetRegistry assets)
    {
        string path = assets.PathFor("COLLECTION_PAGE_TURN_SOURCE");
        if (String.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("缺少翻页曲线", path);
        var data = JsonUtility.FromJson<Source>(File.ReadAllText(path));
        if (data == null || data.animations == null || data.skins == null) throw new InvalidDataException("翻页数据不完整");
        root = assets.Scene("SCENE_COLLECTION_MANAGER");
        try
        {
        root.transform.position = new Vector3(0, -3000, 0);
        var manager = root.transform.Find("CollectionManager");
        var pageManager = manager == null ? null : manager.Find("CollectionPageManager");
        var turn = pageManager == null ? null : pageManager.Find("PageTurn");
        if (turn == null) throw new InvalidDataException("缺少翻页节点");
        foreach (Transform child in manager) if (child != pageManager) child.gameObject.SetActive(false);
        foreach (Transform child in pageManager) if (child != turn) child.gameObject.SetActive(false);
        foreach (Transform node in turn.GetComponentsInChildren<Transform>(true))
        { node.gameObject.SetActive(true); node.gameObject.layer = 27; }
        front = CreateSkin(turn, "PageTurnFront", data.skins.PageTurnFront);
        back = CreateSkin(turn, "PageTurnBack", data.skins.PageTurnBack);
        materials = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .Where(m => m != null).Distinct().ToArray();
        left = Bind(turn, data.animations.PageTurnLeft, out float leftDuration);
        right = Bind(turn, data.animations.PageTurnRight, out float rightDuration);
        LeftDuration = leftDuration; RightDuration = rightDuration;
        var boxScene = assets.Scene("SCENE_MAIN_MENU");
        try
        {
            var opened = boxScene.transform.Find("TheBox/Bones/CameraOpened");
            var cameraData = JsonUtility.FromJson<SceneData>(File.ReadAllText(assets.PathFor("SCENE_MAIN_MENU")))
                .nodes.FirstOrDefault(n => n.name == "TheBoxCamera");
            if (opened == null || cameraData == null || cameraData.camera == null) throw new InvalidDataException("缺少收藏相机");
            sourceCameraPosition = opened.position + new Vector3(0, -3000, 0);
            sourceCameraRotation = opened.rotation;
            sourceCameraFov = cameraData.camera.field_of_view;
            sourceCameraNear = cameraData.camera.near;
            sourceCameraFar = cameraData.camera.far;
        }
        finally
        {
            foreach (var mat in boxScene.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                AssetRegistry.ReleaseTransient(mat);
            AssetRegistry.ReleaseTransient(boxScene);
        }
        var bounds = front.bounds;
        camera = new GameObject("CollectionPageTurnDiagnosticCamera").AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.aspect = 1f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << 27; camera.nearClipPlane = .01f; camera.farClipPlane = 1000f;
        camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .85f;
        camera.transform.SetPositionAndRotation(bounds.center + Vector3.up * 200f,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));
        texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        texture.Create();
        }
        catch
        {
            if (materials == null && root != null)
                foreach (var material in root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                    AssetRegistry.ReleaseTransient(material);
            Dispose();
            throw;
        }
    }

    static SkinnedMeshRenderer CreateSkin(Transform root, string name, Skin skin)
    {
        var node = root.Find(name);
        var old = node == null ? null : node.GetComponent<MeshRenderer>();
        var filter = node == null ? null : node.GetComponent<MeshFilter>();
        if (old == null || filter == null || skin == null || skin.bones == null) throw new InvalidDataException("缺少翻页网格: " + name);
        var transforms = root.GetComponentsInChildren<Transform>(true);
        var bones = skin.bones.Select(b => transforms.Single(t => t.name == b.name)).ToArray();
        if (filter.sharedMesh == null || filter.sharedMesh.bindposes.Length != bones.Length) throw new InvalidDataException("翻页绑定姿势不匹配: " + name);
        Mesh mesh = filter.sharedMesh;
        Material[] sourceMaterials = old.sharedMaterials;
        old.enabled = false;
        var renderer = node.gameObject.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh; renderer.sharedMaterials = sourceMaterials;
        renderer.bones = bones; renderer.rootBone = bones[0]; renderer.updateWhenOffscreen = true;
        var local = renderer.sharedMesh.bounds; local.Expand(local.size.magnitude * 3f); renderer.localBounds = local;
        return renderer;
    }

    static Binding[] Bind(Transform root, Clip clip, out float duration)
    {
        if (clip == null || clip.curves == null) throw new InvalidDataException("缺少翻页动画");
        var result = new System.Collections.Generic.List<Binding>();
        float maxTime = 0f;
        foreach (var pair in new[] { ("position", clip.curves.m_PositionCurves, 3), ("rotation", clip.curves.m_RotationCurves, 4), ("scale", clip.curves.m_ScaleCurves, 3) })
        foreach (var track in pair.Item2 ?? new Track[0])
        {
            var target = root.Find(track.path);
            if (target == null || track.curve == null || track.curve.m_Curve == null) throw new InvalidDataException("缺少翻页骨骼曲线: " + track.path);
            var channels = new AnimationCurve[pair.Item3];
            for (int axis = 0; axis < channels.Length; axis++)
            {
                int index = axis;
                var keys = track.curve.m_Curve.Select(k => new Keyframe(k.time, k.value[index], k.inSlope[index], k.outSlope[index], k.inWeight[index], k.outWeight[index])
                    { weightedMode = (WeightedMode)k.weightedMode }).ToArray();
                channels[axis] = new AnimationCurve(keys);
            }
            if (track.curve.m_Curve.Length > 0) maxTime = Mathf.Max(maxTime, track.curve.m_Curve.Max(k => k.time));
            result.Add(new Binding { transform = target, property = pair.Item1, channels = channels });
        }
        duration = maxTime;
        if (result.Count != 24 || duration <= 0f) throw new InvalidDataException("翻页骨骼曲线不完整");
        return result.ToArray();
    }

    public void Sample(bool turnRight, float time)
    {
        foreach (var b in turnRight ? right : left)
        {
            var c = b.channels;
            if (b.property == "rotation")
                b.transform.localRotation = new Quaternion(c[0].Evaluate(time), c[1].Evaluate(time), c[2].Evaluate(time), c[3].Evaluate(time)).normalized;
            else
            {
                var value = new Vector3(c[0].Evaluate(time), c[1].Evaluate(time), c[2].Evaluate(time));
                if (b.property == "position") b.transform.localPosition = value; else b.transform.localScale = value;
            }
        }
    }

    public Vector3[] BakeFrontVertices()
    {
        var mesh = new Mesh();
        try { front.BakeMesh(mesh); return mesh.vertices; }
        finally { AssetRegistry.ReleaseTransient(mesh); }
    }

    public void SetPageTexture(Texture pageTexture)
    {
        foreach (var renderer in new[] { front, back })
            foreach (var material in renderer.sharedMaterials)
                if (material != null) material.mainTexture = pageTexture;
    }

    public void SetScreenProjection(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
        int targetHeight = 900;
        int targetWidth = Mathf.Clamp(Mathf.RoundToInt(targetHeight * width / (float)height), 512, 2048);
        if (texture.width != targetWidth || texture.height != targetHeight)
        {
            AssetRegistry.ReleaseTransient(texture);
            texture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            texture.Create();
        }
        camera.orthographic = false; camera.aspect = width / (float)height;
        camera.fieldOfView = sourceCameraFov; camera.nearClipPlane = sourceCameraNear; camera.farClipPlane = sourceCameraFar;
        camera.transform.SetPositionAndRotation(sourceCameraPosition, sourceCameraRotation);
    }

    public RenderTexture Render(bool turnRight, float time)
    {
        Sample(turnRight, time);
        camera.targetTexture = texture;
        try { camera.Render(); }
        finally { camera.targetTexture = null; }
        return texture;
    }

    public void Dispose()
    {
        foreach (var material in materials ?? new Material[0]) AssetRegistry.ReleaseTransient(material);
        if (camera != null) AssetRegistry.ReleaseTransient(camera.gameObject);
        if (root != null) AssetRegistry.ReleaseTransient(root);
        AssetRegistry.ReleaseTransient(texture);
    }
}
