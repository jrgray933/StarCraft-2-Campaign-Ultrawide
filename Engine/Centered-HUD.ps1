param([ValidateSet('Enable','Disable','Status')][string]$Action='Status',[int]$TargetWidth=3440,[int]$TargetHeight=1440)
$ErrorActionPreference='Stop'
if(-not ('SC2CampaignModeHook' -as [type])){Add-Type -Path @((Join-Path $PSScriptRoot 'CampaignGate.cs'),(Join-Path $PSScriptRoot 'CampaignModeHook.cs'),(Join-Path $PSScriptRoot 'SC2HudHook.cs'),(Join-Path $PSScriptRoot 'MemoryRead.cs'))}
[SC2CampaignModeHook]::ValidateTarget($TargetWidth,$TargetHeight)
$hudInset=[SC2CampaignModeHook]::HudInset($TargetWidth,$TargetHeight)
$games=@(Get-Process SC2_x64 -ErrorAction SilentlyContinue)
if($games.Count -ne 1){throw 'Open exactly one StarCraft II campaign session first.'}
$game=$games[0]
$moduleBase=[System.UInt64]$game.MainModule.BaseAddress.ToInt64()
function ReadBytes([System.UInt64]$address,[int]$size){$bytes=[SC2Memory]::Read($game.Id,$address,$size);if(!$bytes){throw 'Cannot read HUD state.'};return ,$bytes}
function Ptr([System.UInt64]$address){[BitConverter]::ToUInt64((ReadBytes $address 8),0)}
function U32([System.UInt64]$address){[BitConverter]::ToUInt32((ReadBytes $address 4),0)}
function Hex([string]$value){[Convert]::ToUInt64($value,16)}
function WriteChecked([System.UInt64]$address,[byte[]]$expected,[byte[]]$replacement){[SC2HudHook]::CheckedWrite($game.Id,$address,$expected,$replacement)}
function Signal([System.UInt64]$state,[System.UInt64]$owner,[int]$request){
 if((U32 ($state+4)) -ne 0){throw 'HUD update is busy; run again.'}
 WriteChecked $state ([BitConverter]::GetBytes([int]0)) ([BitConverter]::GetBytes($request))
 # Request an ordinary layout notification. Native callback applies the job on
 # the game's own UI path; no remote thread or external reset loop is used.
 $flag=ReadBytes ($owner+0x28) 1
 WriteChecked ($owner+0x28) $flag ([byte[]]@([byte]($flag[0] -bor 0x10)))
}
$gameUI=Ptr ($moduleBase+0x4032368)
if(!$gameUI -or (Ptr $gameUI) -ne ($moduleBase+0x2D522A8)){throw 'Load a campaign mission before using the HUD helper.'}
$console=Ptr ($gameUI+0xc60)
$owner=Ptr ($gameUI+0xc68)
$world=Ptr ($gameUI+0xc18)
$originalTable=$moduleBase+0x2D51C58
$recordPath=Join-Path $PSScriptRoot 'hud-runtime-session.json'
$record=$null
if(Test-Path -LiteralPath $recordPath){$candidate=Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json;if($candidate.GamePid -eq $game.Id -and ([datetime]$candidate.Started).ToUniversalTime().Ticks -eq $game.StartTime.ToUniversalTime().Ticks -and $candidate.Owner -eq $owner.ToString('X')){$record=$candidate}}
if($Action -eq 'Enable'){[SC2CampaignGate]::Require($game.Id,$moduleBase);if($record -and $record.GateRevision -ne 2){throw 'Close StarCraft II before using the updated campaign restrictions.'}}
if($Action -eq 'Enable' -and $record -and ($record.Console -ne $console.ToString('X') -or ($record.WorldPanel -and $record.WorldPanel -ne $world.ToString('X')))){
 # A mission can recreate dependent frames while retaining the console root.
 # Retire the old job before rebuilding its frame references.
 $oldState=Hex $record.State
 if((U32 ($oldState+4)) -ne 0){throw 'Previous mission HUD update is still busy.'}
 $pending=U32 $oldState
 if($pending -ne 0){WriteChecked $oldState ([BitConverter]::GetBytes($pending)) ([BitConverter]::GetBytes([uint32]0))}
 if((Ptr $owner) -eq (Hex $record.VTable)){WriteChecked $owner ([BitConverter]::GetBytes((Hex $record.VTable))) ([BitConverter]::GetBytes($originalTable))}
 $record=$null
}
if($Action -eq 'Enable'){
 $device=Ptr ($moduleBase+0x43D0E08);$resource=Ptr ($device+0x80);$packed=U32 ($resource+0x60)
 if(($packed -band 0x3fff) -ne $TargetWidth -or (($packed -shr 14) -band 0x3fff) -ne $TargetHeight){throw 'Activate your selected resolution before centering the HUD.'}
 if($record -and ($record.TargetWidth -ne $TargetWidth -or $record.TargetHeight -ne $TargetHeight)){throw 'Close StarCraft II before changing resolution.'}
 if(!$record -or (Ptr $owner) -ne (Hex $record.VTable)){
  if((Ptr $owner) -ne $originalTable -or (Ptr $console) -ne ($moduleBase+0x2D62E98)){throw 'Unexpected console frame type; no changes made.'}
  if((Ptr ($originalTable+0x148)) -ne ($moduleBase+0x16B53C0)){throw 'Layout callback mismatch.'}
  $expected=[byte[]]@(0x40,0x53,0x55,0x57,0x48,0x83,0xec,0x50,0x0f,0x29,0x74,0x24,0x40,0x49,0x8b,0xe8,0x66,0x41,0x0f,0x6e,0xf1,0x48,0x8b,0xf9,0xf3,0x0f,0x11,0x74,0x24,0x20,0x8b,0xda)
  if([BitConverter]::ToString((ReadBytes ($moduleBase+0x16BCFC0) $expected.Length)) -ne [BitConverter]::ToString($expected)){throw 'Native anchor function signature mismatch.'}
  $commands=New-Object 'System.Collections.Generic.List[SC2HudHook+Command]'
  foreach($frame in @($console,$owner)){
   $parent=Ptr ($frame+0x50)
   foreach($side in @(1,3)){
    $anchor=ReadBytes ($frame+0x68+16*$side) 16
    $relative=[BitConverter]::ToUInt64($anchor,0);$position=[BitConverter]::ToInt16($anchor,8)/2048.0;$offset=[BitConverter]::ToSingle($anchor,12)
    $newPosition=0.0;$newOffset=$hudInset
    if($side -eq 3){$newPosition=1.0;$newOffset=-$hudInset}
    if($relative -ne $parent -or $position -ne $newPosition -or ([Math]::Abs($offset) -gt 0.001 -and [Math]::Abs($offset-$newOffset) -gt 0.001) -or ([BitConverter]::ToUInt16($anchor,10) -band 4)){throw 'Console anchors differ from the verified default layout.'}
    $command=New-Object 'SC2HudHook+Command';$command.Frame=$frame;$command.Parent=$parent;$command.Side=$side;$command.Position=$newPosition;$command.Offset=$newOffset;$command.OldPosition=$position;$command.OldOffset=0;$commands.Add($command)
   }
  }
  $prepared=[SC2HudHook]::Prepare($game.Id,$owner,$originalTable,($moduleBase+0x16BCFC0),$commands.ToArray())
  $record=[pscustomobject]@{GateRevision=2;GamePid=$game.Id;Started=$game.StartTime.ToString('o');Build=$game.MainModule.FileVersionInfo.FileVersion;TargetWidth=$TargetWidth;TargetHeight=$TargetHeight;Owner=$owner.ToString('X');Console=$console.ToString('X');OriginalVTable=$originalTable.ToString('X');VTable=$prepared.VTable.ToString('X');Code=$prepared.Code.ToString('X');State=$prepared.State.ToString('X');Length=$prepared.Length;Commands=@($commands | ForEach-Object {[pscustomobject]@{Frame=$_.Frame.ToString('X');Parent=$_.Parent.ToString('X');Side=$_.Side;Position=$_.Position;Offset=$_.Offset;OldPosition=$_.OldPosition;OldOffset=$_.OldOffset}})}
  $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
  if((Ptr ($gameUI+0xc68)) -ne $owner){throw 'Mission UI changed before attachment.'}
  WriteChecked $owner ([BitConverter]::GetBytes($originalTable)) ([BitConverter]::GetBytes($prepared.VTable))
 }
 $stateAddress=Hex $record.State
 if((U32 $stateAddress) -ne 0 -or (U32 ($stateAddress+4)) -ne 0){throw 'A HUD update is pending; return to the mission first.'}
 if(!$record.WorldOffsetAddress){
  $world=Ptr ($gameUI+0xc18)
  if((Ptr $world) -ne ($moduleBase+0x2D6C370)){throw 'Unexpected world panel type.'}
  $worldAnchor=ReadBytes ($world+0x88) 16
  if([BitConverter]::ToUInt64($worldAnchor,0) -ne $gameUI -or [BitConverter]::ToInt16($worldAnchor,8) -ne 2048 -or [BitConverter]::ToSingle($worldAnchor,12) -ne -200 -or [BitConverter]::ToSingle((ReadBytes ($gameUI+0xbfc) 4),0) -ne 200){throw 'World viewport differs from the verified campaign layout.'}
  if((U32 ($stateAddress+48)) -ne 4){throw 'Unexpected HUD command count.'}
  $worldCommand=[pscustomobject]@{Frame=$world.ToString('X');Parent=$gameUI.ToString('X');Side=2;Position=1.0;Offset=0.0;OldPosition=1.0;OldOffset=-200.0}
  $buffer=New-Object byte[] 40
  [Array]::Copy([BitConverter]::GetBytes($world),0,$buffer,0,8)
  [Array]::Copy([BitConverter]::GetBytes($gameUI),0,$buffer,8,8)
  [Array]::Copy([BitConverter]::GetBytes([int]2),0,$buffer,16,4)
  [Array]::Copy([BitConverter]::GetBytes([single]1),0,$buffer,20,4)
  [Array]::Copy([BitConverter]::GetBytes([single]1),0,$buffer,28,4)
  [Array]::Copy([BitConverter]::GetBytes([single]-200),0,$buffer,32,4)
  $record.Commands=@($record.Commands)+@($worldCommand)
  $record | Add-Member NoteProperty WorldOffsetAddress ($gameUI+0xbfc).ToString('X')
  $record | Add-Member NoteProperty WorldOffsetOriginal 200.0
  $record | Add-Member NoteProperty WorldPanel $world.ToString('X')
  # Save the rollback value before any new live write.
  $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
  WriteChecked ($stateAddress+64+4*40) (New-Object byte[] 40) $buffer
  WriteChecked ($stateAddress+48) ([BitConverter]::GetBytes([int]4)) ([BitConverter]::GetBytes([int]5))
 }
 # MenuBar has separate console/fullscreen anchor frames, outside the
 # centered console roots. Shift their right edges; self-relative left anchors
 # preserve the button strip width and vertical anchors remain unchanged.
 if(!$record.MenuConsoleAnchor){
  if((U32 ($stateAddress+48)) -ne 5){throw 'Unexpected HUD command count before menu alignment.'}
  $menuCommands=@();$menuFrames=@()
  foreach($member in @(0xc80,0xc88)){
   $frame=Ptr ($gameUI+$member);$parent=Ptr ($frame+0x50)
   if(!$frame -or !$parent -or (Ptr $frame) -ne $originalTable){throw 'Unexpected menu anchor frame.'}
   $anchor=ReadBytes ($frame+0x98) 16
   $offset=[BitConverter]::ToSingle($anchor,12)
   if([BitConverter]::ToUInt64($anchor,0) -ne $parent -or [BitConverter]::ToInt16($anchor,8) -ne 2048 -or ([BitConverter]::ToUInt16($anchor,10) -band 4) -or ([Math]::Abs($offset) -gt 0.001 -and [Math]::Abs($offset+$hudInset) -gt 0.001)){throw 'Menu anchors differ from the supported layout.'}
   $menuFrames+=,$frame
   $menuCommands += [pscustomobject]@{Frame=$frame.ToString('X');Parent=$parent.ToString('X');Side=3;Position=1.0;Offset=-$hudInset;OldPosition=1.0;OldOffset=0.0}
  }
  $record.Commands=@($record.Commands)+$menuCommands
  $record | Add-Member NoteProperty MenuConsoleAnchor $menuFrames[0].ToString('X')
  $record | Add-Member NoteProperty MenuFullscreenAnchor $menuFrames[1].ToString('X')
  $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
  $buffer=New-Object byte[] 80
  for($i=0;$i -lt 2;$i++){
   $entry=$menuCommands[$i];$index=40*$i
   [Array]::Copy([BitConverter]::GetBytes((Hex $entry.Frame)),0,$buffer,$index,8)
   [Array]::Copy([BitConverter]::GetBytes((Hex $entry.Parent)),0,$buffer,($index+8),8)
   [Array]::Copy([BitConverter]::GetBytes([int]3),0,$buffer,($index+16),4)
   [Array]::Copy([BitConverter]::GetBytes([single]1),0,$buffer,($index+20),4)
   [Array]::Copy([BitConverter]::GetBytes([single](-$hudInset)),0,$buffer,($index+24),4)
   [Array]::Copy([BitConverter]::GetBytes([single]1),0,$buffer,($index+28),4)
  }
  [SC2CampaignGate]::Require($game.Id,$moduleBase)
  WriteChecked ($stateAddress+64+5*40) (New-Object byte[] 80) $buffer
  WriteChecked ($stateAddress+48) ([BitConverter]::GetBytes([int]5)) ([BitConverter]::GetBytes([int]7))
 }
 if((Ptr ($gameUI+0xc80)) -ne (Hex $record.MenuConsoleAnchor) -or (Ptr ($gameUI+0xc88)) -ne (Hex $record.MenuFullscreenAnchor)){throw 'Menu frames changed; reload the mission before reapplying.'}
 if((U32 ($stateAddress+48)) -ne @($record.Commands).Count){throw 'HUD command state is incomplete; no further changes.'}
 $property=Hex $record.WorldOffsetAddress
 if($property -ne ($gameUI+0xbfc)){throw 'Game UI changed; no viewport write.'}
 $propertyBytes=ReadBytes $property 4
 $value=[BitConverter]::ToSingle($propertyBytes,0)
 if($value -ne 0 -and $value -ne $record.WorldOffsetOriginal){throw 'World crop was changed by another source.'}
 [SC2CampaignGate]::Require($game.Id,$moduleBase)
 WriteChecked $property $propertyBytes ([BitConverter]::GetBytes([single]0))
 [SC2CampaignGate]::Require($game.Id,$moduleBase)
 $record | Add-Member NoteProperty RestoreRequested $false -Force
 $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
 Signal $stateAddress $owner 1
 Write-Output 'Centered console update queued. Return to the paused mission to let the game update its UI.'
}elseif($Action -eq 'Disable'){
 if(!$record -or (Ptr $owner) -ne (Hex $record.VTable)){Write-Output 'No HUD wrapper is attached to this mission.';exit}
 $stateAddress=Hex $record.State
 $pending=U32 $stateAddress
 if($pending -ne 0){WriteChecked $stateAddress ([BitConverter]::GetBytes($pending)) ([BitConverter]::GetBytes([uint32]0))}
 if($record.WorldOffsetAddress){
  if((U32 ($stateAddress+48)) -ne @($record.Commands).Count){throw 'HUD command state is incomplete; no further changes.'}
 $property=Hex $record.WorldOffsetAddress
  if($property -ne ($gameUI+0xbfc)){throw 'Game UI changed; no viewport restoration.'}
  $propertyBytes=ReadBytes $property 4
  $value=[BitConverter]::ToSingle($propertyBytes,0)
  if($value -ne 0 -and $value -ne $record.WorldOffsetOriginal){throw 'World crop was changed by another source.'}
  WriteChecked $property $propertyBytes ([BitConverter]::GetBytes([single]$record.WorldOffsetOriginal))
 }
 $record | Add-Member NoteProperty RestoreRequested $true -Force
 $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
 Signal $stateAddress $owner 2
 Write-Output 'Original console anchors queued for restoration. Return to the mission, then run Status to detach the completed wrapper.'
}
if(!$record){Write-Output 'No centered HUD helper is recorded for this mission.';exit}
$state=ReadBytes (Hex $record.State) 64
$frames=@()
foreach($entry in @([pscustomobject]@{Name='Console artwork';Address=$console},[pscustomobject]@{Name='Console controls';Address=$owner})){
 $b=ReadBytes $entry.Address 0xc0
 $frames += [pscustomobject]@{Name=$entry.Name;LeftOffset=[BitConverter]::ToSingle($b,0x84);RightOffset=[BitConverter]::ToSingle($b,0xa4);RectLeft=[BitConverter]::ToSingle($b,0xac);RectRight=[BitConverter]::ToSingle($b,0xb4);LayoutFlags=$b[0x28]}
}
$attached=((Ptr $owner) -eq (Hex $record.VTable))
if($Action -eq 'Status' -and $record.RestoreRequested -and $attached -and [BitConverter]::ToUInt32($state,0) -eq 0 -and [BitConverter]::ToUInt32($state,4) -eq 0 -and [BitConverter]::ToUInt32($state,12) -gt 0 -and [Math]::Abs($frames[0].LeftOffset) -lt 0.001 -and [Math]::Abs($frames[0].RightOffset) -lt 0.001 -and [Math]::Abs($frames[1].LeftOffset) -lt 0.001 -and [Math]::Abs($frames[1].RightOffset) -lt 0.001){WriteChecked $owner ([BitConverter]::GetBytes((Hex $record.VTable))) ([BitConverter]::GetBytes($originalTable));$attached=$false}
[pscustomobject]@{GamePid=$game.Id;Attached=$attached;Pending=[BitConverter]::ToUInt32($state,0);Busy=[BitConverter]::ToUInt32($state,4);LayoutCallbacks=[BitConverter]::ToUInt32($state,8);CompletedUpdates=[BitConverter]::ToUInt32($state,12);SuccessfulAnchors=[BitConverter]::ToUInt32($state,16);WorldCrop=[BitConverter]::ToSingle((ReadBytes ($gameUI+0xbfc) 4),0);WorldBottom=[BitConverter]::ToSingle((ReadBytes ((Ptr ($gameUI+0xc18))+0xb0) 4),0);Frames=$frames} | ConvertTo-Json -Depth 4



