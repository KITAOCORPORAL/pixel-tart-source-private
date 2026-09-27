# Color Match / GPU / RAW / TIFF current implementation audit — 2026-09-27

Audit base: `efd1ba1b35c094364ba5fbae2367701ef1f76c40` on `integration/pixel-tart-developer-preview`. This is a source, test, and Git-history audit; no product code or algorithm was changed. **`IMPLEMENTED` below means the narrowly stated behavior exists in current source and has a passing relevant test; it does not imply product integration or camera acceptance.** `PARTIAL` means at least one required link or acceptance gate is missing. A design, interface, test fixture, helper, fallback, or API name is never counted as an integrated feature.

## Current Architecture

The **production Color Studio / reference workspace runs the CPU `ReferenceLookMatcher` path (the V3 generation)**. The reference service builds a 33³ preview LUT or 65³ export LUT from that transform. A node stack calls `ColorStudioBitmapRenderer` → `ColorStudioRenderPipeline` → `ReferenceLookMatcher`. The workspace preview uses an up-to-1600-pixel interactive source, then a full-resolution `BitmapSource`; batch export reloads each source through WPF and writes JPEG. [View-model render](../../src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs), [preview service](../../src/RAWSelectionAssistant/Services/ReferenceLookPreviewService.cs), [batch export](../../src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs), and [stack renderer](../../src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs) establish this path.

The **V4 OT engine is a separate Core path**. `rg` of production `src/` finds no construction or call of `ReferenceMatchV4Engine` outside its definition; its current callers are Core tests and the optional benchmark. `ReferenceMatchV4Cache.CreateKey` returns a string but does not store or reuse an output. `ColorMatchTilePlanner` and `V4TilePolicy` calculate tiles but the engine does not process the image tile by tile. [V4 engine](../../src/RAWSelectionAssistant.Core/Services/Projects/ReferenceMatchV4.cs), [V4 telemetry](../../src/RAWSelectionAssistant.Core/Services/Projects/V4Telemetry.cs).

The high-precision `HighBitDepthImageBuffer` plus `ColorStudioRenderPipeline.Render(HighBitDepthImageBuffer, ...)` are real Core processing APIs. The WPF Color Studio adapter still converts `BitmapSource` to `VisualPixelBuffer` RGB24 before calling the byte overload. The professional RAW decoder, high-precision Core renderer, and atomic TIFF16 writer are therefore **not one wired product workflow**. [WPF adapter](../../src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs), [float buffer](../../src/RAWSelectionAssistant.Core/Services/Color/HighBitDepthImageBuffer.cs).

## Match v3

`ReferenceLookMatcher` is the current product match generation. It normalizes multiple reference weights, combines their luma histograms, builds a bounded monotonic quantile tone curve, and compares source pixels with palette-derived reference statistics in OKLab. It computes global and three smooth luminance-zone color offsets, contrast/saturation scalars, skin-candidate/neutral/highlight protection, and sRGB gamut compression. The controls include match, tone, color, contrast, saturation, and keep-original-tone strength. This is deterministic distribution matching on a display-referred proxy; the skin rule is an OKLab hue/chroma heuristic, not semantic skin detection. [Matcher and transform](../../src/RAWSelectionAssistant.Core/Services/Projects/ReferenceLook.cs), [zone and tone builders](../../src/RAWSelectionAssistant.Core/Services/Projects/ReferenceColorCoreV2.cs), [V3 tests](../../tests/RAWSelectionAssistant.Tests/StageIVColorCoreTests.cs).

## GPU Backend

**ComputeSharp 3.2.0 over DirectX 12 compute shaders** is implemented in Core. It is neither DirectML, CUDA, nor ONNX Runtime. A hardware device and AddOne smoke shader gate availability; `PairwiseKernelShader` computes only the `n × m` regularized OKLab pairwise kernel for representative samples. The kernel is copied back to host memory. Sinkhorn iterations, barycentric mapping, region deltas, protections, residual passes, gamut mapping, and full-image pixel application remain CPU code. [package](../../src/RAWSelectionAssistant.Core/RAWSelectionAssistant.Core.csproj), [shader and host loop](../../src/RAWSelectionAssistant.Core/Services/Projects/GpuColorMatchCompute.cs), [engine](../../src/RAWSelectionAssistant.Core/Services/Projects/ReferenceMatchV4.cs).

