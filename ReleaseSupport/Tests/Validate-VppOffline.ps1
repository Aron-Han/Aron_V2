param([Parameter(Mandatory=$true)][string]$Application, [Parameter(Mandatory=$true)][string]$VppRoot, [int]$StartAt = 1)
$ErrorActionPreference = 'Stop'
$application = (Resolve-Path -LiteralPath $Application).Path
$root = (Resolve-Path -LiteralPath $VppRoot).Path
$probe = Join-Path $PSScriptRoot 'VppOfflineProbe.exe'
$reportDirectory = Join-Path $PSScriptRoot ('VppValidation\' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $reportDirectory)
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.vpp' | Sort-Object FullName)
$index = 0
foreach ($file in $files) {
    $index++
    if ($index -lt $StartAt) { continue }
    $before = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $outLog = Join-Path $reportDirectory ($index.ToString('00') + '.out.log')
    $errLog = Join-Path $reportDirectory ($index.ToString('00') + '.err.log')
    $arguments = '"' + $application + '" "' + $file.FullName + '"'
    # One child at a time; exit releases the potentially large persisted images.
    $process = Start-Process -FilePath $probe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (!$process.WaitForExit(250)) {
        $process.Refresh()
        if ($watch.Elapsed.TotalSeconds -gt 60 -or $process.WorkingSet64 -gt 1400MB) {
            $process.Kill(); $process.WaitForExit()
            throw "Stopped ONLY owned VPP probe PID $($process.Id): elapsed=$($watch.Elapsed.TotalSeconds)s, workingMB=$([math]::Round($process.WorkingSet64/1MB)); limit exceeded for $($file.FullName). Reports: $reportDirectory"
        }
    }
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errLog; throw "VPP probe failed; see $reportDirectory" }
    if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $before) { throw "VPP content unexpectedly changed: $($file.FullName)" }
    Write-Output ("[$index/$($files.Count)] " + (Get-Content -LiteralPath $outLog | Where-Object { $_ -like 'PASS *' } | Select-Object -Last 1))
}
Write-Output "PASS VPPs $StartAt through $($files.Count) deserialized with original hashes unchanged. Reports: $reportDirectory"
