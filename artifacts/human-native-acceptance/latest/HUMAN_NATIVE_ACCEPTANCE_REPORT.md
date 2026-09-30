# Human native acceptance — session preparation

SourceHead: 1a5bf7d9b8f0d92aae8df8d87a4aed4953811a63.
Build: Release x64, 0 warnings, 0 errors.

| Evidence class | Current state |
|---|---|
| AUTOMATED | Build PASS; fixture setup only, not native acceptance |
| HUMAN_INPUT | Awaiting human operations |
| OBSERVER_VERIFIED | Pending per-case review; see continuous evidence |
| USER_VISUAL_REVIEW | PENDING |
| AutomatedNative | ENVIRONMENT_POLICY_BLOCKED, not attempted |

Eyedropper / Node Drag / 3D: PENDING. No gate is marked PASS before real operations.
Whole-app MachineGeometry: NOT_RUN; UserVisual: PENDING.
BaselineApproved=false; VisualApproved=false; UserVerified=false.

Read-only evidence: SESSION_MANIFEST.json, BASELINE_OBSERVATION.json,
EYEDROPPER_HUMAN_NATIVE.json, NODE_DRAG_HUMAN_NATIVE.json, 3D_HUMAN_NATIVE.json,
HUMAN_NATIVE_SESSION.json and append-only observer-state.jsonl.
Sampling transitions and route/window sizes have separate JSONL records.
Product runtime/observer errors remain in the isolated runtime and observer-errors.jsonl.

No UI input injection, native capture, automated command invocation after fixture
setup, selection change or camera mutation is performed by the recorder.
Unsettled transient node differences are recorded without declaring success;
final mismatches must be assessed after the user's operation settles.

CPU OnRender duration is not FPS. GPU/VRAM/FPS: NOT_MEASURED.
Precise three-resolution visual matrix is not claimed until actual sizes are observed.
