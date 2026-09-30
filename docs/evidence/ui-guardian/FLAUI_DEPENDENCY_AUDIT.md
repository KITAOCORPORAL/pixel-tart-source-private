# FlaUI dependency audit

- Package: FlaUI.Core 5.0.0 is available on NuGet; FlaUI.UIA3 5.0.0 is available on NuGet.
- License metadata: both packages declare the repository license file LICENSE.txt and repository https://github.com/FlaUI/FlaUI.
- Target project: net10.0-windows10.0.19041.0 x64 WPF test project.
- Compatibility decision: NOT INSTALLED. The current solution uses a process-owned UIA/Win32 harness and no FlaUI dependency was added in this round.
- Restore result: NOT RUN for FlaUI because it is not referenced.
- Desktop automation result: NOT_RUN. No FlaUI PASS is claimed.
- Runtime support: FlaUI 5.0.0 nuspec publishes net8.0-windows7.0 and net6.0-windows7.0 assets; it has no net10.0 asset group. A test-only compatibility build would need a deliberate package adoption check before installation.
- Current native harness: PixelTart.NativeAcceptance launched and owned the production Release EXE and completed its home smoke; this is not FlaUI evidence.
