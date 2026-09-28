# LibRaw modernization candidate comparison

Status at `97c6e1c384f97a85623724cfbb819dc692fc45f5`: **CANDIDATE NOT AVAILABLE; production unchanged**.

## Current production graph

| Layer | Current value |
|---|---|
| Managed package | `Sdcb.LibRaw 0.21.1.7` |
| Native runtime package | `Sdcb.LibRaw.runtime.win64 0.21.1` |
| Native DLL | `raw_r.dll`, `win-x64`, copied from the runtime package for the WPF release output |
| Runtime binding | `Sdcb.LibRaw` P/Invoke/interop through `RawContext`; `LibRawDecoder` owns context lifetime per decode |
| Target runtime | `net10.0` Core and `net10.0-windows`, `win-x64` WPF release |
| Observed native version | `0.21.1-Release` |
| Package license metadata | `(LGPL-2.1-only OR CDDL-1.0)` |

The product calls `OpenFile`, `Unpack`, `DcrawProcess`, and `MakeDcrawMemoryImage`; it requires
genuine 16-bit RGB output and rejects RGB24 fallback for the professional path.

## Candidate search and decision

The configured NuGet feeds expose no newer `Sdcb.LibRaw` managed package or Windows x64 runtime
package than the values above. Upstream LibRaw has a newer source release, but no pinned Windows
x64 binary was available in this checkout. This machine also has no native compiler/SDK toolchain
that could produce a reviewable candidate DLL. A source-version number alone is not a deployable
candidate, and replacing `raw_r.dll` without a matching managed binding would make the product
deployment unverifiable.

Therefore the isolated probe records `candidate.status = NOT_AVAILABLE`; it does not load a
replacement DLL and does not change the production package. A candidate may be supplied later as
a separately built process via `PIXEL_TART_LIBRAW_CANDIDATE_EXE`, with its own pinned native DLL,
managed binding, RID, license record, and five-camera matrix.

## Required promotion gate

Promotion remains blocked until one candidate process reports Sony A7 IV improvement, no new
X-T5/GFX100S/Canon R6 decode or orientation regression, acceptable licensing/deployment, and the
20-run matrix plus visual QA. The existing X-T5 non-determinism is a separate blocker even if a
future candidate fixes Sony.
