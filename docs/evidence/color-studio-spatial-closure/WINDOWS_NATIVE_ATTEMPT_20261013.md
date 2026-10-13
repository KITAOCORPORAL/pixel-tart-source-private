# Authorized Windows native acceptance attempt — 2026-10-13

This is a new attempt after explicit user authorization to start the Release EXE and use Computer Use. Earlier NOT_RUN documents remain historical; they are not rewritten as passed.

Verified unchanged release SHA256: 29E8D7395306357904ED149D6FE771C30418DBBC480B81B12E339FA3D7C7C119. Launched exact release with PIXEL_TART_ISOLATED_RUNTIME=1 and a separate D-drive data root. The other task's PixelTart instance and normal data were not used.

Startup FAIL: an actual modal and isolated app log show System.InvalidOperationException (Sequence contains no matching element) in TetherCaptureView.Group / LoadWorkspacePreferencesAsync during Loaded. STARTUP_OK log preceding this exception is not acceptance PASS. Workbench and Color Studio subsequently observable; reference-color navigation succeeded through UIA Invoke. No layout or production source was modified in this verification turn.

Capture initially failed in sky (FrameArrived/window capture timeout and unavailable input geometry). Alternate installed Computer Use provider recovered UIA geometry and a target application view. Foreground changed repeatedly to unrelated user apps; neither those occluded screenshots nor private desktop text was retained in files. No further keyboard/data import/system-DPI change attempted after interference. User's ordinary session was not cleared or closed.

100–200% physical DPI, actual photo import/switch/cancel, save/reopen and memory return remain NOT_RUN. Window coordinates are not a measurement of physical DPI. Application exit is recorded, not proof that task memory recovered; no before/after process sampling series was obtained. No production EXE remains running from this attempt.

Required next step: fix or separately authorize the startup preferences lookup defect, then reserve an uninterrupted desktop interval for the remaining interaction matrix. The permission blocker is resolved; the current blockers are startup failure and concurrent foreground interaction/capture reliability.
