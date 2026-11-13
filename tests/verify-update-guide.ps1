$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$type = [Panseo.Views.UpdateGuideWindow]
$guide = $type.GetMethod('CreateIllustratedGuide',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@())
$cards = $guide.Children[1]
if ($cards.Children.Count -ne 4) { throw 'Update illustrations missing' }
if (-not $cards.Children[0].Child.Children[0].Text.Contains('Ctrl+Q')) { throw 'Zoom shortcut in illustration incorrect' }
if ($cards.Children[1].Child.Children[1].Child.Children.Count -ne 3) { throw 'Number illustration missing' }
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
$window.SetStatus('Download complete')
$status = $type.GetField('_status',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)
if ($status.Text -ne 'Download complete') { throw 'Update status failed' }
$toolbar = [Panseo.Views.FloatingToolbarWindow]::new($null)
if (-not $toolbar.FindName('TxtCircleToolIcon').Text.Contains(([char]0x270F).ToString())) { throw 'Pencil icon not restored' }
if (-not $toolbar.FindName('BtnScreenZoom').Content.Contains('Ctrl+Q')) { throw 'Toolbar zoom shortcut incorrect' }
'PASS: illustrated update guide, release image export, status updates, Ctrl+Q label, pencil icon.'
