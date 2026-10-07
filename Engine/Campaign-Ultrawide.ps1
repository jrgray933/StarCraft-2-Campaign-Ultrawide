param([ValidateSet('Install','Status','Disable')][string]$Action='Status',[int]$TargetWidth=3440,[int]$TargetHeight=1440)
$ErrorActionPreference='Stop'
if(-not ('SC2CampaignModeHook' -as [type])){Add-Type -Path @((Join-Path $PSScriptRoot 'SC2Addresses.cs'),(Join-Path $PSScriptRoot 'CampaignGate.cs'),(Join-Path $PSScriptRoot 'HubCamera.cs'),(Join-Path $PSScriptRoot 'CampaignModeHook.cs'),(Join-Path $PSScriptRoot 'SC2HudHook.cs'),(Join-Path $PSScriptRoot 'MemoryRead.cs'))}
[SC2CampaignModeHook]::ValidateTarget($TargetWidth,$TargetHeight)
$recordPath=Join-Path $PSScriptRoot 'engine-reset-session.json'
$games=@(Get-Process SC2_x64 -ErrorAction SilentlyContinue)
if($games.Count -ne 1){throw 'Open exactly one StarCraft II session first'}
$game=$games[0]
$base=$game.MainModule.BaseAddress.ToInt64()
[SC2Addresses]::Ensure($game.Id,[System.UInt64]$base)
function ReadBytes([System.UInt64]$address,[int]$size){$bytes=[SC2Memory]::Read($game.Id,$address,$size);if(!$bytes){throw 'Cannot read graphics state'};return ,$bytes}
function Ptr([System.UInt64]$address){[BitConverter]::ToUInt64((ReadBytes $address 8),0)}
function U32([System.UInt64]$address){[BitConverter]::ToUInt32((ReadBytes $address 4),0)}
function Hex([string]$value){[Convert]::ToUInt64($value,16)}
$device=Ptr ($base+([SC2Addresses]::Rva(0x43D0E08)))
$originalVtable=[System.UInt64]($base+([SC2Addresses]::Rva(0x2DB8098)))
$resourceVtable=[System.UInt64]($base+([SC2Addresses]::Rva(0x2DB9DA8)))
$record=$null
if(Test-Path -LiteralPath $recordPath){
 $candidate=Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
 if($candidate.GamePid -eq $game.Id -and ([datetime]$candidate.Started).ToUniversalTime().Ticks -eq $game.StartTime.ToUniversalTime().Ticks){$record=$candidate}
}
if($Action -eq 'Install'){
 [SC2DisplayGate]::Require($game.Id,[System.UInt64]$base)
 if($record -and $record.GateRevision -ne 3){throw 'Close StarCraft II before using the updated campaign restrictions.'}
 if($record -and (Ptr $device) -eq (Hex $record.VTable)){Write-Output 'Campaign ultrawide wrapper is already attached.'}
 else{
  if((Ptr $device) -ne $originalVtable){throw 'Unexpected graphics device type'}
  if((Ptr ($originalVtable+0x28)) -ne ($base+([SC2Addresses]::Rva(0xE60F70)))){throw 'Mode selection method mismatch'}
  $resource=Ptr ($device+0x80)
  if((Ptr $resource) -ne $resourceVtable){throw 'Unexpected display resource type'}
  $packed=U32 ($resource+0x60)
  if(($packed -band 0x3fff) -lt 1280 -or (($packed -shr 14) -band 0x3fff) -lt 720){throw 'Choose a widescreen fullscreen mode in Graphics options first'}
  if((U32 ($resource+0x80)) -band 255){throw 'Use fullscreen display mode'}
  $prepared=[SC2CampaignModeHook]::Prepare($game.Id,$originalVtable,$TargetWidth,$TargetHeight)
  $record=[pscustomobject]@{GateRevision=3;GamePid=$game.Id;Started=$game.StartTime.ToString('o');Build=$game.MainModule.FileVersionInfo.FileVersion;TargetWidth=$TargetWidth;TargetHeight=$TargetHeight;Device=$device.ToString('X');OriginalVTable=$originalVtable.ToString('X');VTable=$prepared.VTable.ToString('X');Code=$prepared.Code.ToString('X');State=$prepared.State.ToString('X');Length=$prepared.Length}
  $record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
  [SC2DisplayGate]::Require($game.Id,[System.UInt64]$base)
  [SC2CampaignModeHook]::SwapPointer($game.Id,$device,$originalVtable,$prepared.VTable)
  Write-Output 'Resolution enabled. Change the fullscreen resolution in Graphics options once to apply your selection.'
 }
}elseif($Action -eq 'Disable'){
 if(!$record){Write-Output 'No engine wrapper recorded for this game session.';exit}
 [SC2CampaignModeHook]::Disable($game.Id,(Hex $record.State))
 if($device -eq (Hex $record.Device) -and (Ptr $device) -eq (Hex $record.VTable)){
  [SC2CampaignModeHook]::SwapPointer($game.Id,$device,(Hex $record.VTable),(Hex $record.OriginalVTable))
  Write-Output 'Resolution override disabled. Choose your preferred resolution in Graphics options, or close the game.'
 }else{Write-Output 'Wrapper disabled; graphics device has already changed.'}
 exit
}
if(!$record){Write-Output 'No engine wrapper recorded for this game session.';exit}
$state=ReadBytes (Hex $record.State) 64
$resource=Ptr ($device+0x80)
$packed=U32 ($resource+0x60)
[pscustomobject]@{
 GamePid=$game.Id
 Attached=($device -eq (Hex $record.Device) -and (Ptr $device) -eq (Hex $record.VTable))
 Enabled=([BitConverter]::ToUInt32($state,0) -eq 1)
 CurrentWidth=($packed -band 0x3FFF)
 CurrentHeight=(($packed -shr 14) -band 0x3FFF)
 ModeCalls=[BitConverter]::ToUInt32($state,32)
 ModeOverrides=[BitConverter]::ToUInt32($state,36)
 RequestedWidth=[BitConverter]::ToUInt32($state,40)
 RequestedHeight=[BitConverter]::ToUInt32($state,44)
 CachedWidth=U32 ($device+0x28)
 CachedHeight=U32 ($device+0x2C)
} | ConvertTo-Json



