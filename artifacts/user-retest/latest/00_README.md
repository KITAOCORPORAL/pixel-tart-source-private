# Pixel Tart Round 3 User Retest

Release x64 user visual approval checklist. Please open the Release EXE and mark each item yourself after checking the stated workflow. `VisualApproved=false` and `UserVerified=false` until you do so.

- Test at 150% Windows scaling first, then 100%, 125%, and 200% where practical.
- Check Asset Library at 1180x720, 1600x920, and 1920x1080.
- Record any failure with route, scale, window size, and steps.

## Order (15–20 minutes)

1. Asset Library: import one JPG/PNG/TIFF, rate it, open the context menu, and check one edge submenu.
2. Color Studio: open a target and reference, inspect Navigator Fit/100%/zoom/pan, select two filmstrip targets, sync, then run a small export.
3. 3D: build the cloud, sample image → cloud and cloud → image, then press Esc to clear the preview overlay.
4. Free Canvas: import one landscape and one portrait image, use Fit and the zoom menu, save, close, and reopen.

Automated gates are evidence for code and tests. Please do not mark a row complete until you have viewed the Release EXE. Keep `VisualApproved=false` and `UserVerified=false` in the evidence until the human review is complete.

Known automated limitations to retest: preview/export parity is proven on the documented deterministic fixture, not every source/profile; physical camera tethering is `WAITING_FOR_HARDWARE`; GPU remains `DEFERRED`.
