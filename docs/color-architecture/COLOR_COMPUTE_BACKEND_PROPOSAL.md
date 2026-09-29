# Color compute backend proposal

Keep color science and transform parameters backend-neutral. CPU is the reference executor; DX12 is an optional executor. Future backends must consume the same immutable transform and preserve cancellation, generation identity and fallback semantics.
