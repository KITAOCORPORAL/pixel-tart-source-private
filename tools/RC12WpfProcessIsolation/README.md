# RC12 WPF process-isolated gate

Runs every `RAWSelectionAssistant.WpfTests` test class in its own `dotnet test`
process. This prevents `System.Windows.Application`, dispatcher, popup, window,
and static resource state from leaking between fixtures.

```powershell
./tools/RC12WpfProcessIsolation/Invoke-RC12WpfProcessIsolation.ps1
```

The gate writes one TRX and log pair per fixture plus a JSON manifest. A failed,
missing, or skipped test makes the gate fail. `-ClassPattern` is available for
targeted diagnosis without changing the full-gate fixture inventory.
