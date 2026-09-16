# RC12 WPF process-isolated gate

Runs every `RAWSelectionAssistant.WpfTests` test class in its own `dotnet test`
process. This prevents `System.Windows.Application`, dispatcher, popup, window,
and static resource state from leaking between fixtures.

```powershell
./tools/RC12WpfProcessIsolation/Invoke-RC12WpfProcessIsolation.ps1
```

The gate writes one TRX and log pair per fixture plus a JSON manifest. A failed,
missing, or skipped product test makes the gate fail. The opt-in `P3Diagnostic`
performance fixture is intentionally owned by the separate 10K/50K/100K scale
gate and is excluded here. `-ClassPattern` is available for targeted diagnosis
without changing the full-gate fixture inventory.
