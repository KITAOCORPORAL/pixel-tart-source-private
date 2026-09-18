# Stage V Planning Center v1 Evidence Manifest

## Automated evidence

- Release build: `dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- Core full TRX: `artifacts/stage-v-planning/test-results/core-full.trx` — 1367 passed, 0 failed, 0 skipped.
- WPF full TRX: `artifacts/stage-v-planning/test-results/wpf-full.trx` — 1252 passed, 0 failed, 1 skipped.
- Planning filters: Core 8/8 passed; WPF 14/14 passed.

## Physical evidence status

| Evidence | Status |
|---|---|
| Real WPF application screenshot | NOT AVAILABLE in current environment |
| 1920×1080 / 2560×1440 / 4K | NOT TESTED on physical displays |
| 100% / 125% / 150% / 200% DPI | NOT TESTED on physical displays |
| Multi-monitor drag | NOT TESTED |
| Real camera/tethered capture | NOT TESTED |

No synthetic screenshot is included. The missing physical evidence is an explicit acceptance handoff, not a hidden pass.
