[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RunRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [Collections.Generic.List[string]]::new()
$requiredHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Add-Failure {
    param([string]$Message)
    $script:failures.Add($Message)
}

function Get-PropertyValue {
    param(
        [AllowNull()]$InputObject,
        [string]$Name
    )
    if ($null -eq $InputObject) { return $null }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-PropertyState {
    param(
        [AllowNull()]$InputObject,
        [string]$Name
    )
    if ($null -eq $InputObject) {
        return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
    }
    if ($InputObject -is [Collections.IDictionary]) {
        if (-not $InputObject.Contains($Name)) {
            return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
        }
        $value = $InputObject[$Name]
    }
    else {
        $property = $InputObject.PSObject.Properties[$Name]
        if ($null -eq $property) {
            return [pscustomobject]@{ Present = $false; IsArray = $false; Value = $null }
        }
        $value = $property.Value
    }
    return [pscustomobject]@{ Present = $true; IsArray = $value -is [Array]; Value = $value }
}

function Test-StrictScalarPropertyValue {
    param(
        [AllowNull()]$InputObject,
        [string]$Name,
        [Type]$ExpectedType,
        [AllowNull()]$ExpectedValue
    )
    $state = Get-PropertyState -InputObject $InputObject -Name $Name
    return $state.Present -and
        -not $state.IsArray -and
        $null -ne $state.Value -and
        $state.Value.GetType() -eq $ExpectedType -and
        [object]::Equals($state.Value, $ExpectedValue)
}

function Test-PropertyPresent {
    param(
        [AllowNull()]$InputObject,
        [string]$Name
    )
    if ($null -eq $InputObject) { return $false }
    if ($InputObject -is [Collections.IDictionary]) { return $InputObject.Contains($Name) }
    return $null -ne $InputObject.PSObject.Properties[$Name]
}

function Get-NestedValue {
    param(
        [AllowNull()]$InputObject,
        [string[]]$Path
    )
    $current = $InputObject
    foreach ($name in $Path) {
        $current = Get-PropertyValue -InputObject $current -Name $name
        if ($null -eq $current) { return $null }
    }
    return $current
}

function Require-True {
    param(
        [bool]$Condition,
        [string]$Message
    )
    if (-not $Condition) { Add-Failure $Message }
}

function Require-Equal {
    param(
        [AllowNull()]$Actual,
        [AllowNull()]$Expected,
        [string]$Message
    )
    if ($null -eq $Actual -or $null -eq $Expected -or -not [object]::Equals($Actual, $Expected)) {
        Add-Failure "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Test-ExactString {
    param(
        [AllowNull()]$Actual,
        [AllowNull()]$Expected
    )
    return $null -ne $Actual -and $null -ne $Expected -and
        [string]::Equals([string]$Actual, [string]$Expected, [StringComparison]::Ordinal)
}

function Test-TrueValue {
    param([AllowNull()]$Value)
    return $null -ne $Value -and [bool]$Value
}

function Test-LowercaseCommitSha {
    param([AllowNull()]$Value)
    return $null -ne $Value -and [string]$Value -cmatch '^[0-9a-f]{40}$'
}

function Test-UppercaseSha256 {
    param([AllowNull()]$Value)
    return $null -ne $Value -and [string]$Value -cmatch '^[0-9A-F]{64}$'
}

function Test-SameFullPath {
    param(
        [AllowNull()][string]$First,
        [AllowNull()][string]$Second
    )
    if (-not (Test-FullyQualifiedPath $First) -or -not (Test-FullyQualifiedPath $Second)) { return $false }
    return [string]::Equals(
        [IO.Path]::GetFullPath($First),
        [IO.Path]::GetFullPath($Second),
        [StringComparison]::OrdinalIgnoreCase)
}

function Try-GetTimestamp {
    param(
        [AllowNull()]$Value,
        [ref]$Timestamp
    )
    $parsed = [DateTimeOffset]::MinValue
    if ($null -eq $Value -or -not [DateTimeOffset]::TryParse([string]$Value, [ref]$parsed)) { return $false }
    $Timestamp.Value = $parsed
    return $true
}

function Read-JsonFile {
    param([string]$Path)
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Add-Failure "Invalid JSON in '$Path': $($_.Exception.Message)"
        return $null
    }
}

function Convert-ToTimestamp {
    param(
        [AllowNull()]$Value,
        [string]$Context
    )
    $parsed = [DateTimeOffset]::MinValue
    if ($null -eq $Value -or -not [DateTimeOffset]::TryParse([string]$Value, [ref]$parsed)) {
        Add-Failure "$Context must contain an ISO-8601 timestamp."
        return [DateTimeOffset]::MinValue
    }
    return $parsed
}

function Test-PathInsideRoot {
    param(
        [string]$Path,
        [string]$Root
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-PathAtOrInsideRoot {
    param(
        [string]$Path,
        [string]$Root
    )
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return [string]::Equals($fullPath, $fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Test-PathInsideRoot $fullPath $fullRoot)
}

function Test-FullyQualifiedPath {
    param([AllowNull()][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    return $Path -match '^(?:(?:[A-Za-z]:[\\/])|(?:\\\\[^\\/]+[\\/][^\\/]+(?:[\\/]|$)))'
}

function Get-PngUInt32 {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )
    return [long]$Bytes[$Offset] * 16777216L +
        [long]$Bytes[$Offset + 1] * 65536L +
        [long]$Bytes[$Offset + 2] * 256L +
        [long]$Bytes[$Offset + 3]
}

function Get-PngCrc32 {
    param(
        [byte[]]$Bytes,
        [int]$Offset,
        [int]$Count
    )
    [long]$crc = 0xFFFFFFFFL
    for ($index = $Offset; $index -lt $Offset + $Count; $index++) {
        $crc = ($crc -bxor [long]$Bytes[$index]) -band 0xFFFFFFFFL
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 1L) -ne 0) {
                $crc = (($crc -shr 1) -bxor 0xEDB88320L) -band 0xFFFFFFFFL
            }
            else {
                $crc = ($crc -shr 1) -band 0xFFFFFFFFL
            }
        }
    }
    return (-bnot $crc) -band 0xFFFFFFFFL
}

function Test-PngBytes {
    param(
        [byte[]]$Bytes,
        [string]$Context
    )
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    if ($Bytes.Length -lt 8) {
        Add-Failure "$Context is not a PNG: the file is shorter than the PNG signature."
        return $null
    }
    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($Bytes[$index] -ne $signature[$index]) {
            Add-Failure "$Context has an invalid PNG signature."
            return $null
        }
    }

    $offset = 8
    $chunkIndex = 0
    $width = 0L
    $height = 0L
    $colorType = -1
    $sawIdat = $false
    $idatBytes = 0L
    $idatEnded = $false
    $sawIend = $false
    while ($offset + 12 -le $Bytes.Length) {
        $length = Get-PngUInt32 $Bytes $offset
        if ($length -gt [int]::MaxValue -or [long]$offset + 12L + $length -gt $Bytes.Length) {
            Add-Failure "$Context has an invalid PNG chunk length."
            return $null
        }
        $chunkType = [Text.Encoding]::ASCII.GetString($Bytes, $offset + 4, 4)
        if ($chunkType -notmatch '^[A-Za-z]{4}$') {
            Add-Failure "$Context has an invalid PNG chunk type."
            return $null
        }
        $expectedCrc = Get-PngUInt32 $Bytes ([int]($offset + 8 + $length))
        $actualCrc = Get-PngCrc32 $Bytes ($offset + 4) ([int]($length + 4))
        if ($actualCrc -ne $expectedCrc) {
            Add-Failure "$Context PNG chunk '$chunkType' has a CRC mismatch."
            return $null
        }
        if ($chunkType -in @('tEXt', 'zTXt', 'iTXt', 'eXIf')) {
            Add-Failure "$Context contains forbidden textual or EXIF metadata chunk '$chunkType'."
            return $null
        }
        if ([char]::IsUpper($chunkType[0]) -and $chunkType -notin @('IHDR', 'PLTE', 'IDAT', 'IEND')) {
            Add-Failure "$Context contains unknown critical PNG chunk '$chunkType'."
            return $null
        }

        if ($chunkIndex -eq 0) {
            if ($chunkType -cne 'IHDR' -or $length -ne 13) {
                Add-Failure "$Context does not begin with one 13-byte IHDR chunk."
                return $null
            }
            $width = Get-PngUInt32 $Bytes ($offset + 8)
            $height = Get-PngUInt32 $Bytes ($offset + 12)
            $bitDepth = [int]$Bytes[$offset + 16]
            $colorType = [int]$Bytes[$offset + 17]
            $validBitDepth = switch ($colorType) {
                0 { $bitDepth -in @(1, 2, 4, 8, 16) }
                2 { $bitDepth -in @(8, 16) }
                3 { $bitDepth -in @(1, 2, 4, 8) }
                4 { $bitDepth -in @(8, 16) }
                6 { $bitDepth -in @(8, 16) }
                default { $false }
            }
            if ($width -le 0 -or $height -le 0 -or -not $validBitDepth -or
                $Bytes[$offset + 18] -ne 0 -or $Bytes[$offset + 19] -ne 0 -or
                $Bytes[$offset + 20] -notin @(0, 1)) {
                Add-Failure "$Context has invalid IHDR dimensions or encoding fields."
                return $null
            }
        }
        elseif ($chunkType -ceq 'IHDR') {
            Add-Failure "$Context contains more than one IHDR chunk."
            return $null
        }

        if ($chunkType -ceq 'IDAT') {
            if ($idatEnded) {
                Add-Failure "$Context contains non-consecutive IDAT chunks."
                return $null
            }
            $sawIdat = $true
            $idatBytes += $length
        }
        elseif ($sawIdat -and $chunkType -cne 'IEND') {
            $idatEnded = $true
        }

        $offset = [int]($offset + 12 + $length)
        $chunkIndex++
        if ($chunkType -ceq 'IEND') {
            if ($length -ne 0 -or $offset -ne $Bytes.Length) {
                Add-Failure "$Context has a non-empty IEND chunk or trailing bytes."
                return $null
            }
            $sawIend = $true
            break
        }
    }

    if (-not $sawIdat -or $idatBytes -le 0 -or -not $sawIend) {
        Add-Failure "$Context does not contain non-empty IDAT data followed by a complete IEND chunk."
        return $null
    }
    return [pscustomobject]@{ Width = [int]$width; Height = [int]$height }
}

function Get-WindowRecordSummary {
    param(
        $Record,
        [AllowNull()]$ExpectedDisplay
    )
    $json = $Record.Json
    $context = "window evidence '$($Record.Name)'"
    $captureName = Get-PropertyValue $json 'capture_name'
    $expectedExecutable = Get-NestedValue $json @('expected', 'executable_path')
    $observedExecutable = Get-NestedValue $json @('process', 'executable_path')
    $expectedTitle = Get-NestedValue $json @('expected', 'window_title')
    $expectedHwnd = Get-NestedValue $json @('expected', 'window_hwnd')
    $beforeTitle = Get-NestedValue $json @('window_before_capture', 'title')
    $afterTitle = Get-NestedValue $json @('window_after_capture', 'title')
    $beforeHwnd = Get-NestedValue $json @('window_before_capture', 'hwnd')
    $afterHwnd = Get-NestedValue $json @('window_after_capture', 'hwnd')
    $processId = Get-NestedValue $json @('process', 'process_id')
    $expectedProcessId = Get-NestedValue $json @('expected', 'process_id')
    $beforeDpi = Get-NestedValue $json @('window_before_capture', 'dpi')
    $afterDpi = Get-NestedValue $json @('window_after_capture', 'dpi')
    $preCaptureGate = Get-PropertyValue $json 'pre_capture_gate'

    Require-True (Test-ExactString (Get-PropertyValue $json 'schema') $script:contract.window_evidence_schema) "$context has the wrong schema."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$captureName)) "$context is missing capture_name."
    Require-True (Test-PropertyPresent $json 'ui_input_generated') "$context is missing ui_input_generated."
    Require-Equal (Get-PropertyValue $json 'ui_input_generated') $false "$context ui_input_generated must be false."
    Require-True (Test-PropertyPresent $json 'synthetic_ui_events_generated') "$context is missing synthetic_ui_events_generated."
    Require-Equal (Get-PropertyValue $json 'synthetic_ui_events_generated') $false "$context synthetic_ui_events_generated must be false."
    Require-True ($null -ne $preCaptureGate) "$context is missing pre_capture_gate."
    if ($null -ne $preCaptureGate) {
        foreach ($field in @('timeout_seconds', 'required_stable_milliseconds', 'elapsed_milliseconds', 'poll_count', 'passed', 'exact_original_hwnd_required', 'ui_activation_attempted')) {
            Require-True (Test-PropertyPresent $preCaptureGate $field) "$context pre_capture_gate is missing $field."
        }
        $gateTimeoutSeconds = Get-PropertyValue $preCaptureGate 'timeout_seconds'
        $gateStableMilliseconds = Get-PropertyValue $preCaptureGate 'required_stable_milliseconds'
        $gateElapsedMilliseconds = Get-PropertyValue $preCaptureGate 'elapsed_milliseconds'
        $gatePollCount = Get-PropertyValue $preCaptureGate 'poll_count'
        Require-True ($null -ne $gateTimeoutSeconds -and [int]$gateTimeoutSeconds -ge 1 -and [int]$gateTimeoutSeconds -le 1800) "$context pre_capture_gate.timeout_seconds is outside the contract range."
        Require-True ($null -ne $gateStableMilliseconds -and [int]$gateStableMilliseconds -ge 100 -and [int]$gateStableMilliseconds -le 5000) "$context pre_capture_gate.required_stable_milliseconds is outside the contract range."
        Require-True ($null -ne $gateTimeoutSeconds -and $null -ne $gateStableMilliseconds -and [int]$gateStableMilliseconds -le ([int]$gateTimeoutSeconds * 1000)) "$context pre_capture_gate requires a stable interval longer than its timeout."
        Require-True ($null -ne $gateElapsedMilliseconds -and [double]$gateElapsedMilliseconds -ge [double]$gateStableMilliseconds) "$context pre_capture_gate elapsed time is shorter than its required stable interval."
        Require-True ($null -ne $gatePollCount -and [int]$gatePollCount -ge 1) "$context pre_capture_gate.poll_count proves no stability polling."
        Require-Equal (Get-PropertyValue $preCaptureGate 'passed') $true "$context pre_capture_gate.passed must be true."
        Require-Equal (Get-PropertyValue $preCaptureGate 'exact_original_hwnd_required') $true "$context pre_capture_gate.exact_original_hwnd_required must be true."
        Require-Equal (Get-PropertyValue $preCaptureGate 'ui_activation_attempted') $false "$context pre_capture_gate.ui_activation_attempted must be false."
    }
    Require-Equal $processId $expectedProcessId "$context PID identity mismatch."
    Require-True (Test-ExactString $expectedExecutable $observedExecutable) "$context executable path identity mismatch."
    Require-True (Test-ExactString ([IO.Path]::GetFileName([string]$observedExecutable)) $script:contract.expected_executable_name) "$context executable name is not the contract executable."
    Require-True (Test-ExactString (Get-NestedValue $json @('process', 'executable_name')) $script:contract.expected_executable_name) "$context process executable_name is wrong."
    Require-True (Test-ExactString $expectedTitle $script:contract.expected_window_title) "$context expected title is wrong."
    Require-True (Test-ExactString $beforeTitle $expectedTitle) "$context before-capture title mismatch."
    Require-True (Test-ExactString $afterTitle $expectedTitle) "$context after-capture title mismatch."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$beforeHwnd)) "$context is missing HWND."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$expectedHwnd)) "$context is missing expected session HWND."
    Require-True (Test-ExactString $expectedHwnd $beforeHwnd) "$context expected session HWND differs from the captured HWND."
    Require-True (Test-ExactString $beforeHwnd $afterHwnd) "$context HWND changed during capture."
    Require-Equal $beforeDpi $afterDpi "$context DPI changed during capture."
    foreach ($field in @('left', 'top', 'right', 'bottom', 'width', 'height')) {
        Require-Equal (Get-NestedValue $json @('window_before_capture', 'rect_physical_pixels', $field)) (Get-NestedValue $json @('window_after_capture', 'rect_physical_pixels', $field)) "$context window rectangle changed during capture."
    }
    Require-True (Test-TrueValue (Get-NestedValue $json @('window_before_capture', 'is_foreground'))) "$context target was not foreground before capture."
    Require-True (Test-TrueValue (Get-NestedValue $json @('window_after_capture', 'is_foreground'))) "$context target was not foreground after capture."
    Require-True (-not (Test-TrueValue (Get-NestedValue $json @('window_before_capture', 'is_minimized')))) "$context target was minimized before capture."
    Require-True (-not (Test-TrueValue (Get-NestedValue $json @('window_after_capture', 'is_minimized')))) "$context target was minimized after capture."

    foreach ($path in @(
            @('process', 'global_matching_name_process_count'),
            @('process', 'global_matching_exact_path_process_count'),
            @('process', 'global_matching_name_process_count_after_capture'),
            @('process', 'global_matching_exact_path_process_count_after_capture'),
            @('exact_title_main_window_count_before_capture'),
            @('exact_title_main_window_count_after_capture'))) {
        Require-Equal (Get-NestedValue $json $path) $script:contract.expected_gui_process_count "$context single-GUI assertion '$($path -join '.')' failed."
    }
    Require-Equal (Get-PropertyValue $json 'unexpected_auxiliary_window_count') 0 "$context has an unexpected auxiliary window."
    Require-Equal (Get-PropertyValue $json 'unexpected_auxiliary_window_count_after_capture') 0 "$context gained an unexpected auxiliary window during capture."
    foreach ($field in @(
            'exact_pid_path_title_verified',
            'single_product_main_window_verified',
            'single_global_matching_process_verified',
            'exact_window_foreground_verified',
            'no_unapproved_auxiliary_window_during_capture',
            'window_stable_during_capture',
            'display_mode_and_scale_stable_during_capture')) {
        Require-True (Test-TrueValue (Get-NestedValue $json @('verification', $field))) "$context verification.$field is not true."
    }
    Require-True (Test-TrueValue (Get-NestedValue $json @('verification', 'passed'))) "$context verification.passed is not true."
    Require-True (Test-ExactString (Get-NestedValue $json @('dpi_awareness', 'observed_dpi_source')) 'GetDpiForWindow') "$context does not use GetDpiForWindow."

    foreach ($side in @('before_capture', 'after_capture')) {
        Require-True (Test-ExactString (Get-NestedValue $json @('display', $side, 'current_mode_source')) 'EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)') "$context $side does not use EnumDisplaySettingsExW."
        Require-True (Test-ExactString (Get-NestedValue $json @('display', $side, 'scale_factor_source')) 'GetScaleFactorForMonitor') "$context $side does not use GetScaleFactorForMonitor."
    }

    if ($null -ne $ExpectedDisplay) {
        foreach ($side in @('before_capture', 'after_capture')) {
            Require-Equal (Get-NestedValue $json @('display', $side, 'current_width_physical_pixels')) $ExpectedDisplay.width "$context $side width mismatch."
            Require-Equal (Get-NestedValue $json @('display', $side, 'current_height_physical_pixels')) $ExpectedDisplay.height "$context $side height mismatch."
            Require-Equal (Get-NestedValue $json @('display', $side, 'scale_factor_percent')) $ExpectedDisplay.scale_percent "$context $side scale mismatch."
            if ($null -ne (Get-PropertyValue $ExpectedDisplay 'refresh_rate_hz')) {
                Require-Equal (Get-NestedValue $json @('display', $side, 'current_refresh_rate_hz')) $ExpectedDisplay.refresh_rate_hz "$context $side refresh-rate mismatch."
            }
            else {
                Require-True ([int](Get-NestedValue $json @('display', $side, 'current_refresh_rate_hz')) -gt 0) "$context $side has no real refresh rate."
            }
        }
        Require-Equal $beforeDpi $ExpectedDisplay.dpi "$context GetDpiForWindow value mismatch."
    }

    $screenshotName = [string](Get-NestedValue $json @('screenshot', 'file_name'))
    $screenshotPath = $null
    $actualHash = $null
    $pngInfo = $null
    if ([string]::IsNullOrWhiteSpace($screenshotName) -or [IO.Path]::GetFileName($screenshotName) -cne $screenshotName) {
        Add-Failure "$context screenshot.file_name must be one file name."
    }
    else {
        $screenshotPath = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $Record.Path) $screenshotName))
        Require-True (Test-PathInsideRoot $screenshotPath $script:resolvedRunRoot) "$context screenshot escapes the run root."
        if (-not (Test-Path -LiteralPath $screenshotPath -PathType Leaf)) {
            Add-Failure "$context PNG is missing."
        }
        else {
            $actualHash = (Get-FileHash -LiteralPath $screenshotPath -Algorithm SHA256).Hash
            Require-True (Test-ExactString $actualHash (Get-NestedValue $json @('screenshot', 'sha256'))) "$context PNG SHA-256 does not match the file."
            Require-True (Test-TrueValue (Get-NestedValue $json @('screenshot', 'png_signature_verified'))) "$context did not record a verified PNG signature."
            $pngInfo = Test-PngBytes -Bytes ([IO.File]::ReadAllBytes($screenshotPath)) -Context $context
            if ($null -ne $pngInfo) {
                Require-Equal $pngInfo.Width (Get-NestedValue $json @('screenshot', 'width_physical_pixels')) "$context PNG IHDR width does not match the screenshot manifest."
                Require-Equal $pngInfo.Height (Get-NestedValue $json @('screenshot', 'height_physical_pixels')) "$context PNG IHDR height does not match the screenshot manifest."
                Require-Equal $pngInfo.Width (Get-NestedValue $json @('window_before_capture', 'rect_physical_pixels', 'width')) "$context PNG width does not match the captured window rectangle."
                Require-Equal $pngInfo.Height (Get-NestedValue $json @('window_before_capture', 'rect_physical_pixels', 'height')) "$context PNG height does not match the captured window rectangle."
            }
        }
        $recordedAbsolutePath = [string](Get-NestedValue $json @('screenshot', 'absolute_path'))
        if (-not (Test-FullyQualifiedPath $recordedAbsolutePath)) {
            Add-Failure "$context runtime screenshot.absolute_path is missing or not absolute."
        }
        elseif (-not [string]::Equals([IO.Path]::GetFullPath($recordedAbsolutePath), $screenshotPath, [StringComparison]::OrdinalIgnoreCase)) {
            Add-Failure "$context runtime screenshot.absolute_path does not identify its PNG."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$observedExecutable)) {
        if (-not (Test-Path -LiteralPath ([string]$observedExecutable) -PathType Leaf)) {
            Add-Failure "$context executable no longer exists for hash verification."
        }
        else {
            $actualExecutableHash = (Get-FileHash -LiteralPath ([string]$observedExecutable) -Algorithm SHA256).Hash
            Require-True (Test-ExactString $actualExecutableHash (Get-NestedValue $json @('process', 'executable_sha256'))) "$context executable SHA-256 does not match the file."
        }
    }

    return [pscustomobject]@{
        CaptureName = [string]$captureName
        CapturedAt = Convert-ToTimestamp (Get-PropertyValue $json 'captured_at_utc') "$context captured_at_utc"
        ProcessId = [int]$processId
        ExecutablePath = [string]$observedExecutable
        WindowTitle = [string]$beforeTitle
        Hwnd = [string]$beforeHwnd
        Dpi = [int]$beforeDpi
        Hash = [string]$actualHash
        ExecutableHash = [string](Get-NestedValue $json @('process', 'executable_sha256'))
    }
}

