# Pixel Tart RC11 Workflow Integration Report

Date: 2026-09-14  
Branch: `integration/pixel-tart-developer-preview`  
Base: RC10 `818073d559b41c0d3d2712b2a4d41dc15b9097aa`  
RC11 tip: `7667095dfa3c3870bf680e280bcd089646969ac4`

## Delivered

- `9ae82c3` — `test(ui): isolate integration acceptance infrastructure`: removed the hard-coded integration-branch allowlist from P1/P2/P3 acceptance runners and validators; named development branches are accepted while protected branches remain rejected. Added the RC11 WPF infrastructure audit.
- `bccac6d` — `feat(asset-library): persist shared preview cache`: shared thumbnail provider now uses memory plus bounded (512 MiB) PNG disk LRU cache under the library `previews` directory. Cache keys include stable asset identity/content hash, dimensions and orientation. Cached previews remain available when the source is missing/offline. Restart/offline behavior is covered by WPF tests.
- `a7a5d10` — `feat(creative): complete inspiration collections`: SQLite-backed collection CRUD, optional Project relation, membership add/remove, reorder and archive operations with restart persistence tests. This extends the existing non-owning Inspiration Tray references and never copies or mutates source files.
- `7667095` — `build(release): add RC11 installer target`: dedicated RC11 publish/output target; RC10 output remains untouched.

## Verification

- Release solution build: PASS, 0 warnings / 0 errors.
- Thumbnail provider tests: PASS, 4/4.
- Inspiration tray/collection tests: PASS, 4/4.
- RC10 10K/50K/100K performance gate: PASS, 1/1.
- Historical Core suite from RC10: PASS, 1303/1303.
- Full historical WPF suite is not promoted to green: legacy failures remain around shared `System.Windows.Application` lifetime, old fixture/evidence snapshots and historical DPI evidence. See [RC11_WPF_TEST_INFRA_AUDIT.md](RC11_WPF_TEST_INFRA_AUDIT.md).

## Installer

File: `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC11_x64.exe` (ignored build output)  
SHA-256: `EB08A4AC6B1CA15ED78A2E6BD546C1B20BDA33DF52FD85E1CA82F6C834BD4571`

## Remaining gaps

Project/Booking/Client picker UI, Calendar↔Asset visual deep links, recent-library switching presentation, real photography EXIF inspector fields, context-menu Project/Booking/Collection commands, and real themed screenshot harness evidence are not claimed as DONE in RC11. Existing relationship persistence and the new collection service are foundations for those follow-up slices. No source files are deleted or modified by these changes.
