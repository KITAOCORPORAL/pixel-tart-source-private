# Installed Acceptance Portable ZIP Delivery

2026-09-20 — PORTABLE PREFLIGHT PASS; installed UI workflow still AWAITING LOCAL ACCEPTANCE RUN.

## Corrected delivery

The previous response linked a BAT launcher alone, which could not bring its sibling runner/runtime to the user's download directory. This was a delivery defect. It is not evidence of a Pixel Tart startup failure. This pass delivers one complete ZIP and fails visibly when files are missing.

- File: `PixelTart-Installed-Acceptance-Kit-8cb95e6.zip`.
- Path: `artifacts/PixelTart-Installed-Acceptance-Kit-8cb95e6.zip`.
- Size: 213,377,221 bytes / 203.49 MiB.
- SHA256: `4DE0EF7998163F3C8CBD0B81E1B3C1FC356E7E1EB98F6B4AD1F6BB76B5DEBCE5`.
- 605 file entries: 604 manifest-listed payload files plus KIT_MANIFEST.json. No nested artifacts/worktree/output wrapper.
- Root directly contains 运行安装版验收.bat, Launch-Acceptance.ps1, PixelTart.InstalledAcceptance.exe/.dll/.runtimeconfig.json/.deps.json, planning-full.plan.json, upgrade-full.plan.json, README_验收说明.md, KIT_MANIFEST.json, THIRD_PARTY_NOTICES.md, all self-contained root runtime DLLs and native helpers.
- Subfolders: installer/, poppler/ and runtime localization folders. Both current and old Setup are bundled and use relative paths.
- Full root inventory: `artifacts/portable-zip-root-inventory.txt` (generated from the actual delivered archive).

## Portable test actually executed

Copied the ZIP and extracted it into `C:\Users\Administrator\Downloads\PixelTartAcceptancePortableTest-20260920-final\解压 验收包`.

Executed the actual extracted BAT through CMD with `--preflight-only`, using Windows PowerShell 5.1 and the extracted self-contained EXE, not dotnet/SDK, bin, obj or product source. The test stops before installation, product startup, elevation or UI Automation.

| Gate | Result |
|---|---|
| ZIP extraction | PASS |
| BAT immediate Chinese banner/output | PASS in captured CMD output; Explorer double-click/physical window visibility not visually observed |
| Preflight | 6/6 PASS |
| Runner / --kit argument | PASS, direct EXE exit 0 with --preflight-only |
| Both installers found and hashed | PASS |
| Result directory and startup log | PASS |
| acceptance-result.zip | PASS; no upload |
| Missing runner | PASS: Chinese FAIL, exit 1, log/archive, pause prompt |
| Missing dependency | PASS: gate 2 FAIL, exit 1, no product execution |
| Missing installer | PASS: gate 5 FAIL, actual relative expected path, exit 1 |
| Missing full plan | PASS: gate 3 FAIL, exit 1 |
| Replaced/tampered full plan | PASS: hash rejects it before Runner live launch |
| Restored kit | PASS again, exit 0 |

Test evidence: portable-tests.json and seven named .txt transcripts beside the extracted directory. `artifacts/portable-tests.log` records the seven results. Failure fixtures were reversible moves of this new extraction only; original ZIP was not modified. Pause prompts were captured; stdin was redirected to NUL solely to finish the non-interactive smoke test. UAC cancellation and true desktop UI behavior were not live-tested; their handlers are implemented, not falsely marked runtime PASS.

Two compatibility defects found during actual smoke were fixed before final ZIP: PowerShell 5.1 JSON array enumeration and unavailable Get-FileHash module (now uses .NET SHA256 directly). CMD redirection after a numeric exit code was corrected with a separating space. Scripts are packaged with CMD CRLF and PowerShell UTF-8 BOM for Chinese text.

## Portability and privilege audit

- Both plan JSON files: `D:\`, `AI AGENT`, `worktrees`, repository name matches = 0. Repo source dependency = 0.
- Shipped launcher contains the forbidden-name search regex intentionally; it is a rejection rule, not a development directory dependency. No shipped script resolves an actual development path.
- KIT_MANIFEST records RelativePath/Size/SHA256; launcher validates all payload dependencies, plans and installers, and Runner repeats integrity validation before accepting --kit.
- Preflight runs asInvoker; no unconditional RunAs or UAC. Only confirmed live execution elevates because the frozen Setup declares PrivilegesRequired=admin, without override allowance.
- Inno Setup's official documentation states /CURRENTUSER requires an allowed commandline/dialog override: https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm . Merely choosing LocalAppData does not change this installer contract. Product installer remains unchanged.
- Runner starts PixelTart with UseShellExecute=false, inheriting its token; no cross-level UIA design introduced. Both RunAs exceptions and canceled authorization are logged and the BAT reaches pause.
- Output failures are shown in Chinese with detailed logs; unreadable/unwritable output directory uses an announced temporary log fallback where possible. An OS policy blocking CMD/PowerShell itself cannot be guaranteed to produce kit logs.

## Frozen product

Modified src/ or installer/: NO. PRODUCT_SOURCE_SHA `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`.

Installer SHA256 unchanged: `C2E5F26A1D00BFE2A466450C0BB9D015CDD35D6733E909FE8ADCB5BAEFA6247D`.

Runner build: 0 warnings / 0 errors. Both complete plans validate; 23 offline tests PASS. No new installed screenshots, actual exported PDF, UIA acceptance, visual approval or release readiness is claimed. Product functionality is unchanged. Deliver the ZIP, then STOP.
