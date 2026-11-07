param([Parameter(Mandatory=$true)][string]$Tag, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
if ($Tag -notmatch '^Ver\d+\.\d+\.\d+-\d{8}_\d{6}$') { throw 'Use an exact version-buildDate tag.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dest = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $dest) { throw 'Choose a new, empty destination.' }
& git -C $repo fetch origin --tags
if ($LASTEXITCODE) { throw 'Fetch failed.' }
$branch = 'hotfix/' + $Tag + '-' + (Get-Date -Format yyyyMMdd_HHmmss)
& git -C $repo worktree add -b $branch (Join-Path $dest 'Aron_V2') "refs/tags/$Tag"
if ($LASTEXITCODE) { throw 'Historical checkout failed.' }
$support = Join-Path $dest 'Aron_V2\ReleaseSupport'
Get-ChildItem -LiteralPath $support | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse }
Write-Host "Historical source: $dest ; branch: $branch"
Write-Host 'Restore NuGet packages; supply the recorded licensed VisionPro/GoSDK/Hsl dependencies. Do not copy current source into this checkout.'
