# Pixel Tart native acceptance scaffold — NOT native acceptance evidence

This test-only project was already untracked when the 2026-09-30 continuation
started at `59c8ef89f0266178c54b832159a2d47c952e8650`. It is preserved, not discarded.
It is not referenced by a production project or added to the product solution.
`IsPackable` and `IsPublishable` are false.

## This run's permitted verification

Only `NativeHarnessContractTests` were executed. They exercise in-memory identity,
DPI coordinate, nonce, predicate, PNG validation and build-output isolation
contracts. The PNGs in these unit tests are generated test patterns, not window
captures. No test in this class launches a product, invokes UIA, calls SendInput,
PrintWindow or BitBlt. A passing contract suite is NOT a native gate pass.

Live execution status: **ENVIRONMENT_POLICY_BLOCKED**. The current automation
environment requires its designated Computer Use APIs for Windows app automation.
This project is not an alternate route around that restriction. No custom input
or capture backend was invoked, and the failed external Computer Use APIs were
not retried. Future execution requires an environment that permits this backend.

## Source inventory, not runtime claims

- `PixelTartProcessHost`: fixed production executable under the supplied repository,
  isolated test runtime and only self-started PID/HWND. No attach API.
- `TargetGuard`: executable path, PID, HWND, process start time, foreground, DPI and
  bounds checks; pointer hit ownership is checked by the host.
- `NativeInput`: bounded input primitives and per-event provenance. Unexecuted.
- `NativeWindowCapture`: PrintWindow / BitBlt candidates with PNG validation.
  Unexecuted. No RenderTargetBitmap is labelled a native screenshot.
- `NativeEvidenceReader` / `NativeWait`: nonce-based read-only observations and
  explicit state predicates with bounded deadlines.
- `NativeHarnessContractTests`: pure contracts only; production output check covers
  the current Release directory and deps.json, not a new installer/publish run.

## Remaining implementation/acceptance work

There is no completed Eyedropper, node-drag, route or 3D matrix runner here.
Before native execution can be accepted, validate focus-loss/key-release handling,
per-event guard races, observer timeout during a held gesture, startup cleanup,
capture failure logging and current-monitor DPI in a permitted environment.
Implement the requested BEFORE/input/AFTER matrices and read-only observer
handshake; require matching model/visual/render IDs, genuine sample changes and
actual projected camera changes. Capture/input success must be measured, never
inferred from these source guards. Keep historical failure evidence unchanged.

Current report: `docs/evidence/ui-guardian/NATIVE_HARNESS_CLOSURE_REPORT.md`.
