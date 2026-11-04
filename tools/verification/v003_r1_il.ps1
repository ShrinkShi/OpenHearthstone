$ErrorActionPreference = 'Stop'
$probeCecil = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll'
Add-Type -Path $probeCecil
function Get-ProbeTypes($items) {
    foreach ($item in $items) {
        $item
        if ($item.HasNestedTypes) { Get-ProbeTypes $item.NestedTypes }
    }
}
$probeAssemblies = @{
    'Assembly-CSharp.dll' = @('SuperSpell','Spell','PowerTaskList',
        'SuperSpell/<WaitAndSpawnMissile>d__78','SuperSpell/<WaitAndSpawnImpact>d__89',
        'SuperSpell/<CompleteTasksUntilMetaData>d__126','SuperSpell/<CompleteTasksFromMetaData>d__129',
        'SuperSpell/<WaitForTasks>d__128')
    'Blizzard.T5.iTween.dll' = @('iTween')
}
foreach ($probeName in $probeAssemblies.Keys) {
    $probePath = Join-Path 'D:\Program Files (x86)\Hearthstone\Hearthstone_Data\Managed' $probeName
    $probeHash = (Get-FileHash -LiteralPath $probePath -Algorithm SHA256).Hash
    if ($probeName -eq 'Assembly-CSharp.dll' -and $probeHash -ne '3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728') { throw 'Original identity changed' }
    $probeAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($probePath)
    $probeCount = 0
    try {
        foreach ($probeType in (Get-ProbeTypes $probeAssembly.MainModule.Types)) {
            if ($probeType.FullName -notin $probeAssemblies[$probeName]) { continue }
            $probeCacheName = $probeType.FullName.Replace('/','.') -replace '[<>:"\\|?*]', '_'
            $probeCache = @(Get-Content -LiteralPath ("local/script-audit/$probeName.$probeCacheName.il.txt"))
            $probeLines = [System.Collections.Generic.List[string]]::new()
            foreach ($probeField in $probeType.Fields) {
                if ($probeField.HasConstant) { $probeLines.Add($probeField.FullName + ' = ' + $probeField.Constant) }
            }
            foreach ($probeMethod in $probeType.Methods) {
                $probeLines.Add($probeMethod.FullName)
                if ($probeMethod.HasBody) {
                    foreach ($probeInstruction in $probeMethod.Body.Instructions) { $probeLines.Add($probeInstruction.ToString()) }
                }
            }
            if (@(Compare-Object -ReferenceObject $probeCache -DifferenceObject $probeLines.ToArray() -SyncWindow 0).Count) { throw "IL mismatch: $($probeType.FullName)" }
            $probeCount++
            Write-Output ("MATCH " + $probeType.FullName + ': ' + $probeLines.Count + ' lines')
        }
        if ($probeCount -ne $probeAssemblies[$probeName].Count) { throw 'Missing requested type' }
        Write-Output ("SOURCE_SHA256 " + $probeName + '=' + $probeHash)
    } finally { $probeAssembly.Dispose() }
}
Write-Output 'V003_R1_STATIC_IL=PASS (Cecil read only; no original assembly execution)'
