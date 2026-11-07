$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$workspace = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $workspace 'Aron_V2\bin\x64\Debug\Visual Inspection.exe'))
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$type = $assembly.GetType('Aron_V2.BrandingSettings')
$create = $type.GetMethod('CreateMark', $flags)
$output = Join-Path $PSScriptRoot 'UIValidation'
[IO.Directory]::CreateDirectory($output) | Out-Null
$board = New-Object Drawing.Bitmap 1300,360
$graphics = [Drawing.Graphics]::FromImage($board)
$graphics.Clear([Drawing.Color]::FromArgb(245,248,252))
$titleFont = New-Object Drawing.Font 'Segoe UI',20,([Drawing.FontStyle]::Bold)
$labelFont = New-Object Drawing.Font 'Segoe UI',12
$brush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(35,52,75))
$graphics.DrawString('BETTERWAY / ARON VISION - LOGO OPTIONS', $titleFont, $brush, 32,22)
$names = @('A  Vision Eye','B  Precision Target','C  Betterway B','D  Aperture','E  3D Layers')
for ($index=0; $index -lt 5; $index++) {
    $mark = $create.Invoke($null,@($index,256))
    $graphics.DrawImage($mark, (36+$index*254),88,190,190)
    $graphics.DrawString($names[$index],$labelFont,$brush,(36+$index*254),298)
    $mark.Dispose()
}
$board.Save((Join-Path $output 'LogoOptions.png'))
$graphics.Dispose(); $board.Dispose(); $titleFont.Dispose(); $labelFont.Dispose(); $brush.Dispose()
'Logo candidates rendered: PASS'
