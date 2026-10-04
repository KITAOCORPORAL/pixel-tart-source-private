$ErrorActionPreference = 'Stop'
$captures = @()
foreach ($folder in @('after','final-after','r5-after','r6-after','r7-after','r8-after','r9-after')) {
    $directory = Join-Path $PSScriptRoot $folder
    if (!(Test-Path -LiteralPath $directory)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.json') {
        $entry = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if (!$entry.SourceHead) { continue }
        $images = @()
        for ($index=0; $index -lt $entry.Screenshots.Count; $index++) {
            $suffix = if ($index -eq 0) { '' } else { '_' + $index }
            $name = $file.BaseName + $suffix + '.png'
            $path = Join-Path $directory $name
            if (Test-Path -LiteralPath $path) { $images += [ordered]@{Path=$folder+'/'+$name;Sha256=(Get-FileHash -LiteralPath $path).Hash} }
        }
        $captures += [ordered]@{Id=$folder+'/'+$file.BaseName;SourceHead=$entry.SourceHead;ReleaseExe=$entry.ReleaseExe;CapturedAt=$entry.CapturedAt;DpiScale=$null;DpiNote='Actual window DPI not independently measured; legacy 1.5 field was assumed from requested environment';Window=$entry.Window;Screenshots=$images;Dimensions=$entry.Screenshots}
    }
}
[ordered]@{ProductionSourceHead='dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7';CaptureMethod='sky.get_window_state production windows; isolated test library';CaptureCount=$captures.Count;Captures=$captures;AllRequiredRuntimeComplete=$false;VisualApproved=$false;UserVerified=$false} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-evidence-manifest.json') -Encoding utf8
