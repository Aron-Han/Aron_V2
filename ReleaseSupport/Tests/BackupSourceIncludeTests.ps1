$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$repo=Join-Path $workspace 'Aron_V2'
$project=Join-Path $repo 'Aron_V2.csproj'
$testProject=Join-Path $repo '.BackupSourceIncludeValidation.csproj'
$msbuild='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'
$expected=@('Diagnostics\ProjectBackupService.cs','Form\ImageRecord.Backup.cs')
function CheckIncludes([string]$path) {
    $output=& $msbuild $path -getItem:Compile /p:DesignTimeBuild=true /p:Configuration=Debug /p:Platform=AnyCPU /nr:false
    if($LASTEXITCODE) { throw 'MSBuild item evaluation failed.' }
    $items=($output -join "`n" | ConvertFrom-Json).Items.Compile
    foreach($source in $expected) {
        $matches=@($items | Where-Object { $_.Identity -eq $source })
        if($matches.Count -ne 1) { throw "$source must be compiled exactly once; got $($matches.Count)" }
    }
}
CheckIncludes $project
if(Test-Path -LiteralPath $testProject) { throw 'Validation fixture already exists.' }
try {
    [xml]$xml=Get-Content -LiteralPath $project -Raw
    $namespace=New-Object Xml.XmlNamespaceManager($xml.NameTable)
    $namespace.AddNamespace('m','http://schemas.microsoft.com/developer/msbuild/2003')
    foreach($source in $expected) {
        $node=$xml.SelectSingleNode("//m:Compile[@Include='$source']",$namespace)
        if($node) { $null=$node.ParentNode.RemoveChild($node) }
    }
    $xml.Save($testProject)
    CheckIncludes $testProject
    Write-Host 'PASS normal and stale-project design-time evaluation: both backup sources included exactly once.'
}
finally { if(Test-Path -LiteralPath $testProject) { Remove-Item -LiteralPath $testProject -Force } }
