$ErrorActionPreference = 'Stop'
$taskSource = 'D:\Program Files (x86)\Hearthstone\Hearthstone_Data\Managed\Assembly-CSharp.dll'
$taskExpectedHash = '3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728'
$taskHash = (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
if ($taskHash -ne $taskExpectedHash) { throw "Original assembly identity mismatch: $taskHash" }
Add-Type -Path 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll'
$taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskSource)
function Get-ProbeType($items) {
    foreach ($item in $items) {
        $item
        if ($item.HasNestedTypes) { Get-ProbeType $item.NestedTypes }
    }
}
$taskNames = @('SuperSpell','SuperSpell/<WaitAndSpawnImpact>d__89',
    'SuperSpell/<CompleteTasksUntilMetaData>d__126','SuperSpell/<CompleteTasksFromMetaData>d__129',
    'SuperSpell/<WaitForTasks>d__128','SpellUtils','SpellManager','Spell')
try {
    foreach ($taskType in (Get-ProbeType $taskAssembly.MainModule.Types)) {
        if ($taskType.FullName -notin $taskNames) { continue }
        $taskCacheName = $taskType.FullName.Replace('/','.') -replace '[<>:"\\|?*]', '_'
        $taskCache = @(Get-Content -LiteralPath ("local/script-audit/Assembly-CSharp.dll.$taskCacheName.il.txt"))
        $taskLines = [System.Collections.Generic.List[string]]::new()
        foreach ($taskField in $taskType.Fields) {
            if ($taskField.HasConstant) { $taskLines.Add($taskField.FullName + ' = ' + $taskField.Constant) }
        }
        foreach ($taskMethod in $taskType.Methods) {
            $taskLines.Add($taskMethod.FullName)
            if ($taskMethod.HasBody) {
                foreach ($taskInstruction in $taskMethod.Body.Instructions) { $taskLines.Add($taskInstruction.ToString()) }
            }
        }
        $taskDiff = @(Compare-Object -ReferenceObject $taskCache -DifferenceObject $taskLines.ToArray() -SyncWindow 0)
        if ($taskDiff.Count -ne 0) { throw "IL cache mismatch: $($taskType.FullName)" }
        Write-Output ("MATCH " + $taskType.FullName + ': ' + $taskLines.Count + ' lines')
    }
    Write-Output ("SOURCE_SHA256=" + $taskHash)
    Write-Output 'V002_R1_STATIC_IL=PASS (Cecil metadata read only)'
} finally { $taskAssembly.Dispose() }
