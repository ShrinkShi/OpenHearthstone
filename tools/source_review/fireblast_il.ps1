param(
    [string]$OriginalDll = 'D:\Program Files (x86)\Hearthstone\Hearthstone_Data\Managed\Assembly-CSharp.dll',
    [string]$CecilDll = 'F:\Program Files\unity\2022.3.60f1c1\Editor\Data\Managed\Unity.Cecil.dll',
    [string]$Output = (Join-Path $PSScriptRoot '..\..\work\fireblast-source-review')
)
$ErrorActionPreference = 'Stop'
$null = New-Item -ItemType Directory -Path $Output -Force
Add-Type -Path $CecilDll
$taskOriginal = $OriginalDll
$taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskOriginal)
function Types($items) {
    foreach ($item in $items) {
        $item
        if ($item.HasNestedTypes) { Types $item.NestedTypes }
    }
}
try {
    $taskTypes = @(Types $taskAssembly.MainModule.Types)
    $taskLines = [System.Collections.Generic.List[string]]::new()
    $taskLines.Add('Source SHA256=' + (Get-FileHash -LiteralPath $taskOriginal -Algorithm SHA256).Hash)
    foreach ($taskType in $taskTypes) {
        if ($taskType.FullName -eq 'RenderToTexture') {
            foreach ($taskMethod in $taskType.Methods) {
                if ($taskMethod.Name -notin @('.ctor','OnEnable','OnDisable','OnDestroy','CleanUp','RenderTex','LateUpdate','Init','CreateTexture','CreateCamera','CalcTextureSize','CalcWorldWidthHeightScale','OrthoSize','SetupForRender','SetupMaterial','CreateMeshPlane','CreateRenderPlane','CreateBloomPlane','CreateBloomCapturePlane','CreateBloomCaptureCamera','RenderBloom','Render')) { continue }
                $taskLines.Add($taskMethod.FullName)
                foreach ($taskInstruction in $taskMethod.Body.Instructions) { $taskLines.Add($taskInstruction.ToString()) }
            }
        }
        if ($taskType.FullName -eq '<PrivateImplementationDetails>') {
            foreach ($taskField in $taskType.Fields) {
                if ($taskField.Name -ne 'F743410F068E382F5CD38968D9B625EC8FE5A17DC235B579429F25AC83330D1D') {continue}
                $taskLines.Add('PLANE_TRIANGLES bytes=' + [Convert]::ToHexString($taskField.InitialValue))
                $taskIndices = for ($taskIndex=0; $taskIndex -lt $taskField.InitialValue.Length; $taskIndex+=4) {
                    [BitConverter]::ToInt32($taskField.InitialValue,$taskIndex)
                }
                $taskLines.Add('PLANE_TRIANGLES=' + ($taskIndices -join ','))
            }
        }
        if ($taskType.FullName -like 'RenderToTexture/*') {
            $taskLines.Add($taskType.FullName)
            foreach ($taskField in $taskType.Fields) { if ($taskField.HasConstant) {$taskLines.Add($taskField.Name + '=' + $taskField.Constant)} }
        }
        if ($taskType.FullName -eq 'Board') {
            foreach ($taskMethod in $taskType.Methods) {
                if (!$taskMethod.HasBody -or $taskMethod.Name -notin @('.ctor','ResetAmbientColor','RaiseTheLights','UpdateAmbientColor')) {continue}
                $taskLines.Add($taskMethod.FullName)
                foreach ($taskInstruction in $taskMethod.Body.Instructions) {$taskLines.Add($taskInstruction.ToString())}
            }
        }
    }
    [IO.File]::WriteAllLines((Join-Path $Output 'RenderToTexture.source.il.txt'),$taskLines)
    $taskLines = [System.Collections.Generic.List[string]]::new()
    foreach ($taskType in $taskTypes) {
        foreach ($taskMethod in $taskType.Methods) {
            if (!$taskMethod.HasBody) {continue}
            $taskHits = @($taskMethod.Body.Instructions | Where-Object {$_.Operand -and $_.Operand.ToString() -match 'Board::(RaiseTheLights|ResetAmbientColor)|Actor::(SetLit|SetUnlit)'})
            if ($taskHits.Count -eq 0) {continue}
            $taskLines.Add($taskMethod.FullName)
            foreach ($taskInstruction in $taskMethod.Body.Instructions) {$taskLines.Add($taskInstruction.ToString())}
        }
    }
    [IO.File]::WriteAllLines((Join-Path $Output 'Lighting.callers.il.txt'),$taskLines)
    Write-Output ('RT source lines=' + (Get-Content (Join-Path $Output 'RenderToTexture.source.il.txt')).Count)
    Write-Output ('Lighting caller lines=' + $taskLines.Count)
} finally {$taskAssembly.Dispose()}
