using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed partial class ClientApp
{
    sealed class F024ReplayGuiTarget
    {
        public Rect rect, designRect;
        public Vector2 screenCenter, designCenter;
        public Matrix4x4 guiMatrix;
        public int frame;
    }

    sealed class F024ReplayGuiEvent
    {
        public EventType type;
        public string target, coordinateTarget, handler;
        public string eventMarker;
        public Vector2 screenPosition, designPosition, clientPosition;
        public Event queuedEvent, injectedEvent;
        public bool completed;
        public bool nativeEventQueued, nativeEventObserved;
        public int hotControlBefore;
        public F024ReplayGuiInputRecord record;
    }

    readonly Dictionary<string, F024ReplayGuiTarget> f024ReplayGuiTargets = new Dictionary<string, F024ReplayGuiTarget>();
    F024ReplayGuiEvent f024ReplayGuiCurrent;
    Vector2 f024ReplayScreenOrigin, f024ReplayScreenRight, f024ReplayScreenDown;
    Matrix4x4 f024ReplayGuiRootMatrix = Matrix4x4.identity;
    bool f024ReplayCoordinateFrameValid;
    int f024ReplayExpectedHotControl;
    Vector2 f024ReplayGuiLastClientPosition;
    bool f024ReplayGuiHasLastPosition;
    int f024ReplayGuiSequence;
    static MethodInfo f024ReplayQueueEventMethod;
    string f024ReplayLastGuiHandler = "";
    string f024ReplayCollectionSearchFocusedControlBefore = "";
    int f024ReplayCollectionSearchKeyboardControlBefore;
    string f024ReplayCollectionSearchFocusedControl = "";
    int f024ReplayCollectionSearchKeyboardControl;
    bool f024AccountGuiReplayMode;
    bool f024AccountEditorGuiReplayMode;
    bool f024AccountEditorNativeInputMode;

    string F024ReplayMenuTarget(string node)
    {
        if (node == "Button_Store") return "menu.shop";
        if (node == "Button_OpenPacks_mesh") return "menu.packs";
        if (node == "Button_Collection_mesh") return "menu.collection";
        if (node == "QuestLogButton") return "menu.quests";
        if (node == "SecondButton") return "menu.traditional";
        if (node == "FirstButton") return "menu.adventure";
        if (node == "ThirdButton") return "menu.arena";
        if (node == "FourthButton") return "menu.other";
        return "menu." + (node ?? "unknown");
    }

    bool F024ReplayGuiEnabled
    {
        get { return f024AccountReplayActive && f024AccountGuiReplayMode; }
    }

    void F024ReplayGuiCaptureCoordinateFrame()
    {
        f024ReplayGuiRootMatrix = GUI.matrix;
        f024ReplayScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
        f024ReplayScreenRight = GUIUtility.GUIToScreenPoint(Vector2.right);
        f024ReplayScreenDown = GUIUtility.GUIToScreenPoint(Vector2.up);
        f024ReplayCoordinateFrameValid = true;
    }

    Vector2 F024ReplayGuiScreenToDesign(Vector2 screen)
    {
        Vector2 xAxis = f024ReplayScreenRight - f024ReplayScreenOrigin;
        Vector2 yAxis = f024ReplayScreenDown - f024ReplayScreenOrigin;
        Vector2 delta = screen - f024ReplayScreenOrigin;
        float determinant = xAxis.x * yAxis.y - xAxis.y * yAxis.x;
        if (!f024ReplayCoordinateFrameValid || Mathf.Abs(determinant) < 0.0001f) return Vector2.zero;
        return new Vector2(
            (delta.x * yAxis.y - delta.y * yAxis.x) / determinant,
            (xAxis.x * delta.y - xAxis.y * delta.x) / determinant);
    }

    Vector2 F024ReplayGuiDesignToScreen(Vector2 design)
    {
        return f024ReplayScreenOrigin +
            (f024ReplayScreenRight - f024ReplayScreenOrigin) * design.x +
            (f024ReplayScreenDown - f024ReplayScreenOrigin) * design.y;
    }

    bool F024ReplayGuiBeginOnGUI()
    {
        if (!F024ReplayGuiEnabled) return false;
        if (Event.current.type == EventType.Repaint) F024ReplayGuiCaptureCoordinateFrame();

        var input = f024ReplayGuiCurrent;
        if (input == null || input.completed || !input.nativeEventQueued || input.nativeEventObserved) return false;

        Event current = Event.current;
        var record = input.record;
        if (record != null && (current.type == input.type || current.rawType == input.type))
        {
            record.candidate_event_type = current.type.ToString();
            record.candidate_raw_event_type = current.rawType.ToString();
            record.candidate_event_marker = current.commandName ?? "";
            record.candidate_mouse_x = current.mousePosition.x;
            record.candidate_mouse_y = current.mousePosition.y;
            record.candidate_frame = Time.frameCount;
        }

        bool leftMouseEvent = input.type == EventType.MouseDown || input.type == EventType.MouseDrag ||
            input.type == EventType.MouseUp;
        bool typeMatches = current.type == input.type && current.rawType == input.type &&
            (!leftMouseEvent || current.button == 0);
        bool markerMatches = String.Equals(current.commandName, input.eventMarker, StringComparison.Ordinal);
        Vector2 expectedPointer = input.designPosition;
        bool pointerMatches = (current.mousePosition - expectedPointer).sqrMagnitude <= 4f;
        bool keyboardEvent = input.type == EventType.KeyDown || input.type == EventType.KeyUp;
        bool keyMatches = keyboardEvent && current.keyCode == input.queuedEvent.keyCode &&
            (input.type == EventType.KeyUp || current.character == input.queuedEvent.character);
        if (!typeMatches || (!markerMatches && (keyboardEvent ? !keyMatches : !pointerMatches))) return false;

        input.injectedEvent = current;
        input.nativeEventObserved = true;
        input.hotControlBefore = GUIUtility.hotControl;
        if (record != null)
        {
            record.native_event_observed = true;
            record.root_pointer_matches = pointerMatches;
            record.event_match = markerMatches ? "native-marker" :
                (keyboardEvent ? "native-type-key-character" : "native-type-button-position");
            record.current_event_type = current.type.ToString();
            record.raw_event_type = current.rawType.ToString();
            record.frame = Time.frameCount;
            record.current_event_button = current.button;
            record.current_mouse_x = current.mousePosition.x;
            record.current_mouse_y = current.mousePosition.y;
            record.pointer_delta_x = current.mousePosition.x - expectedPointer.x;
            record.pointer_delta_y = current.mousePosition.y - expectedPointer.y;
            record.hot_control_before = input.hotControlBefore;
            record.hot_control_after = input.hotControlBefore;
            record.observed_event_marker = current.commandName ?? "";
            record.event_marker_matches = markerMatches;
        }
        return true;
    }

    void F024ReplayGuiEndOnGUI()
    {
        var input = f024ReplayGuiCurrent;
        if (input == null || input.injectedEvent == null) return;
        int hotAfter = GUIUtility.hotControl;
        int control = input.type == EventType.MouseDown ? hotAfter : input.hotControlBefore;
        var record = input.record;
        record.event_type_after = Event.current.type.ToString();
        record.current_event_button = input.injectedEvent.button;
        record.event_consumed = Event.current.type == EventType.Used;
        record.hot_control_after = hotAfter;
        record.control_id = control;

        if (input.type == EventType.MouseDown)
        {
            if (input.hotControlBefore != 0 || hotAfter == 0)
                f024ReplayFailure = "生产MouseDown未取得IMGUI hotControl: " + input.target;
            f024ReplayExpectedHotControl = hotAfter;
        }
        else if (input.type == EventType.MouseDrag)
        {
            if (f024ReplayExpectedHotControl == 0 || input.hotControlBefore != f024ReplayExpectedHotControl || hotAfter != f024ReplayExpectedHotControl)
                f024ReplayFailure = "生产MouseDrag没有保持IMGUI hotControl: " + input.target;
        }
        else if (input.type == EventType.MouseUp)
        {
            if (f024ReplayExpectedHotControl == 0 || input.hotControlBefore != f024ReplayExpectedHotControl || hotAfter != 0)
                f024ReplayFailure = "生产MouseUp没有释放IMGUI hotControl: " + input.target;
            f024ReplayExpectedHotControl = 0;
        }
        else if (input.type == EventType.KeyDown || input.type == EventType.KeyUp)
        {
            if (f024ReplayReport != null)
            {
                char character = input.injectedEvent.character;
                f024ReplayReport.steps.Add(String.Format(
                    "collection.search {0} keyCode={1} character=U+{2:X4} focusBefore={3} keyboardBefore={4} focusAfter={5} keyboardAfter={6} query={7}",
                    input.type, input.injectedEvent.keyCode, (int)character,
                    f024ReplayCollectionSearchFocusedControlBefore, f024ReplayCollectionSearchKeyboardControlBefore,
                    f024ReplayCollectionSearchFocusedControl, f024ReplayCollectionSearchKeyboardControl,
                    f024CollectionSearch ?? ""));
            }
        }
        f024ReplayGuiLastClientPosition = input.clientPosition;
        f024ReplayGuiHasLastPosition = true;
        input.queuedEvent = null;
        input.injectedEvent = null;
        input.completed = true;
        f024ReplayGuiCurrent = null;
    }

    void F024ReplayGuiRegisterTarget(string target, Rect rect)
    {
        if (!F024ReplayGuiEnabled || Event.current.type != EventType.Repaint || String.IsNullOrEmpty(target)) return;
        F024ReplayGuiRegisterTargetInDesign(target, rect);
    }

    void F024ReplayGuiRegisterTarget(string target, Rect rect, Rect scrollView, Vector2 scroll)
    {
        if (!F024ReplayGuiEnabled || Event.current.type != EventType.Repaint || String.IsNullOrEmpty(target)) return;
        // Convert scroll content coordinates to the root GUI space used by queued input.
        var rootRect = new Rect(scrollView.x + rect.x - scroll.x, scrollView.y + rect.y - scroll.y,
            rect.width, rect.height);
        F024ReplayGuiRegisterTargetInDesign(target, rootRect);
    }

    void F024ReplayGuiRegisterTargetInDesign(string target, Rect rect)
    {
        // GUI.matrix maps this local control to the window; invert the root matrix to record it in root design space.
        // Keep the local center for queuing so the current matrix is applied exactly once to the native event.
        Matrix4x4 currentMatrix = GUI.matrix;
        Matrix4x4 localToRoot = f024ReplayGuiRootMatrix.inverse * currentMatrix;
        Vector3 localCenter = new Vector3(rect.center.x, rect.center.y, 0f);
        Vector3 rootCenter = localToRoot.MultiplyPoint3x4(localCenter);
        Vector3 rootTopLeft = localToRoot.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin, 0f));
        Vector3 rootTopRight = localToRoot.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMin, 0f));
        Vector3 rootBottomLeft = localToRoot.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMax, 0f));
        Vector3 rootBottomRight = localToRoot.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMax, 0f));
        float minX = Mathf.Min(rootTopLeft.x, rootTopRight.x, rootBottomLeft.x, rootBottomRight.x);
        float maxX = Mathf.Max(rootTopLeft.x, rootTopRight.x, rootBottomLeft.x, rootBottomRight.x);
        float minY = Mathf.Min(rootTopLeft.y, rootTopRight.y, rootBottomLeft.y, rootBottomRight.y);
        float maxY = Mathf.Max(rootTopLeft.y, rootTopRight.y, rootBottomLeft.y, rootBottomRight.y);
        var designRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        var designCenter = new Vector2(rootCenter.x, rootCenter.y);
        Vector2 screenCenter = F024ReplayGuiDesignToScreen(designCenter);
        f024ReplayGuiTargets[target] = new F024ReplayGuiTarget
        {
            rect = rect,
            designRect = designRect,
            screenCenter = screenCenter,
            designCenter = designCenter,
            guiMatrix = currentMatrix,
            frame = Time.frameCount
        };
    }

    void F024ReplayGuiObserveControlBefore(string target, Rect rect)
    {
        var input = f024ReplayGuiCurrent;
        if (!F024ReplayGuiEnabled || input == null || input.injectedEvent == null ||
            input.target != target || input.record == null) return;
        Vector2 mouse = Event.current.mousePosition;
        var record = input.record;
        record.control_event_type = Event.current.type.ToString();
        record.control_raw_event_type = Event.current.rawType.ToString();
        record.control_mouse_x = mouse.x;
        record.control_mouse_y = mouse.y;
        record.control_contains_mouse = rect.Contains(mouse);
        record.control_enabled = GUI.enabled;
        record.control_hot_before = GUIUtility.hotControl;
        record.control_rect_x = rect.x;
        record.control_rect_y = rect.y;
        record.control_rect_width = rect.width;
        record.control_rect_height = rect.height;
    }

    void F024ReplayGuiObserveControlAfter(string target)
    {
        var input = f024ReplayGuiCurrent;
        if (!F024ReplayGuiEnabled || input == null || input.injectedEvent == null ||
            input.target != target || input.record == null) return;
        input.record.control_event_type_after = Event.current.type.ToString();
        input.record.control_hot_after = GUIUtility.hotControl;
    }

    void F024ReplayGuiObserveScroll(string target, string phase, Rect viewport, Vector2 scroll)
    {
        var input = f024ReplayGuiCurrent;
        if (!F024ReplayGuiEnabled || input == null || input.injectedEvent == null ||
            input.target != target || input.record == null) return;
        var record = input.record;
        Vector2 mouse = Event.current.mousePosition;
        if (phase == "before")
        {
            record.scroll_event_before = Event.current.type.ToString();
            record.scroll_before_contains_mouse = viewport.Contains(mouse);
            record.scroll_mouse_x_before = mouse.x;
            record.scroll_mouse_y_before = mouse.y;
            record.scroll_x_before = scroll.x;
            record.scroll_y_before = scroll.y;
            return;
        }
        Vector2 screen = GUIUtility.GUIToScreenPoint(mouse);
        record.scroll_event_after = Event.current.type.ToString();
        record.scroll_after_contains_mouse = new Rect(scroll.x, scroll.y, viewport.width, viewport.height).Contains(mouse);
        record.scroll_mouse_x_after = mouse.x;
        record.scroll_mouse_y_after = mouse.y;
        record.scroll_x_after = scroll.x;
        record.scroll_y_after = scroll.y;
        record.scroll_screen_x_after = screen.x;
        record.scroll_screen_y_after = screen.y;
    }

    bool F024ReplayGuiTryGetTarget(string target, out F024ReplayGuiTarget value)
    {
        return f024ReplayGuiTargets.TryGetValue(target ?? "", out value) &&
            Time.frameCount - value.frame <= 2;
    }

    IEnumerator F024ReplayGuiQueueEvent(EventType type, string target, string coordinateTarget, float timeout = 20f,
        KeyCode keyCode = KeyCode.None, char character = '\0', EventModifiers modifiers = EventModifiers.None)
    {
        if (!F024ReplayGuiEnabled)
        {
            f024ReplayFailure = "GUI replay input requires --account-gui-replay or --account-editor-gui-replay";
            yield break;
        }
        F024ReplayGuiTarget point = null;
        float started = Time.realtimeSinceStartup;
        while (!F024ReplayGuiTryGetTarget(coordinateTarget, out point) &&
               String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if (point == null)
        {
            f024ReplayFailure = "GUI replay control is not visible: " + coordinateTarget;
            yield break;
        }
        if (f024ReplayGuiCurrent != null)
        {
            f024ReplayFailure = "上一条原生GUI事件尚未完成: " + f024ReplayGuiCurrent.target;
            yield break;
        }
        Vector3 windowPoint = point.guiMatrix.MultiplyPoint3x4(
            new Vector3(point.rect.center.x, point.rect.center.y, 0f));
        var input = new F024ReplayGuiEvent
        {
            type = type,
            target = target,
            coordinateTarget = coordinateTarget,
            screenPosition = point.screenCenter,
            designPosition = point.designCenter,
            clientPosition = new Vector2(windowPoint.x, windowPoint.y),
            eventMarker = "F024_GUI_REPLAY_" + (++f024ReplayGuiSequence)
        };
        var queued = new Event();
        queued.type = type;
        queued.button = 0;
        queued.clickCount = 1;
        queued.mousePosition = input.clientPosition;
        queued.keyCode = keyCode;
        queued.character = character;
        queued.modifiers = modifiers;
        queued.delta = f024ReplayGuiHasLastPosition
            ? input.clientPosition - f024ReplayGuiLastClientPosition
            : Vector2.zero;
        queued.commandName = input.eventMarker;
        input.queuedEvent = queued;
        F024ReplayGuiTarget targetRect;
        if (!F024ReplayGuiTryGetTarget(coordinateTarget, out targetRect))
        {
            f024ReplayFailure = "GUI replay control expired before native queue: " + coordinateTarget;
            yield break;
        }
        Vector2 guiPoint = F024ReplayGuiScreenToDesign(input.screenPosition);
        input.record = new F024ReplayGuiInputRecord
        {
            target = target,
            coordinate_target = coordinateTarget,
            expected_event_type = type.ToString(),
            current_event_type = "",
            input_context = f024AccountEditorNativeInputMode
                ? "editor-game-input-queue"
                : (f024AccountEditorGuiReplayMode ? "editor-game-view-send-event"
                    : "unity-event-queue/native-OnGUI"),
            queue_api = f024AccountEditorNativeInputMode
                ? "UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(Event)"
                : (f024AccountEditorGuiReplayMode ? "UnityEditor.EditorWindow.SendEvent(Event)"
                    : "UnityEngine.Event.QueueEvent(Event)"),
            coordinate_space = f024AccountEditorNativeInputMode
                ? "Player render-target pixels passed directly to EditorGUIUtility.QueueGameViewInputEvent"
                : (f024AccountEditorGuiReplayMode ? "GameView window points from runtime target pixels"
                    : "window-client-pixels"),
            event_marker = input.eventMarker,
            queue_frame = Time.frameCount,
            screen_x = input.screenPosition.x,
            screen_y = input.screenPosition.y,
            gui_x = guiPoint.x,
            gui_y = guiPoint.y,
            design_x = input.designPosition.x,
            design_y = input.designPosition.y,
            queued_mouse_x = input.clientPosition.x,
            queued_mouse_y = input.clientPosition.y,
            rect_x = targetRect.designRect.x,
            rect_y = targetRect.designRect.y,
            rect_width = targetRect.designRect.width,
            rect_height = targetRect.designRect.height
        };
        if (f024ReplayReport != null) f024ReplayReport.gui_inputs.Add(input.record);
        if (f024ReplayReport != null && (type == EventType.KeyDown || type == EventType.KeyUp))
            f024ReplayReport.steps.Add(String.Format(
                "collection.search requested {0} keyCode={1} character=U+{2:X4}", type, keyCode, (int)character));
        f024ReplayGuiCurrent = input;
        string queueError;
        bool accepted;
#if UNITY_EDITOR
        if (f024AccountEditorGuiReplayMode)
            accepted = F024ReplayGuiScheduleEditorInput(input, out queueError);
        else
        {
            input.nativeEventQueued = true;
            input.record.native_event_queued = true;
            accepted = F024ReplayGuiTryQueueNativeEvent(input, out queueError);
        }
#else
        input.nativeEventQueued = true;
        input.record.native_event_queued = true;
        accepted = F024ReplayGuiTryQueueNativeEvent(input, out queueError);
#endif
        if (!accepted)
        {
            input.record.queue_error = queueError;
            input.record.native_event_queued = false;
            input.nativeEventQueued = false;
            f024ReplayFailure = f024AccountEditorGuiReplayMode
                ? (f024AccountEditorNativeInputMode
                    ? "EditorGUIUtility.QueueGameViewInputEvent 不能接收回放输入: " + queueError
                    : "Editor GameView.SendEvent 不能接收回放输入: " + queueError)
                : "Unity 原生 Event.QueueEvent 不可用，未执行回放输入: " + queueError;
            input.completed = true;
            f024ReplayGuiCurrent = null;
            yield break;
        }
        started = Time.realtimeSinceStartup;
        while (!input.completed && String.IsNullOrEmpty(f024ReplayFailure) &&
               Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if (!input.completed && String.IsNullOrEmpty(f024ReplayFailure))
        {
            input.record.queue_error = f024AccountEditorGuiReplayMode
                ? (f024AccountEditorNativeInputMode
                    ? "EditorGUIUtility.QueueGameViewInputEvent returned but the Player OnGUI event was not observed before timeout"
                    : "GameView.SendEvent completed but the Player OnGUI event was not observed before timeout")
                : "原生事件在超时前未被OnGUI观察";
            f024ReplayFailure = f024AccountEditorGuiReplayMode
                ? (f024AccountEditorNativeInputMode
                    ? "EditorGUIUtility.QueueGameViewInputEvent未进入匹配的Player OnGUI事件: " + type + " " + target
                    : "Editor GameView.SendEvent未进入匹配的Player OnGUI事件: " + type + " " + target)
                : "原生Event.QueueEvent未进入匹配的OnGUI事件: " + type + " " + target;
        }
    }

    static bool F024ReplayGuiTryQueueNativeEvent(F024ReplayGuiEvent input, out string error)
    {
        error = "";
        try
        {
            if (f024ReplayQueueEventMethod == null)
            {
                f024ReplayQueueEventMethod = typeof(Event).GetMethod("QueueEvent",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { typeof(Event) }, null);
            }
            if (f024ReplayQueueEventMethod == null)
            {
                error = "未找到 UnityEngine.Event.QueueEvent(Event)";
                return false;
            }
            f024ReplayQueueEventMethod.Invoke(null, new object[] { input.queuedEvent });
            return true;
        }
        catch (Exception exception)
        {
            var invoke = exception as TargetInvocationException;
            var cause = invoke != null && invoke.InnerException != null ? invoke.InnerException : exception;
            error = cause.GetType().Name + ": " + cause.Message;
            return false;
        }
    }

#if UNITY_EDITOR
    bool F024ReplayGuiScheduleEditorInput(F024ReplayGuiEvent input, out string error)
    {
        error = "";
        if (input == null || input.queuedEvent == null || input.record == null)
        {
            error = "Editor event input record is incomplete";
            return false;
        }
        input.record.editor_dispatch_schedule = "UnityEditor.EditorApplication.delayCall";
        input.record.editor_input_scheduled = true;
        if (!f024AccountEditorNativeInputMode) input.record.editor_send_event_scheduled = true;
        EditorApplication.delayCall += () => F024ReplayGuiDispatchEditorInput(input);
        return true;
    }

    void F024ReplayGuiDispatchEditorInput(F024ReplayGuiEvent input)
    {
        if (input == null || input.completed || f024ReplayGuiCurrent != input) return;
        input.record.editor_dispatch_frame = Time.frameCount;
        input.record.editor_input_dispatch_invoked = true;
        string error;
        bool queued = f024AccountEditorNativeInputMode
            ? F024ReplayGuiTryQueueGameViewInput(input, out error)
            : F024ReplayGuiTrySendGameViewEvent(input, out error);
        if (queued)
        {
            input.nativeEventQueued = true;
            input.record.native_event_queued = true;
            return;
        }

        input.record.queue_error = error;
        input.record.native_event_queued = false;
        input.nativeEventQueued = false;
        input.completed = true;
        f024ReplayFailure = f024AccountEditorNativeInputMode
            ? "EditorGUIUtility.QueueGameViewInputEvent 不能接收回放输入: " + error
            : "Editor GameView.SendEvent 不能接收回放输入: " + error;
        f024ReplayGuiCurrent = null;
    }

    static bool F024ReplayEditorTryGetProperty<T>(object target, string propertyName, out T value)
    {
        value = default(T);
        if (target == null) return false;
        var property = target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

    static bool F024ReplayEditorTryGetField<T>(object target, string fieldName, out T value)
    {
        value = default(T);
        if (target == null) return false;
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return false;
        try
        {
            object raw = field.GetValue(target);
            if (!(raw is T)) return false;
            value = (T)raw;
            return true;
        }
        catch
        {
            return false;
        }
    }

    bool F024ReplayGuiTrySendGameViewEvent(F024ReplayGuiEvent input, out string error)
    {
        error = "";
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            error = "Editor GameView input requires an unpaused Play Mode";
            return false;
        }
        if (Screen.width != 1920 || Screen.height != 1080)
        {
            error = "Player target must remain 1920x1080; actual " + Screen.width + "x" + Screen.height;
            return false;
        }

        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", false);
        if (gameViewType == null)
        {
            error = "UnityEditor.GameView type is unavailable in this Editor version";
            return false;
        }
        // Unity 2022.3's hasFocus checks HostView.actualView; this selects the active tab without calling Focus or Show.
        var gameView = Resources.FindObjectsOfTypeAll(gameViewType)
            .OfType<EditorWindow>()
            .FirstOrDefault(window => window != null && window.hasFocus);
        if (gameView == null)
        {
            error = "No selected GameView is attached to the current Editor layout; the replay will not open or focus one";
            return false;
        }

        if (input.queuedEvent == null || input.queuedEvent.type != input.type || input.queuedEvent.rawType != input.type)
        {
            error = "Synthetic Editor event lost its requested type before GameView.SendEvent";
            return false;
        }

        Vector2 targetSize, gameMouseOffset;
        Rect targetInView, viewInWindow;
        float gameMouseScale;
        RenderTexture targetTexture;
        if (!F024ReplayEditorTryGetProperty(gameView, "targetRenderSize", out targetSize) ||
            !F024ReplayEditorTryGetProperty(gameView, "targetInView", out targetInView) ||
            !F024ReplayEditorTryGetProperty(gameView, "viewInWindow", out viewInWindow) ||
            !F024ReplayEditorTryGetProperty(gameView, "gameMouseOffset", out gameMouseOffset) ||
            !F024ReplayEditorTryGetProperty(gameView, "gameMouseScale", out gameMouseScale) ||
            !F024ReplayEditorTryGetField(gameView, "m_RenderTexture", out targetTexture))
        {
            error = "Could not read Unity 2022.3 GameView target and input transform";
            return false;
        }
        if (targetTexture == null || !targetTexture.IsCreated())
        {
            error = "Selected GameView has not created its render target yet";
            return false;
        }
        if (Mathf.RoundToInt(targetSize.x) != Screen.width || Mathf.RoundToInt(targetSize.y) != Screen.height)
        {
            error = "GameView render target " + targetSize.x + "x" + targetSize.y +
                " does not match Player Screen " + Screen.width + "x" + Screen.height;
            return false;
        }
        if (targetTexture.width != Screen.width || targetTexture.height != Screen.height)
        {
            error = "GameView RenderTexture " + targetTexture.width + "x" + targetTexture.height +
                " does not match Player Screen " + Screen.width + "x" + Screen.height;
            return false;
        }
        if (gameMouseScale <= 0f)
        {
            error = "GameView reported an invalid mouse scale";
            return false;
        }

        if (input.clientPosition.x < 0f || input.clientPosition.y < 0f ||
            input.clientPosition.x >= targetTexture.width || input.clientPosition.y >= targetTexture.height)
        {
            error = "Production control coordinate is outside the Player render target";
            return false;
        }

        // GameView.OnGUI maps editor points with (mouse + offset) * scale before queuing Player input.
        Vector2 eventPosition = input.clientPosition / gameMouseScale - gameMouseOffset;
        Vector2 mappedBack = (eventPosition + gameMouseOffset) * gameMouseScale;
        if (!viewInWindow.Contains(eventPosition) || (mappedBack - input.clientPosition).sqrMagnitude > 0.25f)
        {
            error = "Mapped GameView event is outside the render view or failed its pixel round trip";
            return false;
        }

        input.queuedEvent.mousePosition = eventPosition;
        input.queuedEvent.delta = f024ReplayGuiHasLastPosition
            ? eventPosition - (f024ReplayGuiLastClientPosition / gameMouseScale - gameMouseOffset)
            : Vector2.zero;
        input.record.game_view_target_width = targetSize.x;
        input.record.game_view_target_height = targetSize.y;
        input.record.game_view_rt_width = targetTexture.width;
        input.record.game_view_rt_height = targetTexture.height;
        input.record.player_screen_width = Screen.width;
        input.record.player_screen_height = Screen.height;
        input.record.game_view_mouse_scale = gameMouseScale;
        input.record.game_view_mouse_offset_x = gameMouseOffset.x;
        input.record.game_view_mouse_offset_y = gameMouseOffset.y;
        input.record.game_view_event_mouse_x = eventPosition.x;
        input.record.game_view_event_mouse_y = eventPosition.y;
        input.record.game_view_view_x = viewInWindow.x;
        input.record.game_view_view_y = viewInWindow.y;
        input.record.game_view_view_width = viewInWindow.width;
        input.record.game_view_view_height = viewInWindow.height;
        input.record.game_view_target_x = targetInView.x;
        input.record.game_view_target_y = targetInView.y;
        input.record.game_view_target_view_width = targetInView.width;
        input.record.game_view_target_view_height = targetInView.height;
        input.record.submitted_event_type = input.queuedEvent.type.ToString();
        input.record.submitted_raw_event_type = input.queuedEvent.rawType.ToString();
        try
        {
            input.record.editor_send_event_invoked = true;
            input.record.editor_send_event_returned = gameView.SendEvent(input.queuedEvent);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    bool F024ReplayGuiTryQueueGameViewInput(F024ReplayGuiEvent input, out string error)
    {
        error = "";
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            error = "Editor GameView input requires an unpaused Play Mode";
            return false;
        }
        if (Screen.width != 1920 || Screen.height != 1080)
        {
            error = "Player target must remain 1920x1080; actual " + Screen.width + "x" + Screen.height;
            return false;
        }

        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", false);
        if (gameViewType == null)
        {
            error = "UnityEditor.GameView type is unavailable in this Editor version";
            return false;
        }
        var gameView = Resources.FindObjectsOfTypeAll(gameViewType)
            .OfType<EditorWindow>()
            .FirstOrDefault(window => window != null && window.hasFocus);
        if (gameView == null)
        {
            error = "No selected GameView is attached to the current Editor layout; the replay will not open or focus one";
            return false;
        }
        if (input.queuedEvent == null || input.queuedEvent.type != input.type || input.queuedEvent.rawType != input.type)
        {
            error = "Synthetic Editor event lost its requested type before QueueGameViewInputEvent";
            return false;
        }

        Vector2 targetSize;
        RenderTexture targetTexture;
        int targetDisplay;
        if (!F024ReplayEditorTryGetProperty(gameView, "targetRenderSize", out targetSize) ||
            !F024ReplayEditorTryGetProperty(gameView, "targetDisplay", out targetDisplay) ||
            !F024ReplayEditorTryGetField(gameView, "m_RenderTexture", out targetTexture))
        {
            error = "Could not read Unity 2022.3 GameView render target or display index";
            return false;
        }
        if (targetTexture == null || !targetTexture.IsCreated())
        {
            error = "Selected GameView has not created its render target yet";
            return false;
        }
        if (Mathf.RoundToInt(targetSize.x) != Screen.width || Mathf.RoundToInt(targetSize.y) != Screen.height ||
            targetTexture.width != Screen.width || targetTexture.height != Screen.height)
        {
            error = "GameView target and RenderTexture must match Player Screen 1920x1080; target=" +
                targetSize.x + "x" + targetSize.y + ", RT=" + targetTexture.width + "x" + targetTexture.height +
                ", Player=" + Screen.width + "x" + Screen.height;
            return false;
        }
        if (input.clientPosition.x < 0f || input.clientPosition.y < 0f ||
            input.clientPosition.x >= targetTexture.width || input.clientPosition.y >= targetTexture.height)
        {
            error = "Production control coordinate is outside the Player render target";
            return false;
        }

        input.queuedEvent.mousePosition = input.clientPosition;
        input.queuedEvent.delta = f024ReplayGuiHasLastPosition
            ? input.clientPosition - f024ReplayGuiLastClientPosition
            : Vector2.zero;
        input.queuedEvent.displayIndex = targetDisplay;
        input.record.editor_target_display = targetDisplay;
        input.record.game_view_target_width = targetSize.x;
        input.record.game_view_target_height = targetSize.y;
        input.record.game_view_rt_width = targetTexture.width;
        input.record.game_view_rt_height = targetTexture.height;
        input.record.player_screen_width = Screen.width;
        input.record.player_screen_height = Screen.height;
        input.record.game_view_event_mouse_x = input.queuedEvent.mousePosition.x;
        input.record.game_view_event_mouse_y = input.queuedEvent.mousePosition.y;
        input.record.submitted_event_type = input.queuedEvent.type.ToString();
        input.record.submitted_raw_event_type = input.queuedEvent.rawType.ToString();
        try
        {
            input.record.editor_game_input_queue_invoked = true;
            EditorGUIUtility.QueueGameViewInputEvent(input.queuedEvent);
            input.record.editor_game_input_queue_returned = true;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

#endif

    IEnumerator F024ReplayGuiClick(string target, string handler, float timeout = 20f)
    {
        f024ReplayLastGuiHandler = "";
        yield return F024ReplayGuiQueueEvent(EventType.MouseDown, target, target, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseDrag, target, target, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseUp, target, target, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f024ReplayLastGuiHandler != handler)
            f024ReplayFailure = "GUI replay clicked " + target + " but production handler was " + f024ReplayLastGuiHandler;
    }

    static bool F024ReplayGuiTryMapAsciiKey(char character, out KeyCode keyCode, out EventModifiers modifiers)
    {
        keyCode = KeyCode.None;
        modifiers = EventModifiers.None;
        try
        {
            if (character >= 'a' && character <= 'z')
            {
                keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), Char.ToUpperInvariant(character).ToString());
                return true;
            }
            if (character >= 'A' && character <= 'Z')
            {
                keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), character.ToString());
                modifiers = EventModifiers.Shift;
                return true;
            }
            if (character >= '0' && character <= '9')
            {
                keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + character);
                return true;
            }
            if (character == '_')
            {
                keyCode = KeyCode.Minus;
                modifiers = EventModifiers.Shift;
                return true;
            }
            if (character == '-')
            {
                keyCode = KeyCode.Minus;
                return true;
            }
        }
        catch
        {
            keyCode = KeyCode.None;
            modifiers = EventModifiers.None;
        }
        return false;
    }

    IEnumerator F024ReplayGuiTypeSearchText(string value, float timeout = 20f)
    {
        const string target = "collection.search.input";
        if (String.IsNullOrEmpty(value) || value.Length > 48)
        {
            f024ReplayFailure = "GUI search replay requires 1-48 ASCII characters";
            yield break;
        }
        for (int i = 0; i < value.Length; i++)
        {
            KeyCode ignoredKeyCode;
            EventModifiers ignoredModifiers;
            if (value[i] > 0x7f || !F024ReplayGuiTryMapAsciiKey(value[i], out ignoredKeyCode, out ignoredModifiers))
            {
                f024ReplayFailure = "GUI search replay only supports ASCII CardIds; unsupported character at index " + i;
                yield break;
            }
        }

        yield return F024ReplayGuiClick(target, "FocusCollectionSearch", timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f024ReplayCollectionSearchFocusedControl != target || f024ReplayCollectionSearchKeyboardControl == 0)
        {
            f024ReplayFailure = "GUI search click did not focus the named TextField: " +
                f024ReplayCollectionSearchFocusedControl + " control=" + f024ReplayCollectionSearchKeyboardControl;
            yield break;
        }
        if (f024ReplayReport != null)
            f024ReplayReport.steps.Add("collection.search focus assertion passed: " + target +
                " keyboardControl=" + f024ReplayCollectionSearchKeyboardControl);

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            KeyCode keyCode;
            EventModifiers modifiers;
            F024ReplayGuiTryMapAsciiKey(character, out keyCode, out modifiers);
            yield return F024ReplayGuiQueueEvent(EventType.KeyDown, target, target, timeout,
                keyCode, character, modifiers);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F024ReplayGuiQueueEvent(EventType.KeyUp, target, target, timeout,
                keyCode, '\0', modifiers);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024ReplayCollectionSearchFocusedControl != target || f024ReplayCollectionSearchKeyboardControl == 0)
            {
                f024ReplayFailure = "GUI search lost TextField focus while typing at character " + i;
                yield break;
            }
        }
        if (!String.Equals(f024CollectionSearch, value, StringComparison.Ordinal))
            f024ReplayFailure = "GUI keyboard search text mismatch: expected " + value + ", got " + f024CollectionSearch;
        else if (f024ReplayReport != null)
            f024ReplayReport.steps.Add("collection.search keyboard query assertion passed: " + f024CollectionSearch);
    }

    IEnumerator F024ReplayGuiPackDrag(int setId, bool releaseInside, float timeout = 20f)
    {
        string target = "packs.drag:" + setId;
        string source = "packs.source:" + setId;
        string destination = releaseInside ? "packs.slot" : "packs.outside";
        f024ReplayLastGuiHandler = "";
        yield return F024ReplayGuiQueueEvent(EventType.MouseDown, target, source, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseDrag, target, destination, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseUp, target, destination, timeout);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        string expected = releaseInside ? "F024OpenPackFromDrag" : "";
        if (f024ReplayLastGuiHandler != expected)
            f024ReplayFailure = "GUI pack drag handler mismatch: expected " + expected + ", got " + f024ReplayLastGuiHandler;
    }

    IEnumerator F028ReplayCardDrag(string source, string destination, string handler)
    {
        f024ReplayLastGuiHandler = "";
        yield return F024ReplayGuiQueueEvent(EventType.MouseDown, source, source);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseDrag, source, destination);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayGuiQueueEvent(EventType.MouseUp, source, destination);
        if (String.IsNullOrEmpty(f024ReplayFailure) && f024ReplayLastGuiHandler != handler)
            f024ReplayFailure = "Card drag handler mismatch: " + source + " -> " + destination;
    }

    IEnumerator F024ReplayGuiNavigate(string menuTarget, string handler, string targetPage)
    {
        yield return F025ReplayGuiCloseOverlays();
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (page == targetPage)
        {
            yield return F024ReplayWaitForSettled(20);
            yield break;
        }
        if (page != "menu")
        {
            string backTarget = page == "account" ? "account.back" : "page.back";
            string backHandler = page == "account" ? "ReturnFromAccountPage" : "ReturnToMenuAndRefreshAccount";
            yield return F024ReplayGuiClick(backTarget, backHandler);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F024ReplayWaitForSettled(20);
        }
        yield return F024ReplayGuiClick(menuTarget, handler);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (page != targetPage)
            f024ReplayFailure = "GUI navigation did not reach " + targetPage + " (current page " + page + ")";
        if (String.IsNullOrEmpty(f024ReplayFailure)) yield return F024ReplayWaitForSettled(20);
    }

    IEnumerator F024ReplayGuiSelectCollectionCard(string cardId, int ownedMode = 1)
    {
        yield return F025ReplayGuiCloseOverlays();
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        CardDef card;
        if (!cards.TryGetValue(cardId ?? "", out card))
        {
            f024ReplayFailure = "GUI collection replay could not find card " + cardId;
            yield break;
        }
        f024CollectionTab = 0;
        f024CollectionOwnedMode = ownedMode;
        f024CollectionPage = 0;
        f024CollectionSearch = cardId;
        yield return null;
        yield return null;
        if (!f024FilteredCards.Any(item => item.CardId == cardId))
        {
            f024ReplayFailure = "GUI collection search did not show opened card " + cardId;
            yield break;
        }
        yield return F024ReplayGuiClick("collection.card:" + cardId, "SelectCollectionCard");
    }

    IEnumerator F024ReplayGuiVerifyCollectionFilters()
    {
        int savedTab = f024CollectionTab;
        int savedClass = f024CollectionClass;
        int savedMana = f024CollectionMana;
        int savedSet = f024CollectionSet;
        int savedOwnedMode = f024CollectionOwnedMode;
        bool savedCraftingMode = f029CraftingMode;
        int savedPage = f024CollectionPage;
        string savedSearch = f024CollectionSearch;
        string savedSelectedCardId = f024SelectedCardId;
        try
        {
            if (page != "collection")
            {
                f024ReplayFailure = "GUI collection filter replay requires the collection page";
                yield break;
            }

            // Establish a broad in-memory baseline; each tested filter is then changed and restored by native GUI clicks.
            f024CollectionTab = 0;
            f024CollectionClass = -1;
            f024CollectionMana = -1;
            f024CollectionSet = -1;
            f024CollectionOwnedMode = 0;
            f029CraftingMode = false;
            f024CollectionPage = 0;
            f024CollectionSearch = "";
            f024CollectionCacheKey = "";
            yield return null;
            yield return null;

            F024BuildCollectionList();
            CardDef searchCard = null;
            foreach (var candidate in f024FilteredCards ?? new CardDef[0])
            {
                if (candidate == null || String.IsNullOrEmpty(candidate.CardId) || candidate.CardId.Length > 48) continue;
                bool supported = true;
                for (int i = 0; i < candidate.CardId.Length; i++)
                {
                    KeyCode keyCode;
                    EventModifiers modifiers;
                    if (candidate.CardId[i] > 0x7f || !F024ReplayGuiTryMapAsciiKey(candidate.CardId[i], out keyCode, out modifiers))
                    {
                        supported = false;
                        break;
                    }
                }
                if (supported)
                {
                    searchCard = candidate;
                    break;
                }
            }
            if (searchCard == null)
            {
                f024ReplayFailure = "GUI search replay has no visible ASCII CardId to type";
                yield break;
            }
            string typedCardId = searchCard.CardId;
            yield return F024ReplayGuiTypeSearchText(typedCardId);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            F024BuildCollectionList();
            if (!String.Equals(f024CollectionSearch, typedCardId, StringComparison.Ordinal) ||
                f024FilteredCards == null || !f024FilteredCards.Any(item => item != null && item.CardId == typedCardId))
            {
                f024ReplayFailure = "GUI keyboard search did not filter to the typed CardId " + typedCardId;
                yield break;
            }
            if (f024ReplayReport != null)
                f024ReplayReport.steps.Add("collection.search typed CardId assertion passed: " + typedCardId);
            yield return F024ReplayGuiClick("collection.search.clear", "ClearCollectionSearch");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (!String.IsNullOrEmpty(f024CollectionSearch))
            {
                f024ReplayFailure = "GUI search clear button did not empty the query";
                yield break;
            }
            yield return null;
            yield return null;
            F024BuildCollectionList();
            if (!String.IsNullOrEmpty(f024CollectionSearch))
            {
                f024ReplayFailure = "GUI search query was repopulated after clicking clear";
                yield break;
            }
            if (f024ReplayReport != null)
                f024ReplayReport.steps.Add("collection.search clear-button assertion passed");

            if (database.classes == null || database.classes.Length == 0)
            {
                f024ReplayFailure = "GUI collection filter replay has no hero class to select";
                yield break;
            }
            int selectedClass = database.classes[0].id;
            yield return F024ReplayGuiClick("collection.filter.class." + selectedClass, "ChangeCollectionClassFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionClass != selectedClass)
            {
                f024ReplayFailure = "GUI collection class filter click did not select the hero class";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.filter.class.all", "ChangeCollectionClassFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionClass != -1)
            {
                f024ReplayFailure = "GUI collection class filter did not restore all classes";
                yield break;
            }

            yield return F024ReplayGuiClick("collection.crafting-mode", "ToggleCollectionCraftingMode");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (!f029CraftingMode)
            {
                f024ReplayFailure = "GUI collection crafting mode did not open";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.filter.ownership.1", "ChangeCollectionOwnershipFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionOwnedMode != 1)
            {
                f024ReplayFailure = "GUI collection ownership filter click did not select owned cards";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.filter.ownership.2", "ChangeCollectionOwnershipFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionOwnedMode != 2)
            {
                f024ReplayFailure = "GUI collection ownership filter did not select unowned cards";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.crafting-mode", "ToggleCollectionCraftingMode");
            if (!String.IsNullOrEmpty(f024ReplayFailure) || f029CraftingMode || f024CollectionOwnedMode != 0)
            {
                f024ReplayFailure = "GUI collection crafting mode did not reset ownership";
                yield break;
            }

            int[] sets = F024CollectionSetIds();
            if (sets == null || sets.Length == 0)
            {
                f024ReplayFailure = "GUI collection filter replay has no expansion sets to select";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.set.dropdown", "ToggleCollectionSetDropdown");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (!f024CollectionSetDropdownOpen)
            {
                f024ReplayFailure = "GUI collection expansion dropdown did not open";
                yield break;
            }
            float trayDeadline = Time.realtimeSinceStartup + 3f;
            while (f031SetTrayAnimating && Time.realtimeSinceStartup < trayDeadline) yield return null;
            if (f031SetTrayAnimating || f031SetTrayProgress < .999f)
            {
                f024ReplayFailure = "GUI collection expansion dropdown opening animation did not finish";
                yield break;
            }
            yield return F024ReplayCapture("account-replay-collection-set-dropdown");
            yield return F024ReplayGuiClick("collection.set.item." + sets[0], "ChangeCollectionSet");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionSet != sets[0] || f024CollectionSetDropdownOpen)
            {
                f024ReplayFailure = "GUI collection expansion dropdown did not apply the selected set";
                yield break;
            }
            while (f031SetTrayAnimating) yield return null;
            yield return F024ReplayGuiClick("collection.set.dropdown", "ToggleCollectionSetDropdown");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F024ReplayGuiClick("collection.set.item.all", "ChangeCollectionSet");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionSet != -1 || f024CollectionSetDropdownOpen)
            {
                f024ReplayFailure = "GUI collection expansion dropdown did not restore all sets";
                yield break;
            }

            yield return F024ReplayGuiClick("collection.filter.mana.7", "ChangeCollectionManaFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionMana != 7)
            {
                f024ReplayFailure = "GUI collection mana filter click did not select 7+ mana";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.filter.mana.7", "ChangeCollectionManaFilter");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionMana != -1)
            {
                f024ReplayFailure = "GUI collection mana filter did not restore all costs";
                yield break;
            }

            f024CollectionCacheKey = "";
            F024BuildCollectionList();
            if (f024FilteredCards == null || f024FilteredCards.Length <= F024CollectionPageSize)
            {
                f024ReplayFailure = "GUI collection filter replay cannot verify pagination because the full card list has one page";
                yield break;
            }
            if (f024CollectionPage != 0)
            {
                f024ReplayFailure = "GUI collection pagination baseline was not page one";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.page.next", "ChangeCollectionPage");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionPage != 1)
            {
                f024ReplayFailure = "GUI collection next-page click did not advance the page";
                yield break;
            }
            float flipDeadline = Time.realtimeSinceStartup + 5f;
            while (f032PageTurnPending && Time.realtimeSinceStartup < flipDeadline) yield return null;
            if (!f032PageTurnActive || !f032PageTurnRight || f032PageTurn == null || f032PageSnapshot == null)
            {
                f024ReplayFailure = "GUI collection page snapshot or skinned animation did not start";
                yield break;
            }
            float visualSample = Time.realtimeSinceStartup + .08f;
            while (f032PageTurnActive && Time.realtimeSinceStartup < visualSample) yield return null;
            yield return F024ReplayCapture("account-replay-collection-page-turn");
            while ((f032PageTurnPending || f032PageTurnActive) && Time.realtimeSinceStartup < flipDeadline) yield return null;
            if (f032PageTurnPending || f032PageTurnActive)
            {
                f024ReplayFailure = "GUI collection next-page animation did not finish";
                yield break;
            }
            yield return F024ReplayGuiClick("collection.page.previous", "ChangeCollectionPage");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024CollectionPage != 0)
            {
                f024ReplayFailure = "GUI collection previous-page click did not restore page one";
                yield break;
            }
            flipDeadline = Time.realtimeSinceStartup + 5f;
            while (f032PageTurnPending && Time.realtimeSinceStartup < flipDeadline) yield return null;
            if (!f032PageTurnActive || f032PageTurnRight || f032PageSnapshot == null)
            {
                f024ReplayFailure = "GUI collection previous-page animation did not start";
                yield break;
            }
            float reverseSample = Time.realtimeSinceStartup + .08f;
            while (f032PageTurnActive && Time.realtimeSinceStartup < reverseSample) yield return null;
            yield return F024ReplayCapture("account-replay-collection-page-turn-back");
            while ((f032PageTurnPending || f032PageTurnActive) && Time.realtimeSinceStartup < flipDeadline) yield return null;
            if (f032PageTurnPending || f032PageTurnActive)
            {
                f024ReplayFailure = "GUI collection previous-page animation did not finish";
                yield break;
            }
            F024BuildCollectionList();
            CardDef relatedSource = (f024FilteredCards ?? new CardDef[0]).FirstOrDefault(item =>
                item != null && item.RelatedCardIds != null &&
                item.RelatedCardIds.Any(id => !String.IsNullOrEmpty(id) && id != item.CardId && cards.ContainsKey(id)));
            if (relatedSource == null)
            {
                f024ReplayFailure = "GUI collection replay has no card with a resolvable related card";
                yield break;
            }
            f024CollectionSearch = relatedSource.CardId;
            f024CollectionCacheKey = "";
            yield return null;
            yield return null;
            yield return F024ReplayGuiClick("collection.card:" + relatedSource.CardId, "SelectCollectionCard");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (!f025CollectionDetailOpen || f024SelectedCardId != relatedSource.CardId)
            {
                f024ReplayFailure = "GUI related-card example did not open its detail overlay";
                yield break;
            }
            if (f024ReplayReport != null)
                f024ReplayReport.steps.Add("collection related detail example: " + relatedSource.CardId);
            yield return F024ReplayCapture("account-replay-collection-related");
            yield return F024ReplayGuiClick("collection.detail.close", "CloseCollectionDetail");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024ReplayReport != null)
                f024ReplayReport.steps.Add("收藏原生 GUI 回放已验证 CardId 键盘搜索与 X 清空、扩展包、费用、拥有状态及分页筛选");
        }
        finally
        {
            f024CollectionTab = savedTab;
            f024CollectionClass = savedClass;
            f024CollectionMana = savedMana;
            f024CollectionSet = savedSet;
            f024CollectionOwnedMode = savedOwnedMode;
            f029CraftingMode = savedCraftingMode;
            f024CollectionPage = savedPage;
            f024CollectionSearch = savedSearch;
            f024SelectedCardId = savedSelectedCardId;
            f024CollectionCacheKey = "";
        }
    }

    IEnumerator F025ReplayGuiCloseOverlays()
    {
        if (f025SettingsOpen)
            yield return F024ReplayGuiClick("settings.close", "CloseSettings");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f025CollectionDetailOpen)
            yield return F024ReplayGuiClick("collection.detail.close", "CloseCollectionDetail");
        if (String.IsNullOrEmpty(f024ReplayFailure)) yield return F024ReplayWaitForSettled(20);
    }

    IEnumerator F025ReplayGuiOpenAccountSettings()
    {
        if (f025CollectionDetailOpen)
            yield return F024ReplayGuiClick("collection.detail.close", "CloseCollectionDetail");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (!f025SettingsOpen)
            yield return F024ReplayGuiClick("settings.open", "OpenSettings");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f025SettingsTab == 0)
            yield return F024ReplayGuiClick("settings.options", "OpenOptions");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f025SettingsTab != 2)
            yield return F024ReplayGuiClick("settings.account", "OpenAccountSettings");
        if (String.IsNullOrEmpty(f024ReplayFailure)) yield return F024ReplayWaitForSettled(20);
    }

    void F024ReplayPreparePackViewport(int setId)
    {
        var packs = ((f024Account == null ? null : f024Account.packs) ?? new F024AccountPack[0])
            .Where(item => item != null && item.count > 0).ToArray();
        int index = Array.FindIndex(packs, item => item.set_id == setId);
        if (index < 0) return;
        f024SelectedPackSet = setId;
        int maxScroll = Mathf.Max(0, packs.Length * 118 - 520);
        f024PackListScroll = new Vector2(0, Mathf.Clamp(index * 118 - 354, 0, maxScroll));
    }

    void F024ReplayPrepareShopViewport(int setId)
    {
        var items = ((f024Account == null ? null : f024Account.shop) ?? new F024AccountShopItem[0])
            .Where(item => item != null).OrderBy(item => item.name, StringComparer.OrdinalIgnoreCase).ToArray();
        int index = Array.FindIndex(items, item => item.set_id == setId);
        if (index < 0) return;
        int maxScroll = Mathf.Max(0, Mathf.CeilToInt(items.Length / 3f) * 330 - 655);
        f024ShopScroll = new Vector2(0, Mathf.Clamp((index / 3) * 330 - 160, 0, maxScroll));
    }

    IEnumerator F024ReplayGuiWaitForDeck(string deckId, float timeout = 20f)
    {
        var decks = ((f024Account == null ? null : f024Account.decks) ?? new F024AccountDeck[0])
            .Where(item => item != null).ToArray();
        int index = Array.FindIndex(decks, item => item.deck_id == deckId);
        if (index < 0)
        {
            f024ReplayFailure = "GUI replay server deck is missing: " + deckId;
            yield break;
        }
        int maxScroll = Mathf.Max(0, decks.Length * 66 - 630);
        f024DeckListScroll = new Vector2(0, Mathf.Clamp(index * 66 - 528, 0, maxScroll));
        float started = Time.realtimeSinceStartup;
        F024ReplayGuiTarget target;
        string control = "decks.select:" + deckId;
        while (!F024ReplayGuiTryGetTarget(control, out target) && String.IsNullOrEmpty(f024ReplayFailure) &&
               Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if (!F024ReplayGuiTryGetTarget(control, out target) && String.IsNullOrEmpty(f024ReplayFailure))
            f024ReplayFailure = "GUI replay deck row is not visible: " + deckId;
    }

    void F024ReplayGuiMarkHandler(string target, string handler)
    {
        if (!F024ReplayGuiEnabled || f024ReplayGuiCurrent == null ||
            f024ReplayGuiCurrent.injectedEvent == null || !f024ReplayGuiCurrent.nativeEventObserved ||
            f024ReplayGuiCurrent.type != EventType.MouseUp || f024ReplayGuiCurrent.target != target) return;
        f024ReplayLastGuiHandler = handler ?? "";
        f024ReplayGuiCurrent.handler = f024ReplayLastGuiHandler;
        if (f024ReplayGuiCurrent.record != null) f024ReplayGuiCurrent.record.handler = f024ReplayLastGuiHandler;
    }

    void F024ReplayGuiAttachRequest(string requestId)
    {
        if (!F024ReplayGuiEnabled || f024ReplayGuiCurrent == null ||
            f024ReplayGuiCurrent.injectedEvent == null || !f024ReplayGuiCurrent.nativeEventObserved ||
            f024ReplayGuiCurrent.type != EventType.MouseUp || f024ReplayGuiCurrent.record == null) return;
        f024ReplayGuiCurrent.record.request_id = requestId ?? "";
    }

}
