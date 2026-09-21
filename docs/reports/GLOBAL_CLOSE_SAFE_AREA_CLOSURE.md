# Global Close Safe Area Closure

- Product source SHA: `9bcce6098ce876096bdd444030f554b968384fa0`
- Root cause before: Shell close floated at ZIndex 30000 over route content with no reserved width.
- Fix: Shell-owned 56 DIP structural column; 40×40 target and 8 DIP gap retained.
- Modal ownership: Settings, Task Details, Calendar Details, Online Selection Create, Finance Editor and Booking Editor collapse both the Shell close and its reserve so only the local close authority remains.

## Scope

Eighteen formal production routes were rendered: Workbench, Asset Library, Workflow, Work Calendar, Planning, Tether, Online Selection, Finance, History, Toolbox, Reference Color, Publishing, RAW→JPG, Photo Grouping, Collage, Settings, Activation and Help. Asset quick loupe/viewer and Planning dialog/preview were also captured. Non-closable surfaces are recorded as N/A rather than being given a synthetic close.

Runtime bounds gate results:

- Closable production render states inspected: 33
- Collision before: reproducible because route content and Shell target shared the same right edge
- Collision after: 0
- Width contract test: 1180 / 1366 / 1600 / 1920 / 2560
- Scale contract test: 100% / 125% / 150% / 200%
- Production DPI captures: Reference Color at 100% / 125% / 150% / 200%, plus 2K and 4K
- Visible text geometry audit: 1,695 nodes, 0 findings

Evidence: `artifacts/global-close-safe-area/01_workbench.png` through `16_dialog.png`, plus `GLOBAL_CLOSE_SAFE_AREA_CONTACT_SHEET.png`. The enlarged sheet uses approximately 520×180 top-right crops so hit-target separation remains reviewable.

Tests: `GlobalCloseSafeAreaTests`, `ReferenceWorkspaceWideRatioTests`, and the production `WholeAppVisualAcceptanceTests` runtime gate.

- P0: 0
- P1: 0