**Does a GPU actually perform image matching computation?** Yes, when the isolated V4 engine runs on a validated hardware device, it dispatches the representative pairwise kernel. The current-head GPU parity tests passed on this host without a GPU skip. **The production Color Studio match path does not call that engine, so ordinary product preview/export does not currently use this GPU match computation.** The prior [GPU performance record](REFERENCE_MATCH_V4_GPU_PERFORMANCE.md) names an RTX 5060 Ti host; that historical device identity is not a fresh measurement of this audit run.

## GPU Processing Stages

| Stage | Current GPU work | Current product path |
|---|---|---|
| Reference analysis | None; WPF/CPU visual analysis and per-service cache | CPU |
| Feature extraction / representative sampling | CPU samples and converts to OKLab | V4 not wired |
| Histogram / statistics | None; V3 CPU histogram and palette analysis | CPU |
| Color transform / OT | **GPU only for V4 pairwise kernel**; Sinkhorn and mapped colors on CPU | V3 CPU |
| Local correction / residual | None; V4 CPU luminance bands and scaled residual passes | V3 CPU zone correction |
| Mask / protection | None; V4 CPU heuristic protection; byte stack selection on CPU | CPU |
| Preview | None | WPF/CPU rendering |
| Export | None | WPF/CPU JPEG; separate TIFF paths |

`V4GpuStage` enum names stages that have no GPU shader or dispatch in the current engine. [Telemetry types](../../src/RAWSelectionAssistant.Core/Services/Projects/V4Telemetry.cs) are not called from the V4 match engine.

## VRAM Strategy

`GpuMemoryBudget.FromBytes` classifies **decimal** dedicated GB: `<8` LOW (256 samples/384 tile/1 parallel), `8–<12` STANDARD (512/512/2), `12–<16` HIGH (1024/768/3), `≥16` ULTRA (2048/1024/4). Thus nominal 8GB, 12GB, and 16GB map to STANDARD, HIGH, and ULTRA respectively. This is a **budget descriptor**, not a runtime allocator or dispatch policy. `ForQuality(High/Ultra, budget)` supplies settings when explicitly called; production code does not select it from detected VRAM. Current GPU allocation is six sample buffers plus an `n × m` float kernel; it has no live-free-VRAM query, memory reservation, parallel-tile scheduler, or budget enforcement. [tier code](../../src/RAWSelectionAssistant.Core/Services/Projects/ReferenceMatchV4.cs), [shader allocations](../../src/RAWSelectionAssistant.Core/Services/Projects/GpuColorMatchCompute.cs).

Failure handling exists **inside the isolated V4 engine**: unavailable GPU or GPU dispatch exception falls back to CPU; `OutOfMemoryException` is separately recorded. It retries the small representative mapping on CPU, not a full pipeline checkpoint/recovery strategy. Failure categories such as DeviceLost and DriverReset are declared but not specifically diagnosed. No production Color Studio OOM policy calls this code. The tile planner and resolution-aware `V4TilePolicy` are helpers only; the full source/output arrays are processed in CPU loops, not chunks.

## 16GB Mode

**No automatic 16GB quality mode is enabled in the product or V4 engine.** A 16GB budget maps to ULTRA and can influence an explicitly requested `ForQuality(Ultra, budget)` settings object, but the V4 `Match` overload defaults to `new ReferenceMatchV4Settings()` and never calls capability detection or `ForQuality`. HIGH/ULTRA settings are not selected by the Color Studio UI. At default settings, 8GB/12GB/16GB hardware gets the same algorithm parameters. The higher sample/tile values are potential settings, not an observed high-quality mode.

## RAW Pipeline

The existing RAW-to-JPEG product task calls `LibRawDecoder` → `WpfJpegEncoder`. Candidate recognition is `.ARW`, `.CR2`, `.CR3`, `.NEF`, `.NRW`, `.RAF`, `.DNG`, `.RW2`, `.ORF`, `.ORI`, `.PEF`, `.3FR`, `.FFF`, `.IIQ`, `.SRW`, `.RWL`, `.X3F`. This recognizes Sony, Canon, Nikon, Fujifilm, Panasonic, OM/Olympus, Pentax, Leica/DNG, Hasselblad, Phase One, Samsung, Sigma, and generic DNG candidates; **it does not verify a camera model or decode that vendor's files**. The local fixture manifest has `FIXTURE_PENDING` and an empty fixture list. [candidate list](../../src/RAWSelectionAssistant.Core/Models/RawToJpegModels.cs), [fixture manifest](../../tests/fixtures-local/raw/RAW_FIXTURE_MANIFEST.json).

