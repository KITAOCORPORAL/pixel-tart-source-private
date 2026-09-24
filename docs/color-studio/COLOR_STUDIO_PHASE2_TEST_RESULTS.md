# Color Studio Phase 2 Test Results

Source checkpoints: `c95a3bc` (P2.1), `4a7dccc` (camera), `822093f` (linking).

| Suite | Result |
|---|---:|
| Core Color Studio / OKLab / Film / proxy filtered run | **26/26 PASS** |
| `ColorSpaceProxyTests` | **7/7 PASS** |
| Large synthetic proxy cases | **3/3 PASS** (6000×4000, 7680×5760, 9500×6316) |
| Release x64 build | **PASS, 0 warnings / 0 errors** |
| WPF production 3D visual suite | **NOT RUN / NOT IMPLEMENTED** |
| Physical DPI | **NOT TESTED** |

| Full Core solution suite | **1409 PASS / 7 FAIL / 1 SKIP**; failures are pre-existing evidence-hash and legacy theme-literal checks outside Phase 2 files |
| Color Studio WPF suite | **41/41 PASS** |

The filtered Core run includes existing Phase 1 color/film regressions and the new deterministic proxy, camera, cache, cancellation, marker, cluster, and large-buffer tests. No screenshot or native 3D gesture result is inferred from these tests.
