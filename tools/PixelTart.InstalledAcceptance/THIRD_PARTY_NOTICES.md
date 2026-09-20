# Bundled local acceptance dependencies

This is a local test kit, not a product release or public distribution.

- Microsoft .NET Windows Desktop runtime: copied by `dotnet publish --self-contained`. Runtime LICENSE.txt / ThirdPartyNotices.txt remain in the publish output when supplied by the SDK.
- Poppler 26.07.0, Windows x64, package `poppler=26.07.0=h6618ce5_3`: copied from the existing Codex native dependency cache. `poppler/manifest.json` preserves its package identity. Bundled helper binaries and DLLs are used only to inspect the actual exported PDF. Upstream: https://poppler.freedesktop.org/ . The cache does not provide a complete license/source archive; do not treat this local kit as redistribution-cleared. Obtain the corresponding upstream/package licenses and source obligations before distributing it externally.
- Two existing private Pixel Tart Developer Preview installers, unmodified; source SHA and SHA256 are pinned in both plans.
