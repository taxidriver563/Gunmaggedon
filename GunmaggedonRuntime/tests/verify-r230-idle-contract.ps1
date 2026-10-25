param([Parameter(Mandatory=$true)][string]$RuntimeDll,
 [string]$GameDirectory='C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option')
$ErrorActionPreference='Stop'
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDirectory 'BepInEx/core/Mono.Cecil.dll'))) | Out-Null
$runtime=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($RuntimeDll))
$native=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDirectory 'NuclearOption_Data/Managed/Assembly-CSharp.dll'))
try{
 $controller=$runtime.MainModule.Types | Where-Object Name -eq 'R230Controller'
 foreach($name in @('get_NativeReady','get_WarningReady')){
  $method=$controller.Methods | Where-Object Name -eq $name
  if(!$method){throw "Missing $name"}
  if($method.Body.Instructions | Where-Object {"$($_.Operand)" -match 'Behaviour::get_enabled'}){throw "$name requires enabled and can deadlock idle guns"}
 }
 $gun=$native.MainModule.Types | Where-Object Name -eq 'Gun'
 $fire=$gun.Methods | Where-Object Name -eq 'Fire'
 if(!($fire.Body.Instructions | Where-Object {"$($_.Operand)" -match 'Behaviour::set_enabled'})){throw 'Native Fire enabling contract changed; inspect native code'}
 Write-Output 'PASS: compiled R230 readiness permits idle disabled guns; installed native Gun.Fire enables them. IL contract check only, not live execution.'
}finally{$runtime.Dispose();$native.Dispose()}
