param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$root = Split-Path $PSScriptRoot
Import-Module (Join-Path $root 'Build\VisionPro.Build.psm1') -Force
function Assert($ok, $message) { if (!$ok) { throw $message }; Write-Host ('PASS ' + $message) }
foreach ($file in Get-ChildItem (Join-Path $root 'Build') -File | Where-Object Extension -in @('.ps1','.psm1')) {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
    Assert ($errors.Count -eq 0) ('PowerShell syntax: ' + $file.Name)
}
$installed = @(Get-VisionProInstallations)
Assert ($installed.Count -gt 0) 'At least one installed SDK discovered without activating Cognex'
$sdk = $installed[0]
$equivalent = Get-VisionProSdkInfo -Path $sdk.ReferenceDirectory
Assert ($sdk.SdkRoot -eq $equivalent.SdkRoot) 'SDK root and ReferencedAssemblies input select the same installation'
$invalidRejected = $false
try { Get-VisionProSdkInfo -Path (Join-Path $root 'Tests') | Out-Null } catch { $invalidRejected = $true }
Assert $invalidRejected 'Non-SDK path is rejected'
$plan = @(Get-VisionProReferencePlan -Sdk $sdk -Project (Join-Path $root 'Aron_V2\Aron_V2.csproj'))
Assert ($plan.Count -ge 20) 'All source Cognex references enumerated'
Assert (@($plan | Where-Object Version -ne $sdk.AssemblyVersion).Count -eq 0) 'Reference plan never mixes another assembly version'
$fakeSdk = [pscustomobject]@{ ReferenceDirectory=$sdk.ReferenceDirectory; AssemblyVersion='0.0.0.0' }
$crossVersionRejected = $false
try { Get-VisionProReferencePlan -Sdk $fakeSdk -Project (Join-Path $root 'Aron_V2\Aron_V2.csproj') | Out-Null } catch { $crossVersionRejected = $true }
Assert $crossVersionRejected 'Missing target identity cannot silently fall back to another installed version'
Assert ([bool](Find-AronMSBuild)) 'MSBuild discovered without opening the IDE'
$manifestFile = Join-Path $BuildDirectory 'runtime.json'
Assert (Test-Path -LiteralPath $manifestFile) 'Successful build publishes launcher manifest'
$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
Assert ($manifest.status -eq 'compiled' -and $manifest.runtimeValidation -eq 'not-performed') 'Compile success does not claim runtime/VPP validation'
$exe = Join-Path $BuildDirectory $manifest.application
Assert (Test-Path -LiteralPath $exe) 'Manifest entry point exists'
Assert ((Get-FileHash -LiteralPath $exe).Hash -eq $manifest.applicationSha256) 'Executable matches recorded SHA256'
Assert ($manifest.architecture -eq 'x64' -and $manifest.targetFramework -eq 'net472') 'Package records required platform/framework'
Assert ($manifest.references.Count -eq $plan.Count) 'Manifest includes every direct SDK reference'
$resolvedLines = @(Get-Content -LiteralPath (Join-Path $BuildDirectory 'work\resolved-references.txt'))
foreach ($reference in $manifest.references) {
    $resolved = @($resolvedLines | Where-Object { $_.EndsWith('|' + $reference.source, [StringComparison]::OrdinalIgnoreCase) })
    Assert ($resolved.Count -eq 1) ('Actual compiler reference: ' + $reference.name)
}
Assert (!(Test-Path -LiteralPath (Join-Path $BuildDirectory 'build-failed.txt'))) 'Successful package has no failure marker'
Assert (Test-Path -LiteralPath (Join-Path $BuildDirectory 'app\Visual Inspection.exe.config')) 'Application config is included'
if ($manifest.projectDataIncluded.Count -gt 0) {
    $unchanged = $true
    foreach ($folder in $manifest.projectDataIncluded) {
        $sourceFolder = Join-Path $manifest.projectDataSource $folder
        foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceFolder -Recurse -File) {
            $relative = $sourceFile.FullName.Substring($manifest.projectDataSource.Length).TrimStart('\')
            $copy = Join-Path (Join-Path $BuildDirectory 'app\Project') $relative
            if (!(Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $copy).Hash -ne (Get-FileHash -LiteralPath $sourceFile.FullName).Hash) { $unchanged = $false }
        }
    }
    Assert $unchanged 'Copied Config/VPP snapshots retain byte-identical content'
    Assert (!(Test-Path -LiteralPath (Join-Path $BuildDirectory 'app\users.xml'))) 'Root user credentials are not copied'
    Assert (!(Test-Path -LiteralPath (Join-Path $BuildDirectory 'app\Project\Database'))) 'Production database is not copied'
} else {
    Assert (!(Test-Path -LiteralPath (Join-Path $BuildDirectory 'app\Project'))) 'Program-only package does not silently include site project data'
}
Write-Host 'All build checks passed. No application/SDK UI was started.'
exit 0
