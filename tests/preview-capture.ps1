$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$image = [Windows.Media.Imaging.BitmapImage]::new([Uri]::new((Join-Path $PSScriptRoot 'capture-test-output.png')))
$editor = [Panseo.Views.CaptureEditorWindow]::new($image)
$surface = $editor.Content
$surface.Measure([Windows.Size]::new(650,400))
$surface.Arrange([Windows.Rect]::new(0,0,650,400))
$surface.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(650,400,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($surface)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path $PSScriptRoot 'capture-editor-preview.png'))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
'Editor preview rendered.'
