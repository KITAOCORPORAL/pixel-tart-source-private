# Mechanical migration of the two historical optional save prompts. MainWindow
# now flushes asynchronously and closes without a save dialog; do not hide a bug
# behind Optional. Keep the formal step IDs/counts for evidence continuity.
$ErrorActionPreference='Stop'
foreach($name in @('planning-full','upgrade-full')) {
    $path=Join-Path $PSScriptRoot "$name.plan.json"
    $p=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -AsHashtable
    foreach($s in $p.Steps|Where-Object {$_.Id -in @('restart-close-confirm','final-close-confirm')}) {
        $s.Action='waitExit'; $s.TimeoutMs=30000
        $s.Remove('Optional'); $s.Remove('SelectorRef')
    }
    [IO.File]::WriteAllText($path,($p|ConvertTo-Json -Depth 20)+"`n",[Text.UTF8Encoding]::new($false))
}
