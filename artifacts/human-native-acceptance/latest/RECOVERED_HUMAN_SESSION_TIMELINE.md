# Recovered human session timeline — NO ACCEPTANCE VERDICT

Status: ISSUES_REPORTED_BY_USER. Machine Native Approval: NOT GRANTED.
VisualApproved=false; UserVerified=false; Native Closure=NOT CLOSED.
The user reports completing the session and finding many issues. No individual issue is invented here.

Session: 3005e060-fd05-454a-a973-d32af16d62e3
SourceHead: 1a5bf7d9b8f0d92aae8df8d87a4aed4953811a63
PID / HWND: 20052 / 6950308
Start: 09/30/2026 10:56:24; recorder stop: 09/30/2026 11:10:59
Final polling snapshot: 09/30/2026 11:10:58
Table timestamps were formatted by PowerShell in the host local timezone (UTC+08:00), to second precision. The immutable source JSON retains the exact fractional timestamps and offsets. Exact UTC timestamps are explicitly written below where required.

## What actually ran

**B. PERIODIC_POLLING**, backed by **C. REQUEST/RESPONSE SNAPSHOT** in the product, with limited event buffers.
Recorder completed 1412 requests, persisted 175 changed raw snapshots, 125 node state records, 16 samples, and 34 route/window records. One request-file sharing exception was recorded; the loop continued.
Across raw snapshots, 749 unique PropertyChanged events (sequences 1..749) are recoverable. This is not a mouse/key/command event log.
A hidden PowerShell process (recorder PID 544) repeatedly called the existing reader, then delayed 500 ms after work. The actual cadence includes observation/serialization cost; it is not a guaranteed 2 Hz trace.
The product timer checks for a new request nonce every 50 ms and writes one response per new nonce. Sampling history is capped at 128 entries; selected property events at 256. 3D camera state has no event history in the observer.
Changed states are appended to observer-state.jsonl; aggregate JSON and session heartbeat files are periodically replaced. These files are now frozen/copied; they were not a lossless log of every successful poll.

## Recovery preservation

pre-freeze/ preserves top-level files while the recorder was still active. frozen/ contains the stopped recorder evidence and runtime text/log files; FROZEN_FILE_INVENTORY.json records copy SHA256 hashes.
STOP_OBSERVER.txt requested graceful recorder stop. Its own final flush set BOUNDED_SESSION_ENDED; this label does not mean the four-hour limit was reached. Pixel Tart was not closed.
POST_SESSION_FINAL_OBSERVATION.json at session root is a new read-only observation, not retroactive evidence. Timestamp: 2026-09-30T11:12:37.0386489+08:00; nonce: 68d70d69-1eef-4992-9b41-f18549fbe68d.
The first recovery helper invocation had an incorrect repository-parent path and stopped before any observer request. Its local path was corrected; frozen copies were not overwritten. FREEZE_MANIFEST.json records product path/start-time/hash/HWND checks.

## Eyedropper — actual product sample records

16 unique sample sequences 1..16: the product Valid field is true for 15 and false for 1. These are recorded fields, NOT acceptance verdicts. Normal mode: 1; add mode: 15; no subtract/negative-mode sample was found.
Fourteen samples carry IsFit=true. Two carry IsFit=false, but their pan is effectively zero. Substantial zoom/pan changes are present separately, without samples at those offsets. No claim is made for the full zoom+pan sampling matrix.

