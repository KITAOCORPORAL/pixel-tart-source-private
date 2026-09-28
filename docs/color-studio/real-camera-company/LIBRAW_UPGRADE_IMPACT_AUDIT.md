# LibRaw upgrade impact audit

Current production packages:

* `Sdcb.LibRaw` **0.21.1.7** (`LibRaw 0.21.1-Release` managed/native binding)
* `Sdcb.LibRaw.runtime.win64` **0.21.1** (`raw_r.dll` x64; package states vcpkg precompiled native binaries)
* LGPL-2.1-only OR CDDL-1.0 license expression in the runtime package metadata.

## Compatibility surface to preserve

The decoder relies on `RawContext.OpenFile`, `ImageParams`, `ImageOtherParams.Timestamp`, `Unpack`, `DcrawProcess`, `OutputParams` (`HalfSize`, camera/auto WB, camera matrix, sRGB, OutputBps, interpolation, UserFlip) and `MakeDcrawMemoryImage`. Four existing real camera paths currently decode 16-bit RGB: Fuji RAF, Canon CR3; existing tests cover RAW capability, 8-bit rejection and RGB48/TIFF16 precision. Native x64 deployment is part of Release packaging. Any upgrade must rerun CR3, RAF, NEF/DNG/TIFF tests and legal fixture evidence; licensing, DLL packaging and source compatibility must be checked.

## Decision

**Do not upgrade blindly in this phase.** Current evidence proves this binary rejects the Sony fixture even though its supported-camera list contains ILCE-7M4. It does not yet prove a newer LibRaw fixes this exact ARW compression variant. A candidate upgrade may be evaluated in an external, side-by-side probe only, with SHA-pinned package/native DLL and the full five-camera matrix. Production package changes remain blocked pending that controlled experiment.
