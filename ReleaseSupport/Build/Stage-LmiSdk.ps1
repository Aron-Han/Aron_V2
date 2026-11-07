[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$destination = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Aron_V2\LmiSdk'))
if (Test-Path -LiteralPath $destination) { throw "Staging directory already exists; inspect it before replacing: $destination" }
# A pinned release from ONE vendor package. kApi has its own version numbering.
$versions = [ordered]@{ 'GoSdk.dll'='6.5.2.5'; 'GoSdkNet.dll'='6.5.2.5'; 'kApi.dll'='10.3.3.22'; 'kApiNet.dll'='10.3.3.22' }
foreach ($name in $versions.Keys) {
    $file = Get-Item -LiteralPath (Join-Path $source $name)
    if ($file.VersionInfo.FileVersion -ne $versions[$name]) { throw "Unexpected version of $name : $($file.VersionInfo.FileVersion)" }
}
# Dependency closure of the four GoSDK libraries, verified with dumpbin /dependents.
# Do not copy Emulator, Accelerator, GoHMI, Cognex or unrelated third-party DLLs.
# Target Windows 10/11 supplies API-set contracts and .NET 4.7.2+.
# This vendor win64 folder contains x86 api-ms-win-* forwarders; never ship those.
$names = @($versions.Keys) + @('vcruntime140.dll','ucrtbase.dll')
$entries = foreach ($name in $names) {
    $file = Get-Item -LiteralPath (Join-Path $source $name)
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $pe = [BitConverter]::ToInt32($bytes, 0x3c)
    if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw "Not AMD64: $name" }
    [ordered]@{ name=$name; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash; fileVersion=$file.VersionInfo.FileVersion; length=$file.Length }
}
[void](New-Item -ItemType Directory -Path $destination)
foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $destination $name) }
$manifest = [ordered]@{
    schemaVersion=1; package='LMI GO Utilities 6.5.2.5'; architecture='AMD64'; goSdkVersion='6.5.2.5'; kApiVersion='10.3.3.22'
    sourceDirectory=$source; stagedAtUtc=[DateTime]::UtcNow.ToString('o'); files=@($entries)
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'runtime-manifest.json') -Encoding UTF8
Write-Output "Staged $($entries.Count) x64 runtime DLLs from one package into $destination"
