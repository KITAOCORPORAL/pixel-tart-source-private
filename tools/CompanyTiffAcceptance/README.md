# Company TIFF acceptance tools

These tools launch the ordinary App with an isolated runtime and a 2 GiB / 50 ms process guard, and read actual TIFF files row by row. They do not automate product commands or replace native file dialogs. Existing run directories are never overwritten.

Launch the ordinary Release App (use its unique apphost alias):

```text
python ordinary_app_guard.py --app FULL_APP_EXE --run NEW_RUN_DIRECTORY --runtime OWNED_RUNTIME_DIRECTORY
```

Write the current observed operation name to RUN_DIRECTORY/phase.txt before each action. Save/close normally. Relaunch the same App with the same runtime and a new run directory for independent reopen. No forced GC is used. FINISH_MONITOR ends monitoring without closing the App and records incomplete status; it must not be used as a completed acceptance run.

Build CompareTiffs.csproj using the existing .NET 10 SDK. The probe accepts the authorized company input (RGB48, orientation1) and RGBA64 outputs. Comparison fails on non-16-bit storage or any non-1 orientation; this company-sample tool does not silently normalize unrelated orientation fixtures. It reads every stored channel and normalized RGB48 channel row, hashing both, and reads complete ICC bytes. No RAW-decoder assemblies are referenced.

```text
CompareTiffs.exe INPUT_TIFF INPUT_PROBE_JSON
CompareTiffs.exe INPUT_TIFF COMPARISON_JSON FIRST_ACTUAL_EXPORT REOPEN_ACTUAL_EXPORT
```

Use --argument on the same guard to forward each probe argument when guarding the probe. Run input probes and output comparison in separate processes; this keeps their resource samples separate from the ordinary App. A matching file probe attests file contents only. Native viewport fit/100%/zoom, session save/close/reopen, and actual exports still require their own UI and process evidence.

Use --scope native-app, controlled-replay, or readonly-probe to record the actual authority. --reopen-session only forwards an existing .ptstudio.json path to the controlled product host. --phase-file can read an explicit stage marker; without marker updates all process samples remain startup and must not be reported as operation-specific peaks. Rebuild sealed sources with -t:Rebuild -p:Platform=x64, since copied files can retain old timestamps.
