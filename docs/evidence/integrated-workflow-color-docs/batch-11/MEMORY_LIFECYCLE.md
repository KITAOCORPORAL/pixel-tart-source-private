# Memory lifecycle and bounded processing

Old baseline: width*height*72+64MiB admitted full source, orientation, cloned current/next node, RGB8 display and old film scratch; Fuji7752x5184 was rejected. New pointwise path uses64-row bands, halo0, preserving global coordinates for grain/vignette/paper texture and SpatialVersion1/2. Float input/output math and rounding are unchanged, exact old full-render comparisons cover odd band edges and node groups.

Declared peak for bounded path:36bytes/source pixel (source12 + possible upright12 + output12), width*min(64,height)*96bytes for conservative band node/temp storage, plus32MiB. Canon plan793453376bytes; Fuji1527891968bytes, below unchanged2GiB. This is a live-buffer conservative plan, not measured OS allocation or a hard cap. No arbitrary threshold increase.

Lifecycle:

1. Full native decode: codec/native + ushort decode + float master; decode reservation independent.
2. Source retained while orientation output created (only ifEXIF2–8).
3. Full output allocated once; each64-row band copies input and executes old stack with bounded intermediate arrays; earlier band's arrays become unreachable. Zero-neighbor old film retains global x/y/full dimensions.
4. Master result supersedes source cache; HQ/tiles/export pin the specific cached generation. Replacement/release while pinned moves its bytes to retired accounting until last lease ends.
5. Encoding owns row/codec reservation; cancellation/finally releases lease/work. GC/native allocator reclamation may lag; retired accounting is not OS memory tracking.

ReferenceMatch, Details, Develop with structure/detail, Bloom/Halation remain full-image dependencies and use conservative72bytes/pixel+64MiB. They are NOT called streamable; Fuji may still safely reject these nodes. Plan lists blocking node names. Identity or no-reference paths do not introduce new color processing versions because pixels are bit-identical.

Store audit: active pins cannot be eviction victims; retired generations remain counted after replacement; classification limit includes retired entries. Old eviction callback checks master object identity before clearing target, preventing old-generation callback from clearing new master. Cache master usage in activation/HQ/native blocks/export and uncommitted presentation master has a lease/work reservation. Noncached strong references still require caller-owned work reservation. Ledger is not blanket coverage of every historical UI cache.

Actual workingSet/private/peak/GC0–2/managed/heap measured separately in REAL_WORKSPACE_PRESSURE.json. Explicit GC is measurement only, never automatic product policy. A drop in managed survivors with OS pages retained is not alone leak proof; ledger zero is not memory convergence. Peak includes prior real operations in the same process and is not isolated Canon-only peak.
