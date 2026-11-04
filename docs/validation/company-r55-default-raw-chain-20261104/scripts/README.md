# Reuse-only closure scripts

- Verify-Reuse.ps1: read-only identity check; Repository must name the original checkout holding the existing built WPF test assembly. Use a new empty EvidenceRoot; does not rebuild or decode RAW. Package/pass4 paths intentionally pin this company evidence.
- Compare-Existing-Tiff.mjs: streams only the four existing uncompressed +0.5/reopened TIFF pixel payloads. Arguments are the pass4 directory and a **nonexistent** output directory. No image conversion/RAW decoding; 2 MiB buffers.
- Run-RawReview.historical.ps1 is the unmodified historical runner, **not executed in this closure**. It runs real RAW processing and is not a routine validation command. Do not run it to verify this report; future use requires the task's existing explicit authorization manifest, at most three approved files, and a fresh isolated root.

All original failures/results remain local. Review evidence, not a mathematical algorithm approval.
