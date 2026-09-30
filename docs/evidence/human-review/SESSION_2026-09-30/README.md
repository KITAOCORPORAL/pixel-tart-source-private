# Human Native session evidence — 2026-09-30

This directory is the canonical Git copy of the read-only evidence recovered
from session `3005e060-fd05-454a-a973-d32af16d62e3`.

- Source HEAD: `1a5bf7d9b8f0d92aae8df8d87a4aed4953811a63`
- Product PID/HWND: `20052` / `6950308`
- Input source: human; no Codex input injection
- Automated Native: `ENVIRONMENT_POLICY_BLOCKED`
- Machine Native Approval: `NOT GRANTED`
- User Visual Approval: `false`
- UserVerified: `false`
- Native Closure: `NOT CLOSED`

`observer-state.jsonl` is the raw changed-state stream (13 MB). The product
observer is request/response based and the external recorder polled it; this is
not a lossless OS input/event trace. `NODE_DRAG_HUMAN_NATIVE.json` is retained
as the raw node-state evidence (13 MB). The timeline and human-review document
explain the evidence gap and do not convert any field to PASS.

## Included

Canonical session manifests, final observation, sample/node/3D evidence, raw
observer and route/sampling streams, observer errors, product drag event, frozen
file inventory/provenance, recorder protocol scripts, checklist, acceptance
report, recovered timeline and the empty user issue table.

## Deliberately excluded from Git

The original `artifacts/human-native-acceptance/latest/` remains intact locally.
Its runtime database (`pixel-tart.db`, `-wal`, `-shm`), settings, cache, backups,
generated PNG fixtures, runtime response file, build logs, and duplicate
pre-freeze/frozen copies are machine-local runtime or redundant copies. They are
not product source or portable acceptance evidence and must not be uploaded.
The committed `FROZEN_FILE_INVENTORY.json` records the complete local frozen
inventory and SHA-256 values without uploading those private/runtime files.

No product process was closed or changed while this evidence was copied.
