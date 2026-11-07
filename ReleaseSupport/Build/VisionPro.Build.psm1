Set-StrictMode -Version 2.0

function Get-VisionProSdkInfo {
    param([Parameter(Mandatory=$true)][string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if ((Split-Path $full -Leaf) -eq 'ReferencedAssemblies') { $references = $full; $root = Split-Path $full }
    else { $root = $full; $references = Join-Path $full 'ReferencedAssemblies' }
    $core = Join-Path $references 'Cognex.VisionPro.Core.dll'
    if (!(Test-Path -LiteralPath $core -PathType Leaf)) { throw "Not a VisionPro .NET SDK directory: $full (missing $core)" }
    $identity = [Reflection.AssemblyName]::GetAssemblyName($core)
    if ($identity.Name -ne 'Cognex.VisionPro.Core') { throw "Invalid SDK core assembly: $core" }
    if ($identity.ProcessorArchitecture.ToString() -eq 'X86') { throw 'This project requires the x64 VisionPro SDK, not x86.' }
    $marker = @(Get-ChildItem -LiteralPath $root -Filter '*installed_productcode.txt' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch 'Documents' } | Sort-Object Name | Select-Object -First 1)
    $release = Split-Path $root -Leaf
    if ($marker.Count -gt 0) { $release = $marker[0].Name -replace '_installed_productcode.txt$','' }
    [pscustomobject]@{
        DisplayName = "$release (assembly $($identity.Version), x64)"
        Release = $release
        AssemblyVersion = $identity.Version.ToString()
        SdkRoot = $root
        ReferenceDirectory = $references
        CoreIdentity = $identity.FullName
    }
}

function Get-VisionProInstallations {
    # Read metadata only. Never instantiate Cognex controls, COM objects or VPP tools.
    $roots = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('VPRO_ROOT','VPRO_ROOT64')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { [void]$roots.Add($value) }
    }
    foreach ($programFiles in @($env:ProgramW6432, $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Select-Object -Unique) {
        if (!$programFiles) { continue }
        $cognex = Join-Path $programFiles 'Cognex'
        if (!(Test-Path -LiteralPath $cognex)) { continue }
        foreach ($folder in Get-ChildItem -LiteralPath $cognex -Directory -Filter 'VisionPro*' -ErrorAction SilentlyContinue) {
            [void]$roots.Add($folder.FullName)
            foreach ($child in Get-ChildItem -LiteralPath $folder.FullName -Directory -ErrorAction SilentlyContinue) {
                if (Test-Path -LiteralPath (Join-Path $child.FullName 'ReferencedAssemblies')) { [void]$roots.Add($child.FullName) }
            }
        }
    }
    foreach ($registryPath in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
        foreach ($entry in Get-ItemProperty $registryPath -ErrorAction SilentlyContinue) {
            if ($entry.PSObject.Properties['DisplayName'] -and $entry.DisplayName -match 'VisionPro' -and
                $entry.PSObject.Properties['InstallLocation'] -and $entry.InstallLocation) { [void]$roots.Add($entry.InstallLocation) }
        }
    }
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $roots) {
        try {
            $sdk = Get-VisionProSdkInfo -Path $root
            if ($seen.Add($sdk.ReferenceDirectory)) { $sdk }
        } catch { Write-Verbose $_.Exception.Message }
    }
}

function Get-VisionProReferencePlan {
    param([Parameter(Mandatory=$true)]$Sdk, [Parameter(Mandatory=$true)][string]$Project)
    [xml]$xml = Get-Content -LiteralPath $Project -Raw
    $manager = New-Object Xml.XmlNamespaceManager($xml.NameTable)
    $manager.AddNamespace('m','http://schemas.microsoft.com/developer/msbuild/2003')
    $names = @($xml.SelectNodes('//m:Reference', $manager) | ForEach-Object { $_.GetAttribute('Include').Split(',')[0] } |
        Where-Object { $_ -like 'Cognex.*' } | Sort-Object -Unique)
    if ($names.Count -eq 0) { throw 'No Cognex references found in the source project.' }
    foreach ($name in $names) {
        $file = Join-Path $Sdk.ReferenceDirectory ($name + '.dll')
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Selected SDK is missing $name.dll. This source/API family may require adaptation; no fallback to another SDK is allowed."
        }
        $identity = [Reflection.AssemblyName]::GetAssemblyName($file)
        if ($identity.Name -ne $name -or $identity.ProcessorArchitecture.ToString() -eq 'X86') { throw "Wrong assembly identity or architecture: $file" }
        $origin = 'SdkDirectory'
        # The installed Core identifies this runtime family. Some installations
        # contain different-version reference facades alongside that Core. Never
        # compile both API identities: resolve the SAME target version explicitly
        # from the installed GAC, or stop. No search for a newer/older substitute.
        if ($identity.Version.ToString() -ne $Sdk.AssemblyVersion) {
            $token = -join ($identity.GetPublicKeyToken() | ForEach-Object { $_.ToString('x2') })
            $gacMatch = $null
            foreach ($store in @('GAC_64','GAC_MSIL')) {
                $candidate = Join-Path $env:windir ('Microsoft.NET\assembly\' + $store + '\' + $name + '\v4.0_' + $Sdk.AssemblyVersion + '__' + $token + '\' + $name + '.dll')
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $gacMatch = $candidate; break }
            }
            if (!$gacMatch) { throw "SDK reference $name is $($identity.Version), but selected runtime is $($Sdk.AssemblyVersion). No exact-version installed assembly exists; repair/use a coherent SDK. No cross-version fallback is allowed." }
            $file = $gacMatch
            $identity = [Reflection.AssemblyName]::GetAssemblyName($file)
            if ($identity.Name -ne $name -or $identity.Version.ToString() -ne $Sdk.AssemblyVersion) { throw "Installed runtime identity mismatch: $file" }
            $origin = 'InstalledGACExactVersion'
        }
        [pscustomobject]@{ Name=$name; Identity=$identity.FullName; Version=$identity.Version.ToString(); Path=$file; Origin=$origin }
    }
}

function Find-AronMSBuild {
    param([string]$Override)
    if ($Override) {
        if (!(Test-Path -LiteralPath $Override -PathType Leaf)) { throw "MSBuild not found: $Override" }
        return [IO.Path]::GetFullPath($Override)
    }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $found = @(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
        if ($found.Count -gt 0) { return $found[0] }
    }
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'MSBuild was not found. Install Visual Studio Build Tools with .NET desktop build tools and the .NET Framework 4.7.2 targeting pack. No Visual Studio IDE is required.'
}

Export-ModuleMember -Function Get-VisionProSdkInfo,Get-VisionProInstallations,Get-VisionProReferencePlan,Find-AronMSBuild
