using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// V002 独立探针。临时复制到 Assets/Editor 后使用 V002R1LateNoneProbe.Run。
public static class V002R1LateNoneProbe
{
    public static void Run()
    {
        var root = new GameObject("V002 host");
        var target = new GameObject("V002 target");
        var model = new GameObject("V002 clone");
        NativeSuperImpact flow = null;
        IEnumerator scheduled = null;
        Action stateStarted = null;
        Action finished = null;
        int state = 5, attempts = 0, finishes = 0;
        bool releaseThrows = true;
        var errors = new List<string>();
        var clone = new NativeAreaClone
        {
            Object = model, AddStateStarted = callback => stateStarted = callback,
            AddFinished = callback => finished = callback, ActiveState = () => state,
            SetSource = value => { }, SetLocation = (location, parent) => { },
            UpdatePosition = () => { }, UpdateOrientation = () => { }, Activate = () => { }
        };
        var services = new NativeSuperImpactServices
        {
            Root = root, Source = () => root, Targets = new List<GameObject> { target },
            IsValidTarget = value => value != null, MetaDataIndex = index => -1,
            RandomRange = (min, max) => min,
            StartCoroutine = body => { scheduled = body; return body.MoveNext() ? body : null; },
            GetSpell = prefab => new NativeImpactClone { Clone = clone, AddTarget = value => { } },
            FinishIfPossible = () => finishes++,
            Error = (index, stage, error) => errors.Add(stage + ":" + error.Message),
            Release = (value, reset) =>
            {
                attempts++;
                if (releaseThrows) throw new InvalidOperationException("release-attempt-" + attempts);
                return true;
            }
        };
        try
        {
            flow = new NativeSuperImpact(new NativeImpactInfo
            {
                enabled = true, prefab = new NativeImpactPrefab { prefab_reference = "independent-probe" }
            }, services);
            flow.Spawn(0);
            if (flow.Pending != 1 || !(scheduled.Current is WaitForSeconds))
                throw new Exception("Invalid fixture: Spawn did not yield the zero-second wait");
            scheduled.MoveNext();
            if (stateStarted == null || finished == null || flow.ActiveClones.Count != 1)
                throw new Exception("Invalid fixture: clone callbacks were not registered");

            flow.CancelAll();
            int errorsBefore = errors.Count;
            state = 0;
            stateStarted();
            bool diagnosticDelivered = errors.Count == errorsBefore + 1 &&
                flow.LastError != null && flow.LastError.Message == "release-attempt-2";
            Debug.Log("V002_LATE_NONE attempts=" + attempts + " errorsBefore=" + errorsBefore +
                " errorsAfter=" + errors.Count + " lastError=" + flow.LastError?.GetType().Name +
                " lastMessage=" + flow.LastError?.Message + " retained=" + flow.ActiveClones.Count +
                " diagnosticDelivered=" + diagnosticDelivered);
            Debug.Log("V002_REPORTED_ERRORS " + String.Join(" | ", errors));

            releaseThrows = false;
            flow.CancelAll();
            finished();
            stateStarted();
            bool recovered = attempts == 3 && flow.ActiveClones.Count == 0 && flow.Pending == 0 &&
                flow.Cancelled == 1 && flow.Faulted == 0 && finishes == 0;
            Debug.Log("V002_EXPLICIT_RETRY recovered=" + recovered + " attempts=" + attempts +
                " pending=" + flow.Pending + " cancelled=" + flow.Cancelled + " finishes=" + finishes);
            if (!recovered) throw new Exception("Explicit retry/late callback cleanup failed");
            if (!diagnosticDelivered)
                throw new Exception("V002-R1 FAIL: late None release error after cancellation was not reported");
        }
        finally
        {
            releaseThrows = false;
            flow?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(target);
            if (model != null) UnityEngine.Object.DestroyImmediate(model);
        }
    }
}