function Get-SnapshotByStage {
    param(
        [object[]]$Snapshots,
        [string]$Stage,
        [int]$Attempt
    )
    return @($Snapshots | Where-Object {
            (Test-ExactString (Get-PropertyValue $_ 'stage') $Stage) -and
            [int](Get-PropertyValue $_ 'attempt') -eq $Attempt
        })
}

function Test-AllowedStateHashDuplicate {
    param(
        [string]$FirstSceneId,
        [string]$SecondSceneId
    )
    foreach ($allowedSet in @($script:contract.state_chain.duplicate_hash_allowed_scene_sets)) {
        $ids = @($allowedSet | ForEach-Object { [string]$_ })
        if ($ids -contains $FirstSceneId -and $ids -contains $SecondSceneId) { return $true }
    }
    return $false
}

function Test-KeyInputLayers {
    param(
        $Attempt,
        [string]$ControlAutomationId,
        [string]$Key,
        [string[]]$RequiredWpfEvents,
        [bool]$AllowKeyDownCompletedActivation = $false
    )
    if ($null -eq $Attempt) { return $false }
    if (-not (Test-ExactString (Get-PropertyValue $Attempt 'origin') 'Win32')) { return $false }
    if (-not (Test-ExactString (Get-PropertyValue $Attempt 'key') $Key)) { return $false }
    $expectedVirtualKey = switch -CaseSensitive ($Key) {
        'Enter' { 13 }
        'Space' { 32 }
        'Left' { 37 }
        'Right' { 39 }
        default { -1 }
    }
    if ($expectedVirtualKey -lt 0) { return $false }
    if ([int](Get-PropertyValue $Attempt 'virtual_key') -ne $expectedVirtualKey) { return $false }

    $layer1 = Get-PropertyValue $Attempt 'layer1_win32'
    if (-not (Test-TrueValue (Get-PropertyValue $layer1 'key_down_received')) -or
        -not (Test-TrueValue (Get-PropertyValue $layer1 'key_up_received'))) { return $false }
    $nativeEvents = @(Get-PropertyValue $layer1 'events')
    if ($nativeEvents.Count -ne 2) { return $false }
    $nativeByMessage = @{}
    foreach ($message in @('WM_KEYDOWN', 'WM_KEYUP')) {
        $matchingNative = @($nativeEvents | Where-Object { Test-ExactString (Get-PropertyValue $_ 'message') $message })
        if ($matchingNative.Count -ne 1) { return $false }
        foreach ($nativeEvent in $matchingNative) {
            if ([int](Get-PropertyValue $nativeEvent 'virtual_key') -ne $expectedVirtualKey -or
                [int](Get-PropertyValue $nativeEvent 'scan_code') -le 0 -or
                [int](Get-PropertyValue $nativeEvent 'repeat_count') -ne 1 -or
                $null -eq (Get-PropertyValue $nativeEvent 'modifiers') -or
                [int](Get-PropertyValue $nativeEvent 'native_message_time') -lt 0 -or
                (Convert-ToTimestamp (Get-PropertyValue $nativeEvent 'timestamp') 'Keyboard native event timestamp') -eq [DateTimeOffset]::MinValue) { return $false }
        }
        $nativeByMessage[$message] = $matchingNative[0]
    }

    $layer2 = Get-PropertyValue $Attempt 'layer2_wpf'
    $layer4 = Get-PropertyValue $Attempt 'layer4_action'
    $isKeyDownCompletedActivation = $AllowKeyDownCompletedActivation -and
        (Test-ExactString $ControlAutomationId 'RetryAssetLibraryLoad') -and
        $Key -ceq 'Enter'
    foreach ($flag in @('preview_key_down_received', 'key_down_received')) {
        if (-not (Test-TrueValue (Get-PropertyValue $layer2 $flag))) { return $false }
    }
    if ($isKeyDownCompletedActivation) {
        foreach ($flag in @('preview_key_up_received', 'key_up_received')) {
            if (Test-TrueValue (Get-PropertyValue $layer2 $flag)) { return $false }
        }
    }
    else {
        foreach ($flag in @('preview_key_up_received', 'key_up_received')) {
            if (-not (Test-TrueValue (Get-PropertyValue $layer2 $flag))) { return $false }
        }
    }
    $wpfEvents = @(Get-PropertyValue $layer2 'events')
    $expectedWpfEvents = @($RequiredWpfEvents)
    if ($wpfEvents.Count -ne $expectedWpfEvents.Count) { return $false }
    $wpfByName = @{}
    foreach ($eventName in $expectedWpfEvents) {
        $matchingWpf = @($wpfEvents | Where-Object {
                (Test-ExactString (Get-PropertyValue $_ 'event_name') $eventName) -and
                (Test-ExactString (Get-PropertyValue $_ 'key') $Key)
        })
        if ($matchingWpf.Count -ne 1) { return $false }
        $wpfByName[$eventName] = $matchingWpf[0]
    }

    $layer3 = Get-PropertyValue $Attempt 'layer3_target'
    if (-not (Test-ExactString (Get-PropertyValue $layer3 'control_automation_id') $ControlAutomationId) -or
        -not (Test-ExactString (Get-NestedValue $layer3 @('control', 'automation_id')) $ControlAutomationId) -or
        -not (Test-ExactString (Get-NestedValue $layer3 @('focused_element_at_down', 'automation_id')) $ControlAutomationId) -or
        -not (Test-ExactString (Get-PropertyValue $layer3 'focused_automation_id_at_down') $ControlAutomationId)) { return $false }
    $downChain = @(Get-PropertyValue $layer3 'focus_parent_chain_at_down')
    $upChain = @(Get-PropertyValue $layer3 'focus_parent_chain_at_up')
    if ($downChain.Count -eq 0 -or
        -not (Test-ExactString (Get-PropertyValue $downChain[0] 'automation_id') $ControlAutomationId)) { return $false }

    if (-not $isKeyDownCompletedActivation) {
        if (-not (Test-ExactString (Get-NestedValue $layer3 @('focused_element_at_up', 'automation_id')) $ControlAutomationId) -or
            -not (Test-ExactString (Get-PropertyValue $layer3 'focused_automation_id_at_up') $ControlAutomationId) -or
            $downChain.Count -ne $upChain.Count) { return $false }
        for ($index = 0; $index -lt $downChain.Count; $index++) {
            if (-not (Test-ExactString (Get-PropertyValue $downChain[$index] 'automation_id') (Get-PropertyValue $upChain[$index] 'automation_id')) -or
                -not (Test-ExactString (Get-PropertyValue $downChain[$index] 'type') (Get-PropertyValue $upChain[$index] 'type'))) { return $false }
        }
        return $true
    }

    $focusedElementSnapshotStateAtUp = Get-PropertyState $layer3 'actual_focused_element_at_native_key_up'
    $focusedElementSnapshotAtUp = $focusedElementSnapshotStateAtUp.Value
    $focusedElementIdStateAtUp = Get-PropertyState $focusedElementSnapshotAtUp 'automation_id'
    $focusedIdStateAtUp = Get-PropertyState $layer3 'actual_focused_automation_id_at_native_key_up'
    $focusedElementAtUpIsString = $focusedElementIdStateAtUp.Present -and
        -not $focusedElementIdStateAtUp.IsArray -and
        $null -ne $focusedElementIdStateAtUp.Value -and
        $focusedElementIdStateAtUp.Value.GetType() -eq [string]
    $focusedAtUpIsString = $focusedIdStateAtUp.Present -and
        -not $focusedIdStateAtUp.IsArray -and
        $null -ne $focusedIdStateAtUp.Value -and
        $focusedIdStateAtUp.Value.GetType() -eq [string]
    $nearestFocusMatchesControlAtUp = $focusedAtUpIsString -and (Test-ExactString $focusedIdStateAtUp.Value $ControlAutomationId)
    $directFocusMatchesControlAtUp = $focusedElementAtUpIsString -and (Test-ExactString $focusedElementIdStateAtUp.Value $ControlAutomationId)
    $focusedElementIsOriginalTargetAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_is_original_target_at_native_key_up' ([bool]) $true
    $focusedElementIsDifferentTargetAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_is_original_target_at_native_key_up' ([bool]) $false
    $focusedElementAvailableAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_available_at_native_key_up' ([bool]) $true
    $focusedElementUnavailableAtUp = Test-StrictScalarPropertyValue $layer3 'actual_focused_element_available_at_native_key_up' ([bool]) $false
    $focusStateAtUpIsValid = if ($focusedElementIsOriginalTargetAtUp) {
        $nearestFocusMatchesControlAtUp -and
        $directFocusMatchesControlAtUp -and
        $focusedElementUnavailableAtUp
    }
    else {
        $focusedAtUpIsString -and
        $focusedElementAtUpIsString -and
        -not $nearestFocusMatchesControlAtUp -and
        -not $directFocusMatchesControlAtUp -and
        $focusedElementIsDifferentTargetAtUp -and
        $focusedElementAvailableAtUp
    }
    $clickEvents = @((Get-PropertyValue $layer4 'events') | Where-Object { Test-ExactString (Get-PropertyValue $_ 'event_name') 'ButtonClick' })
    if (-not (Test-StrictScalarPropertyValue $layer4 'button_click_received' ([bool]) $true) -or
        -not (Test-StrictScalarPropertyValue $layer4 'physical_target_confirmed' ([bool]) $true) -or
        -not (Test-StrictScalarPropertyValue $layer4 'activation_completed_on_key_down' ([bool]) $true) -or
        -not (Test-StrictScalarPropertyValue $layer4 'activation_finalized_at_native_key_up' ([bool]) $true) -or
        -not (Test-StrictScalarPropertyValue $layer3 'target_available_at_native_key_up' ([bool]) $false) -or
        -not $focusedElementSnapshotStateAtUp.Present -or
        $focusedElementSnapshotStateAtUp.IsArray -or
        $null -eq $focusedElementSnapshotAtUp -or
        -not $focusedElementAtUpIsString -or
        -not $focusedAtUpIsString -or
        -not $focusStateAtUpIsValid -or
        $clickEvents.Count -ne 1 -or
        -not (Test-ExactString (Get-NestedValue $layer4 @('button', 'automation_id')) $ControlAutomationId)) { return $false }

    $nativeDownAt = Convert-ToTimestamp (Get-PropertyValue $nativeByMessage['WM_KEYDOWN'] 'timestamp') 'Retry keyboard native down timestamp'
    $nativeUpAt = Convert-ToTimestamp (Get-PropertyValue $nativeByMessage['WM_KEYUP'] 'timestamp') 'Retry keyboard native up timestamp'
    $previewDownAt = Convert-ToTimestamp (Get-PropertyValue $wpfByName['PreviewKeyDown'] 'timestamp') 'Retry keyboard WPF PreviewKeyDown timestamp'
    $keyDownAt = Convert-ToTimestamp (Get-PropertyValue $wpfByName['KeyDown'] 'timestamp') 'Retry keyboard WPF KeyDown timestamp'
    $clickAt = Convert-ToTimestamp (Get-PropertyValue $clickEvents[0] 'timestamp') 'Retry keyboard ButtonClick timestamp'
    $finalizedAt = Convert-ToTimestamp (Get-PropertyValue $layer4 'activation_finalized_at') 'Retry keyboard native-key-up finalization timestamp'
    return $nativeDownAt -ne [DateTimeOffset]::MinValue -and
        $nativeUpAt -ne [DateTimeOffset]::MinValue -and
        $previewDownAt -ne [DateTimeOffset]::MinValue -and
        $keyDownAt -ne [DateTimeOffset]::MinValue -and
        $clickAt -ne [DateTimeOffset]::MinValue -and
        $finalizedAt -ne [DateTimeOffset]::MinValue -and
        $nativeDownAt -le $previewDownAt -and
        $previewDownAt -le $clickAt -and
        $clickAt -le $keyDownAt -and
        $keyDownAt -le $nativeUpAt -and
        $nativeUpAt -le $finalizedAt
}

