# Asset Library P1 foreground evidence helper

`Capture-AssetLibraryP1WindowEvidence.ps1` records one bounded window capture after a human or Computer Use action has already occurred. It never moves the pointer, sends keyboard input, invokes a command, or raises a synthetic UI event.

The caller must provide the exact PID, full executable path, exact window title, output directory, and a unique capture name. The helper refuses to capture unless all of the following are true:

- the PID resolves to the supplied executable path;
- exactly one process with that executable name and exactly one process at that path are running globally;
- the PID owns exactly one visible product window with the supplied title; non-foreground, untitled Chinese IME status/composition windows are recorded and accepted only by exact class allowlist;
- any other visible auxiliary top-level window (including a transient WPF ToolTip/Popup) is never broadly allowlisted: the helper waits up to 15 seconds for a quiet window set while continuously requiring the same exact foreground main HWND, then rejects the capture if the auxiliary window remains;
- that window has the supplied title, is not minimized, and is currently foreground;
- the output PNG and JSON names do not already exist.

The default screenshot mode is the physical screen via `System.Drawing.Graphics.CopyFromScreen`. When a Computer Use pointer/highlight overlay would obscure evidence, pass `-CaptureMethod PrintWindow`; the helper then uses `PrintWindow(PW_RENDERFULLCONTENT)` on the same exact foreground HWND. That mode records the unedited pixels rendered by the live WPF window and excludes unrelated desktop overlays; it is not a crop, repaint, synthetic UI event, or image post-processing step. The companion `*.window-evidence.json` records the selected method, exact PID/path/title, exact-title main-window count, any allowlisted non-foreground IME auxiliary window, Win32 handle and physical-pixel rectangle before and after capture, `GetDpiForWindow`, `EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)` width/height/refresh rate, `GetScaleFactorForMonitor` scale, the owning monitor bounds and working area, current video-controller display mode, global process counts, executable SHA-256, PNG SHA-256, and whether the window and display observation remained stable. A false or unavailable DPI-awareness-context request is recorded rather than hidden; the observed window DPI always comes from `GetDpiForWindow`.

Example for an already-running, foreground DevPreview:

```powershell
.\tools\AssetLibraryP1Acceptance\Capture-AssetLibraryP1WindowEvidence.ps1 `
  -ProcessId 12345 `
  -ExecutablePath 'D:\acceptance\PixelTart_ModularHarness_V1_DevPreview.exe' `
  -WindowTitle '像素蛋挞 [Modular Harness Dev]' `
  -OutputRoot 'D:\acceptance\p1-gate-a' `
  -CaptureName 'asset-library-default-150pct'
```

Use a new capture name for every action. The helper never overwrites evidence. The JSON contains machine-local absolute paths and runtime PIDs, so keep it in the run-specific acceptance output rather than committing it as a portable repository artifact. A successful helper run proves only the recorded screen/window identity, DPI, and pixels; it does not by itself prove that the preceding physical action or the visible product behavior passed.
