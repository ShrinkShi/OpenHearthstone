import json
import struct
from pathlib import Path

import pytest

from tools.account_player import REQUIRED_ACCOUNT_OPERATIONS, REQUIRED_GUI_HANDLERS, main


def _make_record(event_type, target, coordinate_target, point, rect, control_id, hot_before, hot_after,
                 handler="", request_id="", control=True):
    x, y = point
    left, top, width, height = rect
    record = {
        "target": target,
        "coordinate_target": coordinate_target,
        "expected_event_type": event_type,
        "current_event_type": event_type,
        "raw_event_type": event_type,
        "candidate_event_type": event_type,
        "candidate_raw_event_type": event_type,
        "submitted_event_type": event_type,
        "submitted_raw_event_type": event_type,
        "event_type_after": "Used",
        "event_consumed": True,
        "input_context": "editor-game-input-queue",
        "queue_api": "UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(Event)",
        "coordinate_space": "Player render-target pixels passed directly to EditorGUIUtility.QueueGameViewInputEvent",
        "editor_dispatch_schedule": "UnityEditor.EditorApplication.delayCall",
        "editor_input_scheduled": True,
        "editor_input_dispatch_invoked": True,
        "editor_game_input_queue_invoked": True,
        "editor_game_input_queue_returned": True,
        "native_event_queued": True,
        "native_event_observed": True,
        "editor_send_event_invoked": False,
        "editor_send_event_scheduled": False,
        "event_match": "native-marker",
        "event_marker": f"marker-{control_id}-{event_type}",
        "observed_event_marker": f"marker-{control_id}-{event_type}",
        "event_marker_matches": True,
        "root_pointer_matches": True,
        "control_id": control_id,
        "hot_control_before": hot_before,
        "hot_control_after": hot_after,
        "handler": handler,
        "request_id": request_id,
        "screen_x": x,
        "screen_y": y,
        "gui_x": x,
        "gui_y": y,
        "design_x": x,
        "design_y": y,
        "queued_mouse_x": x,
        "queued_mouse_y": y,
        "current_mouse_x": x,
        "current_mouse_y": y,
        "candidate_mouse_x": x,
        "candidate_mouse_y": y,
        "game_view_event_mouse_x": x,
        "game_view_event_mouse_y": y,
        "rect_x": left,
        "rect_y": top,
        "rect_width": width,
        "rect_height": height,
        "player_screen_width": 1920,
        "player_screen_height": 1080,
        "game_view_target_width": 1920,
        "game_view_target_height": 1080,
        "game_view_rt_width": 1920,
        "game_view_rt_height": 1080,
        "control_rect_x": left if control else 0,
        "control_rect_y": top if control else 0,
        "control_rect_width": width if control else 0,
        "control_rect_height": height if control else 0,
        "control_mouse_x": x if control else 0,
        "control_mouse_y": y if control else 0,
        "control_contains_mouse": control,
        "control_event_type": event_type if control else "",
        "control_raw_event_type": event_type if control else "",
        "control_event_type_after": "Used" if control else "",
        "control_hot_before": hot_before if control else 0,
        "control_hot_after": hot_after if control else 0,
    }
    return record


def _append_gesture(records, target, coordinate_target, point, rect, control_id, handler="", request_id="",
                    control=True):
    records.extend((
        _make_record("mouseDown", target, coordinate_target, point, rect, control_id, 0, control_id,
                     control=control),
        _make_record("mouseDrag", target, coordinate_target, point, rect, control_id, control_id, control_id,
                     control=control),
        _make_record("mouseUp", target, coordinate_target, point, rect, control_id, control_id, 0,
                     handler=handler, request_id=request_id, control=control),
    ))


def _append_search_keyboard(records, steps, card_id):
    keyboard_control = 777
    steps.append(f"collection.search focus assertion passed: collection.search.input keyboardControl={keyboard_control}")
    query = ""
    for character in card_id:
        key_code = ("Alpha" + character if character.isdigit() else
                    "Minus" if character in "_-" else character.upper())
        query += character
        for event_type, event_character, consumed, event_after in (
                ("KeyDown", ord(character), True, "Used"),
                ("keyUp", 0, False, "keyUp")):
            record = _make_record(event_type, "collection.search.input", "collection.search.input",
                                  (50, 50), (0, 0, 100, 100), 900, 0, 0)
            record.update({
                "event_match": "native-type-key-character",
                "root_pointer_matches": False,
                "event_marker_matches": False,
                "observed_event_marker": "",
                "event_consumed": consumed,
                "control_enabled": True,
                "event_type_after": event_after,
                "control_event_type_after": event_after,
            })
            records.append(record)
            steps.append(
                f"collection.search requested {event_type} keyCode={key_code} character=U+{event_character:04X}")
            steps.append(
                f"collection.search {event_type} keyCode={key_code} character=U+{event_character:04X} "
                f"focusBefore=collection.search.input keyboardBefore={keyboard_control} "
                f"focusAfter=collection.search.input keyboardAfter={keyboard_control} query={query}")
    steps.append(f"collection.search keyboard query assertion passed: {card_id}")
    steps.append(f"collection.search typed CardId assertion passed: {card_id}")


