# Cross-platform imaging audit

Baseline: `b05bf3cfc00a166fb630e215c664d2d7c89440eb`.

## Scope

This audit covers the Match V4/RAW/TIFF Core path, not the WPF application shell.

| Finding | Classification | Action |
|---|---|---|
| `HighBitDepthImageBuffer`, `MatchTransformV4`, CPU color science, RAW contracts and TIFF writer | PLATFORM_INDEPENDENT_CORE | retained in `RAWSelectionAssistant.Core` |
| `System.Windows`, `BitmapSource`, file picker and WPF adapters | ACCEPTABLE_PLATFORM_LAYER | remain in `RAWSelectionAssistant`/WPF services |
| LibRaw native runtime | PLATFORM BACKEND DEPENDENCY | isolated behind `IRawDecoder`; no path is hard-coded in Core |
| ComputeSharp/DX12 | INVALID_CORE_COUPLING (before Phase 2) | removed from Core and moved to `PixelTart.MatchV4.Dx12` |
| Future Metal | FUTURE PLATFORM BOUNDARY | no fake implementation created |

The Core project no longer references ComputeSharp. The Windows-only DX12 project references
Core and exposes only platform-neutral backend contracts. The WPF project may compose that
backend later; Core itself does not return WPF or DirectX types.

## Remaining audit notes

`DeviceFingerprintService` uses Windows registry APIs for application diagnostics and is outside
the Match V4 compute path. `LibRawDecoder` remains a native Windows deployment concern, but the
product route consumes `IRawDecoder` and `FrozenRawMaster`, so Match V4 never opens a RAW file.
No `C:\`, `D:\`, or `N:\` path is embedded in the Match V4 Core implementation.
