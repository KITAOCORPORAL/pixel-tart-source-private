# Build / test record

All commands ran from the actual repository root using the existing SDK 10.0.302.
`dotnet` below denotes that verified SDK executable, not an arbitrary PATH host.
No global.json, TargetFramework, dependency version or roll-forward setting changed.

## Build

```powershell
dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Debug -p:Platform=x64
dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -p:Platform=x64 --no-restore
dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:InformationalVersion=2.3.0+1a9a53913665490298e6de1b5656a33d0efc934b -o artifacts/releases/company-repair-2026-10-08/publish/win-x64
```

Restore PASS (implicit restore during build/publish); Debug PASS (0 warnings/errors);
Release PASS (0 warnings/errors); production publish PASS. Baseline production
publish used the same command with a9f61bab informational version and the distinct
company-baseline output directory. No installer or acceptance/mock startup args.

## Tests

All test commands use `-c Release -p:Platform=x64`, a unique TRX logger filename
and `--results-directory artifacts/company-repair-2026-10-08/tests`.

```powershell
dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64 --logger "trx;LogFileName=company-repair-core-full.trx" --results-directory artifacts/company-repair-2026-10-08/tests
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --no-build --logger "trx;LogFileName=company-repair-wpf-full.trx" --results-directory artifacts/company-repair-2026-10-08/tests
dotnet test tests/PixelTart.ModularHarness.Tests/PixelTart.ModularHarness.Tests.csproj -c Release -p:Platform=x64 --logger "trx;LogFileName=company-repair-modular.trx" --results-directory artifacts/company-repair-2026-10-08/tests
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~Studio|FullyQualifiedName~ColorStudio|FullyQualifiedName~RuntimeCorrection|FullyQualifiedName~ReferenceWorkspace" --logger "trx;LogFileName=company-release-source-regression.trx" --results-directory artifacts/company-repair-2026-10-08/tests
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --no-build --logger "trx;LogFileName=company-release-source-wpf-full.trx" --results-directory artifacts/company-repair-2026-10-08/tests
```

| Execution | Passed | Failed | Skipped | Scope |
|---|---:|---:|---:|---|
| Company baseline Core (ColorStudio / ColorSpace filter) | 78 | 0 | 2 | Before source editing |
| Company baseline WPF (Studio filter) | 98 | 0 | 2 | Before source editing |
| Core full | 1573 | 0 | 6 | No Core source changed in repair |
| WPF full | 1499 | 0 | 11 | Full run before final bounded layout/resource polish |
| Modular full | 14 | 0 | 0 | After final source changes |
| Final layout/localization | 12 | 0 | 0 | Final source before source commit |
| Release-source related WPF regression | 123 | 0 | 3 | Rebuilt/tested from committed 1a9a539 source |
| Release-source full WPF regression | 1499 | 0 | 11 | Full suite rerun after final committed-source test build; 10m30s |

The final related regression was followed by a full-suite rerun against the same
committed-source test binaries. Neither is native UI acceptance. WPF test builds report existing MSTEST0037 suggestions;
assertions were not removed or weakened. The intended Escape contract test now
asserts idle Studio Escape remains handled **and** source/stack remain unchanged.

Skipped tests are individually named in TEST_RESULTS.json. They require explicitly
authorized RAW/JPEG corpus, opt-in performance/GPU comparison, manual visual gates,
or evidence outputs. Not all skips are hardware failures. No skip is counted PASS.
The DpiTests / native acceptance artifact executables were not run as a substitute
for actual Windows DPI; no installer tests apply to this non-installer request.

## Failures retained

- `cu01-before.trx`: 0 pass / 1 fail. Right rail exceeded its actual 950 DIP
  allocation under inherited Inspector MinWidth. The same bound assertion passed
  after fixing margin/column/MinWidth ownership.
- `company-repair-layout-locale.trx`: 8 pass / 2 fail during repair. Expanded dock
  boundaries touched the main photo; duplicate locale keys were also detected.
  An explicit dock gap and duplicate-key removal fixed the causes; subsequent
  10/10 and final 12/12 pass. Original raw failures and hashes retained.
- One command initially named a nonexistent Core.Tests project. It did not run
  tests; corrected to the repository's RAWSelectionAssistant.Tests project above.

TRX contains machine/account paths and remains local. Public JSON contains only
counts, test names and hashes. Historical test-generated ui-guardian reports were
restored after archiving this run's outputs locally, not submitted as old PASS.
