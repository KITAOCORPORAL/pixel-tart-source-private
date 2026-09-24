# Pixel Tart Windows development environment

This repository is a Windows WPF/.NET 10 application. The normal development path is a native Windows checkout; WSL, Ubuntu, Docker, and a VM are **not required**.

## Required

- Windows 10 19045 or newer (Windows Desktop target: `net10.0-windows10.0.19041.0`).
- .NET SDK 10.0.302 baseline or a compatible later 10.0 feature band. `global.json` sets `rollForward: latestFeature` without a machine-specific path. SDK 10.0.401 has been verified.
- Windows Desktop runtime/SDK support for WPF.
- Git for Windows.
- PowerShell 5.1 or PowerShell 7+.
- A normal NuGet connection to `https://api.nuget.org/v3/index.json`; restore is repository/project based.
- x64 CPU for the Release x64 product build.

The solution's production runtime identifier is `win-x64`. Core libraries target `net10.0`; WPF projects target `net10.0-windows10.0.19041.0`. The main application and LibRaw runtime package are x64.

## Optional / release-only

- Git LFS is installed on the current machine, but this repository currently has no LFS-tracked files (`git lfs ls-files` is empty). Do not run `git lfs pull` unless that changes.
- A camera/tethering device and vendor driver are optional hardware integrations, not required for ordinary build or Color Studio development.
- Physical DPI validation is a release hardware gate. In-process logical DPI tests (100/125/150/200%) are development checks and do not certify a physical display.
- Installer generation is not part of the current handoff.

## Dependencies

NuGet packages are restored from project files, including MSTest 4.0.2, Microsoft.Data.Sqlite 10.0.10, SQLitePCLRaw.bundle_e_sqlite3 2.1.12, MetadataExtractor 2.9.0, System.Security.Cryptography.ProtectedData 10.0.0, and Sdcb.LibRaw 0.21.1.7 plus its Windows x64 runtime package. No local DLL or company-only package source is required by the current solution.

Run `powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-home.ps1` after cloning, then `powershell -ExecutionPolicy Bypass -File .\scripts\verify-home-dev.ps1` for the bounded gate.

## Release gates versus ordinary development

Ordinary development: restore, Release x64 build, focused Color Studio and Photography regressions, and logical-DPI checks.

Release gate: full product/release evidence, physical DPI hardware validation, and any explicitly scoped installer/package acceptance. The current Color Studio Phase 1 closure remains BLOCKED by native pointer interaction/final UX review only; the Photography development gate is PASS.

No WSL, Ubuntu, Docker, VM, administrator install, or reboot is needed for this workflow.
