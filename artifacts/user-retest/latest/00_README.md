# Pixel Tart Runtime UX Correction — User Retest

Release EXE: `artifacts/releases/runtime-ux-correction/publish/win-x64/KitaoPhotoSelector.exe`
SourceHead: `d497e9b207e98c5a907485dfd85555e33e157a4a`
Manifest: `artifacts/releases/runtime-ux-correction/release-manifest.json` (EXE, actual application DLL and all publish-file hashes).

This build contains the supplied runtime corrections, not a claim that the original handoff is complete. The required DOCX, latest recording and instructions after section 25 are still unavailable. The reopened matrix is `docs/evidence/human-review/POST_VIDEO_RUNTIME_REVIEW_MATRIX.md`.

Release x64 user visual approval checklist. Please open the Release EXE and mark each item yourself after checking the stated workflow. `VisualApproved=false` and `UserVerified=false` until you do so.

- Start at 150% Windows scaling. The other DPI states are optional.
- If something looks wrong, take a screenshot and add one sentence describing it. No technical log is required.

## Order (15–20 minutes)

1. Asset Library: import one JPG/PNG/TIFF, rate it, open the context menu, and check one edge submenu.
2. Color Studio: open a target and reference, inspect Navigator Fit/100%/zoom/pan, select two filmstrip targets, sync, then run a small export.
3. 3D: build the cloud, sample image → cloud and cloud → image, then press Esc to clear the preview overlay.
4. Free Canvas: import one landscape and one portrait image, use Fit and the zoom menu, save, close, and reopen.

The Release EXE is the only approval surface. Keep `VisualApproved=false` and `UserVerified=false` until you personally finish the retest.

Known automated limitations: the old full parity closure was invalidated; current synthetic equal-resolution JPEG comparison does not establish TIFF16/high precision, ICC or proxy/full-resolution corpus parity. Existing DPI artifact tests refer to older captures, not this build. Physical camera tethering is `WAITING_FOR_HARDWARE`; GPU remains `DEFERRED`.


