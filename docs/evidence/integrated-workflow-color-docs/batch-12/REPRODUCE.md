# Reproduce batch12 validation (Windows x64, .NET10.0.401)

Run commands from repository root. Set DOTNET_ROOT/PATH only for the current process to a local SDK;
never change persistent machine settings. Use ample local temporary capacity (D: here). App EXE need
not be launched. The testhost is not physical-device acceptance.

```powershell
$r = (Get-Location).Path
$env:TEMP = "$r/artifacts/batch12/temp"
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP

dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --logger 'trx;LogFileName=core.trx' --results-directory artifacts/batch12/core

dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --filter 'FullyQualifiedName~PublicColorReferenceTests|FullyQualifiedName~PublicCubeAndLegacyTests|FullyQualifiedName~PairedCaptureMetadataTests' --logger 'trx;LogFileName=contracts.trx' --results-directory artifacts/batch12/contracts

python tools/batch12/build_cube_fixture.py
python tools/batch12/fetch_public_raw.py --output artifacts/batch12/local-raw/00_0010.CR2
dotnet run --project tools/batch12/PublicRawProbe -c Release -- artifacts/batch12/local-raw/00_0010.CR2 artifacts/batch12/public-raw.json

dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:DeveloperPreviewBuild=true -o artifacts/batch12/release
```

## Optional official reference import (rights required for intended use)

Acquire the two ZIPs from the official support page in DATA_SOURCES_AND_LICENSES.md, preserve bytes,
extract locally outside tracked data. Do not redistribute originals/derived patch values. Run:

```powershell
dotnet run --project tools/batch12/OfficialReferenceProbe -c Release -- LOCAL_BEFORE_TXT LOCAL_AFTER_TXT artifacts/batch12/official.json
```

Both files should produce24patches each, edition/hash checks and unknown-whitepoint/redistribution
refusals. Synthetic parser tests run without X-Rite files or network. Tests do not use printed charts
or website swatches. The importer is a reference-data boundary, not a camera profiling algorithm.

## Independent base golden

`git archive` base9e0fc52b0fd3598bae36ad1ccb5db2b1520ad0d8 into an ignored local baseline directory,
including Core source, Directory.Build.props and build/Version.props; do NOT change the original
worktree. Point `-p:CoreRoot=ABS_BASE/src/RAWSelectionAssistant.Core` at that archive when running
LegacyPixelProbe. Compare the72entry JSON to the pinned pv456-base-golden.json and current capture.
Frozen project state is serialized/reopened before rendering; all EXIF1-8 and instantFilmoff/V1/V2.
No original customer project is substituted by this synthetic corpus.

## Focused existing WPF color contracts

```powershell
$env:KITAO_BATCH11_EVIDENCE = "$r/artifacts/batch12/wpf-color"
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --filter 'FullyQualifiedName~BatchElevenColorContractTests|FullyQualifiedName~BatchTenLegacyRawProductTests' --blame-hang-timeout 60s --logger 'trx;LogFileName=wpf-color.trx' --results-directory artifacts/batch12/wpf-color
```

The authorized company RAW test remains opt-in/skipped here; public RAW was verified separately.
WPF full-suite runs in this environment did not complete. Do not run them indefinitely or count their
partial completed tests as a full pass. The currently running/uncompleted test name is only a locator,
not established root cause. No UI changes made to fix unrelated suite hangs in this batch.
