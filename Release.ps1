param([switch]$SkipUi)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Tests\Run.ps1') -SkipUi:$SkipUi
$build=Join-Path $PSScriptRoot 'build'
$exe=Join-Path $build 'Campaign Ultrawide.exe'
$version=[Version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion
$tag='{0}.{1}.{2}' -f $version.Major,$version.Minor,$version.Build
$output=Join-Path $build 'release'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-Zip([string]$path,[hashtable]$files){
    $stream=[IO.File]::Open($path,[IO.FileMode]::Create)
    $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try{foreach($entry in @($files.Keys | Sort-Object)){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$files[$entry],$entry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}}finally{$zip.Dispose();$stream.Dispose()}
}
$binary=@{'Campaign Ultrawide.exe'=$exe}
foreach($name in @('README.md','LICENSE','CHANGELOG.md')){$binary[$name]=Join-Path $PSScriptRoot $name}
$source=@{}
foreach($name in @('Launcher.cs','Build.ps1','Release.ps1','Starcraft-2-Campaign-Ultrawide.csproj','README.md','LICENSE','CHANGELOG.md','CONTRIBUTING.md','.gitignore','.gitattributes','.editorconfig')){$source['Starcraft-2-Campaign-Ultrawide/'+$name]=Join-Path $PSScriptRoot $name}
foreach($folder in @('Engine','Tests','.github')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -Recurse -File -Force){
        $relative=$file.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/')
        $source['Starcraft-2-Campaign-Ultrawide/'+$relative]=$file.FullName
    }
}
$windows=Join-Path $output ('Starcraft-2-Campaign-Ultrawide-'+$tag+'-windows-x64.zip')
$sources=Join-Path $output ('Starcraft-2-Campaign-Ultrawide-'+$tag+'-source.zip')
Write-Zip $windows $binary
Write-Zip $sources $source
$lines=foreach($file in @($windows,$sources)){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)}
$lines | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Release packages: '+$output)
