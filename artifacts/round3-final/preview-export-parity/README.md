# Preview / Export parity evidence

SourceHead: `978b31d30bc9af3aaf22ac7e3f812451767299bf`

The production V3 preview and export paths use the shared CPU processing services. The WPF parity test compares the rendered preview pixels with the frozen target export pixels. Proxy resizing and output encoding are allowed to change resolution and container bytes; they must not change the processing contract.

## Contract

- Engine version, operation order, match/protection parameters, tone mapping, ICC assumptions, working color space and gamma semantics are shared.
- Resolution, resampling and preview cache encoding may differ.
- GPU remains deferred.
- V4 is explicit experimental foundation and is not claimed as the production default.
- No legal RAW corpus was present in this checkout; real RAW parity is `NOT_RUN / CORPUS_NOT_AVAILABLE`.

See `PARITY_MATRIX.json` and `PARITY_SUMMARY.md` for the run-level results.