| Sample | Product timestamp | Observation | Mode | Valid field | IsFit | Image coordinate | RGB |
|---|---|---:|---|---|---|---|---|
| 1 | 09/30/2026 11:00:19 | 380 | 普通取样 | True | True | {"X":579,"Y":586} | {"R":210,"G":0,"B":40} |
| 2 | 09/30/2026 11:00:35 | 406 | 增加取样 | True | True | {"X":345,"Y":962} | {"R":17,"G":26,"B":112} |
| 3 | 09/30/2026 11:00:40 | 413 | 增加取样 | True | True | {"X":560,"Y":1075} | {"R":32,"G":50,"B":65} |
| 4 | 09/30/2026 11:01:23 | 486 | 增加取样 | False | True | null | null |
| 5 | 09/30/2026 11:01:27 | 491 | 增加取样 | True | True | {"X":547,"Y":714} | {"R":195,"G":33,"B":54} |
| 6 | 09/30/2026 11:01:28 | 493 | 增加取样 | True | True | {"X":519,"Y":707} | {"R":191,"G":0,"B":46} |
| 7 | 09/30/2026 11:01:29 | 494 | 增加取样 | True | True | {"X":539,"Y":711} | {"R":182,"G":30,"B":52} |
| 8 | 09/30/2026 11:01:30 | 497 | 增加取样 | True | True | {"X":537,"Y":695} | {"R":203,"G":37,"B":59} |
| 9 | 09/30/2026 11:01:32 | 500 | 增加取样 | True | True | {"X":542,"Y":725} | {"R":77,"G":30,"B":40} |
| 10 | 09/30/2026 11:01:34 | 503 | 增加取样 | True | True | {"X":537,"Y":713} | {"R":187,"G":31,"B":53} |
| 11 | 09/30/2026 11:01:36 | 506 | 增加取样 | True | True | {"X":528,"Y":705} | {"R":149,"G":0,"B":36} |
| 12 | 09/30/2026 11:01:36 | 507 | 增加取样 | True | True | {"X":528,"Y":705} | {"R":149,"G":0,"B":36} |
| 13 | 09/30/2026 11:01:47 | 523 | 增加取样 | True | True | {"X":535,"Y":714} | {"R":183,"G":30,"B":49} |
| 14 | 09/30/2026 11:01:48 | 525 | 增加取样 | True | True | {"X":632,"Y":550} | {"R":138,"G":57,"B":103} |
| 15 | 09/30/2026 11:06:10 | 941 | 增加取样 | True | False | {"X":693,"Y":81} | {"R":255,"G":255,"B":255} |
| 16 | 09/30/2026 11:06:14 | 947 | 增加取样 | True | False | {"X":632,"Y":200} | {"R":37,"G":46,"B":58} |

Screen/viewport coordinates, ImageRect, exact zoom/pan and InputTimestamp remain in EYEDROPPER_HUMAN_NATIVE.json; no re-computation or invented values were inserted.

### Sampling / image viewport state changes

| Timestamp | Observation | Sampling | Mode | Zoom | PanX | PanY | IsFit |
|---|---:|---|---|---:|---:|---:|---|
| 09/30/2026 10:56:34 | 1 | False | 增加取样 | 0.669166666666667 | 0 | 0 | True |
| 09/30/2026 10:58:56 | 241 | False | 增加取样 | 0.374883286647993 | 0 | 0 | True |
| 09/30/2026 11:00:16 | 374 | True | 普通取样 | 0.374883286647993 | 0 | 0 | True |
| 09/30/2026 11:00:19 | 380 | False | 普通取样 | 0.374883286647993 | 0 | 0 | True |
| 09/30/2026 11:00:33 | 403 | True | 增加取样 | 0.374883286647993 | 0 | 0 | True |
| 09/30/2026 11:01:44 | 518 | True | 增加取样 | 0.720354808590103 | 0 | 0 | True |
| 09/30/2026 11:01:49 | 527 | True | 增加取样 | 0.720354808590103 | 0 | 0 | True |
| 09/30/2026 11:01:51 | 529 | True | 增加取样 | 1.13349223738562 | -28.53258816 | 124.45370112 | False |
| 09/30/2026 11:01:51 | 530 | True | 增加取样 | 2.50579021462751 | -104.955940222315 | 378.301771300041 | False |
| 09/30/2026 11:01:52 | 531 | True | 增加取样 | 2.80648504038282 | -120.720653048993 | 424.697983856046 | False |
| 09/30/2026 11:01:53 | 533 | True | 增加取样 | 1.2695113058719 | -40.1407844534858 | 187.548043213584 | False |
| 09/30/2026 11:01:54 | 534 | True | 增加取样 | 0.574262443072467 | 0 | 0 | False |
| 09/30/2026 11:01:55 | 535 | True | 增加取样 | 1.01204664052288 | -10.6968480000001 | 3.37440000000018 | False |
| 09/30/2026 11:01:55 | 536 | True | 增加取样 | 1.59247498208571 | -31.9821671789774 | 10.0890117283843 | False |
| 09/30/2026 11:01:56 | 537 | True | 增加取样 | 1.99760061752831 | -46.8388305093092 | 14.7756563120853 | False |
| 09/30/2026 11:01:57 | 538 | True | 增加取样 | 0.903613071895425 | -6.7204000000001 | 2.12000000000015 | False |
| 09/30/2026 11:01:57 | 539 | True | 增加取样 | 0.457798503724862 | 0 | 0 | False |
| 09/30/2026 11:01:58 | 540 | True | 增加取样 | 0.643173936241163 | 0 | 0 | False |
| 09/30/2026 11:03:26 | 681 | True | 增加取样 | 1.2695113058719 | 0 | 12.9598086144002 | False |
| 09/30/2026 11:03:27 | 682 | True | 增加取样 | 2.23731269163171 | -50.8520499199999 | 35.7994195418519 | False |
| 09/30/2026 11:03:29 | 686 | True | 增加取样 | 1.13349223738562 | 0 | 9.74982912000019 | False |
| 09/30/2026 11:03:30 | 687 | True | 增加取样 | 0.643173936241163 | 0 | 0 | False |
| 09/30/2026 11:03:31 | 688 | True | 增加取样 | 0.720354808590103 | 0 | 1.13686837721616E-13 | False |

