$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'frozen'
$session = Get-Content (Join-Path $root 'HUMAN_NATIVE_SESSION.json') -Raw | ConvertFrom-Json
$manifest = Get-Content (Join-Path $root 'SESSION_MANIFEST.json') -Raw | ConvertFrom-Json
$eye = Get-Content (Join-Path $root 'EYEDROPPER_HUMAN_NATIVE.json') -Raw | ConvertFrom-Json
$node = Get-Content (Join-Path $root 'NODE_DRAG_HUMAN_NATIVE.json') -Raw | ConvertFrom-Json
$three = Get-Content (Join-Path $root '3D_HUMAN_NATIVE.json') -Raw | ConvertFrom-Json
$raw = @(Get-Content (Join-Path $root 'observer-state.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$routes = @(Get-Content (Join-Path $root 'route-window-observations.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$sampling = @(Get-Content (Join-Path $root 'sampling-state.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$events = @($raw.State.Events | Group-Object Sequence | ForEach-Object {$_.Group[0]} | Sort-Object Sequence)
$dragEvents = @(Get-Content (Join-Path $root 'runtime/ColorStudioFixture/native-node-drag-events.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$lines = [Collections.Generic.List[string]]::new()
function Add([string]$line = '') { $lines.Add($line) }
function J($value) { ConvertTo-Json -InputObject $value -Depth 8 -Compress }
Add '# Recovered human session timeline — NO ACCEPTANCE VERDICT'
Add
Add 'Status: ISSUES_REPORTED_BY_USER. Machine Native Approval: NOT GRANTED.'
Add 'VisualApproved=false; UserVerified=false; Native Closure=NOT CLOSED.'
Add 'The user reports completing the session and finding many issues. No individual issue is invented here.'
Add
Add "Session: $($manifest.SessionId)"
Add "SourceHead: $($manifest.SourceHead)"
Add "PID / HWND: $($manifest.PID) / $($manifest.HWND)"
Add "Start: $($manifest.StartTime); recorder stop: $($manifest.RecorderEndTime)"
Add "Final polling snapshot: $($manifest.LastObservationTime)"
Add 'All timeline timestamps retain their source offset. Product event timestamps are generally UTC+08:00; UTC entries are explicitly marked.'
Add
Add '## What actually ran'
Add
Add '**B. PERIODIC_POLLING**, backed by **C. REQUEST/RESPONSE SNAPSHOT** in the product, with limited event buffers.'
Add "Recorder completed $($session.ObservationCount) requests, persisted $($raw.Count) changed raw snapshots, $(@($node.Transitions).Count) node state records, $(@($eye.Samples).Count) samples, and $($routes.Count) route/window records. One request-file sharing exception was recorded; the loop continued."
Add "Across raw snapshots, $($events.Count) unique PropertyChanged events (sequences 1..749) are recoverable. This is not a mouse/key/command event log."
Add 'A hidden PowerShell process (recorder PID 544) repeatedly called the existing reader, then delayed 500 ms after work. The actual cadence includes observation/serialization cost; it is not a guaranteed 2 Hz trace.'
Add 'The product timer checks for a new request nonce every 50 ms and writes one response per new nonce. Sampling history is capped at 128 entries; selected property events at 256. 3D camera state has no event history in the observer.'
Add 'Changed states are appended to observer-state.jsonl; aggregate JSON and session heartbeat files are periodically replaced. These files are now frozen/copied; they were not a lossless log of every successful poll.'
Add
Add '## Recovery preservation'
Add
Add 'pre-freeze/ preserves top-level files while the recorder was still active. frozen/ contains the stopped recorder evidence and runtime text/log files; FROZEN_FILE_INVENTORY.json records copy SHA256 hashes.'
Add 'STOP_OBSERVER.txt requested graceful recorder stop. Its own final flush set BOUNDED_SESSION_ENDED; this label does not mean the four-hour limit was reached. Pixel Tart was not closed.'
Add 'POST_SESSION_FINAL_OBSERVATION.json at session root is a new read-only observation, not retroactive evidence. Timestamp: 2026-09-30T11:12:37.0386489+08:00; nonce: 68d70d69-1eef-4992-9b41-f18549fbe68d.'
Add 'The first recovery helper invocation had an incorrect repository-parent path and stopped before any observer request. Its local path was corrected; frozen copies were not overwritten. FREEZE_MANIFEST.json records product path/start-time/hash/HWND checks.'
Add
Add '## Eyedropper — actual product sample records'
Add
Add '16 unique sample sequences 1..16: the product Valid field is true for 15 and false for 1. These are recorded fields, NOT acceptance verdicts. Normal mode: 1; add mode: 15; no subtract/negative-mode sample was found.'
Add 'Fourteen samples carry IsFit=true. Two carry IsFit=false, but their pan is effectively zero. Substantial zoom/pan changes are present separately, without samples at those offsets. No claim is made for the full zoom+pan sampling matrix.'
Add
Add '| Sample | Product timestamp | Observation | Mode | Valid field | IsFit | Image coordinate | RGB |'
Add '|---|---|---:|---|---|---|---|---|'
foreach ($item in $eye.Samples) {
    $p = $item.ProductSample
    Add "| $($p.Sequence) | $($p.Timestamp) | $($item.Observation) | $($p.Mode) | $($p.Valid) | $($p.IsFit) | $(J $p.Image) | $(J $p.Value) |"
}
Add
Add 'Screen/viewport coordinates, ImageRect, exact zoom/pan and InputTimestamp remain in EYEDROPPER_HUMAN_NATIVE.json; no re-computation or invented values were inserted.'
Add
Add '### Sampling / image viewport state changes'
Add
Add '| Timestamp | Observation | Sampling | Mode | Zoom | PanX | PanY | IsFit |'
Add '|---|---:|---|---|---:|---:|---:|---|'
foreach ($item in $sampling) { $s=$item.State; Add "| $($item.Timestamp) | $($item.Observation) | $($s.Sampling) | $($s.Mode) | $($s.Zoom) | $($s.PanX) | $($s.PanY) | $($s.IsFit) |" }
Add
Add 'Sampling true->false at observation 380 accompanies ordinary single-sample mode; it cannot be attributed to Escape. Final observation still has IsSampling=true. No keyboard event provenance exists, so Escape success/failure is not inferred.'
Add
Add '## Node/model/visual/render order and Undo/Redo'
Add
Add '125 node state records include 45 synchronized flags and 80 pending/not-visible flags; none constitutes acceptance. There is one observed Drag.Active=true snapshot. Six distinct model ID sequences include empty/replaced node sets, not six successful reorders.'
Add 'IDs are abbreviated to eight characters only in this table; full IDs remain in source JSON. Count changes/UndoCount increments alone do not prove a drag or an Undo command. RedoCount is 0 in all recorded states; no unambiguous Undo/Redo pair is recoverable.'
Add
Add '| Timestamp | Obs | Model IDs | Visual IDs | Render IDs | Undo | Redo | Settled | Sync field |'
Add '|---|---:|---|---|---|---:|---:|---|---|'
function Ids($ids) { (@($ids) | Where-Object { $_ } | ForEach-Object {$_.Substring(0,[Math]::Min(8,$_.Length))}) -join ', ' }
$prev = $null
foreach ($item in $node.Transitions) {
    $a = $item.After
    $key = (@($a.ModelIds)-join ',') + "|$($a.UndoCount)|$($a.RedoCount)"
    if ($key -cne $prev) { Add "| $($item.Timestamp) | $($item.Observation) | $(Ids $a.ModelIds) | $(Ids $a.VisualIds) | $(Ids $a.RenderedNodeIds) | $($a.UndoCount) | $($a.RedoCount) | $($a.IsSettled) | $($a.Synchronized) |" }
    $prev = $key
}
Add
Add '### Recoverable drag sequence'
Add
Add 'Observation 730 (11:03:57.512790+08:00) changes order; 732 (11:03:58.976621+08:00) records Drag.Active=true; 733 (11:03:59.727657+08:00) changes order again. At 746 (11:04:07.756493+08:00) Model/Visual/Rendered IDs are again equal and Settled=true. These facts do not establish the full requested drag matrix or explain the user-reported issues.'
foreach ($event in $dragEvents) {
    Add "Product drag event: UTC $($event.RecordedAtUtc); SourceNode=$($event.SourceNode); DestinationNode=$($event.DestinationNode); DropAfter=$($event.DropAfter)."
    Add "Before names: $(@($event.BeforeOrder)-join ' > '); After names: $(@($event.AfterOrder)-join ' > ')."
}
Add 'The product drag hook waits for settling asynchronously and reports node names; it does not supply a per-input action ID or complete pointer sequence. Do not equate this one record to six operations.'
Add
Add '## 3D Orbit / Pan / Zoom / Reset / Fit / Resize'
Add
Add '3D_HUMAN_NATIVE.json contains ONE initial state (observation 1): ModelLoaded=false, SampleCount=0, Camera=null, RenderCount=0, ProjectedBounds=null, viewport not visible.'
Add 'Every retained raw snapshot likewise has no loaded 3D model/camera. There is no recoverable Orbit, Pan, Zoom, Reset or Fit transition. No bounds-aware resize claim is possible. This is HUMAN_SESSION_OBSERVATION_GAP; it does not prove the user did not attempt those actions or prove a product root cause.'
Add
Add '## Route navigation and window changes'
Add
Add '| Timestamp | Obs | Route | Settings modal | Physical window | DIP window |'
Add '|---|---:|---|---|---|---|'
foreach ($item in $routes) { $s=$item.State; $w=$s.Window; Add "| $($item.Timestamp) | $($item.Observation) | $($s.Route) | $($s.Settings) | $($w.Width)x$($w.Height) | $($w.DipWidth)x$($w.DipHeight) |" }
Add
Add '12 route identifiers are present. SettingsModalOpen=true is not recorded. The exact 1180x720 / 1600x920 / 1920x1080 matrix is not recorded; observed physical sizes are 2400x1500 and 3860x2180 at 150% DPI. Route arrival is not a visual/layout approval.'
Add
Add '## Evidence limitations and status'
Add
Add 'HUMAN_VISUAL_TEST_COMPLETED: user reported. OBSERVER_CONTINUOUS_EVIDENCE: PARTIAL_AVAILABLE (periodic state-change history, not full event capture). HUMAN_SESSION_OBSERVATION_GAP: YES.'
Add 'Missing: 3D interaction history, complete drag case attribution, Undo/Redo action history, negative sampling, explicit Escape event, exact three-size visual matrix, Settings confirmation and screenshots. Rapid changes between polls can be missed. No final-state backfill is used.'
Add 'The raw change log ends at 11:06:17.776258+08:00, while polling heartbeat continued to 11:10:58.913025+08:00. Because unchanged polls were not all persisted, this interval must not be re-labelled a complete event trace.'
Add 'No product change, bug fix, user re-run request, acceptance verdict, commit or push was made by this recovery.'
$out = Join-Path (Split-Path -Parent $PSScriptRoot) 'RECOVERED_HUMAN_SESSION_TIMELINE.md'
$stream=[IO.File]::Open($out,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
$writer=[IO.StreamWriter]::new($stream,[Text.UTF8Encoding]::new($false))
try { $writer.Write(($lines -join "`n")+"`n") } finally { $writer.Dispose() }
[pscustomobject]@{Timeline=$out;Samples=@($eye.Samples).Count;NodeStateRecords=@($node.Transitions).Count;Snapshots=$raw.Count;Polls=$session.ObservationCount;RouteRecords=$routes.Count;Events=$events.Count} | ConvertTo-Json
