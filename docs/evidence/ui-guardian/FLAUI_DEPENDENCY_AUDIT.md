# FlaUI dependency audit

- FlaUI.Core: NOT REFERENCED
- FlaUI.UIA3: NOT REFERENCED
- Target: existing test project targets `net10.0-windows10.0.19041.0`
- Restore result: NOT RUN because no package reference exists and this round does not add a new automation dependency without a pinned compatibility decision.
- Windows support / license review: package adoption deferred; no desktop automation result is claimed.
- Desktop smoke: NOT_RUN

The in-process WPF capture and geometry harness remains the source of truth for this round. Native pointer validation remains a user review step.
