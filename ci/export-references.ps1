param(
    [string]$GameDirectory = 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option',
    [string]$OutputZip
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputZip) { $OutputZip = Join-Path $repoRoot 'GunmaggedonRuntime/build/private/ci-references.zip' }
$OutputZip = [IO.Path]::GetFullPath($OutputZip)
if (Test-Path -LiteralPath $OutputZip) { throw 'Reference bundle already exists; choose a new output filename.' }
$staging = Join-Path (Split-Path $OutputZip -Parent) ('refs-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$files = @(Get-ChildItem (Join-Path $GameDirectory 'NuclearOption_Data/Managed') -Filter '*.dll' -File)
$files += Get-Item (Join-Path $GameDirectory 'BepInEx/core/BepInEx.dll'), (Join-Path $GameDirectory 'BepInEx/core/0Harmony.dll')
foreach ($file in $files) {
    $destination = Join-Path $staging $file.Name
    if (Test-Path -LiteralPath $destination) { throw "Duplicate reference filename: $($file.Name)" }
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
foreach ($required in @('Assembly-CSharp.dll','UnityEngine.CoreModule.dll','mscorlib.dll','BepInEx.dll','0Harmony.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $staging $required))) { throw "Missing reference: $required" }
}
$manifest = [ordered]@{ schema = 1; files = @($files | Sort-Object Name | ForEach-Object {
    [ordered]@{ name = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() }
}) }
[IO.File]::WriteAllText((Join-Path $staging 'references.json'), ($manifest | ConvertTo-Json -Depth 4))
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $OutputZip -CompressionLevel Optimal
Write-Output "PRIVATE build references prepared: $OutputZip"
Write-Output 'Upload only to your private dependency storage; never to the public mod release.'
Write-Output ('GUNMAGGEDON_REFERENCES_SHA256=' + (Get-FileHash -LiteralPath $OutputZip).Hash.ToLowerInvariant())
