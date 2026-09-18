# First real installed failure — 2026-09-18

Status: BLOCKED — INSTALLED STARTUP FAILURE. Old candidate REJECTED FOR FURTHER TESTING.

## ROOT CAUSE

Actual user log `PixelTart.DeveloperPreview/Logs/app-20260918.log` records failures at 16:40:14, 16:40:33 and 16:41:16 local time. Exception: `System.Windows.Markup.XamlParseException`; inner exception: resource `BooleanToVisibilityConverter` not found. Stack: `StaticResourceExtension.ProvideValueInternal` → WPF BAML loading → `MainWindow.InitializeComponent` → `MainWindow..ctor` → `App.OnStartup`. Compiled log provides no XAML line number. No missing DLL is reported.

ReferenceColorWorkspaceView resolves this StaticResource during its own InitializeComponent, before it is attached to MainWindow. The converter exists only in MainWindow.Resources, not in the child or App resources. The new eagerly constructed child therefore prevents the whole shell from opening.

## Installed build identity

- USER_INSTALLED_BUILD_ID: `2.3.0-dev.67362b1` (Inno registration; binary version alone is only 2.3.0).
- USER_INSTALLED_PRODUCT_SHA: `67362b112e873ec165508c39ec543ddba73e3b7e` (provenance + binary hash match).
- USER_INSTALLED_BINARY_HASH_MATCH: true, both EXE and DLL.
- EXE SHA256: `18A3C48F9A0D7C889C1B4395C732D90520479E0422F2218424D8297EDCAF06F1`.
- DLL SHA256: `89B8A0D7CE659C7C9177412D204384915FC79645664C06CAFF6CD993A6934FDC`.
- FileVersion 2.3.0.0, assembly version 2.3.0.0, informational version 2.3.0: old binary did not embed its source SHA.
- Installed target and both public desktop/start-menu shortcuts: `D:\1\PixelTart Developer Preview\PixelTart.exe`; working directory is its parent; arguments empty. No stale shortcut mismatch.
- Installer SHA256: `16373B8F5374DA92A9159FFEA4971E47E876BEE460496A23335F2F79219F8BB2`.

## Independent real installer reproduction

Ran the preserved 67362b1 installer, not extraction, with VERYSILENT/SUPPRESSMSGBOXES/NORESTART/NOICONS, empty tasks and a separate AppData test installation directory. Installer exit code 0. Started the installed EXE with C:\Windows as working directory and a separate Chinese/space-containing data root via PIXEL_TART_ACCEPTANCE_ROOT. At 16:53:35 the exact missing-resource exception reproduced. Existing installation files/data were not overwritten. Original Inno registration was backed up and restored because the old installer has a fixed AppId. First hidden diagnostic launch was terminated after capture; this is not a clean-exit PASS.

## SECONDARY ERRORS

None in the three original user traces. Subsequent real source startup at 16:58:23 exposed two masked secondary errors: InvalidOperationException from an implicit TwoWay binding to read-only TetherCaptureViewModel.NextCaptureFolder, and XamlParseException for missing IconButton in Tether's reference item template. These were fixed with OneWay binding and an existing icon-button style alias. Reference workspace Visibility was also bound to its child context rather than shell; corrected with ancestor binding. Later installed startup evidence is recorded in the closure report; it does not imply every UI workflow is accepted.

## AFFECTED FILES

ReferenceColorWorkspaceView.xaml; MainWindow.xaml; App.xaml.cs; startup error dialog; production startup tests and installer provenance/gates.

## WHY AUTOMATED GATES MISSED IT

StageV2StartupCompatibilityTests.MainWindowCanLoadPlanningResourcesTests only searches source text. It never constructs MainWindow, loads the compiled child view, runs production startup or launches installed files. Build and resource compilation do not resolve all deferred runtime StaticResource lookups. Earlier PENDING USER launch status did not establish installed startup success.

Historical installer, manifest, local raw log and registration backup remain in the local history directory. Do not redistribute the failed installer.
