using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 独立验证：复制到 Assets/Editor 后调用 V002R2StoppedProbe.Run。
public static class V002R2StoppedProbe
{
    sealed class Item
    {
        public GameObject model;
        public NativeAreaClone clone;
        public Action finished, started;
        public int state = 5, attempts;
    }

    sealed class Fixture : IDisposable
    {
        public readonly GameObject root = new GameObject("V002-r2 host");
        public readonly GameObject target = new GameObject("V002-r2 target");
        public readonly List<Item> items = new List<Item>();
        public readonly List<IEnumerator> scheduled = new List<IEnumerator>();
        public readonly List<string> errors = new List<string>();
        public NativeSuperImpact flow;
        public NativeSuperImpactServices services;
        public int finishes;

        public Fixture(int count)
        {
            var targets = new List<GameObject>();
            for (int i = 0; i < count; i++) targets.Add(target);
            services = new NativeSuperImpactServices
            {
                Root = root, Source = () => root, Targets = targets,
                IsValidTarget = value => value != null, MetaDataIndex = index => -1,
                RandomRange = (min, max) => min,
                StartCoroutine = body =>
                {
                    scheduled.Add(body);
                    return body.MoveNext() ? body : null;
                },
                GetSpell = prefab =>
                {
                    var item = new Item { model = new GameObject("V002-r2 clone") };
                    item.clone = new NativeAreaClone
                    {
                        Object = item.model, AddFinished = callback => item.finished = callback,
                        AddStateStarted = callback => item.started = callback,
                        ActiveState = () => item.state, SetSource = value => { },
                        SetLocation = (location, parent) => { }, UpdatePosition = () => { },
                        UpdateOrientation = () => { }, Activate = () => { }
                    };
                    items.Add(item);
                    return new NativeImpactClone { Clone = item.clone, AddTarget = value => { } };
                },
                FinishIfPossible = () => finishes++,
                Error = (index, stage, error) => errors.Add(index + ":" + stage + ":" + error.Message),
                Release = (clone, reset) =>
                {
                    Require(!reset, "release must not reset");
                    var item = items.Find(value => value.clone == clone);
                    item.attempts++;
                    return false;
                }
            };
            flow = new NativeSuperImpact(new NativeImpactInfo
            {
                enabled = true, prefab = new NativeImpactPrefab { prefab_reference = "v002-r2" }
            }, services);
            flow.SpawnAll();
            Require(flow.Pending == count && items.Count == 0, "zero delay must still yield");
            foreach (var body in scheduled.ToArray()) body.MoveNext();
            Require(flow.Pending == count && items.Count == count, "fixture must own separate clones");
        }

