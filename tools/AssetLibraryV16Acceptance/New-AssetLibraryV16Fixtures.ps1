[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot,

    [ValidateRange(1, 1000)]
    [int]$PerformanceCount = 1000,

    [ValidateRange(64, 4096)]
    [int]$PerformanceWidth = 1024,

    [ValidateRange(64, 4096)]
    [int]$PerformanceHeight = 640,

    [ValidateRange(1, 100)]
    [int]$JpegQuality = 88
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Get-NormalizedDirectoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Assert-SafeOutputRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalizedOutput = Get-NormalizedDirectoryPath -Path $Path
    $normalizedTemp = Get-NormalizedDirectoryPath -Path ([IO.Path]::GetTempPath())
    $tempPrefix = $normalizedTemp + [IO.Path]::DirectorySeparatorChar

    if (-not $normalizedOutput.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputRoot must be an explicit child directory of the system temporary directory.'
    }

    if ($normalizedOutput -eq $normalizedTemp -or $normalizedOutput -eq [IO.Path]::GetPathRoot($normalizedOutput).TrimEnd('\', '/')) {
        throw 'OutputRoot must not be a broad temporary or drive root.'
    }

    if (Test-Path -LiteralPath $normalizedOutput) {
        $existing = Get-ChildItem -LiteralPath $normalizedOutput -Force | Select-Object -First 1
        if ($null -ne $existing) {
            throw 'OutputRoot must be new or empty. Existing files are never overwritten or deleted.'
        }
    }

    return $normalizedOutput
}

function Get-JpegCodec {
    return [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
        Where-Object { $_.MimeType -eq 'image/jpeg' } |
        Select-Object -First 1
}

function Save-Jpeg {
    param(
        [Parameter(Mandatory = $true)][Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Quality
    )

    $codec = Get-JpegCodec
    if ($null -eq $codec) {
        throw 'The Windows JPEG encoder is unavailable.'
    }

    $parameters = [Drawing.Imaging.EncoderParameters]::new(1)
    try {
        $parameters.Param[0] = [Drawing.Imaging.EncoderParameter]::new(
            [Drawing.Imaging.Encoder]::Quality,
            [long]$Quality)
        $Bitmap.Save($Path, $codec, $parameters)
    }
    finally {
        $parameters.Dispose()
    }
}

function New-ColorFixture {
    param([Parameter(Mandatory = $true)][string]$Path)

    $width = 1600
    $height = 1000
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $segments = @(
            @{ X = 0; Width = 880; Color = [Drawing.Color]::FromArgb(28, 105, 82) },
            @{ X = 880; Width = 400; Color = [Drawing.Color]::FromArgb(214, 147, 48) },
            @{ X = 1280; Width = 240; Color = [Drawing.Color]::FromArgb(72, 82, 96) },
            @{ X = 1520; Width = 80; Color = [Drawing.Color]::FromArgb(207, 55, 78) }
        )

        foreach ($segment in $segments) {
            $brush = [Drawing.SolidBrush]::new($segment.Color)
            try {
                $graphics.FillRectangle($brush, $segment.X, 0, $segment.Width, $height)
            }
            finally {
                $brush.Dispose()
            }
        }

        Save-Jpeg -Bitmap $bitmap -Path $Path -Quality $JpegQuality
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return [ordered]@{
        width = $width
        height = $height
        expected = [ordered]@{
            purpose = 'palette_weight_reference'
            intended_rgb_weights = @(
                [ordered]@{ rgb = '#1C6952'; weight = 0.55 },
                [ordered]@{ rgb = '#D69330'; weight = 0.25 },
                [ordered]@{ rgb = '#485260'; weight = 0.15 },
                [ordered]@{ rgb = '#CF374E'; weight = 0.05 }
            )
        }
    }
}

function New-HistogramFixture {
    param([Parameter(Mandatory = $true)][string]$Path)

    $width = 1600
    $height = 1000
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $contentTop = 20
        $contentHeight = 960
        for ($index = 0; $index -lt 256; $index++) {
            $left = [int][Math]::Floor($index * $width / 256.0)
            $right = [int][Math]::Floor(($index + 1) * $width / 256.0)
            $color = [Drawing.Color]::FromArgb($index, 255 - $index, ($index * 37) % 256)
            $brush = [Drawing.SolidBrush]::new($color)
            try {
                $graphics.FillRectangle($brush, $left, $contentTop, [Math]::Max(1, $right - $left), $contentHeight)
            }
            finally {
                $brush.Dispose()
            }
        }

        $graphics.FillRectangle([Drawing.Brushes]::Black, 0, 0, $width, 20)
        $graphics.FillRectangle([Drawing.Brushes]::White, 0, 980, $width, 20)
        Save-Jpeg -Bitmap $bitmap -Path $Path -Quality $JpegQuality
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return [ordered]@{
        width = $width
        height = $height
        expected = [ordered]@{
            purpose = 'rgb_luma_histogram_reference'
            intended_black_area_ratio = 0.02
            intended_white_area_ratio = 0.02
            note = 'JPEG compression requires tolerance; the fixture is not an exact-bin oracle.'
        }
    }
}

function New-ToneFixture {
    param([Parameter(Mandatory = $true)][string]$Path)

    $width = 1600
    $height = 1000
    $values = @(8, 45, 105, 180, 248)
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        for ($index = 0; $index -lt $values.Count; $index++) {
            $value = $values[$index]
            $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($value, $value, $value))
            try {
                $graphics.FillRectangle($brush, 0, $index * 200, $width, 200)
            }
            finally {
                $brush.Dispose()
            }
        }

        Save-Jpeg -Bitmap $bitmap -Path $Path -Quality $JpegQuality
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return [ordered]@{
        width = $width
        height = $height
        expected = [ordered]@{
            purpose = 'five_tone_band_reference'
            intended_gray_values = $values
            intended_area_ratio_per_band = 0.20
            note = 'Production tone thresholds are linear-light; validate with documented tolerance.'
        }
    }
}

function New-PerformanceFixture {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Ordinal
    )

    $bitmap = [Drawing.Bitmap]::new($PerformanceWidth, $PerformanceHeight, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $background = [Drawing.Color]::FromArgb(
            ($Ordinal * 37 + 29) % 256,
            ($Ordinal * 67 + 71) % 256,
            ($Ordinal * 97 + 113) % 256)
        $graphics.Clear($background)

        for ($band = 0; $band -lt 8; $band++) {
            $left = [int][Math]::Floor($band * $PerformanceWidth / 8.0)
            $right = [int][Math]::Floor(($band + 1) * $PerformanceWidth / 8.0)
            $color = [Drawing.Color]::FromArgb(
                ($Ordinal * 19 + $band * 31) % 256,
                ($Ordinal * 23 + $band * 47) % 256,
                ($Ordinal * 29 + $band * 59) % 256)
            $brush = [Drawing.SolidBrush]::new($color)
            try {
                $graphics.FillRectangle($brush, $left, 0, [Math]::Max(1, $right - $left), $PerformanceHeight)
            }
            finally {
                $brush.Dispose()
            }
        }

        $ellipseBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(
            220,
            ($Ordinal * 41) % 256,
            ($Ordinal * 53) % 256))
        try {
            $diameter = [Math]::Max(32, [Math]::Min($PerformanceWidth, $PerformanceHeight) / 3)
            $xRange = [Math]::Max(1, $PerformanceWidth - $diameter)
            $yRange = [Math]::Max(1, $PerformanceHeight - $diameter)
            $x = ($Ordinal * 43) % $xRange
            $y = ($Ordinal * 61) % $yRange
            $graphics.FillEllipse($ellipseBrush, $x, $y, $diameter, $diameter)
        }
        finally {
            $ellipseBrush.Dispose()
        }

        Save-Jpeg -Bitmap $bitmap -Path $Path -Quality $JpegQuality
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return [ordered]@{
        width = $PerformanceWidth
        height = $PerformanceHeight
        expected = [ordered]@{
            purpose = 'full_pipeline_performance_input'
            ordinal = $Ordinal
        }
    }
}

function Add-ManifestEntry {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$Entries,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][Collections.IDictionary]$Descriptor,
        [int]$Ordinal = -1
    )

    $normalizedRoot = Get-NormalizedDirectoryPath -Path $Root
    $normalizedPath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $normalizedRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $normalizedPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Fixture path escaped the explicit output root.'
    }
    $relativePath = $normalizedPath.Substring($rootPrefix.Length).Replace('\', '/')
    $file = Get-Item -LiteralPath $Path
    $entry = [ordered]@{
        relative_path = $relativePath
        category = $Category
        width = $Descriptor.width
        height = $Descriptor.height
        bytes = $file.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
        expected = $Descriptor.expected
    }
    if ($Ordinal -ge 0) {
        $entry.ordinal = $Ordinal
    }
    $Entries.Add($entry)
}