function Get-TransitionForAttempt {
    param(
        [object[]]$Transitions,
        $Attempt,
        [string]$ControlAutomationId,
        [string]$Key
    )
    $attemptId = [string](Get-PropertyValue $Attempt 'attempt_id')
    return @($Transitions | Where-Object {
            (Test-ExactString (Get-PropertyValue $_ 'correlated_key_attempt_id') $attemptId) -and
            (Test-ExactString (Get-PropertyValue $_ 'input_kind') 'Keyboard') -and
            (Test-ExactString (Get-PropertyValue $_ 'input_key') $Key) -and
            (Test-ExactString (Get-NestedValue $_ @('control', 'automation_id')) $ControlAutomationId)
        })
}

function Test-RegularKeyTransition {
    param(
        $Transition,
        $Attempt,
        [string]$Delta,
        $Control
    )
    if ($null -eq $Transition) { return $false }
    foreach ($flag in @(
            'target_matched_at_start',
            'layer1_win32_confirmed',
            'layer2_wpf_confirmed',
            'layer3_target_confirmed',
            'layer4_action_confirmed',
            'state_changed',
            'settings_state_changed',
            'settings_write_back_confirmed')) {
        if (-not (Test-TrueValue (Get-PropertyValue $Transition $flag))) { return $false }
    }
    if (-not (Test-ExactString (Get-PropertyValue $Transition 'result') 'Confirmed') -or
        $null -eq (Get-PropertyValue $Transition 'completed_at')) { return $false }
    if (-not [string]::Equals([string](Get-PropertyValue $Transition 'expected_adjustment'), $Delta, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-ExactString (Get-PropertyValue $Transition 'control_kind') ([string]$Control.control_kind)) -or
        -not (Test-ExactString (Get-PropertyValue $Transition 'property_name') ([string]$Control.property_name)) -or
        [Math]::Abs([double](Get-PropertyValue $Transition 'minimum_value') - [double]$Control.minimum) -gt 0.5 -or
        [Math]::Abs([double](Get-PropertyValue $Transition 'maximum_value') - [double]$Control.maximum) -gt 0.5) { return $false }
    $before = [double](Get-PropertyValue $Transition 'before_actual_value')
    $after = [double](Get-PropertyValue $Transition 'after_actual_value')
    $afterPersisted = [double](Get-PropertyValue $Transition 'after_persisted_value')
    if ([Math]::Abs($after - $afterPersisted) -gt 0.5) { return $false }
    if ($Delta -ceq 'increase' -and $after -le $before) { return $false }
    if ($Delta -ceq 'decrease' -and $after -ge $before) { return $false }

    $layer4 = Get-PropertyValue $Attempt 'layer4_action'
    return (Test-TrueValue (Get-PropertyValue $layer4 'control_state_transition_confirmed')) -and
        (Test-TrueValue (Get-PropertyValue $layer4 'settings_write_back_confirmed')) -and
        (Test-TrueValue (Get-PropertyValue $layer4 'state_changed')) -and
        [Math]::Abs([double](Get-PropertyValue $layer4 'before_actual_value') - $before) -le 0.5 -and
        [Math]::Abs([double](Get-PropertyValue $layer4 'after_actual_value') - $after) -le 0.5 -and
        [Math]::Abs([double](Get-PropertyValue $layer4 'after_persisted_value') - $afterPersisted) -le 0.5 -and
        (Test-ExactString (Get-PropertyValue $layer4 'transition_id') (Get-PropertyValue $Transition 'transition_id')) -and
        $null -ne (Get-PropertyValue $layer4 'completed_at')
}

function Test-KeyActionInsideCaptureWindow {
    param(
        $Attempt,
        $Transition,
        [DateTimeOffset]$DefaultCapturedAt,
        [DateTimeOffset]$InteractionCapturedAt,
        [string[]]$RequiredWpfEvents
    )
    $attemptStartedAt = [DateTimeOffset]::MinValue
    $attemptUpdatedAt = [DateTimeOffset]::MinValue
    $transitionStartedAt = [DateTimeOffset]::MinValue
    $transitionCompletedAt = [DateTimeOffset]::MinValue
    $layer4CompletedAt = [DateTimeOffset]::MinValue
    if (-not (Try-GetTimestamp (Get-PropertyValue $Attempt 'started_at') ([ref]$attemptStartedAt)) -or
        -not (Try-GetTimestamp (Get-PropertyValue $Attempt 'updated_at') ([ref]$attemptUpdatedAt)) -or
        -not (Try-GetTimestamp (Get-PropertyValue $Transition 'started_at') ([ref]$transitionStartedAt)) -or
        -not (Try-GetTimestamp (Get-PropertyValue $Transition 'completed_at') ([ref]$transitionCompletedAt)) -or
        -not (Try-GetTimestamp (Get-NestedValue $Attempt @('layer4_action', 'completed_at')) ([ref]$layer4CompletedAt))) { return $false }

    if ($attemptStartedAt -le $DefaultCapturedAt -or $attemptUpdatedAt -gt $InteractionCapturedAt -or
        $attemptUpdatedAt -lt $attemptStartedAt -or
        $transitionStartedAt -lt $attemptStartedAt -or $transitionCompletedAt -gt $InteractionCapturedAt -or
        $transitionCompletedAt -lt $transitionStartedAt -or
        $layer4CompletedAt -lt $transitionStartedAt -or $layer4CompletedAt -gt $InteractionCapturedAt) { return $false }

    $nativeTimestamps = @()
    foreach ($nativeEvent in @(Get-NestedValue $Attempt @('layer1_win32', 'events'))) {
        $timestamp = [DateTimeOffset]::MinValue
        if (-not (Try-GetTimestamp (Get-PropertyValue $nativeEvent 'timestamp') ([ref]$timestamp)) -or
            $timestamp -le $DefaultCapturedAt -or $timestamp -gt $InteractionCapturedAt) { return $false }
        $nativeTimestamps += $timestamp
    }
    if ($nativeTimestamps.Count -ne 2 -or $nativeTimestamps[1] -le $nativeTimestamps[0]) { return $false }

    $wpfEvents = @(Get-NestedValue $Attempt @('layer2_wpf', 'events'))
    if ($wpfEvents.Count -ne $RequiredWpfEvents.Count) { return $false }
    $previousWpfAt = [DateTimeOffset]::MinValue
    for ($index = 0; $index -lt $RequiredWpfEvents.Count; $index++) {
        if (-not (Test-ExactString (Get-PropertyValue $wpfEvents[$index] 'event_name') $RequiredWpfEvents[$index])) { return $false }
        $timestamp = [DateTimeOffset]::MinValue
        if (-not (Try-GetTimestamp (Get-PropertyValue $wpfEvents[$index] 'timestamp') ([ref]$timestamp)) -or
            $timestamp -le $DefaultCapturedAt -or $timestamp -gt $InteractionCapturedAt -or
            $timestamp -lt $previousWpfAt) { return $false }
        $previousWpfAt = $timestamp
    }
    return $true
}

function Test-BoundaryTransition {
    param(
        $Transition,
        $Attempt,
        [double]$Boundary
    )
    if ($null -eq $Transition) { return $false }
    foreach ($flag in @(
            'target_matched_at_start',
            'layer1_win32_confirmed',
            'layer2_wpf_confirmed',
            'layer3_target_confirmed',
            'layer4_action_confirmed',
            'settings_write_back_confirmed',
            'boundary_reached',
            'boundary_no_op_confirmed')) {
        if (-not (Test-TrueValue (Get-PropertyValue $Transition $flag))) { return $false }
    }
    if (Test-TrueValue (Get-PropertyValue $Transition 'state_changed')) { return $false }
    if (-not (Test-ExactString (Get-PropertyValue $Transition 'result') 'BoundaryNoOpConfirmed')) { return $false }
    $before = [double](Get-PropertyValue $Transition 'before_actual_value')
    $after = [double](Get-PropertyValue $Transition 'after_actual_value')
    $afterPersisted = [double](Get-PropertyValue $Transition 'after_persisted_value')
    $layer4 = Get-PropertyValue $Attempt 'layer4_action'
    return [Math]::Abs($before - $Boundary) -le 0.5 -and
        [Math]::Abs($after - $Boundary) -le 0.5 -and
        [Math]::Abs($afterPersisted - $Boundary) -le 0.5 -and
        (Test-TrueValue (Get-PropertyValue $layer4 'control_state_transition_confirmed')) -and
        (Test-TrueValue (Get-PropertyValue $layer4 'settings_write_back_confirmed')) -and
        (Test-TrueValue (Get-PropertyValue $layer4 'boundary_reached')) -and
        (Test-TrueValue (Get-PropertyValue $layer4 'boundary_no_op_confirmed')) -and
        -not (Test-TrueValue (Get-PropertyValue $layer4 'state_changed')) -and
        (Test-ExactString (Get-PropertyValue $layer4 'transition_id') (Get-PropertyValue $Transition 'transition_id')) -and
        $null -ne (Get-PropertyValue $Transition 'completed_at')
}

$contractPath = Join-Path $PSScriptRoot 'gate-a-evidence-contract.json'
if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
    throw "Gate A evidence contract is missing beside the validator."
}
$contractRaw = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8
$contract = $contractRaw | ConvertFrom-Json
$script:contract = $contract

if ($contractRaw -match '(?i)(?:[a-z]:[\\/]|\\\\[^\\])') {
    Add-Failure 'The portable Gate A contract contains a machine-specific absolute path.'
}
Require-True (Test-ExactString $contract.schema 'pixel-tart-asset-library-p1-gate-a-evidence-contract/v1') 'Gate A contract schema mismatch.'
Require-True (Test-ExactString $contract.capture_status 'captured') 'Gate A capture_status is not captured.'
Require-True (Test-TrueValue $contract.synthetic_fixture_only) 'Gate A contract is not synthetic-fixture-only.'
Require-True (-not (Test-TrueValue $contract.customer_media_allowed)) 'Gate A contract allows customer media.'
Require-True (-not (Test-TrueValue $contract.portable_output.machine_paths_allowed)) 'Gate A portable output allows machine paths.'

$resolvedRunRoot = [IO.Path]::GetFullPath($RunRoot)
$script:resolvedRunRoot = $resolvedRunRoot
if (-not (Test-Path -LiteralPath $resolvedRunRoot -PathType Container)) {
    Add-Failure 'The supplied Gate A run root does not exist or is not a directory.'
}

# Source and binary identity come from two same-run manifests. The build manifest is the
# authority produced after a tracked-clean dedicated build; the manual manifest and every
# process session must repeat that identity exactly.
$trustedSourceHead = $null
$trustedExecutablePath = $null
$trustedExecutableHash = $null
$trustedAssetModulePath = $null
$trustedAssetModuleHash = $null
$trustedBuildConfiguration = $null
$manualManifest = $null
$buildManifest = $null
$manualSessionsById = @{}
$manualManifestPath = Join-Path $resolvedRunRoot ([string]$contract.source_identity.manual_manifest_file)
$buildManifestPath = Join-Path $resolvedRunRoot ([string]$contract.source_identity.build_manifest_file)
$manualManifestFiles = @(if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
        Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter ([string]$contract.source_identity.manual_manifest_file)
    })
$buildManifestFiles = @(if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
        Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter ([string]$contract.source_identity.build_manifest_file)
    })
Require-Equal $manualManifestFiles.Count 1 'The run must contain exactly one manual-run manifest.'
Require-Equal $buildManifestFiles.Count 1 'The run must contain exactly one authoritative build manifest.'
if ($manualManifestFiles.Count -eq 1) {
    Require-True (Test-SameFullPath $manualManifestFiles[0].FullName $manualManifestPath) 'The manual-run manifest is not at the run root.'
    $manualManifest = Read-JsonFile $manualManifestFiles[0].FullName
}
if ($buildManifestFiles.Count -eq 1) {
    Require-True (Test-SameFullPath $buildManifestFiles[0].FullName $buildManifestPath) 'The authoritative build manifest is not at the run root.'
    $buildManifest = Read-JsonFile $buildManifestFiles[0].FullName
}

if ($null -ne $buildManifest) {
    Require-True (Test-ExactString (Get-PropertyValue $buildManifest 'schema') $contract.source_identity.build_manifest_schema) 'Build manifest schema mismatch.'
    $trustedSourceHead = [string](Get-PropertyValue $buildManifest 'source_head')
    $trustedExecutablePath = [string](Get-PropertyValue $buildManifest 'executable_path')
    $trustedExecutableHash = [string](Get-PropertyValue $buildManifest 'executable_sha256')
    $trustedAssetModulePath = [string](Get-PropertyValue $buildManifest 'asset_module_path')
    $trustedAssetModuleHash = [string](Get-PropertyValue $buildManifest 'asset_module_sha256')
    $trustedBuildConfiguration = [string](Get-PropertyValue $buildManifest 'build_configuration')
    Require-True (Test-LowercaseCommitSha $trustedSourceHead) 'Build manifest source_head must be exactly 40 lowercase hexadecimal characters.'
    Require-True (Test-UppercaseSha256 $trustedExecutableHash) 'Build manifest executable_sha256 must be exactly 64 uppercase hexadecimal characters.'
    Require-True (Test-UppercaseSha256 $trustedAssetModuleHash) 'Build manifest asset_module_sha256 must be exactly 64 uppercase hexadecimal characters.'
    Require-True (Test-FullyQualifiedPath $trustedExecutablePath) 'Build manifest executable_path is not absolute.'
    Require-True (Test-FullyQualifiedPath $trustedAssetModulePath) 'Build manifest asset_module_path is not absolute.'
    if (Test-FullyQualifiedPath $trustedAssetModulePath) {
        Require-True (Test-ExactString ([IO.Path]::GetFileName($trustedAssetModulePath)) ([string]$contract.source_identity.expected_asset_module_name)) "Build manifest Asset DLL must be named '$($contract.source_identity.expected_asset_module_name)'."
        if (Test-FullyQualifiedPath $trustedExecutablePath) {
            Require-True (Test-SameFullPath (Split-Path -Parent $trustedAssetModulePath) (Split-Path -Parent $trustedExecutablePath)) 'Build manifest Asset DLL must be beside the executable.'
        }
    }
    $matchingBuildConfigurations = @($contract.source_identity.allowed_build_configurations | Where-Object {
            Test-ExactString $_ $trustedBuildConfiguration
        })
    Require-Equal $matchingBuildConfigurations.Count 1 'Build manifest configuration is not allowed by the contract.'
    Require-True (Test-TrueValue (Get-PropertyValue $buildManifest 'repository_tracked_clean')) 'Build manifest does not attest a tracked-clean source tree.'
    Require-True (Test-TrueValue (Get-PropertyValue $buildManifest 'source_head_is_current_head')) 'Build manifest does not attest that source_head was current HEAD.'
    Require-True (Test-TrueValue (Get-PropertyValue $buildManifest 'dedicated_build_succeeded')) 'Build manifest does not attest a successful dedicated build.'
    [void](Convert-ToTimestamp (Get-PropertyValue $buildManifest 'created_at') 'Build manifest created_at')
    if (Test-FullyQualifiedPath $trustedExecutablePath) {
        Require-True (Test-Path -LiteralPath $trustedExecutablePath -PathType Leaf) 'Build-authority executable does not exist.'
        if (Test-Path -LiteralPath $trustedExecutablePath -PathType Leaf) {
            $authorityFileHash = (Get-FileHash -LiteralPath $trustedExecutablePath -Algorithm SHA256).Hash
            Require-True (Test-ExactString $authorityFileHash $trustedExecutableHash) 'Build manifest executable_sha256 does not match the executable bytes.'
        }
    }
    if (Test-FullyQualifiedPath $trustedAssetModulePath) {
        Require-True (Test-Path -LiteralPath $trustedAssetModulePath -PathType Leaf) 'Build-authority Asset DLL does not exist.'
        if (Test-Path -LiteralPath $trustedAssetModulePath -PathType Leaf) {
            $assetModuleFileHash = (Get-FileHash -LiteralPath $trustedAssetModulePath -Algorithm SHA256).Hash
            Require-True (Test-ExactString $assetModuleFileHash $trustedAssetModuleHash) 'Build manifest asset_module_sha256 does not match the Asset DLL bytes.'
        }
    }
}

