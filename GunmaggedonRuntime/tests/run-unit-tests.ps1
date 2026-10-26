param([string[]]$Suites)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data'
$mono = Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $unityData 'MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe'
$output = Join-Path $projectRoot 'build/validation/unit'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$cases = Get-Content (Join-Path $PSScriptRoot 'suites.json') -Raw | ConvertFrom-Json
$selected = @($cases.PSObject.Properties | Where-Object { !$Suites -or $_.Name -in $Suites })
if ($selected.Count -eq 0) { throw 'No matching test suites.' }
$passed = 0
foreach ($case in $selected) {
    $name = $case.Name
    $exe = Join-Path $output "$name.exe"
    $sources = @($case.Value | ForEach-Object { Join-Path $projectRoot "src/$_" })
    & $mono $compiler -nologo -target:exe "-out:$exe" @sources (Join-Path $PSScriptRoot "unit/$name.cs")
    if ($LASTEXITCODE -ne 0) {
        throw "$name did not compile using Unity Mono; $passed of $($selected.Count) selected suites passed before execution stopped; later suites were not run. Use ci/test.ps1 with the pinned SDK for full supported API coverage."
    }
    & $mono $exe
    if ($LASTEXITCODE -ne 0) { throw "$name failed under Unity Mono; $passed of $($selected.Count) selected suites passed." }
    $passed++
}
Write-Output "All $passed selected manifest-listed standalone suites passed under Unity Mono."
