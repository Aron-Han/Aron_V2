[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Directory, [string]$ManifestPath)
$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
if (!$ManifestPath) { $ManifestPath = Join-Path $Directory 'runtime-manifest.json' }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.architecture -ne 'AMD64' -or $manifest.goSdkVersion -ne '6.5.2.5' -or $manifest.kApiVersion -ne '10.3.3.22') { throw 'Unexpected pinned GoSDK package.' }
$expected = @{}
foreach ($entry in $manifest.files) {
    if ([IO.Path]::GetFileName($entry.name) -ne $entry.name) { throw 'Invalid manifest filename.' }
    $expected[$entry.name] = $true
    $file = Get-Item -LiteralPath (Join-Path $Directory $entry.name)
    if ($file.Length -ne $entry.length -or (Get-Sha256 $file.FullName) -ne $entry.sha256) { throw "GoSDK runtime changed/mixed: $($file.FullName)" }
}
foreach ($name in @('GoSdk.dll','GoSdkNet.dll','kApi.dll','kApiNet.dll','vcruntime140.dll','ucrtbase.dll')) {
    if (!$expected.ContainsKey($name)) { throw "Required runtime absent from manifest: $name" }
}
foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.dll') {
    if ($file.DirectoryName -ne (Get-Item -LiteralPath $Directory).FullName -or !$expected.ContainsKey($file.Name)) { throw "Unexpected DLL in LmiSdk; do not mix runtime packages: $($file.FullName)" }
}
Write-Output "LmiSdk validated: GoSDK $($manifest.goSdkVersion), kApi $($manifest.kApiVersion), $($expected.Count) pinned DLLs."
