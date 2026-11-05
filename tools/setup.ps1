param(
    [switch]$RuntimeOnly,
    [switch]$NoLaunch,
    [switch]$SkipUnity,
    [string]$ReleaseBase = 'https://github.com/ShrinkShi/OpenHearthstone/releases/download/openh-assets-20261105'
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$stage = Join-Path $root 'work\setup-downloads'
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$base = $ReleaseBase.TrimEnd('/')
$sevenHash = '256feca8e274e5da655e2a284fabafd9f554365eb164862089dacd4e8276d282'
$pythonHash = '5ee42c4eee1e6b4464bb23722f90b45303f79442df63083f05322f1785f5fdde'
$assetStudioHash = 'badc1aa356fe0623ce4053d1a81c22f852c72cb882ca30f1876a37d930d2fbc8'
$uabeaHash = '0623ab1dc099a6397c3a7e72782b91405e60b286c6825fb6fa21e553ff2ea580'

function Get-VerifiedFile([string]$url, [string]$path, [string]$sha256) {
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha256) { return }
    $partial = $path + '.download'
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            Write-Host "Downloading $([IO.Path]::GetFileName($path)) (attempt $attempt/4)"
            Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $partial
            if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $sha256) {
                throw "SHA256 mismatch: $url"
            }
            Move-Item -LiteralPath $partial -Destination $path -Force
            return
        } catch {
            if ($attempt -eq 4) { throw }
            Write-Warning "Download failed; retrying: $($_.Exception.Message)"
            Start-Sleep -Seconds (5 * $attempt)
        }
    }
}

function Get-Python311 {
    $venvPython = Join-Path $root '.venv\Scripts\python.exe'
    if (Test-Path -LiteralPath $venvPython) {
        & $venvPython -c 'import sys; assert sys.version_info[:2] == (3, 11)' 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Existing .venv is not usable Python 3.11; preserve it and repair it before continuing.' }
        return $venvPython
    }
    $bootstrap = Join-Path $root 'work\python311\python.exe'
    $py = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($py) {
        & $py.Source -3.11 -c 'import sys; print(sys.executable)' 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            & $py.Source -3.11 -m venv (Join-Path $root '.venv')
            if ($LASTEXITCODE -ne 0) { throw 'Could not create the Python environment.' }
            return $venvPython
        }
    }
    foreach ($key in @('HKCU:\Software\Python\PythonCore\3.11\InstallPath',
                       'HKLM:\Software\Python\PythonCore\3.11\InstallPath')) {
        $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($entry -and $entry.ExecutablePath -and (Test-Path -LiteralPath $entry.ExecutablePath)) {
            & $entry.ExecutablePath -c 'import sys; assert sys.version_info[:2] == (3, 11)' 2>$null
            if ($LASTEXITCODE -eq 0) {
                & $entry.ExecutablePath -m venv (Join-Path $root '.venv')
                if ($LASTEXITCODE -ne 0) { throw 'Could not create the Python environment.' }
                return $venvPython
            }
        }
    }
    if (-not (Test-Path -LiteralPath $bootstrap)) {
        $installer = Join-Path $stage 'python-3.11.9-amd64.exe'
        Get-VerifiedFile 'https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.exe' $installer $pythonHash
        $target = Join-Path $root 'work\python311'
        $p = Start-Process -FilePath $installer -ArgumentList @('/quiet','InstallAllUsers=0','PrependPath=0','Include_test=0',('TargetDir="' + $target + '"')) -WindowStyle Hidden -PassThru -Wait
        if ($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $bootstrap)) { throw "Python installer failed: $($p.ExitCode)" }
    }
    & $bootstrap -m venv (Join-Path $root '.venv')
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the Python environment.' }
    return $venvPython
}

