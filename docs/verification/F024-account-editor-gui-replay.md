# F024 Unity Editor GUI replay

This Editor-only route sends real IMGUI mouse events into the selected Unity GameView's Player. The preferred `--account-editor-native-input` mode uses Unity 2022.3's `EditorGUIUtility.QueueGameViewInputEvent(Event)` with Player render-target pixel coordinates and the GameView display index. The older `--account-editor-gui-replay` mode keeps `EditorWindow.SendEvent(Event)` as a comparison route. Both are separate from standalone Player `--account-gui-replay`; an API call alone does not pass. The Player must observe each event and the production control must acquire, hold, and release `GUIUtility.hotControl`.

The runner uses the existing `Assets/Scenes/Main.unity`, selects the existing fixed 1920×1080 GameView size, fits that render target to the selected view using Unity's `DefaultScaleForTargetInView`/`SnapZoom` path, and enters Play Mode with `PlayUnfocused`. It keeps the render-target resolution fixed while adjusting only the GameView zoom. It never creates, shows, or focuses a window. The supplied layout must already have a selected GameView; otherwise it writes a capability failure immediately. It refuses dirty scenes and a previously-used account profile. The account server must already be the isolated loopback `accounts_v1` fixture on `--port`; the caller owns that server process and its SQLite/token-key/log directories.

Run from PowerShell with an isolated output directory and an Editor layout containing an active GameView tab. This starts a private account service on an OS-assigned loopback port and checks its `accounts_v1` capability before launching Unity:

```powershell
$root = 'E:\HS\HearthstoneClone'
$unity = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runId = [guid]::NewGuid().ToString('N').Substring(0,8)
$output = Join-Path $root "work\f024-account-editor-gui-$stamp-$PID-$runId"
$profile = "f024-editor-$PID-$([guid]::NewGuid().ToString('N').Substring(0,8))"
$layout = 'E:\HS\HearthstoneClone\work\f024-editor-test-layout.dwlt'
$captures = Join-Path $output 'captures'
$logs = Join-Path $output 'logs'
New-Item -ItemType Directory -Force -Path $captures, $logs, (Join-Path $output 'matches') | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$python = Join-Path $root '.venv\Scripts\python.exe'
$server = Start-Process -FilePath $python -ArgumentList "-X utf8 -m game.server --host 127.0.0.1 --port $port --account-db `"$output\accounts.sqlite3`" --logdir `"$output\matches`"" -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'server.log') -RedirectStandardError (Join-Path $logs 'server-error.log')
$serverReady = $false
for ($attempt = 0; $attempt -lt 100 -and -not $serverReady; $attempt++) {
    if ($server.HasExited) { throw "Account server exited with $($server.ExitCode); inspect $logs\server.log" }
    try {
        $client = [Net.Sockets.TcpClient]::new()
        $client.ReceiveTimeout = 1000
        $client.Connect([Net.IPAddress]::Loopback, $port)
        $reader = [IO.StreamReader]::new($client.GetStream())
        $hello = $reader.ReadLine() | ConvertFrom-Json
        $serverReady = $hello.type -eq 'hello' -and $hello.capabilities -contains 'accounts_v1'
        $reader.Dispose(); $client.Dispose()
    } catch { Start-Sleep -Milliseconds 100 }
}
if (-not $serverReady) { Stop-Process -Id $server.Id -Force; throw 'Private server did not advertise accounts_v1.' }
$editor = $null
try {
    $arguments = "-projectPath `"$root\UnityClient`" -layout `"$layout`" -logFile `"$logs\editor.log`" -executeMethod F024AccountEditorReplayRunner.Start --account-replay --account-editor-native-input --account-replay-exit --profile $profile --port $port --screenshot-dir `"$captures`" --account-replay-report `"$output\account-replay.json`" --account-editor-timeout-seconds 900"
    $editor = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $editor.WaitForExit(1000 * 1000) | Out-Null
    if (-not $editor.HasExited) { Stop-Process -Id $editor.Id -Force; throw 'Unity Editor replay timed out after 1000 seconds.' }
    if ($editor.ExitCode -ne 0) { throw "Unity Editor replay failed with exit code $($editor.ExitCode); inspect $logs\editor.log" }
} finally {
    if ($editor -and -not $editor.HasExited) { Stop-Process -Id $editor.Id -Force }
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force }
}
```

Do not add `-batchmode`, `-quit`, or `-nographics`: this route needs Unity's running Editor Play Mode and GameView dispatch. The isolated server should be started before Unity with its own random port, database, token key, match directory, and log. Keep the Editor process hidden and wait on its exit code. The runner restores the GameView size/Play Mode behavior in the test Editor and exits with the replay report's result. Captures include registration, quests, the main menu, purchased shop state, pack inventory before opening, and the subsequent collection/economy flow.

Editor input is dispatched with `EditorApplication.delayCall` after the Player coroutine yields. In native-input mode, the report records the exact `QueueGameViewInputEvent` invocation/return, selected display index, submitted Player-pixel coordinates, requested/submitted event types, render-target dimensions, events observed by `ClientApp.OnGUI`, production handlers, and hot-control transitions. It does not log the generated password or session token. A report with `input_mode: editor-game-input-queue` remains a failure if the Player never observes the event or the production control does not handle it. The comparison route reports `input_mode: editor-game-view-send-event`; it retains the event's Player-pixel to GameView-point conversion and is not used as an automatic fallback.

To reproduce the older `SendEvent` comparison, replace `--account-editor-native-input` in the command with `--account-editor-gui-replay`. The runner requires exactly one of these two flags. The earlier SendEvent path did reach Player `OnGUI`, but its mapped pointer missed the production control under the tested GameView clipping transform; the native-input mode keeps its own result separate.

Unity can save the Editor's current layout when the test process exits. The runner restores the prior GameView size selection, zoom scale/translation, and Play Mode behavior before exit; it does not add a custom resolution or call `EditorPrefs`/`GameViewSizes.SaveToHDD`.