`FastPreview` requests LibRaw 8-bit sRGB RGB24. Explicit `ProfessionalDecode` requests 16-bit sRGB RGB48 and reads `ushort` samples if returned; file length/mtime are checked around decode, and make/model/time/orientation are read. An embedded-preview API and bounded `RawDecodeCache` exist, but production callers of the cache were not found; the RAW-to-JPEG default remains `FastPreview`. `WpfJpegEncoder` constructs `PixelFormats.Rgb24` from `Rgb24Pixels`, so it is not a professional RGB48 output adapter. [decoder](../../src/RAWSelectionAssistant.Core/Services/RawToJpeg/LibRawDecoder.cs), [load/cache contracts](../../src/RAWSelectionAssistant.Core/Services/RawToJpeg/RawLoadContracts.cs), [JPEG encoder](../../src/RAWSelectionAssistant/Services/RawToJpeg/WpfJpegEncoder.cs).

**Current product paths are separate:** RAW → LibRaw 8-bit sRGB decode → JPEG is the RAW conversion task. WPF-loadable JPEG/PNG/TIFF → V3 CPU reference match → 8-bit preview → JPEG export is the Color Studio path. A user can feed a converted JPEG into Color Studio, but there is no single RAW → Match → Preview → TIFF product workflow. A code-capable but **unwired** professional Core sequence is RAW RGB48 → float buffer → float Color Studio/V3 or V4 Core match → TIFF16 writer. No test uses an actual camera RAW through that full sequence. `HighBitDepthImageBuffer.FromRaw` also uses the RGB48 constructor that defaults orientation to 1 and drops the `RawImageMetadata` object, a metadata/rotation propagation gap.

## TIFF Pipeline

There are two distinct paths. The general Publishing UI uses WPF `RenderTargetBitmap(Pbgra32)` and `TiffBitmapEncoder` with LZW, so it is an **8-bit rendered output path** even if the source TIFF is 16-bit. The Core `TiffExport.WriteRgb24` writes uncompressed little-endian RGB TIFF at 8-bit or writes 8-bit values multiplied by 257 for 16-bit; that latter option is **promotion, not recovered precision**. `TiffExport.WriteRgb48` writes true 16-bit samples from a float buffer. `AtomicTiffWriter.WriteRgb48Async` writes a temporary file, flushes and validates it with the own-format reader, then replaces the destination. [Publishing renderer](../../src/RAWSelectionAssistant/Services/Publishing/WpfPublishingRenderer.cs), [Core writer](../../src/RAWSelectionAssistant.Core/Services/Export/TiffExport.cs), [atomic adapter](../../src/RAWSelectionAssistant.Core/Services/Export/AtomicTiffWriter.cs).

The Core writer labels RGB output and can embed a structurally checked ICC byte payload (header length and `acsp` signature), explicit DPI, orientation, and Software. It **does not color-convert** between sRGB/Adobe RGB/ProPhoto/P3, validate profile semantics, preserve camera EXIF, write the supplied `Metadata` dictionary, or compress RGB48. `TiffReadBack` reads only the writer's bounded little-endian 16-bit RGB layout and selected ICC/DPI/orientation fields; it is not a general TIFF importer. The WPF publishing path has separate color-context/metadata behavior, with no demonstrated 16-bit match parity.

## Preview / Export Parity

The V3 **full-resolution CPU** test compares `ReferenceLookPreviewService` preview plus film with `ProcessForExportAsync` pixels and gets exact channel equality on a small generated PNG. Another WPF test covers a frozen Color Studio node stack for an inactive batch target. That supports equality for those paths and fixtures, not every input, proxy, film node, RAW, TIFF, color profile, or GPU path. Interactive preview may be downscaled to 1600 pixels while export reloads full resolution; the 33³ preview LUT and 65³ export LUT are separate outputs. No V4 production preview/export path exists, so **V4 GPU preview versus final export pixel consistency is NOT VERIFIED**. [parity tests](../../tests/RAWSelectionAssistant.WpfTests/BatchExportProcessedPixelsTests.cs).

