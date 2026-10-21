param(
    [Parameter(Mandatory=$true)][string]$ReferencesDirectory,
    [string]$OutputDirectory,
    [string]$DotnetPath = 'dotnet',
    [string]$SourceRevision
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$ReferencesDirectory = (Resolve-Path -LiteralPath $ReferencesDirectory).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($required in @('Assembly-CSharp.dll','UnityEngine.CoreModule.dll','mscorlib.dll','BepInEx.dll','0Harmony.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ReferencesDirectory $required))) { throw "Missing build reference: $required" }
}
Push-Location $repoRoot
try {
    $sdkVersion = (& $DotnetPath --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The pinned .NET SDK is unavailable.' }
    $sdkLine = @(& $DotnetPath --list-sdks | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + ' \[') })
    if ($sdkLine.Count -ne 1 -or $sdkLine[0] -notmatch '\[(.+)\]$') { throw 'Cannot locate the selected SDK compiler.' }
    $compiler = Join-Path (Join-Path $Matches[1] $sdkVersion) 'Roslyn/bincore/csc.dll'
    $scratch = Join-Path $repoRoot 'GunmaggedonRuntime/build/ci/runtime'
    New-Item -ItemType Directory -Path $scratch -Force | Out-Null
    $dll = Join-Path $OutputDirectory 'Gunmaggedon.Runtime.dll'
    $arguments = @('-nologo','-nostdlib+','-target:library','-optimize+','-deterministic+','-langversion:9.0',('-pathmap:"' + $repoRoot + '=/_/"'),('-out:"' + $dll + '"'))
    $arguments += Get-ChildItem -LiteralPath $ReferencesDirectory -Filter '*.dll' -File | Sort-Object Name | ForEach-Object { '-r:"' + $_.FullName + '"' }
    $sources = @(Get-ChildItem (Join-Path $repoRoot 'GunmaggedonRuntime/src') -Filter '*.cs' -File -Recurse | Sort-Object FullName)
    if ($sources.Count -eq 0) { throw 'No runtime sources found.' }
    $arguments += $sources | ForEach-Object { '"' + $_.FullName + '"' }
    $response = Join-Path $scratch 'compiler.rsp'
    [IO.File]::WriteAllLines($response,$arguments)
    & $DotnetPath $compiler -noconfig ("@$response")
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dll)) { throw 'CI runtime compilation failed.' }
    if (-not $SourceRevision) {
        $SourceRevision = (& git -C $repoRoot rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source revision.' }
    }
    $pluginSource = Get-Content (Join-Path $repoRoot 'GunmaggedonRuntime/src/Core/Plugin.cs') -Raw
    $versionMatch = [regex]::Match($pluginSource,'\[BepInPlugin\("[^"]+",\s*"[^"]+",\s*"([^"]+)"\)\]')
    if (-not $versionMatch.Success) { throw 'Cannot read the runtime plugin version.' }
    $info = [ordered]@{ runtime_version = $versionMatch.Groups[1].Value; source_commit = $SourceRevision; dotnet_sdk = $sdkVersion; references_sha256 = $env:GUNMAGGEDON_REFERENCES_SHA256; asset_pack = '0.0.18' }
    $package = Join-Path $scratch ('package-' + [Guid]::NewGuid().ToString('N'))
    $pluginFolder = Join-Path $package 'BepInEx/plugins/Gunmaggedon'
    New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $pluginFolder 'Gunmaggedon.Runtime.dll') -Force
    [IO.File]::WriteAllText((Join-Path $package 'build-info.json'),($info | ConvertTo-Json))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RUNTIME-INSTALL.txt') -Destination (Join-Path $package 'README.txt') -Force
    Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $OutputDirectory 'Gunmaggedon.Runtime.zip') -Force
    Write-Output "Built $($sources.Count) runtime sources using SDK $sdkVersion; release folder contains only the mod DLL and install ZIP."
} finally { Pop-Location }
