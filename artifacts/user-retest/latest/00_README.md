# Pixel Tart Round 3 Final User Retest

Release EXE: `artifacts/releases/round3-final/KitaoPhotoSelector.exe`
SourceHead: `de4c91a67c9146b5272bf20e10360172c4bf6189`

Release x64 user visual approval checklist. Please open the Release EXE and mark each item yourself after checking the stated workflow. `VisualApproved=false` and `UserVerified=false` until you do so.

- Start at 150% Windows scaling. The other DPI states are optional.
- If something looks wrong, take a screenshot and add one sentence describing it. No technical log is required.

## Order (15–20 minutes)

1. Asset Library: import one JPG/PNG/TIFF, rate it, open the context menu, and check one edge submenu.
2. Color Studio: open a target and reference, inspect Navigator Fit/100%/zoom/pan, select two filmstrip targets, sync, then run a small export.
3. 3D: build the cloud, sample image → cloud and cloud → image, then press Esc to clear the preview overlay.
4. Free Canvas: import one landscape and one portrait image, use Fit and the zoom menu, save, close, and reopen.

The Release EXE is the only approval surface. Keep `VisualApproved=false` and `UserVerified=false` until you personally finish the retest.

Known automated limitations to retest: preview/export parity is proven on the documented deterministic fixture, not every source/profile; physical camera tethering is `WAITING_FOR_HARDWARE`; GPU remains `DEFERRED`.