CPU and GPU V4 share one high-level `ReferenceMatchV4Engine` and the same CPU pixel-application code, differing only in representative kernel calculation. They do **not** share a fully accelerated canonical processing pipeline, and the production V3 renderer does not call V4. The float Color Studio overload and byte WPF overload are distinct implementations; float `ColorRange` does not use the byte path's sample selection, and float Film implements a subset of byte Film effects. Therefore complete byte/float preview/export parity is open.

## Performance

The optional V4 benchmark uses **synthetic RGB buffers**, not camera images, with 256 representative samples, 16 Sinkhorn iterations, zero residual passes, and three CPU/GPU attempts at 12/24/45/60MP. Its 24MP and 45MP results are historical test-run data; 33MP and 61MP are absent, and 60MP must not be relabeled 61MP. The current-head targeted test run left this benchmark skipped because `PIXEL_TART_RUN_V4_BENCHMARK` was not set. The historical [performance record](REFERENCE_MATCH_V4_GPU_PERFORMANCE.md) reports roughly 3.5 s at 24MP and 6.8–7.1 s at 45MP for both paths; it did not measure peak VRAM, utilization, or real-photo throughput. The test's `tileCount` is a planner count, not proof of tiled processing. [benchmark source](../../tests/RAWSelectionAssistant.Tests/ReferenceMatchV4BenchmarkTests.cs).

## Tests

Current HEAD execution in this audit:

| Command scope | Result | What it proves / does not prove |
|---|---|---|
| Filtered Core classes: V4, V3/Color Studio, float precision, RAW contracts, TIFF, telemetry | **40 passed, 0 failed, 1 skipped** | Core synthetic/contracts; optional large benchmark skipped; V4 GPU parity tests did not skip on this host |
| Filtered WPF `BatchExportProcessedPixelsTests` and `ReferenceColorRefreshTests` | **21 passed, 0 failed, 0 skipped** | Generated-image preview/export, cache, batch and UI integration behavior; not vendor RAW or installed-device acceptance |

Relevant test families: [V4 backend/parity/fallback](../../tests/RAWSelectionAssistant.Tests/ReferenceMatchV4Tests.cs), [V3 quality and LUT](../../tests/RAWSelectionAssistant.Tests/StageIVColorCoreTests.cs), [Color Studio stack](../../tests/RAWSelectionAssistant.Tests/ColorStudioStackTests.cs), [float precision](../../tests/RAWSelectionAssistant.Tests/ProfessionalPipelineNoOpPrecisionTests.cs), [RAW contracts](../../tests/RAWSelectionAssistant.Tests/RawPrecisionTraceTests.cs), [TIFF write/read](../../tests/RAWSelectionAssistant.Tests/TiffReadBackTests.cs), [atomic TIFF](../../tests/RAWSelectionAssistant.Tests/AtomicTiffWriterTests.cs), and [WPF matching/batch/cache](../../tests/RAWSelectionAssistant.WpfTests/BatchExportProcessedPixelsTests.cs). GPU failure injection uses a throwing fake; real device loss/driver reset and OOM are not hardware-tested. No legal vendor RAW fixture is present.

Additional Match-related test files identified in the test tree: `StageIIIReferenceLookAndShotTests.cs` (look and shot persistence), `ColorStudioSampleMappingTests.cs` (sampling coordinates), `ColorStudioStateClosureTests.cs`, `StageIVReferenceWorkflowTests.cs`, `ReferenceColorRefreshTests.cs`, `ReferenceFilmVisualEvidenceTests.cs`, `ReferenceWorkspaceDpiEvidenceTests.cs`, and `ReferenceWorkspaceWideRatioTests.cs` (WPF workflow/visual/layout); `ReferenceMatchV4BenchmarkTests.cs` (small synthetic quality and optional large benchmark); `V4TelemetryTests.cs` (helper only); `ProfessionalRawTiffTests.cs`, `RawPrecisionContractTests.cs`, `RawPrecisionTraceTests.cs`, `HighBitDepthImageBufferTests.cs`, `TiffReadBackTests.cs`, and `AtomicTiffWriterTests.cs` (adjacent RAW/TIFF contracts). These are inventory, not a claim that every visual or optional benchmark test was run in this audit. The two current-head filtered runs above are the executed evidence.