        public void None(Item item) { item.state = 0; item.started(); }
        public void Dispose()
        {
            services.Error = (index, stage, error) => { };
            services.Release = (clone, reset) => false;
            flow.Dispose();
            flow.CancelAll();
            foreach (var item in items) UnityEngine.Object.DestroyImmediate(item.model);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void Require(bool value, string reason)
    { if (!value) throw new Exception("V002-r2: " + reason); }

    public static void Run()
    {
        int cases = 0;
        foreach (string stop in new[] { "cancel", "dispose", "fault" })
        foreach (string observer in new[] { "ordinary", "retry", "throw" })
        foreach (bool finishFirst in new[] { false, true })
        using (var fixture = new Fixture(1))
        {
            var item = fixture.items[0];
            var lateError = new InvalidOperationException("late-release");
            fixture.services.Release = (clone, reset) =>
            {
                Require(!reset, "reset changed");
                item.attempts++;
                if (item.attempts == 1) throw new InvalidOperationException("first-release");
                if (item.attempts == 2) throw lateError;
                return false;
            };
            if (stop == "cancel") fixture.flow.CancelAll();
            else if (stop == "dispose") fixture.flow.Dispose();
            else
            {
                fixture.services.FinishIfPossible = () => { throw new InvalidOperationException("finish-fault"); };
                item.finished();
            }
            Require(item.attempts == 1 && fixture.flow.Pending == 0 && fixture.flow.ActiveClones.Count == 1,
                "initial stop must retain failed release");
            int before = fixture.errors.Count;
            bool correctDiagnosis = false;
            fixture.services.Error = (index, stage, error) =>
            {
                fixture.errors.Add(index + ":" + stage + ":" + error.Message);
                correctDiagnosis = index == 0 && stage == "release" && ReferenceEquals(error, lateError);
                if (observer == "retry")
                {
                    fixture.flow.CancelAll();
                    item.finished();
                    fixture.None(item);
                }
                if (observer == "throw") throw new InvalidOperationException("observer-fault");
            };
            if (finishFirst) item.finished();
            fixture.None(item);
            if (!finishFirst) item.finished();
            Require(correctDiagnosis && fixture.errors.Count == before + 1 && ReferenceEquals(fixture.flow.LastError, lateError),
                "late error must be delivered exactly once");
            Require(fixture.flow.Pending == 0 && fixture.finishes == 0 &&
                fixture.flow.Cancelled == (stop == "fault" ? 0 : 1) &&
                fixture.flow.Faulted == (stop == "fault" ? 1 : 0), "late callbacks must not settle twice");
            Require(fixture.flow.DiagnosticFailures == (observer == "throw" ? 1 : 0),
                "observer exception must be isolated");
            Require(item.attempts == (observer == "retry" ? 3 : 2) &&
                fixture.flow.ActiveClones.Count == (observer == "retry" ? 0 : 1),
                "diagnostic reentry must have bounded release ownership");
            fixture.flow.CancelAll();
            for (int i = 0; i < 3; i++) { item.finished(); fixture.None(item); fixture.flow.CancelAll(); }
            Require(item.attempts == 3 && fixture.flow.ActiveClones.Count == 0 &&
                fixture.flow.Pending == 0 && fixture.errors.Count == before + 1 && fixture.finishes == 0,
                "recovered request must ignore repeated completion/None/cancel");
            Debug.Log("V002_R2_CASE " + stop + "/" + observer + "/finishFirst=" + finishFirst + " PASS");
            cases++;
        }

        using (var fixture = new Fixture(2))
        {
            var stopped = fixture.items[0];
            var peer = fixture.items[1];
            fixture.services.Release = (clone, reset) =>
            {
                Require(!reset, "peer release reset");
                var item = fixture.items.Find(value => value.clone == clone);
                item.attempts++;
                if (item == stopped && item.attempts <= 2)
                    throw new InvalidOperationException(item.attempts == 1 ? "first-release" : "late-release");
                return false;
            };
            fixture.services.FinishIfPossible = () => { throw new InvalidOperationException("finish-fault"); };
            stopped.finished();
            Require(fixture.flow.Pending == 1 && fixture.flow.Faulted == 1 && fixture.flow.Cancelled == 0,
                "fault must leave peer pending");
            fixture.services.Error = (index, stage, error) =>
            {
                fixture.errors.Add(index + ":" + stage + ":" + error.Message);
                if (index == 0 && error.Message == "late-release") fixture.flow.CancelAll();
            };
            fixture.None(stopped);
            stopped.finished(); peer.finished(); fixture.None(peer); fixture.None(stopped);
            fixture.flow.CancelAll();
            Require(stopped.attempts == 3 && peer.attempts == 1 && fixture.flow.ActiveClones.Count == 0 &&
                fixture.flow.Pending == 0 && fixture.flow.Faulted == 1 && fixture.flow.Cancelled == 1 &&
                fixture.finishes == 0 && fixture.flow.DiagnosticFailures == 0,
                "late-error observer cancellation must settle each peer once");
            Debug.Log("V002_R2_CASE stopped-error-observer-cancels-peer PASS");
            cases++;
        }
        Debug.Log("V002_R2_INDEPENDENT=" + cases + " cases PASS");
    }
}
