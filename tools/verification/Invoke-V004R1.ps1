param([string]$Unity = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe',
    [string]$Work = 'work\v004-r1')
$ErrorActionPreference = 'Stop'
$v004Repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
# V004-r2沿用r1采集探针，仅新增隔离输出参数；旧证据不覆盖。
$v004Work = Join-Path $v004Repo $Work
Push-Location -LiteralPath $v004Repo
try {
    foreach ($v004Run in @('a', 'b')) {
        $v004Log = Join-Path $v004Work "gpu-$v004Run.log"
        $v004Output = Join-Path $v004Work "capture-$v004Run"
        $v004Args = @('-batchmode', '-quit', '-projectPath', (Join-Path $v004Repo 'UnityClient'),
            '-executeMethod', 'V004R1Checks.Run', '-task4Inputs', (Join-Path $v004Work 'scenes'),
            '-task4Output', $v004Output, '-logFile', $v004Log)
        $v004Started = [DateTime]::UtcNow
        $v004Process = Start-Process -FilePath $Unity -ArgumentList $v004Args -WindowStyle Hidden -PassThru
        $v004Handle = $v004Process.Handle
        Write-Output "V004_UNITY_PID=$($v004Process.Id) RUN=$v004Run"
        $v004Process.WaitForExit()
        $v004Exit = $v004Process.ExitCode
        @{pid=$v004Process.Id; exit_code=$v004Exit; started_utc=$v004Started.ToString('o');
            finished_utc=[DateTime]::UtcNow.ToString('o'); log=$v004Log; output=$v004Output} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $v004Work "process-$v004Run.json") -Encoding utf8
        Write-Output "V004_UNITY_EXIT=$v004Exit RUN=$v004Run"
        if ($v004Exit -ne 0) { throw "V004 Unity failed: $v004Run / $v004Exit" }
    }
}
finally { Pop-Location }
