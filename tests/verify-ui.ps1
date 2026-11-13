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
foreach ($name in 'RbPointerCircle', 'RbToolMouse', 'RbToolEraser', 'BtnSaveScreenshot', 'BtnClearAll', 'BtnPinScreenshot', 'BtnScreenZoom', 'BtnManual', 'BtnUpdateCheck', 'RbToolNumber', 'BtnResetNumber', 'ChkBlackboard') {
    $control = $window.FindName($name)
    $rect = $control.TransformToAncestor($card).TransformBounds([Windows.Rect]::new(0, 0, $control.ActualWidth, $control.ActualHeight))
    if ($rect.Width -le 0 -or $rect.Height -le 0 -or $rect.Right -gt $card.ActualWidth + 1 -or $rect.Bottom -gt $card.ActualHeight + 1) { throw "Control clipped: $name ($rect)" }
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
foreach ($key in 0x50, 0x48, 0x41, 0x52, 0x4F, 0x43, 0x4C, 0x54, 0x45, 0x31, 0x36, 0x2E, 0x4E) {
    if ($shortcut.Invoke($window, @([int]$key))) { throw "Unmodified key intercepted: $key" }
}
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