if ($null -ne $manualManifest) {
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'schema') $contract.source_identity.manual_manifest_schema) 'Manual-run manifest schema mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'mode') 'Run') 'Manual-run manifest is not a real Run packet.'
    Require-True (Test-SameFullPath ([string](Get-PropertyValue $manualManifest 'run_root')) $resolvedRunRoot) 'Manual-run manifest run_root does not identify the supplied run.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'build_manifest_file') $contract.source_identity.build_manifest_file) 'Manual-run manifest does not identify the authoritative build manifest.'
    Require-True (Test-LowercaseCommitSha (Get-PropertyValue $manualManifest 'source_head')) 'Manual-run source_head must be exactly 40 lowercase hexadecimal characters.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'source_head') $trustedSourceHead) 'Manual-run source_head differs from the build authority.'
    Require-True (Test-SameFullPath ([string](Get-PropertyValue $manualManifest 'executable_path')) $trustedExecutablePath) 'Manual-run executable_path differs from the build authority.'
    Require-True (Test-UppercaseSha256 (Get-PropertyValue $manualManifest 'executable_sha256')) 'Manual-run executable_sha256 must be exactly 64 uppercase hexadecimal characters.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'executable_sha256') $trustedExecutableHash) 'Manual-run executable_sha256 differs from the build authority.'
    Require-True (Test-SameFullPath ([string](Get-PropertyValue $manualManifest 'asset_module_path')) $trustedAssetModulePath) 'Manual-run asset_module_path differs from the build authority.'
    Require-True (Test-UppercaseSha256 (Get-PropertyValue $manualManifest 'asset_module_sha256')) 'Manual-run asset_module_sha256 must be exactly 64 uppercase hexadecimal characters.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'asset_module_sha256') $trustedAssetModuleHash) 'Manual-run asset_module_sha256 differs from the build authority.'
    Require-True (Test-ExactString (Get-PropertyValue $manualManifest 'build_configuration') $trustedBuildConfiguration) 'Manual-run build configuration differs from the build authority.'
    Require-True (Test-TrueValue (Get-PropertyValue $manualManifest 'synthetic_fixture_only')) 'Manual-run manifest is not synthetic-fixture-only.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $manualManifest 'customer_media_allowed'))) 'Manual-run manifest allows customer media.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $manualManifest 'eagle_library_write_allowed'))) 'Manual-run manifest allows Eagle library writes.'
    [void](Convert-ToTimestamp (Get-PropertyValue $manualManifest 'created_at') 'Manual-run manifest created_at')

    $manualSessions = @(Get-PropertyValue $manualManifest 'sessions')
    Require-Equal $manualSessions.Count @($contract.source_identity.required_manual_session_ids).Count 'Manual-run manifest session count mismatch.'
    $seenSessionProcessIds = [Collections.Generic.HashSet[int]]::new()
    foreach ($sessionIdValue in @($contract.source_identity.required_manual_session_ids)) {
        $sessionId = [string]$sessionIdValue
        $matches = @($manualSessions | Where-Object { Test-ExactString (Get-PropertyValue $_ 'session_id') $sessionId })
        Require-Equal $matches.Count 1 "Manual-run session '$sessionId' must occur exactly once."
        if ($matches.Count -ne 1) { continue }
        $session = $matches[0]
        $manualSessionsById[$sessionId] = $session
        $sessionProcessId = [int](Get-PropertyValue $session 'process_id')
        Require-True ($sessionProcessId -gt 0) "Manual-run session '$sessionId' has no positive PID."
        Require-True ($seenSessionProcessIds.Add($sessionProcessId)) "Manual-run session '$sessionId' reuses another session PID."
        Require-True (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $session 'window_hwnd'))) "Manual-run session '$sessionId' has no HWND."
        Require-True (Test-ExactString (Get-PropertyValue $session 'source_head') $trustedSourceHead) "Manual-run session '$sessionId' HEAD differs from build authority."
        Require-True (Test-ExactString (Get-PropertyValue $session 'build_configuration') $trustedBuildConfiguration) "Manual-run session '$sessionId' build configuration differs from build authority."
        Require-True (Test-SameFullPath ([string](Get-PropertyValue $session 'executable_path')) $trustedExecutablePath) "Manual-run session '$sessionId' executable path differs from build authority."
        Require-True (Test-ExactString (Get-PropertyValue $session 'executable_sha256') $trustedExecutableHash) "Manual-run session '$sessionId' executable hash differs from build authority."
    }
}

$windowRecords = @()
if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter '*.window-evidence.json')) {
        $json = Read-JsonFile $file.FullName
        if ($null -ne $json) {
            $windowRecords += [pscustomobject]@{
                Path = $file.FullName
                Name = $file.Name
                Json = $json
                CaptureName = [string](Get-PropertyValue $json 'capture_name')
            }
        }
    }
}

# Every captured window must belong to exactly one declared session and repeat the same
# build-authority executable identity. This includes supporting keyboard/restart captures,
# not only the screenshots selected later by capture-name tokens.
foreach ($record in $windowRecords) {
    $recordProcessId = [int](Get-NestedValue $record.Json @('process', 'process_id'))
    $sessionMatches = @($manualSessionsById.Values | Where-Object { [int](Get-PropertyValue $_ 'process_id') -eq $recordProcessId })
    Require-Equal $sessionMatches.Count 1 "Window evidence '$($record.Name)' does not belong to exactly one manual-run session."
    Require-True (Test-SameFullPath ([string](Get-NestedValue $record.Json @('process', 'executable_path'))) $trustedExecutablePath) "Window evidence '$($record.Name)' executable path differs from build authority."
    Require-True (Test-SameFullPath ([string](Get-NestedValue $record.Json @('expected', 'executable_path'))) $trustedExecutablePath) "Window evidence '$($record.Name)' expected executable path differs from build authority."
    Require-True (Test-ExactString (Get-NestedValue $record.Json @('process', 'executable_sha256')) $trustedExecutableHash) "Window evidence '$($record.Name)' executable hash differs from build authority."
    if ($sessionMatches.Count -eq 1) {
        Require-True (Test-ExactString (Get-NestedValue $record.Json @('expected', 'window_hwnd')) (Get-PropertyValue $sessionMatches[0] 'window_hwnd')) "Window evidence '$($record.Name)' expected HWND differs from its manual-run session."
        Require-True (Test-ExactString (Get-NestedValue $record.Json @('window_before_capture', 'hwnd')) (Get-PropertyValue $sessionMatches[0] 'window_hwnd')) "Window evidence '$($record.Name)' HWND differs from its manual-run session."
        Require-True (Test-ExactString (Get-NestedValue $record.Json @('window_after_capture', 'hwnd')) (Get-PropertyValue $sessionMatches[0] 'window_hwnd')) "Window evidence '$($record.Name)' post-capture HWND differs from its manual-run session."
    }
}
foreach ($sessionIdValue in @($contract.source_identity.required_manual_session_ids)) {
    $sessionId = [string]$sessionIdValue
    $session = $manualSessionsById[$sessionId]
    if ($null -eq $session) { continue }
    $sessionWindows = @($windowRecords | Where-Object {
            [int](Get-NestedValue $_.Json @('process', 'process_id')) -eq [int](Get-PropertyValue $session 'process_id') -and
            (Test-ExactString (Get-NestedValue $_.Json @('window_before_capture', 'hwnd')) (Get-PropertyValue $session 'window_hwnd'))
        })
    Require-True ($sessionWindows.Count -ge 1) "Manual-run session '$sessionId' has no matching PID/HWND window evidence."
}

# Four state captures are validated from raw window JSON and the corresponding PNG bytes.
$stateSummaries = @()
$stateHashesById = @{}
foreach ($scene in @($contract.state_chain.scenes)) {
    $matches = @($windowRecords | Where-Object {
            $_.CaptureName.StartsWith([string]$scene.capture_name_prefix, [StringComparison]::Ordinal)
        })
    Require-Equal $matches.Count 1 "State '$($scene.id)' must have exactly one PNG + window-evidence pair."
    if ($matches.Count -eq 1) {
        $summary = Get-WindowRecordSummary $matches[0] $null
        $stateSummaries += [pscustomobject]@{ Scene = $scene; Summary = $summary }
        Require-Equal $summary.Dpi $contract.state_chain.state_evidence_dpi "State '$($scene.id)' DPI mismatch."
        if (-not [string]::IsNullOrWhiteSpace($summary.Hash)) {
            $duplicateScenes = @($stateHashesById.Keys | Where-Object { Test-ExactString $stateHashesById[$_] $summary.Hash })
            foreach ($duplicateScene in $duplicateScenes) {
                Require-True (Test-AllowedStateHashDuplicate ([string]$duplicateScene) ([string]$scene.id)) "State '$($scene.id)' duplicates state '$duplicateScene' outside the explicit empty-state allowance."
            }
            $stateHashesById[[string]$scene.id] = $summary.Hash
            if ($duplicateScenes.Count -eq 0) { [void]$requiredHashes.Add($summary.Hash) }
        }
    }
}
foreach ($sessionContract in @($contract.state_chain.sessions)) {
    $sessionStates = @($stateSummaries | Where-Object { Test-ExactString $_.Scene.session_id ([string]$sessionContract.id) })
    Require-Equal $sessionStates.Count @($sessionContract.scene_ids).Count "State session '$($sessionContract.id)' is incomplete."
    if ($sessionStates.Count -gt 0) {
        $identity = $sessionStates[0].Summary
        $manualSession = $manualSessionsById[[string]$sessionContract.id]
        Require-True ($null -ne $manualSession) "State session '$($sessionContract.id)' is absent from the manual-run identity manifest."
        if ($null -ne $manualSession) {
            Require-Equal $identity.ProcessId (Get-PropertyValue $manualSession 'process_id') "State session '$($sessionContract.id)' PID differs from the manual-run identity."
            Require-True (Test-ExactString $identity.Hwnd (Get-PropertyValue $manualSession 'window_hwnd')) "State session '$($sessionContract.id)' HWND differs from the manual-run identity."
            Require-True (Test-SameFullPath $identity.ExecutablePath ([string](Get-PropertyValue $manualSession 'executable_path'))) "State session '$($sessionContract.id)' executable differs from the manual-run identity."
            Require-True (Test-ExactString $identity.ExecutableHash (Get-PropertyValue $manualSession 'executable_sha256')) "State session '$($sessionContract.id)' executable hash differs from the manual-run identity."
        }
        foreach ($item in $sessionStates | Select-Object -Skip 1) {
            Require-Equal $item.Summary.ProcessId $identity.ProcessId "State '$($item.Scene.id)' PID differs within session '$($sessionContract.id)'."
            Require-True ([string]::Equals($item.Summary.ExecutablePath, $identity.ExecutablePath, [StringComparison]::OrdinalIgnoreCase)) "State '$($item.Scene.id)' executable path differs within session '$($sessionContract.id)'."
            Require-True (Test-ExactString $item.Summary.WindowTitle $identity.WindowTitle) "State '$($item.Scene.id)' title differs within session '$($sessionContract.id)'."
            Require-True (Test-ExactString $item.Summary.Hwnd $identity.Hwnd) "State '$($item.Scene.id)' HWND differs within session '$($sessionContract.id)'."
            Require-Equal $item.Summary.Dpi $identity.Dpi "State '$($item.Scene.id)' DPI differs within session '$($sessionContract.id)'."
        }
    }
}

# The scenario and every state snapshot are read directly; no aggregate pass field can satisfy this gate.
$allScenarioFiles = @(if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
        Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter $contract.state_chain.scenario_manifest_file
    })
$scenarioCandidates = @()
foreach ($file in $allScenarioFiles) {
    $manifest = Read-JsonFile $file.FullName
    if ($null -ne $manifest) {
        $scenarioCandidates += [pscustomobject]@{ File = $file; Json = $manifest }
    }
}
Require-Equal $scenarioCandidates.Count @($contract.state_chain.sessions).Count 'The run must contain exactly one state-controller manifest per declared session.'
$retrySessionContract = @($contract.state_chain.sessions | Where-Object { Test-ExactString $_.id 'retry-session' }) | Select-Object -First 1
$retryCandidates = @($scenarioCandidates | Where-Object { Test-ExactString (Get-PropertyValue $_.Json 'scenario') ([string]$retrySessionContract.scenario) })
Require-Equal $retryCandidates.Count 1 'The run must contain exactly one loading-error-retry-empty state session.'
$scenarioFiles = @($retryCandidates | ForEach-Object { $_.File })
$snapshotFiles = @(if ($scenarioFiles.Count -eq 1) {
        Get-Item -LiteralPath (Join-Path $scenarioFiles[0].DirectoryName ([string]$contract.state_chain.snapshot_file)) -ErrorAction SilentlyContinue
    })
$controllerEventFiles = @(if ($scenarioFiles.Count -eq 1) {
        Get-Item -LiteralPath (Join-Path $scenarioFiles[0].DirectoryName ([string]$contract.state_chain.controller_event_file)) -ErrorAction SilentlyContinue
    })
Require-Equal $snapshotFiles.Count 1 'The run must contain exactly one state-controller JSONL snapshot file.'
Require-Equal $controllerEventFiles.Count 1 'The run must contain exactly one state-controller JSONL event file.'

