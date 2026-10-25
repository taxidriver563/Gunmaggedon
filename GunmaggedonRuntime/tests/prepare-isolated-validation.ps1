param([string]$GameDirectory='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option',
      [string]$UnityData='C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data')
$ErrorActionPreference='Stop'
$runtime=Split-Path -Parent $PSScriptRoot
$root=Split-Path -Parent $runtime
$suffix=[Guid]::NewGuid().ToString('N')
$namespace='GunmaggedonValidation_'+$suffix
$output=Join-Path $runtime "build/validation/isolated/$suffix"
New-Item -ItemType Directory -Force $output | Out-Null
$mono=Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$compiler=Join-Path $UnityData 'MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe'
$references=@(Get-ChildItem "$GameDirectory/NuclearOption_Data/Managed" -Filter '*.dll' | ForEach-Object {'-r:'+$_.FullName})
$references+='-r:'+(Join-Path $GameDirectory 'BepInEx/core/0Harmony.dll')
$references+='-r:'+(Join-Path $GameDirectory 'BepInEx/core/BepInEx.dll')
$helper=Join-Path $output "Gunmaggedon.Validation_$suffix.dll"
$sources=@()
foreach($source in Get-ChildItem (Join-Path $runtime 'src') -Filter '*.cs' -Recurse -File){
    $destination=Join-Path $output ($source.FullName.Substring($runtime.Length+1).Replace('\','_'))
    $text=[IO.File]::ReadAllText($source.FullName).Replace('namespace GunmageddonAudio','namespace '+$namespace).Replace('GunmageddonAudio.',$namespace+'.')
    [IO.File]::WriteAllText($destination,$text)
    $sources+=$destination
}
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library "-out:$helper" @references @sources
if($LASTEXITCODE -ne 0){throw 'Isolated runtime compilation failed'}
$fixtures=Join-Path $output "Gunmaggedon.Fixtures_$suffix.dll"
$testReferences=$references+@('-r:'+(Join-Path $UnityData 'Managed/UnityEditor.dll'))
$testReferences+=Get-ChildItem (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEditor*.dll' | ForEach-Object {'-r:'+$_.FullName}
$tests=@()
foreach($name in @('regression/StaticReviewRegression.cs','integration/SearchlightCoastalIntegration.cs','regression/F88RigRegression.cs','regression/F88EffectsRegression.cs','integration/F88FlakIntegration.cs','regression/F88ReloadRegression.cs','regression/L25RigRegression.cs','integration/L25TrailerIntegration.cs','integration/UnitLODIntegration.cs','integration/F88FragmentsIntegration.cs','integration/RuntimeOptimizationIntegration.cs','regression/F120RigRegression.cs','integration/F120DamageIntegration.cs','integration/F120IncendiaryIntegration.cs','integration/CD203ConcreteIntegration.cs','regression/CD203ClearanceRegression.cs','regression/CD203ScannerRegression.cs','integration/R230Integration.cs','integration/R230NuclearIntegration.cs','CurrentBuildValidation.cs')){
    $destination=Join-Path $output ('fixture_'+[IO.Path]::GetFileName($name))
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $name)).Replace('GunmageddonAudio.',$namespace+'.').Replace('using GunmageddonAudio;','using '+$namespace+';')
    # Byte-loading the same helper in every group creates distinct CLR types.
    # Unity can then return a component from an earlier copy of that assembly.
    # LoadFrom keeps one runtime identity throughout the sequential fixture run.
    $text=$text.Replace('Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")))','Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"))')
    $text=$text.Replace('Assembly.Load(File.ReadAllBytes(helper))','Assembly.LoadFrom(helper)')
    $text=$text.Replace('Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??Path.Combine(repo,"GunmaggedonRuntime/build/1.9.2/Gunmaggedon.Runtime.dll")))','Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??Path.Combine(repo,"GunmaggedonRuntime/build/1.9.2/Gunmaggedon.Runtime.dll"))')
    [IO.File]::WriteAllText($destination,$text);$tests+=$destination
}
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library "-out:$fixtures" @testReferences @tests
if($LASTEXITCODE -ne 0){throw 'Isolated fixture compilation failed'}
@{root=$root;helper=$helper;fixtures=$fixtures;runtimeNamespace=$namespace;sources=$sources.Count} | ConvertTo-Json | Set-Content (Join-Path $runtime 'build/validation/isolated/current.json')
Write-Output "Compiled $($sources.Count) current sources into unique validation namespace: $namespace"
Write-Output "Manifest: $runtime/build/validation/isolated/current.json"
