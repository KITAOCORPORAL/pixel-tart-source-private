# Studio UI v1 — loading timing audit

Product source: `993854ce0fd2437bd1fc05dc36c6bac316305a48`.
Evidence: `artifacts/pixel-tart-studio-ui-v1/review-closure/loading-timing.jsonl`.
Status: **PARTIAL**. Synthetic development timings, not a performance benchmark.

| Operation | Elapsed ms | Busy-state observed ms | Delayed indicator ms | UI heartbeats |
| --- | ---: | ---: | ---: | ---: |
| Asset import, 12 images | 170.3 | 69.9 | none (short task) | 3 |
| RAW match, empty-index recovery | 248.5 | 27.6 | not instrumented | 3 |
| Reference target decode + current preview | 1377.5 | 1.3 | 369.1 | 40 |
| Reference render | 1172.4 | 30.5 | 350.3 | 34 |
| Publishing preview, 3 images | 200.0 | 0.9 | none (short task) | 6 |
| Publishing export, 3 images | 209.0 | 10.3 | none (short task) | 4 |

Busy-state sampling is distinct from visible feedback. A null busy sample can occur
when a brief busy interval falls between timer ticks. The measured reference decode
entry uses the real LoadTargetAsync workflow and includes its subsequent preview;
it is not an isolated codec benchmark. RAW match exercises actual matching and
not-found persistence, not successful RAW decoding or large-directory throughput.

## Implemented

- Shared attached presentation state: 300 ms delay, 1500 ms long-running stage;
  completion/unload reset, no changes to task cancellation or engine semantics.
- Reference/publishing/tether reference delayed progress renders a 20 DIP spinner
  followed by an indeterminate bar. Asset loading mask is delayed.
- Deterministic boundary tests plus dispatcher-based start/reset/long-running test.
- Real operation timing and dispatcher heartbeat records for all six named operations.

## Remaining scope

- Asset mask still uses its existing bar after 300 ms rather than the small spinner.
- RAW matching and every legacy tool have not migrated to shared staged feedback.
- Error/cancel/retry timing for every operation and successful populated RAW matching
  remain unmeasured. Long reference render is captured through controlled suspension.
- Source-bound tests verify current contracts, not a universal latency guarantee.

Do not mark global loading consistency PASS from these six development samples.
