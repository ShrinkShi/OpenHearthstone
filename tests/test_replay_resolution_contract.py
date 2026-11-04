from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_fireblast_replay_requires_and_waits_for_explicit_player_resolution():
    replay = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    h11 = (ROOT / 'UnityClient/Assets/Scripts/H11PlayerReplay.cs').read_text(encoding='utf-8')
    fireblast_runner = (ROOT / 'tools/fireblast_evidence.py').read_text(encoding='utf-8')
    h11_runner = (ROOT / 'tools/death_knight_player_evidence.py').read_text(encoding='utf-8')

    install = replay.split('static void Install()', 1)[1].split('void ', 1)[0]
    tick = replay.split('void Tick()', 1)[1].split('bool IsPairedPriestRole', 1)[0]
    ensure_resolution = h11.split('bool EnsureResolution()', 1)[1].split('bool resolutionRequested;', 1)[0]

    # Replay-only setup keeps ordinary Player startup untouched.
    assert 'if(role=="")return;' in install
    assert 'Explicit positive -screen-width and -screen-height are required for replay' in tick
    assert 'requestedWidth<=0||requestedHeight<=0' in tick
    assert tick.count('Screen.SetResolution(') == 1
    assert 'Screen.SetResolution(requestedWidth,requestedHeight,FullScreenMode.Windowed)' in tick
    assert tick.index('if(!resolutionRequested)') < tick.index('Screen.SetResolution(')
    assert 'if(Screen.width!=requestedWidth||Screen.height!=requestedHeight)' in tick
    assert 'Time.realtimeSinceStartup-started>15' in tick
    assert 'Actual Player resolution differs from explicit request:' in tick

    # H11 uses the same one-request, wait-for-match, bounded-failure lifecycle.
    assert 'Explicit -screen-width and -screen-height are required' in ensure_resolution
    assert ensure_resolution.count('Screen.SetResolution(') == 1
    assert 'Screen.width==width&&Screen.height==height' in ensure_resolution
    assert 'Time.realtimeSinceStartup-started>15' in ensure_resolution
    assert 'Player resolution differs from requested size:' in ensure_resolution

    # Both harnesses pass matching Unity Player flags; source presence does not prove runtime acceptance.
    assert "'-screen-width', size.split('x')[0], '-screen-height', size.split('x')[1], '-screen-fullscreen', '0'" in fireblast_runner
    assert "'-screen-width', width" in h11_runner
    assert "'-screen-height', height, '-screen-fullscreen', '0'" in h11_runner
