$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$plugins='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins'
$runtime=Join-Path $plugins 'Gunmaggedon/Gunmaggedon.Runtime.dll'
$old=Join-Path $plugins 'Gunmageddon_0.0.40.nobp'
$next=Join-Path $plugins 'Gunmageddon_0.0.41.nobp'
$source=Join-Path $workspace 'ModBuild/0.0.41/Gunmageddon_0.0.41.nobp'
$rollback=Join-Path $workspace 'UnitySetupBackup/Wreckage/BeforeInstall-0.0.41'
$audit=Get-Content (Join-Path $workspace 'GunmaggedonRuntime/build/wreckage/export-audit.json') -Raw|ConvertFrom-Json
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Close Nuclear Option before installing'}
if((Get-FileHash -LiteralPath $runtime).Hash -ne '5C25B88AB84CDDB888C3D2A694E5F4C7164548D3C9D103C1C48D1E4FE0A35DAE'){throw 'Runtime differs from verified1.22.0; no changes'}
if((Get-FileHash -LiteralPath $old).Hash -ne '0524C999C73BFA72DA7AACE794249B799441274A893ED54DC55248FC6D58FE22'){throw 'Installed pack differs from verified0.0.40'}
if((Test-Path -LiteralPath $next) -or (Test-Path -LiteralPath $rollback)){throw 'Install/rollback destination exists'}
$active=@(Get-ChildItem -LiteralPath $plugins -Filter 'Gunmageddon*.nobp' -File)
if($active.Count -ne 1 -or $active[0].FullName -ne [IO.Path]::GetFullPath($old)){throw 'Unexpected active pack combination'}
if(!$audit.passed -or $audit.packSha256 -ne (Get-FileHash -LiteralPath $source).Hash){throw 'Candidate does not match export audit'}
foreach($file in @('validation','source-validation','collision-fit')){$result=Get-Content (Join-Path $workspace "GunmaggedonRuntime/build/wreckage/$file.json") -Raw|ConvertFrom-Json;if(!$result.passed){throw "Failed check: $file"}}
New-Item -ItemType Directory -Path $rollback|Out-Null
$backup=Join-Path $rollback 'Gunmageddon_0.0.40.nobp'
Copy-Item -LiteralPath $old -Destination $backup
if((Get-FileHash -LiteralPath $backup).Hash -ne (Get-FileHash -LiteralPath $old).Hash){throw 'Rollback copy mismatch'}
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Game launched during preparation; no installed files changed'}
try{Copy-Item -LiteralPath $source -Destination $next;if((Get-FileHash -LiteralPath $next).Hash -ne $audit.packSha256){throw 'Installed pack hash mismatch'};Remove-Item -LiteralPath $old}
catch{Copy-Item -LiteralPath $backup -Destination $old -Force;if(Test-Path -LiteralPath $next){Remove-Item -LiteralPath $next};throw}
$record=@{runtime='1.22.0';runtimeUnchanged=$true;pack='0.0.41';protocol=9;packSha256=$audit.packSha256;runtimeSha256=(Get-FileHash -LiteralPath $runtime).Hash;rollback=$rollback;gameClosed=$true;installedAt=[DateTime]::UtcNow.ToString('o')}
$record|ConvertTo-Json|Set-Content (Join-Path $workspace 'GunmaggedonRuntime/build/wreckage/install.json')
$record|ConvertTo-Json