function Find-UnityEditor {
    $candidates = @($env:UNITY_EDITOR)
    $hubFile = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hubFile) {
        try {
            $editors = Get-Content -Raw -LiteralPath $hubFile | ConvertFrom-Json
            $candidates += @($editors.data | Where-Object { $_.version -eq '2022.3.60f1c1' } | ForEach-Object { $_.location })
        } catch { Write-Warning 'Could not read Unity Hub editor list.' }
    }
    foreach ($drive in Get-PSDrive -PSProvider FileSystem) {
        if ($drive.Root -notmatch '^[A-Za-z]:\\$') { continue }
        $candidates += Join-Path $drive.Root 'Program Files\Unity\Hub\Editor\2022.3.60f1c1\Editor\Unity.exe'
        $candidates += Join-Path $drive.Root 'Program Files\unity\2022.3.60f1c1\Editor\Unity.exe'
        $candidates += Join-Path $drive.Root 'Program Files\Unity 2022.3.60f1c1\Editor\Unity.exe'
    }
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { return $candidate }
    }
    return $null
}

function Ensure-UnityEditor {
    $editor = Find-UnityEditor
    if ($editor) { Write-Host "Unity Editor: $editor"; return }
    if ($SkipUnity) { Write-Warning 'Unity Editor setup skipped; the bundled player can still start.'; return }
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host 'Installing the exact Unity Editor version; Windows may request administrator confirmation.'
        & $winget.Source install --id Unity.Unity.CN.2022 --exact --version 2022.3.60f1c1 --silent --accept-package-agreements --accept-source-agreements
        $editor = Find-UnityEditor
        if ($editor) { Write-Host "Unity Editor: $editor"; return }
    }
    $hub = Join-Path ${env:ProgramFiles} 'Unity Hub\Unity Hub.exe'
    if (-not (Test-Path -LiteralPath $hub)) {
        if ($winget) {
            Write-Host 'Installing Unity Hub (Windows may request confirmation).'
            & $winget.Source install --id Unity.UnityHub --exact --silent --accept-package-agreements --accept-source-agreements
        }
    }
    if (Test-Path -LiteralPath $hub) {
        Write-Host 'Installing Unity Editor 2022.3.60f1c1 through Unity Hub; this may take a while.'
        $p = Start-Process -FilePath $hub -ArgumentList @('--','--headless','install','--version','2022.3.60f1c1','--changeset','469b28c2de88') -WindowStyle Hidden -PassThru -Wait
        $editor = Find-UnityEditor
        if ($editor) { Write-Host "Unity Editor: $editor"; return }
        Write-Warning "Unity Hub could not install the required editor automatically (exit $($p.ExitCode))."
    }
    Write-Warning 'Install Unity 2022.3.60f1c1 and activate a Unity license in Unity Hub to edit/build the client.'
}

function Restore-LocalLayout {
    $local = Join-Path $root 'local'
    New-Item -ItemType Directory -Force -Path $local | Out-Null
    $links = @{
        runtime = 'assets\runtime'; cache = 'assets\cache'; visual = 'assetsresearch\visual'
        'collection-uabea-audit' = 'assetsresearch\collection-uabea-audit'
        'compiled-shader-audit' = 'assetsresearch\compiled-shader-audit'
        'effect-source-audit' = 'assetsresearch\effect-source-audit'
        'script-audit' = 'assetsresearch\script-audit'
        'timeline-source-audit' = 'assetsresearch\timeline-source-audit'
        probes = 'assetsresearch\probes'
    }
    foreach ($name in $links.Keys) {
        $link = Join-Path $local $name
        $target = Join-Path $root $links[$name]
        if (-not (Test-Path -LiteralPath $link) -and (Test-Path -LiteralPath $target)) {
            New-Item -ItemType Junction -Path $link -Target $target | Out-Null
        }
    }
    foreach ($name in @('card-database.json', 'ui-calibration.json')) {
        $destination = Join-Path $local $name
        $source = Join-Path $root ('assets\' + $name)
        if (-not (Test-Path -LiteralPath $destination) -and (Test-Path -LiteralPath $source)) {
            Copy-Item -LiteralPath $source -Destination $destination
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $local 'runtime\registry.json'))) {
        throw 'The existing local runtime directory does not contain a registry; preserve and inspect it.'
    }
}

