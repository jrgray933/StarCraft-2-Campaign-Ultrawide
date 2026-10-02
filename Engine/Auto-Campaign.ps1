param([int]$TargetWidth=3440,[int]$TargetHeight=1440,[int]$HudScale=100,[ValidateSet('Off','Fit','Expanded')][string]$HubScale='Off',[int]$MaxZoom=0,[int]$ZoomSteps=5)
$ErrorActionPreference='Stop'
$logPath=Join-Path $PSScriptRoot 'auto-campaign.log'
$statePath=Join-Path $PSScriptRoot 'auto-campaign-state.json'
$stopPath=Join-Path $PSScriptRoot 'auto-campaign-stop.request'
$mutex=New-Object Threading.Mutex($false,'Local\SC2CampaignAuto97563')
$owned=$false;$hubCamera=$null;$missionZoom=$null
function Log([string]$message){Add-Content -LiteralPath $logPath -Value ((Get-Date -Format o)+' '+$message) -Encoding UTF8}
function State([string]$phase,[object]$detail){[pscustomobject]@{WorkerPid=$PID;Updated=(Get-Date -Format o);Phase=$phase;Detail=$detail} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding UTF8}
function Bytes([System.UInt64]$address,[int]$length){$bytes=[SC2Memory]::Read($game.Id,$address,$length);if(!$bytes){throw 'Game data is not ready.'};return ,$bytes}
function Ptr([System.UInt64]$address){[BitConverter]::ToUInt64((Bytes $address 8),0)}
function U32([System.UInt64]$address){[BitConverter]::ToUInt32((Bytes $address 4),0)}
function F32([System.UInt64]$address){[BitConverter]::ToSingle((Bytes $address 4),0)}
function Hex([string]$value){[Convert]::ToUInt64($value,16)}
try{
 try{$owned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owned=$true}
 if(!$owned){exit}
 if(Test-Path -LiteralPath $stopPath){Remove-Item -LiteralPath $stopPath}
 Log 'Automatic campaign helper starting.';State 'Preparing' $null
 Add-Type -Path @((Join-Path $PSScriptRoot 'CampaignGate.cs'),(Join-Path $PSScriptRoot 'HubCamera.cs'),(Join-Path $PSScriptRoot 'MissionZoom.cs'),(Join-Path $PSScriptRoot 'CampaignDisplayRefresh.cs'),(Join-Path $PSScriptRoot 'CampaignModeHook.cs'),(Join-Path $PSScriptRoot 'SC2AutoStart.cs'),(Join-Path $PSScriptRoot 'MemoryRead.cs'),(Join-Path $PSScriptRoot 'SC2HudHook.cs'),(Join-Path $PSScriptRoot 'SC2HudScale.cs'))
 [SC2CampaignModeHook]::ValidateTarget($TargetWidth,$TargetHeight)
 $hudInset=[SC2CampaignModeHook]::HudInset($TargetWidth,$TargetHeight)
 [SC2HudScale]::ValidateScale($TargetWidth,$TargetHeight,$HudScale)
 [SC2MissionZoom]::Validate($MaxZoom);[SC2MissionZoom]::ValidateSteps($ZoomSteps)
 $scaleResetNeeded=$true;$scaleNext=Get-Date;$reportedScale=-1
 State 'Waiting for StarCraft II' $null
 $deadline=(Get-Date).AddMinutes(10);$game=$null
 while(!$game -and (Get-Date) -lt $deadline){
  if(Test-Path -LiteralPath $stopPath){State 'Stopped' $null;exit}
  $games=@(Get-Process SC2_x64 -ErrorAction SilentlyContinue)
  if($games.Count -gt 1){throw 'More than one StarCraft II instance is running.'}
  if($games.Count -eq 1){$game=$games[0];break}
  Start-Sleep -Milliseconds 75
 }
 if(!$game){throw 'StarCraft II did not start within ten minutes.'}
 $expectedExe=$game.MainModule.FileName
 [SC2AutoStart]::ValidateExecutable($expectedExe,$game.MainModule.FileVersionInfo.FileVersion)
 Log ('Detected StarCraft II PID '+$game.Id+'.');State 'Waiting for offline campaign' @{GamePid=$game.Id}
 $moduleBase=[System.UInt64]$game.MainModule.BaseAddress.ToInt64()
 $modeRecordPath=Join-Path $PSScriptRoot 'engine-reset-session.json'
 $attached=$null
 if(Test-Path -LiteralPath $modeRecordPath){
  $oldMode=Get-Content -LiteralPath $modeRecordPath -Raw | ConvertFrom-Json
  if($oldMode.GamePid -eq $game.Id -and ([datetime]$oldMode.Started).ToUniversalTime().Ticks -eq $game.StartTime.ToUniversalTime().Ticks){
   if($oldMode.GateRevision -ne 3){throw 'Close StarCraft II before using the updated campaign restrictions.'}
   if($oldMode.TargetWidth -ne $TargetWidth -or $oldMode.TargetHeight -ne $TargetHeight){throw 'Close StarCraft II before changing resolution.'}
   if((Ptr (Hex $oldMode.Device)) -eq (Hex $oldMode.VTable)){$attached=[pscustomobject]@{Device=(Hex $oldMode.Device);State=(Hex $oldMode.State);VTable=(Hex $oldMode.VTable);AttachedAfterMilliseconds=-1;ResourceAlreadyExisted=$true};Log 'Using the already attached resolution hook.'}
  }
 }
 if(!$attached){$attached=[SC2AutoStart]::Attach($game.Id,$expectedExe,$modeRecordPath,$stopPath,3600000,$TargetWidth,$TargetHeight);if($attached.Recovered){Log 'Recovered the existing resolution hook; continuing campaign setup.'}else{Log ('Resolution hook attached '+[Math]::Round($attached.AttachedAfterMilliseconds)+' ms after process start; existing display resource='+$attached.ResourceAlreadyExisted+'.')}}
 $moduleBase=[System.UInt64]$game.MainModule.BaseAddress.ToInt64()
 $hubCamera=New-Object SC2HubCamera($game.Id,$moduleBase,(Join-Path $PSScriptRoot 'hub-camera-session.txt'))
 $missionZoom=New-Object SC2MissionZoom($game.Id,$moduleBase,(Join-Path $PSScriptRoot 'mission-zoom-session.txt'))
 $refresh=New-Object SC2DisplayRefreshSchedule
 $timer=[Diagnostics.Stopwatch]::StartNew()
 $refreshPhase='';$lastHubStatus=''
 $context='';$stableSince=Get-Date;$hudReady='';$lastError='';$attemptAfter=Get-Date;$lastWidth=0;$lastHeight=0;$blocked=$false;$pendingHud='';$pendingSince=Get-Date
 while(!$game.HasExited){
  if(Test-Path -LiteralPath $stopPath){Log 'Automatic monitoring stopped; existing session fixes remain until restored or game exit.';State 'Stopped' @{GamePid=$game.Id};exit}
  try{
   $settingsFile=Join-Path $PSScriptRoot 'resolution.json'
   if(Test-Path -LiteralPath $settingsFile){$liveSettings=Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json;if($liveSettings.HudScale){[SC2HudScale]::ValidateScale($TargetWidth,$TargetHeight,[int]$liveSettings.HudScale);$HudScale=[int]$liveSettings.HudScale};$HubScale=if($liveSettings.HubScale -in @('Fit','Expanded')){[string]$liveSettings.HubScale}else{'Off'};$MaxZoom=[Math]::Min(100,[int]$liveSettings.MaxZoom);$ZoomSteps=if($liveSettings.ZoomSteps){[int]$liveSettings.ZoomSteps}else{5};[SC2MissionZoom]::ValidateSteps($ZoomSteps);[SC2MissionZoom]::Validate($MaxZoom)}
   try{$null=$missionZoom.Tick($MaxZoom,$ZoomSteps)}catch{$zoomError=$_.Exception.Message;if($zoomError -ne $lastZoomError){Log ('Zoom: '+$zoomError);$lastZoomError=$zoomError}}
   $eligibility=[SC2CampaignGate]::Check($game.Id,$moduleBase)
   $hub=[SC2HubGate]::Check($game.Id,$moduleBase)
   if(!$hub.Allowed){$lastHubStatus='';$null=$hubCamera.Stop()}
   if(!$eligibility.Allowed -or $hub.Allowed){
    if(!$hub.Allowed){$refresh.Clear();$refreshPhase=''}
    if(!$blocked -and !$hub.Allowed){Log $eligibility.Reason}
    $blocked=$true;if(!$hub.Allowed){State 'Waiting for offline campaign' @{GamePid=$game.Id;Reason=$eligibility.Reason}}
    # A pending apply is also checked in its native callback. Restore only an
    # existing, matching live HUD; destroyed mission frames are never touched.
    if($context){
     $currentUi=Ptr ($moduleBase+0x4032368)
     if($currentUi -and (Ptr $currentUi) -eq ($moduleBase+0x2D522A8)){
      $liveOwner=Ptr ($currentUi+0xc68)
      if($liveOwner -eq $owner){$scaled=& (Join-Path $PSScriptRoot 'Scaled-HUD.ps1') -Action Disable;if(!$scaled.Ready){Start-Sleep -Milliseconds 100;continue};$null=& (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Disable -TargetWidth $TargetWidth -TargetHeight $TargetHeight}
     }
     $context='';$hudReady='';$pendingHud=''
    }
    if(!$hub.Allowed){Start-Sleep -Milliseconds 150;continue}
   }
   if($blocked -and $eligibility.Allowed -and !$hub.Allowed){Log ('Offline campaign confirmed: '+$eligibility.MapPath);$lastWidth=0;$blocked=$false}
   $device=Ptr ($moduleBase+0x43D0E08);if(!$device){Start-Sleep -Milliseconds 100;continue}
   $resource=Ptr ($device+0x80);if(!$resource){Start-Sleep -Milliseconds 100;continue}
   $packed=U32 ($resource+0x60);$width=$packed -band 0x3fff;$height=($packed -shr 14) -band 0x3fff
   if($width -ne $lastWidth -or $height -ne $lastHeight){Log ('Display '+$width+'x'+$height+'.');$phase=if($width -eq $TargetWidth -and $height -eq $TargetHeight){'Waiting for campaign mission'}else{'Preparing campaign display'};State $phase @{GamePid=$game.Id;Width=$width;Height=$height;StartupOverrides=(U32 ($attached.State+36))};$lastWidth=$width;$lastHeight=$height}
   $ui=Ptr ($moduleBase+0x4032368)
   $uiReady=$ui -and (Ptr $ui) -eq ($moduleBase+0x2D522A8)
   $atTarget=$width -eq $TargetWidth -and $height -eq $TargetHeight
   $displayContext=if($hub.Allowed){'Hub:'+ $hub.Key}else{$eligibility.MapPath}
   $refreshKey=('{0}:{1:X}:{2:X}' -f $displayContext,$ui,$device)
   $focused=$uiReady -and [SC2CampaignDisplayRefresh]::IsForeground($game.Id)
   $decision=$refresh.Observe($refreshKey,$focused,$atTarget,$timer.Elapsed.TotalMilliseconds)
   if(!$atTarget){
    $phase='Preparing campaign display'
    if($decision -eq 'Focus'){$phase='Return to campaign'}
    elseif($decision -eq 'Failed'){$phase='Display setup paused'}
    elseif($decision -eq 'Request'){
     $result=[SC2CampaignDisplayRefresh]::Request($game.Id,$moduleBase,$device,$attached.VTable,$attached.State)
     if($result -eq 'Requested'){$refresh.Requested($timer.Elapsed.TotalMilliseconds);Log 'Queued the normal graphics-settings update for this campaign.';$phase='Applying campaign resolution'}
     elseif($result -eq 'Waiting for fullscreen mode'){$phase='Waiting for fullscreen'}
    }elseif($decision -eq 'Pending'){$phase='Applying campaign resolution'}
    if($phase -ne $refreshPhase){State $phase @{GamePid=$game.Id;Width=$width;Height=$height};$refreshPhase=$phase}
    Start-Sleep -Milliseconds 100;continue
   }
   $refreshPhase=''
   if($hub.Allowed){
    $cameraReady=$hubCamera.Tick($hub,$HubScale,([double]$TargetWidth/$TargetHeight))
    $hubStatus=$hub.Key+':'+$HubScale+':'+$cameraReady
    if($hubStatus -ne $lastHubStatus){
     if($cameraReady){State 'Ready' @{GamePid=$game.Id;Width=$width;Height=$height;Hub=$true}}else{State 'Preparing campaign display' @{GamePid=$game.Id;Width=$width;Height=$height}}
     $lastHubStatus=$hubStatus
    }
    Start-Sleep -Milliseconds 25;continue
   }

   $ui=Ptr ($moduleBase+0x4032368)
   if(!$ui -or (Ptr $ui) -ne ($moduleBase+0x2D522A8)){$context='';$hudReady='';Start-Sleep -Milliseconds 100;continue}
   $console=Ptr ($ui+0xc60);$owner=Ptr ($ui+0xc68);$world=Ptr ($ui+0xc18)
   if(!$console -or !$owner -or !$world){Start-Sleep -Milliseconds 100;continue}
   $key=('{0:X}:{1:X}:{2:X}:{3:X}:{4}' -f $ui,$owner,$console,$world,$eligibility.MapPath)
   if($key -ne $context){$scaleResetNeeded=$true;$reportedScale=-1;$context=$key;$stableSince=Get-Date;$hudReady='';$pendingHud='';$lastError='';$attemptAfter=(Get-Date).AddMilliseconds(300)}
   if($scaleResetNeeded){$scaled=& (Join-Path $PSScriptRoot 'Scaled-HUD.ps1') -Action Disable;if(!$scaled.Ready){State 'Applying HUD size' $null;Start-Sleep -Milliseconds 100;continue};$scaleResetNeeded=$false}
   if($pendingHud -eq $key){
    $hudRecord=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'hud-runtime-session.json') -Raw | ConvertFrom-Json
    $hudState=Hex $hudRecord.State
    if((U32 $hudState) -eq 0 -and (U32 ($hudState+4)) -eq 0 -and (U32 ($hudState+12)) -gt 0){
     if([Math]::Abs((F32 ($console+0x84))-$hudInset) -lt 0.01 -and [Math]::Abs((F32 ($owner+0x84))-$hudInset) -lt 0.01 -and [Math]::Abs((F32 ($world+0x94))) -lt 0.01 -and (F32 ($ui+0xbfc)) -eq 0 -and $hudRecord.MenuConsoleAnchor -and [Math]::Abs((F32 ((Ptr ($ui+0xc80))+0xa4))+$hudInset) -lt 0.01 -and [Math]::Abs((F32 ((Ptr ($ui+0xc88))+0xa4))+$hudInset) -lt 0.01){
      $hudReady=$key;$pendingHud='';Log 'Campaign HUD and menu buttons centered; bottom corners filled.';State 'Applying HUD size' @{GamePid=$game.Id;Width=$width;Height=$height}
     }else{$pendingHud='';$attemptAfter=(Get-Date).AddSeconds(1)}
    }elseif(((Get-Date)-$pendingSince).TotalSeconds -gt 20){Log 'HUD job is waiting for a normal layout callback.';$pendingSince=Get-Date}
   }
   if($hudReady -ne $key -and !$pendingHud -and (Get-Date) -ge $attemptAfter){
    [SC2CampaignGate]::Require($game.Id,$moduleBase)
    $attemptAfter=(Get-Date).AddSeconds(2)
    if((Ptr $console) -ne ($moduleBase+0x2D62E98) -or (Ptr $world) -ne ($moduleBase+0x2D6C370)){throw 'Waiting for the supported campaign console.'}
    $result=& (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Enable -TargetWidth $TargetWidth -TargetHeight $TargetHeight
    $pendingHud=$key;$pendingSince=Get-Date;Log 'Mission UI ready; queued native HUD and viewport update.';State 'Applying campaign HUD' @{GamePid=$game.Id;Width=$width;Height=$height}
   }
   if($hudReady -eq $key -and (Get-Date) -ge $scaleNext){
    $scaleNext=(Get-Date).AddMilliseconds(500)
    $scaled=& (Join-Path $PSScriptRoot 'Scaled-HUD.ps1') -Action Enable -TargetWidth $TargetWidth -TargetHeight $TargetHeight -ScalePercent $HudScale
    if($scaled.Ready){
     if($reportedScale -ne $HudScale){Log ('Bottom HUD size applied: '+$HudScale+'%.');$reportedScale=$HudScale}
     State 'Ready' @{GamePid=$game.Id;Width=$width;Height=$height;Hud='Centered';HudScale=$HudScale;Corners='Filled';Context=$key}
    }else{State 'Applying HUD size' @{GamePid=$game.Id;HudScale=$HudScale}}
   }
  }catch{
   $message=$_.Exception.Message
   if($message -ne $lastError){Log ('Waiting: '+$message);$lastError=$message}
  }
  Start-Sleep -Milliseconds 100
 }
 Log 'Game exited; automatic helper finished.';State 'Game closed' $null
}catch{if(Test-Path -LiteralPath $stopPath){Log 'Automatic helper stopped.';State 'Stopped' $null;exit 0};Log ('ERROR: '+$_.Exception.Message);State 'Error' $_.Exception.Message;exit 1}
finally{if($missionZoom){try{$null=$missionZoom.Stop()}catch{Log ('Zoom restore: '+$_.Exception.Message)}};if($hubCamera){try{$null=$hubCamera.Stop()}catch{}};if($owned){$mutex.ReleaseMutex()};$mutex.Dispose()}



