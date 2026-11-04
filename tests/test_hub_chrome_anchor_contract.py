"""Static coordinate contract; real IMGUI events still require a Unity run."""
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "UnityClient" / "Assets" / "Scripts"


def source(name):
    return (SCRIPTS / name).read_text(encoding="utf-8")


def viewport(width, height, menu):
    scale = min(height / 1000, width / (1120 if menu else 1600))
    return scale, (width - 1600 * scale) / 2, (height - 1000 * scale) / 2


def to_client(rect, transform):
    scale, x, y = transform
    return (rect[0] * scale + x, rect[1] * scale + y,
            rect[2] * scale, rect[3] * scale)


def contains(rect, point):
    x, y, width, height = rect
    return x <= point[0] < x + width and y <= point[1] < y + height


def overlaps(a, b):
    return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]


@pytest.mark.parametrize("size", [(1600, 1000), (1920, 1080), (1280, 960), (3440, 1440), (600, 1000)])
@pytest.mark.parametrize("menu", [True, False], ids=["menu", "fit"])
def test_window_anchors_and_replay_centers(size, menu):
    width, height = size
    transform = viewport(width, height, menu)
    scale, offset_x, offset_y = transform
    left = -offset_x / scale
    right = (width - offset_x) / scale
    bottom = (height - offset_y) / scale
    friend = (left + 8, bottom - 70, 80, 40)
    gear = (right - 100, bottom - 64, 84, 52)
    currency = (right - 332, bottom - 75, 224, 62)
    dust = (currency[0] - 16, currency[1], 116, currency[3])
    gold = (currency[0] + 108, currency[1], 116, currency[3])
    friend_px, gear_px = to_client(friend, transform), to_client(gear, transform)

    assert friend_px[0] == pytest.approx(8 * scale)
    assert height - (friend_px[1] + friend_px[3]) == pytest.approx(30 * scale)
    assert width - (gear_px[0] + gear_px[2]) == pytest.approx(16 * scale)
    assert height - (gear_px[1] + gear_px[3]) == pytest.approx(12 * scale)
    assert not overlaps(friend, dust)
    assert not overlaps(dust, gold)
    assert not overlaps(gold, gear)

    for rect in (friend, gear):
        client = to_client(rect, transform)
        # The registered local center is transformed once when a replay event is queued.
        center = (client[0] + client[2] / 2, client[1] + client[3] / 2)
        root = ((center[0] - offset_x) / scale, (center[1] - offset_y) / scale)
        assert contains(rect, root)
        assert root == pytest.approx((rect[0] + rect[2] / 2, rect[1] + rect[3] / 2))
        assert not contains(rect, (rect[0] - .01, root[1]))
        assert not contains(rect, (rect[0] + rect[2], root[1]))


@pytest.mark.parametrize("size", [
    (1600, 1000), (1680, 1020),
    (1920, 1080), (1280, 960), (3440, 1440), (600, 1000),
])
def test_collection_back_does_not_overlap_settings(size):
    # Fixed collection button only; PackOpening uses a projected native button.
    width, height = size
    scale, offset_x, offset_y = viewport(width, height, False)
    right = (width - offset_x) / scale
    bottom = (height - offset_y) / scale
    gear = (right - 100, bottom - 64, 84, 52)
    assert not overlaps(gear, (1380, 878, 160, 42))


def test_production_sources_use_the_verified_coordinate_contract():
    chrome = source("ClientApp.HubChrome.cs")
    app = source("ClientApp.cs")
    view = source("DesignViewport.cs")
    replay = source("ClientApp.AccountReplayInput.cs")
    for snippet in (
        "Matrix4x4 inverse = GUI.matrix.inverse;",
        "inverse.MultiplyPoint3x4(Vector3.zero)",
        "inverse.MultiplyPoint3x4(new Vector3(Screen.width, Screen.height, 0))",
        "new Rect(window.xMin + 8, window.yMax - 70, 80, 40)",
        "new Rect(window.xMax - 332, window.yMax - 75, 224, 62)",
        "new Rect(window.xMax - 100, window.yMax - 64, 84, 52)",
        "new Rect(currency.x - 16, currency.y, 116, currency.height)",
        "new Rect(currency.x + 108, currency.y, 116, currency.height)",
    ):
        assert snippet in chrome
    for target, rect, widget in (("social.open", "friendRect", "chrome.friends"),
                                 ("settings.open", "gearRect", "chrome.gear")):
        assert f'F026DrawWidget({rect}, "{widget}")' in chrome
        assert f'F024ReplayGuiRegisterTarget("{target}", {rect})' in chrome
        assert f'F024ReplayGuiObserveControlBefore("{target}", {rect})' in chrome
        assert f'GUI.Button({rect}, GUIContent.none, GUIStyle.none)' in chrome
    assert 'page=="menu"?DesignViewport.Menu.Matrix:DesignViewport.Fit(1600,1000)' in app
    assert 'new DesignViewport(Screen.width,Screen.height,1600,1000,1120)' in view
    assert 'Mathf.Min(WindowHeight/height,WindowWidth/coreWidth)' in view
    assert 'Mathf.Min(Screen.width/width,Screen.height/height)' in view
    assert 'f024ReplayGuiRootMatrix.inverse * currentMatrix' in replay
    assert 'point.guiMatrix.MultiplyPoint3x4(' in replay
    assert 'new Vector3(point.rect.center.x, point.rect.center.y, 0f)' in replay
    assert 'queued.mousePosition = input.clientPosition;' in replay
    assert 'new Rect(1380, 878, 160, 42)' in source("ClientApp.HubPages.cs")
