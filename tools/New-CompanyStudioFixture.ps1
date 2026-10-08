param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/company-repair-2026-10-08/inputs'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
# Original mathematical test rasters, dedicated to the public domain (CC0).
# These test geometry, range mapping and repeatability, not photographic quality.
foreach ($spec in @(@('chart',1600,1000), @('portrait',1000,1600), @('square',1000,1000))) {
    $bitmap = [Drawing.Bitmap]::new([int]$spec[1],[int]$spec[2])
    $g = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.Clear([Drawing.Color]::Gray)
        for ($i=0; $i -lt 16; $i++) {
            $v = $i * 17
            $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($v,$v,$v))
            $g.FillRectangle($brush, $i*$bitmap.Width/16, 0, $bitmap.Width/16+1, $bitmap.Height/3)
            $brush.Dispose()
        }
        $colors = @('Black','White','Red','Lime','Blue','Cyan','Magenta','Yellow','Sienna','SteelBlue')
        for ($i=0; $i -lt $colors.Count; $i++) {
            $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromName($colors[$i]))
            $g.FillRectangle($brush, $i*$bitmap.Width/10, $bitmap.Height*2/3, $bitmap.Width/10+1, $bitmap.Height/3+1)
            $brush.Dispose()
        }
        for ($i=0; $i -lt 320; $i++) {
            $r=[int](127.5+127.5*[Math]::Sin($i/320.0*2*[Math]::PI))
            $b=[int](127.5+127.5*[Math]::Sin($i/320.0*2*[Math]::PI+2))
            $green=[int](127.5+127.5*[Math]::Sin($i/320.0*2*[Math]::PI+4))
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($r,$green,$b))
            $g.FillRectangle($brush,$i*$bitmap.Width/320,$bitmap.Height/3,$bitmap.Width/320+1,$bitmap.Height/3+1)
            $brush.Dispose()
        }
        $bitmap.Save((Join-Path $OutputDirectory ($spec[0]+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Save((Join-Path $OutputDirectory ($spec[0]+'.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
        if ($spec[0] -eq 'chart') { $bitmap.Save((Join-Path $OutputDirectory 'chart.tif'),[Drawing.Imaging.ImageFormat]::Tiff) }
    } finally { $g.Dispose(); $bitmap.Dispose() }
}
Get-ChildItem -LiteralPath $OutputDirectory -File | Get-FileHash -Algorithm SHA256
