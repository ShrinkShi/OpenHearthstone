"""Independent real-frame timing audit; no official visual reference is inferred."""
import argparse
import json
import math
from pathlib import Path
from .v005_r1_evidence import inspect


def review(folder):
    inspect(folder)
    result = {"scope": "Fresh dual-GUI timing, DSP and inputs; not official fidelity", "players": []}
    for role in ("driver", "observer"):
        report = json.loads((folder / role / "report.json").read_text(encoding="utf-8"))
        path = folder / role / "timeline.json"
        if not path.exists():
            continue
        frames = json.loads(path.read_text(encoding="utf-8"))["frames"]
        waiting = [f for f in frames if not f["ready"]]
        ready = [f for f in frames if f["ready"]]
        if not frames:
            continue
        assert waiting and ready
        first = ready[0]
        assert all(f["authorityHealth"] == waiting[0]["displayHealth"] - 1 for f in frames)
        assert all(f["displayHealth"] == waiting[0]["displayHealth"] and f["hitCallbacks"] == 0
                   and not f["applied"] and f["tint"] == f["motion"] == f["shake"] == 0 for f in waiting)
        assert all(f["displayHealth"] == f["authorityHealth"] and f["hitCallbacks"] == 1 and f["applied"] for f in ready)
        assert abs(first["batchTime"] - first["hitStart"]) < 1e-5
        assert all(not f["ready"] for f in frames[:frames.index(first)])
        assert all(f["ready"] for f in frames[frames.index(first):])
        assert first["arrival"] and any("Arrival" in f["nativeEvents"] for f in ready)
        finish = next(f for f in ready if f["batchTime"] - f["hitStart"] >= f["hitDuration"])
        window = finish["time"] - first["time"]
        assert first["hitDuration"] <= window < first["hitDuration"] + .15
        drift = max(abs((f["time"] - first["time"]) - (f["batchTime"] - first["batchTime"])) for f in ready)
        assert drift < .02
        tint_error = motion_error = shake_error = 0
        for f in ready:
            age = f["batchTime"] - f["hitStart"]
            tint = .65 * math.sin(math.pi * age / .28) if 0 <= age < .28 else 0
            motion = abs(math.sin(110 * age) * 9 * math.exp(-10 * age)) if 0 <= age <= f["hitDuration"] else 0
            shake = math.hypot(math.sin(130 * age), math.cos(110 * age)) * 3 * (1 - age / .16) if 0 <= age < .16 else 0
            tint_error = max(tint_error, abs(f["tint"] - tint))
            motion_error = max(motion_error, abs(f["motion"] - motion))
            shake_error = max(shake_error, abs(f["shake"] - shake))
        assert max(tint_error, motion_error, shake_error) < .0001
        intervals = [b["time"] - a["time"] for a, b in zip(frames, frames[1:])]
        assert all(v >= 0 for v in intervals)
        item = dict(role=role, frames=len(frames), waiting=len(waiting), ready=len(ready),
                    unique_hits=ready[-1]["hitCallbacks"], health_before=waiting[0]["displayHealth"],
                    health_after=ready[0]["displayHealth"], first_hit_wall=first["time"],
                    full_window_wall=window, full_window_scene=first["hitDuration"], maximum_drift=drift,
                    errors=dict(tint=tint_error, motion=motion_error, shake=shake_error),
                    median_frame_interval=sorted(intervals)[len(intervals)//2])
        if role == "driver":
            records = report["records"]
            pause = next(r for r in records if r["message"].startswith("actual paused selection retains"))
            double = next(r for r in records if r["message"].startswith("actual source 2x uses"))
            half = next(r for r in records if r["message"].startswith("actual source half speed uses"))
            before = next(r for r in records if r["phase"] == "selection-paused" and r["message"] == "enter")
            assert pause["sourceTime"] == before["sourceTime"]
            ratios = [(double["sourceTime"]-pause["sourceTime"])/(double["time"]-pause["time"]),
                      (half["sourceTime"]-double["sourceTime"])/(half["time"]-double["time"])]
            assert abs(ratios[0]-2) < .05 and abs(ratios[1]-.5) < .05
            item["independent_selection_rates"] = ratios
        result["players"].append(item)
    (folder / "v005-r2-timing-audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folders", type=Path, nargs="+")
    for folder in parser.parse_args().folders:
        review(folder)