$resolvedOutput = Assert-SafeOutputRoot -Path $OutputRoot
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

$phaseRoot = Join-Path $resolvedOutput 'phase0'
$performanceRoot = Join-Path $resolvedOutput 'performance'
New-Item -ItemType Directory -Path $phaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $performanceRoot -Force | Out-Null

$entries = [Collections.Generic.List[object]]::new()

$colorPath = Join-Path $phaseRoot 'color_blocks.jpg'
$color = New-ColorFixture -Path $colorPath
Add-ManifestEntry -Entries $entries -Root $resolvedOutput -Path $colorPath -Category 'phase0_palette' -Descriptor $color

$histogramPath = Join-Path $phaseRoot 'histogram_gradient.jpg'
$histogram = New-HistogramFixture -Path $histogramPath
Add-ManifestEntry -Entries $entries -Root $resolvedOutput -Path $histogramPath -Category 'phase0_histogram' -Descriptor $histogram

$tonePath = Join-Path $phaseRoot 'tone_bands.jpg'
$tone = New-ToneFixture -Path $tonePath
Add-ManifestEntry -Entries $entries -Root $resolvedOutput -Path $tonePath -Category 'phase0_tone' -Descriptor $tone

for ($ordinal = 0; $ordinal -lt $PerformanceCount; $ordinal++) {
    $path = Join-Path $performanceRoot ('asset_{0:D4}.jpg' -f $ordinal)
    $descriptor = New-PerformanceFixture -Path $path -Ordinal $ordinal
    Add-ManifestEntry -Entries $entries -Root $resolvedOutput -Path $path -Category 'performance' -Descriptor $descriptor -Ordinal $ordinal
}

$cohorts = [Collections.Generic.List[object]]::new()
foreach ($count in @(100, 1000)) {
    if ($PerformanceCount -ge $count) {
        $cohorts.Add([ordered]@{
            count = $count
            selection = "performance ordinals 0-$($count - 1)"
        })
    }
}

$manifest = [ordered]@{
    schema = 'pixel-tart-asset-library-v16-fixtures/v1'
    generator_version = '1.0.0'
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    contains_customer_media = $false
    contains_source_paths = $false
    metadata_policy = 'Fresh RGB JPEG encoding only; no source EXIF, XMP, GPS, profile or path metadata is copied.'
    icc_reference_included = $false
    raw_embedded_preview_included = $false
    performance_count = $PerformanceCount
    performance_cohorts = $cohorts
    fixtures = $entries
}

$manifestPath = Join-Path $resolvedOutput 'fixtures.manifest.json'
$json = $manifest | ConvertTo-Json -Depth 12
[IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    OutputRoot = $resolvedOutput
    ManifestPath = $manifestPath
    FixtureCount = $entries.Count
    ICCReferenceIncluded = $false
    RawEmbeddedPreviewIncluded = $false
}
