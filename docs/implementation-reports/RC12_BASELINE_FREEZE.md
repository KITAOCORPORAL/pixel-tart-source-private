# RC12 Stable Baseline Freeze

状态：`REAL_MACHINE_ACCEPTANCE = PENDING`  
本文件只冻结当前候选基线，不代表 production-ready，也不是正式 release tag。

## Git 基线

- Branch: `integration/pixel-tart-developer-preview`
- HEAD: `9ac4c18a69ae9f9436d2cbe97612b639d7cd7709`
- Remote: `source-private/integration/pixel-tart-developer-preview`
- Working tree: clean
- Local HEAD = remote HEAD: yes
- 不 merge `main`，不 force push

## Installer

- File: `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`
- Product: Pixel Tart 2.3.0-RC12
- Source: clean RC12 source, win-x64 self-contained publish
- Size: 51,209,065 bytes
- SHA-256: `4FDD855257A4F854CF0C347B40556E3A8BF832ACAC6FA810D14BF34D2FB51D97`
- Authenticode: `NotSigned`
- Installer is a local release artifact and is not committed to Git

## Data and product gates

- Database schema: v7
- Release build: 0 warnings / 0 errors
- Focused RC12 regression: PASS
- P2 sealed acceptance and immutable existing-run validation: PASS
- WPF process isolation: 83 fixtures, 1162/1162 PASS, 0 failed, 0 skipped
- Project / Booking / Client: DONE
- Calendar ↔ Asset: DONE
- Inspiration Collection: DONE
- Recent Libraries: DONE
- EXIF: DONE
- Context Menu: DONE
- Preview Cache: PASS

## Visual and performance gates

- Product Visual Harness: 50/50 PASS
- Current DPI: 100% / 125% / 150% / 200% PASS
- Asset Library resolution captures: PASS
- 10K / 50K / 100K visual performance: PASS
- Current visual audit: no P0 clipping, overflow or white-surface failure

## Release policy

- `REAL_MACHINE_ACCEPTANCE = PENDING` 必须保持到用户明确反馈 RC12 实机通过。
- 在实机通过前禁止 merge `main`、创建正式 release tag 或声明 production-ready。
- `RC13_NOT_AUTHORIZED`：不新增 schema，不开发 Moodboard、Planning Center、Browser Extension、AI、MCP，不大改 Workbench。
