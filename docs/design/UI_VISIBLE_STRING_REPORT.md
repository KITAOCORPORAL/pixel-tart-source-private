# Pixel Tart UI Visible String Report

Date: 2026-09-15  
Scope: all `src/**/*.xaml` literal values in `Text`, `Content`, `Header`, `Title`, `ToolTip`, `Subtitle`, placeholder, accessible name and help text; plus primary runtime status/error sources for Organize, RAW conversion, compression, Task Center and Asset Library.

## Automated result

| Check | Result |
|---|---:|
| XAML files scanned | all files under `src` |
| Visible attribute categories | 9 |
| Forbidden internal-term leaks | 0 |
| Audited dynamic message files | 9 |
| Dynamic primary-UI leaks | 0 |

Forbidden terms include P1/P2/P3 when present as literal product text, QueryOption, TaskId, LibraryId, AssetId, BookingId, ProjectId, LibRaw, SQLite, SHA-256, ContentHash and raw True/False. Bindings, code symbols, AutomationIds, logs, tests and collapsed diagnostics are not user-facing literals and remain valid implementation details.

The executable report is `ProductLanguageLeakTests`; this document records its RC12 acceptance scope rather than duplicating every ordinary product string.
