param([string]$OutputDirectory, [switch]$Diagnostics, [string]$AssemblyName = 'Gunmaggedon.Runtime')

$ErrorActionPreference = 'Stop'
$gameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
$unityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data'
$buildDir = Join-Path $PSScriptRoot 'build/1.10.0'
if ($OutputDirectory) { $buildDir = [IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
if ($AssemblyName -notmatch '^[A-Za-z0-9_.]+$') { throw 'Invalid output assembly name.' }
$compilerArgs = @('-nologo', '-noconfig', '-nostdlib+', '-target:library', "-out:$buildDir/$AssemblyName.dll")
$compilerArgs += Get-ChildItem "$gameDir/NuclearOption_Data/Managed" -Filter '*.dll' | ForEach-Object { "-r:$($_.FullName)" }
$compilerArgs += "-r:$gameDir/BepInEx/core/BepInEx.dll"
$compilerArgs += "-r:$gameDir/BepInEx/core/0Harmony.dll"
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -File -Filter '*.cs' | Sort-Object FullName)
if ($sources.Count -eq 0) { throw 'No runtime C# sources found under src.' }
if ($Diagnostics) { $compilerArgs += '-define:GUNMAGGEDON_DIAGNOSTICS' }
$compilerArgs += $sources | ForEach-Object { $_.FullName }
& "$unityData/MonoBleedingEdge/bin/mono.exe" "$unityData/MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Gunmaggedon runtime compilation failed.' }
Write-Output "Built $($sources.Count) runtime sources: $buildDir/$AssemblyName.dll"