$scenarioManifest = $null
$snapshots = @()
$controllerEvents = @()
$timelineEntries = @()
if ($scenarioFiles.Count -eq 1) {
    $scenarioManifest = $retryCandidates[0].Json
    if ($null -ne $scenarioManifest) {
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'protocol') $contract.state_chain.scenario_protocol) 'State-controller scenario protocol mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'scenario') $retrySessionContract.scenario) 'State-controller scenario name mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'processName') ([IO.Path]::GetFileNameWithoutExtension($contract.expected_executable_name))) 'State-controller process name mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'startRouteSource') $contract.acceptance_start_route.environment_variable) 'Retry manifest start-route source mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'startRoute') $contract.acceptance_start_route.route) 'Retry manifest start route mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'startRouteCurrentPage') $contract.acceptance_start_route.current_page) 'Retry manifest start-route current page mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'startRouteHead') $trustedSourceHead) 'Retry manifest start-route HEAD differs from build authority.'
        [void](Convert-ToTimestamp (Get-PropertyValue $scenarioManifest 'startRouteRecordedAt') 'Retry manifest startRouteRecordedAt')
        $isolatedRoot = [string](Get-PropertyValue $scenarioManifest 'isolatedRoot')
        Require-True (Test-FullyQualifiedPath $isolatedRoot) 'State-controller isolatedRoot is not absolute runtime evidence.'
        if (-not [string]::IsNullOrWhiteSpace($isolatedRoot)) {
            Require-True (Test-PathAtOrInsideRoot $isolatedRoot $resolvedRunRoot) 'State-controller isolatedRoot is outside the supplied run root.'
            Require-True (Test-PathInsideRoot $scenarioFiles[0].FullName $isolatedRoot) 'State-controller scenario manifest is outside its isolated root.'
        }
        if ((Test-FullyQualifiedPath $isolatedRoot) -and (Test-PathAtOrInsideRoot $isolatedRoot $resolvedRunRoot)) {
            $retryImportDiagnosticPath = Join-Path $isolatedRoot 'InputDiagnostics\asset-library-import.json'
            if (Test-Path -LiteralPath $retryImportDiagnosticPath) {
                $retryImportDiagnosticIsFile = Test-Path -LiteralPath $retryImportDiagnosticPath -PathType Leaf
                Require-True $retryImportDiagnosticIsFile 'Retry session InputDiagnostics/asset-library-import.json must be absent or a JSON file.'
                if ($retryImportDiagnosticIsFile) {
                    $retryImportDiagnostic = Read-JsonFile $retryImportDiagnosticPath
                    if ($null -ne $retryImportDiagnostic) {
                        $selectedFileCount = 0
                        $importedCount = 0
                        $selectedFileCountText = [string](Get-PropertyValue $retryImportDiagnostic 'selected_file_count')
                        $importedCountText = [string](Get-PropertyValue $retryImportDiagnostic 'imported_count')
                        $selectedFileCountIsZero = [string]::IsNullOrWhiteSpace($selectedFileCountText) -or
                            ([int]::TryParse($selectedFileCountText, [ref]$selectedFileCount) -and $selectedFileCount -eq 0)
                        $importedCountIsZero = [string]::IsNullOrWhiteSpace($importedCountText) -or
                            ([int]::TryParse($importedCountText, [ref]$importedCount) -and $importedCount -eq 0)
                        $retryImportContaminated =
                            (Test-TrueValue (Get-PropertyValue $retryImportDiagnostic 'picker_accepted')) -or
                            (Test-TrueValue (Get-PropertyValue $retryImportDiagnostic 'import_command_entered')) -or
                            (Test-TrueValue (Get-PropertyValue $retryImportDiagnostic 'import_service_entered')) -or
                            -not $selectedFileCountIsZero -or
                            -not $importedCountIsZero -or
                            -not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $retryImportDiagnostic 'source_kind'))
                        Require-True (-not $retryImportContaminated) 'Retry session contains file-picker/import contamination.'
                    }
                }
            }
        }
        Require-True (Test-TrueValue (Get-PropertyValue $scenarioManifest 'freshDatabaseVerified')) 'State-controller manifest did not record a fresh database verification.'
        $manifestDatabasePath = [string](Get-PropertyValue $scenarioManifest 'databasePath')
        Require-True (Test-FullyQualifiedPath $manifestDatabasePath) 'State-controller manifest databasePath is not absolute runtime evidence.'
        if ((Test-FullyQualifiedPath $manifestDatabasePath) -and (Test-FullyQualifiedPath $isolatedRoot)) {
            Require-True (Test-PathInsideRoot $manifestDatabasePath $isolatedRoot) 'State-controller databasePath is outside its isolated root.'
            Require-True (Test-Path -LiteralPath $manifestDatabasePath -PathType Leaf) 'State-controller fresh database file does not exist.'
        }
        $manifestSnapshotFile = [string](Get-PropertyValue $scenarioManifest 'snapshotFile')
        Require-True (Test-FullyQualifiedPath $manifestSnapshotFile) 'State-controller manifest snapshotFile is not absolute runtime evidence.'
        if ($snapshotFiles.Count -eq 1 -and (Test-FullyQualifiedPath $manifestSnapshotFile)) {
            Require-True ([string]::Equals(
                    [IO.Path]::GetFullPath($manifestSnapshotFile),
                    [IO.Path]::GetFullPath($snapshotFiles[0].FullName),
                    [StringComparison]::OrdinalIgnoreCase)) 'State-controller manifest does not identify the raw snapshot file.'
        }
        $manifestControllerEventFile = [string](Get-PropertyValue $scenarioManifest 'controllerEventFile')
        Require-True (Test-FullyQualifiedPath $manifestControllerEventFile) 'State-controller manifest controllerEventFile is not absolute runtime evidence.'
        if ($controllerEventFiles.Count -eq 1 -and (Test-FullyQualifiedPath $manifestControllerEventFile)) {
            Require-True ([string]::Equals(
                    [IO.Path]::GetFullPath($manifestControllerEventFile),
                    [IO.Path]::GetFullPath($controllerEventFiles[0].FullName),
                    [StringComparison]::OrdinalIgnoreCase)) 'State-controller manifest does not identify the raw controller event file.'
        }
        $retryStateSummaries = @($stateSummaries | Where-Object { Test-ExactString $_.Scene.session_id 'retry-session' })
        if ($retryStateSummaries.Count -gt 0) {
            Require-Equal (Get-PropertyValue $scenarioManifest 'processId') $retryStateSummaries[0].Summary.ProcessId 'State-controller PID does not match the retry-session state captures.'
        }
    }
}
if ($snapshotFiles.Count -eq 1) {
    $lineNumber = 0
    foreach ($line in @(Get-Content -LiteralPath $snapshotFiles[0].FullName -Encoding UTF8)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $envelope = $line | ConvertFrom-Json }
        catch {
            Add-Failure "State-controller JSONL line $lineNumber is invalid JSON."
            continue
        }
        Require-True (Test-ExactString (Get-PropertyValue $envelope 'protocol') $contract.state_chain.scenario_protocol) "State-controller JSONL line $lineNumber protocol mismatch."
        $sequence = [long](Get-PropertyValue $envelope 'sequence')
        Require-True ($sequence -gt 0) "State-controller JSONL line $lineNumber has no positive sequence."
        $snapshot = Get-PropertyValue $envelope 'snapshot'
        if ($null -eq $snapshot) { Add-Failure "State-controller JSONL line $lineNumber has no snapshot." }
        else {
            $recordedAt = Convert-ToTimestamp (Get-PropertyValue $snapshot 'recordedAt') "State-controller JSONL line $lineNumber recordedAt"
            $snapshots += $snapshot
            $timelineEntries += [pscustomobject]@{
                Sequence = $sequence
                Stream = 'snapshot'
                Stage = [string](Get-PropertyValue $snapshot 'stage')
                Attempt = [int](Get-PropertyValue $snapshot 'attempt')
                RecordedAt = $recordedAt
            }
        }
    }
}

if ($controllerEventFiles.Count -eq 1) {
    $lineNumber = 0
    foreach ($line in @(Get-Content -LiteralPath $controllerEventFiles[0].FullName -Encoding UTF8)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $controllerEvent = $line | ConvertFrom-Json }
        catch {
            Add-Failure "State-controller event JSONL line $lineNumber is invalid JSON."
            continue
        }
        Require-True (Test-ExactString (Get-PropertyValue $controllerEvent 'protocol') $contract.state_chain.scenario_protocol) "State-controller event JSONL line $lineNumber protocol mismatch."
        $sequence = [long](Get-PropertyValue $controllerEvent 'sequence')
        Require-True ($sequence -gt 0) "State-controller event JSONL line $lineNumber has no positive sequence."
        $recordedAt = Convert-ToTimestamp (Get-PropertyValue $controllerEvent 'recordedAt') "State-controller event JSONL line $lineNumber recordedAt"
        $controllerEvents += $controllerEvent
        $timelineEntries += [pscustomobject]@{
            Sequence = $sequence
            Stream = 'controller'
            Stage = [string](Get-PropertyValue $controllerEvent 'stage')
            Attempt = [int](Get-PropertyValue $controllerEvent 'attempt')
            RecordedAt = $recordedAt
        }
    }
}

$orderedTimeline = @($timelineEntries | Sort-Object Sequence)
$expectedSequence = 1L
$previousRecordedAt = [DateTimeOffset]::MinValue
foreach ($entry in $orderedTimeline) {
    Require-Equal ([long]$entry.Sequence) $expectedSequence 'State-controller snapshot/event sequence has a duplicate or gap.'
    Require-True ($entry.RecordedAt -ge $previousRecordedAt) 'State-controller snapshot/event timestamps move backwards.'
    $expectedSequence++
    $previousRecordedAt = $entry.RecordedAt
}

$previousRequiredSequence = 0L
foreach ($requiredEntry in @($retrySessionContract.required_timeline)) {
    $matches = @($orderedTimeline | Where-Object {
            (Test-ExactString $_.Stream ([string]$requiredEntry.stream)) -and
            (Test-ExactString $_.Stage ([string]$requiredEntry.stage)) -and
            [int]$_.Attempt -eq [int]$requiredEntry.attempt
        })
    Require-Equal $matches.Count 1 "Required timeline entry '$($requiredEntry.stream):$($requiredEntry.stage):$($requiredEntry.attempt)' must occur exactly once."
    if ($matches.Count -eq 1) {
        Require-True ([long]$matches[0].Sequence -gt $previousRequiredSequence) "Required timeline entry '$($requiredEntry.stage)' is out of order."
        $previousRequiredSequence = [long]$matches[0].Sequence
    }
}
Require-Equal $orderedTimeline.Count @($retrySessionContract.required_timeline).Count 'Retry-session state timeline contains undeclared entries.'
$loadingSnapshots = @(Get-SnapshotByStage $snapshots 'loading-entered' 1)
$loadingControllerEvents = @($controllerEvents | Where-Object { (Test-ExactString (Get-PropertyValue $_ 'stage') 'loading-barrier-waiting') -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
$releasedControllerEvents = @($controllerEvents | Where-Object { (Test-ExactString (Get-PropertyValue $_ 'stage') 'loading-barrier-released') -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
$injectionControllerEvents = @($controllerEvents | Where-Object { (Test-ExactString (Get-PropertyValue $_ 'stage') 'recoverable-query-error-injected') -and [int](Get-PropertyValue $_ 'attempt') -eq 1 })
$errorSnapshots = @(Get-SnapshotByStage $snapshots 'error-visible' 1)
$realRepositoryEvents = @($controllerEvents | Where-Object { (Test-ExactString (Get-PropertyValue $_ 'stage') 'real-repository-query-entered') -and [int](Get-PropertyValue $_ 'attempt') -eq 2 })
$realRepositoryCompletedEvents = @($controllerEvents | Where-Object { (Test-ExactString (Get-PropertyValue $_ 'stage') 'real-repository-query-completed') -and [int](Get-PropertyValue $_ 'attempt') -eq 2 })
$attemptTwoQuerySnapshots = @(Get-SnapshotByStage $snapshots 'initial-query-entered' 2)
$readySnapshots = @(Get-SnapshotByStage $snapshots 'ready' 2)
if ($loadingSnapshots.Count -gt 0) {
    $loading = $loadingSnapshots[-1]
    Require-True (Test-TrueValue (Get-PropertyValue $loading 'isLoading')) 'Loading snapshot does not have IsLoading=true.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $loading 'isReady'))) 'Loading snapshot has IsReady=true.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $loading 'hasLoadError'))) 'Loading snapshot has an error.'
}
if ($errorSnapshots.Count -gt 0) {
    $errorSnapshot = $errorSnapshots[-1]
    Require-True (-not (Test-TrueValue (Get-PropertyValue $errorSnapshot 'isLoading'))) 'Attempt 1 error snapshot still has IsLoading=true.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $errorSnapshot 'isReady'))) 'Attempt 1 error snapshot has IsReady=true.'
    Require-True (Test-TrueValue (Get-PropertyValue $errorSnapshot 'hasLoadError')) 'Attempt 1 error snapshot does not have HasLoadError=true.'
    Require-Equal (Get-PropertyValue $errorSnapshot 'visibleAssetCount') 0 'Attempt 1 error snapshot visible asset count mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $errorSnapshot 'exceptionType') $contract.state_chain.recoverable_exception_type) 'Attempt 1 error snapshot exception type mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $errorSnapshot 'injectionId') $contract.state_chain.recoverable_injection_id) 'Attempt 1 error snapshot injection id mismatch.'
}
if ($injectionControllerEvents.Count -eq 1) {
    Require-True (Test-ExactString (Get-PropertyValue $injectionControllerEvents[0] 'exceptionType') $contract.state_chain.recoverable_exception_type) 'Attempt 1 controller injection exception type mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $injectionControllerEvents[0] 'injectionId') $contract.state_chain.recoverable_injection_id) 'Attempt 1 controller injection id mismatch.'
}
if ($attemptTwoQuerySnapshots.Count -gt 0) {
    Require-True (Test-TrueValue (Get-PropertyValue $attemptTwoQuerySnapshots[-1] 'isLoading')) 'Attempt 2 did not enter the real repository while loading.'
}
if ($readySnapshots.Count -gt 0) {
    $ready = $readySnapshots[-1]
    Require-True (-not (Test-TrueValue (Get-PropertyValue $ready 'isLoading'))) 'Attempt 2 ready snapshot still has IsLoading=true.'
    Require-True (Test-TrueValue (Get-PropertyValue $ready 'isReady')) 'Attempt 2 ready snapshot does not have IsReady=true.'
    Require-True (-not (Test-TrueValue (Get-PropertyValue $ready 'hasLoadError'))) 'Attempt 2 ready snapshot still has an error.'
    Require-Equal (Get-PropertyValue $ready 'visibleAssetCount') 0 'Attempt 2 ready snapshot is not empty.'
    Require-True (Test-ExactString (Get-PropertyValue $ready 'repositorySource') $contract.state_chain.repository_source) 'Attempt 2 ready snapshot is not backed by the real repository.'
    Require-True (Test-ExactString (Get-PropertyValue $ready 'repositoryImplementation') $contract.state_chain.repository_implementation) 'Attempt 2 ready snapshot is not backed by the required SQLite repository implementation.'
    Require-Equal (Get-PropertyValue $ready 'repositorySchemaVersion') $contract.state_chain.repository_schema_version 'Attempt 2 repository schema mismatch.'
    Require-Equal (Get-PropertyValue $ready 'repositoryAssetCount') $contract.state_chain.repository_asset_count 'Attempt 2 repository count mismatch.'
    if ($realRepositoryCompletedEvents.Count -eq 1) {
        Require-Equal (Get-PropertyValue $realRepositoryCompletedEvents[0] 'repositoryAssetCount') $contract.state_chain.repository_asset_count 'Attempt 2 real repository completion count mismatch.'
    }
    if ($null -ne $scenarioManifest) {
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'repositorySource') (Get-PropertyValue $ready 'repositorySource')) 'Retry manifest repositorySource does not match the raw ready snapshot.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'repositoryImplementation') (Get-PropertyValue $ready 'repositoryImplementation')) 'Retry manifest repositoryImplementation does not match the raw ready snapshot.'
        Require-Equal (Get-PropertyValue $scenarioManifest 'repositorySchemaVersion') (Get-PropertyValue $ready 'repositorySchemaVersion') 'Retry manifest schema does not match the raw ready snapshot.'
        Require-Equal (Get-PropertyValue $scenarioManifest 'repositoryAssetCount') (Get-PropertyValue $ready 'repositoryAssetCount') 'Retry manifest count does not match the raw ready snapshot.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'repositoryProofStage') 'ready') 'Retry manifest repository proof is not the final ready state.'
        $readyRecordedAt = Convert-ToTimestamp (Get-PropertyValue $ready 'recordedAt') 'Attempt 2 ready recordedAt'
        $proofRecordedAt = Convert-ToTimestamp (Get-PropertyValue $scenarioManifest 'repositoryProofRecordedAt') 'Retry manifest repositoryProofRecordedAt'
        Require-True ($proofRecordedAt -eq $readyRecordedAt) 'Retry manifest repository proof timestamp does not match the raw ready snapshot.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'exceptionType') $contract.state_chain.recoverable_exception_type) 'Retry manifest recoverable exception type mismatch.'
        Require-True (Test-ExactString (Get-PropertyValue $scenarioManifest 'injectionId') $contract.state_chain.recoverable_injection_id) 'Retry manifest recoverable injection id mismatch.'
        Require-Equal (Get-PropertyValue $scenarioManifest 'failureAttempt') 1 'Retry manifest failure attempt mismatch.'
    }
}

$stateSummaryById = @{}
foreach ($item in $stateSummaries) { $stateSummaryById[[string]$item.Scene.id] = $item.Summary }
if ($loadingControllerEvents.Count -gt 0 -and $releasedControllerEvents.Count -gt 0 -and $stateSummaryById.ContainsKey('loading')) {
    $waitingAt = Convert-ToTimestamp (Get-PropertyValue $loadingControllerEvents[0] 'recordedAt') 'loading-barrier-waiting recordedAt'
    $releasedAt = Convert-ToTimestamp (Get-PropertyValue $releasedControllerEvents[-1] 'recordedAt') 'loading-barrier-released recordedAt'
    $capturedAt = $stateSummaryById['loading'].CapturedAt
    Require-True ($capturedAt -ge $waitingAt -and $capturedAt -le $releasedAt) 'Loading screenshot was not captured while the real loading gate was held.'
}
if ($errorSnapshots.Count -gt 0 -and $realRepositoryEvents.Count -gt 0 -and $stateSummaryById.ContainsKey('recoverable-error')) {
    $errorAt = Convert-ToTimestamp (Get-PropertyValue $errorSnapshots[-1] 'recordedAt') 'error-visible recordedAt'
    $attemptTwoAt = Convert-ToTimestamp (Get-PropertyValue $realRepositoryEvents[0] 'recordedAt') 'real-repository-query-entered recordedAt'
    $capturedAt = $stateSummaryById['recoverable-error'].CapturedAt
    Require-True ($capturedAt -ge $errorAt -and $capturedAt -le $attemptTwoAt) 'Recoverable-error screenshot was not captured between attempt 1 error and attempt 2.'
}
if ($readySnapshots.Count -gt 0) {
    $readyAt = Convert-ToTimestamp (Get-PropertyValue $readySnapshots[-1] 'recordedAt') 'ready recordedAt'
    if ($stateSummaryById.ContainsKey('retry-recovered')) {
        Require-True ($stateSummaryById['retry-recovered'].CapturedAt -ge $readyAt) "State 'retry-recovered' was captured before attempt 2 reached the real empty repository."
    }
}

