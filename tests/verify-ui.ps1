$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$window = New-Object Panseo.Views.FloatingToolbarWindow -ArgumentList (, $null)
$window.OpenMenu()
$window.UpdateLayout()
$layout = $window.FindName('MenuLayout')
if ($layout.DesiredSize.Width -gt 350 -or $layout.DesiredSize.Height -lt 600) { throw 'Menu measurement failed.' }
$card = $window.FindName('MenuCard')
$card.Measure([Windows.Size]::new($window.Width, $window.Height))
$card.Arrange([Windows.Rect]::new(0, 0, $window.Width, $window.Height))
$card.UpdateLayout()
foreach ($name in 'RbPointerCircle', 'RbToolMouse', 'RbToolEraser', 'BtnSaveScreenshot', 'BtnSaveDrawing', 'BtnClearAll', 'BtnPinScreenshot', 'BtnScreenZoom', 'BtnManual', 'BtnUpdateCheck', 'RbToolNumber', 'BtnResetNumber', 'TxtStartNumber', 'BtnApplyStartNumber', 'BtnNumberSmaller', 'BtnNumberLarger', 'ChkBlackboard', 'BtnPointerColorPicker', 'BtnDrawingColorPicker') {
    $control = $window.FindName($name)
    $rect = $control.TransformToAncestor($card).TransformBounds([Windows.Rect]::new(0, 0, $control.ActualWidth, $control.ActualHeight))
    if ($rect.Width -le 0 -or $rect.Height -le 0 -or $rect.Right -gt $card.ActualWidth + 1 -or $rect.Bottom -gt $card.ActualHeight + 1) { throw "Control clipped: $name ($rect)" }
}
$clear = $window.FindName('BtnClearAll')
$zoom = $window.FindName('BtnScreenZoom')
$pin = $window.FindName('BtnPinScreenshot')
$save = $window.FindName('BtnSaveScreenshot')
if ($clear.Parent -ne $zoom.Parent -or $pin.Parent -ne $save.Parent -or $clear.Parent -ne $pin.Parent -or
    $clear.TransformToAncestor($card).Transform([Windows.Point]::new(0,0)).X -ge $zoom.TransformToAncestor($card).Transform([Windows.Point]::new(0,0)).X -or
    $save.TransformToAncestor($card).Transform([Windows.Point]::new(0,0)).X -ge $pin.TransformToAncestor($card).Transform([Windows.Point]::new(0,0)).X) { throw 'Capture/zoom button order failed' }
if ($save.Content.Inlines.LastInline.Text -ne 'F1' -or $pin.Content.Inlines.LastInline.Text -notlike 'F3*') { throw 'Capture shortcut labels are stale' }
if ($window.FindName('BtnClose').ToolTip -ne '프로그램 종료' -or $window.FindName('BtnSaveDrawing').ToolTip -notmatch 'PNG') { throw 'Exit or drawing save action missing' }
$shortcutSource = Get-Content (Join-Path $root 'src\Views\FloatingToolbarWindow.xaml.cs') -Raw
if ($shortcutSource -notmatch 'HOTKEY_ID_CAPTURE, 0x4000, VK_F1' -or $shortcutSource -notmatch 'HOTKEY_ID_PIN, 0x4000, VK_F3') { throw 'F1/F3 global hotkeys missing' }
if ($shortcutSource -notmatch '_zoomHotkeyRegistered = Win32.RegisterHotKey' -or $shortcutSource -notmatch '_zoomNumpadHotkeyRegistered = Win32.RegisterHotKey' -or $shortcutSource -notmatch 'if \(nCode >= 0 && \(!_zoomHotkeyRegistered \|\| !_zoomNumpadHotkeyRegistered\)\)') { throw 'Zoom hotkey fallback missing' }
$zoomKeys = $window.GetType().GetMethod('IsZoomKeyCombination',[Reflection.BindingFlags]'Static,NonPublic')
$drawingModeKeys = $window.GetType().GetMethod('IsDrawingModeKeyCombination',[Reflection.BindingFlags]'Static,NonPublic')
if (-not $drawingModeKeys.Invoke($null,@([int]0x100,[uint32]0x32,$true,$false,$false)) -or
    -not $drawingModeKeys.Invoke($null,@([int]0x100,[uint32]0x62,$true,$false,$false)) -or
    $drawingModeKeys.Invoke($null,@([int]0x100,[uint32]0x32,$false,$false,$false)) -or
    $drawingModeKeys.Invoke($null,@([int]0x100,[uint32]0x32,$true,$true,$false))) { throw 'Ctrl+2 drawing mode combination failed' }
