param(
    [string]$GameRoot = $env:HEARTHSTONE_GAME_ROOT,
    [string]$UabeaDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'work\uabea-v8\files'),
    [string]$OutputDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assetsresearch\collection-uabea-audit')
)
$ErrorActionPreference = 'Stop'
if (-not $GameRoot) { throw 'Set HEARTHSTONE_GAME_ROOT or pass -GameRoot to inspect the original installation.' }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Add-Type -Path (Join-Path $UabeaDir 'AssetsTools.NET.dll')
Add-Type -Path (Join-Path $UabeaDir 'Mono.Cecil.dll')
$bundlePath = Join-Path $GameRoot 'Data\Win\cardasset_base_qualityhigh_cm_global-prefab-0.unity3d'
$dllPath = Join-Path $GameRoot 'Hearthstone_Data\Managed\Assembly-CSharp.dll'
$manager = [AssetsTools.NET.Extra.AssetsManager]::new()
$null = $manager.LoadClassPackage((Join-Path $UabeaDir 'classdata.tpk'))
$bundle = $manager.LoadBundleFile($bundlePath, $true)
$assets = $manager.LoadAssetsFileFromBundle($bundle, 0, $false)
$rootId = [long]'-6398912150071876638'
$root = $manager.GetBaseField($assets, $rootId, 0)
if ($root['m_Name'].AsString -ne 'CollectionManager') { throw 'Unexpected prefab identity' }
$components = @()
foreach ($item in $root['m_Component']['Array'].Children) {
    $id = $item['component']['m_PathID'].AsLong
    $info = $assets.file.GetAssetInfo($id)
    $field = $manager.GetBaseField($assets, $info, 0)
    $script = $null
    $references = [ordered]@{}
    if ($info.TypeId -eq 114 -and $field['m_Script']['m_FileID'].AsInt -eq 0) {
        $scriptId = $field['m_Script']['m_PathID'].AsLong
        $scriptField = $manager.GetBaseField($assets, $scriptId, 0)
        $script = $scriptField['m_ClassName'].AsString
    }
    if ($script -eq 'CollectionManagerDisplay') {
        foreach ($name in @('m_bookBack','m_pageManager','m_manaTabManager','m_setFilterTrayContainer','m_filterButton','m_craftingModeButton')) {
            $value = $field[$name]
            if (-not $value.IsDummy) {
                $references[$name] = [ordered]@{file_id=$value['m_FileID'].AsInt; path_id="$($value['m_PathID'].AsLong)"}
            }
        }
    }
    $components += [ordered]@{path_id="$id"; class_id=$info.TypeId; script=$script;
        fields=@($field.Children | ForEach-Object { $_.TemplateField.Name }); references=$references}
}
$targets = @{
    CollectionManagerDisplay = @('Start','OnDoneEditingDeck','OnSetFilterButtonPressed','ShowCraftingTray','HideCraftingTray','InitCollectionWhenReady','EnableSetAndManaFiltersByViewMode')
    CollectionPageManager = @('OnDoneEditingDeck','FilterByManaCost','ShowCraftingModeCards','PageRight','PageLeft','OnCraftingTrayShown','OnCraftingTrayHidden')
    CollectionDeckTray = @('CompleteMyDeckButtonPress','FinishMyDeck','SaveCurrentDeckAndEnterDeckListMode','SaveCurrentDeck','AddCardToDeck','ShowDeckBigCard')
    SetFilterTray = @('Awake','Show','ClearFilter','SetFilterButtonGlowActive','<Awake>b__20_0','<Awake>b__20_1')
    CollectionManagerScene = @('Awake','Update','OnUIScreenLoaded')
    CollectionInputMgr = @('Awake','HandleKeyboardInput','StartDragWithActor')
    BookPageManager = @('Awake','Start','OnPageLeftPressed','OnPageRightPressed','PageRight','PageLeft','FlipToPage','TransitionPageWhenReady','TransitionPage','OnPageTurnComplete')
    PageTurn = @('Awake','SetupCamera','OnBeginContextRendering','TurnLeft','TurnRight')
}
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dllPath)
$rows = @()
try {
    foreach ($typeName in ($targets.Keys | Sort-Object)) {
        $type = $assembly.MainModule.Types | Where-Object FullName -eq $typeName | Select-Object -First 1
        if (-not $type) { throw "Missing type: $typeName" }
        foreach ($name in $targets[$typeName]) {
            $methods = @($type.Methods | Where-Object Name -eq $name)
            foreach ($method in $methods) {
                $calls = @($method.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt','newobj') } | ForEach-Object { $_.Operand.ToString() } | Sort-Object -Unique)
                $lines = @($method.FullName) + @($method.Body.Instructions | ForEach-Object ToString)
                $safeName = $name -replace '[<>:"\\|?*]', '_'
                $file = "$typeName.$safeName.$($method.MetadataToken.ToInt32()).il.txt"
                $lines | Set-Content -LiteralPath (Join-Path $OutputDir $file) -Encoding utf8
                $rows += [ordered]@{type=$typeName; method=$name; signature=$method.FullName; calls=$calls; il_file=$file}
            }
        }
    }
} finally { $assembly.Dispose() }
$report = [ordered]@{tool='UABEA v8 AssetsTools.NET + Mono.Cecil'; bundle=$bundlePath;
    bundle_sha256=(Get-FileHash $bundlePath -Algorithm SHA256).Hash; prefab_path_id="$rootId";
    prefab_name=$root['m_Name'].AsString; components=$components; assembly=$dllPath;
    assembly_sha256=(Get-FileHash $dllPath -Algorithm SHA256).Hash; methods=$rows}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDir 'report.json') -Encoding utf8
Write-Output "UABEA collection audit: $($components.Count) components, $($rows.Count) methods; $OutputDir"