# The pristine first-empty scene is a separate process/session with its own raw streams.
$firstSessionContract = @($contract.state_chain.sessions | Where-Object { Test-ExactString $_.id 'first-empty-session' }) | Select-Object -First 1
$firstCandidates = @($scenarioCandidates | Where-Object { Test-ExactString (Get-PropertyValue $_.Json 'scenario') ([string]$firstSessionContract.scenario) })
Require-Equal $firstCandidates.Count 1 'The run must contain exactly one pristine first-empty state session.'
$firstSnapshots = @()
$firstControllerEvents = @()
$firstTimeline = @()
if ($firstCandidates.Count -eq 1) {
    $firstManifestFile = $firstCandidates[0].File
    $firstManifest = $firstCandidates[0].Json
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'protocol') $contract.state_chain.scenario_protocol) 'First-empty scenario protocol mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'processName') ([IO.Path]::GetFileNameWithoutExtension($contract.expected_executable_name))) 'First-empty process name mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'startRouteSource') $contract.acceptance_start_route.environment_variable) 'First-empty manifest start-route source mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'startRoute') $contract.acceptance_start_route.route) 'First-empty manifest start route mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'startRouteCurrentPage') $contract.acceptance_start_route.current_page) 'First-empty manifest start-route current page mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'startRouteHead') $trustedSourceHead) 'First-empty manifest start-route HEAD differs from build authority.'
    [void](Convert-ToTimestamp (Get-PropertyValue $firstManifest 'startRouteRecordedAt') 'First-empty manifest startRouteRecordedAt')
    $firstIsolatedRoot = [string](Get-PropertyValue $firstManifest 'isolatedRoot')
    Require-True (Test-FullyQualifiedPath $firstIsolatedRoot) 'First-empty isolatedRoot is not absolute runtime evidence.'
    if (Test-FullyQualifiedPath $firstIsolatedRoot) {
        Require-True (Test-PathAtOrInsideRoot $firstIsolatedRoot $resolvedRunRoot) 'First-empty isolatedRoot is outside the supplied run root.'
        Require-True (Test-PathInsideRoot $firstManifestFile.FullName $firstIsolatedRoot) 'First-empty manifest is outside its isolated root.'
    }
    Require-True (Test-TrueValue (Get-PropertyValue $firstManifest 'freshDatabaseVerified')) 'First-empty manifest did not record a fresh database verification.'
    $firstDatabasePath = [string](Get-PropertyValue $firstManifest 'databasePath')
    Require-True (Test-FullyQualifiedPath $firstDatabasePath) 'First-empty manifest databasePath is not absolute runtime evidence.'
    if ((Test-FullyQualifiedPath $firstDatabasePath) -and (Test-FullyQualifiedPath $firstIsolatedRoot)) {
        Require-True (Test-PathInsideRoot $firstDatabasePath $firstIsolatedRoot) 'First-empty databasePath is outside its isolated root.'
        Require-True (Test-Path -LiteralPath $firstDatabasePath -PathType Leaf) 'First-empty fresh database file does not exist.'
    }
    if ($scenarioFiles.Count -eq 1) {
        Require-True (-not [string]::Equals($firstManifestFile.DirectoryName, $scenarioFiles[0].DirectoryName, [StringComparison]::OrdinalIgnoreCase)) 'First-empty and retry sessions reuse one evidence directory.'
    }

    $firstState = @($stateSummaries | Where-Object { Test-ExactString $_.Scene.session_id 'first-empty-session' }) | Select-Object -First 1
    if ($null -ne $firstState) {
        Require-Equal (Get-PropertyValue $firstManifest 'processId') $firstState.Summary.ProcessId 'First-empty manifest PID does not match its state capture.'
    }

    $firstSnapshotFile = Join-Path $firstManifestFile.DirectoryName ([string]$contract.state_chain.snapshot_file)
    $firstControllerFile = Join-Path $firstManifestFile.DirectoryName ([string]$contract.state_chain.controller_event_file)
    Require-True (Test-Path -LiteralPath $firstSnapshotFile -PathType Leaf) 'First-empty snapshot JSONL is missing.'
    Require-True (Test-Path -LiteralPath $firstControllerFile -PathType Leaf) 'First-empty controller-event JSONL is missing.'
    $firstManifestSnapshotFile = [string](Get-PropertyValue $firstManifest 'snapshotFile')
    Require-True (Test-FullyQualifiedPath $firstManifestSnapshotFile) 'First-empty manifest snapshotFile is not absolute runtime evidence.'
    if ((Test-FullyQualifiedPath $firstManifestSnapshotFile) -and (Test-Path -LiteralPath $firstSnapshotFile -PathType Leaf)) {
        Require-True ([string]::Equals([IO.Path]::GetFullPath($firstManifestSnapshotFile), [IO.Path]::GetFullPath($firstSnapshotFile), [StringComparison]::OrdinalIgnoreCase)) 'First-empty manifest does not identify its raw snapshot file.'
    }
    $manifestControllerEventFile = [string](Get-PropertyValue $firstManifest 'controllerEventFile')
    Require-True (Test-FullyQualifiedPath $manifestControllerEventFile) 'First-empty manifest controllerEventFile is not absolute runtime evidence.'
    if ((Test-FullyQualifiedPath $manifestControllerEventFile) -and (Test-Path -LiteralPath $firstControllerFile -PathType Leaf)) {
        Require-True ([string]::Equals([IO.Path]::GetFullPath($manifestControllerEventFile), [IO.Path]::GetFullPath($firstControllerFile), [StringComparison]::OrdinalIgnoreCase)) 'First-empty manifest does not identify its raw controller event file.'
    }

    if (Test-Path -LiteralPath $firstSnapshotFile -PathType Leaf) {
        $lineNumber = 0
        foreach ($line in @(Get-Content -LiteralPath $firstSnapshotFile -Encoding UTF8)) {
            $lineNumber++
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try { $envelope = $line | ConvertFrom-Json }
            catch {
                Add-Failure "First-empty snapshot JSONL line $lineNumber is invalid JSON."
                continue
            }
            Require-True (Test-ExactString (Get-PropertyValue $envelope 'protocol') $contract.state_chain.scenario_protocol) "First-empty snapshot JSONL line $lineNumber protocol mismatch."
            $snapshot = Get-PropertyValue $envelope 'snapshot'
            if ($null -eq $snapshot) {
                Add-Failure "First-empty snapshot JSONL line $lineNumber has no snapshot."
                continue
            }
            $entry = [pscustomobject]@{
                Sequence = [long](Get-PropertyValue $envelope 'sequence')
                Stream = 'snapshot'
                Stage = [string](Get-PropertyValue $snapshot 'stage')
                Attempt = [int](Get-PropertyValue $snapshot 'attempt')
                RecordedAt = Convert-ToTimestamp (Get-PropertyValue $snapshot 'recordedAt') "First-empty snapshot JSONL line $lineNumber recordedAt"
            }
            $firstSnapshots += $snapshot
            $firstTimeline += $entry
        }
    }
    if (Test-Path -LiteralPath $firstControllerFile -PathType Leaf) {
        $lineNumber = 0
        foreach ($line in @(Get-Content -LiteralPath $firstControllerFile -Encoding UTF8)) {
            $lineNumber++
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try { $controllerEvent = $line | ConvertFrom-Json }
            catch {
                Add-Failure "First-empty controller-event JSONL line $lineNumber is invalid JSON."
                continue
            }
            Require-True (Test-ExactString (Get-PropertyValue $controllerEvent 'protocol') $contract.state_chain.scenario_protocol) "First-empty controller-event JSONL line $lineNumber protocol mismatch."
            $entry = [pscustomobject]@{
                Sequence = [long](Get-PropertyValue $controllerEvent 'sequence')
                Stream = 'controller'
                Stage = [string](Get-PropertyValue $controllerEvent 'stage')
                Attempt = [int](Get-PropertyValue $controllerEvent 'attempt')
                RecordedAt = Convert-ToTimestamp (Get-PropertyValue $controllerEvent 'recordedAt') "First-empty controller-event JSONL line $lineNumber recordedAt"
            }
            $firstControllerEvents += $controllerEvent
            $firstTimeline += $entry
        }
    }

    $firstTimeline = @($firstTimeline | Sort-Object Sequence)
    $expectedSequence = 1L
    $previousRecordedAt = [DateTimeOffset]::MinValue
    foreach ($entry in $firstTimeline) {
        Require-Equal ([long]$entry.Sequence) $expectedSequence 'First-empty snapshot/event sequence has a duplicate or gap.'
        Require-True ($entry.RecordedAt -ge $previousRecordedAt) 'First-empty snapshot/event timestamps move backwards.'
        $expectedSequence++
        $previousRecordedAt = $entry.RecordedAt
    }
    $previousRequiredSequence = 0L
    foreach ($requiredEntry in @($firstSessionContract.required_timeline)) {
        $matches = @($firstTimeline | Where-Object {
                (Test-ExactString $_.Stream ([string]$requiredEntry.stream)) -and
                (Test-ExactString $_.Stage ([string]$requiredEntry.stage)) -and
                [int]$_.Attempt -eq [int]$requiredEntry.attempt
            })
        Require-Equal $matches.Count 1 "First-empty timeline entry '$($requiredEntry.stream):$($requiredEntry.stage):$($requiredEntry.attempt)' must occur exactly once."
        if ($matches.Count -eq 1) {
            Require-True ([long]$matches[0].Sequence -gt $previousRequiredSequence) "First-empty timeline entry '$($requiredEntry.stage)' is out of order."
            $previousRequiredSequence = [long]$matches[0].Sequence
        }
    }
    Require-Equal $firstTimeline.Count @($firstSessionContract.required_timeline).Count 'First-empty state timeline contains undeclared entries.'

    $firstReadySnapshots = @(Get-SnapshotByStage $firstSnapshots 'ready' 1)
    Require-Equal $firstReadySnapshots.Count 1 'First-empty session must have exactly one attempt-1 ready snapshot.'
    if ($firstReadySnapshots.Count -eq 1) {
        $firstReady = $firstReadySnapshots[0]
        Require-True (-not (Test-TrueValue (Get-PropertyValue $firstReady 'isLoading'))) 'First-empty ready snapshot still has IsLoading=true.'
        Require-True (Test-TrueValue (Get-PropertyValue $firstReady 'isReady')) 'First-empty ready snapshot does not have IsReady=true.'
        Require-True (-not (Test-TrueValue (Get-PropertyValue $firstReady 'hasLoadError'))) 'First-empty ready snapshot has an error.'
        Require-Equal (Get-PropertyValue $firstReady 'visibleAssetCount') 0 'First-empty ready snapshot is not empty.'
        Require-True (Test-ExactString (Get-PropertyValue $firstReady 'repositorySource') $contract.state_chain.repository_source) 'First-empty snapshot is not backed by the real repository.'
        Require-True (Test-ExactString (Get-PropertyValue $firstReady 'repositoryImplementation') $contract.state_chain.repository_implementation) 'First-empty snapshot is not backed by the required SQLite repository implementation.'
        Require-Equal (Get-PropertyValue $firstReady 'repositorySchemaVersion') $contract.state_chain.repository_schema_version 'First-empty repository schema mismatch.'
        Require-Equal (Get-PropertyValue $firstReady 'repositoryAssetCount') $contract.state_chain.repository_asset_count 'First-empty repository count mismatch.'
        $firstCompletedEvents = @($firstControllerEvents | Where-Object {
                (Test-ExactString (Get-PropertyValue $_ 'stage') 'real-repository-query-completed') -and
                [int](Get-PropertyValue $_ 'attempt') -eq 1
            })
        if ($firstCompletedEvents.Count -eq 1) {
            Require-Equal (Get-PropertyValue $firstCompletedEvents[0] 'repositoryAssetCount') $contract.state_chain.repository_asset_count 'First-empty real repository completion count mismatch.'
        }
        Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'repositorySource') (Get-PropertyValue $firstReady 'repositorySource')) 'First-empty manifest repositorySource does not match the raw ready snapshot.'
        Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'repositoryImplementation') (Get-PropertyValue $firstReady 'repositoryImplementation')) 'First-empty manifest repositoryImplementation does not match the raw ready snapshot.'
        Require-Equal (Get-PropertyValue $firstManifest 'repositorySchemaVersion') (Get-PropertyValue $firstReady 'repositorySchemaVersion') 'First-empty manifest schema does not match the raw ready snapshot.'
        Require-Equal (Get-PropertyValue $firstManifest 'repositoryAssetCount') (Get-PropertyValue $firstReady 'repositoryAssetCount') 'First-empty manifest count does not match the raw ready snapshot.'
        Require-True (Test-ExactString (Get-PropertyValue $firstManifest 'repositoryProofStage') 'ready') 'First-empty manifest repository proof is not the final ready state.'
        $firstProofAt = Convert-ToTimestamp (Get-PropertyValue $firstManifest 'repositoryProofRecordedAt') 'First-empty manifest repositoryProofRecordedAt'
        $firstReadyAt = Convert-ToTimestamp (Get-PropertyValue $firstReady 'recordedAt') 'First-empty ready recordedAt'
        Require-True ($firstProofAt -eq $firstReadyAt) 'First-empty manifest repository proof timestamp does not match the raw ready snapshot.'
        if ($stateSummaryById.ContainsKey('first-empty')) {
            Require-True ($stateSummaryById['first-empty'].CapturedAt -ge $firstReadyAt) 'First-empty screenshot was captured before the pristine repository reached ready.'
        }
    }
}

# Consume every scene's declared raw stream/stage/attempt and bind it to the visible-state semantics.
foreach ($scene in @($contract.state_chain.scenes)) {
    $source = if (Test-ExactString $scene.session_id 'first-empty-session') {
        if (Test-ExactString $scene.evidence_stream 'snapshot') { $firstSnapshots } else { $firstControllerEvents }
    }
    else {
        if (Test-ExactString $scene.evidence_stream 'snapshot') { $snapshots } else { $controllerEvents }
    }
    $matches = @($source | Where-Object {
            (Test-ExactString (Get-PropertyValue $_ 'stage') ([string]$scene.snapshot_stage)) -and
            [int](Get-PropertyValue $_ 'attempt') -eq [int]$scene.snapshot_attempt
        })
    Require-Equal $matches.Count 1 "State '$($scene.id)' does not resolve to exactly one declared raw stream/stage/attempt."
    switch ([string]$scene.automation_id) {
        'AssetLibraryLoadingState' { Require-True (Test-ExactString $scene.id 'loading') "State '$($scene.id)' uses the wrong loading AutomationId." }
        'AssetLibraryErrorState' { Require-True (Test-ExactString $scene.id 'recoverable-error') "State '$($scene.id)' uses the wrong error AutomationId." }
        'AssetLibraryEmptyState' { Require-True ((Test-ExactString $scene.id 'first-empty') -or (Test-ExactString $scene.id 'retry-recovered')) "State '$($scene.id)' uses the wrong empty-state AutomationId." }
        default { Add-Failure "State '$($scene.id)' declares an unrecognized AutomationId." }
    }
}

# Physical retry must be exactly one verified real mouse or keyboard activation and must bridge error to readiness.
$pointerDocuments = @()
if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter '*physical-pointer*.json')) {
        $json = Read-JsonFile $file.FullName
        if ($null -ne $json -and (Test-ExactString (Get-PropertyValue $json 'protocol') $contract.splitter_keyboard.diagnostic_protocol)) {
            $pointerDocuments += [pscustomobject]@{ Path = $file.FullName; Json = $json }
        }
    }
}
foreach ($document in $pointerDocuments) {
    $documentProcessId = [int](Get-PropertyValue $document.Json 'process_id')
    $sessionMatches = @($manualSessionsById.Values | Where-Object { [int](Get-PropertyValue $_ 'process_id') -eq $documentProcessId })
    Require-Equal $sessionMatches.Count 1 "Physical diagnostic '$($document.Path)' does not belong to exactly one manual-run session."
}
$retryMouseRecords = @()
$retryKeyboardRecords = @()
$retryKeyboardContract = Get-PropertyValue $contract.retry_physical_activation 'keyboard'
$retryAllowedKeys = @(Get-PropertyValue $retryKeyboardContract 'allowed_keys')
$retryRequiredWpfEvents = @(Get-PropertyValue $retryKeyboardContract 'required_layer2_events')
$retryForbiddenWpfEvents = @(Get-PropertyValue $retryKeyboardContract 'forbidden_layer2_events')
$retryUsesKeyDownNativeUpFinalization =
    $retryAllowedKeys.Count -eq 1 -and
    (Test-ExactString $retryAllowedKeys[0] 'Enter') -and
    (Test-ExactString (Get-PropertyValue $retryKeyboardContract 'completion_phase') 'KeyDown') -and
    (Test-StrictScalarPropertyValue $retryKeyboardContract 'native_key_up_finalization_required' ([bool]) $true) -and
    (Test-StrictScalarPropertyValue $retryKeyboardContract 'native_key_up_focus_policy' ([string]) 'different-focus-or-unavailable-original-target') -and
    $retryRequiredWpfEvents.Count -eq 2 -and
    (Test-ExactString $retryRequiredWpfEvents[0] 'PreviewKeyDown') -and
    (Test-ExactString $retryRequiredWpfEvents[1] 'KeyDown') -and
    $retryForbiddenWpfEvents.Count -eq 2 -and
    (Test-ExactString $retryForbiddenWpfEvents[0] 'PreviewKeyUp') -and
    (Test-ExactString $retryForbiddenWpfEvents[1] 'KeyUp')
