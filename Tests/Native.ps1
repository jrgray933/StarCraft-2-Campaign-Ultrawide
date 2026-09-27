$ErrorActionPreference='Stop'
if(-not [Environment]::Is64BitProcess -or $PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use 64-bit Windows PowerShell 5.1.'}
$engine=Join-Path (Split-Path -Parent $PSScriptRoot) 'Engine'
Add-Type -Path @((Join-Path $engine 'CampaignGate.cs'),(Join-Path $engine 'CampaignModeHook.cs'),(Join-Path $engine 'SC2HudScale.cs'),(Join-Path $engine 'SC2AutoStart.cs'),(Join-Path $PSScriptRoot 'CampaignGateTests.cs'))
[CampaignGateTests]::Run()
[CampaignGateTests]::RunQueue()
[CampaignGateTests]::RunCargo()
foreach($path in @('C:\Games\StarCraft II\Versions\Base97563\SC2_x64.exe','D:\Another Library\SC2_x64.exe')){[SC2AutoStart]::ValidateExecutable($path,'5.0.16.97563')}
foreach($case in @(@('C:\Games\other.exe','5.0.16.97563'),@('D:\Games\SC2_x64.exe','5.0.16.99999'))){
    $rejected=$false
    try{[SC2AutoStart]::ValidateExecutable($case[0],$case[1])}catch{$rejected=$true}
    if(!$rejected){throw 'Executable validation accepted an unsupported name or build.'}
}
'PASS: alternate installation directories accepted; wrong executable names and builds rejected.'
