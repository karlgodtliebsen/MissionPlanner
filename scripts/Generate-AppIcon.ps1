# Repackages the existing PNG artwork as a multi-resolution Windows icon.
# Run on Windows after changing Resources/AppIcon/mpdesktop.png.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path $PSScriptRoot '../src/UI/MissionPlanner.App/Resources/AppIcon'
$source = [System.Drawing.Image]::FromFile((Join-Path $assetDirectory 'mpdesktop.png'))
$frames = @()
$sizes = @(16, 24, 32, 48, 64, 128, 256)
try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
    $output = [System.IO.File]::Create((Join-Path $assetDirectory 'mpdesktop.ico'))
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        for ($index = 0; $index -lt $frames.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) {
            $writer.Write([byte[]]$frame)
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $source.Dispose()
}
