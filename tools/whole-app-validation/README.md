# Full product diagnostic validation

Use the adapter-discovered runner for every test in Core, WPF, module tests and document tests. It retains every original TRX and explicit missing/non-passing result. WPF uses independent processes and an isolated runtime per fixture. Never count offscreen fixture captures as desktop screenshots.

Build with WholeAppDiagnosticBuild=true and PhotographerTrialBuild=false. The diagnostic package retains the actual MainWindow; it refuses to start without an explicit isolated runtime before migration/logging/database initialization. No installer acceptance is implied.
