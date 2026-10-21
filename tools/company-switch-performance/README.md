# Scenario11-only company investigation

Source baseline: 99d7818e035310c9273565e73752ee0359ebb8f1. Original 10000 alternating OpenAsync calls and 30000ms threshold are unchanged. The 100000 metadata rows are grouped after Stop(), not inside the switch timer. This is not the P3 400ms query gate.

CompanySwitchProbe is diagnostic-only. Production timings load a copied published Core with verified SHA; phase timestamps use a separately copied Core patched by base64-decoded baseline-instrumentation.patch.b64 plus AssetLibraryOpenDiagnostics.cs.txt. Never ship or substitute that instrumented DLL for acceptance. It adds overhead and is not a before/after gate.

run_concurrent.py runs four fixed named WPF workers. Preserve every repeat, logs and TRX. Process IO counters are logical operations/bytes. sample_disk.ps1 supplies disk-wide raw counters, not process-attributed or per-Open physical disk wait. Disk and SQLite busy-wait fields remain explicit unknown where not observed; wall-minus-CPU is not disk wait.

Synthetic data only. No original files/DB/photos/RAW/credentials/release DLLs committed. Do not change the threshold, loops, skips or math for green results.
