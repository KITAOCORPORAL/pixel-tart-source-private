# Independent installed UI acceptance runner

Status: **compile/plan validation only; never live-executed in this session**. This is partial infrastructure, not a completed acceptance pass.

No project references or production assembly loading. The runner installs into a new SHA-named directory under `%LOCALAPPDATA%/PixelTart-TestAcceptance`, launches its real `PixelTart.exe` with isolated data, validates product version, and uses PID-scoped Microsoft UI Automation patterns. No ViewModel, ICommand, reflection, JSON/SQLite state editing or RenderTargetBitmap.

`--validate plan.json` only checks schema, isolation and installer hash; no UI, install or process launch. `--run plan.json` is a live desktop operation and must be used only in an environment permitting that API. The current Codex computer-use skill restricts live Windows automation to its supported JS API, so this session must not invoke `--run` or a substitute wrapper.

Ambiguous selectors, missing patterns, window clipping, black images, escaped focus, installer/hash/version mismatch and abnormal close fail closed. A partial plan can never emit READY FOR USER ACCEPTANCE. Screenshot capture alone does not prove no occlusion or full popup inclusion; manual review is required.

Build with `dotnet build tools/PixelTart.InstalledAcceptance -c Release`. Use the included `planning-smoke.plan.json` only as an initial, unverified selector plan. It stops after opening the workspace and dumping supported patterns. Before extending it, inspect actual UIA results: duplicate “新建策划” buttons require stable scoped identification, not selecting the first match. Missing AutomationIds are not yet proven and no production accessibility changes have been made.

Remaining implementation/verification: scoped modal selectors; complete create/edit/restart flow; import and quick preview; context menu and popup capture; actual SaveFileDialog export and PDF inspection; tether/online navigation state assertions; old→new complete upgrade. These are explicitly not PASS.
