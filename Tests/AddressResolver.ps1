param([Parameter(Mandatory=$true)][string]$ImagePath,[Parameter(Mandatory=$true)][UInt64]$ModuleBase)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'Engine\SC2Addresses.cs')
$image=[IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $ImagePath))
$result=[SC2Addresses]::ResolveImage($image,$ModuleBase)
if($result.Count -ne 44){throw 'Incomplete address table.'}
function Reject([byte[]]$bytes,[string]$label){
 $rejected=$false;try{[void][SC2Addresses]::ResolveImage($bytes,$ModuleBase)}catch{$rejected=$true}
 if(!$rejected){throw ('Accepted '+$label)}
 'PASS: rejects '+$label
}
# Damage one uniquely matched function, then restore the snapshot.
$address=[int]$result[[UInt64]0x16bcfc0];$saved=$image[$address];$image[$address]=$saved -bxor 255
Reject $image 'missing anchor setter';$image[$address]=$saved
# Duplicate its signature inside executable space. No first-match fallback.
$copy=[byte[]]$image.Clone();[Array]::Copy($image,$address,$copy,0x1000,128)
Reject $copy 'ambiguous anchor setter';$copy=$null
# Independently resolved vtable and method must agree.
$slot=[int]$result[[UInt64]0x2db8098]+0x28;$saved=$image[$slot];$image[$slot]=$saved -bxor 1
Reject $image 'changed native method relationship';$image[$slot]=$saved
# Refuse an unrecognized map-member accessor rather than guessing its offset.
$slot=[int]$result[[UInt64]0x2e99200]+0xd0;$getter=[int]([BitConverter]::ToUInt64($image,$slot)-$ModuleBase)
$saved=$image[$getter];$image[$getter]=0xcc;Reject $image 'changed map accessor';$image[$getter]=$saved
# Rechecking the intact snapshot gives exactly the same result.
$again=[SC2Addresses]::ResolveImage($image,$ModuleBase)
foreach($key in $result.Keys){if($again[$key] -ne $result[$key]){throw 'Address resolution is not deterministic.'}}
'PASS: all 44 targets resolved deterministically; no game process was opened.'