try {
    $needRuntime = -not ((Test-Path 'assets\runtime\registry.json') -and (Test-Path 'assets\card-database.json') -and (Test-Path 'build\client\HearthstoneClone.exe'))
    $fullMarker = Join-Path $stage 'full-assets.complete'
    $needFull = -not $RuntimeOnly -and -not (Test-Path -LiteralPath $fullMarker)
    if ($needRuntime -or $needFull) {
        $manifestPath = Join-Path $stage 'manifest.json'
        Invoke-WebRequest -UseBasicParsing -Uri "$base/manifest.json" -OutFile $manifestPath
        $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        $seven = Join-Path $stage '7zr.exe'
        Get-VerifiedFile "$base/7zr.exe" $seven $sevenHash
        $groups = @()
        if ($needRuntime) { $groups += 'runtime' }
        if ($needFull) { $groups += 'full'; $groups += 'research' }
        foreach ($group in $groups) {
            $parts = @($manifest.$group | Sort-Object name)
            if ($parts.Count -eq 0) { throw "Missing $group archive in release manifest." }
            foreach ($part in $parts) {
                if ($part.name -notmatch '^openh-(runtime-v2|full-v2|research)\.7z\.\d{3}$' -or $part.sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid release manifest entry.' }
                Get-VerifiedFile "$base/$($part.name)" (Join-Path $stage $part.name) $part.sha256
            }
            Write-Host "Extracting $group assets (existing files are preserved)."
            & $seven x (Join-Path $stage $parts[0].name) ('-o' + $root) -aos -y
            if ($LASTEXITCODE -ne 0) { throw "Failed to extract $group archive." }
        }
        if ($needFull) { Set-Content -LiteralPath $fullMarker -Value 'completed' -Encoding ASCII }
    }
    $python = Get-Python311
    Write-Host 'Installing Python dependencies.'
    & $python -m pip install -r (Join-Path $root 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }
    & $python -X utf8 -m tools.prepare_data
    if ($LASTEXITCODE -ne 0) { throw 'Card data preparation failed.' }
    & $python -m tools.verify_runtime
    if ($LASTEXITCODE -ne 0) { throw 'Runtime asset verification failed.' }
    Restore-LocalLayout
    $assetStudio = Join-Path $root 'tools\thirdparty\AssetStudio.v0.16.0'
    if (-not (Test-Path -LiteralPath (Join-Path $assetStudio 'AssetStudioGUI.exe'))) {
        if (Test-Path -LiteralPath $assetStudio) { throw "Incomplete AssetStudio directory; preserve and inspect: $assetStudio" }
        $archive = Join-Path $stage 'AssetStudio.v0.16.0.zip'
        Get-VerifiedFile "$base/AssetStudio.v0.16.0.zip" $archive $assetStudioHash
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $assetStudio) | Out-Null
        Expand-Archive -LiteralPath $archive -DestinationPath $assetStudio
        if (-not (Test-Path -LiteralPath (Join-Path $assetStudio 'AssetStudioGUI.exe'))) { throw 'AssetStudio archive has an unexpected layout.' }
    }
    $uabea = Join-Path $root 'work\uabea-v8\files'
    if (-not (Test-Path -LiteralPath (Join-Path $uabea 'AssetsTools.NET.dll'))) {
        if (Test-Path -LiteralPath $uabea) { throw "Incomplete UABEA directory; preserve and inspect: $uabea" }
        $archive = Join-Path $stage 'uabea-windows.zip'
        Get-VerifiedFile "$base/uabea-windows.zip" $archive $uabeaHash
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $uabea) | Out-Null
        Expand-Archive -LiteralPath $archive -DestinationPath $uabea
        if (-not (Test-Path -LiteralPath (Join-Path $uabea 'AssetsTools.NET.dll'))) { throw 'UABEA archive has an unexpected layout.' }
    }
    Ensure-UnityEditor
    if (-not $NoLaunch) { & (Join-Path $root 'Start-LocalDuel.cmd'); if ($LASTEXITCODE -ne 0) { throw 'Launcher failed.' } }
    Write-Host 'Setup complete.'
} catch {
    Write-Error $_
    exit 1
}
