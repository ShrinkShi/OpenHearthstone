$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'UnityClient/Assets/Scripts/HeroPowerText.cs')

$databasePath = Join-Path $root 'local/card-database.json'
$database = ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($databasePath))
$ids = @('HERO_01bp','HERO_02bp','HERO_03bp','HERO_04bp','HERO_05bp','HERO_06bp','HERO_07bp','HERO_08bp','HERO_09bp','HERO_10bp','HERO_11bp')
$checks = 0

function Assert-Check([bool]$condition,[string]$message) {
    if (-not $condition) { throw "Hero power text check failed: $message" }
    $script:checks++
}

$cards = @{}
foreach ($id in $ids) {
    $card = $database.cards | Where-Object CardId -eq $id | Select-Object -First 1
    Assert-Check ($null -ne $card -and -not [string]::IsNullOrEmpty($card.Name) -and $card.Cost -gt 0) "CardDef identity/name/cost $id"
    $cards[$id] = $card
}

$cases = @(
    @{ Id='HERO_05bp'; Marker='$2'; Value='2' },
    @{ Id='HERO_07bp'; Marker='$2'; Value='2' },
    @{ Id='HERO_08bp'; Marker='$1'; Value='1' },
    @{ Id='HERO_09bp'; Marker='#2'; Value='2' }
)
foreach ($case in $cases) {
    $card = $cards[$case.Id]
    $resolved = [HeroPowerText]::Resolve($case.Id,$card.Text,0,$false)
    $expected = ([string]$card.Text).Replace($case.Marker,$case.Value)
    Assert-Check ($resolved -ceq $expected -and -not $resolved.Contains($case.Marker)) "authored base placeholder $($case.Id)"
}

$mage = [HeroPowerText]::Resolve('HERO_08bp',$cards['HERO_08bp'].Text,3,$true)
Assert-Check ($mage -ceq ([string]$cards['HERO_08bp'].Text).Replace('$1','3')) 'known Mage dynamic damage'
$hunter = [HeroPowerText]::Resolve('HERO_05bp',$cards['HERO_05bp'].Text,4,$true)
Assert-Check ($hunter -ceq ([string]$cards['HERO_05bp'].Text).Replace('$2','4')) 'known Hunter dynamic damage'

$priest = [HeroPowerText]::Resolve('HERO_09bp',$cards['HERO_09bp'].Text,0,$false,4,$true)
Assert-Check ($priest -ceq ([string]$cards['HERO_09bp'].Text).Replace('#2','4')) 'known Priest dynamic healing'
$warlock = [HeroPowerText]::Resolve('HERO_07bp',$cards['HERO_07bp'].Text,4,$true,0,$false)
Assert-Check ($warlock -ceq ([string]$cards['HERO_07bp'].Text).Replace('$2','4')) 'known Warlock dynamic damage'

$interactionSource = [System.IO.File]::ReadAllText((Join-Path $root 'UnityClient/Assets/Scripts/BattleInteraction.cs'))
Assert-Check ($interactionSource.Contains('display_heal=e.display_heal') -and $interactionSource.Contains('display_heal_known=e.display_heal_known')) 'entity copy retains Priest display healing metadata'

foreach ($id in $ids | Where-Object { $_ -notin @('HERO_05bp','HERO_07bp','HERO_08bp','HERO_09bp') }) {
    $text = [string]$cards[$id].Text
    Assert-Check ($text -notmatch '[$#]' -and [HeroPowerText]::Resolve($id,$text,0,$false) -ceq $text) "no inferred placeholder $id"
}

$unknown = [HeroPowerText]::Resolve('HERO_05bp','unknown $7',0,$false)
Assert-Check ($unknown -ceq 'unknown $7') 'unmapped marker stays visible'
Write-Output "Hero power text checks passed: $checks"
