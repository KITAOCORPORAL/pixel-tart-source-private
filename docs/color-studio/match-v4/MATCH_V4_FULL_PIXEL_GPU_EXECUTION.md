# Match V4 full-pixel GPU execution

Implemented in `Dx12ColorMatchComputeBackend`:

- float32 RGB input and output;
- shared transform fields: strength, keep-luminance, luminance/chroma limits, and protection settings;
- sRGB transfer and OKLab conversion in the shader;
- pointwise tiled upload, dispatch, readback, and output assembly;
- cancellation checks before each tile and before publication;
- bounded tile shrink retry on `OutOfMemoryException`.

The shader constants mirror the current Core Oklab/sRGB contract. No byte or RGB24 conversion occurs in the GPU path.

Not yet proven: CPU/GPU numerical parity on a real DX12 adapter, 102MP GFX100S execution, device-lost injection, and WPF/TIFF16 product routing.