Sampling true->false at observation 380 accompanies ordinary single-sample mode; it cannot be attributed to Escape. Final observation still has IsSampling=true. No keyboard event provenance exists, so Escape success/failure is not inferred.

## Node/model/visual/render order and Undo/Redo

125 node state records include 45 synchronized flags and 80 pending/not-visible flags; none constitutes acceptance. There is one observed Drag.Active=true snapshot. Six distinct model ID sequences include empty/replaced node sets, not six successful reorders.
IDs are abbreviated to eight characters only in this table; full IDs remain in source JSON. Count changes/UndoCount increments alone do not prove a drag or an Undo command. RedoCount is 0 in all recorded states; no unambiguous Undo/Redo pair is recoverable.

| Timestamp | Obs | Model IDs | Visual IDs | Render IDs | Undo | Redo | Settled | Sync field |
|---|---:|---|---|---|---:|---:|---|---|
| 09/30/2026 10:56:34 | 1 | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 7 | 0 | True | True |
| 09/30/2026 10:58:46 | 226 | 72e0e376, 2023a0c3, c1475ca3, 510fee7f |  | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 21 | 0 | False | False |
| 09/30/2026 10:58:47 | 227 | 72e0e376, 2023a0c3, c1475ca3, 510fee7f |  | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 38 | 0 | False | False |
| 09/30/2026 10:58:48 | 228 | 72e0e376, 2023a0c3, c1475ca3, 510fee7f |  | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 48 | 0 | False | False |
| 09/30/2026 10:58:56 | 241 |  |  | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 48 | 0 | False | False |
| 09/30/2026 10:59:54 | 340 | fe75b757, 54caf4e1, 71f668a0 |  | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 48 | 0 | True | False |
| 09/30/2026 10:59:59 | 348 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 72e0e376, 2023a0c3, c1475ca3, 510fee7f | 49 | 0 | False | False |
| 09/30/2026 11:00:00 | 350 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 50 | 0 | True | True |
| 09/30/2026 11:00:10 | 365 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 51 | 0 | True | True |
| 09/30/2026 11:00:12 | 368 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 52 | 0 | True | True |
| 09/30/2026 11:00:19 | 380 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 53 | 0 | False | False |
| 09/30/2026 11:00:35 | 406 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 54 | 0 | False | False |
| 09/30/2026 11:00:40 | 413 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 55 | 0 | False | False |
| 09/30/2026 11:01:15 | 472 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 56 | 0 | False | False |
| 09/30/2026 11:01:16 | 474 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 57 | 0 | False | False |
| 09/30/2026 11:01:39 | 511 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 58 | 0 | False | False |
| 09/30/2026 11:02:07 | 554 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 59 | 0 | False | False |
| 09/30/2026 11:02:15 | 568 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 60 | 0 | False | False |
| 09/30/2026 11:02:16 | 569 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 61 | 0 | False | False |
| 09/30/2026 11:02:17 | 571 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 62 | 0 | False | False |
| 09/30/2026 11:02:47 | 619 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | fe75b757, 54caf4e1, 71f668a0 | 63 | 0 | False | False |
| 09/30/2026 11:02:56 | 634 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0 | 64 | 0 | False | False |
| 09/30/2026 11:02:58 | 636 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0 | 65 | 0 | False | False |
| 09/30/2026 11:02:58 | 637 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0 | 66 | 0 | False | False |
| 09/30/2026 11:03:20 | 671 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 |  | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | 67 | 0 | False | False |
| 09/30/2026 11:03:22 | 674 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 |  | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | 68 | 0 | False | False |
| 09/30/2026 11:03:57 | 730 | 54caf4e1, fe75b757, 71f668a0, df8a72f2 | 54caf4e1, fe75b757, 71f668a0, df8a72f2 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | 69 | 0 | False | False |
| 09/30/2026 11:03:59 | 733 | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | fe75b757, 54caf4e1, 71f668a0, df8a72f2 | 70 | 0 | False | False |
| 09/30/2026 11:04:52 | 818 | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | 71 | 0 | False | False |
| 09/30/2026 11:05:05 | 838 |  |  | 54caf4e1, 71f668a0, fe75b757, df8a72f2 | 71 | 0 | False | False |

