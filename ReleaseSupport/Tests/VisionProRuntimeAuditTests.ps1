param([Parameter(Mandatory=$true)][string]$Application)
$ErrorActionPreference = 'Stop'
$Application = (Resolve-Path -LiteralPath $Application).Path
$testRoot = Join-Path $PSScriptRoot ('VisionProAuditValidation\' + [Guid]::NewGuid().ToString('N'))
function Check-Exit([string]$exe, [int]$expected, [string]$name) {
    $process = Start-Process -FilePath $exe -ArgumentList '--visionpro-check' -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne $expected) { throw "$name : expected $expected, got $($process.ExitCode)" }
    Write-Host "PASS $name"
}
Check-Exit $Application 0 'Exact-version managed runtime closure loads without starting UI/devices'
foreach ($scenario in @('MissingManifest','MixedDll')) {
    $folder = Join-Path $testRoot $scenario
    [void](New-Item -ItemType Directory -Path $folder -Force)
    $exe = Join-Path $folder 'Visual Inspection.exe'
    Copy-Item -LiteralPath $Application -Destination $exe
    Copy-Item -LiteralPath ($Application + '.config') -Destination ($exe + '.config')
    if ($scenario -eq 'MixedDll') {
        Copy-Item -LiteralPath ($Application + '.visionpro-references.txt') -Destination ($exe + '.visionpro-references.txt')
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
        & $compiler /nologo /target:library ('/out:' + (Join-Path $folder 'Cognex.VisionPro.dll')) (Join-Path $PSScriptRoot 'Fixtures\ForeignVisionProAssembly.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Could not compile foreign-DLL fixture.' }
    }
    Check-Exit $exe 2 "$scenario is rejected before application/device startup"
    if(Test-Path -LiteralPath (Join-Path $folder 'Logs\VisionPro')) { throw 'Legacy VisionPro log directory was recreated.' }
    $logs = @(Get-ChildItem -LiteralPath (Join-Path $folder 'Logs\Diagnostics') -Filter '*.jsonl')
    if ($logs.Count -eq 0 -or !(Select-String -LiteralPath $logs[0].FullName -Pattern 'ERROR' -Quiet)) { throw 'Missing actionable diagnostic log.' }
    Write-Host "PASS $scenario produces an error log"
}
Write-Host "Isolated diagnostic fixtures (no site data): $testRoot"
