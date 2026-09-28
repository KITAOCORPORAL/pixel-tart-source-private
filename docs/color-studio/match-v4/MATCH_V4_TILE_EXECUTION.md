# Match V4 tile execution

`MatchV4TileExecutor` now executes deterministic non-overlapping tiles through a backend-neutral
transform callback and checks cancellation at tile boundaries. For the current V4 semantics the
pixel transform is pointwise, so overlap is not required; a future neighborhood stage must set a
nonzero overlap and add seam validation.

This is not yet a DX12 tile dispatch. Whole-image GPU resource residency, 24/45/60/102MP real
measurements, boundary error and GFX100S stress acceptance remain open.
