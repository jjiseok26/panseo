$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$frames = @()
foreach ($size in 16,20,24,32,48,64,128,256) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([Drawing.Color]::Transparent)
    $radius = [single]($size * 0.32)
    $edge = [single]($size - 1)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0,0,$radius,$radius,180,90)
    $path.AddArc($edge-$radius,0,$radius,$radius,270,90)
    $path.AddArc($edge-$radius,$edge-$radius,$radius,$radius,0,90)
    $path.AddArc(0,$edge-$radius,$radius,$radius,90,90)
    $path.CloseFigure()
    $brush = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0,0),[Drawing.Point]::new($size,$size),[Drawing.Color]::FromArgb(79,70,229),[Drawing.Color]::FromArgb(147,51,234))
    $font = [Drawing.Font]::new('Malgun Gothic',[single]($size * 0.34),[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
    $format = [Drawing.StringFormat]::new()
    $format.Alignment = [Drawing.StringAlignment]::Center
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $format.FormatFlags = [Drawing.StringFormatFlags]::NoWrap
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.FillPath($brush,$path)
        $graphics.DrawString(([char]0xD310).ToString()+[char]0xC11C,$font,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(0,0,$size,$size),$format)
        if ($size -eq 256) { $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png) }
        else {
            # Small frames use classic DIB data for .NET Framework NotifyIcon compatibility.
            $dib = [IO.BinaryWriter]::new($stream,[Text.Encoding]::UTF8,$true)
            try {
                $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
                $dib.Write([uint32]40); $dib.Write([int32]$size); $dib.Write([int32]($size*2))
                $dib.Write([uint16]1); $dib.Write([uint16]32); $dib.Write([uint32]0)
                $dib.Write([uint32]($size*$size*4+$maskStride*$size))
                foreach ($unused in 1..4) { $dib.Write([int32]0) }
                for ($y=$size-1; $y -ge 0; $y--) {
                    for ($x=0; $x -lt $size; $x++) {
                        $pixel = $bitmap.GetPixel($x,$y)
                        $dib.Write([byte]$pixel.B); $dib.Write([byte]$pixel.G); $dib.Write([byte]$pixel.R); $dib.Write([byte]$pixel.A)
                    }
                }
                for ($y=$size-1; $y -ge 0; $y--) {
                    $mask = [byte[]]::new($maskStride)
                    for ($x=0; $x -lt $size; $x++) {
                        if ($bitmap.GetPixel($x,$y).A -eq 0) {
                            $index = [int][Math]::Floor($x/8.0)
                            $mask[$index] = $mask[$index] -bor (128 -shr ($x % 8))
                        }
                    }
                    $dib.Write($mask)
                }
            } finally { $dib.Dispose() }
        }
        $frames += ,@{ size=$size; bytes=$stream.ToArray() }
        if ($size -eq 256) { $bitmap.Save((Join-Path $root 'app_logo.png'),[Drawing.Imaging.ImageFormat]::Png) }
    } finally { $stream.Dispose(); $format.Dispose(); $font.Dispose(); $brush.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$file = [IO.File]::Create((Join-Path $root 'panseo.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.size -eq 256) { 0 } else { $frame.size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.bytes) }
} finally { $writer.Dispose() }
