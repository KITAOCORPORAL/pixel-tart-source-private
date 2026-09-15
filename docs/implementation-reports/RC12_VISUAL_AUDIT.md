# RC12 Visual Audit

Date: 2026-09-15  
Product: Pixel Tart 2.3.0-RC12  
Evidence: `artifacts/rc12-product-visual-pass2-final/rc12-product-visual-evidence.json`

## Method

The current App.xaml/MainWindow and Pixel Tart dark theme were exercised with synthetic assets only. Each capture used a fresh process and the validator checked the capture manifest, process lifecycle, dimensions, clipping/overflow flags, zero-sized interactive controls, and unexpected white surfaces.

## Required product states

| Capture | Result | Audit note |
|---|---|---|
| AssetLibraryGrid | PASS | Dense grid remains legible; selected tile has a clear teal focus ring. |
| AssetLibraryMasonry | PASS | Variable-height cards retain readable filename/metadata rows. |
| AssetFilter | PASS | Filter controls remain visible against the dark shell. |
| AssetContextMenu | PASS | Context menu is present without white-system surface leakage. |
| AssetInspectorProject | PASS | Inspector/picker state is present and readable. |
| AssetViewer | PASS | Viewer state is captured with the shared dark chrome. |
| CalendarBookingAssets | PASS | Booking detail includes the linked-asset strip and workflow counts. |
| AssetInspirationTray | PASS | Inspiration tray state is visible and bounded. |
| AssetInspirationCollection | PASS | Collection panel/grid state is visible; the companion WPF contract covers drag/reorder target wiring. |
| AssetRecentLibraries | PASS | Recent-library switcher state is visible; restart proof remains a functional-gap item. |
| AssetOfflineCachedPreview | PASS | Offline/cached preview state is visible with dark fallback treatment. |
| AssetProjectBookingPicker | PASS | Project/booking picker state is visible and bounded. |

## DPI and resolution

- 32 current-DPI captures passed at 100%, 125%, 150% and 200%.
- Six Asset Library resolution captures passed.
- The validator reports 50/50 captures passed, with unique process IDs and clean process exit between captures.

## Remaining visual tuning

No P0 visual failure was found in this evidence set. The remaining RC12 gate items are behavior/evidence gaps rather than screenshot clipping failures: P2 sealed-run probe/full zero-skip isolation seal, relation/client click-and-restart fixtures, and Calendar click-route workflow.
