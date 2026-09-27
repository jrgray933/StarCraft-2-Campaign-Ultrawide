param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference='Stop'
if(-not [Environment]::Is64BitOperatingSystem){throw 'Build on 64-bit Windows.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'The .NET Framework C# compiler is required.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$engineFiles=@(
    'Auto-Campaign.ps1',
    'Campaign-Ultrawide.ps1',
    'CampaignDisplayRefresh.cs',
    'CampaignGate.cs',
    'CampaignModeHook.cs',
    'Centered-HUD.ps1',
    'Control.ps1',
    'MemoryRead.cs',
    'SC2AutoStart.cs',
    'SC2HudHook.cs',
    'SC2HudScale.cs',
    'Scaled-HUD.ps1'
)
$arguments=@('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',('/out:'+(Join-Path $OutputDirectory 'Campaign Ultrawide.exe')),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Core.dll')
foreach($name in $engineFiles){
    $file=Join-Path (Join-Path $PSScriptRoot 'Engine') $name
    if(!(Test-Path -LiteralPath $file)){throw ('Missing engine source: '+$name)}
    $arguments+=('/resource:'+$file+',Engine.'+$name)
}
$arguments+=(Join-Path $PSScriptRoot 'Launcher.cs')
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
Write-Output (Join-Path $OutputDirectory 'Campaign Ultrawide.exe')
