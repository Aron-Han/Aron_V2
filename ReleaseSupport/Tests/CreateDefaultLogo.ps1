$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'Aron_V2\bin\x64\Debug\Visual Inspection.exe'))
$create = $assembly.GetType('Aron_V2.BrandingSettings').GetMethod('CreateMark', [Reflection.BindingFlags]'Static,NonPublic')
$sizes = @(16,24,32,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = $create.Invoke($null,@(3,$size))
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$stream.ToArray()
    $bitmap.Dispose(); $stream.Dispose()
}
$assets = Join-Path $root 'Aron_V2\Assets'
[IO.Directory]::CreateDirectory($assets) | Out-Null
$file = [IO.File]::Create((Join-Path $assets 'LogoD.ico'))
$writer = New-Object IO.BinaryWriter $file
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index=0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) {0} else {$sizes[$index]}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frames[$index].Length); $writer.Write([UInt32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
'Default D icon generated: 16/24/32/48/64/128/256 px'
