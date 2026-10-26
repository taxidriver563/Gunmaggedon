$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$plugins='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins'
$runtime=Join-Path $plugins 'Gunmaggedon/Gunmaggedon.Runtime.dll'
$oldPack=Join-Path $plugins 'Gunmageddon_0.0.41.nobp'
$newPack=Join-Path $plugins 'Gunmageddon_0.0.42.nobp'
$sourceRuntime=Join-Path $workspace 'GunmaggedonRuntime/build/1.22.1/Gunmaggedon.Runtime.dll'
$sourcePack=Join-Path $workspace 'ModBuild/0.0.42/Gunmageddon_0.0.42.nobp'
$rollback=Join-Path $workspace 'UnitySetupBackup/ReleaseCheck/BeforeInstall-1.22.1'
$reportDir=Join-Path $workspace 'GunmaggedonRuntime/build/release-check'
$auditPath=Join-Path $reportDir 'export-audit.json'
$identityPath=Join-Path $reportDir 'source-identity.json'
$nativePath=Join-Path $reportDir 'native.json'
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Close Nuclear Option before installing'}
if((Get-FileHash -LiteralPath $runtime).Hash -ne '5C25B88AB84CDDB888C3D2A694E5F4C7164548D3C9D103C1C48D1E4FE0A35DAE'){throw 'Installed runtime differs from verified1.22.0; no changes'}
if((Get-FileHash -LiteralPath $oldPack).Hash -ne '781D5E2C02C0F2783CBA7695467698AE7597D3E8CBBC99009632C9720D3A4489'){throw 'Installed pack differs from verified0.0.41; no changes'}
if((Test-Path -LiteralPath $newPack) -or (Test-Path -LiteralPath $rollback)){throw 'Destination/rollback already exists; inspect before retry'}
$active=@(Get-ChildItem -LiteralPath $plugins -Filter 'Gunmageddon*.nobp' -File)
if($active.Count -ne 1 -or $active[0].FullName -ne [IO.Path]::GetFullPath($oldPack)){throw 'Unexpected active pack combination'}
$audit=Get-Content -LiteralPath $auditPath -Raw|ConvertFrom-Json
$identity=Get-Content -LiteralPath $identityPath -Raw|ConvertFrom-Json
$native=Get-Content -LiteralPath $nativePath -Raw|ConvertFrom-Json
$runtimeHash=(Get-FileHash -LiteralPath $sourceRuntime).Hash
$packHash=(Get-FileHash -LiteralPath $sourcePack).Hash
if(!$audit.passed -or !$identity.passed -or !$native.passed){throw 'Export, source identity and native validation must pass'}
foreach($group in $native.PSObject.Properties){if($group.Value -and $group.Value.PSObject.Properties['passed'] -and !$group.Value.passed){throw ('Native validation failed: '+$group.Name)}}
if($audit.runtime -ne '1.22.1' -or $audit.pack -ne '0.0.42' -or $audit.protocol -ne 10 -or $identity.runtime -ne '1.22.1' -or $identity.pack -ne '0.0.42' -or $identity.protocol -ne 10){throw 'Validated version/protocol mismatch'}
if($audit.runtimeSha256 -ne $runtimeHash -or $identity.runtimeSha256 -ne $runtimeHash -or $audit.packSha256 -ne $packHash){throw 'Candidate binaries differ from the validated audit'}
if($audit.sourceIdentitySha256 -ne (Get-FileHash -LiteralPath $identityPath).Hash -or $audit.nativeReportSha256 -ne (Get-FileHash -LiteralPath $nativePath).Hash -or $native.helper -ne $identity.helper){throw 'Validation evidence changed after export audit'}
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $workspace 'GunmaggedonRuntime/src'))+[IO.Path]::DirectorySeparatorChar
if(@($identity.sources).Count -ne $identity.sourceCount -or @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs').Count -ne $identity.sourceCount){throw 'Source snapshot membership changed'}
foreach($source in $identity.sources){$path=[IO.Path]::GetFullPath((Join-Path (Join-Path $workspace 'GunmaggedonRuntime') $source.path));if(!$path.StartsWith($sourceRoot,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path).Hash -ne $source.sha256){throw ('Source changed after native validation: '+$source.path)}}
New-Item -ItemType Directory -Path $rollback|Out-Null
$oldRuntimeCopy=Join-Path $rollback 'Gunmaggedon.Runtime.dll'
$oldPackCopy=Join-Path $rollback 'Gunmageddon_0.0.41.nobp'
Copy-Item -LiteralPath $runtime -Destination $oldRuntimeCopy
Copy-Item -LiteralPath $oldPack -Destination $oldPackCopy
if((Get-FileHash -LiteralPath $oldRuntimeCopy).Hash -ne (Get-FileHash -LiteralPath $runtime).Hash -or (Get-FileHash -LiteralPath $oldPackCopy).Hash -ne (Get-FileHash -LiteralPath $oldPack).Hash){throw 'Rollback copies failed verification'}
if(Get-Process NuclearOption -ErrorAction SilentlyContinue){throw 'Game launched during preparation; no installed files changed'}
try{
    Copy-Item -LiteralPath $sourceRuntime -Destination $runtime -Force
    Copy-Item -LiteralPath $sourcePack -Destination $newPack
    if((Get-FileHash -LiteralPath $runtime).Hash -ne $runtimeHash -or (Get-FileHash -LiteralPath $newPack).Hash -ne $packHash){throw 'Installed binary hash mismatch'}
    Remove-Item -LiteralPath $oldPack
}catch{
    Copy-Item -LiteralPath $oldRuntimeCopy -Destination $runtime -Force
    Copy-Item -LiteralPath $oldPackCopy -Destination $oldPack -Force
    if(Test-Path -LiteralPath $newPack){Remove-Item -LiteralPath $newPack}
    throw
}
$record=@{runtime='1.22.1';pack='0.0.42';protocol=10;runtimeSha256=$runtimeHash;packSha256=$packHash;rollback=$rollback;gameClosed=$true;installedAt=[DateTime]::UtcNow.ToString('o');nativeReportSha256=$audit.nativeReportSha256;sourceIdentitySha256=$audit.sourceIdentitySha256}
$record|ConvertTo-Json|Set-Content (Join-Path $reportDir 'install.json')
$record|ConvertTo-Json
