$ErrorActionPreference='Stop'
$vRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$vOriginal='D:\Program Files (x86)\Hearthstone\Hearthstone_Data\Managed\Assembly-CSharp.dll'
Add-Type -Path 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll'
$vAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($vOriginal)
try {
    $vLines=[System.Collections.Generic.List[string]]::new()
    foreach($vType in $vAssembly.MainModule.Types){
        if($vType.Name -notin @('TargetReticleManager','SuperSpell','SuperSpellStart','InputManager','ActorNames','UberText')){continue}
        foreach($vMethod in $vType.Methods){
            if($vMethod.Name -notmatch 'Update|Birth|Cancel|StartSpell|ActivatePlaySpell|GetHistoryActor|FontSize|OnAction|TargetMode|TargetArrow'){continue}
            $vLines.Add($vMethod.FullName)
            if($vMethod.HasBody){foreach($vInstruction in $vMethod.Body.Instructions){$vLines.Add($vInstruction.ToString())}}
        }
    }
    $vLines | Set-Content -LiteralPath (Join-Path $vRoot 'work/v005-r1-original.il.txt') -Encoding utf8
    "V005_STATIC_IL_LINES=$($vLines.Count)"
} finally {$vAssembly.Dispose()}
Get-FileHash -LiteralPath $vOriginal
