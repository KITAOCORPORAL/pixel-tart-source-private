# Portrait highlight EV0 review

Use a fresh external artifact root. The frozen implementation is b29bff914eed894ac663c1c0a006e18d76672035 as delivered at618ba102. These scripts add research observations only, not a product node or UI.

1. Verify delivery Git bytes, including the known REPORT-only manifest drift. Run PortraitHighlightResearchTests and build PortraitHighlightProbe with the workspace .NET SDK.
2. Run fetch_references.py ARTIFACT_ROOT FROZEN_REFERENCES_JSON to retrieve the seven exact public source/license files using the frozen commits and SHA-256. Keep GPL files outside this repository. rebuild_refs_msvc.py ARTIFACT_ROOT ORIGINAL_BUILDER CL_EXE VSDEVCMD builds the same generated scalar C++ sources with the available compiler. It records actual flags and distinguishes compiler/EXE identity from frozen source identity. It does not claim full darktable/RT application parity.
3. replay_synthetic.py ROOT DOTNET_EXE PROBE_DLL FROZEN_CURVES verifies579 frozen rows, observer on/off output and the3075-sample EV0 ladder. Every child has a512 MiB bound and90-second watchdog.
4. inspect_inputs.py ROOT AUTHORIZED_RENDERED_IDENTITY_MANIFEST reads company default TIFF16 files by fixed hash and produces input-only selection previews. rgb16_io accepts only contiguous, uncompressed RGB16 classic TIFF, orientation1/8, and reads ROI data with memmap; it never opens RAW.
5. freeze_rois.py ROOT writes fixed384x384 photo regions, source parameters, shared midtone anchors and two explicitly synthetic supplements. Do not relabel the synthetic inputs as white-cloth or metal photographs.
6. compare_rois.py ROOT DOTNET_EXE PROBE_DLL --output-name NEW_NAME runs EV0/+1/+2 comparisons. It solves candidate EV to match a fixed input-mask meanY anchor, then validates that match in the actual CLI. Unavailable anchors are null and gray-labelled, not repeated same-EV results. RGB16 TIFF pixels, independent formula tolerance, orientation and ICC byte identity are checked.
7. plot_review.py ROOT emits the EV0 numeric curve and local review sheets. NumPy2.4.6/Pillow12.2.0/Matplotlib3.11.2/psutil7.2.2 were used. The local TIFF parser replaces the absent frozen tifffile dependency; no package was silently upgraded.

Photos, TIFF crops, f32/f64 buffers, GPL source and reference EXEs remain local. New review files use explicit identity/ROI/parameter records. Preserve the original references, manifests, memory STOP/ledger and pending RAW batch. See the report for actual observations, missing material categories and the pre-clip integration warning.
