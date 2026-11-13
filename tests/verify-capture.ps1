$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$pixels = [byte[]]::new(320 * 200 * 4)
for ($i = 0; $i -lt $pixels.Length; $i += 4) { $pixels[$i] = 255; $pixels[$i+1] = 255; $pixels[$i+2] = 255; $pixels[$i+3] = 255 }
$image = [Windows.Media.Imaging.BitmapSource]::Create(320, 200, 96, 96, [Windows.Media.PixelFormats]::Bgra32, $null, $pixels, 1280)
$crop = [Panseo.Views.RegionCaptureWindow].GetMethod('Crop', [Reflection.BindingFlags]'Static,NonPublic')
$cropped = $crop.Invoke($null, @($image, [Windows.Rect]::new(10,20,80,40), [Windows.Size]::new(160,100)))
if ($cropped.PixelWidth -ne 160 -or $cropped.PixelHeight -ne 80) { throw 'Region scaling failed' }
$clamped = $crop.Invoke($null, @($image, [Windows.Rect]::new(-10,-10,200,200), [Windows.Size]::new(160,100)))
if ($clamped.PixelWidth -ne 320 -or $clamped.PixelHeight -ne 200) { throw 'Region clamping failed' }
[Panseo.Models.AppSettings]::Instance.DrawingColor = [Windows.Media.Colors]::Red
[Panseo.Models.AppSettings]::Instance.StrokeThickness = 4
$editor = [Panseo.Views.CaptureEditorWindow]::new($image)
$canvas = $editor.GetType().GetField('_canvas', $flags).GetValue($editor)
$shapeMethod = $editor.GetType().GetMethod('CreateShape', $flags)
foreach ($tool in 'Pen','Rectangle','Ellipse','Arrow') {
    $shape = $shapeMethod.Invoke($editor, @([Panseo.Models.DrawingTool]::$tool, [Windows.Point]::new(20,20), [Windows.Point]::new(100,80)))
    [void]$canvas.Children.Add($shape)
}
$editor.GetType().GetMethod('BeginText', $flags).Invoke($editor, @([Windows.Point]::new(120,100)))
$text = $editor.GetType().GetField('_textEditor', $flags).GetValue($editor)
$text.Text = '캡처 테스트'
$result = $editor.RenderCapture()
if ($canvas.Children.Count -ne 5 -or $result.PixelWidth -ne 320 -or $result.PixelHeight -ne 200) { throw 'Annotation render failed' }
$editor.Undo()
if ($canvas.Children.Count -ne 4) { throw 'Undo failed' }
$editor.Redo()
if ($canvas.Children.Count -ne 5) { throw 'Redo failed' }
$output = [byte[]]::new(320*200*4)
$result.CopyPixels($output,1280,0)
$red = 0
for ($i=0; $i -lt $output.Length; $i+=4) { if ($output[$i+2] -gt 220 -and $output[$i+1] -lt 50) { $red++ } }
if ($red -lt 200 -or $output[3] -ne 255 -or $output[0] -ne 255) { throw 'Image/annotation pixels missing' }
$write = $assembly.GetType('Panseo.Views.CaptureFiles').GetMethod('WritePng', [Reflection.BindingFlags]'Static,NonPublic')
$path = Join-Path $PSScriptRoot 'capture-test-output.png'
[void]$write.Invoke($null,@($result,[string]$path))
$roundTrip = [Windows.Media.Imaging.BitmapImage]::new([Uri]::new($path))
if ($roundTrip.PixelWidth -ne 320 -or $roundTrip.PixelHeight -ne 200) { throw 'PNG roundtrip failed' }
$pinned = [Panseo.Views.PinnedCaptureWindow]::new($result)
if (-not $pinned.Topmost -or $pinned.Content.ContextMenu.Items.Count -ne 2 -or $pinned.Content.ContextMenu.Items[0].Header -ne '저장하기' -or $pinned.Content.ContextMenu.Items[1].Header -ne '닫기') { throw 'Pinned menu failed' }
'PASS: scaled/clamped region crop, five annotation tools, undo/redo, original-pixel PNG export, floating context menu.'
