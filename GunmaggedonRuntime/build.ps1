param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$gameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
$unityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data'
$buildDir = Join-Path $PSScriptRoot 'build/1.8.4'
if ($OutputDirectory) { $buildDir = [IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$compilerArgs = @('-nologo', '-noconfig', '-nostdlib+', '-target:library', "-out:$buildDir/Gunmaggedon.Runtime.dll")
$compilerArgs += Get-ChildItem "$gameDir/NuclearOption_Data/Managed" -Filter '*.dll' | ForEach-Object { "-r:$($_.FullName)" }
$compilerArgs += "-r:$gameDir/BepInEx/core/BepInEx.dll"
$compilerArgs += "-r:$gameDir/BepInEx/core/0Harmony.dll"
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -File -Filter '*.cs' | Sort-Object FullName)
if ($sources.Count -eq 0) { throw 'No runtime C# sources found under src.' }
$compilerArgs += $sources | ForEach-Object { $_.FullName }
& "$unityData/MonoBleedingEdge/bin/mono.exe" "$unityData/MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Gunmaggedon runtime compilation failed.' }
Write-Output "Built $($sources.Count) runtime sources: $buildDir/Gunmaggedon.Runtime.dll"
