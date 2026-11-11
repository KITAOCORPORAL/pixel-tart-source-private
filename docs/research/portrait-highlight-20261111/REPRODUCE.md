# Reproduce

Run PowerShell from repository root. Use a fresh external local directory.

```powershell
$localRun = "../PortraitHighlights-20261111-replay01"
$env:PYTHONPATH = "../ColorResearchHome-20261030/deps/python-tools"
python tools/portrait-highlight/prepare_input.py --source ../kitao-instant-film-evidence-v3-20261016/.validation/instant-v3/visual-review-final/sony-dsc08015-isolated-frozen.tif --local-root $localRun
python tools/portrait-highlight/build_references.py --local-root $localRun --compiler ../ColorResearchHome-20261030/deps/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin/clang++.exe
dotnet build tools/PortraitHighlightProbe -c Release --nologo
python tools/portrait-highlight/run_baseline.py --local-root $localRun
python tools/portrait-highlight/validate_candidate.py --local-root $localRun
```

GPL reference harness source and binaries are generated only under local-root. gh api retrieves two exact pinned commits. baseline and candidate-validation require fresh directories. Settings, function, compiler and executable hashes are retained.

CURVES.csv has 579 measured synthetic rows. Hue is atan2(sqrt(3)*(G-B),2R-G-B) in linear sRGB; opponent magnitude <=1e-6 is undefined. Encoded stages are decoded before Y measurement in RESULTS. Early BASELINE ranges use the stored domain; RESULTS.linearYRange is authoritative for cross-stage luminance. Real photo traces remain local.

```powershell
$env:PYTHONPATH = "../PortraitHighlights-20261111/python-plot;../ColorResearchHome-20261030/deps/python-tools"
python tools/portrait-highlight/plot_curves.py --local-root ../PortraitHighlights-20261111 --output docs/research/portrait-highlight-20261111/curves
```

Use requirements.txt in a separate local --target environment. Original double observations are .f64; .f32 represents materialization rounding. seal.py is only for sealing this delivery and expects tests/closure-final.trx and tests-final-console.txt. See REPORT.md for test scope and limitations.

```powershell
dotnet test tests/RAWSelectionAssistant.Tests -c Release --filter 'FullyQualifiedName~PortraitHighlightResearchTests|FullyQualifiedName~ProfessionalRawPipelineTests|FullyQualifiedName~BatchTwoColorGoldenTests|FullyQualifiedName~ColorStudioToolProcessorTests|FullyQualifiedName~ProfessionalPipelineNoOpPrecisionTests|FullyQualifiedName~LinearRgbColorScienceTests|FullyQualifiedName~LinearWideToolsTests|FullyQualifiedName~RawWideOutputTests|FullyQualifiedName~BatchTenLegacyRawTests|FullyQualifiedName~BatchNineInstantFilmTests' --logger 'trx;LogFileName=closure-final.trx' --results-directory ../PortraitHighlights-20261111-replay01/tests
```
