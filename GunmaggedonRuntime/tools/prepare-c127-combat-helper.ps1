param([string]$OutputPath='GunmaggedonRuntime/build/c127-combat/setup.cs')
$ErrorActionPreference='Stop'
$lod=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SetupUnitLODs.cs'))
$setup=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SetupC127Combat.cs'))
$setup=[regex]::Replace($setup,'(?m)^using .*;\r?$','')
$source="using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;`n"+$lod+"`n"+$setup
$absolute=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force (Split-Path -Parent $absolute) | Out-Null
[IO.File]::WriteAllText($absolute,$source)
Write-Output $absolute
