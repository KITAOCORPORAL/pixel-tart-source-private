# Files and types

Source commit: `1a9a53913665490298e6de1b5656a33d0efc934b`. Production/test/tool files:

| File | Type and purpose |
|---|---|
| src/RAWSelectionAssistant/AssemblyInfo.Tests.cs | Tests: expose internal editing contracts to WPF tests. |
| src/RAWSelectionAssistant/MainWindow.xaml.cs | Runtime: numeric draft Esc before popup/navigation. |
| src/RAWSelectionAssistant/Resources/Studio/en-US.json | Resources: English Studio product strings. |
| src/RAWSelectionAssistant/Resources/Studio/zh-CN.json | Resources: Simplified Chinese Studio product strings. |
| src/RAWSelectionAssistant/Resources/Studio/zh-TW.json | Resources: Traditional Chinese Studio product strings. |
| src/RAWSelectionAssistant/Services/StudioLocalizationService.cs | Runtime: shared formatted resource rendering. |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Analysis.cs | Runtime: real analysis source description follows language. |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Filmstrip.cs | Runtime: translated visible/selected/hidden counts, stable filter IDs. |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs | Runtime: localized workfile dialogs/status; existing session format unchanged. |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs | Runtime: frozen sync source/targets/counts; localized operation feedback. |
| src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs | Runtime: edit target identity; cancel transaction restores stack/redo; localized native dialog titles. |
| src/RAWSelectionAssistant/Views/CloudInspectionControls.cs | UI: localized inspection help/labels and wrapping. |
| src/RAWSelectionAssistant/Views/ColorSpace3DViewport.cs | Runtime: cancel pointer capture without accidental pick; localized help. |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.Layout.cs | UI: product menu resources, no user-name translation. |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml | UI: reachable right rail, wrapped modes, pinned tone entry and shared resources. |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs | Runtime/UI: existing cloud dock expansion; Esc precedence; responsive bounds. |
| src/RAWSelectionAssistant/Views/StudioColorLabelFilters.cs | UI: localized swatch semantics and language-neutral all glyph. |
| src/RAWSelectionAssistant/Views/StudioCurveEditor.cs | Runtime: route curve cancellation to existing transaction rollback. |
| src/RAWSelectionAssistant/Views/StudioHistogramView.cs | UI: localized histogram hint. |
| src/RAWSelectionAssistant/Views/StudioNumericEditor.cs | Runtime: draft-only numeric editor, finite/range validation, Enter/blur/Esc and target guard. |
| src/RAWSelectionAssistant/Views/StudioSpaceControls.cs | UI: localized automation labels. |
| src/RAWSelectionAssistant/Views/StudioTextExtension.cs | UI: dynamic presentation binding while preserving model keys and user names. |
| src/RAWSelectionAssistant/Views/StudioToolPanel.cs | UI: connect numeric editor to existing stack; restore on target changes. |
| tests/RAWSelectionAssistant.WpfTests/ColorStudioFilmstripStateTests.cs | Tests: frozen sync boundaries across active/filter/selection changes. |
| tests/RAWSelectionAssistant.WpfTests/CompanyColorStudioRepairTests.cs | Tests: right rail/tone/dock layout, camera retention, numeric drafts and cancellation. |
| tests/RAWSelectionAssistant.WpfTests/RuntimeCorrectionWpfTests.cs | Tests: explicit Studio Escape route contract with unchanged source/stack. |
| tests/RAWSelectionAssistant.WpfTests/StudioLocalizationTests.cs | Tests: product resource coverage, duplicates and bound labels. |
| tools/Capture-ColorStudioWindow.ps1 | Evidence tool: opt-in app-owned native-dialog capture, actual DPI and DIP; no input injection. |
| tools/New-CompanyStudioFixture.ps1 | Fixture generator: original public-domain mathematical rasters. |
| tools/Start-CompanyStudioReview.ps1 | Launcher: new Release explicit path + child-only isolated data root; visible app. |

Evidence files (this directory):

| File | Type |
|---|---|
| 00_START_HERE.md | Evidence entry, sync/provenance and incomplete status |
| REPAIR_MATRIX.md | Six-issue implementation/verification matrix and gaps |
| RUNTIME_CHECKLIST.md | All 95 original checkpoints with separate company verdict |
| BUILD_IDENTITY.json | Source/binary/startup identity, SHA256, DPI and launcher limitations |
| RELEASE_MANIFEST.json | Relative paths, bytes, SHA256 for all 287 published files; no binaries committed |
| TEST_RESULTS.json | Sanitized test counts/names and raw-result hashes, including original failures |
| TEST_COMMANDS.md | Reproduction commands and coverage boundaries |
| SCREENSHOT_INDEX.md | Actual startup/empty-state screenshots and missing pair index |
| FILES_AND_TYPES.md | This per-file inventory |
| screenshots/01-company-release-start.png | Real production app onboarding, no private media |
| screenshots/02-company-studio-observed.png | Real production app Studio empty state, no private media |

Raw logs/TRX and private-media captures remain in ignored artifacts/company-repair-2026-10-08. The full WPF suite rewrote three old ui-guardian generated reports; these outputs were copied locally and tracked historical contents restored exactly (blob hashes checked). No historical report is silently updated. No user database, input originals or existing workfile was removed. No dependencies upgraded.
