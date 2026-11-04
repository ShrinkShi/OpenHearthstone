param(
    [string]$GameRoot = 'D:\Program Files (x86)\Hearthstone',
    [string]$Cecil = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll',
    [string[]]$ExtraTypes = @(),
    [string[]]$ExtraAssemblies = @()
)
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskProject 'local\script-audit'
$taskAuditPath = Join-Path $taskProject 'manifests\original-script-audit.json'
$taskPrevious = if (Test-Path -LiteralPath $taskAuditPath) { Get-Content -LiteralPath $taskAuditPath -Raw | ConvertFrom-Json } else { $null }
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
Add-Type -Path $Cecil
$taskNative = Get-Content (Join-Path $taskProject 'manifests\native-effect-audit.json') -Raw | ConvertFrom-Json
$taskNames = @($taskNative.cards | ForEach-Object {$_.actions.PSObject.Properties.Name} | Sort-Object -Unique)
$taskNames += @('Spell','SpellState','SpellController','SpellStateAction','AnimationPlaythroughAction')
$taskNames += @('RenderToTexture','EaseFsmAction','Easing','OffscreenCamera','SuperSpell')
$taskNames += @('RenderToTextureUtils','RenderCommandLists','LightWeightCamera')
$taskNames += @('Glow','ScreenEffectGlow','GetAmbientColorAction','SetAmbientColorAction','ResetAmbientColorAction','PortraitLighting')
$taskNames += @('Board')
$taskNames += @('SpellAction','ActionData','FsmEvent','FsmUtility')
$taskNames += @('NamedVariable')
$taskNames += @('AnimateFsmAction')
$taskNames += @('<DelaySpellFinished>d__4')
$taskNames += $ExtraTypes
function Get-TaskTypes($taskTypes) {
    foreach ($taskTypeItem in $taskTypes) {
        $taskTypeItem
        if ($taskTypeItem.HasNestedTypes) { Get-TaskTypes $taskTypeItem.NestedTypes }
    }
}
$taskRows = [System.Collections.Generic.List[object]]::new()
$taskManaged = Join-Path $GameRoot 'Hearthstone_Data\Managed'
foreach ($taskName in (@('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','Blizzard.T5.Game.dll','Blizzard.T5.PlayMaker.Runtime.dll','Blizzard.T5.PlayMakerCustomActions.Runtime.dll','PlayMaker.dll') + $ExtraAssemblies)) {
    $taskPath = Join-Path $taskManaged $taskName
    # Cecil reads metadata/IL. Never load or execute the official game assembly.
    $taskDefinition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskPath)
    try {
        foreach ($taskType in (Get-TaskTypes $taskDefinition.MainModule.Types)) {
            if ($taskType.Name -notin $taskNames -and $taskType.FullName -notin $taskNames) { continue }
            $taskLines = [System.Collections.Generic.List[string]]::new()
            $taskCalls = [System.Collections.Generic.HashSet[string]]::new()
            foreach ($taskField in $taskType.Fields) {
                if ($taskField.HasConstant) { $taskLines.Add($taskField.FullName + ' = ' + $taskField.Constant) }
            }
            foreach ($taskMethod in $taskType.Methods) {
                $taskLines.Add($taskMethod.FullName)
                if (-not $taskMethod.HasBody) { continue }
                foreach ($taskInstruction in $taskMethod.Body.Instructions) {
                    $taskLines.Add($taskInstruction.ToString())
                    if ($taskInstruction.OpCode.Name -in @('call','callvirt','newobj')) {
                        $null = $taskCalls.Add($taskInstruction.Operand.ToString())
                    }
                }
            }
            $taskFileType = if ($taskType.IsNested) {$taskType.FullName.Replace('/','.')} else {$taskType.Name}
            $taskFileType = $taskFileType -replace '[<>:"\\|?*]', '_'
            $taskFile = $taskName + '.' + $taskFileType + '.il.txt'
            $taskLines | Set-Content -LiteralPath (Join-Path $taskOutput $taskFile) -Encoding utf8
            $taskRows.Add([pscustomobject]@{
                assembly=$taskName; assembly_sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash
                type=$taskType.FullName; cache=('local/script-audit/'+$taskFile)
                methods=$taskType.Methods.Count; calls=@($taskCalls | Sort-Object)
                restored=$false
            })
        }
    } finally { $taskDefinition.Dispose() }
}
$taskMerged = @{}
$taskKeys = [System.Collections.Generic.List[string]]::new()
foreach ($taskRow in @($taskPrevious.scripts) + @($taskRows.ToArray())) {
    if ($null -eq $taskRow) { continue }
    $taskKey = $taskRow.assembly + ':' + $taskRow.type
    if (-not $taskMerged.ContainsKey($taskKey)) { $taskKeys.Add($taskKey) }
    $taskMerged[$taskKey] = $taskRow
}
[ordered]@{notes=@('IL is evidence, not executed original scripts. No official assembly is loaded into the client.'); scripts=@($taskKeys | ForEach-Object { $taskMerged[$_] }); kind='static-il-audit'} |
    ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $taskAuditPath -Encoding utf8
Write-Output ('STATIC SCRIPT AUDIT: '+$taskRows.Count+' types')