Git-history cross-check: `813fcc5` introduced live reference match; `32652eb` added protection/LUT work; `d6a9d13` upgraded the CPU reference engine; `32f9359` added multi-reference flow; `1181ee5` introduced V4 Core OT; `22941ed` added capability/benchmark foundations; `a895594` added ComputeSharp DX12 pairwise compute; `b14d8fa` and `94d7e1c` added RAW precision contracts; `db5917a` added V4 telemetry/tile helpers; `b61702c` added float Core processing; `a6d750c` added atomic TIFF16 validation; `e286a8e` corrected documentation. Present source and current tests, rather than the commit subject, determine the statuses above.

## Implemented

- Production CPU V3 matching, multi-reference weighting, controls/protection, preview and JPEG export on supported WPF image inputs.
- Per-instance external-reference analysis cache keyed by full path, length, and mtime; concurrent requests share a task, and a changed timestamp misses the old entry.
- Isolated V4 CPU OT and real DX12 representative pairwise shader with tested CPU fallback/parity on this host.
- LibRaw 8-bit path and explicit 16-bit decode branch; Core float-buffer processing and true RGB48 TIFF writer with atomic validation. These Core components have separate tests and are not a tested camera-to-TIFF product chain.

## Partial

- RAW high-bit-depth chain: decoder and Core processing exist, but no legal vendor fixtures or full product RAW → match → TIFF path.
- TIFF color/metadata: ICC byte embedding and a few tags work; color conversion, EXIF and product 16-bit delivery do not.
- V3 preview/export parity: small full-resolution fixture passes; interactive proxy, large/RAW/TIFF/color-managed paths need evidence.
- GPU support: only isolated V4 pairwise calculation is GPU; no production Color Studio GPU match or full-image GPU processing.
- VRAM budget, tile and telemetry types exist but are not wired into allocation, dispatch or quality selection.

## Not Implemented

- V4 selection in the production Color Studio/Reference Match UI or export path; V4 multi-reference look integration.
- GPU reference analysis, feature sampling, Sinkhorn, local correction, masks, residuals, gamut, preview or export.
- Automatic 16GB Ultra mode; actual tiled/chunked match application; live VRAM budget enforcement.
- Verified RAW vendor/camera matrix, full RAW16 product export, complete camera metadata propagation.
- Real-photo 24/33/45/61MP performance and V4 GPU preview/export parity.

## Technical Debt

1. V4 `CacheKey` has no cache store, source content fingerprint validation, or invalidation lifecycle; it omits several settings that can alter output (for example displacement caps and residual controls).
2. V4's `ResidualError` measures the applied delta magnitude, not error against a target distribution; later passes reapply scaled deltas rather than recompute correspondence. `Decompose` fills some fields with constants/zeros. These are foundation metadata, not a validated perceptual optimizer.
3. The reference analysis cache is unbounded per service instance; old identity keys remain after file changes, and new service instances do not share it. The key is path/length/mtime, not a content hash.
4. The Core float stack and WPF byte stack diverge for sampled color ranges and Film effects. `PrecisionTrace.ProfessionalDefault` is a static stage list rather than runtime proof of every path; no label should substitute for an end-to-end precision check.
5. `WpfJpegEncoder` assumes RGB24 even though the decoder has a professional RGB48 mode. Product callers must explicitly choose a compatible adapter rather than pass the high-bit-depth result into this encoder.
6. TIFF Core options expose metadata that the writer does not emit; ICC validation checks structure only. Publishing TIFF uses a separate 8-bit WPF render path.
7. GPU capability reports an exact feature level and driver version as unknown; `GpuFailureReason` declarations exceed actual classified failures. No measured peak VRAM or GPU utilization is captured.

## Recommended Match v4 Architecture

Keep one versioned, color-managed processing contract from decode to export: source identity + profile → high-precision canonical RGB buffer → reference analysis keyed by content/profile/algorithm → bounded representative OT and explicit CPU/GPU backend → deterministic per-pixel application → preview adapter or high-bit-depth export adapter. Store the actual selected quality/backend and stage telemetry with each result. The GPU path should accelerate only stages that pass differential CPU/GPU tests, while a stage failure reruns from a known CPU checkpoint. Define a stable output contract for proxy versus full resolution, ICC conversion, orientation and metadata before promising pixel equality. This is a recommendation, **not current implementation**.