Require-True $retryUsesKeyDownNativeUpFinalization 'Retry keyboard contract must require Enter KeyDown completion, exactly the two WPF down events, forbid both WPF up events, require native-key-up finalization, and distinguish a different focus from an unavailable original-target focus reference.'
$retryStateIdentity = @($stateSummaries | Where-Object { Test-ExactString $_.Scene.session_id 'retry-session' }) | Select-Object -First 1
$retryManualSession = $manualSessionsById['retry-session']
foreach ($document in $pointerDocuments) {
    if ($null -ne $retryStateIdentity -and [int](Get-PropertyValue $document.Json 'process_id') -ne $retryStateIdentity.Summary.ProcessId) { continue }
    if ($null -eq $retryManualSession -or [int](Get-PropertyValue $document.Json 'process_id') -ne [int](Get-PropertyValue $retryManualSession 'process_id')) { continue }
    foreach ($attempt in @(Get-PropertyValue $document.Json 'attempts')) {
        if ((Test-ExactString (Get-PropertyValue $attempt 'down_target_automation_id') $contract.retry_physical_activation.automation_id) -and
            (Test-ExactString (Get-PropertyValue $attempt 'up_target_automation_id') $contract.retry_physical_activation.automation_id)) {
            $retryMouseRecords += [pscustomobject]@{ Document = $document; Attempt = $attempt; Mode = 'mouse' }
        }
    }
    foreach ($attempt in @(Get-PropertyValue $document.Json 'key_attempts')) {
        foreach ($allowedKey in @($contract.retry_physical_activation.keyboard.allowed_keys)) {
            if (Test-KeyInputLayers $attempt ([string]$contract.retry_physical_activation.automation_id) ([string]$allowedKey) @($contract.retry_physical_activation.keyboard.required_layer2_events) $retryUsesKeyDownNativeUpFinalization) {
                $retryKeyboardRecords += [pscustomobject]@{ Document = $document; Attempt = $attempt; Mode = 'keyboard' }
            }
        }
    }
}
Require-Equal ($retryMouseRecords.Count + $retryKeyboardRecords.Count) 1 'RetryAssetLibraryLoad must have exactly one verified physical mouse or keyboard activation.'
if ($retryMouseRecords.Count -eq 1 -and $retryKeyboardRecords.Count -eq 0) {
    $retry = $retryMouseRecords[0].Attempt
    Require-True (Test-ExactString (Get-PropertyValue $retry 'origin') 'Win32') 'Retry pointer Layer 1 origin is not Win32.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer1_win32', 'l_button_down_received'))) 'Retry pointer Layer 1 is missing WM_LBUTTONDOWN.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer1_win32', 'l_button_up_received'))) 'Retry pointer Layer 1 is missing WM_LBUTTONUP.'
    [void](Convert-ToTimestamp (Get-NestedValue $retry @('layer1_win32', 'down', 'timestamp')) 'Retry pointer Layer 1 down timestamp')
    [void](Convert-ToTimestamp (Get-NestedValue $retry @('layer1_win32', 'up', 'timestamp')) 'Retry pointer Layer 1 up timestamp')
    $mouseEvents = @(Get-NestedValue $retry @('layer2_wpf', 'events'))
    foreach ($eventName in @($contract.retry_physical_activation.mouse.required_layer2_events)) {
        Require-Equal @($mouseEvents | Where-Object { Test-ExactString (Get-PropertyValue $_ 'event_name') ([string]$eventName) }).Count 1 "Retry pointer Layer 2 event '$eventName' must occur exactly once."
    }
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer2_wpf', 'preview_mouse_down_received'))) 'Retry pointer Layer 2 is missing preview down.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer2_wpf', 'preview_mouse_up_received'))) 'Retry pointer Layer 2 is missing preview up.'
    Require-True (Test-ExactString (Get-PropertyValue $retry 'down_control_automation_id') $contract.retry_physical_activation.automation_id) 'Retry pointer Layer 3 down control mismatch.'
    Require-True (Test-ExactString (Get-PropertyValue $retry 'up_control_automation_id') $contract.retry_physical_activation.automation_id) 'Retry pointer Layer 3 up control mismatch.'
    Require-True (Test-TrueValue (Get-PropertyValue $retry 'button_instance_same_down_up')) 'Retry pointer Layer 3 did not retain the same button instance.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer4_action', 'button_click_received'))) 'Retry pointer Layer 4 has no Button.Click.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer4_action', 'physical_target_confirmed'))) 'Retry pointer Layer 4 did not confirm the physical target.'
    Require-True (Test-ExactString (Get-NestedValue $retry @('layer4_action', 'button', 'automation_id')) $contract.retry_physical_activation.automation_id) 'Retry pointer Layer 4 button AutomationId mismatch.'
}
elseif ($retryKeyboardRecords.Count -eq 1 -and $retryMouseRecords.Count -eq 0) {
    $retry = $retryKeyboardRecords[0].Attempt
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer4_action', 'button_click_received'))) 'Retry keyboard Layer 4 has no Button.Click.'
    Require-True (Test-TrueValue (Get-NestedValue $retry @('layer4_action', 'physical_target_confirmed'))) 'Retry keyboard Layer 4 did not confirm the focused Retry target.'
    Require-True (Test-ExactString (Get-NestedValue $retry @('layer4_action', 'button', 'automation_id')) $contract.retry_physical_activation.automation_id) 'Retry keyboard Layer 4 button AutomationId mismatch.'
}
if (($retryMouseRecords.Count + $retryKeyboardRecords.Count) -eq 1) {
    $retry = @($retryMouseRecords + $retryKeyboardRecords)[0].Attempt
    if ($errorSnapshots.Count -gt 0 -and $readySnapshots.Count -gt 0) {
        $retryAt = Convert-ToTimestamp (Get-PropertyValue $retry 'started_at') 'Retry physical activation started_at'
        $errorAt = Convert-ToTimestamp (Get-PropertyValue $errorSnapshots[-1] 'recordedAt') 'error-visible recordedAt'
        $readyAt = Convert-ToTimestamp (Get-PropertyValue $readySnapshots[-1] 'recordedAt') 'ready recordedAt'
        Require-True ($retryAt -ge $errorAt -and $retryAt -le $readyAt) 'Retry physical activation is not between attempt 1 error and attempt 2 readiness.'
    }
}

# One diagnostic session must contain all four ordinary splitter key attempts and their linked transitions.
$keyboardDocument = $null
$regularTransitionsByControl = @{}
$windowEvidenceProcessIds = @($windowRecords | ForEach-Object { [int](Get-NestedValue $_.Json @('process', 'process_id')) } | Select-Object -Unique)
$keyboardManualSession = $manualSessionsById['keyboard-session']
foreach ($document in $pointerDocuments) {
    $documentProcessId = [int](Get-PropertyValue $document.Json 'process_id')
    if ($documentProcessId -le 0 -or $documentProcessId -notin $windowEvidenceProcessIds) { continue }
    if ($null -eq $keyboardManualSession -or $documentProcessId -ne [int](Get-PropertyValue $keyboardManualSession 'process_id')) { continue }
    $attempts = @(Get-PropertyValue $document.Json 'key_attempts')
    $transitions = @(Get-PropertyValue $document.Json 'control_state_transitions')
    $allFound = $true
    $candidateMap = @{}
    foreach ($control in @($contract.splitter_keyboard.controls)) {
        foreach ($key in @('Left', 'Right')) {
            $qualified = @()
            foreach ($attempt in $attempts) {
                if (-not (Test-KeyInputLayers $attempt ([string]$control.automation_id) $key @($contract.splitter_keyboard.required_layer2_events))) { continue }
                $linked = Get-TransitionForAttempt $transitions $attempt ([string]$control.automation_id) $key
                $delta = if ($key -ceq 'Left') { [string]$control.left_delta } else { [string]$control.right_delta }
                foreach ($transition in $linked) {
                    if (Test-RegularKeyTransition $transition $attempt $delta $control) {
                        $qualified += [pscustomobject]@{ Attempt = $attempt; Transition = $transition }
                    }
                }
            }
            if ($qualified.Count -ne 1) { $allFound = $false }
            else { $candidateMap["$($control.automation_id)|$key"] = $qualified[0] }
        }
    }
    if ($allFound) {
        $keyboardDocument = $document
        $regularTransitionsByControl = $candidateMap
        break
    }
}
$keyboardRestoreByPrefix = @{}
Require-True ($null -ne $keyboardDocument) 'No single physical diagnostic session contains all four splitter Left/Right Layer 1-4 attempts.'

if ($null -ne $keyboardDocument) {
    Require-True (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $keyboardDocument.Json 'diagnostic_id'))) 'Keyboard diagnostic has no diagnostic_id.'
    [void](Convert-ToTimestamp (Get-PropertyValue $keyboardDocument.Json 'process_started_at') 'Keyboard diagnostic process_started_at')
    $attempts = @(Get-PropertyValue $keyboardDocument.Json 'key_attempts')
    $transitions = @(Get-PropertyValue $keyboardDocument.Json 'control_state_transitions')
    foreach ($control in @($contract.splitter_keyboard.controls)) {
        foreach ($key in @('Left', 'Right')) {
            $mapKey = "$($control.automation_id)|$key"
            Require-True ($regularTransitionsByControl.ContainsKey($mapKey)) "Missing same-attempt Layer 1-4 evidence for $mapKey."
        }

        foreach ($boundaryName in @('minimum', 'maximum')) {
            $boundary = [double](Get-PropertyValue $control $boundaryName)
            $boundaryKeyName = "${boundaryName}_boundary_key"
            $boundaryKey = [string](Get-PropertyValue $control $boundaryKeyName)
            $qualifiedBoundaries = @()
            foreach ($transition in $transitions) {
                if (-not (Test-ExactString (Get-PropertyValue $transition 'input_kind') 'Keyboard') -or
                    -not (Test-ExactString (Get-PropertyValue $transition 'input_key') $boundaryKey) -or
                    -not (Test-ExactString (Get-NestedValue $transition @('control', 'automation_id')) ([string]$control.automation_id))) { continue }
                $attemptId = [string](Get-PropertyValue $transition 'correlated_key_attempt_id')
                $attempt = @($attempts | Where-Object { Test-ExactString (Get-PropertyValue $_ 'attempt_id') $attemptId }) | Select-Object -First 1
                if ($null -ne $attempt -and
                    (Test-KeyInputLayers $attempt ([string]$control.automation_id) $boundaryKey @($contract.splitter_keyboard.required_layer2_events)) -and
                    (Test-BoundaryTransition $transition $attempt $boundary)) {
                    $qualifiedBoundaries += $transition
                }
            }
            Require-Equal $qualifiedBoundaries.Count 1 "$($control.automation_id) must have exactly one physical $boundaryName boundary no-op attempt."
        }

        $prefix = if ([string]$control.automation_id -ceq 'AssetOrganizationSplitter') { 'organization' } else { 'inspector' }
        $latestRegular = [DateTimeOffset]::MinValue
        $latestRegularTransition = $null
        foreach ($key in @('Left', 'Right')) {
            $candidateTransition = $regularTransitionsByControl["$($control.automation_id)|$key"].Transition
            $completed = Convert-ToTimestamp (Get-PropertyValue $candidateTransition 'completed_at') "$($control.automation_id) $key completed_at"
            if ($completed -gt $latestRegular) {
                $latestRegular = $completed
                $latestRegularTransition = $candidateTransition
            }
        }
        $keyboardAdjustedWidth = [double](Get-PropertyValue $latestRegularTransition 'after_persisted_value')
        $restoreSnapshots = @(Get-PropertyValue $keyboardDocument.Json 'workspace_restore_snapshots')
        $collapsedCandidates = @($restoreSnapshots | Where-Object {
                (Test-TrueValue (Get-PropertyValue $_ "${prefix}_collapsed")) -and
                -not (Test-TrueValue (Get-PropertyValue $_ "${prefix}_visible")) -and
                (Convert-ToTimestamp (Get-PropertyValue $_ 'timestamp') "$prefix collapsed timestamp") -gt $latestRegular -and
                [Math]::Abs([double](Get-PropertyValue $_ "${prefix}_persisted_width") - $keyboardAdjustedWidth) -le 0.5
            } | Sort-Object { [DateTimeOffset]::Parse([string](Get-PropertyValue $_ 'timestamp')) })
        $collapsed = $collapsedCandidates | Select-Object -First 1
        Require-True ($null -ne $collapsed) "$($control.automation_id) has no post-keyboard collapse snapshot."
        if ($null -ne $collapsed) {
            $collapsedAt = Convert-ToTimestamp (Get-PropertyValue $collapsed 'timestamp') "$prefix collapsed timestamp"
            $persistedWidth = [double](Get-PropertyValue $collapsed "${prefix}_persisted_width")
            Require-True ([Math]::Abs($persistedWidth - $keyboardAdjustedWidth) -le 0.5) "$($control.automation_id) collapse did not retain the latest keyboard-adjusted width."
            $reopenedCandidates = @($restoreSnapshots | Where-Object {
                    -not (Test-TrueValue (Get-PropertyValue $_ "${prefix}_collapsed")) -and
                    (Test-TrueValue (Get-PropertyValue $_ "${prefix}_visible")) -and
                    (Convert-ToTimestamp (Get-PropertyValue $_ 'timestamp') "$prefix reopened timestamp") -gt $collapsedAt -and
                    [Math]::Abs([double](Get-PropertyValue $_ "${prefix}_persisted_width") - $persistedWidth) -le 0.5 -and
                    [Math]::Abs([double](Get-PropertyValue $_ "${prefix}_actual_width") - $persistedWidth) -le 0.5
                } | Sort-Object { [DateTimeOffset]::Parse([string](Get-PropertyValue $_ 'timestamp')) })
            $reopened = $reopenedCandidates | Select-Object -First 1
            Require-True ($null -ne $reopened) "$($control.automation_id) did not reopen at its keyboard-adjusted persisted width."
            if ($null -ne $reopened) {
                $keyboardRestoreByPrefix[$prefix] = [pscustomobject]@{
                    PersistedWidth = $keyboardAdjustedWidth
                    Collapsed = [bool](Get-PropertyValue $reopened "${prefix}_collapsed")
                    Snapshot = $reopened
                }
            }
        }
    }
}

$restartDocuments = @($pointerDocuments | Where-Object {
        $previous = Get-PropertyValue $_.Json 'previous_session'
        $null -ne $previous -and (Test-TrueValue (Get-PropertyValue $previous 'has_workspace_state'))
    })
