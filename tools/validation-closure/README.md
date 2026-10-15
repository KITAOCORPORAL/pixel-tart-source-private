# Validation closure research tools

Research only. Do not copy any reference executables, RAW/TIFF, caches or third-party source into Git/product/Release. These tools do not admit a pixel change under AGENTS/POLICY.

Use Windows PowerShell from this worktree. Host-specific dependencies are fixed in docs/evidence/validation-closure-20261015/01_FIXED_IDENTITIES.json and 02_REFERENCES.json. All original files must match hashes before use. Existing reference directories are intentionally reused read-only; only the new artifacts directory receives outputs. Run each mode once, preserve failed logs, and do not overwrite previous evidence.

ICC compile (Zig0.16.0 from original reference manifest):

    zig c++ -O2 -std=c++17 -DSKCMS_PORTABLE -DSKCMS_DISABLE_HSW -DSKCMS_DISABLE_SKX -I <fixed-skcms> tools/validation-closure/skcms_stages.cpp <fixed-skcms>/skcms.cc <fixed-skcms>/src/skcms_TransformBaseline.cc -o artifacts/validation-closure/skcms_stages.exe
    python tools/validation-closure/trace_icc.py

Original seven profiles are read from D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/results, not regenerated. New identity-M sample is a diagnostic representation, not a replacement.

RAW C API probe (Zig against fixed candidate headers/import lib, put exact matching libraw.dll beside isolated EXE):

    zig c++ -O2 -std=c++17 -I <LibRaw-0.22.2> tools/validation-closure/raw_stages.cpp <LibRaw-0.22.2>/lib/libraw.lib -o artifacts/validation-closure/raw_stages.exe
    raw_stages.exe <verified-original> <new-output.tif> 1

raw_stages_no_auto.cpp additionally fixes no_auto_bright=1. Raw stats code does not reverse RGB to sensor data. raw_image statistics include row pitch. Sony color4 buffer is not treated as a single sensor plane.

RT5.11: isolated process env RT_SETTINGS=<new-root>/rt-config; RT_CACHE=<new-root>/rt-cache; OMP_NUM_THREADS=2. Default shared-LibRaw results are retained separately. For independent decoder evidence, set only isolated options [Raw Decoder] EnableLibRaw=false and [General] Verbose=true. Use:

    rawtherapee-cli.exe -q -O <new-output.tif> -p tools/validation-closure/raw-reference.pp3 -t -b16 -c <verified-original>

Exported PP3 stores effective processing defaults; options pins internal decoder. Sony rejection is expected on this internal backend. Never mask it with the shared-LibRaw run. No -Y, no sidecar next to original. Details in 02_INDEPENDENCE_ADDENDUM.

Python final-output analysis requires local-only numpy2.5.4/tifffile2026.9.20 in artifacts/validation-closure/python-deps. analyze_raw.py reads all research TIFFs there; compare_raw.py compares initial default-LibRaw vs configured shared-backend RT (explicitly not independent); compare_independent_raw.py compares Canon no-auto vs RT internal without cropping, still configuration-not-equivalent. Fuji independent geometry differs and is not cropped to fake equality.

Structure observation:

    dotnet build tools/validation-closure/SpatialAllocationProbe -c Release
    dotnet tools/validation-closure/SpatialAllocationProbe/bin/Release/net10.0/SpatialAllocationProbe.dll Structure <full|tile|full-cancel|cancel> <new-json> <Canon-original> B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401

No forced GC/working-set trim. Timings include runtime allocation sampling; allocation tick aggregation includes decode/analysis and is approximate. GC.GetTotalAllocatedBytes delta encloses render only. Existing full/tile paths remain production-identical.

Tests: build tools/VerifiedTestRunner in this worktree with workspace .NET10.0.302; run tools/planning-r7/verified_runner.py --assembly <sealed-published/core/RAWSelectionAssistant.Tests.dll> --output <new-root> --single-process. Set current-process PIXEL_TART_BATCH12_RELEASE_CORE=<sealed testhost Core.dll>. Strict exit remains1 with NotExecuted; never reinterpret shell status as all tests passed. Native UI/WPF/DPI/WPS NOT_RUN on active shared desktop.
