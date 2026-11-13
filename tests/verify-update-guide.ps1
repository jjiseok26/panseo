$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$type = [Panseo.Views.UpdateGuideWindow]
$guide = $type.GetMethod('CreateIllustratedGuide',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@())
$cards = $guide.Children[1]
if ($cards.Children.Count -ne 4) { throw 'Update illustrations missing' }
if (-not $cards.Children[0].Child.Children[0].Text.Contains('Ctrl+Q')) { throw 'Zoom shortcut in illustration incorrect' }
if ($cards.Children[1].Child.Children[1].Child.Children.Count -ne 6) { throw 'Number illustration missing' }
$guide.Measure([Windows.Size]::new(720,[double]::PositiveInfinity))
$height = [int][Math]::Ceiling($guide.DesiredSize.Height)
$guide.Arrange([Windows.Rect]::new(0,0,720,$height))
$guide.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(720,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($guide)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path $PSScriptRoot 'update-guide.png'))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
$window = [Panseo.Views.UpdateGuideWindow]::new('Checking')
$update = $type.GetField('_update',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)
if ($update.IsEnabled) { throw 'Update was enabled before download verification' }
$actions = $window.Content.Children[1]
if ($actions.Children[0] -ne $update -or $actions.Children[1] -isnot [Windows.Controls.Button]) { throw 'Update button must precede Close' }
$script:updateClicked = $false
$window.add_UpdateRequested([Action]{ $script:updateClicked = $true })
$ready = [string][char]0xC0C8 + ' ' + [char]0xBC84 + [char]0xC804 + ' v1.0.9'
$window.SetStatus($ready)
if (-not $update.IsEnabled) { throw 'Verified update action not enabled' }
$click = [Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent)
$update.RaiseEvent($click)
if (-not $script:updateClicked) { throw 'Update button did not invoke installer action' }
$window.SetStatus('Download complete')
$status = $type.GetField('_status',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)
if ($status.Text -ne 'Download complete' -or $update.IsEnabled) { throw 'Update status failed' }
$toolbar = [Panseo.Views.FloatingToolbarWindow]::new($null)
if (-not $toolbar.FindName('TxtCircleToolIcon').Text.Contains(([char]0x270F).ToString())) { throw 'Pencil icon not restored' }
if (-not $toolbar.FindName('BtnScreenZoom').Content.Contains('Ctrl+Q')) { throw 'Toolbar zoom shortcut incorrect' }
'PASS: illustrated update guide, release image export, status updates, Ctrl+Q label, pencil icon.'
