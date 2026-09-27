param([ValidateSet('EnableResolution','DisableResolution','EnableHUD','RestoreHUD','Inspect','SelfTest')][string]$Action)
$ErrorActionPreference='Stop'
try {
 if($Action -eq 'SelfTest') {
  foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.ps1) {
   $tokens=$null;$errors=$null
   [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
   if($errors.Count){throw ($errors | Out-String)}
  }
  Add-Type -Path @((Join-Path $PSScriptRoot 'CampaignGate.cs'),(Join-Path $PSScriptRoot 'CampaignDisplayRefresh.cs'),(Join-Path $PSScriptRoot 'CampaignModeHook.cs'),(Join-Path $PSScriptRoot 'SC2AutoStart.cs'),(Join-Path $PSScriptRoot 'MemoryRead.cs'),(Join-Path $PSScriptRoot 'SC2HudHook.cs'),(Join-Path $PSScriptRoot 'SC2HudScale.cs'))
  [SC2CampaignModeHook]::ModeSelfTest()
  [SC2HudHook]::SelfTest()
  [SC2HudScale]::SelfTest()
  [SC2HudScale]::BatchSelfTest()
  [SC2HudScale]::TextSelfTest()
  [SC2HudScale]::ScaleSelfTest()
  [SC2HudScale]::DynamicSelfTest()
  [SC2HudScale]::CargoSelfTest()
  [SC2DisplayRefreshSchedule]::SelfTest()
  'All embedded scripts parsed and both native wrapper self-tests completed.'
  exit 0
 }
 $settings=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'resolution.json') -Raw | ConvertFrom-Json
 $TargetWidth=[int]$settings.Width;$TargetHeight=[int]$settings.Height
 $HudScale=100;if($settings.HudScale){$HudScale=[int]$settings.HudScale}
 if($Action -eq 'EnableHUD' -or $Action -eq 'RestoreHUD'){
  $deadline=(Get-Date).AddMinutes(2)
  do{$scaled=& (Join-Path $PSScriptRoot 'Scaled-HUD.ps1') -Action Disable;if($scaled.Ready){break};Start-Sleep -Milliseconds 250}while((Get-Date) -lt $deadline)
  if(!$scaled.Ready){throw 'Return to the campaign to finish restoring the previous HUD size.'}
 }
 switch($Action) {
  EnableResolution { & (Join-Path $PSScriptRoot 'Campaign-Ultrawide.ps1') -Action Install -TargetWidth $TargetWidth -TargetHeight $TargetHeight }
  DisableResolution { & (Join-Path $PSScriptRoot 'Campaign-Ultrawide.ps1') -Action Disable -TargetWidth $TargetWidth -TargetHeight $TargetHeight }
  EnableHUD { & (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Enable -TargetWidth $TargetWidth -TargetHeight $TargetHeight }
  Inspect {
   & (Join-Path $PSScriptRoot 'Campaign-Ultrawide.ps1') -Action Status -TargetWidth $TargetWidth -TargetHeight $TargetHeight
   & (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Status -TargetWidth $TargetWidth -TargetHeight $TargetHeight
   & (Join-Path $PSScriptRoot 'Scaled-HUD.ps1') -Action Status | ConvertTo-Json
  }
  RestoreHUD {
   & (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Disable -TargetWidth $TargetWidth -TargetHeight $TargetHeight
   'Return to the mission to allow restoration. Waiting up to two minutes for the layout callback...'
   $deadline=(Get-Date).AddMinutes(2)
   do {
    Start-Sleep -Seconds 2
    $status=& (Join-Path $PSScriptRoot 'Centered-HUD.ps1') -Action Status -TargetWidth $TargetWidth -TargetHeight $TargetHeight
    $text=$status -join "`n"
    if($text.StartsWith('{')) {
     $state=$text | ConvertFrom-Json
     if(!$state.Attached){'Original HUD restored and wrapper detached.';exit 0}
    } else { $text;exit 0 }
   } while((Get-Date) -lt $deadline)
   throw 'Restoration is queued. Return to the mission, then use Inspect hooks to complete detachment.'
  }
 }
} catch { Write-Error $_;exit 1 }