$restartMatches = @()
$restartDpiManualSession = $manualSessionsById['restart-dpi-session']
if ($null -ne $keyboardDocument -and $keyboardRestoreByPrefix.Count -eq 2) {
    $keyboardDiagnosticId = [string](Get-PropertyValue $keyboardDocument.Json 'diagnostic_id')
    $keyboardProcessId = [int](Get-PropertyValue $keyboardDocument.Json 'process_id')
    $keyboardStartedAt = Convert-ToTimestamp (Get-PropertyValue $keyboardDocument.Json 'process_started_at') 'Keyboard diagnostic process_started_at'
    $keyboardUpdatedAt = Convert-ToTimestamp (Get-PropertyValue $keyboardDocument.Json 'updated_at') 'Keyboard diagnostic updated_at'
    $previousSnapshots = @(Get-PropertyValue $keyboardDocument.Json 'workspace_restore_snapshots' | Sort-Object {
            [DateTimeOffset]::Parse([string](Get-PropertyValue $_ 'timestamp'))
        })
    $previousFinalSnapshot = $previousSnapshots | Select-Object -Last 1
    Require-True ($null -ne $previousFinalSnapshot) 'Keyboard diagnostic has no final raw workspace snapshot for restart comparison.'

    if ($null -ne $previousFinalSnapshot) {
        foreach ($prefix in @('organization', 'inspector')) {
            $expected = $keyboardRestoreByPrefix[$prefix]
            Require-True ($null -ne $expected) "Keyboard diagnostic has no collapse/reopen closure for $prefix."
            if ($null -ne $expected) {
                Require-True ([Math]::Abs([double](Get-PropertyValue $previousFinalSnapshot "${prefix}_persisted_width") - [double]$expected.PersistedWidth) -le 0.5) "Final previous-session $prefix width is not the keyboard-adjusted reopened width."
                Require-Equal ([bool](Get-PropertyValue $previousFinalSnapshot "${prefix}_collapsed")) ([bool]$expected.Collapsed) "Final previous-session $prefix collapse state differs from the reopened state."
            }
        }

        foreach ($document in $restartDocuments) {
            $newProcessId = [int](Get-PropertyValue $document.Json 'process_id')
            $newStartedAt = Convert-ToTimestamp (Get-PropertyValue $document.Json 'process_started_at') 'Restart diagnostic process_started_at'
            $previous = Get-PropertyValue $document.Json 'previous_session'
            if ($newProcessId -le 0 -or $newProcessId -eq $keyboardProcessId -or
                $newProcessId -notin $windowEvidenceProcessIds -or
                $null -eq $restartDpiManualSession -or
                $newProcessId -ne [int](Get-PropertyValue $restartDpiManualSession 'process_id') -or
                -not (Test-ExactString (Get-PropertyValue $previous 'diagnostic_id') $keyboardDiagnosticId) -or
                [int](Get-PropertyValue $previous 'process_id') -ne $keyboardProcessId -or
                $newStartedAt -le $keyboardStartedAt -or $newStartedAt -lt $keyboardUpdatedAt) { continue }

            $previousMatchesRaw = $true
            foreach ($prefix in @('organization', 'inspector')) {
                if ([Math]::Abs([double](Get-PropertyValue $previous "${prefix}_persisted_width") - [double](Get-PropertyValue $previousFinalSnapshot "${prefix}_persisted_width")) -gt 0.5 -or
                    [bool](Get-PropertyValue $previous "${prefix}_collapsed") -ne [bool](Get-PropertyValue $previousFinalSnapshot "${prefix}_collapsed")) {
                    $previousMatchesRaw = $false
                }
            }
            if ([Math]::Abs([double](Get-PropertyValue $previous 'thumbnail_persisted_width') - [double](Get-PropertyValue $previousFinalSnapshot 'thumbnail_persisted_width')) -gt 0.5) {
                $previousMatchesRaw = $false
            }
            if (-not $previousMatchesRaw) { continue }

            foreach ($snapshot in @(Get-PropertyValue $document.Json 'workspace_restore_snapshots')) {
                $snapshotAt = Convert-ToTimestamp (Get-PropertyValue $snapshot 'timestamp') 'Restart workspace snapshot timestamp'
                if ($snapshotAt -lt $newStartedAt -or
                    -not (Test-TrueValue (Get-PropertyValue $snapshot 'restore_confirmed')) -or
                    -not (Test-TrueValue (Get-PropertyValue $snapshot 'restart_comparison_performed')) -or
                    -not (Test-TrueValue (Get-PropertyValue $snapshot 'restart_settings_match_previous_session')) -or
                    -not (Test-ExactString (Get-PropertyValue $snapshot 'previous_diagnostic_id') $keyboardDiagnosticId)) { continue }

                $snapshotMatchesRaw = $true
                foreach ($prefix in @('organization', 'inspector')) {
                    $persisted = [double](Get-PropertyValue $previous "${prefix}_persisted_width")
                    $collapsed = [bool](Get-PropertyValue $previous "${prefix}_collapsed")
                    $actual = [double](Get-PropertyValue $snapshot "${prefix}_actual_width")
                    if ([Math]::Abs([double](Get-PropertyValue $snapshot "${prefix}_persisted_width") - $persisted) -gt 0.5 -or
                        [bool](Get-PropertyValue $snapshot "${prefix}_collapsed") -ne $collapsed -or
                        [bool](Get-PropertyValue $snapshot "${prefix}_visible") -eq $collapsed -or
                        ((-not $collapsed) -and [Math]::Abs($actual - $persisted) -gt 0.5) -or
                        ($collapsed -and [Math]::Abs($actual) -gt 0.5)) {
                        $snapshotMatchesRaw = $false
                    }
                }
                $thumbnailPersisted = [double](Get-PropertyValue $previous 'thumbnail_persisted_width')
                if ([Math]::Abs([double](Get-PropertyValue $snapshot 'thumbnail_persisted_width') - $thumbnailPersisted) -gt 0.5 -or
                    [Math]::Abs([double](Get-PropertyValue $snapshot 'thumbnail_actual_width') - $thumbnailPersisted) -gt 0.5 -or
                    -not (Test-TrueValue (Get-PropertyValue $snapshot 'thumbnail_restore_confirmed'))) {
                    $snapshotMatchesRaw = $false
                }
                if ($snapshotMatchesRaw) {
                    $restartMatches += [pscustomobject]@{ Document = $document; Snapshot = $snapshot }
                }
            }
        }
    }
}
Require-Equal @($restartMatches | ForEach-Object { $_.Document.Path } | Select-Object -Unique).Count 1 'Exactly one new DevPreview process must recompute collapse/reopen settings from the prior raw workspace snapshot.'
Require-True ($restartMatches.Count -ge 1) 'No physical diagnostic proves collapse/reopen settings survived a new process restart.'

# Each real Windows tuple needs exactly one default and one post-action capture from one GUI at one path.
$matrixSummaries = @()
$matrixExecutablePath = $null
$previousTupleInteractionAt = [DateTimeOffset]::MinValue
foreach ($tuple in @($contract.dpi_matrix)) {
    $tupleByKind = @{}
    foreach ($kind in @($contract.dpi_capture_kinds)) {
        $matches = @($windowRecords | Where-Object {
                $name = $_.CaptureName
                $containsAll = $true
                foreach ($token in @($tuple.capture_tokens)) {
                    if ($name.IndexOf([string]$token, [StringComparison]::OrdinalIgnoreCase) -lt 0) { $containsAll = $false }
                }
                $containsAll -and $name.IndexOf([string]$kind, [StringComparison]::OrdinalIgnoreCase) -ge 0
            })
        Require-Equal $matches.Count 1 "DPI tuple $($tuple.width)x$($tuple.height)@$($tuple.scale_percent)% must have exactly one '$kind' capture."
        if ($matches.Count -eq 1) {
            $match = $matches[0]
            $summary = Get-WindowRecordSummary $match $tuple
            $tupleByKind[[string]$kind] = $summary
            $matrixSummaries += $summary
            if (-not [string]::IsNullOrWhiteSpace($summary.Hash)) {
                Require-True ($requiredHashes.Add($summary.Hash)) "DPI capture '$($summary.CaptureName)' duplicates a required screenshot hash."
            }
            if ($null -eq $matrixExecutablePath) { $matrixExecutablePath = $summary.ExecutablePath }
            else {
                Require-True ([string]::Equals($summary.ExecutablePath, $matrixExecutablePath, [StringComparison]::OrdinalIgnoreCase)) "DPI capture '$($summary.CaptureName)' uses a different executable path."
            }
        }
    }
    if ($tupleByKind.ContainsKey('default') -and $tupleByKind.ContainsKey('interaction')) {
        $default = $tupleByKind['default']
        $interaction = $tupleByKind['interaction']
        Require-Equal $interaction.ProcessId $default.ProcessId "DPI tuple $($tuple.width)x$($tuple.height) changed PID between default and interaction."
        Require-True (Test-ExactString $interaction.Hwnd $default.Hwnd) "DPI tuple $($tuple.width)x$($tuple.height) changed HWND between default and interaction."
        Require-True (Test-ExactString $interaction.WindowTitle $default.WindowTitle) "DPI tuple $($tuple.width)x$($tuple.height) changed title between default and interaction."
        Require-True ($default.CapturedAt -gt $previousTupleInteractionAt) "DPI tuple $($tuple.width)x$($tuple.height) was captured out of contract order."
        Require-True ($interaction.CapturedAt -gt $default.CapturedAt) "DPI tuple $($tuple.width)x$($tuple.height) interaction capture does not follow its default capture."
        if ($null -ne $restartDpiManualSession) {
            Require-Equal $default.ProcessId (Get-PropertyValue $restartDpiManualSession 'process_id') "DPI tuple $($tuple.width)x$($tuple.height) does not belong to the restart/DPI session PID."
            Require-True (Test-ExactString $default.Hwnd (Get-PropertyValue $restartDpiManualSession 'window_hwnd')) "DPI tuple $($tuple.width)x$($tuple.height) does not belong to the restart/DPI session HWND."
            Require-True (Test-SameFullPath $default.ExecutablePath ([string](Get-PropertyValue $restartDpiManualSession 'executable_path'))) "DPI tuple $($tuple.width)x$($tuple.height) executable path differs from the restart/DPI session."
            Require-True (Test-ExactString $default.ExecutableHash (Get-PropertyValue $restartDpiManualSession 'executable_sha256')) "DPI tuple $($tuple.width)x$($tuple.height) executable hash differs from the restart/DPI session."
        }

        $physicalMouseActions = @()
        $physicalKeyboardActions = @()
        foreach ($document in @($pointerDocuments | Where-Object { [int](Get-PropertyValue $_.Json 'process_id') -eq $default.ProcessId })) {
            $transitions = @(Get-PropertyValue $document.Json 'control_state_transitions')
            foreach ($attempt in @(Get-PropertyValue $document.Json 'attempts')) {
                $startedAt = [DateTimeOffset]::MinValue
                $completedAt = [DateTimeOffset]::MinValue
                if (-not (Try-GetTimestamp (Get-PropertyValue $attempt 'started_at') ([ref]$startedAt)) -or
                    -not (Try-GetTimestamp (Get-PropertyValue $attempt 'updated_at') ([ref]$completedAt))) { continue }
                if ($startedAt -le $default.CapturedAt -or $completedAt -gt $interaction.CapturedAt -or $completedAt -lt $startedAt -or
                    -not (Test-ExactString (Get-PropertyValue $attempt 'origin') 'Win32') -or
                    -not (Test-TrueValue (Get-NestedValue $attempt @('layer1_win32', 'l_button_down_received'))) -or
                    -not (Test-TrueValue (Get-NestedValue $attempt @('layer1_win32', 'l_button_up_received'))) -or
                    -not (Test-TrueValue (Get-NestedValue $attempt @('layer2_wpf', 'preview_mouse_down_received'))) -or
                    -not (Test-TrueValue (Get-NestedValue $attempt @('layer2_wpf', 'preview_mouse_up_received')))) { continue }
                $events = @(Get-NestedValue $attempt @('layer2_wpf', 'events'))
                if (@($events | Where-Object { Test-ExactString (Get-PropertyValue $_ 'event_name') 'PreviewMouseDown' }).Count -ne 1 -or
                    @($events | Where-Object { Test-ExactString (Get-PropertyValue $_ 'event_name') 'PreviewMouseUp' }).Count -ne 1) { continue }

                $buttonAction = (Test-TrueValue (Get-NestedValue $attempt @('layer4_action', 'button_click_received'))) -and
                    (Test-TrueValue (Get-NestedValue $attempt @('layer4_action', 'physical_target_confirmed'))) -and
                    -not [string]::IsNullOrWhiteSpace([string](Get-NestedValue $attempt @('layer4_action', 'button', 'automation_id')))
                $attemptId = [string](Get-PropertyValue $attempt 'attempt_id')
                $confirmedTransitions = @($transitions | Where-Object {
                        $transitionStartedAt = [DateTimeOffset]::MinValue
                        $transitionCompletedAt = [DateTimeOffset]::MinValue
                        (Try-GetTimestamp (Get-PropertyValue $_ 'started_at') ([ref]$transitionStartedAt)) -and
                        (Try-GetTimestamp (Get-PropertyValue $_ 'completed_at') ([ref]$transitionCompletedAt)) -and
                        $transitionStartedAt -gt $default.CapturedAt -and
                        $transitionCompletedAt -le $interaction.CapturedAt -and
                        $transitionCompletedAt -ge $transitionStartedAt -and
                        (Test-ExactString (Get-PropertyValue $_ 'correlated_pointer_attempt_id') $attemptId) -and
                        (Test-ExactString (Get-PropertyValue $_ 'input_kind') 'MouseDrag') -and
                        (Test-TrueValue (Get-PropertyValue $_ 'layer1_win32_confirmed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'layer2_wpf_confirmed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'layer3_target_confirmed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'layer4_action_confirmed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'state_changed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'settings_state_changed')) -and
                        (Test-TrueValue (Get-PropertyValue $_ 'settings_write_back_confirmed')) -and
                        (Test-ExactString (Get-PropertyValue $_ 'result') 'Confirmed')
                    })
                if ($buttonAction -or $confirmedTransitions.Count -gt 0) {
                    $physicalMouseActions += [pscustomobject]@{ Document = $document; Attempt = $attempt }
                }
            }

            foreach ($attempt in @(Get-PropertyValue $document.Json 'key_attempts')) {
                $key = [string](Get-PropertyValue $attempt 'key')
                if ($key -notin @('Left', 'Right')) { continue }
                $controlAutomationId = [string](Get-NestedValue $attempt @('layer3_target', 'control_automation_id'))
                $controls = @($contract.splitter_keyboard.controls | Where-Object { Test-ExactString $_.automation_id $controlAutomationId })
                if ($controls.Count -ne 1) { continue }
                $control = $controls[0]
                if (-not (Test-KeyInputLayers $attempt $controlAutomationId $key @($contract.splitter_keyboard.required_layer2_events))) { continue }
                $delta = if ($key -ceq 'Left') { [string]$control.left_delta } else { [string]$control.right_delta }
                foreach ($transition in @(Get-TransitionForAttempt $transitions $attempt $controlAutomationId $key)) {
                    if ((Test-RegularKeyTransition $transition $attempt $delta $control) -and
                        (Test-KeyActionInsideCaptureWindow $attempt $transition $default.CapturedAt $interaction.CapturedAt @($contract.splitter_keyboard.required_layer2_events))) {
                        $physicalKeyboardActions += [pscustomobject]@{ Document = $document; Attempt = $attempt; Transition = $transition }
                    }
                }
            }
        }
        Require-True (($physicalMouseActions.Count + $physicalKeyboardActions.Count) -ge 1) "DPI tuple $($tuple.width)x$($tuple.height) interaction capture is not linked to a same-PID/HWND physical mouse action or strict Left/Right Layer 1-4 state transition inside its capture window."
        $previousTupleInteractionAt = $interaction.CapturedAt
    }
}

$baselineMatches = @($windowRecords | Where-Object {
        $_.CaptureName.StartsWith([string]$contract.restore_baseline.capture_name_prefix, [StringComparison]::Ordinal)
    })
Require-Equal $baselineMatches.Count 1 'The run must have exactly one final 3840x2160@60/150%/DPI144 baseline-restore capture.'
foreach ($match in $baselineMatches) {
    $summary = Get-WindowRecordSummary $match $contract.restore_baseline
    Require-True ([string]::Equals($summary.ExecutablePath, $matrixExecutablePath, [StringComparison]::OrdinalIgnoreCase)) 'Baseline-restore capture uses a different executable path.'
    Require-True ($summary.CapturedAt -gt $previousTupleInteractionAt) 'Baseline-restore capture was not recorded after the complete ordered DPI matrix.'
    if ($null -ne $restartDpiManualSession) {
        Require-Equal $summary.ProcessId (Get-PropertyValue $restartDpiManualSession 'process_id') 'Baseline-restore capture does not belong to the restart/DPI session PID.'
        Require-True (Test-ExactString $summary.Hwnd (Get-PropertyValue $restartDpiManualSession 'window_hwnd')) 'Baseline-restore capture does not belong to the restart/DPI session HWND.'
    }
    if (-not [string]::IsNullOrWhiteSpace($summary.Hash)) {
        Require-True ($requiredHashes.Add($summary.Hash)) 'Baseline-restore screenshot duplicates a required screenshot hash.'
    }
}

# The imported fixture diagnostic is raw, synthetic-only evidence; customer media never satisfies it.
$fixtureFiles = @(if (Test-Path -LiteralPath $resolvedRunRoot -PathType Container) {
        Get-ChildItem -LiteralPath $resolvedRunRoot -Recurse -File -Filter $contract.synthetic_fixture.import_diagnostics_file
    })
Require-Equal $fixtureFiles.Count 1 'The run must contain exactly one synthetic import diagnostic.'
if ($fixtureFiles.Count -eq 1) {
    $fixture = Read-JsonFile $fixtureFiles[0].FullName
    if ($null -ne $fixture) {
        Require-True (Test-ExactString (Get-PropertyValue $fixture 'source_kind') $contract.synthetic_fixture.source_kind) 'Fixture source_kind is not synthetic-directory-recursive.'
        Require-Equal (Get-PropertyValue $fixture 'selected_file_count') $contract.synthetic_fixture.selected_file_count 'Synthetic selected-file count mismatch.'
        Require-Equal (Get-PropertyValue $fixture 'imported_count') $contract.synthetic_fixture.imported_count 'Synthetic imported count mismatch.'
        Require-Equal (Get-PropertyValue $fixture 'failed_count') $contract.synthetic_fixture.failed_count 'Synthetic failed count mismatch.'
        Require-Equal (Get-PropertyValue $fixture 'repository_asset_count_before') 0 'Synthetic repository pre-count mismatch.'
        Require-Equal (Get-PropertyValue $fixture 'repository_asset_count_after') $contract.synthetic_fixture.imported_count 'Synthetic repository post-count mismatch.'
        Require-True (Test-TrueValue (Get-PropertyValue $fixture 'picker_accepted')) 'Synthetic picker acceptance was not recorded.'
        Require-True (Test-TrueValue (Get-PropertyValue $fixture 'import_command_entered')) 'Synthetic import command was not entered.'
        Require-True (Test-TrueValue (Get-PropertyValue $fixture 'import_service_entered')) 'Synthetic import service was not entered.'
    }
}

if ($failures.Count -gt 0) {
    $details = ($failures | ForEach-Object { " - $_" }) -join [Environment]::NewLine
    throw "Gate A evidence validation failed ($($failures.Count)):$([Environment]::NewLine)$details"
}

[pscustomobject]@{
    schema = 'pixel-tart-asset-library-p1-gate-a-validation/v1'
    passed = $true
    capture_status = [string]$contract.capture_status
    synthetic_fixture_only = $true
    state_capture_count = $stateSummaries.Count
    splitter_direction_attempt_count = 4
    dpi_tuple_count = @($contract.dpi_matrix).Count
    required_screenshot_hash_count = $requiredHashes.Count
    baseline_restored = $true
    portable_machine_paths_emitted = $false
}
