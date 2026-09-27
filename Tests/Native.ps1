$ErrorActionPreference='Stop'
if(-not [Environment]::Is64BitProcess -or $PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use 64-bit Windows PowerShell 5.1.'}
$engine=Join-Path (Split-Path -Parent $PSScriptRoot) 'Engine'
Add-Type -Path @((Join-Path $engine 'CampaignGate.cs'),(Join-Path $engine 'CampaignModeHook.cs'),(Join-Path $engine 'SC2HudScale.cs'),(Join-Path $engine 'SC2AutoStart.cs'),(Join-Path $PSScriptRoot 'CampaignGateTests.cs'))
[CampaignGateTests]::Run()
[CampaignGateTests]::RunQueue()
[CampaignGateTests]::RunCargo()
foreach($path in @('C:\Games\StarCraft II\Versions\Base97563\SC2_x64.exe','D:\Another Library\SC2_x64.exe')){
    foreach($version in @('5.0.16.97563','5.0.16.99999','6.0.0.100000','',$null)){
        [SC2AutoStart]::ValidateExecutable($path,$version)
    }
}
foreach($path in @('C:\Games\other.exe','D:\Games\SC2.exe','')){
    $rejected=$false
    try{[SC2AutoStart]::ValidateExecutable($path,'6.0.0.100000')}catch{$rejected=$true}
    if(!$rejected){throw 'Executable validation accepted an unsupported executable name.'}
}
'PASS: alternate installation directories and game versions accepted; wrong executable names rejected.'
