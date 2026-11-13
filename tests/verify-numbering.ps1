$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Windows.Forms
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$settings = [Panseo.Models.AppSettings]::Instance
$settings.ResetNumbering()
$settings.CurrentTool = [Panseo.Models.DrawingTool]::Number
$one = [Panseo.Views.OverlayWindow]::new([Windows.Forms.Screen]::PrimaryScreen)
$two = [Panseo.Views.OverlayWindow]::new([Windows.Forms.Screen]::PrimaryScreen)
$down = $one.GetType().GetMethod('OnMouseDown',$flags)
foreach ($overlay in @($one,$one,$two)) {
    $event = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
    $event.RoutedEvent = [Windows.Input.Mouse]::MouseDownEvent
    [void]$down.Invoke($overlay,@($event))
    if (-not $event.Handled) { throw 'Number click was not consumed' }
}
$canvasOne = $one.FindName('DrawingCanvas')
$canvasTwo = $two.FindName('DrawingCanvas')
if ($canvasOne.Children.Count -ne 2 -or $canvasOne.Children[0].Tag -ne 1 -or $canvasOne.Children[1].Tag -ne 2 -or $canvasTwo.Children[0].Tag -ne 3) { throw 'Click sequence does not span monitors' }
$one.Undo()
if ($canvasOne.Children.Count -ne 1) { throw 'Number undo failed' }
$one.Redo()
if ($canvasOne.Children.Count -ne 2 -or $canvasOne.Children[1].Tag -ne 2 -or $settings.TakeNextNumber() -ne 4) { throw 'Number redo/sequence failed' }
$settings.ResetNumbering()
if ($settings.TakeNextNumber() -ne 1) { throw 'Number reset failed' }
$toolbar = [Panseo.Views.FloatingToolbarWindow]::new($null)
$toolbar.SelectTool([Panseo.Models.DrawingTool]::Number)
$toolbar.ToggleDrawingMode()
$toolbar.ToggleDrawingMode()
if ($settings.CurrentTool -ne [Panseo.Models.DrawingTool]::Number -or -not $toolbar.FindName('RbToolNumber').IsChecked) { throw 'Number mode restoration failed' }
$toolbar.OpenMenu()
$toolbar.Content.Measure([Windows.Size]::new($toolbar.Width,$toolbar.Height))
$toolbar.Content.Arrange([Windows.Rect]::new(0,0,$toolbar.Width,$toolbar.Height))
$toolbar.Content.UpdateLayout()
$minimum = $toolbar.FindName('BtnMinimizeToCircle')
$manual = $toolbar.FindName('BtnManual')
$update = $toolbar.FindName('BtnUpdateCheck')
$header = $toolbar.FindName('MenuHeader')
$updateX = $update.TransformToAncestor($header).Transform([Windows.Point]::new(0,0)).X
$manualX = $manual.TransformToAncestor($header).Transform([Windows.Point]::new(0,0)).X
$minimumX = $minimum.TransformToAncestor($header).Transform([Windows.Point]::new(0,0)).X
if ($updateX -ge $manualX -or $manualX -ge $minimumX -or $manual.Parent -ne $minimum.Parent) { throw 'Header button order incorrect' }
if ($null -ne $toolbar.FindName('FloatingCircle').Effect) { throw 'Floating circle still has a shadow' }
# Start-number parsing and the actual toolbar apply action.
foreach ($value in '', '0', '-1', '1.5', 'abc', '2147483648') {
    $parsed = 0
    if ([Panseo.Models.AppSettings]::TryParseStartNumber($value,[ref]$parsed)) { throw "Invalid start accepted: $value" }
}
$parsed = 0
if (-not [Panseo.Models.AppSettings]::TryParseStartNumber(' 5 ',[ref]$parsed) -or $parsed -ne 5) { throw 'Valid start rejected' }
$toolbar.FindName('TxtStartNumber').Text = '5'
$apply = $toolbar.GetType().GetMethod('BtnApplyStartNumber_Click',$flags)
[void]$apply.Invoke($toolbar,@($null,$null))
foreach ($overlay in @($one,$two)) {
    $event = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
    $event.RoutedEvent = [Windows.Input.Mouse]::MouseDownEvent
    [void]$down.Invoke($overlay,@($event))
}
if ($canvasOne.Children[$canvasOne.Children.Count-1].Tag -ne 5 -or $canvasTwo.Children[$canvasTwo.Children.Count-1].Tag -ne 6) { throw 'Custom start not used across monitors' }
$toolbar.FindName('TxtStartNumber').Text = '0'
[void]$apply.Invoke($toolbar,@($null,$null))
if ($settings.TakeNextNumber() -ne 7) { throw 'Invalid input changed current sequence' }
$before = $canvasOne.Children.Count
[void]$toolbar.GetType().GetMethod('BtnResetNumber_Click',$flags).Invoke($toolbar,@($null,$null))
if ($settings.TakeNextNumber() -ne 1 -or $canvasOne.Children.Count -ne $before -or $toolbar.FindName('TxtStartNumber').Text -ne '1') { throw 'Reset changed old stamps or input was not reset' }
$settings.SetStartNumber([int]::MaxValue)
if ($settings.TakeNextNumber() -ne 2147483647L -or $settings.TakeNextNumber() -ne 2147483648L) { throw 'Large start overflowed' }
$settings.ResetNumbering()
$pixels = [byte[]]::new(320*200*4)
$image = [Windows.Media.Imaging.BitmapSource]::Create(320,200,96,96,[Windows.Media.PixelFormats]::Bgra32,$null,$pixels,1280)
$editor = [Panseo.Views.CaptureEditorWindow]::new($image)
$editor.SelectTool([Panseo.Models.DrawingTool]::Number)
$mouseDown = $editor.GetType().GetMethod('CanvasMouseDown',$flags)
foreach ($index in 1..3) {
    $event = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
    $event.RoutedEvent = [Windows.Input.Mouse]::MouseDownEvent
    [void]$mouseDown.Invoke($editor,@($editor,$event))
}
$canvas = $editor.GetType().GetField('_canvas',$flags).GetValue($editor)
if ($canvas.Children.Count -ne 3 -or $canvas.Children[2].Tag -ne 3 -or $editor.GetType().GetField('_drawing',$flags).GetValue($editor)) { throw 'Capture click numbering failed' }
$editor.Undo(); $editor.Redo()
if ($canvas.Children.Count -ne 3) { throw 'Capture number undo/redo failed' }
$input = $editor.GetType().GetField('_startNumberInput',$flags).GetValue($editor)
$input.Text = '80'
[void]$editor.GetType().GetMethod('ApplyStartNumber',$flags).Invoke($editor,@())
foreach ($index in 1..2) {
    $event = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
    $event.RoutedEvent = [Windows.Input.Mouse]::MouseDownEvent
    [void]$mouseDown.Invoke($editor,@($editor,$event))
}
if ($canvas.Children.Count -ne 5 -or $canvas.Children[3].Tag -ne 80 -or $canvas.Children[4].Tag -ne 81) { throw 'Capture custom start failed' }
$input.Text = '-2'
[void]$editor.GetType().GetMethod('ApplyStartNumber',$flags).Invoke($editor,@())
if ($editor.GetType().GetField('_nextNumber',$flags).GetValue($editor) -ne 82) { throw 'Invalid capture input changed sequence' }
$preview = [Windows.Controls.Canvas]::new()
$preview.Background = [Windows.Media.Brushes]::White
$x = 30
foreach ($number in 1,2,3,10,100) {
    $color = if ($number -eq 100) { [Windows.Media.Colors]::Yellow } else { [Windows.Media.Colors]::Red }
    [void]$preview.Children.Add([Panseo.Helpers.DrawingHelper]::CreateNumberStamp($number,[Windows.Point]::new($x,35),$color,4))
    $x += 55
}
$preview.Measure([Windows.Size]::new(300,70))
$preview.Arrange([Windows.Rect]::new(0,0,300,70))
$preview.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(300,70,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($preview)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path $PSScriptRoot 'numbering-preview.png'))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
'PASS: custom starts, validation, large-number safety, cross-monitor sequence, reset, undo/redo, capture input, header order, shadow removal.'
