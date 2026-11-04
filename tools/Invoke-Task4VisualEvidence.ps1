param(
    [string]$Unity = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$task4Repo = Split-Path -Parent $PSScriptRoot
$task4Python = Join-Path $task4Repo '.venv\Scripts\python.exe'
$task4Work = Join-Path $task4Repo 'work\task4'
Push-Location -LiteralPath $task4Repo
try {
    foreach ($task4Step in @('inventory', 'scenes', 'shader-source')) {
        & $task4Python -X utf8 -m tools.task4_visual_evidence $task4Step
        if ($LASTEXITCODE -ne 0) { throw "Task4 step failed: $task4Step / $LASTEXITCODE" }
    }
    foreach ($task4Run in @('a', 'b')) {
        $task4Log = Join-Path $task4Work "gpu-$task4Run.log"
        $task4Output = Join-Path $task4Work "capture-$task4Run"
        $task4Args = @('-batchmode', '-quit', '-projectPath', (Join-Path $task4Repo 'UnityClient'),
            '-executeMethod', 'Task4VisualEvidence.Run', '-task4Inputs', (Join-Path $task4Work 'scenes'),
            '-task4Output', $task4Output, '-logFile', $task4Log)
        $task4Started = [DateTime]::UtcNow
        $task4Process = Start-Process -FilePath $Unity -ArgumentList $task4Args -WindowStyle Hidden -PassThru
        $task4Handle = $task4Process.Handle
        Write-Output "TASK4_UNITY_PID=$($task4Process.Id) RUN=$task4Run"
        $task4Process.WaitForExit()
        $task4Exit = $task4Process.ExitCode
        @{ pid = $task4Process.Id; exit_code = $task4Exit; started_utc = $task4Started.ToString('o');
            finished_utc = [DateTime]::UtcNow.ToString('o'); log = $task4Log; output = $task4Output } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $task4Work "process-$task4Run.json") -Encoding utf8
        Write-Output "TASK4_UNITY_EXIT=$task4Exit RUN=$task4Run"
        if ($task4Exit -ne 0) { throw "Task4 Unity failed: $task4Run / $task4Exit" }
    }
    & $task4Python -X utf8 -m tools.task4_visual_evidence assemble --repeat (Join-Path $task4Work 'capture-b')
    if ($LASTEXITCODE -ne 0) { throw "Task4 assembly failed: $LASTEXITCODE" }
}
finally { Pop-Location }
