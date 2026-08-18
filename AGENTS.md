# Pixel Tart source handoff

This is the private, source-only handoff repository for Pixel Tart (像素蛋挞).

## Branch discipline

- `main` is the stable baseline. Do not treat source delivery as a product release.
- Keep `feature/pixel-tart-product-redesign`, `feature/modular-harness-v1`, `feature/asset-library-v1`, and `feature/online-selection-v1` independent until their own acceptance and merge decisions are complete.
- `handoff/source-snapshot-20260818` is an auditable snapshot of seven test-only SQLite pool-isolation edits found uncommitted in a detached local worktree. Review it before merging.
- Do not force-push, rewrite delivered history, or merge feature branches merely to simplify the repository.

## Safety boundary

- Never commit credentials, `.env` files, signing keys, customer media, RAW/JPG originals, runtime databases, logs, crash dumps, browser/session data, or machine-specific paths.
- Keep generated output out of Git, including `bin/`, `obj/`, `.vs/`, `node_modules/`, `publish/`, `artifacts/`, `TestResults/`, installers, archives, PDBs, thumbnails, proxies, caches, and test databases.
- Example configuration must contain placeholders or empty values only. Do not replace templates with live credentials.
- Use synthetic fixtures in the system temporary directory for tests; never reuse customer data.

## Build and verification

- Required SDK: .NET 10 on Windows 10/11 x64 with Windows Desktop Runtime support.
- Restore and build the active branch with `dotnet restore RAWSelectionAssistant.sln` and `dotnet build RAWSelectionAssistant.sln -c Debug --no-restore`.
- Run the branch's test projects under `tests/`. WPF tests require `-p:Platform=x64` when invoked as an individual project.
- Asset Library performance acceptance tests also require restoring `tools/AssetLibraryV16Acceptance/PixelTart.AssetLibrary.V16.AcceptanceRunner.csproj`.
- DPI/evidence contract tests consume generated acceptance evidence under ignored `artifacts/` paths. Generate that evidence locally; it is intentionally not versioned.
- The WeChat mini-program uses `clients/wechat-mini-program/localdev.config.example.ts`; keep tokens empty and use local temporary overrides only.

See the public handoff repository's `reports/SOURCE_REPOSITORY_DELIVERY.md` for original-to-delivery commit mapping and the latest verified results.
