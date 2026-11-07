param([string]$BuildOutput, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = Split-Path $repo -Parent
function GitRun([string[]]$Arguments) {
    $result = & git -C $repo @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git failed: $($Arguments -join ' ')" }
    return $result
}
if ((GitRun @('remote','get-url','origin')) -ne 'https://github.com/Aron-Han/Aron_V2.git') { throw 'Unexpected GitHub remote.' }
if (!(Test-Path -LiteralPath $BuildOutput -PathType Container)) { throw 'Specify the verified build output directory.' }
$buildOutput = [IO.Path]::GetFullPath($BuildOutput)
$info = [IO.File]::ReadAllText((Join-Path $repo 'BuildInfo.cs'))
$version = [regex]::Match($info, 'SoftwareVersion = "(Ver[0-9.]+)"').Groups[1].Value
$date = [regex]::Match($info, 'BuildDate = "([0-9_]+)"').Groups[1].Value
if (!$version -or !$date) { throw 'Missing version/build date.' }
$exe = Join-Path $buildOutput 'Visual Inspection.exe'
$identity = [Reflection.Assembly]::LoadFile($exe).GetType('Aron_V2.BuildInfo')
if ($identity.GetField('SoftwareVersion').GetRawConstantValue() -ne $version -or $identity.GetField('BuildDate').GetRawConstantValue() -ne $date) { throw 'EXE does not match source BuildInfo.' }
$tag = "$version-$date"
# Capture required sibling sources without changing the existing VS layout.
$support = Join-Path $repo 'ReleaseSupport'
if (Test-Path -LiteralPath $support) {
    $resolved = [IO.Path]::GetFullPath($support)
    if ($resolved -ne (Join-Path $repo 'ReleaseSupport')) { throw 'Invalid support path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $support -Force | Out-Null
foreach ($folder in @('Build','PluginContracts','Plugins','Tests')) {
    $source = Join-Path $workspace $folder
    Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|\.git|UIValidation)[\\/]' -and
        $_.Extension -in @('.cs','.csproj','.resx','.targets','.ps1','.psm1','.md') -or
        ($_.Name -eq 'plugin.xml' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]')
    } | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length).TrimStart('\','/')
        $target = Join-Path (Join-Path $support $folder) $relative
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target
    }
}
foreach ($name in @('Aron_V2.sln','Build-VisionPro.cmd')) { Copy-Item -LiteralPath (Join-Path $workspace $name) -Destination $support }
$files = @(Get-ChildItem -LiteralPath $buildOutput -File -Recurse | Where-Object {
    $_.Extension -in @('.exe','.dll','.pdb','.config') -and $_.FullName -notmatch '[\\/](Project|Logs?|Config|Recipes)[\\/]'
} | ForEach-Object { [ordered]@{ path=$_.FullName.Substring($buildOutput.Length).TrimStart('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length } })
$manifest = [ordered]@{ version=$version; buildDate=$date; tag=$tag; baseCommit=(GitRun @('rev-parse','HEAD')); visionPro='9.6 SR2'; framework='net472'; architecture='x64'; artifacts=$files }
$records = Join-Path $repo 'ReleaseRecords'
New-Item -ItemType Directory -Path $records -Force | Out-Null
$record = Join-Path $records "$tag.json"
if (GitRun @('ls-files', '--', "ReleaseRecords/$tag.json")) { throw 'Release record already committed. Never overwrite a published build.' }
[IO.File]::WriteAllText($record, ($manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
if ($PrepareOnly) { Write-Host "Prepared $tag. Review git diff and ReleaseSupport before publishing."; return }
GitRun @('fetch','origin') | Out-Host
if (@(GitRun @('tag','--list',$tag)).Count) { throw 'Tag already exists.' }
$branch = "release/$tag"
GitRun @('switch','-c',$branch) | Out-Host
# Existing tracked source plus explicitly reviewed new source paths only.
GitRun @('add','-u') | Out-Host
GitRun @('add','--','AGENTS.md','ReleaseTools','ReleaseRecords','ReleaseSupport','*.cs','*.csproj','*.resx','*.ps1','*.targets','*.ico') | Out-Host
$staged = @(GitRun @('diff','--cached','--name-only'))
if ($staged | Where-Object { $_ -match '(?i)\.vpp$|(^|/)(Project|Config|Recipes|Logs?)/' }) { throw 'Field data found in staged release. Review and unstage it before publishing.' }
GitRun @('commit','-m',"Release $tag - source and build manifest") | Out-Host
GitRun @('tag','-a',$tag,'-m',"$version build $date; VisionPro 9.6 SR2; manifest ReleaseRecords/$tag.json") | Out-Host
GitRun @('push','--atomic','origin',"HEAD:refs/heads/$branch","refs/tags/$tag") | Out-Host
$sha = GitRun @('rev-parse','HEAD')
$remote = GitRun @('ls-remote','origin',"refs/tags/$tag^{}")
if (!$remote -or !$remote.StartsWith($sha)) { throw 'Remote tag verification failed. Do not report upload success.' }
Write-Host "VERIFIED: https://github.com/Aron-Han/Aron_V2/tree/$tag"
