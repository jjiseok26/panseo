$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing, System.Windows.Forms
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
$selector = [Panseo.Views.RegionCaptureWindow]::new($image,[Drawing.Rectangle]::new(0,0,320,200))
$map = $selector.GetType().GetMethod('MapToViewport',[Reflection.BindingFlags]'Static,NonPublic')
$mapped = $map.Invoke($null,@([Drawing.Rectangle]::new(1920,100,500,400),[Drawing.Rectangle]::new(0,0,4992,1728),[Windows.Size]::new(2496,864)))
if ($mapped.Left -ne 960 -or $mapped.Top -ne 50 -or $mapped.Width -ne 250 -or $mapped.Height -ne 200) { throw 'Automatic region mapping across monitors failed' }
$toDesktop = $selector.GetType().GetMethod('MapToDesktop',[Reflection.BindingFlags]'Static,NonPublic')
$physical = $toDesktop.Invoke($null,@([Windows.Rect]::new(960,50,250,200),[Drawing.Rectangle]::new(0,0,4992,1728),[Windows.Size]::new(2496,864)))
if ($physical.X -ne 1920 -or $physical.Y -ne 100 -or $physical.Width -ne 500 -or $physical.Height -ne 400) { throw 'Pinned capture location mapping failed' }
$surface = $selector.GetType().GetField('_surface',$flags).GetValue($selector)
$surface.Measure([Windows.Size]::new(320,200))
$surface.Arrange([Windows.Rect]::new(0,0,320,200))
$updateSelection = $selector.GetType().GetMethod('UpdateSelection',$flags)
$drag = $selector.GetType().GetMethod('UpdateDrag',$flags)
$selector.GetType().GetField('_automaticSelection',$flags).SetValue($selector,$true)
[void]$updateSelection.Invoke($selector,@([Windows.Rect]::new(20,20,200,120)))
[void]$selector.GetType().GetMethod('BeginSelectionAt',$flags).Invoke($selector,@([Windows.Point]::new(100,70)))
$selection = $selector.GetType().GetField('_selection',$flags).GetValue($selector)
if ($selection.Width -gt 1 -or $selection.Height -gt 1 -or $selector.GetType().GetField('_automaticSelection',$flags).GetValue($selector)) { throw 'Dragging inside automatic region did not start a new selection' }
$initial = [Windows.Rect]::new(30,40,100,60)
[void]$updateSelection.Invoke($selector,@($initial))
$selector.GetType().GetField('_dragOriginal',$flags).SetValue($selector,$initial)
$selector.GetType().GetField('_edgeX',$flags).SetValue($selector,1)
$selector.GetType().GetField('_edgeY',$flags).SetValue($selector,1)
[void]$drag.Invoke($selector,@([Windows.Point]::new(170,125)))
$selection = $selector.GetType().GetField('_selection',$flags).GetValue($selector)
if ($selection.Width -ne 140 -or $selection.Height -ne 85) { throw 'Post-drag corner resize failed' }
$selector.GetType().GetField('_edgeX',$flags).SetValue($selector,0)
$selector.GetType().GetField('_edgeY',$flags).SetValue($selector,0)
$selector.GetType().GetField('_moving',$flags).SetValue($selector,$true)
$selector.GetType().GetField('_dragOriginal',$flags).SetValue($selector,$selection)
$selector.GetType().GetField('_start',$flags).SetValue($selector,[Windows.Point]::new(50,50))
[void]$drag.Invoke($selector,@([Windows.Point]::new(500,500)))
$selection = $selector.GetType().GetField('_selection',$flags).GetValue($selector)
if ($selection.Right -ne 320 -or $selection.Bottom -ne 200 -or -not $selector.GetType().GetField('_confirm',$flags).GetValue($selector).IsEnabled) { throw 'Moving selection outside capture bounds failed' }
$initial = [Windows.Rect]::new(50,50,100,50)
$selector.GetType().GetField('_dragOriginal',$flags).SetValue($selector,$initial)
$selector.GetType().GetField('_edgeX',$flags).SetValue($selector,-1)
$selector.GetType().GetField('_edgeY',$flags).SetValue($selector,-1)
$selector.GetType().GetField('_moving',$flags).SetValue($selector,$false)
[void]$drag.Invoke($selector,@([Windows.Point]::new(-20,-30)))
$selection = $selector.GetType().GetField('_selection',$flags).GetValue($selector)
if ($selection.Left -ne 0 -or $selection.Top -ne 0 -or $selection.Right -ne 150 -or $selection.Bottom -ne 100) { throw 'Top-left resize bounds failed' }
$selector.GetType().GetField('_edgeX',$flags).SetValue($selector,0)
$selector.GetType().GetField('_edgeY',$flags).SetValue($selector,0)
$selector.GetType().GetField('_start',$flags).SetValue($selector,[Windows.Point]::new(10,10))
[void]$drag.Invoke($selector,@([Windows.Point]::new(80,90)))
$selection = $selector.GetType().GetField('_selection',$flags).GetValue($selector)
if ($selection.Left -ne 10 -or $selection.Top -ne 10 -or $selection.Width -ne 70 -or $selection.Height -ne 80) { throw 'Selecting a new region failed' }
$instruction = $selector.GetType().GetField('_instruction',$flags).GetValue($selector)
$monitor = [Windows.Forms.Screen]::PrimaryScreen.Bounds
$selector.GetType().GetMethod('PositionInstruction',$flags).Invoke($selector,@($monitor,[Drawing.Point]::new($monitor.Left+30,$monitor.Top+30)))
if ($instruction.Margin.Left -lt 0 -or $instruction.Margin.Left + $instruction.Width -gt $surface.ActualWidth) { throw 'Instruction extends beyond selected monitor' }
$detector = $assembly.GetType('Panseo.Helpers.CaptureTargetDetector').GetMethod('Detect',[Reflection.BindingFlags]'Static,NonPublic')
$cursor = [Windows.Forms.Cursor]::Position
$target = $detector.Invoke($null,@($cursor,[Windows.Forms.SystemInformation]::VirtualScreen))
if (-not $target.Contains($cursor) -or $target.Width -lt 24 -or $target.Height -lt 24) { throw 'Automatic target detection failed' }
$detectWindow = $assembly.GetType('Panseo.Helpers.CaptureTargetDetector').GetMethod('DetectWindow',[Reflection.BindingFlags]'Static,NonPublic')
$windowTarget = $detectWindow.Invoke($null,@($cursor,[Windows.Forms.SystemInformation]::VirtualScreen))
if (-not $windowTarget.Contains($cursor) -or $windowTarget.Width -lt $target.Width -or $windowTarget.Height -lt $target.Height) { throw 'Window target toggle failed' }
$handles = $selector.GetType().GetField('_handles',$flags).GetValue($selector)
if ($handles.Count -ne 8 -or @($handles | Where-Object { $_.Visibility -ne [Windows.Visibility]::Visible }).Count -ne 0) { throw 'Selection resize handles are hidden' }
$confirm = $selector.GetType().GetField('_confirm',$flags).GetValue($selector)
if ($confirm.Margin.Left -le $selection.Right -or $confirm.Margin.Top -ne $selection.Top) { throw 'Confirm button is not beside selection' }
[Panseo.Models.AppSettings]::Instance.DrawingColor = [Windows.Media.Colors]::Red
[Panseo.Models.AppSettings]::Instance.StrokeThickness = 4
$editor = [Panseo.Views.CaptureEditorWindow]::new($image)
$toolPanel = $editor.Content.Children[0]
$groups = @($toolPanel.Children)
if ($groups.Count -ne 3 -or $groups[0].Tag -ne '그리기' -or $groups[1].Tag -ne '색상 · 굵기' -or $groups[2].Tag -ne '편집 · 파일') { throw 'Capture toolbar groups missing' }
$groupPanels = @($groups | ForEach-Object { $_.Child.Children[1] })
$toolbarControls = @($groupPanels | ForEach-Object { $_.Children })
$colors = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.Button] -and $_.ToolTip -eq '빨강' })
$stroke = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.Slider] })
$picker = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.Button] -and $_.ToolTip -eq '색상 선택' })
if ($colors.Count -ne 1 -or $stroke.Count -ne 1 -or $picker.Count -ne 1) { throw 'Capture color, picker, or stroke controls missing' }
$iconControls = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.Primitives.ButtonBase] })
foreach ($control in $iconControls) {
    if (-not $control.ToolTip -or ($control.Content -is [string] -and $control.Content.Length -gt 2)) { throw 'Capture editor icon or tooltip missing' }
}
if ($iconControls.Count -lt 16) { throw 'Capture editor toolbar actions missing' }
$eraser = @($iconControls | Where-Object { $_.ToolTip -like '지우개*' })[0]
$clear = @($iconControls | Where-Object { $_.ToolTip -eq '전체 지우기' })[0]
$fileButton = @($iconControls | Where-Object { $_.ToolTip -like '파일로 저장하기*' })[0]
if ($eraser.Content -isnot [Windows.Controls.Canvas] -or $clear.Content -ne '🗑' -or -not $fileButton) { throw 'Eraser, trash, or file save action missing' }
if ($picker[0].Content -isnot [Windows.Shapes.Ellipse] -or $picker[0].Content.Fill -isnot [Windows.Media.LinearGradientBrush]) { throw 'Gradient palette icon missing' }
$colorPicker = $assembly.GetType('Panseo.Helpers.ColorPicker')
$fromHsv = $colorPicker.GetMethod('FromHsv',[Reflection.BindingFlags]'Static,NonPublic')
foreach ($sample in @(@(0,255,0,0),@(120,0,255,0),@(240,0,0,255))) {
    $color = $fromHsv.Invoke($null,@([double]$sample[0],[double]1,[double]1))
    if ($color.R -ne $sample[1] -or $color.G -ne $sample[2] -or $color.B -ne $sample[3]) { throw 'HSV color picker conversion failed' }
}
$colors[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
if ([Panseo.Models.AppSettings]::Instance.DrawingColor -ne [Windows.Media.Colors]::Red) { throw 'Capture color selection failed' }
$stroke[0].Value = 12
if ([Panseo.Models.AppSettings]::Instance.StrokeThickness -ne 12) { throw 'Capture stroke slider failed' }
$canvas = $editor.GetType().GetField('_canvas', $flags).GetValue($editor)
$wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,120)
$wheel.RoutedEvent = [Windows.Input.Mouse]::PreviewMouseWheelEvent
$canvas.RaiseEvent($wheel)
if (-not $wheel.Handled -or $stroke[0].Value -ne 13) { throw 'Capture wheel stroke adjustment failed' }
[Panseo.Models.AppSettings]::Instance.StrokeThickness = 4
$toolButton = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.RadioButton] })[0]
$actionButton = @($toolbarControls | Where-Object { $_ -is [Windows.Controls.Button] })[0]
foreach ($control in @($toolButton,$actionButton)) {
    if (-not $control.Style -or -not @($control.Style.Setters | Where-Object { $_.Property -eq [Windows.Controls.Control]::TemplateProperty }).Count) { throw 'Capture editor Material button style missing' }
}
$editor.Content.Measure([Windows.Size]::new(900,500))
$editor.Content.Arrange([Windows.Rect]::new(0,0,900,500))
$preview = [Windows.Media.Imaging.RenderTargetBitmap]::new(900,500,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$preview.Render($editor.Content)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($preview))
$previewFile = [IO.File]::Create((Join-Path $PSScriptRoot 'editor-preview.png'))
try { $encoder.Save($previewFile) } finally { $previewFile.Dispose() }
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
$transparent = $editor.RenderDrawingOnly()
$transparentPixels = [byte[]]::new(320*200*4)
$transparent.CopyPixels($transparentPixels,1280,0)
$opaqueDrawingPixels = 0
for ($i=3; $i -lt $transparentPixels.Length; $i+=4) { if ($transparentPixels[$i] -gt 100) { $opaqueDrawingPixels++ } }
if ($transparentPixels[3] -ne 0 -or $opaqueDrawingPixels -lt 200) { throw 'Editor transparent background save failed' }
if ($editor.RenderCapture().PixelWidth -ne 320) { throw 'Editor background was not restored' }
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
$drawingCanvas = [Windows.Controls.Canvas]::new()
$drawingCanvas.Width = 40; $drawingCanvas.Height = 30
$drawingCanvas.Background = [Windows.Media.Brushes]::Transparent
$drawing = [Windows.Shapes.Rectangle]::new()
$drawing.Width = 10; $drawing.Height = 10; $drawing.Fill = [Windows.Media.Brushes]::Red
[Windows.Controls.Canvas]::SetLeft($drawing,5); [Windows.Controls.Canvas]::SetTop($drawing,5)
[void]$drawingCanvas.Children.Add($drawing)
$drawingCanvas.Measure([Windows.Size]::new(40,30))
$drawingCanvas.Arrange([Windows.Rect]::new(0,0,40,30))
$renderDrawing = [Panseo.Views.OverlayWindow].GetMethod('RenderDrawingCanvas',[Reflection.BindingFlags]'Static,NonPublic')
$drawingImage = $renderDrawing.Invoke($null,@($drawingCanvas,[int]40,[int]30))
$drawingPixels = [byte[]]::new(40*30*4)
$drawingImage.CopyPixels($drawingPixels,160,0)
if ($drawingPixels[3] -ne 0 -or $drawingPixels[(7*40+7)*4+2] -lt 200 -or $drawingPixels[(7*40+7)*4+3] -lt 200) { throw 'Transparent drawing PNG render failed' }
$pinned = [Panseo.Views.PinnedCaptureWindow]::new($result)
if (-not $pinned.Topmost -or $pinned.Content.ContextMenu.Items.Count -ne 3 -or $pinned.Content.ContextMenu.Items[0].Header -ne '저장하기' -or $pinned.Content.ContextMenu.Items[1].Header -ne '복사하기' -or $pinned.Content.ContextMenu.Items[2].Header -ne '닫기') { throw 'Pinned menu failed' }
$grip = $pinned.Content.Child.Children[1]
$oldWidth = $pinned.Width
$resize = [Windows.Controls.Primitives.DragDeltaEventArgs]::new(40,10)
$resize.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragDeltaEvent
$grip.RaiseEvent($resize)
if ($pinned.Width -le $oldWidth -or [Math]::Abs(($pinned.Width-2)/($pinned.Height-2)-1.6) -gt 0.01) { throw 'Pinned capture window resize failed' }
'PASS: scaled/clamped region crop, five annotation tools, undo/redo, original-pixel PNG export, floating context menu.'
