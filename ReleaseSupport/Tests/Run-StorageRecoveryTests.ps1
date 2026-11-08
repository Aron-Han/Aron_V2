param([string]$BuildOutput = (Join-Path $PSScriptRoot '..\artifacts\StorageRecoveryValidation'))
$ErrorActionPreference='Stop'
$sdk='C:\Program Files\Cognex\VisionPro\ReferencedAssemblies'
$refs=@('Cognex.VisionPro.dll','Cognex.VisionPro.Core.dll','Cognex.VisionPro.CorePlus.dll','Cognex.VisionPro.Interop.Core.dll','Cognex.VisionPro3D.dll','Cognex.VisionPro3D.Core.dll','Cognex.VisionPro.ToolGroup.dll','Cognex.VisionPro.ImageProcessing.dll','Cognex.VisionPro.ImageFile.dll') | ForEach-Object { '/r:'+ (Join-Path $sdk $_) }
foreach($test in @('StorageRecoveryTests','RecoveryLogPolicyTests')) {
  $binary=Join-Path $BuildOutput ($test+'.exe')
  & C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /platform:x64 ('/out:'+$binary) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll $refs (Join-Path $PSScriptRoot ($test+'.cs'))
  if($LASTEXITCODE -ne 0){throw "$test compilation failed"}
  & $binary (Join-Path $BuildOutput 'Visual Inspection.exe') (Join-Path $BuildOutput ('TestData\'+[guid]::NewGuid().ToString('N')))
  if($LASTEXITCODE -ne 0){throw "$test failed"}
}
$binary=Join-Path $BuildOutput 'ProfinetEchoLifecycleTests.exe'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /platform:x64 ('/out:'+$binary) ('/r:'+(Join-Path $BuildOutput 'Visual Inspection.exe')) /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/r:'+(Join-Path $sdk 'Cognex.VisionPro.dll')) ('/r:'+(Join-Path $sdk 'Cognex.VisionPro.Comm.dll')) (Join-Path $PSScriptRoot 'ProfinetEchoLifecycleTests.cs')
if($LASTEXITCODE -ne 0){throw 'ProfinetEchoLifecycleTests compilation failed'}
& $binary
if($LASTEXITCODE -ne 0){throw 'ProfinetEchoLifecycleTests failed'}
