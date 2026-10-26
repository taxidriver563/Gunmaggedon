$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$gameRoot='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
$plugins=Join-Path $gameRoot 'BepInEx/plugins'
$runtime=Join-Path $plugins 'Gunmaggedon/Gunmaggedon.Runtime.dll'
$oldPack=Join-Path $plugins 'Gunmageddon_0.0.39.nobp'
$newPack=Join-Path $plugins 'Gunmageddon_0.0.40.nobp'
$sourceRuntime=Join-Path $workspace 'GunmaggedonRuntime/build/1.22.0/Gunmaggedon.Runtime.dll'
$sourcePack=Join-Path $workspace 'ModBuild/0.0.40/Gunmageddon_0.0.40.nobp'
$rollback=Join-Path $workspace 'UnitySetupBackup/C127/BeforeInstall-1.22.0'
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Close Nuclear Option first'}
if((Get-FileHash -LiteralPath $runtime).Hash -ne 'B97CC6AD554B3D6FA59FE497217312BB6D4BD842D8DA2A1AFEF51EFB36C71798'){throw 'Installed runtime changed from verified1.21.0 baseline'}
if((Get-FileHash -LiteralPath $oldPack).Hash -ne 'E09922C0A5D33AC892DD9DD2F8A71C7F0CB67796AB03F82AA73CC5542F20B069'){throw 'Installed pack changed from verified0.0.39 baseline'}
$packs=@(Get-ChildItem -LiteralPath $plugins -Filter 'Gunmageddon*.nobp' -File)
if($packs.Count -ne 1 -or $packs[0].FullName -ne [IO.Path]::GetFullPath($oldPack)){throw 'Unexpected active packs; no changes'}
if((Test-Path -LiteralPath $newPack) -or (Test-Path -LiteralPath $rollback)){throw 'Destination or rollback already exists; inspect before retry'}
$runtimeHash=(Get-FileHash -LiteralPath $sourceRuntime).Hash
$packHash=(Get-FileHash -LiteralPath $sourcePack).Hash
$audit=Get-Content (Join-Path $workspace 'GunmaggedonRuntime/build/c127-combat/export-audit.json') -Raw | ConvertFrom-Json
if(!$audit.passed -or $audit.runtimeSha256 -ne $runtimeHash -or $audit.packSha256 -ne $packHash){throw 'Export audit and candidate hashes must match'}
$validation=Get-Content (Join-Path $workspace 'UnitySetupBackup/C127/Validation/Related.json') -Raw|ConvertFrom-Json
foreach($group in $validation.PSObject.Properties){if(!$group.Value.passed){throw ('Validation failed: '+$group.Name)}}
New-Item -ItemType Directory -Path $rollback | Out-Null
$rollbackRuntime=Join-Path $rollback 'Gunmaggedon.Runtime.dll'
$rollbackPack=Join-Path $rollback 'Gunmageddon_0.0.39.nobp'
Copy-Item -LiteralPath $runtime -Destination $rollbackRuntime
Copy-Item -LiteralPath $oldPack -Destination $rollbackPack
if((Get-FileHash -LiteralPath $rollbackRuntime).Hash -ne (Get-FileHash -LiteralPath $runtime).Hash -or (Get-FileHash -LiteralPath $rollbackPack).Hash -ne (Get-FileHash -LiteralPath $oldPack).Hash){throw 'Rollback verification failed'}
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Game launched during preparation; no installed files changed'}
try{
    Copy-Item -LiteralPath $sourceRuntime -Destination $runtime -Force
    Copy-Item -LiteralPath $sourcePack -Destination $newPack
    if((Get-FileHash -LiteralPath $runtime).Hash -ne $runtimeHash -or (Get-FileHash -LiteralPath $newPack).Hash -ne $packHash){throw 'Installed hash mismatch'}
    Remove-Item -LiteralPath $oldPack
}catch{
    Copy-Item -LiteralPath $rollbackRuntime -Destination $runtime -Force
    Copy-Item -LiteralPath $rollbackPack -Destination $oldPack -Force
    if(Test-Path -LiteralPath $newPack){Remove-Item -LiteralPath $newPack}
    throw
}
$record=@{runtime='1.22.0';pack='0.0.40';protocol=9;runtimeSha256=$runtimeHash;packSha256=$packHash;rollback=$rollback;gameClosed=$true;installedAt=[DateTime]::UtcNow.ToString('o')}
$record | ConvertTo-Json | Set-Content (Join-Path $workspace 'GunmaggedonRuntime/build/c127-combat/install.json')
$record | ConvertTo-Json
