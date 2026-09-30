# Human Review — 2026-09-30

Status: ISSUES_REPORTED_BY_USER

Machine Native Approval: NOT GRANTED

Current Product Status: USER_REPORTED_ISSUES_PRESENT

User Visual Approval: false

VisualApproved: false

UserVerified: false

Native Closure: NOT CLOSED

## User report

The user reports completing the entire human Native session and finding “很多问题”.
No specific route/problem, severity or root cause has yet been supplied in this
request. The issue table is intentionally empty. Automatic state flags do not
override the user's report and no item is assigned an acceptance verdict.

| ID | Area | User Description | Severity | Reproduction | Expected | Actual | Screenshot | Observer Evidence | Root Cause | Status |
|---|---|---|---|---|---|---|---|---|---|---|

## Session recovered, not rerun

- Session: `3005e060-fd05-454a-a973-d32af16d62e3`.
- Source HEAD: `1a5bf7d9b8f0d92aae8df8d87a4aed4953811a63`.
- Start: `2026-09-30T02:56:24.8875014+00:00` (10:56:24.887 +08:00).
- Product PID: `20052`; HWND: `6950308`; process identity still matched during recovery.
- EXE SHA256: `E7A976412CD5254F5CC5227F954CEEB0C2203E69D246CF4574FBEFD3E21B89F5`.
- Initial recovery ObserverStatus was RECORDING; recorder PID 544 was active.
- Recorder alone was gracefully stopped using its existing stop-file protocol.
  Its final flush wrote BOUNDED_SESSION_ENDED at `2026-09-30T03:10:59.4783539Z`.
  The product was not closed or changed. This status label is the script's generic
  loop-ending label, not proof that its four-hour duration expired.
- Final read-only snapshot: `2026-09-30T11:12:37.0386489+08:00`, new nonce
  `68d70d69-1eef-4992-9b41-f18549fbe68d`. It is not historical evidence.

Local evidence root (ignored, no images or logs committed):
`artifacts/human-native-acceptance/latest/`.

Preservation: `post-session-recovery-20260930/pre-freeze/` and `frozen/`.
Frozen copy inventory/digests: `post-session-recovery-20260930/FROZEN_FILE_INVENTORY.json`.
Final identity: `post-session-recovery-20260930/FREEZE_MANIFEST.json`.
Timeline: `RECOVERED_HUMAN_SESSION_TIMELINE.md`.
Final observation: `POST_SESSION_FINAL_OBSERVATION.json`.
The original artifacts were not deleted; recovery adds separate files/copies.

## Actual observation mechanism

Primary mode: **B — PERIODIC_POLLING**. The product endpoint itself is
**C — REQUEST/RESPONSE SNAPSHOT**, with limited in-memory event buffers.
It was not D (recorder absent), and it was not a lossless A event recorder.

`ReferenceColorWorkspaceView.NativeEvidence.cs` appends actual sampling events to
a 128-entry buffer. `ColorStudioAcceptanceFixture.StartNativeObserver` listens to
three editor property-change types, retaining 256 events. A WPF DispatcherTimer
checks `native-observe-request.txt` every 50 ms. Only a new valid nonce requests a
snapshot; the product publishes `native-observe-response.json` atomically.
No request invokes commands, changes selection or changes camera.

`scripts/read-native-closure-observation.ps1` actively sends a fresh nonce and
waits for its matching response. It does not run continuously by itself.
The actual local `record-human-session.ps1` ran in hidden PowerShell PID 544 and
repeatedly called that reader, with 500 ms delay after each read/serialization.
It wrote changed snapshots to JSONL, updated aggregate JSON and a heartbeat every
ten observations. The actual interval includes read/processing overhead.

The user did not see an observer because it has no product UI, no tray indicator
and no visible console. `RECORDER_READY.json` and `SESSION_MANIFEST.json` are file
signals only. stdout/stderr logs were empty; there was no visible RECORDER ACTIVE
indicator. This is a recorder usability/evidence limitation, not user error.

## What is recoverable (no acceptance verdict)

- 1,412 successful polling observations; 175 changed raw snapshots saved.
- 16 sample records: 15 product `Valid=true`, 1 `Valid=false`; these booleans
  are not acceptance judgments. No negative/subtract sample recorded.
- 125 node state records; one product drag-completion JSONL event; 749 unique
  retained property events recoverable across snapshots. RedoCount always zero.
- 3D contains only its initial ModelLoaded=false / Camera=null state. No actual
  Orbit/Pan/Zoom/Reset/Fit camera history is recoverable.
- 34 route/window records covering 12 route identifiers; no Settings-open state
  and no exact three-resolution matrix. No visual screenshots captured.
- One transient request-file sharing error at `2026-09-30T02:59:02.1856716Z`;
  later observations prove the recorder continued. No error was hidden/deleted.

HUMAN_VISUAL_TEST_COMPLETED: user reported.

OBSERVER_CONTINUOUS_EVIDENCE: PARTIAL_AVAILABLE, not a full native event history.

HUMAN_SESSION_OBSERVATION_GAP: YES — 3D interactions; full drag matrix and
Undo/Redo attribution; negative sample; explicit Escape provenance; exact
resolution matrix; Settings confirmation; screenshots. Final state cannot fill
these gaps. Polling can miss brief intermediate states. Missing records do not
prove the user did not operate the feature.

## Next recorder capability — design only, no implementation in this turn

Reuse the existing read-only observer and recorder; do not create a second input
engine. Existing evidence is sufficient to begin analysis now. Before a future
acceptance session, extend recorder visibility/reliability separately:

1. A test-only recorder surface with user-operated Start / Stop and an explicit
   **RECORDER ACTIVE** status. No product commands, mouse/keyboard control or
   ViewModel writes. Show session ID, PID, start time, last nonce/response time,
   heartbeat count, output directory and errors; distinguish STARTING, ACTIVE,
   STALE, STOPPED and ERROR.
2. Mark ACTIVE only after identity/source verification and at least two fresh
   matching responses. Continuously show heartbeat age so stalled recording is
   visible before/during user operations.
3. Append every observation envelope/heartbeat and error durably to JSONL; use
   change payloads/deduplicated event IDs to avoid repeatedly writing a large
   event buffer. On stop, flush and freeze a timestamped manifest with checksums.
4. Validate evidence prerequisites without changing them: correct product view,
   non-null 3D camera/model when that gate is chosen, current visible bounds,
   expected node fields. A missing state remains an explicit observation gap.
5. Optional user-operated case markers in recorder only (not product): identify
   which case the user intends, without injecting or executing it. This helps
   correlate Undo/Redo, Escape and gestures but is not proof of those inputs.
6. Existing observer cannot supply missing command/camera event history. Polling
   improvements alone cannot guarantee lossless fast interaction capture. If
   later authorized, separately evaluate read-only event instrumentation; never
   silently infer events from aggregate/final state.

No recorder/product fix, build, commit, push or next development phase is part of
this recovery. Do not request the user to repeat the completed session.
