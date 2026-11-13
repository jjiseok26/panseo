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
if ($pin.Content.Child -isnot [Panseo.Views.ZoomImageView]) { throw 'Pinned capture lacks zoom view' }
if ($pin.Content.ContextMenu.Items.Count -ne 2) { throw 'Pinned capture menu changed' }
$manual = [Panseo.Views.ManualWindow]::new()
$document = $manual.Content.Document
$text = [Windows.Documents.TextRange]::new($document.ContentStart,$document.ContentEnd).Text
if ($text.Length -lt 1000 -or -not $text.Contains('Ctrl+Q') -or -not $text.Contains('Ctrl+F10')) { throw 'Offline manual is incomplete' }
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
