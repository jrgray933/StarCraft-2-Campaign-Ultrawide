param([ValidateSet('Enable','Disable','Status')][string]$Action='Status',[int]$TargetWidth=3440,[int]$TargetHeight=1440,[int]$ScalePercent=100)
$ErrorActionPreference='Stop'
if(-not ('SC2HudScale' -as [type])){Add-Type -Path @((Join-Path $PSScriptRoot 'CampaignGate.cs'),(Join-Path $PSScriptRoot 'HubCamera.cs'),(Join-Path $PSScriptRoot 'MemoryRead.cs'),(Join-Path $PSScriptRoot 'SC2HudScale.cs'))}
$games=@(Get-Process SC2_x64 -ErrorAction SilentlyContinue)
if($games.Count -ne 1){return [pscustomobject]@{Active=$false;Ready=$true;ScalePercent=100}}
$game=$games[0];$moduleBase=[System.UInt64]$game.MainModule.BaseAddress.ToInt64()
function Bytes([System.UInt64]$a,[int]$n){$b=[SC2Memory]::Read($game.Id,$a,$n);if(!$b){throw 'Campaign HUD is not ready.'};return ,$b}
function Ptr([System.UInt64]$a){[BitConverter]::ToUInt64((Bytes $a 8),0)}
function U32([System.UInt64]$a){[BitConverter]::ToUInt32((Bytes $a 4),0)}
function W([System.UInt64]$a,[byte[]]$before,[byte[]]$after){[SC2HudScale]::CheckedWrite($game.Id,$a,$before,$after)}
function Signal([System.UInt64]$state,[System.UInt64]$owner,[int]$value){W $state ([BitConverter]::GetBytes([int]0)) ([BitConverter]::GetBytes($value));$f=Bytes ($owner+0x28) 1;W ($owner+0x28) $f ([byte[]]@([byte]($f[0] -bor 0x10)))}
$ui=Ptr ($moduleBase+0x4032368)
if(!$ui -or (Ptr $ui) -ne $moduleBase+0x2d522a8){return [pscustomobject]@{Active=$false;Ready=$false;ScalePercent=100}}
$owner=Ptr ($ui+0xc68);$console=Ptr ($ui+0xc60)
$recordPath=Join-Path $PSScriptRoot 'hud-scale-session.json';$record=$null
if(Test-Path -LiteralPath $recordPath){
 $candidate=Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
 if($candidate.Pid -eq $game.Id -and ([datetime]$candidate.Started).ToUniversalTime().Ticks -eq $game.StartTime.ToUniversalTime().Ticks -and $candidate.Owner -eq $owner -and (Ptr $owner) -eq $candidate.VTable){$record=$candidate}
}
if($record){
 $state=[System.UInt64]$record.State
 if((U32 ($state+4)) -ne 0 -or (U32 $state) -ne 0){return [pscustomobject]@{Active=$true;Ready=$false;ScalePercent=$record.ScalePercent}}
 if($record.Restoring){
  W $owner ([BitConverter]::GetBytes([System.UInt64]$record.VTable)) ([BitConverter]::GetBytes([System.UInt64]$record.OriginalVTable));$record=$null
 }elseif($Action -eq 'Disable' -or ($Action -eq 'Enable' -and $record.ScalePercent -ne $ScalePercent)){
  $identities=New-Object 'System.Collections.Generic.List[SC2HudScale+Identity]'
  foreach($f in $record.Frames){$id=New-Object 'SC2HudScale+Identity';$id.Frame=$f.Frame;$id.VTable=$f.VTable;$id.Parent=$f.Parent;$identities.Add($id)}
  [SC2HudScale]::GuardRestore($game.Id,$moduleBase,$state,$record.CommandCount,$identities.ToArray())
  $record.Restoring=$true;$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
  Signal $state $owner 2
  return [pscustomobject]@{Active=$true;Ready=$false;ScalePercent=$record.ScalePercent}
 }else{return [pscustomobject]@{Active=$true;Ready=$true;ScalePercent=$record.ScalePercent}}
}
if($Action -ne 'Enable' -or $ScalePercent -eq 100){return [pscustomobject]@{Active=$false;Ready=$true;ScalePercent=100}}
[SC2HudScale]::ValidateScale($TargetWidth,$TargetHeight,$ScalePercent)
[SC2CampaignGate]::Require($game.Id,$moduleBase)
$baselinePath=Join-Path $PSScriptRoot 'hud-runtime-session.json'
if(!(Test-Path -LiteralPath $baselinePath)){throw 'Waiting for the centered campaign HUD.'}
$baseline=Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
$activeTable=Ptr $owner
if($game.Id -ne $baseline.GamePid -or $activeTable -ne [Convert]::ToUInt64($baseline.VTable,16)){throw 'Waiting for the centered campaign HUD.'}
$plan=[SC2HudScale]::CreatePlan($game.Id,$moduleBase,$TargetWidth,$TargetHeight,$ScalePercent)
$prepared=[SC2HudScale]::Prepare($game.Id,$owner,($moduleBase+0x2d51c58),($moduleBase+0x16bcfc0),$plan.Commands,$activeTable,$plan.CargoWidths)
$record=[pscustomobject]@{Pid=$game.Id;Started=$game.StartTime.ToString('o');Ui=$ui;Owner=$owner;Console=$console;OriginalVTable=$activeTable;State=$prepared.State;VTable=$prepared.VTable;Code=$prepared.Code;Length=$prepared.Length;CommandCount=$plan.Commands.Count;ScalePercent=$ScalePercent;Restoring=$false;Frames=@($plan.Frames)}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
[SC2CampaignGate]::Require($game.Id,$moduleBase)
if((Ptr ($moduleBase+0x4032368)) -ne $ui -or (Ptr ($ui+0xc68)) -ne $owner -or (Ptr ($ui+0xc60)) -ne $console){throw 'Campaign HUD changed before resizing.'}
W $owner ([BitConverter]::GetBytes($activeTable)) ([BitConverter]::GetBytes($prepared.VTable))
Signal $prepared.State $owner 1
[pscustomobject]@{Active=$true;Ready=$false;ScalePercent=$ScalePercent}
