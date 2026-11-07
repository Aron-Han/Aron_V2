param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
$bin = [IO.Path]::GetFullPath((Join-Path $workspace 'Aron_V2\bin')).TrimEnd('\')
$debug = [IO.Path]::GetFullPath((Join-Path $bin 'x64\Debug')).TrimEnd('\')
function Assert-InBin([string]$path) {
    $absolute = [IO.Path]::GetFullPath($path)
    if (!$absolute.StartsWith($bin + '\', [StringComparison]::OrdinalIgnoreCase) -or $absolute -eq $bin) { throw "Outside intended bin directory: $absolute" }
    return $absolute
}
function Assert-NoReparsePoints([string]$path) {
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing reparse point: $path" }
    if (@(Get-ChildItem -LiteralPath $path -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) { throw "Refusing tree containing reparse points: $path" }
}
$running = @(Get-Process | Where-Object { try { $_.Path -and $_.Path.StartsWith($bin + '\', [StringComparison]::OrdinalIgnoreCase) } catch { $false } })
if ($running.Count) { throw 'An application in this bin directory is running. Close it before reorganizing deployment data.' }

# Capture hashes only, not backup copies. Preserve the active legacy paths,
# all VPPs, both database stores, counters, logs and the pinned GoSDK runtime.
$moves = @{
    'Project\Config\ConfigSaveImage.xml'='Project\Config\General\ConfigSaveImage.xml'
    'Project\Config\DisplayLayout.xml'='Project\Config\General\DisplayLayout.xml'
    'Project\Config\LogView.xml'='Project\Config\General\LogView.xml'
    'Project\Config\Parameter.xml'='Project\Config\Jobs\Parameter.xml'
    'Project\Config\VPPOutput.xml'='Project\Config\Communication\VPPOutput.xml'
    'Project\Config\Communication.xml'='Project\Config\Communication\Communication.xml'
    'Project\Config\ProfinetConfig.xml'='Project\Config\Communication\Profinet\ProfinetConfig.xml'
    'Project\PlcInput.xml'='Project\Config\Communication\Profinet\PlcInput.xml'
    'Project\Config\TcpIpConfig.xml'='Project\Config\Communication\TcpIp\TcpIpConfig.xml'
    'Config\S7Config.xml'='Project\Config\Communication\S7\S7Config.xml'
    'Config\S7InputTableConfig.xml'='Project\Config\Communication\S7\S7InputTableConfig.xml'
    'Config\S7OutputTableConfig.xml'='Project\Config\Communication\S7\S7OutputTableConfig.xml'
    'users.xml'='Project\Config\Security\users.xml'
    'Project\Config\UserAccess.xml'='Project\Config\Security\UserAccess.xml'
    'Project\Config\Permissions.xml'='Project\Config\Security\Permissions.xml'
    'mydata.db'='Data\Database\LegacyOrders.db'
}
$checks = [Collections.Generic.List[object]]::new()
foreach ($entry in $moves.GetEnumerator()) {
    $source = Join-Path $debug $entry.Key
    if (Test-Path -LiteralPath $source -PathType Leaf) { $checks.Add([pscustomobject]@{Target=$entry.Value; Hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash}) }
}
foreach ($pair in @(@('Project\VPP','Project\VPP'),@('Project\Database','Data\Database'),@('Project\Stats','Data\Stats'),@('Log\ApplicationLogs','Logs\Application'),@('LmiSdk','LmiSdk'))) {
    $source = Join-Path $debug $pair[0]
    if (!(Test-Path -LiteralPath $source)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $checks.Add([pscustomobject]@{Target=(Join-Path $pair[1] $file.FullName.Substring($source.Length+1)); Hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash})
    }
}
$process = Start-Process -FilePath (Join-Path $debug 'Visual Inspection.exe') -ArgumentList '--organize-project' -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "File-only migration failed with exit code $($process.ExitCode); cleanup stopped." }
foreach ($check in $checks) {
    $target = Join-Path $debug $check.Target
    if (!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $check.Hash) { throw "Preserved file did not verify: $target" }
}
Write-Output "Verified $($checks.Count) preserved files after migration."

$removed = [Collections.Generic.List[object]]::new()
foreach ($name in @('BlankVppValidation','CommunicationValidation','Debug','EditorValidation','LanguageLayoutValidation','LmiValidationRelease','LogToolbarValidation','RecoveryValidation','Release','SettingsCenterValidation','VisionProCommValidation','VisionProReleaseValidation','x64\Release')) {
    $target = Assert-InBin (Join-Path $bin $name)
    if (!(Test-Path -LiteralPath $target)) { continue }
    Assert-NoReparsePoints $target
    $size = (Get-ChildItem -LiteralPath $target -File -Recurse | Measure-Object Length -Sum).Sum
    $removed.Add([pscustomobject]@{Path=$target; Bytes=[long]$size})
    Write-Output "Deleting obsolete build output: $target"
    Remove-Item -LiteralPath $target -Recurse -Force
}
$x86 = Assert-InBin (Join-Path $debug 'x86')
if (Test-Path -LiteralPath $x86) {
    Assert-NoReparsePoints $x86
    if (@(Get-ChildItem -LiteralPath $x86 -File -Recurse | Where-Object {$_.Name -ne 'SQLite.Interop.dll'}).Count) { throw 'Unexpected files in x86; do not delete them without inspection.' }
    $size = (Get-ChildItem -LiteralPath $x86 -File -Recurse | Measure-Object Length -Sum).Sum
    $removed.Add([pscustomobject]@{Path=$x86; Bytes=[long]$size}); Remove-Item -LiteralPath $x86 -Recurse -Force
}
# These S7 point-table backups and the inactive root PlcInput copy are not
# consumed by the program. The active paths were hash-verified above.
$oldConfig = Assert-InBin (Join-Path $debug 'Config')
if (Test-Path -LiteralPath $oldConfig) {
    foreach ($file in Get-ChildItem -LiteralPath $oldConfig -File -Filter 'S7*TableConfig.xml.*.bak') { Remove-Item -LiteralPath $file.FullName -Force }
}
$inactiveInput = Assert-InBin (Join-Path $debug 'PlcInput.xml')
if ((Test-Path -LiteralPath $inactiveInput) -and (Test-Path -LiteralPath (Join-Path $debug 'Project\Config\Communication\Profinet\PlcInput.xml'))) { Remove-Item -LiteralPath $inactiveInput -Force }
foreach ($relative in @('Config','Recipes','Log')) {
    $target = Assert-InBin (Join-Path $debug $relative)
    if ((Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) { Remove-Item -LiteralPath $target -Force }
}
$total = [long](($removed | Measure-Object Bytes -Sum).Sum)
$report = [pscustomobject]@{UpdatedUtc=[DateTime]::UtcNow.ToString('o'); Deployment=$debug; VerifiedFiles=$checks.Count; DeletedBytes=$total; DeletedDirectories=$removed; PreservedHashes=$checks}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'DeploymentCleanup.latest.json') -Encoding UTF8
Write-Output "Deleted obsolete outputs without backup: $([math]::Round($total/1GB,2)) GiB."
