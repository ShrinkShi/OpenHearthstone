using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class F024AccountEditorReplayRunner
{
    const string ActiveKey = "F024.EditorAccountReplay.Active";
    const string ReportKey = "F024.EditorAccountReplay.Report";
    const string DeadlineKey = "F024.EditorAccountReplay.DeadlineTicks";
    const string ExitKey = "F024.EditorAccountReplay.Exit";
    const string EnteredPlayKey = "F024.EditorAccountReplay.EnteredPlay";
    const string OriginalSizeKey = "F024.EditorAccountReplay.OriginalSize";
    const string OriginalPlayModeKey = "F024.EditorAccountReplay.OriginalPlayMode";
    const string OriginalZoomTranslationXKey = "F024.EditorAccountReplay.OriginalZoomTranslationX";
    const string OriginalZoomTranslationYKey = "F024.EditorAccountReplay.OriginalZoomTranslationY";
    const string OriginalZoomScaleXKey = "F024.EditorAccountReplay.OriginalZoomScaleX";
    const string OriginalZoomScaleYKey = "F024.EditorAccountReplay.OriginalZoomScaleY";
    const string ProfileKey = "F024.EditorAccountReplay.Profile";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [Serializable]
    sealed class ReplayResult
    {
        public string result, error, input_mode;
    }

    static F024AccountEditorReplayRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        EditorApplication.update += PollReplay;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Start()
    {
        string reportPath = "";
        string profile = "";
        bool exitOnCompletion = Has("--account-replay-exit");
        EditorWindow gameView = null;
        int originalSize = -1;
        string originalPlayMode = "";
        Vector2 originalZoomTranslation = Vector2.zero;
        Vector2 originalZoomScale = Vector2.one;
        bool zoomStateCaptured = false;
        bool viewChanged = false;
        bool sendEventMode = Has("--account-editor-gui-replay");
        bool nativeInputMode = Has("--account-editor-native-input");
        try
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Editor GUI replay requires a non-batchmode Unity Editor.");
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("The isolated Editor must start outside Play Mode.");
            if (!Has("--account-replay") || sendEventMode == nativeInputMode)
                throw new InvalidOperationException("Pass --account-replay and exactly one of --account-editor-gui-replay or --account-editor-native-input.");
            if (!exitOnCompletion)
                throw new InvalidOperationException("Pass --account-replay-exit so this isolated Editor can close after writing its report.");

            profile = Regex.Replace(Value("--profile"), "[^A-Za-z0-9_-]", "_");
            if (String.IsNullOrEmpty(profile))
                throw new InvalidOperationException("Pass a unique --profile for this Editor replay.");
            int port;
            if (!Int32.TryParse(Value("--port"), out port) || port < 1 || port > 65535)
                throw new InvalidOperationException("Pass --port for the isolated loopback account fixture.");
            string screenshotDirectory = Value("--screenshot-dir");
            if (String.IsNullOrWhiteSpace(screenshotDirectory))
                throw new InvalidOperationException("Pass --screenshot-dir for replay captures.");
            screenshotDirectory = Path.GetFullPath(screenshotDirectory);
            Directory.CreateDirectory(screenshotDirectory);

            reportPath = Value("--account-replay-report");
            if (String.IsNullOrWhiteSpace(reportPath))
                throw new InvalidOperationException("Pass --account-replay-report for the machine-readable result.");
            reportPath = Path.GetFullPath(reportPath);
            if (File.Exists(reportPath))
                throw new InvalidOperationException("Replay report already exists; choose a fresh output path.");
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!String.IsNullOrEmpty(reportDirectory)) Directory.CreateDirectory(reportDirectory);

            string profileDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HearthstoneClone", "profiles", profile);
            if (Directory.Exists(profileDirectory))
                throw new InvalidOperationException("Replay profile already exists; choose a fresh profile name.");
            if (SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("An Editor account replay is already active in this project.");

            Type gameViewType;
            gameView = FindSelectedGameView(out gameViewType);
            if (gameView == null)
                throw new InvalidOperationException("Capability fail: this test layout has no selected GameView. The runner will not create, show, or focus a window.");

            string absoluteScenePath = Path.Combine(Application.dataPath, "Scenes", "Main.unity");
            if (!File.Exists(absoluteScenePath))
                throw new FileNotFoundException("The project main scene is missing.", absoluteScenePath);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved edits; the isolated replay will not discard them.");
            }

            originalSize = ReadSelectedSizeIndex(gameView);
            originalPlayMode = ReadPlayModeBehavior(gameView);
            zoomStateCaptured = TryReadZoomState(gameView, out originalZoomTranslation, out originalZoomScale);
            if (originalSize < 0 || String.IsNullOrEmpty(originalPlayMode) || !zoomStateCaptured)
                throw new InvalidOperationException("Could not inspect the selected GameView size, Play Mode behavior, or zoom transform.");

            Scene mainScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!mainScene.IsValid() || !mainScene.isLoaded ||
                !mainScene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<ClientApp>(true).Length > 0))
                throw new InvalidOperationException("Assets/Scenes/Main.unity did not load a ClientApp instance.");

            SessionState.SetInt(OriginalSizeKey, originalSize);
            SessionState.SetString(OriginalPlayModeKey, originalPlayMode);
            SessionState.SetFloat(OriginalZoomTranslationXKey, originalZoomTranslation.x);
            SessionState.SetFloat(OriginalZoomTranslationYKey, originalZoomTranslation.y);
            SessionState.SetFloat(OriginalZoomScaleXKey, originalZoomScale.x);
            SessionState.SetFloat(OriginalZoomScaleYKey, originalZoomScale.y);
            SessionState.SetString(ProfileKey, profile);
            SetPlayModeBehavior(gameView, "PlayUnfocused");
            viewChanged = true;
            SelectBuiltInFullHD(gameView, gameViewType);
            Vector2 targetSize;
            if (!TryGetProperty(gameView, "targetRenderSize", out targetSize) ||
                Mathf.RoundToInt(targetSize.x) != 1920 || Mathf.RoundToInt(targetSize.y) != 1080)
                throw new InvalidOperationException("Could not select the built-in fixed 1920x1080 GameView target.");
            FitGameViewTarget(gameView);
            gameView.Repaint();

            int timeoutSeconds = 600;
            Int32.TryParse(Value("--account-editor-timeout-seconds", "600"), out timeoutSeconds);
            timeoutSeconds = Mathf.Clamp(timeoutSeconds, 30, 3600);
            SessionState.SetString(ReportKey, reportPath);
            SessionState.SetString(DeadlineKey, DateTime.UtcNow.AddSeconds(timeoutSeconds).Ticks.ToString());
            SessionState.SetBool(ExitKey, exitOnCompletion);
            SessionState.SetBool(EnteredPlayKey, false);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.update += PollReplay;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            Debug.Log("F024 editor account replay starting: scene=" + ScenePath +
                ", profile=" + profile + ", port=" + port + ", GameView target=1920x1080, mode=PlayUnfocused");
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            if (viewChanged && gameView != null)
                RestoreGameView(gameView, originalSize, originalPlayMode, originalZoomTranslation, originalZoomScale);
            WriteFailureReport(reportPath, exception.Message);
            Debug.LogError("F024 editor account replay startup failed: " + exception.Message);
            if (exitOnCompletion) EditorApplication.Exit(2);
        }
    }

    static EditorWindow FindSelectedGameView(out Type gameViewType)
    {
        gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", false);
        if (gameViewType == null) return null;
        return Resources.FindObjectsOfTypeAll(gameViewType)
            .OfType<EditorWindow>()
            .FirstOrDefault(window => window != null && window.hasFocus);
    }

    static void SelectBuiltInFullHD(EditorWindow gameView, Type gameViewType)
    {
        Type sizesType = gameViewType.Assembly.GetType("UnityEditor.GameViewSizes", false);
        if (sizesType == null) throw new InvalidOperationException("Unity 2022.3 GameViewSizes type is unavailable.");
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        PropertyInfo singletonProperty = sizesType.GetProperty("instance", flags);
        object sizes = singletonProperty == null ? null : singletonProperty.GetValue(null, null);
        PropertyInfo currentGroupProperty = sizesType.GetProperty("currentGroup", flags);
        object group = sizes == null || currentGroupProperty == null ? null : currentGroupProperty.GetValue(sizes, null);
        if (group == null) throw new InvalidOperationException("Could not read the current GameView size group.");

        MethodInfo totalMethod = group.GetType().GetMethod("GetTotalCount", flags);
        MethodInfo getSizeMethod = group.GetType().GetMethod("GetGameViewSize", flags, null, new[] { typeof(int) }, null);
        if (totalMethod == null || getSizeMethod == null)
            throw new InvalidOperationException("Could not enumerate fixed GameView sizes.");
        int total = (int)totalMethod.Invoke(group, null);
        int fullHdIndex = -1;
        for (int i = 0; i < total; i++)
        {
            object size = getSizeMethod.Invoke(group, new object[] { i });
            int width, height;
            object kind;
            if (TryGetProperty(size, "width", out width) && TryGetProperty(size, "height", out height) &&
                TryGetProperty(size, "sizeType", out kind) && kind != null &&
                kind.ToString() == "FixedResolution" && width == 1920 && height == 1080)
            {
                fullHdIndex = i;
                break;
            }
        }
        if (fullHdIndex < 0)
            throw new InvalidOperationException("The current GameView group has no built-in 1920x1080 fixed resolution; the runner will not write a custom size into Editor preferences.");

        MethodInfo callback = gameViewType.GetMethod("SizeSelectionCallback", flags,
            null, new[] { typeof(int), typeof(object) }, null);
        if (callback == null)
            throw new InvalidOperationException("Unity 2022.3 GameView.SizeSelectionCallback is unavailable.");
        callback.Invoke(gameView, new object[] { fullHdIndex, null });
    }

    static int ReadSelectedSizeIndex(EditorWindow gameView)
    {
        int index;
        return TryGetProperty(gameView, "selectedSizeIndex", out index) ? index : -1;
    }

    static string ReadPlayModeBehavior(EditorWindow gameView)
    {
        object behavior;
        return TryGetProperty(gameView, "enterPlayModeBehavior", out behavior) && behavior != null
            ? behavior.ToString() : "";
    }

    static void SetPlayModeBehavior(EditorWindow gameView, string name)
    {
        PropertyInfo property = gameView.GetType().GetProperty("enterPlayModeBehavior",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || !property.CanWrite)
            throw new InvalidOperationException("Unity PlayModeView.enterPlayModeBehavior is unavailable.");
        object value = Enum.Parse(property.PropertyType, name);
        property.SetValue(gameView, value, null);
    }

    static bool TryReadZoomState(EditorWindow gameView, out Vector2 translation, out Vector2 scale)
    {
        translation = Vector2.zero;
        scale = Vector2.one;
        object zoomArea;
        return TryGetField(gameView, "m_ZoomArea", out zoomArea) && zoomArea != null &&
            TryGetProperty(zoomArea, "translation", out translation) &&
            TryGetProperty(zoomArea, "scale", out scale);
    }

    static void FitGameViewTarget(EditorWindow gameView)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        Vector2 targetSize;
        Rect viewRect;
        if (!TryGetProperty(gameView, "targetRenderSize", out targetSize) ||
            !TryGetProperty(gameView, "viewInWindow", out viewRect))
            throw new InvalidOperationException("Could not read GameView target and viewport before fitting.");

        InvokeGameViewMethod(gameView, "UpdateZoomAreaAndParent", Type.EmptyTypes, new object[0]);
        MethodInfo defaultScaleMethod = gameView.GetType().GetMethod("DefaultScaleForTargetInView", flags,
            null, new[] { typeof(Vector2), typeof(Vector2) }, null);
        if (defaultScaleMethod == null)
            throw new InvalidOperationException("Unity 2022.3 GameView.DefaultScaleForTargetInView is unavailable.");
        float fitScale = (float)defaultScaleMethod.Invoke(gameView, new object[] { targetSize, viewRect.size });
        if (fitScale <= 0f || Single.IsNaN(fitScale) || Single.IsInfinity(fitScale))
            throw new InvalidOperationException("Unity GameView returned an invalid fit zoom.");

        InvokeGameViewMethod(gameView, "SnapZoom", new[] { typeof(float) }, new object[] { fitScale });
        InvokeGameViewMethod(gameView, "EnforceZoomAreaConstraints", Type.EmptyTypes, new object[0]);
        gameView.Repaint();

        Rect targetInView;
        if (!TryGetProperty(gameView, "targetInView", out targetInView) ||
            !TryGetProperty(gameView, "viewInWindow", out viewRect))
            throw new InvalidOperationException("Could not verify fitted GameView target bounds.");
        Rect targetInWindow = new Rect(viewRect.position + targetInView.position, targetInView.size);
        const float tolerance = 1f;
        if (targetInWindow.xMin < viewRect.xMin - tolerance || targetInWindow.yMin < viewRect.yMin - tolerance ||
            targetInWindow.xMax > viewRect.xMax + tolerance || targetInWindow.yMax > viewRect.yMax + tolerance)
            throw new InvalidOperationException("Unity GameView fit left part of the 1920x1080 target outside the viewport: target=" +
                targetInWindow + ", viewport=" + viewRect + ", scale=" + fitScale);
        Debug.Log("F024 GameView fit: target=" + targetInWindow + ", viewport=" + viewRect + ", zoom=" + fitScale);
    }

    static void InvokeGameViewMethod(EditorWindow gameView, string name, Type[] signature, object[] arguments)
    {
        MethodInfo method = gameView.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
            null, signature, null);
        if (method == null) throw new InvalidOperationException("Unity 2022.3 GameView." + name + " is unavailable.");
        method.Invoke(gameView, arguments);
    }

    static bool RestoreGameView(EditorWindow gameView, int selectedSize, string playMode,
        Vector2 originalTranslation, Vector2 originalScale)
    {
        try
        {
            if (selectedSize >= 0)
            {
                Type gameViewType = gameView.GetType();
                MethodInfo callback = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
                    null, new[] { typeof(int), typeof(object) }, null);
                callback?.Invoke(gameView, new object[] { selectedSize, null });
            }
            InvokeGameViewMethod(gameView, "UpdateZoomAreaAndParent", Type.EmptyTypes, new object[0]);
            object zoomArea;
            if (!TryGetField(gameView, "m_ZoomArea", out zoomArea) || zoomArea == null)
                throw new InvalidOperationException("Could not restore the original GameView zoom transform.");
            InvokeZoomAreaMethod(zoomArea, "SetTransform", originalTranslation, originalScale);
            InvokeGameViewMethod(gameView, "EnforceZoomAreaConstraints", Type.EmptyTypes, new object[0]);
            if (!String.IsNullOrEmpty(playMode)) SetPlayModeBehavior(gameView, playMode);
            gameView.Repaint();
            Vector2 restoredTranslation, restoredScale;
            if (ReadSelectedSizeIndex(gameView) != selectedSize ||
                !String.Equals(ReadPlayModeBehavior(gameView), playMode, StringComparison.Ordinal) ||
                !TryReadZoomState(gameView, out restoredTranslation, out restoredScale) ||
                (restoredTranslation - originalTranslation).sqrMagnitude > 0.01f ||
                (restoredScale - originalScale).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("The original GameView size, zoom, or Play Mode behavior did not restore exactly.");
            Debug.Log("F024 GameView restored: sizeIndex=" + selectedSize + ", zoomScale=" + restoredScale +
                ", zoomTranslation=" + restoredTranslation + ", PlayModeBehavior=" + playMode);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not restore the test GameView settings: " + exception.Message);
            return false;
        }
    }

    static void InvokeZoomAreaMethod(object zoomArea, string name, Vector2 translation, Vector2 scale)
    {
        MethodInfo method = zoomArea.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
            null, new[] { typeof(Vector2), typeof(Vector2) }, null);
        if (method == null) throw new InvalidOperationException("Unity ZoomableArea." + name + " is unavailable.");
        method.Invoke(zoomArea, new object[] { translation, scale });
    }

    static bool TryGetField<T>(object target, string name, out T value)
    {
        value = default(T);
        if (target == null) return false;
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        if (field == null) return false;
        try
        {
            object raw = field.GetValue(target);
            if (!(raw is T)) return false;
            value = (T)raw;
            return true;
        }
        catch { return false; }
    }

    static bool TryGetProperty<T>(object target, string name, out T value)
    {
        value = default(T);
        if (target == null) return false;
        PropertyInfo property = target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        if (property == null || property.GetIndexParameters().Length != 0) return false;
        try
        {
            object raw = property.GetValue(target, null);
            if (!(raw is T)) return false;
            value = (T)raw;
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetBool(EnteredPlayKey, true);
        else if (state == PlayModeStateChange.EnteredEditMode &&
            SessionState.GetBool(ActiveKey, false) && SessionState.GetBool(EnteredPlayKey, false))
            Complete(2, "Unity left Play Mode before the account replay wrote its report.");
    }

    static void PollReplay()
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= PollReplay;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            return;
        }

        string reportPath = SessionState.GetString(ReportKey, "");
        if (File.Exists(reportPath))
        {
            try
            {
                ReplayResult report = JsonUtility.FromJson<ReplayResult>(File.ReadAllText(reportPath));
                if (report != null && (report.result == "ok" || report.result == "failed"))
                {
                    Complete(report.result == "ok" ? 0 : 1, report.error);
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("F024 replay report is not complete yet: " + exception.Message);
            }
        }

        long deadline;
        if (Int64.TryParse(SessionState.GetString(DeadlineKey, ""), out deadline) &&
            DateTime.UtcNow.Ticks >= deadline)
            Complete(2, "Timed out waiting for account-replay.json from ClientApp.");
    }

    static void Complete(int exitCode, string error)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (!String.IsNullOrEmpty(error)) Debug.LogError("F024 editor account replay failed: " + error);
        var gameView = FindSelectedGameView(out _);
        bool restored = gameView != null && RestoreGameView(gameView, SessionState.GetInt(OriginalSizeKey, -1),
                SessionState.GetString(OriginalPlayModeKey, ""),
                new Vector2(SessionState.GetFloat(OriginalZoomTranslationXKey, 0f), SessionState.GetFloat(OriginalZoomTranslationYKey, 0f)),
                new Vector2(SessionState.GetFloat(OriginalZoomScaleXKey, 1f), SessionState.GetFloat(OriginalZoomScaleYKey, 1f)));
        if (!restored)
        {
            Debug.LogError("F024 editor replay could not restore the original GameView layout state.");
            exitCode = 2;
        }
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        EditorApplication.update -= PollReplay;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (SessionState.GetBool(ExitKey, false))
            EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        else if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.isPlaying = false;
    }

    static void WriteFailureReport(string reportPath, string error)
    {
        if (String.IsNullOrEmpty(reportPath) || File.Exists(reportPath)) return;
        try
        {
            var report = new ReplayResult
            {
                result = "failed",
                error = error ?? "Editor replay setup failed",
                input_mode = Has("--account-editor-native-input")
                    ? "editor-game-input-queue"
                    : "editor-game-view-send-event"
            };
            string directory = Path.GetDirectoryName(reportPath);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not write the Editor replay startup report: " + exception.Message);
        }
    }

    static bool Has(string name)
    {
        return Environment.GetCommandLineArgs().Contains(name);
    }

    static string Value(string name, string fallback = "")
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