def _write_evidence(folder):
    folder = Path(folder)
    captures_dir = folder / "captures"
    captures_dir.mkdir(parents=True)
    operations = sorted(REQUIRED_ACCOUNT_OPERATIONS)
    requests = []
    request_ids = {}
    for index, operation in enumerate(operations):
        request_id = f"request-{index}"
        request_ids[operation] = request_id
        requests.append({
            "request_id": request_id,
            "operation": operation,
            "completed": True,
            "ok": True,
            "error": "",
            "opened_card_count": 5 if operation == "account_pack_open" else 0,
        })

    gui_inputs = []
    control_id = 10
    handler_requests = {
        "F024Register": "account_register",
        "ConfirmShopBuy": "account_shop_buy",
        "F024RequestCraft": "account_craft",
        "F024RequestDisenchant": "account_disenchant",
        "F024RequestDeckSave": "account_deck_save",
        "F024Logout": "account_logout",
        "F024Login": "account_login",
    }
    for handler in sorted(REQUIRED_GUI_HANDLERS - {
            "F024OpenPackFromDrag", "FocusCollectionSearch", "ClearCollectionSearch"}):
        request_id = request_ids[handler_requests[handler]] if handler in handler_requests else ""
        _append_gesture(gui_inputs, f"action:{handler}", f"action:{handler}", (50, 50),
                        (0, 0, 100, 100), control_id, handler, request_id)
        control_id += 1

    _append_gesture(gui_inputs, "collection.search.input", "collection.search.input", (50, 50),
                    (0, 0, 100, 100), control_id, "FocusCollectionSearch")
    control_id += 1
    steps = ["GUI flow completed"]
    _append_search_keyboard(gui_inputs, steps, "EX1_001")
    _append_gesture(gui_inputs, "collection.search.clear", "collection.search.clear", (50, 50),
                    (0, 0, 100, 100), control_id, "ClearCollectionSearch")
    steps.append("collection.search clear-button assertion passed")

    _append_gesture(gui_inputs, "packs.drag:1", "packs.source:1", (10, 10), (0, 0, 20, 20),
                    control_id, control=False)
    _append_gesture(gui_inputs, "packs.drag:1", "packs.outside", (50, 50), (40, 40, 20, 20),
                    control_id, control=False)
    _append_gesture(gui_inputs, "packs.drag:1", "packs.source:1", (10, 10), (0, 0, 20, 20),
                    control_id, control=False)
    _append_gesture(gui_inputs, "packs.drag:1", "packs.slot", (150, 150), (100, 100, 100, 100),
                    control_id, "F024OpenPackFromDrag", request_ids["account_pack_open"], control=False)

    screenshots = []
    for index in range(13):
        screenshots.append(f"capture-{index}.png")
    screenshots.extend(f"account-replay-pack-1-card-{index}.png" for index in range(1, 6))
    png_header = b"\x89PNG\r\n\x1a\n" + bytes(8) + struct.pack(">II", 1920, 1080)
    for name in screenshots:
        (captures_dir / name).write_bytes(png_header)

    report = {
        "result": "ok",
        "input_mode": "editor-game-input-queue",
        "steps": steps,
        "requests": requests,
        "gui_inputs": gui_inputs,
        "screenshots": [str(captures_dir / name) for name in screenshots],
    }
    report_path = folder / "account-replay.json"
    report_path.write_text(json.dumps(report), encoding="utf-8")
    return report_path


