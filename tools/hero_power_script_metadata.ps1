param(
    [string]$GameRoot = 'D:\Program Files (x86)\Hearthstone',
    [string]$Cecil = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll',
    [string[]]$TypeNames = @('OverrideCustomSpawnSpell', 'SuperSpell', 'Spell', 'SpellMissileInfo', 'HutongGames.PlayMaker.Actions.AudioPlayClipAction', 'HutongGames.PlayMaker.Actions.AudioPlayRandomClipAction')
)
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path $PSScriptRoot -Parent
$assemblyPath = Join-Path $GameRoot 'Hearthstone_Data\Managed\Assembly-CSharp.dll'
$outputPath = Join-Path $taskProject 'local\runtime\hero-power-script-metadata.json'
Add-Type -Path $Cecil

function Get-TaskTypes($Types) {
    foreach ($Type in $Types) {
        $Type
        if ($Type.HasNestedTypes) { Get-TaskTypes $Type.NestedTypes }
    }
}

$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
try {
    $available = @(Get-TaskTypes $assembly.MainModule.Types)
    $hash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $records = foreach ($name in $TypeNames) {
        $matches = @($available | Where-Object { $_.FullName -eq $name -or $_.Name -eq $name })
        foreach ($type in $matches) {
            $callSet = [System.Collections.Generic.HashSet[string]]::new()
            $selectedMethods = foreach ($method in $type.Methods) {
                if (-not $method.HasBody) { continue }
                $instructions = @($method.Body.Instructions | ForEach-Object { $_.ToString() })
                foreach ($instruction in $method.Body.Instructions) {
                    if ($instruction.OpCode.Name -in @('call', 'callvirt', 'newobj')) {
                        $null = $callSet.Add($instruction.Operand.ToString())
                    }
                }
                if ($method.Name -in @('OnEnter', 'Play', 'Delay', 'ChooseClip', 'ChooseVolume', 'ChoosePitch', 'FireMissileOnPath', 'GenerateMissilePath', 'GenerateMissilePathCenterPoint', 'get_TargetJoint')) {
                    [ordered]@{ method = $method.Name; instructions = $instructions }
                }
            }
            [ordered]@{
                type = $type.FullName
                base_type = if ($type.BaseType) { $type.BaseType.FullName } else { $null }
                assembly = 'Hearthstone_Data/Managed/Assembly-CSharp.dll'
                assembly_sha256 = $hash
                calls = @($callSet | Sort-Object)
                selected_method_il = @($selectedMethods)
            }
        }
        if (-not $matches.Count) {
            [ordered]@{ type = $name; base_type = $null; assembly = 'Hearthstone_Data/Managed/Assembly-CSharp.dll'; assembly_sha256 = $hash; error = 'type not found' }
        }
    }
    [ordered]@{
        method = 'Mono.Cecil metadata-only read; no official assembly execution'
        assembly_sha256 = $hash
        types = @($records)
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
    Write-Output ('STATIC TYPE METADATA: ' + $outputPath)
} finally {
    $assembly.Dispose()
}
