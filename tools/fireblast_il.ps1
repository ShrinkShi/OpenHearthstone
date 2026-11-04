param([string[]]$Types = @('RenderUtils'))
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskProject 'local\script-audit'
Add-Type -Path 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll'
$taskOriginal = 'D:\Program Files (x86)\Hearthstone\Hearthstone_Data\Managed\Assembly-CSharp.dll'
$taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskOriginal)
try {
    foreach ($taskType in $taskAssembly.MainModule.Types) {
        if ($taskType.Name -notin $Types) { continue }
        $taskLines = [System.Collections.Generic.List[string]]::new()
        foreach ($taskMethod in $taskType.Methods) {
            if ($taskMethod.Name -notmatch 'Alpha') { continue }
            $taskLines.Add($taskMethod.FullName)
            if ($taskMethod.HasBody) {
                foreach ($taskInstruction in $taskMethod.Body.Instructions) { $taskLines.Add($taskInstruction.ToString()) }
            }
        }
        $taskLines | Set-Content -LiteralPath (Join-Path $taskOutput ('Assembly-CSharp.dll.' + $taskType.Name + '.Fireblast.il.txt')) -Encoding utf8
        Write-Output ($taskType.FullName + ': ' + $taskLines.Count + ' lines')
    }
} finally { $taskAssembly.Dispose() }
Get-FileHash -LiteralPath $taskOriginal -Algorithm SHA256
