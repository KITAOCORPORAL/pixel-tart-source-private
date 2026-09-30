# Native node drag timeline — incomplete

Source under observation: `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`. All times below include their timezone.

- Historical observer: model-derived Items order was labelled UI order; no independent visual-position check.
- 2026-09-30 09:53:20.278 +08:00: source d7ca23b observer response replacement raised UnauthorizedAccessException while a reader held the destination. The UI logged an unhandled exception; the next request timed out. Retained local evidence: `artifacts/native-closure/committed-d7ca23b/Logs/app-20260930.log`.
- PROVEN: Windows response-file sharing contention can break this observer. The writer now retains the last complete response and retries the same nonce on transient IO/access errors. The reader allows delete sharing. Lock-contention regression PASS.
- 2026-09-30 09:56:42.1639781–09:56:43.49599 +08:00: 20 distinct production observer requests succeeded; model IDs, screen-Y-sorted realized row IDs and completed-render IDs agreed; one unchanged output hash; no error.
- Native input blocker: screenshot capture failed twice with E_NOINTERFACE; text-only click could not obtain coordinate geometry. No drag was injected.
- Native drag root cause: NOT PROVEN. Collection notification ordering, drag coordinates, virtualization and history semantics must still be exercised.
- All ten requested native cases, Undo and Redo: NOT RUN. See `NODE_DRAG_NATIVE_MATRIX.json`; null input fields mean no native input, not missing successful evidence.

Synchronization contract: a fresh nonce, settled editor, no product error, no active drag, exact model/render IDs and ordered realized visual IDs. The reader fails at a bounded deadline; elapsed sleep is never a success condition. Initial state agreement does not establish post-drag agreement.