def _read_report(path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_report(path, report):
    path.write_text(json.dumps(report), encoding="utf-8")


def _analyze(path, capsys):
    exit_code = main(["--analyze-editor", str(path.parent)])
    output = json.loads(capsys.readouterr().out)
    return exit_code, output


def test_editor_evidence_cli_checks_receipts_pack_boundary_and_screenshots_read_only(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report_before = report_path.read_bytes()
    captures_before = sorted(path.name for path in (report_path.parent / "captures").iterdir())

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 0
    assert output["result"] == "ok"
    assert output["input_mode"] == "editor-game-input-queue"
    assert output["checks"]["editor_native_gameview_receipts"]
    assert output["checks"]["im_gui_hot_control"]
    assert output["checks"]["coordinate_consistency"]
    assert output["checks"]["pack_open_boundary"]
    assert output["checks"]["full_hd_screenshots"]
    assert output["checks"]["five_pack_reveal_frames"]
    assert output["checks"]["keyboard_search_input"]
    assert output["keyboard_search"]["card_id"] == "EX1_001"
    assert output["counts"]["keyboard_events"] == 14
    assert output["counts"]["mouse_gestures"] == 16
    assert output["pack_open"]["outside_release_without_request_count"] == 1
    assert output["pack_open"]["matched_successful_open_requests"] == 1
    assert output["pack_open"]["opened_card_count"] == 5
    assert "operating-system mouse" in output["evidence_limits"][0]
    assert "ASCII CardId was typed" in output["evidence_limits"][1]
    assert report_path.read_bytes() == report_before
    assert sorted(path.name for path in (report_path.parent / "captures").iterdir()) == captures_before


def test_editor_evidence_requires_gui_input_for_expected_save_rejection(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    report["requests"].append({"request_id": "rejected-save", "operation": "account_deck_save",
                               "completed": True, "ok": False, "error": "卡组不存在", "expected_failure": True})
    _append_gesture(report["gui_inputs"], "collection.deck-complete", "collection.deck-complete",
                    (50, 50), (0, 0, 100, 100), 87, "F024RequestDeckSave", "rejected-save")
    _write_report(report_path, report)
    exit_code, output = _analyze(report_path, capsys)
    assert exit_code == 0
    assert output["counts"]["expected_rejections"] == 1
    for record in report["gui_inputs"]:
        if record.get("request_id") == "rejected-save":
            record["request_id"] = ""
    _write_report(report_path, report)
    exit_code, output = _analyze(report_path, capsys)
    assert exit_code == 1
    assert not output["checks"]["successful_requests"]


def test_editor_evidence_rejects_non_editor_input_mode(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    report["input_mode"] = "unity-event-queue/native-OnGUI"
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert output["result"] == "failed"
    assert any("input_mode" in error for error in output["errors"])


def test_editor_evidence_rejects_missing_editor_queue_receipt(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    report["gui_inputs"][0]["editor_game_input_queue_returned"] = False
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["editor_native_gameview_receipts"]


def test_editor_evidence_rejects_keyboard_event_without_observed_queue_delivery(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    key_record = next(item for item in report["gui_inputs"] if item["expected_event_type"] == "KeyDown")
    key_record["native_event_observed"] = False
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["editor_native_gameview_receipts"]
    assert not output["checks"]["keyboard_search_input"]


def test_editor_evidence_rejects_keyboard_keycode_character_mismatch(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    requested = next(i for i, step in enumerate(report["steps"])
                     if step.startswith("collection.search requested KeyDown"))
    report["steps"][requested] = report["steps"][requested].replace("keyCode=E", "keyCode=F", 1)
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["keyboard_search_input"]
    assert any("keyCode/character" in error for error in output["errors"])


def test_editor_evidence_rejects_keyboard_focus_loss_and_query_drift(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    observed = next(i for i, step in enumerate(report["steps"])
                    if step.startswith("collection.search KeyDown"))
    report["steps"][observed] = report["steps"][observed].replace(
        "keyboardBefore=777", "keyboardBefore=0", 1).replace("query=E", "query=wrong", 1)
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["keyboard_search_input"]
    assert any("focus" in error or "query" in error for error in output["errors"])


def test_editor_evidence_rejects_broken_hot_control_chain(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    report["gui_inputs"][1]["hot_control_after"] = 0
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["im_gui_hot_control"]


def test_editor_evidence_rejects_gameview_coordinate_drift(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    report["gui_inputs"][0]["game_view_event_mouse_x"] += 20
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["coordinate_consistency"]


def test_editor_evidence_rejects_request_on_outside_pack_release(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    outside_release = next(item for item in report["gui_inputs"]
                           if item["coordinate_target"] == "packs.outside" and
                           item["expected_event_type"] == "mouseUp")
    outside_release["request_id"] = next(item["request_id"] for item in report["requests"]
                                         if item["operation"] == "account_pack_open")
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["pack_open_boundary"]


def test_editor_evidence_rejects_duplicate_pack_open_request(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    pack_request = next(item for item in report["requests"] if item["operation"] == "account_pack_open")
    duplicate = dict(pack_request, request_id="duplicate-pack-request")
    report["requests"].append(duplicate)
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["pack_open_boundary"]


def test_editor_evidence_rejects_pack_request_for_wrong_card_count(tmp_path, capsys):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    pack_request = next(item for item in report["requests"] if item["operation"] == "account_pack_open")
    pack_request["opened_card_count"] = 4
    _write_report(report_path, report)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["pack_open_boundary"]


@pytest.mark.parametrize("width,height", [(1280, 720), (1920, 0)])
def test_editor_evidence_rejects_non_full_hd_screenshot(tmp_path, capsys, width, height):
    report_path = _write_evidence(tmp_path / "editor-replay")
    report = _read_report(report_path)
    first_capture = Path(report["screenshots"][0])
    png_header = b"\x89PNG\r\n\x1a\n" + bytes(8) + struct.pack(">II", width, height)
    first_capture.write_bytes(png_header)

    exit_code, output = _analyze(report_path, capsys)

    assert exit_code == 1
    assert not output["checks"]["full_hd_screenshots"]


def test_editor_evidence_rejects_missing_report(tmp_path, capsys):
    exit_code = main(["--analyze-editor", str(tmp_path)])
    output = json.loads(capsys.readouterr().out)

    assert exit_code == 1
    assert output["result"] == "failed"
    assert any("account-replay.json" in error for error in output["errors"])
