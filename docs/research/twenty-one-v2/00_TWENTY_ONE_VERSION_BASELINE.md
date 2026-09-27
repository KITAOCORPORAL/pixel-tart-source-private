# Twenty One Product Research v2 — Version Baseline

Status: **PARTIAL / BLOCKED FOR RUNTIME OBSERVATION**

| Field | Result | Evidence / confidence |
|---|---|---|
| App name | TWENTY ONE WOMB | User-supplied DMG filename |
| Version | v0.13.33 | Filename only; About not readable |
| Build | UNVERIFIED | DMG could not be mounted |
| Update date | 2026-09-27 15:23:57 local file timestamp | Filesystem timestamp, not vendor release date |
| Platform | macOS package (`.dmg`) | Container format |
| Architecture | UNVERIFIED | No Info.plist/executable metadata |
| Size | 67,316,158 bytes | Filesystem observation |
| SHA-256 | `D9DDE93FDD44DC01E48E74AFFFCC5D41812004A339EDD25359D8772B0FB85139` | Get-FileHash |
| Mount/read | FAILED: “文件或目录损坏且无法读取。” | Windows Mount-DiskImage |

## Evidence boundary

The supplied DMG was checked read-only. It could not be mounted or launched in this Windows runtime, so About, changelog, pages, menus, shortcuts, workflows, and screenshots were not observable. Runtime claims in this package are explicitly `UNVERIFIED`, `INFERRED`, or `NOT CAPTURED`; no convention is promoted to fact.

There are no prior Twenty One research files in the repository. `docs/reports/WOMB_REFERENCE_TRANSLATION_REPORT.md` is a design-boundary note, not a feature audit.

No proprietary source, logo, icon, bitmap, protocol, executable content, or protected asset was copied.

## Reproduction

1. Locate `C:UsersAdmin/Downloads/TWENTY ONE WOMB v0.13.33.dmg`.
2. Compute SHA-256.
3. Attempt a read-only disk-image mount.
4. Mount failed before launch.

This document must be revisited with a readable macOS runtime or exported screen recording before a COMPLETE status is possible.

