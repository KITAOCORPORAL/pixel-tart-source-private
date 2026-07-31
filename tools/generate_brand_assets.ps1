param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\RAWSelectionAssistant\Assets'))
$brandDirectory = Join-Path $assetDirectory 'Brand'
[System.IO.Directory]::CreateDirectory($brandDirectory) | Out-Null

function New-LogoBitmap([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 1024.0
    function RectangleF([double]$x, [double]$y, [double]$width, [double]$height) {
        [System.Drawing.RectangleF]::new([single]($x * $scale), [single]($y * $scale), [single]($width * $scale), [single]($height * $scale))
    }
    function RoundedPath([double]$x, [double]$y, [double]$width, [double]$height, [double]$radius) {
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $diameter = 2 * $radius
        $path.AddArc([single]($x * $scale), [single]($y * $scale), [single]($diameter * $scale), [single]($diameter * $scale), 180, 90)
        $path.AddArc([single](($x + $width - $diameter) * $scale), [single]($y * $scale), [single]($diameter * $scale), [single]($diameter * $scale), 270, 90)
        $path.AddArc([single](($x + $width - $diameter) * $scale), [single](($y + $height - $diameter) * $scale), [single]($diameter * $scale), [single]($diameter * $scale), 0, 90)
        $path.AddArc([single]($x * $scale), [single](($y + $height - $diameter) * $scale), [single]($diameter * $scale), [single]($diameter * $scale), 90, 90)
        $path.CloseFigure()
        return $path
    }

    $background = RoundedPath 64 64 896 896 208
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#171D24')), $background)

    if ($size -le 48) {
        $graphics.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#D7962D')), (RectangleF 190 310 644 500))
        $graphics.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F4C96B')), (RectangleF 276 190 472 472))
        $graphics.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F7F1DF')), (RectangleF 276 530 472 176))
        $graphics.FillPolygon([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#34373C')), [System.Drawing.PointF[]]@([System.Drawing.PointF]::new([single](310*$scale),[single](670*$scale)),[System.Drawing.PointF]::new([single](430*$scale),[single](560*$scale)),[System.Drawing.PointF]::new([single](520*$scale),[single](630*$scale)),[System.Drawing.PointF]::new([single](600*$scale),[single](580*$scale)),[System.Drawing.PointF]::new([single](706*$scale),[single](670*$scale))))
        $pixel = RoundedPath 742 150 100 100 24
        $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F4C96B')), $pixel)
        $graphics.Dispose()
        return $bitmap
    }

    $shell = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shell.AddBezier([single](236*$scale),[single](426*$scale),[single](236*$scale),[single](304*$scale),[single](337*$scale),[single](216*$scale),[single](512*$scale),[single](216*$scale))
    $shell.AddBezier([single](512*$scale),[single](216*$scale),[single](687*$scale),[single](216*$scale),[single](788*$scale),[single](304*$scale),[single](788*$scale),[single](426*$scale))
    $shell.AddLine([single](788*$scale),[single](426*$scale),[single](730*$scale),[single](744*$scale))
    $shell.AddBezier([single](730*$scale),[single](744*$scale),[single](717*$scale),[single](818*$scale),[single](654*$scale),[single](864*$scale),[single](580*$scale),[single](864*$scale))
    $shell.AddLine([single](580*$scale),[single](864*$scale),[single](444*$scale),[single](864*$scale))
    $shell.AddBezier([single](444*$scale),[single](864*$scale),[single](370*$scale),[single](864*$scale),[single](307*$scale),[single](818*$scale),[single](294*$scale),[single](744*$scale))
    $shell.CloseFigure()
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#D79A34')), $shell)
    $graphics.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#FFD875')), (RectangleF 280 258 464 344))
    $graphics.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F7F1DF')), (RectangleF 356 378 182 142))

    $photo = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $photo.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([single](382*$scale),[single](500*$scale)),
        [System.Drawing.PointF]::new([single](382*$scale),[single](470*$scale)),
        [System.Drawing.PointF]::new([single](428*$scale),[single](426*$scale)),
        [System.Drawing.PointF]::new([single](466*$scale),[single](462*$scale)),
        [System.Drawing.PointF]::new([single](494*$scale),[single](436*$scale)),
        [System.Drawing.PointF]::new([single](530*$scale),[single](474*$scale)),
        [System.Drawing.PointF]::new([single](530*$scale),[single](500*$scale))))
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#26313B')), $photo)
    $graphics.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#D79A34')), (RectangleF 472 394 36 36))

    foreach ($pixel in @(@(568,350,76,76,14,'#F4C96B'),@(656,350,52,52,10,'#F7F1DF'),@(568,438,52,52,10,'#F7F1DF'))) {
        $path = RoundedPath $pixel[0] $pixel[1] $pixel[2] $pixel[3] $pixel[4]
        $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($pixel[5])), $path)
    }

    $pen1 = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#F7F1DF'), [single](28*$scale))
    $pen1.StartCap = $pen1.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($pen1, [single](342*$scale), [single](676*$scale), [single](682*$scale), [single](676*$scale))
    $pen2 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(170,247,241,223), [single](24*$scale))
    $pen2.StartCap = $pen2.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($pen2, [single](372*$scale), [single](742*$scale), [single](652*$scale), [single](742*$scale))

    $graphics.Dispose()
    return $bitmap
}

$sizes = @(16,20,24,32,40,48,64,128,256)
$pngFiles = @()
foreach ($size in $sizes) {
    $bitmap = New-LogoBitmap $size
    $path = Join-Path $brandDirectory "PixelTart-$size.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $pngFiles += $path
}

$primary = New-LogoBitmap 1024
$primary.Save((Join-Path $assetDirectory 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$primary.Dispose()

$cover = [System.Drawing.Bitmap]::new(960, 640, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$coverGraphics = [System.Drawing.Graphics]::FromImage($cover)
$coverGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$coverRectangle = [System.Drawing.Rectangle]::new(0, 0, 960, 640)
$coverBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($coverRectangle, [System.Drawing.ColorTranslator]::FromHtml('#183D3A'), [System.Drawing.ColorTranslator]::FromHtml('#44331D'), 28)
$coverGraphics.FillRectangle($coverBrush, $coverRectangle)
for ($index = 0; $index -lt 7; $index++) {
    $alpha = 35 + $index * 9
    $coverGraphics.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($alpha,244,201,107)), 80 + $index * 118, 96 + ($index % 2) * 70, 250, 250)
}
$coverLogo = New-LogoBitmap 180
$coverGraphics.DrawImage($coverLogo, 390, 178, 180, 180)
$coverLogo.Dispose()
$coverGraphics.Dispose()
$cover.Save((Join-Path $assetDirectory 'WorkbenchProjectCover.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$cover.Dispose()

$streams = [System.Collections.Generic.List[byte[]]]::new()
foreach ($path in $pngFiles) {
    $streams.Add([System.IO.File]::ReadAllBytes($path))
}
$iconPath = Join-Path $assetDirectory 'AppIcon.ico'
$stream = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$streams[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $streams[$index].Length
}
foreach ($bytes in $streams) { $writer.Write($bytes) }
$writer.Dispose()
$stream.Dispose()
