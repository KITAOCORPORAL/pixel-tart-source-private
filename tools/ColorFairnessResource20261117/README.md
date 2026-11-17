# Fixed highlight fairness / zero-RAW resource review

This folder contains research tools only. No source under src/tests is edited. The fixed candidate is never executed or modified: its same-EV and midtone-matched float32 output is read from1bfdfbe9's frozen local artifact root. No Amount/V2/default/recipe/DLL admission is implemented.

## Exact-control comparison

Build ExactControlProbe.csproj with the existing.NET10SDK, Release Platform=x64. It references the repository Core and invokes actual RenderedSrgbExposure and ColorStudioToolProcessor; no handwritten approximation is used in accepted results. Parent run_fairness.py takes NEW_EXTERNAL_ROOT, HOST_EXE, EXISTING_DOTNET_ROOT. The root must already contain the reviewed FROZEN_PLAN.json, ORIGINAL_ROI_RESULTS.json and verified96x96 every4th-pixel samples of all fixed ROIs and their original separate anchors. Copy those from the saved research root only after SHA checks; do not choose different anchors or parameters.

The parent freezes grids/brackets/iteration limits, logs statuses and produces same/mid/joint full384x384 outputs. It bounds the control process to512MiB/600s,22,000 requests. It does not open RAW. render_fair_panels.py reads accepted control f32 plus frozen V1 f32, computes fullROI high-mask metrics, keeps failed comparisons gray, and makes local-only PNG panels. probe_rgb48_tiff.py streams uncompressedRGB48 TIFF for payload/metadata identity without image/nativeRAW libraries. plot_resources.py uses already-installed ReportLab charts; NumPy/Pillow are only data/image utilities. Do not install or upgrade the system environment.

The rejected initial approximate comparator, failed setup logs and original plan remain external under invalid-preflight-comparator. Its72rows are not evidence. Two exact-control runs occurred; the second adds a diagnostic metric, not different pixel math. Current source/results/binary hashes are recorded separately.

fair_viewer.html is a local material view, not a product UI or new algorithm. data.json contains the180 finite display conditions. Default100% scales CSS width by browserDPR; this is nominal sampling only, not display calibration. Matched meanY does not imply equal white point, color or perception.

## Zero-RAW resource host

ResourceHost references an existing frozen product App/Core (ProductAppDirectory MSBuild property; default points to the recorded e818 package). Build with WPF.NET10 Platform=x64. prepare_stage.py copies managed runtime assets to a fresh separate directory and excludes raw_r.dll/libraw.dll. No product/发行DLL is replaced. Then run:

python ResourceHost/guard_zero_raw.py DOTNET_EXE STAGED_HOST_DLL NEW_RUN_ROOT FROZEN_RGB48 [reopen-proxy-export PRIOR_SAVED_SESSION]

Use an explicit isolatedruntime; the guard sets it before launch, preserves run directories, applies2GiB/600s and50ms samples, and terminates only its own child on limit. Never run the full-size host unguarded. It injects a payload-only IRawDecoder replacement, reading known0806D680 RGB48; the originalRAF is never opened. The synthetic marker path is not the original source identity. No native candidate/worker or heavyledger is used; OS WPF/WIC internals are not claimed to be absent.

Default mode loads realVM/photo page plus hidden legacy view, requests detail/zoom, saves, and calls production export. The separate mode opens a previously saved file in a newprocess, usesproxy only, exports, disposes and attempts a same-process reopen. Preserve all STOPs. The measured runs both hit resource guards at different stages; they are NOT product passes. No forcedGC. The later independent/proxy run is not a retry of the failed detail export. Source code changed only to expose the unresolved path, not product pixel/memory behavior; first stopped binary was retained separately.

Snapshots report exact managed object identities so shared masters/bitmaps are not double-counted. GC.GetTotalMemory(false) and processPrivateBytes are not a root/allocation census; disappearance of references does not promise immediate decommit. No final native module census was written because guardstopped both hosts; do not invent one. See RESOURCE_FINDINGS for what current source and snapshots prove and what original820b trigger remains unknown.

Results, images, rawpixelpayload, resourcesamples, copied managed product DLLs and stopped host binaries remain external. Git includes tools, finite plan, numerical metrics, evidence hashes and focused reports. Replaying a resource failure is not algorithm/product approval.
