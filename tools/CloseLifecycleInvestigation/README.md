# CloseLifecycleInvestigation

Standalone controlled WPF investigation against the unmodified unified product source e8180221, delivery base fdf01831. No product implementation changes are made. Each case has a fresh process, application, synthetic database/root and 2GiB/50ms guard. No RAW inputs or forced GC.

Build with the workspace .dotnet/dotnet.exe, Release/x64 and the same WholeAppDiagnosticBuild properties recorded in RUN_COMMANDS.ps1. Run run_guarded.py with --exe and --root; use a fresh --round. Existing run directories are not overwritten.

The helper uses production AssetLibraryPage/FreeCanvasView/FreeCanvasSurface, real WPF Window.Close, atomic production save and actual PNG encoder. Reflection controls private save gates, exact existing prompt delegates and a delayed provider. Nested pumping is explicitly induced to test WPF reentry. These are controlled-host receipts, not full Shell or physical-input acceptance.

Exit0 means the defined expected observation occurred, including reproducing known product failures. It does not mean the product bug was fixed. Original fixture errors are retained and labeled NOT_PRODUCT_FAILURE. Synthetic source images, databases, detailed samples and build output stay outside Git.

Collect small source-bound receipts with collect_evidence.py after the expected rounds exist. Review REPORT.md, OPTIONS_AND_MINIMAL_PROPOSAL.md and PATCH_AND_OWNERSHIP_AUDIT.md before implementing any product changes. Implementation requires user review under the current task instruction.
