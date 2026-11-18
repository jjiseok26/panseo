$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$pixels = [byte[]]::new(320*200*4)
$image = [Windows.Media.Imaging.BitmapSource]::Create(320,200,96,96,[Windows.Media.PixelFormats]::Bgra32,$null,$pixels,1280)
$view = [Panseo.Views.ZoomImageView]::new($image,$true)
$view.Measure([Windows.Size]::new(320,200))
$view.Arrange([Windows.Rect]::new(0,0,320,200))
$view.UpdateLayout()
$view.Reset()
$view.ZoomAt(2,[Windows.Point]::new(200,80))
$offset = $view.GetType().GetField('_offset',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
if ($view.ZoomRatio -ne 2 -or $offset.X -ne -200 -or $offset.Y -ne -80) { throw 'Zoom did not preserve cursor anchor' }
$view.PanBy([Windows.Vector]::new(10000,10000))
if ($offset.X -ne 0 -or $offset.Y -ne 0) { throw 'Pan bounds failed' }
$view.ZoomAt(100,[Windows.Point]::new(160,100))
if ($view.ZoomRatio -ne 8) { throw 'Maximum zoom failed' }
$view.ZoomAt(0.0001,[Windows.Point]::new(160,100))
if ($view.ZoomRatio -ne 0.25 -or $offset.X -ne 120 -or $offset.Y -ne 75) { throw 'Minimum zoom/centering failed' }
$view.Reset()
$wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,120)
$wheel.RoutedEvent = [Windows.Input.Mouse]::PreviewMouseWheelEvent
$view.RaiseEvent($wheel)
if (-not $wheel.Handled -or [Math]::Abs($view.ZoomRatio-1.2) -gt 0.0001) { throw 'Wheel zoom failed' }
$view.ZoomAt([double]::NaN,[Windows.Point]::new(0,0))
if ([double]::IsNaN($view.ZoomRatio)) { throw 'Invalid scale accepted' }
$view.Reset()
if ($view.ZoomRatio -ne 1) { throw 'Reset failed' }
$pin = [Panseo.Views.PinnedCaptureWindow]::new($image)
if ($pin.Content.Child.Children[0] -isnot [Panseo.Views.ZoomImageView] -or $pin.Content.Child.Children[1] -isnot [Windows.Controls.Primitives.Thumb]) { throw 'Pinned capture lacks zoom or resize handle' }
if ($pin.Content.ContextMenu.Items.Count -ne 3) { throw 'Pinned capture menu changed' }
$redPixels = [byte[]]::new(320*200*4)
for ($i=0; $i -lt $redPixels.Length; $i+=4) { $redPixels[$i+2]=255; $redPixels[$i+3]=255 }
$redImage = [Windows.Media.Imaging.BitmapSource]::Create(320,200,96,96,[Windows.Media.PixelFormats]::Bgra32,$null,$redPixels,1280)
$smallPin = [Panseo.Views.PinnedCaptureWindow]::new($redImage)
$smallPin.Width = 162; $smallPin.Height = 102
$content = $smallPin.Content
$content.Measure([Windows.Size]::new(162,102))
$content.Arrange([Windows.Rect]::new(0,0,162,102))
$content.UpdateLayout()
$rendered = [Windows.Media.Imaging.RenderTargetBitmap]::new(162,102,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$rendered.Render($content)
$output = [byte[]]::new(162*102*4)
$rendered.CopyPixels($output,162*4,0)
foreach ($point in @(@(20,20),@(130,50),@(80,80))) {
    $i = ($point[1]*162+$point[0])*4
    if ($output[$i+2] -lt 240 -or $output[$i+1] -gt 10 -or $output[$i] -gt 10) { throw "Pinned capture shows black after shrinking at $point" }
}
$manual = [Panseo.Views.ManualWindow]::new()
if (-not $manual.Content.Children[0].Text.Contains('jiseok')) { throw 'Manual copyright missing' }
$document = $manual.Content.Children[1].Document
$text = [Windows.Documents.TextRange]::new($document.ContentStart,$document.ContentEnd).Text
if ($text.Length -lt 1000 -or -not $text.Contains('Ctrl+1') -or -not $text.Contains('F1') -or -not $text.Contains('F3')) { throw 'Offline manual is incomplete' }
$icon = $assembly.GetType('Panseo.Helpers.AppIcon').GetMethod('Load').Invoke($null,@())
try {
    if ($icon.Width -ne 32 -or $icon.Height -ne 32) { throw 'Embedded tray icon invalid' }
    $iconBitmap = $icon.ToBitmap()
    try {
        $purple = 0; $white = 0
        foreach ($x in 0..31) { foreach ($y in 0..31) {
            $pixel = $iconBitmap.GetPixel($x,$y)
            if ($pixel.A -gt 100 -and $pixel.R -gt 50 -and $pixel.R -lt 180 -and $pixel.G -lt 130 -and $pixel.B -gt 150) { $purple++ }
            if ($pixel.A -gt 100 -and $pixel.R -gt 220 -and $pixel.G -gt 220 -and $pixel.B -gt 220) { $white++ }
        } }
        if ($purple -lt 300 -or $white -lt 40) { throw 'Tray icon pixels are corrupt or label is missing' }
    } finally { $iconBitmap.Dispose() }
}
finally { $icon.Dispose() }
$screen = [Panseo.Views.ScreenZoomWindow]::new($image,[Drawing.Rectangle]::new(0,0,320,200))
if (-not $screen.Topmost -or $screen.ZoomView -isnot [Panseo.Views.ZoomImageView]) { throw 'Screen zoom window invalid' }
'PASS: wheel zoom, cursor anchor, scale/pan bounds, reset, pinned zoom, offline manual, embedded tray icon.'
