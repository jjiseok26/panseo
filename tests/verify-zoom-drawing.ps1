$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Drawing
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$pixels = [byte[]]::new(320*200*4)
for ($i = 0; $i -lt $pixels.Length; $i += 4) { $pixels[$i] = 255; $pixels[$i+1] = 255; $pixels[$i+2] = 255; $pixels[$i+3] = 255 }
$image = [Windows.Media.Imaging.BitmapSource]::Create(320,200,96,96,[Windows.Media.PixelFormats]::Bgra32,$null,$pixels,1280)
$screen = [Panseo.Views.ScreenZoomWindow]::new($image,[Drawing.Rectangle]::new(0,0,320,200))
$view = $screen.ZoomView
$base = [Panseo.Views.CaptureEditorWindow]
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$canvas = $base.GetField('_canvas',$flags).GetValue($screen)
if ($view.Children.Count -ne 1 -or $view.Children[0] -ne $canvas) { throw 'Annotations and snapshot do not share zoom transform' }
$view.Measure([Windows.Size]::new(320,200))
$view.Arrange([Windows.Rect]::new(0,0,320,200))
$view.UpdateLayout()
$view.Reset()
foreach ($tool in 'Pen','Highlighter','Line','Rectangle','Ellipse','Arrow','Text','Number','Eraser') {
    $screen.SelectTool([Panseo.Models.DrawingTool]::$tool)
    if ($screen.CurrentTool.ToString() -ne $tool -or $view.LeftDragPans) { throw "Drawing tool did not take input: $tool" }
}
$screen.SelectTool([Panseo.Models.DrawingTool]::None)
if (-not $view.LeftDragPans) { throw 'Mouse mode did not restore drag pan' }
$toolbar = [Panseo.Views.FloatingToolbarWindow]::new($null)
$toolbar.GetType().GetField('_zoomWindow',$flags).SetValue($toolbar,$screen)
$toolbar.SelectTool([Panseo.Models.DrawingTool]::Pen)
if ($screen.CurrentTool -ne [Panseo.Models.DrawingTool]::Pen -or [Panseo.Models.AppSettings]::Instance.CurrentTool -ne [Panseo.Models.DrawingTool]::None) { throw 'Toolbar drawing did not route into zoom window' }
$toolbar.ToggleDrawingMode()
if ($screen.CurrentTool -ne [Panseo.Models.DrawingTool]::None) { throw 'Zoom drawing toggle did not select pan' }
$toolbar.ToggleDrawingMode()
if ($screen.CurrentTool -ne [Panseo.Models.DrawingTool]::Pen) { throw 'Zoom drawing toggle did not restore pen' }
$create = $base.GetMethod('CreateShape',$flags)
$add = $base.GetMethod('AddAnnotation',$flags)
$start = [Windows.Point]::new(30,40)
$end = [Windows.Point]::new(110,80)
foreach ($tool in 'Pen','Highlighter','Line','Rectangle','Ellipse','Arrow') {
    $shape = $create.Invoke($screen,@([Panseo.Models.DrawingTool]::$tool,$start,$end))
    [void]$add.Invoke($screen,@($shape))
}
$screen.SetStartNumber(8)
[void]$base.GetMethod('StampNumber',$flags).Invoke($screen,@([Windows.Point]::new(180,90)))
if ($canvas.Children.Count -ne 7) { throw 'Shapes and numbered stamp missing' }
$screen.Undo()
if ($canvas.Children.Count -ne 6) { throw 'Zoom annotation undo failed' }
$screen.Redo()
if ($canvas.Children.Count -ne 7) { throw 'Zoom annotation redo failed' }
$view.ZoomAt(2,[Windows.Point]::new(160,100))
$view.PanBy([Windows.Vector]::new(-20,-10))
$before = $screen.RenderCapture()
if ($before.PixelWidth -ne 320 -or $before.PixelHeight -ne 200 -or $view.ZoomRatio -ne 2) { throw 'Zoom export size or viewing scale changed' }
$a = [byte[]]::new(320*200*4)
$before.CopyPixels($a,1280,0)
$view.Reset()
$after = $screen.RenderCapture()
$b = [byte[]]::new(320*200*4)
$after.CopyPixels($b,1280,0)
for ($i = 0; $i -lt $a.Length; $i++) { if ($a[$i] -ne $b[$i]) { throw "Zoom changed exported pixels at $i" } }
$screen.SelectTool([Panseo.Models.DrawingTool]::Eraser)
[void]$base.GetMethod('EraseAtPoint',$flags).Invoke($screen,@([Windows.Point]::new(180,90)))
if ($canvas.Children.Count -ne 6) { throw 'Eraser missed annotation' }
$screen.Undo()
if ($canvas.Children.Count -ne 7) { throw 'Eraser undo failed' }
$screen.Redo()
if ($canvas.Children.Count -ne 6) { throw 'Eraser redo failed' }
$screen.ClearAll()
if ($canvas.Children.Count -ne 0) { throw 'Zoom clear all failed' }
'PASS: screen zoom drawing tools, shared transform, numbering, undo/redo, erase, full-resolution export, clear.'
