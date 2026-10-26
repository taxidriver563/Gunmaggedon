param([string]$OutputPath)
$ErrorActionPreference='Stop'
$runtime=Split-Path -Parent $PSScriptRoot
if(!$OutputPath){$OutputPath=Join-Path $runtime 'build/c127-staging/SetupC127Rigs.combined.cs'}
$lod=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SetupUnitLODs.cs'))
if(!$lod.Contains('public static void ConfigureCarriage(GameObject root)')){throw 'Inspect changed LOD helper before composing'}
$lod=$lod.Replace('SetupUnitLODs','SetupC127LODs').Replace('Assets/Blueprinter/Mods/Gunmageddon/','Assets/GunmaggedonStaging/C127/')
$setup=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SetupC127Rigs.cs'))
$setup=[regex]::Replace($setup,'(?m)^using .*;\r?$','')
$source="using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;`n"+$lod+"`n"+$setup
$absolute=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force (Split-Path -Parent $absolute) | Out-Null
[IO.File]::WriteAllText($absolute,$source)
Write-Output $absolute
# Compile/execute in the connected Unity editor, class SetupC127Rigs.
# Run refuses configured destinations. RefreshPreviews only replaces owned clips.
# This script prepares source only; it never edits Unity assets or installs files.
