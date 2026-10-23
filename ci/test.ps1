param([string]$DotnetPath = 'dotnet', [string[]]$Suites)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runtimeRoot = Join-Path $repoRoot 'GunmaggedonRuntime'
$output = Join-Path $runtimeRoot 'build/ci/tests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$cases = Get-Content (Join-Path $runtimeRoot 'tests/suites.json') -Raw | ConvertFrom-Json
Push-Location $repoRoot
try {
    $sdkVersion = (& $DotnetPath --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The pinned .NET SDK is unavailable.' }
    $sdkLine = @(& $DotnetPath --list-sdks | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + ' \[') })
    if ($sdkLine.Count -ne 1 -or $sdkLine[0] -notmatch '\[(.+)\]$') { throw 'Cannot locate the selected SDK compiler.' }
    $sdkRoot = $Matches[1]
    $compiler = Join-Path (Join-Path $sdkRoot $sdkVersion) 'Roslyn/bincore/csc.dll'
    $dotnetRoot = Split-Path -Parent $sdkRoot
    $packs = @(Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object { $_.Name -match '^10\.0\.\d+$' } | Sort-Object { [Version]$_.Name } -Descending)
    if ($packs.Count -eq 0) { throw 'The SDK must include .NET 10 reference assemblies.' }
    $references = @(Get-ChildItem (Join-Path $packs[0].FullName 'ref/net10.0') -Filter '*.dll' -File | Sort-Object Name)
    foreach ($case in $cases.PSObject.Properties) {
        $name = $case.Name
        if ($Suites -and $name -notin $Suites) { continue }
        $directory = Join-Path $output $name
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $dll = Join-Path $directory "$name.dll"
        $arguments = @('-nologo','-nostdlib+','-target:exe','-optimize+',('-out:"' + $dll + '"'))
        $arguments += $references | ForEach-Object { '-r:"' + $_.FullName + '"' }
        $files = @($case.Value | ForEach-Object { Join-Path $runtimeRoot "src/$_" })
        $files += Join-Path $runtimeRoot "tests/unit/$name.cs"
        $arguments += $files | ForEach-Object { '"' + $_ + '"' }
        $response = Join-Path $directory 'compiler.rsp'
        [IO.File]::WriteAllLines($response,$arguments)
        & $DotnetPath $compiler -noconfig ("@$response")
        if ($LASTEXITCODE -ne 0) { throw "$name compilation failed." }
        $config = @{ runtimeOptions = @{ tfm = 'net10.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = '10.0.0' } } } | ConvertTo-Json -Depth 4
        [IO.File]::WriteAllText((Join-Path $directory "$name.runtimeconfig.json"),$config)
        & $DotnetPath $dll
        if ($LASTEXITCODE -ne 0) { throw "$name failed." }
    }
    $executedCount = @($cases.PSObject.Properties | Where-Object { !$Suites -or $_.Name -in $Suites }).Count
    if ($executedCount -eq 0) { throw 'No matching test suites.' }
    Write-Output "All $executedCount selected SDK test suites passed."
} finally { Pop-Location }
