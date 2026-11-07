[CmdletBinding()]
param(
    [switch]$Interactive,
    [switch]$List,
    [string]$VisionProPath,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$ProjectDataPath,
    [string]$OutputRoot,
    [string]$MSBuildPath
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'VisionPro.Build.psm1') -Force
$repository = Split-Path $PSScriptRoot
$project = Join-Path $repository 'Aron_V2\Aron_V2.csproj'
$buildDirectory = $null
try {
    if ($List) {
        ConvertTo-Json -InputObject @(Get-VisionProInstallations | Sort-Object ReferenceDirectory) -Depth 4
        exit 0
    }
    if (!$VisionProPath) {
        $installed = @(Get-VisionProInstallations | Sort-Object ReferenceDirectory)
        Write-Host 'Select VisionPro SDK (.NET Framework / x64):'
        for ($i=0; $i -lt $installed.Count; $i++) {
            Write-Host ('[{0}] {1}' -f ($i+1), $installed[$i].DisplayName)
            Write-Host ('    ' + $installed[$i].SdkRoot)
        }
        Write-Host '[M] Enter a different installed SDK directory'
        Write-Host '[Q] Cancel'
        $selection = Read-Host 'Selection'
        if ($selection -eq 'Q') { exit 0 }
        if ($selection -eq 'M') { $VisionProPath = (Read-Host 'SDK root or ReferencedAssemblies directory').Trim('"') }
        else {
            $number = 0
            if (![int]::TryParse($selection, [ref]$number) -or $number -lt 1 -or $number -gt $installed.Count) { throw 'Invalid SDK selection.' }
            $VisionProPath = $installed[$number-1].SdkRoot
        }
    }
    $sdk = Get-VisionProSdkInfo -Path $VisionProPath
    $plan = @(Get-VisionProReferencePlan -Sdk $sdk -Project $project)
    $msbuild = Find-AronMSBuild -Override $MSBuildPath
    Write-Host ('Selected: ' + $sdk.DisplayName)
    Write-Host ('SDK: ' + $sdk.ReferenceDirectory)
    foreach ($reference in $plan | Where-Object Origin -eq 'InstalledGACExactVersion') {
        Write-Host ('Exact installed runtime reference: ' + $reference.Name + ' -> ' + $reference.Path) -ForegroundColor Yellow
    }
    if ($Interactive -and !$ProjectDataPath) {
        $defaultData = Join-Path $repository 'Aron_V2\bin\x64\Debug\Project'
        if (Test-Path -LiteralPath $defaultData) {
            Write-Host 'Optional: copy existing Project/Config and Project/VPP as a snapshot.'
            Write-Host 'No databases, statistics, accounts or logs are copied. VPP files are NOT converted.'
            if ((Read-Host 'Copy the current Debug project data? [y/N]') -eq 'Y') { $ProjectDataPath = $defaultData }
        }
    }
    if ($ProjectDataPath) {
        $ProjectDataPath = [IO.Path]::GetFullPath($ProjectDataPath)
        if (!(Test-Path -LiteralPath (Join-Path $ProjectDataPath 'Config\Jobs\Parameter.xml') -PathType Leaf) -and
            !(Test-Path -LiteralPath (Join-Path $ProjectDataPath 'Config\Parameter.xml') -PathType Leaf)) {
            throw 'ProjectDataPath must be a Project directory containing Config\Jobs\Parameter.xml (or a legacy Config\Parameter.xml).'
        }
    }
    if (!$OutputRoot) { $OutputRoot = Join-Path $repository 'artifacts\VisionPro' }
    $OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    # Always create a new build. Never clean or overwrite an existing release.
    $label = ($sdk.Release + '-asm' + $sdk.AssemblyVersion) -replace '[^A-Za-z0-9_.-]','_'
    $id = (Get-Date -Format 'yyyyMMdd_HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $buildDirectory = Join-Path (Join-Path (Join-Path $OutputRoot $label) $Configuration) $id
    $appDirectory = Join-Path $buildDirectory 'app'
    $workDirectory = Join-Path $buildDirectory 'work'
    [void](New-Item -ItemType Directory -Path $appDirectory, $workDirectory)
    $referencesFile = Join-Path $workDirectory 'VisionPro.References.props'
    $resolvedFile = Join-Path $workDirectory 'resolved-references.txt'
    $infoFile = Join-Path $workDirectory 'BuildInfo.cs'
    $utf8 = New-Object Text.UTF8Encoding($false)
    $buildDate = Get-Date -Format 'yyyyMMdd_HHmmss'
    [IO.File]::WriteAllText($infoFile, 'namespace Aron_V2 { public static class BuildInfo { public const string BuildDate = "' + $buildDate + '"; } }', $utf8)
    $xml = New-Object Xml.XmlDocument
    $ns = 'http://schemas.microsoft.com/developer/msbuild/2003'
    $xmlRoot = $xml.CreateElement('Project', $ns); [void]$xml.AppendChild($xmlRoot)
    $group = $xml.CreateElement('ItemGroup', $ns); [void]$xmlRoot.AppendChild($group)
    foreach ($reference in $plan) {
        $item = $xml.CreateElement('Reference', $ns); $item.SetAttribute('Include', $reference.Identity)
        foreach ($entry in @(@('HintPath',$reference.Path), @('SpecificVersion','True'), @('EmbedInteropTypes','False'), @('Private', $(if ($reference.Name -eq 'Cognex.VisionPro.Interop.Core') {'False'} else {'True'})))) {
            $node = $xml.CreateElement($entry[0], $ns); $node.InnerText = $entry[1]; [void]$item.AppendChild($node)
        }
        [void]$group.AppendChild($item)
    }
    $xml.Save($referencesFile)
    $properties = [ordered]@{
        Configuration=$Configuration; Platform='x64'; PlatformTarget='x64'; Prefer32Bit='false'
        VisionProTargetVersion=$sdk.AssemblyVersion; VisionProSdkRoot=$sdk.SdkRoot
        DesignTimeBuild='false'; PreBuildEvent=''; OutputPath=($appDirectory + '/')
        BaseIntermediateOutputPath=($workDirectory + '/obj/')
        IntermediateOutputPath=($workDirectory + '/obj/' + $Configuration + '/')
        CustomAfterMicrosoftCommonTargets=(Join-Path $PSScriptRoot 'VisionPro.Build.targets')
        VisionProReferencesFile=$referencesFile; VisionProBuildInfoFile=$infoFile
        VisionProResolvedReferencesFile=$resolvedFile
    }
    $arguments = @($project, '/t:Build', '/m:1', '/nr:false', '/v:minimal')
    foreach ($key in $properties.Keys) { $arguments += ('/p:{0}={1}' -f $key, $properties[$key]) }
    Write-Host ('Build directory: ' + $buildDirectory)
    & $msbuild @arguments 2>&1 | Tee-Object -FilePath (Join-Path $buildDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed (exit $LASTEXITCODE). See build.log. No successful runtime manifest was created." }
    if (!(Test-Path -LiteralPath $resolvedFile)) { throw 'Reference isolation target did not run; build cannot be accepted.' }
    $resolved = @{}
    foreach ($line in Get-Content -LiteralPath $resolvedFile) {
        $parts = $line.Split('|',2)
        if ($parts.Count -eq 2) { $resolved[[IO.Path]::GetFileNameWithoutExtension($parts[1])] = $parts[1] }
    }
    foreach ($reference in $plan) {
        if (!$resolved.ContainsKey($reference.Name) -or ![string]::Equals([IO.Path]::GetFullPath($resolved[$reference.Name]), [IO.Path]::GetFullPath($reference.Path), [StringComparison]::OrdinalIgnoreCase)) {
            throw "SDK isolation failed for $($reference.Name). Expected $($reference.Path), resolved $($resolved[$reference.Name]). No successful runtime manifest was created."
        }
    }
    $exe = Join-Path $appDirectory 'Visual Inspection.exe'
    if (!(Test-Path -LiteralPath $exe)) { throw 'Expected application executable was not generated.' }
    $copiedData = @()
    if ($ProjectDataPath) {
        $destination = Join-Path $appDirectory 'Project'
        [void](New-Item -ItemType Directory -Path $destination)
        foreach ($folder in @('Config','VPP')) {
            $source = Join-Path $ProjectDataPath $folder
            if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $destination -Recurse; $copiedData += $folder }
        }
    }
    $manifest = [ordered]@{
        schemaVersion = 1; kind = 'AronVisionProRuntime'; status = 'compiled'; runtimeValidation = 'not-performed'
        builtAtUtc = [DateTime]::UtcNow.ToString('o'); buildId = $id; configuration = $Configuration
        architecture = 'x64'; targetFramework = 'net472'; application = 'app/Visual Inspection.exe'
        applicationSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
        sdk = $sdk; projectDataIncluded = $copiedData; projectDataSource = $ProjectDataPath
        requiresInstalledVisionPro = $true; requiresLicenseAndHardwareValidation = $true
        references = @($plan | ForEach-Object { [ordered]@{ name=$_.Name; identity=$_.Identity; source=$_.Path; origin=$_.Origin; sha256=(Get-FileHash -LiteralPath $_.Path -Algorithm SHA256).Hash } })
    }
    [IO.File]::WriteAllText((Join-Path $buildDirectory 'runtime.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
    Write-Host ''
    Write-Host 'BUILD SUCCEEDED. Runtime/VPP compatibility has NOT been tested.' -ForegroundColor Green
    Write-Host ('Application: ' + $exe)
    Write-Host ('Launcher metadata: ' + (Join-Path $buildDirectory 'runtime.json'))
    if ($copiedData.Count -eq 0) { Write-Warning 'No project data included. Configure Project/Config and Project/VPP before using this application.' }
    exit 0
} catch {
    if ($buildDirectory -and (Test-Path -LiteralPath $buildDirectory)) {
        [IO.File]::WriteAllText((Join-Path $buildDirectory 'build-failed.txt'), $_.Exception.ToString())
    }
    Write-Host ('BUILD FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
