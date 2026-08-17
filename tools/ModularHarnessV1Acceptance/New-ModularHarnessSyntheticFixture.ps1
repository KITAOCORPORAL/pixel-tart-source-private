[CmdletBinding()]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

function Write-Utf8WithoutBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Convert-HexColor {
    param([string]$Value)
    return [System.Drawing.ColorTranslator]::FromHtml($Value)
}

try { Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop }
catch { Add-Type -AssemblyName System.Drawing -ErrorAction Stop }

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $temporaryRoot (Join-Path 'PixelTart_ModularHarness_V1_Acceptance' ("SyntheticFixture-{0}-{1}" -f [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N')))
}
$fixtureRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if ($fixtureRoot.Equals($temporaryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase) -or
    -not $fixtureRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Synthetic fixture output must be a new subdirectory under the Windows temporary root: $temporaryRoot"
}
if (Test-Path -LiteralPath $fixtureRoot) {
    throw "Synthetic fixture output already exists; refusing to overwrite or delete it: $fixtureRoot"
}

$imagesRoot = Join-Path $fixtureRoot 'images'
[System.IO.Directory]::CreateDirectory($imagesRoot) | Out-Null
$definitions = @(
    [pscustomobject]@{ File='01_warm_low_key.jpg'; Tone='low'; Contrast='high'; Start='#180B08'; End='#7A2016'; Accent='#FFC15A' },
    [pscustomobject]@{ File='02_cool_high_key.jpg'; Tone='high'; Contrast='low'; Start='#DFF6FF'; End='#8CC8E8'; Accent='#477AA8' },
    [pscustomobject]@{ File='03_red_mid_high_contrast.jpg'; Tone='mid'; Contrast='high'; Start='#2A0608'; End='#D92332'; Accent='#FFF3E0' },
    [pscustomobject]@{ File='04_green_low_medium_contrast.jpg'; Tone='low'; Contrast='medium'; Start='#071B13'; End='#237A4B'; Accent='#9BE58C' },
    [pscustomobject]@{ File='05_blue_mid_high_contrast.jpg'; Tone='mid'; Contrast='high'; Start='#06162E'; End='#1769C2'; Accent='#F7D34A' },
    [pscustomobject]@{ File='06_cyan_high_medium_contrast.jpg'; Tone='high'; Contrast='medium'; Start='#D6FFF9'; End='#37B8C7'; Accent='#13627A' },
    [pscustomobject]@{ File='07_magenta_low_high_contrast.jpg'; Tone='low'; Contrast='high'; Start='#21051E'; End='#B01885'; Accent='#FFD3F2' },
    [pscustomobject]@{ File='08_amber_high_low_contrast.jpg'; Tone='high'; Contrast='low'; Start='#FFF0B8'; End='#E6A62E'; Accent='#9A5815' },
    [pscustomobject]@{ File='09_violet_mid_medium_contrast.jpg'; Tone='mid'; Contrast='medium'; Start='#281544'; End='#7957BD'; Accent='#9CF1E7' },
    [pscustomobject]@{ File='10_teal_low_medium_contrast.jpg'; Tone='low'; Contrast='medium'; Start='#06201F'; End='#147D78'; Accent='#F29B76' },
    [pscustomobject]@{ File='11_neutral_high_high_contrast.jpg'; Tone='high'; Contrast='high'; Start='#F5F5F2'; End='#777A80'; Accent='#15171A' },
    [pscustomobject]@{ File='12_complementary_mid_high_contrast.jpg'; Tone='mid'; Contrast='high'; Start='#153C75'; End='#F06A22'; Accent='#F7E64A' }
)

$width = 960
$height = 640
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg' | Select-Object -First 1
if ($null -eq $codec) { throw 'Windows JPEG encoder is unavailable.' }
$items = @()
for ($index = 0; $index -lt $definitions.Count; $index++) {
    $definition = $definitions[$index]
    $path = Join-Path $imagesRoot $definition.File
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite synthetic image: $path" }

    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $rectangle = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
            $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
                $rectangle,
                (Convert-HexColor $definition.Start),
                (Convert-HexColor $definition.End),
                [single](18 + ($index * 13)))
            try { $graphics.FillRectangle($gradient, $rectangle) }
            finally { $gradient.Dispose() }

            $accent = Convert-HexColor $definition.Accent
            $contrastAlpha = switch ($definition.Contrast) { 'high' { 238 } 'medium' { 174 } default { 96 } }
            $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($contrastAlpha, $accent))
            $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(82, 0, 0, 0))
            try {
                $diameter = 180 + (($index % 4) * 42)
                $graphics.FillEllipse($shadowBrush, 90 + ($index * 23), 70 + (($index % 3) * 96), $diameter + 36, $diameter + 36)
                $graphics.FillEllipse($accentBrush, 72 + ($index * 23), 52 + (($index % 3) * 96), $diameter, $diameter)
                $points = [System.Drawing.Point[]]@(
                    [System.Drawing.Point]::new(510, 80 + (($index % 4) * 26)),
                    [System.Drawing.Point]::new(890, 160 + (($index % 3) * 54)),
                    [System.Drawing.Point]::new(760, 560),
                    [System.Drawing.Point]::new(440, 470)
                )
                $graphics.FillPolygon($accentBrush, $points)
            }
            finally {
                $accentBrush.Dispose()
                $shadowBrush.Dispose()
            }
        }
        finally { $graphics.Dispose() }

        $encoderParameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
        try {
            $encoderParameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, [long]92)
            $bitmap.Save($path, $codec, $encoderParameters)
        }
        finally { $encoderParameters.Dispose() }
    }
    finally { $bitmap.Dispose() }

    $bytes = [System.IO.File]::ReadAllBytes($path)
    $latin = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes)
    if ($latin.IndexOf("Exif`0`0", [StringComparison]::Ordinal) -ge 0) {
        throw "Unexpected EXIF payload in generated fixture: $path"
    }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha256.Dispose() }
    $items += [ordered]@{
        relative_path = ('images/' + $definition.File)
        sha256 = $hash
        byte_count = $bytes.Length
        width = $width
        height = $height
        tone = $definition.Tone
        contrast = $definition.Contrast
        start_color = $definition.Start
        end_color = $definition.End
        accent_color = $definition.Accent
        exif_present = $false
    }
}

$manifest = [ordered]@{
    schema = 'pixel-tart-modular-harness-v1-synthetic-fixture/v1'
    synthetic_only = $true
    customer_media = $false
    generated_count = $items.Count
    created_at = [DateTimeOffset]::UtcNow.ToString('O')
    files = $items
}
$manifestPath = Join-Path $fixtureRoot 'fixture-manifest.json'
Write-Utf8WithoutBom -Path $manifestPath -Content ($manifest | ConvertTo-Json -Depth 6)
[pscustomobject]@{
    fixture_root = $fixtureRoot
    manifest_path = $manifestPath
    generated_count = $items.Count
}