## Next Development Stage

1. First obtain legal RAW fixtures across the candidate vendors and run a real RAW16 decode → float V3 match → TIFF16 read-back with profile/metadata checks; wire the existing Core path to a product entry only after that result.
2. Set one canonical byte/float parity contract for V3 preview/export, then integrate V4 behind an explicit version switch with provenance. Do not silently replace V3 output.
3. Move V4 per-pixel work to bounded tiles only after memory measurements on 8/12/16GB devices. Instrument actual allocations, OOM/device-loss recovery, stage timings and output parity; then consider an automatic 16GB quality tier.
4. Run same-source, real-photo 24/33/45/61MP CPU/GPU benchmarks and inspect output quality before a V4 production claim.

### COLOR MATCH COMPLETION %

**12 / 28 = 42.9%**, using strict binary credit. The denominator is the 28 explicitly enumerated functional/acceptance items below for the intended Color Match + GPU + professional RAW/TIFF chain. A `PARTIAL` item earns **0**, even when its underlying Core API exists. This is a scope-accounting metric, not a visual-quality score or release-readiness claim.

| # | Functional item | Credit | Evidence / missing gate |
|---:|---|---:|---|
| 1 | Production V3 weighted reference target | 1 | `ReferenceLookMatcher`, V3 tests |
| 2 | Production V3 tone/zone/color transform | 1 | `ReferenceLookTransform`, V3 tests |
| 3 | Production V3 protection/strength controls | 1 | matcher and WPF tests |
| 4 | Production multi-reference look model | 1 | `ReferenceLook.Normalize`, workspace flow |
| 5 | Production CPU preview | 1 | preview service and WPF tests |
| 6 | Production matched JPEG export | 1 | workspace export and WPF tests |
| 7 | Full-resolution V3 preview/export fixture parity | 1 | exact-pixel WPF test, limited to its fixture |
| 8 | In-process reference-analysis reuse/invalidation | 1 | service cache and WPF test |
| 9 | Isolated V4 CPU representative OT | 1 | engine and Core tests |
| 10 | Isolated V4 hardware DX12 pairwise kernel | 1 | shader and passing GPU parity tests |
| 11 | Isolated V4 CPU fallback | 1 | engine and injected-failure test |
| 12 | Core true RGB48 TIFF16 write/read | 1 | writer/read-back and atomic tests; no product-chain claim |
| 13 | Production V4 selection and provenance | 0 | no product caller |
| 14 | Production V4 multi-reference integration | 0 | no `ReferenceLook` V4 adapter |
| 15 | Production GPU match preview/export | 0 | production path is V3 CPU |
| 16 | GPU Sinkhorn and mapped-color reduction | 0 | CPU loops |
| 17 | GPU per-pixel transform/residual/mask | 0 | CPU loops |
| 18 | Real tile/chunk processing | 0 | planner only |
| 19 | Runtime VRAM budget enforcement/OOM recovery | 0 | descriptor and narrow fallback only |
| 20 | Automatic 16GB quality mode | 0 | no runtime `ForQuality` call |
| 21 | V4 GPU preview/export parity | 0 | no product V4 preview/export |
| 22 | Professional 16-bit RAW decode on legal camera fixtures | 0 | decoder branch exists; fixtures absent |
| 23 | Verified RAW vendor/camera coverage | 0 | empty fixture manifest |
| 24 | Product RAW16 → match → TIFF16 workflow | 0 | disconnected adapters |
| 25 | ICC color conversion through RAW/match/TIFF | 0 | profile payload only; no conversion chain |
| 26 | Camera EXIF/orientation preservation through TIFF | 0 | metadata dropped/ignored in relevant adapters |
| 27 | Real-photo 24/33/45/61MP performance acceptance | 0 | only synthetic 12/24/45/60MP history |
| 28 | High-precision product preview/export parity | 0 | Core no-op tests only |

**Total: 12 credited of 28.** Items 9–12 are explicitly Core/isolated capabilities; the percentage must not be read as 42.9% of the user-facing professional workflow being delivered.
