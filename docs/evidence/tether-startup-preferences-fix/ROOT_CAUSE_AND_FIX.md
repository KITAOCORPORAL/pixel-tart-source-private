# Startup preference lifecycle fix

Base eae12944c66e9e519fceb0842344f8b0c9038b7b; independent worktree D:/AI AGENT/kitao-startup-fix-20261013; branch fix/kitao-startup-preferences-20261013; product source cb55960ec622951877311d1a1cc5aa4a7a0057f8.

Verified the baseline release identity, actual startup log and code. Old released source924d48683f9995721335e2dcd1bc89fe7040305d; old EXE SHA25629E8D7395306357904ED149D6FE771C30418DBBC480B81B12E339FA3D7C7C119 remains unchanged in original worktree. No old output replaced.

## Proven cause

TetherCaptureView Loaded awaits persisted preferences then calls Group(header). Group enumerates the main InspectorPanel visual descendants and First matches a translated display header. The main host is collapsed if HasSelection=false or compact; no ContentControl template instance is guaranteed at that moment. InspectorDrawer is a separate instance of the same DataTemplate and was not included by the old lookup. UpdateLayout cannot guarantee a collapsed/no-content visual tree. The old save method repeats every mandatory lookup, failing on no-selection, deferred template or collapsed main panel.

## Implementation

TetherInspectorPreferenceState owns the nine logical persisted expansion values independent of visual generation. Load creates one shared asynchronous read task, restores logical values and saved width, never enumerates expanders or forces templates. Existing production Expander IsExpanded properties bind TwoWay to that shared state on the ancestor TetherCaptureView. When either template instance materializes it receives current values; toggling main/drawer updates state and the other instance. Other selected-asset bindings remain unchanged. No group header/name, dimensions, order, style or Grok layout is changed.

Save snapshots logical state, awaits pending read, and preserves last valid width when main ActualWidth=0. Missing/cleared template, no selected photo and both collapsed panels cannot erase values or throw because no visual lookup remains. Unloaded and Loaded now expose their lifecycle task. Only recoverable IO/access/JSON errors are caught, with retained Exception, explicit error message and Trace error; logic/contract exceptions are not swallowed. There is no empty catch around Group.

Review-presentation helper updates the same logical group flags, so it remains useful before any template instance exists. Test injection uses an isolated preference store path; normal parameterless constructor remains intact.

## Tests

First startup/no data and save before visual creation; actual Loaded/Unloaded dispatcher exception capture; two actual production InspectorTemplate instances restored from file; bidirectional toggle propagation; narrow width while main is zero-width; both collapsed; template removal/recreation with writable no-selected-photo test context; save/reopen width/flags; injected blocked filesystem path records failure and retains state/file. Focused suite5new+8existing=13/0/0. Full Core1660/0/6, WPF1547/0/23. Final release component suite13/0/0; hashes match release components.

First test attempts failed on test assumptions (multiple ContentControls; templates materializing despite null content; readonly fake SelectedAsset boundTwoWay). Corrected tests explicitly select template hosts, remove/recreate templates and use a writable selected property; failureTRX retained. Product assertions not relaxed. No Core colour/math algorithms or PV4/PV5/PV6 files changed.
