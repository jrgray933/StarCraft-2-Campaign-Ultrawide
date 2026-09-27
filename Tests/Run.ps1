param([switch]$SkipUi)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$exe=& (Join-Path $project 'Build.ps1')
$hostExe=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$native=Join-Path $PSScriptRoot 'Native.ps1'
& $hostExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $native
if($LASTEXITCODE -ne 0){throw 'Native eligibility and installation tests failed.'}
$modes=@('--self-test')
if(!$SkipUi){$modes+='--smoke-test'}
foreach($mode in $modes){
    $process=Start-Process -FilePath $exe -ArgumentList $mode -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(90000)){$process.Kill();throw ('Test timed out: '+$mode)}
    $report=Join-Path (Split-Path -Parent $exe) $(if($mode -eq '--self-test'){'self-test-results.txt'}else{'layout-test-results.txt'})
    if(Test-Path -LiteralPath $report){Get-Content -LiteralPath $report -Encoding UTF8}
    if($process.ExitCode -ne 0){throw ('Test failed: '+$mode+' (exit '+$process.ExitCode+')')}
}
'All requested tests passed.'
