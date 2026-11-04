"""Run the isolated account flow through a built Windows Unity Player."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import secrets
import socket
import struct
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from tools.background_player import startup


ROOT = Path(__file__).resolve().parents[1]
PROTECTED_PORTS = {17492, 37240}
EXCEPTION_NAMES = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*Exception)\b")
REQUIRED_ACCOUNT_OPERATIONS = {
    "account_register", "account_shop_buy", "account_pack_open", "account_craft",
    "account_disenchant", "account_deck_save", "account_logout", "account_login",
}
REQUIRED_GUI_HANDLERS = {
    "F024Register", "ConfirmShopBuy", "F024OpenPackFromDrag",
    "F024AdvancePackReveal", "SelectCollectionCard", "F024RequestCraft",
    "F024RequestDisenchant", "F024RequestDeckSave", "F024Logout",
    "F024Login", "Connect", "FocusCollectionSearch", "ClearCollectionSearch",
}
EDITOR_INPUT_MODE = "editor-game-input-queue"
EDITOR_QUEUE_API = "UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(Event)"
EDITOR_COORDINATE_SPACE = "Player render-target pixels passed directly to EditorGUIUtility.QueueGameViewInputEvent"
MOUSE_EVENT_KINDS = {"mousedown", "mousedrag", "mouseup"}
KEYBOARD_EVENT_KINDS = {"keydown", "keyup"}
SEARCH_TARGET = "collection.search.input"


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def reserve_port(requested=None):
    if requested in PROTECTED_PORTS:
        raise ValueError(f"port {requested} is reserved for the existing game service")
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        listener.bind(("127.0.0.1", requested or 0))
        selected = listener.getsockname()[1]
    if selected in PROTECTED_PORTS:
        return reserve_port(None)
    return selected


def server_hello(port):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.5) as connection:
            connection.settimeout(0.5)
            line = connection.makefile("rb").readline(8193)
        if not line or len(line) > 8192:
            return None
        hello = json.loads(line.decode("utf-8"))
        if hello.get("type") != "hello" or hello.get("protocol") != 1:
            return None
        return hello
    except (OSError, UnicodeDecodeError, json.JSONDecodeError):
        return None


def wait_for_server(port, process, timeout):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f"isolated account server exited with {process.returncode}")
        hello = server_hello(port)
        if hello is not None:
            capabilities = hello.get("capabilities", [])
            if "accounts_v1" not in capabilities:
                raise RuntimeError("isolated server did not advertise accounts_v1")
            return hello
        time.sleep(0.1)
    raise TimeoutError(f"isolated account server did not become ready on {port}")


def stop_child(process, timeout=8):
    if process is None or process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=timeout)


def local_profile_path(profile):
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        local_app_data = str(Path.home() / "AppData" / "Local")
    return Path(local_app_data) / "HearthstoneClone" / "profiles" / profile


def choose_profile():
    for _ in range(20):
        name = f"f024acct-{os.getpid()}-{secrets.token_hex(5)}"
        if not local_profile_path(name).exists():
            return name
    raise RuntimeError("could not allocate a new isolated Player profile")


def dll_identity(client):
    data = client.parent / (client.stem + "_Data")
    candidates = [
        data / "Managed" / "Assembly-CSharp.dll",
        data / "Plugins" / "x86_64" / "GameAssembly.dll",
    ]
    entries = []
    for path in candidates:
        if path.is_file():
            entries.append({"path": str(path), "sha256": sha256(path)})
    return entries


def log_exception_types(path):
    if not path.is_file():
        return []
    found = []
    try:
        for line_number, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            match = EXCEPTION_NAMES.search(line)
            if match:
                found.append({"line": line_number, "type": match.group(1)})
                if len(found) >= 40:
                    break
    except OSError:
        return [{"line": 0, "type": "log_unreadable"}]
    return found


def read_replay_report(path, errors, require_gui_input=False):
    if not path.is_file():
        errors.append("Player did not create account-replay.json")
        return None
    try:
        report = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"Player replay report is unreadable: {error}")
        return None
    if not isinstance(report, dict):
        errors.append("Player replay report must be a JSON object")
        return None
    if report.get("result") != "ok":
        errors.append("Player account replay result is not ok: " + str(report.get("error", "missing result")))
    if not report.get("steps"):
        errors.append("Player replay report contains no completed steps")
    operations = {item.get("operation") for item in report.get("requests", []) if isinstance(item, dict)}
    missing = sorted(REQUIRED_ACCOUNT_OPERATIONS - operations)
    if missing:
        errors.append("Player replay report is missing request operations: " + ", ".join(missing))
    if require_gui_input:
        if report.get("input_mode") != "unity-event-queue/native-OnGUI":
            errors.append("Player replay did not use Unity's native IMGUI event queue")
        inputs = [item for item in report.get("gui_inputs", []) if isinstance(item, dict)]
        mouse_inputs = [item for item in inputs if _event_kind(item.get("expected_event_type")) in MOUSE_EVENT_KINDS]
        keyboard_inputs = [item for item in inputs if _event_kind(item.get("expected_event_type")) in KEYBOARD_EVENT_KINDS]
        if not inputs:
            errors.append("Player replay report contains no GUI input records")
        if any(item.get("input_context") != "unity-event-queue/native-OnGUI" or
               item.get("queue_api") != "UnityEngine.Event.QueueEvent(Event)" or
               item.get("coordinate_space") != "window-client-pixels" or
               item.get("native_event_queued") is not True or item.get("native_event_observed") is not True or
               _event_kind(item.get("expected_event_type")) not in MOUSE_EVENT_KINDS | KEYBOARD_EVENT_KINDS or
               _event_kind(item.get("raw_event_type")) != _event_kind(item.get("expected_event_type")) or
               _event_kind(item.get("current_event_type")) != _event_kind(item.get("expected_event_type")) or
               not _is_event_after(item.get("event_type_after"), item.get("expected_event_type")) or
               item.get("event_match") not in ({"native-marker", "native-type-button-position"}
                                                if _event_kind(item.get("expected_event_type")) in MOUSE_EVENT_KINDS
                                                else {"native-marker", "native-type-key-character"}) for item in inputs):
            errors.append("GUI input records do not show queued native events passing through OnGUI")
        if len(mouse_inputs) % 3:
            errors.append("GUI replay input records do not form MouseDown/MouseDrag/MouseUp groups")
        mouse_positions = [index for index, item in enumerate(inputs)
                           if _event_kind(item.get("expected_event_type")) in MOUSE_EVENT_KINDS]
        for offset in range(0, len(mouse_inputs) - 2, 3):
            down, drag, up = mouse_inputs[offset:offset + 3]
            control = down.get("control_id")
            if ([_event_kind(down.get("expected_event_type")), _event_kind(drag.get("expected_event_type")),
                 _event_kind(up.get("expected_event_type"))] != ["mousedown", "mousedrag", "mouseup"] or
                    len({down.get("target"), drag.get("target"), up.get("target")}) != 1 or
                    not control or drag.get("control_id") != control or up.get("control_id") != control or
                    down.get("hot_control_after") != control or
                    drag.get("hot_control_before") != control or drag.get("hot_control_after") != control or
                    up.get("hot_control_before") != control or up.get("hot_control_after") != 0):
                errors.append("GUI replay did not preserve one hot control across a three-frame input sequence")
                break
            if (mouse_positions[offset + 1] != mouse_positions[offset] + 1 or
                    mouse_positions[offset + 2] != mouse_positions[offset + 1] + 1):
                errors.append("GUI replay interleaved another event into a MouseDown/MouseDrag/MouseUp gesture")
                break
        keyboard_search = _validate_keyboard_search(inputs, report.get("steps"))
        errors.extend("Keyboard search evidence: " + item for item in keyboard_search["errors"])
        handlers = {item.get("handler") for item in inputs if item.get("handler")}
        missing_handlers = sorted(REQUIRED_GUI_HANDLERS - handlers)
        if missing_handlers:
            errors.append("GUI replay did not reach production handlers: " + ", ".join(missing_handlers))
        outside_releases = [item for item in inputs if item.get("target", "").startswith("packs.drag:") and
                            item.get("coordinate_target") == "packs.outside" and
                            _event_kind(item.get("expected_event_type")) == "mouseup"]
        if not outside_releases:
            errors.append("GUI replay did not record a pack release outside the opening slot")
        valid_pack_releases = [item for item in inputs if item.get("target", "").startswith("packs.drag:") and
                               item.get("coordinate_target") == "packs.slot" and
                               _event_kind(item.get("expected_event_type")) == "mouseup" and item.get("request_id")]
        open_requests = [item for item in report.get("requests", []) if isinstance(item, dict) and
                         item.get("operation") == "account_pack_open"]
        if len(valid_pack_releases) != len(open_requests) or not open_requests:
            errors.append("GUI pack-slot releases and account_pack_open requests do not match one-to-one")
    return report


def inspect_screenshots(paths, capture_dir, errors):
    captures = []
    for raw_path in paths or []:
        try:
            path = Path(raw_path).resolve()
            path.relative_to(capture_dir.resolve())
        except (TypeError, OSError, RuntimeError, ValueError):
            errors.append("Replay screenshot path is outside the capture folder")
            captures.append({"path": str(raw_path), "exists": False})
            continue
        record = {"path": str(path), "exists": path.is_file()}
        if path.is_file():
            try:
                data = path.read_bytes()
                record["bytes"] = len(data)
                record["sha256"] = hashlib.sha256(data).hexdigest()
                if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
                    errors.append(f"Replay screenshot is not a valid PNG: {path.name}")
                else:
                    record["width"], record["height"] = struct.unpack(">II", data[16:24])
                    if (record["width"], record["height"]) != (1920, 1080):
                        errors.append(f"Replay screenshot is not 1920x1080: {path.name}")
            except OSError as error:
                errors.append(f"Could not inspect replay screenshot {path.name}: {error}")
        else:
            errors.append(f"Replay screenshot is missing: {path.name}")
        captures.append(record)
    return captures


def _evidence_number(record, key):
    value = record.get(key)
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:
        return False


def _near(record, left, right, tolerance):
    return (_evidence_number(record, left) and _evidence_number(record, right) and
            abs(record[left] - record[right]) <= tolerance)


def _is_allowed(value, choices):
    return isinstance(value, str) and value in choices


def _event_kind(value):
    return value.casefold() if isinstance(value, str) else ""


def _is_event_after(value, expected):
    return isinstance(value, str) and (_event_kind(value) == _event_kind(expected) or value == "Used")


def _contains_point(record, x_key, y_key, rect_x_key, rect_y_key, width_key, height_key, tolerance=0.25):
    keys = (x_key, y_key, rect_x_key, rect_y_key, width_key, height_key)
    if not all(_evidence_number(record, key) for key in keys):
        return False
    x, y = record[x_key], record[y_key]
    left, top = record[rect_x_key], record[rect_y_key]
    right, bottom = left + record[width_key], top + record[height_key]
    return left - tolerance <= x <= right + tolerance and top - tolerance <= y <= bottom + tolerance


def _expected_ascii_key_code(character):
    if "a" <= character <= "z" or "A" <= character <= "Z":
        return character.upper()
    if "0" <= character <= "9":
        return "Alpha" + character
    if character in {"_", "-"}:
        return "Minus"
    return None


def _validate_keyboard_search(records, steps):
    """Validate the queued ASCII search keys against their observed IMGUI evidence and assertions."""
    errors = []
    key_records = [item for item in records if isinstance(item, dict) and
                   _event_kind(item.get("expected_event_type")) in KEYBOARD_EVENT_KINDS]
    requested_pattern = re.compile(
        r"^collection\.search requested (KeyDown|keyUp|KeyUp) keyCode=([^ ]+) character=U\+([0-9A-Fa-f]{4})$")
    observed_pattern = re.compile(
        r"^collection\.search (KeyDown|keyUp|KeyUp) keyCode=([^ ]+) character=U\+([0-9A-Fa-f]{4}) "
        r"focusBefore=(.*?) keyboardBefore=(\d+) focusAfter=(.*?) keyboardAfter=(\d+) query=(.*)$")
    focus_pattern = re.compile(
        r"^collection\.search focus assertion passed: collection\.search\.input keyboardControl=(\d+)$")
    requested = []
    observed = []
    requested_step_indices = []
    observed_step_indices = []
    focus_controls = []
    focus_step_indices = []
    keyboard_query_assertions = []
    typed_id_assertions = []
    clear_assertions = []
    for step_index, step in enumerate(steps if isinstance(steps, list) else []):
        if not isinstance(step, str):
            continue
        match = requested_pattern.fullmatch(step)
        if match:
            requested.append((match.group(1), match.group(2), int(match.group(3), 16)))
            requested_step_indices.append(step_index)
            continue
        match = observed_pattern.fullmatch(step)
        if match:
            observed.append({
                "event_type": match.group(1),
                "key_code": match.group(2),
                "character": int(match.group(3), 16),
                "focus_before": match.group(4),
                "keyboard_before": int(match.group(5)),
                "focus_after": match.group(6),
                "keyboard_after": int(match.group(7)),
                "query": match.group(8),
            })
            observed_step_indices.append(step_index)
            continue
        match = focus_pattern.fullmatch(step)
        if match:
            focus_controls.append(int(match.group(1)))
            focus_step_indices.append(step_index)
        elif step.startswith("collection.search keyboard query assertion passed: "):
            keyboard_query_assertions.append((step_index, step.split(": ", 1)[1]))
        elif step.startswith("collection.search typed CardId assertion passed: "):
            typed_id_assertions.append((step_index, step.split(": ", 1)[1]))
        elif step == "collection.search clear-button assertion passed":
            clear_assertions.append(step_index)

    if not key_records:
        errors.append("no search KeyDown/KeyUp GUI input records")
    if len(requested) != len(key_records):
        errors.append("requested keyCode/character steps do not match the keyboard input records")
    if len(observed) != len(key_records):
        errors.append("observed keyCode/character/focus steps do not match the keyboard input records")
    if len(focus_controls) != 1 or (focus_controls and focus_controls[0] <= 0):
        errors.append("named search TextField focus assertion is missing or invalid")
    if len(keyboard_query_assertions) != 1 or len(typed_id_assertions) != 1:
        errors.append("final keyboard query and typed CardId assertions must each appear once")
    if len(clear_assertions) != 1:
        errors.append("search clear-button assertion is missing or duplicated")
    if (len(focus_step_indices) == 1 and requested_step_indices and
            focus_step_indices[0] >= requested_step_indices[0]):
        errors.append("search TextField focus assertion does not precede the queued keyboard input")
    if (len(requested_step_indices) == len(observed_step_indices) and requested_step_indices and
            any(requested_index >= observed_index or
                (index + 1 < len(requested_step_indices) and observed_index >= requested_step_indices[index + 1])
                for index, (requested_index, observed_index) in enumerate(zip(requested_step_indices, observed_step_indices)))):
        errors.append("keyboard request and observation steps are not in native event order")
    if (len(keyboard_query_assertions) == len(typed_id_assertions) == len(clear_assertions) == 1 and
            observed_step_indices and
            not (observed_step_indices[-1] < keyboard_query_assertions[0][0] <
                 typed_id_assertions[0][0] < clear_assertions[0])):
        errors.append("keyboard query, typed CardId, and clear assertions are out of order")

    typed_text = ""
    previous_key_code = None
    for index, record in enumerate(key_records):
        expected = record.get("expected_event_type")
        kind = _event_kind(expected)
        if (record.get("target") != SEARCH_TARGET or record.get("coordinate_target") != SEARCH_TARGET or
                record.get("native_event_queued") is not True or record.get("native_event_observed") is not True or
                _event_kind(record.get("current_event_type")) != kind or
                _event_kind(record.get("raw_event_type")) != kind or
                _event_kind(record.get("submitted_event_type")) != kind or
                _event_kind(record.get("submitted_raw_event_type")) != kind or
                _event_kind(record.get("candidate_event_type")) != kind or
                _event_kind(record.get("candidate_raw_event_type")) != kind or
                not _is_event_after(record.get("event_type_after"), expected) or
                record.get("event_match") not in {"native-marker", "native-type-key-character"}):
            errors.append("a keyboard event lacks a matching queued and observed search-field receipt")
            break
        if not isinstance(record.get("event_consumed"), bool) or (kind == "keydown" and not record["event_consumed"]):
            errors.append("search KeyDown was not consumed by the TextField or a keyboard receipt is incomplete")
            break
        if record.get("event_match") == "native-marker" and (
                record.get("event_marker_matches") is not True or not record.get("event_marker") or
                record.get("observed_event_marker") != record.get("event_marker")):
            errors.append("a keyboard event marker does not match its observed event")
            break
        if (not _evidence_number(record, "control_rect_width") or
                not _evidence_number(record, "control_rect_height") or
                record["control_rect_width"] <= 0 or record["control_rect_height"] <= 0 or
                record.get("control_enabled") is not True or
                _event_kind(record.get("control_event_type")) != kind or
                _event_kind(record.get("control_raw_event_type")) != kind or
                not _is_event_after(record.get("control_event_type_after"), expected)):
            errors.append("a keyboard event was not observed by the enabled search TextField")
            break

        if index >= len(requested) or index >= len(observed):
            continue
        request_type, request_code, request_char = requested[index]
        event = observed[index]
        if (_event_kind(request_type) != kind or _event_kind(event["event_type"]) != kind or
                request_code != event["key_code"] or request_char != event["character"]):
            errors.append("queued and observed keyboard event types or keyCode/character values differ")
            break
        if index % 2 == 0:
            if kind != "keydown" or not 0x20 <= event["character"] <= 0x7e:
                errors.append("search keyboard events must begin with printable ASCII KeyDown events")
                break
            character = chr(event["character"])
            if event["key_code"] != _expected_ascii_key_code(character):
                errors.append("search KeyDown keyCode does not correspond to its ASCII character")
                break
            typed_text += character
            previous_key_code = event["key_code"]
        else:
            if (kind != "keyup" or event["character"] != 0 or
                    event["key_code"] != previous_key_code):
                errors.append("search KeyUp does not match the preceding KeyDown")
                break
        if (event["focus_before"] != SEARCH_TARGET or event["keyboard_before"] <= 0 or
                event["focus_after"] != SEARCH_TARGET or event["keyboard_after"] <= 0):
            errors.append("search TextField focus was not held across a keyboard event")
            break
        expected_query = typed_text
        if event["query"] != expected_query:
            errors.append("search query after a keyboard event does not match the typed characters")
            break

    if key_records and len(key_records) % 2:
        errors.append("search keyboard events do not form KeyDown/KeyUp pairs")
    if not errors and requested and observed and len(requested) == len(key_records) == len(observed):
        assertions = [value for _, value in keyboard_query_assertions + typed_id_assertions]
        if not typed_text or any(value != typed_text for value in assertions):
            errors.append("final keyboard query assertion does not equal the observed ASCII CardId")
    focus_handler = any(item.get("target") == SEARCH_TARGET and item.get("handler") == "FocusCollectionSearch" and
                        _event_kind(item.get("expected_event_type")) == "mouseup" for item in records)
    clear_handler = any(item.get("target") == "collection.search.clear" and
                        item.get("handler") == "ClearCollectionSearch" and
                        _event_kind(item.get("expected_event_type")) == "mouseup" for item in records)
    focus_index = next((i for i, item in enumerate(records) if item.get("handler") == "FocusCollectionSearch" and
                        item.get("target") == SEARCH_TARGET and
                        _event_kind(item.get("expected_event_type")) == "mouseup"), None)
    clear_index = next((i for i, item in enumerate(records) if item.get("handler") == "ClearCollectionSearch" and
                        item.get("target") == "collection.search.clear" and
                        _event_kind(item.get("expected_event_type")) == "mouseup"), None)
    key_indices = [i for i, item in enumerate(records) if _event_kind(item.get("expected_event_type")) in KEYBOARD_EVENT_KINDS]
    if (not focus_handler or focus_index is None or not key_indices or focus_index >= min(key_indices)):
        errors.append("search keyboard input is not preceded by a production TextField focus click")
    if (not clear_handler or clear_index is None or not key_indices or clear_index <= max(key_indices)):
        errors.append("search X clear handler is not reached after the keyboard input")

    return {
        "ok": not errors,
        "card_id": typed_text if typed_text else None,
        "keyboard_event_count": len(key_records),
        "focus_verified": focus_handler and len(focus_controls) == 1 and bool(focus_controls and focus_controls[0] > 0),
        "clear_verified": clear_handler and len(clear_assertions) == 1,
        "errors": errors,
    }


def analyze_editor_evidence(folder):
    """Read and summarize a saved Editor GameView account replay without modifying it."""
    folder = Path(folder).expanduser().resolve()
    report_path = folder / "account-replay.json"
    result = {
        "analysis": "f024-editor-gui-evidence",
        "folder": str(folder),
        "report": str(report_path),
        "result": "failed",
        "input_mode": None,
        "input_scope": "Unity Editor native GameView input queue observed by production OnGUI handlers",
        "checks": {},
        "counts": {},
        "pack_open": {},
        "screenshots": [],
        "evidence_limits": [
            "Does not establish operating-system mouse input or standalone Player GUI behavior.",
            "An ASCII CardId was typed into the collection search TextField; account credentials were prefilled.",
        ],
        "errors": [],
    }
    errors = result["errors"]
    if not folder.is_dir():
        errors.append(f"Editor replay folder does not exist: {folder}")
        return result
    if not report_path.is_file():
        errors.append(f"Editor replay folder has no account-replay.json: {report_path}")
        return result
    try:
        report = json.loads(report_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"Editor account replay report is unreadable: {error}")
        return result
    if not isinstance(report, dict):
        errors.append("Editor account replay report must be a JSON object")
        return result

    result["input_mode"] = report.get("input_mode")
    if report.get("result") != "ok":
        errors.append("Editor account replay result is not ok: " + str(report.get("error", "missing result")))
    if not report.get("steps"):
        errors.append("Editor account replay report contains no completed steps")
    if report.get("input_mode") != EDITOR_INPUT_MODE:
        errors.append("Replay input_mode is not editor-game-input-queue")

    requests = report.get("requests")
    if not isinstance(requests, list):
        requests = []
        errors.append("Editor account replay requests must be an array")
    request_map = {}
    duplicate_request_ids = set()
    request_successes = 0
    expected_rejections = []
    for item in requests:
        if not isinstance(item, dict):
            errors.append("Editor account replay contains a malformed request record")
            continue
        request_id = item.get("request_id")
        if not isinstance(request_id, str) or not request_id:
            errors.append("Editor request record is missing request_id")
        elif request_id in request_map:
            duplicate_request_ids.add(request_id)
        else:
            request_map[request_id] = item
        if item.get("completed") is True and item.get("ok") is True and not item.get("error"):
            request_successes += 1
        elif (item.get("expected_failure") is True and item.get("completed") is True and
              item.get("ok") is False and item.get("operation") == "account_deck_save" and
              item.get("error") == "卡组不存在"):
            expected_rejections.append(request_id)
        else:
            errors.append("Editor request did not complete successfully: " + str(item.get("operation", "unknown")))
    if len(expected_rejections) > 1:
        errors.append("Editor replay contains multiple expected deck save rejections")
    if duplicate_request_ids:
        errors.append("Editor request IDs are duplicated")
    operations = {item.get("operation") for item in requests if isinstance(item, dict) and
                  isinstance(item.get("operation"), str)}
    missing_operations = sorted(REQUIRED_ACCOUNT_OPERATIONS - operations)
    if missing_operations:
        errors.append("Editor replay is missing request operations: " + ", ".join(missing_operations))
    result["counts"]["requests"] = len(requests)
    result["counts"]["successful_requests"] = request_successes
    result["counts"]["expected_rejections"] = len(expected_rejections)

    inputs = report.get("gui_inputs")
    if not isinstance(inputs, list):
        inputs = []
        errors.append("Editor GUI input records must be an array")
    records = [item for item in inputs if isinstance(item, dict)]
    if len(records) != len(inputs):
        errors.append("Editor GUI input records contain malformed entries")
    mouse_records = [item for item in records if _event_kind(item.get("expected_event_type")) in MOUSE_EVENT_KINDS]
    keyboard_records = [item for item in records if _event_kind(item.get("expected_event_type")) in KEYBOARD_EVENT_KINDS]
    keyboard_search = _validate_keyboard_search(records, report.get("steps"))
    errors.extend("Keyboard search evidence: " + item for item in keyboard_search["errors"])
    result["keyboard_search"] = {key: value for key, value in keyboard_search.items() if key != "errors"}
    result["counts"]["gui_input_records"] = len(records)
    result["counts"]["mouse_gestures"] = len(mouse_records) // 3
    result["counts"]["keyboard_events"] = len(keyboard_records)
    handlers = {item.get("handler") for item in records if isinstance(item.get("handler"), str) and item.get("handler")}
    missing_handlers = sorted(REQUIRED_GUI_HANDLERS - handlers)
    if missing_handlers:
        errors.append("Editor replay did not reach production GUI handlers: " + ", ".join(missing_handlers))

    receipts_ok = bool(records)
    hot_control_ok = bool(records)
    coordinates_ok = True
    for index, item in enumerate(records):
        expected = item.get("expected_event_type")
        kind = _event_kind(expected)
        is_mouse = kind in MOUSE_EVENT_KINDS
        is_keyboard = kind in KEYBOARD_EVENT_KINDS
        if (item.get("input_context") != EDITOR_INPUT_MODE or
                item.get("queue_api") != EDITOR_QUEUE_API or
                item.get("coordinate_space") != EDITOR_COORDINATE_SPACE):
            receipts_ok = False
        if (item.get("editor_dispatch_schedule") != "UnityEditor.EditorApplication.delayCall" or
                item.get("editor_input_scheduled") is not True or
                item.get("editor_input_dispatch_invoked") is not True or
                item.get("editor_game_input_queue_invoked") is not True or
                item.get("editor_game_input_queue_returned") is not True or
                item.get("native_event_queued") is not True or
                item.get("native_event_observed") is not True or
                item.get("editor_send_event_invoked") is not False or
                item.get("editor_send_event_scheduled") is not False or
                item.get("queue_error")):
            receipts_ok = False
        allowed_matches = ({"native-marker", "native-type-button-position"} if is_mouse else
                           {"native-marker", "native-type-key-character"} if is_keyboard else set())
        if (not (is_mouse or is_keyboard) or
                _event_kind(item.get("current_event_type")) != kind or
                _event_kind(item.get("raw_event_type")) != kind or
                _event_kind(item.get("candidate_event_type")) != kind or
                _event_kind(item.get("candidate_raw_event_type")) != kind or
                _event_kind(item.get("submitted_event_type")) != kind or
                _event_kind(item.get("submitted_raw_event_type")) != kind or
                not _is_event_after(item.get("event_type_after"), expected) or
                not isinstance(item.get("event_consumed"), bool) or
                ((is_mouse or kind == "keydown") and item.get("event_consumed") is not True) or
                not _is_allowed(item.get("event_match"), allowed_matches)):
            receipts_ok = False
        if item.get("event_match") == "native-marker":
            if (item.get("event_marker_matches") is not True or
                    not item.get("event_marker") or
                    item.get("observed_event_marker") != item.get("event_marker")):
                receipts_ok = False
        elif is_mouse and item.get("root_pointer_matches") is not True:
            receipts_ok = False

        numeric_pairs = (
            ("screen_x", "queued_mouse_x", 0.25),
            ("screen_y", "queued_mouse_y", 0.25),
            ("gui_x", "design_x", 0.25),
            ("gui_y", "design_y", 0.25),
            ("game_view_event_mouse_x", "queued_mouse_x", 0.25),
            ("game_view_event_mouse_y", "queued_mouse_y", 0.25),
            ("current_mouse_x", "design_x", 2.0),
            ("current_mouse_y", "design_y", 2.0),
            ("candidate_mouse_x", "current_mouse_x", 0.25),
            ("candidate_mouse_y", "current_mouse_y", 0.25),
        )
        if (not all(_near(item, left, right, tolerance) for left, right, tolerance in numeric_pairs) or
                not _contains_point(item, "design_x", "design_y", "rect_x", "rect_y", "rect_width", "rect_height") or
                (is_mouse and item.get("root_pointer_matches") is not True)):
            coordinates_ok = False
        for width_key, height_key in (("player_screen_width", "player_screen_height"),
                                      ("game_view_target_width", "game_view_target_height"),
                                      ("game_view_rt_width", "game_view_rt_height")):
            if (not _evidence_number(item, width_key) or not _evidence_number(item, height_key) or
                    (item[width_key], item[height_key]) != (1920, 1080)):
                coordinates_ok = False

        control_width = item.get("control_rect_width", 0)
        control_height = item.get("control_rect_height", 0)
        if is_mouse and _evidence_number(item, "control_rect_width") and _evidence_number(item, "control_rect_height") and control_width > 0 and control_height > 0:
            if (item.get("control_contains_mouse") is not True or
                    item.get("control_event_type") != expected or
                    item.get("control_raw_event_type") != expected or
                    not _is_event_after(item.get("control_event_type_after"), expected) or
                    not _contains_point(item, "control_mouse_x", "control_mouse_y", "control_rect_x", "control_rect_y",
                                        "control_rect_width", "control_rect_height")):
                coordinates_ok = False

        if is_mouse:
            for key in ("control_id", "hot_control_before", "hot_control_after"):
                if not isinstance(item.get(key), int) or isinstance(item.get(key), bool):
                    receipts_ok = False
                    break

    if mouse_records and len(mouse_records) % 3:
        errors.append("Editor GUI input records do not form MouseDown/MouseDrag/MouseUp groups")
        hot_control_ok = False
    mouse_positions = [index for index, item in enumerate(records)
                       if _event_kind(item.get("expected_event_type")) in MOUSE_EVENT_KINDS]
    for offset in range(0, len(mouse_records) - 2, 3):
        down, drag, up = mouse_records[offset:offset + 3]
        control = down.get("control_id")
        target = down.get("target")
        if (not isinstance(target, str) or
                [_event_kind(down.get("expected_event_type")), _event_kind(drag.get("expected_event_type")),
                 _event_kind(up.get("expected_event_type"))] != ["mousedown", "mousedrag", "mouseup"] or
                drag.get("target") != target or up.get("target") != target or
                not isinstance(control, int) or control == 0 or
                drag.get("control_id") != control or up.get("control_id") != control or
                down.get("hot_control_before") != 0 or down.get("hot_control_after") != control or
                drag.get("hot_control_before") != control or drag.get("hot_control_after") != control or
                up.get("hot_control_before") != control or up.get("hot_control_after") != 0):
            hot_control_ok = False
            break
        if (mouse_positions[offset + 1] != mouse_positions[offset] + 1 or
                mouse_positions[offset + 2] != mouse_positions[offset + 1] + 1):
            hot_control_ok = False
            break

        for record in (down, drag, up):
            control_width = record.get("control_rect_width", 0)
            control_height = record.get("control_rect_height", 0)
            if (_evidence_number(record, "control_rect_width") and _evidence_number(record, "control_rect_height") and
                    control_width > 0 and control_height > 0):
                if record["expected_event_type"] == "mouseDown":
                    valid_hot = record.get("control_hot_before") == 0 and record.get("control_hot_after") == control
                elif record["expected_event_type"] == "mouseDrag":
                    valid_hot = record.get("control_hot_before") == control and record.get("control_hot_after") == control
                else:
                    valid_hot = record.get("control_hot_before") == control and record.get("control_hot_after") == 0
                if not valid_hot:
                    hot_control_ok = False
                    break

    if not receipts_ok:
        errors.append("Editor native GameView input receipts or observed event types are inconsistent")
    if not hot_control_ok:
        errors.append("Production IMGUI hotControl did not remain held across each MouseDown/MouseDrag/MouseUp group")
    if not coordinates_ok:
        errors.append("Editor GameView, queued, OnGUI, control, or target coordinates do not agree")
    result["checks"]["editor_native_gameview_receipts"] = receipts_ok
    result["checks"]["im_gui_hot_control"] = hot_control_ok
    result["checks"]["coordinate_consistency"] = coordinates_ok
    result["checks"]["keyboard_search_input"] = keyboard_search["ok"]
    rejection_evidence = all(any(isinstance(record, dict) and record.get("target") == "collection.deck-complete" and
        record.get("handler") == "F024RequestDeckSave" and record.get("request_id") == request_id
        for record in records) for request_id in expected_rejections)
    if not rejection_evidence:
        errors.append("Expected deck rejection is missing its GUI save input")
    result["checks"]["successful_requests"] = (bool(requests) and rejection_evidence and
        request_successes + len(expected_rejections) == len(requests))

    outside_releases = [item for item in records if isinstance(item.get("target"), str) and
                        item.get("target").startswith("packs.drag:") and
                        item.get("coordinate_target") == "packs.outside" and
                        _event_kind(item.get("expected_event_type")) == "mouseup"]
    slot_releases = [item for item in records if isinstance(item.get("target"), str) and
                     item.get("target").startswith("packs.drag:") and
                     item.get("coordinate_target") == "packs.slot" and
                     _event_kind(item.get("expected_event_type")) == "mouseup"]
    pack_requests = [item for item in requests if isinstance(item, dict) and item.get("operation") == "account_pack_open"]
    outside_without_request = [item for item in outside_releases if not item.get("request_id")]
    pack_request_matches = [item for item in slot_releases if isinstance(item.get("request_id"), str) and
                            item.get("request_id") and
                            request_map.get(item.get("request_id"), {}).get("operation") == "account_pack_open"]
    if not outside_releases:
        errors.append("Editor replay did not record a pack release outside the opening slot")
    if len(outside_without_request) != len(outside_releases):
        errors.append("Pack release outside the opening slot carried a request")
    if len(slot_releases) != 1 or len(pack_requests) != 1 or len(pack_request_matches) != 1:
        errors.append("Pack slot release and account_pack_open request must match exactly once")
    elif (pack_request_matches[0].get("handler") != "F024OpenPackFromDrag" or
          pack_requests[0].get("request_id") != pack_request_matches[0].get("request_id") or
          pack_requests[0].get("completed") is not True or pack_requests[0].get("ok") is not True or
          pack_requests[0].get("error") or pack_requests[0].get("opened_card_count") != 5):
        errors.append("The single pack-slot release did not produce one successful five-card open")
    result["pack_open"] = {
        "outside_release_count": len(outside_releases),
        "outside_release_without_request_count": len(outside_without_request),
        "slot_release_count": len(slot_releases),
        "matched_successful_open_requests": len(pack_request_matches),
        "opened_card_count": pack_requests[0].get("opened_card_count") if len(pack_requests) == 1 else None,
    }
    result["checks"]["pack_open_boundary"] = (
        bool(outside_releases) and len(outside_without_request) == len(outside_releases) and
        len(slot_releases) == 1 and len(pack_requests) == 1 and len(pack_request_matches) == 1 and
        pack_requests[0].get("completed") is True and pack_requests[0].get("ok") is True and
        not pack_requests[0].get("error") and pack_requests[0].get("opened_card_count") == 5
    )

    screenshot_paths = report.get("screenshots")
    if not isinstance(screenshot_paths, list):
        screenshot_paths = []
        errors.append("Editor screenshot paths must be an array")
    captures = inspect_screenshots(screenshot_paths, folder / "captures", errors)
    result["screenshots"] = captures
    result["counts"]["screenshots"] = len(captures)
    valid_screenshots = [item for item in captures if item.get("exists") and
                         item.get("width") == 1920 and item.get("height") == 1080]
    result["counts"]["valid_1920x1080_screenshots"] = len(valid_screenshots)
    if len(captures) < 18:
        errors.append(f"Editor replay must contain at least 18 screenshots; found {len(captures)}")
    card_frames = {}
    for item in captures:
        match = re.search(r"account-replay-pack-(\d+)-card-(\d+)\.png$", Path(item.get("path", "")).name)
        if match:
            card_frames.setdefault(match.group(1), []).append(int(match.group(2)))
    has_five_frames = any(sorted(indices) == [1, 2, 3, 4, 5] for indices in card_frames.values())
    if not has_five_frames:
        errors.append("Editor replay screenshots do not include the five pack reveal frames")
    if len({item.get("path") for item in captures}) != len(captures):
        errors.append("Editor replay screenshot paths are not unique")
    result["checks"]["full_hd_screenshots"] = len(captures) >= 18 and len(valid_screenshots) == len(captures)
    result["checks"]["five_pack_reveal_frames"] = has_five_frames

    result["result"] = "ok" if not errors else "failed"
    return result


def inside(path, parent):
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except (OSError, RuntimeError, ValueError):
        return False


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, default=ROOT / "build/client/HearthstoneClone.exe")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--port", type=int, help="reserved loopback port; omitted chooses an OS-assigned port")
    parser.add_argument("--timeout", type=int, default=900)
    parser.add_argument("--server-timeout", type=float, default=30)
    parser.add_argument("--gui-input-replay", action="store_true",
                        help="queue isolated MouseDown/MouseDrag/MouseUp events for production OnGUI controls")
    parser.add_argument("--analyze-editor", type=Path, metavar="FOLDER",
                        help="read-only analysis of an existing Editor GameView account replay folder")
    args = parser.parse_args(argv)
    if args.analyze_editor is not None:
        analysis = analyze_editor_evidence(args.analyze_editor)
        print(json.dumps(analysis, ensure_ascii=False, indent=2))
        return 0 if analysis["result"] == "ok" else 1
    if os.name != "nt":
        parser.error("the Unity Player replay harness currently requires Windows")
    if args.timeout < 30 or args.server_timeout <= 0:
        parser.error("--timeout must be at least 30 seconds and --server-timeout must be positive")

    if args.output is None:
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        output = ROOT / "work" / f"f024-account-player-{stamp}-{os.getpid()}-{secrets.token_hex(3)}"
    else:
        output = args.output if args.output.is_absolute() else ROOT / args.output
        output = output.resolve()
    work_root = (ROOT / "work").resolve()
    if not inside(output, work_root):
        parser.error("--output must resolve under this repository's work directory")
    if output.exists():
        parser.error(f"output folder already exists; refusing to overwrite: {output}")
    output.mkdir(parents=True, exist_ok=False)
    captures_dir = output / "captures"
    logs_dir = output / "logs"
    matches_dir = output / "matches"
    for folder in (captures_dir, logs_dir, matches_dir):
        folder.mkdir(parents=True, exist_ok=True)

    client_path = args.client.resolve()
    runtime = ROOT / "local" / "runtime"
    account_db = output / "accounts.sqlite3"
    token_key = Path(str(account_db) + ".token-key")
    report_path = output / "account-replay.json"
    unity_log = logs_dir / "player-unity.log"
    process_log = logs_dir / "player-stdout.log"
    server_log_path = logs_dir / "server.log"
    result = {
        "schema_version": 1,
        "started_utc": utc_now(),
        "result": "failed",
        "output": str(output),
        "database": str(account_db),
        "token_key": str(token_key),
        "matches": str(matches_dir),
        "captures_directory": str(captures_dir),
        "server_log": str(server_log_path),
        "player_log": str(unity_log),
        "requests": [],
        "steps": [],
        "screenshots": [],
        "errors": [],
    }
    errors = result["errors"]
    server = None
    player = None
    server_stream = None
    player_stream = None
    profile = None
    profile_path = None
    timed_out = False
    selected_port = None
    hello = None

    try:
        if not client_path.is_file():
            raise FileNotFoundError(f"Unity Player not found: {client_path}")
        dlls = dll_identity(client_path)
        result["player_executable"] = str(client_path)
        result["player_executable_sha256"] = sha256(client_path)
        result["player_dlls"] = dlls
        if not dlls:
            raise FileNotFoundError("no Assembly-CSharp.dll or GameAssembly.dll in the built Player")
        for required in (runtime / "registry.json", ROOT / "local" / "card-database.json"):
            if not required.is_file():
                raise FileNotFoundError(f"required runtime asset is missing: {required}")

        selected_port = reserve_port(args.port)
        result["port"] = selected_port
        server_stream = server_log_path.open("wb")
        server_command = [
            sys.executable, "-X", "utf8", "-m", "game.server",
            "--host", "127.0.0.1", "--port", str(selected_port),
            "--account-db", str(account_db), "--logdir", str(matches_dir),
        ]
        server = subprocess.Popen(
            server_command, cwd=ROOT, stdout=server_stream, stderr=subprocess.STDOUT,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        result["server_pid"] = server.pid
        hello = wait_for_server(selected_port, server, args.server_timeout)
        result["server_capabilities"] = hello.get("capabilities", [])

        profile = choose_profile()
        profile_path = local_profile_path(profile)
        result["profile"] = profile
        result["profile_path"] = str(profile_path)
        player_stream = process_log.open("wb")
        command = [
            str(client_path), "--profile", profile,
            "--port", str(selected_port), "--account-replay", "--account-replay-exit",
            "--screenshot-dir", str(captures_dir.resolve()),
            "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0",
            "-logFile", str(unity_log),
        ]
        if args.gui_input_replay:
            command.append("--account-gui-replay")
        player = subprocess.Popen(command, cwd=ROOT, stdout=player_stream,
                                  stderr=subprocess.STDOUT, **startup(True))
        result["player_pid"] = player.pid
        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            player_code = player.poll()
            if player_code is not None:
                break
            if server.poll() is not None:
                raise RuntimeError(f"isolated account server exited while Player was running ({server.returncode})")
            time.sleep(0.25)
        else:
            timed_out = True
            errors.append(f"Unity Player account replay timed out after {args.timeout} seconds")
    except Exception as error:
        errors.append(f"{type(error).__name__}: {error}")
    finally:
        for process, label in ((player, "Player"), (server, "server")):
            try:
                stop_child(process)
            except Exception as error:
                errors.append(f"could not stop harness-owned {label} PID: {error}")
        for stream in (player_stream, server_stream):
            if stream is not None:
                try:
                    stream.close()
                except OSError:
                    pass

    result["timed_out"] = timed_out
    result["player_exit_code"] = None if player is None else player.returncode
    result["server_exit_code_after_cleanup"] = None if server is None else server.returncode
    result["token_key_created"] = token_key.is_file()
    if token_key.is_file():
        try:
            result["token_key_bytes"] = token_key.stat().st_size
            result["token_key_sha256"] = sha256(token_key)
            if result["token_key_bytes"] != 32:
                errors.append("isolated account token key is not 32 bytes")
        except OSError as error:
            errors.append(f"could not inspect isolated account token key: {error}")
    elif server is not None:
        errors.append("isolated account token key was not created")

    player_report = None
    if profile_path is not None:
        source_report = profile_path / "account-replay.json"
        player_report = read_replay_report(source_report, errors, args.gui_input_replay)
        if player_report is not None:
            report_path.write_text(json.dumps(player_report, ensure_ascii=False, indent=2), encoding="utf-8")
            result["player_report"] = str(report_path)
            result["steps"] = player_report.get("steps", [])
            result["requests"] = player_report.get("requests", [])
            result["input_mode"] = player_report.get("input_mode", "")
            result["gui_inputs"] = player_report.get("gui_inputs", [])
            result["screenshots"] = inspect_screenshots(
                player_report.get("screenshots", []), captures_dir, errors)
        session_path = profile_path / "account-session.bin"
        try:
            if session_path.is_file():
                session_path.unlink()
                result["session_token_file_removed"] = True
        except OSError as error:
            errors.append(f"could not remove the generated encrypted session token: {error}")

    for path, label in ((unity_log, "Unity"), (process_log, "Player stdout"), (server_log_path, "server")):
        exceptions = log_exception_types(path)
        if exceptions:
            result.setdefault("log_exceptions", {})[label] = exceptions
            errors.append(f"{label} log contains exception types: " + ", ".join(sorted({item["type"] for item in exceptions})))
    if player is not None and player.returncode != 0:
        errors.append(f"Unity Player exited with code {player.returncode}")
    result["completed_utc"] = utc_now()
    result["result"] = "ok" if not errors else "failed"
    summary_path = output / "summary.json"
    summary_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["result"] == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
