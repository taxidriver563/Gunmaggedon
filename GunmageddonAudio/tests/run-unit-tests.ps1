$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data'
$mono = Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $unityData 'MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe'
$output = Join-Path $projectRoot 'build/validation/unit'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$cases = [ordered]@{
    ArtilleryBallisticsTests = 'Artillery/ArtilleryBallistics.cs'
    FlareBallisticsTests = 'Illumination/FlareBallistics.cs'
    RangingSolutionTests = 'Ranging/RangingSolution.cs'
    SpecialShellPolicyTests = 'SpecialShells/SpecialShellPolicy.cs'
    QueryOptimizationTests = @('SpecialShells/QueryGeometry.cs', 'SpecialShells/ClusterDamageQuery.cs')
    ArmorPiercingPolicyTests = 'Ammunition/ArmorPiercingPolicy.cs'
}
foreach ($name in $cases.Keys) {
    $exe = Join-Path $output "$name.exe"
    $sources = @($cases[$name] | ForEach-Object { Join-Path $projectRoot "src/$_" })
    & $mono $compiler -nologo -target:exe "-out:$exe" @sources (Join-Path $PSScriptRoot "unit/$name.cs")
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed." }
    & $mono $exe
    if ($LASTEXITCODE -ne 0) { throw "$name failed." }
}
Write-Output "All $($cases.Count) standalone unit suites passed."
