param([string]$GameDirectory = 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option',
      [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data')
$ErrorActionPreference = 'Stop'
$runtime = Split-Path -Parent $PSScriptRoot
$root = Split-Path -Parent $runtime
$output = Join-Path $runtime 'build/validation/current'
$suffix = [Guid]::NewGuid().ToString('N')
$name = 'Gunmaggedon.ValidationRuntime_' + $suffix
& (Join-Path $runtime 'build.ps1') -OutputDirectory $output -AssemblyName $name
if ($LASTEXITCODE -ne 0) { throw 'Current runtime build failed.' }
$fixture = Join-Path $output ('CurrentReviewFixtures_' + $suffix + '.dll')
$arguments = @('-nologo','-noconfig','-nostdlib+','-target:library',"-out:$fixture")
$arguments += Get-ChildItem (Join-Path $GameDirectory 'NuclearOption_Data/Managed') -Filter '*.dll' | ForEach-Object { '-r:' + $_.FullName }
$arguments += '-r:' + (Join-Path $GameDirectory 'BepInEx/core/0Harmony.dll')
$arguments += '-r:' + (Join-Path $GameDirectory 'BepInEx/core/BepInEx.dll')
$arguments += '-r:' + (Join-Path $UnityData 'Managed/UnityEditor.dll')
$arguments += Get-ChildItem (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEditor*.dll' | ForEach-Object { '-r:' + $_.FullName }
$arguments += Join-Path $PSScriptRoot 'regression/StaticReviewRegression.cs'
$arguments += Join-Path $PSScriptRoot 'integration/SearchlightCoastalIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'integration/F88FlakIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'regression/F88RigRegression.cs'
$arguments += Join-Path $PSScriptRoot 'regression/F88EffectsRegression.cs'
$arguments += Join-Path $PSScriptRoot 'regression/F88ReloadRegression.cs'
$arguments += Join-Path $PSScriptRoot 'regression/L25RigRegression.cs'
$arguments += Join-Path $PSScriptRoot 'integration/L25TrailerIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'integration/UnitLODIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'integration/F88FragmentsIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'regression/F120RigRegression.cs'
$arguments += Join-Path $PSScriptRoot 'integration/F120DamageIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'integration/CD203ConcreteIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'regression/CD203ClearanceRegression.cs'
$arguments += Join-Path $PSScriptRoot 'regression/CD203ScannerRegression.cs'
$arguments += Join-Path $PSScriptRoot 'integration/R230Integration.cs'
$arguments += Join-Path $PSScriptRoot 'integration/RuntimeOptimizationIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'CurrentBuildValidation.cs'
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') (Join-Path $UnityData 'MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Current review fixture compilation failed.' }
$manifest = @{ root=$root; helper=(Join-Path $output "$name.dll"); fixtures=$fixture }
$manifest | ConvertTo-Json | Set-Content (Join-Path $output 'current.json')
Write-Output 'Fresh current-build validation prepared. Run current-build fixtures in Unity using current.json; historical fixtures are unchanged.'
