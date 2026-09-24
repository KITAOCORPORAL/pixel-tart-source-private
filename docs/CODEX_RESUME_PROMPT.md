Read these files before changing anything:

1. `docs/PIXEL_TART_HOME_DEV_HANDOFF.md`
2. `docs/COLOR_STUDIO_PHASE1_CLOSURE.md`
3. `docs/COLOR_STUDIO_CURRENT_IMPLEMENTATION_MAP.md`
4. the latest 20 commits on the current branch

Then inspect the current branch, HEAD, working tree, .NET SDK, and restore state. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-home-dev.ps1
```

Continue **Color Studio Phase 1 Final UX Closure** from the current source. Do not re-plan the whole product, do not start from old RC12, do not redo Photography, do not enter 3D Color Space, and do not install WSL, Ubuntu, Docker, or a VM. Preserve the canonical shared Simple/Professional state, per-target isolation, existing renderer, scheme migration, cancellation/revision/last-wins semantics, and the Pixel Tart palette. Treat the current Color Studio status as BLOCKED until its documented UX and visual gates are actually closed; only then report `READY FOR COLOR STUDIO PHASE 1 UX REVIEW`.
