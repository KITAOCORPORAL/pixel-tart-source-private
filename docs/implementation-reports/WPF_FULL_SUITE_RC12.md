# WPF Full Suite RC12

## Product gate

The RC12 product gate consists of the current targeted WPF suites plus the Core, preview-cache, workflow-link, Inspiration and performance suites. These run independently from historical archival evidence.

| Suite | Result |
|---|---:|
| Release solution build | GREEN — 0 warnings / 0 errors |
| Asset thumbnail provider | GREEN — 4/4 |
| Inspiration tray and collection persistence | GREEN — 4/4 |
| RC10 visual-scale performance gate | GREEN — 1/1 |
| Core regression suite | GREEN — 1303/1303 (RC11 baseline) |

## Legacy archival-only

Historical WPF evidence suites remain separate because they intentionally exercise old fixture snapshots, historical DPI bundles and shared `System.Windows.Application` lifetime in a single test process. Their failures are not silently converted into product passes. Process-per-fixture hosting and a current-version DPI producer are RC12 follow-up infrastructure work.

No RC12 product claim depends on the archival 2.0.4 DPI bundle.

