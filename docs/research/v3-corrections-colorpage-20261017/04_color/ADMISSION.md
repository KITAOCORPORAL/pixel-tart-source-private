# PhotoWideV1 formula admission and actual execution

One available archive Resolve-Color-Reference.zip read/CRC+entry hashes, not assumed four independently received files. Attachments are data. Existing other worktrees/branches checked, no matching implementation to import. V3 remains display-referred sRGB; this math library does NOT upgrade old clipped RAW or connect arbitrary wide-gamut defaults.

Actual fixed colour-science0.4.7 functions (BSD-3), OCIO2.4.2 CPU CDL (BSD-3) installed local ignored target. Module identities and input/output in INDEPENDENT_REFERENCE.json, actual binary output via PhotoColorFormulaProbe. DI/ACEScct/sRGB/BT709/BT2020/PQ against external implementation, DWG normative matrix vs independent primary-derived matrix; CDL against OCIO CPU with OPTIMIZATION_NONE. No self-roundtrip-only admission. PQ explicitly cd/m²0..10000, rejects negative/out-of-range. ACEScct identity AP1/D60; no hidden chromatic adaptation. sRGB/BT709 shared primaries but separate transfer identities, BT2020 independent primaries/OETF.

First numeric failures preserved: chosen analytic BT2020 constants differed from colour12bit normative rounded1.0993/.0181; explicit contract corrected, not tolerance. OCIO default optimized approximation yielded~1e-5 vs gate2e-6; reference unoptimized CPU now~1e-7, no threshold relaxation. CDL1.2 Clamp SOP/output versus OCIO no-clamp negative slope-offset branch explicitly passes negative unchanged before saturation. Identity saturation1 exact endpoint avoids needless roundoff. First unit failure retained and endpoint corrected in code.

Limits: matrix6e-9 (published8decimal coefficients), scalar encoding2e-10, PQ1e-12, OCIO floatCDL2e-6. Threshold +/-epsilon, negative/overrange, NaN/Inf and explicit mode tests. ACEScct decoder intentionally extends float positive range rather than silent65504 clipping; not half-precision. Output transform/display ICC/file encoder are separate contracts, existing monitor ICC disagreements remain OPEN. This is formula support, not full Resolve colour engine/palette/DRT support.

## 续接补证（2026-10-17）

网络恢复后，实际 `git ls-remote` 固定 colour-science v0.4.7 解引用提交 a3bfe349685f528100672e5c8ca2dfeeef64a273；OpenColorIO v2.4.2 提交 6918fad3f5d22ac3ef2397c754bf4268c2b58dd0。REFERENCE_PINS.json 包含准确入口与许可证源文件哈希。均为 BSD-3-Clause，只用于外部数值对照，不随产品分发。既有公式不改像素。探针改为读取实际 Core 矩阵字段，删除手写副本；追加逆 DWG、sRGB、BT.2020、AP1/D60 以及三种反传递函数和 PQ 解码的独立对照。检查参考版本、非有限值和失败退出码。仍不构成宽色域生产管线、ICC、输出变换或相机标定准入。