### Recoverable drag sequence

Observation 730 (11:03:57.512790+08:00) changes order; 732 (11:03:58.976621+08:00) records Drag.Active=true; 733 (11:03:59.727657+08:00) changes order again. At 746 (11:04:07.756493+08:00) Model/Visual/Rendered IDs are again equal and Settled=true. These facts do not establish the full requested drag matrix or explain the user-reported issues.
Product drag event: 2026-09-30T03:04:07.4691694+00:00 (11:04:07.4691694 +08:00); SourceNode=颜色范围; DestinationNode=色彩过渡; DropAfter=False.
Before names: 色彩过渡 > 颜色范围 > 胶片 > 色彩过渡; After names: 色彩过渡 > 胶片 > 颜色范围 > 色彩过渡.
The product drag hook waits for settling asynchronously and reports node names; it does not supply a per-input action ID or complete pointer sequence. Do not equate this one record to six operations.

## 3D Orbit / Pan / Zoom / Reset / Fit / Resize

3D_HUMAN_NATIVE.json contains ONE initial state (observation 1): ModelLoaded=false, SampleCount=0, Camera=null, RenderCount=0, ProjectedBounds=null, viewport not visible.
Every retained raw snapshot likewise has no loaded 3D model/camera. There is no recoverable Orbit, Pan, Zoom, Reset or Fit transition. No bounds-aware resize claim is possible. This is HUMAN_SESSION_OBSERVATION_GAP; it does not prove the user did not attempt those actions or prove a product root cause.

## Route navigation and window changes

| Timestamp | Obs | Route | Settings modal | Physical window | DIP window |
|---|---:|---|---|---|---|
| 09/30/2026 10:56:34 | 1 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:42 | 15 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:43 | 16 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:48 | 25 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:49 | 26 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:51 | 30 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 10:56:52 | 31 | ReferenceColor | False | 2400x1500 | 1600x1000 |
| 09/30/2026 11:01:44 | 518 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:27 | 586 | Workflow | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:35 | 599 | Tether | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:39 | 606 | OnlineSelection | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:40 | 608 | History | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:41 | 609 | Finance | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:42 | 610 | Workbench | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:02:47 | 619 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:14 | 757 | Tether | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:16 | 760 | OnlineSelection | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:17 | 762 | Finance | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:19 | 765 | History | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:21 | 767 | Workbench | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:26 | 776 | AssetLibrary | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:30 | 782 | WorkCalendar | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:33 | 787 | Planning | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:39 | 797 | Workbench | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:45 | 806 | Toolbox | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:04:52 | 818 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:05:33 | 881 | Toolbox | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:05:36 | 885 | Publishing | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:00 | 925 | Toolbox | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:08 | 938 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:11 | 942 | Toolbox | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:13 | 945 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:16 | 949 | Toolbox | False | 3860x2180 | 2573.33333333333x1453.33333333333 |
| 09/30/2026 11:06:16 | 950 | ReferenceColor | False | 3860x2180 | 2573.33333333333x1453.33333333333 |

12 route identifiers are present. SettingsModalOpen=true is not recorded. The exact 1180x720 / 1600x920 / 1920x1080 matrix is not recorded; observed physical sizes are 2400x1500 and 3860x2180 at 150% DPI. Route arrival is not a visual/layout approval.

## Evidence limitations and status

HUMAN_VISUAL_TEST_COMPLETED: user reported. OBSERVER_CONTINUOUS_EVIDENCE: PARTIAL_AVAILABLE (periodic state-change history, not full event capture). HUMAN_SESSION_OBSERVATION_GAP: YES.
Missing: 3D interaction history, complete drag case attribution, Undo/Redo action history, negative sampling, explicit Escape event, exact three-size visual matrix, Settings confirmation and screenshots. Rapid changes between polls can be missed. No final-state backfill is used.
The raw change log ends at 11:06:17.776258+08:00, while polling heartbeat continued to 11:10:58.913025+08:00. Because unchanged polls were not all persisted, this interval must not be re-labelled a complete event trace.
No product change, bug fix, user re-run request, acceptance verdict, commit or push was made by this recovery.
