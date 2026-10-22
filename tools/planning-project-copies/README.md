# Planning project-owned copies — scoped validation

Base 6cdb3f7bab745dc28df9797f278c85967d130a29; product 3710317f8c2681141cb7d0e959e78ab9f9bb6037. No merge/main/force push. Existing dirty owned-resource worktree is not used.

Build final full shell with WholeAppDiagnosticBuild=true and PhotographerTrialBuild=false, then bind published DLL bytes into Core/WPF hosts. run_final.py selects actual adapter-discovered affected planning/storage/DOCX fixtures only, not full application suites; new user-choice/move sample is included.

External reference/attachment ownership is additive metadata on existing identities. No source file is modified; stored original path is historical, managed relative path is resolved only against current PlanningProjectStore.StorageRoot. Keep the entire project directory including planning/project-copies; this is NOT a general project migration/portable-project user entry.

In existing add flow: select files → Import project copy? Yes imports, No offers external-only, second No cancels. Per-file max64MiB; empty/nonregular/reparse/alternate-stream/device paths rejected. Temporary .part is never a valid receipt. File copy/checksum must complete before one atomic planning JSON replacement; cancellation/ordinary I/O/save failure rollback under project gate. Abrupt process/power loss can leave unreferenced .part or orphan finished files; never promoted to a valid receipt automatically (cleanup UI/transaction crash journal are not implemented).

Repeated same source keeps original ID. Same names at different paths do not collide. Changed-source repeated import is refused, not silent replacement; use distinct explicit source/reference. Existing external links remain non-owning. Missing/corrupt owned copies are disclosed and never use the old external original as fallback.

Safe imported reference rendering currently shares existing PNG-only, one-frame,64MiB,40MP,16384-axis rules. Other formats remain hash-verified copy files accessible through source/reveal; no unsafe auto thumbnail and DOCX lists non-rendered status. No arbitrary file conversion, pixel algorithm or DOCX adapter contract changes.

Run records retain initial failures separately; no old results counted as current. Real mouse/WPS/physical DPI/installer/general portability remain separately NOT_RUN/BLOCKED.