$pointerSize = $window.GetType().GetMethod('NextPointerSize',[Reflection.BindingFlags]'Static,NonPublic')
if ($pointerSize.Invoke($null,@([double]65,[int]5)) -ne 70 -or $pointerSize.Invoke($null,@([double]160,[int]5)) -ne 160 -or $pointerSize.Invoke($null,@([double]20,[int]-5)) -ne 20) { throw 'Pointer size keys exceed slider range' }
$pointerKey = $window.GetType().GetMethod('PointerSizeStep',[Reflection.BindingFlags]'Static,NonPublic')
if ($pointerKey.Invoke($null,@([int]0xBB,[Windows.Input.ModifierKeys]::None)) -ne 0 -or
    $pointerKey.Invoke($null,@([int]0xBD,[Windows.Input.ModifierKeys]::None)) -ne 0 -or
    $pointerKey.Invoke($null,@([int]0x6B,[Windows.Input.ModifierKeys]::Control)) -ne 5 -or
    $pointerKey.Invoke($null,@([int]0x6D,[Windows.Input.ModifierKeys]::Control)) -ne -5) { throw 'Pointer size keys intercept ordinary typing' }
$thickness = [Panseo.Views.OverlayWindow].GetMethod('NextThickness',[Reflection.BindingFlags]'Static,NonPublic')
if ($thickness.Invoke($null,@([double]4,[int]120)) -ne 5 -or $thickness.Invoke($null,@([double]2,[int]-120)) -ne 2 -or $thickness.Invoke($null,@([double]24,[int]120)) -ne 24) { throw 'Drawing wheel thickness bounds failed' }
$settings = [Panseo.Models.AppSettings]::Instance
$settingsPath = Join-Path $root ('tests\Panseo-settings-' + [guid]::NewGuid().ToString('N') + '.json')
$saveSettings = $settings.GetType().GetMethod('Save',[Reflection.BindingFlags]'Instance,NonPublic',$null,[Type[]]@([string]),$null)
$loadSettings = $settings.GetType().GetMethod('Load',[Reflection.BindingFlags]'Instance,NonPublic',$null,[Type[]]@([string]),$null)
try {
    $settings.PointerSize = 105; $settings.StrokeThickness = 13; $settings.NumberSize = 1.5
    $settings.DrawingColor = [Windows.Media.Colors]::Blue; $settings.CurrentTool = [Panseo.Models.DrawingTool]::Pen
    $settings.MenuOpen = $true; $settings.ToolbarLeft = 140; $settings.ToolbarTop = 180
    [void]$saveSettings.Invoke($settings,[object[]]@([string]$settingsPath))
    [void]$saveSettings.Invoke($settings,[object[]]@([string]$settingsPath))
    $settings.PointerSize = 40; $settings.StrokeThickness = 4; $settings.NumberSize = 1
    $settings.DrawingColor = [Windows.Media.Colors]::Red; $settings.CurrentTool = [Panseo.Models.DrawingTool]::None
    [void]$loadSettings.Invoke($settings,[object[]]@([string]$settingsPath))
    if ($settings.PointerSize -ne 105 -or $settings.StrokeThickness -ne 13 -or $settings.NumberSize -ne 1.5 -or
        $settings.DrawingColor -ne [Windows.Media.Colors]::Blue -or $settings.CurrentTool -ne [Panseo.Models.DrawingTool]::Pen -or
        -not $settings.MenuOpen -or $settings.ToolbarLeft -ne 140 -or $settings.ToolbarTop -ne 180) { throw 'Settings did not survive restart' }
    $window.RestoreSettings()
    if ($window.FindName('SliderPointerSize').Value -ne 105 -or $window.FindName('SliderStrokeThickness').Value -ne 13 -or
        -not $window.FindName('RbToolPen').IsChecked) { throw 'Saved settings were not reflected in toolbar' }
} finally { Remove-Item -LiteralPath $settingsPath -ErrorAction SilentlyContinue }
foreach ($key in 0x31,0x61) {
    if (-not $zoomKeys.Invoke($null,@([int]0x100,[uint32]$key,$true,$false,$false))) { throw "Ctrl+1 key rejected: $key" }
    if ($zoomKeys.Invoke($null,@([int]0x100,[uint32]$key,$true,$true,$false))) { throw 'Ctrl+Shift+1 conflicts with zoom' }
}
if ($null -eq $window.FindName('MenuHeader').Background) { throw 'Header empty space is not draggable' }
$previousBottom = 0
foreach ($name in 'RbPointerCircle', 'RbToolMouse', 'ChkBlackboard') {
    $control = $window.FindName($name)
    $rect = $control.TransformToAncestor($card).TransformBounds([Windows.Rect]::new(0, 0, $control.ActualWidth, $control.ActualHeight))
    if ($rect.Top -le $previousBottom) { throw "Incorrect section order: $name" }
    $previousBottom = $rect.Bottom
}
# Exercise the same WM_HOTKEY dispatch used by Ctrl+F5 through Ctrl+F9.
$hook = $window.GetType().GetMethod('HwndHook', [Reflection.BindingFlags]'Instance,NonPublic')
$window.SelectTool([Panseo.Models.DrawingTool]::None)
$arguments = @([IntPtr]::Zero, [int]0x0312, [IntPtr]9014, [IntPtr]::Zero, $false)
[void]$hook.Invoke($window, $arguments)
if (-not $arguments[4] -or [Panseo.Models.AppSettings]::Instance.CurrentTool -eq [Panseo.Models.DrawingTool]::None) { throw 'Ctrl+2 did not enable drawing' }
$arguments[4] = $false
[void]$hook.Invoke($window, $arguments)
if (-not $arguments[4] -or [Panseo.Models.AppSettings]::Instance.CurrentTool -ne [Panseo.Models.DrawingTool]::None) { throw 'Ctrl+2 did not disable drawing' }
$names = 'RbPointerCircle', 'RbPointerLaser', 'RbPointerSpotlight', 'RbPointerRing', 'RbPointerNone'
foreach ($index in 0..4) {
    $arguments = @([IntPtr]::Zero, [int]0x0312, [IntPtr](9004 + $index), [IntPtr]::Zero, $false)
    [void]$hook.Invoke($window, $arguments)
    if (-not $arguments[4] -or [int][Panseo.Models.AppSettings]::Instance.PointerShape -ne $index -or -not $window.FindName($names[$index]).IsChecked) { throw "Pointer shortcut failed: $index" }
}
[Panseo.Models.AppSettings]::Instance.CurrentTool = [Panseo.Models.DrawingTool]::Pen
$arguments = @([IntPtr]::Zero, [int]0x0312, [IntPtr]9011, [IntPtr]::Zero, $false)
[void]$hook.Invoke($window, $arguments)
if (-not $arguments[4] -or [Panseo.Models.AppSettings]::Instance.CurrentTool -ne [Panseo.Models.DrawingTool]::None -or -not $window.FindName('RbToolMouse').IsChecked) { throw 'Ctrl+M dispatch failed' }
$window.TogglePointerEffect()
if ([Panseo.Models.AppSettings]::Instance.PointerShape -ne [Panseo.Models.PointerShape]::FocusRing) { throw 'Pointer toggle did not restore last effect.' }
$selectPointer = $window.GetType().GetMethod('SelectPointerShape', [Reflection.BindingFlags]'Instance,NonPublic')
[void]$selectPointer.Invoke($window, @([Panseo.Models.PointerShape]::CircleHighlight))
$shortcut = $window.GetType().GetMethod('HandleDrawingShortcut', [Reflection.BindingFlags]'Instance,NonPublic')
foreach ($key in 0x50, 0x48, 0x41, 0x52, 0x4F, 0x4C, 0x54, 0x45, 0x31, 0x36, 0x2E, 0x4E, 0x4D, 0x5A, 0x59) {
    if (-not $shortcut.Invoke($window, @([int]$key))) { throw "Drawing mode key missing: $key" }
}
if ($shortcutSource -notmatch 'vk == 0x43.*C: 색상 선택' -or $shortcutSource -notmatch 'if \(isDrawingActive\)') { throw 'Drawing color picker or mode guard missing' }
# Verify that the entire menu also fits a small viewport without scrolling.
foreach ($size in @(@(320, 480), @(350, 600))) {
    $card.Measure([Windows.Size]::new($size[0], $size[1]))
    $card.Arrange([Windows.Rect]::new(0, 0, $size[0], $size[1]))
    $card.UpdateLayout()
    $rect = $layout.TransformToAncestor($card).TransformBounds([Windows.Rect]::new(0, 0, $layout.ActualWidth, $layout.ActualHeight))
    if ($rect.Right -gt $card.ActualWidth + 1 -or $rect.Bottom -gt $card.ActualHeight + 1) { throw "Menu clipped at $size" }
}
$card.Measure([Windows.Size]::new($window.Width, $window.Height))
$card.Arrange([Windows.Rect]::new(0, 0, $window.Width, $window.Height))
$card.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($window.Width), [int][Math]::Ceiling($window.Height), 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($card)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path $root 'tests\menu-preview.png'))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
"PASS: vertical order and width, pointer shortcuts, small viewports, plain keys. Menu: $($window.Width) x $($window.Height)."
