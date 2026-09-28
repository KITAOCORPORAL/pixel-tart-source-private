# RAW → Match v3 → TIFF16 product pipeline audit

Baseline: `c7c729667c760812817a3b9c9f8bbcad50e65097` (2026-09-28). This is a call-site audit before integration, not a camera-support certification.

## Actual product paths at baseline

1. `MainViewModel` constructs `ReferenceColorWorkspaceViewModel`; `ChooseTargetCommand` in that view-model offers JPEG/PNG/TIFF/BMP only. `LoadThumbnailAsync` and `ActivateTargetAsync` load `BitmapImage`; activation calls `TetherReferenceModeViewModel.SetSourceAsync(BitmapSource)`.
2. The editor's `RenderAsync` calls `ColorStudioBitmapRenderer.Render` for the stack or `ReferenceLookPreviewService.RenderWithResultAsync` for the legacy look. `ColorStudioBitmapRenderer` first calls `HistogramService.EnsureBgra32`, copies channels to `VisualPixelBuffer` RGB24, then calls the byte overload of `ColorStudioRenderPipeline.Render`. `ReferenceLookMatcher` is Match v3. The interactive source is a 1600-edge WPF proxy.
3. `ReferenceColorWorkspaceViewModel.ExportAsync` freezes per-target look, film and stack, then `TetherReferenceModeViewModel.ProcessForExportAsync` reloads `BitmapImage`, runs the byte/WPF renderer, and encodes JPEG. There is no TIFF16 choice in this path. Writing 16-bit TIFF from this RGB24 result would merely promote 8-bit samples.
4. Separately, `RawToJpegViewModel` starts `RawToJpegTaskCoordinator`/`RawToJpegSafeConversionService` via `ApplicationCompositionRoot`. Its default `RawDecodeMode.FastPreview` invokes `LibRawDecoder` at 8 bits and `WpfJpegEncoder`; this conversion tool is not the Color Studio import/export route. `LibRawDecoder` also has an explicit `ProfessionalDecode` branch requesting 16-bit sRGB and returning RGB48 samples. A candidate extension is not a verified camera model.
5. `HighBitDepthImageBuffer.FromRaw`, the float overload of `ColorStudioRenderPipeline.Render` using `ReferenceLookMatcher.BuildTransform(HighBitDepthImageBuffer, ...)`, `AtomicTiffWriter.WriteRgb48Async`, and `TiffReadBack.Read` form a Core-only route used by the real-corpus runner, not a product command. The runner compares RGB24 adapter and TIFF16 read-back; it does not exercise WPF.

## Precision and ownership at baseline

| Boundary | Representation | Constraint |
|---|---|---|
| LibRaw professional decode | RGB48, 16-bit/channel, RGB order | `OutputColor = SRGB`; decoded output is display-referred, not linear sensor RGB. |
| Existing high-precision Core | float32 RGB normalized from `ushort/65535` | `HighBitDepthImageBuffer` labels sRGB, but `FromRaw` takes the RGB48 constructor and loses orientation/metadata. |
| Existing Color Studio WPF | BGRA32 → RGB24 | Premultiplication/alpha and 8-bit quantization prohibit a genuine high-precision export. |
| Float Match v3 | float32 RGB, OKLab transform | Analysis may use an RGB24 *proxy*, but final pixels should remain float. |
| Existing TIFF16 writer | float32 → RGB48, uncompressed TIFF | Atomic write/read-back exists. ICC payload is optional; no color conversion or complete camera EXIF propagation. |

Orientation needs one authority: decode with `AutoRotate:false`, retain source EXIF orientation with the high-precision buffer, and tag TIFF rather than rotating pixels a second time. RGB is three channels, no alpha; the assumed working/output transfer is sRGB nonlinear with D65 OKLab in the matcher. Unknown/missing ICC must not be labeled embedded. Input RAW files stay read-only. This integration must not route export through RGB24 or `TiffExport.WriteRgb24(... Sixteen)`.

## Acceptance boundary

The company checkout has no third-party RAW corpus. Synthetic RGB48/fake-decoder product tests can prove wiring, precision, preview/export math and atomic failure behavior, but cannot close Canon EOS R6/Fuji X-T5/GFX100S parity or vendor compatibility. The home corpus remains a separate rerun gate.
