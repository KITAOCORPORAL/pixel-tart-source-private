# Pixel Tart Online Selection LocalDev Preview

This is an isolated development preview, not a production cloud service.

Run `powershell -ExecutionPolicy Bypass -File tools/PixelTart_OnlineSelection_LocalDev_Preview.ps1` from the repository root. The launcher builds and starts the server on `127.0.0.1`, waits for `/health/ready`, starts the dedicated Online Selection Preview window, and stops only the server process it created after that preview exits. Runtime data stays under the temporary `PixelTart_OnlineSelection_LocalDev_Preview` root; it never opens the product database. `-NoWait` is intended only for scripted smoke tests; use the process IDs written to `launcher-processes.json` to clean up that exact launch.

The server stores an independent SQLite database and re-encoded JPEG Thumb (480), Preview (1600), and Proxy (2560) objects. RAW input is refused. Desktop access tokens are DPAPI-protected for the current Windows user. Image URLs do not contain the mutation token; they require a short-lived read-only media session.

The mini program has exactly five pages. Its committed `localdev.config.example.ts` is disabled and contains no credential. Set a temporary `pixel-tart-localdev-config/v1` object in WeChat DevTools local storage for a LocalDev session; the token stays outside Git. A real phone cannot reach the computer through `127.0.0.1`; this preview therefore makes no real-device or deployment claim. Formal networking requires HTTPS and a permitted domain.

WeChat identity boundary: `wx.login` returns only a temporary code. `code2Session`, `AppSecret`, and `session_key` belong only on a server. The mini program must never store a long-lived secret. This round uses Mock/LocalDev only and is not online.

Use `tools/open-wechat-dev-project.ps1` to open the project if WeChat DevTools is already installed and signed in. The script does not install software, sign in, or add credentials.

Known Preview limits: cross-device field-level conflict merging, server-side enforcement of all selection rules, orphan-object cleanup after a disk delete failure, and the seven-screen sanitized UI review remain deferred. Do not use this build as a production service or claim those items as verified.
