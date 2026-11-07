$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $root 'Aron_V2\LmiSdk'
$validator = Join-Path $root 'Build\Validate-LmiSdk.ps1'
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('Aron-LmiSdk-test-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $sandbox)
try {
    Copy-Item -LiteralPath $source -Destination (Join-Path $sandbox 'LmiSdk') -Recurse
    $relocated = Join-Path $sandbox 'LmiSdk'
    & $validator -Directory $relocated
    & (Join-Path $PSScriptRoot 'LmiRuntimeSmokeTests.exe') $relocated
    if ($LASTEXITCODE -ne 0) { throw 'Relocated runtime initialization failed.' }
    Copy-Item -LiteralPath (Join-Path $source 'kApi.dll') -Destination (Join-Path $relocated 'GoSdk.dll') -Force
    $rejected = $false
    try { & $validator -Directory $relocated } catch { $rejected = $true }
    if (!$rejected) { throw 'Mixed DLL was accepted.' }
    Write-Output 'PASS mixed/replaced DLL rejected by hash validation.'
    Copy-Item -LiteralPath (Join-Path $source 'GoSdk.dll') -Destination (Join-Path $relocated 'GoSdk.dll') -Force
    Copy-Item -LiteralPath (Join-Path $source 'kApi.dll') -Destination (Join-Path $relocated 'Unexpected.dll')
    $rejected = $false
    try { & $validator -Directory $relocated } catch { $rejected = $true }
    if (!$rejected) { throw 'Extra DLL was accepted.' }
    Write-Output 'PASS unexpected DLL rejected; source package unchanged.'
    & $validator -Directory $source
}
finally {
    # Only remove this test's exact, freshly-created directory, never a computed parent.
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolved = [IO.Path]::GetFullPath($sandbox)
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFileName($resolved).StartsWith('Aron-LmiSdk-test-')) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
