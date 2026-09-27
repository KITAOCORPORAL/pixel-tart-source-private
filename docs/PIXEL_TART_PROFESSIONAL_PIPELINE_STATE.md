# Pixel Tart professional pipeline state

Current branch: `integration/pixel-tart-developer-preview`.

Photography Context now has an explicit core reducer for active asset, selected assets, rating,
preset preview/commit and batch target scope. The existing Asset Library remains backward
compatible and exposes the batch target summary.

RAW professional decode requests LibRaw RGB48, but Color Studio currently consumes RGB24; the
end-to-end high-precision imaging pipeline is therefore **PARTIAL**. TIFF16 writing, Adobe XMP
subset import and GPU pairwise OT remain real bounded foundations, not complete product claims.

Twenty One research is partial and runtime-unverified. Only the mechanism-level ideas of explicit
selection scope, compact metadata and cancellable long tasks are adapted; no visual identity or
unverified product behavior is copied.
