# Publishing progress and cancellation validation

## Contract

For every source/recipe item the publishing service reports these bounded stages:

| Stage | Item fraction |
|---|---:|
| Preparation | 5% |
| Rendering | 20% |
| Verification | 65% |
| Completed item | 100% of the item |

The task engine still aggregates item completion across all source/recipe pairs. Cancellation is passed through the task engine, service, renderer, verification, hashing, and file writes. Temporary `.publishing` files are removed in the service finally block.

## Evidence

- `PublishingSourceSafetyTests.ReportsPreparationRenderAndVerificationProgressBeforeCompletion` verifies ordered stage labels, stage fractions, and the final 100% report.
- `PublishingSourceSafetyTests.MultiRecipeCancellationAccountsForCurrentAndEveryPendingItem` verifies that a cancelled multi-recipe job accounts for every pending output and leaves no temporary publishing file.
- Release x64 solution build: PASS, 0 warnings, 0 errors.

## Preview latest-wins behavior

`PublishingExportViewModel` cancels the previous preview CTS when settings, selection, or recipe state changes. The renderer receives that token and the view model checks it before decoding and publishing a `BitmapImage`. Obsolete previews are discarded; the latest request is retried after the active render exits.

This is a correctness contract, not a claim of measured UI latency. Manual visual verification remains required for high-DPI and long-running native WPF sessions.
