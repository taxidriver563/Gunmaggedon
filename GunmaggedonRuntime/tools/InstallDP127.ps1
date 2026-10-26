$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$gameRoot='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
$plugins=Join-Path $gameRoot 'BepInEx/plugins'
$runtime=Join-Path $plugins 'Gunmaggedon/Gunmaggedon.Runtime.dll'
$oldPack=Join-Path $plugins 'Gunmageddon_0.0.38.nobp'
$newPack=Join-Path $plugins 'Gunmageddon_0.0.39.nobp'
$sourceRuntime=Join-Path $workspace 'GunmaggedonRuntime/build/1.21.0/Gunmaggedon.Runtime.dll'
$sourcePack=Join-Path $workspace 'ModBuild/0.0.39/Gunmageddon_0.0.39.nobp'
$rollback=Join-Path $workspace 'UnitySetupBackup/DP127/BeforeInstall-1.21.0'
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Close Nuclear Option first'}
if((Get-FileHash -LiteralPath $runtime).Hash -ne '7D72D8F5B8FA5C6B124573BE442F339D98A18751FBC610274A1FE8558F64FABC'){throw 'Installed runtime changed from verified1.20.0 baseline'}
if((Get-FileHash -LiteralPath $oldPack).Hash -ne 'FE16588476FDE096236DACC28611BB0FFA7DE09761F902AF11BBFB4EC4350BFB'){throw 'Installed pack changed from verified0.0.38 baseline'}
$packs=@(Get-ChildItem -LiteralPath $plugins -Filter 'Gunmageddon*.nobp' -File)
if($packs.Count -ne 1 -or $packs[0].FullName -ne [IO.Path]::GetFullPath($oldPack)){throw 'Unexpected active packs; no changes'}
if((Test-Path -LiteralPath $newPack) -or (Test-Path -LiteralPath $rollback)){throw 'Destination or rollback already exists; inspect before retry'}
$runtimeHash=(Get-FileHash -LiteralPath $sourceRuntime).Hash
$packHash=(Get-FileHash -LiteralPath $sourcePack).Hash
$audit=Get-Content (Join-Path $workspace 'GunmaggedonRuntime/build/dp127/export-audit.json') -Raw | ConvertFrom-Json
if(!$audit.passed){throw 'Export audit required'}
New-Item -ItemType Directory -Path $rollback | Out-Null
$rollbackRuntime=Join-Path $rollback 'Gunmaggedon.Runtime.dll'
$rollbackPack=Join-Path $rollback 'Gunmageddon_0.0.38.nobp'
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
$record=@{runtime='1.21.0';pack='0.0.39';protocol=8;runtimeSha256=$runtimeHash;packSha256=$packHash;rollback=$rollback;gameClosed=$true;installedAt=[DateTime]::UtcNow.ToString('o')}
$record | ConvertTo-Json | Set-Content (Join-Path $workspace 'GunmaggedonRuntime/build/dp127/install.json')
$record | ConvertTo-Json
